namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeDiscoveryResult(
    ERuntimeProviderOutcome Outcome,
    string Stage,
    string? Error,
    IReadOnlyList<RuntimeResource> Resources)
{
    public bool Succeeded => Outcome is ERuntimeProviderOutcome.Succeeded;
}