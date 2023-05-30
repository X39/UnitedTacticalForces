using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Common;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Provides API endpoints for <see cref="ModPackDefinition"/>s and <see cref="ModPackRevision"/>s.
/// </summary>
[ApiController]
[Route(Constants.Routes.ModPacks)]
public class ModPackController : ControllerBase
{
    public record ModPackUpdate(string? Title, string? Html);
    private readonly ILogger<ModPackController> _logger;
    private readonly ApiDbContext               _apiDbContext;

    /// <summary>
    ///  Initializes a new instance of the <see cref="ModPackController"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to use.</param>
    /// <param name="apiDbContext">The <see cref="ApiDbContext"/> to use.</param>
    public ModPackController(ILogger<ModPackController> logger, ApiDbContext apiDbContext)
    {
        _logger       = logger;
        _apiDbContext = apiDbContext;
    }

    /// <summary>
    /// Downloads the <see cref="ModPackRevision"/> and updates the last downloaded timestamp in the users meta data.
    /// </summary>
    /// <param name="modPackDefinitionId">The <see cref="ModPackDefinition.PrimaryKey"/> of the <see cref="ModPackDefinition"/> to download.</param>
    /// <param name="modPackRevisionId">The <see cref="ModPackRevision.PrimaryKey"/> of the <see cref="ModPackRevision"/> to download.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>A file response containing the corresponding <see cref="ModPackRevision"/> HTML and title.</returns>
    [Authorize]
    [HttpGet("{modPackDefinitionId:long}/download/{modPackRevisionId:long}", Name = nameof(DownloadModPackAsync))]
    [ProducesDefaultResponseType(typeof(FileContentResult))]
    public async Task<FileContentResult> DownloadModPackAsync(
        [FromRoute] long modPackDefinitionId,
        [FromRoute] long modPackRevisionId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            throw new UnauthorizedAccessException();
        var modPackData = await _apiDbContext.ModPackRevisions
            .Where((q) => q.PrimaryKey == modPackRevisionId)
            .Where((q) => q.DefinitionFk == modPackDefinitionId)
            .Select(
                (q) => new
                {
                    q.Html,
                    q.Definition!.Title
                })
            .SingleAsync(cancellationToken);
        var user = await _apiDbContext.Users
            .Include(
                (e) => e.ModPackMetas!.Where(
                    (q) => q.ModPackRevisionFk == modPackRevisionId && q.ModPackDefinitionFk == modPackDefinitionId))
            .Where((q) => q.PrimaryKey == userId)
            .SingleAsync(cancellationToken);
        var userModPackMeta = user.ModPackMetas?.FirstOrDefault(
            (q) => q.ModPackRevisionFk == modPackRevisionId && q.ModPackDefinitionFk == modPackDefinitionId);
        if (userModPackMeta is null)
        {
            var userModPackMetaEntity = await _apiDbContext.UserModPackMetas.AddAsync(
                new UserModPackMeta
                {
                    UserFk              = userId,
                    ModPackDefinitionFk = modPackDefinitionId,
                    ModPackRevisionFk   = modPackRevisionId,
                },
                cancellationToken);
            userModPackMeta = userModPackMetaEntity.Entity;
        }

        userModPackMeta.TimeStampDownloaded = DateTimeOffset.Now;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        var htmlBytes = Encoding.UTF8.GetBytes(modPackData.Html);
        var fileName = new string(modPackData.Title.Where((q) => q.IsLetterOrDigit()).ToArray());
        Response.Headers.Add(
            "Content-Disposition",
            new System.Net.Mime.ContentDisposition
            {
                FileName = $"{fileName}.html",
                Inline   = false,
            }.ToString());
        return new FileContentResult(htmlBytes, "text/html")
        {
            FileDownloadName = "",
        };
    }

