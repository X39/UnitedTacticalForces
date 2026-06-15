using System.Security.Claims;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO.Payloads;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface IGameServerLifecycleService
{
    Task<GameServer> CreateAsync(
        GameServerCreationPayload payload,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<LifecycleJob> EnqueueActionAsync(
        Guid gameServerId,
        string actionId,
        string? confirmation,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<LifecycleJob> RetryAsync(
        Guid gameServerId,
        Guid lifecycleJobId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<LifecycleJob> SkipAsync(
        Guid gameServerId,
        Guid lifecycleJobId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task ChangeCostOwnerAsync(
        Guid gameServerId,
        Guid costOwnerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
