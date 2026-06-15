namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Integrity and attribution manifest embedded in every worker storage archive.
/// </summary>
public sealed record WorkerStorageArchiveManifest
{
    public int Version { get; init; } = WorkerStorageProtocol.Version;

    public EWorkerStorageArchiveKind Kind { get; init; }

    public Guid ServerId { get; init; }

    public Guid DeploymentId { get; init; }

    public Guid WorkerId { get; init; }

    public Guid CommandId { get; init; }

    public string? PreservationPlanToken { get; init; }

    public IReadOnlyList<WorkerStorageArchiveEntry> Entries { get; init; } = [];
}