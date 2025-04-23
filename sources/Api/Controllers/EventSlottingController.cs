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
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Contract.Event;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Offers API endpoints for slotting on <see cref="Event"/>'s.
/// </summary>
[ApiController]
[Route(Constants.Routes.Events + "/{eventId:guid}/" + Constants.Routes.EventSlotting)]
public class EventSlottingController : ControllerBase
{
    private readonly ILogger<EventSlottingController> _logger;
    private readonly ApiDbContext                     _apiDbContext;
    private readonly IUpdateStreamService             _updateStreamService;

    /// <summary>
    /// Creates a new instance of <see cref="EventSlottingController"/>.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to use.</param>
    /// <param name="apiDbContext">The <see cref="ApiDbContext"/> to use.</param>
    /// <param name="updateStreamService">The <see cref="IUpdateStreamService"/> to use.</param>
    public EventSlottingController(
        ILogger<EventSlottingController> logger,
        ApiDbContext apiDbContext,
        IUpdateStreamService updateStreamService)
    {
        _logger              = logger;
        _apiDbContext        = apiDbContext;
        _updateStreamService = updateStreamService;
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
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType<PlainEventSlotDto[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AllAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var eventExists = await _apiDbContext.Events
            .AnyAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (!eventExists)
            return NotFound();
        var query = _apiDbContext.EventSlots
            .Include(e => e.AssignedTo)
            .Where(q => q.EventFk == eventId)
            .AsQueryable();
        if (!User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty))
            query = query.Where(q => q.IsVisible || q.Event!.HostedByFk == userId);

        var results = await query
            .OrderBy(q => q.SlotNumber)
            .Select(e => new PlainEventSlotDto
                {
                    SlotNumber       = e.SlotNumber,
                    AssignedToFk     = e.AssignedToFk,
                    EventFk          = e.EventFk,
                    IsSelfAssignable = e.IsSelfAssignable,
                    IsVisible        = e.IsVisible,
                    Group            = e.Group,
                    Title            = e.Title,
                    Side             = e.Side,
                }
            )
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
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<PlainEventSlotDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MySlotAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var eventExists = await _apiDbContext.Events
            .AnyAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (!eventExists)
            return NotFound();
        var query = _apiDbContext.EventSlots
            .Include(e => e.AssignedTo)
            .Where(q => q.EventFk == eventId)
            .Where(q => q.AssignedToFk == userId)
            .Select(e => new PlainEventSlotDto
                {
                    SlotNumber       = e.SlotNumber,
                    AssignedToFk     = e.AssignedToFk,
                    EventFk          = e.EventFk,
                    IsSelfAssignable = e.IsSelfAssignable,
                    IsVisible        = e.IsVisible,
                    Group            = e.Group,
                    Title            = e.Title,
                    Side             = e.Side,
                }
            )
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
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType<bool>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UnassignIfAssignedAsync(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var eventExists = await _apiDbContext.Events
            .AnyAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (!eventExists)
            return NotFound();
        var eventSlot = await _apiDbContext.EventSlots
            .Where(q => q.EventFk == eventId)
            .Where(q => q.AssignedToFk == userId)
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
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AssignEventSlotToSelf(
        [FromRoute] Guid eventId,
        [FromRoute] short slotNumber,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();

        var newEventSlot = await _apiDbContext.EventSlots
            .Where(q => q.EventFk == eventId && q.SlotNumber == slotNumber)
            .SingleOrDefaultAsync(cancellationToken);
        if (newEventSlot is null)
            return NotFound();
        if (!newEventSlot.IsSelfAssignable
            && !User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty)
            && existingEvent.HostedByFk != userId)
            return Forbid();
        if (newEventSlot.AssignedToFk is not null)
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .Where(q => q.EventFk == eventId)
            .Where(q => q.AssignedToFk == userId)
            .SingleOrDefaultAsync(cancellationToken);

        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        await EventUtils.SetAcceptanceOfEventAsync(
            _updateStreamService,
            _logger,
            _apiDbContext,
            eventId,
            userId,
            EEventAcceptance.Accepted,
            cancellationToken,
            slotNumber);

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
    [HttpPost("{slotNumber:int}/assign/user/{userId:guid}", Name = nameof(AssignEventSlotToUserAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AssignEventSlotToUserAsync(
        [FromRoute] Guid eventId,
        [FromRoute] short slotNumber,
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (existingEvent.HostedByFk != userId
            && !User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return Forbid();

        var newEventSlot = await _apiDbContext.EventSlots
            .Where(q => q.EventFk == eventId && q.SlotNumber == slotNumber)
            .SingleOrDefaultAsync(cancellationToken);
        if (newEventSlot is null)
            return NotFound();
        if (newEventSlot.AssignedToFk is not null)
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .Where(q => q.EventFk == eventId)
            .Where(q => q.AssignedToFk == userId)
            .SingleOrDefaultAsync(cancellationToken);

        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        await EventUtils.SetAcceptanceOfEventAsync(
            _updateStreamService,
            _logger,
            _apiDbContext,
            eventId,
            userId,
            EEventAcceptance.Accepted,
            cancellationToken,
            slotNumber);
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
    [HttpPost("{slotNumber:int}/unassign/user/{userId:guid}", Name = nameof(UnassignEventSlotToUserAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnassignEventSlotToUserAsync(
        [FromRoute] Guid eventId,
        [FromRoute] int slotNumber,
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (existingEvent.HostedByFk != userId
            && !User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .Where(q => q.EventFk == eventId && q.SlotNumber == slotNumber)
            .Where(q => q.AssignedToFk == userId)
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
    /// <param name="payload">The <see cref="EventSlot"/> to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [HttpPost("create", Name = nameof(CreateEventSlotAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType<PlainEventSlotDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateEventSlotAsync(
        [FromRoute] Guid eventId,
        [FromBody] EventSlotCreationPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (existingEvent.HostedByFk != userId
            && !User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return Forbid();
        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        var maxSlotNumber = await _apiDbContext.EventSlots
            .Where(q => q.EventFk == eventId)
            .Select(q => q.SlotNumber)
            .DefaultIfEmpty()
            .MaxAsync(cancellationToken);
        var eventSlot = new EventSlot
        {
            AssignedTo       = null,
            Event            = null,
            EventFk          = eventId,
            SlotNumber       = checked((short) (maxSlotNumber + 1)),
            IsSelfAssignable = payload.IsSelfAssignable,
            IsVisible        = payload.IsVisible,
            Group            = payload.Group,
            Title            = payload.Title,
            Side             = payload.Side,
            AssignedToFk     = payload.AssignedToFk,
        };
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
    /// <param name="payload">The <see cref="EventSlot"/> to create.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [HttpPost("{slotNumber:int}/update", Name = nameof(UpdateEventSlotAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateEventSlotAsync(
        [FromRoute] Guid eventId,
        [FromRoute] int slotNumber,
        [FromBody] EventSlotUpdate payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (existingEvent.HostedByFk != userId
            && !User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .SingleOrDefaultAsync(q => q.EventFk == eventId && q.SlotNumber == slotNumber, cancellationToken);
        if (existingEventSlot is null)
            return NotFound();
        if (payload.Title is not null)
            existingEventSlot.Title            = payload.Title;
        if (payload.Group is not null)
            existingEventSlot.Group            = payload.Group;
        if (payload.IsSelfAssignable is not null)
            existingEventSlot.IsSelfAssignable = payload.IsSelfAssignable.Value;
        if (payload.IsVisible is not null)
            existingEventSlot.IsVisible        = payload.IsVisible.Value;
        if (payload.Side is not null)
            existingEventSlot.Side             = payload.Side.Value;
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
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteEventSlotAsync(
        [FromRoute] Guid eventId,
        [FromRoute] int slotNumber,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingEvent = await _apiDbContext.Events
            .SingleOrDefaultAsync(q => q.PrimaryKey == eventId, cancellationToken);
        if (existingEvent is null)
            return NotFound();
        if (existingEvent.HostedByFk != userId
            && !User.HasClaim(Claims.Administrative.All, string.Empty) &&
            !User.HasClaim(Claims.Administrative.Event, string.Empty))
            return Forbid();
        var existingEventSlot = await _apiDbContext.EventSlots
            .Where(q => q.EventFk == eventId && q.SlotNumber == slotNumber)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingEventSlot is null)
            return NotFound();

        _apiDbContext.EventSlots.Remove(existingEventSlot);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
