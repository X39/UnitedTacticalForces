using System.Collections.Immutable;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.SlotRepository;

[Scoped<SlotRepositoryImpl, ISlotRepository>]
public sealed class SlotRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl) : RepositoryBase(httpClientFactory, baseUrl),
    ISlotRepository
{
    public async Task<PlainEventSlotDto?> MySlotAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var result = await Client.Events[eventId]
            .Slotting
            .MySlot
            .GetAsync(PreferJsonResponse, cancellationToken: cancellationToken)
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
            .GetAsync(PreferJsonResponse, cancellationToken: cancellationToken)
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
        EventSlotCreationPayload eventSlot,
        CancellationToken cancellationToken = default
    )
    {
        await Client.Events[eventId]
            .Slotting
            .Create
            .PostAsync(eventSlot, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SelfAssignSlotAsync(Guid eventId, int slotNumber, CancellationToken cancellationToken = default)
    {
        await Client.Events[eventId]
            .Slotting[slotNumber]
            .Assign
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AssignSlotAsync(
        Guid eventId,
        int slotNumber,
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        await Client.Events[eventId]
            .Slotting[slotNumber]
            .Assign
            .User[userId]
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UnassignSlotAsync(
        Guid eventId,
        int slotNumber,
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        await Client.Events[eventId]
            .Slotting[slotNumber]
            .Unassign
            .User[userId]
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateSlotAsync(
        Guid eventId,
        int slotNumber,
        EventSlotUpdate eventSlotUpdate,
        CancellationToken cancellationToken = default
    )
    {
        await Client.Events[eventId]
            .Slotting[slotNumber]
            .Update
            .PostAsync(eventSlotUpdate, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
