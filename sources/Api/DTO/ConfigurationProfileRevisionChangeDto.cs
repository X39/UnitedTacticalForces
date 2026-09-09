namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Describes one setting changed by an immutable profile revision.
/// </summary>
public sealed record ConfigurationProfileRevisionChangeDto
{
    /// <summary>Gets the stable configuration-definition key.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Gets the kind of change.</summary>
    public EConfigurationProfileRevisionChangeKind Kind { get; init; }
}
