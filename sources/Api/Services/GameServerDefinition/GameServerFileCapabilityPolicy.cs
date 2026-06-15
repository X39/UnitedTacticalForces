using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>
/// Fail-closed game-file boundary shared by browser mutations and worker persistence transfer.
/// </summary>
public static class GameServerFileCapabilityPolicy
{
    private static readonly HashSet<string> WorkerOwnedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "server-instance",
        "logs",
        "state",
        "worker-state",
        "tooling",
        "tools",
        "steamcmd",
        "steamcmd-tools",
    };

    public static GameServerFileDefinition? Resolve(
        GameServerDefinition definition,
        string relativePath,
        bool isDirectory)
    {
        var normalized = SafeServerPath.Normalize(relativePath, false);
        if (IsWorkerOwnedRoot(normalized))
            return null;
        return definition.FileCapabilities
            .Select(q => new
            {
                Definition = q,
                Path = SafeServerPath.Normalize(q.RelativePath, false),
            })
            .Where(q => q.Definition.Recursive
                ? normalized.Equals(q.Path, StringComparison.OrdinalIgnoreCase)
                  || normalized.StartsWith(q.Path + "/", StringComparison.OrdinalIgnoreCase)
                : !isDirectory && normalized.Equals(q.Path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(q => PathDepth(q.Path))
            .ThenBy(q => q.Definition.Recursive)
            .Select(q => q.Definition)
            .FirstOrDefault();
    }

    public static bool CanMutate(
        GameServerDefinition definition,
        string relativePath,
        bool isDirectory,
        EFileContext context)
    {
        var normalized = SafeServerPath.Normalize(relativePath, false);
        var rule = Resolve(definition, normalized, isDirectory);
        if (rule is null || !IsMutable(rule, context))
            return false;
        return !isDirectory || !HasNonMutableDescendant(definition, normalized, context);
    }

    public static bool IsPersistentSnapshotPathAllowed(
        GameServerDefinition definition,
        string relativePath,
        bool isDirectory)
    {
        var normalized = SafeServerPath.Normalize(relativePath, false);
        if (IsWorkerOwnedRoot(normalized))
            return false;
        if (!isDirectory)
            return CanMutate(definition, normalized, false, EFileContext.Persistent);
        if (Resolve(definition, normalized, true) is { MutableWhileStopped: true })
            return true;
        var prefix = normalized + "/";
        return definition.FileCapabilities.Any(q =>
            q.MutableWhileStopped
            && SafeServerPath.Normalize(q.RelativePath, false)
                .StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsPreservationSelectionAllowed(
        GameServerDefinition definition,
        string relativePath,
        EPreservationScope scope)
    {
        if (!Enum.IsDefined(scope))
            return false;
        var normalized = SafeServerPath.Normalize(
            relativePath,
            scope is not EPreservationScope.File);
        if (normalized.Length == 0 || IsWorkerOwnedRoot(normalized))
            return false;
        return CanMutate(
            definition,
            normalized,
            scope is not EPreservationScope.File,
            EFileContext.Persistent);
    }

    public static bool IsWorkerOwnedRoot(string relativePath)
    {
        var normalized = SafeServerPath.Normalize(relativePath, false);
        var separator = normalized.IndexOf('/');
        var root = separator < 0 ? normalized : normalized[..separator];
        return WorkerOwnedRoots.Contains(root);
    }

    private static bool HasNonMutableDescendant(
        GameServerDefinition definition,
        string directoryPath,
        EFileContext context)
    {
        var prefix = directoryPath + "/";
        return definition.FileCapabilities.Any(q =>
        {
            var capabilityPath = SafeServerPath.Normalize(q.RelativePath, false);
            return capabilityPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                   && !IsMutable(q, context);
        });
    }

    private static bool IsMutable(GameServerFileDefinition definition, EFileContext context)
        => context is EFileContext.Persistent
            ? definition.MutableWhileStopped
            : definition.MutableWhileRunning;

    private static int PathDepth(string path) => path.Count(q => q == '/');
}
