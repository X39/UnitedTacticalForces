using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ModPackController : ControllerBase
{
    private readonly ILogger<ModPackController> _logger;
    private readonly ApiDbContext               _apiDbContext;

    public ModPackController(ILogger<ModPackController> logger, ApiDbContext apiDbContext)
    {
        _logger       = logger;
        _apiDbContext = apiDbContext;
    }

    /// <summary>
    /// Downloads the <see cref="ModPack"/> and updates the last downloaded timestamp in the users meta data.
    /// </summary>
    /// <param name="modPackId">The <see cref="ModPack.PrimaryKey"/> of the <see cref="ModPack"/> to download.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>The created <see cref="ModPack"/>.</returns>
    [Authorize]
    [HttpGet("{modPackId:long}/download", Name = nameof(DownloadModPackAsync))]
    [ProducesDefaultResponseType(typeof(FileContentResult))]
    public async Task<FileContentResult> DownloadModPackAsync(
        [FromRoute] long modPackId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            throw new UnauthorizedAccessException();
        var modPackData = await _apiDbContext.ModPacks
            .Where((q) => q.PrimaryKey == modPackId)
            .Select(
                (q) => new
                {
                    q.Html,
                    q.Title,
                })
            .SingleAsync(cancellationToken);
        var user = await _apiDbContext.Users
            .Include((e) => e.ModPackMetas!.Where((q) => q.ModPackFk == modPackId))
            .Where((q) => q.PrimaryKey == userId)
            .SingleAsync(cancellationToken);
        var userModPackMeta = user.ModPackMetas?.FirstOrDefault((q) => q.ModPackFk == modPackId);
        if (userModPackMeta is null)
        {
            var userModPackMetaEntity = await _apiDbContext.UserModPackMetas.AddAsync(
                new UserModPackMeta
                {
                    UserFk    = userId,
                    ModPackFk = modPackId,
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
            FileDownloadName = ""
        };
    }

    /// <summary>
    /// Creates a new <see cref="ModPack"/>.
    /// </summary>
    /// <param name="modPack">The <see cref="ModPack"/> to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>The created <see cref="ModPack"/>.</returns>
    [Authorize(Roles = Roles.Admin + "," + Roles.ModPackCreate)]
    [HttpPost("create", Name = nameof(CreateModPackAsync))]
    public async Task<ActionResult<ModPack>> CreateModPackAsync(
        [FromBody] ModPack modPack,
        CancellationToken cancellationToken)
    {
        // ToDo: Add audit log
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        modPack.IsActive         = true;
        modPack.OwnerFk          = userId;
        modPack.TimeStampCreated = DateTimeOffset.Now;
        modPack.TimeStampUpdated = modPack.TimeStampCreated;
        var entity = await _apiDbContext.ModPacks.AddAsync(modPack, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity.Entity);
    }

    /// <summary>
    /// Allows to change the contents of a <see cref="ModPack"/>.
    /// </summary>
    /// <remarks>
    ///     Only non-system properties are allowed:
    ///     <list type="bullet">
    ///         <item><see cref="ModPack.Title"/></item>
    ///         <item><see cref="ModPack.Html"/></item>
    ///     </list>
    /// </remarks>
    /// <param name="modPackId">The <see cref="ModPack.PrimaryKey"/> of the <see cref="ModPack"/>.</param>
    /// <param name="updatedModPack">The updated mod pack data.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpPost("{modPackId:long}/update", Name = nameof(UpdateModPackAsync))]
    public async Task UpdateModPackAsync(
        [FromRoute] long modPackId,
        [FromBody] ModPack updatedModPack,
        CancellationToken cancellationToken)
    {
        // ToDo: Check roles
        // ToDo: Add audit log
        var existingModPack = await _apiDbContext.ModPacks
            .SingleAsync((q) => q.PrimaryKey == modPackId, cancellationToken);
        existingModPack.Title = updatedModPack.Title;
        if (updatedModPack.Html != existingModPack.Html)
        {
            existingModPack.Html             = updatedModPack.Html;
            existingModPack.TimeStampUpdated = DateTimeOffset.Now;
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }


    /// <summary>
    /// Modifies the owning user of a <see cref="ModPack"/>.
    /// </summary>
    /// <remarks>
    /// Operation is final and only allowed by either the owner or a user with the
    /// <see cref="Roles.Admin"/> role.
    /// </remarks>
    /// <param name="modPackId">The <see cref="ModPack.PrimaryKey"/> of the <see cref="ModPack"/>.</param>
    /// <param name="newUserId">The <see cref="User.PrimaryKey"/> of the new <see cref="User"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpPost("{modPackId:long}/change-owner", Name = nameof(ChangeOwnerAsync))]
    public async Task ChangeOwnerAsync(
        [FromRoute] long modPackId,
        [FromQuery] Guid newUserId,
        CancellationToken cancellationToken)
    {
        // ToDo: Check roles
        // ToDo: Add audit log
        var userExists = await _apiDbContext.Users.AnyAsync((q) => q.PrimaryKey == newUserId, cancellationToken);
        if (!userExists)
            throw new ArgumentException("No user found with that id.");

        var existingModPack =
            await _apiDbContext.ModPacks.SingleAsync((q) => q.PrimaryKey == modPackId, cancellationToken);
        existingModPack.OwnerFk = newUserId;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Receives a single <see cref="ModPack"/>.
    /// </summary>
    /// <param name="modPackId">The <see cref="ModPack.PrimaryKey"/> of the <see cref="ModPack"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpGet("{modPackId:long}", Name = nameof(GetModPackAsync))]
    public async Task<ModPack> GetModPackAsync(
        [FromRoute] long modPackId,
        CancellationToken cancellationToken)
    {
        // ToDo: Check roles
        // ToDo: Add audit log
        var existingModPack = await _apiDbContext.ModPacks
            .SingleAsync((q) => q.PrimaryKey == modPackId, cancellationToken);
        return existingModPack;
    }

    /// <summary>
    /// Makes a <see cref="ModPack"/> unavailable for further usage.
    /// </summary>
    /// <remarks>
    /// To not break data consistency, this call will not actually delete a <see cref="ModPack"/> but rather make it not appear in
    /// any call but a direct one.
    /// </remarks>
    /// <param name="modPackId">The <see cref="ModPack.PrimaryKey"/> of the <see cref="ModPack"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpPost("{modPackId:long}/delete", Name = nameof(DeleteModPackAsync))]
    public async Task DeleteModPackAsync(
        [FromRoute] long modPackId,
        CancellationToken cancellationToken)
    {
        // ToDo: Check roles
        // ToDo: Add audit log
        var existingModPack = await _apiDbContext.ModPacks
            .SingleAsync((q) => q.PrimaryKey == modPackId, cancellationToken);
        existingModPack.IsActive = false;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns all <see cref="ModPack"/>'s available or, given <paramref name="myModPacksOnly"/> is set, only those
    /// of the currently authorized user.
    /// </summary>
    /// <param name="skip">The amount of <see cref="ModPack"/>'s to skip. Paging argument.</param>
    /// <param name="take">The amount of <see cref="ModPack"/>'s to take after skip. Paging argument.</param>
    /// <param name="myModPacksOnly">
    ///     If <see langword="true"/>, only those <see cref="ModPack"/>'s of the current user are returned.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="search">
    ///     Searches the <see cref="ModPack.Title"/> with a function akin to <see cref="string.StartsWith(string)"/>
    /// </param>
    /// <returns>
    ///     The available <see cref="ModPack"/>'s.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="take"/> is greater then 500.</exception>
    [Authorize]
    [HttpPost("all", Name = nameof(GetModPacksAsync))]
    public async Task<ActionResult<IEnumerable<ModPack>>> GetModPacksAsync(
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
        IQueryable<ModPack> modPacks;
        if (myModPacksOnly)
        {
            modPacks = _apiDbContext.ModPacks
                .Include((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
                .Where((q) => q.IsActive)
                .Where((q) => q.OwnerFk == userId)
                .Skip(skip)
                .Take(take);
        }
        else
        {
            modPacks = _apiDbContext.ModPacks
                .Include((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
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
    /// Returns the count of all <see cref="ModPack"/>'s available or, given <paramref name="myModPacksOnly"/> is set,
    /// only those of the currently authorized user.
    /// </summary>
    /// <param name="myModPacksOnly">
    ///     If <see langword="true"/>, only those <see cref="ModPack"/>'s of the current user are accounted for.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    ///     The count of available <see cref="ModPack"/>'s.
    /// </returns>
    [Authorize]
    [HttpPost("all/count", Name = nameof(GetModPacksCountAsync))]
    public async Task<ActionResult<long>> GetModPacksCountAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool myModPacksOnly = false)
    {
        long count;
        if (myModPacksOnly)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized();
            count = await _apiDbContext.ModPacks
                .Where((q) => q.IsActive)
                .Where((q) => q.OwnerFk == userId)
                .LongCountAsync(cancellationToken);
        }
        else
        {
            count = await _apiDbContext.ModPacks
                .Where((q) => q.IsActive)
                .LongCountAsync(cancellationToken);
        }

        return count;
    }
}