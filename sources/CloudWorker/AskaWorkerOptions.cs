namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Defines paths and timing controls required by the ASKA Wine runtime.</summary>
/// <param name="ExecutableRelativePath">Relative path to the ASKA server executable.</param>
/// <param name="WinePath">Absolute path to the Wine executable.</param>
/// <param name="WineBootPath">Absolute path to the Wine prefix initializer.</param>
/// <param name="XvfbRunPath">Absolute path to the isolated X server launcher.</param>
/// <param name="SetSidPath">Absolute path to the process-session launcher.</param>
/// <param name="ReadinessTimeout">Maximum time allowed for ASKA startup readiness.</param>
public sealed record AskaWorkerOptions(
    string ExecutableRelativePath,
    string WinePath,
    string WineBootPath,
    string XvfbRunPath,
    string SetSidPath,
    TimeSpan ReadinessTimeout)
{
    /// <summary>Creates validated ASKA options from worker arguments and environment variables.</summary>
    /// <param name="values">Parsed worker command-line values.</param>
    /// <param name="getEnvironmentVariable">Environment variable resolver.</param>
    /// <returns>The validated ASKA options.</returns>
    internal static AskaWorkerOptions Create(
        IReadOnlyDictionary<string, string> values,
        Func<string, string?> getEnvironmentVariable)
    {
        var executable = Get(values, "aska-executable", "UTF_ASKA_EXECUTABLE", "AskaServer.exe", getEnvironmentVariable);
        if (Path.IsPathFullyQualified(executable) || executable.Split('/', '\\').Any(q => q is "" or "." or ".."))
            throw new InvalidOperationException("The ASKA executable must be a safe relative path.");

        var timeoutValue = Get(values, "aska-readiness-seconds", "UTF_ASKA_READINESS_SECONDS", "180", getEnvironmentVariable);
        if (!double.TryParse(timeoutValue, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var timeoutSeconds)
            || timeoutSeconds is < 10 or > 900)
            throw new InvalidOperationException("The ASKA readiness timeout must be between 10 and 900 seconds.");

        return new AskaWorkerOptions(
            executable,
            Absolute(Get(values, "aska-wine", "UTF_ASKA_WINE_PATH", "/usr/bin/wine", getEnvironmentVariable)),
            Absolute(Get(values, "aska-wineboot", "UTF_ASKA_WINEBOOT_PATH", "/usr/bin/wineboot", getEnvironmentVariable)),
            Absolute(Get(values, "aska-xvfb-run", "UTF_ASKA_XVFB_RUN_PATH", "/usr/bin/xvfb-run", getEnvironmentVariable)),
            Absolute(Get(values, "aska-setsid", "UTF_ASKA_SETSID_PATH", "/usr/bin/setsid", getEnvironmentVariable)),
            TimeSpan.FromSeconds(timeoutSeconds));
    }

    private static string Get(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentName,
        string fallback,
        Func<string, string?> getEnvironmentVariable)
        => values.GetValueOrDefault(key) ?? getEnvironmentVariable(environmentName) ?? fallback;

    private static string Absolute(string path)
        => Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : throw new InvalidOperationException("ASKA tooling paths must be absolute.");
}
