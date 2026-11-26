using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
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
        var subscriptionPath = await ResolveSubscriptionPathAsync(path, userId, HttpContext.RequestAborted);
        if (subscriptionPath is null)
            return Forbid();
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            return BadRequest();

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        _logger.Log(LogLevel.Information, "WebSocket connection established for {User}", userId);
        await _updateStreamService.RegisterAsync(subscriptionPath, webSocket, HttpContext.RequestAborted);
        return new EmptyResult();
    }

    /// <summary>
    /// Resolves the update-stream path to register for a requested subscription, or rejects the subscription.
    /// </summary>
    /// <remarks>
    /// Event subscriptions keep the requested path. The fleet subscription requires exactly the two segments
    /// <c>game-servers</c> and <c>fleet</c> plus access to the server section, and resolves to the path addressed to
    /// the authenticated user built by <see cref="FleetChangeStreamPaths.ForUser"/> from <paramref name="userId"/>.
    /// No part of the client-supplied path reaches the returned string, so a request with any further segment,
    /// including a user identifier, resolves to <see langword="null"/>. The section gate is deliberately wider than
    /// the per-server view rule and only decides who may open the socket; which server's signal reaches the socket is
    /// decided when the signal is published.
    /// </remarks>
    /// <param name="path">The path requested by the client.</param>
    /// <param name="userId">The identifier of the authenticated user.</param>
    /// <param name="cancellationToken">A token that cancels the authorization query.</param>
    /// <returns>
    /// The string to register with the update stream service, or <see langword="null"/> when the subscription is
    /// not allowed.
    /// </returns>
    internal async Task<string?> ResolveSubscriptionPathAsync(
        string path,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
            return null;

        if (string.Equals(segments[0], Constants.Routes.Events, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[1], out var eventId))
        {
            var canSubscribe = await _apiDbContext.Events
                .AsNoTracking()
                .Where(q => q.PrimaryKey == eventId)
                .AnyAsync(q
                    => q.IsVisible
                       || q.HostedByFk == userId
                       || q.OwnerFk == userId
                       || User.HasClaim(Claims.Administrative.All, string.Empty)
                       || User.HasClaim(Claims.Administrative.Event, string.Empty), cancellationToken);
            return canSubscribe ? path : null;
        }

        if (segments.Length == 2
            && string.Equals(segments[0], Constants.Routes.GameServers, StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[1], "fleet", StringComparison.OrdinalIgnoreCase))
        {
            return CanAccessServerSection() ? FleetChangeStreamPaths.ForUser(userId) : null;
        }

        return null;
    }

    /// <summary>
    /// Returns whether the current principal may see the server section, mirroring the client-side
    /// <c>canAccessGameServers</c> rule: a global administrator or any holder of a server-section claim.
    /// </summary>
    /// <remarks>
    /// This gate is deliberately wider than the per-server view rule (it also admits <c>crt:sv</c> and bare
    /// <c>sv:view</c> or <c>sv</c>) because it only decides who may open the fleet socket. Which server's signal
    /// reaches the socket, which is registered on the path addressed to the authenticated user, is decided when the
    /// signal is published.
    /// </remarks>
    /// <returns><see langword="true"/> when the principal holds a server-section claim of any value.</returns>
    private bool CanAccessServerSection()
    {
        if (User.IsGlobalAdministrator())
            return true;

        string[] serverSectionClaims =
        [
            Claims.Administrative.Server,
            Claims.Administrative.Hosting,
            Claims.Creation.Server,
            Claims.ResourceBased.Server.All,
            Claims.ResourceBased.Server.View,
        ];
        return User.Claims.Any(claim => serverSectionClaims.Contains(claim.Type, StringComparer.Ordinal));
    }
}
