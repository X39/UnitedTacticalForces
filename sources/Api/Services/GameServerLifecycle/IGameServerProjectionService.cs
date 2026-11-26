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

    /// <summary>
    /// Lists the cost owners the caller may transfer an existing server to: every owner an administrator may use,
    /// otherwise the caller's own owner and the owners granted through an exact cost-owner use claim. The server's
    /// current owner is excluded and frozen owners are included and flagged.
    /// </summary>
    /// <param name="gameServerId">The identity of the server whose owner would change.</param>
    /// <param name="search">The optional display-name search text; blank text matches every owner.</param>
    /// <param name="take">The maximum number of owners to return.</param>
    /// <param name="principal">The authenticated caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The matching owners, the caller's own owner first, then by display name and identity.
    /// </returns>
    /// <exception cref="HostingForbiddenException">The caller may not modify the server.</exception>
    /// <exception cref="HostingNotFoundException">The server does not exist or is archived.</exception>
    Task<IReadOnlyCollection<GameServerCostOwnerOptionDto>> GetCostOwnerOptionsAsync(
        Guid gameServerId,
        string? search,
        int take,
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
