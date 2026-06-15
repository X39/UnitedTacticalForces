using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Verein;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads.Verein;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Controller responsible for Verein documents, formal memberships, and Kassenbuch records.
/// </summary>
[ApiController]
[Route(Constants.Routes.Verein)]
public class VereinController(DatabaseContext apiDbContext) : ControllerBase
{
    private const int MaximumDocumentTitleLength = 180;
    private const int MaximumDocumentSummaryLength = 512;
    private const int MaximumDocumentCommentLength = 512;
    private const int MaximumDocumentMarkdownLength = 60_000;
    private const int MaximumMembershipNotesLength = 2048;
    private const int MaximumMemberNumberLength = 64;
    private const int MaximumMembershipRequestNameLength = 256;
    private const int MaximumMembershipRequestEmailLength = 1024;
    private const int MaximumMembershipRequestPhoneLength = 64;
    private const int MaximumMembershipRequestAddressLineLength = 512;
    private const int MaximumMembershipRequestPostalCodeLength = 32;
    private const int MaximumMembershipRequestCityLength = 256;
    private const int MaximumMembershipRequestCountryLength = 128;
    private const int MaximumMembershipRequestMotivationLength = 4096;
    private const int MaximumMembershipRequestDecisionReasonLength = 512;
    private const int MaximumSettingsCurrencyLength = 3;
    private const int MaximumSettingsPublicTextLength = 4096;
    private const int MaximumLedgerCategoryLength = 96;
    private const int MaximumLedgerDescriptionLength = 512;
    private const int MaximumLedgerReferenceLength = 128;
    private const int MaximumLedgerDocumentNumberLength = 128;
    private const string DefaultContributionCurrency = "EUR";
    private const string DefaultContributionTextDe =
        "Eine formale Vereinsmitgliedschaft hilft, laufende Infrastruktur-, Verwaltungs- und Betriebskosten zu tragen. Der aktuelle Beitrag wird vom Verein bestaetigt, bevor dein Antrag abgeschlossen wird.";
    private const string DefaultContributionTextEn =
        "Formal Verein membership helps cover ongoing infrastructure, administration, and operating costs. The current contribution is confirmed by the Verein before your request is completed.";

    /// <summary>
    /// Returns public Verein settings for Join and Verein pages.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<VereinSettingsDto>(StatusCodes.Status200OK)]
    [HttpGet("settings", Name = nameof(GetSettingsAsync))]
    public async Task<IActionResult> GetSettingsAsync(CancellationToken cancellationToken = default)
        => Ok(ToSettingsDto(await ReadSettingsAsync(cancellationToken).ConfigureAwait(false)));

