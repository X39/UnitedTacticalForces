using System.Globalization;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Native Arma 3 tooling configuration supplied by the worker image or secret mounts.
/// </summary>
public sealed record Arma3WorkerOptions(
    string SteamCmdPath,
    string SteamUsername,
    string? SteamPasswordFile,
    string? Branch,
    string ExecutableName,
    TimeSpan GracefulStopTimeout,
    int? GameUserId = null,
    int? GameGroupId = null,
    string? PrivilegeDropPath = null,
    int? SteamUserId = null)
{
    public string TemporaryDirectory { get; init; } = "/tmp/united-tactical-forces";

    internal static Arma3WorkerOptions Create(
        IReadOnlyDictionary<string, string> values,
        Func<string, string?> getEnvironmentVariable)
        => new(
            Get(values, "steamcmd", "UTF_STEAMCMD_PATH", "/opt/steamcmd/steamcmd.sh", getEnvironmentVariable),
            Get(values, "steam-username", "UTF_STEAM_USERNAME", "anonymous", getEnvironmentVariable),
            GetOptional(values, "steam-password-file", "UTF_STEAM_PASSWORD_FILE", getEnvironmentVariable),
            GetOptional(values, "arma3-branch", "UTF_ARMA3_BRANCH", getEnvironmentVariable),
            Get(values, "arma3-executable", "UTF_ARMA3_EXECUTABLE", "arma3server_x64", getEnvironmentVariable),
            TimeSpan.FromSeconds(ParsePositiveInt(getEnvironmentVariable("UTF_ARMA3_STOP_TIMEOUT_SECONDS"), 30)),
            ParseOptionalPositiveInt(getEnvironmentVariable("UTF_GAME_UID")),
            ParseOptionalPositiveInt(getEnvironmentVariable("UTF_GAME_GID")),
            GetOptional(values, "game-privilege-drop", "UTF_GAME_PRIVILEGE_DROP_PATH", getEnvironmentVariable),
            ParseOptionalPositiveInt(getEnvironmentVariable("UTF_STEAM_UID")))
        {
            TemporaryDirectory = Get(
                values,
                "temporary-directory",
                "UTF_WORKER_TEMP_DIRECTORY",
                "/tmp/united-tactical-forces",
                getEnvironmentVariable),
        };

    public async Task<string?> ReadSteamPasswordAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SteamPasswordFile))
            return null;
        var fullPath = Path.GetFullPath(SteamPasswordFile);
        RestrictedFilePermissions.EnsureOwnerOnlyReadable(fullPath, "Steam password secret");
        var password = await File.ReadAllTextAsync(fullPath, cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(password) ? null : password.Trim();
    }

    private static int ParsePositiveInt(string? value, int fallback)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : fallback;

    private static int? ParseOptionalPositiveInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;

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

    private static string? GetOptional(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentName,
        Func<string, string?> getEnvironmentVariable)
        => values.TryGetValue(key, out var value) ? value : getEnvironmentVariable(environmentName);
}