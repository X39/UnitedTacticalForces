namespace X39.UnitedTacticalForces.Api.Services.ServerMetrics;

/// <summary>
/// The trusted attribution of a metrics sample, built from the authenticated worker and never from the payload.
/// </summary>
/// <param name="GameServerId">The server the authenticated worker's deployment belongs to.</param>
/// <param name="DeploymentId">The deployment of the authenticated worker.</param>
/// <param name="WorkerId">The authenticated worker.</param>
/// <param name="IsPrimaryHost">
/// Whether the worker is the primary host of the deployment, which is the game-server service with ordinal 0.
/// Only primary-host samples are stored.
/// </param>
public sealed record ServerMetricSampleSource(Guid GameServerId, Guid DeploymentId, Guid WorkerId, bool IsPrimaryHost);
