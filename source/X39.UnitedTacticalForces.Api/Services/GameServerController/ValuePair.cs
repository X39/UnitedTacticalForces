namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

/// <summary>
/// Values available for <see cref="ConfigurationEntryDefinition"/>'s of the type <see cref="EConfigurationEntryKind.Selection"/>.
/// </summary>
/// <param name="DisplayName">The name to display the user.</param>
/// <param name="Value">The actual value to use.</param>
/// <param name="DisplayDescription">The description of the value, if applicable.</param>
public record ValuePair(string DisplayName, string Value, string? DisplayDescription = default);