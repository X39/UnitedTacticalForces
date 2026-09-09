using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Core;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>Creates and reads normalized immutable mod-pack revisions.</summary>
[ApiController]
[Authorize]
[Route("game-content/mod-packs")]
public sealed class NormalizedModPacksController(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog gameDefinitions,
    IEnumerable<IModSourceProvider> sourceProviders) : ControllerBase
{
    /// <summary>Lists active game-scoped normalized mod packs.</summary>
    /// <param name="gameIdentifier">Owning game identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Definitions and their latest active revision.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync(string gameIdentifier, CancellationToken cancellationToken)
        => Ok(await dbContext.ModPackDefinitions.AsNoTracking()
            .Where(q => q.IsActive && q.GameIdentifier == gameIdentifier)
            .OrderBy(q => q.Title)
            .Select(q => new
            {
                DefinitionId = q.PrimaryKey,
                q.GameIdentifier,
                q.Title,
                LatestRevisionId = q.ModPackRevisionsOwned!.Where(r => r.IsActive)
                    .OrderByDescending(r => r.TimeStampCreated).Select(r => (long?)r.PrimaryKey).FirstOrDefault(),
            }).ToArrayAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Gets a normalized mod-pack workspace.</summary>
    /// <param name="modPackId">Definition identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Definition metadata and revision history.</returns>
    [HttpGet("{modPackId:long}")]
    public async Task<IActionResult> GetAsync(long modPackId, CancellationToken cancellationToken)
    {
        var workspace = await dbContext.ModPackDefinitions.AsNoTracking().Where(q => q.PrimaryKey == modPackId)
            .Select(q => new
            {
                DefinitionId = q.PrimaryKey,
                q.GameIdentifier,
                q.Title,
                q.IsActive,
                Revisions = q.ModPackRevisionsOwned!.OrderByDescending(r => r.TimeStampCreated).Select(r => new
                {
                    RevisionId = r.PrimaryKey, r.IsActive, r.TimeStampCreated, r.UpdatedByFk,
                    DirectModCount = r.Mods.Count, NestedModPackCount = r.Dependencies.Count,
                }),
            }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return workspace is null ? NotFound() : Ok(workspace);
    }

    /// <summary>Creates a definition and first revision atomically.</summary>
    /// <param name="payload">Definition and revision content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created identifiers.</returns>
    [Authorize(Claims.Creation.ModPack)]
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        NormalizedModPackPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Unauthorized();
        var game = gameDefinitions.Find(payload.GameIdentifier.Trim().ToLowerInvariant());
        if (game is null || !game.ContentCapabilities.SupportsModPacks)
            return BadRequest("The game does not support mod packs.");
        var drafts = await ResolveDraftsAsync(game, payload.Revision.NewMods, cancellationToken);
        if (drafts.Result is not null) return drafts.Result;
        var validation = await ValidateMembershipAsync(null, game.GameIdentifier, payload.Revision, cancellationToken);
        if (validation is not null) return BadRequest(validation);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var definition = new ModPackDefinition
        {
            GameIdentifier = game.GameIdentifier,
            Title = payload.Title.Trim(),
            OwnerFk = userId,
            IsActive = true,
            TimeStampCreated = now,
        };
        var revision = CreateRevision(definition, userId, payload.Revision, drafts.Mods, now);
        definition.ModPackRevisionsOwned = [revision];
        dbContext.ModPackDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetMembershipAsync), new { revisionId = revision.PrimaryKey },
            new { DefinitionId = definition.PrimaryKey, RevisionId = revision.PrimaryKey });
    }

    /// <summary>Appends a new immutable revision to a definition.</summary>
    /// <param name="modPackId">Definition identifier.</param>
    /// <param name="payload">Complete new revision content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new revision identifier.</returns>
    [Authorize(Claims.ResourceBased.ModPack.Modify)]
    [HttpPost("{modPackId:long}/revisions")]
    public async Task<IActionResult> AppendRevisionAsync(
        long modPackId,
        NormalizedModPackRevisionPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Unauthorized();
        var definition = await dbContext.ModPackDefinitions.SingleOrDefaultAsync(
            q => q.PrimaryKey == modPackId && q.IsActive, cancellationToken).ConfigureAwait(false);
        if (definition is null) return NotFound();
        var game = gameDefinitions.Find(definition.GameIdentifier)!;
        var drafts = await ResolveDraftsAsync(game, payload.NewMods, cancellationToken);
        if (drafts.Result is not null) return drafts.Result;
        var validation = await ValidateMembershipAsync(modPackId, definition.GameIdentifier, payload, cancellationToken);
        if (validation is not null) return BadRequest(validation);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.ModPackRevisions.Where(q => q.DefinitionFk == modPackId && q.IsActive)
            .ExecuteUpdateAsync(q => q.SetProperty(x => x.IsActive, false), cancellationToken).ConfigureAwait(false);
        var revision = CreateRevision(definition, userId, payload, drafts.Mods, NodaSystemClock.Instance.GetCurrentInstant());
        dbContext.ModPackRevisions.Add(revision);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { RevisionId = revision.PrimaryKey });
    }

    /// <summary>Gets direct or recursively flattened revision membership.</summary>
    /// <param name="revisionId">Revision identifier.</param>
    /// <param name="effective">Whether nested packs should be flattened.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered membership data.</returns>
    [HttpGet("revisions/{revisionId:long}/members")]
    public async Task<IActionResult> GetMembershipAsync(
        long revisionId,
        [FromQuery] bool effective = false,
        CancellationToken cancellationToken = default)
    {
        var root = await dbContext.ModPackRevisions.AsNoTracking().Include(q => q.Definition)
            .SingleOrDefaultAsync(q => q.PrimaryKey == revisionId, cancellationToken).ConfigureAwait(false);
        if (root is null) return NotFound();
        if (!effective)
        {
            var mods = await ReadModsAsync(revisionId, cancellationToken);
            var dependencies = await dbContext.ModPackRevisionDependencies.AsNoTracking()
                .Where(q => q.ParentRevisionFk == revisionId).OrderBy(q => q.Order)
                .Select(q => new { q.ChildDefinitionFk, q.PinnedChildRevisionFk, q.Order })
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            return Ok(new { Mods = mods, Dependencies = dependencies });
        }
        var result = new List<MembershipProjection>();
        await FlattenAsync(revisionId, new HashSet<long>(), new HashSet<Guid>(), result, cancellationToken);
        return Ok(new
        {
            Mods = result.Select((q, index) => new
            {
                q.ModFk, q.PinnedVersionFk, EffectiveOrder = index, q.Title, q.ProviderIdentifier,
                q.CanonicalSourceIdentifier, q.UsageRole,
            }),
        });
    }

    private static ModPackRevision CreateRevision(
        ModPackDefinition definition,
        Guid userId,
        NormalizedModPackRevisionPayload payload,
        IReadOnlyList<Mod> newMods,
        Instant now)
        => new()
        {
            Definition = definition,
            UpdatedByFk = userId,
            IsActive = true,
            TimeStampCreated = now,
            Mods = payload.Mods.Select((q, index) => new ModPackRevisionMod
                {
                    ModFk = q.ModId, PinnedVersionFk = q.PinnedVersionId, Order = index,
                })
                .Concat(newMods.Select((q, index) => new ModPackRevisionMod
                {
                    Mod = q, ModFk = q.PrimaryKey, Order = payload.Mods.Count + index,
                })).ToList(),
            Dependencies = payload.Dependencies.Select((q, index) => new ModPackRevisionDependency
            {
                ChildDefinitionFk = q.DefinitionId,
                PinnedChildRevisionFk = q.PinnedRevisionId,
                Order = index,
            }).ToList(),
        };

    private async Task<string?> ValidateMembershipAsync(
        long? rootDefinitionId,
        string gameIdentifier,
        NormalizedModPackRevisionPayload payload,
        CancellationToken cancellationToken)
    {
        if (payload.Mods.Count + payload.NewMods.Count + payload.Dependencies.Count == 0)
            return "A mod-pack revision must not be empty.";
        if (payload.Mods.Select(q => q.ModId).Distinct().Count() != payload.Mods.Count
            || payload.Dependencies.Select(q => q.DefinitionId).Distinct().Count() != payload.Dependencies.Count)
            return "Duplicate memberships are not allowed.";
        var modIds = payload.Mods.Select(q => q.ModId).ToArray();
        var validMods = await dbContext.Mods.CountAsync(q => modIds.Contains(q.PrimaryKey)
            && q.IsActive && q.GameIdentifier == gameIdentifier, cancellationToken).ConfigureAwait(false);
        if (validMods != modIds.Length) return "Every mod must exist, be active, and belong to the same game.";
        foreach (var membership in payload.Mods.Where(q => q.PinnedVersionId.HasValue))
            if (!await dbContext.ModVersions.AnyAsync(q => q.PrimaryKey == membership.PinnedVersionId
                    && q.ModFk == membership.ModId, cancellationToken).ConfigureAwait(false))
                return "A pinned mod version does not belong to its referenced mod.";
        var dependencyIds = payload.Dependencies.Select(q => q.DefinitionId).ToArray();
        if (await dbContext.ModPackDefinitions.CountAsync(q => dependencyIds.Contains(q.PrimaryKey)
                && q.IsActive && q.GameIdentifier == gameIdentifier, cancellationToken) != dependencyIds.Length)
            return "Every nested mod pack must exist, be active, and belong to the same game.";
        foreach (var dependency in payload.Dependencies.Where(q => q.PinnedRevisionId.HasValue))
            if (!await dbContext.ModPackRevisions.AnyAsync(q => q.PrimaryKey == dependency.PinnedRevisionId
                    && q.DefinitionFk == dependency.DefinitionId, cancellationToken).ConfigureAwait(false))
                return "A pinned nested revision does not belong to its referenced mod pack.";
        if (rootDefinitionId is { } root && await WouldCreateCycleAsync(root, dependencyIds, cancellationToken))
            return "Nested mod packs must not form a cycle.";
        return null;
    }

    private async Task<(IActionResult? Result, IReadOnlyList<Mod> Mods)> ResolveDraftsAsync(
        GameServerDefinition game,
        IReadOnlyList<NormalizedModDraftPayload> drafts,
        CancellationToken cancellationToken)
    {
        var result = new List<Mod>();
        var identities = new HashSet<(string Provider, string Source)>();
        foreach (var draft in drafts)
        {
            var provider = sourceProviders.FirstOrDefault(q => q.Identifier == draft.ProviderIdentifier);
            var binding = game.ContentCapabilities.ModProviderBindings.FirstOrDefault(
                q => q.ProviderIdentifier == draft.ProviderIdentifier);
            if (provider is null || binding is null || !binding.InstallerIdentifiers.Contains(draft.InstallerIdentifier))
                return (BadRequest("A new mod uses an unsupported provider or installer."), []);
            ModSourceResolution source;
            try { source = await provider.ResolveAsync(draft.Source, draft.VersionSelector, cancellationToken); }
            catch (ModSourceUnavailableException exception)
            { return (Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable), []); }
            catch (ArgumentException exception) { return (BadRequest(exception.Message), []); }
            if (!identities.Add((source.ProviderIdentifier, source.CanonicalSourceIdentifier)))
                return (BadRequest("Duplicate new mod sources are not allowed."), []);
            var existing = await dbContext.Mods.AnyAsync(q => q.GameIdentifier == game.GameIdentifier
                && q.ProviderIdentifier == source.ProviderIdentifier
                && q.CanonicalSourceIdentifier == source.CanonicalSourceIdentifier, cancellationToken);
            if (existing) return (Conflict("A reviewed new source already exists in the catalog; reference its Mod ID."), []);
            try { using var _ = JsonDocument.Parse(draft.InstallerConfiguration); }
            catch (JsonException) { return (BadRequest("Installer configuration must be valid JSON."), []); }
            var mod = new Mod
            {
                PrimaryKey = Guid.NewGuid(), GameIdentifier = game.GameIdentifier,
                ProviderIdentifier = source.ProviderIdentifier, CanonicalSourceIdentifier = source.CanonicalSourceIdentifier,
                Title = string.IsNullOrWhiteSpace(draft.Title) ? source.Title : draft.Title.Trim(), UsageRole = draft.UsageRole,
                IsActive = true, TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            };
            mod.Versions.Add(new ModVersion
            {
                PrimaryKey = Guid.NewGuid(), Mod = mod, ModFk = mod.PrimaryKey, VersionNumber = 1,
                VersionLabel = string.IsNullOrWhiteSpace(source.VersionSelector) ? "latest" : source.VersionSelector,
                VersionSelector = source.VersionSelector, InstallerIdentifier = draft.InstallerIdentifier,
                InstallerConfiguration = draft.InstallerConfiguration,
                TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            });
            result.Add(mod);
        }
        return (null, result);
    }

    private async Task<bool> WouldCreateCycleAsync(long root, IReadOnlyCollection<long> children, CancellationToken cancellationToken)
    {
        var edges = await dbContext.ModPackRevisionDependencies.AsNoTracking()
            .Where(q => q.ParentRevision!.IsActive)
            .Select(q => new { q.ParentRevision!.DefinitionFk, q.ChildDefinitionFk }).ToArrayAsync(cancellationToken);
        var graph = edges.GroupBy(q => q.DefinitionFk).ToDictionary(q => q.Key, q => q.Select(x => x.ChildDefinitionFk).ToArray());
        graph[root] = children.ToArray();
        var active = new HashSet<long>();
        bool Visit(long node)
        {
            if (!active.Add(node)) return true;
            var cyclic = graph.TryGetValue(node, out var next) && next.Any(Visit);
            active.Remove(node);
            return cyclic;
        }
        return Visit(root);
    }

    private async Task<MembershipProjection[]> ReadModsAsync(long revisionId, CancellationToken cancellationToken)
        => await dbContext.ModPackRevisionMods.AsNoTracking().Where(q => q.ModPackRevisionFk == revisionId)
            .OrderBy(q => q.Order).Select(q => new MembershipProjection(
                q.ModFk, q.PinnedVersionFk, q.Order, q.Mod!.Title, q.Mod.ProviderIdentifier,
                q.Mod.CanonicalSourceIdentifier, q.Mod.UsageRole.ToString()))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    private async Task FlattenAsync(
        long revisionId,
        ISet<long> active,
        ISet<Guid> resolvedMods,
        ICollection<MembershipProjection> result,
        CancellationToken cancellationToken)
    {
        if (!active.Add(revisionId)) throw new InvalidOperationException("A persisted mod-pack cycle was detected.");
        foreach (var mod in await ReadModsAsync(revisionId, cancellationToken))
        {
            if (resolvedMods.Add(mod.ModFk)) result.Add(mod);
        }
        var dependencies = await dbContext.ModPackRevisionDependencies.AsNoTracking()
            .Where(q => q.ParentRevisionFk == revisionId).OrderBy(q => q.Order).ToArrayAsync(cancellationToken);
        foreach (var dependency in dependencies)
        {
            var childRevision = dependency.PinnedChildRevisionFk ?? await dbContext.ModPackRevisions
                .Where(q => q.DefinitionFk == dependency.ChildDefinitionFk && q.IsActive)
                .OrderByDescending(q => q.TimeStampCreated).Select(q => q.PrimaryKey)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (childRevision == 0) throw new InvalidOperationException("A nested mod pack has no active revision.");
            await FlattenAsync(childRevision, active, resolvedMods, result, cancellationToken);
        }
        active.Remove(revisionId);
    }

    /// <summary>Internal strongly typed membership projection.</summary>
    private sealed record MembershipProjection(
        Guid ModFk,
        Guid? PinnedVersionFk,
        int Order,
        string Title,
        string ProviderIdentifier,
        string CanonicalSourceIdentifier,
        string UsageRole);
}
