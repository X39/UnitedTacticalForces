namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Describes a completed deployment with provider cost that may still be corrected.
/// </summary>
public sealed record HostingCorrectableDeploymentDto
{
    /// <summary>
    /// Gets the deployment identifier.
    /// </summary>
    public Guid DeploymentId { get; init; }

    /// <summary>
    /// Gets the server identifier.
    /// </summary>
    public Guid ServerId { get; init; }

    /// <summary>
    /// Gets the server title.
    /// </summary>
    public string ServerTitle { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider cost still attributed to the original owner.
    /// </summary>
    public decimal RemainingCorrectableAmount { get; init; }

    /// <summary>
    /// Gets the currency code.
    /// </summary>
    public string Currency { get; init; } = "EUR";

    /// <summary>
    /// Gets when the deployment was resolved.
    /// </summary>
    public Instant TimeStampResolved { get; init; }
}
