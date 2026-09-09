namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Imports and exports one game-owned mod-pack file format.</summary>
public interface IModPackFormatHandler
{
    /// <summary>Gets the owning game identifier.</summary>
    string GameIdentifier { get; }
    /// <summary>Gets public format metadata.</summary>
    ModPackFormatDefinition Definition { get; }
    /// <summary>Parses an import without mutating application state.</summary>
    /// <param name="content">Uploaded format content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed candidate entries and warnings.</returns>
    Task<ModPackFormatImportResult> ImportAsync(Stream content, CancellationToken cancellationToken = default);
    /// <summary>Serializes normalized effective membership.</summary>
    /// <param name="title">Mod-pack title.</param>
    /// <param name="entries">Effective normalized entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Serialized bytes and warnings.</returns>
    Task<ModPackFormatExportResult> ExportAsync(
        string title,
        IReadOnlyCollection<ModPackFormatExportEntry> entries,
        CancellationToken cancellationToken = default);
}
