using System.Text.Json;

namespace X39.UnitedTacticalForces.GameServerImplementations;

/// <summary>
/// Describes one typed, user-facing choice for a game configuration value.
/// </summary>
/// <param name="Value">The typed JSON value persisted for the choice.</param>
/// <param name="Label">The concise user-facing label.</param>
/// <param name="Description">The optional explanation of the choice.</param>
public sealed record GameConfigurationChoice(JsonElement Value, string Label, string? Description = null);
