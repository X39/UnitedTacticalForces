namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// One evaluation input of the idle clock.
/// </summary>
/// <param name="Armed">Whether idle shutdown applies: it is switched on, the game reports a player count, and the
/// deployment is running.</param>
/// <param name="DeploymentId">The running deployment the observation belongs to, or <see langword="null" /> when
/// there is none.</param>
/// <param name="PlayerCount">The known player count, or <see langword="null" /> when the reading is unavailable
/// (stale, measuring, worker offline, or failed).</param>
/// <param name="HasActiveJob">Whether a lifecycle job of the deployment is queued, running, or waiting.</param>
public sealed record IdleObservation(bool Armed, Guid? DeploymentId, int? PlayerCount, bool HasActiveJob);
