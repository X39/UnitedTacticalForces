using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Records the non-secret topology applied before an Arma process is launched.</summary>
internal static class Arma3LaunchTopologyContext
{
    /// <summary>The worker-owned state file kept outside persistent game data.</summary>
    internal const string FileName = "arma3-launch-topology.sha256";

    /// <summary>Atomically records the endpoint context for a launch after configuration reconciliation.</summary>
    /// <param name="payload">The immutable command whose topology will be applied.</param>
    /// <param name="paths">The deployment's worker-owned paths.</param>
    /// <param name="cancellationToken">Cancels the state write.</param>
    /// <returns>A task completing when the fingerprint is durable.</returns>
    /// <exception cref="InvalidDataException">The topology contains an invalid address.</exception>
    public static async Task SaveAsync(
        Arma3CommandPayload payload,
        Arma3RuntimePaths paths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fingerprint = Fingerprint(payload);
        using var temporary = await RestrictedTemporaryFile.CreateAsync(
                paths.AgentStatePath,
                "arma3-launch-topology",
                ".tmp",
                fingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(temporary.Path, Path.Combine(paths.AgentStatePath, FileName), true);
    }

    /// <summary>Checks that process recovery is using the same applied endpoint context.</summary>
    /// <param name="payload">The immutable command being recovered.</param>
    /// <param name="paths">The deployment's worker-owned paths.</param>
    /// <param name="cancellationToken">Cancels the state read.</param>
    /// <returns>Whether the context matches, including absent state for legacy empty topology only.</returns>
    public static async Task<bool> MatchesAsync(
        Arma3CommandPayload payload,
        Arma3RuntimePaths paths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var stream = new FileStream(
                Path.Combine(paths.AgentStatePath, FileName),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64,
                FileOptions.Asynchronous);
            if (stream.Length != 64)
                return false;
            using var reader = new StreamReader(stream);
            var saved = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(saved, Fingerprint(payload), StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return payload.Command.Topology is { Count: 0 };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or InvalidDataException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Hashes canonical endpoint identity without configuration, credentials, or process data.</summary>
    /// <param name="payload">The command carrying endpoint attribution.</param>
    /// <returns>A deterministic SHA-256 hexadecimal fingerprint.</returns>
    /// <exception cref="InvalidDataException">An endpoint address cannot be canonicalized.</exception>
    private static string Fingerprint(Arma3CommandPayload payload)
    {
        var command = payload.Command;
        var topology = command.Topology
                       ?? throw new InvalidDataException("Arma launch topology is missing.");
        var canonical = new
        {
            Version = 1,
            command.ServerId,
            command.DeploymentId,
            command.ServiceIdentifier,
            command.ServiceOrdinal,
            command.IsPrimaryService,
            command.Port,
            Topology = topology.Select(endpoint => new
                {
                    endpoint.ServiceIdentifier,
                    endpoint.ServiceOrdinal,
                    Address = CanonicalAddress(endpoint.Address),
                    endpoint.Port,
                    endpoint.IsPrimary,
                })
                .OrderBy(endpoint => endpoint.ServiceIdentifier, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.ServiceOrdinal)
                .ThenBy(endpoint => endpoint.Address, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.Port)
                .ThenBy(endpoint => endpoint.IsPrimary)
                .ToArray(),
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical)));
    }

    /// <summary>Normalizes IP spelling so equivalent addresses have the same identity.</summary>
    /// <param name="value">The attributed address to normalize.</param>
    /// <returns>The canonical IP address text.</returns>
    /// <exception cref="InvalidDataException">The address is not an IP literal.</exception>
    private static string CanonicalAddress(string value)
    {
        if (!IPAddress.TryParse(value, out var address))
            throw new InvalidDataException("Arma launch topology contains an invalid address.");
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
}
