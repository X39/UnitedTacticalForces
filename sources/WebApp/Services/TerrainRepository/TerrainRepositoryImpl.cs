using System.Collections.Immutable;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.TerrainRepository;

[Scoped<TerrainRepositoryImpl, ITerrainRepository>]
internal sealed class TerrainRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl)
    : RepositoryBase(httpClient, baseUrl), ITerrainRepository
{
    public async Task<long> GetTerrainCountAsync(CancellationToken cancellationToken = default)
    {
        var result = await Client.Terrains
            .All
            .Count
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? default;
    }

    public async Task<IReadOnlyCollection<PlainTerrainDto>> GetTerrainsAsync(
        int skip,
        int take,
        string? search = default,
        CancellationToken cancellationToken = default
    )
    {
        var terrains = await Client.Terrains
            .All
            .PostAsync(
                conf =>
                {
                    conf.QueryParameters.Skip   = skip;
                    conf.QueryParameters.Take   = take;
                    conf.QueryParameters.Search = search;
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return terrains?.ToImmutableArray() ?? [];
    }

    public async Task<PlainTerrainDto> CreateTerrainAsync(
        TerrainCreationPayload payload,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.Terrains
            .Create
            .PostAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Unable to create new terrain");
    }

    public async Task ModifyTerrainAsync(
        long terrainId,
        TerrainUpdate payload,
        CancellationToken cancellationToken = default
    )
    {
        await Client.Terrains[terrainId]
            .Update
            .PostAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteTerrainAsync(long terrainId, CancellationToken cancellationToken = default)
    {
        await Client.Terrains[terrainId]
            .DeletePath
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PlainTerrainDto?> GetTerrainAsync(long terrainFk, CancellationToken cancellationToken = default)
    {
        var result = await Client.Terrains[terrainFk]
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result;
    }
}
