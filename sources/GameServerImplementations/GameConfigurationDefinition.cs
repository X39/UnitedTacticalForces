using System.Text.Json;

namespace X39.UnitedTacticalForces.GameServerImplementations;

/// <summary>Describes one game-owned configuration setting.</summary>
/// <param name="Key">The stable setting key.</param>
/// <param name="TargetPath">The generated target file.</param>
/// <param name="Setting">The setting name written to the target file.</param>
/// <param name="Group">The UI domain group.</param>
/// <param name="DisplayName">The human-readable label.</param>
/// <param name="Description">The human-readable explanation.</param>
/// <param name="Kind">The JSON value kind accepted by the setting.</param>
/// <param name="Required">Whether the setting must have a value.</param>
/// <param name="Sensitive">Whether the value must be redacted.</param>
/// <param name="Order">The display and render order.</param>
/// <param name="DefaultValue">The optional default value.</param>
/// <param name="Minimum">The optional numeric minimum.</param>
/// <param name="Maximum">The optional numeric maximum.</param>
/// <param name="AllowedValues">The compatible list of string values.</param>
/// <param name="Choices">The optional typed choices and their labels.</param>
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
    IReadOnlyList<string>? AllowedValues = null,
    IReadOnlyList<GameConfigurationChoice>? Choices = null);
