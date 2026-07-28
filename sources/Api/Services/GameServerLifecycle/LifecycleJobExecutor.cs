using System.Text.Json;
using System.Diagnostics;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Coordinates durable lifecycle intent across the cloud provider and one external deployment-bound worker.
/// No game process or worker-local side effect executes in the API process.
/// </summary>
[Scoped<LifecycleJobExecutor, ILifecycleJobExecutor>]
internal sealed class LifecycleJobExecutor(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog gameServerDefinitions,
    IEnumerable<IRuntimeProvider> runtimeProviders,
    IWorkerCredentialService workerCredentialService,
    IWorkerBootstrapPayloadRenderer bootstrapPayloadRenderer,
    IGameServerContentSnapshotResolver contentSnapshotResolver,
    IHostingAuthorizationService authorizationService,
    IHostingCostSliceService costSliceService,
    IHostingAuditWriter auditWriter,
    IOptions<LifecycleWorkerOptions> options) : ILifecycleJobExecutor
{
    private static readonly JsonSerializerOptions CommandJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LifecycleWorkerOptions _options = options.Value;

    public async Task ExecuteAsync(LifecycleJob lifecycleJob, CancellationToken cancellationToken = default)
    {
        var expectedLeaseOwner = lifecycleJob.LeaseOwner;
        var expectedAttempt = lifecycleJob.Attempt;
        var job = await dbContext.LifecycleJobs
            .Include(q => q.GameServer)!
            .ThenInclude(q => q!.CurrentCostOwner)
            .Include(q => q.Deployment)!
            .ThenInclude(q => q!.Resources)
            .Include(q => q.Deployment)!
            .ThenInclude(q => q!.Workers)
            .Include(q => q.WorkerCommands)
            .SingleOrDefaultAsync(q => q.PrimaryKey == lifecycleJob.PrimaryKey, cancellationToken)
            .ConfigureAwait(false)
                  ?? throw new LifecycleExecutionException("The claimed lifecycle job no longer exists.");
        if (job.Status is not ELifecycleJobStatus.Running)
            return;

        var server = job.GameServer
                     ?? throw new LifecycleExecutionException("The lifecycle job has no server record.");
        var deployment = job.Deployment
                         ?? throw new LifecycleExecutionException("The lifecycle job has no deployment record.");
        using var activity = HostingTelemetry.StartRuntimeOperation(
            server.RuntimeProviderIdentifier,
            $"lifecycle.{job.ActionIdentifier}",
            server.PrimaryKey,
            deployment.PrimaryKey);
        activity?.SetTag("utf.hosting.lifecycle.job.id", job.PrimaryKey);
        activity?.SetTag("utf.hosting.lifecycle.job.kind", job.Kind.ToString());

        try
        {
            if (deployment.Status is EDeploymentStatus.TerminatingDueToFault
                && job.Kind is not ELifecycleJobKind.Terminate)
            {
                await CancelNormalCommandsAsync(deployment.PrimaryKey, null, cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                var activeWorker = deployment.Workers.SingleOrDefault(q => q.TimeStampStopped == null);
                if (activeWorker is not null)
                {
                    await CollectBoundedFaultEvidenceAsync(job, server, deployment, activeWorker, cancellationToken)
                        .ConfigureAwait(false);
                }
                await ReleaseDeploymentAsync(
                        job,
                        server,
                        deployment,
                        terminate: true,
                        faultTriggered: true,
                        cancellationToken)
                    .ConfigureAwait(false);
                throw new PermanentLifecycleExecutionException(
                    deployment.FaultMessage ?? "The deployment was terminated after a confirmed fault.");
            }

            switch (job.Kind)
            {
                case ELifecycleJobKind.Start:
                    await ExecuteStartAsync(
                            job,
                            server,
                            deployment,
                            expectedLeaseOwner,
                            expectedAttempt,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case ELifecycleJobKind.Stop:
                    await ExecuteStopAsync(job, server, deployment, cancellationToken).ConfigureAwait(false);
                    break;
                case ELifecycleJobKind.Terminate:
                    await ExecuteTerminateAsync(job, server, deployment, cancellationToken).ConfigureAwait(false);
                    break;
                case ELifecycleJobKind.Restart:
                case ELifecycleJobKind.Install:
                case ELifecycleJobKind.Update:
                case ELifecycleJobKind.CollectLogs:
                case ELifecycleJobKind.Custom:
                    await ExecuteRuntimeActionAsync(job, server, deployment, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(job.Kind), job.Kind, "Unknown lifecycle job kind.");
            }
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HostingTelemetry.RecordException(activity, ex, HostingFailureSanitizer.Sanitize(ex));
            throw;
        }
    }

    private async Task ExecuteStartAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        string? expectedLeaseOwner,
        int expectedAttempt,
        CancellationToken cancellationToken)
    {
        await RevalidateStartAsync(job, server, deployment, cancellationToken).ConfigureAwait(false);
        var gameDefinition = gameServerDefinitions.GetDefinitions().SingleOrDefault(q => string.Equals(
            q.GameIdentifier,
            server.GameIdentifier,
            StringComparison.OrdinalIgnoreCase));
        if (gameDefinition is null)
        {
            await RejectStartAsync(
                    job,
                    server,
                    deployment,
                    "game-definition-unavailable",
                    "The server's game definition is unavailable.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var networkFailure = RuntimeInboundNetworkPlanValidator.Validate(
            gameDefinition.InboundNetworkRequirements);
        if (networkFailure is not null)
        {
            await RejectStartAsync(
                    job,
                    server,
                    deployment,
                    "game-network-plan-invalid",
                    networkFailure,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var provider = ResolveProvider(server.RuntimeProviderIdentifier);
        if (!provider.Capabilities.SupportsStableProvisioningIntent
            || !provider.Capabilities.SupportsProviderWideDiscovery
            || !provider.Capabilities.SupportsVerifiedRelease
            || !provider.Capabilities.IsReady)
        {
            await RejectStartAsync(
                    job,
                    server,
                    deployment,
                    "provider-attribution-unsupported",
                    provider.Capabilities.ReadinessFailure
                    ?? "The selected provider cannot reliably attribute, discover, and verify release of its resources.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var intent = await EnsureProvisioningIntentAsync(job, server, deployment, provider, cancellationToken)
            .ConfigureAwait(false);
        RuntimeResource? observedResource;
        try
        {
            observedResource = await FindExpectedResourceAsync(
                    job,
                    server,
                    deployment,
                    intent.Worker,
                    intent.Resource,
                    provider,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ProviderResourceGraphFaultException ex)
        {
            await TriggerFaultCostStopAsync(
                    job,
                    server,
                    deployment,
                    intent.Worker,
                    ex.FaultCode,
                    ex.Message,
                    cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
        catch (PermanentLifecycleExecutionException ex) when (
            intent.Resource.ProviderResourceIdentifier is null
            && intent.Resource.TimeStampProvisioningRequested is null
            && !intent.Resource.PossibleCost)
        {
            await RejectStartAsync(
                    job,
                    server,
                    deployment,
                    "provider-unavailable-before-create",
                    ex.Message,
                    cancellationToken)
                .ConfigureAwait(false);
            throw;
        }

        if (observedResource is null)
        {
            await ThrowIfProvisioningBlockedAsync(deployment.PrimaryKey, cancellationToken).ConfigureAwait(false);
            // Validate and render all local bootstrap inputs before reserving money. A configuration failure here has
            // not reached the provider and therefore must not leave financial headroom reserved.
            WorkerBootstrapPayload bootstrapPayload;
            try
            {
                var bootstrap = workerCredentialService.IssueBootstrapCredential(intent.Worker);
                bootstrapPayload = bootstrapPayloadRenderer.Render(
                    bootstrap,
                    server.PrimaryKey,
                    server.GameIdentifier,
                    gameDefinition.InboundNetworkRequirements);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                workerCredentialService.RevokeWorkerCredential(
                    intent.Worker,
                    NodaSystemClock.Instance.GetCurrentInstant());
                await RejectStartAsync(
                        job,
                        server,
                        deployment,
                        "worker-bootstrap-invalid",
                        HostingFailureSanitizer.Sanitize(ex),
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            try
            {
                await costSliceService.ReserveInitialSliceAsync(
                        deployment.PrimaryKey,
                        provider.ProviderIdentifier,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HostingAffordabilityException ex)
            {
                var providerMutationWasIssued = intent.Resource.TimeStampProvisioningRequested.HasValue
                                                || deployment.Resources.Any(q =>
                                                    q.ProviderResourceIdentifier != null
                                                    && q.TimeStampReleased == null);
                if (providerMutationWasIssued)
                {
                    await TriggerFaultCostStopAsync(
                            job,
                            server,
                            deployment,
                            intent.Worker,
                            "provider-cost-authorization-exhausted",
                            ex.Message,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    workerCredentialService.RevokeWorkerCredential(
                        intent.Worker,
                        NodaSystemClock.Instance.GetCurrentInstant());
                    await RejectStartAsync(
                            job,
                            server,
                            deployment,
                            "provider-cost-unaffordable",
                            ex.Message,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            await ThrowIfProvisioningBlockedAsync(deployment.PrimaryKey, cancellationToken).ConfigureAwait(false);
            await SetJobProgressAsync(job, "provider-provisioning", 15, cancellationToken).ConfigureAwait(false);
            var provisionRequest = new RuntimeProvisionRequest(
                server.PrimaryKey,
                deployment.PrimaryKey,
                intent.Worker.PrimaryKey,
                server.GameIdentifier,
                intent.IntentId,
                bootstrapPayload.CloudInit,
                gameDefinition.InboundNetworkRequirements,
                deployment.RuntimeProfileIdentifier);

            // Atomically claim the one permitted provider mutation for this durable intent. This server-row lock is
            // cross-process: overlapping lease holders may both have observed an empty provider graph, but only one
            // can persist TimeStampProvisioningRequested and proceed to the provider transport. Once claimed, enter
            // the provider immediately: its first guarded create must either run before Terminate or return a
            // definitive closed-attempt result after Terminate. No fallible state update belongs in that interval.
            await ClaimProvisioningMutationAsync(
                    job,
                    server,
                    deployment,
                    intent.Worker,
                    intent.Resource,
                    provider,
                    expectedLeaseOwner,
                    expectedAttempt,
                    cancellationToken)
                .ConfigureAwait(false);
            // A normal lease cancellation (including one caused by Terminate) must not interrupt the provider call
            // after the durable create claim. Bound it independently so a broken provider cannot hang shutdown.
            using var providerCallTimeout = new CancellationTokenSource(_options.ProviderSettlementTimeout);
            var provision = await provider.ProvisionAsync(provisionRequest, providerCallTimeout.Token)
                .ConfigureAwait(false);
            // A result returned near the provider-call deadline still needs a full bounded window for mandatory
            // inventory, accounting, and closure. Reusing the almost-expired call token could discard known IDs.
            using var resultSettlementTimeout = new CancellationTokenSource(_options.ProviderSettlementTimeout);
            var settlementCancellationToken = resultSettlementTimeout.Token;
            var hasPotentialProviderCost = provision.ProvisionedResources.Any(q => q.IsPotentiallyCostGenerating);
            var providerResultRequiresCostAccounting = provision.Succeeded
                                                       || hasPotentialProviderCost
                                                       || provision.Outcome is ERuntimeProviderOutcome.Unknown;
            if (provision.ProvisionedResources.Count > 0 || providerResultRequiresCostAccounting)
            {
                // Terminate may commit immediately after the provider's final guarded POST. Re-enter the shared
                // server lock and reload all authoritative provider state before saving the concrete result; otherwise
                // stale deployment concurrency tokens can roll back every returned provider identifier.
                await using var resultTransaction = await BeginServerMutationAsync(
                        server.PrimaryKey,
                        settlementCancellationToken)
                    .ConfigureAwait(false);
                await dbContext.Entry(deployment).ReloadAsync(settlementCancellationToken).ConfigureAwait(false);
                await ReloadDeploymentProviderStateAsync(deployment.PrimaryKey, settlementCancellationToken)
                    .ConfigureAwait(false);
                if (provision.ProvisionedResources.Count > 0)
                {
                    await AdoptDiscoveredResourcesAsync(
                            deployment,
                            provider,
                            provision.ProvisionedResources,
                            intent.Resource,
                            settlementCancellationToken,
                            updateDeploymentCostState: false)
                        .ConfigureAwait(false);
                }
                if (provision.Outcome is ERuntimeProviderOutcome.Unknown)
                {
                    // The provider may have created a resource that is not observable yet. Make that ambiguity durable
                    // in the same transaction as every concrete result so crash recovery cannot treat it as no-cost.
                    intent.Resource.State = EDeploymentResourceState.Unknown;
                    intent.Resource.PossibleCost = true;
                    intent.Resource.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                    await dbContext.SaveChangesAsync(settlementCancellationToken).ConfigureAwait(false);
                }
                if (providerResultRequiresCostAccounting)
                {
                    // Do not enlist the tracked deployment in optimistic concurrency here: a reconciler may update an
                    // unrelated summary field while this returned graph is being inventoried. A targeted update keeps
                    // resource identifiers and the cost summary atomic without allowing that race to roll them back.
                    var accountedAt = NodaSystemClock.Instance.GetCurrentInstant();
                    await dbContext.GameServerDeployments
                        .Where(q => q.PrimaryKey == deployment.PrimaryKey)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(q => q.PossibleCost, true)
                                .SetProperty(q => q.TimeStampUpdated, accountedAt),
                            settlementCancellationToken)
                        .ConfigureAwait(false);
                    await dbContext.Entry(deployment).ReloadAsync(settlementCancellationToken).ConfigureAwait(false);
                }
                await resultTransaction.CommitAsync(settlementCancellationToken).ConfigureAwait(false);
            }
            if (providerResultRequiresCostAccounting)
            {
                // A successful, potentially billable partial, or ambiguous mutation can already incur cost. Commit
                // the balance-neutral reservation exactly once; definitive non-billable failures release it below.
                await costSliceService.CommitInitialReservationAsync(
                        deployment.PrimaryKey,
                        provider.ProviderIdentifier,
                        settlementCancellationToken)
                    .ConfigureAwait(false);
            }
            if (provision.IsProvisioningAttemptDefinitivelyClosed)
            {
                await dbContext.Entry(deployment).ReloadAsync(settlementCancellationToken).ConfigureAwait(false);
                if (deployment.IsProvisioningBlocked)
                {
                    // Only an unseen placeholder can be closed without provider teardown. A concrete resource may
                    // have been returned by the guarded create even when its attribution/configuration is invalid and
                    // provider-wide discovery cannot find it. Keep that resource unresolved so Terminate or the
                    // reconciler must release its persisted provider identifier and verify the non-billable outcome.
                    if (intent.Resource.ProviderResourceIdentifier is null)
                    {
                        var resolvedAt = NodaSystemClock.Instance.GetCurrentInstant();
                        intent.Resource.State = EDeploymentResourceState.Released;
                        intent.Resource.PossibleCost = false;
                        intent.Resource.TimeStampProvisioningResolved = resolvedAt;
                        intent.Resource.TimeStampReleased = resolvedAt;
                        intent.Resource.TimeStampUpdated = resolvedAt;
                    }
                    await dbContext.SaveChangesAsync(settlementCancellationToken).ConfigureAwait(false);
                    if (!hasPotentialProviderCost)
                    {
                        await costSliceService.ReleaseInitialReservationAsync(
                                deployment.PrimaryKey,
                                "The durable provisioning barrier closed before another provider create was issued.",
                                settlementCancellationToken)
                            .ConfigureAwait(false);
                    }
                    throw new LifecycleSupersededException(
                        provision.Error ?? "Provisioning was stopped by the durable deployment barrier.");
                }

                if (provision.ProvisionedResources.Count == 0)
                {
                    // The provider completed no mutation (for example, its preflight discovery failed). Clear the
                    // durable create claim so a normal retry can safely try again without treating an unissued POST as
                    // an ambiguous late resource.
                    var resetAt = NodaSystemClock.Instance.GetCurrentInstant();
                    intent.Resource.State = EDeploymentResourceState.Provisioning;
                    intent.Resource.PossibleCost = false;
                    intent.Resource.TimeStampProvisioningRequested = null;
                    intent.Resource.TimeStampProvisioningResolved = null;
                    intent.Resource.TimeStampReleased = null;
                    intent.Resource.TimeStampUpdated = resetAt;
                    deployment.Stage = provision.Stage;
                    deployment.TimeStampUpdated = resetAt;
                    await dbContext.SaveChangesAsync(settlementCancellationToken).ConfigureAwait(false);
                    await costSliceService.ReleaseInitialReservationAsync(
                            deployment.PrimaryKey,
                            "The provider confirmed that no provisioning mutation was issued.",
                            settlementCancellationToken)
                        .ConfigureAwait(false);
                    if (provision.Outcome is ERuntimeProviderOutcome.PermanentFailure)
                    {
                        await RejectStartAsync(
                                job,
                                server,
                                deployment,
                                "provider-provisioning-invalid",
                                provision.Error ?? "The provider rejected the provisioning request.",
                                settlementCancellationToken)
                            .ConfigureAwait(false);
                    }
                    throw new LifecycleExecutionException(
                        provision.Error ?? "Provider provisioning preflight failed before any mutation was issued.",
                        provision.Stage);
                }

                // Every attempted POST has a conclusive outcome, but provisioning stopped with a partial or invalid
                // graph. Resolve the unseen primary placeholder before fault teardown; all concrete resources were
                // adopted above and remain individually subject to verified provider release.
                if (intent.Resource.ProviderResourceIdentifier is null)
                {
                    var resolvedAt = NodaSystemClock.Instance.GetCurrentInstant();
                    intent.Resource.State = EDeploymentResourceState.Released;
                    intent.Resource.PossibleCost = false;
                    intent.Resource.TimeStampProvisioningResolved = resolvedAt;
                    intent.Resource.TimeStampReleased = resolvedAt;
                    intent.Resource.TimeStampUpdated = resolvedAt;
                }
                await dbContext.SaveChangesAsync(settlementCancellationToken).ConfigureAwait(false);
                if (!hasPotentialProviderCost)
                {
                    await costSliceService.ReleaseInitialReservationAsync(
                            deployment.PrimaryKey,
                            "The closed partial provider graph contained no potentially billable resource.",
                            settlementCancellationToken)
                        .ConfigureAwait(false);
                }
                await TriggerFaultCostStopAsync(
                        job,
                        server,
                        deployment,
                        intent.Worker,
                        "provider-partial-provisioning-closed",
                        provision.Error ?? "Provider provisioning ended with a closed partial resource graph.",
                        settlementCancellationToken)
                    .ConfigureAwait(false);
            }
            if (!provision.Succeeded || provision.Resource is null)
            {
                intent.Resource.State = provision.Outcome is ERuntimeProviderOutcome.Unknown
                    ? EDeploymentResourceState.Unknown
                    : EDeploymentResourceState.Provisioning;
                intent.Resource.PossibleCost = provision.Outcome is ERuntimeProviderOutcome.Unknown;
                deployment.PossibleCost |= intent.Resource.PossibleCost;
                deployment.Stage = provision.Stage;
                deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.DeploymentResourceProvisioningFailed,
                    "Provider resource provisioning did not complete.",
                    serverId: server.PrimaryKey,
                    deploymentId: deployment.PrimaryKey,
                    lifecycleJobId: job.PrimaryKey,
                    workerId: intent.Worker.PrimaryKey,
                    costOwnerId: deployment.CostOwnerFk,
                    details: $"Provider={provider.ProviderIdentifier}; Kind={intent.Resource.Kind}; "
                             + $"Stage={provision.Stage}; Failure={provision.Error}",
                    costImpact: intent.Resource.PossibleCost
                        ? ECostImpact.KeepsCostRunning
                        : ECostImpact.Unknown);
                await dbContext.SaveChangesAsync(settlementCancellationToken).ConfigureAwait(false);
                if (provision.Outcome is ERuntimeProviderOutcome.PermanentFailure
                    && provision.ProvisionedResources.Count > 0)
                {
                    if (!hasPotentialProviderCost)
                    {
                        await costSliceService.ReleaseInitialReservationAsync(
                                deployment.PrimaryKey,
                                "Provider partial allocation contained no potentially billable resource.",
                                settlementCancellationToken)
                            .ConfigureAwait(false);
                    }
                    await TriggerFaultCostStopAsync(
                            job,
                            server,
                            deployment,
                            intent.Worker,
                            "provider-partial-provisioning-failed",
                            provision.Error ?? "Provider provisioning failed after allocating part of the resource graph.",
                            settlementCancellationToken)
                        .ConfigureAwait(false);
                }
                if (provision.Outcome is ERuntimeProviderOutcome.PermanentFailure)
                {
                    if (provision.ProvisionedResources.Count == 0)
                    {
                        await costSliceService.ReleaseInitialReservationAsync(
                                deployment.PrimaryKey,
                                "The provider definitively rejected provisioning without allocating a resource.",
                                settlementCancellationToken)
                            .ConfigureAwait(false);
                    }
                    intent.Resource.TimeStampProvisioningResolved = NodaSystemClock.Instance.GetCurrentInstant();
                    intent.Resource.TimeStampUpdated = intent.Resource.TimeStampProvisioningResolved.Value;
                    await dbContext.SaveChangesAsync(settlementCancellationToken).ConfigureAwait(false);
                    await RejectStartAsync(
                            job,
                            server,
                            deployment,
                            "provider-provisioning-invalid",
                            provision.Error ?? "The provider rejected the provisioning request.",
                            settlementCancellationToken)
                        .ConfigureAwait(false);
                }
                throw new LifecycleExecutionException(
                    provision.Error ?? "Provider provisioning is waiting for safe reconciliation.",
                    provision.Stage);
            }
            observedResource = provision.Resource;
        }

        // Late provider discovery may be the first proof that an earlier ambiguous create became billable.
        await costSliceService.CommitInitialReservationAsync(
                deployment.PrimaryKey,
                provider.ProviderIdentifier,
                cancellationToken)
            .ConfigureAwait(false);

        if (observedResource.HasConfirmedOperationalFault)
        {
            await TriggerFaultCostStopAsync(
                    job,
                    server,
                    deployment,
                    intent.Worker,
                    "provider-resource-operational-fault",
                    "The provider confirmed that the newly allocated primary runtime resource is not operational.",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (observedResource.OperationalState is ERuntimeResourceOperationalState.Uncertain)
        {
            intent.Resource.State = EDeploymentResourceState.Unknown;
            intent.Resource.PossibleCost = true;
            intent.Resource.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            deployment.Stage = "provider-operational-state-pending";
            deployment.PossibleCost = true;
            deployment.TimeStampUpdated = intent.Resource.TimeStampUpdated;
            job.MaxAttempts = int.MaxValue;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleExecutionException(
                "The provider has not yet reported a conclusive operational state for the primary runtime resource.");
        }

        await PublishAllocatedResourceAsync(
                job,
                server,
                deployment,
                intent.Worker,
                intent.Resource,
                observedResource,
                cancellationToken)
            .ConfigureAwait(false);
        var command = await EnsureCommandPersistedAsync(job, server, intent.Worker, cancellationToken)
            .ConfigureAwait(false);
        await using (var transaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
                         .ConfigureAwait(false))
        {
            await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
            if (job.Status is not ELifecycleJobStatus.Running
                || deployment.IsProvisioningBlocked
                || deployment.TimeStampResolved.HasValue)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                throw new LifecycleSupersededException(
                    "Waiting for the worker was superseded by durable stop or termination state.");
            }
            var waitingAt = NodaSystemClock.Instance.GetCurrentInstant();
            job.Stage = "waiting-for-worker";
            job.Progress = Math.Max(job.Progress, 35);
            job.TimeStampUpdated = waitingAt;
            deployment.Status = EDeploymentStatus.WaitingForWorker;
            deployment.Stage = "waiting-for-worker";
            deployment.TimeStampUpdated = waitingAt;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var registrationDeadline = intent.Resource.TimeStampObserved!.Value
                                   + Duration.FromTimeSpan(_options.RegistrationGrace);
        if (intent.Worker.BootstrapCredentialExpiresAt is { } bootstrapExpiry
            && bootstrapExpiry < registrationDeadline)
        {
            registrationDeadline = bootstrapExpiry;
        }
        try
        {
            await WaitForCommandAsync(
                    job,
                    server,
                    deployment,
                    intent.Worker,
                    command,
                    registrationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (WorkerCommandFailedException ex)
        {
            await TriggerFaultCostStopAsync(
                    job,
                    server,
                    deployment,
                    intent.Worker,
                    ex.ErrorCode,
                    ex.Message,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await using (var transaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
                         .ConfigureAwait(false))
        {
            await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
            if (job.Status is not ELifecycleJobStatus.Running
                || deployment.IsProvisioningBlocked
                || deployment.TimeStampResolved.HasValue)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                throw new LifecycleSupersededException(
                    "The worker start completed after durable stop or termination had already taken priority.");
            }
            var now = NodaSystemClock.Instance.GetCurrentInstant();
            deployment.Status = EDeploymentStatus.Running;
            deployment.Stage = "running";
            deployment.PossibleCost = true;
            deployment.TimeStampStarted ??= now;
            deployment.TimeStampUpdated = now;
            job.Stage = "running";
            job.Progress = 100;
            job.TimeStampUpdated = now;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteStopAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        CancellationToken cancellationToken)
    {
        var worker = deployment.Workers.SingleOrDefault(q => q.TimeStampStopped == null);
        if (worker is null)
            throw new LifecycleExecutionException("The deployment has no dedicated worker to preserve and stop.");

        await using (var transaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
                         .ConfigureAwait(false))
        {
            await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
            if (job.Status is not ELifecycleJobStatus.Running
                || !deployment.IsProvisioningBlocked
                || deployment.TimeStampResolved.HasValue
                || deployment.Status is EDeploymentStatus.Terminating
                    or EDeploymentStatus.TerminatingDueToFault
                    or EDeploymentStatus.Terminated
                    or EDeploymentStatus.TerminatedDueToFault)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                throw new LifecycleSupersededException(
                    "Ordinary Stop was superseded by durable termination state.");
            }
            deployment.Status = EDeploymentStatus.Stopping;
            deployment.Stage = "worker-stop-queued";
            deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        var command = await EnsureCommandPersistedAsync(job, server, worker, cancellationToken).ConfigureAwait(false);
        try
        {
            await WaitForCommandAsync(
                    job,
                    server,
                    deployment,
                    worker,
                    command,
                    registrationDeadline: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (WorkerCommandFailedException ex)
        {
            await using var transaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
                .ConfigureAwait(false);
            await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
            if (job.Status is not ELifecycleJobStatus.Running
                || deployment.Status is EDeploymentStatus.Terminating
                    or EDeploymentStatus.TerminatingDueToFault
                    or EDeploymentStatus.Terminated
                    or EDeploymentStatus.TerminatedDueToFault)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                throw new LifecycleSupersededException(
                    "A preservation failure arrived after durable termination took priority.");
            }
            deployment.Status = EDeploymentStatus.PreservationFailed;
            deployment.Stage = "worker-stop-or-preservation-failed";
            deployment.FaultCode = ex.ErrorCode;
            deployment.FaultMessage = ex.Message;
            deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleExecutionException(ex.Message);
        }

        await ReleaseDeploymentAsync(job, server, deployment, terminate: false, faultTriggered: false, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ExecuteTerminateAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        CancellationToken cancellationToken)
    {
        bool faultTriggered;
        await using (var transitionTransaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
                         .ConfigureAwait(false))
        {
            await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
            await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
            // Another authorized teardown path may have finished while this durable Terminate job was queued. Treat
            // that as idempotent success; never regress a resolved deployment back to an in-progress status.
            if (deployment.TimeStampResolved.HasValue || job.Status is not ELifecycleJobStatus.Running)
            {
                await transitionTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            faultTriggered = deployment.Status is EDeploymentStatus.TerminatingDueToFault;
            deployment.IsProvisioningBlocked = true;
            if (!faultTriggered)
                deployment.Status = EDeploymentStatus.Terminating;
            deployment.Stage = faultTriggered ? "fault-evidence-window" : "provider-teardown";
            deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            await CancelNormalCommandsAsync(deployment.PrimaryKey, job.PrimaryKey, cancellationToken)
                .ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transitionTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        if (faultTriggered)
        {
            var worker = deployment.Workers.SingleOrDefault(q => q.TimeStampStopped == null);
            if (worker is not null)
            {
                await CollectBoundedFaultEvidenceAsync(job, server, deployment, worker, cancellationToken)
                    .ConfigureAwait(false);
            }
            deployment.Stage = "provider-teardown";
            deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        await ReleaseDeploymentAsync(
                job,
                server,
                deployment,
                terminate: true,
                faultTriggered,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ExecuteRuntimeActionAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        CancellationToken cancellationToken)
    {
        if (deployment.Status is not EDeploymentStatus.Running)
            throw new LifecycleExecutionException("The deployment is not available for runtime work.");
        var worker = deployment.Workers.SingleOrDefault(q => q.TimeStampStopped == null)
                     ?? throw new LifecycleExecutionException("The deployment has no dedicated worker.");
        var command = await EnsureCommandPersistedAsync(job, server, worker, cancellationToken).ConfigureAwait(false);
        await WaitForCommandAsync(
                job,
                server,
                deployment,
                worker,
                command,
                registrationDeadline: null,
                cancellationToken)
            .ConfigureAwait(false);
        await SetJobProgressAsync(job, "completed-by-worker", 100, cancellationToken).ConfigureAwait(false);
    }

    private async Task ClaimProvisioningMutationAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        LifecycleWorker worker,
        DeploymentResource resource,
        IRuntimeProvider provider,
        string? expectedLeaseOwner,
        int expectedAttempt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Entry(resource).ReloadAsync(cancellationToken).ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (job.Status is not ELifecycleJobStatus.Running
            || deployment.IsProvisioningBlocked
            || deployment.TimeStampResolved.HasValue)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleSupersededException(
                "Provider mutation claiming was superseded by durable stop or termination state.");
        }
        if (expectedLeaseOwner is not null
            && (!string.Equals(job.LeaseOwner, expectedLeaseOwner, StringComparison.Ordinal)
                || job.Attempt != expectedAttempt
                || job.LeaseExpires is null
                || job.LeaseExpires <= now))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleSupersededException(
                "The lifecycle processor lease changed before the provider mutation could be claimed.");
        }
        if (resource.TimeStampProvisioningRequested.HasValue)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleSupersededException(
                "This durable provisioning intent was already claimed; provider create will not be reposted.");
        }

        worker.Status = EWorkerStatus.Provisioning;
        worker.TimeStampUpdated = now;
        deployment.Status = EDeploymentStatus.Provisioning;
        deployment.Stage = "provider-create-ready";
        deployment.TimeStampUpdated = now;
        resource.TimeStampProvisioningRequested = now;
        resource.TimeStampProvisioningResolved = null;
        resource.TimeStampUpdated = now;
        job.MaxAttempts = int.MaxValue;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.DeploymentResourceProvisioningRequested,
            "Provider resource provisioning requested.",
            serverId: server.PrimaryKey,
            deploymentId: deployment.PrimaryKey,
            lifecycleJobId: job.PrimaryKey,
            workerId: worker.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            details: $"Provider={provider.ProviderIdentifier}; Kind={resource.Kind}; "
                     + $"Intent={resource.ProvisioningIntentIdentifier}",
            costImpact: ECostImpact.StartsCost);

        // The one-way bootstrap hash and provider-queryable intent are committed before the create call.
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProvisioningIntent> EnsureProvisioningIntentAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        IRuntimeProvider provider,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
        // The aggregate was loaded before the cross-process server lock was acquired. Refresh both collections so a
        // waiter sees reservations and provider-request markers committed by the lock holder. A normal tracking query
        // only adds missing entities and does not overwrite stale values on rows that were already tracked.
        await ReloadDeploymentProviderStateAsync(deployment.PrimaryKey, cancellationToken).ConfigureAwait(false);
        if (job.Status is not ELifecycleJobStatus.Running
            || deployment.IsProvisioningBlocked
            || deployment.TimeStampResolved.HasValue)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleSupersededException(
                "Provisioning intent persistence was superseded by durable stop or termination state.");
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = deployment.Workers.SingleOrDefault(q => q.TimeStampStopped == null);
        if (worker is null)
        {
            worker = new LifecycleWorker
            {
                PrimaryKey       = Guid.NewGuid(),
                DeploymentFk     = deployment.PrimaryKey,
                Identity         = $"worker-{deployment.PrimaryKey:N}",
                Status           = EWorkerStatus.Provisioning,
                TimeStampCreated = now,
                TimeStampUpdated = now,
            };
            deployment.Workers.Add(worker);
            dbContext.LifecycleWorkers.Add(worker);
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.WorkerProvisioned,
                "Dedicated external worker identity reserved for provider provisioning.",
                serverId: server.PrimaryKey,
                deploymentId: deployment.PrimaryKey,
                lifecycleJobId: job.PrimaryKey,
                workerId: worker.PrimaryKey,
                costOwnerId: deployment.CostOwnerFk,
                costImpact: ECostImpact.NoCostImpact);
        }

        var providerResources = deployment.Resources.Where(q =>
                string.Equals(q.ProviderIdentifier, provider.ProviderIdentifier, StringComparison.OrdinalIgnoreCase)
                && q.TimeStampReleased == null)
            .ToArray();
        var resource = providerResources.SingleOrDefault(q => q.ProviderResourceIdentifier is null)
                       ?? providerResources.SingleOrDefault(q =>
                           string.Equals(q.Kind, "server", StringComparison.OrdinalIgnoreCase));
        if (resource is null && providerResources.Length > 0)
        {
            throw new PermanentLifecycleExecutionException(
                "The deployment has provider resources but no unambiguous primary runtime resource.");
        }
        if (resource is null)
        {
            var intentId = Guid.NewGuid();
            resource = new DeploymentResource
            {
                PrimaryKey                   = Guid.NewGuid(),
                DeploymentFk                 = deployment.PrimaryKey,
                ProviderIdentifier           = provider.ProviderIdentifier,
                ProvisioningIntentIdentifier = intentId.ToString("N"),
                Kind                         = "server",
                State                        = EDeploymentResourceState.Provisioning,
                TimeStampCreated             = now,
                TimeStampUpdated             = now,
            };
            deployment.Resources.Add(resource);
            dbContext.DeploymentResources.Add(resource);
        }
        if (!Guid.TryParseExact(resource.ProvisioningIntentIdentifier, "N", out var parsedIntent)
            && !Guid.TryParse(resource.ProvisioningIntentIdentifier, out parsedIntent))
        {
            throw new PermanentLifecycleExecutionException(
                "The persisted provider provisioning intent is invalid and cannot be reconciled safely.");
        }

        deployment.Stage = "provider-intent-persisted";
        deployment.TimeStampUpdated = now;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ProvisioningIntent(worker, resource, parsedIntent);
    }

    private async Task<RuntimeResource?> FindExpectedResourceAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        LifecycleWorker worker,
        DeploymentResource resource,
        IRuntimeProvider provider,
        CancellationToken cancellationToken)
    {
        var intentId = ParseIntent(resource);
        var requiredResourceTypes = provider.Capabilities.RequiredProvisionedResourceTypes;
        var requiresResourceGraphDiscovery = requiredResourceTypes is { Count: > 1 };
        var hadPrimaryResourceIdentifier = resource.ProviderResourceIdentifier is { Length: > 0 };
        if (!requiresResourceGraphDiscovery
            && resource.ProviderResourceIdentifier is { Length: > 0 } resourceIdentifier)
        {
            var observation = await provider.ObserveAsync(
                    new RuntimeObservationRequest(
                        server.PrimaryKey,
                        deployment.PrimaryKey,
                        resourceIdentifier,
                        intentId,
                        worker.PrimaryKey),
                    cancellationToken)
                .ConfigureAwait(false);
            if (observation.Outcome is ERuntimeProviderOutcome.NotFound)
                throw new ConfirmedRuntimeMissingException("The provider confirmed that the required worker resource is absent.");
            if (!observation.Succeeded || observation.Resource is null)
            {
                resource.State = EDeploymentResourceState.Unknown;
                deployment.Stage = observation.Stage;
                deployment.PossibleCost |= resource.PossibleCost;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                throw new LifecycleExecutionException(
                    observation.Error ?? "Provider observation is degraded; allocation remains unresolved.");
            }
            if (observation.Resource.HasConfirmedOperationalFault)
            {
                throw new ProviderResourceGraphFaultException(
                    "provider-resource-operational-fault",
                    "Provider observation confirmed that the primary runtime resource is no longer operational.");
            }
            if (observation.Resource.OperationalState is ERuntimeResourceOperationalState.Uncertain)
            {
                resource.State = EDeploymentResourceState.Unknown;
                resource.PossibleCost = true;
                deployment.Stage = "provider-operational-state-pending";
                deployment.PossibleCost = true;
                deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                throw new LifecycleExecutionException(
                    "The provider has not yet reported a conclusive operational state for the primary runtime resource.");
            }
            return observation.Resource;
        }

        var discovery = await provider.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        if (!discovery.Succeeded)
        {
            resource.State = EDeploymentResourceState.Unknown;
            deployment.Stage = discovery.Stage;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (discovery.Outcome is ERuntimeProviderOutcome.PermanentFailure)
            {
                throw new PermanentLifecycleExecutionException(
                    discovery.Error ?? "Provider discovery is not configured.");
            }
            throw new LifecycleExecutionException(
                discovery.Error ?? "Provider discovery is temporarily unavailable.");
        }

        var matches = discovery.Resources
            .Where(q => q.Attribution?.ServerId == server.PrimaryKey
                        && q.Attribution.DeploymentId == deployment.PrimaryKey
                        && q.Attribution.ProvisioningIntentId == intentId)
            .ToArray();
        if (matches.Length == 0)
        {
            if (hadPrimaryResourceIdentifier)
            {
                throw new ConfirmedRuntimeMissingException(
                    "Provider-wide discovery confirmed that the attributed runtime resource graph is absent.");
            }
            if (resource.TimeStampProvisioningRequested.HasValue
                && resource.TimeStampProvisioningResolved is null)
            {
                resource.State = EDeploymentResourceState.Unknown;
                resource.PossibleCost = true;
                resource.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                deployment.PossibleCost = true;
                deployment.Stage = "provider-create-not-observed";
                deployment.TimeStampUpdated = resource.TimeStampUpdated;
                job.MaxAttempts = int.MaxValue;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                throw new ProviderResourceGraphFaultException(
                    "provider-create-not-observed",
                    "Provider-wide discovery has not observed the issued create intent. The intent remains unresolved "
                    + "and must stay under reconciliation without reposting create.");
            }
            resource.TimeStampProvisioningResolved ??= NodaSystemClock.Instance.GetCurrentInstant();
            resource.TimeStampUpdated = resource.TimeStampProvisioningResolved.Value;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        var duplicateKinds = matches
            .GroupBy(q => q.ResourceType, StringComparer.OrdinalIgnoreCase)
            .Where(q => q.Count() > 1)
            .Select(q => q.Key)
            .ToArray();
        if (duplicateKinds.Length > 0)
        {
            await AdoptDiscoveredResourcesAsync(deployment, provider, matches, resource, cancellationToken)
                .ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new ProviderResourceGraphFaultException(
                "provider-resource-graph-duplicate",
                "Provider discovery found duplicate resource kinds for one intent; the complete discovered graph "
                + "must enter verified fault teardown.");
        }
        if (matches.Any(q => q.Attribution?.WorkerId != worker.PrimaryKey))
        {
            await AdoptDiscoveredResourcesAsync(deployment, provider, matches, resource, cancellationToken)
                .ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new ProviderResourceGraphFaultException(
                "provider-resource-graph-attribution-conflict",
                "Provider discovery found a deployment resource attributed to a different worker identity; the "
                + "discovered graph must enter verified fault teardown.");
        }
        var primaryMatches = matches.Where(q =>
                string.Equals(q.ResourceType, resource.Kind, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        await AdoptDiscoveredResourcesAsync(deployment, provider, matches, resource, cancellationToken)
            .ConfigureAwait(false);
        var graphShapeComplete = primaryMatches.Length == 1
                                 && (requiredResourceTypes is not { Count: > 0 }
                                     || requiredResourceTypes.All(requiredType => matches.Count(q => string.Equals(
                                         q.ResourceType,
                                         requiredType,
                                         StringComparison.OrdinalIgnoreCase)) == 1));
        if (!graphShapeComplete)
        {
            if (hadPrimaryResourceIdentifier && primaryMatches.Length == 0)
            {
                throw new ConfirmedRuntimeMissingException(
                    "Provider-wide discovery confirmed that the primary runtime resource is absent.");
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new ProviderResourceGraphFaultException(
                "provider-resource-graph-incomplete",
                "Stable provider discovery found only a partial resource graph; the complete discovered graph must "
                + "enter verified fault teardown without reposting create.");
        }
        if (matches.Any(q => q.HasProvisioningConflict))
        {
            throw new ProviderResourceGraphFaultException(
                "provider-resource-graph-safety-conflict",
                "Stable provider discovery found a conflicting or unsafe resource relationship; the complete "
                + "discovered graph must enter verified fault teardown without reposting create.");
        }
        if (matches.Any(q => !q.IsProvisioningReady))
        {
            resource.State = EDeploymentResourceState.Unknown;
            resource.PossibleCost = true;
            resource.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            deployment.Stage = "provider-resource-graph-readiness-pending";
            deployment.PossibleCost = true;
            deployment.TimeStampUpdated = resource.TimeStampUpdated;
            job.MaxAttempts = int.MaxValue;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleExecutionException(
                "The attributed provider resource graph is complete but its relationships are still converging.");
        }
        if (primaryMatches.Length != 1)
        {
            throw new LifecycleExecutionException(
                "Provider discovery did not return one unambiguous primary runtime resource for the intent.");
        }
        var primary = primaryMatches[0];
        if (primary.HasConfirmedOperationalFault)
        {
            throw new ProviderResourceGraphFaultException(
                "provider-resource-operational-fault",
                "Provider discovery confirmed that the primary runtime resource is no longer operational.");
        }
        if (primary.OperationalState is ERuntimeResourceOperationalState.Uncertain)
        {
            resource.State = EDeploymentResourceState.Unknown;
            resource.PossibleCost = true;
            resource.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            deployment.Stage = "provider-operational-state-pending";
            deployment.PossibleCost = true;
            deployment.TimeStampUpdated = resource.TimeStampUpdated;
            job.MaxAttempts = int.MaxValue;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleExecutionException(
                "The provider has not yet reported a conclusive operational state for the primary runtime resource.");
        }
        return primary;
    }

    private async Task PublishAllocatedResourceAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        LifecycleWorker worker,
        DeploymentResource resource,
        RuntimeResource observed,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
        if (job.Status is not ELifecycleJobStatus.Running
            || deployment.IsProvisioningBlocked
            || deployment.TimeStampResolved.HasValue)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleSupersededException(
                "Publishing provider allocation was superseded by durable stop or termination state.");
        }
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        resource.ProviderResourceIdentifier = observed.ResourceIdentifier;
        resource.Kind = observed.ResourceType;
        RuntimeResourceFacts.Apply(resource, observed);
        resource.State = EDeploymentResourceState.Allocated;
        resource.PossibleCost = observed.IsPotentiallyCostGenerating;
        resource.TimeStampObserved = now;
        resource.TimeStampProvisioningResolved = now;
        resource.TimeStampUpdated = now;
        deployment.PossibleCost |= observed.IsPotentiallyCostGenerating;
        deployment.Status = EDeploymentStatus.WaitingForWorker;
        deployment.Stage = "provider-resource-allocated";
        deployment.TimeStampUpdated = now;
        worker.Status = EWorkerStatus.Provisioning;
        worker.TimeStampUpdated = now;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.DeploymentResourceObserved,
            "Provider resource allocated and attributed to the deployment.",
            serverId: server.PrimaryKey,
            deploymentId: deployment.PrimaryKey,
            lifecycleJobId: job.PrimaryKey,
            workerId: worker.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            details: $"Provider={resource.ProviderIdentifier}; Kind={resource.Kind}; Identity={resource.ProviderResourceIdentifier}",
            costImpact: observed.IsPotentiallyCostGenerating ? ECostImpact.StartsCost : ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkerCommand> EnsureCommandPersistedAsync(
        LifecycleJob job,
        GameServer server,
        LifecycleWorker worker,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
        var deployment = await dbContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == job.DeploymentFk, cancellationToken)
            .ConfigureAwait(false);
        await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
        var stopCommandIsStillAuthorized = job.Kind is ELifecycleJobKind.Stop
                                           && deployment.Status is (EDeploymentStatus.Stopping
                                               or EDeploymentStatus.PreservationFailed);
        if (job.Status is not ELifecycleJobStatus.Running
            || deployment.TimeStampResolved.HasValue
            || deployment.Status is EDeploymentStatus.Terminating
                or EDeploymentStatus.TerminatingDueToFault
                or EDeploymentStatus.Terminated
                or EDeploymentStatus.TerminatedDueToFault
            || deployment.IsProvisioningBlocked && !stopCommandIsStillAuthorized)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleSupersededException(
                "Worker command creation was superseded by durable stop or termination state.");
        }

        var existing = await dbContext.WorkerCommands
            .SingleOrDefaultAsync(q => q.LifecycleJobFk == job.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }

        var sequence = (await dbContext.WorkerCommands
            .Where(q => q.DeploymentFk == job.DeploymentFk)
            .MaxAsync(q => (long?) q.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0) + 1;
        var content = await contentSnapshotResolver
            .ResolveAsync(server.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        var payload = new WorkerCommandPayload
        {
            ServerId                  = server.PrimaryKey,
            DeploymentId              = job.DeploymentFk,
            GameIdentifier            = server.GameIdentifier,
            ActionIdentifier          = job.ActionIdentifier,
            GracefulStopAttempts      = server.GracefulStopAttempts,
            GracefulStopTimeoutSeconds = server.GracefulStopTimeoutSeconds,
            Port                      = null,
            Content                   = content,
        };
        var command = new WorkerCommand
        {
            PrimaryKey       = Guid.NewGuid(),
            LifecycleJobFk   = job.PrimaryKey,
            DeploymentFk     = job.DeploymentFk,
            WorkerFk         = worker.PrimaryKey,
            Sequence         = sequence,
            Priority         = job.Priority,
            ActionIdentifier = job.ActionIdentifier,
            Payload          = JsonSerializer.Serialize(payload, CommandJsonOptions),
            Status           = EWorkerCommandStatus.Queued,
            Stage            = "queued-for-external-worker",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        job.WorkerCommands.Add(command);
        dbContext.WorkerCommands.Add(command);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.WorkerCommandQueued,
            "Worker command persisted before outbound delivery.",
            serverId: server.PrimaryKey,
            deploymentId: job.DeploymentFk,
            lifecycleJobId: job.PrimaryKey,
            workerId: worker.PrimaryKey,
            workerCommandId: command.PrimaryKey);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return command;
    }

    private async Task WaitForCommandAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        LifecycleWorker worker,
        WorkerCommand command,
        Instant? registrationDeadline,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await dbContext.WorkerCommands
                .AsNoTracking()
                .Where(q => q.PrimaryKey == command.PrimaryKey)
                .Select(q => new CommandState(q.Status, q.Stage, q.ErrorCode, q.ErrorMessage))
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
            if (state.Status is EWorkerCommandStatus.Succeeded)
                return;
            if (state.Status is EWorkerCommandStatus.Failed)
            {
                throw new WorkerCommandFailedException(
                    state.ErrorCode ?? "worker-command-failed",
                    state.ErrorMessage ?? "The external worker command failed.");
            }
            if (state.Status is EWorkerCommandStatus.CancelledByCostStop
                or EWorkerCommandStatus.Retried
                or EWorkerCommandStatus.Skipped)
            {
                throw new LifecycleSupersededException("The worker command was superseded by durable lifecycle state.");
            }

            var workerState = await dbContext.LifecycleWorkers
                .AsNoTracking()
                .Where(q => q.PrimaryKey == worker.PrimaryKey)
                .Select(q => new WorkerState(q.Status, q.TimeStampRegistered, q.LastHeartbeatAt))
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
            var now = NodaSystemClock.Instance.GetCurrentInstant();
            if (registrationDeadline is { } deadline
                && workerState.TimeStampRegistered is null
                && now >= deadline)
            {
                await TriggerFaultCostStopAsync(
                        job,
                        server,
                        deployment,
                        worker,
                        "worker-registration-timeout",
                        "The dedicated worker did not register within its startup window.",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            if (workerState.TimeStampRegistered is not null
                && workerState.LastHeartbeatAt is { } lastHeartbeat
                && now - lastHeartbeat >= Duration.FromTimeSpan(_options.StaleThreshold))
            {
                await TriggerFaultCostStopAsync(
                        job,
                        server,
                        deployment,
                        worker,
                        "worker-control-lost",
                        "The dedicated worker exceeded the configured heartbeat-loss threshold.",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            job.Stage = workerState.TimeStampRegistered is null ? "waiting-for-worker-registration" : state.Stage;
            job.Progress = workerState.TimeStampRegistered is null ? 40 : Math.Max(job.Progress, 50);
            job.TimeStampUpdated = now;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(_options.IdlePollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TriggerFaultCostStopAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        LifecycleWorker worker,
        string faultCode,
        string faultMessage,
        CancellationToken cancellationToken)
    {
        var isPostgreSql = dbContext.Database.ProviderName?.Contains(
            "Npgsql",
            StringComparison.OrdinalIgnoreCase) == true;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
                isPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
                cancellationToken)
            .ConfigureAwait(false);
        if (isPostgreSql)
        {
            await dbContext.GameServers
                .FromSqlInterpolated(
                    $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {server.PrimaryKey} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""UPDATE "GameServers" SET "TimeStampUpdated" = "TimeStampUpdated" WHERE "PrimaryKey" = {server.PrimaryKey}""",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
        var existingTermination = await dbContext.LifecycleJobs.AnyAsync(
                q => q.DeploymentFk == deployment.PrimaryKey
                     && q.Kind == ELifecycleJobKind.Terminate
                     && (q.Status == ELifecycleJobStatus.Queued
                         || q.Status == ELifecycleJobStatus.Running
                         || q.Status == ELifecycleJobStatus.RetryPending
                         || q.Status == ELifecycleJobStatus.WaitingForWorker),
                cancellationToken)
            .ConfigureAwait(false);
        if (existingTermination
            || deployment.TimeStampResolved.HasValue
            || deployment.Status is EDeploymentStatus.Terminating or EDeploymentStatus.Terminated)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleSupersededException(
                "A durable Terminate action already owns provider teardown for this deployment.");
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        deployment.IsProvisioningBlocked = true;
        deployment.Status = EDeploymentStatus.TerminatingDueToFault;
        deployment.Stage = "fault-cost-stop";
        deployment.FaultCode = faultCode;
        deployment.FaultMessage = faultMessage;
        deployment.TimeStampFaulted ??= now;
        deployment.TimeStampUpdated = now;
        worker.Status = EWorkerStatus.Faulted;
        worker.TimeStampUpdated = now;
        await CancelNormalCommandsAsync(deployment.PrimaryKey, null, cancellationToken).ConfigureAwait(false);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.WorkerDegraded,
            "Confirmed worker fault triggered provider-independent cost-stop.",
            serverId: server.PrimaryKey,
            deploymentId: deployment.PrimaryKey,
            lifecycleJobId: job.PrimaryKey,
            workerId: worker.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            details: faultMessage,
            costImpact: deployment.PossibleCost ? ECostImpact.StopsCost : ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await CollectBoundedFaultEvidenceAsync(job, server, deployment, worker, cancellationToken)
            .ConfigureAwait(false);
        deployment.Stage = "provider-teardown";
        deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ReleaseDeploymentAsync(job, server, deployment, terminate: true, faultTriggered: true, cancellationToken)
            .ConfigureAwait(false);
        throw new PermanentLifecycleExecutionException(faultMessage);
    }

    private async Task CollectBoundedFaultEvidenceAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        LifecycleWorker worker,
        CancellationToken cancellationToken)
    {
        var (command, deadline) = await EnsureFaultEvidenceCommandPersistedAsync(
                job,
                server,
                deployment,
                worker,
                cancellationToken)
            .ConfigureAwait(false);
        if (IsTerminal(command.Status))
            return;

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (!HasUsableWorkerCredential(worker, now))
        {
            await CancelFaultEvidenceAsync(
                    job,
                    server,
                    deployment,
                    worker,
                    command,
                    notBefore: null,
                    "fault-evidence-unavailable",
                    "Fault diagnostics and preservation were unavailable; provider cost-stop continues immediately.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        deployment.Stage = "fault-evidence-window";
        deployment.TimeStampUpdated = now;
        job.Stage = "fault-evidence-window";
        job.TimeStampUpdated = now;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await dbContext.WorkerCommands
                .AsNoTracking()
                .Where(q => q.PrimaryKey == command.PrimaryKey)
                .Select(q => q.Status)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
            if (IsTerminal(state))
                return;

            now = NodaSystemClock.Instance.GetCurrentInstant();
            if (now >= deadline)
            {
                await CancelFaultEvidenceAsync(
                        job,
                        server,
                        deployment,
                        worker,
                        command,
                        notBefore: deadline,
                        "fault-evidence-window-expired",
                        "The bounded fault diagnostics and preservation window expired; provider cost-stop continues.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var remaining = (deadline - now).ToTimeSpan();
            var delay = remaining < _options.IdlePollInterval ? remaining : _options.IdlePollInterval;
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(WorkerCommand command, Instant deadline)> EnsureFaultEvidenceCommandPersistedAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        LifecycleWorker worker,
        CancellationToken cancellationToken)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        deployment.TimeStampFaulted ??= now;
        var deadline = deployment.TimeStampFaulted.Value
                       + Duration.FromTimeSpan(_options.FaultEvidenceCollectionWindow);
        var existing = job.WorkerCommands.FirstOrDefault(q => string.Equals(
            q.ActionIdentifier,
            GameServerActionIds.FaultStop,
            StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            var existingPayload = JsonSerializer.Deserialize<WorkerCommandPayload>(existing.Payload, CommandJsonOptions)
                                  ?? throw new PermanentLifecycleExecutionException(
                                      "The persisted fault evidence command payload is invalid.");
            if (existingPayload.FaultEvidenceDeadline != deadline.ToDateTimeOffset())
            {
                existing.Payload = JsonSerializer.Serialize(
                    existingPayload with { FaultEvidenceDeadline = deadline.ToDateTimeOffset() },
                    CommandJsonOptions);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            return (existing, deadline);
        }

        var sequence = (await dbContext.WorkerCommands
            .Where(q => q.DeploymentFk == job.DeploymentFk)
            .MaxAsync(q => (long?) q.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0) + 1;
        var payload = new WorkerCommandPayload
        {
            ServerId                   = server.PrimaryKey,
            DeploymentId               = job.DeploymentFk,
            GameIdentifier             = server.GameIdentifier,
            ActionIdentifier           = GameServerActionIds.FaultStop,
            GracefulStopAttempts       = server.GracefulStopAttempts,
            GracefulStopTimeoutSeconds = server.GracefulStopTimeoutSeconds,
            FaultEvidenceDeadline      = deadline.ToDateTimeOffset(),
            Port                       = null,
        };
        var command = new WorkerCommand
        {
            PrimaryKey       = Guid.NewGuid(),
            LifecycleJobFk   = job.PrimaryKey,
            DeploymentFk     = job.DeploymentFk,
            WorkerFk         = worker.PrimaryKey,
            Sequence         = sequence,
            Priority         = int.MaxValue,
            ActionIdentifier = GameServerActionIds.FaultStop,
            Payload          = JsonSerializer.Serialize(payload, CommandJsonOptions),
            Status           = EWorkerCommandStatus.Queued,
            Stage            = "fault-evidence-queued",
            TimeStampCreated = now,
        };
        job.WorkerCommands.Add(command);
        dbContext.WorkerCommands.Add(command);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.WorkerCommandQueued,
            "Bounded fault diagnostics and preservation command persisted before provider teardown.",
            serverId: server.PrimaryKey,
            deploymentId: job.DeploymentFk,
            lifecycleJobId: job.PrimaryKey,
            workerId: worker.PrimaryKey,
            workerCommandId: command.PrimaryKey,
            costOwnerId: job.Deployment?.CostOwnerFk,
            costImpact: ECostImpact.StopsCost);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (command, deadline);
    }

    private async Task CancelFaultEvidenceAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        LifecycleWorker worker,
        WorkerCommand command,
        Instant? notBefore,
        string stage,
        string auditSummary,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (IsPostgreSql)
        {
            await dbContext.GameServers
                .FromSqlInterpolated(
                    $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {server.PrimaryKey} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            await dbContext.GameServerDeployments
                .FromSqlInterpolated(
                    $"""SELECT * FROM "GameServerDeployments" WHERE "PrimaryKey" = {deployment.PrimaryKey} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.Entry(command).ReloadAsync(cancellationToken).ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (IsTerminal(command.Status) || notBefore is { } earliest && now < earliest)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        command.Status = EWorkerCommandStatus.CancelledByCostStop;
        command.Stage = stage;
        command.LeaseOwner = null;
        command.LeaseExpires = null;
        command.TimeStampCompleted = now;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.Recovery,
            auditSummary,
            serverId: server.PrimaryKey,
            deploymentId: deployment.PrimaryKey,
            lifecycleJobId: job.PrimaryKey,
            workerId: worker.PrimaryKey,
            workerCommandId: command.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            costImpact: ECostImpact.StopsCost);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool HasUsableWorkerCredential(LifecycleWorker worker, Instant now)
        => worker.TimeStampRegistered is not null
           && worker.TimeStampStopped is null
           && ((worker.WorkerCredentialRevokedAt is null
                && worker.WorkerCredentialHash is not null
                && worker.WorkerCredentialExpiresAt > now)
               || (worker.PendingWorkerCredentialHash is not null
                   && worker.PendingWorkerCredentialExpiresAt > now));

    private static bool IsTerminal(EWorkerCommandStatus status)
        => status is EWorkerCommandStatus.Succeeded
            or EWorkerCommandStatus.Failed
            or EWorkerCommandStatus.Retried
            or EWorkerCommandStatus.Skipped
            or EWorkerCommandStatus.CancelledByCostStop;

    private async Task ReleaseDeploymentAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        bool terminate,
        bool faultTriggered,
        CancellationToken cancellationToken)
    {
        if (!terminate)
        {
            var preservationSucceeded = await dbContext.WorkerCommands.AnyAsync(
                    q => q.LifecycleJobFk == job.PrimaryKey
                         && q.DeploymentFk == deployment.PrimaryKey
                         && q.Status == EWorkerCommandStatus.Succeeded
                         && q.ActionIdentifier == GameServerActions.Stop,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!preservationSucceeded)
            {
                deployment.Stage = "worker-stop-preservation-not-confirmed";
                deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                throw new LifecycleExecutionException(
                    "Provider teardown is blocked until the ordinary Stop command and preservation publish succeed.");
            }
        }

        var provider = ResolveProvider(server.RuntimeProviderIdentifier);
        var discovery = await provider.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        if (!discovery.Succeeded)
        {
            deployment.PossibleCost = true;
            deployment.Stage = discovery.Stage;
            deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleExecutionException(
                discovery.Error ?? "Provider discovery is unavailable; non-billable teardown cannot be verified.");
        }

        var discovered = discovery.Resources
            .Where(q => q.Attribution?.ServerId == server.PrimaryKey
                        && q.Attribution.DeploymentId == deployment.PrimaryKey)
            .ToArray();
        var placeholder = deployment.Resources.FirstOrDefault(q => q.TimeStampReleased == null
                                                                   && q.ProviderResourceIdentifier == null);
        await AdoptDiscoveredResourcesAsync(deployment, provider, discovered, placeholder, cancellationToken)
            .ConfigureAwait(false);

        var worker = deployment.Workers.SingleOrDefault(q => q.TimeStampStopped == null);
        foreach (var resource in deployment.Resources
                     .Where(q => q.TimeStampReleased == null)
                     .OrderBy(q => q.ProviderResourceIdentifier is null ? 4 : ProviderReleaseOrder(q.Kind))
                     .ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (resource.ProviderResourceIdentifier is not { Length: > 0 } identifier)
            {
                if (resource.TimeStampProvisioningRequested.HasValue
                    && resource.TimeStampProvisioningResolved is null)
                {
                    resource.State = EDeploymentResourceState.Unknown;
                    resource.PossibleCost = true;
                    deployment.PossibleCost = true;
                    deployment.Stage = "provider-create-reconciliation-before-teardown";
                    deployment.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                    job.MaxAttempts = int.MaxValue;
                    await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    throw new LifecycleExecutionException(
                        "An issued provider create intent remains unresolved and cannot be declared released by elapsed time.");
                }
                resource.State = EDeploymentResourceState.Released;
                resource.PossibleCost = false;
                resource.TimeStampProvisioningResolved ??= NodaSystemClock.Instance.GetCurrentInstant();
                resource.TimeStampReleased = NodaSystemClock.Instance.GetCurrentInstant();
                resource.TimeStampUpdated = resource.TimeStampReleased.Value;
                continue;
            }
            var intentId = ParseIntent(resource);
            resource.State = EDeploymentResourceState.Releasing;
            resource.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.DeploymentResourceReleaseRequested,
                "Provider resource release requested.",
                serverId: server.PrimaryKey,
                deploymentId: deployment.PrimaryKey,
                lifecycleJobId: job.PrimaryKey,
                workerId: worker?.PrimaryKey,
                costOwnerId: deployment.CostOwnerFk,
                details: $"Provider={resource.ProviderIdentifier}; Kind={resource.Kind}; Identity={identifier}",
                costImpact: resource.PossibleCost ? ECostImpact.KeepsCostRunning : ECostImpact.NoCostImpact);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            var release = await provider.ReleaseAsync(
                    new RuntimeReleaseRequest(
                        server.PrimaryKey,
                        deployment.PrimaryKey,
                        identifier,
                        intentId,
                        worker?.PrimaryKey ?? Guid.Empty),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!release.Succeeded || !release.IsVerifiedNonBillable)
            {
                resource.State = release.Outcome is ERuntimeProviderOutcome.Unknown
                    ? EDeploymentResourceState.Unknown
                    : EDeploymentResourceState.ReleasePending;
                resource.PossibleCost = true;
                resource.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                deployment.PossibleCost = true;
                deployment.Stage = release.Stage;
                deployment.TimeStampUpdated = resource.TimeStampUpdated;
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.DeploymentResourceReleaseFailed,
                    "Provider resource release could not be verified.",
                    serverId: server.PrimaryKey,
                    deploymentId: deployment.PrimaryKey,
                    lifecycleJobId: job.PrimaryKey,
                    workerId: worker?.PrimaryKey,
                    costOwnerId: deployment.CostOwnerFk,
                    details: $"Provider={resource.ProviderIdentifier}; Kind={resource.Kind}; Identity={identifier}; "
                             + $"Stage={release.Stage}; Failure={release.Error}",
                    costImpact: ECostImpact.KeepsCostRunning);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                throw new LifecycleExecutionException(
                    release.Error ?? "Provider resource release is not yet verified non-billable.");
            }
            resource.State = EDeploymentResourceState.Released;
            resource.PossibleCost = false;
            resource.TimeStampReleased = NodaSystemClock.Instance.GetCurrentInstant();
            resource.TimeStampObserved = resource.TimeStampReleased;
            resource.TimeStampUpdated = resource.TimeStampReleased.Value;
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.DeploymentResourceReleased,
                "Provider resource release verified non-billable.",
                serverId: server.PrimaryKey,
                deploymentId: deployment.PrimaryKey,
                lifecycleJobId: job.PrimaryKey,
                workerId: worker?.PrimaryKey,
                costOwnerId: deployment.CostOwnerFk,
                details: $"Provider={resource.ProviderIdentifier}; Kind={resource.Kind}; Identity={identifier}",
                costImpact: ECostImpact.StopsCost);
        }
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Close the lost-response window: no late attributed resource may remain after the first deletion pass.
        var verification = await provider.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        if (!verification.Succeeded)
        {
            deployment.PossibleCost = true;
            deployment.Stage = verification.Stage;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleExecutionException(
                verification.Error ?? "Final provider teardown verification is unavailable.");
        }
        var late = verification.Resources
            .Where(q => q.Attribution?.ServerId == server.PrimaryKey
                        && q.Attribution.DeploymentId == deployment.PrimaryKey)
            .ToArray();
        if (late.Length > 0)
        {
            await AdoptDiscoveredResourcesAsync(deployment, provider, late, null, cancellationToken)
                .ConfigureAwait(false);
            deployment.PossibleCost = true;
            deployment.Stage = "late-provider-resource-adopted";
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleExecutionException(
                "A late provider resource was adopted and remains pending automatic cleanup.");
        }

        // Provider calls cannot participate in the database transaction. Re-enter the server mutation lock before
        // publishing the terminal state so a Terminate committed during deletion always wins over an older Stop.
        await using var finalizationTransaction = await BeginServerMutationAsync(server.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        await dbContext.Entry(job).ReloadAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
        if (deployment.TimeStampResolved.HasValue)
        {
            await finalizationTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var hasActiveTerminate = await dbContext.LifecycleJobs.AnyAsync(
                q => q.DeploymentFk == deployment.PrimaryKey
                     && q.Kind == ELifecycleJobKind.Terminate
                     && (q.Status == ELifecycleJobStatus.Queued
                         || q.Status == ELifecycleJobStatus.Running
                         || q.Status == ELifecycleJobStatus.RetryPending
                         || q.Status == ELifecycleJobStatus.WaitingForWorker),
                cancellationToken)
            .ConfigureAwait(false);
        if (job.Kind is not ELifecycleJobKind.Terminate && hasActiveTerminate)
        {
            // Provider cleanup performed by an older Stop/fault path is useful, but the durable Terminate job owns
            // terminal publication. Letting it finish also prevents its in-flight provider result from overwriting
            // freshly resolved metadata.
            await finalizationTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        var effectiveTerminate = terminate
                                 || hasActiveTerminate
                                 || deployment.Status is EDeploymentStatus.Terminating
                                     or EDeploymentStatus.TerminatingDueToFault;
        var effectiveFaultTriggered = deployment.Status == EDeploymentStatus.TerminatingDueToFault
                                      || faultTriggered && deployment.Status != EDeploymentStatus.Terminating;
        if (!effectiveTerminate)
        {
            var preservationStillSucceeded = await dbContext.WorkerCommands.AnyAsync(
                    q => q.LifecycleJobFk == job.PrimaryKey
                         && q.DeploymentFk == deployment.PrimaryKey
                         && q.Status == EWorkerCommandStatus.Succeeded
                         && q.ActionIdentifier == GameServerActions.Stop,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!preservationStillSucceeded)
            {
                await finalizationTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                throw new LifecycleExecutionException(
                    "Ordinary Stop lost its durable preservation evidence before terminal finalization.");
            }
        }
        var activeWorkerId = await dbContext.LifecycleWorkers
            .AsNoTracking()
            .Where(q => q.DeploymentFk == deployment.PrimaryKey && q.TimeStampStopped == null)
            .Select(q => (Guid?) q.PrimaryKey)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        LifecycleWorker? activeWorker = null;
        if (activeWorkerId.HasValue)
        {
            activeWorker = await dbContext.LifecycleWorkers
                .SingleAsync(q => q.PrimaryKey == activeWorkerId.Value, cancellationToken)
                .ConfigureAwait(false);
            await dbContext.Entry(activeWorker).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (activeWorker is not null)
        {
            workerCredentialService.RevokeWorkerCredential(activeWorker, now);
            activeWorker.Status = EWorkerStatus.Offline;
            activeWorker.ProcessId = null;
            activeWorker.ProcessStartedAt = null;
            activeWorker.TimeStampStopped ??= now;
            activeWorker.TimeStampUpdated = now;
        }
        var hadPossibleCost = deployment.PossibleCost;
        deployment.PossibleCost = false;
        deployment.Status = effectiveTerminate
            ? effectiveFaultTriggered ? EDeploymentStatus.TerminatedDueToFault : EDeploymentStatus.Terminated
            : EDeploymentStatus.Stopped;
        deployment.Stage = effectiveTerminate
            ? effectiveFaultTriggered ? "terminated-due-to-fault" : "terminated"
            : "stopped";
        deployment.TimeStampResolved = now;
        deployment.TimeStampUpdated = now;
        auditWriter.Add(
            dbContext,
            effectiveTerminate ? EHostingAuditEventKind.DeploymentTerminated : EHostingAuditEventKind.DeploymentStopped,
            effectiveTerminate
                ? effectiveFaultTriggered
                    ? "Deployment terminated after a fault; provider resources are verified non-billable."
                    : "Deployment terminated; provider resources are verified non-billable."
                : "Deployment stopped; provider resources are verified non-billable.",
            serverId: server.PrimaryKey,
            deploymentId: deployment.PrimaryKey,
            lifecycleJobId: job.PrimaryKey,
            workerId: activeWorker?.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            costImpact: hadPossibleCost ? ECostImpact.StopsCost : ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await finalizationTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task AdoptDiscoveredResourcesAsync(
        GameServerDeployment deployment,
        IRuntimeProvider provider,
        IReadOnlyCollection<RuntimeResource> discovered,
        DeploymentResource? placeholder,
        CancellationToken cancellationToken,
        bool updateDeploymentCostState = true)
    {
        foreach (var observed in discovered)
        {
            var existing = deployment.Resources.SingleOrDefault(q =>
                string.Equals(q.ProviderIdentifier, provider.ProviderIdentifier, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    q.ProviderResourceIdentifier,
                    observed.ResourceIdentifier,
                    StringComparison.Ordinal));
            var resolvedPlaceholder = false;
            if (existing is null
                && placeholder is not null
                && placeholder.ProviderResourceIdentifier is null
                && string.Equals(placeholder.Kind, observed.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                existing = placeholder;
                placeholder = null;
                resolvedPlaceholder = true;
            }
            var newlyDiscovered = existing is null;
            if (existing is null)
            {
                var observedIntent = observed.Attribution?.ProvisioningIntentId
                                     ?? throw new LifecycleExecutionException(
                                         "A linked provider resource is missing its durable provisioning intent attribution.");
                existing = new DeploymentResource
                {
                    PrimaryKey                   = Guid.NewGuid(),
                    DeploymentFk                 = deployment.PrimaryKey,
                    ProviderIdentifier           = provider.ProviderIdentifier,
                    ProvisioningIntentIdentifier = observedIntent.ToString("N"),
                    Kind                         = observed.ResourceType,
                    TimeStampCreated             = NodaSystemClock.Instance.GetCurrentInstant(),
                };
                deployment.Resources.Add(existing);
                dbContext.DeploymentResources.Add(existing);
            }
            existing.ProviderResourceIdentifier = observed.ResourceIdentifier;
            existing.Kind = observed.ResourceType;
            RuntimeResourceFacts.Apply(existing, observed);
            existing.State = observed.IsAllocated
                ? EDeploymentResourceState.Allocated
                : EDeploymentResourceState.Released;
            existing.PossibleCost = observed.IsPotentiallyCostGenerating;
            existing.TimeStampObserved = NodaSystemClock.Instance.GetCurrentInstant();
            existing.TimeStampProvisioningResolved = existing.TimeStampObserved;
            existing.TimeStampUpdated = existing.TimeStampObserved.Value;
            if (observed.IsAllocated)
                existing.TimeStampReleased = null;
            else
                existing.TimeStampReleased ??= existing.TimeStampObserved;
            // Provider-result settlement can race unrelated deployment summary updates from reconciliation. Its first
            // responsibility is to make every concrete provider identifier durable; the resource-level PossibleCost
            // flag is authoritative and must not be rolled back by a deployment concurrency-token conflict.
            if (updateDeploymentCostState)
                deployment.PossibleCost |= observed.IsPotentiallyCostGenerating;
            if (newlyDiscovered && !resolvedPlaceholder)
            {
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.DeploymentResourceObserved,
                    "Provider resource discovered and attributed to the deployment.",
                    serverId: deployment.GameServerFk,
                    deploymentId: deployment.PrimaryKey,
                    workerId: observed.Attribution?.WorkerId,
                    costOwnerId: deployment.CostOwnerFk,
                    details: $"Provider={provider.ProviderIdentifier}; Kind={observed.ResourceType}; "
                             + $"Identity={observed.ResourceIdentifier}; State={observed.ProviderState ?? "unknown"}",
                    costImpact: observed.IsPotentiallyCostGenerating
                        ? ECostImpact.StartsCost
                        : ECostImpact.NoCostImpact);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CancelNormalCommandsAsync(
        Guid deploymentId,
        Guid? excludedLifecycleJobId,
        CancellationToken cancellationToken)
    {
        var commands = await dbContext.WorkerCommands
            .Where(q => q.DeploymentFk == deploymentId
                        && (!excludedLifecycleJobId.HasValue
                            || q.LifecycleJobFk != excludedLifecycleJobId.Value)
                        && q.ActionIdentifier != GameServerActionIds.FaultStop
                        && (q.Status == EWorkerCommandStatus.Queued
                            || q.Status == EWorkerCommandStatus.Received
                            || q.Status == EWorkerCommandStatus.Running
                            || q.Status == EWorkerCommandStatus.Failed))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        foreach (var command in commands)
        {
            command.Status = EWorkerCommandStatus.CancelledByCostStop;
            command.Stage = "cancelled-by-cost-stop";
            command.LeaseOwner = null;
            command.LeaseExpires = null;
            command.TimeStampCompleted ??= now;
        }
    }

    private async Task RevalidateStartAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        CancellationToken cancellationToken)
    {
        if (deployment.IsProvisioningBlocked)
            throw new LifecycleSupersededException("Provisioning is blocked by durable stop or termination state.");
        if (server.IsLaunchDisabled || server.IsArchived)
        {
            await RejectStartAsync(
                    job,
                    server,
                    deployment,
                    "server-not-launchable",
                    "The server is no longer eligible to launch.",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (server.CurrentCostOwnerFk != deployment.CostOwnerFk
            || server.CurrentCostOwner is null
            || server.CurrentCostOwner.IsFrozen)
        {
            await RejectStartAsync(
                    job,
                    server,
                    deployment,
                    "cost-owner-invalid",
                    "The deployment cost owner is no longer valid.",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        var principal = job.RequestedByFk is { } requestedBy
            ? await authorizationService
                .RebuildPrincipalAsync(requestedBy, cancellationToken)
                .ConfigureAwait(false)
            : null;
        if (principal is null
            || !authorizationService.HasServerPermission(
                principal,
                server.PrimaryKey,
                Claims.ResourceBased.Server.Operate)
            || !await authorizationService
                .CanUseCostOwnerAsync(principal, deployment.CostOwnerFk, cancellationToken)
                .ConfigureAwait(false))
        {
            await RejectStartAsync(
                    job,
                    server,
                    deployment,
                    "actor-no-longer-authorized",
                    "The requesting actor is no longer authorized to launch this server.",
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task RejectStartAsync(
        LifecycleJob job,
        GameServer server,
        GameServerDeployment deployment,
        string failureCode,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var isPostgreSql = dbContext.Database.ProviderName?.Contains(
            "Npgsql",
            StringComparison.OrdinalIgnoreCase) == true;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
                isPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
                cancellationToken)
            .ConfigureAwait(false);
        if (isPostgreSql)
        {
            await dbContext.GameServers
                .FromSqlInterpolated(
                    $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {server.PrimaryKey} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""UPDATE "GameServers" SET "TimeStampUpdated" = "TimeStampUpdated" WHERE "PrimaryKey" = {server.PrimaryKey}""",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
        await ReloadDeploymentProviderStateAsync(deployment.PrimaryKey, cancellationToken).ConfigureAwait(false);
        var providerMutationMayExist = deployment.PossibleCost
                                       || await dbContext.DeploymentResources
                                           .AsNoTracking()
                                           .AnyAsync(
                                               q => q.DeploymentFk == deployment.PrimaryKey
                                                    && q.TimeStampReleased == null
                                                    && (q.ProviderResourceIdentifier != null
                                                        || q.PossibleCost
                                                        || (q.TimeStampProvisioningRequested != null
                                                            && q.TimeStampProvisioningResolved == null)),
                                               cancellationToken)
                                           .ConfigureAwait(false);
        if (providerMutationMayExist)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleSupersededException(
                "Start rejection cannot publish a no-cost terminal state while a provider mutation remains unresolved.");
        }
        if (deployment.IsProvisioningBlocked || deployment.TimeStampResolved.HasValue)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new LifecycleSupersededException(
                "A durable stop or termination transition superseded start rejection.");
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        deployment.Status = EDeploymentStatus.Failed;
        deployment.Stage = "start-revalidation-failed";
        deployment.IsProvisioningBlocked = true;
        deployment.PossibleCost = false;
        deployment.FaultCode = failureCode;
        deployment.FaultMessage = HostingFailureSanitizer.Sanitize(failureMessage, 2048);
        deployment.TimeStampFaulted = now;
        deployment.TimeStampResolved = now;
        deployment.TimeStampUpdated = now;
        foreach (var resource in deployment.Resources.Where(q =>
                     q.ProviderResourceIdentifier == null
                     && q.TimeStampReleased == null
                     && (q.TimeStampProvisioningRequested == null
                         || q.TimeStampProvisioningResolved != null)))
        {
            resource.State = EDeploymentResourceState.Released;
            resource.PossibleCost = false;
            resource.TimeStampReleased = now;
            resource.TimeStampUpdated = now;
        }
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.LifecycleFailed,
            "Deployment start was rejected before provider cost began.",
            serverId: server.PrimaryKey,
            deploymentId: deployment.PrimaryKey,
            lifecycleJobId: job.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            details: deployment.FaultMessage,
            costImpact: ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        throw new PermanentLifecycleExecutionException(deployment.FaultMessage, deployment.FaultCode);
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

    private async Task ReloadDeploymentProviderStateAsync(
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        await dbContext.LifecycleWorkers
            .Where(q => q.DeploymentFk == deploymentId)
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        await dbContext.DeploymentResources
            .Where(q => q.DeploymentFk == deploymentId)
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);

        var workerEntries = dbContext.ChangeTracker
            .Entries<LifecycleWorker>()
            .Where(q => q.Entity.DeploymentFk == deploymentId && q.State is not EntityState.Added)
            .ToArray();
        foreach (var workerEntry in workerEntries)
            await workerEntry.ReloadAsync(cancellationToken).ConfigureAwait(false);

        var resourceEntries = dbContext.ChangeTracker
            .Entries<DeploymentResource>()
            .Where(q => q.Entity.DeploymentFk == deploymentId && q.State is not EntityState.Added)
            .ToArray();
        foreach (var resourceEntry in resourceEntries)
            await resourceEntry.ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ThrowIfProvisioningBlockedAsync(Guid deploymentId, CancellationToken cancellationToken)
    {
        if (await dbContext.GameServerDeployments
                .AsNoTracking()
                .Where(q => q.PrimaryKey == deploymentId)
                .Select(q => q.IsProvisioningBlocked)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false))
        {
            throw new LifecycleSupersededException("Provisioning is blocked by durable stop or termination state.");
        }
    }

    private async Task SetJobProgressAsync(
        LifecycleJob job,
        string stage,
        int progress,
        CancellationToken cancellationToken)
    {
        job.Stage = stage;
        job.Progress = progress;
        job.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private IRuntimeProvider ResolveProvider(string identifier)
        => runtimeProviders.SingleOrDefault(q => string.Equals(
               q.ProviderIdentifier,
               identifier,
               StringComparison.OrdinalIgnoreCase))
           ?? throw new PermanentLifecycleExecutionException(
               $"Runtime provider '{HostingFailureSanitizer.Sanitize(identifier, 128)}' is unavailable.");

    private bool IsPostgreSql
        => dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private static Guid ParseIntent(DeploymentResource resource)
        => Guid.TryParse(resource.ProvisioningIntentIdentifier, out var parsed)
            ? parsed
            : throw new PermanentLifecycleExecutionException(
                "A deployment resource has an invalid provisioning intent identity.");

    private static int ProviderReleaseOrder(string resourceKind)
        => resourceKind.ToLowerInvariant() switch
        {
            "primary-ip" => 0,
            "server" => 1,
            "firewall" => 2,
            _ => 3,
        };

    private sealed record ProvisioningIntent(
        LifecycleWorker Worker,
        DeploymentResource Resource,
        Guid IntentId);

    private sealed record CommandState(
        EWorkerCommandStatus Status,
        string Stage,
        string? ErrorCode,
        string? ErrorMessage);

    private sealed record WorkerState(
        EWorkerStatus Status,
        Instant? TimeStampRegistered,
        Instant? LastHeartbeatAt);

    private class ProviderResourceGraphFaultException(string faultCode, string message)
        : LifecycleExecutionException(message)
    {
        public string FaultCode { get; } = faultCode;
    }

    private sealed class ConfirmedRuntimeMissingException(string message)
        : ProviderResourceGraphFaultException("provider-runtime-missing", message);

    private sealed class WorkerCommandFailedException(string errorCode, string message)
        : LifecycleExecutionException(message)
    {
        public string ErrorCode { get; } = errorCode;
    }
}
