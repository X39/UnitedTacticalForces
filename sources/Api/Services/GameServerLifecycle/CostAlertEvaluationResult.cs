namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// The outcome of one cost alert evaluation pass.
/// </summary>
/// <param name="Accruals">The accrued cost of every deployment the pass evaluated.</param>
/// <param name="RaisedAlerts">The alerts the pass recorded; empty when no new threshold was crossed.</param>
internal sealed record CostAlertEvaluationResult(
    IReadOnlyList<DeploymentAccrual> Accruals,
    IReadOnlyList<RaisedCostAlert> RaisedAlerts);
