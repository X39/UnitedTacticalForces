using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.Services;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Contract.Event;

namespace X39.UnitedTacticalForces.Api.Helpers;

public static class EventUtils
{
    public static async Task SetAcceptanceOfEventAsync(
        IUpdateStreamService updateStreamService,
        ILogger logger,
        ApiDbContext apiDbContext,
        Guid eventId,
        Guid userId,
        EEventAcceptance acceptance,
        CancellationToken cancellationToken,
        short? slotNumber = null)
    {
        async Task ActAsync()
        {
            using var logScope = logger.BeginScope(nameof(SetAcceptanceOfEventAsync));
            logger.LogDebug(
                "Changing acceptance for user {UserId} with event {EventId} to {NewAcceptance}",
                userId,
                eventId,
                acceptance);
            var existingEvent = await apiDbContext.Events
                .Include((e) => e.UserMetas!.Where((q) => q.UserFk == userId))
                .SingleAsync((q) => q.PrimaryKey == eventId, cancellationToken);
            if (existingEvent.UserMetas?.FirstOrDefault() is { } userEventMeta)
            {
                switch (userEventMeta.Acceptance)
                {
                    case EEventAcceptance.Rejected:
                    {
                        logger.LogTrace(
                            $"Decreasing {nameof(Event.RejectedCount)} of with event {{EventId}} from {{OldRejectedCount}} to {{NewRejectedCount}}",
                            eventId,
                            existingEvent.RejectedCount,
                            existingEvent.RejectedCount - 1);
                        existingEvent.RejectedCount--;
                        var slotCandidate = await apiDbContext.EventSlots
                            .Where((q) => q.EventFk == eventId)
                            .SingleOrDefaultAsync((q) => q.AssignedToFk == userId, cancellationToken);
                        if (slotCandidate is not null)
                            slotCandidate.AssignedToFk = null;
                        break;
                    }
                    case EEventAcceptance.Maybe:
                    {
                        logger.LogTrace(
                            $"Decreasing {nameof(Event.MaybeCount)} of with event {{EventId}} from {{OldMaybeCount}} to {{NewMaybeCount}}",
                            eventId,
                            existingEvent.MaybeCount,
                            existingEvent.MaybeCount - 1);
                        existingEvent.MaybeCount--;
                        break;
                    }
                    case EEventAcceptance.Accepted:
                    {
                        logger.LogTrace(
                            $"Decreasing {nameof(Event.AcceptedCount)} of with event {{EventId}} from {{OldAcceptedCount}} to {{NewAcceptedCount}}",
                            eventId,
                            existingEvent.AcceptedCount,
                            existingEvent.AcceptedCount - 1);
                        existingEvent.AcceptedCount--;
                        var slotCandidate = await apiDbContext.EventSlots
                            .Where((q) => q.EventFk == eventId)
                            .SingleOrDefaultAsync((q) => q.AssignedToFk == userId, cancellationToken);
                        if (slotCandidate is not null)
                            slotCandidate.AssignedToFk = null;
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                userEventMeta.Acceptance = acceptance;
            }
            else
            {
                await apiDbContext.UserEventMetas.AddAsync(
                    new UserEventMeta
                    {
                        User       = default,
                        Event      = default,
                        UserFk     = userId,
                        EventFk    = eventId,
                        Acceptance = acceptance,
                    },
                    cancellationToken);
            }

            switch (acceptance)
            {
                case EEventAcceptance.Rejected:
                {
                    logger.LogTrace(
                        $"Increasing {nameof(Event.RejectedCount)} of with event {{EventId}} from {{OldRejectedCount}} to {{NewRejectedCount}}",
                        eventId,
                        existingEvent.RejectedCount,
                        existingEvent.RejectedCount + 1);
                    existingEvent.RejectedCount++;
                    break;
                }
                case EEventAcceptance.Maybe:
                {
                    logger.LogTrace(
                        $"Increasing {nameof(Event.MaybeCount)} of with event {{EventId}} from {{OldMaybeCount}} to {{NewMaybeCount}}",
                        eventId,
                        existingEvent.MaybeCount,
                        existingEvent.MaybeCount + 1);
                    existingEvent.MaybeCount++;
                    break;
                }
                case EEventAcceptance.Accepted:
                {
                    logger.LogTrace(
                        $"Increasing {nameof(Event.AcceptedCount)} of with event {{EventId}} from {{OldAcceptedCount}} to {{NewAcceptedCount}}",
                        eventId,
                        existingEvent.AcceptedCount,
                        existingEvent.AcceptedCount + 1);
                    existingEvent.AcceptedCount++;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(acceptance), acceptance, null);
            }

            await apiDbContext.SaveChangesAsync(cancellationToken);
            logger.LogTrace(
                "Changed acceptance for user {UserId} with event {EventId} to {NewAcceptance}",
                userId,
                eventId,
                acceptance);
        }

        if (apiDbContext.Database.CurrentTransaction is null)
        {
            await using var dbTransaction = await apiDbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await ActAsync().ConfigureAwait(false);
            await dbTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ActAsync().ConfigureAwait(false);
        }
        await updateStreamService.SendUpdateAsync(
                $"{Constants.Routes.Events}/{eventId}/slots",
                new Contract.UpdateStream.Eventing.AcceptanceChanged()
                {
                    EventId = eventId,
                    UserId = userId,
                    EventAcceptance = acceptance,
                    SlotNumber = slotNumber,
                })
            .ConfigureAwait(false);
    }
}