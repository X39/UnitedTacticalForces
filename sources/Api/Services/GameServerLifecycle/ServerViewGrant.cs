namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// One server-bound view grant read by the server view audience queries: a user holds, directly or through a role,
/// a server claim that lets the user view the server identified by <see cref="ServerValue"/>.
/// </summary>
/// <param name="UserId">The primary key of the user who holds the grant.</param>
/// <param name="ServerValue">
/// The stored claim value, which names the server through the default <see cref="Guid.ToString()"/> text of its id.
/// </param>
internal sealed record ServerViewGrant(Guid UserId, string ServerValue);
