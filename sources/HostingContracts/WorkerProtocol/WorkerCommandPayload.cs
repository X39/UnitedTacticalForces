using System.Text.Json;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Attribution and execution policy captured when a dedicated-worker command is persisted.
/// Game-specific workers may extend this JSON object with additional allowlisted settings.
/// </summary>
public sealed record WorkerCommandPayload
{
    public Guid ServerId { get; init; }

    public Guid DeploymentId { get; init; }

    public string GameIdentifier { get; init; } = string.Empty;

    public string ActionIdentifier { get; init; } = string.Empty;

    public int GracefulStopAttempts { get; init; }

    public int GracefulStopTimeoutSeconds { get; init; }

    /// <summary>
    /// Absolute control-plane deadline for the internal bounded fault-evidence command.
    /// Null for ordinary worker commands.
    /// </summary>
    public DateTimeOffset? FaultEvidenceDeadline { get; init; }

    /// <summary>
    /// Optional game port allocated by the control plane for this durable server identity.
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// Provider-neutral allowlisted content selected for this command.
    /// Game workers decide which reference sources and identifiers they support.
    /// </summary>
    public IReadOnlyCollection<WorkerContentReference> Content { get; init; } = [];

    /// <summary>
    /// Resolved, typed game-configuration snapshot captured when this durable command is created.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Configuration { get; init; }
        = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>
    /// Conservative peak bytes snapshotted by the control plane for validation before preparation.
    /// </summary>
    public long? PeakStorageRequiredBytes { get; init; }
}
