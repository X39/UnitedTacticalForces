namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Stable client-facing status codes for runtime profile catalog availability.
/// </summary>
public static class RuntimeProviderAvailabilityStatuses
{
    /// <summary>The provider catalog is available and exposes at least one selectable profile.</summary>
    public const string Available = "available";

    /// <summary>The provider must be configured before its profile catalog can be queried.</summary>
    public const string ConfigurationRequired = "configuration-required";

    /// <summary>The provider catalog could not be queried at this time.</summary>
    public const string CatalogUnavailable = "catalog-unavailable";

    /// <summary>The provider catalog query succeeded but returned no selectable profiles.</summary>
    public const string NoProfiles = "no-profiles";
}
