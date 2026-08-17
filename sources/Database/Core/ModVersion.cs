using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Core;

[Index(nameof(ModFk), nameof(VersionNumber), IsUnique = true)]
[Index(nameof(ModFk), nameof(SourceKind), nameof(SourceIdentifier))]
public sealed class ModVersion
{
    [Key] public Guid PrimaryKey { get; set; }
    [ForeignKey(nameof(ModFk)), DeleteBehavior(DeleteBehavior.Cascade)] public Mod? Mod { get; set; }
    public Guid ModFk { get; set; }
    public int VersionNumber { get; set; }
    [MaxLength(128)] public string VersionLabel { get; set; } = string.Empty;
    public EContentSourceKind SourceKind { get; set; }
    [MaxLength(256)] public string SourceIdentifier { get; set; } = string.Empty;
    public EModUsageRole UsageRole { get; set; }
    [MaxLength(128)] public string InstallerIdentifier { get; set; } = string.Empty;
    [Column(TypeName = "jsonb")] public string InstallerConfiguration { get; set; } = "{}";
    [MaxLength(128)] public string? ArtifactIdentifier { get; set; }
    [MaxLength(128)] public string? ChecksumSha256 { get; set; }
    public long? ArtifactSizeBytes { get; set; }
    public Instant TimeStampCreated { get; set; }
}
