using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Media;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.Media;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Controller responsible for public-site image settings.
/// </summary>
[ApiController]
[Produces("application/json")]
[Route(Constants.Routes.SiteImageSettings)]
public class SiteImageSettingsController(DatabaseContext apiDbContext) : ControllerBase
{
    /// <summary>
    /// Returns image settings for public page rendering.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<SiteImageSettingDto[]>(StatusCodes.Status200OK)]
    [HttpGet(Name = nameof(GetPublicSiteImageSettingsAsync))]
    public async Task<IActionResult> GetPublicSiteImageSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await CreateSettingsQuery()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(settings.Select(ToDto).ToArray());
    }

    /// <summary>
    /// Returns image settings for command-center editing.
    /// </summary>
    [Authorize]
    [ProducesResponseType<SiteImageSettingDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("admin", Name = nameof(GetAdminSiteImageSettingsAsync))]
    public async Task<IActionResult> GetAdminSiteImageSettingsAsync(
        [FromQuery] string? group = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageSiteImages())
            return Forbid();

        var query = CreateSettingsQuery();
        if (!string.IsNullOrWhiteSpace(group))
        {
            var normalizedGroup = group.Trim().ToLowerInvariant();
            query = query.Where(q => q.Group == normalizedGroup);
        }

        var settings = await query
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(settings.Select(ToDto).ToArray());
    }

    /// <summary>
    /// Updates one public-site image setting.
    /// </summary>
    [Authorize]
    [ProducesResponseType<SiteImageSettingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPut("admin/{key}", Name = nameof(UpdateSiteImageSettingAsync))]
    public async Task<IActionResult> UpdateSiteImageSettingAsync(
        [FromRoute] string key,
        [FromBody] SiteImageSettingUpdatePayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageSiteImages())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (payload.ImageAssetFk == default)
            return BadRequest("Image asset is required.");
        if (!await MediaAssetExistsAsync(payload.ImageAssetFk, cancellationToken).ConfigureAwait(false))
            return BadRequest("Image asset does not exist.");

        var normalizedKey = key.Trim().ToLowerInvariant();
        var setting = await apiDbContext.SiteImageSettings
            .Include(q => q.UpdatedBy)
            .SingleOrDefaultAsync(q => q.PrimaryKey == normalizedKey, cancellationToken)
            .ConfigureAwait(false);
        if (setting is null)
            return NotFound();

        setting.ImageAssetFk = payload.ImageAssetFk;
        setting.UpdatedByFk = userId;
        setting.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
        await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        setting = await CreateSettingsQuery()
            .SingleAsync(q => q.PrimaryKey == normalizedKey, cancellationToken)
            .ConfigureAwait(false);
        return Ok(ToDto(setting));
    }

    private IQueryable<SiteImageSetting> CreateSettingsQuery()
        => apiDbContext.SiteImageSettings
            .AsNoTracking()
            .Include(q => q.UpdatedBy)
            .OrderBy(q => q.Group)
            .ThenBy(q => q.SortOrder)
            .ThenBy(q => q.Title);

    private async Task<bool> MediaAssetExistsAsync(Guid assetId, CancellationToken cancellationToken)
        => await apiDbContext.MediaAssets
            .AsNoTracking()
            .AnyAsync(q => q.PrimaryKey == assetId, cancellationToken)
            .ConfigureAwait(false);

    private bool CanManageSiteImages()
        => HasAnyEmptyClaim(
               Claims.Administrative.All,
               Claims.Administrative.Wiki,
               Claims.Creation.Wiki
           )
           || HasBaseClaim(Claims.Wiki.All)
           || HasBaseClaim(Claims.Content.Pages);

    private bool HasAnyEmptyClaim(params string[] claimIdentifiers)
        => claimIdentifiers.Any(claimIdentifier => User.HasClaim(claimIdentifier, string.Empty));

    private bool HasBaseClaim(string claimIdentifier)
        => User.HasClaim(claimIdentifier, string.Empty);

    private static SiteImageSettingDto ToDto(SiteImageSetting setting)
        => new()
        {
            Key              = setting.PrimaryKey,
            Title            = setting.Title,
            Description      = setting.Description,
            Group            = setting.Group,
            SortOrder        = setting.SortOrder,
            ImageAssetFk     = setting.ImageAssetFk,
            ImagePath        = IMediaAssetService.CreateImagePath(setting.ImageAssetFk),
            TimeStampCreated = setting.TimeStampCreated,
            TimeStampUpdated = setting.TimeStampUpdated,
            UpdatedByName    = setting.UpdatedBy?.Nickname,
        };
}
