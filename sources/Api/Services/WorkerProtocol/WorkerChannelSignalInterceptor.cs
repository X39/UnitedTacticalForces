using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Observes committed worker command and deployment state and wakes the affected connected workers through the
/// <see cref="IWorkerChannelSignal"/>.
/// </summary>
/// <remarks>
/// <para>
/// EF Core interceptors registered on the context options are singletons shared by every context instance, so all
/// per-save state lives in a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed by the context and guarded by a
/// per-context lock. There is no other mutable state, so two contexts saving at the same time never see each other's
/// identifiers.
/// </para>
/// <para>
/// Covered changes: a <see cref="WorkerCommand"/> that is added, or whose status was modified, wakes its worker;
/// a <see cref="GameServerDeployment"/> whose status or provisioning block was modified wakes every connected worker
/// of that deployment, so a busy worker probes its running command and learns about a cost-stop supersession
/// promptly. Stage, progress, lease and timestamp churn produce no signal. A Wake is a hint: the worker answers it
/// by asking the protocol service, which stays the only place that decides what the worker may do.
/// </para>
/// <para>
/// Timing mirrors the fleet change interceptor: identifiers are collected before the save. After a successful save
/// without an explicit transaction they are published at once; inside an explicit transaction they are deferred and
/// published only when that transaction commits, so a Wake never precedes readable data. A failed or canceled save
/// discards what it collected; a rolled-back or failed transaction discards everything it deferred.
/// </para>
/// <para>
/// The interceptor never throws into the save or transaction path and never blocks it: every override returns the
/// incoming result unchanged, and the signal call only queues.
/// </para>
/// </remarks>
internal sealed class WorkerChannelSignalInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly IWorkerChannelSignal                            _signal;
    private readonly ILogger<WorkerChannelSignalInterceptor>         _logger;
    private readonly ConditionalWeakTable<DbContext, PendingChanges> _pending = new();

    /// <summary>
    /// Creates an interceptor that wakes affected workers through the specified signal.
    /// </summary>
    /// <param name="signal">The signal that queues Wake frames for connected workers.</param>
    /// <param name="logger">The logger receiving unexpected collection or publish failures.</param>
    public WorkerChannelSignalInterceptor(
        IWorkerChannelSignal signal,
        ILogger<WorkerChannelSignalInterceptor> logger)
    {
        _signal = signal;
        _logger = logger;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PublishOrDefer(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        PublishOrDefer(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
        => DiscardCollected(eventData.Context);

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardCollected(eventData.Context);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void SaveChangesCanceled(DbContextEventData eventData)
        => DiscardCollected(eventData.Context);

    /// <inheritdoc />
    public override Task SaveChangesCanceledAsync(
        DbContextEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardCollected(eventData.Context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Discards identifiers left deferred by a transaction that was disposed without commit or rollback, so they
    /// cannot leak into the commit of the next transaction on the same context.
    /// </summary>
    /// <param name="connection">The connection the transaction was started on.</param>
    /// <param name="eventData">The transaction event data carrying the context.</param>
    /// <param name="result">The started transaction.</param>
    /// <returns>The incoming <paramref name="result"/>, unchanged.</returns>
    public DbTransaction TransactionStarted(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result)
    {
        DiscardDeferred(eventData.Context);
        return result;
    }

    /// <summary>
    /// Discards identifiers left deferred by a transaction that was disposed without commit or rollback, so they
    /// cannot leak into the commit of the next transaction on the same context.
    /// </summary>
    /// <param name="connection">The connection the transaction was started on.</param>
    /// <param name="eventData">The transaction event data carrying the context.</param>
    /// <param name="result">The started transaction.</param>
    /// <param name="cancellationToken">A token that cancels the operation; not observed.</param>
    /// <returns>The incoming <paramref name="result"/>, unchanged.</returns>
    public ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        DiscardDeferred(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Publishes the identifiers deferred by saves inside the committed transaction.
    /// </summary>
    /// <param name="transaction">The committed transaction.</param>
    /// <param name="eventData">The transaction event data carrying the context.</param>
    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        => PublishDeferred(eventData.Context);

    /// <summary>
    /// Publishes the identifiers deferred by saves inside the committed transaction.
    /// </summary>
    /// <param name="transaction">The committed transaction.</param>
    /// <param name="eventData">The transaction event data carrying the context.</param>
    /// <param name="cancellationToken">A token that cancels the operation; not observed.</param>
    /// <returns>A completed task.</returns>
    public Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        PublishDeferred(eventData.Context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Discards the identifiers deferred by saves inside the rolled-back transaction without publishing them.
    /// </summary>
    /// <param name="transaction">The rolled-back transaction.</param>
    /// <param name="eventData">The transaction event data carrying the context.</param>
    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        => DiscardDeferred(eventData.Context);

    /// <summary>
    /// Discards the identifiers deferred by saves inside the rolled-back transaction without publishing them.
    /// </summary>
    /// <param name="transaction">The rolled-back transaction.</param>
    /// <param name="eventData">The transaction event data carrying the context.</param>
    /// <param name="cancellationToken">A token that cancels the operation; not observed.</param>
    /// <returns>A completed task.</returns>
    public Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardDeferred(eventData.Context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Discards the identifiers deferred inside a transaction whose commit or rollback failed, because its outcome
    /// is unknown and publishing could announce data that never became readable.
    /// </summary>
    /// <param name="transaction">The failed transaction.</param>
    /// <param name="eventData">The transaction error event data carrying the context.</param>
    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        => DiscardDeferred(eventData.Context);

    /// <summary>
    /// Discards the identifiers deferred inside a transaction whose commit or rollback failed, because its outcome
    /// is unknown and publishing could announce data that never became readable.
    /// </summary>
    /// <param name="transaction">The failed transaction.</param>
    /// <param name="eventData">The transaction error event data carrying the context.</param>
    /// <param name="cancellationToken">A token that cancels the operation; not observed.</param>
    /// <returns>A completed task.</returns>
    public Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardDeferred(eventData.Context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Collects the workers and deployments affected by the relevant tracked changes of a context about to save.
    /// </summary>
    /// <param name="context">The saving context, or <see langword="null"/> when the event carries none.</param>
    private void Collect(DbContext? context)
    {
        if (context is null)
            return;

        try
        {
            List<Guid>? workerIds = null;
            List<Guid>? deploymentIds = null;
            foreach (var entry in context.ChangeTracker.Entries<WorkerCommand>())
            {
                if (!IsRelevant(entry))
                    continue;
                (workerIds ??= []).Add(entry.Entity.WorkerFk);
            }

            foreach (var entry in context.ChangeTracker.Entries<GameServerDeployment>())
            {
                if (!IsRelevant(entry))
                    continue;
                (deploymentIds ??= []).Add(entry.Entity.PrimaryKey);
            }

            if (workerIds is null && deploymentIds is null)
                return;

            var pending = _pending.GetValue(context, static _ => new PendingChanges());
            lock (pending.Lock)
            {
                if (workerIds is not null)
                    pending.CollectedWorkers.UnionWith(workerIds);
                if (deploymentIds is not null)
                    pending.CollectedDeployments.UnionWith(deploymentIds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect worker wake signals before saving lifecycle state");
        }
    }

    /// <summary>
    /// Determines whether a tracked command changed in a way its worker must hear about.
    /// </summary>
    /// <param name="entry">The tracked command entry.</param>
    /// <returns><see langword="true"/> when the command was added or its status was modified.</returns>
    private static bool IsRelevant(EntityEntry<WorkerCommand> entry)
        => entry.State switch
        {
            EntityState.Added    => true,
            EntityState.Modified => entry.Property(q => q.Status).IsModified,
            _                    => false,
        };

    /// <summary>
    /// Determines whether a tracked deployment changed in a way its workers must hear about: a status change or a
    /// provisioning block decides whether a command may still execute.
    /// </summary>
    /// <param name="entry">The tracked deployment entry.</param>
    /// <returns><see langword="true"/> when its status or provisioning block was modified.</returns>
    private static bool IsRelevant(EntityEntry<GameServerDeployment> entry)
        => entry.State is EntityState.Modified
           && (entry.Property(q => q.Status).IsModified
               || entry.Property(q => q.IsProvisioningBlocked).IsModified);

    /// <summary>
    /// After a successful save, publishes the collected identifiers when no explicit transaction is open, or moves
    /// them into the deferred sets of the open transaction.
    /// </summary>
    /// <param name="context">The context that saved, or <see langword="null"/> when the event carries none.</param>
    private void PublishOrDefer(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
            return;

        try
        {
            var deferToCommit = context.Database.CurrentTransaction is not null;
            Guid[] workerIds;
            Guid[] deploymentIds;
            lock (pending.Lock)
            {
                if (deferToCommit)
                {
                    pending.DeferredWorkers.UnionWith(pending.CollectedWorkers);
                    pending.DeferredDeployments.UnionWith(pending.CollectedDeployments);
                    pending.CollectedWorkers.Clear();
                    pending.CollectedDeployments.Clear();
                    return;
                }

                workerIds     = pending.CollectedWorkers.ToArray();
                deploymentIds = pending.CollectedDeployments.ToArray();
                pending.CollectedWorkers.Clear();
                pending.CollectedDeployments.Clear();
            }

            Publish(workerIds, deploymentIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish worker wake signals after saving lifecycle state");
        }
    }

    /// <summary>
    /// Publishes and clears the identifiers deferred for a context whose transaction committed.
    /// </summary>
    /// <param name="context">The context whose transaction committed, or <see langword="null"/>.</param>
    private void PublishDeferred(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
            return;

        try
        {
            Guid[] workerIds;
            Guid[] deploymentIds;
            lock (pending.Lock)
            {
                workerIds     = pending.DeferredWorkers.ToArray();
                deploymentIds = pending.DeferredDeployments.ToArray();
                pending.DeferredWorkers.Clear();
                pending.DeferredDeployments.Clear();
            }

            Publish(workerIds, deploymentIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish deferred worker wake signals after a transaction commit");
        }
    }

    /// <summary>
    /// Queues one Wake per distinct worker and deployment identifier.
    /// </summary>
    /// <param name="workerIds">The distinct workers to wake.</param>
    /// <param name="deploymentIds">The distinct deployments whose workers to wake.</param>
    private void Publish(Guid[] workerIds, Guid[] deploymentIds)
    {
        if (workerIds.Length > 0)
            _signal.WakeWorkers(workerIds);
        if (deploymentIds.Length > 0)
            _signal.WakeDeployments(deploymentIds);
    }

    /// <summary>
    /// Clears the identifiers collected by a save that failed or was canceled; deferred identifiers are kept.
    /// </summary>
    /// <param name="context">The context whose save did not complete, or <see langword="null"/>.</param>
    private void DiscardCollected(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
            return;

        lock (pending.Lock)
        {
            pending.CollectedWorkers.Clear();
            pending.CollectedDeployments.Clear();
        }
    }

    /// <summary>
    /// Clears the identifiers deferred for a context without publishing them.
    /// </summary>
    /// <param name="context">The context whose transaction ended without a usable commit, or <see langword="null"/>.</param>
    private void DiscardDeferred(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
            return;

        lock (pending.Lock)
        {
            pending.DeferredWorkers.Clear();
            pending.DeferredDeployments.Clear();
        }
    }

    /// <summary>
    /// Holds the workers and deployments collected for one context between the start and the end of a save, and
    /// those deferred until the context's explicit transaction commits.
    /// </summary>
    private sealed class PendingChanges
    {
        /// <summary>
        /// Gets the lock object that guards all four collections.
        /// </summary>
        /// <value>A dedicated lock instance for this context.</value>
        public Lock Lock { get; } = new();

        /// <summary>
        /// Gets the workers collected during the current save.
        /// </summary>
        /// <value>The distinct worker identifiers of the save in progress.</value>
        public HashSet<Guid> CollectedWorkers { get; } = [];

        /// <summary>
        /// Gets the deployments collected during the current save.
        /// </summary>
        /// <value>The distinct deployment identifiers of the save in progress.</value>
        public HashSet<Guid> CollectedDeployments { get; } = [];

        /// <summary>
        /// Gets the workers held back until the context's explicit transaction commits.
        /// </summary>
        /// <value>The distinct worker identifiers awaiting a transaction commit.</value>
        public HashSet<Guid> DeferredWorkers { get; } = [];

        /// <summary>
        /// Gets the deployments held back until the context's explicit transaction commits.
        /// </summary>
        /// <value>The distinct deployment identifiers awaiting a transaction commit.</value>
        public HashSet<Guid> DeferredDeployments { get; } = [];
    }
}
