using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// What one schedule scan of a server committed.
/// </summary>
/// <param name="RaisedAlerts">The warning alerts recorded by committed transactions of the scan.</param>
/// <param name="ResolvedAlerts">
/// The warning alerts the scan withdrew because a scheduled stop superseded their warning.
/// </param>
/// <param name="StartsQueued">The number of scheduled start jobs the scan queued.</param>
internal sealed record ScheduleScanResult(
    IReadOnlyList<RaisedCostAlert> RaisedAlerts,
    IReadOnlyList<ResolvedCostAlert> ResolvedAlerts,
    int StartsQueued);
