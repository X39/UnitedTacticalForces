using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.DTO.Updates;

/// <summary>
/// Represents a data transfer object for updating an event slot with various attributes.
/// </summary>
public record EventSlotUpdate
{
    /// <summary>
    /// Represents the title of the event slot.
    /// Can be used to specify or update the label or description for an individual slot in an event.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public string? Title { get; init; }

    /// <summary>
    /// Represents the group or category associated with an event slot.
    /// Can be used to classify or organize event slots under a specific label or grouping.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public string? Group { get; init; }

    /// <summary>
    /// Indicates whether the event slot can be assigned to the user themselves without requiring admin intervention.
    /// This property manages the self-assignment capability of a given event slot.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public bool? IsSelfAssignable { get; init; }

    /// <summary>
    /// Indicates whether the event slot should be visible to users.
    /// This property can be used to toggle the visibility of specific event slots
    /// within an event, allowing for dynamic control over their display.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public bool? IsVisible { get; init; }

    /// <summary>
    /// Gets or initializes the side associated with the event slot in the context of Arma game mechanics.
    /// Represents what side the event slot belongs to, such as East, West, Civilian, etc.,
    /// using the <see cref="EArmaSide"/> enumeration.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> is provided, no change will be made.
    /// </remarks>
    public EArmaSide? Side { get; init; }
}
