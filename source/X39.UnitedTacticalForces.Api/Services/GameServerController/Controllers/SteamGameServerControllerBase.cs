using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
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
    /// The App-Id of the server.
    /// </summary>
    protected abstract long ServerAppId { get; }

    /// <summary>
    /// The App-Id of the game.
    /// </summary>
    protected abstract long GameAppId { get; }

    /// <summary>
    /// Determines whether the game allows anonymous access to the server files or not.
    /// </summary>
    protected abstract bool RequireLogin { get; }

    /// <summary>
    /// Determines whether the game must be owned for downloading workshop items.
    /// </summary>
    protected abstract bool RequirePurchaseForWorkshop { get; }

    /// <inheritdoc />
    protected SteamGameServerControllerBase(
        IConfiguration configuration,
        GameServer gameServer,
        IDbContextFactory<ApiDbContext> dbContextFactory,
        ILogger logger) : base(gameServer, dbContextFactory, logger)
    {
        _configuration           = configuration;
        _cancellationTokenSource = new();
        _semaphore               = new SemaphoreSlim(1, 1);
    }

    /// <inheritdoc />
    public override bool CanUpdateConfiguration => CanStart;

    /// <inheritdoc />
    public override bool CanStart => (Process is null || Process.HasExited) && _semaphore.CurrentCount is not 0;

    /// <inheritdoc />
    public override bool CanStop => Process is not null && !Process.HasExited;

    /// <inheritdoc />
    public override bool CanInstallOrUpgrade => CanStart;

    /// <inheritdoc />
    public override bool IsRunning => Process is not null;

    /// <inheritdoc />
    public override async Task UpdateConfigurationAsync()
    {
        if (!CanUpdateConfiguration)
            throw new InvalidOperationException("CanUpdateConfiguration reports false");
        await _semaphore.LockedAsync(
                async () => { await DoUpdateConfigurationAsync().ConfigureAwait(false); })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task StartAsync(User? executingUser)
    {
        if (!CanStart)
            throw new InvalidOperationException("CanStart reports false");
        await _semaphore.LockedAsync(
                async () =>
                {
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync()
                        .ConfigureAwait(false);
                    dbContext.GameServers.Attach(GameServer);
                    Logger.LogInformation(
                        "User {UserId} initialized stopping of server {GameServer} ({GameServerPk})",
                        GameServer.Title,
                        GameServer.PrimaryKey,
                        executingUser?.PrimaryKey);
                    Process? process = null;
                    ProcessStartInfo? processStartInfo = null;
                    try
                    {
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
                        processStartInfo = await GetProcessStartInfoAsync(dbContext, executingUser);
                        Logger.LogInformation(
                            "User {User} has requested starting of server {GameServer} ({GameServerPk}) with {LaunchArgs}",
                            GameServer.Title,
                            GameServer.PrimaryKey,
                            $"{processStartInfo.FileName} {string.Join(' ', processStartInfo.ArgumentList.Select((q) => string.Concat('"', q, '"')))}",
                            executingUser?.PrimaryKey);
                        process = new Process
                        {
                            StartInfo           = processStartInfo,
                            EnableRaisingEvents = true,
                        };
                        process.Exited             += ProcessOnExited;
                        process.ErrorDataReceived  += ProcessOnErrorDataReceived;
                        process.OutputDataReceived += ProcessOnOutputDataReceived;
                        var startResult = process.Start();
                        Process = process;
                        process.BeginErrorReadLine();
                        process.BeginOutputReadLine();
                        await OnStartAsync(executingUser, dbContext, process).ConfigureAwait(false);
                        await Task.Delay(TimeSpan.FromSeconds(1))
                            .ConfigureAwait(false);
                        if (startResult is false)
                            throw new FailedToStartProcessException(processStartInfo);
                        if (Process.HasExited)
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
                        if (process is not null)
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            Fault.Ignore(() => process.Kill());
                            // ReSharper disable once AccessToDisposedClosure
                            Fault.Ignore(() => process.Close());
                            process.Dispose();
                        }

                        Logger.LogError(
                            ex,
                            "Failed starting server {GameServer} ({GameServerPk}) with {LaunchArgs} requested by user {User}",
                            GameServer.Title,
                            GameServer.PrimaryKey,
                            processStartInfo?.ArgumentList,
                            executingUser?.PrimaryKey);
                        Process           = null;
                        GameServer.Status = ELifetimeStatus.Stopped;
                        await dbContext.SaveChangesAsync()
                            .ConfigureAwait(false);
                        throw;
                    }
                })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task StopAsync(User? executingUser)
    {
        if (!CanStop)
            throw new InvalidOperationException("CanStop reports false");
        await _semaphore.LockedAsync(
                async () =>
                {
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

                        // ReSharper disable once MergeIntoPattern
                        while (Process is { } process && !process.HasExited)
                        {
                            await Task.Delay(250);
                        }

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
                        Fault.Ignore(() => Process?.Kill(true));
                    }
                    finally
                    {
                        Process?.CancelErrorRead();
                        Process?.CancelOutputRead();
                        Process?.Close();
                        var tmp = Process;
                        Process = null;
                        tmp?.Dispose();
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
    /// Returns the path where the actual game server is installed.
    /// </summary>
    public string GameInstallPath
    {
        get
        {
            var installBasePath = GetInstallBasePath();
            var installPath = Path.Combine(
                installBasePath,
                ServerAppId.ToString(),
                GameServer.PrimaryKey.ToString("00000000"),
                "server-instance");
            return installPath;
        }
    }

    /// <summary>
    /// Returns the path where the game server is living.
    /// </summary>
    public string GameServerPath
    {
        get
        {
            var installBasePath = GetInstallBasePath();
            var installPath = Path.Combine(
                installBasePath,
                ServerAppId.ToString(),
                GameServer.PrimaryKey.ToString("00000000"));
            return installPath;
        }
    }

    /// <inheritdoc />
    public override async Task InstallOrUpgradeAsync(User? executingUser)
    {
        if (!CanInstallOrUpgrade)
            throw new InvalidOperationException("CanInstallOrUpgrade reports false");
        await _semaphore.LockedAsync(
                async () =>
                {
                    var (steamCmdPath,
                        steamUsername,
                        steamPassword,
                        _) = GetSteamCmdInformationTuple();
                    var appId = ServerAppId;
                    var installPath = GameInstallPath;
                    GameServer.ActiveModPack   = GameServer.SelectedModPack;
                    GameServer.ActiveModPackFk = GameServer.SelectedModPackFk;
                    await DoUpdateGameAsync(
                            steamCmdPath,
                            installPath,
                            steamUsername,
                            steamPassword,
                            appId,
                            executingUser)
                        .ConfigureAwait(false);
                    await OnInstallOrUpgradeAsync(executingUser)
                        .ConfigureAwait(false);
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                    dbContext.Attach(GameServer);
                    GameServer.TimeStampUpgraded = DateTimeOffset.Now;
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                })
            .ConfigureAwait(false);
    }

    private (string steamCmdPath, string steamUsername, string steamPassword, string
        gameInstallBasePath
        ) GetSteamCmdInformationTuple()
    {
        var steamCmdPath = GetSteamCmdPath();
        var steamUsername = RequireLogin ? GetSteamUsername() : string.Empty;
        var steamPassword = RequireLogin ? GetSteamPassword() : string.Empty;
        var gameInstallBasePath = GetInstallBasePath();
        return (steamCmdPath, steamUsername, steamPassword, gameInstallBasePath);
    }

    private async Task DoUpdateGameAsync(
        string steamCmdPath,
        string installPath,
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
            FileName               = steamCmdPath,
            UseShellExecute        = false,
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding  = Encoding.ASCII,
            StandardOutputEncoding = Encoding.ASCII,
            WorkingDirectory       = Path.GetDirectoryName(steamCmdPath),
        };
        psi.ArgumentList.Add("+force_install_dir");
        psi.ArgumentList.Add(installPath);
        psi.ArgumentList.Add("+login");
        if (RequireLogin)
        {
            psi.ArgumentList.Add(steamUsername);
            psi.ArgumentList.Add(steamPassword);
        }
        else
        {
            psi.ArgumentList.Add("anonymous");
        }

        psi.ArgumentList.Add("+app_update");
        psi.ArgumentList.Add(appId.ToString());
        psi.ArgumentList.Add("+quit");
        if (Process is not null && !Process.HasExited)
            throw new InvalidOperationException("Process must be finished for the operation");
        using var tmp = new Process
        {
            StartInfo = psi,
        };
        Process = tmp;
        using var disposable = new Disposable(() => Process = null);
        await StartAndWaitForSteamCmdExitAndLogAsync(tmp);
        if (tmp.ExitCode is not 0)
            throw new Exception("SteamCmd operation failed.");
        Logger.LogInformation(
            "Finished update of {GameServer} ({GameServerPk}) via SteamCmd, requested by user {UserId}",
            GameServer.Title,
            GameServer.PrimaryKey,
            executingUser?.PrimaryKey);
    }

    private async Task SteamCmdLogAsync(LogLevel logLevel, string message, DateTimeOffset timeStamp)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
        dbContext.GameServerLogs.Add(
            new GameServerLog
            {
                Message   = message,
                TimeStamp = timeStamp,
                LogLevel  = logLevel,
                Source    = "SteamCmd",
            });
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the corresponding path of a <paramref name="workshopId"/>
    /// </summary>
    /// <param name="workshopId">The workshop id to get a path for.</param>
    /// <returns>The path to the workshop id that may or may not exist.</returns>
    protected string GetWorkshopPath(long workshopId)
    {
        var installBasePath = GetInstallBasePath();
        return Path.Combine(installBasePath, workshopId.ToString());
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
            steamUsername,
            steamPassword,
            installBasePath) = GetSteamCmdInformationTuple();
        var installPath = GetWorkshopPath(workshopId);
        Logger.LogInformation(
            "Starting update of workshop item {WorkshopId} via SteamCmd, requested by user {UserId}",
            workshopId,
            executingUser?.PrimaryKey);
        var psi = new ProcessStartInfo
        {
            FileName               = steamCmdPath,
            UseShellExecute        = false,
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding  = Encoding.ASCII,
            StandardOutputEncoding = Encoding.ASCII,
            WorkingDirectory       = Path.GetDirectoryName(steamCmdPath),
        };
        psi.ArgumentList.Add("+force_install_dir");
        psi.ArgumentList.Add(installPath);
        psi.ArgumentList.Add("+login");
        if (RequireLogin)
        {
            psi.ArgumentList.Add(steamUsername);
            psi.ArgumentList.Add(steamPassword);
        }
        else
        {
            psi.ArgumentList.Add("anonymous");
        }

        psi.ArgumentList.Add("+workshop_download_item");
        psi.ArgumentList.Add(GameAppId.ToString());
        psi.ArgumentList.Add(workshopId.ToString());
        psi.ArgumentList.Add("+quit");
        if (Process is not null && !Process.HasExited)
            throw new InvalidOperationException("Process must be finished for the operation");

        await ExecuteAsync(psi);

        Logger.LogInformation(
            "Finished update of workshop item {WorkshopId} via SteamCmd, requested by user {UserId}",
            workshopId,
            executingUser?.PrimaryKey);
        return installPath;
    }

    private async Task ExecuteAsync(ProcessStartInfo psi, int attempts = 3)
    {
        for (var i = 1; i <= attempts; i++)
        {
            try
            {
                using var tmp = new Process
                {
                    StartInfo = psi,
                };
                Process = tmp;
                using var disposable = new Disposable(() => Process = null);
                await StartAndWaitForSteamCmdExitAndLogAsync(tmp);

                if (tmp.ExitCode is not 0)
                    throw new Exception("SteamCmd operation failed.");
                return;
            }
            catch (Exception ex) when (i < attempts)
            {
                Logger.LogError(ex, "Execution failed during attempt {I}/{Attempts}", i, attempts);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Execution failed during attempt {I}/{Attempts}", i, attempts);
                throw;
            }
        }
    }

    private async Task StartAndWaitForSteamCmdExitAndLogAsync(Process tmp)
    {
        tmp.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not { } message)
                return;
            _ = SteamCmdLogAsync(LogLevel.Error, message, DateTimeOffset.Now).ConfigureAwait(false);
        };
        tmp.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not { } message)
                return;
            _ = SteamCmdLogAsync(LogLevel.Information, message, DateTimeOffset.Now).ConfigureAwait(false);
        };
