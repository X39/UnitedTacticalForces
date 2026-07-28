using System.Globalization;
using System.Net;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Database;
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
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new PermanentLifecycleExecutionException("The game server no longer exists.");
        if (server.SelectedModPackDefinitionFk is not { } definitionId)
            return [];

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
        if (!string.Equals(definition.GameIdentifier, server.GameIdentifier, StringComparison.OrdinalIgnoreCase))
            throw InvalidSelection("The selected content definition belongs to a different game.");

        // Provider-neutral selections remain durable for every game. Arma 3 is the first implementation that maps
        // those selections to concrete worker content references.
        if (!string.Equals(
                server.GameIdentifier,
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
                server.GameIdentifier,
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
            throw InvalidSelection("The selected Arma 3 content contains an invalid mod display name.");
        }
        return displayName.Length <= 128 ? displayName : displayName[..128];
    }

    private static PermanentLifecycleExecutionException InvalidSelection(string message)
        => new(message, "content-selection-invalid");

    private sealed record RevisionSnapshot(string Html, string GameIdentifier);
}
