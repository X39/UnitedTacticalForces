using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Core;

[Index(nameof(ScenarioFk), nameof(VersionNumber), IsUnique = true)]
public sealed class ScenarioVersion
{
    [Key] public Guid PrimaryKey { get; set; }
    [ForeignKey(nameof(ScenarioFk)), DeleteBehavior(DeleteBehavior.Cascade)] public Scenario? Scenario { get; set; }
    public Guid ScenarioFk { get; set; }
    public int VersionNumber { get; set; }
    [MaxLength(128)] public string VersionLabel { get; set; } = string.Empty;
    public EContentSourceKind SourceKind { get; set; }
    [MaxLength(256)] public string SourceIdentifier { get; set; } = string.Empty;
    [MaxLength(128)] public string? ArtifactIdentifier { get; set; }
    [MaxLength(128)] public string? ChecksumSha256 { get; set; }
    public long? ArtifactSizeBytes { get; set; }
    [Column(TypeName = "jsonb")] public string Metadata { get; set; } = "{}";
    [MaxLength(2048)] public string? Changelog { get; set; }
    public Instant TimeStampCreated { get; set; }
    [ForeignKey(nameof(CreatedByFk)), DeleteBehavior(DeleteBehavior.Restrict)] public User? CreatedBy { get; set; }
    public Guid? CreatedByFk { get; set; }
    public ICollection<ScenarioVersionRequiredModPack> RequiredModPacks { get; set; } = [];
}
