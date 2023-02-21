using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class EventsController : ControllerBase
{
    private readonly ILogger<EventsController> _logger;
    private readonly ApiDbContext _apiDbContext;

    public EventsController(ILogger<EventsController> logger, ApiDbContext apiDbContext)
    {
        _logger = logger;
        _apiDbContext = apiDbContext;
    }

    [HttpGet("upcoming", Name = nameof(GetEventsAsync))]
    public Task<IEnumerable<Event>> GetEventsAsync()
    {
        return Task.FromResult<IEnumerable<Event>>(_apiDbContext.Events
            .Include((e) => e.Terrain)
            .Include((e) => e.ModPack)
            .Include((e) => e.CreatedBy)
            .Include((e) => e.HostedBy));
    }

    [Authorize]
    [HttpPost("{eventId:guid}/subscribe", Name = nameof(SubscribeToEventAsync))]
    public Task SubscribeToEventAsync(
        [FromRoute] Guid eventId)
    {
        throw new NotImplementedException();
    }

    [Authorize]
    [HttpPost("{eventId:guid}/unsubscribe", Name = nameof(UnsubscribeFromEventAsync))]
    public Task UnsubscribeFromEventAsync(
        [FromRoute] Guid eventId)
    {
        throw new NotImplementedException();
    }

    [Authorize]
    [HttpPost("create", Name = nameof(CreateEventAsync))]
    public async Task<Event> CreateEventAsync([FromBody] Event newEvent, CancellationToken cancellationToken)
    {
        newEvent.TimeStampCreated = DateTimeOffset.Now;
        newEvent.ScheduledForOriginal = newEvent.ScheduledForOriginal;
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
        var existingEvent = await _apiDbContext.Events.SingleAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        existingEvent.Title = updatedEvent.Title;
        existingEvent.Description = updatedEvent.Description;
        existingEvent.ScheduledFor = updatedEvent.ScheduledFor;
        existingEvent.Image = updatedEvent.Image;
        existingEvent.ImageMimeType = updatedEvent.ImageMimeType;
        existingEvent.HostedByFk = updatedEvent.HostedByFk;
        existingEvent.ModPackFk = updatedEvent.ModPackFk;
        existingEvent.TerrainFk = updatedEvent.TerrainFk;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }
}