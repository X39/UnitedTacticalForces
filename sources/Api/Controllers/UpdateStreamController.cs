using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Provides a websocket connection to the client
/// </summary>
[ApiController]
[Route(Constants.Routes.UpdateStream)]
public class UpdateStreamController : ControllerBase
{
    private readonly ILogger<UpdateStreamController> _logger;
    private readonly IUpdateStreamService            _updateStreamService;
    private readonly DatabaseContext                    _apiDbContext;

    /// <summary>
    /// Handles the management and operations of the update stream, including websocket connections.
    /// </summary>
    public UpdateStreamController(
        ILogger<UpdateStreamController> logger,
        IUpdateStreamService updateStreamService,
        DatabaseContext apiDbContext)
    {
        _logger              = logger;
        _updateStreamService = updateStreamService;
        _apiDbContext        = apiDbContext;
    }

    /// <summary>
    /// Allows to subscribe to the update stream using a websocket.
    /// </summary>
    /// <param name="path">The path to subscribe to.</param>
    [HttpGet("subscribe", Name = nameof(UpdateStreamController) + "." + nameof(SubscribeAsync))]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [Authorize]
    public async Task<IActionResult> SubscribeAsync([FromQuery] string path)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (!await CanSubscribeAsync(path, userId, HttpContext.RequestAborted))
            return Forbid();
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            return BadRequest();

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        _logger.Log(LogLevel.Information, "WebSocket connection established for {User}", userId);
        await _updateStreamService.RegisterAsync(path, webSocket, HttpContext.RequestAborted);
        return new EmptyResult();
    }

    private async Task<bool> CanSubscribeAsync(
        string path,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
            return false;

        if (string.Equals(segments[0], Constants.Routes.Events, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[1], out var eventId))
        {
            return await _apiDbContext.Events
                .AsNoTracking()
                .Where(q => q.PrimaryKey == eventId)
                .AnyAsync(q
                    => q.IsVisible
                       || q.HostedByFk == userId
                       || q.OwnerFk == userId
                       || User.HasClaim(Claims.Administrative.All, string.Empty)
                       || User.HasClaim(Claims.Administrative.Event, string.Empty), cancellationToken);
        }

        return false;
    }
}
