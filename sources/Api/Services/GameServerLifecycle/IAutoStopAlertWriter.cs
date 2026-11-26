using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Records and withdraws the alert that warns a cost owner about a pending automatic stop. The alert is the
/// <see cref="Hosting.Contracts.ECostAlertKind.AutoStop" /> kind of the cost alert stack, so it reuses the recipients,
/// channels, outbox and per-viewer dismissal of cost threshold alerts.
/// </summary>
/// <remarks>
/// Neither method saves. The caller adds the alert to the same transaction as the pending stop and the audit event, so
/// the warning, its delivery intents and the stop state commit together.
/// </remarks>
internal interface IAutoStopAlertWriter
{
    /// <summary>
    /// Adds the warning alert and its delivery intents for a pending stop to the database context.
    /// </summary>
    /// <param name="pendingStop">The pending stop the alert announces.</param>
    /// <param name="deployment">The deployment the stop applies to; its cost owner receives the warning.</param>
    /// <param name="serverTitle">The title of the server at the time of the warning.</param>
    /// <param name="now">The instant the alert and its delivery rows are created at.</param>
    /// <param name="cancellationToken">A token that cancels the database lookups.</param>
    /// <returns>
    /// A task resolving to the data needed to publish the alert after the caller saved it. The alert is not
    /// persisted until the caller saves the context.
    /// </returns>
    Task<RaisedCostAlert> AddWarningAsync(
        AutomationPendingStop pendingStop,
        GameServerDeployment deployment,
        string serverTitle,
        Instant now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Withdraws the warning alert of a pending stop that was cancelled or whose stop job started, by marking its
    /// not yet delivered rows as skipped in the database context.
    /// </summary>
    /// <param name="pendingStopId">The primary key of the resolved pending stop.</param>
    /// <param name="now">The instant the rows are updated at.</param>
    /// <param name="cancellationToken">A token that cancels the database lookups.</param>
    /// <returns>
    /// A task resolving to the withdrawn alert, or <see langword="null" /> when the pending stop has no alert. The
    /// change is not persisted until the caller saves the context.
    /// </returns>
    Task<ResolvedCostAlert?> ResolveAsync(Guid pendingStopId, Instant now, CancellationToken cancellationToken);
}
