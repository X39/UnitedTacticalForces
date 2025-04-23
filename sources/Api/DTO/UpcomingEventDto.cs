using X39.UnitedTacticalForces.Contract.Event;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents details about an upcoming event in the system.
/// </summary>
public record UpcomingEventDto
{
    /// <summary>
    /// Uniquely identifies the upcoming event.
    /// </summary>
    public Guid PrimaryKey { get; init; }

    /// <summary>
    /// Represents the title of the upcoming event.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Provides a detailed explanation or description of the event.
    /// </summary>
    /// <remarks>
    /// Description will contain Markdown content.
    /// </remarks>
    public string Description { get; init; } = string.Empty;

    #region Terrain

    /// <summary>
    /// Indicates the title of the terrain associated with the event.
    /// </summary>
    public string TerrainTitle { get; init; } = string.Empty;

    #endregion

    #region ModPackRevision

    /// <summary>
    /// Meta data for users.
    /// </summary>
    public DateTimeOffset? ModPackRevisionTimeStampDownloaded { get; init; }

    #endregion

    #region ModPackDefinition

    /// <summary>
    /// The name of this modpack.
    /// </summary>
    public string ModPackDefinitionTitle { get; init; } = string.Empty;


    /// <summary>
    /// Whether this modpack is a composition of multiple modpack revisions
    /// or not.
    /// </summary>
    public bool ModPackDefinitionIsComposition { get; init; }

    #endregion


    /// <summary>
    /// Represents the image associated with the event.
    /// </summary>
    public byte[] Image { get; init; } = [];

    /// <summary>
    /// Specifies the MIME type of the associated image.
    /// </summary>
    public string ImageMimeType { get; init; } = string.Empty;

    /// <summary>
    /// The original scheduled date and time for the event.
    /// </summary>
    public DateTimeOffset ScheduledForOriginal { get; init; }

    /// <summary>
    /// Specifies the scheduled date and time when the event is set to occur.
    /// </summary>
    public DateTimeOffset ScheduledFor { get; init; }

    /// <summary>
    /// Represents the timestamp when the event was created.
    /// </summary>
    public DateTimeOffset TimeStampCreated { get; init; }

    /// <summary>
    /// The total count of participants who have accepted the invitation to the event.
    /// </summary>
    public int AcceptedCount { get; init; }

    /// <summary>
    /// The count of participants who have rejected the event.
    /// </summary>
    public int RejectedCount { get; init; }

    /// <summary>
    /// Represents the count of participants who have expressed uncertainty or a "maybe" response regarding their attendance.
    /// </summary>
    public int MaybeCount { get; init; }

    /// <summary>
    /// Specifies the minimum number of participants that must accept the event.
    /// </summary>
    public int MinimumAccepted { get; init; }

    /// <summary>
    /// Specifies the user hosting the event.
    /// </summary>
    public PlainUserDto HostedBy { get; init; } = new();

    /// <summary>
    /// Represents the calculated or system-determined level of acceptance for an event,
    /// based on user responses such as accepted, maybe, or rejected.
    /// </summary>
    public EEventAcceptance? MetaAcceptance { get; init; }
}
