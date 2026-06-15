using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Reports a terminal command failure without exposing raw worker exception data.
/// </summary>
public sealed record WorkerCommandFailureRequest
{
    public Guid DeploymentId { get; init; }

    [Required, MaxLength(128)]
    public string Stage { get; init; } = string.Empty;

    [Required, MaxLength(128)]
    public string ErrorCode { get; init; } = string.Empty;

    [Required, MaxLength(2048)]
    public string ErrorMessage { get; init; } = string.Empty;

    [MaxLength(16384)]
    public string? Output { get; init; }
}
