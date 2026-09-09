namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Non-mutating result of parsing a game-owned mod-pack format.</summary>
/// <param name="Title">Imported pack title.</param>
/// <param name="Entries">Normalized source candidates.</param>
/// <param name="Warnings">Non-fatal import warnings.</param>
public sealed record ModPackFormatImportResult(
    string Title,
    IReadOnlyList<ModPackFormatImportEntry> Entries,
    IReadOnlyList<string> Warnings);
