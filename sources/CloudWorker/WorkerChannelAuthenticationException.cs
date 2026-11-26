namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// The control plane rejected the worker credential on the persistent channel and the credential has not changed
/// since, so reconnecting cannot succeed.
/// </summary>
/// <remarks>
/// The runtime treats this like an HTTP 401: it is fatal until a replacement credential exists. Messages never
/// contain the credential.
/// </remarks>
public sealed class WorkerChannelAuthenticationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerChannelAuthenticationException"/> class.
    /// </summary>
    public WorkerChannelAuthenticationException()
        : base("The control plane rejected the worker credential.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerChannelAuthenticationException"/> class.
    /// </summary>
    /// <param name="message">A secret-free description of the rejection.</param>
    public WorkerChannelAuthenticationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerChannelAuthenticationException"/> class with an inner
    /// cause.
    /// </summary>
    /// <param name="message">A secret-free description of the rejection.</param>
    /// <param name="innerException">The transport failure that revealed the rejection.</param>
    public WorkerChannelAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
