using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.TerrainRepository;

[Scoped<TerrainRepositoryImpl, ITerrainRepository>]
internal class TerrainRepositoryImpl : RepositoryBase, ITerrainRepository
{
    public async Task<Terrain> CreateAsync(Terrain terrain, CancellationToken cancellationToken = default)
    {
        return await Client.TerrainsCreateAsync(terrain, cancellationToken);
    }

    public TerrainRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }
}