using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Core;

[Index(nameof(TerrainFk), nameof(VersionNumber), IsUnique = true)]
public sealed class TerrainVersion
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(TerrainFk)), DeleteBehavior(DeleteBehavior.Cascade)]
    public Terrain? Terrain { get; set; }
    public long TerrainFk { get; set; }

    public int VersionNumber { get; set; }
    [MaxLength(128)] public string VersionLabel { get; set; } = string.Empty;
    public EContentSourceKind SourceKind { get; set; }
    [MaxLength(256)] public string SourceIdentifier { get; set; } = string.Empty;
    [MaxLength(128)] public string? ArtifactIdentifier { get; set; }
    [MaxLength(128)] public string? ChecksumSha256 { get; set; }
    public long? ArtifactSizeBytes { get; set; }
    [Column(TypeName = "jsonb")] public string Metadata { get; set; } = "{}";
    public Instant TimeStampCreated { get; set; }
    [ForeignKey(nameof(CreatedByFk)), DeleteBehavior(DeleteBehavior.Restrict)] public User? CreatedBy { get; set; }
    public Guid? CreatedByFk { get; set; }
}
