using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.WikiRepository;

public interface IWikiRepository
{
    Task<IReadOnlyCollection<WikiPageHeader>> GetWikiPageHeadersAsync(
        int? skip = null,
        int? take = null,
        bool showDeleted = false,
        CancellationToken cancellationToken = default
    );

    Task<WikiPageDto> GetWikiPageAsync(
        Guid pageId,
        bool allRevisions = false,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<WikiPageRevisionHeader>> GetWikiPageRevisionHeadersAsync(
        Guid pageId,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default
    );

    Task<WikiPageRevisionDto> GetWikiPageRevisionAsync(
        Guid pageId,
        Guid revisionId,
        CancellationToken cancellationToken = default
    );

    Task<WikiPageDto> CreatePageAsync(
        string title,
        string markdown,
        string comment,
        CancellationToken cancellationToken = default
    );

    Task RenameAsync(Guid pageId, string title, CancellationToken cancellationToken = default);

    Task<WikiPageRevisionDto> CreateRevisionAsync(
        Guid pageId,
        string markdown,
        string comment,
        CancellationToken cancellationToken = default
    );

    Task DeleteWikiPageAsync(Guid pageId, CancellationToken cancellationToken = default);

    Task RestoreWikiPageAsync(Guid pageId, CancellationToken cancellationToken = default);
}
