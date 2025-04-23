using X39.UnitedTacticalForces.WebApp.Api.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.SlotRepository;

public interface ISlotRepository
{
    Task<PlainEventSlotDto?> MySlotAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlainEventSlotDto>> AllSlotsAsync(
        Guid eventId,
        CancellationToken cancellationToken = default
    );

    Task DeleteSlotAsync(Guid eventId, int slotNumber, CancellationToken cancellationToken = default);

    Task CreateSlotAsync(
        Guid eventId,
        EventSlotCreationPayload eventSlot,
        CancellationToken cancellationToken = default
    );

    Task SelfAssignSlotAsync(Guid eventId, int slotNumber, CancellationToken cancellationToken = default);

    Task AssignSlotAsync(Guid eventId, int slotNumber, Guid userId, CancellationToken cancellationToken = default);

    Task UnassignSlotAsync(Guid eventId, int slotNumber, Guid userId, CancellationToken cancellationToken = default);

    Task UpdateSlotAsync(
        Guid eventId,
        int slotNumber,
        EventSlotUpdate eventSlotUpdate,
        CancellationToken cancellationToken = default
    );
}
