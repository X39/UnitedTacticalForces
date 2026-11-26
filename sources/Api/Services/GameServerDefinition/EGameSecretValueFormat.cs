namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>
/// Describes which characters a game can use in the value of a credential. The API rejects values that violate the
/// declared format.
/// </summary>
public enum EGameSecretValueFormat
{
    /// <summary>
    /// Any characters except control characters, so the value fits on a single configuration line. This is the
    /// default of every credential.
    /// </summary>
    SingleLine,

    /// <summary>
    /// Printable ASCII characters from 0x21 to 0x7E without the double quote and without the backslash. The value is
    /// safe for space-delimited and quoted configuration formats.
    /// </summary>
    Token,
}
