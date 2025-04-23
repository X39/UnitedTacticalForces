using System.Net.WebSockets;

namespace X39.UnitedTacticalForces.Api.Services.UpdateStreamService;

/// <summary>
/// Represents a service for managing update streams via websockets.
/// </summary>
public interface IUpdateStreamService
{
    /// <summary>
    /// Registers a websocket for a user.
    /// </summary>
    /// <param name="path">The update path to subscribe to.</param>
    /// <param name="webSocket">The websocket to communicate the changes to.</param>
    /// <returns>An awaitable <see cref="ValueTask"/> that completes once the websocket was added.</returns>
    ValueTask RegisterAsync(string path, WebSocket webSocket);

    /// <summary>
    /// Allows to send an update to all registered websockets for a specific path.
    /// </summary>
    /// <param name="path">The path to send the update to.</param>
    /// <param name="package">The data to send.</param>
    /// <typeparam name="T">The type of <paramref name="package"/>.</typeparam>
    /// <returns>An awaitable task that completes once the update was send to all participants.</returns>
    ValueTask SendUpdateAsync<T>(string path, T package);
}