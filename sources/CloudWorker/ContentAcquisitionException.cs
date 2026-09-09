namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Describes a safe content-provider acquisition failure.</summary>
public sealed class ContentAcquisitionException : Exception
{
    /// <summary>Initializes a provider acquisition failure.</summary>
    /// <param name="errorCode">Stable secret-free error code.</param>
    /// <param name="message">Secret-free diagnostic message.</param>
    /// <param name="retryable">Whether a later command may succeed without changing its source.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public ContentAcquisitionException(
        string errorCode,
        string message,
        bool retryable,
        Exception? innerException = null) : base(message, innerException)
    {
        ErrorCode = errorCode;
        Retryable = retryable;
    }

    /// <summary>Gets the stable secret-free error code.</summary>
    public string ErrorCode { get; }

    /// <summary>Gets whether retrying the provider operation may succeed.</summary>
    public bool Retryable { get; }
}
