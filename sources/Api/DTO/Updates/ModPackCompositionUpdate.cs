namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// Represents an update to a composition-based mod pack.
/// </summary>
public class ModPackCompositionUpdate
{
    /// <summary>
    /// Represents the title or name associated with the event.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public string? Title { get; init; }

    /// <summary>
    /// Represents a collection of identifiers for mod pack revisions associated with an update.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public ICollection<long>? ModPackRevisionIds { get; init; }

    /// <summary>
    /// Indicates whether the latest revision of a mod pack should be used.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public bool? UseLatest { get; init; }
}
