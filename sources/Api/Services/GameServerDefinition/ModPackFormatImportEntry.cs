namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>One normalized source entry discovered by a custom mod-pack format.</summary>
/// <param name="Title">Title supplied by the format.</param>
/// <param name="SourceInput">Provider source input.</param>
public sealed record ModPackFormatImportEntry(string Title, string SourceInput);
