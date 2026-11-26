namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Fixed English reasons why a mod pack cannot be selected or resolved. They are shared by the pack options, the
/// pack contents projection and the pack change validation so that all three explain an unavailable pack alike.
/// </summary>
internal static class ModPackAvailabilityReasons
{
    /// <summary>The mod pack definition has been deactivated.</summary>
    public const string Inactive = "This mod pack is no longer active.";

    /// <summary>The mod pack is active but has no active revision to resolve.</summary>
    public const string NoActiveRevision = "This mod pack has no active revision.";

    /// <summary>The mod pack contains content that cannot be resolved.</summary>
    public const string Unresolvable = "This mod pack contains content that cannot be resolved.";
}
