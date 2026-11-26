using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// The persistent worker channel could not complete a request right now. The condition is transient and the caller
/// keeps its normal transient-failure handling, exactly as for a non-401 HTTP error.
/// </summary>
/// <remarks>
/// Messages never contain a credential or a frame body. The only server-provided text is the bounded, already
/// redacted error of an ack.
/// </remarks>
public sealed class WorkerChannelUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerChannelUnavailableException"/> class.
    /// </summary>
    /// <param name="message">A secret-free description of why the channel was unavailable.</param>
    public WorkerChannelUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerChannelUnavailableException"/> class for an ack that did
    /// not accept the request.
    /// </summary>
    /// <param name="message">A secret-free description of why the request was not accepted.</param>
    /// <param name="outcome">The outcome the control plane returned in its ack.</param>
    public WorkerChannelUnavailableException(string message, EWorkerChannelOutcome outcome)
        : base(message)
    {
        Outcome = outcome;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerChannelUnavailableException"/> class with an inner cause.
    /// </summary>
    /// <param name="message">A secret-free description of why the channel was unavailable.</param>
    /// <param name="innerException">The transport failure that caused the condition.</param>
    public WorkerChannelUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets the outcome the control plane returned when it refused the request.
    /// </summary>
    /// <value>
    /// <see cref="EWorkerChannelOutcome.Rejected"/> or <see cref="EWorkerChannelOutcome.Unavailable"/> for a refused
    /// request, or <see langword="null"/> when the failure was local (not connected, timed out, dropped).
    /// </value>
    public EWorkerChannelOutcome? Outcome { get; }
}
