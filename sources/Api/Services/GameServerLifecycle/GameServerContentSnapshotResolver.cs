using System.Net;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Resolves mutable provider-neutral content selection into the immutable references persisted on a worker command.
/// </summary>
[Scoped<GameServerContentSnapshotResolver, IGameServerContentSnapshotResolver>]
internal sealed class GameServerContentSnapshotResolver(DatabaseContext dbContext)
    : IGameServerContentSnapshotResolver
{
    public async Task<IReadOnlyCollection<WorkerContentReference>> ResolveAsync(
        Guid gameServerId,
        CancellationToken cancellationToken = default)
    {
        var server = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == gameServerId)
            .Select(q => new
            {
                q.GameIdentifier,
                q.SelectedModPackDefinitionFk,
                q.PinnedModPackRevisionFk,
                q.SelectedScenarioFk,
                q.PinnedScenarioVersionFk,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new PermanentLifecycleExecutionException("The game server no longer exists.");
        var definitionIds = new List<(long DefinitionId, long? PinnedRevisionId)>();
        if (server.SelectedModPackDefinitionFk is { } selectedDefinition)
            definitionIds.Add((selectedDefinition, server.PinnedModPackRevisionFk));
        if (server.SelectedScenarioFk is { } scenarioId)
        {
            var scenarioVersionId = server.PinnedScenarioVersionFk
                                    ?? await dbContext.ScenarioVersions
                                        .Where(q => q.ScenarioFk == scenarioId)
                                        .OrderByDescending(q => q.VersionNumber)
                                        .Select(q => (Guid?)q.PrimaryKey)
                                        .FirstOrDefaultAsync(cancellationToken)
                                        .ConfigureAwait(false);
            if (scenarioVersionId is { } resolvedScenarioVersionId)
            {
                definitionIds.AddRange(await dbContext.ScenarioVersionRequiredModPacks
                    .Where(q => q.ScenarioVersionFk == resolvedScenarioVersionId)
                    .Select(q => new ValueTuple<long, long?>(q.ModPackDefinitionFk, q.PinnedRevisionFk))
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false));
            }
        }

        return await ResolveNormalizedAsync(
                server.GameIdentifier,
                definitionIds,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<WorkerContentReference>> ResolveAsync(
        string gameIdentifier,
        long? modPackDefinitionId,
        CancellationToken cancellationToken = default)
    {
        if (modPackDefinitionId is not { } definitionId)
            return [];

        return await ResolveNormalizedAsync(
                gameIdentifier,
                [(definitionId, null)],
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BoundDisplayName(string title)
    {
        var displayName = WebUtility.HtmlDecode(title).Trim();
        if (string.IsNullOrWhiteSpace(displayName)
            || displayName is "." or ".."
            || displayName.IndexOfAny(['/', '\\']) >= 0
            || displayName.Any(char.IsControl))
        {
            throw InvalidSelection("The selected content contains an invalid mod display name.");
        }
        return displayName.Length <= 128 ? displayName : displayName[..128];
    }

    private static PermanentLifecycleExecutionException InvalidSelection(string message)
        => new(message, "content-selection-invalid");

    private async Task<IReadOnlyCollection<WorkerContentReference>> ResolveNormalizedAsync(
        string gameIdentifier,
        IReadOnlyCollection<(long DefinitionId, long? PinnedRevisionId)> roots,
        CancellationToken cancellationToken)
    {
        var result = new List<WorkerContentReference>();
        var resolvedMods = new HashSet<Guid>();
        var visitedDefinitions = new HashSet<long>();
        var activeDefinitions = new HashSet<long>();
        foreach (var root in roots)
        {
            await ResolveDefinitionAsync(
                    root.DefinitionId,
                    root.PinnedRevisionId,
                    gameIdentifier,
                    visitedDefinitions,
                    activeDefinitions,
                    resolvedMods,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        return result;
    }

    private async Task ResolveDefinitionAsync(
        long definitionId,
        long? pinnedRevisionId,
        string gameIdentifier,
        ISet<long> visitedDefinitions,
        ISet<long> activeDefinitions,
        ISet<Guid> resolvedMods,
        ICollection<WorkerContentReference> result,
        CancellationToken cancellationToken)
    {
        if (visitedDefinitions.Contains(definitionId))
            return;
        if (!activeDefinitions.Add(definitionId))
            throw InvalidSelection("The selected mod pack graph contains a cycle.");

        var definition = await dbContext.ModPackDefinitions.AsNoTracking()
            .SingleOrDefaultAsync(q => q.PrimaryKey == definitionId && q.IsActive, cancellationToken)
            .ConfigureAwait(false)
            ?? throw InvalidSelection("A selected mod pack is unavailable.");
        if (!string.Equals(definition.GameIdentifier, gameIdentifier, StringComparison.OrdinalIgnoreCase))
            throw InvalidSelection("The selected mod pack graph contains cross-game content.");

        var revision = pinnedRevisionId is { } pinned
            ? await dbContext.ModPackRevisions.AsNoTracking()
                .SingleOrDefaultAsync(q => q.PrimaryKey == pinned && q.DefinitionFk == definitionId, cancellationToken)
                .ConfigureAwait(false)
            : await dbContext.ModPackRevisions.AsNoTracking()
                .Where(q => q.DefinitionFk == definitionId && q.IsActive)
                .OrderByDescending(q => q.TimeStampCreated)
                .ThenByDescending(q => q.PrimaryKey)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (revision is null)
            throw InvalidSelection("A selected mod pack has no resolvable revision.");

        var memberships = await dbContext.ModPackRevisionMods.AsNoTracking()
            .Where(q => q.ModPackRevisionFk == revision.PrimaryKey)
            .OrderBy(q => q.Order)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var membership in memberships)
        {
            var mod = await dbContext.Mods.AsNoTracking()
                .SingleAsync(q => q.PrimaryKey == membership.ModFk && q.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(mod.GameIdentifier, gameIdentifier, StringComparison.OrdinalIgnoreCase))
                throw InvalidSelection("The selected mod pack contains cross-game content.");
            var version = membership.PinnedVersionFk is { } pinnedVersion
                ? await dbContext.ModVersions.AsNoTracking()
                    .SingleOrDefaultAsync(q => q.PrimaryKey == pinnedVersion && q.ModFk == mod.PrimaryKey, cancellationToken)
                    .ConfigureAwait(false)
                : await dbContext.ModVersions.AsNoTracking()
                    .Where(q => q.ModFk == mod.PrimaryKey)
                    .OrderByDescending(q => q.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (version is null)
                throw InvalidSelection($"Mod '{mod.Title}' has no resolvable version.");
            if (resolvedMods.Add(mod.PrimaryKey)) result.Add(new WorkerContentReference
            {
                Source = mod.ProviderIdentifier,
                Identifier = mod.CanonicalSourceIdentifier,
                DisplayName = BoundDisplayName(mod.Title),
                UsageRole = mod.UsageRole.ToString(),
                InstallerIdentifier = version.InstallerIdentifier,
                VersionIdentifier = version.VersionSelector,
                ArtifactIdentifier = version.ArtifactIdentifier,
                ChecksumSha256 = version.ChecksumSha256,
                InstallerConfiguration = version.InstallerConfiguration,
            });
        }

        var dependencies = await dbContext.ModPackRevisionDependencies.AsNoTracking()
            .Where(q => q.ParentRevisionFk == revision.PrimaryKey)
            .OrderBy(q => q.Order)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var dependency in dependencies)
        {
            await ResolveDefinitionAsync(
                    dependency.ChildDefinitionFk,
                    dependency.PinnedChildRevisionFk,
                    gameIdentifier,
                visitedDefinitions,
                activeDefinitions,
                resolvedMods,
                result,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        activeDefinitions.Remove(definitionId);
        visitedDefinitions.Add(definitionId);
    }
}
