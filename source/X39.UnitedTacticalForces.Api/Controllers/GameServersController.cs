using System.Globalization;
using System.Net;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.GarbageWorkarounds;
using X39.UnitedTacticalForces.Api.Services.GameServerController;
using X39.UnitedTacticalForces.Common;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route(Constants.Routes.GameServers)]
[Authorize(Roles = Roles.Admin + "," + Roles.ServerAccess)]
public class GameServersController : ControllerBase
{
    /// <summary>
    /// Class containing a virtual <see cref="GameServer"/> and corresponding information about
    /// the physical instance of it.
    /// </summary>
    /// <param name="GameServer">The virtual server.</param>
    /// <param name="IsRunning">Whether the server is running or not.</param>
    /// <param name="CanStart">Whether the server can be started or not.</param>
    /// <param name="CanStop">Whether the server can be stopped or not.</param>
    /// <param name="CanUpdateConfiguration">Whether the server can change the configuration or not.</param>
    /// <param name="CanUpgrade">Whether the server can be upgraded or not.</param>
    [PublicAPI]
    public record GameServerInfo(
        GameServer GameServer,
        bool IsRunning,
        bool CanStart,
        bool CanStop,
        bool CanUpdateConfiguration,
        bool CanUpgrade)
    {
        /// <summary>
        /// Class containing a virtual <see cref="GameServer"/> and corresponding information about
        /// the physical instance of it.
        /// </summary>
        /// <param name="gameServer">The virtual server.</param>
        /// <param name="controller">The <see cref="IGameServerController"/>.</param>
        public GameServerInfo(GameServer gameServer, IGameServerController controller) : this(
            gameServer,
            controller.IsRunning,
            controller.CanStart,
            controller.CanStop,
            controller.CanUpdateConfiguration,
            controller.CanInstallOrUpgrade)
        {
        }
    }

    private readonly ILogger<GameServersController> _logger;
    private readonly ApiDbContext                   _apiDbContext;
    private readonly IGameServerControllerFactory   _gameServerControllerFactory;

    /// <inheritdoc />
    public GameServersController(
        ILogger<GameServersController> logger,
        ApiDbContext apiDbContext,
        IGameServerControllerFactory gameServerControllerFactory)
    {
        _logger                      = logger;
        _apiDbContext                = apiDbContext;
        _gameServerControllerFactory = gameServerControllerFactory;
    }

    /// <summary>
    /// Returns the amount of available <see cref="GameServer"/>'s.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [ProducesResponseType(typeof(int), (int) HttpStatusCode.OK)]
    [HttpGet("all/count", Name = nameof(GetGameServerCountAsync))]
    public async Task<ActionResult<int>> GetGameServerCountAsync(
        CancellationToken cancellationToken)
    {
        var count = await _apiDbContext.GameServers.CountAsync(cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// Returns all available <see cref="GameServer"/>'s and their state.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [ProducesResponseType(typeof(IEnumerable<GameServerInfo>), (int) HttpStatusCode.OK)]
    [HttpGet("all", Name = nameof(GetGameServersAsync))]
    public async Task<ActionResult<IEnumerable<GameServerInfo>>> GetGameServersAsync(
        CancellationToken cancellationToken)
    {
        var allServers = await _apiDbContext.GameServers
            .OrderBy((q) => q.Title)
            .ToArrayAsync(cancellationToken);
        var gameServerInfos = new List<GameServerInfo>();
        foreach (var gameServer in allServers)
        {
            var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
            gameServerInfos.Add(
                new GameServerInfo(
                    gameServer,
                    controller.IsRunning,
                    controller.CanStart,
                    controller.CanStop,
                    controller.CanUpdateConfiguration,
                    controller.CanInstallOrUpgrade));
        }

        return Ok(gameServerInfos);
    }

    /// <summary>
    /// Returns a single <see cref="GameServer"/> and it's state.
    /// </summary>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [ProducesResponseType(typeof(GameServerInfo), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpGet("{gameServerId:long}", Name = nameof(GetGameServerAsync))]
    public async Task<ActionResult<GameServerInfo>> GetGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();

        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        return Ok(new GameServerInfo(gameServer, controller));
    }

    /// <summary>
    ///     Updates properties of a single game server.
    ///     The following properties can be changed:
    ///     <list type="bullet">
    ///         <item><see cref="GameServer.Title"/></item>
    ///         <item><see cref="GameServer.SelectedModPack"/></item>
    ///     </list>
    /// </summary>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    /// <param name="updatedGameServer">The payload with the updated game server.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpPut("{gameServerId:long}/update", Name = nameof(UpdateGameServerAsync))]
    public async Task<ActionResult<GameServerInfo>> UpdateGameServerAsync(
        [FromRoute] long gameServerId,
        [FromBody] GameServer updatedGameServer,
        CancellationToken cancellationToken)
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        gameServer.Title             = updatedGameServer.Title;
        gameServer.SelectedModPackFk = updatedGameServer.SelectedModPackFk;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Returns a list of all available game server controllers.
    /// </summary>
    [ProducesResponseType(typeof(IEnumerable<string>), (int) HttpStatusCode.OK)]
    [HttpGet("all/controllers", Name = nameof(GetGameServerControllers))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerCreateOrDelete)]
    public IEnumerable<string> GetGameServerControllers()
    {
        return _gameServerControllerFactory.GetGameControllers();
    }

