using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Exercises engine evidence parsing, bounded file reads and monotonic recovery deadlines.</summary>
public sealed class Arma3JoinMonitorTests
{
    /// <summary>The canonical launch marker used only by these isolated evidence fixtures.</summary>
    private const string Nonce = "0123456789abcdef0123456789abcdef";

    /// <summary>Accepts both raw engine output and timestamped report records.</summary>
    /// <param name="decoration">The valid prefix and suffix fixture.</param>
    [Theory]
    [InlineData("{0}")]
    [InlineData("  9:42:03.123 {0}  ")]
    [InlineData("12:42:03 \"{0}\"")]
    [InlineData("2026/11/16, 12:42:03 {0}")]
    public void Parse_AcceptsCurrentPositiveAndNegativeEngineRecords(string decoration)
    {
        var positive = Arma3JoinEvidence.Parse(string.Format(decoration, Record(42, 17, 1)), Nonce);
        Assert.Equal(new Arma3JoinEvidence(42, 17, true), positive);
        var negative = Arma3JoinEvidence.Parse(string.Format(decoration, Record(43, 0, 0)), Nonce);
        Assert.Equal(new Arma3JoinEvidence(43, 0, false), negative);
    }

    /// <summary>Rejects malformed roles, counters and decoration rather than promoting ambiguous text.</summary>
    /// <param name="record">A record template with an unsafe protocol field or decoration.</param>
    [Theory]
    [InlineData("UTFHC|2|{0}|1|3|1")]
    [InlineData("UTFHC|1|{0}|0|3|1")]
    [InlineData("UTFHC|1|{0}|-1|3|1")]
    [InlineData("UTFHC|1|{0}|1.0|3|1")]
    [InlineData("UTFHC|1|{0}|10000000000000000|3|1")]
    [InlineData("UTFHC|1|{0}|1|2147483648|1")]
    [InlineData("UTFHC|1|{0}|1|-3|1")]
    [InlineData("UTFHC|1|{0}|1|0|1")]
    [InlineData("UTFHC|1|{0}|1|1|1")]
    [InlineData("UTFHC|1|{0}|1|2|1")]
    [InlineData("UTFHC|1|{0}|1|3|0")]
    [InlineData("UTFHC|1|{0}|1|3|2")]
    [InlineData("UTFHC|1|{0}|1|3|")]
    [InlineData("UTFHC|1|{0}|1|3|1|extra")]
    [InlineData("player chat: UTFHC|1|{0}|1|3|1")]
    [InlineData("\"UTFHC|1|{0}|1|3|1")]
    [InlineData("UTFHC|1|{0}|1|3|1\"")]
    public void Parse_RejectsMalformedRecords(string record)
        => Assert.Null(Arma3JoinEvidence.Parse(string.Format(record, Nonce), Nonce));

    /// <summary>Bounds parser input and requires the exact current launch marker.</summary>
    [Fact]
    public void Parse_RejectsOldLaunchAndOversizedInput()
    {
        Assert.Null(Arma3JoinEvidence.Parse(Record(1, 3, 1), new string('f', 32)));
        Assert.Null(Arma3JoinEvidence.Parse(Record(1, 3, 1).Replace(Nonce, Nonce.ToUpperInvariant()), Nonce));
        Assert.Null(Arma3JoinEvidence.Parse(new string(' ', 257) + Record(1, 3, 1), Nonce));
    }