#if DEBUG
        Logger.LogTrace(
            "Starting SteamCmd with {Command}",
            $"{tmp.StartInfo.FileName} {string.Join(' ', tmp.StartInfo.ArgumentList)}");
        await SteamCmdLogAsync(
            LogLevel.Trace,
            $"{tmp.StartInfo.FileName} {string.Join(' ', tmp.StartInfo.ArgumentList)}",
            DateTimeOffset.Now);
#endif
        if (!tmp.Start())
            throw new FailedToStartProcessException(tmp.StartInfo);
        tmp.BeginErrorReadLine();
        tmp.BeginOutputReadLine();
        while (!tmp.HasExited)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250))
                .ConfigureAwait(false);
        }

        await tmp.WaitForExitAsync()
            .ConfigureAwait(false);
        // Wait a full minute to not exceed steams rate limit with SteamCmd calls.
        await Task.Delay(TimeSpan.FromMinutes(1))
            .ConfigureAwait(false);
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

    private string GetInstallBasePath()
    {
        var installBasePath = _configuration[Constants.Configuration.Steam.InstallBasePath];
        if (installBasePath is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.InstallBasePath} in appsettings.json is null");
        installBasePath = Path.GetFullPath(installBasePath);

        CreateDirectory(installBasePath);
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
    protected abstract ValueTask<ProcessStartInfo> GetProcessStartInfoAsync(
        ApiDbContext dbContext,
        User? executingUser);

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
                const int maxAttempts = 3;
                for (var i = 1; i < maxAttempts; i++)
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
                                    Source    = $"[{GameServer.PrimaryKey}] {GameServer.Title}",
                                },
                                _cancellationTokenSource.Token)
                            .ConfigureAwait(false);
                        await dbContext.SaveChangesAsync()
                            .ConfigureAwait(false);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(
                            ex,
                            "Failed to register the following in attempt {Attempt}/{MaxAttempts} log message for {GameServer} ({GameServerPk}): [{LogLevel}] {Message}",
                            i,
                            maxAttempts,
                            GameServer.Title,
                            GameServer.PrimaryKey,
                            logLevel,
                            message);
                    }
                }
            },
            _cancellationTokenSource.Token);
    }

    private void ProcessOnExited(object? sender, EventArgs e)
    {
        using var dbContext = DbContextFactory.CreateDbContext();
        dbContext.GameServers.Attach(GameServer);
        GameServer.Status = ELifetimeStatus.Stopped;
        dbContext.SaveChanges();
        if (Process is { } process)
        {
            var exitCode = process.ExitCode;
            var exitTime = process.ExitTime;
            Process = null;
            // ReSharper disable once AccessToDisposedClosure
            Fault.Ignore(() => process.Kill());
            process.Dispose();
            BeginWritingServerLog(
                exitCode is 0 ? LogLevel.Information : LogLevel.Error,
                $"-----Process exited with {exitCode:X} at {exitTime}-----");
        }
        else
        {
            BeginWritingServerLog(LogLevel.Information, "-----Process exited-----");
        }
    }
}