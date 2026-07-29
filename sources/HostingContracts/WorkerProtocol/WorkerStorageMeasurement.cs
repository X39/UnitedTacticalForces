using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// A bounded, deployment-local storage measurement produced by a game worker after preparation.
/// </summary>
public sealed record WorkerStorageMeasurement
{
    [Required, MaxLength(64)]
    public string ComponentKind { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string ApplicationIdentifier { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string ItemIdentifier { get; init; } = string.Empty;

    [Range(0, long.MaxValue)]
    public long InstalledBytes { get; init; }

    [MaxLength(256)]
    public string? VersionMetadata { get; init; }
}
