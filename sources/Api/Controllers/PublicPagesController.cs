using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Content;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads.PublicPages;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.Media;
using X39.UnitedTacticalForces.Common.PublicPages;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Controller responsible for public, command-center editable pages.
/// </summary>
[ApiController]
[Route(Constants.Routes.PublicPages)]
public class PublicPagesController(DatabaseContext apiDbContext) : ControllerBase
{
    private const int MaximumSlugLength = 128;
    private const int MaximumTitleLength = 180;
    private const int MaximumSummaryLength = 512;
    private const int MaximumHeroKickerLength = 96;
    private const int MaximumHeroTitleLength = 180;
    private const int MaximumHeroDescriptionLength = 512;
    private const int MaximumCommentLength = 512;
    private const int MaximumMarkdownLength = 60_000;
    private const int MaximumContentJsonLength = 120_000;
    private const string GdprSlug = "gdpr";

    /// <summary>
    /// Returns the latest published revision for a public page.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<PublishedPublicPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{slug}", Name = nameof(GetPublishedPageAsync))]
    public async Task<IActionResult> GetPublishedPageAsync(
        [FromRoute] string slug,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeSlug(slug, out var normalizedSlug))
            return NotFound();

        var revision = await GetLatestPublishedRevisionQuery(normalizedSlug)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision?.Page is null ? NotFound() : Ok(ToPublishedPageDto(revision.Page, revision));
    }

    /// <summary>
    /// Returns public pages for administrative workflows.
    /// </summary>
    [Authorize]
    [ProducesResponseType<PublicPageDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("admin", Name = nameof(GetAdminPagesAsync))]
    public async Task<IActionResult> GetAdminPagesAsync(CancellationToken cancellationToken = default)
    {
        if (!CanManagePages())
            return Forbid();

        var pages = await apiDbContext.PublicPages
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.Revisions!)
            .ThenInclude(q => q.Author)
            .Where(q => !q.IsDeleted)
            .OrderBy(q => q.Slug)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(pages.Select(ToAdminPageDto).ToArray());
    }

    /// <summary>
    /// Returns one public page for administrative workflows.
    /// </summary>
    [Authorize]
    [ProducesResponseType<PublicPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("admin/{slug}", Name = nameof(GetAdminPageAsync))]
    public async Task<IActionResult> GetAdminPageAsync(
        [FromRoute] string slug,
        CancellationToken cancellationToken = default)
    {
        if (!CanManagePages())
            return Forbid();
        if (!TryNormalizeSlug(slug, out var normalizedSlug))
            return NotFound();

        var page = await apiDbContext.PublicPages
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.Revisions!)
            .ThenInclude(q => q.Author)
            .SingleOrDefaultAsync(q => q.Slug == normalizedSlug && !q.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        return page is null ? NotFound() : Ok(ToAdminPageDto(page));
    }

    /// <summary>
    /// Creates the next revision for a public page, creating the page when the slug is new.
    /// </summary>
    [Authorize]
    [ProducesResponseType<PublicPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpPost("admin/{slug}/revisions", Name = nameof(CreatePageRevisionAsync))]
    public async Task<IActionResult> CreatePageRevisionAsync(
        [FromRoute] string slug,
        [FromBody] PublicPageRevisionPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManagePages())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!TryNormalizeSlug(slug, out var normalizedSlug))
            return BadRequest("Invalid page slug.");
        if (!TryNormalizePagePayload(normalizedSlug, payload, out var normalized, out var error))
            return BadRequest(error);
        if (!await MediaAssetExistsAsync(normalized.HeroImageAssetFk, cancellationToken).ConfigureAwait(false))
            return BadRequest("Hero image asset does not exist.");

        var page = await apiDbContext.PublicPages
            .Include(q => q.Revisions)
            .SingleOrDefaultAsync(q => q.Slug == normalizedSlug && !q.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var isNewPage = page is null;
        if (page is null)
        {
            page = new PublicPage
            {
                Slug             = normalizedSlug,
                IsDeleted        = false,
                TimeStampCreated = now,
                TimeStampUpdated = now,
                CreatedByFk      = userId,
                UpdatedByFk      = userId,
            };
            await apiDbContext.PublicPages.AddAsync(page, cancellationToken)
                .ConfigureAwait(false);
        }

        var revision = new PublicPageRevision
        {
            Page                = isNewPage ? page : null,
            PageFk              = page.PrimaryKey,
            Version             = (page.Revisions?.Select(q => q.Version).DefaultIfEmpty().Max() ?? 0) + 1,
            Title               = normalized.Title,
            Summary             = normalized.Summary,
            HeroKicker          = normalized.HeroKicker,
            HeroTitle           = normalized.HeroTitle,
            HeroDescription     = normalized.HeroDescription,
            HeroImageAssetFk    = normalized.HeroImageAssetFk,
            Markdown            = normalized.Markdown,
            ContentJson         = normalized.ContentJson,
            Comment             = normalized.Comment,
            IsPublished         = normalized.IsPublished,
            TimeStampPublished  = normalized.IsPublished ? now : null,
            TimeStampCreated    = now,
            AuthorFk            = userId,
        };
        page.TimeStampUpdated = now;
        page.UpdatedByFk      = userId;
        await apiDbContext.PublicPageRevisions.AddAsync(revision, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        await AddPageAuditAsync(
                page.PrimaryKey,
                revision.PrimaryKey,
                userId,
                isNewPage ? EPublicPageAuditAction.Created : EPublicPageAuditAction.RevisionCreated,
                now,
                new
                {
                    page.Slug,
                    revision.Version,
                    revision.IsPublished,
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (revision.IsPublished)
        {
            await AddPageAuditAsync(
                    page.PrimaryKey,
                    revision.PrimaryKey,
                    userId,
                    EPublicPageAuditAction.Published,
                    now,
                    new
                    {
                        revision.TimeStampPublished,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadAdminPageDtoAsync(page.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Publishes a public page revision.
    /// </summary>
    [Authorize]
    [ProducesResponseType<PublicPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("admin/revisions/{revisionId:long}/publish", Name = nameof(PublishPageRevisionAsync))]
    public async Task<IActionResult> PublishPageRevisionAsync(
        [FromRoute] long revisionId,
        CancellationToken cancellationToken = default)
        => await SetRevisionPublicationStateAsync(revisionId, true, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Unpublishes a public page revision.
    /// </summary>
    [Authorize]
    [ProducesResponseType<PublicPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("admin/revisions/{revisionId:long}/unpublish", Name = nameof(UnpublishPageRevisionAsync))]
    public async Task<IActionResult> UnpublishPageRevisionAsync(
        [FromRoute] long revisionId,
        CancellationToken cancellationToken = default)
        => await SetRevisionPublicationStateAsync(revisionId, false, cancellationToken).ConfigureAwait(false);

    internal static PublishedPublicPageDto ToPublishedPageDto(PublicPage page, PublicPageRevision? revision)
        => new()
        {
            PrimaryKey       = page.PrimaryKey,
            Slug             = page.Slug,
            Revision         = revision is null ? null : ToPublishedRevisionDto(revision),
            TimeStampUpdated = page.TimeStampUpdated,
        };

    internal static PublicPageDto ToAdminPageDto(PublicPage page)
    {
        var revisions = (page.Revisions ?? [])
            .OrderByDescending(q => q.Version)
            .Select(ToRevisionDto)
            .ToArray();
        return new PublicPageDto
        {
            PrimaryKey       = page.PrimaryKey,
            Slug             = page.Slug,
            IsDeleted        = page.IsDeleted,
            Revision         = revisions.FirstOrDefault(),
            Revisions        = revisions,
            TimeStampCreated = page.TimeStampCreated,
            TimeStampUpdated = page.TimeStampUpdated,
        };
    }

    internal static PublicPageRevisionDto ToRevisionDto(PublicPageRevision revision)
        => new()
        {
            PrimaryKey         = revision.PrimaryKey,
            PageFk             = revision.PageFk,
            Version            = revision.Version,
            Title              = revision.Title,
            Summary            = revision.Summary,
            HeroKicker         = revision.HeroKicker,
            HeroTitle          = revision.HeroTitle,
            HeroDescription    = revision.HeroDescription,
            HeroImageAssetFk   = revision.HeroImageAssetFk,
            HeroImagePath      = IMediaAssetService.CreateImagePath(revision.HeroImageAssetFk),
            Markdown           = revision.Markdown,
            ContentJson        = revision.ContentJson,
            Comment            = revision.Comment,
            IsPublished        = revision.IsPublished,
            TimeStampPublished = revision.TimeStampPublished,
            TimeStampCreated   = revision.TimeStampCreated,
            AuthorFk           = revision.AuthorFk,
            AuthorName         = revision.Author?.Nickname,
        };

    internal static PublishedPublicPageRevisionDto ToPublishedRevisionDto(PublicPageRevision revision)
        => new()
        {
            Version            = revision.Version,
            Title              = revision.Title,
            Summary            = revision.Summary,
            HeroKicker         = revision.HeroKicker,
            HeroTitle          = revision.HeroTitle,
            HeroDescription    = revision.HeroDescription,
            HeroImageAssetFk   = revision.HeroImageAssetFk,
            HeroImagePath      = IMediaAssetService.CreateImagePath(revision.HeroImageAssetFk),
            Markdown           = revision.Markdown,
            ContentJson        = revision.ContentJson,
            TimeStampPublished = revision.TimeStampPublished,
        };

    /// <summary>
    /// Public page shape returned to anonymous readers.
    /// </summary>
    public sealed record PublishedPublicPageDto
    {
        /// <summary>
        /// Page primary key.
        /// </summary>
        public long PrimaryKey { get; init; }

        /// <summary>
        /// Stable URL slug.
        /// </summary>
        public string Slug { get; init; } = string.Empty;

        /// <summary>
        /// Latest published revision.
        /// </summary>
        public PublishedPublicPageRevisionDto? Revision { get; init; }

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public Instant TimeStampUpdated { get; init; }
    }

    /// <summary>
    /// Public page revision shape returned to anonymous readers.
    /// </summary>
    public sealed record PublishedPublicPageRevisionDto
    {
        /// <summary>
        /// Page-local version number.
        /// </summary>
        public int Version { get; init; }

        /// <summary>
        /// Display title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Short summary.
        /// </summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>
        /// Small label rendered above the hero title.
        /// </summary>
        public string HeroKicker { get; init; } = string.Empty;

        /// <summary>
        /// Hero title.
        /// </summary>
        public string HeroTitle { get; init; } = string.Empty;

        /// <summary>
        /// Hero description.
        /// </summary>
        public string HeroDescription { get; init; } = string.Empty;

        /// <summary>
        /// Gallery media asset used as hero background image.
        /// </summary>
        public Guid HeroImageAssetFk { get; init; }

        /// <summary>
        /// Computed API-relative hero image render path.
        /// </summary>
        public string HeroImagePath { get; init; } = string.Empty;

        /// <summary>
        /// Markdown body.
        /// </summary>
        public string Markdown { get; init; } = string.Empty;

        /// <summary>
        /// Optional structured content payload used by template-backed public pages.
        /// </summary>
        public string ContentJson { get; init; } = string.Empty;

        /// <summary>
        /// Publication timestamp.
        /// </summary>
        public Instant? TimeStampPublished { get; init; }
    }

    private IQueryable<PublicPageRevision> GetLatestPublishedRevisionQuery(string slug)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        return apiDbContext.PublicPageRevisions
            .AsNoTracking()
            .Include(q => q.Page)
            .Include(q => q.Author)
            .Where(q => q.Page!.Slug == slug
                        && !q.Page.IsDeleted
                        && q.IsPublished
                        && q.TimeStampPublished <= now)
            .OrderByDescending(q => q.TimeStampPublished)
            .ThenByDescending(q => q.Version);
    }

    private async Task<IActionResult> SetRevisionPublicationStateAsync(
        long revisionId,
        bool isPublished,
        CancellationToken cancellationToken)
    {
        if (!CanManagePages())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var revision = await apiDbContext.PublicPageRevisions
            .Include(q => q.Page)
            .SingleOrDefaultAsync(q => q.PrimaryKey == revisionId && !q.Page!.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (revision is null || revision.Page is null)
            return NotFound();

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        revision.IsPublished        = isPublished;
        revision.TimeStampPublished = isPublished ? revision.TimeStampPublished ?? now : null;
        revision.Page.TimeStampUpdated = now;
        revision.Page.UpdatedByFk      = userId;
        await AddPageAuditAsync(
                revision.PageFk,
                revision.PrimaryKey,
                userId,
                isPublished ? EPublicPageAuditAction.Published : EPublicPageAuditAction.Unpublished,
                now,
                new
                {
                    revision.Version,
                    revision.TimeStampPublished,
                },
                cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadAdminPageDtoAsync(revision.PageFk, cancellationToken).ConfigureAwait(false));
    }

    private async Task<PublicPageDto> ReadAdminPageDtoAsync(long pageId, CancellationToken cancellationToken)
    {
        var page = await apiDbContext.PublicPages
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.Revisions!)
            .ThenInclude(q => q.Author)
            .SingleAsync(q => q.PrimaryKey == pageId, cancellationToken)
            .ConfigureAwait(false);
        return ToAdminPageDto(page);
    }

    private bool CanManagePages()
        => HasAnyEmptyClaim(Claims.Administrative.All)
           || HasBaseClaim(Claims.Content.Pages);

    private bool HasAnyEmptyClaim(params string[] claimIdentifiers)
        => claimIdentifiers.Any(claimIdentifier => User.HasClaim(claimIdentifier, string.Empty));

    private bool HasBaseClaim(string claimIdentifier)
        => User.HasClaim(claimIdentifier, string.Empty);

    private async Task AddPageAuditAsync(
        long pageId,
        long? revisionId,
        Guid? actorId,
        EPublicPageAuditAction action,
        Instant now,
        object data,
        CancellationToken cancellationToken)
        => await apiDbContext.PublicPageAudits.AddAsync(new PublicPageAudit
            {
                PageFk           = pageId,
                RevisionFk       = revisionId,
                UserForeignKey   = actorId,
                Action           = action,
                Data             = JsonSerializer.Serialize(data),
                TimeStampCreated = now,
            },
            cancellationToken).ConfigureAwait(false);

    private static bool TryNormalizeSlug(string slug, out string normalizedSlug)
    {
        normalizedSlug = slug.Trim().ToLowerInvariant();
        return normalizedSlug.Length is > 0 and <= MaximumSlugLength
               && normalizedSlug.All(q => char.IsAsciiLetterOrDigit(q) || q == '-')
               && !normalizedSlug.StartsWith("-", StringComparison.Ordinal)
               && !normalizedSlug.EndsWith("-", StringComparison.Ordinal);
    }

    private static bool TryNormalizePagePayload(
        string slug,
        PublicPageRevisionPayload payload,
        out NormalizedPagePayload normalized,
        out string error)
    {
        var title = (payload.Title ?? string.Empty).Trim();
        var summary = (payload.Summary ?? string.Empty).Trim();
        var heroKicker = (payload.HeroKicker ?? string.Empty).Trim();
        var heroTitle = string.IsNullOrWhiteSpace(payload.HeroTitle)
            ? title
            : payload.HeroTitle.Trim();
        var heroDescription = string.IsNullOrWhiteSpace(payload.HeroDescription)
            ? summary
            : payload.HeroDescription.Trim();
        var markdown = (payload.Markdown ?? string.Empty).Trim();
        var contentJson = (payload.ContentJson ?? string.Empty).Trim();
        var comment = (payload.Comment ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(title) || title.Length > MaximumTitleLength)
        {
            normalized = default;
            error = "Invalid page title.";
            return false;
        }
        if (summary.Length > MaximumSummaryLength)
        {
            normalized = default;
            error = "Invalid page summary.";
            return false;
        }
        if (heroKicker.Length > MaximumHeroKickerLength)
        {
            normalized = default;
            error = "Invalid hero kicker.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(heroTitle) || heroTitle.Length > MaximumHeroTitleLength)
        {
            normalized = default;
            error = "Invalid hero title.";
            return false;
        }
        if (heroDescription.Length > MaximumHeroDescriptionLength)
        {
            normalized = default;
            error = "Invalid hero description.";
            return false;
        }
        if (payload.HeroImageAssetFk == default)
        {
            normalized = default;
            error = "Hero image asset is required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(markdown) || markdown.Length > MaximumMarkdownLength)
        {
            normalized = default;
            error = "Invalid page markdown.";
            return false;
        }
        if (contentJson.Length > MaximumContentJsonLength)
        {
            normalized = default;
            error = "Invalid structured page content.";
            return false;
        }
        if (!TryNormalizeStructuredContent(slug, contentJson, out contentJson, out error))
        {
            normalized = default;
            return false;
        }
        if (contentJson.Length > MaximumContentJsonLength)
        {
            normalized = default;
            error = "Invalid structured page content.";
            return false;
        }
        if (comment.Length > MaximumCommentLength)
        {
            normalized = default;
            error = "Invalid revision comment.";
            return false;
        }

        normalized = new NormalizedPagePayload(
            title,
            summary,
            heroKicker,
            heroTitle,
            heroDescription,
            payload.HeroImageAssetFk,
            markdown,
            contentJson,
            comment,
            payload.IsPublished);
        error = string.Empty;
        return true;
    }

    private static bool TryNormalizeStructuredContent(
        string slug,
        string contentJson,
        out string normalizedContentJson,
        out string error)
    {
        normalizedContentJson = contentJson;
        error = string.Empty;
        if (!string.Equals(slug, GdprSlug, StringComparison.Ordinal))
            return true;

        if (GdprPageContentJson.TryCanonicalize(contentJson, out normalizedContentJson))
            return true;

        error = "Invalid structured page content.";
        return false;
    }

    private async Task<bool> MediaAssetExistsAsync(Guid assetId, CancellationToken cancellationToken)
        => assetId != default
           && await apiDbContext.MediaAssets
               .AsNoTracking()
               .AnyAsync(q => q.PrimaryKey == assetId, cancellationToken)
               .ConfigureAwait(false);

    private readonly record struct NormalizedPagePayload(
        string Title,
        string Summary,
        string HeroKicker,
        string HeroTitle,
        string HeroDescription,
        Guid HeroImageAssetFk,
        string Markdown,
        string ContentJson,
        string Comment,
        bool IsPublished);
}
