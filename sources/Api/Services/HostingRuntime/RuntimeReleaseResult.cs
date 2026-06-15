namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeReleaseResult(
    ERuntimeProviderOutcome Outcome,
    string Stage,
    string? Error,
    bool IsVerifiedNonBillable)
{
    public bool Succeeded => Outcome is ERuntimeProviderOutcome.Succeeded;
}