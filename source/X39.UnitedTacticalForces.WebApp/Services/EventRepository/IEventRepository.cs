namespace X39.UnitedTacticalForces.WebApp.Services.EventRepository;

public interface IEventRepository
{
    Task<long> GetEventCountAsync(bool onlyHostedByMe, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Event>> GetEventsAsync(int skip, int take, bool onlyHostedByMe, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Event>> GetUpcomingEventsAsync(CancellationToken cancellationToken = default);
    Task SetMeAcceptanceAsync(Guid eventItemPrimaryKey, EEventAcceptance acceptance, CancellationToken cancellationToken = default);
    Task<Event> CreateEventAsync(Event eventItem, CancellationToken cancellationToken = default);
    Task ModifyEventAsync(Event eventItem, CancellationToken cancellationToken = default);

    Task<Event?> GetEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<User>> GetEventParticipantsAsync(
        Event eventItem,
        EEventAcceptance? acceptance,
        CancellationToken cancellationToken = default);
}