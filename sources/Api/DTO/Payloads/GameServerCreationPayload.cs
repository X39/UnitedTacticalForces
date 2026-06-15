using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Creates a durable game-server record without starting a deployment.
/// </summary>
public sealed record GameServerCreationPayload
{
    /// <summary>
    /// Human-readable server title.
    /// </summary>
    [Required, MaxLength(256)]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Stable game implementation identifier.
    /// </summary>
    [Required, MaxLength(128)]
    public string GameIdentifier { get; init; } = "arma3";

    /// <summary>
    /// Runtime provider identifier. Hetzner is the first production implementation.
    /// </summary>
    [Required, MaxLength(128)]
    public string RuntimeProviderIdentifier { get; init; } = "hetzner";

    /// <summary>
    /// Optional selected payer. When omitted, the caller's personal cost owner is used.
    /// </summary>
    public Guid? CostOwnerId { get; init; }

    /// <summary>
    /// Whether this record is intentionally unavailable for launch.
    /// </summary>
    public bool IsLaunchDisabled { get; init; }
}
