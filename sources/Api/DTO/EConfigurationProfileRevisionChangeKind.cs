namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Identifies how a configuration value changed in an immutable profile revision.
/// </summary>
public enum EConfigurationProfileRevisionChangeKind
{
    /// <summary>A value was introduced.</summary>
    Added,
    /// <summary>An existing value was replaced.</summary>
    Changed,
    /// <summary>An existing value was removed.</summary>
    Removed,
}
