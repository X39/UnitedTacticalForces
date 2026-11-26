using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.Util.DependencyInjection.Attributes;
using SecurityClaim = System.Security.Claims.Claim;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Database-backed implementation of the hosting authorization rules.
/// </summary>
/// <remarks>
/// <see cref="HasServerPermission"/> and <see cref="GetServerViewAudienceAsync"/> express the same view rule, once
/// per principal and once as a set of users. Any change to one must be mirrored in the other; the audience drift
/// test compares both for every claim class.
/// <see cref="CanViewCostOwnerAsync"/>, <see cref="GetCostOwnerViewAudienceAsync"/> and
/// <see cref="GetViewableCostOwnerIdsAsync"/> likewise express one cost-owner view rule and must change together;
/// their drift tests compare them for every claim class.
/// </remarks>
/// <param name="dbContext">The scoped database context the rules read users, roles, claims and cost owners from.</param>
[Scoped<HostingAuthorizationService, IHostingAuthorizationService>]
internal sealed class HostingAuthorizationService(DatabaseContext dbContext) : IHostingAuthorizationService
{
    /// <summary>
    /// The lower-case global administrative claim identifier, compared with the lower-cased stored identifier.
    /// </summary>
    private static readonly string GlobalAdministratorIdentifier = Claims.Administrative.All.ToLowerInvariant();

    /// <summary>
    /// The lower-case server administrative claim identifier, compared with the lower-cased stored identifier.
    /// </summary>
    private static readonly string ServerAdministratorIdentifier = Claims.Administrative.Server.ToLowerInvariant();

    /// <summary>
    /// The lower-case hosting administrative claim identifier, compared with the lower-cased stored identifier.
    /// </summary>
    private static readonly string HostingAdministratorIdentifier = Claims.Administrative.Hosting.ToLowerInvariant();

    /// <summary>
    /// The lower-case all-actions server claim identifier, compared with the lower-cased stored identifier.
    /// </summary>
    private static readonly string ServerAllIdentifier = Claims.ResourceBased.Server.All.ToLowerInvariant();

    /// <summary>
    /// The lower-case view server claim identifier, compared with the lower-cased stored identifier.
    /// </summary>
    private static readonly string ServerViewIdentifier = Claims.ResourceBased.Server.View.ToLowerInvariant();

    /// <summary>
    /// The lower-case all-finance claim identifier, compared with the lower-cased stored identifier.
    /// </summary>
    private static readonly string FinanceAllIdentifier = Claims.Finance.All.ToLowerInvariant();

    /// <summary>
    /// The lower-case view-all-finance claim identifier, compared with the lower-cased stored identifier.
    /// </summary>
    private static readonly string FinanceViewAllIdentifier = Claims.Finance.ViewAll.ToLowerInvariant();

    /// <summary>
    /// The lower-case cost-owner view claim identifier, compared with the lower-cased stored identifier.
    /// </summary>
    private static readonly string CostOwnerViewIdentifier = Claims.ResourceBased.CostOwner.View.ToLowerInvariant();

