namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Generic creation metadata supplied by a registered game implementation.
/// </summary>
public sealed record GameServerCapabilityInfo
{
    public string GameIdentifier { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyCollection<string> RuntimeProviders { get; init; } = [];
    public IReadOnlyCollection<RuntimeProviderInfo> RuntimeProviderProfiles { get; init; } = [];
    public IReadOnlyCollection<GameServerActionInfo> Actions { get; init; } = [];
    public bool RequiresTerrain { get; init; }
    public string TerrainLabel { get; init; } = "Terrain";
    public bool RequiresScenario { get; init; }
    public string ScenarioLabel { get; init; } = "Scenario";
    public bool SupportsModPacks { get; init; }
    public IReadOnlyCollection<string> TerrainSourceKinds { get; init; } = [];
    public IReadOnlyCollection<string> ModInstallerIdentifiers { get; init; } = [];
}
