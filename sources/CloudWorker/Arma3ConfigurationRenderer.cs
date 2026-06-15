using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.CloudWorker;

public sealed partial class Arma3ConfigurationRenderer
{
    public async Task RenderAsync(
        Arma3CommandPayload payload,
        Arma3RuntimePaths paths,
        CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        await WriteArmaAsync(paths.ServerConfigurationPath, payload.ServerConfiguration, cancellationToken)
            .ConfigureAwait(false);
        await WriteArmaAsync(paths.BasicConfigurationPath, payload.BasicConfiguration, cancellationToken)
            .ConfigureAwait(false);
        await WriteBattlEyeAsync(paths.BattlEyeConfigurationPath, payload.BattlEyeConfiguration, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteArmaAsync(
        string path,
        IReadOnlyDictionary<string, JsonElement> values,
        CancellationToken cancellationToken)
    {
        var lines = values
            .OrderBy(q => q.Key, StringComparer.Ordinal)
            .Select(q => $"{ValidateKey(q.Key)} = {FormatValue(q.Value)};");
        await WriteRestrictedAtomicAsync(path, string.Join(Environment.NewLine, lines) + Environment.NewLine, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteBattlEyeAsync(
        string path,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var lines = values
            .OrderBy(q => q.Key, StringComparer.Ordinal)
            .Select(q => $"{ValidateKey(q.Key)} {SingleLine(q.Value)}");
        await WriteRestrictedAtomicAsync(path, string.Join(Environment.NewLine, lines) + Environment.NewLine, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteRestrictedAtomicAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        using var temporary = await RestrictedTemporaryFile
            .CreateAsync(directory, Path.GetFileName(path), ".tmp", content, cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary.Path, path, true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupWrite);
        }
    }

    private static string ValidateKey(string key)
        => ConfigKeyPattern().IsMatch(key)
            ? key
            : throw new InvalidDataException("Arma configuration contains an invalid key.");

    private static string FormatValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => Quote(value.GetString() ?? string.Empty),
            JsonValueKind.Array => $"{{{string.Join(',', value.EnumerateArray().Select(FormatValue))}}}",
            JsonValueKind.Null => Quote(string.Empty),
            _ => throw new InvalidDataException("Arma configuration value type is not supported."),
        };

    private static string Quote(string value)
        => $"\"{SingleLine(value).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string SingleLine(string value)
        => value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.\\[\\]-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ConfigKeyPattern();
}
