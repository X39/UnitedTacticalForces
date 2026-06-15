using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Authority;

/// <summary>
/// The individual claims that can be assigned to a role.
/// </summary>
public class Claim : IPrimaryKey<long>
{
    /// <summary>
    /// Database index enforcing one logical claim row where value and value type are both present.
    /// </summary>
    public const string UniqueClaimValueIndexName = "IX_Claims_Identifier_Value_ValueType";

    /// <summary>
    /// Database index enforcing one logical claim row where only value type is absent.
    /// </summary>
    public const string UniqueClaimNullValueTypeIndexName = "IX_Claims_Identifier_Value_NullValueType";

    /// <summary>
    /// Database index enforcing one logical claim row where only value is absent.
    /// </summary>
    public const string UniqueClaimNullValueIndexName = "IX_Claims_Identifier_ValueType_NullValue";

    /// <summary>
    /// Database index enforcing one logical claim row where value and value type are both absent.
    /// </summary>
    public const string UniqueClaimNullValueAndValueTypeIndexName = "IX_Claims_Identifier_NullValue_NullValueType";

    /// <summary>
    /// Database filter selecting claim rows where value and value type are both present.
    /// </summary>
    public const string UniqueClaimValueIndexFilter = "\"Value\" IS NOT NULL AND \"ValueType\" IS NOT NULL";

    /// <summary>
    /// Database filter selecting claim rows where only value type is absent.
    /// </summary>
    public const string UniqueClaimNullValueTypeIndexFilter = "\"Value\" IS NOT NULL AND \"ValueType\" IS NULL";

    /// <summary>
    /// Database filter selecting claim rows where only value is absent.
    /// </summary>
    public const string UniqueClaimNullValueIndexFilter = "\"Value\" IS NULL AND \"ValueType\" IS NOT NULL";

    /// <summary>
    /// Database filter selecting claim rows where value and value type are both absent.
    /// </summary>
    public const string UniqueClaimNullValueAndValueTypeIndexFilter = "\"Value\" IS NULL AND \"ValueType\" IS NULL";

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
