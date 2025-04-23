using Discord;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.HostedServices;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Contract.Event;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Provides API endpoints for <see cref="Event"/>'s.
/// </summary>
[ApiController]
[Route(Constants.Routes.Events)]
public class EventsController : ControllerBase
{
    private readonly ILogger<EventsController> _logger;
    private readonly ApiDbContext              _apiDbContext;
    private readonly DiscordBot?               _discordBot;
    private readonly IUpdateStreamService      _updateStreamService;
    private readonly BaseUrl                   _baseUrl;

    /// <summary>
    /// Creates a new instance of <see cref="EventsController"/>.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to use.</param>
    /// <param name="apiDbContext">The <see cref="ApiDbContext"/> to use.</param>
    /// <param name="discordBot">The <see cref="DiscordBot"/> to use.</param>
    /// <param name="baseUrl">The <see cref="BaseUrl"/> to use.</param>
    public EventsController(
        ILogger<EventsController> logger,
        ApiDbContext apiDbContext,
        DiscordBot? discordBot,
        IUpdateStreamService updateStreamService,
        BaseUrl baseUrl
    )
    {
        _logger              = logger;
        _apiDbContext        = apiDbContext;
        _discordBot          = discordBot;
        _updateStreamService = updateStreamService;
        _baseUrl             = baseUrl;
    }

