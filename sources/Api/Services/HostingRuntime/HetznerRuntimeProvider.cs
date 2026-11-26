using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Production runtime provider for one dedicated Hetzner server, explicit Primary IPv4, and firewall per deployment.
/// </summary>
[Singleton<HetznerRuntimeProvider, IRuntimeProvider>]
public sealed class HetznerRuntimeProvider(
    HetznerCloudClient client,
    IHetznerRuntimeOptionsProvider optionsProvider,
    IProviderProvisioningMutationGuard provisioningMutationGuard,
    ILogger<HetznerRuntimeProvider> logger) : IRuntimeProvider
{
    public const string Identifier = "hetzner";
    internal const string ManagedLabel = "utf-managed";
    internal const string ServerLabel = "utf-server";
    internal const string DeploymentLabel = "utf-deployment";
    internal const string IntentLabel = "utf-intent";
    internal const string WorkerLabel = "utf-worker";
    internal const string ServiceLabel = "utf-service";
    internal const string ServiceOrdinalLabel = "utf-service-ordinal";
    internal const string ResourceKindLabel = "utf-resource-kind";
    internal const string ServerConfigurationFingerprintLabel = "utf-server-config";
    internal const string FirewallRulesFingerprintLabel = "utf-firewall-rules";
    internal const string ServerResourceType = "server";
    internal const string PrimaryIpResourceType = "primary-ip";
    internal const string FirewallResourceType = "firewall";
    internal const string VolumeResourceType = "volume";
    private const string ManagedLabelValue = "true";
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _provisioningLocks = new();

    /// <summary>
    /// Gets the billing unit of Hetzner Cloud, which bills every started hour in full.
    /// </summary>
    /// <value>The started-hour unit, <see cref="RuntimeBillingUnit.StartedHour"/>.</value>
    public static RuntimeBillingUnit ProviderBillingUnit => RuntimeBillingUnit.StartedHour;

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
                ],
                Storage: new RuntimeStorageCapabilities(
                    SupportsExternalBlockStorage: failure is null,
                    MinimumCapacityGiB: optionsProvider.Value.VolumeMinimumSizeGiB,
                    MaximumCapacityGiB: optionsProvider.Value.VolumeMaximumSizeGiB,
                    CapacityIncrementGiB: optionsProvider.Value.VolumeSizeIncrementGiB,
                    EstimatedHourlyCostPerGiBEur: optionsProvider.Value.VolumeEstimatedHourlyCostPerGiBEur,
                    DeploymentMountPath: optionsProvider.Value.VolumeMountPath,
                    ReadinessFailure: failure),
                BillingUnit: ProviderBillingUnit);
        }
    }

    public async Task<IReadOnlyCollection<RuntimeProfile>> GetProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        if (optionsProvider.Value.ValidateProvisioning() is { } failure)
            throw new InvalidOperationException(failure);

        var pricingCurrency = await client.GetPricingCurrencyAsync(cancellationToken).ConfigureAwait(false);
        if (pricingCurrency.Outcome is not ERuntimeProviderOutcome.Succeeded
            || string.IsNullOrWhiteSpace(pricingCurrency.Value))
        {
            throw new InvalidOperationException(
                pricingCurrency.Error ?? "Hetzner's project pricing currency could not be loaded.");
        }
        if (!string.Equals(pricingCurrency.Value.Trim(), "EUR", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Hetzner pricing currency '{SafeCurrencyCode(pricingCurrency.Value)}' is not supported; "
                + "UTF hosting cost accounting is EUR-denominated.");
        }
        var result = await client.GetServerTypesAsync(cancellationToken).ConfigureAwait(false);
        if (result.Outcome is not ERuntimeProviderOutcome.Succeeded || result.Value is null)
            throw new InvalidOperationException(result.Error ?? "Hetzner server types could not be loaded.");

        var options = optionsProvider.Value;
        var location = options.Location.Trim();
        var profiles = result.Value
            .Where(q => !string.IsNullOrWhiteSpace(q.Name)
                        && string.Equals(q.Architecture, "x86", StringComparison.OrdinalIgnoreCase)
                        && TryGetAvailableServerTypeLocation(q, location, out _)
                        && ToProfileInteger(q.Cores).HasValue
                        && ToProfileInteger(q.Disk).HasValue)
            .Select(q =>
            {
                var price = q.Prices?.FirstOrDefault(p =>
                    string.Equals(p.Location, location, StringComparison.OrdinalIgnoreCase));
                var cores = ToProfileInteger(q.Cores);
                var disk = ToProfileInteger(q.Disk);
                return new RuntimeProfile(
                    q.Name!.Trim(),
                    string.IsNullOrWhiteSpace(q.Description) ? q.Name.Trim() : q.Description.Trim(),
                    $"{cores} vCPU · {q.Memory:0.##} GiB RAM · {disk} GiB disk",
                    cores,
                    q.Memory,
                    disk,
                    ParsePrice(price?.PriceHourly?.Gross),
                    ParsePrice(price?.PriceMonthly?.Gross));
            })
            .ToArray();
        var configuredDefaultHourly = profiles.FirstOrDefault(q => string.Equals(
            q.Identifier,
            options.ServerType,
            StringComparison.OrdinalIgnoreCase))?.EstimatedHourlyCostEur;
        var fixedResourceOverhead = configuredDefaultHourly is > 0m and { } defaultHourly
            ? Math.Max(0m, options.EstimatedHourlyCostEur - defaultHourly)
            : 0m;
        return profiles
            .Select(q => q with
            {
                EstimatedHourlyCostEur = q.EstimatedHourlyCostEur + fixedResourceOverhead,
                EstimatedMonthlyCostEur = q.EstimatedMonthlyCostEur + fixedResourceOverhead * 730m,
            })
            .OrderByDescending(q => IsRecommendedServerType(result.Value, q.Identifier, location))
            .ThenBy(q => q.EstimatedHourlyCostEur ?? decimal.MaxValue)
            .ThenBy(q => q.Identifier, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<RuntimeProvisionResult> ProvisionAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = HostingTelemetry.StartRuntimeOperation(
            Identifier,
            "provision",
            request.ServerId,
            request.DeploymentId);
        var validationFailure = ValidateProvisionRequest(request);
        ExternalStorageBootstrapBinding? storageBinding = null;
        if (validationFailure is null
            && request.StoragePlan?.UsesExternalStorage == true
            && !ExternalStorageBootstrapBinding.TryRead(request.CloudInit!, out storageBinding))
        {
            validationFailure = "External-storage bootstrap does not contain a valid device binding.";
        }
        if (validationFailure is not null)
        {
            var validationResult = new RuntimeProvisionResult(
                ERuntimeProviderOutcome.PermanentFailure,
                "provision-validation",
                validationFailure,
                null);
            HostingTelemetry.CompleteRuntimeOperation(
                activity,
                validationResult.Outcome,
                validationResult.Stage,
                validationResult.Error);
            return validationResult;
        }

        var gate = _provisioningLocks.GetOrAdd(request.ProvisioningIntentId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                var result = await ProvisionCoreAsync(request, storageBinding, cancellationToken)
                    .ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    logger.LogWarning(
                        "Hetzner provisioning did not complete for server {ServerId}, deployment {DeploymentId}, "
                        + "worker {WorkerId}, and intent {IntentId}. Outcome={Outcome}; Stage={Stage}; Failure={Failure}",
                        request.ServerId,
                        request.DeploymentId,
                        request.WorkerId,
                        request.ProvisioningIntentId,
                        result.Outcome,
                        result.Stage,
                        HostingFailureSanitizer.Sanitize(result.Error, optionsProvider.Value.Token));
                }
                HostingTelemetry.CompleteRuntimeOperation(activity, result.Outcome, result.Stage, result.Error);
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                HostingTelemetry.RecordException(
                    activity,
                    ex,
                    HostingFailureSanitizer.Sanitize(ex.Message, optionsProvider.Value.Token, request.CloudInit));
                logger.LogError(
                    "Unexpected Hetzner provisioning failure for server {ServerId}, deployment {DeploymentId}, "
                    + "worker {WorkerId}, and intent {IntentId}. ExceptionType={ExceptionType}; Failure={Failure}; "
                    + "StackTrace={StackTrace}",
                    request.ServerId,
                    request.DeploymentId,
                    request.WorkerId,
                    request.ProvisioningIntentId,
                    ex.GetType().FullName,
                    HostingFailureSanitizer.Sanitize(ex.Message, optionsProvider.Value.Token, request.CloudInit),
                    ex.StackTrace);
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Discovers or provisions the attributed resources for a validated deployment intent.</summary>
    /// <param name="request">The deployment intent and requested runtime configuration.</param>
    /// <param name="storageBinding">The validated bootstrap binding for an external Volume, when requested.</param>
    /// <param name="cancellationToken">Cancels provider operations.</param>
    /// <returns>The provisioning outcome and any attributed resources.</returns>
    private async Task<RuntimeProvisionResult> ProvisionCoreAsync(
        RuntimeProvisionRequest request,
        ExternalStorageBootstrapBinding? storageBinding,
        CancellationToken cancellationToken)
    {
        var intent = await FindIntentResourcesAsync(request, cancellationToken).ConfigureAwait(false);
        if (intent.Outcome is not ERuntimeProviderOutcome.Succeeded)
        {
            var discoveryFailure = ProvisionFailure(
                intent.Outcome,
                "provision-discovery-failed",
                intent.Error,
                intent);
            // ProvisionCore has not attempted a mutation yet. If discovery returned no attributed resources, this
            // invocation is definitively closed and the control plane may clear its create claim before retrying.
            return discoveryFailure.ProvisionedResources.Count == 0
                ? discoveryFailure with { IsProvisioningAttemptDefinitivelyClosed = true }
                : discoveryFailure;
        }
        var existingResourceCount = (intent.Server is null ? 0 : 1)
                                    + (intent.PrimaryIp is null ? 0 : 1)
                                    + (intent.Firewall is null ? 0 : 1)
                                    + (intent.Volume is null ? 0 : 1);
        var expectedResourceCount = request.StoragePlan?.UsesExternalStorage == true ? 4 : 3;
        if (existingResourceCount is > 0 && existingResourceCount != expectedResourceCount)
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
        var hadUnknownCreateOutcome = false;
        var volume = intent.Volume;
        if (request.StoragePlan?.UsesExternalStorage == true && volume is null)
        {
            var selectedGiB = request.StoragePlan.SelectedCapacityBytes / RuntimeStoragePlanResolver.GiB;
            var labels = BuildLabels(request, VolumeResourceType);
            var guardedResult = await provisioningMutationGuard.ExecuteCreateAsync(
                    request.ServerId,
                    request.DeploymentId,
                    guardedCancellationToken => client.CreateVolumeAsync(
                        new HetznerVolumeCreateRequest(
                            $"utf-{request.WorkerId:N}",
                            selectedGiB,
                            optionsProvider.Value.Location.Trim(),
                            labels),
                        guardedCancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!guardedResult.IsAuthorized || guardedResult.Value is null)
            {
                return ProvisionFailure(
                    ERuntimeProviderOutcome.TransientFailure,
                    "provisioning-blocked",
                    guardedResult.Failure ?? "Provisioning was blocked before Volume creation.",
                    intent,
                    isProvisioningAttemptDefinitivelyClosed: !guardedResult.IsAuthorized);
            }
            var result = guardedResult.Value;
            if (result.Outcome is ERuntimeProviderOutcome.Succeeded
                && result.Value is not null
                && HasExpectedAttribution(result.Value.Labels, request, VolumeResourceType))
            {
                volume = result.Value;
                created = true;
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Unknown)
            {
                hadUnknownCreateOutcome = true;
                var recovered = await FindIntentVolumeAsync(request, cancellationToken).ConfigureAwait(false);
                if (recovered.Outcome is ERuntimeProviderOutcome.Succeeded && recovered.Value is not null)
                {
                    volume = recovered.Value;
                    recoveredAfterAmbiguousCreate = true;
                    hadUnknownCreateOutcome = false;
                }
                else
                {
                    return ProvisionFailure(
                        ERuntimeProviderOutcome.Unknown,
                        "volume-create-outcome-unknown",
                        "Hetzner Volume creation has an unknown outcome; provider-wide reconciliation must continue.",
                        intent with { Volume = recovered.Value });
                }
            }
            else
            {
                return ProvisionFailure(
                    result.Outcome,
                    "volume-create-failed",
                    result.Error,
                    intent with { Volume = result.Value },
                    isProvisioningAttemptDefinitivelyClosed: !hadUnknownCreateOutcome);
            }
        }
        if (request.StoragePlan?.UsesExternalStorage != true && volume is not null)
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "unexpected-volume",
                "The provisioning intent has an external Volume but the resolved storage plan uses included disk.",
                intent);
        }
        if (volume is not null
            && (volume.Id <= 0
                || VerifyVolumeConfiguration(volume, request) is not EProviderConfigurationVerification.Verified))
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "volume-configuration-mismatch",
                "The attributed Volume does not match the required identity, location, size, or protection state.",
                intent with { Volume = volume });
        }
        intent = intent with { Volume = volume };
        var volumeDevicePath = volume is null
            ? null
            : $"/dev/disk/by-id/scsi-0HC_Volume_{volume.Id.ToString(CultureInfo.InvariantCulture)}";
        var firewall = intent.Firewall;
        if (firewall is null)
        {
            var labels = BuildLabels(request, FirewallResourceType);
            var guardedResult = await provisioningMutationGuard.ExecuteCreateAsync(
                    request.ServerId,
                    request.DeploymentId,
                    guardedCancellationToken => client.CreateFirewallAsync(
                        new HetznerFirewallCreateRequest(
                            $"utf-{request.WorkerId:N}",
                            request.InboundNetworkRequirements!
                                .Select(ToFirewallRule)
                                .ToArray(),
                            labels),
                        guardedCancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!guardedResult.IsAuthorized || guardedResult.Value is null)
            {
                return ProvisionFailure(
                    ERuntimeProviderOutcome.TransientFailure,
                    "provisioning-blocked",
                    guardedResult.Failure ?? "Provisioning was blocked before firewall creation.",
                    intent,
                    isProvisioningAttemptDefinitivelyClosed: !guardedResult.IsAuthorized
                                                              && !hadUnknownCreateOutcome);
            }
            var result = guardedResult.Value;
            if (result.Outcome is ERuntimeProviderOutcome.Succeeded
                && result.Value is not null
                && HasExpectedAttribution(result.Value.Labels, request, FirewallResourceType))
            {
                firewall = result.Value;
                created = true;
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Succeeded && result.Value is not null)
            {
                return ProvisionFailure(
                    ERuntimeProviderOutcome.PermanentFailure,
                    "firewall-create-attribution-mismatch",
                    "Hetzner created a firewall whose durable UTF attribution does not match the request.",
                    intent with { Firewall = result.Value },
                    isProvisioningAttemptDefinitivelyClosed: !hadUnknownCreateOutcome);
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Unknown)
            {
                hadUnknownCreateOutcome = true;
                var recovered = await FindIntentFirewallAsync(request, cancellationToken).ConfigureAwait(false);
                if (recovered.Outcome is ERuntimeProviderOutcome.Succeeded && recovered.Value is not null)
                {
                    firewall = recovered.Value;
                    recoveredAfterAmbiguousCreate = true;
                    hadUnknownCreateOutcome = false;
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
                return ProvisionFailure(
                    result.Outcome,
                    "firewall-create-failed",
                    result.Error,
                    intent,
                    isProvisioningAttemptDefinitivelyClosed: !hadUnknownCreateOutcome);
            }
        }

        var firewallConfiguration = VerifyFirewallConfiguration(firewall, request);
        if (firewallConfiguration is not EProviderConfigurationVerification.Verified)
        {
            return ProvisionFailure(
                firewallConfiguration is EProviderConfigurationVerification.Conflict
                    ? ERuntimeProviderOutcome.PermanentFailure
                    : ERuntimeProviderOutcome.Unknown,
                firewallConfiguration is EProviderConfigurationVerification.Conflict
                    ? "firewall-rules-mismatch"
                    : "firewall-rules-unverified",
                firewallConfiguration is EProviderConfigurationVerification.Conflict
                    ? "The attributed Hetzner firewall rules do not exactly match the requested inbound network plan."
                    : "The attributed Hetzner firewall rule configuration could not be verified.",
                intent with { Firewall = firewall },
                isProvisioningAttemptDefinitivelyClosed: !hadUnknownCreateOutcome);
        }

        var primaryIp = intent.PrimaryIp;
        if (primaryIp is null)
        {
            var labels = BuildLabels(request, PrimaryIpResourceType);
            var guardedResult = await provisioningMutationGuard.ExecuteCreateAsync(
                    request.ServerId,
                    request.DeploymentId,
                    guardedCancellationToken => client.CreatePrimaryIpAsync(
                        new HetznerPrimaryIpCreateRequest(
                            $"utf-{request.WorkerId:N}",
                            Type: "ipv4",
                            optionsProvider.Value.Location.Trim(),
                            AutoDelete: true,
                            labels),
                        guardedCancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!guardedResult.IsAuthorized || guardedResult.Value is null)
            {
                return ProvisionFailure(
                    ERuntimeProviderOutcome.TransientFailure,
                    "provisioning-blocked",
                    guardedResult.Failure ?? "Provisioning was blocked before Primary IP creation.",
                    intent with { Firewall = firewall },
                    isProvisioningAttemptDefinitivelyClosed: !guardedResult.IsAuthorized
                                                              && !hadUnknownCreateOutcome);
            }
            var result = guardedResult.Value;
            if (result.Outcome is ERuntimeProviderOutcome.Succeeded
                && result.Value is not null
                && HasExpectedAttribution(result.Value.Labels, request, PrimaryIpResourceType))
            {
                primaryIp = result.Value;
                created = true;
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Succeeded && result.Value is not null)
            {
                return ProvisionFailure(
                    ERuntimeProviderOutcome.PermanentFailure,
                    "primary-ip-create-attribution-mismatch",
                    "Hetzner created a Primary IP whose durable UTF attribution does not match the request.",
                    intent with { Firewall = firewall, PrimaryIp = result.Value },
                    isProvisioningAttemptDefinitivelyClosed: !hadUnknownCreateOutcome);
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Unknown)
            {
                hadUnknownCreateOutcome = true;
                var recovered = await FindIntentPrimaryIpAsync(request, cancellationToken).ConfigureAwait(false);
                if (recovered.Outcome is ERuntimeProviderOutcome.Succeeded && recovered.Value is not null)
                {
                    primaryIp = recovered.Value;
                    recoveredAfterAmbiguousCreate = true;
                    hadUnknownCreateOutcome = false;
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
                    intent with { Firewall = firewall },
                    isProvisioningAttemptDefinitivelyClosed: !hadUnknownCreateOutcome);
            }
        }

        var primaryIpConfiguration = VerifyPrimaryIpConfiguration(primaryIp);
        if (primaryIpConfiguration is not EProviderConfigurationVerification.Verified)
        {
            return ProvisionFailure(
                primaryIpConfiguration is EProviderConfigurationVerification.Conflict
                    ? ERuntimeProviderOutcome.PermanentFailure
                    : ERuntimeProviderOutcome.Unknown,
                primaryIpConfiguration is EProviderConfigurationVerification.Conflict
                    ? "primary-ip-configuration-mismatch"
                    : "primary-ip-configuration-unverified",
                primaryIpConfiguration is EProviderConfigurationVerification.Conflict
                    ? "The attributed Hetzner Primary IP does not have the required IPv4, auto-delete, "
                      + "and location configuration."
                    : "The attributed Hetzner Primary IP configuration could not be verified.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp },
                isProvisioningAttemptDefinitivelyClosed: !hadUnknownCreateOutcome);
        }

        var server = intent.Server;
        if (server is null)
        {
            var options = optionsProvider.Value;
            var labels = BuildLabels(request, ServerResourceType);
            var cloudInit = volumeDevicePath is null
                ? request.CloudInit!
                : storageBinding!.Bind(request.CloudInit!, volumeDevicePath);
            var guardedResult = await provisioningMutationGuard.ExecuteCreateAsync(
                    request.ServerId,
                    request.DeploymentId,
                    guardedCancellationToken => client.CreateServerAsync(
                        new HetznerServerCreateRequest(
                            $"utf-{request.WorkerId:N}",
                            RequestedServerType(request),
                            options.Image.Trim(),
                            options.Location.Trim(),
                            StartAfterCreate: volume is null,
                            cloudInit,
                            new HetznerServerPublicNetRequest(
                                EnableIpv4: true,
                                EnableIpv6: false,
                                Ipv4: primaryIp.Id),
                            [new HetznerServerFirewallRequest(firewall.Id)],
                            labels),
                        guardedCancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!guardedResult.IsAuthorized || guardedResult.Value is null)
            {
                return ProvisionFailure(
                    ERuntimeProviderOutcome.TransientFailure,
                    "provisioning-blocked",
                    guardedResult.Failure ?? "Provisioning was blocked before server creation.",
                    intent with { Firewall = firewall, PrimaryIp = primaryIp },
                    isProvisioningAttemptDefinitivelyClosed: !guardedResult.IsAuthorized
                                                              && !hadUnknownCreateOutcome);
            }
            var result = guardedResult.Value;
            if (result.Outcome is ERuntimeProviderOutcome.Succeeded
                && result.Value is not null
                && HasExpectedAttribution(result.Value.Labels, request, ServerResourceType))
            {
                server = result.Value;
                created = true;
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Succeeded && result.Value is not null)
            {
                return ProvisionFailure(
                    ERuntimeProviderOutcome.PermanentFailure,
                    "server-create-attribution-mismatch",
                    "Hetzner created a server whose durable UTF attribution does not match the request.",
                    intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = result.Value },
                    isProvisioningAttemptDefinitivelyClosed: !hadUnknownCreateOutcome);
            }
            else if (result.Outcome is ERuntimeProviderOutcome.Unknown)
            {
                hadUnknownCreateOutcome = true;
                var recovered = await FindIntentServerAsync(request, cancellationToken).ConfigureAwait(false);
                if (recovered.Outcome is ERuntimeProviderOutcome.Succeeded && recovered.Value is not null)
                {
                    server = recovered.Value;
                    recoveredAfterAmbiguousCreate = true;
                    hadUnknownCreateOutcome = false;
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
                    intent with { Firewall = firewall, PrimaryIp = primaryIp },
                    isProvisioningAttemptDefinitivelyClosed: !hadUnknownCreateOutcome);
            }
        }

        var serverConfiguration = VerifyServerConfiguration(server, request);
        if (serverConfiguration is not EProviderConfigurationVerification.Verified)
        {
            return ProvisionFailure(
                serverConfiguration is EProviderConfigurationVerification.Conflict
                    ? ERuntimeProviderOutcome.PermanentFailure
                    : ERuntimeProviderOutcome.Unknown,
                serverConfiguration is EProviderConfigurationVerification.Conflict
                    ? "server-configuration-mismatch"
                    : "server-configuration-unverified",
                serverConfiguration is EProviderConfigurationVerification.Conflict
                    ? "The attributed Hetzner server type, image, or location does not match the requested configuration."
                    : "The attributed Hetzner server type, image, and location could not be verified.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }

        if (volume is not null)
        {
            if (!volume.ServerId.HasValue)
            {
                var attached = await provisioningMutationGuard.ExecuteCreateAsync(
                        request.ServerId,
                        request.DeploymentId,
                        guardedCancellationToken => client.AttachVolumeAsync(
                            volume.Id,
                            server.Id,
                            guardedCancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!attached.IsAuthorized || attached.Value is null)
                {
                    return ProvisionFailure(
                        ERuntimeProviderOutcome.TransientFailure,
                        "volume-attach-blocked",
                        attached.Failure ?? "Volume attachment was blocked by the provisioning barrier.",
                        intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server, Volume = volume });
                }
                if (attached.Value.Outcome is not ERuntimeProviderOutcome.Succeeded
                    and not ERuntimeProviderOutcome.Unknown)
                {
                    return ProvisionFailure(
                        attached.Value.Outcome,
                        "volume-attach-failed",
                        attached.Value.Error,
                        intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server, Volume = volume });
                }
            }
            var verifiedVolume = await client.GetVolumeAsync(volume.Id, cancellationToken).ConfigureAwait(false);
            if (verifiedVolume.Outcome is not ERuntimeProviderOutcome.Succeeded || verifiedVolume.Value is null)
            {
                return ProvisionFailure(
                    verifiedVolume.Outcome,
                    "volume-attachment-verification-failed",
                    verifiedVolume.Error,
                    intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server, Volume = volume });
            }
            var attachedVolume = verifiedVolume.Value;
            if (attachedVolume.Id != volume.Id
                || !HasExpectedAttribution(attachedVolume.Labels, request, VolumeResourceType))
            {
                return ProvisionFailure(
                    ERuntimeProviderOutcome.PermanentFailure,
                    "volume-attachment-unverified",
                    "The Volume attachment readback did not match the attributed Volume.",
                    intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server, Volume = volume });
            }
            volume = attachedVolume;
            if (VerifyVolumeConfiguration(volume, request) is not EProviderConfigurationVerification.Verified
                || volume.ServerId != server.Id
                || !string.Equals(volume.LinuxDevice, volumeDevicePath, StringComparison.Ordinal))
            {
                return ProvisionFailure(
                    volume.ServerId.HasValue && volume.ServerId != server.Id
                        ? ERuntimeProviderOutcome.PermanentFailure
                        : ERuntimeProviderOutcome.Unknown,
                    "volume-attachment-unverified",
                    "The attributed Volume attachment, location, capacity, or stable device identity is not verified.",
                    intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server, Volume = volume });
            }
            if (string.Equals(server.Status, "off", StringComparison.OrdinalIgnoreCase))
            {
                var poweredOn = await provisioningMutationGuard.ExecuteCreateAsync(
                        request.ServerId,
                        request.DeploymentId,
                        guardedCancellationToken => client.PowerOnServerAsync(server.Id, guardedCancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!poweredOn.IsAuthorized || poweredOn.Value is null)
                {
                    return ProvisionFailure(
                        ERuntimeProviderOutcome.TransientFailure,
                        "server-poweron-blocked",
                        poweredOn.Failure ?? "Server power-on was blocked by the provisioning barrier.",
                        intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server, Volume = volume });
                }
                if (poweredOn.Value.Outcome is not ERuntimeProviderOutcome.Succeeded
                    and not ERuntimeProviderOutcome.Unknown)
                {
                    return ProvisionFailure(
                        poweredOn.Value.Outcome,
                        "server-poweron-failed",
                        poweredOn.Value.Error,
                        intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server, Volume = volume });
                }
                var poweredServer = await client.GetServerAsync(server.Id, cancellationToken).ConfigureAwait(false);
                if (poweredServer.Outcome is ERuntimeProviderOutcome.Succeeded && poweredServer.Value is not null)
                    server = poweredServer.Value;
            }
        }

        var operationalState = GetServerOperationalState(server.Status);
        if (operationalState is ERuntimeResourceOperationalState.Faulted)
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "server-operational-fault",
                $"The attributed Hetzner server entered the faulted provider state '{server.Status}'.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }
        if (operationalState is ERuntimeResourceOperationalState.Uncertain)
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.Unknown,
                "server-operational-state-unverified",
                $"The attributed Hetzner server provider state '{server.Status}' is not yet operationally conclusive.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
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
        primaryIpConfiguration = VerifyPrimaryIpConfiguration(primaryIp);
        if (primaryIpConfiguration is not EProviderConfigurationVerification.Verified)
        {
            return ProvisionFailure(
                primaryIpConfiguration is EProviderConfigurationVerification.Conflict
                    ? ERuntimeProviderOutcome.PermanentFailure
                    : ERuntimeProviderOutcome.Unknown,
                primaryIpConfiguration is EProviderConfigurationVerification.Conflict
                    ? "primary-ip-configuration-mismatch"
                    : "primary-ip-configuration-unverified",
                primaryIpConfiguration is EProviderConfigurationVerification.Conflict
                    ? "The explicit Hetzner Primary IP no longer has the required IPv4, auto-delete, "
                      + "and location configuration."
                    : "The explicit Hetzner Primary IP configuration could not be verified.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }
        if (!primaryIp.AssigneeId.HasValue)
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.Unknown,
                "primary-ip-assignment-pending",
                "The explicit Hetzner Primary IP assignment has not converged yet.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }
        if (!HasExpectedAttribution(primaryIp.Labels, request, PrimaryIpResourceType)
            || primaryIp.AssigneeId.Value != server.Id
            || !string.Equals(primaryIp.AssigneeType, "server", StringComparison.OrdinalIgnoreCase))
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.PermanentFailure,
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
        firewallConfiguration = VerifyFirewallConfiguration(firewall, request);
        if (firewallConfiguration is not EProviderConfigurationVerification.Verified)
        {
            return ProvisionFailure(
                firewallConfiguration is EProviderConfigurationVerification.Conflict
                    ? ERuntimeProviderOutcome.PermanentFailure
                    : ERuntimeProviderOutcome.Unknown,
                firewallConfiguration is EProviderConfigurationVerification.Conflict
                    ? "firewall-rules-mismatch"
                    : "firewall-rules-unverified",
                firewallConfiguration is EProviderConfigurationVerification.Conflict
                    ? "The attributed Hetzner firewall rules do not exactly match the requested inbound network plan."
                    : "The attributed Hetzner firewall rule configuration could not be verified.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }
        if (firewall.AppliedToCount == 0)
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.Unknown,
                "firewall-attachment-pending",
                "The attributed Hetzner firewall attachment has not converged yet.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }
        if (!HasExpectedAttribution(firewall.Labels, request, FirewallResourceType)
            || firewall.AppliedToCount != 1
            || firewall.AppliedServerIds.Count != 1
            || firewall.AppliedServerIds[0] != server.Id)
        {
            return ProvisionFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "firewall-attachment-unverified",
                "The attributed Hetzner firewall is not attached exclusively to the attributed server.",
                intent with { Firewall = firewall, PrimaryIp = primaryIp, Server = server });
        }

        var resources = new List<RuntimeResource>
        {
            CreateResource(server, primaryIp.Ip),
            CreateResource(primaryIp),
            CreateResource(firewall),
        };
        if (volume is not null)
            resources.Add(CreateResource(volume));
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

    private static decimal? ParsePrice(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string SafeCurrencyCode(string value)
    {
        var candidate = value.Trim();
        return candidate.Length == 3 && candidate.All(char.IsAsciiLetter)
            ? candidate.ToUpperInvariant()
            : "unknown";
    }

    private static int? ToProfileInteger(decimal value)
        => value > 0m && value <= int.MaxValue && decimal.Truncate(value) == value
            ? decimal.ToInt32(value)
            : null;

    private static bool TryGetAvailableServerTypeLocation(
        HetznerServerTypeDto serverType,
        string expectedLocation,
        out HetznerServerTypeLocationDto? serverTypeLocation)
    {
        serverTypeLocation = serverType.Locations?.FirstOrDefault(q =>
            q.Available
            && q.Deprecation.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            && string.Equals(q.Name, expectedLocation, StringComparison.OrdinalIgnoreCase));
        return serverTypeLocation is not null;
    }

    private static bool IsRecommendedServerType(
        IEnumerable<HetznerServerTypeDto> serverTypes,
        string identifier,
        string location)
        => serverTypes.Any(q => string.Equals(q.Name, identifier, StringComparison.OrdinalIgnoreCase)
                                && TryGetAvailableServerTypeLocation(q, location, out var serverTypeLocation)
                                && serverTypeLocation!.Recommended);

    public async Task<RuntimeObservationResult> ObserveAsync(
        RuntimeObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = HostingTelemetry.StartRuntimeOperation(
            Identifier,
            "observe",
            request.ServerId,
            request.DeploymentId,
            resourceId: request.ResourceIdentifier);
        var configurationFailure = optionsProvider.Value.ValidateConnection();
        if (configurationFailure is not null)
        {
            var configurationResult = ObservationFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "observe-configuration",
                configurationFailure);
            HostingTelemetry.CompleteRuntimeOperation(
                activity,
                configurationResult.Outcome,
                configurationResult.Stage,
                configurationResult.Error);
            return configurationResult;
        }
        if (!TryParseResourceIdentifier(request.ResourceIdentifier, out var kind, out var providerResourceId))
        {
            var validationResult = ObservationFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "observe-validation",
                "The Hetzner resource identity is invalid.");
            HostingTelemetry.CompleteRuntimeOperation(
                activity,
                validationResult.Outcome,
                validationResult.Stage,
                validationResult.Error);
            return validationResult;
        }

        activity?.SetTag("utf.hosting.resource.type", ResourceType(kind));
        var result = kind switch
        {
            EHetznerResourceKind.Server => await ObserveServerAsync(request, providerResourceId, cancellationToken)
                .ConfigureAwait(false),
            EHetznerResourceKind.PrimaryIp => await ObservePrimaryIpAsync(request, providerResourceId, cancellationToken)
                .ConfigureAwait(false),
            EHetznerResourceKind.Firewall => await ObserveFirewallAsync(request, providerResourceId, cancellationToken)
                .ConfigureAwait(false),
            EHetznerResourceKind.Volume => await ObserveVolumeAsync(request, providerResourceId, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        HostingTelemetry.CompleteRuntimeOperation(activity, result.Outcome, result.Stage, result.Error);
        return result;
    }

    public async Task<RuntimeReleaseResult> ReleaseAsync(
        RuntimeReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = HostingTelemetry.StartRuntimeOperation(
            Identifier,
            "release",
            request.ServerId,
            request.DeploymentId,
            resourceId: request.ResourceIdentifier);
        var configurationFailure = optionsProvider.Value.ValidateConnection();
        if (configurationFailure is not null)
        {
            var configurationResult = ReleaseFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "release-configuration",
                configurationFailure);
            HostingTelemetry.CompleteRuntimeOperation(
                activity,
                configurationResult.Outcome,
                configurationResult.Stage,
                configurationResult.Error);
            return configurationResult;
        }
        if (!TryParseResourceIdentifier(request.ResourceIdentifier, out var kind, out var providerResourceId))
        {
            var validationResult = ReleaseFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "release-validation",
                "The Hetzner resource identity is invalid.");
            HostingTelemetry.CompleteRuntimeOperation(
                activity,
                validationResult.Outcome,
                validationResult.Stage,
                validationResult.Error);
            return validationResult;
        }

        activity?.SetTag("utf.hosting.resource.type", ResourceType(kind));
        var result = kind switch
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
            EHetznerResourceKind.Volume => await ReleaseVolumeAsync(
                    providerResourceId,
                    q => HasExpectedAttribution(q.Labels, request, VolumeResourceType),
                    (server, volume) => HasExpectedAttribution(server.Labels, request, ServerResourceType)
                                        && HasMatchingAttribution(volume.Labels, server.Labels),
                    cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        HostingTelemetry.CompleteRuntimeOperation(activity, result.Outcome, result.Stage, result.Error);
        return result;
    }

    public async Task<RuntimeReleaseResult> ReleaseDanglingAsync(
        RuntimeDanglingReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = HostingTelemetry.StartRuntimeOperation(
            Identifier,
            "release-dangling",
            serverId: null,
            deploymentId: null,
            resourceType: request.ResourceType,
            resourceId: request.ResourceIdentifier);
        try
        {
            var result = await ReleaseDanglingCoreAsync(request, cancellationToken).ConfigureAwait(false);
            HostingTelemetry.CompleteRuntimeOperation(activity, result.Outcome, result.Stage, result.Error);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HostingTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    private async Task<RuntimeReleaseResult> ReleaseDanglingCoreAsync(
        RuntimeDanglingReleaseRequest request,
        CancellationToken cancellationToken)
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
            EHetznerResourceKind.Volume => await ReleaseVolumeAsync(
                    providerResourceId,
                    q => IsSafelyManaged(q.Labels, VolumeResourceType),
                    (server, volume) => IsSafelyManaged(server.Labels, ServerResourceType)
                                        && HasMatchingAttribution(volume.Labels, server.Labels),
                    cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public async Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        using var activity = HostingTelemetry.StartRuntimeOperation(Identifier, "discover");
        try
        {
            var result = await DiscoverCoreAsync(cancellationToken).ConfigureAwait(false);
            HostingTelemetry.CompleteRuntimeOperation(activity, result.Outcome, result.Stage, result.Error);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HostingTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    private async Task<RuntimeDiscoveryResult> DiscoverCoreAsync(CancellationToken cancellationToken)
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
        var volumes = await client.GetVolumesAsync(labels, cancellationToken).ConfigureAwait(false);
        if (!TryDiscoveryValue(volumes, out var discoveredVolumes, out failure))
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
                    GetPrimaryIpProvisioningReadiness(q, providerServers, optionsProvider.Value.Location))),
                .. discoveredFirewalls!.Select(q => CreateResource(
                    q,
                    GetFirewallProvisioningReadiness(q, providerServers))),
                .. discoveredVolumes!.Select(q => CreateResource(
                    q,
                    GetVolumeProvisioningReadiness(q, providerServers, optionsProvider.Value.Location))),
            ]);
    }

    internal static string ResourceIdentifier(long providerServerId) => ServerResourceIdentifier(providerServerId);

    internal static string ServerResourceIdentifier(long providerServerId) => $"server:{providerServerId}";

    internal static string PrimaryIpResourceIdentifier(long providerPrimaryIpId) => $"primary-ip:{providerPrimaryIpId}";

    internal static string FirewallResourceIdentifier(long providerFirewallId) => $"firewall:{providerFirewallId}";

    internal static string VolumeResourceIdentifier(long providerVolumeId) => $"volume:{providerVolumeId}";

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
        return Observed(CreateResource(
            value,
            GetPrimaryIpConfigurationProvisioningReadiness(value, optionsProvider.Value.Location)));
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

    private async Task<RuntimeObservationResult> ObserveVolumeAsync(
        RuntimeObservationRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var observation = await client.GetVolumeAsync(id, cancellationToken).ConfigureAwait(false);
        if (!TryObservedValue(observation, out var value, out var failure))
            return failure!;
        if (!HasExpectedAttribution(value!.Labels, request, VolumeResourceType))
            return UnattributedObservation();
        return Observed(CreateResource(value, GetVolumeConfigurationReadiness(value, optionsProvider.Value.Location)));
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

    private async Task<RuntimeReleaseResult> ReleaseVolumeAsync(
        long id,
        Func<HetznerVolume, bool> isAttributed,
        Func<HetznerServer, HetznerVolume, bool> isSafeAttachment,
        CancellationToken cancellationToken)
    {
        var observed = await client.GetVolumeAsync(id, cancellationToken).ConfigureAwait(false);
        if (observed.Outcome is ERuntimeProviderOutcome.NotFound)
            return VerifiedReleasedResult("released-already-absent");
        if (observed.Outcome is not ERuntimeProviderOutcome.Succeeded || observed.Value is null)
            return ReleaseFailure(observed.Outcome, "release-observation-failed", observed.Error);
        if (!isAttributed(observed.Value))
            return UnattributedRelease();
        if (observed.Value.DeleteProtected)
        {
            return ReleaseFailure(
                ERuntimeProviderOutcome.PermanentFailure,
                "release-volume-protected",
                "The attributed Volume has provider deletion protection enabled.");
        }
        if (observed.Value.ServerId.HasValue)
        {
            var attachedServer = await client.GetServerAsync(observed.Value.ServerId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (attachedServer.Outcome is not ERuntimeProviderOutcome.Succeeded || attachedServer.Value is null)
            {
                return ReleaseFailure(
                    attachedServer.Outcome,
                    "release-volume-attachment-unverified",
                    attachedServer.Error ?? "The server attached to the Volume could not be verified safely.");
            }
            if (!isSafeAttachment(attachedServer.Value, observed.Value))
                return UnattributedRelease();
            var detached = await client.DetachVolumeAsync(id, cancellationToken).ConfigureAwait(false);
            if (detached.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.Unknown)
                return ReleaseFailure(detached.Outcome, "release-volume-detach-failed", detached.Error);
            var verification = await client.GetVolumeAsync(id, cancellationToken).ConfigureAwait(false);
            if (verification.Outcome is ERuntimeProviderOutcome.NotFound)
                return VerifiedReleasedResult("released-during-detach");
            if (verification.Outcome is not ERuntimeProviderOutcome.Succeeded || verification.Value is null)
                return ReleaseFailure(verification.Outcome, "release-volume-detach-verification-failed", verification.Error);
            if (verification.Value.ServerId.HasValue)
            {
                return ReleaseFailure(
                    ERuntimeProviderOutcome.TransientFailure,
                    "release-volume-detach-pending",
                    "The attributed Volume is still attached after requesting detach.");
            }
        }
        var deleted = await client.DeleteVolumeAsync(id, cancellationToken).ConfigureAwait(false);
        if (deleted.Outcome is not ERuntimeProviderOutcome.Succeeded and not ERuntimeProviderOutcome.NotFound)
            return ReleaseFailure(deleted.Outcome, "release-delete-failed", deleted.Error);
        var absent = await client.GetVolumeAsync(id, cancellationToken).ConfigureAwait(false);
        return VerifyRelease(absent.Outcome, absent.Error, VolumeResourceType, id);
    }

    private async Task<IntentResourcesResult> FindIntentResourcesAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken)
    {
        var server = await FindIntentServerAsync(request, cancellationToken).ConfigureAwait(false);
        var primaryIp = await FindIntentPrimaryIpAsync(request, cancellationToken).ConfigureAwait(false);
        var firewall = await FindIntentFirewallAsync(request, cancellationToken).ConfigureAwait(false);
        var volume = request.StoragePlan?.UsesExternalStorage == true
            ? await FindIntentVolumeAsync(request, cancellationToken).ConfigureAwait(false)
            : new IntentResourceResult<HetznerVolume>(
                ERuntimeProviderOutcome.NotFound,
                null,
                null,
                []);
        var failure = new[]
        {
            (server.Outcome, server.Error),
            (primaryIp.Outcome, primaryIp.Error),
            (firewall.Outcome, firewall.Error),
            (volume.Outcome, volume.Error),
        }.Where(q => q.Outcome is not ERuntimeProviderOutcome.Succeeded
                     and not ERuntimeProviderOutcome.NotFound)
            .OrderBy(q => q.Outcome switch
            {
                ERuntimeProviderOutcome.Unknown => 0,
                ERuntimeProviderOutcome.TransientFailure => 1,
                ERuntimeProviderOutcome.PermanentFailure => 2,
                _ => 3,
            })
            .FirstOrDefault();
        return new IntentResourcesResult(
            failure == default ? ERuntimeProviderOutcome.Succeeded : failure.Outcome,
            failure == default ? null : failure.Error,
            server.Value,
            primaryIp.Value,
            firewall.Value,
            volume.Value,
            server.DiscoveredResources,
            primaryIp.DiscoveredResources,
            firewall.DiscoveredResources,
            volume.DiscoveredResources);
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

    private async Task<IntentResourceResult<HetznerVolume>> FindIntentVolumeAsync(
        RuntimeProvisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await client.GetVolumesAsync(IntentLabels(request), cancellationToken).ConfigureAwait(false);
        return SelectSingle(
            result,
            q => HasExpectedAttribution(q.Labels, request, VolumeResourceType),
            VolumeResourceType);
    }

    private static IntentResourceResult<T> SelectSingle<T>(
        HetznerClientResult<IReadOnlyList<T>> result,
        Func<T, bool> hasExpectedAttribution,
        string resourceType)
    {
        if (result.Outcome is not ERuntimeProviderOutcome.Succeeded || result.Value is null)
            return new IntentResourceResult<T>(result.Outcome, result.Error, default, result.Value ?? []);
        if (result.Value.Count == 0)
            return new IntentResourceResult<T>(ERuntimeProviderOutcome.NotFound, null, default, []);
        if (result.Value.Count != 1 || !hasExpectedAttribution(result.Value[0]))
        {
            return new IntentResourceResult<T>(
                ERuntimeProviderOutcome.PermanentFailure,
                $"Hetzner discovery returned conflicting {resourceType} resources for the provisioning intent.",
                default,
                result.Value);
        }
        return new IntentResourceResult<T>(
            ERuntimeProviderOutcome.Succeeded,
            null,
            result.Value[0],
            result.Value);
    }

    /// <summary>Checks the provider configuration and required deployment inputs.</summary>
    /// <param name="request">The requested deployment intent and runtime configuration.</param>
    /// <returns>A validation failure message, or null when the required inputs are valid.</returns>
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
        if (string.IsNullOrWhiteSpace(request.ServiceIdentifier)
            || request.ServiceIdentifier.Length > 63
            || request.ServiceIdentifier.Any(q => !char.IsAsciiLetterOrDigit(q) && q is not ('-' or '_'))
            || request.ServiceOrdinal is < 0 or > 255)
        {
            return "A valid deployment service identity is required for Hetzner provisioning.";
        }
        if (string.IsNullOrWhiteSpace(request.CloudInit))
            return "Cloud-init content is required for Hetzner worker provisioning.";
        // An explicitly empty rule set still needs an attached firewall. Hetzner drops new inbound traffic by default.
        var networkFailure = request.InboundNetworkRequirements is { Count: 0 }
            ? null
            : RuntimeInboundNetworkPlanValidator.Validate(request.InboundNetworkRequirements);
        if (networkFailure is not null)
            return networkFailure;
        return null;
    }

    private EProviderConfigurationVerification VerifyVolumeConfiguration(
        HetznerVolume volume,
        RuntimeProvisionRequest request)
    {
        if (request.StoragePlan?.UsesExternalStorage != true)
            return EProviderConfigurationVerification.Conflict;
        var expectedGiB = request.StoragePlan.SelectedCapacityBytes / RuntimeStoragePlanResolver.GiB;
        if (!HasProviderIdentity(volume.Location))
            return EProviderConfigurationVerification.Uncertain;
        return volume.SizeGiB == expectedGiB
               && !volume.DeleteProtected
               && ProviderIdentityMatches(volume.Location, optionsProvider.Value.Location)
            ? EProviderConfigurationVerification.Verified
            : EProviderConfigurationVerification.Conflict;
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
            SourceIps: requirement.SourceCidrs ?? ["0.0.0.0/0"],
            Description: requirement.Description.Trim());

    internal static string ServerConfigurationFingerprint(string serverType, string image, string location)
        => Fingerprint(
        [
            NormalizeConfigurationValue(serverType),
            NormalizeConfigurationValue(image),
            NormalizeConfigurationValue(location),
        ]);

    internal static string FirewallRulesFingerprint(
        IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements)
        => FirewallRulesFingerprint(inboundNetworkRequirements.Select(q =>
        {
            var rule = ToFirewallRule(q);
            return CanonicalFirewallRule(
                rule.Direction,
                rule.Protocol,
                rule.Port,
                rule.SourceIps,
                []);
        }));

    private static string FirewallRulesFingerprint(IReadOnlyList<HetznerFirewallRule> rules)
        => FirewallRulesFingerprint(rules.Select(q => CanonicalFirewallRule(
            q.Direction,
            q.Protocol,
            q.Port,
            q.SourceIps,
            q.DestinationIps)));

    private static string FirewallRulesFingerprint(IEnumerable<string> canonicalRules)
        => Fingerprint(canonicalRules.Order(StringComparer.Ordinal));

    private static string CanonicalFirewallRule(
        string direction,
        string protocol,
        string port,
        IEnumerable<string> sourceIps,
        IEnumerable<string> destinationIps)
        => string.Join(
            '|',
            NormalizeConfigurationValue(direction),
            NormalizeConfigurationValue(protocol),
            CanonicalPort(port),
            string.Join(',', sourceIps.Select(CanonicalAddress).Order(StringComparer.Ordinal)),
            string.Join(',', destinationIps.Select(CanonicalAddress).Order(StringComparer.Ordinal)));

    private static string CanonicalPort(string port)
    {
        var compact = string.Concat(port.Where(q => !char.IsWhiteSpace(q)));
        var parts = compact.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length is 1
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var single)
            && single is >= 1 and <= 65535)
        {
            return $"{single}-{single}";
        }
        if (parts.Length is 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            && start is >= 1 and <= 65535
            && end is >= 1 and <= 65535
            && start <= end)
        {
            return $"{start}-{end}";
        }
        return NormalizeConfigurationValue(compact);
    }

    private static string CanonicalAddress(string value)
    {
        var parts = value.Trim().Split('/', 2, StringSplitOptions.TrimEntries);
        if (!IPAddress.TryParse(parts[0], out var address))
            return NormalizeConfigurationValue(value);
        return parts.Length == 2 ? $"{address}/{parts[1]}" : address.ToString();
    }

    private static string Fingerprint(IEnumerable<string> values)
    {
        var canonical = string.Join('\n', values);
        return "v1-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string NormalizeConfigurationValue(string value) => value.Trim().ToLowerInvariant();

    private static IReadOnlyDictionary<string, string> IntentLabels(RuntimeProvisionRequest request)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ManagedLabel] = ManagedLabelValue,
            [DeploymentLabel] = LabelValue(request.DeploymentId),
            [IntentLabel] = LabelValue(request.ProvisioningIntentId),
        };

    private IReadOnlyDictionary<string, string> BuildLabels(
        RuntimeProvisionRequest request,
        string resourceType)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ManagedLabel] = ManagedLabelValue,
            [ServerLabel] = LabelValue(request.ServerId),
            [DeploymentLabel] = LabelValue(request.DeploymentId),
            [IntentLabel] = LabelValue(request.ProvisioningIntentId),
            [WorkerLabel] = LabelValue(request.WorkerId),
            [ServiceLabel] = request.ServiceIdentifier,
            [ServiceOrdinalLabel] = request.ServiceOrdinal.ToString(CultureInfo.InvariantCulture),
            [ResourceKindLabel] = resourceType,
        };
        if (string.Equals(resourceType, ServerResourceType, StringComparison.Ordinal))
        {
            labels[ServerConfigurationFingerprintLabel] = ServerConfigurationFingerprint(
                RequestedServerType(request),
                optionsProvider.Value.Image,
                optionsProvider.Value.Location);
        }
        else if (string.Equals(resourceType, FirewallResourceType, StringComparison.Ordinal))
        {
            labels[FirewallRulesFingerprintLabel] = FirewallRulesFingerprint(
                request.InboundNetworkRequirements!);
        }
        return labels;
    }

    private string RequestedServerType(RuntimeProvisionRequest request)
        => string.IsNullOrWhiteSpace(request.RuntimeProfileIdentifier)
            ? optionsProvider.Value.ServerType.Trim()
            : request.RuntimeProfileIdentifier.Trim();

    private EProviderConfigurationVerification VerifyServerConfiguration(
        HetznerServer server,
        RuntimeProvisionRequest request)
    {
        if (!HasProviderIdentity(server.ServerType)
            || !HasProviderIdentity(server.Image)
            || !HasProviderIdentity(server.Location))
        {
            return EProviderConfigurationVerification.Uncertain;
        }

        var expected = ServerConfigurationFingerprint(
            RequestedServerType(request),
            optionsProvider.Value.Image,
            optionsProvider.Value.Location);
        if (!server.Labels.TryGetValue(ServerConfigurationFingerprintLabel, out var attributedExpected)
            || string.IsNullOrWhiteSpace(attributedExpected))
        {
            return EProviderConfigurationVerification.Uncertain;
        }
        if (!string.Equals(attributedExpected, expected, StringComparison.Ordinal)
            || !ProviderIdentityMatches(server.ServerType, RequestedServerType(request))
            || !ProviderIdentityMatches(server.Image, optionsProvider.Value.Image)
            || !ProviderIdentityMatches(server.Location, optionsProvider.Value.Location))
        {
            return EProviderConfigurationVerification.Conflict;
        }
        return EProviderConfigurationVerification.Verified;
    }

    private EProviderConfigurationVerification VerifyPrimaryIpConfiguration(HetznerPrimaryIp primaryIp)
    {
        if (!primaryIp.AutoDelete || !string.Equals(primaryIp.Type, "ipv4", StringComparison.OrdinalIgnoreCase))
            return EProviderConfigurationVerification.Conflict;
        if (!HasProviderIdentity(primaryIp.Location))
            return EProviderConfigurationVerification.Uncertain;
        return ProviderIdentityMatches(primaryIp.Location, optionsProvider.Value.Location)
            ? EProviderConfigurationVerification.Verified
            : EProviderConfigurationVerification.Conflict;
    }

    private static EProviderConfigurationVerification VerifyFirewallConfiguration(
        HetznerFirewall firewall,
        RuntimeProvisionRequest request)
    {
        if (!firewall.HasRuleConfiguration)
            return EProviderConfigurationVerification.Uncertain;

        var expected = FirewallRulesFingerprint(request.InboundNetworkRequirements!);
        if (!firewall.Labels.TryGetValue(FirewallRulesFingerprintLabel, out var attributedExpected)
            || string.IsNullOrWhiteSpace(attributedExpected))
        {
            return EProviderConfigurationVerification.Uncertain;
        }
        if (!string.Equals(attributedExpected, expected, StringComparison.Ordinal)
            || !string.Equals(FirewallRulesFingerprint(firewall.Rules), expected, StringComparison.Ordinal))
        {
            return EProviderConfigurationVerification.Conflict;
        }
        return EProviderConfigurationVerification.Verified;
    }

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
    {
        var operationalState = GetServerOperationalState(server.Status);
        return CreateResource(
            ServerResourceIdentifier(server.Id),
            ServerResourceType,
            server.Status,
            explicitPublicAddress ?? server.PublicIpv4 ?? server.PublicIpv6,
            potentiallyCostGenerating: true,
            server.Labels,
            GetServerConfigurationProvisioningReadiness(server),
            operationalState);
    }

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
        ERuntimeResourceProvisioningReadiness relationshipReadiness = ERuntimeResourceProvisioningReadiness.Ready)
        => CreateResource(
            FirewallResourceIdentifier(firewall.Id),
            FirewallResourceType,
            $"applied:{firewall.AppliedToCount}",
            publicAddress: null,
            potentiallyCostGenerating: false,
            firewall.Labels,
            CombineProvisioningReadiness(
                GetFirewallConfigurationProvisioningReadiness(firewall),
                relationshipReadiness));

    private RuntimeResource CreateResource(
        HetznerVolume volume,
        ERuntimeResourceProvisioningReadiness readiness = ERuntimeResourceProvisioningReadiness.Ready)
        => CreateResource(
            VolumeResourceIdentifier(volume.Id),
            VolumeResourceType,
            volume.ServerId.HasValue ? $"attached:{volume.ServerId.Value}" : volume.Status,
            publicAddress: null,
            potentiallyCostGenerating: true,
            volume.Labels,
            readiness,
            provisionedCapacityBytes: checked(volume.SizeGiB * RuntimeStoragePlanResolver.GiB),
            estimatedHourlyCostEur: volume.SizeGiB * optionsProvider.Value.VolumeEstimatedHourlyCostPerGiBEur);

    private static RuntimeResource CreateResource(
        string identifier,
        string resourceType,
        string providerState,
        string? publicAddress,
        bool potentiallyCostGenerating,
        IReadOnlyDictionary<string, string> labels,
        ERuntimeResourceProvisioningReadiness provisioningReadiness = ERuntimeResourceProvisioningReadiness.Ready,
        ERuntimeResourceOperationalState operationalState = ERuntimeResourceOperationalState.Uncertain,
        long? provisionedCapacityBytes = null,
        decimal? estimatedHourlyCostEur = null)
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
            ProvisioningReadiness: provisioningReadiness,
            OperationalState: operationalState,
            ProvisionedCapacityBytes: provisionedCapacityBytes,
            EstimatedHourlyCostEur: estimatedHourlyCostEur);
    }

    private static ERuntimeResourceProvisioningReadiness GetServerConfigurationProvisioningReadiness(
        HetznerServer server)
    {
        if (!HasProviderIdentity(server.ServerType)
            || !HasProviderIdentity(server.Image)
            || !HasProviderIdentity(server.Location)
            || !server.Labels.TryGetValue(ServerConfigurationFingerprintLabel, out var expected)
            || string.IsNullOrWhiteSpace(expected))
        {
            return ERuntimeResourceProvisioningReadiness.Pending;
        }
        return ServerConfigurationFingerprintMatches(server, expected)
            ? ERuntimeResourceProvisioningReadiness.Ready
            : ERuntimeResourceProvisioningReadiness.Conflict;
    }

    private static bool ServerConfigurationFingerprintMatches(HetznerServer server, string expected)
        => ProviderIdentities(server.ServerType).Any(serverType =>
            ProviderIdentities(server.Image).Any(image =>
                ProviderIdentities(server.Location).Any(location => string.Equals(
                    ServerConfigurationFingerprint(serverType, image, location),
                    expected,
                    StringComparison.Ordinal))));

    private static IReadOnlyList<string> ProviderIdentities(HetznerResourceReference reference)
    {
        var identities = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(reference.Name))
            identities.Add(reference.Name);
        if (reference.Id is > 0)
            identities.Add(reference.Id.Value.ToString(CultureInfo.InvariantCulture));
        return identities;
    }

    private static bool HasProviderIdentity(HetznerResourceReference reference)
        => !string.IsNullOrWhiteSpace(reference.Name) || reference.Id is > 0;

    private static bool ProviderIdentityMatches(HetznerResourceReference observed, string requested)
        => !string.IsNullOrWhiteSpace(requested)
           && ((!string.IsNullOrWhiteSpace(observed.Name)
                && string.Equals(observed.Name.Trim(), requested.Trim(), StringComparison.OrdinalIgnoreCase))
               || (observed.Id is > 0
                   && long.TryParse(requested.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                   && observed.Id.Value == id));

    private static ERuntimeResourceProvisioningReadiness GetFirewallConfigurationProvisioningReadiness(
        HetznerFirewall firewall)
    {
        if (!firewall.HasRuleConfiguration
            || !firewall.Labels.TryGetValue(FirewallRulesFingerprintLabel, out var expected)
            || string.IsNullOrWhiteSpace(expected))
        {
            return ERuntimeResourceProvisioningReadiness.Pending;
        }
        return string.Equals(FirewallRulesFingerprint(firewall.Rules), expected, StringComparison.Ordinal)
            ? ERuntimeResourceProvisioningReadiness.Ready
            : ERuntimeResourceProvisioningReadiness.Conflict;
    }

    private static ERuntimeResourceOperationalState GetServerOperationalState(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "running" => ERuntimeResourceOperationalState.Operational,
            "off" or "stopping" or "deleting" or "failed" => ERuntimeResourceOperationalState.Faulted,
            _ => ERuntimeResourceOperationalState.Uncertain,
        };

    private static ERuntimeResourceProvisioningReadiness CombineProvisioningReadiness(
        ERuntimeResourceProvisioningReadiness left,
        ERuntimeResourceProvisioningReadiness right)
    {
        if (left is ERuntimeResourceProvisioningReadiness.Conflict
            || right is ERuntimeResourceProvisioningReadiness.Conflict)
        {
            return ERuntimeResourceProvisioningReadiness.Conflict;
        }
        return left is ERuntimeResourceProvisioningReadiness.Pending
               || right is ERuntimeResourceProvisioningReadiness.Pending
            ? ERuntimeResourceProvisioningReadiness.Pending
            : ERuntimeResourceProvisioningReadiness.Ready;
    }

    private static ERuntimeResourceProvisioningReadiness GetPrimaryIpProvisioningReadiness(
        HetznerPrimaryIp primaryIp,
        IReadOnlyCollection<HetznerServer> servers,
        string expectedLocation)
    {
        var configurationReadiness = GetPrimaryIpConfigurationProvisioningReadiness(primaryIp, expectedLocation);
        if (configurationReadiness is not ERuntimeResourceProvisioningReadiness.Ready)
            return configurationReadiness;
        if (!primaryIp.AssigneeId.HasValue)
            return ERuntimeResourceProvisioningReadiness.Pending;
        if (!string.Equals(primaryIp.AssigneeType, "server", StringComparison.OrdinalIgnoreCase))
            return ERuntimeResourceProvisioningReadiness.Conflict;

        var attributedServerId = ResolveAttributedServerId(primaryIp.Labels, servers);
        return attributedServerId.HasValue && primaryIp.AssigneeId == attributedServerId
            ? ERuntimeResourceProvisioningReadiness.Ready
            : ERuntimeResourceProvisioningReadiness.Conflict;
    }

    private static ERuntimeResourceProvisioningReadiness GetPrimaryIpConfigurationProvisioningReadiness(
        HetznerPrimaryIp primaryIp,
        string expectedLocation)
    {
        if (!primaryIp.AutoDelete || !string.Equals(primaryIp.Type, "ipv4", StringComparison.OrdinalIgnoreCase))
            return ERuntimeResourceProvisioningReadiness.Conflict;
        if (string.IsNullOrWhiteSpace(expectedLocation) || !HasProviderIdentity(primaryIp.Location))
            return ERuntimeResourceProvisioningReadiness.Pending;
        return ProviderIdentityMatches(primaryIp.Location, expectedLocation)
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

    private static ERuntimeResourceProvisioningReadiness GetVolumeConfigurationReadiness(
        HetznerVolume volume,
        string expectedLocation)
    {
        if (volume.DeleteProtected || volume.SizeGiB <= 0)
            return ERuntimeResourceProvisioningReadiness.Conflict;
        if (!HasProviderIdentity(volume.Location) || string.IsNullOrWhiteSpace(expectedLocation))
            return ERuntimeResourceProvisioningReadiness.Pending;
        return ProviderIdentityMatches(volume.Location, expectedLocation)
            ? ERuntimeResourceProvisioningReadiness.Ready
            : ERuntimeResourceProvisioningReadiness.Conflict;
    }

    private static ERuntimeResourceProvisioningReadiness GetVolumeProvisioningReadiness(
        HetznerVolume volume,
        IReadOnlyCollection<HetznerServer> servers,
        string expectedLocation)
    {
        var configuration = GetVolumeConfigurationReadiness(volume, expectedLocation);
        if (configuration is not ERuntimeResourceProvisioningReadiness.Ready)
            return configuration;
        if (!volume.ServerId.HasValue || string.IsNullOrWhiteSpace(volume.LinuxDevice))
            return ERuntimeResourceProvisioningReadiness.Pending;
        var attributedServerId = ResolveAttributedServerId(volume.Labels, servers);
        return attributedServerId.HasValue && volume.ServerId == attributedServerId
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
            ParseGuidLabel(labels, WorkerLabel),
            labels.GetValueOrDefault(ServiceLabel),
            labels.TryGetValue(ServiceOrdinalLabel, out var ordinal)
            && int.TryParse(ordinal, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedOrdinal)
                ? parsedOrdinal
                : null);

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
            EHetznerResourceKind.Volume => VolumeResourceType,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private RuntimeProvisionResult ProvisionFailure(
        ERuntimeProviderOutcome outcome,
        string stage,
        string? error,
        IntentResourcesResult resources,
        bool isProvisioningAttemptDefinitivelyClosed = false)
    {
        var discovered = new List<RuntimeResource>(3);
        foreach (var server in resources.DiscoveredServers ?? [])
            discovered.Add(CreateResource(server));
        if (resources.Server is not null && discovered.All(q => q.ResourceIdentifier != ServerResourceIdentifier(resources.Server.Id)))
            discovered.Add(CreateResource(resources.Server));
        foreach (var primaryIp in resources.DiscoveredPrimaryIps ?? [])
            discovered.Add(CreateResource(primaryIp));
        if (resources.PrimaryIp is not null
            && discovered.All(q => q.ResourceIdentifier != PrimaryIpResourceIdentifier(resources.PrimaryIp.Id)))
        {
            discovered.Add(CreateResource(resources.PrimaryIp));
        }
        foreach (var firewall in resources.DiscoveredFirewalls ?? [])
            discovered.Add(CreateResource(firewall));
        if (resources.Firewall is not null
            && discovered.All(q => q.ResourceIdentifier != FirewallResourceIdentifier(resources.Firewall.Id)))
        {
            discovered.Add(CreateResource(resources.Firewall));
        }
        foreach (var volume in resources.DiscoveredVolumes ?? [])
            discovered.Add(CreateResource(volume));
        if (resources.Volume is not null
            && discovered.All(q => q.ResourceIdentifier != VolumeResourceIdentifier(resources.Volume.Id)))
        {
            discovered.Add(CreateResource(resources.Volume));
        }
        return new RuntimeProvisionResult(
            outcome,
            stage,
            error,
            null,
            discovered,
            isProvisioningAttemptDefinitivelyClosed);
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
        Volume,
    }

    private enum EProviderConfigurationVerification
    {
        Verified,
        Uncertain,
        Conflict,
    }

    private sealed record IntentResourceResult<T>(
        ERuntimeProviderOutcome Outcome,
        string? Error,
        T? Value,
        IReadOnlyList<T> DiscoveredResources);

    private sealed record IntentResourcesResult(
        ERuntimeProviderOutcome Outcome,
        string? Error,
        HetznerServer? Server,
        HetznerPrimaryIp? PrimaryIp,
        HetznerFirewall? Firewall,
        HetznerVolume? Volume,
        IReadOnlyList<HetznerServer>? DiscoveredServers = null,
        IReadOnlyList<HetznerPrimaryIp>? DiscoveredPrimaryIps = null,
        IReadOnlyList<HetznerFirewall>? DiscoveredFirewalls = null,
        IReadOnlyList<HetznerVolume>? DiscoveredVolumes = null);
}
