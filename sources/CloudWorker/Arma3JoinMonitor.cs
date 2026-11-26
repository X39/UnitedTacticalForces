using System.Text;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Consumes fresh server-engine evidence (headless-client readiness and human player counts) without opening a network listener or trusting process liveness.</summary>
/// <param name="paths">Primary runtime and private worker-state paths.</param>
/// <param name="timeProvider">The monotonic clock used for evidence expiry.</param>
internal sealed class Arma3JoinMonitor(Arma3RuntimePaths paths, TimeProvider timeProvider)
{
    /// <summary>The bounded tail inspected per diagnostic file.</summary>
    private const int MaximumTailBytes = 64 * 1024;
    /// <summary>The time after which a player count whose sequence stopped advancing is no longer trusted.</summary>
    internal static readonly TimeSpan PlayerFreshness = TimeSpan.FromSeconds(90);
    /// <summary>Serializes readiness polling against heartbeat observations.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);
    /// <summary>The launch currently tracked in memory.</summary>
    private string? _nonce;
    /// <summary>The highest observed sequence; repeated file contents cannot renew health.</summary>
    private long _sequence;
    /// <summary>The monotonic time of the most recent new positive observation or recovery grace start.</summary>
    private long _lastReady;
    /// <summary>The launch whose player records are tracked in memory.</summary>
    private string? _playerNonce;
    /// <summary>The highest observed player record sequence of the tracked launch.</summary>
    private long _playerSequence;
    /// <summary>The human count of the highest observed player record, or null before the first record.</summary>
    private int? _playerHumans;
    /// <summary>The monotonic time at which the player sequence last advanced.</summary>
    private long _playerAdvancedAt;
    /// <summary>Gets the private state path, never included in server preservation.</summary>
    /// <value>The launch binding and successful verification record.</value>
    internal string SessionPath => Path.Combine(paths.AgentStatePath, "arma3-join-session.json");

    /// <summary>Installs fresh instrumentation only while preparing a stopped primary process.</summary>
    /// <param name="cancellationToken">Cancels packaging and durable publication.</param>
    /// <returns>The new launch's persisted session.</returns>
    internal async Task<Arma3JoinSession> PrepareAsync(CancellationToken cancellationToken)
    {
        var session = new Arma3JoinSession(Guid.NewGuid().ToString("N"));
        await Arma3JoinAddon.InstallAsync(paths, session.Nonce, cancellationToken).ConfigureAwait(false);
        await SaveAsync(session, cancellationToken).ConfigureAwait(false);
        return session;
    }

