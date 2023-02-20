using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TerrainsController : ControllerBase
{
    private readonly ILogger<TerrainsController> _logger;
    private readonly ApiDbContext _apiDbContext;

    public TerrainsController(ILogger<TerrainsController> logger, ApiDbContext apiDbContext)
    {
        _logger = logger;
        _apiDbContext = apiDbContext;
    }
    
    [Authorize]
    [HttpPost("create", Name = nameof(CreateTerrainAsync))]
    public async Task<Terrain> CreateTerrainAsync([FromBody] Terrain terrain, CancellationToken cancellationToken)
    {
        var entity = await _apiDbContext.Terrains.AddAsync(terrain, cancellationToken);
        await _apiDbContext.Users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            Avatar = terrain.Image,
            AvatarMimeType = terrain.ImageMimeType,
            Nickname = "Test User",
        }, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return entity.Entity;
    }

    [Authorize]
    [HttpPost("{terrainId:long}/update", Name = nameof(UpdateTerrainAsync))]
    public async Task UpdateTerrainAsync(
        [FromRoute] long terrainId,
        [FromBody] Terrain updatedTerrain,
        CancellationToken cancellationToken)
    {
        var existingTerrain = await _apiDbContext.Terrains.SingleAsync((q) => q.Id == terrainId, cancellationToken);
        existingTerrain.Title = updatedTerrain.Title;
        existingTerrain.Image= updatedTerrain.Image;
        existingTerrain.ImageMimeType= updatedTerrain.ImageMimeType;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }
}