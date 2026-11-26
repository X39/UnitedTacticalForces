using System.Security.Claims;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Evaluates the hosting authorization rules for game servers and cost owners, both for one principal and, for the
/// view permission, as the set of users a server is visible to.
/// </summary>
public interface IHostingAuthorizationService
{
    /// <summary>
    /// Returns whether the principal holds a server permission for one game server.
    /// </summary>
    /// <param name="principal">The principal whose claims are evaluated.</param>
    /// <param name="gameServerId">The id of the game server the permission is bound to.</param>
    /// <param name="permission">The resource-bound server permission claim identifier, such as the view claim.</param>
    /// <returns>
    /// <see langword="true"/> when the principal is a hosting administrator, holds the all-actions server claim for
    /// the server, or holds <paramref name="permission"/> for the server; otherwise <see langword="false"/>.
    /// </returns>
    bool HasServerPermission(ClaimsPrincipal principal, Guid gameServerId, string permission);

    /// <summary>
    /// Returns whether the principal may charge hosting cost to a cost owner.
    /// </summary>
    /// <param name="principal">The principal whose claims and identity are evaluated.</param>
    /// <param name="costOwnerId">The id of the cost owner that would be charged.</param>
    /// <param name="cancellationToken">A token that cancels the database lookup.</param>
    /// <returns>A task resolving to whether the principal may use the cost owner.</returns>
    Task<bool> CanUseCostOwnerAsync(
        ClaimsPrincipal principal,
        Guid costOwnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the principal may view a cost owner.
    /// </summary>
    /// <param name="principal">The principal whose claims and identity are evaluated.</param>
    /// <param name="costOwnerId">The id of the cost owner to view.</param>
    /// <param name="cancellationToken">A token that cancels the database lookup.</param>
    /// <returns>A task resolving to whether the principal may view the cost owner.</returns>
    Task<bool> CanViewCostOwnerAsync(
        ClaimsPrincipal principal,
        Guid costOwnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds the claims principal of a stored user from the current database state.
    /// </summary>
    /// <param name="userId">The primary key of the user.</param>
    /// <param name="cancellationToken">A token that cancels the database lookup.</param>
    /// <returns>
    /// A task resolving to a principal carrying the user's own claims and the claims of all the user's roles, or
    /// <see langword="null"/> when the user does not exist, is banned or is deleted.
    /// </returns>
    Task<ClaimsPrincipal?> RebuildPrincipalAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves, for many game servers in one call, the users who may view each server.
    /// </summary>
    /// <param name="gameServerIds">The ids of the game servers whose audience is resolved; duplicates are ignored.</param>
    /// <param name="cancellationToken">A token that cancels the database queries.</param>
    /// <returns>
    /// A task resolving to a dictionary with an entry for every requested server id. Each entry lists the ids of the
    /// users who may view that server; an empty collection means nobody may view it.
    /// </returns>
    /// <remarks>
    /// The rule is identical to <see cref="HasServerPermission"/> with the view permission evaluated on the principal
    /// <see cref="RebuildPrincipalAsync"/> builds: a user is listed when the user is neither deleted nor banned and
    /// holds, directly or through any role, an empty-valued global, server or hosting administrative claim, or the
    /// all-actions or view server claim whose value is the server id. Each user id appears at most once per entry.
    /// Nothing is cached: the result reflects the database at the time of the call, so a revoked claim or a ban takes
    /// effect on the very next call. Archived servers are not excluded, because viewers must still learn that such a
    /// server changed.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetServerViewAudienceAsync(
        IReadOnlyCollection<Guid> gameServerIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves, for many cost owners in one call, the users who may view each cost owner.
    /// </summary>
    /// <param name="costOwnerIds">The ids of the cost owners whose audience is resolved; duplicates are ignored.</param>
    /// <param name="cancellationToken">A token that cancels the database queries.</param>
    /// <returns>
    /// A task resolving to a dictionary with an entry for every requested cost owner id. Each entry lists the ids of
    /// the active users for whom <see cref="CanViewCostOwnerAsync"/> holds; an empty collection means nobody may view
    /// that cost owner.
    /// </returns>
    /// <remarks>
    /// The rule is identical to <see cref="CanViewCostOwnerAsync"/> evaluated on the principal
    /// <see cref="RebuildPrincipalAsync"/> builds. The result feeds cost value frames and the in-app alert audience.
    /// Each user id appears at most once per entry. Nothing is cached: the result reflects the database at the time
    /// of the call.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetCostOwnerViewAudienceAsync(
        IReadOnlyCollection<Guid> costOwnerIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the cost owners one principal may view as a set of ids.
    /// </summary>
    /// <param name="principal">The principal whose claims and identity are evaluated.</param>
    /// <param name="cancellationToken">A token that cancels the database lookup.</param>
    /// <returns>
    /// A task resolving to <see langword="null"/> when the principal may view every cost owner, otherwise to the set
    /// of cost owner ids the principal may view, which is empty when the principal may view none.
    /// </returns>
    /// <remarks>
    /// The rule is identical to <see cref="CanViewCostOwnerAsync"/> for every cost owner id: a hosting administrator
    /// and an empty all-finance or view-all-finance claim view every owner, a cost-owner view claim valued with an
    /// owner id views that owner, and the user behind a personal cost owner views it. Nothing is cached.
    /// </remarks>
    Task<IReadOnlySet<Guid>?> GetViewableCostOwnerIdsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
