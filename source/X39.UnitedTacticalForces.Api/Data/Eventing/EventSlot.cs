using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Authority;

namespace X39.UnitedTacticalForces.Api.Data.Eventing;

[PrimaryKey(nameof(EventFk), nameof(SlotNumber))]
public class EventSlot
{
    // https://github.com/CBATeam/CBA_A3/wiki/Name-Groups-in-Lobby for group-names with slot-names
    // ToDo: Parse MissionSQM in client and create slots which match that
    // ToDo: Add dialog to select slots
    // ToDo: Add sync button in the slot selection for author and role holders
    // ToDo: Add manual adding/removal/modification of slots in dialog for author and role holders
    // ToDo: Add an "IsAvailable" flag for events to allow authors to hold back on events until they are ready for release, admin and a new role should still be able to see events
    // ToDo: Block slotting if not accepted and unslot if acceptance state changes.

    /// <summary>
    /// Part 2 of the primary key.
    /// </summary>
    public short SlotNumber { get; set; }

    [ForeignKey(nameof(AssignedToFk))]
    public User? AssignedTo { get; set; }
    public Guid? AssignedToFk { get; set; }

    [ForeignKey(nameof(EventFk))]
    public Event? Event { get; set; }
    public Guid EventFk { get; set; }

    /// <summary>
    /// Denotes whether a non-author user may assign himself into this slot. 
    /// </summary>
    public bool IsSelfAssignable { get; set; }
    
    /// <summary>
    /// The group this role is part of.
    /// </summary>
    public string Group { get; set; } = string.Empty;
    
    /// <summary>
    /// The title of this role.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// The side of this role.
    /// </summary>
    public EArmaSide Side { get; set; }
    
    // UserAward or UserTraining ???? requirement to allow training-required slots to work by themself.
    
}