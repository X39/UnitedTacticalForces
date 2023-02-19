using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.TerrainRepository;

public interface ITerrainRepository
{
    Task<Terrain> CreateAsync(Terrain terrain, CancellationToken cancellationToken = default);
}

[Scoped<TerrainRepositoryImpl, ITerrainRepository>]
internal class TerrainRepositoryImpl : RepositoryBase, ITerrainRepository
{
    public TerrainRepositoryImpl(HttpClient httpClient) : base(httpClient)
    {
    }

    public async Task<Terrain> CreateAsync(Terrain terrain, CancellationToken cancellationToken = default)
    {
        return await Client.TerrainsCreateAsync(terrain, cancellationToken);
    }
}