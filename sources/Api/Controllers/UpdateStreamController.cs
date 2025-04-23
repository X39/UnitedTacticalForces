using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Handles the management and operations of the update stream, including websocket connections.
    /// </summary>
    public UpdateStreamController(
        ILogger<UpdateStreamController> logger,
        IUpdateStreamService updateStreamService)
    {
        _logger              = logger;
        _updateStreamService = updateStreamService;
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
    public async Task<IActionResult> SubscribeAsync(
        [FromQuery] string path)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            _logger.Log(LogLevel.Information, "WebSocket connection established for {User}", userId);
            await _updateStreamService.RegisterAsync(path, webSocket);
            return Accepted();
        }
        else
        {
            return BadRequest();
        }
    }
}
