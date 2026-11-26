using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Singleton bridge that lets singleton publishers read the server view audience from the scoped hosting
/// authorization service.
/// </summary>
/// <remarks>
/// The only construction dependency is the scope factory. The database context factory's options resolve the fleet
/// change interceptor, which resolves the notifier that consumes this source, so taking a database context or the
/// context factory here would create a construction cycle. Each call opens one DI scope, resolves the authorization
/// service from it and disposes the scope afterwards, so no scoped state is shared between calls.
/// </remarks>
/// <param name="scopeFactory">The factory that creates one DI scope per call.</param>
[Singleton<FleetAudienceSource, IFleetAudienceSource>]
internal sealed class FleetAudienceSource(IServiceScopeFactory scopeFactory) : IFleetAudienceSource
{
    /// <inheritdoc />
    /// <remarks>
    /// Exceptions from scope creation, service resolution or the audience query propagate unchanged.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetViewersAsync(
        IReadOnlyCollection<Guid> gameServerIds,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var authorizationService = scope.ServiceProvider.GetRequiredService<IHostingAuthorizationService>();
        return await authorizationService
            .GetServerViewAudienceAsync(gameServerIds, cancellationToken)
            .ConfigureAwait(false);
    }
}
