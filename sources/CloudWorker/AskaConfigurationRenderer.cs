using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Creates ASKA's runtime properties file while keeping mutable save identity separate from secrets.</summary>
public sealed partial class AskaConfigurationRenderer
{
    /// <summary>Writes the token-bearing runtime properties file with restricted permissions.</summary>
    /// <param name="paths">Deployment-specific ASKA paths.</param>
    /// <param name="configuration">Resolved public configuration values.</param>
    /// <param name="secrets">Ephemeral deployment secrets.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the write.</returns>
    /// <exception cref="InvalidDataException">Thrown when a secret or saved identifier is unsafe.</exception>
    public async Task RenderAsync(
        AskaRuntimePaths paths,
        IReadOnlyDictionary<string, JsonElement> configuration,
        IReadOnlyDictionary<string, string> secrets,
        CancellationToken cancellationToken)
    {
        if (!secrets.TryGetValue("authentication-token", out var token) || string.IsNullOrWhiteSpace(token))
            throw new InvalidDataException("Required ASKA secret 'authentication-token' is unavailable.");
        token = ValidateValue(token, "authentication token", 512);
        var password = secrets.TryGetValue("join-password", out var configuredPassword)
            ? ValidateValue(configuredPassword, "join password", 256)
            : string.Empty;
        var saveId = await ReadSanitizedSaveIdAsync(paths, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        var rendered = GameConfigurationImplementations.Get(AskaConfigurationImplementation.Identifier)
            .Render(configuration)
            .Single();
        var publicSettings = Encoding.UTF8.GetString(rendered.Content);
        var content = $"save id = {saveId}\n{publicSettings}password = {password}\n"
                      + "steam game port = 27015\nsteam query port = 27016\n"
                      + $"authentication token = {token}\ncustom game preset path =\n";

        paths.EnsureDirectories();
        using var temporary = await RestrictedTemporaryFile.CreateAsync(
            paths.RuntimeConfigurationRoot, ".utf-aska-properties", ".tmp", content, cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, paths.PropertiesPath, true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                paths.PropertiesPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        }
    }

    /// <summary>Copies ASKA's generated save identifier into a secret-free preserved state file.</summary>
    /// <param name="paths">Deployment-specific ASKA paths.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing synchronization.</returns>
    public async Task SynchronizeSaveIdAsync(AskaRuntimePaths paths, CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.PropertiesPath))
            return;
        var text = await File.ReadAllTextAsync(paths.PropertiesPath, cancellationToken).ConfigureAwait(false);
        var match = SaveIdLinePattern().Match(text);
        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
            return;
        var saveId = ValidateSaveId(match.Groups[1].Value.Trim());
        paths.EnsureDirectories();
        using var temporary = await RestrictedTemporaryFile.CreateAsync(
            paths.PersistentStateRoot, ".utf-aska-save-id", ".tmp", saveId + "\n", cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, paths.SaveIdPath, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(paths.SaveIdPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
    }

    /// <summary>Deletes the ephemeral token-bearing properties file after the game stops.</summary>
    /// <param name="paths">Deployment-specific ASKA paths.</param>
    public void DeleteRuntimeConfiguration(AskaRuntimePaths paths)
    {
        if (File.Exists(paths.PropertiesPath))
            File.Delete(paths.PropertiesPath);
    }

    private static async Task<string?> ReadSanitizedSaveIdAsync(
        AskaRuntimePaths paths,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.SaveIdPath))
            return null;
        return ValidateSaveId((await File.ReadAllTextAsync(paths.SaveIdPath, cancellationToken)
            .ConfigureAwait(false)).Trim());
    }

    private static string ValidateSaveId(string value)
        => SaveIdPattern().IsMatch(value)
            ? value
            : throw new InvalidDataException("The preserved ASKA save identifier is invalid.");

    private static string ValidateValue(string value, string name, int maximumLength)
        => value.Length <= maximumLength && !value.Contains('\r') && !value.Contains('\n') && !value.Contains('=')
            ? value
            : throw new InvalidDataException($"The ASKA {name} contains unsupported characters or is too long.");

    [GeneratedRegex("^save id[ \\t]*=[ \\t]*([^\\r\\n]*?)[ \\t]*\\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SaveIdLinePattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex SaveIdPattern();
}
