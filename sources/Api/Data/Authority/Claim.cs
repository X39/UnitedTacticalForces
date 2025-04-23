using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Meta;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

/// <summary>
/// The individual claims that can be assigned to a role.
/// </summary>
[Index(nameof(Identifier), nameof(Value), nameof(ValueType), IsUnique = true)]
public class Claim : IPrimaryKey<long>
{
    /// <inheritdoc />
    [Key]
    public long PrimaryKey { get; set; }
    
    /// <summary>
    /// The category this claim belongs to.
    /// </summary>
    [MaxLength(1024)]
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// The Human-Readable title of this claim.
    /// </summary>
    [MaxLength(1024)]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// The identifier of this claim as used in the jwt.
    /// </summary>
    [MaxLength(1024)]
    public string Identifier { get; set; } = string.Empty;
    
    /// <summary>
    /// The description of this claim.
    /// </summary>
    [MaxLength(1024)]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this claim is a prefix claim (and thus should not be used directly but rather implicitly).
    /// </summary>
    /// <remarks>
    /// A prefix claim is a claim that allows to configure resource based claims.
    /// This allows to create claims in the format "claim:id" to eg. allow admin access only to server 1.
    /// </remarks>
    public bool IsPrefix { get; set; }
    
    /// <summary>
    /// The value of this claim.
    /// </summary>
    [MaxLength(1024)]
    public string? Value { get; set; }
    
    /// <summary>
    /// The type of the value of this claim.
    /// </summary>
    [MaxLength(1024)]
    public string? ValueType { get; set; }

    /// <summary>
    /// The roles which have this claim.
    /// </summary>
    [InverseProperty(nameof(Role.Claims))]
    public ICollection<Role>? Roles { get; set; }

    /// <summary>
    /// The users that have this claim.
    /// </summary>
    [InverseProperty(nameof(User.Claims))]
    public ICollection<User>? Users { get; set; }
}
