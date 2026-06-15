using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Reports a running or successful command with optional process evidence.
/// </summary>
public sealed record WorkerCommandProcessRequest
{
    public Guid DeploymentId { get; init; }

    [Required, MaxLength(128)]
    public string Stage { get; init; } = string.Empty;

    public int? ProcessId { get; init; }

    public DateTimeOffset? ProcessStartedAt { get; init; }

    [MaxLength(16384)]
    public string? Output { get; init; }
}
