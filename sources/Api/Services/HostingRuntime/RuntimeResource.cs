namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeResource(
    string ProviderIdentifier,
    string ResourceIdentifier,
    bool IsAllocated,
    bool IsPotentiallyCostGenerating,
    DateTimeOffset ObservedAt,
    string ResourceType = "runtime",
    string? ProviderState = null,
    string? PublicAddress = null,
    RuntimeResourceAttribution? Attribution = null,
    IReadOnlyDictionary<string, string>? ProviderLabels = null,
    bool IsPlatformManaged = false,
    bool HasTrustworthyAttribution = false,
    ERuntimeResourceProvisioningReadiness ProvisioningReadiness = ERuntimeResourceProvisioningReadiness.Ready)
{
    /// <summary>Whether the resource is ready to participate in a publishable recovered graph.</summary>
    public bool IsProvisioningReady => ProvisioningReadiness is ERuntimeResourceProvisioningReadiness.Ready;

    /// <summary>Whether stable provider observation proves an unsafe relationship requiring fault teardown.</summary>
    public bool HasProvisioningConflict => ProvisioningReadiness is ERuntimeResourceProvisioningReadiness.Conflict;
}