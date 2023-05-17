namespace X39.UnitedTacticalForces.WebApp.Services.WikiRepository;

public interface IWikiRepository
{
    Task<IReadOnlyCollection<WikiPageHeader>> GetWikiPageHeadersAsync(CancellationToken cancellationToken = default);
    Task<WikiPage> GetWikiPageAsync(Guid pageId, bool allRevisions = false, CancellationToken cancellationToken = default);
    Task RenameAsync(Guid pageId, string title, CancellationToken cancellationToken = default);
    Task<WikiPageRevision> CreateRevisionAsync(Guid pageId, string markdown, string comment, CancellationToken cancellationToken = default);
    Task DeleteWikiPageAsync(Guid pageId, CancellationToken cancellationToken = default);
    Task<WikiPage> CreatePageAsync(string title, string markdown, string comment = "", CancellationToken cancellationToken = default);
}