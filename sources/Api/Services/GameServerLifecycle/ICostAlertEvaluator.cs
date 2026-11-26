namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Compares the accrued cost of sessions with the thresholds of their cost owners and records each crossing once.
/// </summary>
internal interface ICostAlertEvaluator
{
    /// <summary>
    /// Runs one evaluation pass.
    /// </summary>
    /// <param name="now">The instant accrued cost is calculated for.</param>
    /// <param name="cancellationToken">A token that cancels the pass.</param>
    /// <returns>
    /// A task resolving to the accrued cost of every evaluated deployment and the alerts recorded by this pass.
    /// </returns>
    Task<CostAlertEvaluationResult> EvaluateAsync(Instant now, CancellationToken cancellationToken);
}
