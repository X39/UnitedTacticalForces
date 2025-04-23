namespace X39.UnitedTacticalForces.Api.DTO.Updates;

public record EventUpdate
{

    /// <summary>
    /// Represents the title of the event.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Provides a detailed textual explanation or summary of the event.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// References the unique identifier of the associated terrain in the system.
    /// </summary>
    public long? TerrainFk { get; init; }


    /// <summary>
    /// Represents a reference to the associated mod pack revision.
    /// </summary>
    public long? ModPackRevisionFk { get; init; }


    /// <summary>
    /// Represents the binary image data associated with the event.
    /// </summary>
    public byte[]? Image { get; init; } = [];

    /// <summary>
    /// Specifies the MIME type of the associated image.
    /// </summary>
    public string? ImageMimeType { get; init; } = string.Empty;


    /// <summary>
    /// Indicates the date and time when the event is scheduled to occur.
    /// </summary>
    public DateTimeOffset? ScheduledFor { get; init; }


    /// <summary>
    /// Indicates whether the event is visible to users.
    /// </summary>
    public bool? IsVisible { get; init; }

    /// <summary>
    /// Specifies the minimum number of participants required to consider the event as viable.
    /// </summary>
    public int? MinimumAccepted { get; init; }

    /// <summary>
    /// Represents the foreign key identifying the user hosting the event.
    /// </summary>
    public Guid? HostedByFk { get; init; }
}