    /// <summary>
    /// Creates a new <see cref="ModPackDefinition"/> and a corresponding <see cref="ModPackRevision"/>.
    /// </summary>
    /// <remarks>
    /// The initial <see cref="ModPackRevision"/> must be provided in the <see cref="ModPackDefinition"/> object.
    /// </remarks>
    /// <param name="modPack">The <see cref="ModPackDefinition"/> to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>The created <see cref="ModPackDefinition"/>.</returns>
    [Authorize(Roles = Roles.Admin + "," + Roles.ModPackCreate)]
    [HttpPost("create", Name = nameof(CreateModPackAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ModPackDefinition), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    public async Task<ActionResult> CreateModPackAsync(
        [FromBody] ModPackDefinition modPack,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (modPack.ModPackRevisions is null)
            return BadRequest();
        if (modPack.ModPackRevisions.Count is not 1)
            return BadRequest();
        modPack.IsActive                                   = true;
        modPack.OwnerFk                                    = userId;
        modPack.TimeStampCreated                           = DateTimeOffset.Now;
        modPack.ModPackRevisions.Single().IsActive         = true;
        modPack.ModPackRevisions.Single().TimeStampCreated = modPack.TimeStampCreated;
        modPack.ModPackRevisions.Single().UpdatedByFk      = userId;
        var entity = await _apiDbContext.ModPackDefinitions.AddAsync(modPack, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity.Entity);
    }

    /// <summary>
    /// Allows to update the <see cref="ModPackDefinition"/> title or set a new <see cref="ModPackRevision"/>
    /// as active <see cref="ModPackRevision"/>.
    /// </summary>
    /// <param name="modPackDefinitionId">The <see cref="ModPackDefinition.PrimaryKey"/> of the <see cref="ModPackDefinition"/>.</param>
    /// <param name="modPackUpdate">
    ///     JSON object containing the new title of the <see cref="ModPackDefinition"/> (or null)
    ///     and the new html of the <see cref="ModPackDefinition"/> mod pack data (or null).
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpPost("{modPackDefinitionId:long}/update", Name = nameof(UpdateModPackAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    public async Task<ActionResult> UpdateModPackAsync(
        [FromRoute] long modPackDefinitionId,
        [FromBody] ModPackUpdate modPackUpdate,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var modPackDefinition = await _apiDbContext.ModPackDefinitions
            .SingleOrDefaultAsync((q) => q.PrimaryKey == modPackDefinitionId, cancellationToken);
        if (modPackDefinition is null)
            return NotFound();
        if (!User.IsInRoleOrAdmin(Roles.ModPackModify) && modPackDefinition.OwnerFk != userId)
            return Unauthorized();
        if (modPackUpdate.Title is not null)
        {
            _logger.LogInformation(
                "Changing title of mod pack {ModPackId} from {OldTitle} to {NewTitle}",
                modPackDefinitionId,
                modPackDefinition.Title,
                modPackUpdate.Title);
            modPackDefinition.Title = modPackUpdate.Title;
        }

        if (modPackUpdate.Html is not null)
        {
            var currentActiveRevision = await _apiDbContext.ModPackRevisions.SingleAsync(
                (q) => q.DefinitionFk == modPackDefinitionId && q.IsActive,
                cancellationToken);
            currentActiveRevision.IsActive = false;
            await _apiDbContext.ModPackRevisions.AddAsync(
                new ModPackRevision
                {
                    DefinitionFk     = modPackDefinitionId,
                    IsActive         = true,
                    Html             = modPackUpdate.Html,
                    UpdatedByFk      = userId,
                    TimeStampCreated = DateTimeOffset.Now,
                },
                cancellationToken);
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }


    /// <summary>
    /// Modifies the owning user of a <see cref="ModPackDefinition"/>.
    /// </summary>
    /// <remarks>
    /// Operation is final and only allowed by either the owner or a user with the
    /// <see cref="Roles.Admin"/> role.
    /// </remarks>
    /// <param name="modPackId">The <see cref="ModPackDefinition.PrimaryKey"/> of the <see cref="ModPackDefinition"/>.</param>
    /// <param name="newUserId">The <see cref="User.PrimaryKey"/> of the new <see cref="User"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpPost("{modPackId:long}/change-owner", Name = nameof(ChangeOwnerAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    public async Task<ActionResult> ChangeOwnerAsync(
        [FromRoute] long modPackId,
        [FromQuery] Guid newUserId,
        CancellationToken cancellationToken)
    {
        if (User.IsInRoleOrAdmin(Roles.ModPackModify))
            return Unauthorized();
        var userExists = await _apiDbContext.Users.AnyAsync((q) => q.PrimaryKey == newUserId, cancellationToken);
        if (!userExists)
            return NotFound();

        var existingModPack =
            await _apiDbContext.ModPackDefinitions.SingleOrDefaultAsync(
                (q) => q.PrimaryKey == modPackId,
                cancellationToken);
        if (existingModPack is null)
            return NotFound();
        existingModPack.OwnerFk = newUserId;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Receives a single <see cref="ModPackDefinition"/> and the latest active <see cref="ModPackRevision"/>.
    /// </summary>
    /// <param name="modPackId">The <see cref="ModPackDefinition.PrimaryKey"/> of the <see cref="ModPackDefinition"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpGet("{modPackId:long}", Name = nameof(GetModPackDefinitionAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ModPackDefinition), (int) HttpStatusCode.OK)]
    public async Task<ActionResult<ModPackDefinition>> GetModPackDefinitionAsync(
        [FromRoute] long modPackId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingModPack = await _apiDbContext.ModPackDefinitions
            .Include((e) => e.ModPackRevisions!.Where((q) => q.IsActive))
            .ThenInclude((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
            .SingleAsync((q) => q.PrimaryKey == modPackId, cancellationToken);
        return Ok(existingModPack);
    }

    /// <summary>
    /// Receives a single <see cref="ModPackRevision"/> and the parent <see cref="ModPackDefinition"/>.
    /// </summary>
    /// <param name="modPackRevisionPk">The <see cref="ModPackRevision.PrimaryKey"/> of the <see cref="ModPackRevision"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpGet("revision/{modPackRevisionPk:long}", Name = nameof(GetModPackRevisionAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ModPackRevision), (int) HttpStatusCode.OK)]
    public async Task<ActionResult<ModPackRevision>> GetModPackRevisionAsync(
        [FromRoute] long modPackRevisionPk,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingModPack = await _apiDbContext.ModPackRevisions
            .Include((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
            .Include((e) => e.Definition)
            .SingleAsync((q) => q.PrimaryKey == modPackRevisionPk, cancellationToken);
        return Ok(existingModPack);
    }

    /// <summary>
    /// Makes a <see cref="ModPackDefinition"/> unavailable for further usage.
    /// </summary>
    /// <remarks>
    /// To not break data consistency, this call will not actually delete a <see cref="ModPackDefinition"/> but rather make it not appear in
    /// any call but a direct one.
    /// </remarks>
    /// <param name="modPackId">The <see cref="ModPackDefinition.PrimaryKey"/> of the <see cref="ModPackDefinition"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpPost("{modPackId:long}/delete", Name = nameof(DeleteModPackAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    public async Task<ActionResult> DeleteModPackAsync(
        [FromRoute] long modPackId,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRoleOrAdmin(Roles.ModPackDelete))
            return Unauthorized();
        var existingModPack = await _apiDbContext.ModPackDefinitions
            .SingleOrDefaultAsync((q) => q.PrimaryKey == modPackId, cancellationToken);
        if (existingModPack is null)
            return NotFound();
        existingModPack.IsActive = false;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Returns all <see cref="ModPackDefinition"/>'s available or, given <paramref name="myModPacksOnly"/> is set, only those
    /// of the currently authorized user.
    /// </summary>
    /// <param name="skip">The amount of <see cref="ModPackDefinition"/>'s to skip. Paging argument.</param>
    /// <param name="take">The amount of <see cref="ModPackDefinition"/>'s to take after skip. Paging argument.</param>
    /// <param name="myModPacksOnly">
    ///     If <see langword="true"/>, only those <see cref="ModPackDefinition"/>'s of the current user are returned.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="search">
    ///     Searches the <see cref="ModPackDefinition.Title"/> with a function akin to <see cref="string.StartsWith(string)"/>
    /// </param>
    /// <returns>
    ///     The available <see cref="ModPackDefinition"/>'s.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="take"/> is greater then 500.</exception>
    [Authorize]
    [HttpGet("all", Name = nameof(GetModPacksAsync))]
    public async Task<ActionResult<IEnumerable<ModPackDefinition>>> GetModPacksAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool myModPacksOnly = false)
    {
        if (take > 500)
            throw new ArgumentOutOfRangeException(nameof(take), take, "Take has a hard-maximum of 500.");
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        IQueryable<ModPackDefinition> modPacks;
        if (myModPacksOnly)
        {
            modPacks = _apiDbContext.ModPackDefinitions
                .Include((e) => e.ModPackRevisions!.Where((q) => q.IsActive))
                .ThenInclude((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
                .Where((q) => q.IsActive)
                .Where((q) => q.OwnerFk == userId)
                .Skip(skip)
                .Take(take);
        }
        else
        {
            modPacks = _apiDbContext.ModPackDefinitions
                .Include((e) => e.ModPackRevisions!.Where((q) => q.IsActive))
                .ThenInclude((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
                .Where((q) => q.IsActive)
                .Skip(skip)
                .Take(take);
        }

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
            modPacks = modPacks.Where((q) => EF.Functions.ILike(q.Title, search, "\\"));
        }

        var result = await modPacks.ToArrayAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the count of all <see cref="ModPackDefinition"/>'s available or, given <paramref name="myModPacksOnly"/> is set,
    /// only those of the currently authorized user.
    /// </summary>
    /// <param name="myModPacksOnly">
    ///     If <see langword="true"/>, only those <see cref="ModPackDefinition"/>'s of the current user are accounted for.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    ///     The count of available <see cref="ModPackDefinition"/>'s.
    /// </returns>
    [Authorize]
    [HttpGet("all/count", Name = nameof(GetModPacksCountAsync))]
    public async Task<ActionResult<long>> GetModPacksCountAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool myModPacksOnly = false)
    {
        long count;
        if (myModPacksOnly)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized();
            count = await _apiDbContext.ModPackDefinitions
                .Where((q) => q.IsActive)
                .Where((q) => q.OwnerFk == userId)
                .LongCountAsync(cancellationToken);
        }
        else
        {
            count = await _apiDbContext.ModPackDefinitions
                .Where((q) => q.IsActive)
                .LongCountAsync(cancellationToken);
        }

        return count;
    }
}