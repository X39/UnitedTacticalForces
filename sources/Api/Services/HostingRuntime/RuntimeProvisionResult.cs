namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeProvisionResult(
    ERuntimeProviderOutcome Outcome,
    string Stage,
    string? Error,
    RuntimeResource? Resource,
    IReadOnlyList<RuntimeResource>? Resources = null)
{
    public bool Succeeded => Outcome is ERuntimeProviderOutcome.Succeeded;

    /// <summary>
    /// Every provider resource allocated for the provisioning intent. <see cref="Resource"/> remains the
    /// deployment's primary runtime resource for compatibility with providers that allocate only one resource.
    /// </summary>
    public IReadOnlyList<RuntimeResource> ProvisionedResources
        => Resources ?? (Resource is null ? [] : [Resource]);
}