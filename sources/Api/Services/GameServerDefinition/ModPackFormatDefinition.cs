namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Describes a game-owned bidirectional mod-pack file format.</summary>
/// <param name="Identifier">Stable format identifier.</param>
/// <param name="DisplayName">User-facing name.</param>
/// <param name="FileExtension">File extension without a leading dot.</param>
/// <param name="MediaType">IANA media type.</param>
/// <param name="MaximumSizeBytes">Maximum accepted import size.</param>
public sealed record ModPackFormatDefinition(
    string Identifier,
    string DisplayName,
    string FileExtension,
    string MediaType,
    long MaximumSizeBytes);
