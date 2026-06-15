using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Database.Media;

namespace X39.UnitedTacticalForces.Api.Services.Media;

/// <summary>
/// Stores, queries, tags, and serves reusable image assets for content workflows.
/// </summary>
public interface IMediaAssetService
{
    /// <summary>
    /// Root directory where opaque media asset files are stored.
    /// </summary>
    string StorageRootPath { get; }

    /// <summary>
    /// Returns assets matching metadata, tag, and usage filters.
    /// </summary>
    /// <param name="filter">Filter and paging parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching media assets.</returns>
    Task<MediaAssetDto[]> ListAsync(MediaAssetFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one media asset by id.
    /// </summary>
    /// <param name="assetId">Asset id.</param>
    /// <param name="preferredContentType">Preferred derived representation, or <see langword="null"/> for the source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The asset or <see langword="null"/>.</returns>
    Task<MediaAssetDto?> GetAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an image, deduplicating by content hash and adding supplied tags.
    /// </summary>
    /// <param name="file">Uploaded image file.</param>
    /// <param name="tags">Tags to add.</param>
    /// <param name="createdByFk">Optional uploading user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or reused media asset.</returns>
    Task<MediaAssetDto> UploadAsync(
        IFormFile file,
        IEnumerable<string> tags,
        Guid? createdByFk,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports image bytes into the reusable gallery from an internal stream.
    /// </summary>
    /// <param name="imageStream">Readable image byte stream.</param>
    /// <param name="originalFileName">Logical source file name.</param>
    /// <param name="contentType">Image MIME type.</param>
    /// <param name="tags">Tags to add.</param>
    /// <param name="createdByFk">Optional creating user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or reused media asset.</returns>
    Task<MediaAssetDto> ImportAsync(
        Stream imageStream,
        string originalFileName,
        string contentType,
        IEnumerable<string> tags,
        Guid? createdByFk,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the tag set for an existing asset.
    /// </summary>
    /// <param name="assetId">Asset id.</param>
    /// <param name="tags">Desired tag set.</param>
    /// <param name="createdByFk">Optional user id for newly added tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated asset or <see langword="null"/>.</returns>
    Task<MediaAssetDto?> ReplaceTagsAsync(
        Guid assetId,
        IEnumerable<string> tags,
        Guid? createdByFk,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a stored asset file for safe physical-file serving.
    /// </summary>
    /// <param name="assetId">Asset id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stored file information or <see langword="null"/>.</returns>
    Task<StoredMediaAssetFile?> GetStoredFileAsync(
        Guid assetId,
        string? preferredContentType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns deterministic known usage records for the supplied assets.
    /// </summary>
    /// <param name="assets">Assets to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Usage records keyed by asset id.</returns>
    Task<IReadOnlyDictionary<Guid, MediaAssetUsageDto[]>> GetUsageMapAsync(
        IEnumerable<MediaAsset> assets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the API-relative image reference persisted by content consumers.
    /// </summary>
    /// <param name="assetId">Asset id.</param>
    /// <returns>API-relative content route.</returns>
    static string CreateImagePath(Guid assetId)
        => $"{Constants.Routes.MediaAssets}/{assetId:D}/content";
}
