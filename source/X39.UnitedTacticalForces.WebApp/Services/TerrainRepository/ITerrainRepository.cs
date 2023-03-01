namespace X39.UnitedTacticalForces.WebApp.Services.TerrainRepository;

public interface ITerrainRepository
{
    Task<long> GetTerrainCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Terrain>> GetTerrainsAsync(
        int skip,
        int take,
        string? search = default,
        CancellationToken cancellationToken = default);

    Task<Terrain> CreateTerrainAsync(Terrain terrain, CancellationToken cancellationToken = default);
    Task ModifyTerrainAsync(Terrain terrain, CancellationToken cancellationToken = default);
    Task DeleteTerrainAsync(Terrain terrain, CancellationToken cancellationToken = default);
}