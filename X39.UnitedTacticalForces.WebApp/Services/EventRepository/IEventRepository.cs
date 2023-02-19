namespace X39.UnitedTacticalForces.WebApp.Services.EventRepository;

public interface IEventRepository
{
    Task<IReadOnlyCollection<Event>> GetEventsAsync(CancellationToken cancellationToken = default);
}