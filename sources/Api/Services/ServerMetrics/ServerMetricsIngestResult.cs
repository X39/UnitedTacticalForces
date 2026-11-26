namespace X39.UnitedTacticalForces.Api.Services.ServerMetrics;

/// <summary>
/// The outcome of ingesting one worker metrics sample.
/// </summary>
/// <param name="Stored">
/// Whether the sample was stored. <see langword="false"/> means it was valid but not from the primary host.
/// </param>
/// <param name="GameServerId">The server the sample was attributed to.</param>
public sealed record ServerMetricsIngestResult(bool Stored, Guid GameServerId);
