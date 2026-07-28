using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Represents the payload required for creating a mod pack.
/// </summary>
/// <remarks>
/// This payload combines the basic data of a mod pack definition and its initial revision.
/// It serves as the input model for creating a new mod pack within the system.
/// </remarks>
public record ModPackCreationPayload
{
    [Required]
    [MaxLength(64)]
    public string GameIdentifier { get; init; } = string.Empty;

    [Required]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Html { get; init; } = string.Empty;
}
