using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using X39.UnitedTacticalForces.Api.Data.Meta;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

/// <summary>
/// A collection of <see cref="Claim"/>s that can be assigned to a <see cref="User"/>.
/// </summary>
public class Role : IPrimaryKey<long>
{
    /// <inheritdoc />
    [Key]
    public long PrimaryKey { get; set; }
    
    /// <summary>
    /// The Human-Readable title of this claim.
    /// </summary>
    [MaxLength(1024)]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// The description of this Role.
    /// </summary>
    [MaxLength(1024)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The users that have this role.
    /// </summary>
    [InverseProperty(nameof(User.Roles))]
    public ICollection<User>? Users { get; set; }

    /// <summary>
    /// The Claims this role has.
    /// </summary>
    [InverseProperty(nameof(Claim.Roles))]
    public ICollection<Claim>? Claims { get; set; }
}
