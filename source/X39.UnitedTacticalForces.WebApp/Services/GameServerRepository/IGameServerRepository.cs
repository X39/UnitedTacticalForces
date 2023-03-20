namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public interface IGameServerRepository
{
    Task<long> GetGameServerCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GameServerInfo>> GetGameServersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetGameServerControllersAsync(CancellationToken cancellationToken = default);

    Task<GameServerInfo> CreateGameServerAsync(
        string controllerIdentifier,
        GameServer gameServer,
        CancellationToken cancellationToken = default);

    Task<GameServerInfo> StartGameServerAsync(
        GameServer gameServer,
        CancellationToken cancellationToken = default);
    Task<GameServerInfo> StopGameServerAsync(
        GameServer gameServer,
        CancellationToken cancellationToken = default);
    Task<GameServerInfo> UpgradeGameServerAsync(
        GameServer gameServer,
        CancellationToken cancellationToken = default);

    Task DeleteGameServerAsync(
        GameServer gameServer,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ConfigurationEntryDefinition>> GetConfigurationDefinitionsAsync(
        GameServer gameServer,
        CancellationToken cancellationToken=default);

    Task<IReadOnlyCollection<ConfigurationEntry>> GetConfigurationAsync(
        GameServer gameServer,
        CancellationToken cancellationToken=default);

    Task SetConfigurationAsync(
        GameServer gameServer,
        IEnumerable<ConfigurationEntry> configurationEntries,
        CancellationToken cancellationToken=default);

    Task<GameServerInfo> GetGameServerAsync(
        long gameServerId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(GameServer gameServer, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GameServerLog>> GetLogsAsync(
        GameServer gameServer,
        int skip,
        int take,
        DateTimeOffset? referenceTimeStamp = null,
        bool descendingByTimestamp = false,
        CancellationToken cancellationToken = default);
    Task<long> GetLogsCountAsync(
        GameServer gameServer,
        DateTimeOffset? referenceTimeStamp = null,
        CancellationToken cancellationToken = default);
}