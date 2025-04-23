using X39.UnitedTacticalForces.Contract.GameServer;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a plain data transfer object (DTO) for a game server with relevant
/// information such as metadata, status, and versioning details.
/// </summary>
public class PlainGameServerDto
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// The human-readable title of this entity.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>f
    /// The timestamp this entity got created.
    /// </summary>
    public DateTimeOffset TimeStampCreated { get; init; }

    /// <summary>
    /// The timestamp this entity last was upgraded (that is: The application itself got updated).
    /// </summary>
    public DateTimeOffset TimeStampUpgraded { get; init; }

    /// <summary>
    /// Foreign key of ActiveModPack.
    /// </summary>
    public long? ActiveModPackFk { get; init; }

    /// <summary>
    /// Foreign key of SelectedModPack.
    /// </summary>
    public long? SelectedModPackFk { get; init; }

    /// <summary>
    /// The lifetime state of this entity.
    /// </summary>
    public ELifetimeStatus Status { get; init; }


    /// <summary>
    /// Serialization property for <see cref="Version"/>.
    /// </summary>
    public string VersionString { get; init; } = string.Empty;

    /// <summary>
    /// The game server this represents.
    /// </summary>
    public string ControllerIdentifier { get; init; } = string.Empty;

    /// <summary>
    /// Whether this game server is active or not.
    /// </summary>
    /// <remarks>
    /// Inactive game servers are considered deleted.
    /// </remarks>
    public bool IsActive { get; init; }
}
