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
    string? DefaultScenarioSourceIdentifier = null);
