using System.Collections.Immutable;
using System.Net;
using X39.UnitedTacticalForces.WebApp.ExtensionMethods;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.EventRepository;

[Scoped<EventRepositoryImpl, IEventRepository>]
internal class EventRepositoryImpl : RepositoryBase, IEventRepository
{
    public EventRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }

    public async Task<Event?> GetEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.EventsAsync(eventId, cancellationToken)
                .ConfigureAwait(false);
            return response;
        }
        catch (ApiException apiException) when (apiException.StatusCode == (int) HttpStatusCode.NoContent)
        {
            return null;
        }
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
            case EEventAcceptance.Rejected:
                await Client.EventsRejectAsync(eventItemPrimaryKey, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case EEventAcceptance.Maybe:
                await Client.EventsMaybeAsync(eventItemPrimaryKey, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case EEventAcceptance.Accepted:
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
        var clone = eventItem.Clone();
        clone.Owner = null;
        clone.HostedBy = null;
        clone.ModPack  = null;
        clone.Terrain  = null;
        clone.UserMetas  = null;
        await Client.EventsUpdateAsync(eventItem.PrimaryKey.Value, clone, cancellationToken)
            .ConfigureAwait(false);
    }
}