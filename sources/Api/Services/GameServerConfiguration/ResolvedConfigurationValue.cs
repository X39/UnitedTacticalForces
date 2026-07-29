using System.Text.Json;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;

public sealed record ResolvedConfigurationValue(
    GameConfigurationDefinition Definition,
    JsonElement? Value,
    string Source,
    bool IsCompatible,
    string? Warning);
