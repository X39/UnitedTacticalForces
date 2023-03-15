using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.Util;
using X39.Util.Threading;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController.Controllers;

public abstract class SteamGameServerControllerBase : GameServerControllerBase
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Game server process.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    protected Process? Process { get; private set; }

    private readonly CancellationTokenSource _cancellationTokenSource;

    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// The App-Id of the game.
    /// </summary>
    protected abstract long SteamAppId { get; }

    /// <inheritdoc />
    protected SteamGameServerControllerBase(
        IConfiguration configuration,
        GameServer gameServer,
        IDbContextFactory<ApiDbContext> dbContextFactory,
        ILogger logger) : base(gameServer, dbContextFactory, logger)
    {
        _configuration           = configuration;
        _cancellationTokenSource = new();
        _semaphore               = new SemaphoreSlim(0, 1);
    }

    /// <inheritdoc />
    public override bool CanUpdateConfiguration => CanStart;

    /// <inheritdoc />
    public override bool CanStart => Process is null || Process.HasExited;

    /// <inheritdoc />
    public override bool CanStop => Process is not null && !Process.HasExited;

    /// <inheritdoc />
    public override bool CanInstallOrUpgrade => CanStart;
    
    /// <inheritdoc />
    public override bool IsRunning => Process is null || !Process.HasExited;

    /// <inheritdoc />
    public override async Task UpdateConfigurationAsync()
    {
        await _semaphore.LockedAsync(
                async () =>
                {
                    if (!CanUpdateConfiguration)
                        throw new InvalidOperationException("CanUpdateConfiguration reports false");
                    await DoUpdateConfigurationAsync().ConfigureAwait(false);
                })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task StartAsync(User? executingUser)
    {
        await _semaphore.LockedAsync(
                async () =>
                {
                    if (!CanStart)
                        throw new InvalidOperationException("CanStart reports false");
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync()
                        .ConfigureAwait(false);
                    dbContext.GameServers.Attach(GameServer);
                    Logger.LogInformation(
                        "User {UserId} initialized stopping of server {GameServer} ({GameServerPk})",
                        GameServer.Title,
                        GameServer.PrimaryKey,
                        executingUser?.PrimaryKey);
                    GameServer.Status = ELifetimeStatus.Starting;
                    await dbContext.LifetimeEvents.AddAsync(
                            new LifetimeEvent
                            {
                                GameServerFk = GameServer.PrimaryKey,
                                TimeStamp    = DateTimeOffset.Now,
                                ExecutedByFk = executingUser?.PrimaryKey,
                                Status       = ELifetimeStatus.Starting,
                                GameServer   = default,
                                PrimaryKey   = default,
                                ExecutedBy   = default,
                            })
                        .ConfigureAwait(false);
                    await dbContext.SaveChangesAsync()
                        .ConfigureAwait(false);
                    var processStartInfo = await GetProcessStartInfoAsync(dbContext, executingUser);
                    Logger.LogInformation(
                        "User {User} has requested starting of server {GameServer} ({GameServerPk}) with {LaunchArgs}",
                        GameServer.Title,
                        GameServer.PrimaryKey,
                        processStartInfo.ArgumentList,
                        executingUser?.PrimaryKey);
                    var process = new Process
                    {
                        StartInfo            = processStartInfo,
                        EnableRaisingEvents  = true,
                        PriorityBoostEnabled = false,
                    };
                    process.Exited             += ProcessOnExited;
                    process.ErrorDataReceived  += ProcessOnErrorDataReceived;
                    process.OutputDataReceived += ProcessOnOutputDataReceived;
                    try
                    {
                        var startResult = process.Start();
                        await OnStartAsync(executingUser, dbContext, process).ConfigureAwait(false);
                        if (startResult is false)
                            throw new FailedToStartProcessException(processStartInfo);
                        GameServer.Status = ELifetimeStatus.Running;
                        await dbContext.LifetimeEvents.AddAsync(
                                new LifetimeEvent
                                {
                                    GameServerFk = GameServer.PrimaryKey,
                                    TimeStamp    = DateTimeOffset.Now,
                                    ExecutedByFk = executingUser?.PrimaryKey,
                                    Status       = ELifetimeStatus.Running,
                                    GameServer   = default,
                                    PrimaryKey   = default,
                                    ExecutedBy   = default,
                                })
                            .ConfigureAwait(false);
                        await dbContext.SaveChangesAsync()
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // ReSharper disable once AccessToDisposedClosure
                        Fault.Ignore(() => process.Kill());
                        // ReSharper disable once AccessToDisposedClosure
                        Fault.Ignore(() => process.Close());
                        process.Dispose();
                        Logger.LogError(
                            ex,
                            "Failed starting server {GameServer} ({GameServerPk}) with {LaunchArgs} requested by user {User}",
                            GameServer.Title,
                            GameServer.PrimaryKey,
                            processStartInfo.ArgumentList,
                            executingUser?.PrimaryKey);
                        throw;
                    }

                    Process = process;
                })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task StopAsync(User? executingUser)
    {
        await _semaphore.LockedAsync(
                async () =>
                {
                    if (!CanStop)
                        throw new InvalidOperationException("CanStop reports false");
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync()
                        .ConfigureAwait(false);
                    dbContext.GameServers.Attach(GameServer);
                    Logger.LogInformation(
                        "User {UserId} initialized stopping of server {GameServer} ({GameServerPk})",
                        GameServer.Title,
                        GameServer.PrimaryKey,
                        executingUser?.PrimaryKey);
                    GameServer.Status = ELifetimeStatus.Stopping;
                    await dbContext.LifetimeEvents.AddAsync(
                            new LifetimeEvent
                            {
                                GameServerFk = GameServer.PrimaryKey,
                                TimeStamp    = DateTimeOffset.Now,
                                ExecutedByFk = executingUser?.PrimaryKey,
                                Status       = ELifetimeStatus.Stopping,
                                GameServer   = default,
                                PrimaryKey   = default,
                                ExecutedBy   = default,
                            })
                        .ConfigureAwait(false);
                    await dbContext.SaveChangesAsync()
                        .ConfigureAwait(false);
                    if (Process is null)
                        throw new NullReferenceException("_process was unexpectedly null");
                    try
                    {
                        await OnStopAsync(executingUser, dbContext).ConfigureAwait(false);
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            Process.Kill(LinuxUtils.Signum.SIGTERM);
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            Process.CloseMainWindow();
                        else
                            Process.Kill();

                        await Process.WaitForExitAsync(_cancellationTokenSource.Token)
                            .ConfigureAwait(false);
                        Logger.LogInformation(
                            "Stopped the process of {GameServer} ({GameServerPk}) requested by user {UserId}",
                            GameServer.Title,
                            GameServer.PrimaryKey,
                            executingUser?.PrimaryKey);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(
                            ex,
                            "Failed to gracefully stop the process of {GameServer} ({GameServerPk}) requested by user {UserId}",
                            GameServer.Title,
                            GameServer.PrimaryKey,
                            executingUser?.PrimaryKey);
                        // ReSharper disable once AccessToDisposedClosure
                        Fault.Ignore(() => Process.Kill(true));
                    }
                    finally
                    {
                        Process.Close();
                        Process.Dispose();
                        Process = null;
                    }

                    GameServer.Status = ELifetimeStatus.Stopped;
                    await dbContext.LifetimeEvents.AddAsync(
                            new LifetimeEvent
                            {
                                GameServerFk = GameServer.PrimaryKey,
                                TimeStamp    = DateTimeOffset.Now,
                                ExecutedByFk = executingUser?.PrimaryKey,
                                Status       = ELifetimeStatus.Stopped,
                                GameServer   = default,
                                PrimaryKey   = default,
                                ExecutedBy   = default,
                            })
                        .ConfigureAwait(false);
                    await dbContext.SaveChangesAsync()
                        .ConfigureAwait(false);
                })
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the path where the game server is installed.
    /// </summary>
    public string GameInstallPath
    {
        get
        {
            var installBasePath = GetInstallBasePath();
            var installPath = Path.Combine(installBasePath, SteamAppId.ToString());
            return installPath;
        }
    }

    /// <inheritdoc />
    public override async Task InstallOrUpgradeAsync(User? executingUser)
    {
        await _semaphore.LockedAsync(
                async () =>
                {
                    if (!CanInstallOrUpgrade)
                        throw new InvalidOperationException("CanStop reports false");
                    var (steamCmdPath,
                        anonymousOnly,
                        steamUsername,
                        steamPassword,
                        installBasePath) = GetSteamCmdInformationTuple();
                    var appId = SteamAppId;
                    var installPath = Path.Combine(installBasePath, appId.ToString());
                    await DoUpdateGameAsync(
                            steamCmdPath,
                            installPath,
                            anonymousOnly,
                            steamUsername,
                            steamPassword,
                            appId,
                            executingUser)
                        .ConfigureAwait(false);
                    await OnInstallOrUpgradeAsync(executingUser)
                        .ConfigureAwait(false);
                })
            .ConfigureAwait(false);
    }

    private (string steamCmdPath, bool anonymousOnly, string steamUsername, string steamPassword, string
        gameInstallBasePath
        ) GetSteamCmdInformationTuple()
    {
        var steamCmdPath = GetSteamCmdPath();
        var anonymousOnly = GetAnonymousOnly();
        var steamUsername = anonymousOnly ? string.Empty : GetSteamUsername();
        var steamPassword = anonymousOnly ? string.Empty : GetSteamPassword();
        var gameInstallBasePath = GetInstallBasePath();
        return (steamCmdPath, anonymousOnly, steamUsername, steamPassword, gameInstallBasePath);
    }

    private async Task DoUpdateGameAsync(
        string steamCmdPath,
        string installPath,
        bool anonymousOnly,
        string steamUsername,
        string steamPassword,
        long appId,
        User? executingUser)
    {
        Logger.LogInformation(
            "Starting update of {GameServer} ({GameServerPk}) via SteamCmd, requested by user {UserId}",
            GameServer.Title,
            GameServer.PrimaryKey,
            executingUser?.PrimaryKey);
        var psi = new ProcessStartInfo
        {
            FileName = steamCmdPath,
            ArgumentList =
            {
                "+force_install_dir", installPath,
                "+login", anonymousOnly ? "anonymous" : string.Join(' ', steamUsername, steamPassword),
                "+app_update", appId.ToString(),
                "+quit",
            },
            UseShellExecute        = true,
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
        };
        using var tmp = new Process
        {
            StartInfo           = psi,
            EnableRaisingEvents = true,
        };
        tmp.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not { } message)
                return;
            var now = DateTimeOffset.Now;
            Task.Run(
                async () =>
                {
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254
                    Logger.LogError(message);
#pragma warning restore CA2254
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                    dbContext.GameServerLogs.Add(
                        new GameServerLog
                        {
                            Message   = message,
                            TimeStamp = now,
                            LogLevel  = LogLevel.Error,
                            Source    = "SteamCmd",
                        });
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                });
        };
        tmp.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not { } message)
                return;
            var now = DateTimeOffset.Now;
            Task.Run(
                async () =>
                {
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254
                    Logger.LogInformation(message);
#pragma warning restore CA2254
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                    dbContext.GameServerLogs.Add(
                        new GameServerLog
                        {
                            Message   = message,
                            TimeStamp = now,
                            LogLevel  = LogLevel.Information,
                            Source    = "SteamCmd",
                        });
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                });
        };
        if (!tmp.Start())
            throw new FailedToStartProcessException(psi);
        await tmp.WaitForExitAsync()
            .ConfigureAwait(false);
        Logger.LogInformation(
            "Finished update of {GameServer} ({GameServerPk}) via SteamCmd, requested by user {UserId}",
            GameServer.Title,
            GameServer.PrimaryKey,
            executingUser?.PrimaryKey);
    }

    /// <summary>
    /// Updates the given workshop item identified via <paramref name="workshopId"/>. 
    /// </summary>
    /// <param name="workshopId">The workshop id.</param>
    /// <param name="executingUser">The user that requested the update.</param>
    /// <exception cref="FailedToStartProcessException">Thrown when the SteamCmd process failed to start.</exception>
    /// <returns>The full path to the workshop item.</returns>
    protected async Task<string> DoUpdateWorkshopMod(
        long workshopId,
        User? executingUser)
    {
        var (steamCmdPath,
            anonymousOnly,
            steamUsername,
            steamPassword,
            installBasePath) = GetSteamCmdInformationTuple();
        var installPath = Path.Combine(installBasePath, workshopId.ToString());
        Logger.LogInformation(
            "Starting update of workshop item {WorkshopId} via SteamCmd, requested by user {UserId}",
            workshopId,
            executingUser?.PrimaryKey);
        var psi = new ProcessStartInfo
        {
            FileName = steamCmdPath,
            ArgumentList =
            {
                "+force_install_dir", installPath,
                "+login", anonymousOnly ? "anonymous" : string.Join(' ', steamUsername, steamPassword),
                "+workshop_download_item", workshopId.ToString(),
                "+quit",
            },
            UseShellExecute        = true,
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
        };
        using var tmp = new Process
        {
            StartInfo           = psi,
            EnableRaisingEvents = true,
        };
        tmp.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not { } message)
                return;
            var now = DateTimeOffset.Now;
            Task.Run(
                async () =>
                {
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254
                    Logger.LogError(message);
#pragma warning restore CA2254
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                    dbContext.GameServerLogs.Add(
                        new GameServerLog
                        {
                            Message   = message,
                            TimeStamp = now,
                            LogLevel  = LogLevel.Error,
                            Source    = "SteamCmd",
                        });
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                });
        };
        tmp.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not { } message)
                return;
            var now = DateTimeOffset.Now;
            Task.Run(
                async () =>
                {
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254
                    Logger.LogInformation(message);
#pragma warning restore CA2254
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                    dbContext.GameServerLogs.Add(
                        new GameServerLog
                        {
                            Message   = message,
                            TimeStamp = now,
                            LogLevel  = LogLevel.Information,
                            Source    = "SteamCmd",
                        });
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                });
        };
        if (!tmp.Start())
            throw new FailedToStartProcessException(psi);
        await tmp.WaitForExitAsync()
            .ConfigureAwait(false);
        Logger.LogInformation(
            "Finished update of workshop item {WorkshopId} via SteamCmd, requested by user {UserId}",
            workshopId,
            executingUser?.PrimaryKey);
        return installPath;
    }

    private string GetSteamUsername()
    {
        var username = _configuration[Constants.Configuration.Steam.Username];
        if (username is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.Username} in appsettings.json is null. Leave empty if only anonymous is required.");
        return username;
    }

    private string GetSteamPassword()
    {
        var password = _configuration[Constants.Configuration.Steam.Password];
        if (password is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.Password} in appsettings.json is null. Leave empty if only anonymous is required.");
        return password;
    }

    private string GetSteamCmdPath()
    {
        var steamCmdPath = _configuration[Constants.Configuration.Steam.SteamCmdPath];
        if (steamCmdPath is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.SteamCmdPath} in appsettings.json is null");
        steamCmdPath = Path.GetFullPath(steamCmdPath);
        if (!Path.Exists(steamCmdPath))
            throw new FileNotFoundException($"SteamCmd was not found at {steamCmdPath}", steamCmdPath);
        return steamCmdPath;
    }

    private bool GetAnonymousOnly()
    {
        var anonymousOnly = _configuration[Constants.Configuration.Steam.AnonymousOnly];
        if (anonymousOnly is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.AnonymousOnly} in appsettings.json is null");
        return bool.Parse(anonymousOnly);
    }

    private string GetInstallBasePath()
    {
        var installBasePath = _configuration[Constants.Configuration.Steam.InstallBasePath];
        if (installBasePath is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.InstallBasePath} in appsettings.json is null");
        installBasePath = Path.GetFullPath(installBasePath);

        Directory.CreateDirectory(installBasePath);
        return installBasePath;
    }

    /// <inheritdoc cref="UpdateConfigurationAsync"/>
    protected abstract Task DoUpdateConfigurationAsync();

    /// <inheritdoc cref="StartAsync"/>
    protected virtual Task OnStartAsync(User? executingUser, ApiDbContext apiDbContext, Process process) =>
        Task.CompletedTask;

    /// <inheritdoc cref="StopAsync"/>
    protected virtual Task OnStopAsync(User? executingUser, ApiDbContext apiDbContext) => Task.CompletedTask;

    /// <inheritdoc cref="InstallOrUpgradeAsync"/>
    protected virtual Task OnInstallOrUpgradeAsync(User? executingUser) => Task.CompletedTask;

    /// <summary>
    /// Retrieves the game-specific <see cref="ProcessStartInfo"/>.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="executingUser"></param>
    /// <returns>A new <see cref="ProcessStartInfo"/> to start the game server.</returns>
    protected abstract ValueTask<ProcessStartInfo> GetProcessStartInfoAsync(ApiDbContext dbContext, User? executingUser);

    private void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not { } message)
            return;
        BeginWritingServerLog(LogLevel.Information, message);
    }

    private void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not { } message)
            return;
        BeginWritingServerLog(LogLevel.Error, message);
    }


    private void BeginWritingServerLog(LogLevel logLevel, string message)
    {
        var now = DateTimeOffset.Now;
        Task.Run(
            async () =>
            {
                Logger.LogDebug(
                    "{GameServer} ({GameServerPk}): [{LogLevel}] {Message}",
                    GameServer.Title,
                    GameServer.PrimaryKey,
                    logLevel,
                    message);
                try
                {
                    await using var dbContext = await DbContextFactory
                        .CreateDbContextAsync(_cancellationTokenSource.Token)
                        .ConfigureAwait(false);
                    await dbContext.GameServerLogs.AddAsync(
                            new GameServerLog
                            {
                                TimeStamp = now,
                                LogLevel  = logLevel,
                                Message   = message,
                            },
                            _cancellationTokenSource.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(
                        ex,
                        "Failed to register the following log message for {GameServer} ({GameServerPk}): [{LogLevel}] {Message}",
                        GameServer.Title,
                        GameServer.PrimaryKey,
                        logLevel,
                        message);
                }
            },
            _cancellationTokenSource.Token);
    }

    private void ProcessOnExited(object? sender, EventArgs e)
    {
        BeginWritingServerLog(LogLevel.Information, "-----Process exited-----");
    }
}