using System.Collections.Concurrent;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// In-memory stand-in for <see cref="HetznerRuntimeProvider" /> that is used for manual UAT in the Development
/// environment only. It answers with the provider identifier <c>hetzner</c> so persisted servers keep resolving
/// their provider, but it never contacts the cloud, never executes a game process, and forgets its resources when
/// the API process stops. It is registered by <c>Program</c> only when the Development environment and
/// <see cref="ConfigurationKey" /> are both enabled.
/// </summary>
public sealed class EmulatedHetznerRuntimeProvider : IRuntimeProvider
{
    /// <summary>The configuration key that replaces the real Hetzner provider with this emulation.</summary>
    public const string ConfigurationKey = "Hosting:Providers:Hetzner:Emulated";

    /// <summary>
    /// Identifier prefix of server resources created by <see cref="UatFleetScenarioSeeder" />-style seed data. The
    /// emulation treats any unreleased identifier with this prefix as an allocated, healthy resource even though
    /// it did not create it in this process.
    /// </summary>
    public const string SeededServerPrefix = "emu-server-";

    /// <summary>
    /// Identifier prefix of resources whose state can never be confirmed: observation and release keep failing
    /// transiently, which is how a stop stays unconfirmed with possible cost.
    /// </summary>
    public const string UnconfirmedPrefix = "emu-stuck-";

    private const decimal HourlyCostEur = 0.10m;
    private readonly ConcurrentDictionary<string, byte> _released = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RuntimeResource> _resources = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string ProviderIdentifier => HetznerRuntimeProvider.Identifier;

    /// <inheritdoc />
    public RuntimeProviderCapabilities Capabilities { get; } = new(
        SupportsStableProvisioningIntent: true,
        SupportsProviderWideDiscovery: true,
        SupportsVerifiedRelease: true,
        SupportsPlatformManagedDanglingRelease: true,
        IsReady: true,
        EstimatedHourlyCostEur: HourlyCostEur,
        RequiredProvisionedResourceTypes:
        [
            HetznerRuntimeProvider.ServerResourceType,
            HetznerRuntimeProvider.PrimaryIpResourceType,
            HetznerRuntimeProvider.FirewallResourceType,
        ],
        Storage: new RuntimeStorageCapabilities(SupportsExternalBlockStorage: false),
        BillingUnit: HetznerRuntimeProvider.ProviderBillingUnit);

    /// <summary>
    /// Reports one CCX23-like profile. Start rejects a profile without included disk capacity, so the values
    /// must be complete even though nothing is ever allocated. The included disk is large enough for the default
    /// Arma 3 peak preparation demand (about 102 GiB), so a start of a server without a mod pack can be queued.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns>The single emulated compute profile.</returns>
    public Task<IReadOnlyCollection<RuntimeProfile>> GetProfilesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeProfile>>(
        [
            new RuntimeProfile(
                "default",
                "CCX23 (emulated)",
                "4 vCPU · 16 GiB RAM · 160 GiB disk (emulated, no cloud cost)",
                VirtualCpuCount: 4,
                MemoryGiB: 16m,
                DiskGiB: 160,
                EstimatedHourlyCostEur: HourlyCostEur,
                EstimatedMonthlyCostEur: 73m),
        ]);

