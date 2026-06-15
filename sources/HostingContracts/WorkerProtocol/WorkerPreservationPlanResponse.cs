namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

public sealed record WorkerPreservationPlanResponse
{
    public Guid ServerId { get; init; }

    public Guid DeploymentId { get; init; }

    public Guid WorkerId { get; init; }

    public Guid CommandId { get; init; }

    public string PlanToken { get; init; } = string.Empty;

    public IReadOnlyList<WorkerPreservationSelection> Selections { get; init; } = [];
}