using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Resolves overlap deterministically: exact file, flat folder, recursive folder, then deeper path.
/// </summary>
[Singleton<PreservationSelectionResolver, IPreservationSelectionResolver>]
public sealed class PreservationSelectionResolver : IPreservationSelectionResolver
{
    public PreservationSelectionMatch? Resolve(
        string relativePath,
        bool isDirectory,
        IReadOnlyCollection<PreservationPathSelection> selections)
    {
        var normalizedPath = SafeServerPath.Normalize(relativePath, false);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return selections
            .Select(selection => new PreservationSelectionMatch(selection, NormalizeSelectionPath(selection)))
            .Where(match => Matches(normalizedPath, isDirectory, match, comparison))
            .OrderByDescending(match => ScopeRank(match.Selection.Scope))
            .ThenByDescending(match => PathDepth(match.NormalizedSelectionPath))
            .ThenBy(match => match.Selection.SelectionId)
            .FirstOrDefault();
    }

    private static bool Matches(
        string path,
        bool isDirectory,
        PreservationSelectionMatch match,
        StringComparison comparison)
    {
        var selectedPath = match.NormalizedSelectionPath;
        return match.Selection.Scope switch
        {
            EPreservationScope.File => !isDirectory && path.Equals(selectedPath, comparison),
            EPreservationScope.FlatFolder => !isDirectory
                                                && string.Equals(
                                                    ParentPath(path),
                                                    selectedPath,
                                                    comparison),
            EPreservationScope.RecursiveFolder => path.Equals(selectedPath, comparison)
                                                   || selectedPath.Length == 0
                                                   || path.StartsWith(selectedPath + "/", comparison),
            _ => throw new ArgumentOutOfRangeException(
                nameof(match.Selection.Scope),
                match.Selection.Scope,
                "Unknown preservation scope."),
        };
    }

    private static int ScopeRank(EPreservationScope scope)
        => scope switch
        {
            EPreservationScope.File => 3,
            EPreservationScope.FlatFolder => 2,
            EPreservationScope.RecursiveFolder => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown preservation scope."),
        };

    private static int PathDepth(string path) => path.Length == 0 ? 0 : path.Count(q => q == '/') + 1;

    private static string NormalizeSelectionPath(PreservationPathSelection selection)
        => SafeServerPath.Normalize(
            selection.RelativePath,
            selection.Scope is not EPreservationScope.File);

    private static string ParentPath(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..separator];
    }
}
