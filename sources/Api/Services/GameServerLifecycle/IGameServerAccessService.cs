using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Lists, searches and changes the direct per-server permissions that members and roles hold on one game server.
/// </summary>
public interface IGameServerAccessService
{
    /// <summary>
    /// The exact server-scoped permissions that may be delegated through this service, in display order.
    /// </summary>
    IReadOnlyCollection<string> AllowedPermissions { get; }

    /// <summary>
    /// Lists the direct user and role grants of one server together with the permissions the caller may assign.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="principal">The calling principal, which must hold the ManageAccess permission on the server.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The grants of the server.</returns>
    /// <exception cref="HostingForbiddenException">The caller may not manage access on the server.</exception>
    /// <exception cref="HostingNotFoundException">The server does not exist.</exception>
    Task<GameServerAccessDto> GetAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the members that can still be given direct access to the server: active members without a direct
    /// grant on the server.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="search">The optional nickname search text; blank text matches every member.</param>
    /// <param name="take">The maximum number of candidates, from 1 to 50.</param>
    /// <param name="principal">The calling principal, which must hold the ManageAccess permission on the server.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching candidates ordered by nickname and then identity.</returns>
    /// <exception cref="HostingForbiddenException">The caller may not manage access on the server.</exception>
    /// <exception cref="HostingValidationException"><paramref name="take" /> is outside the allowed range.</exception>
    /// <exception cref="HostingNotFoundException">The server does not exist.</exception>
    Task<GameServerUserAccessCandidateDto[]> SearchUserCandidatesAsync(
        Guid gameServerId,
        string? search,
        int take,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the roles that can still be given direct access to the server: roles without a direct grant on the
    /// server.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="search">The optional role title search text; blank text matches every role.</param>
    /// <param name="take">The maximum number of candidates, from 1 to 50.</param>
    /// <param name="principal">The calling principal, which must hold the ManageAccess permission on the server.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching candidates, each with its active member count, ordered by title and then identity.</returns>
    /// <exception cref="HostingForbiddenException">The caller may not manage access on the server.</exception>
    /// <exception cref="HostingValidationException"><paramref name="take" /> is outside the allowed range.</exception>
    /// <exception cref="HostingNotFoundException">The server does not exist.</exception>
    Task<GameServerRoleAccessCandidateDto[]> SearchRoleCandidatesAsync(
        Guid gameServerId,
        string? search,
        int take,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants one server permission directly to a user; does nothing when the user already holds it.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="userId">The user receiving the permission.</param>
    /// <param name="permission">The permission to grant, one of <see cref="AllowedPermissions" />.</param>
    /// <param name="principal">The calling principal.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the grant is stored.</returns>
    /// <exception cref="HostingValidationException">The permission is not delegable.</exception>
    /// <exception cref="HostingForbiddenException">
    /// The caller may not manage access or does not hold the permission to assign.
    /// </exception>
    /// <exception cref="HostingNotFoundException">The server or the user does not exist.</exception>
    Task GrantUserAsync(
        Guid gameServerId,
        Guid userId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes one directly granted server permission from a user; does nothing when the user does not hold it.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="userId">The user losing the permission.</param>
    /// <param name="permission">The permission to revoke, one of <see cref="AllowedPermissions" />.</param>
    /// <param name="principal">The calling principal.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the grant is removed.</returns>
    /// <exception cref="HostingValidationException">The permission is not delegable.</exception>
    /// <exception cref="HostingForbiddenException">
    /// The caller may not manage access or does not hold the permission to assign.
    /// </exception>
    /// <exception cref="HostingNotFoundException">The server or the user does not exist.</exception>
    Task RevokeUserAsync(
        Guid gameServerId,
        Guid userId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants one server permission directly to a role; does nothing when the role already holds it.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="roleId">The role receiving the permission.</param>
    /// <param name="permission">The permission to grant, one of <see cref="AllowedPermissions" />.</param>
    /// <param name="principal">The calling principal.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the grant is stored.</returns>
    /// <exception cref="HostingValidationException">The permission is not delegable.</exception>
    /// <exception cref="HostingForbiddenException">
    /// The caller may not manage access or does not hold the permission to assign.
    /// </exception>
    /// <exception cref="HostingNotFoundException">The server or the role does not exist.</exception>
    Task GrantRoleAsync(
        Guid gameServerId,
        long roleId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes one directly granted server permission from a role; does nothing when the role does not hold it.
    /// </summary>
    /// <param name="gameServerId">The game server identity.</param>
    /// <param name="roleId">The role losing the permission.</param>
    /// <param name="permission">The permission to revoke, one of <see cref="AllowedPermissions" />.</param>
    /// <param name="principal">The calling principal.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the grant is removed.</returns>
    /// <exception cref="HostingValidationException">The permission is not delegable.</exception>
    /// <exception cref="HostingForbiddenException">
    /// The caller may not manage access or does not hold the permission to assign.
    /// </exception>
    /// <exception cref="HostingNotFoundException">The server or the role does not exist.</exception>
    Task RevokeRoleAsync(
        Guid gameServerId,
        long roleId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
