using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Reports receipt of a deployment-bound command.
/// </summary>
public sealed record WorkerCommandStageRequest
{
    public Guid DeploymentId { get; init; }

    [Required, MaxLength(128)]
    public string Stage { get; init; } = string.Empty;
}
