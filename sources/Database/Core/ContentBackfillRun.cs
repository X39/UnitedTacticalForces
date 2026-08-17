using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Database.Core;

public sealed class ContentBackfillRun
{
    [Key, MaxLength(128)] public string Identifier { get; set; } = string.Empty;
    public Instant TimeStampCompleted { get; set; }
    [MaxLength(128)] public string ImplementationVersion { get; set; } = string.Empty;
}
