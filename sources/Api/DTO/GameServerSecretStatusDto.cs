using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The configuration state of one credential slot of a game server. It never contains the value.
/// </summary>
/// <param name="Key">The stable key of the secret within the game.</param>
/// <param name="DisplayName">The human-readable name of the secret.</param>
/// <param name="RequiredForStart">Whether the server cannot start while the secret has no current value.</param>
/// <param name="Generated">Whether the platform generates the value.</param>
/// <param name="Revealable">Whether an authorized member may reveal the current value.</param>
/// <param name="ExternalProvider">Whether an external provider issues the value.</param>
/// <param name="IsConfigured">Whether the secret has a current value.</param>
/// <param name="Version">The version number of the current value, if configured.</param>
/// <param name="Origin">The origin of the current value, if configured.</param>
/// <param name="TimeStampCreated">The instant the current value was created, if configured.</param>
/// <param name="AcceptsChosenValue">Whether a member may supply a deliberate value for this credential.</param>
/// <param name="ValueFormat">The characters the game can use in a supplied value.</param>
public sealed record GameServerSecretStatusDto(
    string Key,
    string DisplayName,
    bool RequiredForStart,
    bool Generated,
    bool Revealable,
    bool ExternalProvider,
    bool IsConfigured,
    int? Version,
    EGameServerSecretOrigin? Origin,
    Instant? TimeStampCreated,
    bool AcceptsChosenValue,
    EGameSecretValueFormat ValueFormat);
