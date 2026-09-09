namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Represents one ordered mod membership.</summary>
public sealed record ModPackModMemberInfo
{
    /// <summary>Gets the mod identifier.</summary>
    public Guid ModFk { get; init; }
    /// <summary>Gets the optional pinned version identifier.</summary>
    public Guid? PinnedVersionFk { get; init; }
    /// <summary>Gets the membership order.</summary>
    public int Order { get; init; }
    /// <summary>Gets the mod title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets the source provider.</summary>
    public string ProviderIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the canonical source identifier.</summary>
    public string CanonicalSourceIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the usage role.</summary>
    public string UsageRole { get; init; } = string.Empty;
}
