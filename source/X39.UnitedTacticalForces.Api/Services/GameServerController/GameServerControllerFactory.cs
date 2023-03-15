using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.Util.DependencyInjection.Attributes;
using X39.Util.Threading;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

[Singleton<GameServerControllerFactory, IGameServerControllerFactory>]
public class GameServerControllerFactory : IGameServerControllerFactory, IAsyncDisposable
{
    private readonly ImmutableArray<GameControllerInfo>                    _controllerTypes;
    private readonly ConcurrentDictionary<long, IGameServerController> _gameServerControllers;
    private readonly SemaphoreSlim                                     _semaphoreSlim;

    private readonly record struct GameControllerInfo(
        Func<GameServer, Task<IGameServerController>> CreateController,
        string Identifier);

    private static Task<IGameServerController> GenericCreateAsync<T>(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        GameServer gameServer) where T : IGameServerControllerCreatable
    {
        return T.CreateAsync(serviceProvider, configuration, gameServer);
    }

    private static string GenericIdentifierGet<T>() where T : IGameServerControllerCreatable => T.Identifier;

    public GameServerControllerFactory(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        Task<IGameServerController> CreateControllerMethod(
            Type type,
            GameServer gameServer)
        {
            return (Task<IGameServerController>) typeof(GameServerControllerFactory)
                .GetMethod(nameof(GenericCreateAsync), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(type)
                .Invoke(null, Array.Empty<object?>())!;
        }

        string GetIdentifierMethod(Type type)
        {
            return (string) typeof(GameServerControllerFactory)
                .GetMethod(nameof(GenericIdentifierGet), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(type)
                .Invoke(null, Array.Empty<object?>())!;
        }

        _controllerTypes = typeof(GameServerControllerFactory).Assembly.GetTypes()
            .Where((q) => q.IsAssignableTo(typeof(IGameServerControllerFactory)))
            .Select(
                (q) =>
                {
                    Task<IGameServerController> CreateController(GameServer gameServer)
                        => CreateControllerMethod(q, gameServer);

                    return new GameControllerInfo(CreateController, GetIdentifierMethod(q));
                })
            .ToImmutableArray();
        _gameServerControllers = new ConcurrentDictionary<long, IGameServerController>();
        _semaphoreSlim         = new SemaphoreSlim(0, 1);
    }

    /// <inheritdoc/>
    public async Task<IGameServerController> GetGameControllerAsync(GameServer gameServer)
    {
        if (_gameServerControllers.TryGetValue(gameServer.PrimaryKey, out var gameServerController))
            return gameServerController;
        return await _semaphoreSlim.LockedAsync(
            async () =>
            {
                if (_gameServerControllers.TryGetValue(gameServer.PrimaryKey, out gameServerController))
                    return gameServerController;
                gameServerController = await _controllerTypes
                    .FirstOrDefault((q) => q.Identifier == gameServer.ControllerIdentifier)
                    .CreateController(gameServer)
                    .ConfigureAwait(false);
                _gameServerControllers[gameServer.PrimaryKey] = gameServerController;
                return gameServerController;
            }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetGameControllers()
    {
        foreach (var (_, identifier) in _controllerTypes)
            yield return identifier;
    }

    public async ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }
}