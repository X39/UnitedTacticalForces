using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// The outcome of one automation evaluation pass, limited to what committed.
/// </summary>
/// <param name="RaisedAlerts">The warning alerts recorded by committed transactions.</param>
/// <param name="ResolvedAlerts">The warning alerts withdrawn by committed transactions.</param>
/// <param name="JobsQueued">The number of ordinary stop jobs the pass queued.</param>
internal sealed record ServerAutomationEvaluationResult(
    IReadOnlyList<RaisedCostAlert> RaisedAlerts,
    IReadOnlyList<ResolvedCostAlert> ResolvedAlerts,
    int JobsQueued);
