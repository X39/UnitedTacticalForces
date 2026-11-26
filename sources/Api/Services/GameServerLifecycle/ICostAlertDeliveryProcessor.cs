namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Claims due cost alert deliveries from the outbox, sends them through their channel and records the outcome.
/// </summary>
internal interface ICostAlertDeliveryProcessor
{
    /// <summary>
    /// Processes one batch of deliveries that are due at the given instant.
    /// </summary>
    /// <param name="now">The instant that decides which deliveries are due and stamps the recorded outcomes.</param>
    /// <param name="cancellationToken">
    /// Cancels the pass. A delivery interrupted by cancellation keeps its lease and is reclaimed after the lease
    /// expired.
    /// </param>
    /// <returns>The number of deliveries this pass claimed.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<int> ProcessDueAsync(Instant now, CancellationToken cancellationToken);
}
