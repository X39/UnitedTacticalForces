using System.Globalization;
using System.Net;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.GameServerController;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route(Constants.Routes.GameServers)]
[Authorize(Roles.ServerAccess)]
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
    /// <param name="CanUpgrade">Whether the server can be upgraded or not.</param>
    [PublicAPI]
    public record GameServerInfo(GameServer GameServer, bool IsRunning, bool CanStart, bool CanStop, bool CanUpgrade);

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
                    controller is {CanInstallOrUpgrade: true, CanUpdateConfiguration: true}));
        }

        return Ok(gameServerInfos);
    }

    /// <summary>
    /// Starts the given <see cref="GameServer"/> if it can be started.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [HttpPost("{gameServerId:long}/start", Name = nameof(StartGameServerAsync))]
    [Authorize(Roles = Roles.ServerStartStop)]
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
        await controller.StartAsync(user);
        return NoContent();
    }

    /// <summary>
    /// Starts the given <see cref="GameServer"/> if it can be stopped.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to stop.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [HttpPost("{gameServerId:long}/stop", Name = nameof(StopGameServerAsync))]
    [Authorize(Roles = Roles.ServerStartStop)]
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
        await controller.StopAsync(user);
        return NoContent();
    }

    /// <summary>
    /// Starts the given <see cref="GameServer"/> if it can be upgraded.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to upgrade.</param>
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [HttpPost("{gameServerId:long}/upgrade", Name = nameof(UpgradeGameServerAsync))]
    [Authorize(Roles = Roles.ServerUpgrade)]
    public async Task<ActionResult> UpgradeGameServerAsync(
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
        if (controller.IsRunning)
            await controller.StopAsync(user);
        await controller.UpdateConfigurationAsync();
        if (!controller.CanInstallOrUpgrade || controller is {IsRunning: true, CanStop: false})
            return Forbid(); // Forbid because not possible pre-applying, preventing work.
        if (controller.IsRunning)
            await controller.StopAsync(user);
        await controller.InstallOrUpgradeAsync(user);
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
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NoContent)]
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
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(IEnumerable<ConfigurationEntry>), (int) HttpStatusCode.OK)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.Forbidden)]
    [HttpPost("{gameServerId:long}/configuration", Name = nameof(GetConfigurationAsync))]
    public async Task<ActionResult<IEnumerable<ConfigurationEntry>>> GetConfigurationAsync(
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

            var realm = configurationEntry.Realm;
            var path = configurationEntry.Path;
            var existing = await _apiDbContext.ConfigurationEntries.SingleOrDefaultAsync(
                (q) => q.Realm == realm && q.Path == path,
                cancellationToken);
            if (existing is not null) existing.IsActive = false;
            _apiDbContext.ConfigurationEntries.Add(configurationEntry);
        }

        if (!controller.CanUpdateConfiguration || controller is {IsRunning: true, CanStop: false})
            return Forbid(); // Forbid because not possible pre-applying, preventing work.
        if (controller.IsRunning)
            await controller.StopAsync(user);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return Ok(configurationEntries);
    }

    /// <summary>
    /// Return the configuration of the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [Authorize(Roles = Roles.ServerUpdate)]
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
        var cultureInfo = new CultureInfo(Request.Headers.AcceptLanguage.FirstOrDefault() ?? "en-US");
        return Ok(controller.GetConfigurationEntryDefinitions(cultureInfo));
    }
}