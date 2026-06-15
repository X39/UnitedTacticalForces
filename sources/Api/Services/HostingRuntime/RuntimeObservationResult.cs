namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeObservationResult(
    ERuntimeProviderOutcome Outcome,
    string Stage,
    string? Error,
    RuntimeResource? Resource)
{
    /// <summary>
    /// A provider-confirmed absence is a successful observation with a typed NotFound outcome.
    /// </summary>
    public bool Succeeded => Outcome is ERuntimeProviderOutcome.Succeeded or ERuntimeProviderOutcome.NotFound;
}