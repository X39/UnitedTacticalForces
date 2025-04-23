using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a plain data transfer object for an event slot in the system.
/// This DTO is used for transferring data related to specific slots within an event.
/// </summary>
public record PlainEventSlotDto
{
    /// <summary>
    /// Part 2 of the primary key.
    /// </summary>
    public short SlotNumber { get; init; }

    /// <summary>
    /// The unique identifier of the user or entity assigned to this slot.
    /// </summary>
    public Guid? AssignedToFk { get; init; }

    /// <summary>
    /// The foreign key referencing the associated event.
    /// </summary>
    public Guid EventFk { get; init; }

    /// <summary>
    /// Denotes whether a non-author user may assign themselves into this slot. 
    /// </summary>
    public bool IsSelfAssignable { get; init; }

    /// <summary>
    /// Denotes whether a non-author user may view this slot. 
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// The group this role is part of.
    /// </summary>
    [MaxLength(1024)]
    public string Group { get; init; } = string.Empty;

    /// <summary>
    /// The title of this role.
    /// </summary>
    [MaxLength(1024)]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The side of this role.
    /// </summary>
    public EArmaSide Side { get; init; }
}
