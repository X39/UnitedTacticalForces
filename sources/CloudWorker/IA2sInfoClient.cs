namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Queries the A2S_INFO player figures of a game server that runs on the same machine.</summary>
public interface IA2sInfoClient
{
    /// <summary>Queries the loopback query port of a local game server.</summary>
    /// <param name="port">The UDP query port on 127.0.0.1.</param>
    /// <param name="timeout">The total time allowed for the whole query, including challenge rounds.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The reply figures, or null when the server did not answer or answered with a malformed reply.</returns>
    /// <exception cref="OperationCanceledException">The caller cancelled the query.</exception>
    Task<A2sInfo?> QueryAsync(int port, TimeSpan timeout, CancellationToken cancellationToken);
}
