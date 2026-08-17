using System.Globalization;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

internal static class NormalizedContentBackfill
{
    private const string ImplementationVersion = "arma-html-v1";

    public static async Task ApplyAsync(DatabaseContext dbContext, CancellationToken cancellationToken = default)
    {
        var revisions = await dbContext.ModPackRevisions
            .Include(q => q.Definition)
            .Where(q => q.Definition!.GameIdentifier == Arma3GameServerDefinitionRegistration.GameIdentifier)
            .OrderBy(q => q.PrimaryKey)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var revision in revisions)
        {
            var runId = $"{ImplementationVersion}:{revision.PrimaryKey}";
            if (await dbContext.ContentBackfillRuns.AnyAsync(q => q.Identifier == runId, cancellationToken)
                    .ConfigureAwait(false))
                continue;

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            var (_, parsedMods, _) = Arma3ModPackParser.FromHtml(revision.Html);
            var order = 0;
            foreach (var parsedMod in parsedMods)
            {
                var sourceIdentifier = ParseWorkshopId(parsedMod.Link);
                var existingVersion = dbContext.ModVersions.Local.FirstOrDefault(
                    q => q.Mod?.GameIdentifier == Arma3GameServerDefinitionRegistration.GameIdentifier
                         && q.SourceKind == EContentSourceKind.SteamWorkshop
                         && q.SourceIdentifier == sourceIdentifier)
                    ?? await dbContext.ModVersions
                    .Include(q => q.Mod)
                    .SingleOrDefaultAsync(
                        q => q.Mod!.GameIdentifier == Arma3GameServerDefinitionRegistration.GameIdentifier
                             && q.SourceKind == EContentSourceKind.SteamWorkshop
                             && q.SourceIdentifier == sourceIdentifier,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (existingVersion is null)
                {
                    var mod = new Mod
                    {
                        PrimaryKey = Guid.NewGuid(),
                        GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier,
                        Title = parsedMod.Title.Trim(),
                        IsActive = true,
                        TimeStampCreated = revision.TimeStampCreated,
                    };
                    existingVersion = new ModVersion
                    {
                        PrimaryKey = Guid.NewGuid(),
                        Mod = mod,
                        ModFk = mod.PrimaryKey,
                        VersionNumber = 1,
                        VersionLabel = "Workshop latest",
                        SourceKind = EContentSourceKind.SteamWorkshop,
                        SourceIdentifier = sourceIdentifier,
                        UsageRole = EModUsageRole.Shared,
                        InstallerIdentifier = "steam-workshop",
                        TimeStampCreated = revision.TimeStampCreated,
                    };
                    dbContext.ModVersions.Add(existingVersion);
                }

                if (!dbContext.ModPackRevisionMods.Local.Any(
                        q => q.ModPackRevisionFk == revision.PrimaryKey && q.ModFk == existingVersion.ModFk)
                    && !await dbContext.ModPackRevisionMods.AnyAsync(
                            q => q.ModPackRevisionFk == revision.PrimaryKey && q.ModFk == existingVersion.ModFk,
                            cancellationToken).ConfigureAwait(false))
                {
                    dbContext.ModPackRevisionMods.Add(new ModPackRevisionMod
                    {
                        ModPackRevisionFk = revision.PrimaryKey,
                        Mod = existingVersion.Mod,
                        ModFk = existingVersion.ModFk,
                        PinnedVersion = existingVersion,
                        Order = order,
                    });
                }
                order++;
            }

            dbContext.ContentBackfillRuns.Add(new ContentBackfillRun
            {
                Identifier = runId,
                ImplementationVersion = ImplementationVersion,
                TimeStampCompleted = NodaSystemClock.Instance.GetCurrentInstant(),
            });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ParseWorkshopId(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !(string.Equals(uri.Host, "steamcommunity.com", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(uri.Host, "www.steamcommunity.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("An existing Arma modpack contains a non-Steam Workshop URL.");
        var value = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(q => q.Split('=', 2))
            .Where(q => q.Length == 2 && string.Equals(q[0], "id", StringComparison.OrdinalIgnoreCase))
            .Select(q => Uri.UnescapeDataString(q[1]))
            .SingleOrDefault();
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var identifier)
            || identifier == 0)
            throw new InvalidOperationException("An existing Arma modpack contains an invalid Workshop identity.");
        return identifier.ToString(CultureInfo.InvariantCulture);
    }
}
