using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// Represents a plain configuration entry within the system.
/// This class is used to store configuration details including metadata and associations to other entities.
/// </summary>
public record ConfigurationEntryUpdate
{
    /// <summary>
    /// The filename for the configuration.
    /// </summary>
    [MaxLength(2048)]
    public string Realm { get; init; } = string.Empty;

    /// <summary>
    /// The path for the configuration.
    /// </summary>
    [MaxLength(2048)]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// The value held.
    /// </summary>
    [MaxLength(2048)]
    public string Value { get; init; } = string.Empty;
}
