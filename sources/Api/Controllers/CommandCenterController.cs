using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Database.Eventing;
using X39.UnitedTacticalForces.Database.Media;
using X39.UnitedTacticalForces.Database.Verein;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.Media;
using X39.UnitedTacticalForces.Common.Event;
using X39.UnitedTacticalForces.Common.Verein;
using AuthorityClaim = X39.UnitedTacticalForces.Database.Authority.Claim;
using AuthorityRole = X39.UnitedTacticalForces.Database.Authority.Role;
using AuthorityUser = X39.UnitedTacticalForces.Database.Authority.User;
using WikiPage = X39.UnitedTacticalForces.Database.Wiki.WikiPage;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Controller responsible for aggregate Command Center read models.
/// </summary>
[ApiController]
[Produces("application/json")]
[Route(Constants.Routes.CommandCenter)]
[Authorize]
public class CommandCenterController(DatabaseContext apiDbContext, IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Returns the aggregate community administration snapshot.
    /// </summary>
    [ProducesResponseType<CommandCenterCommunitySnapshotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("community-snapshot", Name = nameof(GetCommunitySnapshotAsync))]
    public async Task<IActionResult> GetCommunitySnapshotAsync(
        [FromQuery] int take = 80,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 500)
            return BadRequest();
        if (!CanManageMembers)
            return Forbid();

        var usersQuery = IncludeRoleMemberReferences(apiDbContext.Users.AsNoTracking());
        var members = await ApplySearch(usersQuery, search)
            .GroupJoin(
                apiDbContext.VereinMemberships.AsNoTracking(),
                user => user.PrimaryKey,
                membership => membership.UserFk,
                (user, memberships) => new
                {
                    User = user,
                    Membership = memberships
                        .OrderByDescending(q => q.TimeStampStarted)
                        .FirstOrDefault(),
                })
            .OrderBy(q => q.User.IsVerified)
            .ThenByDescending(q => q.User.IsBanned)
            .ThenBy(q => q.User.IsDeleted)
            .ThenBy(q => q.User.Nickname)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return Ok(new CommandCenterCommunitySnapshotDto
        {
            MemberCount                = await apiDbContext.Users.LongCountAsync(cancellationToken),
            VerifiedMemberCount        = await apiDbContext.Users.LongCountAsync(q => q.IsVerified, cancellationToken),
            UnverifiedMemberCount      = await apiDbContext.Users.LongCountAsync(q => !q.IsVerified, cancellationToken),
            BannedMemberCount          = await apiDbContext.Users.LongCountAsync(q => q.IsBanned, cancellationToken),
            DeletedMemberCount         = await apiDbContext.Users.LongCountAsync(q => q.IsDeleted, cancellationToken),
            ActiveVereinMemberCount    = await CountMembershipsAsync(EVereinMembershipStatus.Active, cancellationToken),
            SuspendedVereinMemberCount = await CountMembershipsAsync(EVereinMembershipStatus.Suspended, cancellationToken),
            EndedVereinMemberCount     = await CountMembershipsAsync(EVereinMembershipStatus.Ended, cancellationToken),
            RoleCount                  = await apiDbContext.Roles.LongCountAsync(cancellationToken),
            ClaimCount                 = await apiDbContext.Claims.LongCountAsync(cancellationToken),
            Members                    = members.Select(q => CreateCommunityMemberDto(q.User, q.Membership)).ToArray(),
        });
    }

    /// <summary>
    /// Returns the aggregate roles and claims snapshot.
    /// </summary>
    [ProducesResponseType<CommandCenterAuthoritySnapshotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("authority-snapshot", Name = nameof(GetAuthoritySnapshotAsync))]
    public async Task<IActionResult> GetAuthoritySnapshotAsync(
        [FromQuery] int take = 120,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 500)
            return BadRequest();
        if (!CanManageAuthority)
            return Forbid();

        var roles = await ApplySearch(apiDbContext.Roles
                .AsNoTracking()
                .AsSplitQuery()
                .Include(q => q.Claims)
                .Include(q => q.Users), search)
            .OrderBy(q => q.Title)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        var claims = await ApplySearch(apiDbContext.Claims
                .AsNoTracking()
                .AsSplitQuery()
                .Include(q => q.Roles)
                .Include(q => q.Users), search)
            .OrderBy(q => q.Category)
            .ThenBy(q => q.Title)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return Ok(new CommandCenterAuthoritySnapshotDto
        {
            MemberCount = await apiDbContext.Users.LongCountAsync(cancellationToken),
            RoleCount   = await apiDbContext.Roles.LongCountAsync(cancellationToken),
            ClaimCount  = await apiDbContext.Claims.LongCountAsync(cancellationToken),
            Roles = roles.Select(q => new CommandCenterAuthorityRoleDto
            {
                Role        = q.ToPlainDto(),
                Claims      = (q.Claims ?? []).Select(claim => claim.ToPlainDto()).ToArray(),
                MemberCount = q.Users?.Count ?? 0,
            }).ToArray(),
            Claims = claims.Select(q => new CommandCenterAuthorityClaimDto
            {
                Claim             = q.ToPlainDto(),
                DirectMemberCount = q.Users?.Count ?? 0,
                RoleCount         = q.Roles?.Count ?? 0,
                RoleTitles        = (q.Roles ?? []).Select(role => role.Title).Order().ToArray(),
            }).ToArray(),
        });
    }

    /// <summary>
    /// Returns the aggregate wiki/media administration snapshot.
    /// </summary>
    [ProducesResponseType<CommandCenterWikiMediaSnapshotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("wiki-media-snapshot", Name = nameof(GetWikiMediaSnapshotAsync))]
    public async Task<IActionResult> GetWikiMediaSnapshotAsync(
        [FromQuery] int take = 80,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 500)
            return BadRequest();
        if (!CanManageWikiMedia)
            return Forbid();

        var pages = await ApplySearch(apiDbContext.WikiPages
                .AsNoTracking()
                .AsSplitQuery()
                .Include(q => q.Revisions!)
                .ThenInclude(q => q.Author), search)
            .OrderBy(q => q.IsDeleted)
            .ThenBy(q => q.Title)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return Ok(new CommandCenterWikiMediaSnapshotDto
        {
            WikiPageCount        = await apiDbContext.WikiPages.LongCountAsync(cancellationToken),
            DeletedWikiPageCount = await apiDbContext.WikiPages.LongCountAsync(q => q.IsDeleted, cancellationToken),
            WikiRevisionCount    = await apiDbContext.WikiPageRevisions.LongCountAsync(cancellationToken),
            WikiAuditRecordCount = await apiDbContext.WikiPageAudits.LongCountAsync(cancellationToken),
            Pages                = pages.Select(CreateWikiMediaPageDto).ToArray(),
        });
    }

    /// <summary>
    /// Returns the aggregate field report administration snapshot.
    /// </summary>
    [ProducesResponseType<CommandCenterFieldReportsSnapshotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("field-reports-snapshot", Name = nameof(GetFieldReportsSnapshotAsync))]
    public async Task<IActionResult> GetFieldReportsSnapshotAsync(
        [FromQuery] int take = 120,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 500)
            return BadRequest();
        if (!CanManageWikiMedia)
            return Forbid();

        var reports = await ApplySearch(apiDbContext.FieldReports
                .AsNoTracking()
                .AsSplitQuery()
                .Include(q => q.CreatedBy)
                .Include(q => q.UpdatedBy)
                .Include(q => q.Audits), search)
            .OrderBy(q => q.IsDeleted)
            .ThenByDescending(q => q.TimeStampUpdated)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return Ok(new CommandCenterFieldReportsSnapshotDto
        {
            FieldReportCount          = await apiDbContext.FieldReports.LongCountAsync(cancellationToken),
            PublishedFieldReportCount = await apiDbContext.FieldReports.LongCountAsync(q => q.IsPublished && !q.IsDeleted, cancellationToken),
            DraftFieldReportCount     = await apiDbContext.FieldReports.LongCountAsync(q => !q.IsPublished && !q.IsDeleted, cancellationToken),
            DeletedFieldReportCount   = await apiDbContext.FieldReports.LongCountAsync(q => q.IsDeleted, cancellationToken),
            Reports                   = reports.Select(CreateFieldReportDto).ToArray(),
        });
    }

    /// <summary>
    /// Returns a redacted settings snapshot.
    /// </summary>
    [ProducesResponseType<CommandCenterSettingsSnapshotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("settings-snapshot", Name = nameof(GetSettingsSnapshot))]
    public IActionResult GetSettingsSnapshot()
    {
        if (!User.HasClaim(Claims.Administrative.All, string.Empty))
            return Forbid();

        var steamEnabled = GetBooleanConfiguration(Constants.Configuration.Steam.Enabled);
        var discordEnabled = GetBooleanConfiguration(Constants.Configuration.Discord.Enabled);

        return Ok(new CommandCenterSettingsSnapshotDto
        {
            ApiBaseUrlConfigured    = HasConfiguration(Constants.Configuration.General.ApiBaseUrl),
            ClientBaseUrlConfigured = HasConfiguration(Constants.Configuration.General.ClientBaseUrl),
            BasePathConfigured      = HasConfiguration(Constants.Configuration.General.BasePath),
            AutoVerifyNewUsers      = GetBooleanConfiguration(Constants.Configuration.General.AutoVerifyNewUsers),
            Features =
            [
                new CommandCenterSettingsFeatureDto
                {
                    Area         = "Steam",
                    IsEnabled    = steamEnabled,
                    IsConfigured = steamEnabled,
                    Detail       = "Steam authentication readiness is exposed without credential values.",
                },
                new CommandCenterSettingsFeatureDto
                {
                    Area         = "Discord OAuth",
                    IsEnabled    = discordEnabled,
                    IsConfigured = HasConfiguration(Constants.Configuration.Discord.OAuth.ClientId)
                                   && HasConfiguration(Constants.Configuration.Discord.OAuth.ClientSecret),
                    Detail       = "OAuth client credentials are redacted; only readiness is exposed.",
                },
                new CommandCenterSettingsFeatureDto
                {
                    Area         = "Discord Bot",
                    IsEnabled    = discordEnabled,
                    IsConfigured = HasConfiguration(Constants.Configuration.Discord.Bot.ApplicationId)
                                   && HasConfiguration(Constants.Configuration.Discord.Bot.PublicKey)
                                   && HasConfiguration(Constants.Configuration.Discord.Bot.BotToken),
                    Detail       = "Bot token and identifiers are redacted; only readiness is exposed.",
                },
                new CommandCenterSettingsFeatureDto
                {
                    Area         = "TeamSpeak",
                    IsEnabled    = HasConfiguration(Constants.Configuration.TeamSpeak.Host),
                    IsConfigured = HasConfiguration(Constants.Configuration.TeamSpeak.Host)
                                   && HasConfiguration(Constants.Configuration.TeamSpeak.Port),
                    Detail       = "Host and port readiness are exposed without password values.",
                },
            ],
        });
    }

    /// <summary>
    /// Returns event administration rows.
    /// </summary>
    [ProducesResponseType<CommandCenterEventAdminSnapshotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpGet("event-admin-snapshot", Name = nameof(GetEventAdminSnapshotAsync))]
    public async Task<IActionResult> GetEventAdminSnapshotAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 80,
        [FromQuery] bool hostedByMeOnly = false,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 500)
            return BadRequest();
        if (!CanManageEvents)
            return Forbid();
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var query = ApplySearch(IncludeEventAdminReferences(CreateEventAdminQuery(userId, hostedByMeOnly)), search);
        var events = await query
            .OrderByDescending(q => q.ScheduledFor)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        var upcoming = await IncludeEventAdminReferences(CreateEventAdminQuery(userId, hostedByMeOnly))
            .Where(q => q.ScheduledFor >= NodaSystemClock.Instance.GetCurrentInstant())
            .OrderBy(q => q.ScheduledFor)
            .Take(8)
            .ToArrayAsync(cancellationToken);

        return Ok(new CommandCenterEventAdminSnapshotDto
        {
            EventCount      = await query.LongCountAsync(cancellationToken),
            Events          = events.Select(e => CreateEventAdminRow(e, userId)).ToArray(),
            UpcomingEvents  = upcoming.Select(e => CreateUpcomingEventDto(e, userId)).ToArray(),
        });
    }

    private bool CanManageEvents
        => HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Event, Claims.Creation.Events);

    private bool CanManageMembers
        => HasBaseClaim(Claims.User.All) || HasBaseClaim(Claims.Verein.All) || HasBaseClaim(Claims.Verein.Members)
           || HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.User, Claims.Administrative.Verein);

    private bool CanManageAuthority
        => HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Role);

    private bool CanManageWikiMedia
        => HasBaseClaim(Claims.Wiki.All) || HasBaseClaim(Claims.Content.Pages)
           || HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Wiki, Claims.Creation.Wiki);

    private bool HasAnyEmptyClaim(params string[] claimIdentifiers)
        => claimIdentifiers.Any(claimIdentifier => User.HasClaim(claimIdentifier, string.Empty));

    private bool HasBaseClaim(string claimIdentifier)
        => User.HasClaim(claimIdentifier, string.Empty);

    private bool HasConfiguration(string key)
        => !string.IsNullOrWhiteSpace(configuration[key]);

    private bool GetBooleanConfiguration(string key)
        => bool.TryParse(configuration[key], out var value) && value;

    private Task<long> CountMembershipsAsync(EVereinMembershipStatus status, CancellationToken cancellationToken)
        => apiDbContext.VereinMemberships.LongCountAsync(q => q.Status == status, cancellationToken);

    private IQueryable<Event> CreateEventAdminQuery(Guid userId, bool hostedByMeOnly)
    {
        var query = apiDbContext.Events.AsNoTracking();
        if (hostedByMeOnly)
            return query.Where(q => q.HostedByFk == userId);
        return HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Event)
            ? query
            : query.Where(q => q.IsVisible || q.HostedByFk == userId);
    }

    private static IQueryable<Event> IncludeEventAdminReferences(IQueryable<Event> query)
        => query
            .AsSplitQuery()
            .Include(q => q.Owner)
            .Include(q => q.HostedBy)
            .Include(q => q.Terrain)
            .Include(q => q.UserMetas)
            .Include(q => q.ModPackRevision)
            .ThenInclude(q => q!.Definition)
            .Include(q => q.ModPackRevision)
            .ThenInclude(q => q!.UserMetas);

    private static IQueryable<AuthorityUser> IncludeRoleMemberReferences(IQueryable<AuthorityUser> query)
        => query
            .AsSplitQuery()
            .Include(q => q.Roles)
            .Include(q => q.Claims);

    private static IQueryable<ModPackDefinition> ApplySearch(IQueryable<ModPackDefinition> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var searchPattern = $"%{EscapeLikePattern(search)}%";
        return query.Where(q => EF.Functions.ILike(q.Title, searchPattern, "\\"));
    }

    private static IQueryable<Terrain> ApplySearch(IQueryable<Terrain> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var searchPattern = $"%{EscapeLikePattern(search)}%";
        return query.Where(q => EF.Functions.ILike(q.Title, searchPattern, "\\"));
    }

    private static IQueryable<Event> ApplySearch(IQueryable<Event> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var searchPattern = $"%{EscapeLikePattern(search)}%";
        return query.Where(q =>
            EF.Functions.ILike(q.Title, searchPattern, "\\")
            || EF.Functions.ILike(q.Owner!.Nickname, searchPattern, "\\")
            || EF.Functions.ILike(q.HostedBy!.Nickname, searchPattern, "\\")
            || EF.Functions.ILike(q.Terrain!.Title, searchPattern, "\\")
            || EF.Functions.ILike(q.ModPackRevision!.Definition!.Title, searchPattern, "\\")
        );
    }

    private static IQueryable<AuthorityUser> ApplySearch(IQueryable<AuthorityUser> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var searchPattern = $"%{EscapeLikePattern(search)}%";
        return query.Where(q => EF.Functions.ILike(q.Nickname, searchPattern, "\\"));
    }

    private static IQueryable<AuthorityRole> ApplySearch(IQueryable<AuthorityRole> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var searchPattern = $"%{EscapeLikePattern(search)}%";
        return query.Where(q =>
            EF.Functions.ILike(q.Title, searchPattern, "\\")
            || EF.Functions.ILike(q.Description, searchPattern, "\\")
        );
    }

    private static IQueryable<AuthorityClaim> ApplySearch(IQueryable<AuthorityClaim> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var searchPattern = $"%{EscapeLikePattern(search)}%";
        return query.Where(q =>
            EF.Functions.ILike(q.Category, searchPattern, "\\")
            || EF.Functions.ILike(q.Title, searchPattern, "\\")
            || EF.Functions.ILike(q.Identifier, searchPattern, "\\")
            || EF.Functions.ILike(q.Description, searchPattern, "\\")
            || q.Value != null && EF.Functions.ILike(q.Value, searchPattern, "\\")
        );
    }

    private static IQueryable<WikiPage> ApplySearch(IQueryable<WikiPage> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var searchPattern = $"%{EscapeLikePattern(search)}%";
        return query.Where(q => EF.Functions.ILike(q.Title, searchPattern, "\\"));
    }

    private static IQueryable<FieldReport> ApplySearch(IQueryable<FieldReport> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;
        var searchPattern = $"%{EscapeLikePattern(search)}%";
        return query.Where(q =>
            EF.Functions.ILike(q.Title, searchPattern, "\\")
            || EF.Functions.ILike(q.Slug, searchPattern, "\\")
            || EF.Functions.ILike(q.Excerpt, searchPattern, "\\")
            || EF.Functions.ILike(q.Category, searchPattern, "\\")
        );
    }

    private static string EscapeLikePattern(string value)
        => value.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("^", "\\^", StringComparison.Ordinal);

    private static CommandCenterCommunityMemberDto CreateCommunityMemberDto(
        AuthorityUser user,
        VereinMembership? membership)
        => new()
        {
            PrimaryKey                = user.PrimaryKey,
            Nickname                  = user.Nickname,
            Avatar                    = user.Avatar,
            AvatarMimeType            = user.AvatarMimeType,
            IsVerified                = user.IsVerified,
            IsBanned                  = user.IsBanned,
            IsDeleted                 = user.IsDeleted,
            VereinMembershipStatus    = membership?.Status.ToString(),
            VereinMemberNumber        = membership?.MemberNumber,
            IsActiveVereinMember      = membership?.Status == EVereinMembershipStatus.Active,
            VereinMembershipStarted   = membership?.TimeStampStarted,
            VereinMembershipEnded     = membership?.TimeStampEnded,
            DirectClaimCount          = user.Claims?.Count ?? 0,
            RoleTitles = (user.Roles ?? [])
                .Select(role => role.Title)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };

    private static CommandCenterWikiMediaPageDto CreateWikiMediaPageDto(WikiPage page)
    {
        var latestRevision = (page.Revisions ?? [])
            .OrderByDescending(q => q.TimeStampCreated)
            .FirstOrDefault();
        return new CommandCenterWikiMediaPageDto
        {
            PrimaryKey               = page.PrimaryKey,
            Title                    = page.Title,
            IsDeleted                = page.IsDeleted,
            TimeStampCreated         = page.TimeStampCreated,
            RevisionCount            = page.Revisions?.Count ?? 0,
            LatestRevisionTimeStamp  = latestRevision?.TimeStampCreated,
            LatestRevisionAuthorName = latestRevision?.Author?.Nickname,
            LatestRevisionComment    = latestRevision?.Comment,
        };
    }

    private static CommandCenterFieldReportDto CreateFieldReportDto(FieldReport report)
        => new()
        {
            PrimaryKey       = report.PrimaryKey,
            Slug             = report.Slug,
            Title            = report.Title,
            Excerpt          = report.Excerpt,
            Category         = report.Category,
            Markdown         = report.Markdown,
            HeroImageAssetFk = report.HeroImageAssetFk,
            HeroImagePath    = report.HeroImageAssetFk is null
                ? null
                : IMediaAssetService.CreateImagePath(report.HeroImageAssetFk.Value),
            HeroImageAlt     = report.HeroImageAlt,
            IsFeatured       = report.IsFeatured,
            SortOrder        = report.SortOrder,
            TimeStampPublished = report.TimeStampPublished,
            TimeStampUpdated = report.TimeStampUpdated,
            IsPublished      = report.IsPublished,
            IsDeleted        = report.IsDeleted,
            TimeStampCreated = report.TimeStampCreated,
            CreatedByName    = report.CreatedBy?.Nickname,
            UpdatedByName    = report.UpdatedBy?.Nickname,
            AuditRecordCount = report.Audits?.Count ?? 0,
        };

    private static CommandCenterEventAdminEventDto CreateEventAdminRow(Event e, Guid userId)
        => new()
        {
            PlainEvent = CreatePlainEventDto(e, userId),
            Owner      = e.Owner?.ToPlainDto(),
            HostedBy   = e.HostedBy?.ToPlainDto(),
            Terrain    = e.Terrain?.ToPlainDto(),
            ModPackRevision = e.ModPackRevision is null
                ? null
                : new ModPackRevisionDto
                {
                    DefinitionPrimaryKey       = e.ModPackRevision.Definition?.PrimaryKey,
                    DefinitionTimeStampCreated = e.ModPackRevision.Definition?.TimeStampCreated,
                    DefinitionTitle            = e.ModPackRevision.Definition?.Title,
                    DefinitionOwnerFk          = e.ModPackRevision.Definition?.OwnerFk,
                    DefinitionIsActive         = e.ModPackRevision.Definition?.IsActive,
                    DefinitionIsComposition    = e.ModPackRevision.Definition?.IsComposition,
                    MetaTimeStampDownloaded = e.ModPackRevision.UserMetas!
                        .Where(meta => meta.UserFk == userId)
                        .Select(meta => (Instant?)meta.TimeStampDownloaded)
                        .OrderByDescending(timeStampDownloaded => timeStampDownloaded)
                        .FirstOrDefault(),
                    PrimaryKey       = e.ModPackRevision.PrimaryKey,
                    TimeStampCreated = e.ModPackRevision.TimeStampCreated,
                    Html             = e.ModPackRevision.Html,
                    UpdatedByFk      = e.ModPackRevision.UpdatedByFk,
                    IsActive         = e.ModPackRevision.IsActive,
                },
        };

    private static PlainEventDto CreatePlainEventDto(Event e, Guid userId)
        => new()
        {
            PrimaryKey           = e.PrimaryKey,
            Title                = e.Title,
            Description          = e.Description,
            TerrainFk            = e.TerrainFk,
            ModPackRevisionFk    = e.ModPackRevisionFk,
            ImageAssetFk         = e.ImageAssetFk,
            ImagePath            = IMediaAssetService.CreateImagePath(e.ImageAssetFk),
            ScheduledForOriginal = e.ScheduledForOriginal,
            ScheduledFor         = e.ScheduledFor,
            TimeStampCreated     = e.TimeStampCreated,
            IsVisible            = e.IsVisible,
            AcceptedCount        = e.AcceptedCount,
            RejectedCount        = e.RejectedCount,
            MaybeCount           = e.MaybeCount,
            MinimumAccepted      = e.MinimumAccepted,
            OwnerFk              = e.OwnerFk,
            HostedByFk           = e.HostedByFk,
            MetaAcceptance = e.UserMetas!
                .Where(meta => meta.UserFk == userId)
                .Select(meta => (EEventAcceptance?)meta.Acceptance)
                .SingleOrDefault(),
        };

    private static UpcomingEventDto CreateUpcomingEventDto(Event e, Guid userId)
        => new()
        {
            PrimaryKey                         = e.PrimaryKey,
            Title                              = e.Title,
            Description                        = e.Description,
            TerrainTitle                       = e.Terrain!.Title,
            ModPackDefinitionTitle             = e.ModPackRevision!.Definition!.Title,
            ModPackDefinitionIsComposition     = e.ModPackRevision.Definition.IsComposition,
            ImageAssetFk                       = e.ImageAssetFk,
            ImagePath                          = IMediaAssetService.CreateImagePath(e.ImageAssetFk),
            ScheduledForOriginal               = e.ScheduledForOriginal,
            ScheduledFor                       = e.ScheduledFor,
            TimeStampCreated                   = e.TimeStampCreated,
            AcceptedCount                      = e.AcceptedCount,
            RejectedCount                      = e.RejectedCount,
            MaybeCount                         = e.MaybeCount,
            MinimumAccepted                    = e.MinimumAccepted,
            ModPackRevisionTimeStampDownloaded = e.ModPackRevision.UserMetas!
                .Where(meta => meta.UserFk == userId)
                .Select(meta => (Instant?)meta.TimeStampDownloaded)
                .OrderByDescending(timeStampDownloaded => timeStampDownloaded)
                .FirstOrDefault(),
            HostedBy       = e.HostedBy!.ToPlainDto(),
            MetaAcceptance = e.UserMetas!
                .Where(meta => meta.UserFk == userId)
                .Select(meta => (EEventAcceptance?)meta.Acceptance)
                .SingleOrDefault(),
        };
}
