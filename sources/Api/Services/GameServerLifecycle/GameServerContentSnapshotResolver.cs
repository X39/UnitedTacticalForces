using System.Globalization;
using System.Net;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
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
    private const string SteamWorkshopSource = "steam-workshop";

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

        var normalized = await ResolveNormalizedAsync(
                server.GameIdentifier,
                definitionIds,
                cancellationToken)
            .ConfigureAwait(false);
        if (normalized.Count > 0 || definitionIds.Count == 0)
            return normalized;

        return await ResolveAsync(server.GameIdentifier, server.SelectedModPackDefinitionFk, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<WorkerContentReference>> ResolveAsync(
        string gameIdentifier,
        long? modPackDefinitionId,
        CancellationToken cancellationToken = default)
    {
        if (modPackDefinitionId is not { } definitionId)
            return [];

        var normalized = await ResolveNormalizedAsync(
                gameIdentifier,
                [(definitionId, null)],
                cancellationToken)
            .ConfigureAwait(false);
        if (normalized.Count > 0)
            return normalized;

        var definition = await dbContext.ModPackDefinitions
            .AsNoTracking()
            .Where(q => q.PrimaryKey == definitionId)
            .Select(q => new
            {
                q.IsActive,
                q.IsComposition,
                q.GameIdentifier,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw InvalidSelection("The selected content definition no longer exists.");
        if (!definition.IsActive)
            throw InvalidSelection("The selected content definition is no longer active.");
        if (!string.Equals(definition.GameIdentifier, gameIdentifier, StringComparison.OrdinalIgnoreCase))
            throw InvalidSelection("The selected content definition belongs to a different game.");

        // Provider-neutral selections remain durable for every game. Arma 3 is the first implementation that maps
        // those selections to concrete worker content references.
        if (!string.Equals(
                gameIdentifier,
                Arma3GameServerDefinitionRegistration.GameIdentifier,
                StringComparison.OrdinalIgnoreCase))
            return [];

        var revisions = definition.IsComposition
            ? await dbContext.ModPackRevisions
                .AsNoTracking()
                .Where(q => q.ModPackDefinitions!.Any(d => d.PrimaryKey == definitionId))
                .OrderBy(q => q.PrimaryKey)
                .Select(q => new RevisionSnapshot(
                    q.Html,
                    q.Definition!.GameIdentifier))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false)
            : await dbContext.ModPackRevisions
                .AsNoTracking()
                .Where(q => q.DefinitionFk == definitionId && q.IsActive)
                .OrderBy(q => q.PrimaryKey)
                .Select(q => new RevisionSnapshot(
                    q.Html,
                    q.Definition!.GameIdentifier))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

        if (!definition.IsComposition && revisions.Length is not 1)
            throw InvalidSelection("The selected standalone content definition has no unique active revision.");
        if (revisions.Any(q => !string.Equals(
                q.GameIdentifier,
                gameIdentifier,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw InvalidSelection("The selected composition contains cross-game content.");
        }

        var references = new Dictionary<ulong, WorkerContentReference>();
        foreach (var revision in revisions)
        {
            var mods = ParseMods(revision.Html);
            foreach (var mod in mods)
            {
                var workshopId = ParseSteamWorkshopId(mod.Link);
                references.TryAdd(workshopId, new WorkerContentReference
                {
                    Source = SteamWorkshopSource,
                    Identifier = workshopId.ToString(CultureInfo.InvariantCulture),
                    DisplayName = BoundDisplayName(mod.Title),
                });
            }
        }

        return references
            .OrderBy(q => q.Key)
            .Select(q => q.Value)
            .ToArray();
    }

    private static IReadOnlyCollection<Arma3ModPackParser.TitleLinkPair> ParseMods(string html)
    {
        try
        {
            var (_, mods, _) = Arma3ModPackParser.FromHtml(html);
            var document = new HtmlDocument();
            document.LoadHtml(html);
            var sourceRows = document.DocumentNode
                .SelectNodes("//tr[@data-type='ModContainer']")
                ?.Count ?? 0;
            if (sourceRows != mods.Count)
                throw InvalidSelection("The selected Arma 3 content contains a malformed mod entry.");
            return mods;
        }
        catch (PermanentLifecycleExecutionException)
        {
            throw;
        }
        catch (Exception)
        {
            throw InvalidSelection("The selected Arma 3 content could not be parsed.");
        }
    }

    private static ulong ParseSteamWorkshopId(string link)
    {
        try
        {
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !(string.Equals(uri.Host, "steamcommunity.com", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(uri.Host, "www.steamcommunity.com", StringComparison.OrdinalIgnoreCase))
                || !string.Equals(
                    uri.AbsolutePath.TrimEnd('/'),
                    "/sharedfiles/filedetails",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidSelection("The selected Arma 3 content contains a non-Steam Workshop link.");
            }

            var workshopIds = uri.Query
                .TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(q => q.Split('=', 2))
                .Where(q => q.Length == 2
                            && string.Equals(
                                Uri.UnescapeDataString(q[0]),
                                "id",
                                StringComparison.OrdinalIgnoreCase))
                .Select(q => Uri.UnescapeDataString(q[1]))
                .ToArray();
            if (workshopIds.Length is not 1
                || !ulong.TryParse(
                    workshopIds[0],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var workshopId)
                || workshopId is 0)
            {
                throw InvalidSelection("The selected Arma 3 content contains an invalid Steam Workshop id.");
            }
            return workshopId;
        }
        catch (PermanentLifecycleExecutionException)
        {
            throw;
        }
        catch (Exception)
        {
            throw InvalidSelection("The selected Arma 3 content contains a malformed Steam Workshop link.");
        }
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
        var result = new Dictionary<Guid, WorkerContentReference>();
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
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        return result.OrderBy(q => q.Value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(q => q.Key)
            .Select(q => q.Value)
            .ToArray();
    }

    private async Task ResolveDefinitionAsync(
        long definitionId,
        long? pinnedRevisionId,
        string gameIdentifier,
        ISet<long> visitedDefinitions,
        ISet<long> activeDefinitions,
        IDictionary<Guid, WorkerContentReference> result,
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
        if (revision is null && definition.IsComposition)
        {
            activeDefinitions.Remove(definitionId);
            visitedDefinitions.Add(definitionId);
            return;
        }
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
            result.TryAdd(mod.PrimaryKey, new WorkerContentReference
            {
                Source = version.SourceKind switch
                {
                    EContentSourceKind.SteamWorkshop => SteamWorkshopSource,
                    EContentSourceKind.UploadedArtifact => "uploaded-artifact",
                    EContentSourceKind.BuiltIn => "built-in",
                    _ => throw InvalidSelection("A mod uses an unsupported source kind."),
                },
                Identifier = version.SourceIdentifier,
                DisplayName = BoundDisplayName(mod.Title),
                UsageRole = version.UsageRole.ToString(),
                InstallerIdentifier = version.InstallerIdentifier,
                VersionIdentifier = version.VersionLabel,
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
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        activeDefinitions.Remove(definitionId);
        visitedDefinitions.Add(definitionId);
    }

    private sealed record RevisionSnapshot(string Html, string GameIdentifier);
}
