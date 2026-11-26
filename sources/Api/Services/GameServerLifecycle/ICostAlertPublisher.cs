namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Pushes accrued cost values, new cost alerts, dismissals and withdrawn warnings over the update stream to the users who may see them.
/// </summary>
/// <remarks>
/// Publishing is best effort: a failure is logged and never thrown, because the REST list of cost alerts and the
/// accrued cost endpoint are the source of truth. Every frame is addressed to one recipient's own path; there is no
/// broadcast.
/// </remarks>
internal interface ICostAlertPublisher
{
    /// <summary>
    /// Pushes the accrued cost of every costing deployment to the users who may view both the server and the cost
    /// owner of its session.
    /// </summary>
    /// <param name="accruals">The accruals of an evaluation pass; resolved deployments are skipped.</param>
    /// <param name="cancellationToken">A token that cancels the audience reads.</param>
    /// <returns>
    /// A task completing once every recipient was attempted, or once the batch was dropped because an audience could
    /// not be read.
    /// </returns>
    Task PublishAccrualsAsync(IReadOnlyList<DeploymentAccrual> accruals, CancellationToken cancellationToken);

    /// <summary>
    /// Pushes every newly recorded alert to the users who may view its cost owner.
    /// </summary>
    /// <param name="alerts">The alerts an evaluation pass recorded.</param>
    /// <param name="cancellationToken">A token that cancels the audience read.</param>
    /// <returns>
    /// A task completing once every recipient was attempted, or once the batch was dropped because the audience could
    /// not be read.
    /// </returns>
    Task PublishRaisedAsync(IReadOnlyList<RaisedCostAlert> alerts, CancellationToken cancellationToken);

    /// <summary>
    /// Tells the sockets of one viewer that alerts were dismissed.
    /// </summary>
    /// <param name="userId">The viewer who dismissed the alerts; the only recipient.</param>
    /// <param name="costAlertIds">The ids of the alerts that were newly dismissed.</param>
    /// <param name="cancellationToken">A token that cancels the sends.</param>
    /// <returns>A task completing once one frame per alert was attempted.</returns>
    Task PublishDismissedAsync(Guid userId, IReadOnlyCollection<Guid> costAlertIds, CancellationToken cancellationToken);

    /// <summary>
    /// Tells the cost owner's audience that automatic stop warnings were withdrawn because their stop was cancelled
    /// or already started.
    /// </summary>
    /// <param name=alerts>The withdrawn alerts; a dismissal frame is sent per alert.</param>
    /// <param name=cancellationToken>A token that cancels the audience read and the sends.</param>
    /// <returns>
    /// A task completing once every recipient was attempted, or once the batch was dropped because the audience could
    /// not be read. It never throws.
    /// </returns>
    Task PublishResolvedAsync(IReadOnlyList<ResolvedCostAlert> alerts, CancellationToken cancellationToken);
}
