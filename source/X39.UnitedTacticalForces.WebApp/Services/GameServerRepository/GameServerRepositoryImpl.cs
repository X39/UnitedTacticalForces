using System.Collections.Immutable;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

[Scoped<GameServerRepositoryImpl, IGameServerRepository>]
internal class GameServerRepositoryImpl : RepositoryBase, IGameServerRepository
{
    public GameServerRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }

    public async Task<long> GetGameServerCountAsync(CancellationToken cancellationToken = default)
    {
        return await Client.GameServersAllCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ConfigurationEntryDefinition>> GetConfigurationDefinitionsAsync(
        GameServer gameServer,
        CancellationToken cancellationToken)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        var result = await Client.GameServersConfigurationDefinitionsAsync(
                gameServer.PrimaryKey.Value,
                cancellationToken)
            .ConfigureAwait(false);
        return result.ToImmutableArray();
    }

    public async Task<IReadOnlyCollection<ConfigurationEntry>> GetConfigurationAsync(
        GameServer gameServer,
        CancellationToken cancellationToken)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        var result = await Client.GameServersConfigurationGetAsync(
                gameServer.PrimaryKey.Value,
                cancellationToken)
            .ConfigureAwait(false);
        return result.ToImmutableArray();
    }

    public async Task SetConfigurationAsync(
        GameServer gameServer,
        IEnumerable<ConfigurationEntry> configurationEntries,
        CancellationToken cancellationToken)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        await Client.GameServersConfigurationPostAsync(
                gameServer.PrimaryKey.Value,
                configurationEntries,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<GameServerInfo>> GetGameServersAsync(
        CancellationToken cancellationToken = default)
    {
        var gameServers = await Client.GameServersAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return gameServers.ToImmutableArray();
    }

    public async Task<IReadOnlyCollection<GameServerLog>> GetLogsAsync(
        GameServer gameServer,
        int skip,
        int take,
        DateTimeOffset? referenceTimeStamp = null,
        bool descendingByTimestamp = false,
        CancellationToken cancellationToken = default)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        var gameServers = await Client.GameServersLogsAsync(
                gameServer.PrimaryKey.Value,
                skip,
                take,
                referenceTimeStamp,
                descendingByTimestamp,
                cancellationToken
            )
            .ConfigureAwait(false);
        return gameServers.ToImmutableArray();
    }
    public async Task<long> GetLogsCountAsync(
        GameServer gameServer,
        DateTimeOffset? referenceTimeStamp = null,
        CancellationToken cancellationToken = default)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        var logsCount = await Client.GameServersLogsCountAsync(
                gameServer.PrimaryKey.Value,
                referenceTimeStamp,
                cancellationToken
            )
            .ConfigureAwait(false);
        return logsCount;
    }

    public async Task<GameServerInfo> GetGameServerAsync(
        long gameServerId,
        CancellationToken cancellationToken = default)
    {
        var gameServer = await Client.GameServersAsync(gameServerId, cancellationToken)
            .ConfigureAwait(false);
        return gameServer;
    }

    public async Task UpdateAsync(
        GameServer gameServer,
        CancellationToken cancellationToken = default)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        var copy = gameServer.ShallowCopy();
        await Client.GameServersUpdateAsync(gameServer.PrimaryKey.Value, gameServer, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> GetGameServerControllersAsync(
        CancellationToken cancellationToken = default)
    {
        var gameServerControllers = await Client.GameServersAllControllersAsync(cancellationToken)
            .ConfigureAwait(false);
        return gameServerControllers.ToImmutableArray();
    }

    public async Task<GameServerInfo> CreateGameServerAsync(
        string controllerIdentifier,
        GameServer gameServer,
        CancellationToken cancellationToken = default)
    {
        gameServer.ActiveModPack        = null;
        gameServer.SelectedModPack      = null;
        gameServer.LifetimeEvents       = null;
        gameServer.ConfigurationEntries = null;
        var info = await Client.GameServersCreateAsync(controllerIdentifier, gameServer, cancellationToken)
            .ConfigureAwait(false);
        return info;
    }

    public async Task DeleteGameServerAsync(GameServer gameServer, CancellationToken cancellationToken = default)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        await Client.GameServersDeleteAsync(gameServer.PrimaryKey.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameServerInfo> StartGameServerAsync(
        GameServer gameServer,
        CancellationToken cancellationToken = default)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        return await Client.GameServersStartAsync(gameServer.PrimaryKey.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameServerInfo> StopGameServerAsync(
        GameServer gameServer,
        CancellationToken cancellationToken = default)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        return await Client.GameServersStopAsync(gameServer.PrimaryKey.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameServerInfo> UpgradeGameServerAsync(
        GameServer gameServer,
        CancellationToken cancellationToken = default)
    {
        if (gameServer.PrimaryKey is null)
            throw new ArgumentException("GameServer.PrimaryKey is null.", nameof(gameServer));
        return await Client.GameServersUpgradeAsync(gameServer.PrimaryKey.Value, cancellationToken)
            .ConfigureAwait(false);
    }
}