using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.Api.Data.Meta;

namespace X39.UnitedTacticalForces.Api.Data.Core;

/// <summary>
/// Represents a terrain entity in the database.
/// </summary>
/// <remarks>
/// The Terrain class is used to define various terrain properties including its title,
/// image, MIME type of the image, and whether the terrain is active or not.
/// </remarks>
public class Terrain : IPrimaryKey<long>
{
    /// <inheritdoc />
    [Key]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the title of the terrain.
    /// </summary>
    /// <remarks>
    /// This property represents the name or title associated with a terrain object.
    /// It is used to identify and refer to a terrain instance.
    /// </remarks>
    [MaxLength(1024)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Represents the binary image data associated with the terrain.
    /// </summary>
    /// <remarks>
    /// This property contains the raw image data, typically used for rendering or displaying the terrain's visual representation.
    /// The MIME type of the image can be found in the <see cref="ImageMimeType"/> property.
    /// </remarks>
    public byte[] Image { get; set; } = [];

    /// <summary>
    /// Represents the MIME type of the image associated with a <see cref="Terrain"/> entity.
    /// </summary>
    /// <remarks>
    /// This property is used to store the content type of the image data provided in the <see cref="Terrain.Image"/> property.
    /// Common examples include "image/png" or "image/jpeg".
    /// </remarks>
    [MaxLength(64)]
    public string ImageMimeType { get; set; } = string.Empty;

    /// Indicates if the terrain is active or not.
    /// A value of `true` means the terrain is currently active and can be used or accessed.
    /// A value of `false` implies the terrain is inactive, typically representing scenarios
    /// such as being marked for deletion or temporarily disabled.
    public bool IsActive { get; set; }
}
