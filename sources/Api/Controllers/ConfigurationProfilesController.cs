using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            .SingleOrDefaultAsync(q => q.PrimaryKey == profileId, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
            return NotFound();
        var includeSensitive = HasPermission(User, profileId, Claims.ResourceBased.ConfigurationProfile.Modify);
        var sensitiveKeys = GameConfigurationImplementations.Get(profile.GameIdentifier).Definitions
            .Where(q => q.Sensitive)
            .Select(q => q.Key)
            .ToHashSet(StringComparer.Ordinal);
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
            Revisions = profile.Revisions.OrderByDescending(q => q.RevisionNumber).Select(q => new
            {
                q.PrimaryKey,
                q.RevisionNumber,
                q.TimeStampCreated,
                Values = q.Values.ToDictionary(
                    v => v.DefinitionKey,
                    v => sensitiveKeys.Contains(v.DefinitionKey) && !includeSensitive
                        ? (JsonElement?) null
                        : Parse(v.ValueJson),
                    StringComparer.Ordinal),
                RedactedKeys = includeSensitive
                    ? Array.Empty<string>()
                    : q.Values.Where(v => sensitiveKeys.Contains(v.DefinitionKey))
                        .Select(v => v.DefinitionKey)
                        .ToArray(),
            }),
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
        var profile = await dbContext.ConfigurationProfiles.Include(q => q.Revisions)
            .SingleOrDefaultAsync(q => q.PrimaryKey == profileId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
            return NotFound();
        if (!TryValidateValues(profile.GameIdentifier, payload.Values, out var values, out var error))
            return BadRequest(new { error });
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var revision = CreateRevision(
            profile,
            (profile.Revisions.Max(q => (int?) q.RevisionNumber) ?? 0) + 1,
            actorId,
            now,
            values!);
        profile.Revisions.Add(revision);
        profile.TimeStampUpdated = now;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ConfigurationProfileRevisionCreated,
            "Configuration profile revision created.",
            actorId,
            details: $"ProfileId={profileId}; Revision={revision.RevisionNumber}.");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ProjectRevision(revision));
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

    private static ConfigurationProfileRevisionDto ProjectRevision(ConfigurationProfileRevision revision)
        => new()
        {
            PrimaryKey = revision.PrimaryKey,
            RevisionNumber = revision.RevisionNumber,
            TimeStampCreated = revision.TimeStampCreated,
            Values = revision.Values.ToDictionary(
                q => q.DefinitionKey,
                q => Parse(q.ValueJson),
                StringComparer.Ordinal),
        };

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
