namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>The persisted identity of one Counter-Strike 2 process started by this worker.</summary>
/// <param name="ServerId">The server the process belongs to.</param>
/// <param name="DeploymentId">The deployment the process belongs to.</param>
/// <param name="ProcessId">The operating-system process id.</param>
/// <param name="ProcessStartedAt">The process start time that guards against process id reuse.</param>
/// <param name="ExecutablePath">The executable the process was started from.</param>
/// <param name="GamePort">
/// The game port the process was started with, which is also its A2S_INFO query port. It is null for identities
/// that were persisted before the port was recorded.
/// </param>
public sealed record CounterStrike2ProcessIdentity(
    Guid ServerId,
    Guid DeploymentId,
    int ProcessId,
    DateTimeOffset ProcessStartedAt,
    string ExecutablePath,
    int? GamePort = null);
