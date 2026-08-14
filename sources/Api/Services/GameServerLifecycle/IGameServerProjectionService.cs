using System.Security.Claims;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

public interface IGameServerProjectionService
{
    Task<IReadOnlyCollection<GameServerCapabilityDto>> GetCapabilitiesAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GameServerDto>> GetServersAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<GameServerDto?> GetServerAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GameServerModPackOptionDto>> GetModPackOptionsAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GameServerModPackOptionDto>> GetCreationModPackOptionsAsync(
        string gameIdentifier,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GameServerCostOwnerOptionDto>> GetCreationCostOwnerOptionsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<StorageDemandDto?> GetStorageDemandAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<StorageDemandDto> EstimateStorageDemandAsync(
        DTO.Payloads.StorageDemandEstimatePayload payload,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GameServerNotificationDto>> GetNotificationsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LifecycleJobDto>> GetJobsAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<HostingOperationDto>> GetOperationsAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        int take = 80,
        CancellationToken cancellationToken = default);

    LifecycleJobDto ProjectJob(LifecycleJob lifecycleJob, ClaimsPrincipal principal);
}
