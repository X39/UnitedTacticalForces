namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Serialized custom mod-pack format and non-fatal warnings.</summary>
/// <param name="Content">Serialized file bytes.</param>
/// <param name="Warnings">Unsupported-entry warnings.</param>
public sealed record ModPackFormatExportResult(byte[] Content, IReadOnlyList<string> Warnings);
