using System.Security.Cryptography;
using System.Text;
using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Validates addon archive interoperability and trusted launch-specific instrumentation.</summary>
public sealed class Arma3JoinAddonTests
{
    /// <summary>Independently decodes the archive table, prefix, contents and complete-file checksum.</summary>
    /// <returns>A task completing after the installed archive has been inspected.</returns>
    [Fact]
    public async Task Install_WritesDeterministicLaunchBoundUncompressedAddon()
    {
        var root = Path.Combine(Path.GetTempPath(), $"utf-addon-{Guid.NewGuid():N}");
        try
        {
            var paths = new Arma3RuntimePaths(root, Path.Combine(root, "state"), Guid.NewGuid(), "arma3server_x64");
            const string nonce = "0123456789abcdef0123456789abcdef";
            await Arma3JoinAddon.InstallAsync(paths, nonce, CancellationToken.None);
            Assert.Equal(Path.Combine(paths.InstallPath, "@utf_hc_join"), Arma3JoinAddon.GetModPath(paths));
            var archivePath = Path.Combine(Arma3JoinAddon.GetModPath(paths), "addons", "utf_hc_join.pbo");
            var bytes = await File.ReadAllBytesAsync(archivePath);
            Assert.Equal(0, bytes[^21]);
            Assert.Equal(SHA1.HashData(bytes.AsSpan(0, bytes.Length - 21)), bytes[^20..]);
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream, Encoding.ASCII);
            Assert.Equal(string.Empty, ReadString(reader));
            Assert.Equal(0x56657273u, reader.ReadUInt32());
            Assert.Equal(new byte[16], reader.ReadBytes(16));
            Assert.Equal("prefix", ReadString(reader));
            Assert.Equal("utf_hc_join", ReadString(reader));
            Assert.Equal(string.Empty, ReadString(reader));
            var entries = new List<(string Name, uint Size)>();
            while (true)
            {
                var name = ReadString(reader);
                var method = reader.ReadUInt32();
                var originalSize = reader.ReadUInt32();
                var reserved = reader.ReadUInt32();
                var timestamp = reader.ReadUInt32();
                var size = reader.ReadUInt32();
                Assert.Equal(0u, method);
                Assert.Equal(0u, reserved);
                Assert.Equal(0u, timestamp);
                Assert.Equal(originalSize, size);
                if (name.Length == 0)
                {
                    Assert.Equal(0u, size);
                    break;
                }
                entries.Add((name, size));
            }
            Assert.Equal(["config.cpp", "fn_init.sqf"], entries.Select(entry => entry.Name));
            var config = Encoding.UTF8.GetString(reader.ReadBytes(checked((int)entries[0].Size)));
            var script = Encoding.UTF8.GetString(reader.ReadBytes(checked((int)entries[1].Size)));
            Assert.Equal(bytes.Length - 21, stream.Position);
            Assert.Contains("requiredVersion = 2.06;", config);
            Assert.Contains("preInit = 1;", config);
            Assert.Contains("file = \"\\utf_hc_join\";", config);
            Assert.Contains($"UTFHC|1|{nonce}|%1|%2|%3", script);
            Assert.DoesNotContain("__UTF_LAUNCH_NONCE__", script);
            Assert.Contains("if (!isServer) exitWith {};", script);
            Assert.Contains("(_info select 7) isEqualTo true", script);
            Assert.Contains("(_info select 3) isEqualTo \"UTFHC1\"", script);
            Assert.Contains("count _matches isEqualTo 1", script);
            Assert.Contains("_candidate > 2", script);
            Assert.Contains("_clientState isEqualTo 10", script);
            Assert.Contains("if (_ready isEqualTo 0) then { _owner = 0; };", script);
            Assert.Contains("uiNamespace setVariable [\"UTFHC_joinSequence\"", script);
            var atomicStart = script.IndexOf("isNil {", StringComparison.Ordinal);
            var sequenceRead = script.IndexOf("private _sequence =", StringComparison.Ordinal);
            var protocolWrite = script.IndexOf("diag_log text format", StringComparison.Ordinal);
            var atomicEnd = script.IndexOf("};", protocolWrite, StringComparison.Ordinal);
            Assert.True(atomicStart >= 0 && atomicStart < sequenceRead && sequenceRead < protocolWrite);
            Assert.True(atomicEnd > protocolWrite);
            Assert.Contains("OnUserDisconnected", script);
            Assert.Contains("MPEnded", script);
            Assert.Contains("forEach allUsers", script);
            Assert.Contains("uiSleep 2;", script);
            Assert.Contains($"UTFPL|1|{nonce}|%1|%2", script);
            Assert.Contains("UTFPL_fnc_samplePlayers = {", script);
            Assert.Contains("!((_info select 7) isEqualTo true)", script);
            Assert.Contains("_owner isEqualType 0 && {_owner > 2}", script);
            Assert.Contains("_humans != _previousHumans", script);
            Assert.Contains("diag_tickTime - _lastEmission >= 30", script);
            Assert.Contains("uiNamespace setVariable [\"UTFPL_playerSequence\"", script);
            var playerAtomicStart = script.IndexOf("isNil {", script.IndexOf("UTFPL_fnc_samplePlayers = {", StringComparison.Ordinal),
                StringComparison.Ordinal);
            var playerSequenceRead = script.IndexOf("private _sequence =", playerAtomicStart, StringComparison.Ordinal);
            var playerWrite = script.IndexOf("UTFPL|1|", StringComparison.Ordinal);
            Assert.True(playerAtomicStart >= 0 && playerAtomicStart < playerSequenceRead && playerSequenceRead < playerWrite);
            Assert.Equal(1, CountOccurrences(script, "UTFPL|1|"));
            Assert.Equal(1, CountOccurrences(script, "UTFHC|1|"));
            // Three user event handlers plus the periodic reconciliation loop.
            Assert.Equal(4, CountOccurrences(script, "call UTFPL_fnc_samplePlayers;"));
            Assert.DoesNotContain("remoteExec", script);
            Assert.DoesNotContain("setGroupOwner", script);
            await Arma3JoinAddon.InstallAsync(paths, nonce, CancellationToken.None);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(archivePath));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(archivePath)!));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Rejects malformed launch markers before creating any addon directories.</summary>
    /// <param name="nonce">An unsafe or noncanonical launch marker.</param>
    /// <returns>A task completing after validation has rejected the marker.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcde|")]
    [InlineData("0123456789abcdef0123456789abcde\"")]
    public async Task Install_RejectsUnsafeNonceBeforeWriting(string? nonce)
    {
        var root = Path.Combine(Path.GetTempPath(), $"utf-addon-{Guid.NewGuid():N}");
        var paths = new Arma3RuntimePaths(root, Path.Combine(root, "state"), Guid.NewGuid(), "arma3server_x64");
        await Assert.ThrowsAsync<ArgumentException>(() => Arma3JoinAddon.InstallAsync(paths, nonce!, CancellationToken.None));
        Assert.False(Directory.Exists(root));
    }

    /// <summary>Counts non-overlapping ordinal occurrences of a marker.</summary>
    /// <param name="text">The text to search.</param>
    /// <param name="marker">The non-empty marker to count.</param>
    /// <returns>The number of occurrences.</returns>
    private static int CountOccurrences(string text, string marker)
    {
        var count = 0;
        for (var index = text.IndexOf(marker, StringComparison.Ordinal); index >= 0;
             index = text.IndexOf(marker, index + marker.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    /// <summary>Reads a PBO header string without relying on the production archive writer.</summary>
    /// <param name="reader">The reader positioned at an ASCII zero-terminated field.</param>
    /// <returns>The decoded field.</returns>
    private static string ReadString(BinaryReader reader)
    {
        var bytes = new List<byte>();
        byte value;
        while ((value = reader.ReadByte()) != 0)
            bytes.Add(value);
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}