    /// <summary>
    /// Returns admin-editable public Verein settings.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("admin/settings", Name = nameof(GetAdminSettingsAsync))]
    public async Task<IActionResult> GetAdminSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!CanManageMemberships())
            return Forbid();

        return Ok(ToSettingsDto(await ReadSettingsAsync(cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>
    /// Updates public Verein settings shown during membership application.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpPut("admin/settings", Name = nameof(UpdateSettingsAsync))]
    public async Task<IActionResult> UpdateSettingsAsync(
        [FromBody] VereinSettingsPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMemberships())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!TryNormalizeSettingsPayload(payload, out var normalized, out var error))
            return BadRequest(error);

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var settings = await apiDbContext.VereinSettings
            .SingleOrDefaultAsync(q => q.Identifier == VereinSettings.DefaultIdentifier, cancellationToken)
            .ConfigureAwait(false);
        var isNew = settings is null;
        if (settings is null)
        {
            settings = new VereinSettings
            {
                Identifier       = VereinSettings.DefaultIdentifier,
                TimeStampCreated = now,
            };
            await apiDbContext.VereinSettings.AddAsync(settings, cancellationToken)
                .ConfigureAwait(false);
        }

        settings.MembershipContributionMonthlyAmount = normalized.MonthlyAmount;
        settings.MembershipContributionAnnualAmount  = normalized.AnnualAmount;
        settings.MembershipContributionCurrency     = normalized.Currency;
        settings.MembershipContributionPublicTextDe = normalized.PublicTextDe;
        settings.MembershipContributionPublicTextEn = normalized.PublicTextEn;
        settings.TimeStampUpdated                   = now;
        settings.UpdatedByFk                        = userId;

        await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await AddSettingsAuditAsync(
                settings.PrimaryKey,
                userId,
                isNew ? EVereinSettingsAuditAction.Created : EVereinSettingsAuditAction.Updated,
                now,
                new
                {
                    settings.MembershipContributionMonthlyAmount,
                    settings.MembershipContributionAnnualAmount,
                    settings.MembershipContributionCurrency,
                    HasGermanText  = !string.IsNullOrWhiteSpace(settings.MembershipContributionPublicTextDe),
                    HasEnglishText = !string.IsNullOrWhiteSpace(settings.MembershipContributionPublicTextEn),
                },
                cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(ToSettingsDto(settings));
    }

    /// <summary>
    /// Returns the latest published Satzung revision.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<PublishedVereinDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("documents/satzung", Name = nameof(GetPublishedSatzungAsync))]
    public async Task<IActionResult> GetPublishedSatzungAsync(CancellationToken cancellationToken = default)
    {
        var revision = await GetLatestPublishedRevisionQuery(EVereinDocumentKind.Satzung)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision?.Document is null ? NotFound() : Ok(ToPublishedDocumentDto(revision.Document, revision));
    }

    /// <summary>
    /// Returns published Beschluss and minutes documents.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<PublishedVereinDocumentDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [HttpGet("documents/minutes", Name = nameof(GetPublishedMinutesAsync))]
    public async Task<IActionResult> GetPublishedMinutesAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 24,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 100)
            return BadRequest();

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var documents = await apiDbContext.VereinDocuments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.Revisions!)
            .ThenInclude(q => q.Author)
            .Where(q => q.Kind == EVereinDocumentKind.Minutes && !q.IsDeleted)
            .OrderByDescending(q => q.TimeStampUpdated)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = documents
            .Select(document => new
            {
                Document = document,
                Revision = document.Revisions?
                    .Where(q => q.IsPublished && q.TimeStampPublished <= now)
                    .OrderByDescending(q => q.TimeStampPublished)
                    .ThenByDescending(q => q.Version)
                    .FirstOrDefault(),
            })
            .Where(q => q.Revision is not null)
            .OrderByDescending(q => q.Revision!.TimeStampPublished)
            .Skip(skip)
            .Take(take)
            .Select(q => ToPublishedDocumentDto(q.Document, q.Revision!))
            .ToArray();

        return Ok(result);
    }

    /// <summary>
    /// Returns one published Beschluss or minutes document.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<PublishedVereinDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("documents/minutes/{documentId:long}", Name = nameof(GetPublishedMinuteAsync))]
    public async Task<IActionResult> GetPublishedMinuteAsync(
        [FromRoute] long documentId,
        CancellationToken cancellationToken = default)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var document = await apiDbContext.VereinDocuments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.Revisions!)
            .ThenInclude(q => q.Author)
            .SingleOrDefaultAsync(
                q => q.PrimaryKey == documentId && q.Kind == EVereinDocumentKind.Minutes && !q.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
        var revision = document?.Revisions?
            .Where(q => q.IsPublished && q.TimeStampPublished <= now)
            .OrderByDescending(q => q.TimeStampPublished)
            .ThenByDescending(q => q.Version)
            .FirstOrDefault();
        return document is null || revision is null ? NotFound() : Ok(ToPublishedDocumentDto(document, revision));
    }

    /// <summary>
    /// Returns Verein documents for administrative workflows.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinDocumentDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("admin/documents", Name = nameof(GetAdminDocumentsAsync))]
    public async Task<IActionResult> GetAdminDocumentsAsync(
        [FromQuery] EVereinDocumentKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageDocuments())
            return Forbid();

        var query = apiDbContext.VereinDocuments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.Revisions!)
            .ThenInclude(q => q.Author)
            .OrderByDescending(q => q.TimeStampUpdated)
            .AsQueryable();
        if (kind is not null)
            query = query.Where(q => q.Kind == kind.Value);

        var documents = await query.ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(documents.Select(ToAdminDocumentDto).ToArray());
    }

    /// <summary>
    /// Creates a Verein document with an initial revision.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpPost("admin/documents/{kind}", Name = nameof(CreateDocumentAsync))]
    public async Task<IActionResult> CreateDocumentAsync(
        [FromRoute] EVereinDocumentKind kind,
        [FromBody] VereinDocumentRevisionPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageDocuments())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!TryNormalizeDocumentPayload(payload, out var normalized, out var error))
            return BadRequest(error);

        if (kind == EVereinDocumentKind.Satzung
            && await apiDbContext.VereinDocuments.AnyAsync(
                q => q.Kind == EVereinDocumentKind.Satzung && !q.IsDeleted,
                cancellationToken).ConfigureAwait(false))
        {
            return BadRequest("Only one active Satzung document is supported. Add a revision to the existing document.");
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var document = new VereinDocument
        {
            Kind             = kind,
            IsDeleted        = false,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            CreatedByFk      = userId,
            UpdatedByFk      = userId,
            Revisions =
            [
                new VereinDocumentRevision
                {
                    Version            = 1,
                    Title              = normalized.Title,
                    Summary            = normalized.Summary,
                    Markdown           = normalized.Markdown,
                    Comment            = normalized.Comment,
                    IsPublished        = normalized.IsPublished,
                    TimeStampPublished = normalized.IsPublished ? now : null,
                    TimeStampCreated   = now,
                    AuthorFk           = userId,
                },
            ],
        };
        await apiDbContext.VereinDocuments.AddAsync(document, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        var revision = document.Revisions!.Single();
        await AddDocumentAuditAsync(document.PrimaryKey, revision.PrimaryKey, userId, EVereinDocumentAuditAction.Created, now, new
            {
                document.Kind,
                revision.Version,
                revision.IsPublished,
            }, cancellationToken)
            .ConfigureAwait(false);
        if (revision.IsPublished)
        {
            await AddDocumentAuditAsync(document.PrimaryKey, revision.PrimaryKey, userId, EVereinDocumentAuditAction.Published, now, new
                {
                    revision.TimeStampPublished,
                }, cancellationToken)
                .ConfigureAwait(false);
        }
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadAdminDocumentDtoAsync(document.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Creates the next revision for an existing Verein document.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("admin/documents/{documentId:long}/revisions", Name = nameof(CreateDocumentRevisionAsync))]
    public async Task<IActionResult> CreateDocumentRevisionAsync(
        [FromRoute] long documentId,
        [FromBody] VereinDocumentRevisionPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageDocuments())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!TryNormalizeDocumentPayload(payload, out var normalized, out var error))
            return BadRequest(error);

        var document = await apiDbContext.VereinDocuments
            .Include(q => q.Revisions)
            .SingleOrDefaultAsync(q => q.PrimaryKey == documentId && !q.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
            return NotFound();

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var revision = new VereinDocumentRevision
        {
            DocumentFk          = document.PrimaryKey,
            Version             = (document.Revisions?.Select(q => q.Version).DefaultIfEmpty().Max() ?? 0) + 1,
            Title               = normalized.Title,
            Summary             = normalized.Summary,
            Markdown            = normalized.Markdown,
            Comment             = normalized.Comment,
            IsPublished         = normalized.IsPublished,
            TimeStampPublished  = normalized.IsPublished ? now : null,
            TimeStampCreated    = now,
            AuthorFk            = userId,
        };
        document.TimeStampUpdated = now;
        document.UpdatedByFk      = userId;
        await apiDbContext.VereinDocumentRevisions.AddAsync(revision, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        await AddDocumentAuditAsync(document.PrimaryKey, revision.PrimaryKey, userId, EVereinDocumentAuditAction.RevisionCreated, now, new
            {
                revision.Version,
                revision.IsPublished,
            }, cancellationToken)
            .ConfigureAwait(false);
        if (revision.IsPublished)
        {
            await AddDocumentAuditAsync(document.PrimaryKey, revision.PrimaryKey, userId, EVereinDocumentAuditAction.Published, now, new
                {
                    revision.TimeStampPublished,
                }, cancellationToken)
                .ConfigureAwait(false);
        }
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadAdminDocumentDtoAsync(document.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Publishes a document revision.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("admin/documents/revisions/{revisionId:long}/publish", Name = nameof(PublishDocumentRevisionAsync))]
    public async Task<IActionResult> PublishDocumentRevisionAsync(
        [FromRoute] long revisionId,
        CancellationToken cancellationToken = default)
        => await SetRevisionPublicationStateAsync(revisionId, true, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Unpublishes a document revision.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("admin/documents/revisions/{revisionId:long}/unpublish", Name = nameof(UnpublishDocumentRevisionAsync))]
    public async Task<IActionResult> UnpublishDocumentRevisionAsync(
        [FromRoute] long revisionId,
        CancellationToken cancellationToken = default)
        => await SetRevisionPublicationStateAsync(revisionId, false, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Returns formal Verein memberships.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinMembershipDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("memberships", Name = nameof(GetMembershipsAsync))]
    public async Task<IActionResult> GetMembershipsAsync(
        [FromQuery] int take = 100,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMemberships())
            return Forbid();
        if (take is < 1 or > 500)
            return BadRequest();

        var query = apiDbContext.VereinMemberships
            .AsNoTracking()
            .Include(q => q.User)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(q => q.User!.Nickname.ToLower().Contains(normalizedSearch)
                                     || (q.MemberNumber != null && q.MemberNumber.ToLower().Contains(normalizedSearch)));
        }

        var memberships = await query
            .OrderBy(q => q.Status)
            .ThenBy(q => q.User!.Nickname)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(memberships.Select(ToMembershipDto).ToArray());
    }

    /// <summary>
    /// Returns the formal Verein membership for one exact user.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinMembershipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("memberships/{userId:guid}", Name = nameof(GetMembershipAsync))]
    public async Task<IActionResult> GetMembershipAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMemberships())
            return Forbid();

        var membership = await apiDbContext.VereinMemberships
            .AsNoTracking()
            .Include(q => q.User)
            .SingleOrDefaultAsync(q => q.UserFk == userId, cancellationToken)
            .ConfigureAwait(false);
        return membership is null ? NotFound() : Ok(ToMembershipDto(membership));
    }

    /// <summary>
    /// Creates or updates formal membership for a user.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinMembershipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [HttpPut("memberships/{userId:guid}", Name = nameof(UpsertMembershipAsync))]
    public async Task<IActionResult> UpsertMembershipAsync(
        [FromRoute] Guid userId,
        [FromBody] VereinMembershipUpdatePayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMemberships())
            return Forbid();
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!TryNormalizeMembershipPayload(payload, out var normalized, out var error))
            return BadRequest(error);

        var userExists = await apiDbContext.Users.AnyAsync(q => q.PrimaryKey == userId, cancellationToken)
            .ConfigureAwait(false);
        if (!userExists)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(normalized.MemberNumber))
        {
            var duplicateMemberNumber = await apiDbContext.VereinMemberships.AnyAsync(
                    q => q.UserFk != userId && q.MemberNumber == normalized.MemberNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            if (duplicateMemberNumber)
                return Conflict();
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var membership = await apiDbContext.VereinMemberships
            .SingleOrDefaultAsync(q => q.UserFk == userId, cancellationToken)
            .ConfigureAwait(false);
        var isNew = membership is null;
        if (membership is null)
        {
            membership = new VereinMembership
            {
                UserFk             = userId,
                TimeStampCreated   = now,
                TimeStampUpdated   = now,
                CreatedByFk        = actorId,
                UpdatedByFk        = actorId,
                TimeStampStarted   = normalized.TimeStampStarted ?? now,
            };
            await apiDbContext.VereinMemberships.AddAsync(membership, cancellationToken)
                .ConfigureAwait(false);
        }

        var previousStatus = membership.Status;
        membership.MemberNumber     = normalized.MemberNumber;
        membership.Status           = normalized.Status;
        membership.TimeStampStarted = normalized.TimeStampStarted ?? membership.TimeStampStarted;
        membership.TimeStampEnded = normalized.Status == EVereinMembershipStatus.Ended
            ? normalized.TimeStampEnded ?? now
            : normalized.TimeStampEnded;
        membership.Notes            = normalized.Notes;
        membership.TimeStampUpdated = now;
        membership.UpdatedByFk      = actorId;

        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        var action = isNew
            ? EVereinMembershipAuditAction.Created
            : normalized.Status switch
            {
                EVereinMembershipStatus.Active when previousStatus != EVereinMembershipStatus.Active => EVereinMembershipAuditAction.Activated,
                EVereinMembershipStatus.Suspended when previousStatus != EVereinMembershipStatus.Suspended => EVereinMembershipAuditAction.Suspended,
                EVereinMembershipStatus.Ended when previousStatus != EVereinMembershipStatus.Ended => EVereinMembershipAuditAction.Ended,
                _ => EVereinMembershipAuditAction.Updated,
            };
        await AddMembershipAuditAsync(membership.PrimaryKey, actorId, action, now, new
            {
                PreviousStatus = previousStatus,
                membership.Status,
                membership.MemberNumber,
                Reason = normalized.Reason,
            }, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        var dto = await apiDbContext.VereinMemberships
            .AsNoTracking()
            .Include(q => q.User)
            .SingleAsync(q => q.PrimaryKey == membership.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        return Ok(ToMembershipDto(dto));
    }

    /// <summary>
    /// Returns the current user's latest formal membership request.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<VereinMembershipRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("membership-requests/me", Name = nameof(GetMyMembershipRequestAsync))]
    public async Task<IActionResult> GetMyMembershipRequestAsync(CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var request = await apiDbContext.VereinMembershipRequests
            .AsNoTracking()
            .Include(q => q.User)
            .Include(q => q.ReviewedBy)
            .Where(q => q.UserFk == userId)
            .OrderByDescending(q => q.TimeStampCreated)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return request is null ? NotFound() : Ok(ToMembershipRequestDto(request));
    }

    /// <summary>
    /// Submits a formal Verein membership request for the current user.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<VereinMembershipRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [HttpPost("membership-requests", Name = nameof(CreateMembershipRequestAsync))]
    public async Task<IActionResult> CreateMembershipRequestAsync(
        [FromBody] VereinMembershipRequestPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!TryNormalizeMembershipRequestPayload(payload, out var normalized, out var error))
            return BadRequest(error);

        var userExists = await apiDbContext.Users.AnyAsync(q => q.PrimaryKey == userId, cancellationToken)
            .ConfigureAwait(false);
        if (!userExists)
            return NotFound();

        var isActiveMember = await apiDbContext.VereinMemberships.AsNoTracking()
            .AnyAsync(
                q => q.UserFk == userId && q.Status == EVereinMembershipStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);
        if (isActiveMember)
            return Conflict("Active Verein members cannot submit another membership request.");

        var hasOpenRequest = await apiDbContext.VereinMembershipRequests.AsNoTracking()
            .AnyAsync(
                q => q.UserFk == userId
                     && (q.Status == EVereinMembershipRequestStatus.Submitted
                         || q.Status == EVereinMembershipRequestStatus.AcceptedForContact),
                cancellationToken)
            .ConfigureAwait(false);
        if (hasOpenRequest)
            return Conflict("An open Verein membership request already exists.");

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var request = new VereinMembershipRequest
        {
            UserFk                               = userId,
            FirstName                            = normalized.FirstName,
            LastName                             = normalized.LastName,
            DateOfBirth                          = normalized.DateOfBirth,
            Email                                = normalized.Email,
            PhoneNumber                          = normalized.PhoneNumber,
            AddressLine1                         = normalized.AddressLine1,
            AddressLine2                         = normalized.AddressLine2,
            PostalCode                           = normalized.PostalCode,
            City                                 = normalized.City,
            Country                              = normalized.Country,
            Motivation                           = normalized.Motivation,
            AcknowledgesMembershipSeparation     = normalized.AcknowledgesMembershipSeparation,
            AcknowledgesDataProcessing           = normalized.AcknowledgesDataProcessing,
            AcknowledgesSatzung                  = normalized.AcknowledgesSatzung,
            Status                               = EVereinMembershipRequestStatus.Submitted,
            TimeStampCreated                     = now,
            TimeStampUpdated                     = now,
        };
        await apiDbContext.VereinMembershipRequests.AddAsync(request, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        await AddMembershipRequestAuditAsync(request.PrimaryKey, userId, EVereinMembershipRequestAuditAction.Created, now, new
            {
                request.Status,
            }, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadMembershipRequestDtoAsync(request.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Withdraws an open formal Verein membership request owned by the current user.
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType<VereinMembershipRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("membership-requests/{requestId:long}/withdraw", Name = nameof(WithdrawMembershipRequestAsync))]
    public async Task<IActionResult> WithdrawMembershipRequestAsync(
        [FromRoute] long requestId,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var request = await apiDbContext.VereinMembershipRequests
            .SingleOrDefaultAsync(q => q.PrimaryKey == requestId, cancellationToken)
            .ConfigureAwait(false);
        if (request is null)
            return NotFound();
        if (request.UserFk != userId)
            return Forbid();
        if (!IsOpenMembershipRequestStatus(request.Status))
            return BadRequest("Only submitted or contact-accepted requests can be withdrawn.");

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var previousStatus = request.Status;
        request.Status            = EVereinMembershipRequestStatus.Withdrawn;
        request.DecisionReason    = string.Empty;
        request.TimeStampUpdated  = now;
        request.TimeStampResolved = now;
        await AddMembershipRequestAuditAsync(request.PrimaryKey, userId, EVereinMembershipRequestAuditAction.Withdrawn, now, new
            {
                PreviousStatus = previousStatus,
                request.Status,
            }, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadMembershipRequestDtoAsync(request.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Returns formal Verein membership requests for administrative review.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinMembershipRequestDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("admin/membership-requests", Name = nameof(GetAdminMembershipRequestsAsync))]
    public async Task<IActionResult> GetAdminMembershipRequestsAsync(
        [FromQuery] EVereinMembershipRequestStatus? status = null,
        [FromQuery] int take = 100,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMemberships())
            return Forbid();
        if (take is < 1 or > 500)
            return BadRequest();

        var query = apiDbContext.VereinMembershipRequests
            .AsNoTracking()
            .Include(q => q.User)
            .Include(q => q.ReviewedBy)
            .AsQueryable();
        if (status is not null)
            query = query.Where(q => q.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(q => q.User!.Nickname.ToLower().Contains(normalizedSearch)
                                     || q.FirstName.ToLower().Contains(normalizedSearch)
                                     || q.LastName.ToLower().Contains(normalizedSearch)
                                     || q.Email.ToLower().Contains(normalizedSearch));
        }

        var requests = await query
            .OrderBy(q => q.Status == EVereinMembershipRequestStatus.Submitted ? 0 :
                q.Status == EVereinMembershipRequestStatus.AcceptedForContact ? 1 : 2)
            .ThenByDescending(q => q.TimeStampUpdated)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(requests.Select(ToMembershipRequestDto).ToArray());
    }

    /// <summary>
    /// Transitions a formal Verein membership request and creates membership on approval.
    /// </summary>
    [Authorize]
    [ProducesResponseType<VereinMembershipRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [HttpPost("admin/membership-requests/{requestId:long}/transition", Name = nameof(TransitionMembershipRequestAsync))]
    public async Task<IActionResult> TransitionMembershipRequestAsync(
        [FromRoute] long requestId,
        [FromBody] VereinMembershipRequestTransitionPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageMemberships())
            return Forbid();
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!TryNormalizeMembershipRequestTransitionPayload(payload, out var normalized, out var error))
            return BadRequest(error);

        await using var transaction = await apiDbContext.Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var request = await apiDbContext.VereinMembershipRequests
            .Include(q => q.User)
            .SingleOrDefaultAsync(q => q.PrimaryKey == requestId, cancellationToken)
            .ConfigureAwait(false);
        if (request is null)
            return NotFound();
        if (!IsOpenMembershipRequestStatus(request.Status))
            return BadRequest("Only submitted or contact-accepted requests can be reviewed.");
        if (normalized.Status == EVereinMembershipRequestStatus.AcceptedForContact
            && request.Status != EVereinMembershipRequestStatus.Submitted)
            return BadRequest("Only submitted requests can be accepted for contact.");

        if (normalized.Status == EVereinMembershipRequestStatus.Approved
            && !string.IsNullOrWhiteSpace(normalized.MemberNumber))
        {
            var duplicateMemberNumber = await apiDbContext.VereinMemberships.AnyAsync(
                    q => q.UserFk != request.UserFk && q.MemberNumber == normalized.MemberNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            if (duplicateMemberNumber)
                return Conflict();
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var previousStatus = request.Status;
        request.Status           = normalized.Status;
        request.DecisionReason   = normalized.Reason;
        request.ReviewedByFk     = actorId;
        request.TimeStampUpdated = now;
        request.TimeStampResolved = normalized.Status is EVereinMembershipRequestStatus.Approved
            or EVereinMembershipRequestStatus.Rejected
            ? now
            : null;

        if (normalized.Status == EVereinMembershipRequestStatus.Approved)
        {
            var membership = await apiDbContext.VereinMemberships
                .SingleOrDefaultAsync(q => q.UserFk == request.UserFk, cancellationToken)
                .ConfigureAwait(false);
            var isNew = membership is null;
            if (membership is null)
            {
                membership = new VereinMembership
                {
                    UserFk           = request.UserFk,
                    TimeStampCreated = now,
                    CreatedByFk      = actorId,
                    TimeStampStarted = now,
                };
                await apiDbContext.VereinMemberships.AddAsync(membership, cancellationToken)
                    .ConfigureAwait(false);
            }

            var previousMembershipStatus = membership.Status;
            membership.MemberNumber     = normalized.MemberNumber;
            membership.Status           = EVereinMembershipStatus.Active;
            membership.TimeStampStarted = membership.TimeStampStarted == default ? now : membership.TimeStampStarted;
            membership.TimeStampEnded   = null;
            membership.Notes            = normalized.Notes;
            membership.TimeStampUpdated = now;
            membership.UpdatedByFk      = actorId;
            await apiDbContext.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            await AddMembershipAuditAsync(
                    membership.PrimaryKey,
                    actorId,
                    isNew ? EVereinMembershipAuditAction.Created : EVereinMembershipAuditAction.Activated,
                    now,
                    new
                    {
                        PreviousStatus = previousMembershipStatus,
                        membership.Status,
                        membership.MemberNumber,
                        Reason = normalized.Reason,
                        MembershipRequestFk = request.PrimaryKey,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await AddMembershipRequestAuditAsync(
                request.PrimaryKey,
                actorId,
                normalized.Status switch
                {
                    EVereinMembershipRequestStatus.AcceptedForContact => EVereinMembershipRequestAuditAction.AcceptedForContact,
                    EVereinMembershipRequestStatus.Approved           => EVereinMembershipRequestAuditAction.Approved,
                    EVereinMembershipRequestStatus.Rejected           => EVereinMembershipRequestAuditAction.Rejected,
                    _                                                  => EVereinMembershipRequestAuditAction.Updated,
                },
                now,
                new
                {
                    PreviousStatus = previousStatus,
                    request.Status,
                    Reason = normalized.Reason,
                },
                cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadMembershipRequestDtoAsync(request.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Returns Kassenbuch entries and totals.
    /// </summary>
    [Authorize]
    [ProducesResponseType<KassenbuchPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("kassenbuch", Name = nameof(GetKassenbuchAsync))]
    public async Task<IActionResult> GetKassenbuchAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] Instant? from = null,
        [FromQuery] Instant? to = null,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 200)
            return BadRequest();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!await CanReadKassenbuchAsync(userId, cancellationToken).ConfigureAwait(false))
            return Forbid();

        var query = CreateKassenbuchQuery(from, to, search, category);
        var entries = await query
            .Include(q => q.CreatedBy)
            .Include(q => q.UpdatedBy)
            .OrderByDescending(q => q.TimeStampBooked)
            .ThenByDescending(q => q.PrimaryKey)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var totalEntries = await query.ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var incomeTotal = totalEntries
            .Where(q => q.Direction == EKassenbuchEntryDirection.Income)
            .Sum(q => q.Amount);
        var expenseTotal = totalEntries
            .Where(q => q.Direction == EKassenbuchEntryDirection.Expense)
            .Sum(q => q.Amount);

        return Ok(new KassenbuchPageDto
        {
            Entries      = entries.Select(ToKassenbuchEntryDto).ToArray(),
            TotalCount   = totalEntries.LongLength,
            IncomeTotal  = incomeTotal,
            ExpenseTotal = expenseTotal,
            Balance      = incomeTotal - expenseTotal,
            Currency     = totalEntries.Select(q => q.Currency).FirstOrDefault() ?? "EUR",
        });
    }

    /// <summary>
    /// Creates a Kassenbuch entry.
    /// </summary>
    [Authorize]
    [ProducesResponseType<KassenbuchEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpPost("kassenbuch", Name = nameof(CreateKassenbuchEntryAsync))]
    public async Task<IActionResult> CreateKassenbuchEntryAsync(
        [FromBody] KassenbuchEntryPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageKassenbuch())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!TryNormalizeKassenbuchPayload(payload, out var normalized, out var error))
            return BadRequest(error);

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var entry = new KassenbuchEntry
        {
            TimeStampBooked   = normalized.TimeStampBooked,
            Direction         = normalized.Direction,
            Amount            = normalized.Amount,
            Currency          = normalized.Currency,
            Category          = normalized.Category,
            Description       = normalized.Description,
            Reference         = normalized.Reference,
            DocumentNumber    = normalized.DocumentNumber,
            IsDeleted         = false,
            TimeStampCreated  = now,
            TimeStampUpdated  = now,
            CreatedByFk       = userId,
            UpdatedByFk       = userId,
        };
        await apiDbContext.KassenbuchEntries.AddAsync(entry, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        await AddKassenbuchAuditAsync(entry.PrimaryKey, userId, EKassenbuchAuditAction.Created, now, new
            {
                entry.Direction,
                entry.Amount,
                entry.Currency,
                entry.Category,
            }, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadKassenbuchEntryDtoAsync(entry.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Updates a Kassenbuch entry.
    /// </summary>
    [Authorize]
    [ProducesResponseType<KassenbuchEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPut("kassenbuch/{entryId:long}", Name = nameof(UpdateKassenbuchEntryAsync))]
    public async Task<IActionResult> UpdateKassenbuchEntryAsync(
        [FromRoute] long entryId,
        [FromBody] KassenbuchEntryPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageKassenbuch())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!TryNormalizeKassenbuchPayload(payload, out var normalized, out var error))
            return BadRequest(error);

        var entry = await apiDbContext.KassenbuchEntries
            .SingleOrDefaultAsync(q => q.PrimaryKey == entryId && !q.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (entry is null)
            return NotFound();

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        entry.TimeStampBooked  = normalized.TimeStampBooked;
        entry.Direction        = normalized.Direction;
        entry.Amount           = normalized.Amount;
        entry.Currency         = normalized.Currency;
        entry.Category         = normalized.Category;
        entry.Description      = normalized.Description;
        entry.Reference        = normalized.Reference;
        entry.DocumentNumber   = normalized.DocumentNumber;
        entry.TimeStampUpdated = now;
        entry.UpdatedByFk      = userId;
        await AddKassenbuchAuditAsync(entry.PrimaryKey, userId, EKassenbuchAuditAction.Updated, now, new
            {
                entry.Direction,
                entry.Amount,
                entry.Currency,
                entry.Category,
            }, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadKassenbuchEntryDtoAsync(entry.PrimaryKey, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Soft-deletes a Kassenbuch entry.
    /// </summary>
    [Authorize]
    [ProducesResponseType<KassenbuchEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpDelete("kassenbuch/{entryId:long}", Name = nameof(DeleteKassenbuchEntryAsync))]
    public async Task<IActionResult> DeleteKassenbuchEntryAsync(
        [FromRoute] long entryId,
        CancellationToken cancellationToken = default)
    {
        if (!CanManageKassenbuch())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var entry = await apiDbContext.KassenbuchEntries
            .SingleOrDefaultAsync(q => q.PrimaryKey == entryId && !q.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (entry is null)
            return NotFound();

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        entry.IsDeleted        = true;
        entry.TimeStampUpdated = now;
        entry.UpdatedByFk      = userId;
        await AddKassenbuchAuditAsync(entry.PrimaryKey, userId, EKassenbuchAuditAction.Deleted, now, new
            {
                entry.Amount,
                entry.Currency,
                entry.Category,
            }, cancellationToken)
            .ConfigureAwait(false);
        await apiDbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(await ReadKassenbuchEntryDtoAsync(entry.PrimaryKey, cancellationToken, includeDeleted: true).ConfigureAwait(false));
    }

    internal static VereinSettingsDto ToSettingsDto(VereinSettings? settings)
        => new()
        {
            MembershipContributionMonthlyAmount = settings?.MembershipContributionMonthlyAmount,
            MembershipContributionAnnualAmount = settings?.MembershipContributionAnnualAmount,
            MembershipContributionCurrency = string.IsNullOrWhiteSpace(settings?.MembershipContributionCurrency)
                ? DefaultContributionCurrency
                : settings.MembershipContributionCurrency,
            MembershipContributionPublicTextDe = string.IsNullOrWhiteSpace(settings?.MembershipContributionPublicTextDe)
                ? DefaultContributionTextDe
                : settings.MembershipContributionPublicTextDe,
            MembershipContributionPublicTextEn = string.IsNullOrWhiteSpace(settings?.MembershipContributionPublicTextEn)
                ? DefaultContributionTextEn
                : settings.MembershipContributionPublicTextEn,
            TimeStampUpdated = settings?.TimeStampUpdated,
        };

    internal static VereinMembershipDto ToMembershipDto(VereinMembership membership)
        => new()
        {
            PrimaryKey         = membership.PrimaryKey,
            UserFk             = membership.UserFk,
            UserNickname       = membership.User?.Nickname,
            MemberNumber       = membership.MemberNumber,
            Status             = membership.Status,
            IsActiveMember     = membership.Status == EVereinMembershipStatus.Active,
            TimeStampStarted   = membership.TimeStampStarted,
            TimeStampEnded     = membership.TimeStampEnded,
            Notes              = membership.Notes,
            TimeStampCreated   = membership.TimeStampCreated,
            TimeStampUpdated   = membership.TimeStampUpdated,
        };

    internal static VereinMembershipRequestDto ToMembershipRequestDto(VereinMembershipRequest request)
        => new()
        {
            PrimaryKey                          = request.PrimaryKey,
            UserFk                              = request.UserFk,
            UserNickname                        = request.User?.Nickname,
            FirstName                           = request.FirstName,
            LastName                            = request.LastName,
            DateOfBirth                         = request.DateOfBirth,
            Email                               = request.Email,
            PhoneNumber                         = request.PhoneNumber,
            AddressLine1                        = request.AddressLine1,
            AddressLine2                        = request.AddressLine2,
            PostalCode                          = request.PostalCode,
            City                                = request.City,
            Country                             = request.Country,
            Motivation                          = request.Motivation,
            AcknowledgesMembershipSeparation    = request.AcknowledgesMembershipSeparation,
            AcknowledgesDataProcessing          = request.AcknowledgesDataProcessing,
            AcknowledgesSatzung                 = request.AcknowledgesSatzung,
            Status                              = request.Status,
            DecisionReason                      = request.DecisionReason,
            ReviewedByFk                        = request.ReviewedByFk,
            ReviewedByName                      = request.ReviewedBy?.Nickname,
            TimeStampCreated                    = request.TimeStampCreated,
            TimeStampUpdated                    = request.TimeStampUpdated,
            TimeStampResolved                   = request.TimeStampResolved,
        };

    internal static PublishedVereinDocumentDto ToPublishedDocumentDto(
        VereinDocument document,
        VereinDocumentRevision? revision)
        => new()
        {
            PrimaryKey       = document.PrimaryKey,
            Kind             = document.Kind,
            Revision         = revision is null ? null : ToPublishedRevisionDto(revision),
            TimeStampUpdated = document.TimeStampUpdated,
        };

    internal static VereinDocumentDto ToAdminDocumentDto(VereinDocument document)
    {
        var revisions = (document.Revisions ?? [])
            .OrderByDescending(q => q.Version)
            .Select(ToRevisionDto)
            .ToArray();
        return new VereinDocumentDto
        {
            PrimaryKey       = document.PrimaryKey,
            Kind             = document.Kind,
            IsDeleted        = document.IsDeleted,
            Revision         = revisions.FirstOrDefault(),
            Revisions        = revisions,
            TimeStampCreated = document.TimeStampCreated,
            TimeStampUpdated = document.TimeStampUpdated,
        };
    }

    internal static VereinDocumentRevisionDto ToRevisionDto(VereinDocumentRevision revision)
        => new()
        {
            PrimaryKey         = revision.PrimaryKey,
            DocumentFk         = revision.DocumentFk,
            Version            = revision.Version,
            Title              = revision.Title,
            Summary            = revision.Summary,
            Markdown           = revision.Markdown,
            Comment            = revision.Comment,
            IsPublished        = revision.IsPublished,
            TimeStampPublished = revision.TimeStampPublished,
            TimeStampCreated   = revision.TimeStampCreated,
            AuthorFk           = revision.AuthorFk,
            AuthorName         = revision.Author?.Nickname,
        };

    internal static PublishedVereinDocumentRevisionDto ToPublishedRevisionDto(VereinDocumentRevision revision)
        => new()
        {
            Version            = revision.Version,
            Title              = revision.Title,
            Summary            = revision.Summary,
            Markdown           = revision.Markdown,
            TimeStampPublished = revision.TimeStampPublished,
        };

    /// <summary>
    /// Verein document shape returned to anonymous readers.
    /// </summary>
    public sealed record PublishedVereinDocumentDto
    {
        /// <summary>
        /// Document primary key.
        /// </summary>
        public long PrimaryKey { get; init; }

        /// <summary>
        /// Document kind.
        /// </summary>
        public EVereinDocumentKind Kind { get; init; }

        /// <summary>
        /// Latest published revision.
        /// </summary>
        public PublishedVereinDocumentRevisionDto? Revision { get; init; }

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public Instant TimeStampUpdated { get; init; }
    }

    /// <summary>
    /// Verein document revision shape returned to anonymous readers.
    /// </summary>
    public sealed record PublishedVereinDocumentRevisionDto
    {
        /// <summary>
        /// Document-local version number.
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
        /// Markdown body.
        /// </summary>
        public string Markdown { get; init; } = string.Empty;

        /// <summary>
        /// Publication timestamp.
        /// </summary>
        public Instant? TimeStampPublished { get; init; }
    }

    internal static KassenbuchEntryDto ToKassenbuchEntryDto(KassenbuchEntry entry)
        => new()
        {
            PrimaryKey       = entry.PrimaryKey,
            TimeStampBooked  = entry.TimeStampBooked,
            Direction        = entry.Direction,
            Amount           = entry.Amount,
            Currency         = entry.Currency,
            Category         = entry.Category,
            Description      = entry.Description,
            Reference        = entry.Reference,
            DocumentNumber   = entry.DocumentNumber,
            IsDeleted        = entry.IsDeleted,
            TimeStampCreated = entry.TimeStampCreated,
            TimeStampUpdated = entry.TimeStampUpdated,
            CreatedByName    = entry.CreatedBy?.Nickname,
            UpdatedByName    = entry.UpdatedBy?.Nickname,
        };

    private IQueryable<VereinDocumentRevision> GetLatestPublishedRevisionQuery(EVereinDocumentKind kind)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        return apiDbContext.VereinDocumentRevisions
            .AsNoTracking()
            .Include(q => q.Document)
            .Include(q => q.Author)
            .Where(q => q.Document!.Kind == kind
                        && !q.Document.IsDeleted
                        && q.IsPublished
                        && q.TimeStampPublished <= now)
            .OrderByDescending(q => q.TimeStampPublished)
            .ThenByDescending(q => q.Version);
    }

    private IQueryable<KassenbuchEntry> CreateKassenbuchQuery(
        Instant? from,
        Instant? to,
        string? search,
        string? category)
    {
        var query = apiDbContext.KassenbuchEntries
            .AsNoTracking()
            .Where(q => !q.IsDeleted);
        if (from is not null)
            query = query.Where(q => q.TimeStampBooked >= from);
        if (to is not null)
            query = query.Where(q => q.TimeStampBooked <= to);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim().ToLowerInvariant();
            query = query.Where(q => q.Category.ToLower() == normalizedCategory);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(q => q.Description.ToLower().Contains(normalizedSearch)
                                     || q.Category.ToLower().Contains(normalizedSearch)
                                     || (q.Reference != null && q.Reference.ToLower().Contains(normalizedSearch))
                                     || (q.DocumentNumber != null && q.DocumentNumber.ToLower().Contains(normalizedSearch)));
        }
        return query;
    }

    private async Task<IActionResult> SetRevisionPublicationStateAsync(
        long revisionId,
        bool isPublished,
        CancellationToken cancellationToken)
    {
        if (!CanManageDocuments())
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var revision = await apiDbContext.VereinDocumentRevisions
            .Include(q => q.Document)
            .SingleOrDefaultAsync(q => q.PrimaryKey == revisionId && !q.Document!.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (revision is null || revision.Document is null)
            return NotFound();

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        revision.IsPublished = isPublished;
        revision.TimeStampPublished = isPublished ? revision.TimeStampPublished ?? now : null;
        revision.Document.TimeStampUpdated = now;
        revision.Document.UpdatedByFk = userId;
        await AddDocumentAuditAsync(
                revision.DocumentFk,
                revision.PrimaryKey,
                userId,
                isPublished ? EVereinDocumentAuditAction.Published : EVereinDocumentAuditAction.Unpublished,
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

        return Ok(await ReadAdminDocumentDtoAsync(revision.DocumentFk, cancellationToken).ConfigureAwait(false));
    }

    private async Task<VereinDocumentDto> ReadAdminDocumentDtoAsync(long documentId, CancellationToken cancellationToken)
    {
        var document = await apiDbContext.VereinDocuments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.Revisions!)
            .ThenInclude(q => q.Author)
            .SingleAsync(q => q.PrimaryKey == documentId, cancellationToken)
            .ConfigureAwait(false);
        return ToAdminDocumentDto(document);
    }

    private async Task<VereinSettings?> ReadSettingsAsync(CancellationToken cancellationToken)
        => await apiDbContext.VereinSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.Identifier == VereinSettings.DefaultIdentifier, cancellationToken)
            .ConfigureAwait(false);

    private async Task<VereinMembershipRequestDto> ReadMembershipRequestDtoAsync(
        long requestId,
        CancellationToken cancellationToken)
    {
        var request = await apiDbContext.VereinMembershipRequests
            .AsNoTracking()
            .Include(q => q.User)
            .Include(q => q.ReviewedBy)
            .SingleAsync(q => q.PrimaryKey == requestId, cancellationToken)
            .ConfigureAwait(false);
        return ToMembershipRequestDto(request);
    }

    private async Task<KassenbuchEntryDto> ReadKassenbuchEntryDtoAsync(
        long entryId,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var entry = await apiDbContext.KassenbuchEntries
            .AsNoTracking()
            .Include(q => q.CreatedBy)
            .Include(q => q.UpdatedBy)
            .SingleAsync(q => q.PrimaryKey == entryId && (includeDeleted || !q.IsDeleted), cancellationToken)
            .ConfigureAwait(false);
        return ToKassenbuchEntryDto(entry);
    }

    private async Task<bool> CanReadKassenbuchAsync(Guid userId, CancellationToken cancellationToken)
        => CanManageKassenbuch()
           || await apiDbContext.VereinMemberships.AsNoTracking()
               .AnyAsync(
                   q => q.UserFk == userId && q.Status == EVereinMembershipStatus.Active,
                   cancellationToken)
               .ConfigureAwait(false);

    private static bool IsOpenMembershipRequestStatus(EVereinMembershipRequestStatus status)
        => status is EVereinMembershipRequestStatus.Submitted
            or EVereinMembershipRequestStatus.AcceptedForContact;

    private bool CanManageMemberships()
        => HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.User, Claims.Administrative.Verein)
           || HasBaseClaim(Claims.User.All)
           || HasBaseClaim(Claims.Verein.All)
           || HasBaseClaim(Claims.Verein.Members);

    private bool CanManageDocuments()
        => HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Verein)
           || HasBaseClaim(Claims.Verein.All)
           || HasBaseClaim(Claims.Verein.Documents);

    private bool CanManageKassenbuch()
        => HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Verein)
           || HasBaseClaim(Claims.Verein.All)
           || HasBaseClaim(Claims.Verein.Kassenbuch);

    private bool HasAnyEmptyClaim(params string[] claimIdentifiers)
        => claimIdentifiers.Any(claimIdentifier => User.HasClaim(claimIdentifier, string.Empty));

    private bool HasBaseClaim(string claimIdentifier)
        => User.HasClaim(claimIdentifier, string.Empty);

    private async Task AddMembershipAuditAsync(
        long membershipId,
        Guid? actorId,
        EVereinMembershipAuditAction action,
        Instant now,
        object data,
        CancellationToken cancellationToken)
        => await apiDbContext.VereinMembershipAudits.AddAsync(new VereinMembershipAudit
            {
                MembershipFk      = membershipId,
                UserForeignKey    = actorId,
                Action            = action,
                Data              = JsonSerializer.Serialize(data),
                TimeStampCreated  = now,
            },
            cancellationToken).ConfigureAwait(false);

    private async Task AddMembershipRequestAuditAsync(
        long requestId,
        Guid? actorId,
        EVereinMembershipRequestAuditAction action,
        Instant now,
        object data,
        CancellationToken cancellationToken)
        => await apiDbContext.VereinMembershipRequestAudits.AddAsync(new VereinMembershipRequestAudit
            {
                RequestFk        = requestId,
                UserForeignKey   = actorId,
                Action           = action,
                Data             = JsonSerializer.Serialize(data),
                TimeStampCreated = now,
            },
            cancellationToken).ConfigureAwait(false);

    private async Task AddSettingsAuditAsync(
        long settingsId,
        Guid? actorId,
        EVereinSettingsAuditAction action,
        Instant now,
        object data,
        CancellationToken cancellationToken)
        => await apiDbContext.VereinSettingsAudits.AddAsync(new VereinSettingsAudit
            {
                SettingsFk       = settingsId,
                UserForeignKey   = actorId,
                Action           = action,
                Data             = JsonSerializer.Serialize(data),
                TimeStampCreated = now,
            },
            cancellationToken).ConfigureAwait(false);

    private async Task AddDocumentAuditAsync(
        long documentId,
        long? revisionId,
        Guid? actorId,
        EVereinDocumentAuditAction action,
        Instant now,
        object data,
        CancellationToken cancellationToken)
        => await apiDbContext.VereinDocumentAudits.AddAsync(new VereinDocumentAudit
            {
                DocumentFk        = documentId,
                RevisionFk        = revisionId,
                UserForeignKey    = actorId,
                Action            = action,
                Data              = JsonSerializer.Serialize(data),
                TimeStampCreated  = now,
            },
            cancellationToken).ConfigureAwait(false);

    private async Task AddKassenbuchAuditAsync(
        long entryId,
        Guid? actorId,
        EKassenbuchAuditAction action,
        Instant now,
        object data,
        CancellationToken cancellationToken)
        => await apiDbContext.KassenbuchEntryAudits.AddAsync(new KassenbuchEntryAudit
            {
                KassenbuchEntryFk = entryId,
                UserForeignKey    = actorId,
                Action            = action,
                Data              = JsonSerializer.Serialize(data),
                TimeStampCreated  = now,
            },
            cancellationToken).ConfigureAwait(false);

    private static bool TryNormalizeDocumentPayload(
        VereinDocumentRevisionPayload payload,
        out NormalizedDocumentPayload normalized,
        out string error)
    {
        var title = payload.Title.Trim();
        var summary = payload.Summary.Trim();
        var markdown = payload.Markdown.Trim();
        var comment = (payload.Comment ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > MaximumDocumentTitleLength)
        {
            normalized = default;
            error = "Invalid document title.";
            return false;
        }
        if (summary.Length > MaximumDocumentSummaryLength)
        {
            normalized = default;
            error = "Invalid document summary.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(markdown) || markdown.Length > MaximumDocumentMarkdownLength)
        {
            normalized = default;
            error = "Invalid document markdown.";
            return false;
        }
        if (comment.Length > MaximumDocumentCommentLength)
        {
            normalized = default;
            error = "Invalid document comment.";
            return false;
        }

        normalized = new NormalizedDocumentPayload(title, summary, markdown, comment, payload.IsPublished);
        error = string.Empty;
        return true;
    }

    private static bool TryNormalizeMembershipPayload(
        VereinMembershipUpdatePayload payload,
        out NormalizedMembershipPayload normalized,
        out string error)
    {
        var memberNumber = string.IsNullOrWhiteSpace(payload.MemberNumber)
            ? null
            : payload.MemberNumber.Trim();
        var notes = (payload.Notes ?? string.Empty).Trim();
        var reason = (payload.Reason ?? string.Empty).Trim();
        if (memberNumber?.Length > MaximumMemberNumberLength)
        {
            normalized = default;
            error = "Invalid member number.";
            return false;
        }
        if (notes.Length > MaximumMembershipNotesLength)
        {
            normalized = default;
            error = "Invalid membership notes.";
            return false;
        }
        if (payload.TimeStampEnded is not null
            && payload.TimeStampStarted is not null
            && payload.TimeStampEnded < payload.TimeStampStarted)
        {
            normalized = default;
            error = "Membership end date must be after start date.";
            return false;
        }

        normalized = new NormalizedMembershipPayload(
            memberNumber,
            payload.Status,
            payload.TimeStampStarted,
            payload.TimeStampEnded,
            notes,
            reason);
        error = string.Empty;
        return true;
    }

    private static bool TryNormalizeSettingsPayload(
        VereinSettingsPayload payload,
        out NormalizedSettingsPayload normalized,
        out string error)
    {
        if ((payload.MembershipContributionMonthlyAmount is not null
             && payload.MembershipContributionMonthlyAmount <= 0)
            || (payload.MembershipContributionAnnualAmount is not null
                && payload.MembershipContributionAnnualAmount <= 0))
        {
            normalized = default;
            error = "Membership contribution amount must be greater than zero.";
            return false;
        }

        if ((payload.MembershipContributionMonthlyAmount is null)
            != (payload.MembershipContributionAnnualAmount is null))
        {
            normalized = default;
            error = "Monthly and annual membership contribution amounts must be provided together.";
            return false;
        }

        var currency = string.IsNullOrWhiteSpace(payload.MembershipContributionCurrency)
            ? DefaultContributionCurrency
            : payload.MembershipContributionCurrency.Trim().ToUpperInvariant();
        if (currency.Length != MaximumSettingsCurrencyLength)
        {
            normalized = default;
            error = "Membership contribution currency must be a three-letter ISO code.";
            return false;
        }

        var publicTextDe = string.IsNullOrWhiteSpace(payload.MembershipContributionPublicTextDe)
            ? DefaultContributionTextDe
            : payload.MembershipContributionPublicTextDe.Trim();
        var publicTextEn = string.IsNullOrWhiteSpace(payload.MembershipContributionPublicTextEn)
            ? DefaultContributionTextEn
            : payload.MembershipContributionPublicTextEn.Trim();
        if (publicTextDe.Length > MaximumSettingsPublicTextLength
            || publicTextEn.Length > MaximumSettingsPublicTextLength)
        {
            normalized = default;
            error = "Membership contribution public text is too long.";
            return false;
        }

        normalized = new NormalizedSettingsPayload(
            payload.MembershipContributionMonthlyAmount is null
                ? null
                : decimal.Round(payload.MembershipContributionMonthlyAmount.Value, 2),
            payload.MembershipContributionAnnualAmount is null
                ? null
                : decimal.Round(payload.MembershipContributionAnnualAmount.Value, 2),
            currency,
            publicTextDe,
            publicTextEn);
        error = string.Empty;
        return true;
    }

    private static bool TryNormalizeMembershipRequestPayload(
        VereinMembershipRequestPayload payload,
        out NormalizedMembershipRequestPayload normalized,
        out string error)
    {
        var firstName = payload.FirstName.Trim();
        var lastName = payload.LastName.Trim();
        var email = payload.Email.Trim();
        var phoneNumber = payload.PhoneNumber.Trim();
        var addressLine1 = payload.AddressLine1.Trim();
        var addressLine2 = (payload.AddressLine2 ?? string.Empty).Trim();
        var postalCode = payload.PostalCode.Trim();
        var city = payload.City.Trim();
        var country = payload.Country.Trim();
        var motivation = payload.Motivation.Trim();
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > MaximumMembershipRequestNameLength)
        {
            normalized = default;
            error = "Invalid first name.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Length > MaximumMembershipRequestNameLength)
        {
            normalized = default;
            error = "Invalid last name.";
            return false;
        }
        if (payload.DateOfBirth == default
            || payload.DateOfBirth >= NodaSystemClock.Instance.GetCurrentInstant().InUtc().Date)
        {
            normalized = default;
            error = "Invalid date of birth.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(email) || email.Length > MaximumMembershipRequestEmailLength || !email.Contains('@'))
        {
            normalized = default;
            error = "Invalid email address.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length > MaximumMembershipRequestPhoneLength)
        {
            normalized = default;
            error = "Invalid phone number.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(addressLine1) || addressLine1.Length > MaximumMembershipRequestAddressLineLength)
        {
            normalized = default;
            error = "Invalid address line.";
            return false;
        }
        if (addressLine2.Length > MaximumMembershipRequestAddressLineLength)
        {
            normalized = default;
            error = "Invalid optional address line.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(postalCode) || postalCode.Length > MaximumMembershipRequestPostalCodeLength)
        {
            normalized = default;
            error = "Invalid postal code.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(city) || city.Length > MaximumMembershipRequestCityLength)
        {
            normalized = default;
            error = "Invalid city.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(country) || country.Length > MaximumMembershipRequestCountryLength)
        {
            normalized = default;
            error = "Invalid country.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(motivation) || motivation.Length > MaximumMembershipRequestMotivationLength)
        {
            normalized = default;
            error = "Invalid motivation.";
            return false;
        }
        if (!payload.AcknowledgesMembershipSeparation
            || !payload.AcknowledgesDataProcessing
            || !payload.AcknowledgesSatzung)
        {
            normalized = default;
            error = "All Verein membership acknowledgements are required.";
            return false;
        }

        normalized = new NormalizedMembershipRequestPayload(
            firstName,
            lastName,
            payload.DateOfBirth,
            email,
            phoneNumber,
            addressLine1,
            addressLine2,
            postalCode,
            city,
            country,
            motivation,
            payload.AcknowledgesMembershipSeparation,
            payload.AcknowledgesDataProcessing,
            payload.AcknowledgesSatzung);
        error = string.Empty;
        return true;
    }

    private static bool TryNormalizeMembershipRequestTransitionPayload(
        VereinMembershipRequestTransitionPayload payload,
        out NormalizedMembershipRequestTransitionPayload normalized,
        out string error)
    {
        var reason = (payload.Reason ?? string.Empty).Trim();
        var memberNumber = string.IsNullOrWhiteSpace(payload.MemberNumber)
            ? null
            : payload.MemberNumber.Trim();
        var notes = (payload.Notes ?? string.Empty).Trim();
        if (payload.Status is not EVereinMembershipRequestStatus.AcceptedForContact
            and not EVereinMembershipRequestStatus.Approved
            and not EVereinMembershipRequestStatus.Rejected)
        {
            normalized = default;
            error = "Invalid membership request transition.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > MaximumMembershipRequestDecisionReasonLength)
        {
            normalized = default;
            error = "Invalid decision reason.";
            return false;
        }
        if (memberNumber?.Length > MaximumMemberNumberLength)
        {
            normalized = default;
            error = "Invalid member number.";
            return false;
        }
        if (notes.Length > MaximumMembershipNotesLength)
        {
            normalized = default;
            error = "Invalid membership notes.";
            return false;
        }

        normalized = new NormalizedMembershipRequestTransitionPayload(
            payload.Status,
            reason,
            memberNumber,
            notes);
        error = string.Empty;
        return true;
    }

    private static bool TryNormalizeKassenbuchPayload(
        KassenbuchEntryPayload payload,
        out NormalizedKassenbuchPayload normalized,
        out string error)
    {
        var currency = (payload.Currency ?? "EUR").Trim().ToUpperInvariant();
        var category = payload.Category.Trim();
        var description = payload.Description.Trim();
        var reference = string.IsNullOrWhiteSpace(payload.Reference) ? null : payload.Reference.Trim();
        var documentNumber = string.IsNullOrWhiteSpace(payload.DocumentNumber) ? null : payload.DocumentNumber.Trim();
        if (payload.Amount <= 0)
        {
            normalized = default;
            error = "Amount must be greater than zero.";
            return false;
        }
        if (currency.Length != 3)
        {
            normalized = default;
            error = "Currency must be a three-letter ISO code.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(category) || category.Length > MaximumLedgerCategoryLength)
        {
            normalized = default;
            error = "Invalid ledger category.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(description) || description.Length > MaximumLedgerDescriptionLength)
        {
            normalized = default;
            error = "Invalid ledger description.";
            return false;
        }
        if (reference?.Length > MaximumLedgerReferenceLength || documentNumber?.Length > MaximumLedgerDocumentNumberLength)
        {
            normalized = default;
            error = "Invalid ledger reference.";
            return false;
        }

        normalized = new NormalizedKassenbuchPayload(
            payload.TimeStampBooked,
            payload.Direction,
            decimal.Round(payload.Amount, 2),
            currency,
            category,
            description,
            reference,
            documentNumber);
        error = string.Empty;
        return true;
    }

    private readonly record struct NormalizedDocumentPayload(
        string Title,
        string Summary,
        string Markdown,
        string Comment,
        bool IsPublished);

    private readonly record struct NormalizedMembershipPayload(
        string? MemberNumber,
        EVereinMembershipStatus Status,
        Instant? TimeStampStarted,
        Instant? TimeStampEnded,
        string Notes,
        string Reason);

    private readonly record struct NormalizedMembershipRequestPayload(
        string FirstName,
        string LastName,
        LocalDate DateOfBirth,
        string Email,
        string PhoneNumber,
        string AddressLine1,
        string AddressLine2,
        string PostalCode,
        string City,
        string Country,
        string Motivation,
        bool AcknowledgesMembershipSeparation,
        bool AcknowledgesDataProcessing,
        bool AcknowledgesSatzung);

    private readonly record struct NormalizedMembershipRequestTransitionPayload(
        EVereinMembershipRequestStatus Status,
        string Reason,
        string? MemberNumber,
        string Notes);

    private readonly record struct NormalizedSettingsPayload(
        decimal? MonthlyAmount,
        decimal? AnnualAmount,
        string Currency,
        string PublicTextDe,
        string PublicTextEn);

    private readonly record struct NormalizedKassenbuchPayload(
        Instant TimeStampBooked,
        EKassenbuchEntryDirection Direction,
        decimal Amount,
        string Currency,
        string Category,
        string Description,
        string? Reference,
        string? DocumentNumber);
}
