using System.Collections.Concurrent;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Production runtime provider for one dedicated Hetzner server, explicit Primary IPv4, and firewall per deployment.
/// </summary>
[Singleton<HetznerRuntimeProvider, IRuntimeProvider>]
public sealed class HetznerRuntimeProvider(
    HetznerCloudClient client,
    IHetznerRuntimeOptionsProvider optionsProvider,
    ILogger<HetznerRuntimeProvider> logger) : IRuntimeProvider
{
    public const string Identifier = "hetzner";
    internal const string ManagedLabel = "utf-managed";
    internal const string ServerLabel = "utf-server";
    internal const string DeploymentLabel = "utf-deployment";
    internal const string IntentLabel = "utf-intent";
    internal const string WorkerLabel = "utf-worker";
    internal const string ResourceKindLabel = "utf-resource-kind";
    internal const string ServerResourceType = "server";
    internal const string PrimaryIpResourceType = "primary-ip";
    internal const string FirewallResourceType = "firewall";
    private const string ManagedLabelValue = "true";
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _provisioningLocks = new();

    public string ProviderIdentifier => Identifier;

    public RuntimeProviderCapabilities Capabilities
    {
        get
        {
            var failure = optionsProvider.Value.ValidateProvisioning();
            return new RuntimeProviderCapabilities(
                SupportsStableProvisioningIntent: true,
                SupportsProviderWideDiscovery: true,
                SupportsVerifiedRelease: true,
                SupportsPlatformManagedDanglingRelease: true,
                IsReady: failure is null,
                ReadinessFailure: failure,
                EstimatedHourlyCostEur: failure is null
                    ? optionsProvider.Value.EstimatedHourlyCostEur
                    : null,
                RequiredProvisionedResourceTypes:
                [
                    ServerResourceType,
                    PrimaryIpResourceType,
                    FirewallResourceType,
                ]);
        }
    }

    public async Task<RuntimeProvisionResult> ProvisionAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateProvisionRequest(request);
        if (validationFailure is not null)
        {
            return new RuntimeProvisionResult(
                ERuntimeProviderOutcome.PermanentFailure,
                "provision-validation",
                validationFailure,
                null);
        }

        var gate = _provisioningLocks.GetOrAdd(request.ProvisioningIntentId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ProvisionCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<RuntimeProvisionResult> ProvisionCoreAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken)
    {
        var intent = await FindIntentResourcesAsync(request, cancellationToken).ConfigureAwait(false);
        if (intent.Outcome is not ERuntimeProviderOutcome.Succeeded)
            return ProvisionFailure(intent.Outcome, "provision-discovery-failed", intent.Error, intent);
        var existingResourceCount = (intent.Server is null ? 0 : 1)
                                    + (intent.PrimaryIp is null ? 0 : 1)
                                    + (intent.Firewall is null ? 0 : 1);
        if (existingResourceCount is > 0 and < 3)
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.Unknown,
                "provision-incomplete-resource-graph",
                "The provisioning intent has a partial Hetzner resource graph. No further provider mutation is safe "
                + "until reconciliation observes the complete graph or an operator resolves the intent.",
                intent);
        }

        var created = false;
        var recoveredAfterAmbiguousCreate = false;
        var firewall = intent.Firewall;
        if (firewall is null)
        {
            var labels = BuildLabels(request, FirewallResourceType);
            var result = await client.CreateFirewallAsync(
                    new HetznerFirewallCreateRequest(
                        $"utf-{request.DeploymentId:N}",
                        request.InboundNetworkRequirements!
                            .Select(ToFirewallRule)
                            .ToArray(),
                        labels),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.Outcome is ERuntimeProviderOutcome.Succeeded
                && result.Value is not null
                && HasExpectedAttribution(result.Value.Labels, request, FirewallResourceType))
            {
                firewall = result.Value;
                created = true;
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Unknown)
            {
                var recovered = await FindIntentFirewallAsync(request, cancellationToken).ConfigureAwait(false);
                if (recovered.Outcome is ERuntimeProviderOutcome.Succeeded && recovered.Value is not null)
                {
                    firewall = recovered.Value;
                    recoveredAfterAmbiguousCreate = true;
                }
                else
                {
                    return ProvisionFailure(
                        ERuntimeProviderOutcome.Unknown,
                        "firewall-create-outcome-unknown",
                        "Hetzner firewall creation has an unknown outcome; provider-wide reconciliation must continue.",
                        intent with { Firewall = recovered.Value });
                }
            }
            else
            {
                return ProvisionFailure(result.Outcome, "firewall-create-failed", result.Error, intent);
            }
        }

        var primaryIp = intent.PrimaryIp;
        if (primaryIp is null)
        {
            var labels = BuildLabels(request, PrimaryIpResourceType);
            var result = await client.CreatePrimaryIpAsync(
                    new HetznerPrimaryIpCreateRequest(
                        $"utf-{request.DeploymentId:N}",
                        Type: "ipv4",
                        optionsProvider.Value.Location.Trim(),
                        AutoDelete: true,
                        labels),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.Outcome is ERuntimeProviderOutcome.Succeeded
                && result.Value is not null
                && HasExpectedAttribution(result.Value.Labels, request, PrimaryIpResourceType))
            {
                primaryIp = result.Value;
                created = true;
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Unknown)
            {
                var recovered = await FindIntentPrimaryIpAsync(request, cancellationToken).ConfigureAwait(false);
                if (recovered.Outcome is ERuntimeProviderOutcome.Succeeded && recovered.Value is not null)
                {
                    primaryIp = recovered.Value;
                    recoveredAfterAmbiguousCreate = true;
                }
                else
                {
                    return ProvisionFailure(
                        ERuntimeProviderOutcome.Unknown,
                        "primary-ip-create-outcome-unknown",
                        "Hetzner Primary IP creation has an unknown outcome; provider-wide reconciliation must continue.",
                        intent with { Firewall = firewall, PrimaryIp = recovered.Value });
                }
            }
            else
            {
                return ProvisionFailure(
                    result.Outcome,
                    "primary-ip-create-failed",
                    result.Error,
                    intent with { Firewall = firewall });
            }
        }

        if (!primaryIp.AutoDelete || !string.Equals(primaryIp.Type, "ipv4", StringComparison.OrdinalIgnoreCase))
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.Unknown,
                "primary-ip-safety-mismatch",
                "The attributed Hetzner Primary IP does not have the required IPv4 auto-delete safety configuration.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp });
        }

        var server = intent.Server;
        if (server is null)
        {
            var options = optionsProvider.Value;
            var labels = BuildLabels(request, ServerResourceType);
            var result = await client.CreateServerAsync(
                    new HetznerServerCreateRequest(
                        $"utf-{request.DeploymentId:N}",
                        options.ServerType.Trim(),
                        options.Image.Trim(),
                        options.Location.Trim(),
                        StartAfterCreate: true,
                        request.CloudInit!,
                        new HetznerServerPublicNetRequest(
                            EnableIpv4: true,
                            EnableIpv6: false,
                            Ipv4: primaryIp.Id),
                        [new HetznerServerFirewallRequest(firewall.Id)],
                        labels),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.Outcome is ERuntimeProviderOutcome.Succeeded
                && result.Value is not null
                && HasExpectedAttribution(result.Value.Labels, request, ServerResourceType))
            {
                server = result.Value;
                created = true;
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Unknown)
            {
                var recovered = await FindIntentServerAsync(request, cancellationToken).ConfigureAwait(false);
                if (recovered.Outcome is ERuntimeProviderOutcome.Succeeded && recovered.Value is not null)
                {
                    server = recovered.Value;
                    recoveredAfterAmbiguousCreate = true;
                }
                else
                {
                    return ProvisionFailure(
                        ERuntimeProviderOutcome.Unknown,
                        "server-create-outcome-unknown",
                        "Hetzner server creation has an unknown outcome; provider-wide reconciliation must continue.",
                        intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = recovered.Value });
                }
            }
            else
            {
                return ProvisionFailure(
                    result.Outcome,
                    "server-create-failed",
                    result.Error,
                    intent with { Firewall = firewall, PrimaryIp = primaryIp });
            }
        }

        var verifiedIp = await client.GetPrimaryIpAsync(primaryIp.Id, cancellationToken).ConfigureAwait(false);
        if (verifiedIp.Outcome is not ERuntimeProviderOutcome.Succeeded || verifiedIp.Value is null)
        {
            return ProvisionFailure(
                verifiedIp.Outcome,
                "primary-ip-assignment-verification-failed",
                verifiedIp.Error,
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }
        primaryIp = verifiedIp.Value;
        if (!HasExpectedAttribution(primaryIp.Labels, request, PrimaryIpResourceType)
            || primaryIp.AssigneeId != server.Id
            || !string.Equals(primaryIp.AssigneeType, "server", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(primaryIp.Type, "ipv4", StringComparison.OrdinalIgnoreCase)
            || !primaryIp.AutoDelete)
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.Unknown,
                "primary-ip-assignment-unverified",
                "The explicit Hetzner Primary IP assignment and auto-delete state could not be verified.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }

        var verifiedFirewall = await client.GetFirewallAsync(firewall.Id, cancellationToken).ConfigureAwait(false);
        if (verifiedFirewall.Outcome is not ERuntimeProviderOutcome.Succeeded || verifiedFirewall.Value is null)
        {
            return ProvisionFailure(
                verifiedFirewall.Outcome,
                "firewall-attachment-verification-failed",
                verifiedFirewall.Error,
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }
        firewall = verifiedFirewall.Value;
        if (!HasExpectedAttribution(firewall.Labels, request, FirewallResourceType)
            || firewall.AppliedToCount != 1
            || firewall.AppliedServerIds.Count != 1
            || firewall.AppliedServerIds[0] != server.Id)
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.Unknown,
                "firewall-attachment-unverified",
                "The attributed Hetzner firewall is not attached exclusively to the attributed server.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }

        var resources = new RuntimeResource[]
        {
            CreateResource(server, primaryIp.Ip),
            CreateResource(primaryIp),
            CreateResource(firewall),
        };
        var stage = recoveredAfterAmbiguousCreate
            ? "recovered-after-ambiguous-create"
            : created
                ? "provisioned"
                : "recovered-existing";
        logger.LogInformation(
            "Verified Hetzner resource graph for deployment {DeploymentId} and intent {IntentId}",
            request.DeploymentId,
            request.ProvisioningIntentId);
        return new RuntimeProvisionResult(
            ERuntimeProviderOutcome.Succeeded,
            stage,
            null,
            resources[0],
            resources);
    }

    public async Task<RuntimeObservationResult> ObserveAsync(
        RuntimeObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var configurationFailure = optionsProvider.Value.ValidateConnection();
        if (configurationFailure is not null)
            return ObservationFailure(ERuntimeProviderOutcome.PermanentFailure, "observe-configuration", configurationFailure);
        if (!TryParseResourceIdentifier(request.ResourceIdentifier, out var kind, out var providerResourceId))
        {
            return ObservationFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "observe-validation",
                "The Hetzner resource identity is invalid.");
        }

        return kind switch
        {
            EHetznerResourceKind.Server => await ObserveServerAsync(request, providerResourceId, cancellationToken)
                .ConfigureAwait(false),
            EHetznerResourceKind.PrimaryIp => await ObservePrimaryIpAsync(request, providerResourceId, cancellationToken)
                .ConfigureAwait(false),
            EHetznerResourceKind.Firewall => await ObserveFirewallAsync(request, providerResourceId, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public async Task<RuntimeReleaseResult> ReleaseAsync(
        RuntimeReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var configurationFailure = optionsProvider.Value.ValidateConnection();
        if (configurationFailure is not null)
            return ReleaseFailure(ERuntimeProviderOutcome.PermanentFailure, "release-configuration", configurationFailure);
        if (!TryParseResourceIdentifier(request.ResourceIdentifier, out var kind, out var providerResourceId))
        {
            return ReleaseFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "release-validation",
                "The Hetzner resource identity is invalid.");
        }

        return kind switch
        {
            EHetznerResourceKind.Server => await ReleaseServerAsync(
                    providerResourceId,
                    q => HasExpectedAttribution(q.Labels, request, ServerResourceType),
                    cancellationToken)
                .ConfigureAwait(false),
            EHetznerResourceKind.PrimaryIp => await ReleasePrimaryIpAsync(
                    providerResourceId,
                    q => HasExpectedAttribution(q.Labels, request, PrimaryIpResourceType),
                    (server, primaryIp) => HasExpectedAttribution(
                        server.Labels,
                        request,
                        ServerResourceType)
                                           && HasMatchingAttribution(primaryIp.Labels, server.Labels),
                    cancellationToken)
                .ConfigureAwait(false),
            EHetznerResourceKind.Firewall => await ReleaseFirewallAsync(
                    providerResourceId,
                    q => HasExpectedAttribution(q.Labels, request, FirewallResourceType),
                    cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public async Task<RuntimeReleaseResult> ReleaseDanglingAsync(
        RuntimeDanglingReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var configurationFailure = optionsProvider.Value.ValidateConnection();
        if (configurationFailure is not null)
            return ReleaseFailure(ERuntimeProviderOutcome.PermanentFailure, "release-configuration", configurationFailure);
        if (!TryParseResourceIdentifier(request.ResourceIdentifier, out var kind, out var providerResourceId)
            || !string.Equals(ResourceType(kind), request.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return ReleaseFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "release-validation",
                "The discovered Hetzner resource identity or type is invalid.");
        }

        return kind switch
        {
            EHetznerResourceKind.Server => await ReleaseServerAsync(
                    providerResourceId,
                    q => IsSafelyManaged(q.Labels, ServerResourceType),
                    cancellationToken)
                .ConfigureAwait(false),
            EHetznerResourceKind.PrimaryIp => await ReleasePrimaryIpAsync(
                    providerResourceId,
                    q => IsSafelyManaged(q.Labels, PrimaryIpResourceType),
                    (server, primaryIp) => IsSafelyManaged(server.Labels, ServerResourceType)
                                           && HasMatchingAttribution(primaryIp.Labels, server.Labels),
                    cancellationToken)
                .ConfigureAwait(false),
            EHetznerResourceKind.Firewall => await ReleaseFirewallAsync(
                    providerResourceId,
                    q => IsSafelyManaged(q.Labels, FirewallResourceType),
                    cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public async Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var configurationFailure = optionsProvider.Value.ValidateConnection();
        if (configurationFailure is not null)
        {
            return new RuntimeDiscoveryResult(
                ERuntimeProviderOutcome.PermanentFailure,
                "discovery-configuration",
                configurationFailure,
                []);
        }

        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ManagedLabel] = ManagedLabelValue,
        };
        var servers = await client.GetServersAsync(labels, cancellationToken).ConfigureAwait(false);
        if (!TryDiscoveryValue(servers, out var discoveredServers, out var failure))
            return failure!;
        var primaryIps = await client.GetPrimaryIpsAsync(labels, cancellationToken).ConfigureAwait(false);
        if (!TryDiscoveryValue(primaryIps, out var discoveredPrimaryIps, out failure))
            return failure!;
        var firewalls = await client.GetFirewallsAsync(labels, cancellationToken).ConfigureAwait(false);
        if (!TryDiscoveryValue(firewalls, out var discoveredFirewalls, out failure))
            return failure!;
        var providerServers = discoveredServers!;

        return new RuntimeDiscoveryResult(
            ERuntimeProviderOutcome.Succeeded,
            "discovered",
            null,
            [
                .. providerServers.Select(q => CreateResource(q)),
                .. discoveredPrimaryIps!.Select(q => CreateResource(
                    q,
                    GetPrimaryIpProvisioningReadiness(q, providerServers))),
                .. discoveredFirewalls!.Select(q => CreateResource(
                    q,
                    GetFirewallProvisioningReadiness(q, providerServers))),
            ]);
    }

    internal static string ResourceIdentifier(long providerServerId) => ServerResourceIdentifier(providerServerId);

    internal static string ServerResourceIdentifier(long providerServerId) => $"server:{providerServerId}";

    internal static string PrimaryIpResourceIdentifier(long providerPrimaryIpId) => $"primary-ip:{providerPrimaryIpId}";

    internal static string FirewallResourceIdentifier(long providerFirewallId) => $"firewall:{providerFirewallId}";

    private async Task<RuntimeObservationResult> ObserveServerAsync(
        RuntimeObservationRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var observation = await client.GetServerAsync(id, cancellationToken).ConfigureAwait(false);
        if (!TryObservedValue(observation, out var value, out var failure))
            return failure!;
        if (!HasExpectedAttribution(value!.Labels, request, ServerResourceType))
            return UnattributedObservation();
        return Observed(CreateResource(value));
    }

    private async Task<RuntimeObservationResult> ObservePrimaryIpAsync(
        RuntimeObservationRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var observation = await client.GetPrimaryIpAsync(id, cancellationToken).ConfigureAwait(false);
        if (!TryObservedValue(observation, out var value, out var failure))
            return failure!;
        if (!HasExpectedAttribution(value!.Labels, request, PrimaryIpResourceType))
            return UnattributedObservation();
        return Observed(CreateResource(value));
    }

    private async Task<RuntimeObservationResult> ObserveFirewallAsync(
        RuntimeObservationRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var observation = await client.GetFirewallAsync(id, cancellationToken).ConfigureAwait(false);
        if (!TryObservedValue(observation, out var value, out var failure))
            return failure!;
        if (!HasExpectedAttribution(value!.Labels, request, FirewallResourceType))
            return UnattributedObservation();
        return Observed(CreateResource(value));
    }

    private async Task<RuntimeReleaseResult> ReleaseServerAsync(
        long id,
        Func<HetznerServer, bool> isAttributed,
        CancellationToken cancellationToken)
    {
        var observed = await client.GetServerAsync(id, cancellationToken).ConfigureAwait(false);
        if (observed.Outcome is ERuntimeProviderOutcome.NotFound)
            return VerifiedReleasedResult("released-already-absent");
        if (observed.Outcome is not ERuntimeProviderOutcome.Succeeded || observed.Value is null)
            return ReleaseFailure(observed.Outcome, "release-observation-failed", observed.Error);
        if (!isAttributed(observed.Value))
            return UnattributedRelease();

        var deleted = await client.DeleteServerAsync(id, cancellationToken).ConfigureAwait(false);
        if (deleted.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.NotFound)
            return ReleaseFailure(deleted.Outcome, "release-delete-failed", deleted.Error);
        var verification = await client.GetServerAsync(id, cancellationToken).ConfigureAwait(false);
        return VerifyRelease(verification.Outcome, verification.Error, ServerResourceType, id);
    }

    private async Task<RuntimeReleaseResult> ReleasePrimaryIpAsync(
        long id,
        Func<HetznerPrimaryIp, bool> isAttributed,
        Func<HetznerServer, HetznerPrimaryIp, bool> isAssigneeSafelyAttributed,
        CancellationToken cancellationToken)
    {
        var observed = await client.GetPrimaryIpAsync(id, cancellationToken).ConfigureAwait(false);
        if (observed.Outcome is ERuntimeProviderOutcome.NotFound)
            return VerifiedReleasedResult("released-already-absent");
        if (observed.Outcome is not ERuntimeProviderOutcome.Succeeded || observed.Value is null)
            return ReleaseFailure(observed.Outcome, "release-observation-failed", observed.Error);
        if (!isAttributed(observed.Value))
            return UnattributedRelease();

        if (observed.Value.AssigneeId.HasValue)
        {
            var server = await client.GetServerAsync(observed.Value.AssigneeId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (server.Outcome is ERuntimeProviderOutcome.Succeeded
                && server.Value is not null)
            {
                if (!isAssigneeSafelyAttributed(server.Value, observed.Value))
                {
                    return ReleaseFailure(
                        ERuntimeProviderOutcome.Unknown,
                        "release-assignee-attribution-conflict",
                        "The Primary IP is assigned to a server whose UTF attribution does not match; automatic "
                        + "power-off and unassignment were refused.");
                }
                if (!string.Equals(server.Value.Status, "off", StringComparison.OrdinalIgnoreCase))
                {
                    var powerOff = await client.PowerOffServerAsync(server.Value.Id, cancellationToken)
                        .ConfigureAwait(false);
                    if (powerOff.Outcome is not ERuntimeProviderOutcome.Succeeded
                        and not ERuntimeProviderOutcome.Unknown)
                    {
                        return ReleaseFailure(powerOff.Outcome, "release-poweroff-failed", powerOff.Error);
                    }
                    return ReleaseFailure(
                        ERuntimeProviderOutcome.TransientFailure,
                        "release-poweroff-pending",
                        "The Primary IP remains assigned while its server powers off.");
                }
            }
            if (server.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.NotFound)
                return ReleaseFailure(server.Outcome, "release-assignee-observation-failed", server.Error);

            var unassign = await client.UnassignPrimaryIpAsync(id, cancellationToken).ConfigureAwait(false);
            if (unassign.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.Unknown)
                return ReleaseFailure(unassign.Outcome, "release-unassign-failed", unassign.Error);
            var unassigned = await client.GetPrimaryIpAsync(id, cancellationToken).ConfigureAwait(false);
            if (unassigned.Outcome is ERuntimeProviderOutcome.NotFound)
                return VerifiedReleasedResult("released-during-unassign");
            if (unassigned.Outcome is not ERuntimeProviderOutcome.Succeeded || unassigned.Value is null)
                return ReleaseFailure(unassigned.Outcome, "release-unassign-verification-failed", unassigned.Error);
            if (unassigned.Value.AssigneeId.HasValue)
            {
                return ReleaseFailure(
                    ERuntimeProviderOutcome.TransientFailure,
                    "release-unassign-pending",
                    "The Hetzner Primary IP is still assigned after requesting unassignment.");
            }
        }

        var deleted = await client.DeletePrimaryIpAsync(id, cancellationToken).ConfigureAwait(false);
        if (deleted.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.NotFound)
            return ReleaseFailure(deleted.Outcome, "release-delete-failed", deleted.Error);
        var verification = await client.GetPrimaryIpAsync(id, cancellationToken).ConfigureAwait(false);
        return VerifyRelease(verification.Outcome, verification.Error, PrimaryIpResourceType, id);
    }

    private async Task<RuntimeReleaseResult> ReleaseFirewallAsync(
        long id,
        Func<HetznerFirewall, bool> isAttributed,
        CancellationToken cancellationToken)
    {
        var observed = await client.GetFirewallAsync(id, cancellationToken).ConfigureAwait(false);
        if (observed.Outcome is ERuntimeProviderOutcome.NotFound)
            return VerifiedReleasedResult("released-already-absent");
        if (observed.Outcome is not ERuntimeProviderOutcome.Succeeded || observed.Value is null)
            return ReleaseFailure(observed.Outcome, "release-observation-failed", observed.Error);
        if (!isAttributed(observed.Value))
            return UnattributedRelease();
        if (observed.Value.AppliedToCount > 0)
        {
            return ReleaseFailure(
                ERuntimeProviderOutcome.TransientFailure,
                "release-firewall-still-applied",
                "The attributed firewall is still applied to a server; deletion is deferred until those "
                + "associations are absent.");
        }

        var deleted = await client.DeleteFirewallAsync(id, cancellationToken).ConfigureAwait(false);
        if (deleted.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.NotFound)
            return ReleaseFailure(deleted.Outcome, "release-delete-failed", deleted.Error);
        var verification = await client.GetFirewallAsync(id, cancellationToken).ConfigureAwait(false);
        return VerifyRelease(verification.Outcome, verification.Error, FirewallResourceType, id);
    }

    private async Task<IntentResourcesResult> FindIntentResourcesAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken)
    {
        var server = await FindIntentServerAsync(request, cancellationToken).ConfigureAwait(false);
        if (server.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.NotFound)
            return new IntentResourcesResult(server.Outcome, server.Error, server.Value, null, null);
        var primaryIp = await FindIntentPrimaryIpAsync(request, cancellationToken).ConfigureAwait(false);
        if (primaryIp.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.NotFound)
            return new IntentResourcesResult(primaryIp.Outcome, primaryIp.Error, server.Value, primaryIp.Value, null);
        var firewall = await FindIntentFirewallAsync(request, cancellationToken).ConfigureAwait(false);
        if (firewall.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.NotFound)
        {
            return new IntentResourcesResult(
                firewall.Outcome,
                firewall.Error,
                server.Value,
                primaryIp.Value,
                firewall.Value);
        }
        return new IntentResourcesResult(
            ERuntimeProviderOutcome.Succeeded,
            null,
            server.Value,
            primaryIp.Value,
            firewall.Value);
    }

    private async Task<IntentResourceResult<HetznerServer>> FindIntentServerAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await client.GetServersAsync(IntentLabels(request), cancellationToken).ConfigureAwait(false);
        return SelectSingle(
            result,
            q => HasExpectedAttribution(q.Labels, request, ServerResourceType),
            ServerResourceType);
    }

    private async Task<IntentResourceResult<HetznerPrimaryIp>> FindIntentPrimaryIpAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await client.GetPrimaryIpsAsync(IntentLabels(request), cancellationToken).ConfigureAwait(false);
        return SelectSingle(
            result,
            q => HasExpectedAttribution(q.Labels, request, PrimaryIpResourceType),
            PrimaryIpResourceType);
    }

    private async Task<IntentResourceResult<HetznerFirewall>> FindIntentFirewallAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await client.GetFirewallsAsync(IntentLabels(request), cancellationToken).ConfigureAwait(false);
        return SelectSingle(
            result,
            q => HasExpectedAttribution(q.Labels, request, FirewallResourceType),
            FirewallResourceType);
    }

    private static IntentResourceResult<T> SelectSingle<T>(
        HetznerClientResult<IReadOnlyList<T>> result,
        Func<T, bool> hasExpectedAttribution,
        string resourceType)
    {
        if (result.Outcome is not ERuntimeProviderOutcome.Succeeded || result.Value is null)
            return new IntentResourceResult<T>(result.Outcome, result.Error, default);
        if (result.Value.Count == 0)
            return new IntentResourceResult<T>(ERuntimeProviderOutcome.NotFound, null, default);
        if (result.Value.Count != 1 || !hasExpectedAttribution(result.Value[0]))
        {
            return new IntentResourceResult<T>(
                ERuntimeProviderOutcome.Unknown,
                $"Hetzner discovery returned conflicting {resourceType} resources for the provisioning intent.",
                default);
        }
        return new IntentResourceResult<T>(ERuntimeProviderOutcome.Succeeded, null, result.Value[0]);
    }

    private string? ValidateProvisionRequest(RuntimeProvisionRequest request)
    {
        var configurationFailure = optionsProvider.Value.ValidateProvisioning();
        if (configurationFailure is not null)
            return configurationFailure;
        if (request.ServerId == Guid.Empty)
            return "A stable server identity is required for Hetzner provisioning.";
        if (request.DeploymentId == Guid.Empty)
            return "A stable deployment identity is required for Hetzner provisioning.";
        if (request.WorkerId == Guid.Empty)
            return "A stable worker identity is required for Hetzner provisioning.";
        if (request.ProvisioningIntentId == Guid.Empty)
            return "A durably persisted provisioning intent is required before Hetzner provisioning.";
        if (string.IsNullOrWhiteSpace(request.CloudInit))
            return "Cloud-init content is required for Hetzner worker provisioning.";
        var networkFailure = RuntimeInboundNetworkPlanValidator.Validate(request.InboundNetworkRequirements);
        if (networkFailure is not null)
            return networkFailure;
        return null;
    }

    private static HetznerFirewallRuleRequest ToFirewallRule(RuntimeInboundNetworkRequirement requirement)
        => new(
            Direction: "in",
            Protocol: requirement.Protocol switch
            {
                ERuntimeNetworkProtocol.Tcp => "tcp",
                ERuntimeNetworkProtocol.Udp => "udp",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(requirement.Protocol),
                    requirement.Protocol,
                    "Unsupported inbound network protocol."),
            },
            Port: requirement.StartPort == requirement.EndPort
                ? requirement.StartPort.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"{requirement.StartPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}-"
                  + requirement.EndPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SourceIps: ["0.0.0.0/0"],
            Description: requirement.Description.Trim());

    private static IReadOnlyDictionary<string, string> IntentLabels(RuntimeProvisionRequest request)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ManagedLabel] = ManagedLabelValue,
            [DeploymentLabel] = LabelValue(request.DeploymentId),
            [IntentLabel] = LabelValue(request.ProvisioningIntentId),
        };

    private static IReadOnlyDictionary<string, string> BuildLabels(
        RuntimeProvisionRequest request,
        string resourceType)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ManagedLabel] = ManagedLabelValue,
            [ServerLabel] = LabelValue(request.ServerId),
            [DeploymentLabel] = LabelValue(request.DeploymentId),
            [IntentLabel] = LabelValue(request.ProvisioningIntentId),
            [WorkerLabel] = LabelValue(request.WorkerId),
            [ResourceKindLabel] = resourceType,
        };

    private static bool HasExpectedAttribution(
        IReadOnlyDictionary<string, string> labels,
        RuntimeProvisionRequest request,
        string resourceType)
        => HasExpectedAttribution(
            labels,
            request.ServerId,
            request.DeploymentId,
            request.ProvisioningIntentId,
            request.WorkerId,
            resourceType);

    private static bool HasExpectedAttribution(
        IReadOnlyDictionary<string, string> labels,
        RuntimeObservationRequest request,
        string resourceType)
        => HasExpectedAttribution(
            labels,
            request.ServerId,
            request.DeploymentId,
            request.ProvisioningIntentId,
            request.WorkerId,
            resourceType);

    private static bool HasExpectedAttribution(
        IReadOnlyDictionary<string, string> labels,
        RuntimeReleaseRequest request,
        string resourceType)
        => HasExpectedAttribution(
            labels,
            request.ServerId,
            request.DeploymentId,
            request.ProvisioningIntentId,
            request.WorkerId,
            resourceType);

    private static bool HasExpectedAttribution(
        IReadOnlyDictionary<string, string> labels,
        Guid serverId,
        Guid deploymentId,
        Guid provisioningIntentId,
        Guid workerId,
        string resourceType)
        => HasLabel(labels, ManagedLabel, ManagedLabelValue)
           && HasLabel(labels, ServerLabel, LabelValue(serverId))
           && HasLabel(labels, DeploymentLabel, LabelValue(deploymentId))
           && (provisioningIntentId == Guid.Empty
               || HasLabel(labels, IntentLabel, LabelValue(provisioningIntentId)))
           && (workerId == Guid.Empty || HasLabel(labels, WorkerLabel, LabelValue(workerId)))
           && HasExactResourceType(labels, resourceType);

    private static bool IsSafelyManaged(IReadOnlyDictionary<string, string> labels, string resourceType)
        => HasLabel(labels, ManagedLabel, ManagedLabelValue)
           && HasExactResourceType(labels, resourceType);

    private static bool HasExactResourceType(IReadOnlyDictionary<string, string> labels, string resourceType)
        => labels.TryGetValue(ResourceKindLabel, out var value)
           && string.Equals(value, resourceType, StringComparison.OrdinalIgnoreCase);

    private static bool HasLabel(IReadOnlyDictionary<string, string> labels, string key, string value)
        => labels.TryGetValue(key, out var observed)
           && string.Equals(observed, value, StringComparison.Ordinal);

    private static bool HasMatchingAttribution(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
        => new[] { ServerLabel, DeploymentLabel, IntentLabel, WorkerLabel }
            .All(key => left.TryGetValue(key, out var leftValue)
                        && right.TryGetValue(key, out var rightValue)
                        && string.Equals(leftValue, rightValue, StringComparison.Ordinal));

    private static RuntimeResource CreateResource(HetznerServer server, string? explicitPublicAddress = null)
        => CreateResource(
            ServerResourceIdentifier(server.Id),
            ServerResourceType,
            server.Status,
            explicitPublicAddress ?? server.PublicIpv4 ?? server.PublicIpv6,
            potentiallyCostGenerating: true,
            server.Labels);

    private static RuntimeResource CreateResource(
        HetznerPrimaryIp primaryIp,
        ERuntimeResourceProvisioningReadiness provisioningReadiness = ERuntimeResourceProvisioningReadiness.Ready)
        => CreateResource(
            PrimaryIpResourceIdentifier(primaryIp.Id),
            PrimaryIpResourceType,
            primaryIp.AssigneeId.HasValue ? $"assigned:{primaryIp.AssigneeId.Value}" : "unassigned",
            primaryIp.Ip,
            potentiallyCostGenerating: true,
            primaryIp.Labels,
            provisioningReadiness);

    private static RuntimeResource CreateResource(
        HetznerFirewall firewall,
        ERuntimeResourceProvisioningReadiness provisioningReadiness = ERuntimeResourceProvisioningReadiness.Ready)
        => CreateResource(
            FirewallResourceIdentifier(firewall.Id),
            FirewallResourceType,
            $"applied:{firewall.AppliedToCount}",
            publicAddress: null,
            potentiallyCostGenerating: false,
            firewall.Labels,
            provisioningReadiness);

    private static RuntimeResource CreateResource(
        string identifier,
        string resourceType,
        string providerState,
        string? publicAddress,
        bool potentiallyCostGenerating,
        IReadOnlyDictionary<string, string> labels,
        ERuntimeResourceProvisioningReadiness provisioningReadiness = ERuntimeResourceProvisioningReadiness.Ready)
    {
        var attribution = ParseAttribution(labels);
        var isPlatformManaged = HasLabel(labels, ManagedLabel, ManagedLabelValue)
                                && HasExactResourceType(labels, resourceType);
        return new RuntimeResource(
            Identifier,
            identifier,
            IsAllocated: true,
            IsPotentiallyCostGenerating: potentiallyCostGenerating,
            ObservedAt: DateTimeOffset.UtcNow,
            ResourceType: resourceType,
            ProviderState: providerState,
            PublicAddress: publicAddress,
            Attribution: attribution,
            ProviderLabels: new Dictionary<string, string>(labels, StringComparer.Ordinal),
            IsPlatformManaged: isPlatformManaged,
            HasTrustworthyAttribution: isPlatformManaged
                                       && attribution.IsComplete,
            ProvisioningReadiness: provisioningReadiness);
    }

    private static ERuntimeResourceProvisioningReadiness GetPrimaryIpProvisioningReadiness(
        HetznerPrimaryIp primaryIp,
        IReadOnlyCollection<HetznerServer> servers)
    {
        if (!primaryIp.AutoDelete || !string.Equals(primaryIp.Type, "ipv4", StringComparison.OrdinalIgnoreCase))
            return ERuntimeResourceProvisioningReadiness.Conflict;
        if (!primaryIp.AssigneeId.HasValue)
            return ERuntimeResourceProvisioningReadiness.Pending;
        if (!string.Equals(primaryIp.AssigneeType, "server", StringComparison.OrdinalIgnoreCase))
            return ERuntimeResourceProvisioningReadiness.Conflict;

        var attributedServerId = ResolveAttributedServerId(primaryIp.Labels, servers);
        return attributedServerId.HasValue && primaryIp.AssigneeId == attributedServerId
            ? ERuntimeResourceProvisioningReadiness.Ready
            : ERuntimeResourceProvisioningReadiness.Conflict;
    }

    private static ERuntimeResourceProvisioningReadiness GetFirewallProvisioningReadiness(
        HetznerFirewall firewall,
        IReadOnlyCollection<HetznerServer> servers)
    {
        if (firewall.AppliedToCount == 0)
            return ERuntimeResourceProvisioningReadiness.Pending;

        var attributedServerId = ResolveAttributedServerId(firewall.Labels, servers);
        return attributedServerId.HasValue
               && firewall.AppliedToCount == 1
               && firewall.AppliedServerIds.Count == 1
               && firewall.AppliedServerIds[0] == attributedServerId
            ? ERuntimeResourceProvisioningReadiness.Ready
            : ERuntimeResourceProvisioningReadiness.Conflict;
    }

    private static long? ResolveAttributedServerId(
        IReadOnlyDictionary<string, string> resourceLabels,
        IReadOnlyCollection<HetznerServer> servers)
    {
        var matches = servers
            .Where(q => HasExactResourceType(q.Labels, ServerResourceType)
                        && HasMatchingAttribution(resourceLabels, q.Labels))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0].Id : null;
    }

    private static RuntimeResourceAttribution ParseAttribution(IReadOnlyDictionary<string, string> labels)
        => new(
            ParseGuidLabel(labels, ServerLabel),
            ParseGuidLabel(labels, DeploymentLabel),
            ParseGuidLabel(labels, IntentLabel),
            ParseGuidLabel(labels, WorkerLabel));

    private static Guid? ParseGuidLabel(IReadOnlyDictionary<string, string> labels, string key)
        => labels.TryGetValue(key, out var value) && Guid.TryParseExact(value, "N", out var parsed)
            ? parsed
            : null;

    private static string LabelValue(Guid value) => value.ToString("N");

    private static bool TryParseResourceIdentifier(
        string resourceIdentifier,
        out EHetznerResourceKind kind,
        out long providerResourceId)
    {
        foreach (var candidate in Enum.GetValues<EHetznerResourceKind>())
        {
            var prefix = $"{ResourceType(candidate)}:";
            if (!resourceIdentifier.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            kind = candidate;
            return long.TryParse(resourceIdentifier.AsSpan(prefix.Length), out providerResourceId)
                   && providerResourceId > 0;
        }
        kind = default;
        providerResourceId = 0;
        return false;
    }

    private static string ResourceType(EHetznerResourceKind kind)
        => kind switch
        {
            EHetznerResourceKind.Server => ServerResourceType,
            EHetznerResourceKind.PrimaryIp => PrimaryIpResourceType,
            EHetznerResourceKind.Firewall => FirewallResourceType,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private static RuntimeProvisionResult ProvisionFailure(
        ERuntimeProviderOutcome outcome,
        string stage,
        string? error,
        IntentResourcesResult resources)
    {
        var discovered = new List<RuntimeResource>(3);
        if (resources.Server is not null)
            discovered.Add(CreateResource(resources.Server));
        if (resources.PrimaryIp is not null)
            discovered.Add(CreateResource(resources.PrimaryIp));
        if (resources.Firewall is not null)
            discovered.Add(CreateResource(resources.Firewall));
        return new RuntimeProvisionResult(outcome, stage, error, null, discovered);
    }

    private static RuntimeObservationResult Observed(RuntimeResource resource)
        => new(ERuntimeProviderOutcome.Succeeded, "observed", null, resource);

    private static RuntimeObservationResult UnattributedObservation()
        => ObservationFailure(
            ERuntimeProviderOutcome.Unknown,
            "observe-unattributed",
            "The Hetzner resource exists but its platform attribution cannot be verified.");

    private static RuntimeObservationResult ObservationFailure(
        ERuntimeProviderOutcome outcome,
        string stage,
        string? error)
        => new(outcome, stage, error, null);

    private static RuntimeReleaseResult UnattributedRelease()
        => ReleaseFailure(
            ERuntimeProviderOutcome.Unknown,
            "release-unattributed",
            "Refusing to delete a Hetzner resource whose platform attribution cannot be verified.");

    private static RuntimeReleaseResult ReleaseFailure(
        ERuntimeProviderOutcome outcome,
        string stage,
        string? error)
        => new(outcome, stage, error, false);

    private static RuntimeReleaseResult VerifyRelease(
        ERuntimeProviderOutcome outcome,
        string? error,
        string resourceType,
        long resourceId)
    {
        if (outcome is ERuntimeProviderOutcome.NotFound)
            return VerifiedReleasedResult("released-verified-absent");
        if (outcome is ERuntimeProviderOutcome.Succeeded)
        {
            return ReleaseFailure(
                ERuntimeProviderOutcome.Unknown,
                "release-still-present",
                $"The Hetzner {resourceType} {resourceId} still exists after deletion; release remains unverified.");
        }
        return ReleaseFailure(outcome, "release-verification-failed", error);
    }

    private static RuntimeReleaseResult VerifiedReleasedResult(string stage)
        => new(ERuntimeProviderOutcome.Succeeded, stage, null, true);

    private static bool TryObservedValue<T>(
        HetznerClientResult<T> observation,
        out T? value,
        out RuntimeObservationResult? failure)
    {
        value = observation.Value;
        if (observation.Outcome is ERuntimeProviderOutcome.Succeeded && value is not null)
        {
            failure = null;
            return true;
        }
        failure = observation.Outcome is ERuntimeProviderOutcome.NotFound
            ? ObservationFailure(ERuntimeProviderOutcome.NotFound, "not-found", null)
            : ObservationFailure(observation.Outcome, "observe-failed", observation.Error);
        return false;
    }

    private static bool TryDiscoveryValue<T>(
        HetznerClientResult<IReadOnlyList<T>> discovery,
        out IReadOnlyList<T>? values,
        out RuntimeDiscoveryResult? failure)
    {
        values = discovery.Value;
        if (discovery.Outcome is ERuntimeProviderOutcome.Succeeded && values is not null)
        {
            failure = null;
            return true;
        }
        failure = new RuntimeDiscoveryResult(
            discovery.Outcome is ERuntimeProviderOutcome.NotFound
                ? ERuntimeProviderOutcome.PermanentFailure
                : discovery.Outcome,
            "discovery-failed",
            discovery.Error,
            []);
        return false;
    }

    private enum EHetznerResourceKind
    {
        Server,
        PrimaryIp,
        Firewall,
    }

    private sealed record IntentResourceResult<T>(
        ERuntimeProviderOutcome Outcome,
        string? Error,
        T? Value);

    private sealed record IntentResourcesResult(
        ERuntimeProviderOutcome Outcome,
        string? Error,
        HetznerServer? Server,
        HetznerPrimaryIp? PrimaryIp,
        HetznerFirewall? Firewall);
}
