using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

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