    /// <summary>Reads the current launch binding with strict validation.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>The session, or null when no instrumented launch was prepared.</returns>
    /// <exception cref="InvalidDataException">The persisted launch binding is malformed.</exception>
    internal async Task<Arma3JoinSession?> ReadSessionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SessionPath))
            return null;
        await using var stream = new FileStream(SessionPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > 4096)
            throw new InvalidDataException("Arma join session exceeds its size limit.");
        Arma3JoinSession? session;
        try
        {
            session = await JsonSerializer.DeserializeAsync<Arma3JoinSession>(stream, CloudWorkerJson.Options,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Arma join session is invalid.", ex);
        }
        if (session is null || session.Nonce is null || session.Nonce.Length != 32
            || session.Nonce.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException("Arma join session is invalid.");
        return session;
    }

    /// <summary>Atomically binds verified evidence to the observed primary incarnation.</summary>
    /// <param name="session">The prepared launch marker and optional verified process.</param>
    /// <param name="cancellationToken">Cancels publication.</param>
    /// <returns>The durable write operation.</returns>
    internal async Task SaveAsync(Arma3JoinSession session, CancellationToken cancellationToken)
    {
        using var temporary = await RestrictedTemporaryFile.CreateAsync(paths.AgentStatePath, "arma3-join", ".tmp",
            JsonSerializer.Serialize(session, CloudWorkerJson.Options), cancellationToken).ConfigureAwait(false);
        File.Move(temporary.Path, SessionPath, true);
    }

    /// <summary>Finds the highest complete current-launch record in bounded local output tails.</summary>
    /// <param name="nonce">The launch marker that rejects all previous server output.</param>
    /// <param name="cancellationToken">Cancels file reads.</param>
    /// <returns>The newest sequence, or null when instrumentation has not produced usable evidence.</returns>
    internal async Task<Arma3JoinEvidence?> ReadLatestAsync(string nonce, CancellationToken cancellationToken)
    {
        Arma3JoinEvidence? latest = null;
        foreach (var line in await ReadTailLinesAsync(cancellationToken).ConfigureAwait(false))
        {
            var evidence = Arma3JoinEvidence.Parse(line, nonce);
            if (evidence is not null && (latest is null || evidence.Sequence > latest.Sequence))
                latest = evidence;
        }
        return latest;
    }

    /// <summary>
    /// Reports the engine-observed human player count of the current launch. Headless clients and the server are
    /// excluded by the addon itself, so the count never includes them.
    /// </summary>
    /// <param name="nonce">The launch marker that rejects all previous server output.</param>
    /// <param name="cancellationToken">Cancels file reads.</param>
    /// <returns>
    /// The human count with no capacity while a newly advanced record was seen within
    /// <see cref="PlayerFreshness" />, query-failed once the sequence stopped advancing for longer, and measuring
    /// while no record of this launch exists.
    /// </returns>
    /// <remarks>
    /// A repeated old line cannot renew freshness: only a strictly higher sequence restarts the freshness window.
    /// The engine does not expose the configured slot count to the addon, so the capacity is always unknown.
    /// </remarks>
    internal async Task<WorkerPlayerObservation> ReadLatestPlayersAsync(
        string nonce, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Arma3PlayerEvidence? latest = null;
            foreach (var line in await ReadTailLinesAsync(cancellationToken).ConfigureAwait(false))
            {
                var evidence = Arma3PlayerEvidence.Parse(line, nonce);
                if (evidence is not null && (latest is null || evidence.Sequence > latest.Sequence))
                    latest = evidence;
            }
            if (_playerNonce != nonce)
            {
                _playerNonce = nonce;
                _playerSequence = 0;
                _playerHumans = null;
            }
            if (latest is not null && latest.Sequence > _playerSequence)
            {
                _playerSequence = latest.Sequence;
                _playerHumans = latest.Humans;
                _playerAdvancedAt = timeProvider.GetTimestamp();
            }
            if (_playerHumans is not { } humans)
                return WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.Measuring);
            return timeProvider.GetElapsedTime(_playerAdvancedAt) > PlayerFreshness
                ? WorkerPlayerObservation.Unavailable(WorkerMetricUnavailableReasons.QueryFailed)
                : WorkerPlayerObservation.Players(humans, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Reads the complete lines of the bounded tail of every candidate diagnostic file.</summary>
    /// <param name="cancellationToken">Cancels file reads.</param>
    /// <returns>The terminated lines without their line endings, in file order.</returns>
    private async Task<IReadOnlyList<string>> ReadTailLinesAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<string> { paths.RuntimeLogPath };
        if (OperatingSystem.IsWindows() && Directory.Exists(paths.ProfilePath))
            candidates.AddRange(Directory.EnumerateFiles(paths.ProfilePath, "*.rpt", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc).Take(4));
        var result = new List<string>();
        foreach (var path in candidates)
        {
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                continue;
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            var offset = Math.Max(0, stream.Length - MaximumTailBytes);
            stream.Seek(offset, SeekOrigin.Begin);
            var bytes = new byte[MaximumTailBytes];
            var length = await stream.ReadAtLeastAsync(bytes, Math.Min(bytes.Length, (int)(stream.Length - offset)),
                throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            var lines = Encoding.UTF8.GetString(bytes, 0, length).Split('\n');
            // Ignore an initial truncated line and any final line not yet terminated by the engine.
            for (var index = offset == 0 ? 0 : 1; index < lines.Length - 1; index++)
                result.Add(lines[index].TrimEnd('\r'));
        }
        return result;
    }

    /// <summary>Reports expired evidence only after readiness was armed for this exact primary incarnation.</summary>
    /// <param name="identity">The currently observed process, not a saved PID alone.</param>
    /// <param name="grace">Allowed evidence silence, including a bounded worker recovery grace.</param>
    /// <param name="cancellationToken">Cancels observation.</param>
    /// <returns>Whether an established readiness check has lost fresh positive evidence.</returns>
    internal async Task<bool> HasExpiredAsync(Arma3ProcessIdentity identity, TimeSpan grace,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await ReadSessionAsync(cancellationToken).ConfigureAwait(false);
            if (session?.VerifiedProcessId != identity.ProcessId || session.VerifiedStartedAt != identity.ProcessStartedAt)
                return false;
            var latest = await ReadLatestAsync(session.Nonce, cancellationToken).ConfigureAwait(false);
            if (_nonce != session.Nonce)
            {
                _nonce = session.Nonce;
                _sequence = latest?.Sequence ?? 0;
                _lastReady = timeProvider.GetTimestamp();
            }
            else if (latest is not null && latest.Sequence > _sequence)
            {
                _sequence = latest.Sequence;
                if (latest.Ready)
                    _lastReady = timeProvider.GetTimestamp();
            }
            return timeProvider.GetElapsedTime(_lastReady) >= grace;
        }
        finally
        {
            _gate.Release();
        }
    }
}
