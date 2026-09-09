using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Creates a provider-backed mod and its first immutable version.</summary>
public sealed record CreateModPayload
{
    /// <summary>Gets the game identifier.</summary>
    public string GameIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the display title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets the provider identifier.</summary>
    public string ProviderIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the user-supplied source.</summary>
    public string Source { get; init; } = string.Empty;
    /// <summary>Gets where the mod is required.</summary>
    public EModUsageRole UsageRole { get; init; }
    /// <summary>Gets the initial immutable version.</summary>
    public CreateModVersionPayload Version { get; init; } = new();
}
