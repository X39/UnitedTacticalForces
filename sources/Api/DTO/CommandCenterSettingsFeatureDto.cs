namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a redacted operational settings row for Command Center settings.
/// </summary>
public record CommandCenterSettingsFeatureDto
{
    /// <summary>
    /// The settings area.
    /// </summary>
    public string Area { get; init; } = string.Empty;

    /// <summary>
    /// Whether the feature is enabled by configuration.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Whether required non-secret configuration markers are present.
    /// </summary>
    public bool IsConfigured { get; init; }

    /// <summary>
    /// Redacted human-readable configuration detail.
    /// </summary>
    public string Detail { get; init; } = string.Empty;
}
