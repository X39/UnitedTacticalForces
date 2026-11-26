namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// A plaintext game server secret value returned to an authorized caller, either by an explicit reveal or once after a
/// rotation generated it.
/// </summary>
/// <param name="Key">The secret key as declared by the game definition.</param>
/// <param name="Value">The plaintext secret value. It is never logged or audited.</param>
/// <param name="Version">The version number of the returned value.</param>
public sealed record GameServerSecretValueDto(string Key, string Value, int Version);
