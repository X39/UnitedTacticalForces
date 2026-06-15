using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Eventing;
using X39.UnitedTacticalForces.Common.Event;

namespace X39.UnitedTacticalForces.Database.Authority;

[PrimaryKey(nameof(UserFk), nameof(EventFk))]
public class UserEventMeta
{
    [ForeignKey(nameof(UserFk))]
    public User? User { get; set; }
    public Guid UserFk { get; set; }

    [ForeignKey(nameof(EventFk))]
    public Event? Event { get; set; }
    public Guid EventFk { get; set; }

    public EEventAcceptance Acceptance { get; set; }
}