namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Creates a normalized mod pack and its first immutable revision.</summary>
public sealed record NormalizedModPackPayload
{
    /// <summary>Gets the owning game identifier.</summary>
    public string GameIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets the initial revision.</summary>
    public NormalizedModPackRevisionPayload Revision { get; init; } = new();
}
