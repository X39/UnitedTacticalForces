using System.Globalization;
using System.Net;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.GameServerController;

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
        var allServers = await _apiDbContext.GameServers.ToArrayAsync(cancellationToken);
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
        _ = controller.StartAsync(user)
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
        _ = controller.StopAsync(user)
            .ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        return Ok(new GameServerInfo(gameServer, controller));
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
        _ = Task.Run(
            async () =>
            {
                if (controller.IsRunning)
                    await controller.StopAsync(user);
                await controller.InstallOrUpgradeAsync(user)
                    .ConfigureAwait(false);
                await controller.UpdateConfigurationAsync()
                    .ConfigureAwait(false);
            },
            cancellationToken);

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
    public async Task<ActionResult<IEnumerable<ConfigurationEntry>>> GetConfigurationAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken)
    {
        if (!await _apiDbContext.GameServers.AnyAsync((q) => q.PrimaryKey == gameServerId, cancellationToken))
            return NotFound();
        var configurationEntries = await _apiDbContext.ConfigurationEntries
            .Where((q) => q.IsActive)
            .Where((q) => q.IsActive)
            .ToArrayAsync(cancellationToken);
        return Ok(configurationEntries);
    }

    /// <summary>
    /// Return the configuration of the given <see cref="GameServer"/>.
    /// </summary>
    /// <remarks>
    /// Latest logs will always be received first.
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
    [ProducesResponseType(typeof(IEnumerable<GameServerLog>), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpGet("{gameServerId:long}/logs", Name = nameof(GetLogsAsync))]
    public async Task<ActionResult<IEnumerable<GameServerLog>>> GetLogsAsync(
        [FromRoute] int skip,
        [FromRoute] int take,
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken,
        [FromRoute] DateTimeOffset? referenceTimeStamp = null)
    {
        if (!await _apiDbContext.GameServers.AnyAsync((q) => q.PrimaryKey == gameServerId, cancellationToken))
            return NotFound();
        var query = _apiDbContext.GameServerLogs.AsQueryable();
        if (referenceTimeStamp is not null)
            query = query.Where((q) => q.TimeStamp >= referenceTimeStamp);
        var configurationEntries = await query
            .OrderBy((q) => q.TimeStamp)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return Ok(configurationEntries);
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
        [FromBody] ConfigurationEntry[] configurationEntries,
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
            await controller.StopAsync(user);
        var definitions = controller.GetConfigurationEntryDefinitions(CultureInfo.CurrentCulture)
            .ToDictionary((q) => q.Identifier);
        var now = DateTimeOffset.Now;
        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        await foreach (var configurationEntry in _apiDbContext.ConfigurationEntries
                           .Where((q) => q.GameServerFk == gameServerId)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            configurationEntry.IsActive = false;
        }

        foreach (var configurationEntry in configurationEntries)
        {
            configurationEntry.PrimaryKey  = default;
            configurationEntry.IsActive    = true;
            configurationEntry.TimeStamp   = now;
            configurationEntry.ChangedByFk = user.PrimaryKey;
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
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerUpdate)]
    [ProducesResponseType(typeof(IEnumerable<ConfigurationEntryDefinition>), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [HttpGet("{gameServerId:long}/configuration/definitions", Name = nameof(GetConfigurationDefinitionsAsync))]
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
        var cultureInfo = new CultureInfo(
            Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',', ';').FirstOrDefault() ?? "en-US");
        return Ok(controller.GetConfigurationEntryDefinitions(cultureInfo));
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
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerCreateOrDelete)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [HttpGet("{gameServerId:long}/delete", Name = nameof(DeleteGameServerAsync))]
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
    [Authorize(Roles = Roles.Admin + "," + Roles.ServerCreateOrDelete)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(GameServerInfo), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [HttpPost("create/{controllerIdentifier}", Name = nameof(CreateGameServerAsync))]
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
        _ = controller.InstallOrUpgradeAsync(user).ConfigureAwait(false);
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