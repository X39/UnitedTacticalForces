namespace X39.UnitedTacticalForces.WebApp.Services.PublicPageRepository;

public interface IPublicPageRepository
{
    Task<PublicPageInfo?> GetPublishedPageAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicPageInfo>> GetAdminPagesAsync(CancellationToken cancellationToken = default);

    Task<PublicPageInfo?> GetAdminPageAsync(string slug, CancellationToken cancellationToken = default);

    Task<PublicPageInfo> CreateRevisionAsync(
        string slug,
        PublicPageRevisionUpsertInfo payload,
        CancellationToken cancellationToken = default);

    Task<PublicPageInfo> PublishRevisionAsync(long revisionId, CancellationToken cancellationToken = default);

    Task<PublicPageInfo> UnpublishRevisionAsync(long revisionId, CancellationToken cancellationToken = default);
}
