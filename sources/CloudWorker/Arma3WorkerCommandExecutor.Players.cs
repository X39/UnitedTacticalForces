using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Reports the engine-observed human player count of the primary Arma 3 server.</summary>
public sealed partial class Arma3WorkerCommandExecutor
{
    /// <summary>Reports the human players connected to the primary Arma 3 server.</summary>
    /// <param name="cancellationToken">Cancels file reads and process observation.</param>
    /// <returns>
    /// Game-does-not-report on a headless-client host, measuring while the primary process is not running, no
    /// launch session was prepared or the addon has not written a record yet, the count (without capacity) while the
    /// addon's record is fresh, and query-failed once its sequence stopped advancing for 90 seconds.
    /// </returns>
    /// <remarks>
    /// The count comes from the worker-owned server addon that runs inside the engine and writes a nonce-bound player
    /// record. It counts connected users that are neither headless clients nor the server, so a headless client never
    /// inflates the count. An A2S query is deliberately not used because it would include headless clients. The
    /// engine does not expose the configured slot count to the addon, so the capacity stays unknown.
    /// </remarks>
    public async Task<WorkerPlayerObservation> ObservePlayersAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(_connection.ServiceIdentifier, "game-server", StringComparison.Ordinal))
            return WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.GameDoesNotReport);
        using var observation = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (!observation.IsRunning)
            return WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.Measuring);
        Arma3JoinSession? session;
        try
        {
            session = await _joinMonitor.ReadSessionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.QueryFailed);
        }
        return session is null
            ? WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.Measuring)
            : await _joinMonitor.ReadLatestPlayersAsync(session.Nonce, cancellationToken).ConfigureAwait(false);
    }
}
