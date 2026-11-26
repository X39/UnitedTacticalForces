namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// The decision of <see cref="PendingStopPlanner" /> for one deployment.
/// </summary>
/// <param name="Decision">What the evaluator has to do.</param>
/// <param name="Candidate">
/// The stop to warn about or to execute (also the replacement of a <see cref="EPendingStopDecision.Replace" />), or
/// <see langword="null" /> for a cancellation and no change.
/// </param>
/// <param name="CancelReason">
/// A short machine-readable code for <see cref="EPendingStopDecision.Cancel" /> and
/// <see cref="EPendingStopDecision.Supersede" /> and <see cref="EPendingStopDecision.Replace" />: <c>activity</c>, <c>settings-changed</c>,
/// <c>deployment-not-running</c>, <c>stop-already-queued</c> or <c>earlier-stop</c>; otherwise <see langword="null" />.
/// </param>
public sealed record PendingStopPlan(
    EPendingStopDecision Decision,
    PendingStopCandidate? Candidate,
    string? CancelReason);
