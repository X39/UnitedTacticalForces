using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// Provider-neutral reference to allowlisted content prepared by a dedicated worker.
/// </summary>
public sealed record WorkerContentReference : IValidatableObject
{
    [Required]
    [MaxLength(64)]
    public string Source { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Identifier { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; init; } = string.Empty;

    [MaxLength(32)]
    public string UsageRole { get; init; } = "Shared";

    [MaxLength(128)]
    public string InstallerIdentifier { get; init; } = string.Empty;

    [MaxLength(128)]
    public string? VersionIdentifier { get; init; }

    [MaxLength(128)]
    public string? ArtifactIdentifier { get; init; }

    [MaxLength(128)]
    public string? ChecksumSha256 { get; init; }

    public string InstallerConfiguration { get; init; } = "{}";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            yield return new ValidationResult("Content source is required.", [nameof(Source)]);
        }
        if (string.IsNullOrWhiteSpace(Identifier))
        {
            yield return new ValidationResult("Content identifier is required.", [nameof(Identifier)]);
        }
        if (string.IsNullOrWhiteSpace(DisplayName)
            || !string.Equals(DisplayName, DisplayName.Trim(), StringComparison.Ordinal)
            || DisplayName is "." or ".."
            || DisplayName.IndexOfAny(['/', '\\']) >= 0
            || DisplayName.Any(char.IsControl))
        {
            yield return new ValidationResult(
                "Content display name contains unsafe characters.",
                [nameof(DisplayName)]);
        }
    }
}
