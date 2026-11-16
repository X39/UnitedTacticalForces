using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Records the secret-free phase of a colocated operation before its process side effects.</summary>
/// <param name="DeploymentId">The deployment whose processes are controlled.</param>
/// <param name="CommandId">The stable command that owns this operation.</param>
/// <param name="PayloadHash">The digest binding recovery to the original payload.</param>
/// <param name="Phase">The last durable preparation or launch boundary.</param>
/// <param name="ResumeRunning">Whether maintenance must restore the running process group.</param>
/// <param name="Primary">The primary incarnation recorded after launch.</param>
/// <param name="Auxiliary">The dependent incarnation recorded after launch.</param>
/// <param name="ContentVersions">Concrete content versions shared by both workloads.</param>
internal sealed record Arma3SupervisionState(
    Guid DeploymentId,
    Guid CommandId,
    string PayloadHash,
    EArma3SupervisionPhase Phase,
    bool ResumeRunning,
    IReadOnlyCollection<WorkerObservedContentVersion> ContentVersions,
    WorkerWorkloadObservation? Primary = null,
    WorkerWorkloadObservation? Auxiliary = null);
