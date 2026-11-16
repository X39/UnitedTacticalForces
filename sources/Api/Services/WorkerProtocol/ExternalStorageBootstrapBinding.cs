using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Locates the device assignment in the renderer's Base64-encoded storage script.
/// </summary>
internal sealed class ExternalStorageBootstrapBinding
{
    /// <summary>The file where cloud-init writes the runtime storage preparation script.</summary>
    internal const string ScriptPath = "/usr/local/sbin/utf-prepare-runtime-storage";

    /// <summary>The complete device assignment emitted for an unbound script.</summary>
    private const string Assignment = "device='" + CloudInitWorkerBootstrapPayloadRenderer.ExternalBlockDeviceToken + "'";

    /// <summary>Recognizes the renderer's file entry independently of its encoding and content.</summary>
    private static readonly Regex ScriptEntry = new(
        $@"(?m)^  - path: {Regex.Escape(ScriptPath)}\r?$",
        RegexOptions.CultureInvariant);

    /// <summary>Locates the content within the renderer's file entry.</summary>
    private static readonly Regex ScriptContent = new(
        @"\G\n    owner: root:root\r?\n    permissions: '0700'\r?\n    encoding: b64\r?\n    content: ([A-Za-z0-9+/=]+)\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>Rejects malformed UTF-8 rather than replacing invalid bytes.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>The encoded content, excluding the surrounding YAML and line endings.</summary>
    private readonly Range _content;

    /// <summary>The decoded script with its original line endings.</summary>
    private readonly string _script;

    /// <summary>Tracks the single validated content span and its decoded script.</summary>
    /// <param name="content">The content span in the original cloud-init document.</param>
    /// <param name="script">The decoded script with its placeholder intact.</param>
    private ExternalStorageBootstrapBinding(Range content, string script)
    {
        _content = content;
        _script = script;
    }

    /// <summary>Validates the storage script in a cloud-init document produced by the worker renderer.</summary>
    /// <param name="cloudInit">The original cloud-init document.</param>
    /// <param name="binding">The validated script and its location, when found.</param>
    /// <returns>Whether the document contains a single valid storage-script binding.</returns>
    internal static bool TryRead(
        string cloudInit,
        [NotNullWhen(true)] out ExternalStorageBootstrapBinding? binding)
    {
        binding = null;
        var entries = ScriptEntry.Matches(cloudInit);
        if (entries.Count != 1)
            return false;

        var entry = entries[0];
        var match = ScriptContent.Match(cloudInit, entry.Index + entry.Length);
        if (!match.Success)
            return false;

        var content = match.Groups[1];
        string script;
        try
        {
            script = StrictUtf8.GetString(Convert.FromBase64String(content.Value));
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
        {
            return false;
        }

        const string token = CloudInitWorkerBootstrapPayloadRenderer.ExternalBlockDeviceToken;
        var tokenIndex = script.IndexOf(token, StringComparison.Ordinal);
        if (tokenIndex < 0
            || tokenIndex != script.LastIndexOf(token, StringComparison.Ordinal)
            || !script.Split('\n').Any(line => line.AsSpan().TrimEnd('\r').SequenceEqual(Assignment)))
        {
            return false;
        }

        binding = new ExternalStorageBootstrapBinding(content.Index..(content.Index + content.Length), script);
        return true;
    }

    /// <summary>Replaces the validated device assignment without rewriting unrelated cloud-init content.</summary>
    /// <param name="cloudInit">The same document passed to <see cref="TryRead"/>.</param>
    /// <param name="devicePath">The shell-safe stable device path derived from the attributed volume ID.</param>
    /// <returns>The cloud-init document containing the bound storage script.</returns>
    internal string Bind(string cloudInit, string devicePath)
    {
        var script = _script.Replace(Assignment, $"device='{devicePath}'", StringComparison.Ordinal);
        var content = Convert.ToBase64String(StrictUtf8.GetBytes(script));
        return string.Concat(cloudInit.AsSpan(0, _content.Start.Value), content, cloudInit.AsSpan(_content.End.Value));
    }
}
