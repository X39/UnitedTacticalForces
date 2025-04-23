using System.Collections.Immutable;
using System.Net;
using Microsoft.AspNetCore.Http;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.SlotRepository;

[Scoped<SlotRepositoryImpl, ISlotRepository>]
public sealed class SlotRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : RepositoryBase(httpClient, baseUrl),
    ISlotRepository
{
    public async Task<PlainEventSlotDto?> MySlotAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var result = await Client.Events[eventId]
            .Slotting
            .MySlot
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyCollection<PlainEventSlotDto>> AllSlotsAsync(
        Guid eventId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.Events[eventId]
            .Slotting
            .All
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result?.ToImmutableArray() ?? [];
    }

    public async Task DeleteSlotAsync(Guid eventId, int slotNumber, CancellationToken cancellationToken = default)
    {
        await Client.Events[eventId]
            .Slotting[slotNumber]
            .DeletePath
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CreateSlotAsync(
        Guid eventId,
        PlainEventSlotDto eventSlot,
        CancellationToken cancellationToken = default
    )
    {
        await Client.Events[eventId].Slotting.Create.PostAsync(eventSlot, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SelfAssignSlotAsync(EventSlot eventSlot, CancellationToken cancellationToken = default)
    {
        if (eventSlot.EventFk is null)
            throw new ArgumentException("EventSlot.EventFk is null.", nameof(eventSlot));
        if (eventSlot.SlotNumber is null)
            throw new ArgumentException("EventSlot.SlotNumber is null.", nameof(eventSlot));
        await Client.EventsSlottingAssignPostAsync(
                eventSlot.EventFk.Value,
                eventSlot.SlotNumber.Value,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task AssignSlotAsync(EventSlot eventSlot, User user, CancellationToken cancellationToken = default)
    {
        if (eventSlot.EventFk is null)
            throw new ArgumentException("EventSlot.EventFk is null.", nameof(eventSlot));
        if (eventSlot.SlotNumber is null)
            throw new ArgumentException("EventSlot.SlotNumber is null.", nameof(eventSlot));
        if (user.PrimaryKey is null)
            throw new ArgumentException("User.PrimaryKey is null.", nameof(user));
        await Client.EventsSlottingAssignPostAsync(
                eventSlot.EventFk.Value,
                eventSlot.SlotNumber.Value,
                user.PrimaryKey.Value,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task UnassignSlotAsync(EventSlot eventSlot, User user, CancellationToken cancellationToken = default)
    {
        if (eventSlot.EventFk is null)
            throw new ArgumentException("EventSlot.EventFk is null.", nameof(eventSlot));
        if (eventSlot.SlotNumber is null)
            throw new ArgumentException("EventSlot.SlotNumber is null.", nameof(eventSlot));
        if (user.PrimaryKey is null)
            throw new ArgumentException("User.PrimaryKey is null.", nameof(user));
        await Client.EventsSlottingUnassignPostAsync(
                eventSlot.EventFk.Value,
                eventSlot.SlotNumber.Value,
                user.PrimaryKey.Value,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task UpdateSlotAsync(
        EventSlot existingEventSlot,
        EventSlot updatedEventSlot,
        CancellationToken cancellationToken = default
    )
    {
        if (existingEventSlot.EventFk is null)
            throw new ArgumentException("EventSlot.EventFk is null.", nameof(existingEventSlot));
        if (existingEventSlot.SlotNumber is null)
            throw new ArgumentException("EventSlot.SlotNumber is null.", nameof(existingEventSlot));
        await Client.EventsSlottingUpdateAsync(
                existingEventSlot.EventFk.Value,
                existingEventSlot.SlotNumber.Value,
                updatedEventSlot,
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
