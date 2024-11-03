using JetBrains.Annotations;
using X39.UnitedTacticalForces.Api.Data.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

/// <summary>
/// Offers definitions for <see cref="ConfigurationEntry"/>'s to user-interfaces.
/// </summary>
/// <param name="Required">Whether this is a required entry.</param>
/// <param name="Realm">The realm (eg. File) this entry lives in. A UI should group corresponding realms together.</param>
/// <param name="Kind">The data-type this expects. Hints to the UI what is supposed to be inserted here.</param>
/// <param name="Path">The actual path. A UI should not display this as primary means of identification.</param>
/// <param name="Regex">An optional RegEx to validate inputs.</param>
/// <param name="DisplayGroup">A human-readable group for grouping different realm-based paths together. This should be used as a sub-group in UIs.</param>
/// <param name="DisplayName">A human-readable name for this entry.</param>
/// <param name="DisplayDescription">A human-readable description for this entry. UIs should prefer tooltips over displaying the description directly here.</param>
/// <param name="MinValue">The minimum value for this entry. Only relevant for <see cref="Kind"/>'s of the type <see cref="EConfigurationEntryKind.Number"/>.</param>
/// <param name="MaxValue">The maximum value for this entry. Only relevant for <see cref="Kind"/>'s of the type <see cref="EConfigurationEntryKind.Number"/>.</param>
/// <param name="DefaultValue">The default value set.</param>
/// <param name="AllowedValues">The selection of values to choose from. Only relevant for <see cref="Kind"/>'s of the type <see cref="EConfigurationEntryKind.Selection"/>.</param>
[PublicAPI]
public record ConfigurationEntryDefinition(
    bool Required,
    string Realm,
    EConfigurationEntryKind Kind,
    string Path,
    string? Regex,
    string DisplayGroup,
    string DisplayName,
    string DisplayDescription,
    double MinValue = double.MinValue,
    double MaxValue = double.MaxValue,
    string? DefaultValue = default,
    ValuePair[]? AllowedValues = default)
{
    /// <summary>
    /// Combined values of <see cref="Realm"/> and <see cref="Path"/>.
    /// </summary>
    public string Identifier => string.Concat(Realm, "://", Path);
}