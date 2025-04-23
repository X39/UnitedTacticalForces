using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.Kiota.Abstractions;
using X39.UnitedTacticalForces.Contract.Event;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.EventRepository;

[Scoped<EventRepositoryImpl, IEventRepository>]
internal class EventRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : RepositoryBase(httpClient, baseUrl),
    IEventRepository
{
    public async Task<FullEventDto?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.Events[eventId]
                .GetAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response;
        }
        catch (ApiException apiException) when (apiException.ResponseStatusCode == StatusCodes.Status204NoContent)
        {
            return null;
        }
    }

    public async Task<IReadOnlyCollection<PlainUserDto>> GetEventParticipantsAsync(
        Guid eventId,
        EEventAcceptance? acceptance = default,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.Events[eventId]
            .Users
            .GetAsync(
                conf => conf.QueryParameters.Acceptance = acceptance switch
                {
                    EEventAcceptance.Accepted => "accepted",
                    EEventAcceptance.Rejected => "rejected",
                    EEventAcceptance.Maybe => "maybe",
                    null => null,
                    _ => throw new ArgumentOutOfRangeException(nameof(acceptance), acceptance, null)
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        return result?.ToImmutableArray() ?? [];
    }

    public async Task<long> GetEventCountAsync(bool onlyHostedByMe, CancellationToken cancellationToken = default)
    {
        var response = await Client.Events
            .All
            .Count
            .GetAsync(conf => conf.QueryParameters.HostedByMeOnly = onlyHostedByMe, cancellationToken)
            .ConfigureAwait(false);
        return response ?? default;
    }

    public async Task<IReadOnlyCollection<PlainEventDto>> GetEventsAsync(
        int skip,
        int take,
        bool onlyHostedByMe,
        CancellationToken cancellationToken = default
    )
    {
        var response = await Client.Events
            .All
            .GetAsync(
                conf =>
                {
                    conf.QueryParameters.Skip           = skip;
                    conf.QueryParameters.Take           = take;
                    conf.QueryParameters.HostedByMeOnly = onlyHostedByMe;
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return response?.ToImmutableArray() ?? [];
    }

    public async Task<IReadOnlyCollection<UpcomingEventDto>> GetUpcomingEventsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var response = await Client.Events
            .Upcoming
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response?.ToImmutableArray() ?? [];
    }

    public async Task SetMeAcceptanceAsync(
        Guid eventItemPrimaryKey,
        EEventAcceptance acceptance,
        CancellationToken cancellationToken = default
    )
    {
        switch (acceptance)
        {
            case EEventAcceptance.Rejected:
                await Client.Events[eventItemPrimaryKey]
                    .Reject
                    .PostAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                break;
            case EEventAcceptance.Maybe:
                await Client.Events[eventItemPrimaryKey]
                    .Maybe
                    .PostAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                break;
            case EEventAcceptance.Accepted:
                await Client.Events[eventItemPrimaryKey]
                    .Accept
                    .PostAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(acceptance), acceptance, null);
        }
    }

    public async Task<PlainEventDto> CreateEventAsync(
        PlainEventDto eventItem,
        CancellationToken cancellationToken = default
    )
    {
        var response = await Client.Events
            .Create
            .PostAsync(eventItem, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response ?? throw new NullReferenceException("Response is null.");
    }

    public async Task ModifyEventAsync(
        Guid eventId,
        EventUpdate eventItem,
        CancellationToken cancellationToken = default
    )
    {
        await Client.Events[eventId]
            .Update
            .PostAsync(eventItem, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
