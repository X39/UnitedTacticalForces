using System.Net;
using Discord;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.HostedServices;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Common;
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
        BaseUrl baseUrl)
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
    public async Task<IEnumerable<Event>> GetUpcomingEventsAsync(CancellationToken cancellationToken)
    {
        IQueryable<Event> query;
        if (User.TryGetUserId(out var userId))
        {
            query = _apiDbContext.Events
                .Include((e) => e.Terrain)
                .Include((e) => e.ModPackRevision)
                .ThenInclude((e) => e!.UserMetas!.Where((q) => q.UserFk == userId))
                .Include((e) => e.ModPackRevision)
                .ThenInclude((e) => e.Definition)
                .Include((e) => e.Owner)
                .Include((e) => e.HostedBy)
                .Include((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
                .Where((q) => q.ScheduledFor >= DateTime.Today)
                .Where((q) => q.IsVisible)
                .OrderBy((q) => q.ScheduledFor);
        }
        else
        {
            query = _apiDbContext.Events
                .Include((e) => e.Terrain)
                .Include((e) => e.ModPackRevision)
                .ThenInclude((e) => e!.UserMetas!.Where((q) => q.UserFk == userId))
                .Include((e) => e.ModPackRevision)
                .ThenInclude((e) => e.Definition)
                .Include((e) => e.Owner)
                .Include((e) => e.HostedBy)
                .Where((q) => q.ScheduledFor >= DateTime.Today)
                .Where((q) => q.IsVisible)
                .OrderBy((q) => q.ScheduledFor);
        }

        return await query.ToArrayAsync(cancellationToken);
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
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(IEnumerable<Event>), (int) HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<Event>>> GetEventsAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] bool hostedByMeOnly = false,
        [FromQuery] bool descendingByScheduledFor = false,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var query = _apiDbContext.Events
            .AsQueryable();
        if (hostedByMeOnly)
            query = query.Where((q) => q.HostedByFk == userId);
        else if (!User.HasClaim(Claims.Administrative.All, string.Empty) &&
                 !User.HasClaim(Claims.Administrative.Event, string.Empty))
            query = query.Where((q) => q.IsVisible || q.HostedByFk == userId);

        query = descendingByScheduledFor
            ? query.OrderByDescending((q) => q.ScheduledFor)
            : query.OrderBy((q) => q.ScheduledFor);
        query = query.Skip(skip).Take(take);

        return await query.ToArrayAsync(cancellationToken);
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
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(long), (int) HttpStatusCode.OK)]
    public async Task<ActionResult<long>> GetEventsCountAsync(
        [FromQuery] bool hostedByMeOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var query = _apiDbContext.Events
            .AsQueryable();
        if (hostedByMeOnly)
            query = query.Where((q) => q.HostedByFk == userId);
        else if (!User.HasClaim(Claims.Administrative.All, string.Empty) &&
                 !User.HasClaim(Claims.Administrative.Event, string.Empty))
            query = query.Where((q) => q.IsVisible || q.HostedByFk == userId);


        return await query.LongCountAsync(cancellationToken);
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
    [ProducesResponseType(typeof(Event), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    public async Task<Event?> GetEventAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        IQueryable<Event> query;
        if (User.TryGetUserId(out var userId))
        {
            query = _apiDbContext.Events
                .Include((e) => e.Terrain)
                .Include((e) => e.ModPackRevision)
                .ThenInclude((e) => e!.UserMetas!.Where((q) => q.UserFk == userId))
                .Include((e) => e.ModPackRevision)
                .ThenInclude((e) => e.Definition)
                .Include((e) => e.Owner)
                .Include((e) => e.HostedBy)
                .Include((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
                .OrderBy((q) => q.ScheduledFor);
        }
        else
        {
            query = _apiDbContext.Events
                .Include((e) => e.Terrain)
                .Include((e) => e.ModPackRevision)
                .ThenInclude((e) => e!.UserMetas!.Where((q) => q.UserFk == userId))
                .Include((e) => e.ModPackRevision)
                .ThenInclude((e) => e.Definition)
                .Include((e) => e.Owner)
                .Include((e) => e.HostedBy)
                .OrderBy((q) => q.ScheduledFor);
        }

        var single = await query.SingleOrDefaultAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (single is null)
            return null;
        if (!single.IsVisible && single.HostedByFk != userId &&
            !User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return null;
        return single;
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
    [ProducesResponseType(typeof(User[]), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    public async Task<ActionResult<User[]>> GetEventUsersAsync(
        [FromRoute] Guid eventId,
        [FromQuery] EEventAcceptance? acceptance = default,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var eventItem = await _apiDbContext.Events
            .SingleOrDefaultAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (eventItem is null)
            return NotFound();
        if (!eventItem.IsVisible && eventItem.HostedByFk != userId &&
            !User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return Forbid();
        User[] usersQuery;
        if (acceptance is not null)
        {
            var tmp = acceptance.Value;
            usersQuery = await _apiDbContext.UserEventMetas
                .Where(meta => meta.EventFk == eventId)
                .Where(meta => meta.Acceptance == tmp)
                .Select(meta => meta.User!)
                .ToArrayAsync(cancellationToken);
        }
        else
        {
            usersQuery = await _apiDbContext.UserEventMetas
                .Where(meta => meta.EventFk == eventId)
                .Select(meta => meta.User!)
                .ToArrayAsync(cancellationToken);
        }

        return Ok(usersQuery);
    }

    /// <summary>
    /// Creates a new <see cref="Event"/>.
    /// </summary>
    /// <param name="newEvent">The event to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>The created event</returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    [Authorize(Claims.Creation.Events)]
    [HttpPost("create", Name = nameof(CreateEventAsync))]
    [ProducesResponseType(typeof(Event), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<Event>> CreateEventAsync(
        [FromBody] Event newEvent,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        // ToDo: Log audit
        newEvent.Owner                = null;
        newEvent.OwnerFk              = userId;
        newEvent.UserMetas            = null;
        newEvent.TimeStampCreated     = DateTimeOffset.Now;
        newEvent.ScheduledForOriginal = newEvent.ScheduledFor;
        newEvent.HostedByFk           = newEvent.HostedBy?.PrimaryKey ?? newEvent.HostedByFk;
        newEvent.HostedBy             = null;
        newEvent.ModPackRevisionFk    = newEvent.ModPackRevision?.PrimaryKey ?? newEvent.ModPackRevisionFk;
        newEvent.ModPackRevision      = null;
        newEvent.TerrainFk            = newEvent.Terrain?.PrimaryKey ?? newEvent.TerrainFk;
        newEvent.Terrain              = null;
        var entity = await _apiDbContext.Events.AddAsync(newEvent, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await CreateOrUpdateDiscordEventAsync(newEvent, true)
            .ConfigureAwait(false);
        await _updateStreamService.SendUpdateAsync(
                $"{Constants.Routes.Events}/{newEvent.PrimaryKey}",
                new Contract.UpdateStream.Eventing.EventCreatedMessage
                {
                    EventId = newEvent.PrimaryKey,
                })
            .ConfigureAwait(false);
        return Ok(entity.Entity);
    }

    [Authorize]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [HttpPost("{eventId:guid}/update", Name = nameof(UpdateEventAsync))]
    public async Task<ActionResult> UpdateEventAsync(
        [FromRoute] Guid eventId,
        [FromBody] Event updatedEvent,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events.SingleAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent.HostedByFk != userId && !User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return Forbid();
        bool imageChanged = false;
        // ToDo: Log audit
        existingEvent.Title        = updatedEvent.Title;
        existingEvent.Description  = updatedEvent.Description;
        existingEvent.ScheduledFor = updatedEvent.ScheduledFor;

        existingEvent.HostedByFk        = updatedEvent.HostedBy?.PrimaryKey ?? updatedEvent.HostedByFk;
        existingEvent.ModPackRevisionFk = updatedEvent.ModPackRevision?.PrimaryKey ?? updatedEvent.ModPackRevisionFk;
        existingEvent.TerrainFk         = updatedEvent.Terrain?.PrimaryKey ?? updatedEvent.TerrainFk;
        existingEvent.MinimumAccepted   = updatedEvent.MinimumAccepted;
        existingEvent.IsVisible         = updatedEvent.IsVisible;
        if (updatedEvent.Image != existingEvent.Image)
        {
            existingEvent.ImageMimeType = updatedEvent.ImageMimeType;
            existingEvent.Image         = updatedEvent.Image;
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
                    Image = updatedEvent.Image != existingEvent.Image
                        ? null
                        : existingEvent.Image,
                    ImageMimeType = updatedEvent.Image != existingEvent.Image
                        ? null
                        : existingEvent.ImageMimeType,
                    EventId = existingEvent.PrimaryKey,
                })
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
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    public async Task<ActionResult> AcceptEventAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
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
            cancellationToken);
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
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    public async Task<ActionResult> MaybeEventAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
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
            cancellationToken);
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
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    public async Task<ActionResult> RejectEventAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
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
            cancellationToken);
        return NoContent();
    }

    private async Task CreateOrUpdateDiscordEventAsync(Event appEvent, bool imageChanged)
    {
        await (_discordBot?.WithDiscordAsync(
            async (client) =>
            {
                var eventUrl = _baseUrl.ResolveClientUrl($"/events/{appEvent.PrimaryKey}");
                foreach (var guild in client.Guilds)
                {
                    var guildEvents = await guild.GetEventsAsync()
                        .ConfigureAwait(false);
                    var guildEvent = guildEvents.FirstOrDefault((q) => q.Location == eventUrl);
                    if (guildEvent is not null)
                    {
                        await guildEvent.ModifyAsync(
                            (properties) =>
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
                            });
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
                                location: eventUrl)
                            .ConfigureAwait(false);
                        await response.ModifyAsync(
                                (properties) =>
                                    properties.CoverImage = new Image(new MemoryStream(appEvent.Image)))
                            .ConfigureAwait(false);
                    }
                }
            }) ?? Task.CompletedTask);
    }
}