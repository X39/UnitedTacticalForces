using System.Collections.Immutable;
using System.Net;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.SlotRepository;

[Scoped<SlotRepositoryImpl, ISlotRepository>]
public class SlotRepositoryImpl : RepositoryBase, ISlotRepository
{
    public SlotRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }

    public async Task<EventSlot?> MySlotAsync(Event eventItem, CancellationToken cancellationToken = default)
    {
        if (eventItem.PrimaryKey is null)
            throw new ArgumentException("Event.PrimaryKey is null.", nameof(eventItem));
        try
        {
            var result = await Client.EventsSlottingMySlotAsync(
                    eventItem.PrimaryKey.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            return result;
        }
        catch (ApiException apiException) when (apiException.StatusCode is (int) HttpStatusCode.NoContent)
        {
            return null;
        }
    }

    public async Task<IReadOnlyCollection<EventSlot>> AllSlotsAsync(Event eventItem, CancellationToken cancellationToken = default)
    {
        if (eventItem.PrimaryKey is null)
            throw new ArgumentException("Event.PrimaryKey is null.", nameof(eventItem));
        var result = await Client.EventsSlottingAllAsync(
                eventItem.PrimaryKey.Value,
                cancellationToken)
            .ConfigureAwait(false);
        return result.ToImmutableArray();
    }
    public async Task DeleteSlotAsync(EventSlot eventSlot, CancellationToken cancellationToken = default)
    {
        if (eventSlot.EventFk is null)
            throw new ArgumentException("EventSlot.EventFk is null.", nameof(eventSlot));
        if (eventSlot.SlotNumber is null)
            throw new ArgumentException("EventSlot.SlotNumber is null.", nameof(eventSlot));
        await Client.EventsSlottingDeleteAsync(
                eventSlot.EventFk.Value,
                eventSlot.SlotNumber.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }
    public async Task CreateSlotAsync(Event eventItem, EventSlot eventSlot, CancellationToken cancellationToken = default)
    {
        if (eventItem.PrimaryKey is null)
            throw new ArgumentException("Event.PrimaryKey is null.", nameof(eventItem));
        await Client.EventsSlottingCreateAsync(
                eventItem.PrimaryKey.Value,
                eventSlot,
                cancellationToken)
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
                cancellationToken)
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
                cancellationToken)
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
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateSlotAsync(
        EventSlot existingEventSlot,
        EventSlot updatedEventSlot,
        CancellationToken cancellationToken = default)
    {
        if (existingEventSlot.EventFk is null)
            throw new ArgumentException("EventSlot.EventFk is null.", nameof(existingEventSlot));
        if (existingEventSlot.SlotNumber is null)
            throw new ArgumentException("EventSlot.SlotNumber is null.", nameof(existingEventSlot));
        await Client.EventsSlottingUpdateAsync(
                existingEventSlot.EventFk.Value,
                existingEventSlot.SlotNumber.Value,
                updatedEventSlot,
                cancellationToken)
            .ConfigureAwait(false);
    }
}