using X39.UnitedTacticalForces.Api.Services.HostingRuntime;

namespace Api.Tests.GameServerHosting;

internal sealed class TestRuntimeProvider : IRuntimeProvider
{
    public const string Identifier = "test-cloud";

    public string ProviderIdentifier => Identifier;

    public RuntimeProviderCapabilities Capabilities { get; } = new(true, true, true);

    public Task<RuntimeProvisionResult> ProvisionAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Runtime execution is outside these control-plane tests.");

    public Task<RuntimeObservationResult> ObserveAsync(
        RuntimeObservationRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Runtime execution is outside these control-plane tests.");

    public Task<RuntimeReleaseResult> ReleaseAsync(
        RuntimeReleaseRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Runtime execution is outside these control-plane tests.");

    public Task<RuntimeReleaseResult> ReleaseDanglingAsync(
        RuntimeDanglingReleaseRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Runtime execution is outside these control-plane tests.");

    public Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new RuntimeDiscoveryResult(
            ERuntimeProviderOutcome.Succeeded,
            "test-discovery",
            null,
            []));
}