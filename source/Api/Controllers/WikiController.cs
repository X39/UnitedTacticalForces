using System.Net;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Wiki;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Offers access to the knowledge base endpoints.
/// </summary>
[ApiController]
[Route(Constants.Routes.Wiki)]
public class WikiController : ControllerBase
{
    private readonly ApiDbContext _apiDbContext;

    /// <summary>
    /// Partial <see cref="WikiPage"/>, containing the minimal information required to display a list of pages.
    /// </summary>
    /// <param name="PrimaryKey">The primary key of the <see cref="WikiPage"/>.</param>
    /// <param name="Name">The name of the <see cref="WikiPage"/>.</param>
    /// <param name="LastModified">Timestamp of when this page was last modified.</param>
    [PublicAPI]
    public record WikiPageHeader(Guid PrimaryKey, string Name, DateTimeOffset LastModified);

    /// <summary>
    /// Partial <see cref="WikiPageRevision"/>, containing the minimal information required to display a list of revisions.
    /// </summary>
    /// <param name="PrimaryKey">The primary key of the <see cref="WikiPageRevision"/>.</param>
    /// <param name="TimeStampCreated">Timestamp of when this revision was created.</param>
    /// <param name="AuthorPrimaryKey">The primary key of the author of this revision.</param>
    /// <param name="AuthorName">The name of the author of this revision.</param>
    /// <param name="Comment">The comment of this revision.</param>
    [PublicAPI]
    public record WikiPageRevisionHeader(
        Guid PrimaryKey,
        DateTimeOffset TimeStampCreated,
        Guid AuthorPrimaryKey,
        string AuthorName,
        string Comment);


    /// <summary>
    /// Creates a new instance of <see cref="WikiController"/>.
    /// </summary>
    /// <param name="apiDbContext">The <see cref="ApiDbContext"/> to use.</param>
    public WikiController(ApiDbContext apiDbContext)
    {
        _apiDbContext = apiDbContext;
    }


    /// <summary>
    /// Returns a list of all wiki pages, with minimal information, sorted by name.
    /// </summary>
    /// <param name="skip">The number of pages to skip.</param>
    /// <param name="take">The number of pages to take.</param>
    /// <param name="showDeleted">If true, deleted pages will be included in the result.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use. Injected by the ASP.Net Framework.</param>
    /// <returns>A list of <see cref="WikiPageHeader"/>s.</returns>
    [HttpGet("all/header", Name = nameof(WikiController) + "." + nameof(GetAllWikiPageHeadersAsync))]
    [AllowAnonymous]
    public async Task<IEnumerable<WikiPageHeader>> GetAllWikiPageHeadersAsync(
        [FromQuery] int? skip = null,
        [FromQuery] int? take = null,
        [FromQuery] bool? showDeleted = null,
        CancellationToken cancellationToken = default)
    {
        var origQuery = _apiDbContext.WikiPages.AsSplitQuery();
        if (showDeleted is not true)
            origQuery = origQuery.Where((q) => !q.IsDeleted);

        var query = from wikiPage in origQuery
            select new
            {
                wikiPage.PrimaryKey,
                wikiPage.Title,
                LastModified = wikiPage.Revisions.Max(revision => revision.TimeStampCreated),
            };
        query = query.OrderBy((q) => q.Title);
        if (skip is not null)
            query = query.Skip(skip.Value);
        if (take is not null)
            query = query.Take(take.Value);
        var result = await query.ToArrayAsync(cancellationToken);
        return result.Select((a) => new WikiPageHeader(a.PrimaryKey, a.Title, a.LastModified));
    }

