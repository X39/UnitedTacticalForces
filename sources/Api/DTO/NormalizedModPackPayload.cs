using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>Creates a normalized mod pack and its first immutable revision.</summary>
public sealed record NormalizedModPackPayload
{
    /// <summary>Gets the owning game identifier.</summary>
    [Required, MaxLength(64)] public string GameIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the mod-pack title.</summary>
    [Required, MaxLength(1024)] public string Title { get; init; } = string.Empty;
    /// <summary>Gets direct and nested revision members.</summary>
    [Required] public NormalizedModPackRevisionPayload Revision { get; init; } = new();
}
