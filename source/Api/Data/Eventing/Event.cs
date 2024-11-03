using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;

namespace X39.UnitedTacticalForces.Api.Data.Eventing;

[Index(nameof(ScheduledFor))]
public class Event
{
    [Key] public Guid PrimaryKey { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [ForeignKey(nameof(TerrainFk))] public Terrain? Terrain { get; set; }
    public long TerrainFk { get; set; }


    [ForeignKey(nameof(ModPackRevisionFk))] public ModPackRevision? ModPackRevision { get; set; }
    public long ModPackRevisionFk { get; set; }


    public byte[] Image { get; set; } = Array.Empty<byte>();
    public string ImageMimeType { get; set; } = string.Empty;

    public DateTimeOffset ScheduledForOriginal { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }

    public DateTimeOffset TimeStampCreated { get; set; }
    
    public bool IsVisible { get; set; }
    
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
    public int MaybeCount { get; set; }
    
    public int MinimumAccepted { get; set; }

    [ForeignKey(nameof(OwnerFk))]
    public User? Owner { get; set; }
    public Guid OwnerFk { get; set; }

    [ForeignKey(nameof(HostedByFk))]
    public User? HostedBy { get; set; }
    public Guid HostedByFk { get; set; }
    
    public ICollection<UserEventMeta>? UserMetas { get; set; }
}