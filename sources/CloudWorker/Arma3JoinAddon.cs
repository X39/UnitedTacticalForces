using System.Security.Cryptography;
using System.Text;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Packages the worker-owned server addon that reports engine-observed Headless Client readiness.</summary>
internal static class Arma3JoinAddon
{
    /// <summary>Resolves the private addon independently of user-selected or preserved mod content.</summary>
    /// <param name="paths">The primary workload's controlled runtime paths.</param>
    /// <returns>The absolute directory passed through the primary's serverMod argument.</returns>
    internal static string GetModPath(Arma3RuntimePaths paths)
        => Path.Combine(paths.InstallPath, "@utf_hc_join");

    /// <summary>Atomically installs an uncompressed addon bound to one primary process launch.</summary>
    /// <param name="paths">The primary workload's controlled runtime paths.</param>
    /// <param name="nonce">A lowercase 32-character hexadecimal launch identifier, never a credential.</param>
    /// <param name="cancellationToken">Cancels resource reads or the temporary archive write.</param>
    /// <returns>A task completing after the archive is available for process startup.</returns>
    /// <exception cref="ArgumentException">The launch identifier cannot be safely embedded in SQF.</exception>
    /// <exception cref="InvalidDataException">An installation path is a link or an embedded asset is missing.</exception>
    internal static async Task InstallAsync(
        Arma3RuntimePaths paths,
        string nonce,
        CancellationToken cancellationToken)
    {
        if (nonce is null || nonce.Length != 32 || nonce.Any(value => !char.IsAsciiHexDigitLower(value)))
            throw new ArgumentException("The Arma join launch identifier must be 32 lowercase hexadecimal characters.", nameof(nonce));

        var config = await ReadAssetAsync("config.cpp", cancellationToken).ConfigureAwait(false);
        var script = (await ReadAssetAsync("fn_init.sqf", cancellationToken).ConfigureAwait(false))
            .Replace("__UTF_LAUNCH_NONCE__", nonce, StringComparison.Ordinal);
        var files = new (string Name, byte[] Content)[]
        {
            ("config.cpp", Encoding.UTF8.GetBytes(config)),
            ("fn_init.sqf", Encoding.UTF8.GetBytes(script)),
        };
        var archive = BuildArchive(files);
        var directory = Path.Combine(GetModPath(paths), "addons");
        var target = Path.Combine(directory, "utf_hc_join.pbo");
        WorkerStoragePath.RejectExistingLinks(paths.DeploymentRoot, target);
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, archive, cancellationToken).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite
                                               | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>Reads a trusted source asset from the worker assembly rather than runtime content.</summary>
    /// <param name="name">The fixed asset file name.</param>
    /// <param name="cancellationToken">Cancels reading the asset.</param>
    /// <returns>The UTF-8 asset text.</returns>
    /// <exception cref="InvalidDataException">The build omitted the requested asset.</exception>
    private static async Task<string> ReadAssetAsync(string name, CancellationToken cancellationToken)
    {
        await using var stream = typeof(Arma3JoinAddon).Assembly.GetManifestResourceStream($"Arma3JoinAddonAssets.{name}")
                                 ?? throw new InvalidDataException("The worker's Arma join addon asset is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes deterministic PBO headers, raw content and the format's SHA-1 trailer.</summary>
    /// <param name="files">The fixed ordered archive members.</param>
    /// <returns>An addon archive with the utf_hc_join virtual prefix.</returns>
    private static byte[] BuildArchive(IReadOnlyList<(string Name, byte[] Content)> files)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        WriteHeader(writer, string.Empty, 0x56657273, 0);
        WriteString(writer, "prefix");
        WriteString(writer, "utf_hc_join");
        WriteString(writer, string.Empty);
        foreach (var (name, content) in files)
            WriteHeader(writer, name, 0, checked((uint)content.Length));
        WriteHeader(writer, string.Empty, 0, 0);
        foreach (var (_, content) in files)
            writer.Write(content);
        writer.Flush();
        // SHA-1 is required by the PBO format; it is not used as an authenticity or security boundary.
        var checksum = SHA1.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
        writer.Write((byte)0);
        writer.Write(checksum);
        return stream.ToArray();
    }

    /// <summary>Writes one little-endian PBO header entry with stable zero timestamps.</summary>
    /// <param name="writer">The destination archive writer.</param>
    /// <param name="name">The member name, or empty for the property and terminal entries.</param>
    /// <param name="method">The properties marker or zero for uncompressed data.</param>
    /// <param name="size">The uncompressed data length.</param>
    private static void WriteHeader(BinaryWriter writer, string name, uint method, uint size)
    {
        WriteString(writer, name);
        writer.Write(method);
        writer.Write(size);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(size);
    }

    /// <summary>Writes an ASCII zero-terminated PBO string without a length prefix.</summary>
    /// <param name="writer">The destination archive writer.</param>
    /// <param name="value">A trusted ASCII header value.</param>
    private static void WriteString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.ASCII.GetBytes(value));
        writer.Write((byte)0);
    }
}
