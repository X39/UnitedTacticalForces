using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// One player count reading of a running game server: either a measured count or exactly one reason why no count
/// is available. An unmeasured count is never represented as zero.
/// </summary>
/// <param name="Count">The measured number of human players, or <see langword="null" /> when unavailable.</param>
/// <param name="Capacity">The maximum player capacity when the game reports it, otherwise <see langword="null" />.</param>
/// <param name="UnavailableReason">
/// The reason from <see cref="WorkerMetricUnavailableReasons.WorkerPlayerReasons" /> when no count is available,
/// otherwise <see langword="null" />.
/// </param>
public sealed record WorkerPlayerObservation(int? Count, int? Capacity, string? UnavailableReason)
{
    /// <summary>The largest player count or capacity the worker accepts as a measured value.</summary>
    public const int MaxPlayers = 1024;

    /// <summary>Creates a measured reading.</summary>
    /// <param name="count">The number of human players, from 0 to <see cref="MaxPlayers" />.</param>
    /// <param name="capacity">The reported maximum capacity, from 0 to <see cref="MaxPlayers" />, or null.</param>
    /// <returns>A reading that carries a count and no reason.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The count or capacity is outside the accepted range.</exception>
    public static WorkerPlayerObservation Players(int count, int? capacity)
    {
        if (count is < 0 or > MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(count), count, "The player count is outside 0 to 1024.");
        if (capacity is < 0 or > MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "The capacity is outside 0 to 1024.");
        return new WorkerPlayerObservation(count, capacity, null);
    }

    /// <summary>Creates a reading that explains why no count is available.</summary>
    /// <param name="reason">One of <see cref="WorkerMetricUnavailableReasons.WorkerPlayerReasons" />.</param>
    /// <returns>A reading that carries a reason and no count.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The reason is not a reason a worker may send for players.</exception>
    public static WorkerPlayerObservation Unavailable(string reason)
    {
        if (!WorkerMetricUnavailableReasons.WorkerPlayerReasons.Contains(reason))
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "The reason is not valid for players.");
        return new WorkerPlayerObservation(null, null, reason);
    }
}
