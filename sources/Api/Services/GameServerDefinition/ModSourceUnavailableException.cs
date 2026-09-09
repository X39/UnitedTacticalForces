namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Signals that source metadata could not be obtained due to a retryable provider failure.</summary>
public sealed class ModSourceUnavailableException : Exception
{
    /// <summary>Initializes a provider-unavailable exception.</summary>
    /// <param name="providerIdentifier">Provider that failed.</param>
    /// <param name="innerException">Underlying transport failure.</param>
    public ModSourceUnavailableException(string providerIdentifier, Exception innerException)
        : base($"The '{providerIdentifier}' source provider is temporarily unavailable.", innerException)
    {
        ProviderIdentifier = providerIdentifier;
    }

    /// <summary>Gets the failed provider identifier.</summary>
    public string ProviderIdentifier { get; }
}
