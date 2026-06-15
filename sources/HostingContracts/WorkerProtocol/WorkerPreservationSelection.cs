namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Current server-owned preservation selection supplied to its dedicated worker.
/// </summary>
public sealed record WorkerPreservationSelection
{
    public Guid SelectionId { get; init; }

    public string RelativePath { get; init; } = string.Empty;

    public EPreservationScope Scope { get; init; }

    public EPreservationMode Mode { get; init; }
}