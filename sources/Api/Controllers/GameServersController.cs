using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.GameServerController;
using X39.UnitedTacticalForces.Contract.GameServer;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Controller responsible for managing game servers, including fetching, updating, and controlling their operations.
/// </summary>
[ApiController]
[Route(Constants.Routes.GameServers)]
[Authorize]
public class GameServersController : ControllerBase
{
    private readonly ILogger<GameServersController> _logger;
    private readonly ApiDbContext                   _apiDbContext;
    private readonly IGameServerControllerFactory   _gameServerControllerFactory;

    /// <inheritdoc />
    public GameServersController(
        ILogger<GameServersController> logger,
        ApiDbContext apiDbContext,
        IGameServerControllerFactory gameServerControllerFactory
    )
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
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    [HttpGet("all/count", Name = nameof(GetGameServerCountAsync))]
    public async Task<IActionResult> GetGameServerCountAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        await foreach (var _ in GetUserAccessibleDatabaseGameServersAsync(cancellationToken))
        {
            count++;
        }

        return Ok(count);
    }

    /// <summary>
    /// Returns all available <see cref="GameServer"/>'s and their state.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [ProducesResponseType<GameServerInfoDto[]>(StatusCodes.Status200OK)]
    [HttpGet("all", Name = nameof(GetGameServersAsync))]
    public async Task<IActionResult> GetGameServersAsync(CancellationToken cancellationToken)
    {
        var gameServerInfos = new List<GameServerInfoDto>();
        await foreach (var gameServer in GetUserAccessibleDatabaseGameServersAsync(cancellationToken))
        {
            var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
            gameServerInfos.Add(CreateDto(gameServer, controller));
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
    [ProducesResponseType<GameServerInfoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{gameServerId:long}", Name = nameof(GetGameServerAsync))]
    [Authorize($"{Claims.ResourceBased.Server.All}:*")]
    public async Task<IActionResult> GetGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();

        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        return Ok(CreateDto(gameServer, controller));
    }

    /// <summary>
    /// Allows to rename a <see cref="GameServer"/>.
    /// </summary>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to rename.</param>
    /// <param name="newName">The new name of the <see cref="GameServer"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPut("{gameServerId:long}/rename", Name = nameof(RenameAsync))]
    [Authorize(Claims.ResourceBased.Server.Rename)]
    public async Task<IActionResult> RenameAsync(
        [FromRoute] long gameServerId,
        [FromBody] string newName,
        CancellationToken cancellationToken
    )
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        gameServer.Title = newName;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Allows to change the mod pack of a <see cref="GameServer"/>.
    /// </summary>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to change the mod pack of.</param>
    /// <param name="modPackId">The id of the mod pack to change to.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPut("{gameServerId:long}/mod-pack/set", Name = nameof(ChangeModPackAsync))]
    [Authorize(Claims.ResourceBased.Server.ModPack)]
    public async Task<IActionResult> ChangeModPackAsync(
        [FromRoute] long gameServerId,
        [FromBody] int modPackId,
        CancellationToken cancellationToken
    )
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        gameServer.SelectedModPackFk = modPackId;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Allows to remove the mod pack of a <see cref="GameServer"/>.
    /// </summary>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to change the mod pack of.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPut("{gameServerId:long}/mod-pack/clear", Name = nameof(ClearModPackAsync))]
    [Authorize(Claims.ResourceBased.Server.ModPack)]
    public async Task<IActionResult> ClearModPackAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        gameServer.SelectedModPackFk = null;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Returns a list of all available game server controllers.
    /// </summary>
    [ProducesResponseType<string[]>(StatusCodes.Status200OK)]
    [HttpGet("all/controllers", Name = nameof(GetGameServerControllers))]
    [Authorize(Claims.Creation.Server)]
    public IActionResult GetGameServerControllers()
    {
        return Ok(_gameServerControllerFactory.GetGameControllers());
    }

    /// <summary>
    /// Starts the given <see cref="GameServer"/> if it can be started.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [ProducesResponseType<GameServerInfoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [HttpPost("{gameServerId:long}/start", Name = nameof(StartGameServerAsync))]
    [Authorize(Claims.ResourceBased.Server.StartStop)]
    public async Task<IActionResult> StartGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
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
                        gameServer.PrimaryKey
                    );
                    try
                    {
                        await controller.StartAsync(user)
                            .ConfigureAwait(false);
                        _logger.LogDebug(
                            "Start completed without exception for {GameServerTitle} ({GameServerPk})",
                            gameServer.Title,
                            gameServer.PrimaryKey
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Exception while starting {GameServerTitle} ({GameServerPk})",
                            gameServer.Title,
                            gameServer.PrimaryKey
                        );
                    }
                },
                CancellationToken.None
            )
            .ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        return Ok(CreateDto(gameServer, controller));
    }

    /// <summary>
    /// Starts the given <see cref="GameServer"/> if it can be stopped.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to stop.</param>
    [ProducesResponseType<GameServerInfoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [HttpPost("{gameServerId:long}/stop", Name = nameof(StopGameServerAsync))]
    [Authorize(Claims.ResourceBased.Server.StartStop)]
    public async Task<IActionResult> StopGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanStop)
            return Forbid();
        _ = ExecuteStopGameServerAsync(gameServer, controller, user)
            .ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        return Ok(CreateDto(gameServer, controller));
    }

    /// <summary>
    /// Starts the given <see cref="GameServer"/> if it can be upgraded.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to upgrade.</param>
    [ProducesResponseType<GameServerInfoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [HttpPost("{gameServerId:long}/upgrade", Name = nameof(UpgradeGameServerAsync))]
    [Authorize(Claims.ResourceBased.Server.Upgrade)]
    public async Task<IActionResult> UpgradeGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanInstallOrUpgrade
            || !controller.CanUpdateConfiguration
            || controller is { IsRunning: true, CanStop: false })
            return Forbid(); // Forbid because not possible pre-applying, preventing work.
        _ = ExecuteUpgradeGameServerAsync(gameServer, controller, user)
            .ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        return Ok(CreateDto(gameServer, controller));
    }

    /// <summary>
    /// Return the configuration of the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to start.</param>
    [ProducesResponseType<PlainConfigurationEntryDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{gameServerId:long}/configuration", Name = nameof(GetConfigurationAsync))]
    [Authorize(Claims.ResourceBased.Server.Configuration)]
    public async Task<IActionResult> GetConfigurationAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken
    )
    {
        if (!await _apiDbContext.GameServers.AnyAsync(q => q.PrimaryKey == gameServerId, cancellationToken))
            return NotFound();
        var configurationEntries = await _apiDbContext.ConfigurationEntries
            .Where(q => q.GameServerFk == gameServerId)
            .Where(q => q.IsActive)
            .Select(e => new PlainConfigurationEntryDto
                {
                    PrimaryKey   = e.PrimaryKey,
                    Realm        = e.Realm,
                    Path         = e.Path,
                    Value        = e.Value,
                    GameServerFk = e.GameServerFk,
                    ChangedByFk  = e.ChangedByFk,
                    TimeStamp    = e.TimeStamp,
                    IsSensitive  = e.IsSensitive,
                    IsActive     = e.IsActive,
                }
            )
            .ToArrayAsync(cancellationToken);
        foreach (var configurationEntry in configurationEntries.Where(q => q.IsSensitive))
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
    [ProducesResponseType<GameFolder[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{gameServerId:long}/folders", Name = nameof(GetGameFoldersAsync))]
    [Authorize(Claims.ResourceBased.Server.Files)]
    public async Task<IActionResult> GetGameFoldersAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
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
    [ProducesResponseType<GameFileInfo[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{gameServerId:long}/folders/{folder}/files", Name = nameof(GetGameFolderFilesAsync))]
    [Authorize(Claims.ResourceBased.Server.Files)]
    public async Task<IActionResult> GetGameFolderFilesAsync(
        [FromRoute] long gameServerId,
        [FromRoute] string folder,
        CancellationToken cancellationToken
    )
    {
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        var folders = await controller.GetGameFoldersAsync(Request.GetCultureInfo(), cancellationToken);
        var gameFolder = folders.FirstOrDefault(q => q.Identifier == folder);
        if (gameFolder is null)
            return NotFound();
        var files = await controller.GetGameFolderFilesAsync(gameFolder, Request.GetCultureInfo(), cancellationToken);
        return Ok(files);
    }

    /// <summary>
    /// Creates or updates a file in a folder of the given <see cref="GameServer"/>.
    /// </summary>
    /// <param name="folder">Identifier of the folder to change a file in.</param>
    /// <param name="file">The data of the file to upload</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="gameServerId">The id of the <see cref="GameServer"/> to change the file from.</param>
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [HttpPost("{gameServerId:long}/folders/{folder}/upload", Name = nameof(UploadGameFolderFileAsync))]
    [Authorize(Claims.ResourceBased.Server.Files)]
    public async Task<IActionResult> UploadGameFolderFileAsync(
        [FromRoute] long gameServerId,
        [FromRoute] string folder, 
        IFormFile file,
        CancellationToken cancellationToken
    )
    {
        if (file.FileName.Contains('/') || file.FileName.Contains('\\') || file.FileName is ".." or ".")
            return BadRequest();
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanModifyGameFiles)
            return Forbid(); // Forbid because not possible, preventing work.
        var folders = await controller.GetGameFoldersAsync(Request.GetCultureInfo(), cancellationToken);
        var gameFolder = folders.FirstOrDefault(q => q.Identifier == folder);
        if (gameFolder is null)
            return NotFound();
        await controller.UploadFileAsync(
            gameFolder,
            new GameFileInfo(file.FileName, file.Length, file.ContentType),
            file.OpenReadStream()
        );
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
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("{gameServerId:long}/folders/{folder}/{file}/delete", Name = nameof(DeleteGameFolderFileAsync))]
    [Authorize(Claims.ResourceBased.Server.Files)]
    public async Task<IActionResult> DeleteGameFolderFileAsync(
        [FromRoute] long gameServerId,
        [FromRoute] string folder,
        [FromRoute] string file,
        CancellationToken cancellationToken
    )
    {
        if (file.Contains('/') || file.Contains('\\') || file is ".." or ".")
            return BadRequest();
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanModifyGameFiles)
            return Forbid(); // Forbid because not possible, preventing work.
        var folders = await controller.GetGameFoldersAsync(Request.GetCultureInfo(), cancellationToken);
        var gameFolder = folders.FirstOrDefault(q => q.Identifier == folder);
        if (gameFolder is null)
            return NotFound();
        await controller.DeleteFileAsync(gameFolder, new GameFileInfo(file, 0, string.Empty));
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
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpPost("{gameServerId:long}/folders/{folder}/{file}/get", Name = nameof(GetGameFolderFileAsync))]
    [Authorize(Claims.ResourceBased.Server.Files)]
    public async Task<ActionResult<Stream>> GetGameFolderFileAsync(
        [FromRoute] long gameServerId,
        [FromRoute] string folder,
        [FromRoute] string file,
        CancellationToken cancellationToken
    )
    {
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanModifyGameFiles)
            return Forbid(); // Forbid because not possible, preventing work.
        var folders = await controller.GetGameFoldersAsync(Request.GetCultureInfo(), cancellationToken);
        var gameFolder = folders.FirstOrDefault(q => q.Identifier == folder);
        if (gameFolder is null)
            return NotFound();
        var fileInfos = await controller.GetGameFolderFilesAsync(
            gameFolder,
            Request.GetCultureInfo(),
            cancellationToken
        );
        var fileInfo = fileInfos.FirstOrDefault(q => q.Name == file);
        if (fileInfo is null)
            return NotFound();
        Response.Headers.Append(
            "Content-Disposition",
            new System.Net.Mime.ContentDisposition
            {
                FileName = fileInfo.Name,
                Inline   = false,
            }.ToString()
        );
        var stream = await controller.GetGameFolderFileAsync(
            gameFolder,
            new GameFileInfo(file, 0, string.Empty),
            cancellationToken
        );
        return new FileStreamResult(stream, fileInfo.MimeType) { FileDownloadName = fileInfo.Name };
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
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{gameServerId:long}/logs/count", Name = nameof(GetLogsCountAsync))]
    [Authorize(Claims.ResourceBased.Server.AccessLogs)]
    public async Task<IActionResult> GetLogsCountAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? referenceTimeStamp = null
    )
    {
        if (!await _apiDbContext.GameServers.AnyAsync(q => q.PrimaryKey == gameServerId, cancellationToken))
            return NotFound();
        var query = _apiDbContext.GameServerLogs.Where(q => q.GameServerFk == gameServerId);
        if (referenceTimeStamp is not null)
            query = query.Where(q => q.TimeStamp >= referenceTimeStamp);
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
    [ProducesResponseType<PlainGameServerLogDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{gameServerId:long}/logs", Name = nameof(GetLogsAsync))]
    [Authorize(Claims.ResourceBased.Server.AccessLogs)]
    public async Task<IActionResult> GetLogsAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? referenceTimeStamp = null,
        [FromQuery] bool descendingByTimeStamp = false
    )
    {
        if (!await _apiDbContext.GameServers.AnyAsync(q => q.PrimaryKey == gameServerId, cancellationToken))
            return NotFound();
        var query = _apiDbContext.GameServerLogs.Where(q => q.GameServerFk == gameServerId);
        if (referenceTimeStamp is not null)
            query = query.Where(q => q.TimeStamp >= referenceTimeStamp);
        query = descendingByTimeStamp ? query.OrderByDescending(q => q.TimeStamp) : query.OrderBy(q => q.TimeStamp);
        var configurationEntries = await query
            .Select(e => new PlainGameServerLogDto
                {
                    PrimaryKey   = e.PrimaryKey,
                    LogLevel     = e.LogLevel,
                    TimeStamp    = e.TimeStamp,
                    Message      = e.Message,
                    Source       = e.Source,
                    GameServerFk = e.GameServerFk,
                }
            )
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
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [HttpGet("{gameServerId:long}/logs/download", Name = nameof(DownloadLogsAsync))]
    [Authorize(Claims.ResourceBased.Server.AccessLogs)]
    public async Task DownloadLogsAsync([FromRoute] long gameServerId, CancellationToken cancellationToken)
    {
        var gameServer = await _apiDbContext.GameServers
            .SingleAsync(q => q.PrimaryKey == gameServerId, cancellationToken)
            .ConfigureAwait(false);
        var logs = _apiDbContext.GameServerLogs
            .Where(q => q.GameServerFk == gameServerId)
            .OrderBy(q => q.TimeStamp)
            .AsAsyncEnumerable();
        Response.Headers.Append(
            "Content-Disposition",
            new System.Net.Mime.ContentDisposition
            {
                FileName = $"{gameServer.Title}.log.txt",
                Inline   = false,
            }.ToString()
        );
        await using var stream = new StreamWriter(Response.Body);
        await foreach (var log in logs.WithCancellation(cancellationToken)
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
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [HttpPost("{gameServerId:long}/logs/clear", Name = nameof(ClearLogsAsync))]
    [Authorize(Claims.ResourceBased.Server.DeleteLogs)]
    public async Task<IActionResult> ClearLogsAsync([FromRoute] long gameServerId, CancellationToken cancellationToken)
    {
        if (!await _apiDbContext.GameServers
                .AnyAsync(q => q.PrimaryKey == gameServerId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();
        await _apiDbContext.GameServerLogs
            .Where(q => q.GameServerFk == gameServerId)
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
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpPost("{gameServerId:long}/configuration", Name = nameof(SetConfigurationAsync))]
    [Authorize(Claims.ResourceBased.Server.Configuration)]
    public async Task<IActionResult> SetConfigurationAsync(
        [FromRoute] long gameServerId,
        [FromBody] List<ConfigurationEntryUpdate> configurationEntries,
        CancellationToken cancellationToken
    )
    {
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
        if (gameServer is null)
            return NotFound();
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        if (!controller.CanUpdateConfiguration || controller is { IsRunning: true, CanStop: false })
            return Forbid(); // Forbid because not possible pre-applying, preventing work.
        if (controller.IsRunning)
            await controller.StopAsync(user);
        var definitions = controller.GetConfigurationEntryDefinitions(CultureInfo.CurrentCulture)
            .ToDictionary(q => q.Identifier);
        var now = DateTimeOffset.Now;
        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        await foreach (var configurationEntry in _apiDbContext.ConfigurationEntries
                           .Where(q => q.GameServerFk == gameServerId)
                           .Where(q => q.IsActive)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            var providedEntry = configurationEntries.SingleOrDefault(q
                => q.Realm == configurationEntry.Realm && q.Path == configurationEntry.Path
            );
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
            var isSensitive = false;
            if (definitions.TryGetValue($"{configurationEntry.Realm}://{configurationEntry.Path}", out var definition))
            {
                isSensitive = definition.Kind is EConfigurationEntryKind.Password;
            }

            _apiDbContext.ConfigurationEntries.Add(new ConfigurationEntry
                {
                    GameServer   = null,
                    ChangedBy    = null,
                    PrimaryKey   = default,
                    Realm        = configurationEntry.Realm,
                    Path         = configurationEntry.Path,
                    Value        = configurationEntry.Value,
                    GameServerFk = gameServerId,
                    ChangedByFk  = user.PrimaryKey,
                    TimeStamp    = now,
                    IsSensitive  = isSensitive,
                    IsActive     = true,
                }
            );
        }

        if (!controller.CanUpdateConfiguration || controller is { IsRunning: true, CanStop: false })
            return Forbid(); // Forbid because not possible pre-applying, preventing work.
        if (controller.IsRunning)
            await controller.StopAsync(user);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        if (!controller.CanUpdateConfiguration || controller is { IsRunning: true, CanStop: false })
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
    [ProducesResponseType<ConfigurationEntryDefinition[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [HttpGet("{gameServerId:long}/configuration/definitions", Name = nameof(GetConfigurationDefinitionsAsync))]
    [Authorize(Claims.ResourceBased.Server.Configuration)]
    public async Task<IActionResult> GetConfigurationDefinitionsAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
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
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpPost("{gameServerId:long}/delete", Name = nameof(DeleteGameServerAsync))]
    [Authorize(Claims.Administrative.Server)]
    public async Task<IActionResult> DeleteGameServerAsync(
        [FromRoute] long gameServerId,
        CancellationToken cancellationToken
    )
    {
        var gameServer = await _apiDbContext.GameServers.SingleOrDefaultAsync(
            q => q.PrimaryKey == gameServerId,
            cancellationToken
        );
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
    /// <param name="title">The title of the new <see cref="GameServer"/>.</param>
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType<GameServerInfoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [HttpPost("create/{controllerIdentifier}", Name = nameof(CreateGameServerAsync))]
    [Authorize(Claims.Administrative.Server)]
    public async Task<IActionResult> CreateGameServerAsync(
        [FromRoute] string controllerIdentifier,
        [FromBody] string title,
        CancellationToken cancellationToken
    )
    {
        var user = await User.GetDatabaseUserAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Forbid();
        var controllerIdentifiers = _gameServerControllerFactory.GetGameControllers();
        if (!controllerIdentifiers.Contains(controllerIdentifier))
            return BadRequest();
        var now = DateTimeOffset.UtcNow;
        var gameServer = new GameServer
        {
            PrimaryKey           = default,
            Title                = title,
            TimeStampCreated     = now,
            TimeStampUpgraded    = now,
            ActiveModPack        = null,
            ActiveModPackFk      = null,
            SelectedModPack      = null,
            SelectedModPackFk    = null,
            Status               = ELifetimeStatus.Stopped,
            VersionString        = "0.0.1",
            LifetimeEvents       = null,
            ConfigurationEntries = null,
            GameServerLogs       = null,
            ControllerIdentifier = controllerIdentifier,
            IsActive             = true,
        };
        await _apiDbContext.GameServers.AddAsync(gameServer, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        var controller = await _gameServerControllerFactory.GetGameControllerAsync(gameServer);
        _ = ExecuteInstallOrUpgradeAsync(gameServer, controller, user)
            .ConfigureAwait(false);
        await Task.Delay(1000, cancellationToken);
        return Ok(CreateDto(gameServer, controller));
    }

    #region Helpers

    private static GameServerInfoDto CreateDto(GameServer gameServer, IGameServerController controller)
    {
        return new GameServerInfoDto
        {
            GameServer             = gameServer.ToPlainDto(),
            IsRunning              = controller.IsRunning,
            CanStart               = controller.CanStart,
            CanStop                = controller.CanStop,
            CanUpdateConfiguration = controller.CanUpdateConfiguration,
            CanUpgrade             = controller.CanInstallOrUpgrade,
        };
    }

    private async IAsyncEnumerable<GameServer> GetUserAccessibleDatabaseGameServersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var allServers = _apiDbContext.GameServers
            .Where(q => q.IsActive)
            .OrderBy(q => q.Title)
            .ToAsyncEnumerable();
        if (User.HasClaim(Claims.Administrative.All, string.Empty)
            || User.HasClaim(Claims.Administrative.Server, string.Empty))
        {
            await foreach (var server in allServers.WithCancellation(cancellationToken))
            {
                yield return server;
            }
        }
        else
        {
            await foreach (var server in allServers.WithCancellation(cancellationToken))
            {
                var hasAnyServerClaim = User.HasClaim(claim
                    => claim.Value == server.PrimaryKey.ToString()
                       && (claim.Type == Claims.ResourceBased.Server.All
                           || claim.Type.StartsWith(Claims.ResourceBased.Server.All + ":"))
                );
                if (hasAnyServerClaim)
                    yield return server;
            }
        }
    }

    private async Task ExecuteUpgradeGameServerAsync(GameServer gameServer, IGameServerController controller, User user)
    {
        _logger.LogDebug(
            "Upgrading game server {GameServerTitle} ({GameServerPk})",
            gameServer.Title,
            gameServer.PrimaryKey
        );
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
                gameServer.PrimaryKey
            );
        }
    }

    private async Task ExecuteStopGameServerAsync(GameServer gameServer, IGameServerController controller, User user)
    {
        _logger.LogDebug(
            "Stopping game server {GameServerTitle} ({GameServerPk})",
            gameServer.Title,
            gameServer.PrimaryKey
        );
        try
        {
            await controller.StopAsync(user)
                .ConfigureAwait(false);
            _logger.LogDebug(
                "Stop completed without exception for {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while stopping {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey
            );
            throw;
        }
    }

    private async Task ExecuteInstallOrUpgradeAsync(GameServer gameServer, IGameServerController controller, User user)
    {
        _logger.LogDebug(
            "Upgrading (or installing) game server {GameServerTitle} ({GameServerPk})",
            gameServer.Title,
            gameServer.PrimaryKey
        );
        try
        {
            await controller.InstallOrUpgradeAsync(user)
                .ConfigureAwait(false);
            _logger.LogDebug(
                "Upgrade (or install) completed without exception for {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while upgrading (or installing) {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey
            );
            throw;
        }
    }

    private async Task ExecuteUpdateConfigurationAsync(
        GameServer gameServer,
        IGameServerController controller,
        User user
    )
    {
        _logger.LogDebug(
            "Updating configuration of game server {GameServerTitle} ({GameServerPk}), issued by {UserNickname} ({UserPk})",
            gameServer.Title,
            gameServer.PrimaryKey,
            user.Nickname,
            user.PrimaryKey
        );
        try
        {
            await controller.UpdateConfigurationAsync()
                .ConfigureAwait(false);
            _logger.LogDebug(
                "Updating configuration completed without exception for {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while updating configuration of {GameServerTitle} ({GameServerPk})",
                gameServer.Title,
                gameServer.PrimaryKey
            );
            throw;
        }
    }

    #endregion
}
