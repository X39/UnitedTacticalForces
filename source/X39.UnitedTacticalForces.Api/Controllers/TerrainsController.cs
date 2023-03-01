using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TerrainController : ControllerBase
{
    private readonly ILogger<TerrainController> _logger;
    private readonly ApiDbContext               _apiDbContext;

    public TerrainController(ILogger<TerrainController> logger, ApiDbContext apiDbContext)
    {
        _logger       = logger;
        _apiDbContext = apiDbContext;
    }

    /// <summary>
    /// Creates a new <see cref="Terrain"/>.
    /// </summary>
    /// <param name="terrain">The <see cref="Terrain"/> to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>The created <see cref="Terrain"/>.</returns>
    [Authorize(Roles = Roles.Admin + "," + Roles.TerrainCreate)]
    [HttpPost("create", Name = nameof(CreateTerrainAsync))]
    public async Task<ActionResult<Terrain>> CreateTerrainAsync(
        [FromBody] Terrain terrain,
        CancellationToken cancellationToken)
    {
        // ToDo: Add audit log
        terrain.IsActive = true;
        var entity = await _apiDbContext.Terrains.AddAsync(terrain, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity.Entity);
    }

    /// <summary>
    /// Allows to change the contents of a <see cref="Terrain"/>.
    /// </summary>
    /// <remarks>
    ///     Only non-system properties are allowed:
    ///     <list type="bullet">
    ///         <item><see cref="Terrain.Title"/></item>
    ///         <item><see cref="Terrain.Image"/></item>
    ///         <item><see cref="Terrain.ImageMimeType"/></item>
    ///     </list>
    /// </remarks>
    /// <param name="terrainId">The <see cref="Terrain.PrimaryKey"/> of the <see cref="Terrain"/>.</param>
    /// <param name="updatedTerrain">The updated mod pack data.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpPost("{terrainId:long}/update", Name = nameof(UpdateTerrainAsync))]
    public async Task UpdateTerrainAsync(
        [FromRoute] long terrainId,
        [FromBody] Terrain updatedTerrain,
        CancellationToken cancellationToken)
    {
        // ToDo: Check roles
        // ToDo: Add audit log
        var existingTerrain = await _apiDbContext.Terrains
            .SingleAsync((q) => q.PrimaryKey == terrainId, cancellationToken);
        existingTerrain.Title = updatedTerrain.Title;
        if (existingTerrain.Image.Any())
        {
            existingTerrain.Image         = updatedTerrain.Image;
            existingTerrain.ImageMimeType = updatedTerrain.ImageMimeType;
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Receives a single <see cref="Terrain"/>.
    /// </summary>
    /// <param name="terrainId">The <see cref="Terrain.PrimaryKey"/> of the <see cref="Terrain"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpGet("{terrainId:long}", Name = nameof(GetTerrainAsync))]
    public async Task<Terrain> GetTerrainAsync(
        [FromRoute] long terrainId,
        CancellationToken cancellationToken)
    {
        // ToDo: Check roles
        // ToDo: Add audit log
        var existingTerrain = await _apiDbContext.Terrains
            .SingleAsync((q) => q.PrimaryKey == terrainId, cancellationToken);
        return existingTerrain;
    }

    /// <summary>
    /// Makes a <see cref="Terrain"/> unavailable for further usage.
    /// </summary>
    /// <remarks>
    /// To not break data consistency, this call will not actually delete a <see cref="Terrain"/> but rather make it not appear in
    /// any call but a direct one.
    /// </remarks>
    /// <param name="terrainId">The <see cref="Terrain.PrimaryKey"/> of the <see cref="Terrain"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpPost("{terrainId:long}/delete", Name = nameof(DeleteTerrainAsync))]
    public async Task DeleteTerrainAsync(
        [FromRoute] long terrainId,
        CancellationToken cancellationToken)
    {
        // ToDo: Check roles
        // ToDo: Add audit log
        var existingTerrain = await _apiDbContext.Terrains
            .SingleAsync((q) => q.PrimaryKey == terrainId, cancellationToken);
        existingTerrain.IsActive = false;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns all <see cref="Terrain"/>'s available.
    /// </summary>
    /// <param name="skip">The amount of <see cref="Terrain"/>'s to skip. Paging argument.</param>
    /// <param name="take">The amount of <see cref="Terrain"/>'s to take after skip. Paging argument.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="search">
    ///     Searches the <see cref="Terrain.Title"/> with a function akin to <see cref="string.StartsWith(string)"/>
    /// </param>
    /// <returns>
    ///     The available <see cref="Terrain"/>'s.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="take"/> is greater then 500.</exception>
    [Authorize]
    [HttpPost("all", Name = nameof(GetTerrainsAsync))]
    public async Task<ActionResult<IEnumerable<Terrain>>> GetTerrainsAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null)
    {
        if (take > 500)
            throw new ArgumentOutOfRangeException(nameof(take), take, "Take has a hard-maximum of 500.");
        var terrains = _apiDbContext.Terrains
            .Where((q) => q.IsActive)
            .Skip(skip)
            .Take(take);
        if (search.IsNotNullOrWhiteSpace())
        {
            search   = search.Trim();
            search   = search.Replace("%", "\\%");
            search   = search.Replace(",", "\\,");
            search   = search.Replace("_", "\\_");
            search   = search.Replace(",", "\\,");
            search   = search.Replace("[", "\\[");
            search   = search.Replace(",", "\\,");
            search   = search.Replace("]", "\\]");
            search   = search.Replace(",", "\\,");
            search   = search.Replace("^", "\\^");
            search   = search.Replace("\\", "\\\\");
            search   = $"{search}%";
            terrains = terrains.Where((q) => EF.Functions.ILike(q.Title, search, "\\"));
        }

        var result = await terrains.ToArrayAsync(cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns the count of all <see cref="Terrain"/>'s available.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    ///     The count of available <see cref="Terrain"/>'s.
    /// </returns>
    [Authorize]
    [HttpPost("all/count", Name = nameof(GetTerrainsCountAsync))]
    public async Task<ActionResult<long>> GetTerrainsCountAsync(
        CancellationToken cancellationToken)
    {
        var count = await _apiDbContext.Terrains
            .Where((q) => q.IsActive)
            .LongCountAsync(cancellationToken);

        return count;
    }
}