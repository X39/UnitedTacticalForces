using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.ExtensionMethods;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class EventsController : ControllerBase
{
    private readonly ILogger<EventsController> _logger;
    private readonly ApiDbContext              _apiDbContext;

    public EventsController(ILogger<EventsController> logger, ApiDbContext apiDbContext)
    {
        _logger       = logger;
        _apiDbContext = apiDbContext;
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
                .Include((e) => e.ModPack)
                .ThenInclude((e) => e!.UserMetas!.Where((q) => q.UserFk == userId))
                .Include((e) => e.Owner)
                .Include((e) => e.HostedBy)
                .Include((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
                .Where((q) => q.ScheduledFor >= DateTime.Today)
                .OrderBy((q) => q.ScheduledFor);
        }
        else
        {
            query = _apiDbContext.Events
                .Include((e) => e.Terrain)
                .Include((e) => e.ModPack)
                .Include((e) => e.Owner)
                .Include((e) => e.HostedBy)
                .Where((q) => q.ScheduledFor >= DateTime.Today)
                .OrderBy((q) => q.ScheduledFor);
        }

        return await query.ToArrayAsync(cancellationToken);
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
    [ProducesResponseType(typeof(Event), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int)HttpStatusCode.NoContent)]
    public async Task<Event?> GetEventAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        IQueryable<Event> query;
        if (User.TryGetUserId(out var userId))
        {
            query = _apiDbContext.Events
                .Include((e) => e.Terrain)
                .Include((e) => e.ModPack)
                .ThenInclude((e) => e!.UserMetas!.Where((q) => q.UserFk == userId))
                .Include((e) => e.Owner)
                .Include((e) => e.HostedBy)
                .Include((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
                .Where((q) => q.ScheduledFor >= DateTime.Today)
                .OrderBy((q) => q.ScheduledFor);
        }
        else
        {
            query = _apiDbContext.Events
                .Include((e) => e.Terrain)
                .Include((e) => e.ModPack)
                .Include((e) => e.Owner)
                .Include((e) => e.HostedBy)
                .Where((q) => q.ScheduledFor >= DateTime.Today)
                .OrderBy((q) => q.ScheduledFor);
        }

        return await query.SingleOrDefaultAsync((q) => q.PrimaryKey == eventId, cancellationToken);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.EventCreate)]
    [HttpPost("create", Name = nameof(CreateEventAsync))]
    public async Task<Event> CreateEventAsync([FromBody] Event newEvent, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            throw new UnauthorizedAccessException();
        newEvent.Owner                = null;
        newEvent.OwnerFk              = userId;
        newEvent.UserMetas            = null;
        newEvent.TimeStampCreated     = DateTimeOffset.Now;
        newEvent.ScheduledForOriginal = newEvent.ScheduledForOriginal;
        newEvent.HostedByFk           = newEvent.HostedBy?.PrimaryKey ?? newEvent.HostedByFk;
        newEvent.HostedBy             = null;
        newEvent.ModPackFk            = newEvent.ModPack?.PrimaryKey ?? newEvent.ModPackFk;
        newEvent.ModPack              = null;
        newEvent.TerrainFk            = newEvent.Terrain?.PrimaryKey ?? newEvent.TerrainFk;
        newEvent.Terrain              = null;
        // ToDo: Authorize
        var entity = await _apiDbContext.Events.AddAsync(newEvent, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return entity.Entity;
    }

    [Authorize]
    [HttpPost("{eventId:guid}/update", Name = nameof(UpdateEventAsync))]
    public async Task UpdateEventAsync(
        [FromRoute] Guid eventId,
        [FromBody] Event updatedEvent,
        CancellationToken cancellationToken)
    {
        // ToDo: Authorize
        var existingEvent = await _apiDbContext.Events.SingleAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        existingEvent.Title           = updatedEvent.Title;
        existingEvent.Description     = updatedEvent.Description;
        existingEvent.ScheduledFor    = updatedEvent.ScheduledFor;
        existingEvent.Image           = updatedEvent.Image;
        existingEvent.ImageMimeType   = updatedEvent.ImageMimeType;
        existingEvent.HostedByFk      = updatedEvent.HostedBy?.PrimaryKey ?? updatedEvent.HostedByFk;
        existingEvent.ModPackFk       = updatedEvent.ModPack?.PrimaryKey ?? updatedEvent.ModPackFk;
        existingEvent.TerrainFk       = updatedEvent.Terrain?.PrimaryKey ?? updatedEvent.TerrainFk;
        existingEvent.MinimumAccepted = updatedEvent.MinimumAccepted;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }

    [Authorize]
    [HttpPost("{eventId:guid}/accept", Name = nameof(AcceptEventAsync))]
    public async Task AcceptEventAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        await SetAcceptanceOfEventAsync(eventId, EEventAcceptance.Accepted, cancellationToken);
    }

    [Authorize]
    [HttpPost("{eventId:guid}/maybe", Name = nameof(MaybeEventAsync))]
    public async Task MaybeEventAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        await SetAcceptanceOfEventAsync(eventId, EEventAcceptance.Maybe, cancellationToken);
    }

    [Authorize]
    [HttpPost("{eventId:guid}/reject", Name = nameof(RejectEventAsync))]
    public async Task RejectEventAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        await SetAcceptanceOfEventAsync(eventId, EEventAcceptance.Rejected, cancellationToken);
    }

    private async Task SetAcceptanceOfEventAsync(
        Guid eventId,
        EEventAcceptance acceptance,
        CancellationToken cancellationToken)
    {
        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!User.TryGetUserId(out var userId))
            throw new UnauthorizedAccessException();
        var existingEvent = await _apiDbContext.Events
            .Include((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
            .SingleAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent.UserMetas?.FirstOrDefault() is { } userEventMeta)
        {
            switch (userEventMeta.Acceptance)
            {
                case EEventAcceptance.Rejected:
                    existingEvent.RejectedCount--;
                    break;
                case EEventAcceptance.Maybe:
                    existingEvent.MaybeCount--;
                    break;
                case EEventAcceptance.Accepted:
                    existingEvent.AcceptedCount--;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            userEventMeta.Acceptance = acceptance;
        }
        else
        {
            await _apiDbContext.UserEventMetas.AddAsync(
                new UserEventMeta
                {
                    User       = default,
                    UserFk     = userId,
                    Event      = default,
                    EventFk    = existingEvent.PrimaryKey,
                    Acceptance = acceptance,
                },
                cancellationToken);
        }

        switch (acceptance)
        {
            case EEventAcceptance.Rejected:
                existingEvent.RejectedCount++;
                break;
            case EEventAcceptance.Maybe:
                existingEvent.MaybeCount++;
                break;
            case EEventAcceptance.Accepted:
                existingEvent.AcceptedCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(acceptance), acceptance, null);
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
    }
}