    /// <inheritdoc />
    public Task<RuntimeProvisionResult> ProvisionAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var attribution = new RuntimeResourceAttribution(
            request.ServerId,
            request.DeploymentId,
            request.ProvisioningIntentId,
            request.WorkerId,
            request.ServiceIdentifier,
            request.ServiceOrdinal);
        var suffix = request.DeploymentId.ToString("N")[..8];
        var created = new List<RuntimeResource>
        {
            CreateResource($"emu-server-{suffix}", HetznerRuntimeProvider.ServerResourceType, attribution, true),
            CreateResource($"emu-ip-{suffix}", HetznerRuntimeProvider.PrimaryIpResourceType, attribution, false),
            CreateResource($"emu-fw-{suffix}", HetznerRuntimeProvider.FirewallResourceType, attribution, false),
        };
        foreach (var resource in created)
            _resources[resource.ResourceIdentifier] = resource;
        return Task.FromResult(new RuntimeProvisionResult(
            ERuntimeProviderOutcome.Succeeded,
            "emulated-provisioned",
            null,
            created[0],
            created));
    }

    /// <inheritdoc />
    public Task<RuntimeObservationResult> ObserveAsync(
        RuntimeObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var identifier = request.ResourceIdentifier;
        if (identifier.StartsWith(UnconfirmedPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult(new RuntimeObservationResult(
                ERuntimeProviderOutcome.TransientFailure,
                "emulated-unconfirmed",
                "The emulated provider cannot confirm this resource.",
                null));
        }
        if (_resources.TryGetValue(identifier, out var resource))
        {
            return Task.FromResult(new RuntimeObservationResult(
                ERuntimeProviderOutcome.Succeeded,
                "emulated-observed",
                null,
                resource with { ObservedAt = DateTimeOffset.UtcNow }));
        }
        if (identifier.StartsWith(SeededServerPrefix, StringComparison.Ordinal)
            && !_released.ContainsKey(identifier))
        {
            var attribution = new RuntimeResourceAttribution(
                request.ServerId,
                request.DeploymentId,
                request.ProvisioningIntentId,
                request.WorkerId,
                "game-server",
                0);
            return Task.FromResult(new RuntimeObservationResult(
                ERuntimeProviderOutcome.Succeeded,
                "emulated-observed-seeded",
                null,
                CreateResource(identifier, HetznerRuntimeProvider.ServerResourceType, attribution, true)));
        }
        return Task.FromResult(
            new RuntimeObservationResult(ERuntimeProviderOutcome.NotFound, "emulated-not-found", null, null));
    }

    /// <inheritdoc />
    public Task<RuntimeReleaseResult> ReleaseAsync(
        RuntimeReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ResourceIdentifier.StartsWith(UnconfirmedPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult(new RuntimeReleaseResult(
                ERuntimeProviderOutcome.TransientFailure,
                "emulated-unconfirmed",
                "The emulated provider cannot confirm this release.",
                IsVerifiedNonBillable: false));
        }
        _resources.TryRemove(request.ResourceIdentifier, out _);
        _released[request.ResourceIdentifier] = 0;
        return Task.FromResult(new RuntimeReleaseResult(
            ERuntimeProviderOutcome.Succeeded,
            "emulated-released",
            null,
            IsVerifiedNonBillable: true));
    }

    /// <inheritdoc />
    public Task<RuntimeReleaseResult> ReleaseDanglingAsync(
        RuntimeDanglingReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        _resources.TryRemove(request.ResourceIdentifier, out _);
        _released[request.ResourceIdentifier] = 0;
        return Task.FromResult(new RuntimeReleaseResult(
            ERuntimeProviderOutcome.Succeeded,
            "emulated-dangling-released",
            null,
            IsVerifiedNonBillable: true));
    }

    /// <inheritdoc />
    public Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new RuntimeDiscoveryResult(
            ERuntimeProviderOutcome.Succeeded,
            "emulated-discovered",
            null,
            _resources.Values.ToArray()));

    private static RuntimeResource CreateResource(
        string identifier,
        string resourceType,
        RuntimeResourceAttribution attribution,
        bool isServer)
        => new(
            HetznerRuntimeProvider.Identifier,
            identifier,
            IsAllocated: true,
            IsPotentiallyCostGenerating: true,
            ObservedAt: DateTimeOffset.UtcNow,
            ResourceType: resourceType,
            ProviderState: "running",
            PublicAddress: isServer ? "203.0.113.10" : null,
            Attribution: attribution,
            ProviderLabels: new Dictionary<string, string>
            {
                [HetznerRuntimeProvider.ManagedLabel] = "true",
            },
            IsPlatformManaged: true,
            HasTrustworthyAttribution: true,
            ProvisioningReadiness: ERuntimeResourceProvisioningReadiness.Ready,
            OperationalState: ERuntimeResourceOperationalState.Operational,
            EstimatedHourlyCostEur: isServer ? HourlyCostEur : null);
}
