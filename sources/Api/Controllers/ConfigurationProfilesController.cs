using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.GameServerImplementations;
using X39.UnitedTacticalForces.Hosting.Contracts;
using DatabaseClaim = X39.UnitedTacticalForces.Database.Authority.Claim;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Authorize]
[Route("configuration-profiles")]
public sealed class ConfigurationProfilesController(
    DatabaseContext dbContext,
    IGameServerConfigurationService configurationService,
    IHostingAuditWriter auditWriter) : ControllerBase
{
    [HttpGet("definitions")]
    public IActionResult GetDefinitions()
        => Ok(new ConfigurationProfileDefinitionCatalogDto
        {
            CanCreate = User.HasClaim(Claims.Creation.ConfigurationProfile, string.Empty)
                        || IsAdministrator(User),
            Games = GameConfigurationImplementations.All
                .OrderBy(q => q.GameIdentifier)
                .Select(q => new GameConfigurationDefinitionSetDto
                {
                    GameIdentifier = q.GameIdentifier,
                    Definitions = q.Definitions.OrderBy(d => d.Order).ToArray(),
                })
                .ToArray(),
        });

    [HttpGet("options")]
    public async Task<IActionResult> GetOptionsAsync(
        [FromQuery] string gameIdentifier,
        CancellationToken cancellationToken)
    {
        var normalized = gameIdentifier.Trim().ToLowerInvariant();
        if (!GameConfigurationImplementations.TryGet(normalized, out _))
            return BadRequest(new { error = "The selected game is unsupported." });
        return Ok(await dbContext.ConfigurationProfiles.AsNoTracking()
            .Where(q => q.GameIdentifier == normalized && !q.IsArchived && q.Revisions.Any())
            .OrderBy(q => q.Title)
            .Select(q => new ConfigurationProfileSummaryDto
            {
                PrimaryKey = q.PrimaryKey,
                Title = q.Title,
                GameIdentifier = q.GameIdentifier,
                LatestRevisionNumber = q.Revisions.Max(r => r.RevisionNumber),
                TimeStampUpdated = q.TimeStampUpdated,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false));
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] string? gameIdentifier,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ConfigurationProfiles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(gameIdentifier))
            query = query.Where(q => q.GameIdentifier == gameIdentifier.Trim().ToLowerInvariant());
        if (!IsAdministrator(User))
        {
            var ids = GetPermittedProfileIds(User, Claims.ResourceBased.ConfigurationProfile.View);
            query = query.Where(q => ids.Contains(q.PrimaryKey));
        }
        return Ok(await query
            .OrderBy(q => q.Title)
            .Select(q => new ConfigurationProfileSummaryDto
            {
                PrimaryKey = q.PrimaryKey,
                Title = q.Title,
                GameIdentifier = q.GameIdentifier,
                IsArchived = q.IsArchived,
                LatestRevisionNumber = q.Revisions.Max(r => (int?) r.RevisionNumber) ?? 0,
                TimeStampUpdated = q.TimeStampUpdated,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false));
    }

    [HttpGet("{profileId:guid}")]
    public async Task<IActionResult> GetAsync(
        [FromRoute] Guid profileId,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.View))
            return Forbid();
        var profile = await dbContext.ConfigurationProfiles
            .AsNoTracking()
            .Include(q => q.Revisions)
            .ThenInclude(q => q.Values)
            .Include(q => q.Revisions)
            .ThenInclude(q => q.CreatedBy)
            .SingleOrDefaultAsync(q => q.PrimaryKey == profileId, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
            return NotFound();
        var sensitiveKeys = GameConfigurationImplementations.Get(profile.GameIdentifier).Definitions
            .Where(q => q.Sensitive)
            .Select(q => q.Key)
            .ToHashSet(StringComparer.Ordinal);
        var projectedRevisions = new List<ConfigurationProfileRevisionDto>();
        IReadOnlyDictionary<string, JsonElement> previousValues =
            new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var revision in profile.Revisions.OrderBy(q => q.RevisionNumber))
        {
            var currentValues = RevisionValues(revision);
            projectedRevisions.Add(ProjectRevision(revision, previousValues, currentValues, sensitiveKeys));
            previousValues = currentValues;
        }
        return Ok(new
        {
            Profile = new ConfigurationProfileSummaryDto
            {
                PrimaryKey = profile.PrimaryKey,
                Title = profile.Title,
                GameIdentifier = profile.GameIdentifier,
                IsArchived = profile.IsArchived,
                LatestRevisionNumber = profile.Revisions.Max(q => (int?) q.RevisionNumber) ?? 0,
                TimeStampUpdated = profile.TimeStampUpdated,
            },
            CanModify = HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.Modify),
            CanManageAccess = HasPermission(
                User,
                profileId,
                Claims.ResourceBased.ConfigurationProfile.ManageAccess),
            CanArchive = HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.Archive),
            Definitions = GameConfigurationImplementations.Get(profile.GameIdentifier).Definitions,
            Revisions = projectedRevisions.OrderByDescending(q => q.RevisionNumber),
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ConfigurationProfileCreationPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!User.HasClaim(Claims.Creation.ConfigurationProfile, string.Empty)
            && !IsAdministrator(User))
            return Forbid();
        var title = payload.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "A configuration-profile title is required." });
        var gameIdentifier = payload.GameIdentifier.Trim().ToLowerInvariant();
        if (!GameConfigurationImplementations.TryGet(gameIdentifier, out _))
            return BadRequest(new { error = "The selected game is unsupported." });
        if (!TryValidateValues(gameIdentifier, payload.Values, out var values, out var error))
            return BadRequest(new { error });

        var actor = await dbContext.Users.Include(q => q.Claims)
            .SingleOrDefaultAsync(q => q.PrimaryKey == actorId && !q.IsDeleted && !q.IsBanned, cancellationToken)
            .ConfigureAwait(false);
        if (actor is null)
            return Unauthorized();
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var profile = new ConfigurationProfile
        {
            PrimaryKey = Guid.NewGuid(),
            Title = title,
            GameIdentifier = gameIdentifier,
            CreatedByFk = actorId,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var revision = CreateRevision(profile, 1, actorId, now, values!);
        profile.Revisions.Add(revision);
        actor.Claims ??= [];
        actor.Claims.Add(new DatabaseClaim
        {
            Identifier = Claims.ResourceBased.ConfigurationProfile.All,
            Value = profile.PrimaryKey.ToString(),
            Category = "Configuration profiles",
            Title = Claims.ResourceBased.ConfigurationProfile.All,
            Description = "Configuration-profile-scoped permission.",
        });
        dbContext.ConfigurationProfiles.Add(profile);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ConfigurationProfileCreated,
            "Configuration profile created.",
            actorId,
            details: $"ProfileId={profile.PrimaryKey}; Game={gameIdentifier}; Revision=1.");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetAsync), new { profileId = profile.PrimaryKey }, new ConfigurationProfileSummaryDto
        {
            PrimaryKey = profile.PrimaryKey,
            Title = profile.Title,
            GameIdentifier = profile.GameIdentifier,
            LatestRevisionNumber = 1,
            TimeStampUpdated = now,
        });
    }

    [HttpPost("{profileId:guid}/revisions")]
    public async Task<IActionResult> CreateRevisionAsync(
        [FromRoute] Guid profileId,
        [FromBody] ConfigurationProfileRevisionCreationPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.Modify))
            return Forbid();
        var profile = await dbContext.ConfigurationProfiles
            .Include(q => q.Revisions)
            .ThenInclude(q => q.Values)
            .SingleOrDefaultAsync(q => q.PrimaryKey == profileId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
            return NotFound();
        var latest = profile.Revisions.MaxBy(q => q.RevisionNumber);
        var latestNumber = latest?.RevisionNumber ?? 0;
        if (payload.BaseRevisionNumber is { } baseRevisionNumber && baseRevisionNumber != latestNumber)
            return Conflict(new ProblemDetails
            {
                Title = "The configuration profile has changed.",
                Detail = "Reload the latest revision before publishing your changes.",
                Status = StatusCodes.Status409Conflict,
            });
        if (!TryValidateRevisionChanges(
                profile.GameIdentifier,
                latest,
                payload,
                out var values,
                out var error))
            return BadRequest(new ProblemDetails
            {
                Title = "The configuration revision is invalid.",
                Detail = error,
                Status = StatusCodes.Status400BadRequest,
            });
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var revision = CreateRevision(
            profile,
            latestNumber + 1,
            actorId,
            now,
            values!);
        dbContext.ConfigurationProfileRevisions.Add(revision);
        profile.TimeStampUpdated = now;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ConfigurationProfileRevisionCreated,
            "Configuration profile revision created.",
            actorId,
            details: $"ProfileId={profileId}; Revision={revision.RevisionNumber}.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return RevisionConflict();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: not null,
            } postgresException
            && postgresException.ConstraintName.Contains(
                "ConfigurationProfileRevisions",
                StringComparison.Ordinal))
        {
            return RevisionConflict();
        }
        var sensitiveKeys = GameConfigurationImplementations.Get(profile.GameIdentifier).Definitions
            .Where(q => q.Sensitive)
            .Select(q => q.Key)
            .ToHashSet(StringComparer.Ordinal);
        return Ok(ProjectRevision(
            revision,
            latest is null ? new Dictionary<string, JsonElement>() : RevisionValues(latest),
            values!,
            sensitiveKeys));
    }

    [HttpPost("{profileId:guid}/archive")]
    public async Task<IActionResult> ArchiveAsync(
        [FromRoute] Guid profileId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.Archive))
            return Forbid();
        var profile = await dbContext.ConfigurationProfiles
            .SingleOrDefaultAsync(q => q.PrimaryKey == profileId, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
            return NotFound();
        if (!profile.IsArchived)
        {
            var now = NodaSystemClock.Instance.GetCurrentInstant();
            profile.IsArchived = true;
            profile.TimeStampArchived = now;
            profile.TimeStampUpdated = now;
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.ConfigurationProfileArchived,
                "Configuration profile archived.",
                actorId,
                details: $"ProfileId={profileId}.");
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return NoContent();
    }

    [HttpGet("{profileId:guid}/access")]
    public async Task<IActionResult> GetAccessAsync(
        [FromRoute] Guid profileId,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.ManageAccess))
            return Forbid();
        if (!await dbContext.ConfigurationProfiles.AnyAsync(q => q.PrimaryKey == profileId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();
        var permissions = ProfilePermissions;
        var value = profileId.ToString();
        var users = await dbContext.Users.AsNoTracking()
            .Include(q => q.Claims!.Where(c => permissions.Contains(c.Identifier) && c.Value == value))
            .Where(q => !q.IsDeleted && q.Claims!.Any(c => permissions.Contains(c.Identifier) && c.Value == value))
            .OrderBy(q => q.Nickname)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var roles = await dbContext.Roles.AsNoTracking()
            .Include(q => q.Claims!.Where(c => permissions.Contains(c.Identifier) && c.Value == value))
            .Where(q => q.Claims!.Any(c => permissions.Contains(c.Identifier) && c.Value == value))
            .OrderBy(q => q.Title)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(new GameServerAccessDto
        {
            AllowedPermissions = permissions,
            Users = users.Select(q => new GameServerUserAccessGrantDto
            {
                UserId = q.PrimaryKey,
                Nickname = q.Nickname,
                Permissions = q.Claims!.Select(c => c.Identifier).OrderBy(c => c).ToArray(),
            }).ToArray(),
            Roles = roles.Select(q => new GameServerRoleAccessGrantDto
            {
                RoleId = q.PrimaryKey,
                Title = q.Title,
                Description = q.Description,
                Permissions = q.Claims!.Select(c => c.Identifier).OrderBy(c => c).ToArray(),
            }).ToArray(),
        });
    }

    /// <summary>Searches verified user candidates for profile-scoped access.</summary>
    /// <param name="profileId">The configuration profile identifier.</param>
    /// <param name="search">The optional nickname search term.</param>
    /// <param name="take">The maximum number of candidates to return.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The matching access candidates.</returns>
    [HttpGet("{profileId:guid}/access/users/options")]
    public async Task<IActionResult> GetUserAccessOptionsAsync(
        [FromRoute] Guid profileId,
        [FromQuery] string? search,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (!HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.ManageAccess))
            return Forbid();
        if (take is < 1 or > 50)
            return BadRequest(new ProblemDetails
            {
                Title = "The candidate limit is invalid.",
                Detail = "Take must be between 1 and 50.",
                Status = StatusCodes.Status400BadRequest,
            });
        var query = dbContext.Users.AsNoTracking().Where(q => !q.IsDeleted && !q.IsBanned && q.IsVerified);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(q => EF.Functions.ILike(q.Nickname, $"%{search.Trim()}%"));
        return Ok(await query.OrderBy(q => q.Nickname).Take(take)
            .Select(q => new ConfigurationProfileUserCandidateDto
            {
                UserId = q.PrimaryKey,
                Nickname = q.Nickname,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>Searches role candidates for profile-scoped access.</summary>
    /// <param name="profileId">The configuration profile identifier.</param>
    /// <param name="search">The optional role-title search term.</param>
    /// <param name="take">The maximum number of candidates to return.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The matching access candidates.</returns>
    [HttpGet("{profileId:guid}/access/roles/options")]
    public async Task<IActionResult> GetRoleAccessOptionsAsync(
        [FromRoute] Guid profileId,
        [FromQuery] string? search,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (!HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.ManageAccess))
            return Forbid();
        if (take is < 1 or > 50)
            return BadRequest(new ProblemDetails
            {
                Title = "The candidate limit is invalid.",
                Detail = "Take must be between 1 and 50.",
                Status = StatusCodes.Status400BadRequest,
            });
        var query = dbContext.Roles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(q => EF.Functions.ILike(q.Title, $"%{search.Trim()}%"));
        return Ok(await query.OrderBy(q => q.Title).Take(take)
            .Select(q => new ConfigurationProfileRoleCandidateDto
            {
                RoleId = q.PrimaryKey,
                Title = q.Title,
                Description = q.Description,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>Replaces all direct profile grants with one validated desired matrix.</summary>
    /// <param name="profileId">The configuration profile identifier.</param>
    /// <param name="payload">The complete desired direct-access matrix.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>No content when the matrix was saved.</returns>
    [HttpPost("{profileId:guid}/access/matrix")]
    public async Task<IActionResult> UpdateAccessMatrixAsync(
        [FromRoute] Guid profileId,
        [FromBody] ConfigurationProfileAccessMatrixPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.ManageAccess))
            return Forbid();
        if (!await dbContext.ConfigurationProfiles.AnyAsync(q => q.PrimaryKey == profileId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();
        if (payload.Users.Select(q => q.UserId).Distinct().Count() != payload.Users.Count
            || payload.Roles.Select(q => q.RoleId).Distinct().Count() != payload.Roles.Count)
            return InvalidAccessMatrix("Each user and role may appear only once.");
        if (payload.Users.Any(q => !TryNormalizePermissions(q.Permissions, out _))
            || payload.Roles.Any(q => !TryNormalizePermissions(q.Permissions, out _)))
            return InvalidAccessMatrix("The access matrix contains an unsupported permission.");

        var value = profileId.ToString();
        var desiredUserIds = payload.Users.Select(q => q.UserId).ToArray();
        var desiredRoleIds = payload.Roles.Select(q => q.RoleId).ToArray();
        var users = await dbContext.Users.Include(q => q.Claims)
            .Where(q => !q.IsDeleted && (desiredUserIds.Contains(q.PrimaryKey)
                                         || q.Claims!.Any(c => ProfilePermissions.Contains(c.Identifier)
                                                               && c.Value == value)))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var roles = await dbContext.Roles.Include(q => q.Claims)
            .Where(q => desiredRoleIds.Contains(q.PrimaryKey)
                        || q.Claims!.Any(c => ProfilePermissions.Contains(c.Identifier) && c.Value == value))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (desiredUserIds.Any(id => users.All(q => q.PrimaryKey != id))
            || desiredRoleIds.Any(id => roles.All(q => q.PrimaryKey != id)))
            return InvalidAccessMatrix("A selected user or role no longer exists.");

        var grantClaims = await dbContext.Claims
            .Where(q => ProfilePermissions.Contains(q.Identifier) && q.Value == value && q.ValueType == null)
            .ToDictionaryAsync(q => q.Identifier, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        foreach (var permission in payload.Users.SelectMany(q => q.Permissions)
                     .Concat(payload.Roles.SelectMany(q => q.Permissions)).Distinct(StringComparer.Ordinal))
        {
            if (grantClaims.ContainsKey(permission))
                continue;
            var claim = new DatabaseClaim
            {
                Identifier = permission,
                Value = value,
                Category = "Configuration profiles",
                Title = permission,
                Description = "Configuration-profile-scoped permission.",
            };
            dbContext.Claims.Add(claim);
            grantClaims.Add(permission, claim);
        }

        foreach (var user in users)
        {
            user.Claims ??= [];
            RemoveProfileGrants(user.Claims, value);
            var row = payload.Users.SingleOrDefault(q => q.UserId == user.PrimaryKey);
            if (row is not null && TryNormalizePermissions(row.Permissions, out var permissions))
                foreach (var permission in permissions)
                    user.Claims.Add(grantClaims[permission]);
        }
        foreach (var role in roles)
        {
            role.Claims ??= [];
            RemoveProfileGrants(role.Claims, value);
            var row = payload.Roles.SingleOrDefault(q => q.RoleId == role.PrimaryKey);
            if (row is not null && TryNormalizePermissions(row.Permissions, out var permissions))
                foreach (var permission in permissions)
                    role.Claims.Add(grantClaims[permission]);
        }

        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.AccessGranted,
            "Configuration-profile access matrix updated.",
            actorId,
            details: $"ProfileId={profileId}; Users={payload.Users.Count}; Roles={payload.Roles.Count}.");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{profileId:guid}/access/users/grant")]
    public Task<IActionResult> GrantUserAccessAsync(
        Guid profileId,
        [FromBody] GameServerAccessPayload payload,
        CancellationToken cancellationToken)
        => ChangeUserAccessAsync(profileId, payload, true, cancellationToken);

    [HttpPost("{profileId:guid}/access/users/revoke")]
    public Task<IActionResult> RevokeUserAccessAsync(
        Guid profileId,
        [FromBody] GameServerAccessPayload payload,
        CancellationToken cancellationToken)
        => ChangeUserAccessAsync(profileId, payload, false, cancellationToken);

    [HttpPost("{profileId:guid}/access/roles/grant")]
    public Task<IActionResult> GrantRoleAccessAsync(
        Guid profileId,
        [FromBody] GameServerRoleAccessPayload payload,
        CancellationToken cancellationToken)
        => ChangeRoleAccessAsync(profileId, payload, true, cancellationToken);

    [HttpPost("{profileId:guid}/access/roles/revoke")]
    public Task<IActionResult> RevokeRoleAccessAsync(
        Guid profileId,
        [FromBody] GameServerRoleAccessPayload payload,
        CancellationToken cancellationToken)
        => ChangeRoleAccessAsync(profileId, payload, false, cancellationToken);

    private async Task<IActionResult> ChangeUserAccessAsync(
        Guid profileId,
        GameServerAccessPayload payload,
        bool grant,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.ManageAccess))
            return Forbid();
        if (!ProfilePermissions.Contains(payload.Permission, StringComparer.Ordinal))
            return BadRequest(new { error = "The requested profile permission is not assignable." });
        if (!await dbContext.ConfigurationProfiles.AnyAsync(q => q.PrimaryKey == profileId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();
        var user = await dbContext.Users.Include(q => q.Claims)
            .SingleOrDefaultAsync(q => q.PrimaryKey == payload.UserId && !q.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
            return NotFound();
        user.Claims ??= [];
        await ChangeGrantAsync(user.Claims, profileId, payload.Permission, grant, cancellationToken)
            .ConfigureAwait(false);
        AddAccessAudit(profileId, actorId, grant, "User", payload.UserId.ToString(), payload.Permission);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private async Task<IActionResult> ChangeRoleAccessAsync(
        Guid profileId,
        GameServerRoleAccessPayload payload,
        bool grant,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.ManageAccess))
            return Forbid();
        if (!ProfilePermissions.Contains(payload.Permission, StringComparer.Ordinal))
            return BadRequest(new { error = "The requested profile permission is not assignable." });
        if (!await dbContext.ConfigurationProfiles.AnyAsync(q => q.PrimaryKey == profileId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();
        var role = await dbContext.Roles.Include(q => q.Claims)
            .SingleOrDefaultAsync(q => q.PrimaryKey == payload.RoleId, cancellationToken)
            .ConfigureAwait(false);
        if (role is null)
            return NotFound();
        role.Claims ??= [];
        await ChangeGrantAsync(role.Claims, profileId, payload.Permission, grant, cancellationToken)
            .ConfigureAwait(false);
        AddAccessAudit(profileId, actorId, grant, "Role", payload.RoleId.ToString(), payload.Permission);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private async Task ChangeGrantAsync(
        ICollection<DatabaseClaim> claims,
        Guid profileId,
        string permission,
        bool grant,
        CancellationToken cancellationToken)
    {
        var value = profileId.ToString();
        var existing = claims.SingleOrDefault(q => q.Identifier == permission
                                                   && q.Value == value
                                                   && q.ValueType == null
                                                   && !q.IsPrefix);
        if (!grant)
        {
            if (existing is not null)
                claims.Remove(existing);
            return;
        }
        if (existing is not null)
            return;
        var claim = await dbContext.Claims.SingleOrDefaultAsync(q => q.Identifier == permission
                                                                    && q.Value == value
                                                                    && q.ValueType == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (claim is null)
        {
            claim = new DatabaseClaim
            {
                Identifier = permission,
                Value = value,
                Category = "Configuration profiles",
                Title = permission,
                Description = "Configuration-profile-scoped permission.",
            };
        }
        else if (claim.IsPrefix)
        {
            throw new InvalidOperationException("The stored configuration-profile grant is not exact.");
        }
        claims.Add(claim);
    }

    private void AddAccessAudit(
        Guid profileId,
        Guid actorId,
        bool grant,
        string subjectKind,
        string subjectId,
        string permission)
        => auditWriter.Add(
            dbContext,
            grant ? EHostingAuditEventKind.AccessGranted : EHostingAuditEventKind.AccessRevoked,
            grant ? "Configuration-profile access granted." : "Configuration-profile access revoked.",
            actorId,
            details: $"ProfileId={profileId}; {subjectKind}={subjectId}; Permission={permission}.");

    private BadRequestObjectResult InvalidAccessMatrix(string detail)
        => BadRequest(new ProblemDetails
        {
            Title = "The access matrix is invalid.",
            Detail = detail,
            Status = StatusCodes.Status400BadRequest,
        });

    /// <summary>Creates the response used when a competing revision wins publication.</summary>
    /// <returns>A conflict response instructing the editor to reload.</returns>
    private ConflictObjectResult RevisionConflict()
        => Conflict(new ProblemDetails
        {
            Title = "The configuration profile has changed.",
            Detail = "Reload the latest revision before publishing your changes.",
            Status = StatusCodes.Status409Conflict,
        });

    private static bool TryNormalizePermissions(
        IReadOnlyList<string> requested,
        out IReadOnlyList<string> permissions)
    {
        permissions = [];
        if (requested.Any(q => !ProfilePermissions.Contains(q, StringComparer.Ordinal)))
            return false;
        permissions = requested.Contains(Claims.ResourceBased.ConfigurationProfile.All, StringComparer.Ordinal)
            ? [Claims.ResourceBased.ConfigurationProfile.All]
            : requested.Distinct(StringComparer.Ordinal).Order().ToArray();
        return true;
    }

    private static void RemoveProfileGrants(ICollection<DatabaseClaim> claims, string profileId)
    {
        foreach (var claim in claims.Where(q => ProfilePermissions.Contains(q.Identifier)
                                                 && q.Value == profileId
                                                 && q.ValueType == null
                                                 && !q.IsPrefix).ToArray())
            claims.Remove(claim);
    }

    private bool TryValidateValues(
        string gameIdentifier,
        IReadOnlyList<ConfigurationValuePayload> payload,
        out IReadOnlyDictionary<string, JsonElement>? values,
        out string? error)
    {
        values = null;
        error = null;
        var groups = payload.GroupBy(q => q.Key, StringComparer.Ordinal).ToArray();
        if (groups.Any(q => q.Count() != 1))
        {
            error = "Configuration keys must be unique.";
            return false;
        }
        try
        {
            values = groups.ToDictionary(
                q => q.Key,
                q => configurationService.ValidateAndClone(gameIdentifier, q.Key, q.Single().Value),
                StringComparer.Ordinal);
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private bool TryValidateRevisionChanges(
        string gameIdentifier,
        ConfigurationProfileRevision? latest,
        ConfigurationProfileRevisionCreationPayload payload,
        out IReadOnlyDictionary<string, JsonElement>? values,
        out string? error)
    {
        values = null;
        error = null;
        var resetKeys = payload.ResetKeys.ToHashSet(StringComparer.Ordinal);
        if (resetKeys.Count != payload.ResetKeys.Count)
        {
            error = "Configuration reset keys must be unique.";
            return false;
        }
        if (payload.Values.Any(q => resetKeys.Contains(q.Key)))
        {
            error = "A configuration key cannot be updated and reset in the same revision.";
            return false;
        }
        if (!TryValidateValues(gameIdentifier, payload.Values, out var updates, out error))
            return false;

        var implementation = GameConfigurationImplementations.Get(gameIdentifier);
        var definitions = implementation.Definitions.ToDictionary(q => q.Key, StringComparer.Ordinal);
        if (resetKeys.Any(q => !definitions.ContainsKey(q)))
        {
            error = "A configuration reset key is not supported by the selected game.";
            return false;
        }

        var merged = latest is null
            ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            : RevisionValues(latest).ToDictionary(q => q.Key, q => q.Value, StringComparer.Ordinal);
        foreach (var key in resetKeys)
            merged.Remove(key);
        foreach (var (key, value) in updates!)
            merged[key] = value;

        var missingRequired = implementation.Definitions
            .Where(q => q.Required && !merged.ContainsKey(q.Key))
            .Select(q => q.DisplayName)
            .ToArray();
        if (missingRequired.Length > 0)
        {
            error = $"Required configuration values are missing: {string.Join(", ", missingRequired)}.";
            return false;
        }

        var original = latest is null
            ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            : RevisionValues(latest);
        if (DictionariesEqual(original, merged))
        {
            error = "The revision does not change any configuration values.";
            return false;
        }
        values = merged;
        return true;
    }

    private static ConfigurationProfileRevision CreateRevision(
        ConfigurationProfile profile,
        int number,
        Guid actorId,
        Instant now,
        IReadOnlyDictionary<string, JsonElement> values)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            ConfigurationProfile = profile,
            ConfigurationProfileFk = profile.PrimaryKey,
            RevisionNumber = number,
            CreatedByFk = actorId,
            TimeStampCreated = now,
            Values = values.Select(q => new ConfigurationProfileValue
            {
                PrimaryKey = Guid.NewGuid(),
                DefinitionKey = q.Key,
                ValueJson = q.Value.GetRawText(),
            }).ToArray(),
        };

    private static ConfigurationProfileRevisionDto ProjectRevision(
        ConfigurationProfileRevision revision,
        IReadOnlyDictionary<string, JsonElement> previousValues,
        IReadOnlyDictionary<string, JsonElement> currentValues,
        IReadOnlySet<string> sensitiveKeys)
        => new()
        {
            PrimaryKey = revision.PrimaryKey,
            RevisionNumber = revision.RevisionNumber,
            TimeStampCreated = revision.TimeStampCreated,
            CreatedById = revision.CreatedByFk,
            CreatedByNickname = revision.CreatedBy?.Nickname ?? string.Empty,
            Values = currentValues.ToDictionary(
                q => q.Key,
                q => sensitiveKeys.Contains(q.Key) ? (JsonElement?) null : q.Value,
                StringComparer.Ordinal),
            SensitiveConfiguredKeys = currentValues.Keys.Where(sensitiveKeys.Contains).Order().ToArray(),
            Changes = previousValues.Keys.Union(currentValues.Keys, StringComparer.Ordinal)
                .Where(key => !previousValues.TryGetValue(key, out var previous)
                              || !currentValues.TryGetValue(key, out var current)
                              || !JsonElement.DeepEquals(previous, current))
                .Order()
                .Select(key => new ConfigurationProfileRevisionChangeDto
                {
                    Key = key,
                    Kind = !previousValues.ContainsKey(key)
                        ? EConfigurationProfileRevisionChangeKind.Added
                        : !currentValues.ContainsKey(key)
                            ? EConfigurationProfileRevisionChangeKind.Removed
                            : EConfigurationProfileRevisionChangeKind.Changed,
                })
                .ToArray(),
        };

    private static Dictionary<string, JsonElement> RevisionValues(ConfigurationProfileRevision revision)
        => revision.Values.ToDictionary(q => q.DefinitionKey, q => Parse(q.ValueJson), StringComparer.Ordinal);

    private static bool DictionariesEqual(
        IReadOnlyDictionary<string, JsonElement> left,
        IReadOnlyDictionary<string, JsonElement> right)
        => left.Count == right.Count
           && left.All(q => right.TryGetValue(q.Key, out var value) && JsonElement.DeepEquals(q.Value, value));

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static bool HasPermission(ClaimsPrincipal principal, Guid profileId, string permission)
        => IsAdministrator(principal)
           || principal.HasClaim(Claims.ResourceBased.ConfigurationProfile.All, profileId.ToString())
           || principal.HasClaim(permission, profileId.ToString());

    private static bool IsAdministrator(ClaimsPrincipal principal)
        => principal.HasClaim(Claims.Administrative.All, string.Empty)
           || principal.HasClaim(Claims.Administrative.Server, string.Empty)
           || principal.HasClaim(Claims.Administrative.Hosting, string.Empty);

    private static Guid[] GetPermittedProfileIds(ClaimsPrincipal principal, string permission)
        => principal.Claims
            .Where(q => q.Type == Claims.ResourceBased.ConfigurationProfile.All || q.Type == permission)
            .Select(q => Guid.TryParse(q.Value, out var id) ? id : Guid.Empty)
            .Where(q => q != Guid.Empty)
            .Distinct()
            .ToArray();

    private static readonly string[] ProfilePermissions =
    [
        Claims.ResourceBased.ConfigurationProfile.All,
        Claims.ResourceBased.ConfigurationProfile.View,
        Claims.ResourceBased.ConfigurationProfile.Modify,
        Claims.ResourceBased.ConfigurationProfile.ManageAccess,
        Claims.ResourceBased.ConfigurationProfile.Archive,
    ];
}
