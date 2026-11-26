using System.Globalization;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Process-local paths and timing controls for the dedicated external worker.
/// </summary>
public sealed record CloudWorkerOptions(
    string BootstrapPath,
    string StateDirectory,
    string RuntimeRoot,
    TimeSpan PollInterval,
    TimeSpan HeartbeatInterval,
    bool ClaimOnce,
    bool AllowInsecureHttp,
    Arma3WorkerOptions Arma3)
{
    /// <summary>
    /// Gets how often the primary host worker sends one metrics sample. Set with <c>--metrics-seconds</c> or
    /// <c>UTF_WORKER_METRICS_SECONDS</c>.
    /// </summary>
    /// <value>The sample interval; 15 seconds by default.</value>
    public TimeSpan MetricsInterval { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets constrained public Git acquisition options.</summary>
    public GitContentOptions Git { get; init; } = new(
        "git",
        new HashSet<string>(["github.com"], StringComparer.OrdinalIgnoreCase),
        512L * 1024 * 1024,
        TimeSpan.FromMinutes(5));

    public CounterStrike2WorkerOptions CounterStrike2 { get; init; } = new("game/bin/linuxsteamrt64/cs2");
    /// <summary>Gets ASKA-specific Wine runtime options.</summary>
    public AskaWorkerOptions Aska { get; init; } = new(
        "AskaServer.exe",
        "/usr/bin/wine",
        "/usr/bin/wineboot",
        "/usr/bin/xvfb-run",
        "/usr/bin/setsid",
        TimeSpan.FromMinutes(3));
    public SteamCmdWorkerOptions SteamCmd => new(
        Arma3.SteamCmdPath,
        Arma3.TemporaryDirectory,
        Arma3.SteamHomeDirectory,
        Arma3.GameUserId,
        Arma3.GameGroupId,
        Arma3.PrivilegeDropPath,
        Arma3.SteamUserId);
    public const string DefaultBootstrapPath = "/run/secrets/utf-worker-bootstrap.json";
    public const string DefaultStateDirectory = "/var/lib/united-tactical-forces/worker";
    public const string DefaultRuntimeRoot = "/srv/united-tactical-forces/runtime";

    public static CloudWorkerOptions FromArgs(IReadOnlyList<string> args)
        => FromArgs(args, Environment.GetEnvironmentVariable);

    internal static CloudWorkerOptions FromArgs(
        IReadOnlyList<string> args,
        Func<string, string?> getEnvironmentVariable)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var claimOnce = IsTrue(getEnvironmentVariable("UTF_WORKER_CLAIM_ONCE"));
        var allowInsecureHttp = IsTrue(getEnvironmentVariable("UTF_WORKER_ALLOW_INSECURE_HTTP"));
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (arg is "--claim-once")
            {
                claimOnce = true;
                continue;
            }
            if (arg is "--allow-insecure-http")
            {
                allowInsecureHttp = true;
                continue;
            }
            if (!arg.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count)
                continue;
            values[arg[2..]] = args[++index];
        }

        return new CloudWorkerOptions(
            Get(values, "bootstrap", "UTF_WORKER_BOOTSTRAP_PATH", DefaultBootstrapPath, getEnvironmentVariable),
            Get(values, "state-dir", "UTF_WORKER_STATE_DIRECTORY", DefaultStateDirectory, getEnvironmentVariable),
            Get(values, "runtime-root", "UTF_WORKER_RUNTIME_ROOT", DefaultRuntimeRoot, getEnvironmentVariable),
            GetDuration(values, "poll-seconds", "UTF_WORKER_POLL_SECONDS", TimeSpan.FromSeconds(5), getEnvironmentVariable),
            GetDuration(
                values,
                "heartbeat-seconds",
                "UTF_WORKER_HEARTBEAT_SECONDS",
                TimeSpan.FromSeconds(15),
                getEnvironmentVariable),
            claimOnce,
            allowInsecureHttp,
            Arma3WorkerOptions.Create(values, getEnvironmentVariable))
        {
            CounterStrike2 = CounterStrike2WorkerOptions.Create(values, getEnvironmentVariable),
            Aska = AskaWorkerOptions.Create(values, getEnvironmentVariable),
            Git = GitContentOptions.Create(values, getEnvironmentVariable),
            MetricsInterval = GetDuration(
                values,
                "metrics-seconds",
                "UTF_WORKER_METRICS_SECONDS",
                TimeSpan.FromSeconds(15),
                getEnvironmentVariable),
        };
    }

    private static string Get(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentName,
        string fallback,
        Func<string, string?> getEnvironmentVariable)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : getEnvironmentVariable(environmentName) is { Length: > 0 } environmentValue
                ? environmentValue
                : fallback;

    private static TimeSpan GetDuration(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentName,
        TimeSpan fallback,
        Func<string, string?> getEnvironmentVariable)
    {
        var raw = values.TryGetValue(key, out var value) ? value : getEnvironmentVariable(environmentName);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : fallback;
    }

    private static bool IsTrue(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}
