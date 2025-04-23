using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Provides API endpoints for managing terrains in the system.
/// </summary>
/// <remarks>
/// This controller handles the creation, retrieval, update, deletion,
/// and listing of <see cref="Terrain"/> entities. Authorization and
/// logging mechanisms are applied accordingly.
/// </remarks>
[ApiController]
[Route(Constants.Routes.Terrains)]
[Authorize]
public class TerrainController : ControllerBase
{
    private readonly ILogger<TerrainController> _logger;
    private readonly ApiDbContext               _apiDbContext;

    /// <summary>
    /// Provides API endpoints for managing terrains in the system.
    /// </summary>
    /// <remarks>
    /// Includes operations such as creating, updating, retrieving, deleting, and listing terrains.
    /// This controller enforces authorization policies and integrates with the system's logging
    /// and data handling mechanisms.
    /// </remarks>
    public TerrainController(ILogger<TerrainController> logger, ApiDbContext apiDbContext)
    {
        _logger       = logger;
        _apiDbContext = apiDbContext;
    }

    /// <summary>
    /// Creates a new <see cref="Terrain"/>.
    /// </summary>
    /// <param name="payload">The <see cref="Terrain"/> to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>The created <see cref="Terrain"/>.</returns>
    [Authorize(Claims.Creation.Terrain)]
    [HttpPost("create", Name = nameof(CreateTerrainAsync))]
    [ProducesResponseType<PlainTerrainDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateTerrainAsync(
        [FromBody] TerrainCreationPayload payload,
        CancellationToken cancellationToken
    )
    {
        // ToDo: Add audit log
        var terrain = new Terrain
        {
            PrimaryKey    = default,
            Title         = payload.Title,
            Image         = payload.Image,
            ImageMimeType = payload.ImageMimeType,
            IsActive      = true,
        };
        var entity = await _apiDbContext.Terrains.AddAsync(terrain, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity.Entity.ToPlainDto());
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
    [Authorize(Claims.ResourceBased.Terrain.Modify)]
    [HttpPost("{terrainId:long}/update", Name = nameof(UpdateTerrainAsync))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTerrainAsync(
        [FromRoute] long terrainId,
        [FromBody] TerrainUpdate updatedTerrain,
        CancellationToken cancellationToken
    )
    {
        // ToDo: Add audit log
        var existingTerrain =
            await _apiDbContext.Terrains.SingleAsync(q => q.PrimaryKey == terrainId, cancellationToken);
        if (updatedTerrain.Title is not null)
            existingTerrain.Title = updatedTerrain.Title;
        if (updatedTerrain.Image is not null && updatedTerrain.ImageMimeType is not null)
        {
            existingTerrain.Image         = updatedTerrain.Image;
            existingTerrain.ImageMimeType = updatedTerrain.ImageMimeType;
        }
        else if (updatedTerrain.Image is not null || updatedTerrain.ImageMimeType is not null)
            return BadRequest();

        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Receives a single <see cref="Terrain"/>.
    /// </summary>
    /// <param name="terrainId">The <see cref="Terrain.PrimaryKey"/> of the <see cref="Terrain"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [HttpGet("{terrainId:long}", Name = nameof(GetTerrainAsync))]
    [ProducesResponseType<PlainTerrainDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTerrainAsync([FromRoute] long terrainId, CancellationToken cancellationToken)
    {
        var existingTerrain = await _apiDbContext.Terrains.SingleOrDefaultAsync(
            q => q.PrimaryKey == terrainId,
            cancellationToken
        );
        if (existingTerrain is null)
            return NotFound();
        return Ok(existingTerrain.ToPlainDto());
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
    [Authorize(Claims.ResourceBased.Terrain.Delete)]
    [HttpPost("{terrainId:long}/delete", Name = nameof(DeleteTerrainAsync))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTerrainAsync([FromRoute] long terrainId, CancellationToken cancellationToken)
    {
        // ToDo: Add audit log
        var existingTerrain =
            await _apiDbContext.Terrains.SingleAsync(q => q.PrimaryKey == terrainId, cancellationToken);
        existingTerrain.IsActive = false;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
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
    [HttpPost("all", Name = nameof(GetTerrainsAsync))]
    [ProducesResponseType<PlainTerrainDto[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTerrainsAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null
    )
    {
        if (take > 500)
            // ReSharper disable once LocalizableElement
            throw new ArgumentOutOfRangeException(nameof(take), take, "Take has a hard-maximum of 500.");
        var terrains = _apiDbContext.Terrains
            .Where(q => q.IsActive)
            .Skip(skip)
            .Take(take);
        if (search.IsNotNullOrWhiteSpace())
        {
            search = search.Trim();
            search = search.Replace("%", "\\%");
            search = search.Replace(",", "\\,");
            search = search.Replace("_", "\\_");
            search = search.Replace(",", "\\,");
            search = search.Replace("[", "\\[");
            search = search.Replace(",", "\\,");
            search = search.Replace("]", "\\]");
            search = search.Replace(",", "\\,");
            search = search.Replace("^", "\\^");
            search = search.Replace("\\", "\\\\");
            search = $"{search}%";
            // ReSharper disable once EntityFramework.ClientSideDbFunctionCall
            terrains = terrains.Where(q => EF.Functions.ILike(q.Title, search, "\\"));
        }

        var result = await terrains.ToArrayAsync(cancellationToken);

        return Ok(
            result.Select(e => e.ToPlainDto())
                .ToArray()
        );
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
    [HttpPost("all/count", Name = nameof(GetTerrainsCountAsync))]
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTerrainsCountAsync(CancellationToken cancellationToken)
    {
        var count = await _apiDbContext.Terrains
            .Where(q => q.IsActive)
            .LongCountAsync(cancellationToken);

        return Ok(count);
    }
}
