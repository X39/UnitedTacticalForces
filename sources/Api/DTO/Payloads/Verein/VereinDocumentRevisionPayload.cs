namespace X39.UnitedTacticalForces.Api.DTO.Payloads.Verein;

/// <summary>
/// Payload for creating a Verein document revision.
/// </summary>
public record VereinDocumentRevisionPayload
{
    /// <summary>
    /// Display title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Short summary.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Markdown body.
    /// </summary>
    public string Markdown { get; init; } = string.Empty;

    /// <summary>
    /// Revision comment.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Whether the revision should be published immediately.
    /// </summary>
    public bool IsPublished { get; init; }
}
