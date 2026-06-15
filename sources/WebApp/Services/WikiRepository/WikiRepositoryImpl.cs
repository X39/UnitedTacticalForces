using System.Collections.Immutable;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Wiki;
using X39.UnitedTacticalForces.WebApp.UtfApi.Wiki.Item;
using X39.UnitedTacticalForces.WebApp.UtfApi.Wiki.Item.Revisions;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Wiki;
using X39.UnitedTacticalForces.WebApp.UtfApi.Wiki.Item;
using X39.UnitedTacticalForces.WebApp.UtfApi.Wiki.Item.Revisions;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.WikiRepository;

[Scoped<WikiRepositoryImpl, IWikiRepository>]
internal sealed class WikiRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl) : RepositoryBase(httpClientFactory, baseUrl),
    IWikiRepository
{
    public async Task<IReadOnlyCollection<WikiPageHeader>> GetWikiPageHeadersAsync(
        int? skip = null,
        int? take = null,
        bool showDeleted = false,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.Wiki
            .All
            .Header
            .GetAsync(
                conf =>
                {
                    conf.QueryParameters.Skip        = skip;
                    conf.QueryParameters.Take        = take;
                    conf.QueryParameters.ShowDeleted = showDeleted;
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return result?.ToImmutableArray() ?? [];
    }

    public async Task<WikiPageDto> GetWikiPageAsync(
        Guid pageId,
        bool allRevisions = false,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.Wiki[pageId]
            .GetAsync(conf => conf.QueryParameters.AllRevisions = allRevisions, cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Wiki page not found.");
    }

    public async Task<IReadOnlyCollection<WikiPageRevisionHeader>> GetWikiPageRevisionHeadersAsync(
        Guid pageId,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.Wiki[pageId]
            .Revisions
            .Header
            .GetAsync(
                conf =>
                {
                    conf.QueryParameters.Skip = skip;
                    conf.QueryParameters.Take = take;
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return result?.ToImmutableArray() ?? [];
    }

    public async Task<WikiPageRevisionDto> GetWikiPageRevisionAsync(
        Guid pageId,
        Guid revisionId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.Wiki[pageId]
            .Revisions[revisionId]
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Wiki page revision not found.");
    }

    public async Task RenameAsync(Guid pageId, string title, CancellationToken cancellationToken = default)
    {
        await Client.Wiki[pageId]
            .PutAsync(
                new WithPageKeyPutRequestBody
                {
                    Title = title,
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task<WikiPageRevisionDto> CreateRevisionAsync(
        Guid pageId,
        string markdown,
        string comment,
        CancellationToken cancellationToken = default
    )
    {
        var results = await Client.Wiki[pageId]
            .Revisions
            .PostAsync(
                new RevisionsPostRequestBody
                {
                    Comment  = comment,
                    Markdown = markdown,
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        return results ?? throw new NullReferenceException("Failed to create revision.");
    }

    public async Task DeleteWikiPageAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        await Client.Wiki[pageId]
            .DeleteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RestoreWikiPageAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        await Client.Wiki[pageId]
            .Restore
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WikiPageDto> CreatePageAsync(
        string title,
        string markdown,
        string comment = "",
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.Wiki
            .PostAsync(
                new WikiPostRequestBody
                {
                    Title    = title,
                    Markdown = markdown,
                    Comment  = comment,
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Failed to create wiki page.");
    }
}
