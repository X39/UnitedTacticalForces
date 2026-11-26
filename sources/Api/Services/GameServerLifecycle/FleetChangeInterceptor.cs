using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Observes committed lifecycle state and reports the affected game servers to the <see cref="IFleetChangeNotifier"/>.
/// </summary>
/// <remarks>
/// <para>
/// EF Core interceptors registered on the context options are singletons shared by every context instance, so all
/// per-save state lives in a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed by the context and guarded by a
/// per-context lock. There is no other mutable state, so two contexts saving at the same time never see each other's
/// server identifiers.
/// </para>
/// <para>
/// Covered changes: a <see cref="GameServer"/> that is added, or modified in its title, archive flag, launch flag or
/// current cost owner; a <see cref="GameServerDeployment"/> that is added, or modified in its status or possible-cost
/// flag; a <see cref="LifecycleJob"/> that is added, or modified in its status. Stage, progress and timestamp churn
/// produce no signal, so a running job cannot flood the stream. Automation changes also count: a
/// <see cref="ServerAutomationSettings"/> row or a <see cref="ServerSchedule"/> rule that is added, modified or
/// deleted, and an <see cref="AutomationPendingStop"/> that is added or modified, so the workspace notice and the
/// status list refresh without waiting for the fallback poll. A <see cref="ServerScheduleRun"/> row that is added, or
/// whose outcome changes, counts too, so the last-run line of a rule refreshes; a waiting row is not rewritten on later
/// passes, so this cannot flood the stream.
/// </para>
/// <para>
/// <see cref="ServerAutomationState"/> is deliberately excluded: the evaluator rewrites it on every pass, and a frame
/// per pass would flood the stream. Changes made with <c>ExecuteUpdateAsync</c> or <c>ExecuteDeleteAsync</c> bypass
/// the change tracker and therefore never produce a frame; a caller that needs a frame must use tracked changes.
/// </para>
/// <para>
/// Timing: affected servers are collected before the save. After a successful save without an explicit transaction
/// they are published at once; inside an explicit transaction they are deferred and published only when that
/// transaction commits, so a signal never precedes readable data. A failed or canceled save discards the identifiers
/// it collected; a rolled-back or failed transaction discards everything it deferred. EF Core commits its own
/// implicit save transaction before <c>SavedChanges</c> runs, at which point nothing is deferred yet, so the
/// implicit commit publishes nothing and the identifiers are published by <c>SavedChanges</c> instead.
/// </para>
/// <para>
/// The interceptor never throws into the save or transaction path and never blocks it: every override returns the
/// incoming result unchanged, and the notifier call is non-blocking.
/// </para>
/// </remarks>
internal sealed class FleetChangeInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly IFleetChangeNotifier                            _notifier;
    private readonly ILogger<FleetChangeInterceptor>                 _logger;
    private readonly ConditionalWeakTable<DbContext, PendingChanges> _pending = new();

    /// <summary>
    /// Creates an interceptor that reports changed game servers to the specified notifier.
    /// </summary>
    /// <param name="notifier">The notifier that publishes fleet-changed signals.</param>
    /// <param name="logger">The logger receiving unexpected collection or publish failures.</param>
    public FleetChangeInterceptor(IFleetChangeNotifier notifier, ILogger<FleetChangeInterceptor> logger)
    {
        _notifier = notifier;
        _logger   = logger;
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
    /// Collects the game servers affected by the fleet-relevant tracked changes of a context about to save.
    /// </summary>
    /// <param name="context">The saving context, or <see langword="null"/> when the event carries none.</param>
    private void Collect(DbContext? context)
    {
        if (context is null)
            return;

        try
        {
            List<Guid>? serverIds = null;
            foreach (var entry in context.ChangeTracker.Entries<GameServer>())
            {
                if (!IsRelevant(entry))
                    continue;
                (serverIds ??= []).Add(entry.Entity.PrimaryKey);
            }

            foreach (var entry in context.ChangeTracker.Entries<GameServerDeployment>())
            {
                if (!IsRelevant(entry))
                    continue;
                (serverIds ??= []).Add(entry.Entity.GameServerFk);
            }

            foreach (var entry in context.ChangeTracker.Entries<LifecycleJob>())
            {
                if (!IsRelevant(entry))
                    continue;
                (serverIds ??= []).Add(entry.Entity.GameServerFk);
            }

            foreach (var entry in context.ChangeTracker.Entries<ServerAutomationSettings>())
            {
                if (!IsRelevant(entry))
                    continue;
                (serverIds ??= []).Add(entry.Entity.GameServerFk);
            }

            foreach (var entry in context.ChangeTracker.Entries<ServerSchedule>())
            {
                if (!IsRelevant(entry))
                    continue;
                (serverIds ??= []).Add(entry.Entity.GameServerFk);
            }

            foreach (var entry in context.ChangeTracker.Entries<AutomationPendingStop>())
            {
                if (!IsRelevant(entry))
                    continue;
                (serverIds ??= []).Add(entry.Entity.GameServerFk);
            }

            foreach (var entry in context.ChangeTracker.Entries<ServerScheduleRun>())
            {
                if (!IsRelevant(entry))
                    continue;
                (serverIds ??= []).Add(entry.Entity.GameServerFk);
            }

            if (serverIds is null)
                return;

            var pending = _pending.GetValue(context, static _ => new PendingChanges());
            lock (pending.Lock)
            {
                pending.Collected.UnionWith(serverIds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect fleet changes before saving lifecycle state");
        }
    }

    /// <summary>
    /// Determines whether a tracked game server changed in a way the fleet shows. A change of the pending-start areas
    /// counts because the workspace banner of other members shows them.
    /// </summary>
    /// <param name="entry">The tracked game server entry.</param>
    /// <returns><see langword="true"/> when the server was added or a fleet-visible property was modified.</returns>
    private static bool IsRelevant(EntityEntry<GameServer> entry)
        => entry.State switch
        {
            EntityState.Added    => true,
            EntityState.Modified => entry.Property(q => q.Title).IsModified
                                    || entry.Property(q => q.IsArchived).IsModified
                                    || entry.Property(q => q.IsLaunchDisabled).IsModified
                                    || entry.Property(q => q.CurrentCostOwnerFk).IsModified
                                    || entry.Property(q => q.PendingStartChangeAreas).IsModified,
            _ => false,
        };

    /// <summary>
    /// Determines whether a tracked deployment changed in a way the fleet shows.
    /// </summary>
    /// <param name="entry">The tracked deployment entry.</param>
    /// <returns><see langword="true"/> when the deployment was added or its status or possible cost was modified.</returns>
    private static bool IsRelevant(EntityEntry<GameServerDeployment> entry)
        => entry.State switch
        {
            EntityState.Added    => true,
            EntityState.Modified => entry.Property(q => q.Status).IsModified
                                    || entry.Property(q => q.PossibleCost).IsModified,
            _ => false,
        };

    /// <summary>
    /// Determines whether a tracked lifecycle job changed in a way the fleet shows.
    /// </summary>
    /// <param name="entry">The tracked lifecycle job entry.</param>
    /// <returns><see langword="true"/> when the job was added or its status was modified.</returns>
    private static bool IsRelevant(EntityEntry<LifecycleJob> entry)
        => entry.State switch
        {
            EntityState.Added    => true,
            EntityState.Modified => entry.Property(q => q.Status).IsModified,
            _                    => false,
        };

    /// <summary>
    /// Determines whether a tracked automation settings row changed. Every add, modification and removal counts
    /// because the settings are edited by users only.
    /// </summary>
    /// <param name="entry">The tracked automation settings entry.</param>
    /// <returns><see langword="true"/> when the settings were added, modified or deleted.</returns>
    private static bool IsRelevant(EntityEntry<ServerAutomationSettings> entry)
        => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    /// <summary>
    /// Determines whether a tracked schedule rule changed.
    /// </summary>
    /// <param name="entry">The tracked schedule rule entry.</param>
    /// <returns><see langword="true"/> when the rule was added, modified or deleted.</returns>
    private static bool IsRelevant(EntityEntry<ServerSchedule> entry)
        => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    /// <summary>
    /// Determines whether a tracked pending automatic stop changed.
    /// </summary>
    /// <param name="entry">The tracked pending stop entry.</param>
    /// <returns><see langword="true"/> when the pending stop was added or modified, for example resolved.</returns>
    private static bool IsRelevant(EntityEntry<AutomationPendingStop> entry)
        => entry.State is EntityState.Added or EntityState.Modified;

    /// <summary>
    /// Determines whether a tracked schedule run row changed in a way the rule's last-run line shows.
    /// </summary>
    /// <param name="entry">The tracked schedule run entry.</param>
    /// <returns>
    /// <see langword="true"/> when the run was added or its outcome was modified; a change of only the recorded instant
    /// or the details produces no signal.
    /// </returns>
    private static bool IsRelevant(EntityEntry<ServerScheduleRun> entry)
        => entry.State switch
        {
            EntityState.Added    => true,
            EntityState.Modified => entry.Property(q => q.Outcome).IsModified,
            _                    => false,
        };

    /// <summary>
    /// After a successful save, publishes the collected identifiers when no explicit transaction is open, or moves
    /// them into the deferred set of the open transaction.
    /// </summary>
    /// <param name="context">The context that saved, or <see langword="null"/> when the event carries none.</param>
    private void PublishOrDefer(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
            return;

        try
        {
            var deferToCommit = context.Database.CurrentTransaction is not null;
            Guid[] serverIds;
            lock (pending.Lock)
            {
                if (deferToCommit)
                {
                    pending.Deferred.UnionWith(pending.Collected);
                    pending.Collected.Clear();
                    return;
                }

                serverIds = pending.Collected.ToArray();
                pending.Collected.Clear();
            }

            Publish(serverIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish fleet changes after saving lifecycle state");
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
            Guid[] serverIds;
            lock (pending.Lock)
            {
                serverIds = pending.Deferred.ToArray();
                pending.Deferred.Clear();
            }

            Publish(serverIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish deferred fleet changes after a transaction commit");
        }
    }

    /// <summary>
    /// Sends one notification per distinct game server identifier.
    /// </summary>
    /// <param name="serverIds">The distinct identifiers to publish.</param>
    private void Publish(Guid[] serverIds)
    {
        foreach (var serverId in serverIds)
            _notifier.Notify(serverId);
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
            pending.Collected.Clear();
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
            pending.Deferred.Clear();
        }
    }

    /// <summary>
    /// Holds the game servers collected for one context between the start and the end of a save, and those
    /// deferred until the context's explicit transaction commits.
    /// </summary>
    private sealed class PendingChanges
    {
        /// <summary>
        /// Gets the lock object that guards both collections.
        /// </summary>
        /// <value>A dedicated lock instance for this context.</value>
        public Lock Lock { get; } = new();

        /// <summary>
        /// Gets the servers collected during the current save that are published or deferred once the save succeeded.
        /// </summary>
        /// <value>The distinct game server identifiers of the save in progress.</value>
        public HashSet<Guid> Collected { get; } = [];

        /// <summary>
        /// Gets the servers held back until the context's explicit transaction commits.
        /// </summary>
        /// <value>The distinct game server identifiers awaiting a transaction commit.</value>
        public HashSet<Guid> Deferred { get; } = [];
    }
}
