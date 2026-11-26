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

    /// <summary>
    /// Queues a start or stop job on behalf of the control plane, without a requesting user.
    /// </summary>
    /// <remarks>
    /// This method never authorizes a user: the caller proves consent and policy before calling it. It applies the
    /// same lifecycle preconditions as <see cref="EnqueueActionAsync" />, never creates a terminate job and joins the
    /// transaction the caller already opened, so the caller's own claim and the job commit or roll back together.
    /// </remarks>
    /// <param name="request">The automated request naming the server, the action, the trigger and the instant.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task resolving to the queued job.</returns>
    /// <exception cref="HostingValidationException">
    /// The action is neither start nor stop, the trigger is manual, or the game does not expose the action.
    /// </exception>
    /// <exception cref="HostingNotFoundException">The server does not exist or is archived.</exception>
    /// <exception cref="HostingConflictException">A lifecycle precondition is not met.</exception>
    Task<LifecycleJob> EnqueueAutomatedActionAsync(
        AutomatedLifecycleRequest request,
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

    Task ChangeRuntimeProfileAsync(
        Guid gameServerId,
        string runtimeProfileIdentifier,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task ChangeRuntimeStorageAsync(
        Guid gameServerId,
        X39.UnitedTacticalForces.Hosting.Contracts.ERuntimeStorageStrategy strategy,
        long? externalStorageSizeGiB,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects or clears the mod pack of a game server for its next preparation or update.
    /// </summary>
    /// <param name="gameServerId">The game server to change.</param>
    /// <param name="modPackDefinitionId">The mod pack definition to select, or <see langword="null"/> to clear it.</param>
    /// <param name="principal">The caller, who needs the server modify permission.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes once the selection was saved; re-selecting the saved pack does nothing.</returns>
    /// <exception cref="HostingForbiddenException">The caller lacks the server modify permission.</exception>
    /// <exception cref="HostingNotFoundException">The server does not exist or is archived.</exception>
    /// <exception cref="HostingValidationException">
    /// The pack is inactive, belongs to another game, has no active revision or cannot be resolved.
    /// </exception>
    Task ChangeModPackAsync(
        Guid gameServerId,
        long? modPackDefinitionId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task ChangeContentSelectionAsync(
        Guid gameServerId,
        long? terrainId,
        Guid? terrainVersionId,
        Guid? scenarioId,
        Guid? scenarioVersionId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
