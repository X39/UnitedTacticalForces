namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Provider-neutral boundary for allocating one deployment's execution resource.
/// </summary>
public interface IRuntimeProvider
{
    string ProviderIdentifier { get; }

    RuntimeProviderCapabilities Capabilities { get; }

    Task<RuntimeProvisionResult> ProvisionAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken = default);

    Task<RuntimeObservationResult> ObserveAsync(
        RuntimeObservationRequest request,
        CancellationToken cancellationToken = default);

    Task<RuntimeReleaseResult> ReleaseAsync(
        RuntimeReleaseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a discovered UTF-managed resource that cannot be linked to a valid deployment. Implementations
    /// must independently verify their platform-management marker before performing a destructive action.
    /// </summary>
    Task<RuntimeReleaseResult> ReleaseDanglingAsync(
        RuntimeDanglingReleaseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers every platform-managed resource in the configured provider scope. This is intentionally
    /// independent of a deployment so reconciliation can find late and dangling resources.
    /// </summary>
    Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);
}