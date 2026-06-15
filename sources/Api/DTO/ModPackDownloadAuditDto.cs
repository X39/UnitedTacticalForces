namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents one recorded user download of a mod pack revision.
/// </summary>
public record ModPackDownloadAuditDto
{
    /// <summary>
    /// The user who downloaded the mod pack revision.
    /// </summary>
    public PlainUserDto User { get; init; } = new();

    /// <summary>
    /// The mod pack definition that was downloaded.
    /// </summary>
    public long ModPackDefinitionId { get; init; }

    /// <summary>
    /// The concrete revision that was downloaded.
    /// </summary>
    public long ModPackRevisionId { get; init; }

    /// <summary>
    /// The owner definition of the downloaded revision.
    /// </summary>
    public long ModPackRevisionDefinitionId { get; init; }

    /// <summary>
    /// The title of the owner definition of the downloaded revision.
    /// </summary>
    public string ModPackRevisionDefinitionTitle { get; init; } = string.Empty;

    /// <summary>
    /// The timestamp when the downloaded revision was created.
    /// </summary>
    public Instant ModPackRevisionTimeStampCreated { get; init; }

    /// <summary>
    /// The most recent timestamp when this user downloaded this revision through this mod pack definition.
    /// </summary>
    public Instant TimeStampDownloaded { get; init; }
}
