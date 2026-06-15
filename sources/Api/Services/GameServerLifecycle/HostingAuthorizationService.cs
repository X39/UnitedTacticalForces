using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.Util.DependencyInjection.Attributes;
using SecurityClaim = System.Security.Claims.Claim;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

[Scoped<HostingAuthorizationService, IHostingAuthorizationService>]
internal sealed class HostingAuthorizationService(DatabaseContext dbContext) : IHostingAuthorizationService
{
    public bool HasServerPermission(ClaimsPrincipal principal, Guid gameServerId, string permission)
    {
        var value = gameServerId.ToString();
        return IsHostingAdministrator(principal)
               || principal.HasClaim(Claims.ResourceBased.Server.All, value)
               || principal.HasClaim(permission, value);
    }

    public async Task<bool> CanUseCostOwnerAsync(
        ClaimsPrincipal principal,
        Guid costOwnerId,
        CancellationToken cancellationToken = default)
    {
        if (IsHostingAdministrator(principal))
            return true;
        if (principal.HasClaim(Claims.ResourceBased.CostOwner.Use, costOwnerId.ToString()))
            return true;
        if (!principal.TryGetUserId(out var userId))
            return false;
        return await dbContext.CostOwners
            .AsNoTracking()
            .AnyAsync(q => q.PrimaryKey == costOwnerId && q.UserFk == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> CanViewCostOwnerAsync(
        ClaimsPrincipal principal,
        Guid costOwnerId,
        CancellationToken cancellationToken = default)
    {
        if (IsHostingAdministrator(principal)
            || principal.HasAnyEmptyClaim(Claims.Finance.All, Claims.Finance.ViewAll)
            || principal.HasClaim(Claims.ResourceBased.CostOwner.View, costOwnerId.ToString()))
            return true;
        if (!principal.TryGetUserId(out var userId))
            return false;
        return await dbContext.CostOwners
            .AsNoTracking()
            .AnyAsync(q => q.PrimaryKey == costOwnerId && q.UserFk == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ClaimsPrincipal?> RebuildPrincipalAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(q => q.Claims)
            .Include(q => q.Roles!)
            .ThenInclude(q => q.Claims)
            .SingleOrDefaultAsync(
                q => q.PrimaryKey == userId && !q.IsDeleted && !q.IsBanned,
                cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
            return null;

        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new SecurityClaim(ClaimTypes.Name, user.PrimaryKey.ToString()));
        foreach (var claim in (user.Claims ?? []).Concat((user.Roles ?? []).SelectMany(q => q.Claims ?? [])))
        {
            identity.AddClaim(new SecurityClaim(claim.Identifier, claim.Value ?? string.Empty));
        }

        return new ClaimsPrincipal(identity);
    }

    private static bool IsHostingAdministrator(ClaimsPrincipal principal)
        => principal.HasAnyEmptyClaim(
            Claims.Administrative.All,
            Claims.Administrative.Server,
            Claims.Administrative.Hosting);
}
