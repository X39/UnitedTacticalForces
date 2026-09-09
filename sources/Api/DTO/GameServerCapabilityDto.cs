namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Game implementation metadata used by the generic creation flow.
/// </summary>
public sealed record GameServerCapabilityDto
{
    public string GameIdentifier { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyCollection<string> RuntimeProviders { get; init; } = [];
    public IReadOnlyCollection<RuntimeProviderDto> RuntimeProviderProfiles { get; init; } = [];
    public IReadOnlyCollection<GameServerActionDto> Actions { get; init; } = [];
    public bool RequiresTerrain { get; init; }
    public string TerrainLabel { get; init; } = "Terrain";
    public bool RequiresScenario { get; init; }
    public string ScenarioLabel { get; init; } = "Scenario";
    public bool SupportsModPacks { get; init; }
    public bool RequiresModPack { get; init; }
    public IReadOnlyCollection<string> TerrainSourceKinds { get; init; } = [];
    public IReadOnlyCollection<string> ModInstallerIdentifiers { get; init; } = [];
    /// <summary>Gets compatible mod source providers and their installers.</summary>
    public IReadOnlyCollection<ModProviderBindingDto> ModProviderBindings { get; init; } = [];
    /// <summary>Gets game-owned bidirectional mod-pack formats.</summary>
    public IReadOnlyCollection<ModPackFormatDto> ModPackFormats { get; init; } = [];
    public string? DefaultTerrainSourceIdentifier { get; init; }
    public string? DefaultScenarioSourceIdentifier { get; init; }
    public IReadOnlyCollection<GameServerSecretCapabilityDto> Secrets { get; init; } = [];
}
