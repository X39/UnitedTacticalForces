namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// One cost-owner-bound view grant read by the cost-owner view audience queries: a user holds, directly or through
/// a role, the cost-owner view claim for the cost owner identified by <see cref="CostOwnerValue"/>.
/// </summary>
/// <param name="UserId">The primary key of the user who holds the grant.</param>
/// <param name="CostOwnerValue">
/// The stored claim value, which names the cost owner through the default <see cref="Guid.ToString()"/> text of its
/// id.
/// </param>
internal sealed record CostOwnerViewGrant(Guid UserId, string CostOwnerValue);
