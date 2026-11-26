namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Timing and size limits of the persistent worker channel.
/// </summary>
/// <param name="KeepAliveInterval">How often the WebSocket layer pings an idle connection.</param>
/// <param name="KeepAliveTimeout">How long the WebSocket layer waits for a pong before it aborts the connection.</param>
/// <param name="RequestTimeout">The longest a request waits for a connection, its send and its ack together.</param>
/// <param name="SendTimeout">The longest a single frame send may take before the connection is aborted.</param>
/// <param name="InitialBackoff">The delay before the first reconnect attempt after a drop or failure.</param>
/// <param name="MaximumBackoff">The ceiling the reconnect delay doubles up to. It stays below the stale threshold.</param>
/// <param name="MaxInboundMessageBytes">The largest inbound message accepted before the socket is closed.</param>
public sealed record CloudWorkerChannelOptions(
    TimeSpan KeepAliveInterval,
    TimeSpan KeepAliveTimeout,
    TimeSpan RequestTimeout,
    TimeSpan SendTimeout,
    TimeSpan InitialBackoff,
    TimeSpan MaximumBackoff,
    int MaxInboundMessageBytes)
{
    /// <summary>
    /// Gets the production defaults: 15 s keep-alive, 30 s keep-alive and request timeouts, 10 s send timeout,
    /// 1 s to 15 s reconnect backoff and a 4 MiB inbound cap.
    /// </summary>
    /// <value>The default options.</value>
    public static CloudWorkerChannelOptions Default { get; } = new(
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(15),
        4194304);
}
