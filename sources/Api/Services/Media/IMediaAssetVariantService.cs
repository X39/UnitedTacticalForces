namespace X39.UnitedTacticalForces.Api.Services.Media;

/// <summary>
/// Creates and caches safe web-format variants of stored media assets.
/// </summary>
public interface IMediaAssetVariantService
{
    /// <summary>
    /// Gets an existing variant or converts the source image to the requested content type.
    /// </summary>
    Task<StoredMediaAssetFile> GetOrCreateAsync(
        StoredMediaAssetFile source,
        string targetContentType,
        CancellationToken cancellationToken = default);
}
