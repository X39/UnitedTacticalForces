namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Public, non-secret topology information shared with deployment-bound workers.
/// </summary>
public sealed record WorkerNodeEndpoint
{
    public string ServiceIdentifier { get; init; } = string.Empty;

    public int ServiceOrdinal { get; init; }

    public string Address { get; init; } = string.Empty;

    public int? Port { get; init; }

    public bool IsPrimary { get; init; }
}
