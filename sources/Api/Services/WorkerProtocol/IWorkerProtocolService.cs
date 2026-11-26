using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

public interface IWorkerProtocolService
{
    Task<WorkerRegistrationResult> RegisterAsync(
        Guid workerId,
        WorkerRegistrationRequest request,
        string bootstrapCredential,
        CancellationToken cancellationToken = default);

    Task<WorkerCredentialRotationResult> RotateCredentialAsync(
        Guid workerId,
        WorkerCredentialRotationRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task HeartbeatAsync(
        Guid workerId,
        WorkerHeartbeatRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task<WorkerCommandClaimResponse?> ClaimCommandAsync(
        Guid workerId,
        WorkerCommandClaimRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task ReportReceivedAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandStageRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task ReportRunningAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandProcessRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task ReportSucceededAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandProcessRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    Task ReportFailedAsync(
        Guid workerId,
        Guid commandId,
        WorkerCommandFailureRequest request,
        string workerCredential,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a worker credential for the persistent worker channel and returns the worker's identity.
    /// </summary>
    /// <remarks>
    /// Authentication promotes a pending credential like every other protocol call, but it never touches
    /// <c>LastHeartbeatAt</c> and writes no audit record: worker liveness stays heartbeat-only, so connecting a socket
    /// cannot mask a worker that does not heartbeat.
    /// </remarks>
    /// <param name="workerId">The id of the worker presenting the credential.</param>
    /// <param name="deploymentId">The id of the deployment the worker claims to be bound to.</param>
    /// <param name="workerCredential">The bearer credential the worker presented.</param>
    /// <param name="cancellationToken">A token cancelling the verification.</param>
    /// <returns>The authenticated identity used to attribute every frame on the channel.</returns>
    /// <exception cref="WorkerProtocolAuthenticationException">
    /// The worker or deployment is unknown or finished, or the credential is wrong, expired or revoked.
    /// </exception>
    Task<AuthenticatedWorker> AuthenticateAsync(
        Guid workerId,
        Guid deploymentId,
        string workerCredential,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports whether a claim by this worker would hand out a command right now.
    /// </summary>
    /// <remarks>
    /// Read-only: it takes no transaction or row lock, tracks nothing and changes nothing. It is a hint for a frame
    /// whose sender the channel already authenticated, used to decide whether to Wake a worker; the claim itself
    /// remains the decision. It applies the same candidate query and cost-stop rule as the claim, so a command the
    /// claim would refuse never counts as deliverable.
    /// </remarks>
    /// <param name="workerId">The id of the authenticated worker.</param>
    /// <param name="deploymentId">The id of the deployment the worker is bound to.</param>
    /// <param name="cancellationToken">A token cancelling the check.</param>
    /// <returns>
    /// True when the worker is active and its next candidate command may execute; false when the worker is unknown,
    /// stopped or resolved, has no candidate, or its candidate is superseded.
    /// </returns>
    Task<bool> HasDeliverableCommandAsync(
        Guid workerId,
        Guid deploymentId,
        CancellationToken cancellationToken = default);
}
