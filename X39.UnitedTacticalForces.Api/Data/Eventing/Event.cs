using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;

namespace X39.UnitedTacticalForces.Api.Data.Eventing;

public class Event
{
    [Key] public Guid PrimaryKey { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [ForeignKey(nameof(TerrainFk))] public Terrain? Terrain { get; set; }
    public long TerrainFk { get; set; }


    [ForeignKey(nameof(ModPackFk))] public ModPack? ModPack { get; set; }
    public long ModPackFk { get; set; }


    public byte[] Image { get; set; } = Array.Empty<byte>();
    public string ImageMimeType { get; set; } = string.Empty;

    public DateTimeOffset ScheduledForOriginal { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }

    public DateTimeOffset TimeStampCreated { get; set; }

    [ForeignKey(nameof(CreatedByFk))]
    public User? CreatedBy { get; set; }
    public Guid CreatedByFk { get; set; }

    [ForeignKey(nameof(HostedByFk))]
    public User? HostedBy { get; set; }
    public Guid HostedByFk { get; set; }
}