using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Game-neutral baseline classification. Game adapters can project more specific path metadata.
/// </summary>
[Singleton<ServerFileClassifier, IServerFileClassifier>]
public sealed class ServerFileClassifier : IServerFileClassifier
{
    private static readonly HashSet<string> LogExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".rpt", ".dmp", ".mdmp",
    };

    private static readonly HashSet<string> ConfigurationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cfg", ".conf", ".config", ".ini", ".json", ".xml", ".yaml", ".yml",
    };

    public EFileClassification Classify(string relativePath, bool isDirectory)
    {
        var normalized = SafeServerPath.Normalize(relativePath);
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var classification = segment.ToLowerInvariant() switch
            {
                "logs" or "diagnostics" or "crashes" => EFileClassification.Log,
                "saves" or "savegames"               => EFileClassification.SaveFile,
                "config" or "configs"                 => EFileClassification.Configuration,
                "mods" or "modpacks"                  => EFileClassification.Mod,
                "missions" or "scenarios"             => EFileClassification.Mission,
                "uploads"                              => EFileClassification.Upload,
                "generated"                            => EFileClassification.Generated,
                "downloads"                            => EFileClassification.Downloaded,
                _                                      => EFileClassification.Unknown,
            };
            if (classification != EFileClassification.Unknown)
                return classification;
        }

        if (!isDirectory && LogExtensions.Contains(Path.GetExtension(normalized)))
            return EFileClassification.Log;
        if (!isDirectory && ConfigurationExtensions.Contains(Path.GetExtension(normalized)))
            return EFileClassification.Configuration;
        return EFileClassification.GameFile;
    }

    public bool IsWritable(EFileContext context, EFileClassification classification)
        => classification is not (EFileClassification.Log
            or EFileClassification.Downloaded
            or EFileClassification.Generated);
}