    /// <summary>Ignores unterminated writes and stale launches, while selecting the highest complete sequence.</summary>
    /// <returns>A task completing after bounded diagnostic reads.</returns>
    [Fact]
    public async Task ReadLatest_IgnoresPartialOldAndDuplicateRecords()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var monitor = new Arma3JoinMonitor(paths, TimeProvider.System);
        await File.WriteAllTextAsync(paths.RuntimeLogPath,
            Record(5, 3, 1) + "\r\n" + Record(5, 3, 1) + "\n"
            + Record(100, 3, 1).Replace(Nonce, new string('f', 32)) + "\n"
            + Record(6, 0, 0) + "\n" + Record(7, 3, 1));
        Assert.Equal(new Arma3JoinEvidence(6, 0, false), await monitor.ReadLatestAsync(Nonce, CancellationToken.None));
        await File.AppendAllTextAsync(paths.RuntimeLogPath, "\n");
        Assert.Equal(new Arma3JoinEvidence(7, 3, true), await monitor.ReadLatestAsync(Nonce, CancellationToken.None));
    }

    /// <summary>Records outside the bounded tail and fragments at its leading boundary cannot count as evidence.</summary>
    /// <returns>A task completing after the large-file boundary checks.</returns>
    [Fact]
    public async Task ReadLatest_OnlyAcceptsCompleteLinesWithinBoundedTail()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var monitor = new Arma3JoinMonitor(paths, TimeProvider.System);
        await File.WriteAllTextAsync(paths.RuntimeLogPath, Record(900, 3, 1) + "\n" + new string('x', 70_000)
                                                         + "\n" + Record(4, 3, 1) + "\n");
        Assert.Equal(new Arma3JoinEvidence(4, 3, true), await monitor.ReadLatestAsync(Nonce, CancellationToken.None));
        // Put a valid-looking record exactly at the tail boundary inside an older unterminated line.
        var fragment = Record(901, 3, 1) + "\n";
        await File.WriteAllTextAsync(paths.RuntimeLogPath, "prefix" + fragment
            + new string('x', 65_536 - fragment.Length - 1) + "\n");
        Assert.Null(await monitor.ReadLatestAsync(Nonce, CancellationToken.None));
    }

    /// <summary>A reconstructed worker needs advancing positive samples; rereading old output cannot extend grace.</summary>
    /// <returns>A task completing after the monotonic expiry checks.</returns>
    [Fact]
    public async Task HasExpired_ReconstructionRequiresAdvancingPositiveEvidence()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var clock = new ManualClock();
        var identity = CreateIdentity(paths);
        var original = new Arma3JoinMonitor(paths, clock);
        await original.SaveAsync(new Arma3JoinSession(Nonce, identity.ProcessId, identity.ProcessStartedAt), CancellationToken.None);
        await File.WriteAllTextAsync(paths.RuntimeLogPath, Record(10, 3, 1) + "\n");
        var recovered = new Arma3JoinMonitor(paths, clock);
        var grace = TimeSpan.FromSeconds(30);
        Assert.False(await recovered.HasExpiredAsync(identity, grace, CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(await recovered.HasExpiredAsync(identity, grace, CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await recovered.HasExpiredAsync(identity, grace, CancellationToken.None));
        await File.AppendAllTextAsync(paths.RuntimeLogPath, Record(11, 3, 1) + "\n");
        Assert.False(await recovered.HasExpiredAsync(identity, grace, CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(await recovered.HasExpiredAsync(identity, grace, CancellationToken.None));
    }

    /// <summary>New negative observations and duplicate positive records cannot refresh established health.</summary>
    /// <returns>A task completing after negative and duplicate sequence checks.</returns>
    [Fact]
    public async Task HasExpired_NegativeAndRepeatedEvidenceDoNotRenewReadiness()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var clock = new ManualClock();
        var identity = CreateIdentity(paths);
        var monitor = new Arma3JoinMonitor(paths, clock);
        await monitor.SaveAsync(new Arma3JoinSession(Nonce, identity.ProcessId, identity.ProcessStartedAt), CancellationToken.None);
        var grace = TimeSpan.FromSeconds(30);
        Assert.False(await monitor.HasExpiredAsync(identity, grace, CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(20));
        await File.WriteAllTextAsync(paths.RuntimeLogPath, Record(1, 3, 1) + "\n");
        Assert.False(await monitor.HasExpiredAsync(identity, grace, CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(20));
        await File.AppendAllTextAsync(paths.RuntimeLogPath, Record(2, 0, 0) + "\n");
        Assert.False(await monitor.HasExpiredAsync(identity, grace, CancellationToken.None));
        await File.AppendAllTextAsync(paths.RuntimeLogPath, Record(1, 3, 1) + "\n");
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(await monitor.HasExpiredAsync(identity, grace, CancellationToken.None));
    }

    /// <summary>No saved verification or a different PID incarnation leaves this monitor unarmed.</summary>
    /// <returns>A task completing after process attribution checks.</returns>
    [Fact]
    public async Task HasExpired_MissingVerificationAndProcessMismatchRemainUnarmed()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var clock = new ManualClock();
        var identity = CreateIdentity(paths);
        var monitor = new Arma3JoinMonitor(paths, clock);
        var grace = TimeSpan.Zero;
        Assert.False(await monitor.HasExpiredAsync(identity, grace, CancellationToken.None));
        await monitor.SaveAsync(new Arma3JoinSession(Nonce), CancellationToken.None);
        Assert.False(await monitor.HasExpiredAsync(identity, grace, CancellationToken.None));
        await monitor.SaveAsync(new Arma3JoinSession(Nonce, identity.ProcessId + 1, identity.ProcessStartedAt), CancellationToken.None);
        Assert.False(await monitor.HasExpiredAsync(identity, grace, CancellationToken.None));
        await monitor.SaveAsync(new Arma3JoinSession(Nonce, identity.ProcessId, identity.ProcessStartedAt.AddSeconds(-1)), CancellationToken.None);
        Assert.False(await monitor.HasExpiredAsync(identity, grace, CancellationToken.None));
    }

    /// <summary>Creates isolated workload directories without starting an executable.</summary>
    /// <param name="root">The fixture's temporary directory.</param>
    /// <returns>Created primary runtime paths.</returns>
    private static Arma3RuntimePaths CreatePaths(string root)
    {
        var paths = new Arma3RuntimePaths(Path.Combine(root, "runtime"), Path.Combine(root, "state"), Guid.NewGuid(), "arma3server_x64");
        paths.EnsureDirectories();
        return paths;
    }

    /// <summary>Provides a synthetic observed incarnation for the monitor's identity comparison contract.</summary>
    /// <param name="paths">The primary runtime paths.</param>
    /// <returns>A process identity that does not require any process creation.</returns>
    private static Arma3ProcessIdentity CreateIdentity(Arma3RuntimePaths paths)
        => new(Guid.NewGuid(), Guid.NewGuid(), 4321, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), paths.ExecutablePath);

    /// <summary>Formats trusted protocol fields without adding a terminating newline.</summary>
    /// <param name="sequence">The increasing engine sequence.</param>
    /// <param name="owner">The engine network owner.</param>
    /// <param name="state">The readiness bit.</param>
    /// <returns>A raw addon record for the current fixture launch.</returns>
    private static string Record(long sequence, int owner, int state)
        => $"UTFHC|1|{Nonce}|{sequence}|{owner}|{state}";

    /// <summary>Advances monotonic time explicitly so expiry tests need no sleeping or wall-clock assumptions.</summary>
    private sealed class ManualClock : TimeProvider
    {
        /// <summary>The current monotonic timestamp in ticks.</summary>
        private long _timestamp;

        /// <inheritdoc />
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        /// <inheritdoc />
        public override long GetTimestamp() => _timestamp;

        /// <summary>Moves the test clock forward by the requested interval.</summary>
        /// <param name="duration">The elapsed interval.</param>
        internal void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
    }
}
