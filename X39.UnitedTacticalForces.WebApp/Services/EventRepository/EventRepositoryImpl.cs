using System.Collections.Immutable;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.EventRepository;

[Scoped<EventRepositoryImpl, IEventRepository>]
internal class EventRepositoryImpl : RepositoryBase, IEventRepository
{
    public EventRepositoryImpl(HttpClient httpClient) : base(httpClient)
    {
    }

    public async Task<IReadOnlyCollection<Event>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        var response = await Client.EvensUpcomingAsync(cancellationToken);
        return response.ToImmutableArray();
    }
}