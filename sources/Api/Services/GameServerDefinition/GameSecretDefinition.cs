namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>
/// Declares one credential slot of a game: how it is obtained, who may see it and which values it accepts.
/// </summary>
/// <param name="Key">The stable key of the secret within the game.</param>
/// <param name="DisplayName">The human-readable name of the secret.</param>
/// <param name="RequiredForStart">Whether the server cannot start while the secret has no current value.</param>
/// <param name="Generated">Whether the platform generates the value instead of a member supplying it.</param>
/// <param name="Revealable">Whether an authorized member may reveal the current value.</param>
/// <param name="ExternalProvider">Whether an external provider issues the value, which a member then supplies.</param>
/// <param name="AcceptsChosenValue">
/// Whether a platform-generated credential also accepts a member-chosen value. It is ignored for credentials that are
/// not generated, which always accept a supplied value.
/// </param>
/// <param name="ValueFormat">The characters the game can use in the value; every supplied value is validated.</param>
public sealed record GameSecretDefinition(
    string Key,
    string DisplayName,
    bool RequiredForStart,
    bool Generated,
    bool Revealable,
    bool ExternalProvider = false,
    bool AcceptsChosenValue = false,
    EGameSecretValueFormat ValueFormat = EGameSecretValueFormat.SingleLine);
