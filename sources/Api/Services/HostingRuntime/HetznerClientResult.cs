namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal sealed record HetznerClientResult(
    ERuntimeProviderOutcome Outcome,
    string? Error)
{
    public static HetznerClientResult Success()
        => new(ERuntimeProviderOutcome.Succeeded, null);

    public static HetznerClientResult Failure(ERuntimeProviderOutcome outcome, string? error)
        => new(outcome, error);
}