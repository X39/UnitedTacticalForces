using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Database.Media;

namespace X39.UnitedTacticalForces.Database.Core;

/// <summary>
/// Represents a terrain entity in the database.
/// </summary>
/// <remarks>
/// The Terrain class is used to define various terrain properties including its title,
/// gallery image asset, and whether the terrain is active or not.
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
    /// Gallery media asset used as terrain image.
    /// </summary>
    [ForeignKey(nameof(ImageAssetFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? ImageAsset { get; set; }

    /// <summary>
    /// Foreign key to <see cref="ImageAsset"/>.
    /// </summary>
    public Guid ImageAssetFk { get; set; }

    /// Indicates if the terrain is active or not.
    /// A value of `true` means the terrain is currently active and can be used or accessed.
    /// A value of `false` implies the terrain is inactive, typically representing scenarios
    /// such as being marked for deletion or temporarily disabled.
    public bool IsActive { get; set; }
}
