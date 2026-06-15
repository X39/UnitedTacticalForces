using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace Api.Tests.GameServerHosting;

internal sealed class NoOpGameServerAccessService : IGameServerAccessService
{
    public IReadOnlyCollection<string> AllowedPermissions => [];

    public Task<GameServerAccessDto> GetAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new GameServerAccessDto());

    public Task GrantUserAsync(
        Guid gameServerId,
        Guid userId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RevokeUserAsync(
        Guid gameServerId,
        Guid userId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task GrantRoleAsync(
        Guid gameServerId,
        long roleId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RevokeRoleAsync(
        Guid gameServerId,
        long roleId,
        string permission,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}