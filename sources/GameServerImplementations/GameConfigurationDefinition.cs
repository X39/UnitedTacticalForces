using System.Text.Json;

namespace X39.UnitedTacticalForces.GameServerImplementations;

public sealed record GameConfigurationDefinition(
    string Key,
    string TargetPath,
    string Setting,
    string Group,
    string DisplayName,
    string Description,
    EGameConfigurationValueKind Kind,
    bool Required,
    bool Sensitive,
    int Order,
    JsonElement? DefaultValue = null,
    decimal? Minimum = null,
    decimal? Maximum = null,
    IReadOnlyList<string>? AllowedValues = null);