    /// <summary>
    /// Returns a list of revisions for a specific page, with minimal information, sorted by timestamp (descending).
    /// </summary>
    /// <param name="pageKey">The primary key of the <see cref="WikiPage"/>.</param>
    /// <param name="skip">The number of revisions to skip.</param>
    /// <param name="take">The number of revisions to take.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use. Injected by the ASP.Net Framework.</param>
    /// <returns></returns>
    [HttpGet(
        "{pageKey:guid}/revisions/header",
        Name = nameof(WikiController) + "." + nameof(GetWikiPageRevisionHeadersAsync))]
    [AllowAnonymous]
    public async Task<IEnumerable<WikiPageRevisionHeader>> GetWikiPageRevisionHeadersAsync(
        [FromRoute] Guid pageKey,
        [FromQuery] int? skip = null,
        [FromQuery] int? take = null,
        CancellationToken cancellationToken = default)
    {
        var query = from wikiPage in _apiDbContext.WikiPages
            where wikiPage.PrimaryKey == pageKey
            from wikiPageRevision in wikiPage.Revisions
            select new
            {
                wikiPageRevision.PrimaryKey,
                wikiPageRevision.TimeStampCreated,
                AuthorPrimaryKey = wikiPageRevision.Author.PrimaryKey,
                AuthorName       = wikiPageRevision.Author.Nickname,
                wikiPageRevision.Comment,
            };
        query = query
            .OrderByDescending((q) => q.TimeStampCreated);
        if (skip is not null)
            query = query.Skip(skip.Value);
        if (take is not null)
            query = query.Take(take.Value);
        var result = await query.ToArrayAsync(cancellationToken);
        return result.Select(
            (a) => new WikiPageRevisionHeader(
                a.PrimaryKey,
                a.TimeStampCreated,
                a.AuthorPrimaryKey,
                a.AuthorName,
                a.Comment));
    }

