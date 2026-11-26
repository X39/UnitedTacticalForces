namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Identifies the exchange a <see cref="WorkerChannelFrame"/> carries on the persistent worker channel.
/// </summary>
/// <remarks>
/// The integer values are stable and part of the worker protocol. Frames travel as JSON, where the type is written
/// as its string member name.
/// </remarks>
public enum EWorkerChannelFrameType
{
    /// <summary>A worker liveness heartbeat. The payload is a <see cref="WorkerHeartbeatRequest"/>.</summary>
    Heartbeat = 0,

    /// <summary>
    /// A request for the next command. The payload is a <see cref="WorkerCommandClaimRequest"/>; the ack payload is a
    /// <see cref="WorkerCommandClaimResponse"/> or JSON null when nothing is deliverable.
    /// </summary>
    Claim = 1,

    /// <summary>A command receipt report. The payload is a <see cref="WorkerCommandStageRequest"/>.</summary>
    ReportReceived = 2,

    /// <summary>A command running report. The payload is a <see cref="WorkerCommandProcessRequest"/>.</summary>
    ReportRunning = 3,

    /// <summary>A command success report. The payload is a <see cref="WorkerCommandProcessRequest"/>.</summary>
    ReportSucceeded = 4,

    /// <summary>A command failure report. The payload is a <see cref="WorkerCommandFailureRequest"/>.</summary>
    ReportFailed = 5,

    /// <summary>A worker metrics sample. The payload is owned by the metrics ingestion route.</summary>
    Metrics = 6,

    /// <summary>
    /// The answer to a request frame. It echoes the request <see cref="WorkerChannelFrame.Id"/> and carries an
    /// <see cref="EWorkerChannelOutcome"/>, an optional bounded error and an optional result payload.
    /// </summary>
    Ack = 7,

    /// <summary>
    /// A hint from the control plane that the worker should ask for work again. It carries no payload and never
    /// replaces the worker asking the protocol service for every decision.
    /// </summary>
    Wake = 8,
}
