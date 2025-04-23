using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Provides API endpoints for <see cref="ModPackDefinition"/>s and <see cref="ModPackRevision"/>s.
/// </summary>
[ApiController]
[Route(Constants.Routes.ModPacks)]
public class ModPackController : ControllerBase
{
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

    #region Endpoints

    /// <summary>
    /// Downloads the <see cref="ModPackRevision"/> and updates the last downloaded timestamp in the users meta data.
    /// </summary>
    /// <param name="modPackDefinitionId">The <see cref="ModPackDefinition.PrimaryKey"/> of the <see cref="ModPackDefinition"/> to download.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>A file response containing the corresponding <see cref="ModPackRevision"/> HTML and title.</returns>
    [AllowAnonymous]
    [HttpGet("{modPackDefinitionId:long}/download/latest", Name = nameof(DownloadLatestModPackAsync))]
    [ProducesDefaultResponseType(typeof(FileContentResult))]
    public async Task<FileContentResult> DownloadLatestModPackAsync(
        [FromRoute] long modPackDefinitionId,
        CancellationToken cancellationToken
    )
    {
        return await ProduceFileContentResultFromModPackAsync(modPackDefinitionId, null, cancellationToken);
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
    [AllowAnonymous]
    [HttpGet("{modPackDefinitionId:long}/download/{modPackRevisionId:long}", Name = nameof(DownloadModPackAsync))]
    [ProducesDefaultResponseType(typeof(FileContentResult))]
    public async Task<FileContentResult> DownloadModPackAsync(
        [FromRoute] long modPackDefinitionId,
        [FromRoute] long modPackRevisionId,
        CancellationToken cancellationToken
    )
    {
        return await ProduceFileContentResultFromModPackAsync(
            modPackDefinitionId,
            modPackRevisionId,
            cancellationToken
        );
    }


    /// <summary>
    /// Creates a new <see cref="ModPackDefinition"/> and a corresponding <see cref="ModPackRevision"/>.
    /// </summary>
    /// <remarks>
    /// The initial <see cref="ModPackRevision"/> must be provided in the <see cref="ModPackDefinition"/> object.
    /// </remarks>
    /// <param name="payload">The <see cref="ModPackCreationPayload"/> containing the modpack data to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>The created <see cref="ModPackDefinition"/>.</returns>
    [Authorize]
    [Authorize(Claims.Creation.ModPack)]
    [HttpPost("create/standalone", Name = nameof(CreateModPackStandaloneAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<PlainModPackDefinitionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateModPackStandaloneAsync(
        [FromBody] ModPackCreationPayload payload,
        CancellationToken cancellationToken
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var now = DateTimeOffset.Now;
        var modPackRevision = new ModPackRevision
        {
            PrimaryKey         = default,
            TimeStampCreated   = now,
            Html               = payload.Revision.Html,
            UpdatedBy          = null,
            UpdatedByFk        = userId,
            IsActive           = payload.Revision.IsActive,
            UserMetas          = null,
            Definition         = null,
            DefinitionFk       = payload.Revision.DefinitionFk,
            ModPackDefinitions = null,
        };
        var modPack = new ModPackDefinition
        {
            PrimaryKey            = default,
            TimeStampCreated      = now,
            Title                 = payload.Definition.Title,
            OwnerFk               = userId,
            IsActive              = true,
            IsComposition         = payload.Definition.IsComposition,
            ModPackRevisions      = [modPackRevision],
            ModPackRevisionsOwned = [modPackRevision],
        };
        var entity = await _apiDbContext.ModPackDefinitions.AddAsync(modPack, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity.Entity.ToPlainDto());
    }

    /// <summary>
    /// Creates a new <see cref="ModPackDefinition"/> and links the given <see cref="ModPackRevision"/> to it.
    /// </summary>
    /// <remarks>
    /// A composition may not own any <see cref="ModPackRevision"/>s.
    /// </remarks>
    /// <param name="payload">The data to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>The created <see cref="ModPackDefinition"/>.</returns>
    [Authorize]
    [Authorize(Claims.Creation.ModPack)]
    [HttpPost("create/composition", Name = nameof(CreateModPackCompositionAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType<PlainModPackDefinitionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateModPackCompositionAsync(
        [FromBody] ModPackCompositionCreationPayload payload,
        CancellationToken cancellationToken
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var modPack = new ModPackDefinition
        {
            IsActive              = true,
            OwnerFk               = userId,
            TimeStampCreated      = DateTimeOffset.Now,
            IsComposition         = true,
            Title                 = payload.Title,
            Owner                 = null,
            ModPackRevisions      = [],
            ModPackRevisionsOwned = null,
        };
        foreach (var modPackRevisionId in payload.RevisionIds)
        {
            var modPackRevision = await _apiDbContext.ModPackRevisions.SingleOrDefaultAsync(
                c => c.PrimaryKey == modPackRevisionId,
                cancellationToken
            );
            if (modPackRevision is null)
                return BadRequest();
            modPack.ModPackRevisions.Add(modPackRevision);
        }

        var entity = await _apiDbContext.ModPackDefinitions.AddAsync(modPack, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity.Entity.ToPlainDto());
    }

    /// <summary>
    /// Allows to update the <see cref="ModPackDefinition"/> title or set a new <see cref="ModPackRevision"/>
    /// as active <see cref="ModPackRevision"/>.
    /// </summary>
    /// <param name="modPackDefinitionId">The <see cref="ModPackDefinition.PrimaryKey"/> of the <see cref="ModPackDefinition"/>.</param>
    /// <param name="modPackStandaloneUpdate">
    ///     JSON object containing the new title of the <see cref="ModPackDefinition"/> (or null)
    ///     and the new html of the <see cref="ModPackDefinition"/> mod pack data (or null).
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [Authorize(Claims.ResourceBased.ModPack.Modify)]
    [HttpPost("{modPackDefinitionId:long}/update/standalone", Name = nameof(UpdateModPackStandaloneAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateModPackStandaloneAsync(
        [FromRoute] long modPackDefinitionId,
        [FromBody] ModPackStandaloneUpdate modPackStandaloneUpdate,
        CancellationToken cancellationToken
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var modPackDefinition = await _apiDbContext.ModPackDefinitions
            .Include(e => e.ModPackRevisions)
            .SingleOrDefaultAsync(q => q.PrimaryKey == modPackDefinitionId, cancellationToken);
        if (modPackDefinition is null)
            return NotFound();
        if (modPackStandaloneUpdate.Title is not null)
        {
            _logger.LogInformation(
                "Changing title of mod pack {ModPackId} from {OldTitle} to {NewTitle}",
                modPackDefinitionId,
                modPackDefinition.Title,
                modPackStandaloneUpdate.Title
            );
            modPackDefinition.Title = modPackStandaloneUpdate.Title;
        }

        if (modPackStandaloneUpdate.Html is not null)
        {
            var currentActiveRevision = await _apiDbContext.ModPackRevisions.SingleAsync(
                q => q.DefinitionFk == modPackDefinitionId && q.IsActive,
                cancellationToken
            );
            currentActiveRevision.IsActive     =   false;
            modPackDefinition.ModPackRevisions ??= new List<ModPackRevision>();
            modPackDefinition.ModPackRevisions.Clear();
            modPackDefinition.ModPackRevisions.Add(
                new ModPackRevision
                {
                    DefinitionFk     = modPackDefinitionId,
                    IsActive         = true,
                    Html             = modPackStandaloneUpdate.Html,
                    UpdatedByFk      = userId,
                    TimeStampCreated = DateTimeOffset.Now,
                }
            );
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Allows to update the <see cref="ModPackDefinition"/> title or set range of existing <see cref="ModPackRevision"/>
    /// as active <see cref="ModPackRevision"/>.
    /// </summary>
    /// <param name="modPackDefinitionId">The <see cref="ModPackDefinition.PrimaryKey"/> of the <see cref="ModPackDefinition"/>.</param>
    /// <param name="modPackCompositionUpdate">
    ///     JSON object containing the new title of the <see cref="ModPackDefinition"/> (or null)
    ///     and the new html of the <see cref="ModPackDefinition"/> mod pack data (or null).
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize(Claims.ResourceBased.ModPack.Modify)]
    [HttpPost("{modPackDefinitionId:long}/update/composition", Name = nameof(UpdateModPackCompositionAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateModPackCompositionAsync(
        [FromRoute] long modPackDefinitionId,
        [FromBody] ModPackCompositionUpdate modPackCompositionUpdate,
        CancellationToken cancellationToken
    )
    {
        var modPackDefinition = await _apiDbContext.ModPackDefinitions
            .Include(e => e.ModPackRevisions)
            .SingleOrDefaultAsync(q => q.PrimaryKey == modPackDefinitionId, cancellationToken);
        if (modPackDefinition is null)
            return NotFound();
        if (modPackCompositionUpdate.Title is not null)
        {
            _logger.LogInformation(
                "Changing title of mod pack {ModPackId} from {OldTitle} to {NewTitle}",
                modPackDefinitionId,
                modPackDefinition.Title,
                modPackCompositionUpdate.Title
            );
            modPackDefinition.Title = modPackCompositionUpdate.Title;
        }

        if (modPackCompositionUpdate.ModPackRevisionIds is not null)
        {
            modPackDefinition.ModPackRevisions ??= new List<ModPackRevision>();
            modPackDefinition.ModPackRevisions.Clear();
            foreach (var modPackRevisionId in modPackCompositionUpdate.ModPackRevisionIds)
            {
                var modPackRevision = await _apiDbContext.ModPackRevisions.SingleAsync(
                    q => q.PrimaryKey == modPackRevisionId,
                    cancellationToken
                );
                modPackDefinition.ModPackRevisions.Add(modPackRevision);
            }
        }

        if (modPackCompositionUpdate.UseLatest is true)
        {
            modPackDefinition.ModPackRevisions ??= new List<ModPackRevision>();
            foreach (var modPackRevision in modPackDefinition.ModPackRevisions.ToArray())
            {
                var owningModPackDefinitionId = modPackRevision.DefinitionFk;
                var latestRevision = await _apiDbContext.ModPackRevisions
                    .Where(q => q.DefinitionFk == owningModPackDefinitionId)
                    .Where(q => q.IsActive)
                    .SingleAsync(cancellationToken);
                modPackDefinition.ModPackRevisions.Remove(modPackRevision);
                modPackDefinition.ModPackRevisions.Add(latestRevision);
            }
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }


    /// <summary>
    /// Modifies the owning user of a <see cref="ModPackDefinition"/>.
    /// </summary>
    /// <remarks>
    /// Operation is final and only allowed by either the owner or a user with the
    /// <see cref="Claims.Administrative.ModPack"/> role.
    /// </remarks>
    /// <param name="modPackId">The <see cref="ModPackDefinition.PrimaryKey"/> of the <see cref="ModPackDefinition"/>.</param>
    /// <param name="newUserId">The <see cref="User.PrimaryKey"/> of the new <see cref="User"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [Authorize(Claims.ResourceBased.ModPack.Modify)]
    [HttpPost("{modPackId:long}/change-owner", Name = nameof(ChangeOwnerAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeOwnerAsync(
        [FromRoute] long modPackId,
        [FromQuery] Guid newUserId,
        CancellationToken cancellationToken
    )
    {
        var userExists = await _apiDbContext.Users.AnyAsync(q => q.PrimaryKey == newUserId, cancellationToken);
        if (!userExists)
            return NotFound();

        var existingModPack = await _apiDbContext.ModPackDefinitions.SingleOrDefaultAsync(
            q => q.PrimaryKey == modPackId,
            cancellationToken
        );
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
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<PlainModPackDefinitionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModPackDefinitionAsync(
        [FromRoute] long modPackId,
        CancellationToken cancellationToken
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingModPack = await _apiDbContext.ModPackDefinitions
            .Include(e => e.ModPackRevisions!.Where(q => q.IsActive))
            .ThenInclude(e => e.UserMetas!.Where(q => q.UserFk == userId))
            .SingleAsync(q => q.PrimaryKey == modPackId, cancellationToken);
        return Ok(existingModPack.ToPlainDto());
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
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<PlainModPackRevisionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModPackRevisionAsync(
        [FromRoute] long modPackRevisionPk,
        CancellationToken cancellationToken
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingModPack = await _apiDbContext.ModPackRevisions
            .Include(e => e.UserMetas!.Where(q => q.UserFk == userId))
            .Include(e => e.Definition)
            .SingleAsync(q => q.PrimaryKey == modPackRevisionPk, cancellationToken);
        return Ok(existingModPack.ToPlainDto());
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
    [Authorize(Claims.ResourceBased.ModPack.Delete)]
    [HttpPost("{modPackId:long}/delete", Name = nameof(DeleteModPackAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteModPackAsync([FromRoute] long modPackId, CancellationToken cancellationToken)
    {
        var existingModPack = await _apiDbContext.ModPackDefinitions.SingleOrDefaultAsync(
            q => q.PrimaryKey == modPackId,
            cancellationToken
        );
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
    [ProducesResponseType<FullModPackDefinitionDto[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModPacksAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool myModPacksOnly = false
    )
    {
        if (take > 500)
            // ReSharper disable once LocalizableElement
            throw new ArgumentOutOfRangeException(nameof(take), take, "Take has a hard-maximum of 500.");
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        IQueryable<ModPackDefinition> modPacks;
        if (myModPacksOnly)
        {
            modPacks = _apiDbContext.ModPackDefinitions
                .Include(e => e.ModPackRevisions!.Where(q => q.IsActive))
                .ThenInclude(e => e.UserMetas!.Where(q => q.UserFk == userId))
                .Where(q => q.IsActive)
                .Where(q => q.OwnerFk == userId)
                .Skip(skip)
                .Take(take);
        }
        else
        {
            modPacks = _apiDbContext.ModPackDefinitions
                .Include(e => e.ModPackRevisions!.Where(q => q.IsActive))
                .ThenInclude(e => e.UserMetas!.Where(q => q.UserFk == userId))
                .Where(q => q.IsActive)
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
            search   = $"%{search}%";
            // ReSharper disable once EntityFramework.ClientSideDbFunctionCall
            modPacks = modPacks.Where(q => EF.Functions.ILike(q.Title, search, "\\"));
        }

        var result = await modPacks.ToArrayAsync(cancellationToken);
        return Ok(
            result.Select(e => new FullModPackDefinitionDto
                {
                    PrimaryKey       = e.PrimaryKey,
                    TimeStampCreated = e.TimeStampCreated,
                    Title            = e.Title,
                    Owner            = e.Owner!.ToPlainDto(),
                    IsActive         = e.IsActive,
                    IsComposition    = e.IsComposition,
                    ModPackRevisions = e.ModPackRevisions!.Select(modPackRevision => modPackRevision.ToPlainDto())
                        .ToArray(),
                }
            )
        );
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
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModPacksCountAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool myModPacksOnly = false
    )
    {
        long count;
        if (myModPacksOnly)
        {
            if (!User.TryGetUserId(out var userId))
                return Unauthorized();
            count = await _apiDbContext.ModPackDefinitions
                .Where(q => q.IsActive)
                .Where(q => q.OwnerFk == userId)
                .LongCountAsync(cancellationToken);
        }
        else
        {
            count = await _apiDbContext.ModPackDefinitions
                .Where(q => q.IsActive)
                .LongCountAsync(cancellationToken);
        }

        return Ok(count);
    }

    #endregion

    #region Helper Methods

    private async Task<FileContentResult> ProduceFileContentResultFromModPackAsync(
        long modPackDefinitionId,
        long? modPackRevisionId,
        CancellationToken cancellationToken
    )
    {
        var modPackDefinition = await _apiDbContext.ModPackDefinitions.SingleAsync(
            q => q.PrimaryKey == modPackDefinitionId,
            cancellationToken
        );


        if (modPackDefinition.IsComposition)
        {
            var modPackRevisions = await _apiDbContext.ModPackRevisions
                .AsNoTracking()
                .Where(q => q.ModPackDefinitions!.Any(c => c.PrimaryKey == modPackDefinitionId))
                .ToArrayAsync(cancellationToken);
            var titles = new List<string>();
            var modList = new HashSet<Helpers.Arma3ModPackParser.TitleLinkPair>();
            var dependencyList = new HashSet<Helpers.Arma3ModPackParser.TitleLinkPair>();

            foreach (var modPackRevision in modPackRevisions)
            {
                var (title, mods, dependencies) = Helpers.Arma3ModPackParser.FromHtml(modPackRevision.Html);
                titles.Add(title);
                foreach (var mod in mods)
                    modList.Add(mod);
                foreach (var dependency in dependencies)
                    dependencyList.Add(dependency);
            }

            var joinedTitle = string.Join(" + ", titles);
            var html = Helpers.Arma3ModPackParser.ToHtml(joinedTitle, modList, dependencyList);
            var htmlBytes = Encoding.UTF8.GetBytes(html);
            var fileName = new string(
                joinedTitle.Where(q => q.IsLetterOrDigit() || q is '+' or '-')
                    .ToArray()
            );
            return FileContentResult(fileName, htmlBytes);
        }
        else
        {
            modPackRevisionId ??= await _apiDbContext.ModPackRevisions
                .Where(q => q.IsActive)
                .Where(q => q.DefinitionFk == modPackDefinitionId)
                .Select(q => q.PrimaryKey)
                .SingleAsync(cancellationToken);

            var modPackData = await _apiDbContext.ModPackRevisions
                .Where(q => q.PrimaryKey == modPackRevisionId)
                .Where(q => q.DefinitionFk == modPackDefinitionId)
                .Select(q => new
                    {
                        q.Html,
                        q.Definition!.Title,
                    }
                )
                .SingleAsync(cancellationToken);
            await UpdateUserModMeta(modPackDefinitionId, modPackRevisionId.Value, cancellationToken);

            await _apiDbContext.SaveChangesAsync(cancellationToken);
            var htmlBytes = Encoding.UTF8.GetBytes(modPackData.Html);
            var fileName = new string(
                modPackData.Title
                    .Where(q => q.IsLetterOrDigit() || q is '+' or '-')
                    .ToArray()
            );
            return FileContentResult(fileName, htmlBytes);
        }
    }

    private FileContentResult FileContentResult(string fileName, byte[] bytes)
    {
        Response.Headers.Append(
            "Content-Disposition",
            new System.Net.Mime.ContentDisposition
            {
                FileName = $"{fileName}.html",
                Inline   = false,
            }.ToString()
        );
        return new FileContentResult(bytes, "text/html")
        {
            FileDownloadName = "",
        };
    }

    private async Task UpdateUserModMeta(
        long modPackDefinitionId,
        long modPackRevisionId,
        CancellationToken cancellationToken
    )
    {
        if (User.TryGetUserId(out var userId))
        {
            var user = await _apiDbContext.Users
                .Include(e => e.ModPackMetas!.Where(q
                        => q.ModPackRevisionFk == modPackRevisionId && q.ModPackDefinitionFk == modPackDefinitionId
                    )
                )
                .Where(q => q.PrimaryKey == userId)
                .SingleAsync(cancellationToken);
            var userModPackMeta = user.ModPackMetas?.FirstOrDefault(q
                => q.ModPackRevisionFk == modPackRevisionId && q.ModPackDefinitionFk == modPackDefinitionId
            );
            if (userModPackMeta is null)
            {
                var userModPackMetaEntity = await _apiDbContext.UserModPackMetas.AddAsync(
                    new UserModPackMeta
                    {
                        UserFk              = userId,
                        ModPackDefinitionFk = modPackDefinitionId,
                        ModPackRevisionFk   = modPackRevisionId,
                    },
                    cancellationToken
                );
                userModPackMeta = userModPackMetaEntity.Entity;
            }

            userModPackMeta.TimeStampDownloaded = DateTimeOffset.Now;
        }
    }

    #endregion
}
