using System.Collections.Immutable;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.TerrainRepository;

[Scoped<TerrainRepositoryImpl, ITerrainRepository>]
internal class TerrainRepositoryImpl : RepositoryBase, ITerrainRepository
{
    public TerrainRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }

    public async Task<long> GetTerrainCountAsync(
        CancellationToken cancellationToken = default)
    {
        return await Client.TerrainsAllCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Terrain>> GetTerrainsAsync(
        int skip,
        int take,
        string? search = default,
        CancellationToken cancellationToken = default)
    {
        var terrains = await Client.TerrainsAllAsync(skip, take, search, cancellationToken)
            .ConfigureAwait(false);
        return terrains.ToImmutableArray();
    }

    public async Task<Terrain> CreateTerrainAsync(
        Terrain terrain,
        CancellationToken cancellationToken = default)
    {
        terrain = await Client.TerrainsCreateAsync(terrain, cancellationToken)
            .ConfigureAwait(false);
        return terrain;
    }

    public async Task ModifyTerrainAsync(
        Terrain terrain,
        CancellationToken cancellationToken = default)
    {
        if (terrain.PrimaryKey is null)
            throw new ArgumentException("Terrain.PrimaryKey is null.", nameof(terrain));
        await Client.TerrainsUpdateAsync(terrain.PrimaryKey.Value, terrain, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteTerrainAsync(
        Terrain terrain,
        CancellationToken cancellationToken = default)
    {
        if (terrain.PrimaryKey is null)
            throw new ArgumentException("Terrain.PrimaryKey is null.", nameof(terrain));
        await Client.TerrainsDeleteAsync(terrain.PrimaryKey.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Terrain?> GetTerrainAsync(long terrainFk, CancellationToken cancellationToken = default)
    {
        return await Client.TerrainsAsync(terrainFk, cancellationToken)
            .ConfigureAwait(false);
    }
}