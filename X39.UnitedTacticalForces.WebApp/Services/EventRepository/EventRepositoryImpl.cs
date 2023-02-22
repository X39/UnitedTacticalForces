using System.Collections.Immutable;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.EventRepository;

[Scoped<EventRepositoryImpl, IEventRepository>]
internal class EventRepositoryImpl : RepositoryBase, IEventRepository
{
    public EventRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }

    public async Task<IReadOnlyCollection<Event>> GetUpcomingEventsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await Client.EventsUpcomingAsync(cancellationToken)
            .ConfigureAwait(false);
        return response.ToImmutableArray();
    }

    public async Task SetMeAcceptanceAsync(
        Guid eventItemPrimaryKey,
        EEventAcceptance acceptance,
        CancellationToken cancellationToken = default)
    {
        switch (acceptance)
        {
            case EEventAcceptance.__1:
                await Client.EventsRejectAsync(eventItemPrimaryKey, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case EEventAcceptance._0:
                await Client.EventsMaybeAsync(eventItemPrimaryKey, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case EEventAcceptance._1:
                await Client.EventsAcceptAsync(eventItemPrimaryKey, cancellationToken)
                    .ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(acceptance), acceptance, null);
        }
    }

    public async Task<Event> CreateEventAsync(
        Event eventItem,
        CancellationToken cancellationToken = default)
    {
        var response = await Client.EventsCreateAsync(eventItem, cancellationToken)
            .ConfigureAwait(false);
        return response;
    }

    public async Task ModifyEventAsync(
        Event eventItem,
        CancellationToken cancellationToken = default)
    {
        if (eventItem.PrimaryKey is null)
            throw new ArgumentException("Event.PrimaryKey is null.", nameof(eventItem));
        await Client.EventsUpdateAsync(eventItem.PrimaryKey.Value, eventItem, cancellationToken)
            .ConfigureAwait(false);
    }
}