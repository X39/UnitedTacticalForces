namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a Data Transfer Object (DTO) for a specific claim associated with users or roles.
/// </summary>
/// <remarks>
/// This record is used to encapsulate detailed information about claims, including metadata such as
/// category, title, identifier, and description. It also supports association with users and roles
/// for establishing permissions or authentication rules. Claims can include optional values and types,
/// as well as indicate whether they are prefix-based.
/// </remarks>
public record PlainClaimDto
{
    /// <summary>
    /// Represents the unique identifier for an entity.
    /// This property is used to distinguish individual entities across different contexts
    /// or within a specific collection, enabling accurate referencing and management.
    /// </summary>
    public long PrimaryKey { get; set; }
    
    /// <summary>
    /// The category this claim belongs to.
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// The Human-Readable title of this claim.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// The identifier of this claim as used in the jwt.
    /// </summary>
    public string Identifier { get; set; } = string.Empty;
    
    /// <summary>
    /// The description of this claim.
    /// </summary>
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
    public string? Value { get; set; }
    
    /// <summary>
    /// The type of the value of this claim.
    /// </summary>
    public string? ValueType { get; set; }
}