using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a Verein governance document with a selected revision.
/// </summary>
public record VereinDocumentDto
{
    /// <summary>
    /// Document primary key.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// Document kind.
    /// </summary>
    public EVereinDocumentKind Kind { get; init; }

    /// <summary>
    /// Whether the document is deleted from active workflows.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Selected or latest revision.
    /// </summary>
    public VereinDocumentRevisionDto? Revision { get; init; }

    /// <summary>
    /// All revisions when requested by administrative endpoints.
    /// </summary>
    public VereinDocumentRevisionDto[] Revisions { get; init; } = [];

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; init; }
}