    /// <summary>
    /// Starts the given <see cref="GameServer"/> if it can be started.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [ProducesResponseType(typeof(GameServerInfo), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [HttpPost("{gameServerId:long}/start", Name = nameof(StartGameServerAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerStartStop)]
    public async Task<ActionResult> StartGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanStart)
            return Forbid();
        _ = Task.Run(
                async () =>
                {
                    _logger.LogDebug(
                        "Starting game server {GameServerTitle} ({GameServerPk})",
                        gameServer.Title,
                        gameServer.PrimaryKey);
                    try
                    {
                        await controller.StartAsync(user)
                            .ConfigureAwait(false);
                        _logger.LogDebug(
                            "Start completed without exception for {GameServerTitle} ({GameServerPk})",
                            gameServer.Title,
                            gameServer.PrimaryKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Exception while starting {GameServerTitle} ({GameServerPk})",
                            gameServer.Title,
                            gameServer.PrimaryKey);
                    }
                })
            .ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        return Ok(new GameServerInfo(gameServer, controller));
    }

    /// <summary>
    /// Starts the given <see cref="GameServer"/> if it can be stopped.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to stop.</param>
    [ProducesResponseType(typeof(GameServerInfo), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [HttpPost("{gameServerId:long}/stop", Name = nameof(StopGameServerAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerStartStop)]
    public async Task<ActionResult> StopGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanStop)
            return Forbid();
        _ = ExecuteStopGameServerAsync(gameServer, controller, user).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        return Ok(new GameServerInfo(gameServer, controller));
    }

    private async Task ExecuteUpgradeGameServerAsync(GameServer gameServer, IGameServerController controller, User user)
    {
        _logger.LogDebug(
            "Upgrading game server {GameServerTitle} ({GameServerPk})",
            gameServer.Title,
            gameServer.PrimaryKey);
        try
        {
            if (controller.IsRunning)
                await ExecuteStopGameServerAsync(gameServer, controller, user)
                    .ConfigureAwait(false);

            await ExecuteUpdateConfigurationAsync(gameServer, controller, user)
                .ConfigureAwait(false);
            await ExecuteInstallOrUpgradeAsync(gameServer, controller, user)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while stopping {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey);
        }
    }

    private async Task ExecuteStopGameServerAsync(GameServer gameServer, IGameServerController controller, User user)
    {
        _logger.LogDebug(
            "Stopping game server {GameServerTitle} ({GameServerPk})",
            gameServer.Title,
            gameServer.PrimaryKey);
        try
        {
            await controller.StopAsync(user)
                .ConfigureAwait(false);
            _logger.LogDebug(
                "Stop completed without exception for {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while stopping {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey);
            throw;
        }
    }

    private async Task ExecuteInstallOrUpgradeAsync(GameServer gameServer, IGameServerController controller, User user)
    {
        _logger.LogDebug(
            "Upgrading (or installing) game server {GameServerTitle} ({GameServerPk})",
            gameServer.Title,
            gameServer.PrimaryKey);
        try
        {
            await controller.InstallOrUpgradeAsync(user)
                .ConfigureAwait(false);
            _logger.LogDebug(
                "Upgrade (or install) completed without exception for {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while upgrading (or installing) {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey);
            throw;
        }
    }

    private async Task ExecuteUpdateConfigurationAsync(
        GameServer gameServer,
        IGameServerController controller,
        User user)
    {
        _logger.LogDebug(
            "Updating configuration of game server {GameServerTitle} ({GameServerPk})",
            gameServer.Title,
            gameServer.PrimaryKey);
        try
        {
            await controller.UpdateConfigurationAsync()
                .ConfigureAwait(false);
            _logger.LogDebug(
                "Updating configuration completed without exception for {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while updating configuration of {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey);
            throw;
        }
    }

    /// <summary>
    /// Starts the given <see cref="GameServer"/> if it can be upgraded.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to upgrade.</param>
    [ProducesResponseType(typeof(GameServerInfo), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [HttpPost("{gameServerId:long}/upgrade", Name = nameof(UpgradeGameServerAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerUpgrade)]
    public async Task<ActionResult<GameServerInfo>> UpgradeGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanInstallOrUpgrade || !controller.CanUpdateConfiguration ||
            controller is {IsRunning: true, CanStop: false})
            return Forbid(); // Forbid because not possible pre-applying, preventing work.
        _ = ExecuteUpgradeGameServerAsync(gameServer, controller, user).ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        return Ok(new GameServerInfo(gameServer, controller));
    }

    /// <summary>
    /// Return the configuration of the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [ProducesResponseType(typeof(IEnumerable<ConfigurationEntry>), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpGet("{gameServerId:long}/configuration", Name = nameof(GetConfigurationAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerUpdate)]
    public async Task<ActionResult<IEnumerable<ConfigurationEntry>>> GetConfigurationAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        if (!await _apiDbContext.GameServers.AnyAsync((q) => q.PrimaryKey == gameServerId, cancellationToken))
            return NotFound();
        var configurationEntries = await _apiDbContext.ConfigurationEntries
            .Where((q) => q.GameServerFk == gameServerId)
            .Where((q) => q.IsActive)
            .ToArrayAsync(cancellationToken);
        foreach (var configurationEntry in configurationEntries.Where((q) => q.IsSensitive))
        {
            configurationEntry.Value = Constants.PasswordReplacement;
        }

        return Ok(configurationEntries);
    }

    /// <summary>
    /// Returns the folders of the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to receive the folders from.</param>
    [ProducesResponseType(typeof(IEnumerable<GameFolder>), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpGet("{gameServerId:long}/folders", Name = nameof(GetGameFoldersAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerFiles)]
    public async Task<ActionResult<IEnumerable<GameFolder>>> GetGameFoldersAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        var folders = await controller.GetGameFoldersAsync(Request.GetCultureInfo(), cancellationToken);
        return Ok(folders);
    }

    /// <summary>
    /// Returns the folders of the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="folder">Identifier of the folder to receive.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to receive the files from.</param>
    [ProducesResponseType(typeof(IEnumerable<GameFileInfo>), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpGet("{gameServerId:long}/{folder}/files", Name = nameof(GetGameFolderFilesAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerFiles)]
    public async Task<ActionResult<IEnumerable<GameFileInfo>>> GetGameFolderFilesAsync(
        [FromRoute] long gameServerId,
        [FromRoute] string folder,
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        var folders = await controller.GetGameFoldersAsync(Request.GetCultureInfo(), cancellationToken);
        var gameFolder = folders.FirstOrDefault((q) => q.Identifier == folder);
        if (gameFolder is null)
            return NotFound();
        var files = await controller.GetGameFolderFilesAsync(gameFolder, Request.GetCultureInfo(), cancellationToken);
        return Ok(files);
    }

    /// <summary>
    /// Creates or updates a file in a folder of the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="folder">Identifier of the folder to change a file in.</param>
    /// <param name="bullshitWrapper">A wrapper containing the data of the file to upload</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to change the file from.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    [HttpPost("{gameServerId:long}/{folder}/upload", Name = nameof(UploadGameFolderFileAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerFiles)]
    public async Task<ActionResult> UploadGameFolderFileAsync(
        [FromRoute] long gameServerId,
        [FromRoute] string folder,
        [FromForm] FuckYouNSwagAspNetCoreAndTheBloodyShawbuckleFile bullshitWrapper,
        CancellationToken cancellationToken)
    {
        var file = bullshitWrapper.File;
        if (file.FileName.Contains('/') || file.FileName.Contains('\\') || file.FileName is ".." or ".")
            return BadRequest();
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanModifyGameFiles)
            return Forbid(); // Forbid because not possible, preventing work.
        var folders = await controller.GetGameFoldersAsync(Request.GetCultureInfo(), cancellationToken);
        var gameFolder = folders.FirstOrDefault((q) => q.Identifier == folder);
        if (gameFolder is null)
            return NotFound();
        await controller.UploadFileAsync(
            gameFolder,
            new GameFileInfo(file.FileName, file.Length, file.ContentType),
            file.OpenReadStream());
        return NoContent();
    }

    /// <summary>
    /// Deletes a file from a folder the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="folder">Identifier of the folder to delete a file from.</param>
    /// <param name="file">Name of the file to delete.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to receive the folders from.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpPost("{gameServerId:long}/{folder}/{file}/delete", Name = nameof(DeleteGameFolderFileAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerFiles)]
    public async Task<ActionResult> DeleteGameFolderFileAsync(
        [FromRoute] long gameServerId,
        [FromRoute] string folder,
        [FromRoute] string file,
        CancellationToken cancellationToken)
    {
        if (file.Contains('/') || file.Contains('\\') || file is ".." or ".")
            return BadRequest();
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanModifyGameFiles)
            return Forbid(); // Forbid because not possible, preventing work.
        var folders = await controller.GetGameFoldersAsync(Request.GetCultureInfo(), cancellationToken);
        var gameFolder = folders.FirstOrDefault((q) => q.Identifier == folder);
        if (gameFolder is null)
            return NotFound();
        await controller.DeleteFileAsync(
            gameFolder,
            new GameFileInfo(file, 0, string.Empty));
        return NoContent();
    }

    /// <summary>
    /// Receive a file from a folder the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="folder">Identifier of the folder to delete a file from.</param>
    /// <param name="file">Name of the file to delete.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to receive the folders from.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpPost("{gameServerId:long}/{folder}/{file}/get", Name = nameof(GetGameFolderFileAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerFiles)]
    public async Task<ActionResult<Stream>> GetGameFolderFileAsync(
        [FromRoute] long gameServerId,
        [FromRoute] string folder,
        [FromRoute] string file,
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanModifyGameFiles)
            return Forbid(); // Forbid because not possible, preventing work.
        var folders = await controller.GetGameFoldersAsync(Request.GetCultureInfo(), cancellationToken);
        var gameFolder = folders.FirstOrDefault((q) => q.Identifier == folder);
        if (gameFolder is null)
            return NotFound();
        var fileInfos = await controller.GetGameFolderFilesAsync(
            gameFolder,
            Request.GetCultureInfo(),
            cancellationToken);
        var fileInfo = fileInfos.FirstOrDefault((q) => q.Name == file);
        if (fileInfo is null)
            return NotFound();
        Response.Headers.Add(
            "Content-Disposition",
            new System.Net.Mime.ContentDisposition
            {
                FileName = fileInfo.Name,
                Inline   = false,
            }.ToString());
        var stream = await controller.GetGameFolderFileAsync(
            gameFolder,
            new GameFileInfo(file, 0, string.Empty),
            cancellationToken);
        return new FileStreamResult(stream, fileInfo.MimeType) {FileDownloadName = fileInfo.Name};
    }

    /// <summary>
    /// Return the count of logs for the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="referenceTimeStamp">
    ///     Timestamp to allow consistent results.
    ///     If provided, this will prevent logs newer then the timestamp to appear in the result.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [ProducesResponseType(typeof(long), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpGet("{gameServerId:long}/logs/count", Name = nameof(GetLogsCountAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerLogs)]
    public async Task<ActionResult<long>> GetLogsCountAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? referenceTimeStamp = null)
    {
        if (!await _apiDbContext.GameServers.AnyAsync((q) => q.PrimaryKey == gameServerId, cancellationToken))
            return NotFound();
        var query = _apiDbContext.GameServerLogs
            .Where((q) => q.GameServerFk == gameServerId);
        if (referenceTimeStamp is not null)
            query = query.Where((q) => q.TimeStamp >= referenceTimeStamp);
        var count = await query.LongCountAsync(cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// Return the logs of the given <see cref="GameServer"/>.
    /// </summary>
    /// <remarks>
    /// Logs are ordered by <see cref="GameServerLog.TimeStamp"/>.
    /// </remarks>
    /// <param name="referenceTimeStamp">
    ///     Timestamp to allow consistent results.
    ///     If provided, this will prevent logs newer then the timestamp to appear in the result
    ///     and consideration with <paramref cref="skip"/> and <paramref cref="take"/>.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="take">The amount of logs to receive</param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    /// <param name="skip">The amount of log entries to skip</param>
    /// <param name="descendingByTimeStamp">If true, order of returned logs will be descending (newest one first).</param>
    [ProducesResponseType(typeof(IEnumerable<GameServerLog>), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpGet("{gameServerId:long}/logs", Name = nameof(GetLogsAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerLogs)]
    public async Task<ActionResult<IEnumerable<GameServerLog>>> GetLogsAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? referenceTimeStamp = null,
        [FromQuery] bool descendingByTimeStamp = false)
    {
        if (!await _apiDbContext.GameServers.AnyAsync((q) => q.PrimaryKey == gameServerId, cancellationToken))
            return NotFound();
        var query = _apiDbContext.GameServerLogs
            .Where((q) => q.GameServerFk == gameServerId);
        if (referenceTimeStamp is not null)
            query = query.Where((q) => q.TimeStamp >= referenceTimeStamp);
        query = descendingByTimeStamp
            ? query.OrderByDescending((q) => q.TimeStamp)
            : query.OrderBy((q) => q.TimeStamp);
        var configurationEntries = await query
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return Ok(configurationEntries);
    }

    /// <summary>
    /// Downloads the logs of the given <see cref="GameServer"/>.
    /// </summary>
    /// <remarks>
    /// Logs are ordered by <see cref="GameServerLog.TimeStamp"/>.
    /// </remarks>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to download the logs of.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(FileStreamResult), (int) HttpStatusCode.OK)]
    [HttpGet("{gameServerId:long}/logs/download", Name = nameof(DownloadLogsAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerLogs)]
    public async Task DownloadLogsAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            throw new UnauthorizedAccessException();
        var gameServer = await _apiDbContext.GameServers
            .SingleAsync((q) => q.PrimaryKey == gameServerId, cancellationToken)
            .ConfigureAwait(false);
        var logs = _apiDbContext.GameServerLogs
            .Where((q) => q.GameServerFk == gameServerId)
            .OrderBy((q) => q.TimeStamp)
            .AsAsyncEnumerable();
        Response.Headers.Add(
            "Content-Disposition",
            new System.Net.Mime.ContentDisposition
            {
                FileName = $"{gameServer.Title}.log.txt",
                Inline   = false,
            }.ToString());
        await using var stream = new StreamWriter(Response.Body);
        await foreach (var log in logs
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            await stream.WriteLineAsync($"{log.TimeStamp:O} {log.Source} {log.Message}");
        }
    }

    /// <summary>
    /// Clears all logs of the <see cref="GameServer"/> given.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to clear the logs of.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [HttpPost("{gameServerId:long}/logs/clear", Name = nameof(ClearLogsAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerLogsClear)]
    public async Task<ActionResult> ClearLogsAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            throw new UnauthorizedAccessException();
        if (!await _apiDbContext.GameServers
                .AnyAsync((q) => q.PrimaryKey == gameServerId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();
        await _apiDbContext.GameServerLogs
            .Where((q) => q.GameServerFk == gameServerId)
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Sets the configuration of the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="configurationEntries">The <see cref="ConfigurationEntry"/>'s to set.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [HttpPost("{gameServerId:long}/configuration", Name = nameof(SetConfigurationAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerUpdate)]
    public async Task<ActionResult<IEnumerable<ConfigurationEntry>>> SetConfigurationAsync(
        [FromRoute] long gameServerId,
        [FromBody] List<ConfigurationEntry> configurationEntries,
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanUpdateConfiguration || controller is {IsRunning: true, CanStop: false})
            return Forbid(); // Forbid because not possible pre-applying, preventing work.
        if (controller.IsRunning)
            if (User.IsInRoleOrAdmin(Roles.ServerStartStop))
                await controller.StopAsync(user);
            else
                return Forbid();
        var definitions = controller.GetConfigurationEntryDefinitions(CultureInfo.CurrentCulture)
            .ToDictionary((q) => q.Identifier);
        var now = DateTimeOffset.Now;
        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        await foreach (var configurationEntry in _apiDbContext.ConfigurationEntries
                           .Where((q) => q.GameServerFk == gameServerId)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            var providedEntry = configurationEntries.SingleOrDefault(
                (q) => q.Realm == configurationEntry.Realm && q.Path == configurationEntry.Path);
            if (providedEntry is not null
                && (providedEntry.Value == configurationEntry.Value
                    || providedEntry.Value is Constants.PasswordReplacement))
            {
                configurationEntries.Remove(providedEntry);
                continue;
            }

            configurationEntry.IsActive = false;
        }

        foreach (var configurationEntry in configurationEntries)
        {
            configurationEntry.PrimaryKey   = default;
            configurationEntry.IsActive     = true;
            configurationEntry.TimeStamp    = now;
            configurationEntry.ChangedByFk  = user.PrimaryKey;
            configurationEntry.ChangedBy    = default;
            configurationEntry.GameServer   = default;
            configurationEntry.GameServerFk = gameServerId;
            if (definitions.TryGetValue($"{configurationEntry.Realm}://{configurationEntry.Path}", out var definition))
            {
                configurationEntry.IsSensitive = definition.Kind is EConfigurationEntryKind.Password;
            }

            _apiDbContext.ConfigurationEntries.Add(configurationEntry);
        }

        if (!controller.CanUpdateConfiguration || controller is {IsRunning: true, CanStop: false})
            return Forbid(); // Forbid because not possible pre-applying, preventing work.
        if (controller.IsRunning)
            await controller.StopAsync(user);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        if (!controller.CanUpdateConfiguration || controller is {IsRunning: true, CanStop: false})
            return Forbid(); // Forbid because not possible pre-applying, preventing work.
        if (controller.IsRunning)
            await controller.StopAsync(user);
        await controller.UpdateConfigurationAsync();
        return NoContent();
    }

    /// <summary>
    /// Return the configuration of the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [ProducesResponseType(typeof(IEnumerable<ConfigurationEntryDefinition>), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpGet("{gameServerId:long}/configuration/definitions", Name = nameof(GetConfigurationDefinitionsAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerUpdate)]
    public async Task<ActionResult<IEnumerable<ConfigurationEntryDefinition>>> GetConfigurationDefinitionsAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        return Ok(controller.GetConfigurationEntryDefinitions(Request.GetCultureInfo()));
    }

    /// <summary>
    /// Deletes the given <see cref="GameServer"/> by making it inactive.
    /// Requires the <see cref="GameServer"/> to be stopped to work.
    /// </summary>
    /// <remarks>
    /// Full deletion of a <see cref="GameServer"/> is not done by this to allow administrators to still view logs.
    /// </remarks>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [HttpPost("{gameServerId:long}/delete", Name = nameof(DeleteGameServerAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerCreateOrDelete)]
    public async Task<ActionResult> DeleteGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            (q) => q.PrimaryKey == gameServerId,
            cancellationToken);
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (controller.IsRunning)
            return Forbid();
        gameServer.IsActive = false;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Creates a new given <see cref="GameServer"/>.
    /// </summary>
    /// <remarks>
    /// This route will not accept any configuration or entities and expects a flat input.
    /// </remarks>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="controllerIdentifier">The type of game to create.</param>
    /// <param name="gameServer">The initial data to create the <see cref="GameServer"/> with.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(GameServerInfo), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [HttpPost("create/{controllerIdentifier}", Name = nameof(CreateGameServerAsync))]
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerCreateOrDelete)]
    public async Task<ActionResult<GameServerInfo>> CreateGameServerAsync(
        [FromRoute] string controllerIdentifier,
        [FromBody] GameServer gameServer,
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Forbid();
        var controllerIdentifiers = _gameServerControllerFactory.GetGameControllers();
        if (!controllerIdentifiers.Contains(controllerIdentifier))
            return BadRequest();
        gameServer.ActiveModPack        = null;
        gameServer.SelectedModPack      = null;
        gameServer.LifetimeEvents       = null;
        gameServer.ConfigurationEntries = null;
        gameServer.IsActive             = true;
        gameServer.Status               = ELifetimeStatus.Stopped;
        gameServer.TimeStampCreated     = DateTimeOffset.UtcNow;
        gameServer.ActiveModPackFk      = null;
        gameServer.ControllerIdentifier = controllerIdentifier;
        await _apiDbContext.GameServers.AddAsync(gameServer, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        _ = ExecuteInstallOrUpgradeAsync(gameServer, controller, user).ConfigureAwait(false);
        await Task.Delay(1000, cancellationToken);
        return Ok(
            new GameServerInfo(
                gameServer,
                controller.IsRunning,
                controller.CanStart,
                controller.CanStop,
                controller.CanUpdateConfiguration,
                controller.CanInstallOrUpgrade));
    }
}