namespace X39.UnitedTacticalForces.WebApp.Services.MediaAssetRepository;

public interface IMediaAssetRepository
{
    Task<IReadOnlyList<MediaAssetInfo>> GetAssetsAsync(
        MediaAssetFilterInfo? filter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaAssetInfo>> GetAssetPageAsync(
        MediaAssetFilterInfo? filter = null,
        CancellationToken cancellationToken = default);

    Task<MediaAssetInfo?> GetAssetAsync(
        Guid assetId,
        CancellationToken cancellationToken = default);

    Task<MediaAssetInfo> UploadAssetAsync(
        Stream fileStream,
        string fileName,
        string fileMimeType,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default);

    Task<MediaAssetInfo> ReplaceTagsAsync(
        Guid assetId,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default);
}
