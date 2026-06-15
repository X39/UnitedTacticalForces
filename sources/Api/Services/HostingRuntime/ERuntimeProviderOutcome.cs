namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public enum ERuntimeProviderOutcome
{
    Succeeded,
    NotFound,
    Unknown,
    TransientFailure,
    PermanentFailure,
}