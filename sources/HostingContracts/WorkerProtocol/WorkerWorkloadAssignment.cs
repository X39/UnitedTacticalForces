namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>Assigns a provider-neutral workload identity to its persisted worker host.</summary>
public sealed record WorkerWorkloadAssignment
{
    /// <summary>Gets stable service identifier of the assigned workload.</summary>
    /// <value>Stable service identifier of the assigned workload.</value>
    public string ServiceIdentifier { get; init; } = string.Empty;

    /// <summary>Gets ordinal distinguishing instances of the workload.</summary>
    /// <value>Ordinal distinguishing instances of the workload.</value>
    public int ServiceOrdinal { get; init; }

    /// <summary>Gets service identifier of the worker host that executes the workload.</summary>
    /// <value>Service identifier of the worker host that executes the workload.</value>
    public string HostServiceIdentifier { get; init; } = string.Empty;

    /// <summary>Gets ordinal distinguishing worker hosts.</summary>
    /// <value>Ordinal distinguishing worker hosts.</value>
    public int HostServiceOrdinal { get; init; }

    /// <summary>Gets whether this workload owns the primary game endpoint.</summary>
    /// <value>Whether this workload owns the primary game endpoint.</value>
    public bool IsPrimary { get; init; }
}
