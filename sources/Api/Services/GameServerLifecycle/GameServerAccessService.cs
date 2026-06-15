using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Authorization.Requirements;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

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
    ];

    private static readonly IReadOnlyCollection<string> PermissionAllowlistView
        = Array.AsReadOnly(PermissionAllowlist);

    public IReadOnlyCollection<string> AllowedPermissions => PermissionAllowlistView;

    public async Task<GameServerAccessDto> GetAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        _ = Authorize(gameServerId, principal);
        await EnsureServerExistsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
        var value = ResourceValue(gameServerId);
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

        return new GameServerAccessDto
        {
            AllowedPermissions = PermissionAllowlist.ToArray(),
            Users = users.Select(q => new GameServerUserAccessGrantDto
                {
                    UserId = q.PrimaryKey,
                    Nickname = q.Nickname,
                    Permissions = ProjectPermissions(q.Claims, value),
                })
                .ToArray(),
            Roles = roles.Select(q => new GameServerRoleAccessGrantDto
                {
                    RoleId = q.PrimaryKey,
                    Title = q.Title,
                    Description = q.Description,
                    Permissions = ProjectPermissions(q.Claims, value),
                })
                .ToArray(),
        };
    }

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
