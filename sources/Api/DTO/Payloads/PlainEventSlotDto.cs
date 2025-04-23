using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Represents a plain data transfer object for an event slot in the system.
/// This DTO is used for transferring data related to specific slots within an event.
/// </summary>
public record EventSlotCreationPayload
{
    /// <summary>
    /// The unique identifier of the user or entity assigned to this slot.
    /// </summary>
    public Guid? AssignedToFk { get; init; }

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
