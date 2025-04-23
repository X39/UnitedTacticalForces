using X39.UnitedTacticalForces.WebApp.Api.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public interface IGameServerRepository
{
    Task<long> GetGameServerCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ConfigurationEntryDefinition>> GetConfigurationDefinitionsAsync(
        long gameServerId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyCollection<PlainConfigurationEntryDto>> GetConfigurationAsync(
        long gameServerId,
        CancellationToken cancellationToken
    );

    Task SetConfigurationAsync(
        long gameServerId,
        IEnumerable<ConfigurationEntryUpdate> configurationEntries,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyCollection<GameServerInfoDto>> GetGameServersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlainGameServerLogDto>> GetLogsAsync(
        long gameServerId,
        int skip,
        int take,
        DateTimeOffset? referenceTimeStamp = null,
        bool descendingByTimestamp = false,
        CancellationToken cancellationToken = default
    );

    Task<long> GetLogsCountAsync(
        long gameServerId,
        DateTimeOffset? referenceTimeStamp = null,
        CancellationToken cancellationToken = default
    );

    Task<GameServerInfoDto> GetGameServerAsync(long gameServerId, CancellationToken cancellationToken = default);

    Task ChangeTitleAsync(long gameServerId, string title, CancellationToken cancellationToken = default);

    Task ChangeModPackAsync(long gameServerId, long modPackDefinitionId, CancellationToken cancellationToken = default);

    Task ClearModPackAsync(long gameServerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetGameServerControllersAsync(CancellationToken cancellationToken = default);

    Task<GameServerInfoDto> CreateGameServerAsync(
        string controllerIdentifier,
        string title,
        CancellationToken cancellationToken = default
    );

    Task DeleteGameServerAsync(long gameServerId, CancellationToken cancellationToken = default);

    Task<GameServerInfoDto> StartGameServerAsync(long gameServerId, CancellationToken cancellationToken = default);

    Task<GameServerInfoDto> StopGameServerAsync(long gameServerId, CancellationToken cancellationToken = default);

    Task<GameServerInfoDto> UpgradeGameServerAsync(long gameServerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GameFolder>> GetGameServerFoldersAsync(
        long gameServerId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<GameFileInfo>> GetGameServerFolderFilesAsync(
        long gameServerId,
        string gameFolderIdentifier,
        CancellationToken cancellationToken = default
    );

    Task UploadGameServerFolderFileAsync(
        long gameServerId,
        string gameFolderIdentifier,
        Stream fileStream,
        string fileName,
        string fileMimeType,
        CancellationToken cancellationToken = default
    );

    Task DeleteGameServerFolderFileAsync(
        long gameServerId,
        string gameFolderIdentifier,
        string gameFileFolder,
        CancellationToken cancellationToken = default
    );

    Task ClearAsync(long gameServerId, CancellationToken cancellationToken = default);
}
