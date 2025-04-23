using System.Collections.Immutable;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.WikiRepository;

[Scoped<WikiRepositoryImpl, IWikiRepository>]
internal class WikiRepositoryImpl : RepositoryBase, IWikiRepository
{
    public WikiRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }

    public async Task<IReadOnlyCollection<WikiPageHeader>> GetWikiPageHeadersAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await Client.WikiAllHeaderAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ToImmutableArray();
    }

    public async Task<WikiPage> GetWikiPageAsync(
        Guid pageId,
        bool allRevisions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await Client.WikiGetAsync(pageId, allRevisions, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    public async Task RenameAsync(Guid pageId, string title, CancellationToken cancellationToken = default)
    {
        await Client.WikiPutAsync(pageId, title, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WikiPageRevision> CreateRevisionAsync(
        Guid pageId,
        string markdown,
        string comment,
        CancellationToken cancellationToken = default)
    {
        return await Client.WikiRevisionsPostAsync(pageId, markdown, comment, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteWikiPageAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        
        await Client.WikiDeleteAsync(pageId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WikiPage> CreatePageAsync(
        string title,
        string markdown,
        string comment = "",
        CancellationToken cancellationToken = default)
    {
        return await Client.WikiPostAsync(title, markdown, comment, cancellationToken)
            .ConfigureAwait(false);
    }
}