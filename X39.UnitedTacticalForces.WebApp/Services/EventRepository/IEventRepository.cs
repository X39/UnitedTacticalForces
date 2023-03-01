namespace X39.UnitedTacticalForces.WebApp.Services.EventRepository;

public interface IEventRepository
{
    Task<IReadOnlyCollection<Event>> GetUpcomingEventsAsync(CancellationToken cancellationToken = default);
    Task SetMeAcceptanceAsync(Guid eventItemPrimaryKey, EEventAcceptance acceptance, CancellationToken cancellationToken = default);
    Task<Event> CreateEventAsync(Event eventItem, CancellationToken cancellationToken = default);
    Task ModifyEventAsync(Event eventItem, CancellationToken cancellationToken = default);

    Task<Event?> GetEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);
}