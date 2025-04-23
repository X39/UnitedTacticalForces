using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Contract.GameServer;
using X39.Util;
using X39.Util.Collections;
using X39.Util.Threading;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController.Controllers;

/// <summary>
/// Represents the base class for managing Steam-based game server operations. This class
/// provides methods for controlling the lifecycle and configuration of a game server,
/// including starting, stopping, installing, upgrading, and updating workshop mods.
/// It is designed to support servers that utilize SteamCMD for server management.
/// </summary>
public abstract class SteamGameServerControllerBase : GameServerControllerBase
{
    /// <summary>
    /// Provides access to the service responsible for handling update streams,
    /// enabling updates to be streamed to connected clients or systems.
    /// </summary>
    public IUpdateStreamService UpdateStreamService { get; }

    /// <summary>
    /// Specifies the default file mode for Unix-based file systems,
    /// combining read, write, and execute permissions for the user,
    /// and read permissions for group and others.
    /// </summary>
    protected const UnixFileMode DefaultUnixFileMode = UnixFileMode.UserRead
                                                       | UnixFileMode.UserWrite
                                                       | UnixFileMode.UserExecute
                                                       | UnixFileMode.GroupRead
                                                       | UnixFileMode.OtherRead;

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
        IUpdateStreamService updateStreamService,
        ILogger logger
    )
        : base(gameServer, dbContextFactory, logger)
    {
        UpdateStreamService      = updateStreamService;
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
        await _semaphore.LockedAsync(async () =>
                {
                    await DoUpdateConfigurationAsync()
                        .ConfigureAwait(false);
                }
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task StartAsync(User? executingUser)
    {
        if (!CanStart)
            throw new InvalidOperationException("CanStart reports false");
        await _semaphore.LockedAsync(async () =>
                {
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync()
                        .ConfigureAwait(false);
                    var gameServer = await GetGameServerAsync(dbContext)
                        .ConfigureAwait(false);
                    Logger.LogInformation(
                        "User {UserId} initialized stopping of server {GameServer} ({GameServerPk})",
                        gameServer.Title,
                        gameServer.PrimaryKey,
                        executingUser?.PrimaryKey
                    );
                    Process? process = null;
                    ProcessStartInfo? processStartInfo = null;
                    try
                    {
                        gameServer.Status = ELifetimeStatus.Starting;
                        await dbContext.LifetimeEvents
                            .AddAsync(
                                new LifetimeEvent
                                {
                                    GameServerFk = gameServer.PrimaryKey,
                                    TimeStamp    = DateTimeOffset.Now,
                                    ExecutedByFk = executingUser?.PrimaryKey,
                                    Status       = ELifetimeStatus.Starting,
                                    GameServer   = default,
                                    PrimaryKey   = default,
                                    ExecutedBy   = default,
                                }
                            )
                            .ConfigureAwait(false);
                        await dbContext.SaveChangesAsync()
                            .ConfigureAwait(false);
                        processStartInfo = await GetProcessStartInfoAsync(dbContext, executingUser);
                        Logger.LogInformation(
                            "User {User} has requested starting of server {GameServer} ({GameServerPk}) with {LaunchArgs}",
                            gameServer.Title,
                            gameServer.PrimaryKey,
                            $"{processStartInfo.FileName} {string.Join(' ', processStartInfo.ArgumentList.Select(q => string.Concat('"', q, '"')))}",
                            executingUser?.PrimaryKey
                        );
                        process = new Process
                        {
                            StartInfo = processStartInfo, EnableRaisingEvents = true,
                        };
                        process.Exited             += ProcessOnExited;
                        process.ErrorDataReceived  += ProcessOnErrorDataReceived;
                        process.OutputDataReceived += ProcessOnOutputDataReceived;
                        var startResult = process.Start();
                        Process = process;
                        process.BeginErrorReadLine();
                        process.BeginOutputReadLine();
                        await OnStartAsync(executingUser, dbContext, process)
                            .ConfigureAwait(false);
                        await Task.Delay(TimeSpan.FromSeconds(1))
                            .ConfigureAwait(false);
                        if (startResult is false)
                            throw new FailedToStartProcessException(processStartInfo);
                        if (Process.HasExited)
                            throw new FailedToStartProcessException(processStartInfo);
                        gameServer.Status = ELifetimeStatus.Running;
                        await dbContext.LifetimeEvents
                            .AddAsync(
                                new LifetimeEvent
                                {
                                    GameServerFk = gameServer.PrimaryKey,
                                    TimeStamp    = DateTimeOffset.Now,
                                    ExecutedByFk = executingUser?.PrimaryKey,
                                    Status       = ELifetimeStatus.Running,
                                    GameServer   = default,
                                    PrimaryKey   = default,
                                    ExecutedBy   = default,
                                }
                            )
                            .ConfigureAwait(false);
                        await dbContext.SaveChangesAsync()
                            .ConfigureAwait(false);
                        await UpdateStreamService.SendUpdateAsync(
                                $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/lifetime-status",
                                new Contract.UpdateStream.GameServer.LifetimeStatusHasChanged
                                {
                                    LifetimeStatus = ELifetimeStatus.Running,
                                    GameServerId   = gameServer.PrimaryKey,
                                }
                            )
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
                            gameServer.Title,
                            gameServer.PrimaryKey,
                            processStartInfo?.ArgumentList,
                            executingUser?.PrimaryKey
                        );
                        Process           = null;
                        gameServer.Status = ELifetimeStatus.Stopped;
                        await dbContext.SaveChangesAsync()
                            .ConfigureAwait(false);
                        throw;
                    }
                }
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task StopAsync(User? executingUser)
    {
        if (!CanStop)
            throw new InvalidOperationException("CanStop reports false");
        await _semaphore.LockedAsync(async () =>
                {
                    await using var dbContext = await DbContextFactory.CreateDbContextAsync()
                        .ConfigureAwait(false);
                    var gameServer = await GetGameServerAsync(dbContext)
                        .ConfigureAwait(false);
                    dbContext.GameServers.Attach(gameServer);
                    Logger.LogInformation(
                        "User {UserId} initialized stopping of server {GameServer} ({GameServerPk})",
                        gameServer.Title,
                        gameServer.PrimaryKey,
                        executingUser?.PrimaryKey
                    );
                    gameServer.Status = ELifetimeStatus.Stopping;
                    await dbContext.LifetimeEvents
                        .AddAsync(
                            new LifetimeEvent
                            {
                                GameServerFk = gameServer.PrimaryKey,
                                TimeStamp    = DateTimeOffset.Now,
                                ExecutedByFk = executingUser?.PrimaryKey,
                                Status       = ELifetimeStatus.Stopping,
                                GameServer   = default,
                                PrimaryKey   = default,
                                ExecutedBy   = default,
                            }
                        )
                        .ConfigureAwait(false);
                    await dbContext.SaveChangesAsync()
                        .ConfigureAwait(false);
                    await UpdateStreamService.SendUpdateAsync(
                            $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/lifetime-status",
                            new Contract.UpdateStream.GameServer.LifetimeStatusHasChanged
                            {
                                LifetimeStatus = ELifetimeStatus.Stopping,
                                GameServerId   = gameServer.PrimaryKey,
                            }
                        )
                        .ConfigureAwait(false);
                    if (Process is null)
                        throw new NullReferenceException("_process was unexpectedly null");
                    try
                    {
                        await OnStopAsync(executingUser, dbContext)
                            .ConfigureAwait(false);
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
                            gameServer.Title,
                            gameServer.PrimaryKey,
                            executingUser?.PrimaryKey
                        );
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(
                            ex,
                            "Failed to gracefully stop the process of {GameServer} ({GameServerPk}) requested by user {UserId}",
                            gameServer.Title,
                            gameServer.PrimaryKey,
                            executingUser?.PrimaryKey
                        );
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

                    gameServer.Status = ELifetimeStatus.Stopped;
                    await dbContext.LifetimeEvents
                        .AddAsync(
                            new LifetimeEvent
                            {
                                GameServerFk = gameServer.PrimaryKey,
                                TimeStamp    = DateTimeOffset.Now,
                                ExecutedByFk = executingUser?.PrimaryKey,
                                Status       = ELifetimeStatus.Stopped,
                                GameServer   = default,
                                PrimaryKey   = default,
                                ExecutedBy   = default,
                            }
                        )
                        .ConfigureAwait(false);
                    await dbContext.SaveChangesAsync()
                        .ConfigureAwait(false);
                    await UpdateStreamService.SendUpdateAsync(
                            $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/lifetime-status",
                            new Contract.UpdateStream.GameServer.LifetimeStatusHasChanged
                            {
                                LifetimeStatus = ELifetimeStatus.Stopped,
                                GameServerId   = gameServer.PrimaryKey,
                            }
                        )
                        .ConfigureAwait(false);
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the path where the actual game server is installed.
    /// </summary>
    public string GameInstallPath
    {
        get
        {
            var installPath = Path.Combine(GameServerPath, "server-instance");
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
                GameServerPrimaryKey.ToString("00000000")
            );
            return installPath;
        }
    }

    /// <inheritdoc />
    public override async Task InstallOrUpgradeAsync(User? executingUser)
    {
        if (!CanInstallOrUpgrade)
            throw new InvalidOperationException("CanInstallOrUpgrade reports false");
        await _semaphore.LockedAsync(async () =>
                {
                    var (steamCmdPath, steamUsername, steamPassword, _) = GetSteamCmdInformationTuple();
                    var appId = ServerAppId;
                    var installPath = GameInstallPath;
                    await using (var dbContext = await DbContextFactory.CreateDbContextAsync()
                                     .ConfigureAwait(false))
                    {
                        var gameServer = await GetGameServerAsync(dbContext)
                            .ConfigureAwait(false);
                        var modPackDefinition = gameServer.SelectedModPackFk is not null
                            ? await dbContext.ModPackDefinitions
                                .Include(e => e.ModPackRevisions!.Where(q => q.IsActive))
                                .SingleOrDefaultAsync(q => q.PrimaryKey == gameServer.SelectedModPackFk)
                                .ConfigureAwait(false)
                            : null;
                        Logger.LogInformation(
                            "Changing ModPack of {GameServerTitle} ({GameServerPrimaryKey}) to {ModPackTitle} ({ModPackId})",
                            gameServer.Title,
                            gameServer.PrimaryKey,
                            modPackDefinition?.Title ?? "null",
                            modPackDefinition?.ModPackRevisions!.First()
                                .PrimaryKey
                        );
                        await dbContext.GameServerLogs
                            .AddAsync(
                                new GameServerLog
                                {
                                    GameServerFk = gameServer.PrimaryKey,
                                    TimeStamp    = DateTimeOffset.Now,
                                    Message = "Changing ModPack of {0} ({1}) to {2} ({3})".Format(
                                        gameServer.Title,
                                        gameServer.PrimaryKey,
                                        modPackDefinition?.Title ?? "null",
                                        modPackDefinition?.ModPackRevisions!.First()
                                            .PrimaryKey
                                        ?? default
                                    ),
                                    Source   = "Server-Upgrade",
                                    LogLevel = LogLevel.Information,
                                }
                            )
                            .ConfigureAwait(false);
                        gameServer.ActiveModPack = modPackDefinition?.IsComposition is true
                            ? null
                            : modPackDefinition?.ModPackRevisions!.First();
                        gameServer.ActiveModPackFk = modPackDefinition?.IsComposition is true
                            ? null
                            : modPackDefinition?.ModPackRevisions!.First()
                                .PrimaryKey;
                        gameServer.Status = ELifetimeStatus.Updating;
                        await dbContext.SaveChangesAsync()
                            .ConfigureAwait(false);
                        await UpdateStreamService.SendUpdateAsync(
                                $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/lifetime-status",
                                new Contract.UpdateStream.GameServer.ModPackChanged()
                                {
                                    GameServerId      = gameServer.PrimaryKey,
                                    ActiveModPackId   = gameServer.ActiveModPackFk,
                                    SelectedModPackId = gameServer.SelectedModPackFk,
                                }
                            )
                            .ConfigureAwait(false);
                        await UpdateStreamService.SendUpdateAsync(
                                $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/lifetime-status",
                                new Contract.UpdateStream.GameServer.LifetimeStatusHasChanged
                                {
                                    LifetimeStatus = ELifetimeStatus.Updating,
                                    GameServerId   = gameServer.PrimaryKey,
                                }
                            )
                            .ConfigureAwait(false);
                    }

                    await DoUpdateGameAsync(
                            steamCmdPath,
                            installPath,
                            steamUsername,
                            steamPassword,
                            appId,
                            executingUser
                        )
                        .ConfigureAwait(false);
                    await OnInstallOrUpgradeAsync(executingUser)
                        .ConfigureAwait(false);
                    await using (var dbContext = await DbContextFactory.CreateDbContextAsync()
                                     .ConfigureAwait(false))
                    {
                        var gameServer = await GetGameServerAsync(dbContext)
                            .ConfigureAwait(false);
                        gameServer.Status            = ELifetimeStatus.Stopped;
                        gameServer.TimeStampUpgraded = DateTimeOffset.Now;
                        await dbContext.SaveChangesAsync()
                            .ConfigureAwait(false);
                        await UpdateStreamService.SendUpdateAsync(
                                $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/lifetime-status",
                                new Contract.UpdateStream.GameServer.LifetimeStatusHasChanged
                                {
                                    LifetimeStatus = ELifetimeStatus.Stopped,
                                    GameServerId   = gameServer.PrimaryKey,
                                }
                            )
                            .ConfigureAwait(false);
                    }
                }
            )
            .ConfigureAwait(false);
    }

    private (string steamCmdPath, string steamUsername, string steamPassword, string gameInstallBasePath )
        GetSteamCmdInformationTuple()
    {
        var steamCmdPath = GetSteamCmdPath();
        var steamUsername = RequireLogin ? GetSteamUsername() : string.Empty;
        var steamPassword = RequireLogin ? GetSteamPassword() : string.Empty;
        var gameInstallBasePath = GetInstallBasePath();
        return (steamCmdPath, steamUsername, steamPassword, gameInstallBasePath);
    }

    private (string depotDownloaderPath, string steamUsername, string steamPassword, string gameInstallBasePath )
        GetDepotDownloaderInformationTuple()
    {
        var steamCmdPath = GetDepotDownloaderPathPath();
        var steamUsername = RequireLogin ? GetSteamUsername() : string.Empty;
        var steamPassword = RequireLogin ? GetSteamPassword() : string.Empty;
        var gameInstallBasePath = GetInstallBasePath();
        return (steamCmdPath, steamUsername, steamPassword, gameInstallBasePath);
    }

    /// <summary>
    /// Represents operational instructions for updating a game through SteamCmd.
    /// </summary>
    /// <param name="psi">
    /// The <see cref="ProcessStartInfo"/> used to define the process parameters for SteamCmd execution.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected virtual Task SteamCmdGameUpdateInstructions(ProcessStartInfo psi) => Task.CompletedTask;

    private async Task DoUpdateGameAsync(
        string steamCmdPath,
        string installPath,
        string steamUsername,
        string steamPassword,
        long appId,
        User? executingUser
    )
    {
        Logger.LogInformation(
            "Starting update of {GameServer} ({GameServerPk}) via SteamCmd, requested by user {UserId}",
            GameServerLastKnownTitle,
            GameServerPrimaryKey,
            executingUser?.PrimaryKey
        );
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
        await SteamCmdGameUpdateInstructions(psi);

        psi.ArgumentList.Add("+quit");
        if (Process is not null && !Process.HasExited)
            throw new InvalidOperationException("Process must be finished for the operation");
        Logger.LogDebug(
            "Starting steamcmd to update {GameServer} ({GameServerPk}), requested by user {UserId}, using {Arguments}",
            GameServerLastKnownTitle,
            GameServerPrimaryKey,
            executingUser?.PrimaryKey,
            psi.ArgumentList.AsEnumerable()
        );
        using var tmp = new Process();
        tmp.StartInfo = psi;
        Process       = tmp;
        using var disposable = new Disposable(() => Process = null);
        await StartAndWaitForSteamCmdExitAndLogAsync(tmp);
        if (tmp.ExitCode is not 0)
            throw new Exception("SteamCmd operation failed.");
        Logger.LogInformation(
            "Finished update of {GameServer} ({GameServerPk}) via SteamCmd, requested by user {UserId}",
            GameServerLastKnownTitle,
            GameServerPrimaryKey,
            executingUser?.PrimaryKey
        );
    }

    private async Task SteamCmdLogAsync(LogLevel logLevel, string message, DateTimeOffset timeStamp)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync()
            .ConfigureAwait(false);
        var gameServer = await GetGameServerAsync(dbContext)
            .ConfigureAwait(false);
        Logger.Log(
            logLevel,
            "[{TimeStamp}] SteamCmd for {GameServer} ({GameServerId}): {Message}",
            timeStamp,
            gameServer.Title,
            gameServer.PrimaryKey,
            message
        );
        dbContext.GameServerLogs.Add(
            new GameServerLog
            {
                Message      = message,
                TimeStamp    = timeStamp,
                LogLevel     = logLevel,
                Source       = "SteamCmd",
                GameServerFk = gameServer.PrimaryKey,
            }
        );
        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
        await UpdateStreamService.SendUpdateAsync(
                $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/log",
                new Contract.UpdateStream.GameServer.LogMessage
                {
                    Message      = message,
                    TimeStamp    = timeStamp,
                    LogLevel     = logLevel,
                    Source       = "SteamCmd",
                    GameServerId = gameServer.PrimaryKey,
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the given workshop items identified via <paramref name="workshopIds"/>. 
    /// </summary>
    /// <remarks>
    /// Will produce <see cref="Contract.UpdateStream.GameServer.LifetimeStatusHasChanged"/> packages in accordance
    /// to <paramref name="additionalUpdateSteps"/>.
    /// Progress is calculated as follows:
    /// <code>currentWorkshopId / (workshopIds +additionalUpdateSteps)</code> 
    /// </remarks>
    /// <param name="workshopIds">The workshop ids.</param>
    /// <param name="installPath">The path to download the mods to (individual pathing is not possible, using steamcmd sadly)</param>
    /// <param name="executingUser">The user that requested the update.</param>
    /// <param name="additionalUpdateSteps">Additional update steps to be done. Used to change how the update is moving.</param>
    /// <exception cref="FailedToStartProcessException">Thrown when the SteamCmd process failed to start.</exception>
    /// <returns>The full path to the workshop items.</returns>
    protected async Task<IReadOnlyCollection<(long workshopId, string installPath)>> DoUpdateWorkshopMods(
        IReadOnlyCollection<long> workshopIds,
        string installPath,
        User? executingUser,
        int additionalUpdateSteps = 0
    )
    {
        var installPaths = new List<(long, string)>();
        foreach (var (workshopId, index) in workshopIds.Indexed())
        {
            var (depotDownloaderPath, steamUsername, steamPassword, _) = GetDepotDownloaderInformationTuple();
            Logger.LogInformation(
                "Starting update of workshop item {WorkshopId} via DebotDownloader, requested by user {UserId}",
                workshopId,
                executingUser?.PrimaryKey
            );
            var psi = new ProcessStartInfo
            {
                FileName               = depotDownloaderPath,
                UseShellExecute        = false,
                RedirectStandardError  = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding  = Encoding.ASCII,
                StandardOutputEncoding = Encoding.ASCII,
                WorkingDirectory       = Path.GetDirectoryName(depotDownloaderPath),
            };
            if (RequireLogin)
            {
                psi.ArgumentList.Add("-username");
                psi.ArgumentList.Add(steamUsername);
                psi.ArgumentList.Add("-password");
                psi.ArgumentList.Add(steamPassword);
                psi.ArgumentList.Add("-remember-password");
            }

            psi.ArgumentList.Add("-app");
            psi.ArgumentList.Add(GameAppId.ToString());
            psi.ArgumentList.Add("-pubfile");
            psi.ArgumentList.Add(workshopId.ToString());
            psi.ArgumentList.Add("-dir");
            installPaths.Add(
                (workshopId,
                    Path.Combine(
                        installPath,
                        "steamapps",
                        "workshop",
                        "content",
                        GameAppId.ToString(),
                        workshopId.ToString()
                    ))
            );

            if (Process is not null && !Process.HasExited)
                throw new InvalidOperationException("Process must be finished for the operation");

            await ExecuteAsync(psi)
                .ConfigureAwait(false);
            await UpdateStreamService.SendUpdateAsync(
                    $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/lifetime-status",
                    new Contract.UpdateStream.GameServer.LifetimeStatusHasChanged
                    {
                        LifetimeStatus = ELifetimeStatus.Updating,
                        GameServerId   = GameServerPrimaryKey,
                        Progress       = index / (double) (workshopIds.Count + additionalUpdateSteps),
                    }
                )
                .ConfigureAwait(false);

            Logger.LogInformation(
                "Finished update of workshop item {WorkshopId} via DebotDownloader, requested by user {UserId}",
                workshopId,
                executingUser?.PrimaryKey
            );
        }

        return installPaths.AsReadOnly();
    }

    private async Task ExecuteAsync(ProcessStartInfo psi, int attempts = 3)
    {
        for (var i = 1; i <= attempts; i++)
        {
            try
            {
                using var tmp = new Process();
                tmp.StartInfo = psi;
                Process       = tmp;
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
            _ = SteamCmdLogAsync(LogLevel.Error, message, DateTimeOffset.Now)
                .ConfigureAwait(false);
        };
        tmp.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not { } message)
                return;
            _ = SteamCmdLogAsync(LogLevel.Information, message, DateTimeOffset.Now)
                .ConfigureAwait(false);
        };
        #if DEBUG
        Logger.LogTrace(
            "Starting SteamCmd with {Command}",
            $"{tmp.StartInfo.FileName} {string.Join(' ', tmp.StartInfo.ArgumentList)}"
        );
        await SteamCmdLogAsync(
            LogLevel.Trace,
            $"{tmp.StartInfo.FileName} {string.Join(' ', tmp.StartInfo.ArgumentList)}",
            DateTimeOffset.Now
        );
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
                $"{Constants.Configuration.Steam.Username} in appsettings.json is null. Leave empty if only anonymous is required."
            );
        return username;
    }

    private string GetSteamPassword()
    {
        var password = _configuration[Constants.Configuration.Steam.Password];
        if (password is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.Password} in appsettings.json is null. Leave empty if only anonymous is required."
            );
        return password;
    }

    private string GetSteamCmdPath()
    {
        var steamCmdPath = _configuration[Constants.Configuration.Steam.SteamCmdPath];
        if (steamCmdPath is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.SteamCmdPath} in appsettings.json is null"
            );
        steamCmdPath = Path.GetFullPath(steamCmdPath);
        if (!Path.Exists(steamCmdPath))
            throw new FileNotFoundException($"SteamCmd was not found at {steamCmdPath}", steamCmdPath);
        return steamCmdPath;
    }

    private string GetDepotDownloaderPathPath()
    {
        var depotDownloaderPathPath = _configuration[Constants.Configuration.Steam.DepotDownloaderPath];
        if (depotDownloaderPathPath is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.DepotDownloaderPath} in appsettings.json is null"
            );
        depotDownloaderPathPath = Path.GetFullPath(depotDownloaderPathPath);
        if (!Path.Exists(depotDownloaderPathPath))
            throw new FileNotFoundException(
                $"DepotDownloaderPath was not found at {depotDownloaderPathPath}",
                depotDownloaderPathPath
            );
        return depotDownloaderPathPath;
    }

    private string GetInstallBasePath()
    {
        var installBasePath = _configuration[Constants.Configuration.Steam.InstallBasePath];
        if (installBasePath is null)
            throw new ConfigurationException(
                $"{Constants.Configuration.Steam.InstallBasePath} in appsettings.json is null"
            );
        installBasePath = Path.GetFullPath(installBasePath);

        CreateDirectory(installBasePath);
        return installBasePath;
    }

    /// <inheritdoc cref="UpdateConfigurationAsync"/>
    protected abstract Task DoUpdateConfigurationAsync();

    /// <inheritdoc cref="StartAsync"/>
    protected virtual Task OnStartAsync(User? executingUser, ApiDbContext apiDbContext, Process process)
        => Task.CompletedTask;

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
        User? executingUser
    );

    /// <summary>
    /// Method being called when the game server produced output on the console.
    /// </summary>
    protected virtual void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not { } message)
            return;
        BeginWritingServerLog(LogLevel.Information, message);
    }

    /// <summary>
    /// Method being called when the game server produced error output on the console.
    /// </summary>
    protected virtual void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not { } message)
            return;
        BeginWritingServerLog(LogLevel.Error, message);
    }


    /// <summary>
    /// Writes a message to the server log with the specified log level.
    /// </summary>
    /// <param name="logLevel">The level of the log message.</param>
    /// <param name="message">The message to be written to the server log.</param>
    protected void BeginWritingServerLog(LogLevel logLevel, string message)
    {
        var now = DateTimeOffset.Now;
        Task.Run(
            async () =>
            {
                Logger.LogDebug(
                    "{GameServer} ({GameServerPk}): [{LogLevel}] {Message}",
                    GameServerLastKnownTitle,
                    GameServerPrimaryKey,
                    logLevel,
                    message
                );
                const int maxAttempts = 3;
                for (var i = 1; i < maxAttempts; i++)
                {
                    try
                    {
                        await using var dbContext = await DbContextFactory
                            .CreateDbContextAsync(_cancellationTokenSource.Token)
                            .ConfigureAwait(false);
                        await dbContext.GameServerLogs
                            .AddAsync(
                                new GameServerLog
                                {
                                    TimeStamp    = now,
                                    LogLevel     = logLevel,
                                    Message      = message,
                                    Source       = $"[{GameServerPrimaryKey}] {GameServerLastKnownTitle}",
                                    GameServerFk = GameServerPrimaryKey,
                                },
                                _cancellationTokenSource.Token
                            )
                            .ConfigureAwait(false);
                        await dbContext.SaveChangesAsync()
                            .ConfigureAwait(false);
                        await UpdateStreamService.SendUpdateAsync(
                                $"{Constants.Routes.GameServers}/{GameServerPrimaryKey}/log",
                                new Contract.UpdateStream.GameServer.LogMessage
                                {
                                    TimeStamp    = now,
                                    LogLevel     = logLevel,
                                    Message      = message,
                                    Source       = $"[{GameServerPrimaryKey}] {GameServerLastKnownTitle}",
                                    GameServerId = GameServerPrimaryKey,
                                }
                            )
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
                            GameServerLastKnownTitle,
                            GameServerPrimaryKey,
                            logLevel,
                            message
                        );
                    }
                }
            },
            _cancellationTokenSource.Token
        );
    }

    private void ProcessOnExited(object? sender, EventArgs e)
    {
        using (var dbContext = DbContextFactory.CreateDbContext())
        {
            var gameServer = dbContext.GameServers.Single(q => q.PrimaryKey == GameServerPrimaryKey);
            gameServer.Status = ELifetimeStatus.Stopped;
            dbContext.SaveChanges();
        }

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
                $"-----Process exited with {exitCode:X} at {exitTime}-----"
            );
        }
        else
        {
            BeginWritingServerLog(LogLevel.Information, "-----Process exited-----");
        }
    }
}