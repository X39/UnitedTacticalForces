using System.Collections.Immutable;
using Microsoft.Kiota.Abstractions;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.UnitedTacticalForces.WebApp.Properties;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

[Scoped<GameServerRepositoryImpl, IGameServerRepository>]
internal class GameServerRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : RepositoryBase(httpClient, baseUrl),
    IGameServerRepository
{
    public async Task<long> GetGameServerCountAsync(CancellationToken cancellationToken = default)
    {
        var result = await Client.GameServers
            .All
            .Count
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? default;
    }

    public async Task<IReadOnlyCollection<ConfigurationEntryDefinition>> GetConfigurationDefinitionsAsync(
        long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var result = await Client.GameServers[gameServerId]
            .Configuration
            .Definitions
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result?.ToImmutableArray() ?? [];
    }

    public async Task<IReadOnlyCollection<PlainConfigurationEntryDto>> GetConfigurationAsync(
        long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var result = await Client.GameServers[gameServerId]
            .Configuration
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result?.ToImmutableArray() ?? [];
    }

    public async Task SetConfigurationAsync(
        long gameServerId,
        IEnumerable<ConfigurationEntryUpdate> configurationEntries,
        CancellationToken cancellationToken
    )
    {
        await Client.GameServers[gameServerId]
            .Configuration
            .PostAsync(configurationEntries.ToList(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<GameServerInfoDto>> GetGameServersAsync(
        CancellationToken cancellationToken = default
    )
    {
        var gameServers = await Client.GameServers
            .All
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return gameServers?.ToImmutableArray() ?? [];
    }

    public async Task<IReadOnlyCollection<PlainGameServerLogDto>> GetLogsAsync(
        long gameServerId,
        int skip,
        int take,
        DateTimeOffset? referenceTimeStamp = null,
        bool descendingByTimestamp = false,
        CancellationToken cancellationToken = default
    )
    {
        var gameServers = await Client.GameServers[gameServerId]
            .Logs
            .GetAsync(
                conf =>
                {
                    conf.QueryParameters.Skip                  = skip;
                    conf.QueryParameters.Take                  = take;
                    conf.QueryParameters.DescendingByTimeStamp = descendingByTimestamp;
                    conf.QueryParameters.ReferenceTimeStamp    = referenceTimeStamp;
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        return gameServers?.ToImmutableArray() ?? [];
    }

    public async Task<long> GetLogsCountAsync(
        long gameServerId,
        DateTimeOffset? referenceTimeStamp = null,
        CancellationToken cancellationToken = default
    )
    {
        var logsCount = await Client.GameServers[gameServerId]
            .Logs
            .Count
            .GetAsync(conf => conf.QueryParameters.ReferenceTimeStamp = referenceTimeStamp, cancellationToken)
            .ConfigureAwait(false);
        return logsCount ?? default;
    }

    public async Task<GameServerInfoDto> GetGameServerAsync(
        long gameServerId,
        CancellationToken cancellationToken = default
    )
    {
        var gameServer = await Client.GameServers[gameServerId]
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return gameServer ?? throw new NullReferenceException("Failed to get game server.");
    }

    public async Task ChangeTitleAsync(long gameServerId, string title, CancellationToken cancellationToken = default)
    {
        await Client.GameServers[gameServerId]
            .Rename
            .PutAsync(title, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ChangeModPackAsync(
        long gameServerId,
        long modPackDefinitionId,
        CancellationToken cancellationToken = default
    )
    {
        await Client.GameServers[gameServerId]
            .ModPack
            .Set
            .PutAsync((int) modPackDefinitionId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ClearModPackAsync(long gameServerId, CancellationToken cancellationToken = default)
    {
        await Client.GameServers[gameServerId]
            .ModPack
            .Clear
            .PutAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> GetGameServerControllersAsync(
        CancellationToken cancellationToken = default
    )
    {
        var gameServerControllers = await Client.GameServers
            .All
            .Controllers
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return gameServerControllers?.ToImmutableArray() ?? [];
    }

    public async Task<GameServerInfoDto> CreateGameServerAsync(
        string controllerIdentifier,
        string title,
        CancellationToken cancellationToken = default
    )
    {
        var info = await Client.GameServers
            .Create[controllerIdentifier]
            .PostAsync(title, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return info ?? throw new NullReferenceException("Failed to create game server.");
    }

    public async Task DeleteGameServerAsync(long gameServerId, CancellationToken cancellationToken = default)
    {
        await Client.GameServers[gameServerId]
            .DeletePath
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameServerInfoDto> StartGameServerAsync(
        long gameServerId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.GameServers[gameServerId]
            .Start
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Failed to start game server.");
    }

    public async Task<GameServerInfoDto> StopGameServerAsync(
        long gameServerId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.GameServers[gameServerId]
            .Stop
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Failed to stop game server.");
    }

    public async Task<GameServerInfoDto> UpgradeGameServerAsync(
        long gameServerId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.GameServers[gameServerId]
            .Upgrade
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Failed to upgrade game server.");
    }

    public async Task<IReadOnlyCollection<GameFolder>> GetGameServerFoldersAsync(
        long gameServerId,
        CancellationToken cancellationToken = default
    )
    {
        var folders = await Client.GameServers[gameServerId]
            .Folders
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return folders?.ToImmutableArray() ?? [];
    }

    public async Task<IReadOnlyCollection<GameFileInfo>> GetGameServerFolderFilesAsync(
        long gameServerId,
        string gameFolderIdentifier,
        CancellationToken cancellationToken = default
    )
    {
        var folders = await Client.GameServers[gameServerId]
            .Folders[gameFolderIdentifier]
            .Files
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return folders?.ToImmutableArray() ?? [];
    }

    public async Task UploadGameServerFolderFileAsync(
        long gameServerId,
        string gameFolderIdentifier,
        Stream fileStream,
        string fileName,
        string fileMimeType,
        CancellationToken cancellationToken = default
    )
    {
        // https://github.com/microsoft/kiota/issues/3826
        var body = new MultipartBody();
        body.AddOrReplacePart(fileName, fileMimeType, fileStream);
        await Client.GameServers[gameServerId]
            .Folders[gameFolderIdentifier]
            .Upload
            .PostAsync(body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteGameServerFolderFileAsync(
        long gameServerId,
        string gameFolderIdentifier,
        string gameFileFolder,
        CancellationToken cancellationToken = default
    )
    {
        await Client.GameServers[gameServerId]
            .Folders[gameFolderIdentifier][gameFileFolder]
            .DeletePath
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ClearAsync(long gameServerId, CancellationToken cancellationToken = default)
    {
        await Client.GameServers[gameServerId]
            .Logs
            .Clear
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