    /// <summary>
    /// Returns a specific wiki page, with the latest revision and the author of that revision.
    /// </summary>
    /// <param name="pageKey">The primary key of the <see cref="WikiPage"/>.</param>
    /// <param name="allRevisions">If true, all revisions will be included in the result.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use. Injected by the ASP.Net Framework.</param>
    /// <returns>The <see cref="WikiPage"/>.</returns>
    [HttpGet("{pageKey:guid}", Name = nameof(WikiController) + "." + nameof(GetWikiPageAsync))]
    [ProducesResponseType(typeof(WikiPage), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int)HttpStatusCode.NotFound)]
    [AllowAnonymous]
    public async Task<ActionResult<WikiPage>> GetWikiPageAsync(
        [FromRoute] Guid pageKey,
        [FromQuery] bool? allRevisions = null,
        CancellationToken cancellationToken = default)
    {
        var query = _apiDbContext.WikiPages.AsSplitQuery();
        if (allRevisions is true)
        {
            query = query.Include((e) => e.Revisions!)
                .ThenInclude((e) => e.Author);
        }
        else
        {
            query = query.Include((wikiPage) => wikiPage.Revisions!.MaxBy((q) => q.TimeStampCreated))
                .ThenInclude((wikiPageRevision) => wikiPageRevision!.Author);
        }
        var result = await query
            .SingleOrDefaultAsync((wikiPage) => wikiPage.PrimaryKey == pageKey, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Returns a specific revision of a wiki page, with the author of that revision.
    /// </summary>
    /// <param name="pageKey">The primary key of the <see cref="WikiPage"/>.</param>
    /// <param name="revisionKey">The primary key of the <see cref="WikiPageRevision"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use. Injected by the ASP.Net Framework.</param>
    /// <returns>The <see cref="WikiPageRevision"/>.</returns>
    [HttpGet(
        "{pageKey:guid}/revisions/{revisionKey:guid}",
        Name = nameof(WikiController) + "." + nameof(GetWikiPageRevisionAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(WikiPageRevision), (int) HttpStatusCode.OK)]
    [AllowAnonymous]
    public async Task<ActionResult<WikiPageRevision>> GetWikiPageRevisionAsync(
        [FromRoute] Guid pageKey,
        [FromRoute] Guid revisionKey,
        CancellationToken cancellationToken = default)
    {
        var result = await _apiDbContext.WikiPageRevisions
            .Include((wikiPageRevision) => wikiPageRevision.Author)
            .SingleOrDefaultAsync(
                (wikiPageRevision) => wikiPageRevision.PrimaryKey == revisionKey
                                      && wikiPageRevision.PageForeignKey == pageKey,
                cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Creates a new wiki page.
    /// </summary>
    /// <param name="title">The title of the page.</param>
    /// <param name="markdown">The markdown content of the page.</param>
    /// <param name="comment">The comment for the first revision.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use. Injected by the ASP.Net Framework.</param>
    /// <returns>The created <see cref="WikiPage"/>.</returns>
    [HttpPost(Name = nameof(WikiController) + "." + nameof(CreateWikiPageAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(WikiPage), (int) HttpStatusCode.OK)]
    [Authorize(Claims.Creation.Wiki)]
    public async Task<ActionResult<WikiPage>> CreateWikiPageAsync(
        [FromForm] string title,
        [FromForm] string markdown,
        [FromForm] string comment,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (title.IsNullOrWhiteSpace())
            return BadRequest();
        var now = DateTime.UtcNow;
        var pageGuid = Guid.NewGuid();
        var page = new WikiPage
        {
            PrimaryKey       = pageGuid,
            Title            = title,
            TimeStampCreated = now,
            Revisions = new List<WikiPageRevision>
            {
                new()
                {
                    TimeStampCreated = now,
                    AuthorForeignKey = userId,
                    Comment          = comment,
                    Author           = default,
                    PrimaryKey       = Guid.NewGuid(),
                    PageForeignKey   = pageGuid,
                    Markdown         = markdown,
                    Page             = default,
                },
            },
        };
        await _apiDbContext.WikiPages.AddAsync(page, cancellationToken);
        await _apiDbContext.WikiPageAudits.AddAsync(
            new WikiPageAudit
            {
                PageForeignKey   = pageGuid,
                UserForeignKey   = userId,
                Page             = default,
                User             = default,
                Data             = "{}",
                Action           = EWikiPageAuditAction.Created,
                TimeStampCreated = now,
                PrimaryKey       = default,
            },
            cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// Creates a new revision for a wiki page.
    /// </summary>
    /// <param name="pageKey">The primary key of the <see cref="WikiPage"/>.</param>
    /// <param name="markdown">The markdown content of the page.</param>
    /// <param name="comment">The comment for the first revision.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use. Injected by the ASP.Net Framework.</param>
    /// <returns>The created <see cref="WikiPage"/>.</returns>
    [HttpPost(
        "{pageKey:guid}/revisions",
        Name = nameof(WikiController) + "." + nameof(CreateWikiPageRevisionAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(WikiPageRevision), (int) HttpStatusCode.OK)]
    [Authorize(Claims.Wiki.Modify)]
    public async Task<ActionResult<WikiPageRevision>> CreateWikiPageRevisionAsync(
        [FromRoute] Guid pageKey,
        [FromForm] string markdown,
        [FromForm] string comment,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var now = DateTimeOffset.UtcNow;
        var pageExists = await _apiDbContext.WikiPages.AnyAsync(
            (wikiPage) => wikiPage.PrimaryKey == pageKey,
            cancellationToken);
        if (!pageExists)
            return NotFound();
        var revision = new WikiPageRevision
        {
            TimeStampCreated = now,
            AuthorForeignKey = userId,
            Comment          = comment,
            Author           = default,
            PrimaryKey       = Guid.NewGuid(),
            PageForeignKey   = pageKey,
            Markdown         = markdown,
            Page             = default,
        };
        await _apiDbContext.WikiPageRevisions.AddAsync(revision, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(revision);
    }

    /// <summary>
    /// Changes the title of a wiki page.
    /// </summary>
    /// <param name="pageKey">The primary key of the <see cref="WikiPage"/>.</param>
    /// <param name="title">The new title of the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use. Injected by the ASP.Net Framework.</param>
    /// <returns>The updated <see cref="WikiPage"/>.</returns>
    [HttpPut(
        "{pageKey:guid}",
        Name = nameof(WikiController) + "." + nameof(RenameWikiPageAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(WikiPage), (int) HttpStatusCode.OK)]
    [Authorize(Claims.Wiki.Rename)]
    public async Task<ActionResult<WikiPage>> RenameWikiPageAsync(
        [FromRoute] Guid pageKey,
        [FromForm] string title,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (title.IsNullOrWhiteSpace())
            return BadRequest();
        var page = await _apiDbContext.WikiPages
            .Include((wikiPage) => wikiPage.Revisions!)
            .ThenInclude((wikiPageRevision) => wikiPageRevision.Author)
            .SingleOrDefaultAsync((wikiPage) => wikiPage.PrimaryKey == pageKey, cancellationToken);
        if (page is null)
            return NotFound();
        await _apiDbContext.WikiPageAudits.AddAsync(
            new WikiPageAudit
            {
                PageForeignKey   = pageKey,
                UserForeignKey   = userId,
                Page             = default,
                User             = default,
                Data             = $$$"""{"title":{"old":"{{{page.Title}}}","new":"{{{title}}}"}}""",
                Action           = EWikiPageAuditAction.TitleChanged,
                TimeStampCreated = DateTimeOffset.UtcNow,
                PrimaryKey       = default,
            },
            cancellationToken);
        page.Title = title;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// Removes a wiki page from the list of active pages.
    /// </summary>
    /// <param name="pageKey">The primary key of the <see cref="WikiPage"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use. Injected by the ASP.Net Framework.</param>
    /// <returns>The updated <see cref="WikiPage"/>.</returns>
    [HttpDelete(
        "{pageKey:guid}",
        Name = nameof(WikiController) + "." + nameof(DeleteWikiPageAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(WikiPage), (int) HttpStatusCode.OK)]
    [Authorize(Claims.Wiki.Delete)]
    public async Task<ActionResult<WikiPage>> DeleteWikiPageAsync(
        [FromRoute] Guid pageKey,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var page = await _apiDbContext.WikiPages
            .SingleOrDefaultAsync((wikiPage) => wikiPage.PrimaryKey == pageKey, cancellationToken);
        if (page is null)
            return NotFound();
        if (page.IsDeleted)
            return BadRequest();
        page.IsDeleted = true;
        await _apiDbContext.WikiPageAudits.AddAsync(
            new WikiPageAudit
            {
                PageForeignKey   = pageKey,
                UserForeignKey   = userId,
                Page             = default,
                User             = default,
                Data             = "{}",
                Action           = EWikiPageAuditAction.Deleted,
                TimeStampCreated = DateTimeOffset.UtcNow,
                PrimaryKey       = default,
            },
            cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// Restore a wiki page to the list of active pages.
    /// </summary>
    /// <param name="pageKey">The primary key of the <see cref="WikiPage"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use. Injected by the ASP.Net Framework.</param>
    /// <returns>The updated <see cref="WikiPage"/>.</returns>
    [HttpPost(
        "{pageKey:guid}/restore",
        Name = nameof(WikiController) + "." + nameof(RestoreWikiPageAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(WikiPage), (int) HttpStatusCode.OK)]
    [Authorize(Claims.Administrative.Wiki)]
    public async Task<ActionResult<WikiPage>> RestoreWikiPageAsync(
        [FromRoute] Guid pageKey,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var page = await _apiDbContext.WikiPages
            .SingleOrDefaultAsync((wikiPage) => wikiPage.PrimaryKey == pageKey, cancellationToken);
        if (page is null)
            return NotFound();
        if (!page.IsDeleted)
            return BadRequest();
        page.IsDeleted = false;
        await _apiDbContext.WikiPageAudits.AddAsync(
            new WikiPageAudit
            {
                PageForeignKey   = pageKey,
                UserForeignKey   = userId,
                Page             = default,
                User             = default,
                Data             = "{}",
                Action           = EWikiPageAuditAction.Restored,
                TimeStampCreated = DateTimeOffset.UtcNow,
                PrimaryKey       = default,
            },
            cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(page);
    }
}