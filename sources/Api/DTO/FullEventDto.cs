namespace X39.UnitedTacticalForces.Api.DTO;

public record FullEventDto
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

    #region Terrain

    /// <summary>
    /// Represents the primary key of an entity within the context of the application's data model.
    /// This property is used as a unique identifier for the associated record in the database.
    /// </summary>
    public long TerrainPrimaryKey { get; init; }

    /// <summary>
    /// Gets or sets the title of the terrain.
    /// </summary>
    /// <remarks>
    /// This property represents the name or title associated with a terrain object.
    /// It is used to identify and refer to a terrain instance.
    /// </remarks>
    public string TerrainTitle { get; init; } = string.Empty;

    /// <summary>
    /// Represents the binary image data associated with the terrain.
    /// </summary>
    /// <remarks>
    /// This property contains the raw image data, typically used for rendering or displaying the terrain's visual representation.
    /// The MIME type of the image can be found in the <see cref="ImageMimeType"/> property.
    /// </remarks>
    public byte[] TerrainImage { get; init; } = [];

    /// <summary>
    /// Represents the MIME type of the image associated with a Terrain entity.
    /// </summary>
    /// <remarks>
    /// This property is used to store the content type of the image data provided in the <see cref="TerrainImage"/> property.
    /// Common examples include "image/png" or "image/jpeg".
    /// </remarks>
    public string TerrainImageMimeType { get; init; } = string.Empty;

    /// Indicates if the terrain is active or not.
    /// A value of `true` means the terrain is currently active and can be used or accessed.
    /// A value of `false` implies the terrain is inactive, typically representing scenarios
    /// such as being marked for deletion or temporarily disabled.
    public bool TerrainIsActive { get; init; }

    #endregion


    #region ModPackRevision

    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    public long ModPackRevisionPrimaryKey { get; init; }

    /// <summary>
    /// The timestamp when this revision was created.
    /// </summary>
    public DateTimeOffset ModPackRevisionTimeStampCreated { get; init; }

    /// <summary>
    /// The HTML content of this revision.
    /// </summary>
    public string ModPackRevisionHtml { get; init; } = string.Empty;

    /// <summary>
    /// The user who updated this revision.
    /// </summary>
    public PlainUserDto? ModPackRevisionUpdatedBy { get; init; }

    /// <summary>
    /// Whether this revision is active or not.
    /// </summary>
    public bool ModPackRevisionIsActive { get; init; }

    #endregion

    #region UserModPackMeta

    /// <summary>
    /// Meta data for users.
    /// </summary>
    public DateTimeOffset? ModPackRevisionTimeStampDownloaded { get; init; }

    #endregion

    #region ModPackDefinition

    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    public long ModPackDefinitionPrimaryKey { get; init; }

    /// <summary>
    /// The timestamp when this modpack was created.
    /// </summary>
    public DateTimeOffset ModPackDefinitionTimeStampCreated { get; init; }

    /// <summary>
    /// The name of this modpack.
    /// </summary>
    public string ModPackDefinitionTitle { get; init; } = string.Empty;

    /// <summary>
    /// The owner of this modpack.
    /// </summary>
    public PlainUserDto? ModPackDefinitionOwner { get; init; }

    /// <summary>
    /// Whether this modpack is active or not.
    /// </summary>
    public bool ModPackDefinitionIsActive { get; init; }

    /// <summary>
    /// Whether this modpack is a composition of multiple modpack revisions
    /// or not.
    /// </summary>
    public bool ModPackDefinitionIsComposition { get; init; }

    #endregion

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
    public PlainUserDto Owner { get; init; } = new();

    /// <summary>
    /// Represents the foreign key identifying the user hosting the event.
    /// </summary>
    public PlainUserDto HostedBy { get; init; } = new();
}
