using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.Media;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Controller responsible for reusable gallery image assets.
/// </summary>
[ApiController]
[Produces("application/json")]
[Route(Constants.Routes.MediaAssets)]
public class MediaAssetsController(IMediaAssetService mediaAssetService) : ControllerBase
{
    /// <summary>
    /// Returns gallery assets with tag and usage filters.
    /// </summary>
    [Authorize]
    [ProducesResponseType<MediaAssetDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet(Name = nameof(GetMediaAssetsAsync))]
    public async Task<IActionResult> GetMediaAssetsAsync(
        [FromQuery] string? search = null,
        [FromQuery(Name = "tag")] string[]? tags = null,
        [FromQuery(Name = "excludeTag")] string[]? excludeTags = null,
        [FromQuery] string? usageStatus = null,
        [FromQuery] string? excludeUsageStatus = null,
        [FromQuery(Name = "usageDomain")] string[]? usageDomains = null,
        [FromQuery(Name = "excludeUsageDomain")] string[]? excludeUsageDomains = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 120,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMedia())
            return Forbid();
        if (skip < 0 || take is < 1 or > 200)
            return BadRequest();

        var assets = await mediaAssetService.ListAsync(
                new MediaAssetFilter(
                    search,
                    tags ?? [],
                    excludeTags ?? [],
                    usageStatus,
                    excludeUsageStatus,
                    usageDomains ?? [],
                    excludeUsageDomains ?? [],
                    skip,
                    take
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        return Ok(assets);
    }

    /// <summary>
    /// Returns one gallery asset.
    /// </summary>
    [Authorize]
    [ProducesResponseType<MediaAssetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{assetId:guid}", Name = nameof(GetMediaAssetAsync))]
    public async Task<IActionResult> GetMediaAssetAsync(
        [FromRoute] Guid assetId,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMedia())
            return Forbid();

        var asset = await mediaAssetService.GetAsync(assetId, cancellationToken).ConfigureAwait(false);
        return asset is null ? NotFound() : Ok(asset);
    }

    /// <summary>
    /// Uploads an image into the reusable gallery.
    /// </summary>
    [Authorize]
    [ProducesResponseType<MediaAssetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpPost("uploads", Name = nameof(UploadMediaAssetAsync))]
    public async Task<IActionResult> UploadMediaAssetAsync(
        [FromForm] IFormFile? file,
        [FromForm] string? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMedia())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var asset = await mediaAssetService
                .UploadAsync(file!, SplitFilterValue(tags), userId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(asset);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Replaces an asset's tag set.
    /// </summary>
    [Authorize]
    [ProducesResponseType<MediaAssetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPut("{assetId:guid}/tags", Name = nameof(ReplaceMediaAssetTagsAsync))]
    public async Task<IActionResult> ReplaceMediaAssetTagsAsync(
        [FromRoute] Guid assetId,
        [FromBody] MediaAssetTagsPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMedia())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var asset = await mediaAssetService
            .ReplaceTagsAsync(assetId, payload.Tags, userId, cancellationToken)
            .ConfigureAwait(false);
        return asset is null ? NotFound() : Ok(asset);
    }

    /// <summary>
    /// Returns image bytes for a gallery asset without disclosing filesystem paths.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{assetId:guid}/content", Name = nameof(GetMediaAssetContentAsync))]
    public async Task<IActionResult> GetMediaAssetContentAsync(
        [FromRoute] Guid assetId,
        [FromQuery] string? format = null,
        CancellationToken cancellationToken = default)
    {
        var source = await mediaAssetService.GetStoredFileAsync(assetId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
            return NotFound();

        var resolution = MediaAssetContentType.Resolve(format, source.ContentType, Request.GetTypedHeaders().Accept);
        if (resolution.IsInvalid || resolution.ContentType is null)
            return BadRequest("Unsupported image format. Use original, avif, webp, png, jpg, or jpeg.");

        var file = await mediaAssetService
            .GetStoredFileAsync(assetId, resolution.ContentType, cancellationToken)
            .ConfigureAwait(false);
        if (file is null)
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.Vary = "Accept";
        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromDays(1),
        };
        return PhysicalFile(file.FilePath, file.ContentType, enableRangeProcessing: true);
    }

    private bool CanManageMedia()
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

    private static string[] SplitFilterValue(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
