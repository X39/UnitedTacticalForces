using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController.Controllers;

public abstract class GameServerControllerBase : IGameServerController
{
    protected string GameServerLastKnownTitle { get; private set; }
    protected long GameServerPrimaryKey { get; }
    protected IDbContextFactory<ApiDbContext> DbContextFactory { get; }
    protected ILogger Logger { get; }


    protected GameServerControllerBase(
        GameServer gameServer,
        IDbContextFactory<ApiDbContext> dbContextFactory,
        ILogger logger)
    {
        GameServerPrimaryKey     = gameServer.PrimaryKey;
        GameServerLastKnownTitle = gameServer.Title;
        DbContextFactory         = dbContextFactory;
        Logger                   = logger;
    }

    public abstract bool AllowAnyConfigurationEntry { get; }
    public abstract IEnumerable<ConfigurationEntryDefinition> GetConfigurationEntryDefinitions(CultureInfo cultureInfo);
    public abstract bool CanUpdateConfiguration { get; }
    public abstract Task UpdateConfigurationAsync();
    public abstract bool CanStart { get; }
    public abstract Task StartAsync(User? executingUser);
    public abstract bool CanStop { get; }
    public abstract Task StopAsync(User? executingUser);
    public abstract bool CanInstallOrUpgrade { get; }
    public abstract bool IsRunning { get; }
    public abstract Task InstallOrUpgradeAsync(User? executingUser);
    public abstract bool CanModifyGameFiles { get; }

    public abstract Task<IEnumerable<GameFolder>> GetGameFoldersAsync(
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default);

    public abstract Task<IEnumerable<GameFileInfo>> GetGameFolderFilesAsync(
        GameFolder folder,
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default);


    public abstract Task<Stream> GetGameFolderFileAsync(
        GameFolder folder,
        GameFileInfo file,
        CancellationToken cancellationToken = default);

    public abstract Task UploadFileAsync(GameFolder folder, GameFileInfo file, Stream stream);
    public abstract Task DeleteFileAsync(GameFolder folder, GameFileInfo file);

    public abstract Task<string?> GetCommonConfigurationAsync(
        ECommonConfiguration commonConfig,
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default);


    protected async ValueTask<string> GetAsync(
        string identifier,
        string fallback = "",
        CancellationToken cancellationToken = default,
        ApiDbContext? dbContext = null)
    {
        async Task<string> Inner()
        {
            var splitIndex = identifier.IndexOf("://", StringComparison.Ordinal);
            var realm = identifier[..splitIndex];
            var path = identifier[(splitIndex + 3)..];
            var gameServerPk = GameServerPrimaryKey;
            var value = await dbContext.ConfigurationEntries
                .Where((q) => q.IsActive)
                .Where((q) => q.GameServerFk == gameServerPk)
                .Where((q) => q.Realm == realm && q.Path == path)
                .Select((q) => q.Value)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            return value ?? fallback;
        }

        if (dbContext is not null)
            return await Inner().ConfigureAwait(false);
        dbContext = await DbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            return await Inner()
                .ConfigureAwait(false);
        }
        finally
        {
            await dbContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    protected async ValueTask<T> GetAsync<T>(
        string identifier,
        T fallback = default,
        CancellationToken cancellationToken = default,
        ApiDbContext? dbContext = null)
        where T : struct
    {
        async Task<T> Inner()
        {
            var value = await GetAsync(identifier, null!, cancellationToken, dbContext)
                .ConfigureAwait(false);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (value is null)
                return fallback;
            var typeConverter = TypeDescriptor.GetConverter(typeof(T));
            if (!typeConverter.CanConvertFrom(typeof(string)))
                throw new InvalidOperationException(
                    $"The TypeConverter for {typeof(T).FullName()} reported " +
                    $"that it is not able to convert from {typeof(string).FullName()}");
            return (T) (typeConverter.ConvertFrom(value) ?? fallback);
        }

        if (dbContext is not null)
            return await Inner().ConfigureAwait(false);
        dbContext = await DbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            return await Inner()
                .ConfigureAwait(false);
        }
        finally
        {
            await dbContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a directory in accordance with OS-Platform user rights.
    /// </summary>
    /// <param name="directory">The directory to create.</param>
    protected static void CreateDirectory(string directory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Directory.CreateDirectory(
                directory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
        else
            Directory.CreateDirectory(directory);
    }

    /// <summary>
    /// Returns the <see cref="GameServer"/> of this controller from the database.
    /// </summary>
    /// <param name="dbContext">The <see cref="ApiDbContext"/> to use.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>The <see cref="GameServer"/> of this controller from the database.</returns>
    /// <exception cref="NullReferenceException">Thrown when the <see cref="GameServer"/> could not be loaded from the database.</exception>
    protected async ValueTask<GameServer> GetGameServerAsync(
        ApiDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var gameServer = await dbContext.GameServers
            .SingleOrDefaultAsync((q) => q.PrimaryKey == GameServerPrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        if (gameServer is null)
            throw new NullReferenceException("Failed to load GameServer from database.");
        GameServerLastKnownTitle = gameServer.Title;
        return gameServer;
    }
}