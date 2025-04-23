namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents detailed information about an event in the system.
/// </summary>
public record PlainEventDto
{
    /// <summary>
    /// Represents the unique identifier for an event.
    /// </summary>
    public Guid PrimaryKey { get; init; }

    /// <summary>
    /// Represents the title of the event.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Provides a detailed textual explanation or summary of the event.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// References the unique identifier of the associated terrain in the system.
    /// </summary>
    public long TerrainFk { get; init; }


    /// <summary>
    /// Represents a reference to the associated mod pack revision.
    /// </summary>
    public long ModPackRevisionFk { get; init; }


    /// <summary>
    /// Represents the binary image data associated with the event.
    /// </summary>
    public byte[] Image { get; init; } = [];

    /// <summary>
    /// Specifies the MIME type of the associated image.
    /// </summary>
    public string ImageMimeType { get; init; } = string.Empty;

    /// <summary>
    /// Represents the original scheduled date and time for the event before any modifications.
    /// </summary>
    public DateTimeOffset ScheduledForOriginal { get; init; }

    /// <summary>
    /// Indicates the date and time when the event is scheduled to occur.
    /// </summary>
    public DateTimeOffset ScheduledFor { get; init; }

    /// <summary>
    /// Indicates the date and time when the event was created.
    /// </summary>
    public DateTimeOffset TimeStampCreated { get; init; }

    /// <summary>
    /// Indicates whether the event is visible to users.
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// Represents the number of participants who have accepted the invitation to the event.
    /// </summary>
    public int AcceptedCount { get; init; }

    /// <summary>
    /// Represents the number of users who have explicitly declined participation in the event.
    /// </summary>
    public int RejectedCount { get; init; }

    /// <summary>
    /// Represents the number of participants who have indicated a "maybe" response for attendance at the event.
    /// </summary>
    public int MaybeCount { get; init; }

    /// <summary>
    /// Specifies the minimum number of participants required to consider the event as viable.
    /// </summary>
    public int MinimumAccepted { get; init; }

    /// <summary>
    /// Represents the foreign key linking the event to its owner.
    /// </summary>
    public Guid OwnerFk { get; init; }

    /// <summary>
    /// Represents the foreign key identifying the user hosting the event.
    /// </summary>
    public Guid HostedByFk { get; init; }
}