    /// <inheritdoc />
    /// <remarks>
    /// Keep this rule identical to <see cref="GetServerViewAudienceAsync"/> for the view permission.
    /// </remarks>
    public bool HasServerPermission(ClaimsPrincipal principal, Guid gameServerId, string permission)
    {
        var value = gameServerId.ToString();
        return IsHostingAdministrator(principal)
               || principal.HasClaim(Claims.ResourceBased.Server.All, value)
               || principal.HasClaim(permission, value);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    /// <remarks>
    /// Keep this rule identical to <see cref="GetCostOwnerViewAudienceAsync"/>.
    /// </remarks>
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

    /// <inheritdoc />
    /// <remarks>
    /// Keep this rule identical to <see cref="CanViewCostOwnerAsync"/> and
    /// <see cref="GetCostOwnerViewAudienceAsync"/>.
    /// </remarks>
    public async Task<IReadOnlySet<Guid>?> GetViewableCostOwnerIdsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (IsHostingAdministrator(principal)
            || principal.HasAnyEmptyClaim(Claims.Finance.All, Claims.Finance.ViewAll))
            return null;

        var ids = new HashSet<Guid>();
        foreach (var claim in principal.FindAll(
                     q => string.Equals(q.Type, Claims.ResourceBased.CostOwner.View, StringComparison.OrdinalIgnoreCase)))
        {
            if (Guid.TryParse(claim.Value, out var costOwnerId)
                && string.Equals(claim.Value, costOwnerId.ToString(), StringComparison.Ordinal))
                ids.Add(costOwnerId);
        }

        if (!principal.TryGetUserId(out var userId))
            return ids;

        var ownIds = await dbContext.CostOwners
            .AsNoTracking()
            .Where(q => q.UserFk == userId)
            .Select(q => q.PrimaryKey)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        ids.UnionWith(ownIds);
        return ids;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetServerViewAudienceAsync(
        IReadOnlyCollection<Guid> gameServerIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameServerIds);
        if (gameServerIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyCollection<Guid>>();

        var idsByValue = gameServerIds
            .Distinct()
            .ToDictionary(q => q.ToString(), q => q, StringComparer.Ordinal);
        var serverValues = idsByValue.Keys.ToArray();
        var users = dbContext.Users.AsNoTracking();

        var globalViewers = await BuildGlobalViewerQuery(users)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var directGrants = await BuildDirectViewGrantQuery(users, serverValues)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var roleGrants = await BuildRoleViewGrantQuery(users, serverValues)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var audiences = idsByValue.Values.ToDictionary(q => q, _ => new HashSet<Guid>(globalViewers));
        foreach (var grant in directGrants.Concat(roleGrants))
        {
            // Values that are not an exact key were not requested; ignoring them fails closed.
            if (idsByValue.TryGetValue(grant.ServerValue, out var gameServerId))
                audiences[gameServerId].Add(grant.UserId);
        }

        return audiences.ToDictionary(
            q => q.Key,
            q => (IReadOnlyCollection<Guid>) q.Value.ToArray());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetCostOwnerViewAudienceAsync(
        IReadOnlyCollection<Guid> costOwnerIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(costOwnerIds);
        if (costOwnerIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyCollection<Guid>>();

        var idsByValue = costOwnerIds
            .Distinct()
            .ToDictionary(q => q.ToString(), q => q, StringComparer.Ordinal);
        var ownerValues = idsByValue.Keys.ToArray();
        var requestedIds = idsByValue.Values.ToArray();
        var users = dbContext.Users.AsNoTracking();

        var globalViewers = await BuildGlobalViewerQuery(users)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var financeViewers = await BuildFinanceViewerQuery(users)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var directGrants = await BuildDirectCostOwnerViewGrantQuery(users, ownerValues)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var roleGrants = await BuildRoleCostOwnerViewGrantQuery(users, ownerValues)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var ownerUsers = await dbContext.CostOwners
            .AsNoTracking()
            .Where(q => requestedIds.Contains(q.PrimaryKey)
                        && q.UserFk != null
                        && dbContext.Users.Any(user => user.PrimaryKey == q.UserFk
                                                       && !user.IsDeleted
                                                       && !user.IsBanned))
            .Select(q => new { CostOwnerId = q.PrimaryKey, UserId = q.UserFk!.Value })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var audiences = idsByValue.Values.ToDictionary(
            q => q,
            _ => new HashSet<Guid>(globalViewers.Concat(financeViewers)));
        foreach (var grant in directGrants.Concat(roleGrants))
        {
            // Values that are not an exact key were not requested; ignoring them fails closed.
            if (idsByValue.TryGetValue(grant.CostOwnerValue, out var costOwnerId))
                audiences[costOwnerId].Add(grant.UserId);
        }

        foreach (var ownerUser in ownerUsers)
        {
            audiences[ownerUser.CostOwnerId].Add(ownerUser.UserId);
        }

        return audiences.ToDictionary(
            q => q.Key,
            q => (IReadOnlyCollection<Guid>) q.Value.ToArray());
    }

    /// <summary>
    /// Builds the query for the ids of active users who may view every server because they hold, directly or through
    /// a role, an empty-valued global, server or hosting administrative claim.
    /// </summary>
    /// <param name="users">The user set to filter, typically the untracked users of the database context.</param>
    /// <returns>A provider-neutral query yielding each matching user id once.</returns>
    /// <remarks>
    /// Deleted and banned users are excluded first. A null stored value counts as empty because every principal
    /// builder maps a null value to the empty string. Identifiers are compared case-insensitively and value type and
    /// prefix flags are ignored, mirroring <see cref="ClaimsPrincipal.HasClaim(string, string)"/> on the rebuilt
    /// principal.
    /// </remarks>
    internal static IQueryable<Guid> BuildGlobalViewerQuery(IQueryable<User> users)
        => users
            .Where(q => !q.IsDeleted && !q.IsBanned)
            .Where(q => q.Claims!.Any(claim =>
                            (claim.Identifier.ToLower() == GlobalAdministratorIdentifier
                             || claim.Identifier.ToLower() == ServerAdministratorIdentifier
                             || claim.Identifier.ToLower() == HostingAdministratorIdentifier)
                            && (claim.Value == null || claim.Value == string.Empty))
                        || q.Roles!.Any(role => role.Claims!.Any(claim =>
                            (claim.Identifier.ToLower() == GlobalAdministratorIdentifier
                             || claim.Identifier.ToLower() == ServerAdministratorIdentifier
                             || claim.Identifier.ToLower() == HostingAdministratorIdentifier)
                            && (claim.Value == null || claim.Value == string.Empty))))
            .Select(q => q.PrimaryKey);

    /// <summary>
    /// Builds the query for the server-bound view grants active users hold through their own claims.
    /// </summary>
    /// <param name="users">The user set to filter, typically the untracked users of the database context.</param>
    /// <param name="serverValues">The default text forms of the requested server ids.</param>
    /// <returns>
    /// A provider-neutral query yielding one grant per user claim with the all-actions or view server identifier and
    /// a value contained in <paramref name="serverValues"/>.
    /// </returns>
    /// <remarks>
    /// Deleted and banned users are excluded first. Identifiers are compared case-insensitively and values ordinally,
    /// mirroring <see cref="ClaimsPrincipal.HasClaim(string, string)"/>.
    /// </remarks>
    internal static IQueryable<ServerViewGrant> BuildDirectViewGrantQuery(
        IQueryable<User> users,
        string[] serverValues)
        => users
            .Where(q => !q.IsDeleted && !q.IsBanned)
            .SelectMany(
                q => q.Claims!.Where(claim =>
                    (claim.Identifier.ToLower() == ServerAllIdentifier
                     || claim.Identifier.ToLower() == ServerViewIdentifier)
                    && claim.Value != null
                    && serverValues.Contains(claim.Value)),
                (user, claim) => new ServerViewGrant(user.PrimaryKey, claim.Value!));

    /// <summary>
    /// Builds the query for the server-bound view grants active users hold through the claims of their roles.
    /// </summary>
    /// <param name="users">The user set to filter, typically the untracked users of the database context.</param>
    /// <param name="serverValues">The default text forms of the requested server ids.</param>
    /// <returns>
    /// A provider-neutral query yielding one grant per role claim with the all-actions or view server identifier and
    /// a value contained in <paramref name="serverValues"/>, attributed to every active member of the role.
    /// </returns>
    /// <remarks>
    /// Deleted and banned users are excluded first. Identifiers are compared case-insensitively and values ordinally,
    /// mirroring <see cref="ClaimsPrincipal.HasClaim(string, string)"/>.
    /// </remarks>
    internal static IQueryable<ServerViewGrant> BuildRoleViewGrantQuery(
        IQueryable<User> users,
        string[] serverValues)
        => users
            .Where(q => !q.IsDeleted && !q.IsBanned)
            .SelectMany(
                q => q.Roles!.SelectMany(role => role.Claims!.Where(claim =>
                    (claim.Identifier.ToLower() == ServerAllIdentifier
                     || claim.Identifier.ToLower() == ServerViewIdentifier)
                    && claim.Value != null
                    && serverValues.Contains(claim.Value))),
                (user, claim) => new ServerViewGrant(user.PrimaryKey, claim.Value!));

    /// <summary>
    /// Builds the query for the ids of active users who may view every cost owner because they hold, directly or
    /// through a role, an empty-valued all-finance or view-all-finance claim.
    /// </summary>
    /// <param name="users">The user set to filter, typically the untracked users of the database context.</param>
    /// <returns>A provider-neutral query yielding each matching user id once.</returns>
    /// <remarks>
    /// Deleted and banned users are excluded first. A null stored value counts as empty because every principal
    /// builder maps a null value to the empty string. Identifiers are compared case-insensitively, mirroring
    /// <see cref="ClaimsPrincipal.HasClaim(string, string)"/> on the rebuilt principal.
    /// </remarks>
    internal static IQueryable<Guid> BuildFinanceViewerQuery(IQueryable<User> users)
        => users
            .Where(q => !q.IsDeleted && !q.IsBanned)
            .Where(q => q.Claims!.Any(claim =>
                            (claim.Identifier.ToLower() == FinanceAllIdentifier
                             || claim.Identifier.ToLower() == FinanceViewAllIdentifier)
                            && (claim.Value == null || claim.Value == string.Empty))
                        || q.Roles!.Any(role => role.Claims!.Any(claim =>
                            (claim.Identifier.ToLower() == FinanceAllIdentifier
                             || claim.Identifier.ToLower() == FinanceViewAllIdentifier)
                            && (claim.Value == null || claim.Value == string.Empty))))
            .Select(q => q.PrimaryKey);

    /// <summary>
    /// Builds the query for the cost-owner-bound view grants active users hold through their own claims.
    /// </summary>
    /// <param name="users">The user set to filter, typically the untracked users of the database context.</param>
    /// <param name="costOwnerValues">The default text forms of the requested cost owner ids.</param>
    /// <returns>
    /// A provider-neutral query yielding one grant per user claim with the cost-owner view identifier and a value
    /// contained in <paramref name="costOwnerValues"/>.
    /// </returns>
    /// <remarks>
    /// Deleted and banned users are excluded first. Identifiers are compared case-insensitively and values ordinally,
    /// mirroring <see cref="ClaimsPrincipal.HasClaim(string, string)"/>.
    /// </remarks>
    internal static IQueryable<CostOwnerViewGrant> BuildDirectCostOwnerViewGrantQuery(
        IQueryable<User> users,
        string[] costOwnerValues)
        => users
            .Where(q => !q.IsDeleted && !q.IsBanned)
            .SelectMany(
                q => q.Claims!.Where(claim =>
                    claim.Identifier.ToLower() == CostOwnerViewIdentifier
                    && claim.Value != null
                    && costOwnerValues.Contains(claim.Value)),
                (user, claim) => new CostOwnerViewGrant(user.PrimaryKey, claim.Value!));

    /// <summary>
    /// Builds the query for the cost-owner-bound view grants active users hold through the claims of their roles.
    /// </summary>
    /// <param name="users">The user set to filter, typically the untracked users of the database context.</param>
    /// <param name="costOwnerValues">The default text forms of the requested cost owner ids.</param>
    /// <returns>
    /// A provider-neutral query yielding one grant per role claim with the cost-owner view identifier and a value
    /// contained in <paramref name="costOwnerValues"/>, attributed to every active member of the role.
    /// </returns>
    /// <remarks>
    /// Deleted and banned users are excluded first. Identifiers are compared case-insensitively and values ordinally,
    /// mirroring <see cref="ClaimsPrincipal.HasClaim(string, string)"/>.
    /// </remarks>
    internal static IQueryable<CostOwnerViewGrant> BuildRoleCostOwnerViewGrantQuery(
        IQueryable<User> users,
        string[] costOwnerValues)
        => users
            .Where(q => !q.IsDeleted && !q.IsBanned)
            .SelectMany(
                q => q.Roles!.SelectMany(role => role.Claims!.Where(claim =>
                    claim.Identifier.ToLower() == CostOwnerViewIdentifier
                    && claim.Value != null
                    && costOwnerValues.Contains(claim.Value))),
                (user, claim) => new CostOwnerViewGrant(user.PrimaryKey, claim.Value!));

    /// <summary>
    /// Returns whether the principal is a hosting administrator, that is, holds an empty-valued global, server or
    /// hosting administrative claim.
    /// </summary>
    /// <param name="principal">The principal whose claims are evaluated.</param>
    /// <returns><see langword="true"/> when the principal administers hosting; otherwise <see langword="false"/>.</returns>
    private static bool IsHostingAdministrator(ClaimsPrincipal principal)
        => principal.HasAnyEmptyClaim(
            Claims.Administrative.All,
            Claims.Administrative.Server,
            Claims.Administrative.Hosting);
}
