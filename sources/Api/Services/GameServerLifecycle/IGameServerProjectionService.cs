using System.Security.Claims;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface IGameServerProjectionService
{
    IReadOnlyCollection<GameServerCapabilityDto> GetCapabilities(ClaimsPrincipal principal);

    Task<IReadOnlyCollection<GameServerDto>> GetServersAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<GameServerDto?> GetServerAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GameServerNotificationDto>> GetNotificationsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LifecycleJobDto>> GetJobsAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    LifecycleJobDto ProjectJob(LifecycleJob lifecycleJob, ClaimsPrincipal principal);
}
