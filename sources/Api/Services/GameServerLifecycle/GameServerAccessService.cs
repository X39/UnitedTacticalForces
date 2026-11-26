using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Authorization.Requirements;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Default <see cref="IGameServerAccessService" /> that stores per-server grants as exact claims and audits
/// every change.
/// </summary>
/// <param name="dbContext">The database context holding users, roles and claims.</param>
/// <param name="auditWriter">Writes the audit record of each grant change.</param>
[Scoped<GameServerAccessService, IGameServerAccessService>]
internal sealed class GameServerAccessService(
    DatabaseContext dbContext,
    IHostingAuditWriter auditWriter) : IGameServerAccessService
{
    private static readonly string[] PermissionAllowlist =
    [
        Claims.ResourceBased.Server.All,
        Claims.ResourceBased.Server.View,
        Claims.ResourceBased.Server.Operate,
        Claims.ResourceBased.Server.Modify,
        Claims.ResourceBased.Server.ManageAccess,
        Claims.ResourceBased.Server.DeleteData,
        Claims.ResourceBased.Server.ManageSecrets,
        Claims.ResourceBased.Server.RevealSecrets,
    ];

    private static readonly IReadOnlyCollection<string> PermissionAllowlistView
        = Array.AsReadOnly(PermissionAllowlist);

    /// <inheritdoc />
    public IReadOnlyCollection<string> AllowedPermissions => PermissionAllowlistView;

    /// <inheritdoc />
    public async Task<GameServerAccessDto> GetAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        _ = Authorize(gameServerId, principal);
        await EnsureServerExistsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        var value = ResourceValue(gameServerId);
        var assignablePermissions = AssignablePermissionsFor(principal, value);
        var users = await dbContext.Users
            .AsNoTracking()
            .Include(q => q.Claims!.Where(claim =>
                PermissionAllowlist.Contains(claim.Identifier)
                && claim.Value == value
                && claim.ValueType == null
                && !claim.IsPrefix))
            .Where(q => !q.IsDeleted && q.Claims!.Any(claim =>
                PermissionAllowlist.Contains(claim.Identifier)
                && claim.Value == value
                && claim.ValueType == null
                && !claim.IsPrefix))
            .OrderBy(q => q.Nickname)
            .ThenBy(q => q.PrimaryKey)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Include(q => q.Claims!.Where(claim =>
                PermissionAllowlist.Contains(claim.Identifier)
                && claim.Value == value
                && claim.ValueType == null
                && !claim.IsPrefix))
            .Where(q => q.Claims!.Any(claim =>
                PermissionAllowlist.Contains(claim.Identifier)
                && claim.Value == value
                && claim.ValueType == null
                && !claim.IsPrefix))
            .OrderBy(q => q.Title)
            .ThenBy(q => q.PrimaryKey)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var roleIds = roles.Select(q => q.PrimaryKey).ToArray();
        var memberCounts = await dbContext.Roles
            .AsNoTracking()
            .Where(q => roleIds.Contains(q.PrimaryKey))
            .Select(q => new
            {
                RoleId = q.PrimaryKey,
                MemberCount = q.Users!.Count(user => !user.IsDeleted && !user.IsBanned && user.IsVerified),
            })
            .ToDictionaryAsync(q => q.RoleId, q => q.MemberCount, cancellationToken)
            .ConfigureAwait(false);

        return new GameServerAccessDto
        {
            AllowedPermissions = PermissionAllowlist.ToArray(),
            AssignablePermissions = assignablePermissions,
            Users = users.Select(q => new GameServerUserAccessGrantDto
                {
                    UserId = q.PrimaryKey,
                    Nickname = q.Nickname,
                    Detail = Fingerprint(q.PrimaryKey),
                    Permissions = ProjectPermissions(q.Claims, value),
                })
                .ToArray(),
            Roles = roles.Select(q => new GameServerRoleAccessGrantDto
                {
                    RoleId = q.PrimaryKey,
                    Title = q.Title,
                    Description = q.Description,
                    MemberCount = memberCounts.GetValueOrDefault(q.PrimaryKey),
                    Permissions = ProjectPermissions(q.Claims, value),
                })
                .ToArray(),
        };
    }

    /// <inheritdoc />
    public async Task<GameServerUserAccessCandidateDto[]> SearchUserCandidatesAsync(
        Guid gameServerId,
        string? search,
        int take,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        _ = Authorize(gameServerId, principal);
        ValidateTake(take);
        await EnsureServerExistsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        var value = ResourceValue(gameServerId);
        var query = ApplyNicknameSearch(
            dbContext.Users
                .AsNoTracking()
                .Where(q => !q.IsDeleted && !q.IsBanned && q.IsVerified)
                .Where(q => !q.Claims!.Any(claim =>
                    PermissionAllowlist.Contains(claim.Identifier)
                    && claim.Value == value
                    && claim.ValueType == null
                    && !claim.IsPrefix)),
            search);
        var rows = await query
            .OrderBy(q => q.Nickname)
            .ThenBy(q => q.PrimaryKey)
            .Take(take)
            .Select(q => new { q.PrimaryKey, q.Nickname })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .Select(q => new GameServerUserAccessCandidateDto
            {
                UserId = q.PrimaryKey,
                Nickname = q.Nickname,
                Detail = Fingerprint(q.PrimaryKey),
            })
            .ToArray();
    }

    /// <summary>
    /// Narrows a user query to the users whose nickname contains the search text, ignoring letter case and
    /// treating every wildcard character of the search text literally.
    /// </summary>
    /// <param name="query">The user query to narrow.</param>
    /// <param name="search">The search text; a null or blank text leaves the query unchanged.</param>
    /// <returns>The narrowed user query.</returns>
    internal static IQueryable<User> ApplyNicknameSearch(IQueryable<User> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var pattern = LikePattern.Contains(search);
        return query.Where(q => EF.Functions.ILike(q.Nickname, pattern, "\\"));
    }

    /// <inheritdoc />
    public async Task<GameServerRoleAccessCandidateDto[]> SearchRoleCandidatesAsync(
        Guid gameServerId,
        string? search,
        int take,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        _ = Authorize(gameServerId, principal);
        ValidateTake(take);
        await EnsureServerExistsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        return await BuildRoleCandidateQuery(
                dbContext.Roles.AsNoTracking(),
                ResourceValue(gameServerId),
                search,
                take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the role candidate query: the roles that hold no direct permission on the server yet and whose title
    /// contains the search text, ordered by title, limited to <paramref name="take" /> and projected with the count of
    /// their active members.
    /// </summary>
    /// <param name="roles">The role query to start from.</param>
    /// <param name="resourceValue">The resource value that identifies the server inside a claim.</param>
    /// <param name="search">The search text; a null or blank text adds no title filter.</param>
    /// <param name="take">The maximum number of candidates.</param>
    /// <returns>The candidate query.</returns>
    internal static IQueryable<GameServerRoleAccessCandidateDto> BuildRoleCandidateQuery(
        IQueryable<Role> roles,
        string resourceValue,
        string? search,
        int take)
        => ApplyTitleSearch(
                roles.Where(q => !q.Claims!.Any(claim =>
                    PermissionAllowlist.Contains(claim.Identifier)
                    && claim.Value == resourceValue
                    && claim.ValueType == null
                    && !claim.IsPrefix)),
                search)
            .OrderBy(q => q.Title)
            .ThenBy(q => q.PrimaryKey)
            .Take(take)
            .Select(q => new GameServerRoleAccessCandidateDto
            {
                RoleId = q.PrimaryKey,
                Title = q.Title,
                Description = q.Description,
                MemberCount = q.Users!.Count(user => !user.IsDeleted && !user.IsBanned && user.IsVerified),
            });

    /// <summary>
    /// Narrows a role query to the roles whose title contains the search text, ignoring letter case and treating
    /// every wildcard character of the search text literally.
    /// </summary>
    /// <param name="query">The role query to narrow.</param>
    /// <param name="search">The search text; a null or blank text leaves the query unchanged.</param>
    /// <returns>The narrowed role query.</returns>
    internal static IQueryable<Role> ApplyTitleSearch(IQueryable<Role> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var pattern = LikePattern.Contains(search);
        return query.Where(q => EF.Functions.ILike(q.Title, pattern, "\\"));
    }

    /// <inheritdoc />
    public Task GrantUserAsync(
        Guid gameServerId,
        Guid userId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => ChangeUserGrantAsync(
            gameServerId,
            userId,
            permission,
            principal,
            grant: true,
            cancellationToken);

    /// <inheritdoc />
    public Task RevokeUserAsync(
        Guid gameServerId,
        Guid userId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => ChangeUserGrantAsync(
            gameServerId,
            userId,
            permission,
            principal,
            grant: false,
            cancellationToken);

    /// <inheritdoc />
    public Task GrantRoleAsync(
        Guid gameServerId,
        long roleId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => ChangeRoleGrantAsync(
            gameServerId,
            roleId,
            permission,
            principal,
            grant: true,
            cancellationToken);

    /// <inheritdoc />
    public Task RevokeRoleAsync(
        Guid gameServerId,
        long roleId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => ChangeRoleGrantAsync(
            gameServerId,
            roleId,
            permission,
            principal,
            grant: false,
            cancellationToken);

    private async Task ChangeUserGrantAsync(
        Guid gameServerId,
        Guid userId,
        string permission,
        ClaimsPrincipal principal,
        bool grant,
        CancellationToken cancellationToken)
    {
        var actorId = Authorize(gameServerId, principal);
        ValidatePermission(permission);
        EnsureAssignable(principal, ResourceValue(gameServerId), permission);
        await EnsureServerExistsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        var user = await dbContext.Users
            .Include(q => q.Claims)
            .SingleOrDefaultAsync(q => q.PrimaryKey == userId && !q.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingNotFoundException($"User {userId} does not exist.");
        var value = ResourceValue(gameServerId);
        user.Claims ??= [];
        var assigned = user.Claims.SingleOrDefault(q => IsExactGrant(q, permission, value));
        if (grant)
        {
            var claim = await GetOrCreateClaimAsync(permission, value, cancellationToken).ConfigureAwait(false);
            if (assigned is not null)
                return;
            user.Claims.Add(claim);
        }
        else
        {
            if (assigned is null)
                return;
            user.Claims.Remove(assigned);
        }

        AddAudit(gameServerId, actorId, grant, "User", userId.ToString(), permission);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ChangeRoleGrantAsync(
        Guid gameServerId,
        long roleId,
        string permission,
        ClaimsPrincipal principal,
        bool grant,
        CancellationToken cancellationToken)
    {
        var actorId = Authorize(gameServerId, principal);
        ValidatePermission(permission);
        EnsureAssignable(principal, ResourceValue(gameServerId), permission);
        await EnsureServerExistsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        var role = await dbContext.Roles
            .Include(q => q.Claims)
            .SingleOrDefaultAsync(q => q.PrimaryKey == roleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingNotFoundException($"Role {roleId} does not exist.");
        var value = ResourceValue(gameServerId);
        role.Claims ??= [];
        var assigned = role.Claims.SingleOrDefault(q => IsExactGrant(q, permission, value));
        if (grant)
        {
            var claim = await GetOrCreateClaimAsync(permission, value, cancellationToken).ConfigureAwait(false);
            if (assigned is not null)
                return;
            role.Claims.Add(claim);
        }
        else
        {
            if (assigned is null)
                return;
            role.Claims.Remove(assigned);
        }

        AddAudit(gameServerId, actorId, grant, "Role", roleId.ToString(), permission);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Claim> GetOrCreateClaimAsync(
        string permission,
        string value,
        CancellationToken cancellationToken)
    {
        var claim = await dbContext.Claims
            .SingleOrDefaultAsync(q =>
                q.Identifier == permission
                && q.Value == value
                && q.ValueType == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (claim is { IsPrefix: true })
        {
            throw new HostingConflictException(
                "The stored server permission uses invalid prefix metadata and cannot be delegated.");
        }
        return claim ?? new Claim
        {
            Identifier = permission,
            Value = value,
            ValueType = null,
            IsPrefix = false,
            Category = "Game servers",
            Title = permission,
            Description = "Server-scoped hosting permission.",
        };
    }

    private async Task EnsureServerExistsAsync(Guid gameServerId, CancellationToken cancellationToken)
    {
        if (!await dbContext.GameServers.AnyAsync(q => q.PrimaryKey == gameServerId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new HostingNotFoundException($"Game server {gameServerId} does not exist.");
        }
    }

    private void AddAudit(
        Guid gameServerId,
        Guid actorId,
        bool grant,
        string subjectKind,
        string subjectId,
        string permission)
        => auditWriter.Add(
            dbContext,
            grant ? EHostingAuditEventKind.AccessGranted : EHostingAuditEventKind.AccessRevoked,
            grant ? "Server access granted." : "Server access revoked.",
            actorId,
            gameServerId,
            details: $"{subjectKind} {subjectId}; permission {permission}.");

    private static void ValidatePermission(string permission)
    {
        if (!PermissionAllowlist.Contains(permission, StringComparer.Ordinal))
            throw new HostingValidationException("The requested server permission is not assignable.");
    }

    private static void EnsureAssignable(ClaimsPrincipal principal, string serverValue, string permission)
    {
        if (!AssignablePermissionsFor(principal, serverValue).Contains(permission, StringComparer.Ordinal))
        {
            throw new HostingForbiddenException(
                "The permission is outside the permissions you may assign on this server.");
        }
    }

    /// <summary>
    /// Resolves the allowlisted permissions the caller may grant or revoke on one server: every permission for
    /// administrators and for holders of full control on the server, otherwise exactly the allowlisted permissions
    /// the caller holds on the server as exact string-typed claims, in allowlist order.
    /// </summary>
    private static string[] AssignablePermissionsFor(ClaimsPrincipal principal, string serverValue)
    {
        var claims = principal.Claims
            .Where(claim => string.Equals(
                claim.ValueType,
                System.Security.Claims.ClaimValueTypes.String,
                StringComparison.Ordinal))
            .ToArray();
        var assignsEverything = claims.Any(claim =>
            (string.Equals(claim.Value, string.Empty, StringComparison.Ordinal)
             && (string.Equals(claim.Type, Claims.Administrative.All, StringComparison.Ordinal)
                 || string.Equals(claim.Type, Claims.Administrative.Server, StringComparison.Ordinal)
                 || string.Equals(claim.Type, Claims.Administrative.Hosting, StringComparison.Ordinal)))
            || HoldsOnServer(claim, Claims.ResourceBased.Server.All, serverValue));
        return assignsEverything
            ? PermissionAllowlist.ToArray()
            : PermissionAllowlist
                .Where(permission => claims.Any(claim => HoldsOnServer(claim, permission, serverValue)))
                .ToArray();
    }

    private static bool HoldsOnServer(System.Security.Claims.Claim claim, string permission, string serverValue)
        => string.Equals(claim.Type, permission, StringComparison.Ordinal)
           && string.Equals(claim.Value, serverValue, StringComparison.Ordinal);

    private static void ValidateTake(int take)
    {
        if (take is < 1 or > HostingSearchLimits.MaximumTake)
        {
            throw new HostingValidationException(
                $"The candidate limit must be between 1 and {HostingSearchLimits.MaximumTake}.");
        }
    }

    /// <summary>
    /// Builds the short identity fingerprint, the character # followed by the last six characters of the id in its
    /// compact hexadecimal form, which tells members with equal nicknames apart without exposing any linked
    /// chat-provider account.
    /// </summary>
    private static string Fingerprint(Guid userId)
        => "#" + userId.ToString("N")[^6..];

    private static Guid Authorize(Guid gameServerId, ClaimsPrincipal principal)
    {
        if (!principal.TryGetUserId(out var actorId)
            || !ServerManageAccessRequirement.HasExactPermission(principal, ResourceValue(gameServerId)))
        {
            throw new HostingForbiddenException("Server ManageAccess permission is required.");
        }
        return actorId;
    }

    private static string[] ProjectPermissions(IEnumerable<Claim>? claims, string value)
        => PermissionAllowlist
            .Where(permission => claims?.Any(q => IsExactGrant(q, permission, value)) == true)
            .ToArray();

    private static bool IsExactGrant(Claim claim, string permission, string value)
        => string.Equals(claim.Identifier, permission, StringComparison.Ordinal)
           && string.Equals(claim.Value, value, StringComparison.Ordinal)
           && claim.ValueType is null
           && !claim.IsPrefix;

    private static string ResourceValue(Guid gameServerId) => gameServerId.ToString("D");
}
