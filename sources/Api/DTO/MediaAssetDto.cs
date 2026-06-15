namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Represents a reusable image asset in the command-center gallery.
/// </summary>
public record MediaAssetDto
{
    /// <summary>
    /// Asset primary key.
    /// </summary>
    public Guid PrimaryKey { get; init; }

    /// <summary>
    /// Original uploaded filename.
    /// </summary>
    public string OriginalFileName { get; init; } = string.Empty;

    /// <summary>
    /// Normalized image MIME type.
    /// </summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    /// Stored file size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// API-relative content path safe to store as an image reference.
    /// </summary>
    public string ImagePath { get; init; } = string.Empty;

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public Instant TimeStampCreated { get; init; }

    /// <summary>
    /// Last metadata update timestamp.
    /// </summary>
    public Instant TimeStampUpdated { get; init; }

    /// <summary>
    /// Display name of the uploading user, when available.
    /// </summary>
    public string? CreatedByName { get; init; }

    /// <summary>
    /// Normalized tags assigned to the asset.
    /// </summary>
    public string[] Tags { get; init; } = [];

    /// <summary>
    /// Deterministic references to known content records using this image.
    /// </summary>
    public MediaAssetUsageDto[] Usages { get; init; } = [];
}
