using System.Collections.Immutable;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

[Scoped<ModPackRepositoryImpl, IModPackRepository>]
internal class ModPackRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    : RepositoryBase(httpClientFactory, baseUrl), IModPackRepository
{
    public async Task<long> GetModPackCountAsync(bool myModPacksOnly, CancellationToken cancellationToken = default)
    {
        var result = await Client.ModPacks
            .All
            .Count
            .GetAsync(conf => conf.QueryParameters.MyModPacksOnly = myModPacksOnly, cancellationToken)
            .ConfigureAwait(false);
        return result ?? default;
    }

    public async Task<IReadOnlyCollection<FullModPackDefinitionDto>> GetModPacksAsync(
        int skip,
        int take,
        bool myModPacksOnly,
        string? search = default,
        CancellationToken cancellationToken = default
    )
    {
        var modPacks = await Client.ModPacks
            .All
            .GetAsync(
                conf =>
                {
                    conf.QueryParameters.Skip           = skip;
                    conf.QueryParameters.Take           = take;
                    conf.QueryParameters.MyModPacksOnly = myModPacksOnly;
                    conf.QueryParameters.Search         = search;
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        return modPacks?.ToImmutableArray() ?? [];
    }

    public async Task<PlainModPackDefinitionDto> CreateModPackAsync(
        ModPackCompositionCreationPayload payload,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.ModPacks
            .Create
            .Composition
            .PostAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Failed to create mod pack.");
    }

    public async Task<PlainModPackDefinitionDto> CreateModPackAsync(
        ModPackCreationPayload payload,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.ModPacks
            .Create
            .Standalone
            .PostAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Failed to create mod pack.");
    }

    public async Task ModifyModPackAsync(
        long modPackDefinitionId,
        string? title = null,
        string? html = null,
        CancellationToken cancellationToken = default
    )
    {
        await Client.ModPacks[modPackDefinitionId]
            .Update
            .Standalone
            .PostAsync(
                new ModPackStandaloneUpdate
                {
                    Html  = html,
                    Title = title,
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task ModifyModPackAsync(
        long modPackDefinitionId,
        string? title = null,
        bool? useLatest = null,
        long[]? modPackRevisionIds = null,
        CancellationToken cancellationToken = default
    )
    {
        await Client.ModPacks[modPackDefinitionId]
            .Update
            .Composition
            .PostAsync(
                new ModPackCompositionUpdate
                {
                    Title = title,
                    ModPackRevisionIds = modPackRevisionIds?.Cast<long?>()
                        .ToList(),
                    UseLatest = useLatest,
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task DeleteModPackAsync(long modPackDefinitionId, CancellationToken cancellationToken = default)
    {
        await Client.ModPacks[modPackDefinitionId]
            .DeletePath
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ModPackDefinitionDto?> GetModPackDefinitionAsync(
        long modPackDefinitionId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.ModPacks[modPackDefinitionId]
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    public async Task<ModPackWorkspaceInfo?> GetModPackWorkspaceAsync(
        long modPackDefinitionId,
        CancellationToken cancellationToken = default
    )
    {
        return await GetFromJsonAsync<ModPackWorkspaceInfo>(
                $"mod-packs/{modPackDefinitionId}/workspace",
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task<FullModPackDefinitionDto> GetFullModPackDefinitionAsync(
        long modPackDefinitionId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await GetFromJsonAsync<FullModPackDefinitionDto>(
                $"mod-packs/{modPackDefinitionId}/full",
                cancellationToken
            )
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Failed to load mod pack.");
    }

    public async Task<IReadOnlyCollection<PlainModPackRevisionDto>> GetModPackRevisionHistoryAsync(
        long modPackDefinitionId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await GetFromJsonAsync<PlainModPackRevisionDto[]>(
                $"mod-packs/{modPackDefinitionId}/revisions",
                cancellationToken
            )
            .ConfigureAwait(false);
        return result ?? [];
    }

    public async Task<IReadOnlyCollection<ModPackDownloadAuditInfo>> GetModPackDownloadAuditsAsync(
        long modPackDefinitionId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await GetFromJsonAsync<ModPackDownloadAuditInfo[]>(
                $"mod-packs/{modPackDefinitionId}/downloads",
                cancellationToken
            )
            .ConfigureAwait(false);
        return result ?? [];
    }

    public async Task<ModPackRevisionDto?> GetModPackRevisionAsync(
        long modPackRevisionPk,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.ModPacks
            .Revision[modPackRevisionPk]
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result;
    }
}
