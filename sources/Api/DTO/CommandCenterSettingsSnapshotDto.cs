namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a redacted aggregate Command Center settings snapshot.
/// </summary>
public record CommandCenterSettingsSnapshotDto
{
    /// <summary>
    /// Whether the API base URL is configured.
    /// </summary>
    public bool ApiBaseUrlConfigured { get; init; }

    /// <summary>
    /// Whether the client base URL is configured.
    /// </summary>
    public bool ClientBaseUrlConfigured { get; init; }

    /// <summary>
    /// Whether a path base is configured.
    /// </summary>
    public bool BasePathConfigured { get; init; }

    /// <summary>
    /// Whether new users are automatically verified.
    /// </summary>
    public bool AutoVerifyNewUsers { get; init; }

    /// <summary>
    /// Redacted feature configuration rows.
    /// </summary>
    public CommandCenterSettingsFeatureDto[] Features { get; init; } = [];
}
