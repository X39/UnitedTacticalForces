namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Constrains public Git acquisition performed by a dedicated worker.</summary>
/// <param name="ExecutablePath">Path to the Git executable.</param>
/// <param name="AllowedHosts">Explicitly allowed public repository hosts.</param>
/// <param name="MaximumRepositoryBytes">Maximum checked-out repository size.</param>
/// <param name="CommandTimeout">Maximum duration of each Git command.</param>
public sealed record GitContentOptions(
    string ExecutablePath,
    IReadOnlySet<string> AllowedHosts,
    long MaximumRepositoryBytes,
    TimeSpan CommandTimeout)
{
    /// <summary>Creates Git acquisition options from worker arguments and environment variables.</summary>
    /// <param name="values">Parsed worker command-line values.</param>
    /// <param name="getEnvironmentVariable">Environment variable accessor.</param>
    /// <returns>Validated Git acquisition options.</returns>
    public static GitContentOptions Create(
        IReadOnlyDictionary<string, string> values,
        Func<string, string?> getEnvironmentVariable)
    {
        var executable = Get(values, "git-path", "UTF_WORKER_GIT_PATH", "git", getEnvironmentVariable);
        var hosts = Get(values, "git-allowed-hosts", "UTF_WORKER_GIT_ALLOWED_HOSTS", "github.com", getEnvironmentVariable)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(host => host.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var maximumBytes = long.TryParse(
            Get(values, "git-maximum-bytes", "UTF_WORKER_GIT_MAXIMUM_BYTES", "536870912", getEnvironmentVariable),
            out var parsedBytes) && parsedBytes is > 0 and <= 2L * 1024 * 1024 * 1024
                ? parsedBytes
                : 512L * 1024 * 1024;
        return new GitContentOptions(executable, hosts, maximumBytes, TimeSpan.FromMinutes(5));
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
}
