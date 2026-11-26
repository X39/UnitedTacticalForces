namespace CloudWorker.Tests;

/// <summary>
/// A time provider whose timers fire only when the test advances time, so backoff delays can be asserted exactly.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
            return _now;
    }

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (_gate)
            return _now.UtcTicks;
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (_gate)
            _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>
    /// Waits until a timer with exactly the expected remaining time is armed.
    /// </summary>
    /// <param name="expected">The remaining time to wait for.</param>
    /// <returns>A task that completes when such a timer exists.</returns>
    /// <exception cref="TimeoutException">No timer with that remaining time was armed within 10 seconds.</exception>
    public async Task WaitForTimerAsync(TimeSpan expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            lock (_gate)
            {
                if (_timers.Any(q => q.Due is { } due && due - _now == expected))
                    return;
            }
            await Task.Delay(5).ConfigureAwait(false);
        }
        throw new TimeoutException($"No timer with remaining time {expected} was armed.");
    }

    /// <summary>
    /// Advances the clock and fires every timer that became due.
    /// </summary>
    /// <param name="by">The amount to advance.</param>
    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _now += by;
            due = _timers.Where(q => q.Due is { } d && d <= _now).ToList();
            foreach (var timer in due)
                timer.Disarm();
        }
        foreach (var timer in due)
            timer.Fire();
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
            _timers.Remove(timer);
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
            return true;
        }

        public void Disarm() => Due = null;

        public void Fire() => callback(state);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
