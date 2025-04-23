using X39.UnitedTacticalForces.WebApp.Api.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.TerrainRepository;

public interface ITerrainRepository
{
    Task<long> GetTerrainCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlainTerrainDto>> GetTerrainsAsync(
        int skip,
        int take,
        string? search = default,
        CancellationToken cancellationToken = default
    );

    Task<PlainTerrainDto> CreateTerrainAsync(
        TerrainCreationPayload payload,
        CancellationToken cancellationToken = default
    );

    Task ModifyTerrainAsync(
        long terrainId,
        TerrainUpdate payload,
        CancellationToken cancellationToken = default
    );

    Task DeleteTerrainAsync(long terrainId, CancellationToken cancellationToken = default);
    Task<PlainTerrainDto?> GetTerrainAsync(long terrainFk, CancellationToken cancellationToken = default);
}
