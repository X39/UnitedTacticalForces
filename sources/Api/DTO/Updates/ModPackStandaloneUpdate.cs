namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// Represents a standalone update to a mod pack in the system.
/// </summary>
public class ModPackStandaloneUpdate
{
    /// <summary>
    /// Represents the title associated with the event or entity.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public string? Title { get; init; }

    /// <summary>
    /// Contains HTML content, typically representing a detailed description or additional information.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public string? Html { get; init; }
}