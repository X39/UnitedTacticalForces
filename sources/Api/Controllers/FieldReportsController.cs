using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Media;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.Services.Media;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Controller responsible for public and administrative field report endpoints.
/// </summary>
[ApiController]
[Produces("application/json")]
[Route(Constants.Routes.FieldReports)]
public class FieldReportsController(
    DatabaseContext apiDbContext,
    IMediaAssetService? mediaAssetService = null,
    IWebHostEnvironment? webHostEnvironment = null) : ControllerBase
{
    private const int MaximumTitleLength = 150;
    private const int MaximumSlugLength = 120;
    private const int MaximumExcerptLength = 320;
    private const int MaximumCategoryLength = 64;
    private const int MaximumMarkdownLength = 20_000;
    private const int MaximumHeroImageAltLength = 200;
    private const string HeroImageStorageDirectoryName = "field-report-images";

    /// <summary>
    /// Returns published field reports for the public site.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<FieldReportSummaryDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [HttpGet(Name = nameof(GetPublishedFieldReportsAsync))]
    public async Task<IActionResult> GetPublishedFieldReportsAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 24,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 100)
            return BadRequest();

        var query = CreatePublishedQuery();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryPattern = category.Trim();
            query = query.Where(q => q.Category == categoryPattern);
        }

        var reports = await query
            .OrderByDescending(q => q.IsFeatured)
            .ThenBy(q => q.SortOrder)
            .ThenByDescending(q => q.TimeStampPublished)
            .ThenBy(q => q.Title)
            .Skip(skip)
            .Take(take)
            .Select(q => ToSummaryDto(q))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(reports);
    }

    /// <summary>
    /// Returns one published field report by slug.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<FieldReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{slug}", Name = nameof(GetPublishedFieldReportAsync))]
    public async Task<IActionResult> GetPublishedFieldReportAsync(
        [FromRoute] string slug,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var report = await CreatePublishedQuery()
            .SingleOrDefaultAsync(q => q.Slug == normalizedSlug, cancellationToken)
            .ConfigureAwait(false);
        return report is null ? NotFound() : Ok(ToDto(report));
    }

    /// <summary>
    /// Returns one editable field report for Command Center.
    /// </summary>
    [Authorize]
    [ProducesResponseType<CommandCenterFieldReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("admin/{reportId:guid}", Name = nameof(GetCommandCenterFieldReportAsync))]
    public async Task<IActionResult> GetCommandCenterFieldReportAsync(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken = default)
    {
        if (!CanReadFieldReports())
            return Forbid();

        var report = await CreateAdminQuery()
            .SingleOrDefaultAsync(q => q.PrimaryKey == reportId, cancellationToken)
            .ConfigureAwait(false);
        if (report is null)
            return NotFound();

        var auditRecordCount = await apiDbContext.FieldReportAudits
            .AsNoTracking()
            .CountAsync(q => q.FieldReportFk == reportId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(ToCommandCenterDto(report, auditRecordCount));
    }

    /// <summary>
    /// Creates a field report.
    /// </summary>
    [Authorize]
    [ProducesResponseType<CommandCenterFieldReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [HttpPost(Name = nameof(CreateFieldReportAsync))]
    public async Task<IActionResult> CreateFieldReportAsync(
        [FromBody] FieldReportUpsertPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanCreateFieldReports())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!TryNormalizePayload(payload, out var normalized, out var error))
            return BadRequest(error);
        if (!await MediaAssetExistsAsync(normalized.HeroImageAssetFk, cancellationToken).ConfigureAwait(false))
            return BadRequest("Hero image asset does not exist.");
        if (await SlugExistsAsync(normalized.Slug, null, cancellationToken).ConfigureAwait(false))
            return Conflict();

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var report = new FieldReport
        {
            PrimaryKey         = Guid.NewGuid(),
            Slug               = normalized.Slug,
            Title              = normalized.Title,
            Excerpt            = normalized.Excerpt,
            Category           = normalized.Category,
            Markdown           = normalized.Markdown,
            HeroImageAssetFk   = normalized.HeroImageAssetFk,
            HeroImageAlt       = normalized.HeroImageAlt,
            IsFeatured         = normalized.IsFeatured,
            SortOrder          = normalized.SortOrder,
            IsPublished        = normalized.IsPublished,
            TimeStampPublished = normalized.IsPublished ? normalized.TimeStampPublished ?? now : null,
            IsDeleted          = false,
            TimeStampCreated   = now,
            TimeStampUpdated   = now,
            CreatedByFk        = userId,
            UpdatedByFk        = userId,
        };

        await apiDbContext.FieldReports.AddAsync(report, cancellationToken).ConfigureAwait(false);
        await AddAuditAsync(report.PrimaryKey, userId, EFieldReportAuditAction.Created, now, new
            {
                report.Slug,
                report.Title,
            }, cancellationToken)
            .ConfigureAwait(false);
        if (report.IsPublished)
        {
            await AddAuditAsync(report.PrimaryKey, userId, EFieldReportAuditAction.Published, now, new
                {
                    report.TimeStampPublished,
                }, cancellationToken)
                .ConfigureAwait(false);
        }

        await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        report = await CreateAdminQuery()
            .SingleAsync(q => q.PrimaryKey == report.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        return Ok(ToCommandCenterDto(report, auditRecordCount: 1 + (report.IsPublished ? 1 : 0)));
    }

    /// <summary>
    /// Updates a field report.
    /// </summary>
    [Authorize]
    [ProducesResponseType<CommandCenterFieldReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [HttpPut("admin/{reportId:guid}", Name = nameof(UpdateFieldReportAsync))]
    public async Task<IActionResult> UpdateFieldReportAsync(
        [FromRoute] Guid reportId,
        [FromBody] FieldReportUpsertPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanModifyFieldReports())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!TryNormalizePayload(payload, out var normalized, out var error))
            return BadRequest(error);
        if (!await MediaAssetExistsAsync(normalized.HeroImageAssetFk, cancellationToken).ConfigureAwait(false))
            return BadRequest("Hero image asset does not exist.");

        var report = await apiDbContext.FieldReports
            .SingleOrDefaultAsync(q => q.PrimaryKey == reportId, cancellationToken)
            .ConfigureAwait(false);
        if (report is null)
            return NotFound();
        if (await SlugExistsAsync(normalized.Slug, reportId, cancellationToken).ConfigureAwait(false))
            return Conflict();

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var changedFields = GetChangedFields(report, normalized);
        var wasPublished = report.IsPublished;

        Apply(report, normalized, userId, now);
        if (report.IsPublished && report.TimeStampPublished is null)
            report.TimeStampPublished = now;
        if (!report.IsPublished)
            report.TimeStampPublished = null;

        if (changedFields.Count > 0)
        {
            await AddAuditAsync(report.PrimaryKey, userId, EFieldReportAuditAction.Updated, now, new
                {
                    Fields = changedFields,
                }, cancellationToken)
                .ConfigureAwait(false);
        }
        if (!wasPublished && report.IsPublished)
        {
            await AddAuditAsync(report.PrimaryKey, userId, EFieldReportAuditAction.Published, now, new
                {
                    report.TimeStampPublished,
                }, cancellationToken)
                .ConfigureAwait(false);
        }
        if (wasPublished && !report.IsPublished)
        {
            await AddAuditAsync(report.PrimaryKey, userId, EFieldReportAuditAction.Unpublished, now, new
                {
                    Reason = "Updated as draft",
                }, cancellationToken)
                .ConfigureAwait(false);
        }

        await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(await ReadCommandCenterDtoAsync(report.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Publishes a field report.
    /// </summary>
    [Authorize]
    [ProducesResponseType<CommandCenterFieldReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("admin/{reportId:guid}/publish", Name = nameof(PublishFieldReportAsync))]
    public async Task<IActionResult> PublishFieldReportAsync(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken = default)
        => await SetPublicationStateAsync(reportId, isPublished: true, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Unpublishes a field report.
    /// </summary>
    [Authorize]
    [ProducesResponseType<CommandCenterFieldReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("admin/{reportId:guid}/unpublish", Name = nameof(UnpublishFieldReportAsync))]
    public async Task<IActionResult> UnpublishFieldReportAsync(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken = default)
        => await SetPublicationStateAsync(reportId, isPublished: false, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Removes a field report from the active editorial workflow.
    /// </summary>
    [Authorize]
    [ProducesResponseType<CommandCenterFieldReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpDelete("admin/{reportId:guid}", Name = nameof(DeleteFieldReportAsync))]
    public async Task<IActionResult> DeleteFieldReportAsync(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken = default)
        => await SetDeletedStateAsync(reportId, isDeleted: true, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Restores a deleted field report.
    /// </summary>
    [Authorize]
    [ProducesResponseType<CommandCenterFieldReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("admin/{reportId:guid}/restore", Name = nameof(RestoreFieldReportAsync))]
    public async Task<IActionResult> RestoreFieldReportAsync(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken = default)
        => await SetDeletedStateAsync(reportId, isDeleted: false, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Uploads a hero image for a field report draft.
    /// </summary>
    [Authorize]
    [ProducesResponseType<FieldReportImageUploadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [Consumes("multipart/form-data")]
    [HttpPost("admin/uploads/hero-image", Name = nameof(UploadHeroImageAsync))]
    public async Task<IActionResult> UploadHeroImageAsync(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!CanCreateFieldReports())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (mediaAssetService is null)
            return Problem("Media asset service is not configured.");

        try
        {
            var asset = await mediaAssetService
                .UploadAsync(file!, ["field-report"], userId, cancellationToken)
                .ConfigureAwait(false);

            return Ok(new FieldReportImageUploadDto
            {
                ImageAssetFk = asset.PrimaryKey,
                ImagePath   = asset.ImagePath,
                ContentType = asset.ContentType,
                Size        = asset.Size,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Returns a stored field report hero image.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("media/{fileName}", Name = nameof(GetHeroImage))]
    public IActionResult GetHeroImage([FromRoute] string fileName)
    {
        if (!IsSafeStoredHeroImageFileName(fileName))
            return NotFound();
        if (GetStoredHeroImageContentType(fileName) is not { } contentType)
            return NotFound();

        var storagePath = GetHeroImageStoragePath();
        var filePath = Path.GetFullPath(Path.Combine(storagePath, fileName));
        if (!IsPathInDirectory(storagePath, filePath) || !System.IO.File.Exists(filePath))
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    private IQueryable<FieldReport> CreatePublishedQuery()
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        return apiDbContext.FieldReports
            .AsNoTracking()
            .Where(q => q.IsPublished
                        && !q.IsDeleted
                        && q.TimeStampPublished != null
                        && q.TimeStampPublished <= now);
    }

    private IQueryable<FieldReport> CreateAdminQuery()
        => apiDbContext.FieldReports
            .AsNoTracking()
            .Include(q => q.CreatedBy)
            .Include(q => q.UpdatedBy);

    private async Task<IActionResult> SetPublicationStateAsync(
        Guid reportId,
        bool isPublished,
        CancellationToken cancellationToken)
    {
        if (!CanModifyFieldReports())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var report = await apiDbContext.FieldReports
            .SingleOrDefaultAsync(q => q.PrimaryKey == reportId, cancellationToken)
            .ConfigureAwait(false);
        if (report is null)
            return NotFound();
        if (report.IsPublished == isPublished)
            return Ok(await ReadCommandCenterDtoAsync(report.PrimaryKey, cancellationToken).ConfigureAwait(false));

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        report.IsPublished = isPublished;
        report.TimeStampPublished = isPublished ? now : null;
        report.TimeStampUpdated = now;
        report.UpdatedByFk = userId;
        await AddAuditAsync(
                report.PrimaryKey,
                userId,
                isPublished ? EFieldReportAuditAction.Published : EFieldReportAuditAction.Unpublished,
                now,
                isPublished ? new { report.TimeStampPublished } : new { Reason = "Manual command" },
                cancellationToken
            )
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(await ReadCommandCenterDtoAsync(report.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    private async Task<IActionResult> SetDeletedStateAsync(
        Guid reportId,
        bool isDeleted,
        CancellationToken cancellationToken)
    {
        if (!CanDeleteFieldReports())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var report = await apiDbContext.FieldReports
            .SingleOrDefaultAsync(q => q.PrimaryKey == reportId, cancellationToken)
            .ConfigureAwait(false);
        if (report is null)
            return NotFound();
        if (report.IsDeleted == isDeleted)
            return Ok(await ReadCommandCenterDtoAsync(report.PrimaryKey, cancellationToken).ConfigureAwait(false));

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        report.IsDeleted = isDeleted;
        report.TimeStampUpdated = now;
        report.UpdatedByFk = userId;
        await AddAuditAsync(
                report.PrimaryKey,
                userId,
                isDeleted ? EFieldReportAuditAction.Deleted : EFieldReportAuditAction.Restored,
                now,
                new { },
                cancellationToken
            )
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(await ReadCommandCenterDtoAsync(report.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    private async Task<CommandCenterFieldReportDto> ReadCommandCenterDtoAsync(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var report = await CreateAdminQuery()
            .SingleAsync(q => q.PrimaryKey == reportId, cancellationToken)
            .ConfigureAwait(false);
        var auditRecordCount = await apiDbContext.FieldReportAudits
            .AsNoTracking()
            .CountAsync(q => q.FieldReportFk == reportId, cancellationToken)
            .ConfigureAwait(false);
        return ToCommandCenterDto(report, auditRecordCount);
    }

    private async Task<bool> SlugExistsAsync(
        string slug,
        Guid? exceptReportId,
        CancellationToken cancellationToken)
        => await apiDbContext.FieldReports
            .AsNoTracking()
            .AnyAsync(
                q => q.Slug == slug && (exceptReportId == null || q.PrimaryKey != exceptReportId.Value),
                cancellationToken
            )
            .ConfigureAwait(false);

    private async Task<bool> MediaAssetExistsAsync(Guid? assetId, CancellationToken cancellationToken)
        => assetId is null
           || await apiDbContext.MediaAssets
               .AsNoTracking()
               .AnyAsync(q => q.PrimaryKey == assetId.Value, cancellationToken)
               .ConfigureAwait(false);

    private async Task AddAuditAsync(
        Guid reportId,
        Guid userId,
        EFieldReportAuditAction action,
        Instant now,
        object data,
        CancellationToken cancellationToken)
        => await apiDbContext.FieldReportAudits.AddAsync(
                new FieldReportAudit
                {
                    FieldReportFk    = reportId,
                    UserForeignKey   = userId,
                    Action           = action,
                    Data             = JsonSerializer.Serialize(data),
                    TimeStampCreated = now,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

    private static void Apply(
        FieldReport report,
        NormalizedFieldReportPayload normalized,
        Guid userId,
        Instant now)
    {
        report.Slug               = normalized.Slug;
        report.Title              = normalized.Title;
        report.Excerpt            = normalized.Excerpt;
        report.Category           = normalized.Category;
        report.Markdown           = normalized.Markdown;
        report.HeroImageAssetFk   = normalized.HeroImageAssetFk;
        report.HeroImageAlt       = normalized.HeroImageAlt;
        report.IsFeatured         = normalized.IsFeatured;
        report.SortOrder          = normalized.SortOrder;
        report.IsPublished        = normalized.IsPublished;
        report.TimeStampPublished = normalized.TimeStampPublished;
        report.TimeStampUpdated   = now;
        report.UpdatedByFk        = userId;
    }

    private static List<string> GetChangedFields(FieldReport report, NormalizedFieldReportPayload normalized)
    {
        var changedFields = new List<string>();
        AddIfChanged(changedFields, nameof(FieldReport.Slug), report.Slug, normalized.Slug);
        AddIfChanged(changedFields, nameof(FieldReport.Title), report.Title, normalized.Title);
        AddIfChanged(changedFields, nameof(FieldReport.Excerpt), report.Excerpt, normalized.Excerpt);
        AddIfChanged(changedFields, nameof(FieldReport.Category), report.Category, normalized.Category);
        AddIfChanged(changedFields, nameof(FieldReport.Markdown), report.Markdown, normalized.Markdown);
        if (report.HeroImageAssetFk != normalized.HeroImageAssetFk)
            changedFields.Add(nameof(FieldReport.HeroImageAssetFk));
        AddIfChanged(changedFields, nameof(FieldReport.HeroImageAlt), report.HeroImageAlt, normalized.HeroImageAlt);
        if (report.IsFeatured != normalized.IsFeatured)
            changedFields.Add(nameof(FieldReport.IsFeatured));
        if (report.SortOrder != normalized.SortOrder)
            changedFields.Add(nameof(FieldReport.SortOrder));
        if (report.TimeStampPublished != normalized.TimeStampPublished)
            changedFields.Add(nameof(FieldReport.TimeStampPublished));
        return changedFields;
    }

    private static void AddIfChanged(List<string> changedFields, string fieldName, string? current, string? next)
    {
        if (!string.Equals(current, next, StringComparison.Ordinal))
            changedFields.Add(fieldName);
    }

    private static bool TryNormalizePayload(
        FieldReportUpsertPayload payload,
        out NormalizedFieldReportPayload normalized,
        out string error)
    {
        normalized = default;
        error = string.Empty;

        var title = NormalizeRequired(payload.Title, MaximumTitleLength, nameof(payload.Title), out error);
        if (title is null)
            return false;
        var excerpt = NormalizeRequired(payload.Excerpt, MaximumExcerptLength, nameof(payload.Excerpt), out error);
        if (excerpt is null)
            return false;
        var category = NormalizeRequired(payload.Category, MaximumCategoryLength, nameof(payload.Category), out error);
        if (category is null)
            return false;
        var markdown = NormalizeRequired(payload.Markdown, MaximumMarkdownLength, nameof(payload.Markdown), out error);
        if (markdown is null)
            return false;

        var slug = string.IsNullOrWhiteSpace(payload.Slug)
            ? CreateSlug(title)
            : NormalizeSlug(payload.Slug);
        if (slug.Length is < 1 or > MaximumSlugLength)
        {
            error = "Slug is invalid.";
            return false;
        }

        var heroImageAlt = NormalizeOptional(payload.HeroImageAlt, MaximumHeroImageAltLength, nameof(payload.HeroImageAlt), out error);
        if (error.Length > 0)
            return false;

        normalized = new NormalizedFieldReportPayload(
            slug,
            title,
            excerpt,
            category,
            markdown,
            payload.HeroImageAssetFk,
            heroImageAlt,
            payload.IsFeatured,
            payload.SortOrder,
            payload.IsPublished,
            payload.TimeStampPublished
        );
        return true;
    }

    private static string? NormalizeRequired(string? value, int maximumLength, string fieldName, out string error)
    {
        error = string.Empty;
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = $"{fieldName} is required.";
            return null;
        }
        if (normalized.Length > maximumLength)
        {
            error = $"{fieldName} is too long.";
            return null;
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string fieldName, out string error)
    {
        error = string.Empty;
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length > maximumLength)
        {
            error = $"{fieldName} is too long.";
            return null;
        }

        return normalized;
    }

    private string GetHeroImageStoragePath()
    {
        var contentRootPath = string.IsNullOrWhiteSpace(webHostEnvironment?.ContentRootPath)
            ? AppContext.BaseDirectory
            : webHostEnvironment.ContentRootPath;
        return Path.Combine(contentRootPath, "App_Data", HeroImageStorageDirectoryName);
    }

    private static string? GetStoredHeroImageContentType(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".avif" => "image/avif",
            ".bmp"  => "image/bmp",
            ".gif"  => "image/gif",
            ".jpg"  => "image/jpeg",
            ".png"  => "image/png",
            ".webp" => "image/webp",
            _       => null,
        };

    private static bool IsSafeStoredHeroImageFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;
        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            return false;
        if (GetStoredHeroImageContentType(fileName) is null)
            return false;

        var identifier = Path.GetFileNameWithoutExtension(fileName);
        return identifier.Length == 32 && identifier.All(IsLowerHexCharacter);
    }

    private static bool IsLowerHexCharacter(char value)
        => value is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static bool IsPathInDirectory(string directoryPath, string filePath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedDirectory = Path.GetFullPath(directoryPath)
                                      .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                  + Path.DirectorySeparatorChar;
        var normalizedFilePath = Path.GetFullPath(filePath);
        return normalizedFilePath.StartsWith(normalizedDirectory, comparison);
    }

    private static string NormalizeSlug(string value)
        => CreateSlug(value);

    private static string CreateSlug(string value)
    {
        var builder = new StringBuilder();
        var previousWasSeparator = false;
        foreach (var c in value.Trim())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }

            if (builder.Length >= MaximumSlugLength)
                break;
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "field-report" : slug;
    }

    internal static FieldReportSummaryDto ToSummaryDto(FieldReport report)
        => new()
        {
            PrimaryKey          = report.PrimaryKey,
            Slug                = report.Slug,
            Title               = report.Title,
            Excerpt             = report.Excerpt,
            Category            = report.Category,
            HeroImageAssetFk    = report.HeroImageAssetFk,
            HeroImagePath       = report.HeroImageAssetFk is { } assetId
                ? IMediaAssetService.CreateImagePath(assetId)
                : null,
            HeroImageAlt        = report.HeroImageAlt,
            IsFeatured          = report.IsFeatured,
            SortOrder           = report.SortOrder,
            TimeStampPublished  = report.TimeStampPublished,
            TimeStampUpdated    = report.TimeStampUpdated,
        };

    internal static FieldReportDto ToDto(FieldReport report)
        => new()
        {
            PrimaryKey          = report.PrimaryKey,
            Slug                = report.Slug,
            Title               = report.Title,
            Excerpt             = report.Excerpt,
            Category            = report.Category,
            Markdown            = report.Markdown,
            HeroImageAssetFk    = report.HeroImageAssetFk,
            HeroImagePath       = report.HeroImageAssetFk is { } assetId
                ? IMediaAssetService.CreateImagePath(assetId)
                : null,
            HeroImageAlt        = report.HeroImageAlt,
            IsFeatured          = report.IsFeatured,
            SortOrder           = report.SortOrder,
            TimeStampPublished  = report.TimeStampPublished,
            TimeStampUpdated    = report.TimeStampUpdated,
        };

    internal static CommandCenterFieldReportDto ToCommandCenterDto(FieldReport report, int auditRecordCount)
        => new()
        {
            PrimaryKey          = report.PrimaryKey,
            Slug                = report.Slug,
            Title               = report.Title,
            Excerpt             = report.Excerpt,
            Category            = report.Category,
            Markdown            = report.Markdown,
            HeroImageAssetFk    = report.HeroImageAssetFk,
            HeroImagePath       = report.HeroImageAssetFk is { } assetId
                ? IMediaAssetService.CreateImagePath(assetId)
                : null,
            HeroImageAlt        = report.HeroImageAlt,
            IsFeatured          = report.IsFeatured,
            SortOrder           = report.SortOrder,
            IsPublished         = report.IsPublished,
            IsDeleted           = report.IsDeleted,
            TimeStampPublished  = report.TimeStampPublished,
            TimeStampCreated    = report.TimeStampCreated,
            TimeStampUpdated    = report.TimeStampUpdated,
            CreatedByName       = report.CreatedBy?.Nickname,
            UpdatedByName       = report.UpdatedBy?.Nickname,
            AuditRecordCount    = auditRecordCount,
        };

    private bool CanReadFieldReports()
        => HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Wiki, Claims.Creation.Wiki)
           || HasBaseClaim(Claims.Wiki.All);

    private bool CanCreateFieldReports()
        => CanReadFieldReports();

    private bool CanModifyFieldReports()
        => HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Wiki, Claims.Wiki.Modify)
           || HasBaseClaim(Claims.Wiki.All);

    private bool CanDeleteFieldReports()
        => HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Wiki, Claims.Wiki.Delete)
           || HasBaseClaim(Claims.Wiki.All);

    private bool HasAnyEmptyClaim(params string[] claimIdentifiers)
        => claimIdentifiers.Any(claimIdentifier => User.HasClaim(claimIdentifier, string.Empty));

    private bool HasBaseClaim(string claimIdentifier)
        => User.HasClaim(claimIdentifier, string.Empty);

    private readonly record struct NormalizedFieldReportPayload(
        string Slug,
        string Title,
        string Excerpt,
        string Category,
        string Markdown,
        Guid? HeroImageAssetFk,
        string? HeroImageAlt,
        bool IsFeatured,
        int SortOrder,
        bool IsPublished,
        Instant? TimeStampPublished);
}
