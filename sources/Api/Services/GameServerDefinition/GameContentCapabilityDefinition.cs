namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public sealed record GameContentCapabilityDefinition(
    bool RequiresTerrain,
    string TerrainLabel,
    bool RequiresScenario,
    string ScenarioLabel,
    bool SupportsModPacks,
    bool RequiresModPack,
    IReadOnlyList<string> TerrainSourceKinds,
    IReadOnlyList<string> ModInstallerIdentifiers,
    string? DefaultTerrainSourceIdentifier = null,
    string? DefaultScenarioSourceIdentifier = null)
{
    /// <summary>Gets game/provider installation bindings used by generic mod editors.</summary>
    public IReadOnlyList<ModProviderBindingDefinition> ModProviderBindings { get; init; } = [];

    /// <summary>Gets game-owned import/export formats.</summary>
    public IReadOnlyList<ModPackFormatDefinition> ModPackFormats { get; init; } = [];
}
