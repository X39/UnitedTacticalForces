using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Durable estimated and measured storage evidence for one game-scoped content component.
/// </summary>
[Index(
    nameof(GameIdentifier),
    nameof(SourceIdentifier),
    nameof(ApplicationIdentifier),
    nameof(ItemIdentifier),
    IsUnique = true)]
public sealed class ContentStorageObservation : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [MaxLength(128)]
    public string GameIdentifier { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ComponentKind { get; set; } = string.Empty;

    [MaxLength(64)]
    public string SourceIdentifier { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ApplicationIdentifier { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ItemIdentifier { get; set; } = string.Empty;

    public long? ReportedBytes { get; set; }

    public long? MeasuredBytes { get; set; }

    [MaxLength(256)]
    public string? ExternalVersion { get; set; }

    [MaxLength(256)]
    public string? MeasuredVersion { get; set; }

    [MaxLength(64)]
    public string ObservationStatus { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ObservationSource { get; set; } = string.Empty;

    public Instant? ExternalUpdatedAt { get; set; }

    public Instant? ReportedAt { get; set; }

    public Instant? MeasuredAt { get; set; }

    [ForeignKey(nameof(LastMeasuredWorkerCommandFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public WorkerCommand? LastMeasuredWorkerCommand { get; set; }

    public Guid? LastMeasuredWorkerCommandFk { get; set; }

    public Instant TimeStampCreated { get; set; }

    public Instant TimeStampUpdated { get; set; }
}
