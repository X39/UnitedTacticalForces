namespace X39.UnitedTacticalForces.WebSockets.Contracts;

/// <summary>
/// The wire envelope for messages sent through an update WebSocket.
/// </summary>
/// <typeparam name="T">The message payload type.</typeparam>
public sealed class WebSocketMessage<T>
{
    /// <summary>
    /// Gets the fully qualified payload type name.
    /// </summary>
    public string Type { get; init; } = typeof(T).FullName ?? typeof(T).Name;

    /// <summary>
    /// Gets the message payload.
    /// </summary>
    public required T Value { get; init; }
}