    /// <summary>
    /// Returns the upcoming <see cref="Event"/>'s.
    /// </summary>
    /// <remarks>
    /// An <see cref="Event"/> is considered upcoming for the whole day of its <see cref="Event.ScheduledFor"/> time.
    /// This means that if an <see cref="Event"/> is scheduled for 2023-02-22T18:00:00+00:00, the event will stay
    /// in the upcoming list until 2023-02-23T00:00:00+00:00. 
    /// </remarks>
    /// <returns>The upcoming <see cref="Event"/>'ss and the data required to display them to the current user.</returns>
    [AllowAnonymous]
    [HttpGet("upcoming", Name = nameof(GetUpcomingEventsAsync))]
    [ProducesResponseType<UpcomingEventDto[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcomingEventsAsync(CancellationToken cancellationToken)
    {
        IQueryable<UpcomingEventDto> query;
        if (User.TryGetUserId(out var userId))
        {
            query = _apiDbContext.Events
                .Where(q => q.ScheduledFor >= DateTime.Today)
                .Where(q => q.IsVisible || q.HostedByFk == userId)
                .Select(e => new UpcomingEventDto
                    {
                        PrimaryKey                     = e.PrimaryKey,
                        Title                          = e.Title,
                        Description                    = e.Description,
                        TerrainTitle                   = e.Terrain!.Title,
                        ModPackDefinitionTitle         = e.ModPackRevision!.Definition!.Title,
                        ModPackDefinitionIsComposition = e.ModPackRevision.Definition.IsComposition,
                        Image                          = e.Image,
                        ImageMimeType                  = e.ImageMimeType,
                        ScheduledForOriginal           = e.ScheduledForOriginal,
                        ScheduledFor                   = e.ScheduledFor,
                        TimeStampCreated               = e.TimeStampCreated,
                        AcceptedCount                  = e.AcceptedCount,
                        RejectedCount                  = e.RejectedCount,
                        MaybeCount                     = e.MaybeCount,
                        MinimumAccepted                = e.MinimumAccepted,
                        ModPackRevisionTimeStampDownloaded =
                            e.ModPackRevision!.UserMetas!.SingleOrDefault(meta => meta.UserFk == userId)!
                                .TimeStampDownloaded,
                        HostedBy = new PlainUserDto
                        {
                            PrimaryKey     = e.HostedBy!.PrimaryKey,
                            Nickname       = e.HostedBy!.Nickname,
                            Avatar         = e.HostedBy!.Avatar,
                            AvatarMimeType = e.HostedBy!.AvatarMimeType,
                        },
                        MetaAcceptance = e.UserMetas!.SingleOrDefault(meta => meta.UserFk == userId)!.Acceptance,
                    }
                )
                .OrderBy(q => q.ScheduledFor);
        }
        else
        {
            query = _apiDbContext.Events
                .Where(q => q.ScheduledFor >= DateTime.Today)
                .Where(q => q.IsVisible)
                .Select(e => new UpcomingEventDto
                    {
                        PrimaryKey                         = e.PrimaryKey,
                        Title                              = e.Title,
                        Description                        = e.Description,
                        TerrainTitle                       = e.Terrain!.Title,
                        ModPackDefinitionTitle             = e.ModPackRevision!.Definition!.Title,
                        ModPackDefinitionIsComposition     = e.ModPackRevision.Definition.IsComposition,
                        Image                              = e.Image,
                        ImageMimeType                      = e.ImageMimeType,
                        ScheduledForOriginal               = e.ScheduledForOriginal,
                        ScheduledFor                       = e.ScheduledFor,
                        TimeStampCreated                   = e.TimeStampCreated,
                        AcceptedCount                      = e.AcceptedCount,
                        RejectedCount                      = e.RejectedCount,
                        MaybeCount                         = e.MaybeCount,
                        MinimumAccepted                    = e.MinimumAccepted,
                        ModPackRevisionTimeStampDownloaded = null,
                        HostedBy = new PlainUserDto
                        {
                            PrimaryKey     = e.HostedBy!.PrimaryKey,
                            Nickname       = e.HostedBy!.Nickname,
                            Avatar         = e.HostedBy!.Avatar,
                            AvatarMimeType = e.HostedBy!.AvatarMimeType,
                        },
                        MetaAcceptance = e.UserMetas!.SingleOrDefault(meta => meta.UserFk == userId)!.Acceptance,
                    }
                )
                .OrderBy(q => q.ScheduledFor);
        }

        var results = await query.ToArrayAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Returns all <see cref="Event"/>'s.
    /// </summary>
    /// <remarks>
    /// Referred entities are not be included.
    /// </remarks>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="take">The amount of logs to receive</param>
    /// <param name="skip">The amount of log entries to skip</param>
    /// <param name="hostedByMeOnly">
    ///     If <see langword="true"/>, will only return events where the <see cref="Event.HostedBy"/> is the calling user.
    /// </param>
    /// <param name="descendingByScheduledFor">
    ///     If <see langword="true"/>, order of returned logs will be descending (next one first).
    /// </param>
    /// <returns>The upcoming <see cref="Event"/>'ss and the data required to display them to the current user.</returns>
    [Authorize]
    [HttpGet("all", Name = nameof(GetEventsAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(PlainEventDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventsAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] bool hostedByMeOnly = false,
        [FromQuery] bool descendingByScheduledFor = false,
        CancellationToken cancellationToken = default
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var query = _apiDbContext.Events.AsQueryable();
        if (hostedByMeOnly)
            query = query.Where(q => q.HostedByFk == userId);
        else if (!User.HasClaim(Claims.Administrative.All, string.Empty)
                 && !User.HasClaim(Claims.Administrative.Event, string.Empty))
            query = query.Where(q => q.IsVisible || q.HostedByFk == userId);

        query = descendingByScheduledFor
            ? query.OrderByDescending(q => q.ScheduledFor)
            : query.OrderBy(q => q.ScheduledFor);
        query = query.Skip(skip)
            .Take(take);

        var results = await query.Select(e => new PlainEventDto
                {
                    PrimaryKey           = e.PrimaryKey,
                    Title                = e.Title,
                    Description          = e.Description,
                    TerrainFk            = e.TerrainFk,
                    ModPackRevisionFk    = e.ModPackRevisionFk,
                    Image                = e.Image,
                    ImageMimeType        = e.ImageMimeType,
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
                    MetaAcceptance       = e.UserMetas!.SingleOrDefault(meta => meta.UserFk == userId)!.Acceptance,
                }
            )
            .ToArrayAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Returns the count of all <see cref="Event"/>'s.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="hostedByMeOnly">
    ///     If <see langword="true"/>, will only account for events where the <see cref="Event.HostedBy"/> is the calling user.
    /// </param>
    /// <returns>The upcoming <see cref="Event"/>'ss and the data required to display them to the current user.</returns>
    [Authorize]
    [HttpGet("all/count", Name = nameof(GetEventsCountAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventsCountAsync(
        [FromQuery] bool hostedByMeOnly = false,
        CancellationToken cancellationToken = default
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var query = _apiDbContext.Events.AsQueryable();
        if (hostedByMeOnly)
            query = query.Where(q => q.HostedByFk == userId);
        else if (!User.HasClaim(Claims.Administrative.All, string.Empty)
                 && !User.HasClaim(Claims.Administrative.Event, string.Empty))
            query = query.Where(q => q.IsVisible || q.HostedByFk == userId);

        var result = await query.LongCountAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single <see cref="Event"/> by it's <paramref name="eventId"/>.
    /// </summary>
    /// <returns>
    ///     The <see cref="Event"/> found or <see langword="null"/>
    ///     if no event with that <paramref name="eventId"/> was found.
    /// </returns>
    [AllowAnonymous]
    [HttpGet("{eventId:guid}", Name = nameof(GetEventAsync))]
    [ProducesResponseType<FullEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetEventAsync([FromRoute] Guid eventId, CancellationToken cancellationToken)
    {
        IQueryable<FullEventDto> query;
        if (User.TryGetUserId(out var userId))
        {
            query = _apiDbContext.Events
                .Select(e => new FullEventDto
                    {
                        PrimaryKey                        = e.PrimaryKey,
                        Title                             = e.Title,
                        Description                       = e.Description,
                        TerrainPrimaryKey                 = e.Terrain!.PrimaryKey,
                        TerrainTitle                      = e.Terrain!.Title,
                        TerrainImage                      = e.Terrain!.Image,
                        TerrainImageMimeType              = e.Terrain!.ImageMimeType,
                        TerrainIsActive                   = e.Terrain!.IsActive,
                        ModPackRevisionPrimaryKey         = e.ModPackRevision!.PrimaryKey,
                        ModPackRevisionTimeStampCreated   = e.ModPackRevision!.TimeStampCreated,
                        ModPackRevisionHtml               = e.ModPackRevision!.Html,
                        ModPackRevisionIsActive           = e.ModPackRevision.IsActive,
                        ModPackDefinitionPrimaryKey       = e.ModPackRevision.Definition!.PrimaryKey,
                        ModPackDefinitionTimeStampCreated = e.ModPackRevision.Definition!.TimeStampCreated,
                        ModPackDefinitionTitle            = e.ModPackRevision.Definition!.Title,
                        ModPackDefinitionIsActive         = e.ModPackRevision.Definition.IsActive,
                        ModPackDefinitionIsComposition    = e.ModPackRevision.Definition.IsComposition,
                        Image                             = e.Image,
                        ImageMimeType                     = e.ImageMimeType,
                        ScheduledForOriginal              = e.ScheduledForOriginal,
                        ScheduledFor                      = e.ScheduledFor,
                        TimeStampCreated                  = e.TimeStampCreated,
                        IsVisible                         = e.IsVisible,
                        AcceptedCount                     = e.AcceptedCount,
                        RejectedCount                     = e.RejectedCount,
                        MaybeCount                        = e.MaybeCount,
                        MinimumAccepted                   = e.MinimumAccepted,
                        Owner = new PlainUserDto
                        {
                            PrimaryKey     = e.Owner!.PrimaryKey,
                            Nickname       = e.Owner!.Nickname,
                            Avatar         = e.Owner!.Avatar,
                            AvatarMimeType = e.Owner!.AvatarMimeType,
                        },
                        HostedBy = new PlainUserDto
                        {
                            PrimaryKey     = e.HostedBy!.PrimaryKey,
                            Nickname       = e.HostedBy!.Nickname,
                            Avatar         = e.HostedBy!.Avatar,
                            AvatarMimeType = e.HostedBy!.AvatarMimeType,
                        },
                        ModPackRevisionTimeStampDownloaded =
                            e.ModPackRevision!.UserMetas!.SingleOrDefault(meta => meta.UserFk == userId)!
                                .TimeStampDownloaded,
                        ModPackDefinitionOwner = new PlainUserDto
                        {
                            PrimaryKey     = e.ModPackRevision.Definition!.Owner!.PrimaryKey,
                            Nickname       = e.ModPackRevision.Definition!.Owner!.Nickname,
                            Avatar         = e.ModPackRevision.Definition!.Owner!.Avatar,
                            AvatarMimeType = e.ModPackRevision.Definition!.Owner!.AvatarMimeType,
                        },
                        ModPackRevisionUpdatedBy = new PlainUserDto
                        {
                            PrimaryKey     = e.ModPackRevision.UpdatedBy!.PrimaryKey,
                            Nickname       = e.ModPackRevision.UpdatedBy!.Nickname,
                            Avatar         = e.ModPackRevision.UpdatedBy!.Avatar,
                            AvatarMimeType = e.ModPackRevision.UpdatedBy!.AvatarMimeType,
                        },
                    }
                )
                .OrderBy(q => q.ScheduledFor);
        }
        else
        {
            query = _apiDbContext.Events
                .Select(e => new FullEventDto
                    {
                        PrimaryKey                        = e.PrimaryKey,
                        Title                             = e.Title,
                        Description                       = e.Description,
                        TerrainPrimaryKey                 = e.Terrain!.PrimaryKey,
                        TerrainTitle                      = e.Terrain!.Title,
                        TerrainImage                      = e.Terrain!.Image,
                        TerrainImageMimeType              = e.Terrain!.ImageMimeType,
                        TerrainIsActive                   = e.Terrain!.IsActive,
                        ModPackRevisionPrimaryKey         = e.ModPackRevision!.PrimaryKey,
                        ModPackRevisionTimeStampCreated   = e.ModPackRevision!.TimeStampCreated,
                        ModPackRevisionHtml               = e.ModPackRevision!.Html,
                        ModPackRevisionIsActive           = e.ModPackRevision.IsActive,
                        ModPackDefinitionPrimaryKey       = e.ModPackRevision.Definition!.PrimaryKey,
                        ModPackDefinitionTimeStampCreated = e.ModPackRevision.Definition!.TimeStampCreated,
                        ModPackDefinitionTitle            = e.ModPackRevision.Definition!.Title,
                        ModPackDefinitionIsActive         = e.ModPackRevision.Definition.IsActive,
                        ModPackDefinitionIsComposition    = e.ModPackRevision.Definition.IsComposition,
                        Image                             = e.Image,
                        ImageMimeType                     = e.ImageMimeType,
                        ScheduledForOriginal              = e.ScheduledForOriginal,
                        ScheduledFor                      = e.ScheduledFor,
                        TimeStampCreated                  = e.TimeStampCreated,
                        IsVisible                         = e.IsVisible,
                        AcceptedCount                     = e.AcceptedCount,
                        RejectedCount                     = e.RejectedCount,
                        MaybeCount                        = e.MaybeCount,
                        MinimumAccepted                   = e.MinimumAccepted,
                        Owner = new PlainUserDto
                        {
                            PrimaryKey     = e.Owner!.PrimaryKey,
                            Nickname       = e.Owner!.Nickname,
                            Avatar         = e.Owner!.Avatar,
                            AvatarMimeType = e.Owner!.AvatarMimeType,
                        },
                        HostedBy = new PlainUserDto
                        {
                            PrimaryKey     = e.HostedBy!.PrimaryKey,
                            Nickname       = e.HostedBy!.Nickname,
                            Avatar         = e.HostedBy!.Avatar,
                            AvatarMimeType = e.HostedBy!.AvatarMimeType,
                        },
                        ModPackRevisionTimeStampDownloaded =
                            e.ModPackRevision!.UserMetas!.SingleOrDefault(meta => meta.UserFk == userId)!
                                .TimeStampDownloaded,
                        ModPackDefinitionOwner = new PlainUserDto
                        {
                            PrimaryKey     = e.ModPackRevision.Definition!.Owner!.PrimaryKey,
                            Nickname       = e.ModPackRevision.Definition!.Owner!.Nickname,
                            Avatar         = e.ModPackRevision.Definition!.Owner!.Avatar,
                            AvatarMimeType = e.ModPackRevision.Definition!.Owner!.AvatarMimeType,
                        },
                        ModPackRevisionUpdatedBy = new PlainUserDto
                        {
                            PrimaryKey     = e.ModPackRevision.UpdatedBy!.PrimaryKey,
                            Nickname       = e.ModPackRevision.UpdatedBy!.Nickname,
                            Avatar         = e.ModPackRevision.UpdatedBy!.Avatar,
                            AvatarMimeType = e.ModPackRevision.UpdatedBy!.AvatarMimeType,
                        },
                    }
                )
                .OrderBy(q => q.ScheduledFor);
        }

        var single = await query.SingleOrDefaultAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (single is null)
            return NoContent();
        if (!single.IsVisible
            && single.HostedBy.PrimaryKey != userId
            && !User.HasClaim(Claims.Administrative.All, string.Empty)
            && !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return NoContent();
        return Ok(single);
    }

    /// <summary>
    /// Returns the users which have interacted with an <see cref="Event"/>'s acceptance status.
    /// </summary>
    /// <param name="eventId">
    ///     The <see cref="Event.PrimaryKey"/> of the <see cref="Event"/> to get the <see cref="User"/>'s from.
    /// </param>
    /// <param name="acceptance">
    ///     If provided, only <see cref="User"/>'s with the given
    ///     <see cref="UserEventMeta.Acceptance"/> will be returned.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    ///     The <see cref="User"/>'s which have set their status for the <see cref="Event"/> to either
    ///     <see cref="EEventAcceptance.Accepted"/>, <see cref="EEventAcceptance.Maybe"/>
    ///     or <see cref="EEventAcceptance.Rejected"/>.
    /// </returns>
    [AllowAnonymous]
    [HttpGet("{eventId:guid}/users", Name = nameof(GetEventUsersAsync))]
    [ProducesResponseType<PlainUserDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<User[]>> GetEventUsersAsync(
        [FromRoute] Guid eventId,
        [FromQuery] EEventAcceptance? acceptance = default,
        CancellationToken cancellationToken = default
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var eventItem = await _apiDbContext.Events.SingleOrDefaultAsync(
            q => q.PrimaryKey == eventId,
            cancellationToken
        );
        if (eventItem is null)
            return NotFound();
        if (!eventItem.IsVisible
            && eventItem.HostedByFk != userId
            && !User.HasClaim(Claims.Administrative.All, string.Empty)
            && !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return Forbid();
        PlainUserDto[] results;
        if (acceptance is not null)
        {
            var tmp = acceptance.Value;
            results = await _apiDbContext.UserEventMetas
                .Where(meta => meta.EventFk == eventId)
                .Where(meta => meta.Acceptance == tmp)
                .Select(meta => meta.User!)
                .Select(e => new PlainUserDto
                    {
                        PrimaryKey     = e.PrimaryKey,
                        Nickname       = e.Nickname,
                        Avatar         = e.Avatar,
                        AvatarMimeType = e.AvatarMimeType,
                    }
                )
                .ToArrayAsync(cancellationToken);
        }
        else
        {
            results = await _apiDbContext.UserEventMetas
                .Where(meta => meta.EventFk == eventId)
                .Select(meta => meta.User!)
                .Select(e => new PlainUserDto
                    {
                        PrimaryKey     = e.PrimaryKey,
                        Nickname       = e.Nickname,
                        Avatar         = e.Avatar,
                        AvatarMimeType = e.AvatarMimeType,
                    }
                )
                .ToArrayAsync(cancellationToken);
        }

        return Ok(results);
    }

    /// <summary>
    /// Creates a new <see cref="Event"/>.
    /// </summary>
    /// <param name="payload">The event to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>The created event</returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [Authorize(Claims.Creation.Events)]
    [HttpPost("create", Name = nameof(CreateEventAsync))]
    [ProducesResponseType<PlainEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateEventAsync(
        [FromBody] EventCreationPayload payload,
        CancellationToken cancellationToken
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        // ToDo: Log audit
        var newEvent = new Event
        {
            Terrain              = null,
            HostedBy             = null,
            Owner                = null,
            UserMetas            = null,
            ModPackRevision      = null,
            PrimaryKey           = default,
            OwnerFk              = userId,
            TimeStampCreated     = DateTimeOffset.Now,
            ScheduledFor         = payload.ScheduledFor,
            Title                = payload.Title,
            Description          = payload.Description,
            IsVisible            = payload.IsVisible,
            AcceptedCount        = payload.AcceptedCount,
            RejectedCount        = payload.RejectedCount,
            MaybeCount           = payload.MaybeCount,
            MinimumAccepted      = payload.MinimumAccepted,
            ScheduledForOriginal = payload.ScheduledFor,
            HostedByFk           = payload.HostedByFk,
            ModPackRevisionFk    = payload.ModPackRevisionFk,
            Image                = payload.Image,
            ImageMimeType        = payload.ImageMimeType,
            TerrainFk            = payload.TerrainFk,
        };

        var entity = await _apiDbContext.Events.AddAsync(newEvent, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await CreateOrUpdateDiscordEventAsync(newEvent, true)
            .ConfigureAwait(false);
        await _updateStreamService.SendUpdateAsync(
                $"{Constants.Routes.Events}/{newEvent.PrimaryKey}",
                new Contract.UpdateStream.Eventing.EventCreatedMessage
                {
                    EventId = newEvent.PrimaryKey,
                }
            )
            .ConfigureAwait(false);
        return Ok(
            new PlainEventDto
            {
                PrimaryKey           = entity.Entity.PrimaryKey,
                Title                = entity.Entity.Title,
                Description          = entity.Entity.Description,
                TerrainFk            = entity.Entity.TerrainFk,
                ModPackRevisionFk    = entity.Entity.ModPackRevisionFk,
                Image                = entity.Entity.Image,
                ImageMimeType        = entity.Entity.ImageMimeType,
                ScheduledForOriginal = entity.Entity.ScheduledForOriginal,
                ScheduledFor         = entity.Entity.ScheduledFor,
                TimeStampCreated     = entity.Entity.TimeStampCreated,
                IsVisible            = entity.Entity.IsVisible,
                AcceptedCount        = entity.Entity.AcceptedCount,
                RejectedCount        = entity.Entity.RejectedCount,
                MaybeCount           = entity.Entity.MaybeCount,
                MinimumAccepted      = entity.Entity.MinimumAccepted,
                OwnerFk              = entity.Entity.OwnerFk,
                HostedByFk           = entity.Entity.HostedByFk,
                MetaAcceptance       = null,
            }
        );
    }

    /// <summary>
    /// Updates an existing event based on the provided event ID and payload.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event to update.</param>
    /// <param name="payload">The <see cref="PlainEventDto"/> containing the updated event details.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> indicating the result of the operation. Possible responses include:
    /// <list type="bullet">
    /// <item>401 Unauthorized: If the user is not authenticated.</item>
    /// <item>403 Forbidden: If the user is not authorized to update the event.</item>
    /// <item>204 No Content: If the event was successfully updated.</item>
    /// </list>
    /// </returns>
    [Authorize]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpPost("{eventId:guid}/update", Name = nameof(UpdateEventAsync))]
    public async Task<IActionResult> UpdateEventAsync(
        [FromRoute] Guid eventId,
        [FromBody] EventUpdate payload,
        CancellationToken cancellationToken
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events.SingleAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent.HostedByFk != userId
            && !User.HasClaim(Claims.Administrative.All, string.Empty)
            && !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return Forbid();
        var imageChanged = false;
        // ToDo: Log audit
        if (payload.Title is not null)
            existingEvent.Title = payload.Title;
        if (payload.Description is not null)
            existingEvent.Description = payload.Description;
        if (payload.ScheduledFor is not null)
            existingEvent.ScheduledFor = payload.ScheduledFor.Value;
        if (payload.HostedByFk is not null)
            existingEvent.HostedByFk = payload.HostedByFk.Value;
        if (payload.ModPackRevisionFk is not null)
            existingEvent.ModPackRevisionFk = payload.ModPackRevisionFk.Value;
        if (payload.TerrainFk is not null)
            existingEvent.TerrainFk = payload.TerrainFk.Value;
        if (payload.MinimumAccepted is not null)
            existingEvent.MinimumAccepted = payload.MinimumAccepted.Value;
        if (payload.IsVisible is not null)
            existingEvent.IsVisible = payload.IsVisible.Value;
        if (payload.Image is not null && payload.ImageMimeType is not null)
        {
            existingEvent.ImageMimeType = payload.ImageMimeType;
            existingEvent.Image         = payload.Image;
            imageChanged                = true;
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await CreateOrUpdateDiscordEventAsync(existingEvent, imageChanged)
            .ConfigureAwait(false);
        await _updateStreamService.SendUpdateAsync(
                $"{Constants.Routes.Events}/{eventId}",
                new Contract.UpdateStream.Eventing.EventChangedMessage
                {
                    Title             = existingEvent.Title,
                    Description       = existingEvent.Description,
                    ScheduledFor      = existingEvent.ScheduledFor,
                    HostedById        = existingEvent.HostedByFk,
                    ModPackRevisionId = existingEvent.ModPackRevisionFk,
                    TerrainFk         = existingEvent.TerrainFk,
                    MinimumAccepted   = existingEvent.MinimumAccepted,
                    IsVisible         = existingEvent.IsVisible,
                    Image             = payload.Image != existingEvent.Image ? null : existingEvent.Image,
                    ImageMimeType     = payload.Image != existingEvent.Image ? null : existingEvent.ImageMimeType,
                    EventId           = existingEvent.PrimaryKey,
                }
            )
            .ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Changes the calling users acceptance status for the given event to <see cref="EEventAcceptance.Accepted"/>.
    /// </summary>
    /// <param name="eventId">The id for the <see cref="Event"/> to change the acceptance of.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [Authorize]
    [HttpPost("{eventId:guid}/accept", Name = nameof(AcceptEventAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AcceptEventAsync([FromRoute] Guid eventId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        await EventUtils.SetAcceptanceOfEventAsync(
            _updateStreamService,
            _logger,
            _apiDbContext,
            eventId,
            userId,
            EEventAcceptance.Accepted,
            cancellationToken
        );
        return NoContent();
    }

    /// <summary>
    /// Changes the calling users acceptance status for the given event to <see cref="EEventAcceptance.Maybe"/>.
    /// </summary>
    /// <remarks>
    /// Any slot selection for the <see cref="Event"/> will be removed in this process.
    /// </remarks>
    /// <param name="eventId">The id for the <see cref="Event"/> to change the acceptance of.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [Authorize]
    [HttpPost("{eventId:guid}/maybe", Name = nameof(MaybeEventAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MaybeEventAsync([FromRoute] Guid eventId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        await EventUtils.SetAcceptanceOfEventAsync(
            _updateStreamService,
            _logger,
            _apiDbContext,
            eventId,
            userId,
            EEventAcceptance.Maybe,
            cancellationToken
        );
        return NoContent();
    }

    /// <summary>
    /// Changes the calling users acceptance status for the given event to <see cref="EEventAcceptance.Rejected"/>.
    /// </summary>
    /// <remarks>
    /// Any slot selection for the <see cref="Event"/> will be removed in this process.
    /// </remarks>
    /// <param name="eventId">The id for the <see cref="Event"/> to change the acceptance of.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [Authorize]
    [HttpPost("{eventId:guid}/reject", Name = nameof(RejectEventAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RejectEventAsync([FromRoute] Guid eventId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        await EventUtils.SetAcceptanceOfEventAsync(
            _updateStreamService,
            _logger,
            _apiDbContext,
            eventId,
            userId,
            EEventAcceptance.Rejected,
            cancellationToken
        );
        return NoContent();
    }

    private async Task CreateOrUpdateDiscordEventAsync(Event appEvent, bool imageChanged)
    {
        await (_discordBot?.WithDiscordAsync(async client =>
                   {
                       var eventUrl = _baseUrl.ResolveClientUrl($"/events/{appEvent.PrimaryKey}");
                       foreach (var guild in client.Guilds)
                       {
                           var guildEvents = await guild.GetEventsAsync()
                               .ConfigureAwait(false);
                           var guildEvent = guildEvents.FirstOrDefault(q => q.Location == eventUrl);
                           if (guildEvent is not null)
                           {
                               await guildEvent.ModifyAsync(properties =>
                                   {
                                       if (guildEvent.Name != appEvent.Title)
                                           properties.Name = appEvent.Title;
                                       if (guildEvent.Description != appEvent.Description)
                                           properties.Description = appEvent.Description.Length > 1000
                                               ? appEvent.Description[..1000]
                                               : appEvent.Description;
                                       if (guildEvent.StartTime != appEvent.ScheduledFor)
                                       {
                                           properties.StartTime = appEvent.ScheduledFor;
                                           properties.EndTime   = appEvent.ScheduledFor.AddHours(4);
                                       }

                                       if (imageChanged)
                                           properties.CoverImage = new Image(new MemoryStream(appEvent.Image));
                                   }
                               );
                           }
                           else
                           {
                               var response = await guild.CreateEventAsync(
                                       name: appEvent.Title,
                                       startTime: appEvent.ScheduledFor,
                                       endTime: appEvent.ScheduledFor.AddHours(4),
                                       type: GuildScheduledEventType.External,
                                       description: appEvent.Description.Length > 1000
                                           ? appEvent.Description[..1000]
                                           : appEvent.Description,
                                       location: eventUrl
                                   )
                                   .ConfigureAwait(false);
                               await response.ModifyAsync(properties
                                       => properties.CoverImage = new Image(new MemoryStream(appEvent.Image))
                                   )
                                   .ConfigureAwait(false);
                           }
                       }
                   }
               )
               ?? Task.CompletedTask);
    }
}
