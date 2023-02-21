namespace X39.UnitedTacticalForces.WebApp.Services.TerrainRepository;

public interface ITerrainRepository
{
    Task<Terrain> CreateAsync(Terrain terrain, CancellationToken cancellationToken = default);
}