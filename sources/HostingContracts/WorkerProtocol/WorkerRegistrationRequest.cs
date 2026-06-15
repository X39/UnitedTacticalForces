using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Binds a newly provisioned worker to its intended deployment.
/// The bootstrap credential is carried only in the Authorization header.
/// </summary>
public sealed record WorkerRegistrationRequest
{
    public Guid DeploymentId { get; init; }

    [Required, MaxLength(256)]
    public string Identity { get; init; } = string.Empty;
}
