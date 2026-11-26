namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Identifies what the control plane decided for one schedule occurrence.
/// </summary>
public enum EServerScheduleRunOutcome
{
    /// <summary>
    /// A start job was queued for the occurrence.
    /// </summary>
    Started = 0,

    /// <summary>
    /// A stop was requested or a stop warning was opened for the occurrence.
    /// </summary>
    Stopped = 1,

    /// <summary>
    /// A start occurrence was skipped because the server was running and no stop of it was in flight.
    /// </summary>
    SkippedRunning = 2,

    /// <summary>
    /// A stop occurrence was skipped because the server was not running.
    /// </summary>
    SkippedOff = 3,

    /// <summary>
    /// A start occurrence was skipped because the cost owner could not pay for the session.
    /// </summary>
    SkippedCost = 4,

    /// <summary>
    /// A start occurrence was skipped because no valid start consent exists.
    /// </summary>
    SkippedConsent = 5,

    /// <summary>
    /// An occurrence was skipped because it fell outside the catch-up window, for example after an outage.
    /// </summary>
    SkippedMissed = 6,

    /// <summary>
    /// An occurrence was skipped because another lifecycle job that is not a stop or terminate was active for the
    /// server, or because the lifecycle service refused the start.
    /// </summary>
    SkippedBusy = 7,

    /// <summary>
    /// The queued job of the occurrence failed. The value is derived at read time and never stored by the evaluator.
    /// </summary>
    Failed = 8,

    /// <summary>
    /// A start occurrence came due while a stop or terminate of the same server was still being processed and waits
    /// for it to finish. It is the only outcome the runner later replaces, by a started job or by a skip.
    /// </summary>
    WaitingForStop = 9,

    /// <summary>
    /// A waiting start occurrence was closed without a start because the stop failed and waits for a person, because
    /// the rule's next occurrence came due, or because the rule changed before the stop finished. A start whose server
    /// still holds a failed stop is recorded the same way.
    /// </summary>
    SkippedStopUnfinished = 10,
}
