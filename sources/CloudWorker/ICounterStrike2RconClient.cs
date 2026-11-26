namespace X39.UnitedTacticalForces.CloudWorker;

public interface ICounterStrike2RconClient
{
    Task<bool> RequestQuitAsync(int port, string password, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Authenticates against the loopback RCON port of a local Counter-Strike 2 server and requests its
    /// <c>status</c> report.
    /// </summary>
    /// <param name="port">The TCP RCON port on 127.0.0.1, which equals the game port.</param>
    /// <param name="password">The RCON password, held in memory only; implementations never log or persist it.</param>
    /// <param name="timeout">The total time allowed for the connection, authentication and the reply.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>
    /// The decoded <c>status</c> response text, or <see langword="null" /> when authentication failed, the server
    /// did not answer in time or the connection broke.
    /// </returns>
    /// <exception cref="OperationCanceledException">The caller cancelled the request.</exception>
    Task<string?> RequestStatusAsync(int port, string password, TimeSpan timeout, CancellationToken cancellationToken);
}
