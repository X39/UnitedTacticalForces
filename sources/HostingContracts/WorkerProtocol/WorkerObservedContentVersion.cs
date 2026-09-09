using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>Records the concrete provider version observed while preparing floating content.</summary>
public sealed record WorkerObservedContentVersion
{
    /// <summary>Gets the registered provider identifier.</summary>
    [Required, MaxLength(64)] public string ProviderIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the canonical source identifier.</summary>
    [Required, MaxLength(512)] public string SourceIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the concrete observed provider version, such as a Git commit.</summary>
    [Required, MaxLength(256)] public string ObservedVersion { get; init; } = string.Empty;
}
