using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;
using LifecycleFailureSanitizer = X39.UnitedTacticalForces.Api.Services.GameServerLifecycle.HostingFailureSanitizer;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Compares provider-wide UTF-managed resource discovery with durable deployment inventory and cleanup incidents.
/// </summary>
[Scoped<ProviderResourceReconciler, IProviderResourceReconciler>]
internal sealed class ProviderResourceReconciler(
    DatabaseContext dbContext,
    IEnumerable<IRuntimeProvider> providers,
    IHostingAuditWriter auditWriter,
    IAutomaticCostStopService automaticCostStopService,
    IWorkerCredentialService workerCredentialService,
    IOptions<ProviderReconciliationOptions> options,
    IOptions<LifecycleWorkerOptions> lifecycleOptions,
    ILogger<ProviderResourceReconciler> logger) : IProviderResourceReconciler
{
    private readonly ProviderReconciliationOptions _options = options.Value;
    private readonly LifecycleWorkerOptions _lifecycleOptions = lifecycleOptions.Value;

    public async Task ReconcileAllAsync(CancellationToken cancellationToken = default)
    {
        _options.ValidateAgainstProviderSettlement(_lifecycleOptions.ProviderSettlementTimeout);
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ReconcileProviderAsync(provider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failure = LifecycleFailureSanitizer.Sanitize(ex);
                logger.LogError(
                    "Provider reconciliation failed for {ProviderIdentifier}: {Failure}",
                    provider.ProviderIdentifier,
                    failure);
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.ProviderReconciliationDegraded,
                    "Provider-wide hosting reconciliation failed.",
                    details: $"Provider={provider.ProviderIdentifier}; Failure={failure}",
                    costImpact: ECostImpact.Unknown);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await RemoveExpiredResolvedIncidentsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReconcileProviderAsync(
        IRuntimeProvider provider,
        CancellationToken cancellationToken)
    {
        if (!provider.Capabilities.SupportsProviderWideDiscovery)
        {
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.ProviderReconciliationDegraded,
                "A configured runtime provider cannot perform mandatory provider-wide discovery.",
                details: $"Provider={provider.ProviderIdentifier}",
                costImpact: ECostImpact.Unknown);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        RuntimeDiscoveryResult discovery;
        try
        {
            discovery = await provider.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            discovery = new RuntimeDiscoveryResult(
                ERuntimeProviderOutcome.TransientFailure,
                "discovery-threw",
                LifecycleFailureSanitizer.Sanitize(ex),
                []);
        }
        if (!discovery.Succeeded)
        {
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.ProviderReconciliationDegraded,
                "Provider-wide resource discovery is degraded.",
                details: $"Provider={provider.ProviderIdentifier}; Stage={discovery.Stage}; Failure={discovery.Error}",
                costImpact: ECostImpact.Unknown);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var providerResources = await dbContext.DeploymentResources
            .Where(q => q.ProviderIdentifier == provider.ProviderIdentifier)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var relevantDeploymentIds = providerResources.Select(q => q.DeploymentFk)
            .Concat(discovery.Resources
                .Select(q => q.Attribution?.DeploymentId)
                .Where(q => q.HasValue)
                .Select(q => q!.Value))
            .Distinct()
            .ToArray();
        var deployments = await dbContext.GameServerDeployments
            .Include(q => q.GameServer)
            .Include(q => q.Resources)
            .Include(q => q.Workers)
            .Include(q => q.LifecycleJobs)
            .ThenInclude(q => q.WorkerCommands)
            .Where(q => relevantDeploymentIds.Contains(q.PrimaryKey))
            .ToDictionaryAsync(q => q.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        var validCostOwnerIds = (await dbContext.CostOwners
                .Select(q => q.PrimaryKey)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();
        var activeDeploymentIdsByServer = (await dbContext.GameServerDeployments
                .AsNoTracking()
                .Where(q => q.TimeStampResolved == null)
                .Select(q => new { q.PrimaryKey, q.GameServerFk })
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(q => q.GameServerFk)
            .ToDictionary(q => q.Key, q => q.Select(x => x.PrimaryKey).ToHashSet());
        var vereinCostOwnerId = await dbContext.CostOwners
            .Where(q => q.Kind == ECostOwnerKind.Verein)
            .Select(q => (Guid?)q.PrimaryKey)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var linkedCleanup = new List<LinkedCleanup>();
        var danglingCleanup = new List<DanglingProviderResourceIncident>();
        var reopenedDeployments = new Dictionary<Guid, ReopenedDeployment>();
        var discoveredKeys = new HashSet<string>(StringComparer.Ordinal);

        var duplicateGraphKinds = discovery.Resources
            .Where(q => string.Equals(
                            q.ProviderIdentifier,
                            provider.ProviderIdentifier,
                            StringComparison.OrdinalIgnoreCase)
                        && q.IsPlatformManaged
                        && q.HasTrustworthyAttribution
                        && q.Attribution?.DeploymentId.HasValue == true
                        && q.Attribution.ProvisioningIntentId.HasValue)
            .GroupBy(q => new
            {
                DeploymentId = q.Attribution!.DeploymentId!.Value,
                IntentId = q.Attribution.ProvisioningIntentId!.Value,
                ResourceType = q.ResourceType.ToUpperInvariant(),
            })
            .Where(q => q.Count() > 1)
            .ToArray();
        foreach (var duplicate in duplicateGraphKinds)
        {
            if (!deployments.TryGetValue(duplicate.Key.DeploymentId, out var deployment))
                continue;
            var observed = duplicate.First();
            await MarkRunningDeploymentGraphFaultAsync(
                    deployment,
                    observed,
                    "provider-resource-graph-duplicate",
                    $"Provider discovery returned more than one {observed.ResourceType} for one provisioning intent.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var group in discovery.Resources.GroupBy(ResourceKey, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (group.Count() != 1)
            {
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.ProviderReconciliationDegraded,
                    "Provider discovery returned a duplicate resource identity; automatic mutation was skipped.",
                    details: $"Provider={provider.ProviderIdentifier}; Identity={group.Key}",
                    costImpact: ECostImpact.Unknown);
                continue;
            }

            var observed = group.Single();
            if (!string.Equals(
                    observed.ProviderIdentifier,
                    provider.ProviderIdentifier,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            discoveredKeys.Add(ResourceKey(observed));

            var existing = providerResources.SingleOrDefault(q => ResourceMatches(q, observed));
            if (existing is not null)
            {
                if (!deployments.TryGetValue(existing.DeploymentFk, out var deployment))
                    continue;
                if (HasConflictingTrustedDeploymentAttribution(existing, observed))
                {
                    existing.State = EDeploymentResourceState.Unknown;
                    existing.PossibleCost = true;
                    existing.TimeStampObserved = NodaSystemClock.Instance.GetCurrentInstant();
                    existing.TimeStampUpdated = existing.TimeStampObserved.Value;
                    await MarkRunningDeploymentGraphFaultAsync(
                            deployment,
                            observed,
                            "provider-attribution-conflict",
                            "Provider attribution conflicts with the durable deployment resource inventory.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    auditWriter.Add(
                        dbContext,
                        EHostingAuditEventKind.ProviderReconciliationDegraded,
                        "Provider attribution conflicts with durable resource inventory; mutation was skipped.",
                        serverId: deployment.GameServerFk,
                        deploymentId: deployment.PrimaryKey,
                        costOwnerId: deployment.CostOwnerFk,
                        details: ResourceDetails(provider, observed),
                        costImpact: ECostImpact.Unknown);
                    continue;
                }

                UpdateResourceObservation(existing, observed);
                deployment.PossibleCost |= observed.IsPotentiallyCostGenerating;
                await MarkRunningDeploymentReadinessFaultAsync(deployment, observed, cancellationToken)
                    .ConfigureAwait(false);
                if (ShouldRelease(deployment))
                {
                    RestoreLateCleanupState(
                        deployment,
                        observed,
                        activeDeploymentIdsByServer,
                        reopenedDeployments);
                    linkedCleanup.Add(new LinkedCleanup(existing, deployment, ResolveWorkerId(deployment, observed)));
                }
                continue;
            }

            var link = TryResolveLink(
                provider,
                observed,
                deployments,
                validCostOwnerIds);
            if (link is not null)
            {
                // Flush observations already staged by this scan, then serialize a new adoption with lifecycle result
                // settlement. Both paths may learn about the same graph member at the same time; the lock-time reload
                // prevents a duplicate insert from rolling back a provider result that only the executor can see.
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await using (var transaction = await BeginServerMutationAsync(
                                 link.Deployment.GameServerFk,
                                 cancellationToken)
                                 .ConfigureAwait(false))
                {
                    await dbContext.Entry(link.Deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
                    await ReloadDeploymentResourcesAsync(link.Deployment.PrimaryKey, cancellationToken)
                        .ConfigureAwait(false);
                    existing = link.Deployment.Resources.SingleOrDefault(q => ResourceMatches(q, observed));
                    if (existing is null)
                    {
                        existing = AdoptResource(provider, observed, link.Deployment, providerResources);
                        auditWriter.Add(
                            dbContext,
                            EHostingAuditEventKind.DeploymentResourceAdopted,
                            "A late provider resource was adopted into its attributed deployment.",
                            serverId: link.Deployment.GameServerFk,
                            deploymentId: link.Deployment.PrimaryKey,
                            workerId: link.WorkerId,
                            costOwnerId: link.Deployment.CostOwnerFk,
                            details: ResourceDetails(provider, observed),
                            costImpact: observed.IsPotentiallyCostGenerating
                                ? ECostImpact.KeepsCostRunning
                                : ECostImpact.NoCostImpact);
                    }
                    else
                    {
                        UpdateResourceObservation(existing, observed);
                        link.Deployment.PossibleCost |= observed.IsPotentiallyCostGenerating;
                        link.Deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                        if (providerResources.All(q => q.PrimaryKey != existing.PrimaryKey))
                            providerResources.Add(existing);
                    }
                    await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                await MarkRunningDeploymentReadinessFaultAsync(link.Deployment, observed, cancellationToken)
                    .ConfigureAwait(false);
                if (ShouldRelease(link.Deployment))
                {
                    RestoreLateCleanupState(
                        link.Deployment,
                        observed,
                        activeDeploymentIdsByServer,
                        reopenedDeployments);
                    linkedCleanup.Add(new LinkedCleanup(existing, link.Deployment, link.WorkerId));
                }
                continue;
            }

            if (!observed.IsPlatformManaged)
            {
                logger.LogWarning(
                    "Ignored provider resource {ProviderResourceIdentifier} from {ProviderIdentifier} because its "
                    + "UTF platform-management marker is not verified.",
                    observed.ResourceIdentifier,
                    provider.ProviderIdentifier);
                continue;
            }

            var incident = await EnsureDanglingIncidentAsync(
                    provider,
                    observed,
                    vereinCostOwnerId,
                    cancellationToken)
                .ConfigureAwait(false);
            danglingCleanup.Add(incident);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var cleanup in linkedCleanup
                     .DistinctBy(q => q.Resource.PrimaryKey)
                     .OrderBy(q => ProviderReleaseOrder(q.Resource.Kind)))
        {
            await ReleaseLinkedResourceAsync(provider, cleanup, cancellationToken).ConfigureAwait(false);
        }

        var unresolvedIncidents = await dbContext.DanglingProviderResourceIncidents
            .Where(q => q.ProviderIdentifier == provider.ProviderIdentifier
                        && q.Status != EDanglingResourceIncidentStatus.Resolved)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var incident in danglingCleanup
                     .Concat(unresolvedIncidents)
                     .DistinctBy(q => q.PrimaryKey)
                     .OrderBy(q => ProviderReleaseOrder(q.ResourceType)))
        {
            await ReleaseDanglingResourceAsync(
                    provider,
                    incident,
                    vereinCostOwnerId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await ReconcileMissingInventoriedResourcesAsync(
                provider,
                providerResources,
                deployments,
                discoveredKeys,
                cancellationToken)
            .ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ConfirmAbsentIssuedProvisioningIntentsAsync(
                provider,
                providerResources,
                deployments,
                discovery.Resources,
                activeDeploymentIdsByServer,
                reopenedDeployments,
                cancellationToken)
            .ConfigureAwait(false);
        var providerWideVerifiedDeploymentIds = await VerifyTeardownBarrierAsync(
                provider,
                deployments.Values,
                cancellationToken)
            .ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await FinalizeVerifiedTeardownsAsync(
                deployments.Values,
                reopenedDeployments.Keys,
                providerWideVerifiedDeploymentIds,
                cancellationToken)
            .ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task MarkRunningDeploymentReadinessFaultAsync(
        GameServerDeployment deployment,
        RuntimeResource observed,
        CancellationToken cancellationToken)
    {
        if (deployment.Status != EDeploymentStatus.Running)
            return;

        if (observed.HasConfirmedOperationalFault)
        {
            await MarkRunningDeploymentGraphFaultAsync(
                    deployment,
                    observed,
                    "provider-resource-operational-fault",
                    $"Provider state '{observed.ProviderState ?? "unknown"}' confirms that the "
                    + $"{observed.ResourceType} is no longer operational.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        if (observed.ProvisioningReadiness is ERuntimeResourceProvisioningReadiness.Ready
            or ERuntimeResourceProvisioningReadiness.Pending)
            return;

        await MarkRunningDeploymentGraphFaultAsync(
                deployment,
                observed,
                "provider-resource-graph-drift",
                $"A provider {observed.ResourceType} conflicts with the attributed deployment graph.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task MarkRunningDeploymentGraphFaultAsync(
        GameServerDeployment deployment,
        RuntimeResource observed,
        string faultCode,
        string faultMessage,
        CancellationToken cancellationToken)
    {
        if (!IsExpectedActive(deployment))
        {
            deployment.PossibleCost = true;
            deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            return;
        }

        // The shared fault-stop service takes the same durable server lock as user lifecycle actions and provider
        // creates. It reloads the deployment under that lock, so a concurrent user Terminate keeps its semantics.
        var faultStopTriggered = await automaticCostStopService.TriggerAsync(
                deployment.PrimaryKey,
                faultCode,
                faultMessage,
                cancellationToken)
            .ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        deployment.PossibleCost = true;
        deployment.TimeStampUpdated = now;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ProviderReconciliationDegraded,
            faultStopTriggered
                ? "Provider graph drift triggered provider-independent fault cost-stop."
                : "Provider graph drift was observed while an existing Terminate action retained teardown ownership.",
            serverId: deployment.GameServerFk,
            deploymentId: deployment.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            details: $"Kind={observed.ResourceType}; Identity={observed.ResourceIdentifier}; Failure={faultMessage}",
            costImpact: faultStopTriggered ? ECostImpact.StopsCost : ECostImpact.KeepsCostRunning);
    }

    private static ReconciliationLink? TryResolveLink(
        IRuntimeProvider provider,
        RuntimeResource observed,
        IReadOnlyDictionary<Guid, GameServerDeployment> deployments,
        IReadOnlySet<Guid> validCostOwnerIds)
    {
        var attribution = observed.Attribution;
        if (!observed.IsPlatformManaged
            || !observed.HasTrustworthyAttribution
            || attribution?.IsComplete != true
            || !deployments.TryGetValue(attribution.DeploymentId!.Value, out var deployment)
            || deployment.GameServerFk != attribution.ServerId
            || !validCostOwnerIds.Contains(deployment.CostOwnerFk)
            || deployment.Workers.All(q => q.PrimaryKey != attribution.WorkerId))
        {
            return null;
        }

        var intent = attribution.ProvisioningIntentId!.Value.ToString("N");
        if (deployment.Resources.All(q =>
                !string.Equals(q.ProviderIdentifier, provider.ProviderIdentifier, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(q.ProvisioningIntentIdentifier, intent, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }
        return new ReconciliationLink(deployment, attribution.WorkerId!.Value);
    }

    private DeploymentResource AdoptResource(
        IRuntimeProvider provider,
        RuntimeResource observed,
        GameServerDeployment deployment,
        ICollection<DeploymentResource> providerResources)
    {
        var intent = observed.Attribution!.ProvisioningIntentId!.Value.ToString("N");
        var resource = deployment.Resources
            .Where(q => string.Equals(
                            q.ProviderIdentifier,
                            provider.ProviderIdentifier,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            q.ProvisioningIntentIdentifier,
                            intent,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(q.Kind, observed.ResourceType, StringComparison.OrdinalIgnoreCase)
                        && q.ProviderResourceIdentifier == null)
            .OrderBy(q => q.TimeStampCreated)
            .FirstOrDefault();
        if (resource is null)
        {
            resource = new DeploymentResource
            {
                PrimaryKey = Guid.NewGuid(),
                DeploymentFk = deployment.PrimaryKey,
                WorkerFk = observed.Attribution.WorkerId,
                ProviderIdentifier = provider.ProviderIdentifier,
                ProvisioningIntentIdentifier = intent,
                Kind = observed.ResourceType,
                TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            };
            deployment.Resources.Add(resource);
            providerResources.Add(resource);
            dbContext.DeploymentResources.Add(resource);
        }
        UpdateResourceObservation(resource, observed);
        deployment.PossibleCost |= observed.IsPotentiallyCostGenerating;
        deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
        return resource;
    }

    private async Task<DanglingProviderResourceIncident> EnsureDanglingIncidentAsync(
        IRuntimeProvider provider,
        RuntimeResource observed,
        Guid? vereinCostOwnerId,
        CancellationToken cancellationToken)
    {
        var incident = await dbContext.DanglingProviderResourceIncidents.SingleOrDefaultAsync(q =>
                q.ProviderIdentifier == provider.ProviderIdentifier
                && q.ResourceType == observed.ResourceType
                && q.ProviderResourceIdentifier == observed.ResourceIdentifier,
            cancellationToken).ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var detected = incident is null || incident.Status == EDanglingResourceIncidentStatus.Resolved;
        if (incident is null)
        {
            incident = new DanglingProviderResourceIncident
            {
                PrimaryKey = Guid.NewGuid(),
                ProviderIdentifier = provider.ProviderIdentifier,
                ResourceType = observed.ResourceType,
                ProviderResourceIdentifier = observed.ResourceIdentifier,
                TimeStampCreated = now,
            };
            dbContext.DanglingProviderResourceIncidents.Add(incident);
        }
        incident.Status = EDanglingResourceIncidentStatus.CleanupPending;
        incident.IsPlatformManaged = true;
        incident.PossibleCost = observed.IsPotentiallyCostGenerating || observed.IsAllocated;
        incident.LastProviderState = Limit(observed.ProviderState, 128);
        incident.LastFailure = null;
        incident.ObservationCount++;
        incident.TimeStampLastObserved = now;
        incident.TimeStampUpdated = now;
        incident.TimeStampResolved = null;
        if (detected)
        {
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.DanglingResourceDetected,
                "A UTF-managed provider resource could not be linked to a valid deployment.",
                costOwnerId: vereinCostOwnerId,
                details: ResourceDetails(provider, observed),
                costImpact: incident.PossibleCost ? ECostImpact.KeepsCostRunning : ECostImpact.Unknown);
        }
        return incident;
    }

    private async Task ReleaseLinkedResourceAsync(
        IRuntimeProvider provider,
        LinkedCleanup cleanup,
        CancellationToken cancellationToken)
    {
        if (cleanup.Resource.ProviderResourceIdentifier is not { Length: > 0 } identifier)
            return;
        cleanup.Resource.State = EDeploymentResourceState.Releasing;
        cleanup.Resource.PossibleCost = true;
        cleanup.Resource.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
        cleanup.Deployment.PossibleCost = true;
        cleanup.Deployment.TimeStampUpdated = cleanup.Resource.TimeStampUpdated;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        RuntimeReleaseResult release;
        try
        {
            release = await provider.ReleaseAsync(
                    new RuntimeReleaseRequest(
                        cleanup.Deployment.GameServerFk,
                        cleanup.Deployment.PrimaryKey,
                        identifier,
                        ParseIntent(cleanup.Resource),
                        cleanup.WorkerId),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            release = new RuntimeReleaseResult(
                ERuntimeProviderOutcome.TransientFailure,
                "release-threw",
                LifecycleFailureSanitizer.Sanitize(ex),
                false);
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (release.Succeeded && release.IsVerifiedNonBillable)
        {
            cleanup.Resource.State = EDeploymentResourceState.Released;
            cleanup.Resource.PossibleCost = false;
            cleanup.Resource.TimeStampObserved = now;
            cleanup.Resource.TimeStampReleased = now;
            cleanup.Resource.TimeStampUpdated = now;
            cleanup.Deployment.PossibleCost = cleanup.Deployment.Resources.Any(q =>
                q.PrimaryKey != cleanup.Resource.PrimaryKey
                && q.TimeStampReleased == null
                && q.PossibleCost);
            cleanup.Deployment.TimeStampUpdated = now;
            if (cleanup.Deployment.TimeStampResolved.HasValue && !cleanup.Deployment.PossibleCost)
                cleanup.Deployment.Stage = "late-provider-resource-cleanup-complete";
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.DeploymentResourceReleased,
                "Scheduled reconciliation verified a linked provider resource non-billable.",
                serverId: cleanup.Deployment.GameServerFk,
                deploymentId: cleanup.Deployment.PrimaryKey,
                workerId: cleanup.WorkerId == Guid.Empty ? null : cleanup.WorkerId,
                costOwnerId: cleanup.Deployment.CostOwnerFk,
                details: $"Provider={provider.ProviderIdentifier}; Kind={cleanup.Resource.Kind}; Identity={identifier}",
                costImpact: ECostImpact.StopsCost);
        }
        else
        {
            cleanup.Resource.State = release.Outcome is ERuntimeProviderOutcome.Unknown
                ? EDeploymentResourceState.Unknown
                : EDeploymentResourceState.ReleasePending;
            cleanup.Resource.PossibleCost = true;
            cleanup.Resource.TimeStampUpdated = now;
            cleanup.Deployment.PossibleCost = true;
            cleanup.Deployment.Stage = release.Stage;
            cleanup.Deployment.TimeStampUpdated = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReleaseDanglingResourceAsync(
        IRuntimeProvider provider,
        DanglingProviderResourceIncident incident,
        Guid? vereinCostOwnerId,
        CancellationToken cancellationToken)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        incident.Status = EDanglingResourceIncidentStatus.Releasing;
        incident.CleanupAttemptCount++;
        incident.TimeStampLastCleanupAttempt = now;
        incident.TimeStampUpdated = now;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        RuntimeReleaseResult release;
        if (!provider.Capabilities.SupportsPlatformManagedDanglingRelease)
        {
            release = new RuntimeReleaseResult(
                ERuntimeProviderOutcome.PermanentFailure,
                "dangling-release-unsupported",
                "The provider cannot safely release discovered UTF-managed dangling resources.",
                false);
        }
        else
        {
            try
            {
                release = await provider.ReleaseDanglingAsync(
                        new RuntimeDanglingReleaseRequest(
                            incident.ProviderResourceIdentifier,
                            incident.ResourceType),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                release = new RuntimeReleaseResult(
                    ERuntimeProviderOutcome.TransientFailure,
                    "dangling-release-threw",
                    LifecycleFailureSanitizer.Sanitize(ex),
                    false);
            }
        }

        now = NodaSystemClock.Instance.GetCurrentInstant();
        if (release.Succeeded && release.IsVerifiedNonBillable)
        {
            incident.Status = EDanglingResourceIncidentStatus.Resolved;
            incident.PossibleCost = false;
            incident.LastFailure = null;
            incident.TimeStampResolved = now;
            incident.TimeStampUpdated = now;
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.DanglingResourceCleanupSucceeded,
                "Scheduled reconciliation verified a dangling provider resource non-billable.",
                costOwnerId: vereinCostOwnerId,
                details: IncidentDetails(incident),
                costImpact: ECostImpact.StopsCost);
        }
        else
        {
            incident.Status = EDanglingResourceIncidentStatus.CleanupFailed;
            incident.PossibleCost = true;
            incident.LastFailure = Limit(LifecycleFailureSanitizer.Sanitize(release.Error), 2048);
            incident.TimeStampUpdated = now;
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.DanglingResourceCleanupFailed,
                "Automatic cleanup of a dangling provider resource remains unresolved.",
                costOwnerId: vereinCostOwnerId,
                details: $"{IncidentDetails(incident)}; Stage={release.Stage}; Failure={incident.LastFailure}",
                costImpact: ECostImpact.KeepsCostRunning);
        }
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReconcileMissingInventoriedResourcesAsync(
        IRuntimeProvider provider,
        IReadOnlyCollection<DeploymentResource> providerResources,
        IReadOnlyDictionary<Guid, GameServerDeployment> deployments,
        IReadOnlySet<string> discoveredKeys,
        CancellationToken cancellationToken)
    {
        foreach (var resource in providerResources.Where(q =>
                     q.TimeStampReleased == null
                     && q.ProviderResourceIdentifier != null
                     && !discoveredKeys.Contains(ResourceKey(q))))
        {
            if (!deployments.TryGetValue(resource.DeploymentFk, out var deployment))
                continue;
            var workerId = resource.WorkerFk
                           ?? deployment.Workers
                               .Where(q => q.TimeStampStopped == null)
                               .Select(q => q.PrimaryKey)
                               .FirstOrDefault();
            RuntimeObservationResult observation;
            try
            {
                observation = await provider.ObserveAsync(
                        new RuntimeObservationRequest(
                            deployment.GameServerFk,
                            deployment.PrimaryKey,
                            resource.ProviderResourceIdentifier!,
                            ParseIntent(resource),
                            workerId),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                observation = new RuntimeObservationResult(
                    ERuntimeProviderOutcome.TransientFailure,
                    "observe-threw",
                    LifecycleFailureSanitizer.Sanitize(ex),
                    null);
            }

            var now = NodaSystemClock.Instance.GetCurrentInstant();
            if (observation.Outcome is ERuntimeProviderOutcome.NotFound)
            {
                resource.State = EDeploymentResourceState.Released;
                resource.PossibleCost = false;
                resource.TimeStampObserved = now;
                resource.TimeStampReleased = now;
                resource.TimeStampUpdated = now;
                if (IsExpectedActive(deployment))
                {
                    await automaticCostStopService.TriggerAsync(
                            deployment.PrimaryKey,
                            "provider-resource-not-found",
                            "A required provider resource is confirmed absent.",
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                deployment.PossibleCost = deployment.Resources.Any(q =>
                    q.PrimaryKey != resource.PrimaryKey && q.TimeStampReleased == null && q.PossibleCost);
                deployment.TimeStampUpdated = now;
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.DeploymentResourceObserved,
                    "Scheduled reconciliation confirmed an inventoried provider resource absent.",
                    serverId: deployment.GameServerFk,
                    deploymentId: deployment.PrimaryKey,
                    workerId: workerId == Guid.Empty ? null : workerId,
                    costOwnerId: deployment.CostOwnerFk,
                    details: $"Provider={provider.ProviderIdentifier}; Kind={resource.Kind}; "
                             + $"Identity={resource.ProviderResourceIdentifier}",
                    costImpact: ECostImpact.StopsCost);
            }
            else if (observation.Succeeded && observation.Resource is not null)
            {
                UpdateResourceObservation(resource, observation.Resource);
                deployment.PossibleCost |= observation.Resource.IsPotentiallyCostGenerating;
                deployment.TimeStampUpdated = now;
                if (ShouldRelease(deployment))
                {
                    await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await ReleaseLinkedResourceAsync(
                            provider,
                            new LinkedCleanup(resource, deployment, workerId),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                resource.State = EDeploymentResourceState.Unknown;
                resource.PossibleCost = true;
                resource.TimeStampUpdated = now;
                deployment.PossibleCost = true;
                deployment.Stage = observation.Stage;
                deployment.TimeStampUpdated = now;
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ConfirmAbsentIssuedProvisioningIntentsAsync(
        IRuntimeProvider provider,
        IEnumerable<DeploymentResource> providerResources,
        IReadOnlyDictionary<Guid, GameServerDeployment> deployments,
        IReadOnlyCollection<RuntimeResource> discoveredResources,
        IReadOnlyDictionary<Guid, HashSet<Guid>> activeDeploymentIdsByServer,
        IDictionary<Guid, ReopenedDeployment> reopenedDeployments,
        CancellationToken cancellationToken)
    {
        var confirmationWindow = Duration.FromTimeSpan(_options.AmbiguousCreateAbsenceConfirmationWindow);
        var candidates = providerResources.Where(q =>
                string.Equals(q.ProviderIdentifier, provider.ProviderIdentifier, StringComparison.OrdinalIgnoreCase)
                && q.ProviderResourceIdentifier == null
                && q.TimeStampProvisioningRequested.HasValue)
            .ToArray();
        foreach (var resource in candidates)
        {
            if (!deployments.TryGetValue(resource.DeploymentFk, out var deployment)
                || !ShouldRelease(deployment))
            {
                continue;
            }
            var intentId = ParseIntent(resource);
            var positiveEvidence = discoveredResources.FirstOrDefault(q =>
                string.Equals(q.ProviderIdentifier, provider.ProviderIdentifier, StringComparison.OrdinalIgnoreCase)
                && q.Attribution?.DeploymentId == deployment.PrimaryKey
                && q.Attribution.ProvisioningIntentId == intentId
                && (q.IsAllocated || q.IsPotentiallyCostGenerating));
            if (positiveEvidence is null
                && (resource.TimeStampProvisioningResolved.HasValue || resource.TimeStampReleased.HasValue))
            {
                continue;
            }

            await using var transaction = await BeginServerMutationAsync(
                    deployment.GameServerFk,
                    cancellationToken)
                .ConfigureAwait(false);
            await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.Entry(resource).ReloadAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(
                    resource.ProviderIdentifier,
                    provider.ProviderIdentifier,
                    StringComparison.OrdinalIgnoreCase)
                || resource.ProviderResourceIdentifier is not null
                || !resource.TimeStampProvisioningRequested.HasValue
                || !ShouldRelease(deployment))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            // The marker may be written by one API instance and evaluated by another. Use the shared database clock
            // for both operations so host clock skew or a local clock correction cannot shorten the safety window.
            var now = await GetDatabaseNowAsync(cancellationToken).ConfigureAwait(false);
            if (positiveEvidence is not null)
            {
                // Positive evidence invalidates any earlier empty-observation window. Cleanup the concrete resource
                // normally, then require two fresh empty confirmations. Reopen a synthetic absence closure if another
                // reconciler instance won that race before this positive observation acquired the server lock.
                var wasSyntheticallyClosed = resource.TimeStampProvisioningResolved.HasValue
                                             || resource.TimeStampReleased.HasValue;
                resource.State = EDeploymentResourceState.Unknown;
                resource.PossibleCost = true;
                resource.TimeStampObserved = null;
                resource.TimeStampProvisioningResolved = null;
                resource.TimeStampReleased = null;
                resource.TimeStampUpdated = now;
                RestoreLateCleanupState(
                    deployment,
                    positiveEvidence,
                    activeDeploymentIdsByServer,
                    reopenedDeployments);
                if (wasSyntheticallyClosed)
                {
                    auditWriter.Add(
                        dbContext,
                        EHostingAuditEventKind.ProviderReconciliationDegraded,
                        "Positive provider evidence reopened an issued create intent that had been confirmed absent.",
                        serverId: deployment.GameServerFk,
                        deploymentId: deployment.PrimaryKey,
                        costOwnerId: deployment.CostOwnerFk,
                        details: $"Provider={provider.ProviderIdentifier}; Intent={resource.ProvisioningIntentIdentifier}",
                        costImpact: ECostImpact.KeepsCostRunning);
                }
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (resource.TimeStampProvisioningResolved.HasValue || resource.TimeStampReleased.HasValue)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (!resource.TimeStampObserved.HasValue)
            {
                resource.TimeStampObserved = now;
                resource.TimeStampUpdated = now;
                deployment.Stage = "provider-create-absence-confirmation-pending";
                deployment.PossibleCost = true;
                deployment.TimeStampUpdated = now;
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.ProviderReconciliationDegraded,
                    "Provider-wide discovery first observed an issued create intent absent; confirmation is pending.",
                    serverId: deployment.GameServerFk,
                    deploymentId: deployment.PrimaryKey,
                    costOwnerId: deployment.CostOwnerFk,
                    details: $"Provider={provider.ProviderIdentifier}; Intent={resource.ProvisioningIntentIdentifier}",
                    costImpact: ECostImpact.Unknown);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (now - resource.TimeStampObserved.Value < confirmationWindow)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            resource.State = EDeploymentResourceState.Released;
            resource.PossibleCost = false;
            resource.TimeStampProvisioningResolved = now;
            resource.TimeStampReleased = now;
            resource.TimeStampUpdated = now;
            deployment.PossibleCost = deployment.Resources.Any(q =>
                q.PrimaryKey != resource.PrimaryKey && q.TimeStampReleased == null && q.PossibleCost);
            deployment.Stage = "provider-create-confirmed-absent";
            deployment.TimeStampUpdated = now;
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.DeploymentResourceReleased,
                "Repeated provider-wide discovery confirmed that an issued create intent produced no resource.",
                serverId: deployment.GameServerFk,
                deploymentId: deployment.PrimaryKey,
                costOwnerId: deployment.CostOwnerFk,
                details: $"Provider={provider.ProviderIdentifier}; Intent={resource.ProvisioningIntentIdentifier}",
                costImpact: ECostImpact.StopsCost);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void RestoreLateCleanupState(
        GameServerDeployment deployment,
        RuntimeResource observed,
        IReadOnlyDictionary<Guid, HashSet<Guid>> activeDeploymentIdsByServer,
        IDictionary<Guid, ReopenedDeployment> reopenedDeployments)
    {
        deployment.PossibleCost |= observed.IsPotentiallyCostGenerating || observed.IsAllocated;
        deployment.IsProvisioningBlocked = true;
        deployment.Stage = "late-provider-resource-cleanup";
        deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
        if (!deployment.TimeStampResolved.HasValue)
            return;

        var hasNewerActiveDeployment = activeDeploymentIdsByServer.TryGetValue(
                                           deployment.GameServerFk,
                                           out var activeIds)
                                       && activeIds.Any(q => q != deployment.PrimaryKey);
        if (hasNewerActiveDeployment)
            return;
        if (!reopenedDeployments.ContainsKey(deployment.PrimaryKey))
        {
            reopenedDeployments.Add(
                deployment.PrimaryKey,
                new ReopenedDeployment(deployment, deployment.Status));
        }
        deployment.TimeStampResolved = null;
        deployment.Status = deployment.Status switch
        {
            EDeploymentStatus.Stopped => EDeploymentStatus.Stopping,
            EDeploymentStatus.Terminated => EDeploymentStatus.Terminating,
            EDeploymentStatus.TerminatedDueToFault => EDeploymentStatus.TerminatingDueToFault,
            _ => EDeploymentStatus.TerminatingDueToFault,
        };
    }

    private async Task<IReadOnlySet<Guid>> VerifyTeardownBarrierAsync(
        IRuntimeProvider provider,
        IEnumerable<GameServerDeployment> deployments,
        CancellationToken cancellationToken)
    {
        var candidates = deployments
            .Where(CanFinalizeFromDurableInventory)
            .ToDictionary(q => q.PrimaryKey);
        if (candidates.Count == 0)
            return new HashSet<Guid>();

        RuntimeDiscoveryResult verification;
        try
        {
            verification = await provider.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            verification = new RuntimeDiscoveryResult(
                ERuntimeProviderOutcome.TransientFailure,
                "final-verification-threw",
                LifecycleFailureSanitizer.Sanitize(ex),
                []);
        }
        if (!verification.Succeeded)
        {
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.ProviderReconciliationDegraded,
                "Final provider-wide teardown verification is degraded; deployments remain unresolved.",
                details: $"Provider={provider.ProviderIdentifier}; Stage={verification.Stage}; "
                         + $"Failure={verification.Error}",
                costImpact: ECostImpact.Unknown);
            return new HashSet<Guid>();
        }

        var blocked = new HashSet<Guid>();
        foreach (var observed in verification.Resources.Where(q =>
                     string.Equals(
                         q.ProviderIdentifier,
                         provider.ProviderIdentifier,
                         StringComparison.OrdinalIgnoreCase)
                     && (q.IsAllocated || q.IsPotentiallyCostGenerating)))
        {
            var matchingDeployments = candidates.Values.Where(deployment =>
                    observed.Attribution?.DeploymentId == deployment.PrimaryKey
                    || deployment.Resources.Any(resource => ResourceMatches(resource, observed)))
                .ToArray();
            foreach (var deployment in matchingDeployments)
            {
                blocked.Add(deployment.PrimaryKey);
                deployment.PossibleCost = true;
                deployment.Stage = "provider-final-verification-found-resource";
                deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.ProviderReconciliationDegraded,
                    "Final provider-wide teardown verification found a remaining attributable resource.",
                    serverId: deployment.GameServerFk,
                    deploymentId: deployment.PrimaryKey,
                    costOwnerId: deployment.CostOwnerFk,
                    details: ResourceDetails(provider, observed),
                    costImpact: ECostImpact.KeepsCostRunning);
            }
        }

        return candidates.Keys.Where(q => !blocked.Contains(q)).ToHashSet();
    }

    private static bool CanFinalizeFromDurableInventory(GameServerDeployment deployment)
        => deployment.TimeStampResolved is null
           && deployment.Status is (EDeploymentStatus.Stopping
               or EDeploymentStatus.Terminating
               or EDeploymentStatus.TerminatingDueToFault)
           && IsProviderTeardownAuthorized(deployment)
           && deployment.Resources.All(q =>
               !q.PossibleCost
               && !(q.TimeStampProvisioningRequested.HasValue && !q.TimeStampProvisioningResolved.HasValue)
               && !(q.ProviderResourceIdentifier != null && !q.TimeStampReleased.HasValue));

    private async Task FinalizeVerifiedTeardownsAsync(
        IEnumerable<GameServerDeployment> deployments,
        IReadOnlyCollection<Guid> reopenedDeploymentIds,
        IReadOnlySet<Guid> providerWideVerifiedDeploymentIds,
        CancellationToken cancellationToken)
    {
        foreach (var deployment in deployments)
        {
            if (!providerWideVerifiedDeploymentIds.Contains(deployment.PrimaryKey))
                continue;

            // Re-evaluate the durable state under the same per-server lock used by lifecycle requests and provider
            // creates. In particular, a user Terminate committed during provider verification must not be finalized
            // with the older ordinary-Stop semantics held by this reconciler context.
            await using var transaction = await BeginServerMutationAsync(deployment.GameServerFk, cancellationToken)
                .ConfigureAwait(false);
            await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
            if (deployment.TimeStampResolved.HasValue
                || deployment.Status is not (EDeploymentStatus.Stopping
                    or EDeploymentStatus.Terminating
                    or EDeploymentStatus.TerminatingDueToFault)
                || !await IsProviderTeardownAuthorizedForFinalizationAsync(deployment, cancellationToken)
                    .ConfigureAwait(false))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }
            var now = NodaSystemClock.Instance.GetCurrentInstant();
            var hasLiveRunningLifecycleOwner = await dbContext.LifecycleJobs.AnyAsync(
                    q => q.DeploymentFk == deployment.PrimaryKey
                         && q.Status == ELifecycleJobStatus.Running
                         && q.LeaseOwner != null
                         && q.LeaseExpires > now,
                    cancellationToken)
                .ConfigureAwait(false);
            if (hasLiveRunningLifecycleOwner)
            {
                // Reconciliation may verify and release resources, but an executor that currently owns the durable
                // job must publish terminal metadata after its in-flight provider call returns.
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }
            var hasBlockingInventory = await dbContext.DeploymentResources
                .AsNoTracking()
                .AnyAsync(
                    q => q.DeploymentFk == deployment.PrimaryKey
                         && (q.PossibleCost
                             || q.TimeStampProvisioningRequested.HasValue
                             && !q.TimeStampProvisioningResolved.HasValue
                             || q.ProviderResourceIdentifier != null && !q.TimeStampReleased.HasValue),
                    cancellationToken)
                .ConfigureAwait(false);
            if (hasBlockingInventory)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var previousStatus = deployment.Status;
            var activeWorkerIds = await dbContext.LifecycleWorkers
                .AsNoTracking()
                .Where(q => q.DeploymentFk == deployment.PrimaryKey && q.TimeStampStopped == null)
                .Select(q => q.PrimaryKey)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var activeWorkerId in activeWorkerIds)
            {
                var worker = await dbContext.LifecycleWorkers
                    .SingleAsync(q => q.PrimaryKey == activeWorkerId, cancellationToken)
                    .ConfigureAwait(false);
                await dbContext.Entry(worker).ReloadAsync(cancellationToken).ConfigureAwait(false);
                if (worker.TimeStampStopped.HasValue)
                    continue;
                workerCredentialService.RevokeWorkerCredential(worker, now);
                worker.Status = EWorkerStatus.Offline;
                worker.ProcessId = null;
                worker.ProcessStartedAt = null;
                worker.TimeStampStopped = now;
                worker.TimeStampUpdated = now;
            }
            deployment.PossibleCost = false;
            deployment.Status = previousStatus switch
            {
                EDeploymentStatus.Stopping => EDeploymentStatus.Stopped,
                EDeploymentStatus.Terminating => EDeploymentStatus.Terminated,
                EDeploymentStatus.TerminatingDueToFault => EDeploymentStatus.TerminatedDueToFault,
                _ => throw new ArgumentOutOfRangeException(),
            };
            deployment.Stage = reopenedDeploymentIds.Contains(deployment.PrimaryKey)
                ? "late-provider-resource-cleanup-complete"
                : deployment.Status switch
                {
                    EDeploymentStatus.Stopped => "stopped",
                    EDeploymentStatus.Terminated => "terminated",
                    _ => "terminated-due-to-fault",
                };
            deployment.TimeStampResolved = now;
            deployment.TimeStampUpdated = now;
            auditWriter.Add(
                dbContext,
                deployment.Status == EDeploymentStatus.Stopped
                    ? EHostingAuditEventKind.DeploymentStopped
                    : EHostingAuditEventKind.DeploymentTerminated,
                "Scheduled reconciliation completed provider teardown and revoked the deployment worker.",
                serverId: deployment.GameServerFk,
                deploymentId: deployment.PrimaryKey,
                costOwnerId: deployment.CostOwnerFk,
                costImpact: ECostImpact.StopsCost);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> IsProviderTeardownAuthorizedForFinalizationAsync(
        GameServerDeployment deployment,
        CancellationToken cancellationToken)
    {
        if (deployment.Status is EDeploymentStatus.Terminating or EDeploymentStatus.TerminatingDueToFault)
            return true;
        if (deployment.Status != EDeploymentStatus.Stopping)
            return false;
        if (string.Equals(deployment.Stage, "late-provider-resource-cleanup", StringComparison.Ordinal))
            return true;
        return await dbContext.LifecycleJobs.AnyAsync(
                q => q.DeploymentFk == deployment.PrimaryKey
                     && q.Kind == ELifecycleJobKind.Stop
                     && q.ActionIdentifier == GameServerActions.Stop
                     && q.WorkerCommands.Any(command =>
                         command.ActionIdentifier == GameServerActions.Stop
                         && command.Status == EWorkerCommandStatus.Succeeded),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IDbContextTransaction> BeginServerMutationAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var isPostgreSql = dbContext.Database.ProviderName?.Contains(
            "Npgsql",
            StringComparison.OrdinalIgnoreCase) == true;
        var transaction = await dbContext.Database.BeginTransactionAsync(
                isPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
                cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (isPostgreSql)
            {
                await dbContext.GameServers
                    .FromSqlInterpolated(
                        $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {serverId} FOR UPDATE""")
                    .LoadAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"""UPDATE "GameServers" SET "TimeStampUpdated" = "TimeStampUpdated" WHERE "PrimaryKey" = {serverId}""",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task ReloadDeploymentResourcesAsync(Guid deploymentId, CancellationToken cancellationToken)
    {
        await dbContext.DeploymentResources
            .Where(q => q.DeploymentFk == deploymentId)
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        var entries = dbContext.ChangeTracker
            .Entries<DeploymentResource>()
            .Where(q => q.Entity.DeploymentFk == deploymentId && q.State is not EntityState.Added)
            .ToArray();
        foreach (var entry in entries)
            await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Instant> GetDatabaseNowAsync(CancellationToken cancellationToken)
    {
        var providerName = dbContext.Database.ProviderName;
        var sql = providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true
            ? "SELECT CAST(FLOOR(EXTRACT(EPOCH FROM clock_timestamp()) * 1000) AS bigint) AS \"Value\""
            : providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
                ? "SELECT CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER) AS \"Value\""
                : null;
        if (sql is null)
            return NodaSystemClock.Instance.GetCurrentInstant();

        var unixTimeMilliseconds = await dbContext.Database.SqlQueryRaw<long>(sql)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return Instant.FromUnixTimeMilliseconds(unixTimeMilliseconds);
    }

    private async Task RemoveExpiredResolvedIncidentsAsync(CancellationToken cancellationToken)
    {
        var cutoff = NodaSystemClock.Instance.GetCurrentInstant()
                     - Duration.FromTimeSpan(_options.CompletedIncidentRetention);
        var expired = await dbContext.DanglingProviderResourceIncidents
            .Where(q => q.Status == EDanglingResourceIncidentStatus.Resolved
                        && q.TimeStampResolved < cutoff)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (expired.Length == 0)
            return;
        dbContext.DanglingProviderResourceIncidents.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void UpdateResourceObservation(DeploymentResource resource, RuntimeResource observed)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        resource.ProviderResourceIdentifier = observed.ResourceIdentifier;
        resource.WorkerFk ??= observed.Attribution?.WorkerId;
        resource.Kind = observed.ResourceType;
        RuntimeResourceFacts.Apply(resource, observed);
        resource.State = observed.IsAllocated
            ? EDeploymentResourceState.Allocated
            : EDeploymentResourceState.Released;
        resource.PossibleCost = observed.IsPotentiallyCostGenerating;
        resource.TimeStampObserved = now;
        resource.TimeStampUpdated = now;
        resource.TimeStampProvisioningResolved ??= now;
        resource.TimeStampReleased = observed.IsAllocated ? null : now;
    }

    private static bool HasConflictingTrustedDeploymentAttribution(
        DeploymentResource existing,
        RuntimeResource observed)
        => observed.HasTrustworthyAttribution
           && observed.Attribution?.DeploymentId.HasValue == true
           && observed.Attribution.DeploymentId.Value != existing.DeploymentFk;

    private static bool ShouldRelease(GameServerDeployment deployment)
        => IsProviderTeardownAuthorized(deployment);

    private static bool IsProviderTeardownAuthorized(GameServerDeployment deployment)
        => deployment.TimeStampResolved.HasValue
           || deployment.Status is EDeploymentStatus.Terminating
               or EDeploymentStatus.TerminatingDueToFault
               or EDeploymentStatus.Stopped
               or EDeploymentStatus.Terminated
               or EDeploymentStatus.TerminatedDueToFault
           || deployment.Status is EDeploymentStatus.Stopping
           && (string.Equals(
                   deployment.Stage,
                   "late-provider-resource-cleanup",
                   StringComparison.Ordinal)
               || deployment.LifecycleJobs.Any(q =>
                   q.Kind == ELifecycleJobKind.Stop
                   && string.Equals(q.ActionIdentifier, GameServerActions.Stop, StringComparison.Ordinal)
                   && q.WorkerCommands.Any(command =>
                       string.Equals(command.ActionIdentifier, GameServerActions.Stop, StringComparison.Ordinal)
                       && command.Status == EWorkerCommandStatus.Succeeded)));

    private static bool IsExpectedActive(GameServerDeployment deployment)
        => !deployment.IsProvisioningBlocked
           && !deployment.TimeStampResolved.HasValue
           && deployment.Status is EDeploymentStatus.Provisioning
               or EDeploymentStatus.WaitingForWorker
               or EDeploymentStatus.Preparing
               or EDeploymentStatus.Running;

    private static Guid ResolveWorkerId(GameServerDeployment deployment, RuntimeResource observed)
        => observed.Attribution?.WorkerId
           ?? deployment.Workers.Where(q => q.TimeStampStopped == null)
               .Select(q => q.PrimaryKey)
               .FirstOrDefault();

    private static Guid ParseIntent(DeploymentResource resource)
        => Guid.TryParse(resource.ProvisioningIntentIdentifier, out var parsed)
            ? parsed
            : Guid.Empty;

    private static bool ResourceMatches(DeploymentResource resource, RuntimeResource observed)
        => string.Equals(resource.Kind, observed.ResourceType, StringComparison.OrdinalIgnoreCase)
           && string.Equals(
               resource.ProviderResourceIdentifier,
               observed.ResourceIdentifier,
               StringComparison.Ordinal);

    private static string ResourceKey(RuntimeResource resource)
        => $"{resource.ResourceType.ToUpperInvariant()}\0{resource.ResourceIdentifier}";

    private static string ResourceKey(DeploymentResource resource)
        => $"{resource.Kind.ToUpperInvariant()}\0{resource.ProviderResourceIdentifier}";

    private static string ResourceDetails(IRuntimeProvider provider, RuntimeResource resource)
        => $"Provider={provider.ProviderIdentifier}; Kind={resource.ResourceType}; "
           + $"Identity={resource.ResourceIdentifier}; State={resource.ProviderState ?? "unknown"}";

    private static string IncidentDetails(DanglingProviderResourceIncident incident)
        => $"Provider={incident.ProviderIdentifier}; Kind={incident.ResourceType}; "
           + $"Identity={incident.ProviderResourceIdentifier}";

    private static string? Limit(string? value, int maximumLength)
        => string.IsNullOrEmpty(value) || value.Length <= maximumLength ? value : value[..maximumLength];

    private static int ProviderReleaseOrder(string resourceKind)
        => resourceKind.ToLowerInvariant() switch
        {
            "primary-ip" => 0,
            "server" => 1,
            "firewall" => 2,
            _ => 3,
        };

    private sealed record ReconciliationLink(GameServerDeployment Deployment, Guid WorkerId);

    private sealed record LinkedCleanup(
        DeploymentResource Resource,
        GameServerDeployment Deployment,
        Guid WorkerId);

    private sealed record ReopenedDeployment(
        GameServerDeployment Deployment,
        EDeploymentStatus OriginalStatus);
}
