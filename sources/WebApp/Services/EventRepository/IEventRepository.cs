using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.EventRepository;

public interface IEventRepository
{
    Task<long> GetEventCountAsync(bool onlyHostedByMe, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlainEventDto>> GetEventsAsync(
        int skip,
        int take,
        bool onlyHostedByMe,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<UpcomingEventDto>> GetUpcomingEventsAsync(CancellationToken cancellationToken = default);

    Task SetMeAcceptanceAsync(
        Guid eventItemPrimaryKey,
        Common.Event.EEventAcceptance acceptance,
        CancellationToken cancellationToken = default
    );

    Task<PlainEventDto> CreateEventAsync(EventCreationPayload eventItem, CancellationToken cancellationToken = default);
    Task ModifyEventAsync(Guid eventId, EventUpdate eventItem, CancellationToken cancellationToken = default);

    Task<FullEventDto?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlainUserDto>> GetEventParticipantsAsync(
        Guid eventId,
        Common.Event.EEventAcceptance? acceptance = default,
        CancellationToken cancellationToken = default
    );
}
