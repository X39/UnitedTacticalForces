namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Describes how the control plane resolved a request frame on the persistent worker channel.
/// </summary>
/// <remarks>
/// The integer values are stable and part of the worker protocol. Frames travel as JSON, where the outcome is written
/// as its string member name.
/// </remarks>
public enum EWorkerChannelOutcome
{
    /// <summary>The request was executed and its effect is persisted.</summary>
    Accepted = 0,

    /// <summary>
    /// The request was refused because the command was superseded or cost-stop state forbids execution. This is the
    /// channel equivalent of an HTTP 409 conflict.
    /// </summary>
    Superseded = 1,

    /// <summary>The request was rejected as invalid, unauthenticated or unknown and must not be retried as is.</summary>
    Rejected = 2,

    /// <summary>The control plane could not process the request right now. The worker may retry it.</summary>
    Unavailable = 3,
}
