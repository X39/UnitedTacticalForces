using JetBrains.Annotations;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

/// <summary>
/// Different value-kinds for <see cref="ConfigurationEntryDefinition"/>'s.
/// </summary>
[PublicAPI]
public enum EConfigurationEntryKind
{
    /// <summary>
    /// Plain-Text.
    /// </summary>
    Text,

    /// <summary>
    /// Password input. UIs should prevent showing the actual value to users for privacy reasons.
    /// </summary>
    /// <remarks>
    /// Equivalent to <see cref="Text"/> in every other relation.
    /// </remarks>
    Password,

    /// <summary>
    /// Boolean value. UIs should provide "true" for <see langword="true"/> and "false" for <see langword="false"/>.
    /// </summary>
    Boolean,

    /// <summary>
    /// Number value. Value is expected to be in the format <code>[0-9]+(?:\.[0-9]+)?</code>.
    /// </summary>
    Number,
}