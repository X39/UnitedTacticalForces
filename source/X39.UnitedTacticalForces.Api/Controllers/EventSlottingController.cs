using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Common;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route(Constants.Routes.Events + "/{eventId:guid}/" + Constants.Routes.EventSlotting)]
public class EventSlottingController : ControllerBase
{
    private readonly ILogger<EventSlottingController> _logger;
    private readonly ApiDbContext                     _apiDbContext;

    public EventSlottingController(ILogger<EventSlottingController> logger, ApiDbContext apiDbContext)
    {
        _logger       = logger;
        _apiDbContext = apiDbContext;
    }

    /// <summary>
    /// Receives all slots currently available on an <see cref="Event"/>, identified via the
    /// <paramref name="eventId"/>.
    /// </summary>
    /// <param name="eventId">The id of the <see cref="Event"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>All <see cref="EventSlot"/>'s currently available on an <see cref="Event"/></returns>
    [HttpGet("all", Name = nameof(AllAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(IEnumerable<EventSlot>), (int) HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<EventSlot>>> AllAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        var eventExists = await _apiDbContext.Events
            .AnyAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (!eventExists)
            return NotFound();
        var query = _apiDbContext.EventSlots
            .Include((e) => e.AssignedTo)
            .Where((q) => q.EventFk == eventId)
            .AsQueryable();
        if (!User.IsInRoleOrAdmin(Roles.EventSlotAssign, Roles.EventSlotUpdate, Roles.EventSlotIgnore))
        {
            query = query.Where((q) => q.IsVisible);
        }

        var results = await query
            .OrderBy((q) => q.SlotNumber)
            .ToArrayAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Receives the slot of the calling user (if available) on an <see cref="Event"/>, identified via the
    /// <paramref name="eventId"/>.
    /// </summary>
    /// <param name="eventId">The id of the <see cref="Event"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>All <see cref="EventSlot"/>'s currently available on an <see cref="Event"/> or NoContent if no slot was found.</returns>
    [HttpGet("my-slot", Name = nameof(MySlotAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(EventSlot), (int) HttpStatusCode.OK)]
    public async Task<ActionResult<EventSlot?>> MySlotAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var eventExists = await _apiDbContext.Events
            .AnyAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (!eventExists)
            return NotFound();
        var query = _apiDbContext.EventSlots
            .Include((e) => e.AssignedTo)
            .Where((q) => q.EventFk == eventId)
            .Where((q) => q.AssignedToFk == userId)
            .AsQueryable();
        var single = await query.SingleOrDefaultAsync(cancellationToken);

        return single is null ? NoContent() : Ok(single);
    }

    /// <summary>
    /// Unassign the user from a possibly slotted <see cref="Event"/>, identified via the <paramref name="eventId"/>.
    /// If the user is not slotted, nothing will be done. 
    /// </summary>
    /// <param name="eventId">The id of the <see cref="Event"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if the user was unassigned from a slot.
    ///     <see langword="false"/> if no slot was assigned.
    /// </returns>
    [HttpPost("unassign", Name = nameof(UnassignIfAssignedAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(bool), (int) HttpStatusCode.OK)]
    public async Task<ActionResult<bool>> UnassignIfAssignedAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var eventExists = await _apiDbContext.Events
            .AnyAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (!eventExists)
            return NotFound();
        var eventSlot = await _apiDbContext.EventSlots
            .Where((q) => q.EventFk == eventId)
            .Where((q) => q.AssignedToFk == userId)
            .SingleOrDefaultAsync(cancellationToken);
        if (eventSlot is null)
            return Ok(false);
        eventSlot.AssignedToFk = null;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    /// <summary>
    /// Assign the user to an <see cref="EventSlot"/>, identified via the <paramref name="slotNumber"/>
    /// in an <see cref="Event"/>, identified via the <paramref name="eventId"/>.
    /// If the user is already slotted, the old slot will be unassigned. 
    /// </summary>
    /// <param name="eventId">The id of the <see cref="Event"/>.</param>
    /// <param name="slotNumber">The id of the <see cref="EventSlot"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [HttpPost("{slotNumber:int}/assign", Name = nameof(AssignEventSlotToSelf))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    public async Task<ActionResult> AssignEventSlotToSelf(
        [FromRoute] Guid eventId,
        [FromRoute] long slotNumber,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();

        var newEventSlot = await _apiDbContext.EventSlots
            .Where((q) => q.EventFk == eventId && q.SlotNumber == slotNumber)
            .SingleOrDefaultAsync(cancellationToken);
        if (newEventSlot is null)
            return NotFound();
        if (!newEventSlot.IsSelfAssignable
            && !User.IsInRoleOrAdmin(Roles.EventSlotAssign, Roles.EventSlotIgnore)
            && existingEvent.HostedByFk != userId)
            return Forbid();
        if (newEventSlot.AssignedToFk is not null)
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .Where((q) => q.EventFk == eventId)
            .Where((q) => q.AssignedToFk == userId)
            .SingleOrDefaultAsync(cancellationToken);

        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        await EventUtils.SetAcceptanceOfEventAsync(
            _logger,
            _apiDbContext,
            eventId,
            userId,
            EEventAcceptance.Accepted,
            cancellationToken);

        if (existingEventSlot is not null)
            existingEventSlot.AssignedToFk = null;
        newEventSlot.AssignedToFk = userId;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Assign a <see cref="User"/>, identified via <paramref name="userId"/>,
    /// to an <see cref="EventSlot"/>, identified via the <paramref name="slotNumber"/>
    /// in an <see cref="Event"/>, identified via the <paramref name="eventId"/>.
    /// If the user is already slotted, the old slot will be unassigned. 
    /// </summary>
    /// <param name="eventId">The id of the <see cref="Event"/>.</param>
    /// <param name="slotNumber">The id of the <see cref="EventSlot"/>.</param>
    /// <param name="userId">The id of the <see cref="User"/></param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [HttpPost("{slotNumber:int}/assign/{userId:guid}", Name = nameof(AssignEventSlotToUserAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    public async Task<ActionResult<bool>> AssignEventSlotToUserAsync(
        [FromRoute] Guid eventId,
        [FromRoute] int slotNumber,
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (!User.IsInRoleOrAdmin(Roles.EventSlotAssign)
            && existingEvent.HostedByFk != userId)
            return Forbid();

        var newEventSlot = await _apiDbContext.EventSlots
            .Where((q) => q.EventFk == eventId && q.SlotNumber == slotNumber)
            .SingleOrDefaultAsync(cancellationToken);
        if (newEventSlot is null)
            return NotFound();
        if (newEventSlot.AssignedToFk is not null)
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .Where((q) => q.EventFk == eventId)
            .Where((q) => q.AssignedToFk == userId)
            .SingleOrDefaultAsync(cancellationToken);

        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        await EventUtils.SetAcceptanceOfEventAsync(
            _logger,
            _apiDbContext,
            eventId,
            userId,
            EEventAcceptance.Accepted,
            cancellationToken);
        if (existingEventSlot is not null)
            existingEventSlot.AssignedToFk = null;
        newEventSlot.AssignedToFk = userId;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Assign a <see cref="User"/>, identified via <paramref name="userId"/>,
    /// to an <see cref="EventSlot"/>, identified via the <paramref name="slotNumber"/>
    /// in an <see cref="Event"/>, identified via the <paramref name="eventId"/>.
    /// If the user is already slotted, the old slot will be unassigned. 
    /// </summary>
    /// <param name="eventId">The id of the <see cref="Event"/>.</param>
    /// <param name="slotNumber">The id of the <see cref="EventSlot"/>.</param>
    /// <param name="userId">The id of the <see cref="User"/></param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [HttpPost("{slotNumber:int}/unassign/{userId:guid}", Name = nameof(UnassignEventSlotToUserAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    public async Task<ActionResult<bool>> UnassignEventSlotToUserAsync(
        [FromRoute] Guid eventId,
        [FromRoute] int slotNumber,
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (!User.IsInRoleOrAdmin(Roles.EventSlotAssign)
            && existingEvent.HostedByFk != userId)
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .Where((q) => q.EventFk == eventId && q.SlotNumber == slotNumber)
            .Where((q) => q.AssignedToFk == userId)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingEventSlot is null)
            return NotFound();

        existingEventSlot.AssignedToFk = null;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Creates a new <see cref="EventSlot"/> in an <see cref="Event"/>, identified via the <paramref name="eventId"/>.
    /// </summary>
    /// <param name="eventId">The id of the <see cref="Event"/>.</param>
    /// <param name="eventSlot">The <see cref="EventSlot"/> to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [HttpPost("create", Name = nameof(CreateEventSlotAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(EventSlot), (int) HttpStatusCode.OK)]
    public async Task<ActionResult<EventSlot>> CreateEventSlotAsync(
        [FromRoute] Guid eventId,
        [FromBody] EventSlot eventSlot,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (!User.IsInRoleOrAdmin(Roles.EventSlotCreate)
            && existingEvent.HostedByFk != userId)
            return Forbid();
        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        var maxSlotNumber = await _apiDbContext.EventSlots
            .Where((q) => q.EventFk == eventId)
            .Select((q) => q.SlotNumber)
            .DefaultIfEmpty()
            .MaxAsync(cancellationToken);
        eventSlot.EventFk    = eventId;
        eventSlot.SlotNumber = checked((short) (maxSlotNumber + 1));
        await _apiDbContext.EventSlots.AddAsync(eventSlot, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return Ok(eventSlot);
    }

    /// <summary>
    /// Updates an existing <see cref="EventSlot"/> with new data.
    /// </summary>
    /// <param name="eventId">The <see cref="Event.PrimaryKey"/> of the <see cref="Event"/>.</param>
    /// <param name="slotNumber">The <see cref="EventSlot.SlotNumber"/> of the <see cref="EventSlot"/>.</param>
    /// <param name="updatedEventSlot">The <see cref="EventSlot"/> to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [HttpPost("{slotNumber:int}/update", Name = nameof(UpdateEventSlotAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    public async Task<ActionResult<EventSlot>> UpdateEventSlotAsync(
        [FromRoute] Guid eventId,
        [FromRoute] int slotNumber,
        [FromBody] EventSlot updatedEventSlot,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (!User.IsInRoleOrAdmin(Roles.EventSlotUpdate)
            && existingEvent.HostedByFk != userId)
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .SingleOrDefaultAsync((q) => q.EventFk == eventId && q.SlotNumber == slotNumber, cancellationToken);
        if (existingEventSlot is null)
            return NotFound();
        existingEventSlot.Title            = updatedEventSlot.Title;
        existingEventSlot.Group            = updatedEventSlot.Group;
        existingEventSlot.IsSelfAssignable = updatedEventSlot.IsSelfAssignable;
        existingEventSlot.IsVisible        = updatedEventSlot.IsVisible;
        existingEventSlot.Side             = updatedEventSlot.Side;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Delete an <see cref="EventSlot"/>, identified via the <paramref name="slotNumber"/>
    /// in an <see cref="Event"/>, identified via the <paramref name="eventId"/>. 
    /// </summary>
    /// <param name="eventId">The id of the <see cref="Event"/>.</param>
    /// <param name="slotNumber">The id of the <see cref="EventSlot"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [HttpPost("{slotNumber:int}/delete", Name = nameof(DeleteEventSlotAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    public async Task<ActionResult<bool>> DeleteEventSlotAsync(
        [FromRoute] Guid eventId,
        [FromRoute] int slotNumber,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync((q) => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (!User.IsInRoleOrAdmin(Roles.EventDelete)
            && existingEvent.HostedByFk != userId)
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .Where((q) => q.EventFk == eventId && q.SlotNumber == slotNumber)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingEventSlot is null)
            return NotFound();

        _apiDbContext.EventSlots.Remove(existingEventSlot);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}