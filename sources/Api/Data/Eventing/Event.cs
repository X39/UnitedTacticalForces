using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;

namespace X39.UnitedTacticalForces.Api.Data.Eventing;

/// <summary>
/// Represents an event in the system with various associated metadata.
/// </summary>
/// <remarks>
/// This class models an event which includes information such as its title, description,
/// terrain information, associated mod pack revision, image details, scheduling details,
/// and other metadata related to visibility and participant statuses.
/// </remarks>
[Index(nameof(ScheduledFor))]
public class Event
{
    /// <summary>
    /// Represents the unique identifier for an event.
    /// </summary>
    /// <remarks>
    /// This property serves as the primary key for the Event entity.
    /// It is a globally unique identifier (GUID) that ensures each event is uniquely identified in the database.
    /// The <see cref="PrimaryKey"/> property is used in various operations, such as querying and updating events.
    /// </remarks>
    [Key] public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the title of the event.
    /// </summary>
    /// <remarks>
    /// This property represents the name or short description of the event.
    /// It is expected to be a brief and meaningful representation of the event's purpose or content.
    /// </remarks>
    [MaxLength(1024)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a brief textual description of the event.
    /// This property provides information about the event's purpose, details, or any additional notes.
    /// </summary>
    [SuppressMessage("ReSharper", "EntityFramework.ModelValidation.UnlimitedStringLength")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Represents a specific terrain.
    /// </summary>
    /// <remarks>
    /// The <see cref="Terrain"/> type is used within the application to denote
    /// terrains that may be associated with events or other domain-specific entities.
    /// </remarks>
    [ForeignKey(nameof(TerrainFk))] public Terrain? Terrain { get; set; }

    /// Represents the foreign key association between an event and a terrain.
    /// This property links an event to its corresponding terrain entity in
    /// the database. It is used as a reference to the `Terrain` associated
    /// with the event.
    /// This property is marked as a foreign key indicating a relationship
    /// with the `Terrain` class. The corresponding navigation property is
    /// `Terrain`.
    /// The value should contain the primary key of the related `Terrain`
    /// entity.
    public long TerrainFk { get; set; }


    /// <summary>
    /// Represents the associated ModPackRevision for an event.
    /// This property establishes a relationship between an event and a specific ModPackRevision,
    /// which includes details about the mod pack used or linked to the event.
    /// </summary>
    [ForeignKey(nameof(ModPackRevisionFk))] public ModPackRevision? ModPackRevision { get; set; }

    /// Represents the foreign key referencing a ModPackRevision entity.
    /// This property is used to establish the relationship between an Event
    /// and the corresponding ModPackRevision it is associated with.
    public long ModPackRevisionFk { get; set; }


    /// <summary>
    /// Represents the binary data of an event's associated image.
    /// </summary>
    /// <remarks>
    /// This property stores image data in a byte array format, which can be used to represent
    /// visual information or branding related to the event. The image data is typically processed
    /// or transmitted in conjunction with its MIME type, which specifies the format
    /// or encoding of the image (e.g., JPEG, PNG).
    /// </remarks>
    public byte[] Image { get; set; } = [];

    /// <summary>
    /// Represents the MIME type of the associated image for an event.
    /// </summary>
    /// <remarks>
    /// This property stores the media type (e.g., "image/png", "image/jpeg") of the image linked to an event.
    /// It is utilized to appropriately handle image rendering or processing based on the type of the file.
    /// </remarks>
    [MaxLength(64)]
    public string ImageMimeType { get; set; } = string.Empty;

    /// Represents the original scheduled date and time for an event.
    /// This property captures the event's planned schedule without modifications
    /// or adjustments. It is used to maintain the initial scheduling information.
    public DateTimeOffset ScheduledForOriginal { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the event is scheduled to occur.
    /// </summary>
    /// <remarks>
    /// This property represents the specific point in time for the event. The value includes both
    /// the date and time, along with the offset from Coordinated Universal Time (UTC).
    /// It is used to determine when an event is considered upcoming or past in the system.
    /// </remarks>
    public DateTimeOffset ScheduledFor { get; set; }

    /// <summary>
    /// Gets or sets the timestamp indicating when the event was created.
    /// </summary>
    /// <remarks>
    /// This property represents the point in time at which the event entity was initially created.
    /// It can be useful for auditing or historical tracking of event creation.
    /// </remarks>
    public DateTimeOffset TimeStampCreated { get; set; }

    /// <summary>
    /// Indicates whether the event is visible to all users.
    /// </summary>
    /// <remarks>
    /// If set to <c>true</c>, the event is publicly visible. When set to <c>false</c>,
    /// the event is hidden and only accessible to specific users, such as the host.
    /// This property is typically used to control event visibility while managing access
    /// for certain users.
    /// </remarks>
    public bool IsVisible { get; set; }

    /// Gets or sets the count of participants who have accepted to participate in the event.
    /// This property represents the total number of users who have confirmed their attendance.
    public int AcceptedCount { get; set; }

    /// Represents the count of users who have explicitly rejected participation in the event.
    /// This property tracks the number of participants who have chosen not to attend the event.
    /// It is primarily used to gauge the level of interest or disinterest in a particular event.
    public int RejectedCount { get; set; }

    /// Represents the count of users who have responded with a "Maybe" status to the event invitation.
    /// This property is used to track the number of tentative participants for the event.
    public int MaybeCount { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of participants required to accept the event for it to proceed.
    /// </summary>
    public int MinimumAccepted { get; set; }

    /// <summary>
    /// The user who owns or created the event. This property represents the relationship between
    /// the event and its associated owner, linking to the <see cref="User"/> entity.
    /// </summary>
    [ForeignKey(nameof(OwnerFk))]
    public User? Owner { get; set; }

    /// <summary>
    /// Represents the foreign key identifier associated with the owner of the event.
    /// </summary>
    /// <remarks>
    /// This property links the event to its respective owner using a foreign key relationship.
    /// The associated owner is typically represented by the <see cref="User"/> entity.
    /// </remarks>
    public Guid OwnerFk { get; set; }

    /// <summary>
    /// Gets or sets the user hosting the event.
    /// </summary>
    /// <remarks>
    /// This property references the <see cref="User"/> entity associated with the event.
    /// The relationship is established via the foreign key <see cref="HostedByFk"/>.
    /// </remarks>
    [ForeignKey(nameof(HostedByFk))]
    public User? HostedBy { get; set; }

    /// <summary>
    /// Gets or sets the foreign key referencing the host user of the event.
    /// </summary>
    /// <remarks>
    /// This property represents the unique identifier for the <see cref="User"/> entity
    /// associated with the hosting of the event. It functions as a foreign key for
    /// the relationship between the <see cref="Event"/> and the <see cref="User"/> entities.
    /// </remarks>
    public Guid HostedByFk { get; set; }

    /// <summary>
    /// Represents a collection of user-specific metadata associated with the event.
    /// </summary>
    /// <remarks>
    /// This property establishes a relationship between the event and associated
    /// <see cref="UserEventMeta"/> entries. It is used to store and manage metadata
    /// specific to a user's interaction with an event, such as their participation
    /// status, preferences, or any other user-specific information related to the event.
    /// </remarks>
    public ICollection<UserEventMeta>? UserMetas { get; set; }
}
