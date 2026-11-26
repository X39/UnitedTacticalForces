using System.Collections.Frozen;

namespace X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

/// <summary>
/// The closed vocabulary of reason codes that explain why a server metric reading is unavailable.
/// </summary>
/// <remarks>
/// The codes are plain strings and travel unchanged over REST, the worker protocol and the update stream. A
/// reading is always either a value or exactly one of these reasons; a reason is never replaced by zero.
/// </remarks>
public static class WorkerMetricUnavailableReasons
{
    /// <summary>
    /// The server has no running session, so there is nothing to measure. Set by the API, never by a worker.
    /// </summary>
    public const string ServerNotRunning = "server-not-running";

    /// <summary>
    /// The worker of the running session is not connected. Set by the API, never by a worker.
    /// </summary>
    public const string WorkerOffline = "worker-offline";

    /// <summary>
    /// The latest accepted sample is older than the stale limit. Set by the API, never by a worker.
    /// </summary>
    public const string Stale = "stale";

    /// <summary>
    /// The game cannot report this reading at all. May be sent by a worker for the player count.
    /// </summary>
    public const string GameDoesNotReport = "game-does-not-report";

    /// <summary>
    /// The worker tried to read the value and the read failed. May be sent by a worker for any reading.
    /// </summary>
    public const string QueryFailed = "query-failed";

    /// <summary>
    /// The worker has not collected enough data yet, for example the first CPU sample after a start. May be sent by
    /// a worker for any reading.
    /// </summary>
    public const string Measuring = "measuring";

    /// <summary>
    /// The worker's platform offers no way to read this machine value. May be sent by a worker for CPU and memory.
    /// </summary>
    public const string UnsupportedPlatform = "unsupported-platform";

    /// <summary>
    /// Gets the reasons a worker may send for a CPU or memory reading.
    /// </summary>
    /// <value>A set containing measuring, unsupported-platform and query-failed.</value>
    public static IReadOnlySet<string> WorkerMachineReasons { get; } =
        new[] { Measuring, UnsupportedPlatform, QueryFailed }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Gets the reasons a worker may send for a player count reading.
    /// </summary>
    /// <value>A set containing measuring, game-does-not-report and query-failed.</value>
    public static IReadOnlySet<string> WorkerPlayerReasons { get; } =
        new[] { Measuring, GameDoesNotReport, QueryFailed }.ToFrozenSet(StringComparer.Ordinal);
}
