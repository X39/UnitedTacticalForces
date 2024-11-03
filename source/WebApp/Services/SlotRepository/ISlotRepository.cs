namespace X39.UnitedTacticalForces.WebApp.Services.SlotRepository;

public interface ISlotRepository
{
    Task<EventSlot?> MySlotAsync(Event eventItem, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EventSlot>> AllSlotsAsync(Event eventItem, CancellationToken cancellationToken = default);
    Task DeleteSlotAsync(EventSlot eventSlot, CancellationToken cancellationToken = default);
    Task CreateSlotAsync(Event eventItem, EventSlot eventSlot, CancellationToken cancellationToken = default);
    Task SelfAssignSlotAsync(EventSlot eventSlot, CancellationToken cancellationToken = default);
    Task AssignSlotAsync(EventSlot eventSlot, User user, CancellationToken cancellationToken = default);
    Task UnassignSlotAsync(EventSlot eventSlot, User user, CancellationToken cancellationToken = default);
    Task UpdateSlotAsync(EventSlot existingEventSlot, EventSlot updatedEventSlot, CancellationToken cancellationToken = default);
}