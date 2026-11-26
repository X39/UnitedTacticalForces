using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.Util.DependencyInjection;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the singleton fleet audience source opens and disposes one DI scope per call, forwards to the scoped
/// authorization service unchanged, is registered as a singleton by attribute and depends only on the scope factory.
/// </summary>
public sealed class FleetAudienceSourceTests
{
    /// <summary>
    /// Every call opens a new scope and disposes it after the call, so two calls create and dispose two scoped
    /// authorization instances.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task EachCall_OpensAndDisposesItsOwnScope()
    {
        var created = new List<RecordingAuthorizationService>();
        await using var provider = BuildProvider(created, () => new RecordingAuthorizationService());
        var source = new FleetAudienceSource(provider.GetRequiredService<IServiceScopeFactory>());

        await source.GetViewersAsync([Guid.NewGuid()], CancellationToken.None);
        await source.GetViewersAsync([Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(2, created.Count);
        Assert.NotSame(created[0], created[1]);
        Assert.All(created, service => Assert.True(service.IsDisposed));
    }

    /// <summary>
    /// The call resolves the service from the opened scope, forwards ids and token unchanged and returns exactly
    /// the service's result.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Call_ForwardsArgumentsAndReturnsServiceResult()
    {
        var expected = new Dictionary<Guid, IReadOnlyCollection<Guid>>
        {
            [Guid.NewGuid()] = [Guid.NewGuid()],
        };
        var created = new List<RecordingAuthorizationService>();
        await using var provider = BuildProvider(
            created,
            () => new RecordingAuthorizationService { Result = expected });
        var source = new FleetAudienceSource(provider.GetRequiredService<IServiceScopeFactory>());
        IReadOnlyCollection<Guid> ids = [Guid.NewGuid(), Guid.NewGuid()];
        using var cancellation = new CancellationTokenSource();

        var actual = await source.GetViewersAsync(ids, cancellation.Token);

        var service = Assert.Single(created);
        Assert.Same(ids, service.ReceivedIds);
        Assert.Equal(cancellation.Token, service.ReceivedToken);
        Assert.Same(expected, actual);
    }

    /// <summary>
    /// An exception thrown by the authorization service reaches the caller unchanged, and the scope is still
    /// disposed.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task ServiceFailure_PropagatesUnchanged()
    {
        var failure = new InvalidOperationException("audience query failed");
        var created = new List<RecordingAuthorizationService>();
        await using var provider = BuildProvider(
            created,
            () => new RecordingAuthorizationService { Failure = failure });
        var source = new FleetAudienceSource(provider.GetRequiredService<IServiceScopeFactory>());

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.GetViewersAsync([Guid.NewGuid()], CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.True(Assert.Single(created).IsDisposed);
    }

    /// <summary>
    /// The attributed registration of the API assembly registers the source as a singleton for its interface.
    /// </summary>
    [Fact]
    public void Source_IsRegisteredAsSingletonByAttribute()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddAttributedServicesFromAssemblyOf<Program>(configuration);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IFleetAudienceSource)
                          && descriptor.ImplementationType == typeof(FleetAudienceSource)
                          && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    /// <summary>
    /// The source can be constructed from a provider without any database registration, proving it adds no
    /// construction dependency on the database; resolution of the authorization service happens only per call.
    /// </summary>
    /// <returns>A task completing once the assertions ran.</returns>
    [Fact]
    public async Task Source_ConstructsFromScopeFactoryAlone()
    {
        await using var provider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var source = new FleetAudienceSource(provider.GetRequiredService<IServiceScopeFactory>());

        Assert.NotNull(source);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.GetViewersAsync([Guid.NewGuid()], CancellationToken.None));
    }

    /// <summary>
    /// Builds a provider whose scoped authorization service is created by <paramref name="factory"/> and recorded in
    /// <paramref name="created"/>.
    /// </summary>
    /// <param name="created">The list receiving every created scoped instance.</param>
    /// <param name="factory">Creates one scoped instance.</param>
    /// <returns>The built provider; dispose it after the test.</returns>
    private static ServiceProvider BuildProvider(
        List<RecordingAuthorizationService> created,
        Func<RecordingAuthorizationService> factory)
        => new ServiceCollection()
            .AddScoped<IHostingAuthorizationService>(_ =>
            {
                var service = factory();
                created.Add(service);
                return service;
            })
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    /// <summary>
    /// Authorization service double that records the audience call and its disposal and rejects every unrelated
    /// member.
    /// </summary>
    private sealed class RecordingAuthorizationService : IHostingAuthorizationService, IAsyncDisposable
    {
        /// <summary>
        /// Gets the result returned from the audience call.
        /// </summary>
        /// <value>Defaults to an empty dictionary.</value>
        public IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> Result { get; init; }
            = new Dictionary<Guid, IReadOnlyCollection<Guid>>();

        /// <summary>
        /// Gets the exception thrown from the audience call instead of returning <see cref="Result"/>.
        /// </summary>
        /// <value><see langword="null"/> when the call succeeds.</value>
        public Exception? Failure { get; init; }

        /// <summary>
        /// Gets the server ids received by the audience call.
        /// </summary>
        /// <value><see langword="null"/> until the call happened.</value>
        public IReadOnlyCollection<Guid>? ReceivedIds { get; private set; }

        /// <summary>
        /// Gets the cancellation token received by the audience call.
        /// </summary>
        /// <value>The default token until the call happened.</value>
        public CancellationToken ReceivedToken { get; private set; }

        /// <summary>
        /// Gets whether the owning scope disposed this instance.
        /// </summary>
        /// <value><see langword="true"/> once disposed.</value>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Not used by the audience source.
        /// </summary>
        /// <param name="principal">Ignored.</param>
        /// <param name="gameServerId">Ignored.</param>
        /// <param name="permission">Ignored.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public bool HasServerPermission(ClaimsPrincipal principal, Guid gameServerId, string permission)
            => throw new NotSupportedException();

        /// <summary>
        /// Not used by the audience source.
        /// </summary>
        /// <param name="principal">Ignored.</param>
        /// <param name="costOwnerId">Ignored.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public Task<bool> CanUseCostOwnerAsync(
            ClaimsPrincipal principal,
            Guid costOwnerId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <summary>
        /// Not used by the audience source.
        /// </summary>
        /// <param name="principal">Ignored.</param>
        /// <param name="costOwnerId">Ignored.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public Task<bool> CanViewCostOwnerAsync(
            ClaimsPrincipal principal,
            Guid costOwnerId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <summary>
        /// Not used by the audience source.
        /// </summary>
        /// <param name="userId">Ignored.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public Task<ClaimsPrincipal?> RebuildPrincipalAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <summary>
        /// Records the arguments and returns <see cref="Result"/>, or throws <see cref="Failure"/> when set.
        /// </summary>
        /// <param name="gameServerIds">The received server ids.</param>
        /// <param name="cancellationToken">The received cancellation token.</param>
        /// <returns>A task resolving to <see cref="Result"/>.</returns>
        public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetServerViewAudienceAsync(
            IReadOnlyCollection<Guid> gameServerIds,
            CancellationToken cancellationToken = default)
        {
            ReceivedIds   = gameServerIds;
            ReceivedToken = cancellationToken;
            return Failure is null
                ? Task.FromResult(Result)
                : Task.FromException<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>(Failure);
        }

        /// <summary>
        /// Not used by the fleet audience source.
        /// </summary>
        /// <param name="costOwnerIds">Ignored.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetCostOwnerViewAudienceAsync(
            IReadOnlyCollection<Guid> costOwnerIds,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <summary>
        /// Not used by the fleet audience source.
        /// </summary>
        /// <param name="principal">Ignored.</param>
        /// <param name="cancellationToken">Ignored.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public Task<IReadOnlySet<Guid>?> GetViewableCostOwnerIdsAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <summary>
        /// Marks this instance as disposed.
        /// </summary>
        /// <returns>A completed task.</returns>
        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
