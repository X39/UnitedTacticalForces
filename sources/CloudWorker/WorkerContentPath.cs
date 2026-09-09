using System.Security.Cryptography;
using System.Text;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Creates deterministic worker-owned paths for normalized provider content.</summary>
internal static class WorkerContentPath
{
    /// <summary>Creates an isolated Arma mod directory name for a content reference.</summary>
    /// <param name="reference">Validated normalized content reference.</param>
    /// <returns>Safe deterministic directory name.</returns>
    public static string GetArma3ModDirectoryName(WorkerContentReference reference)
    {
        var label = new string(reference.DisplayName
            .Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            .Take(48)
            .ToArray());
        if (label.Length == 0) label = "mod";
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{reference.Source}\n{reference.Identifier}")))[..12].ToLowerInvariant();
        return $"@utf-{label}-{identity}";
    }
}
