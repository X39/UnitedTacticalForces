using JetBrains.Annotations;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

/// <summary>
/// Different value-kinds for <see cref="ConfigurationEntryDefinition"/>'s.
/// </summary>
[PublicAPI]
public enum EConfigurationEntryKind
{
    /// <summary>
    /// Raw content.
    /// </summary>
    Raw,
    
    /// <summary>
    /// String that will be stringified.
    /// </summary>
    String,

    /// <summary>
    /// Password input. UIs should prevent showing the actual value to users for privacy reasons.
    /// </summary>
    /// <remarks>
    /// Equivalent to <see cref="String"/> in every other relation.
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
    
    /// <summary>
    /// Any value. Allowed values will be provided by the configuration entry.
    /// </summary>
    Selection,
}