using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Durable operational record for one UTF-managed provider resource that has no valid deployment ownership chain.
/// </summary>
[Index(
    nameof(ProviderIdentifier),
    nameof(ResourceType),
    nameof(ProviderResourceIdentifier),
    IsUnique = true)]
[Index(nameof(Status), nameof(TimeStampUpdated))]
public sealed class DanglingProviderResourceIncident : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key]
    public Guid PrimaryKey { get; set; }

    [MaxLength(128)]
    public string ProviderIdentifier { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ResourceType { get; set; } = string.Empty;

    [MaxLength(256)]
    public string ProviderResourceIdentifier { get; set; } = string.Empty;

    public EDanglingResourceIncidentStatus Status { get; set; }

    public bool IsPlatformManaged { get; set; }

    public bool PossibleCost { get; set; }

    [MaxLength(128)]
    public string? LastProviderState { get; set; }

    [MaxLength(2048)]
    public string? LastFailure { get; set; }

    public int ObservationCount { get; set; }

    public int CleanupAttemptCount { get; set; }

    public Instant TimeStampCreated { get; set; }

    public Instant TimeStampUpdated { get; set; }

    public Instant TimeStampLastObserved { get; set; }

    public Instant? TimeStampLastCleanupAttempt { get; set; }

    public Instant? TimeStampResolved { get; set; }
}
