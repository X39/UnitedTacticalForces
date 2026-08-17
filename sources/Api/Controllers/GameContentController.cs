using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Authorize]
[Route("game-content")]
public sealed partial class GameContentController(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog gameDefinitions,
    IServerFileStorage fileStorage,
    IHostingAuthorizationService authorizationService) : ControllerBase
{
    [HttpPost("servers/{serverId:guid}/artifacts/{artifactIdentifier}")]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
    public async Task<IActionResult> UploadArtifactAsync(
        Guid serverId,
        string artifactIdentifier,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!authorizationService.HasServerPermission(User, serverId, Claims.ResourceBased.Server.Modify)) return Forbid();
        if (!ArtifactIdentifierRegex().IsMatch(artifactIdentifier) || file.Length is <= 0 or > 2L * 1024 * 1024 * 1024)
            return BadRequest("The artifact identity or size is invalid.");
        var serverGame = await dbContext.GameServers.Where(q => q.PrimaryKey == serverId)
            .Select(q => q.GameIdentifier).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (serverGame is null) return NotFound();
        var expectedChecksum = await dbContext.ModVersions
            .Where(q => q.Mod!.GameIdentifier == serverGame
                        && q.ArtifactIdentifier == artifactIdentifier
                        && q.SourceKind == EContentSourceKind.UploadedArtifact)
            .Select(q => q.ChecksumSha256).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (expectedChecksum is null) return BadRequest("The artifact is not declared by a compatible mod version.");
        await using (var checksumStream = file.OpenReadStream())
        {
            var checksum = Convert.ToHexString(await SHA256.HashDataAsync(checksumStream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(checksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
                return BadRequest("The uploaded artifact checksum does not match its immutable version record.");
        }
        await using var content = file.OpenReadStream();
        await fileStorage.UploadAsync(serverId, null, EFileContext.Persistent,
            $"content-artifacts/{artifactIdentifier}.zip", content, true, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
    [HttpGet("terrains")]
    public async Task<IActionResult> GetTerrainsAsync([FromQuery] string gameIdentifier, CancellationToken cancellationToken)
        => Ok(await dbContext.Terrains.AsNoTracking()
            .Where(q => q.GameIdentifier == gameIdentifier && q.IsActive)
            .OrderBy(q => q.Title)
            .Select(q => new
            {
                PrimaryKey = q.PrimaryKey.ToString(), q.GameIdentifier, q.Title, q.ImageAssetFk,
                Versions = q.Versions.OrderByDescending(v => v.VersionNumber).Select(v => new
                {
                    v.PrimaryKey, v.VersionNumber, v.VersionLabel, v.SourceKind, v.SourceIdentifier,
                    v.ArtifactIdentifier, v.ChecksumSha256, v.ArtifactSizeBytes, v.Metadata,
                }),
            }).ToArrayAsync(cancellationToken).ConfigureAwait(false));

    [Authorize(Claims.Creation.Terrain)]
    [HttpPost("terrains")]
    public async Task<IActionResult> CreateTerrainAsync(ContentIdentityPayload payload, CancellationToken cancellationToken)
    {
        var definition = FindDefinition(payload.GameIdentifier);
        if (definition is null) return BadRequest("Game identifier is not registered.");
        var validation = ValidateVersion(definition, "terrain", payload.Version);
        if (validation is not null) return BadRequest(validation);
        var terrain = new Terrain
        {
            GameIdentifier = definition.GameIdentifier,
            Title = payload.Title.Trim(),
            ImageAssetFk = payload.ImageAssetFk,
            IsActive = true,
        };
        terrain.Versions.Add(CreateTerrainVersion(terrain, 1, payload.Version));
        dbContext.Terrains.Add(terrain);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { terrain.PrimaryKey, VersionId = terrain.Versions.Single().PrimaryKey });
    }

    [Authorize(Claims.ResourceBased.Terrain.Modify)]
    [HttpPost("terrains/{terrainId:long}/versions")]
    public async Task<IActionResult> CreateTerrainVersionAsync(
        long terrainId,
        ContentVersionPayload payload,
        CancellationToken cancellationToken)
    {
        var terrain = await dbContext.Terrains.Include(q => q.Versions)
            .SingleOrDefaultAsync(q => q.PrimaryKey == terrainId, cancellationToken).ConfigureAwait(false);
        if (terrain is null) return NotFound();
        var definition = FindDefinition(terrain.GameIdentifier)!;
        var validation = ValidateVersion(definition, "terrain", payload);
        if (validation is not null) return BadRequest(validation);
        var version = CreateTerrainVersion(terrain, terrain.Versions.DefaultIfEmpty().Max(q => q?.VersionNumber ?? 0) + 1, payload);
        dbContext.TerrainVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { version.PrimaryKey, version.VersionNumber });
    }

    [HttpGet("scenarios")]
    public async Task<IActionResult> GetScenariosAsync([FromQuery] string gameIdentifier, CancellationToken cancellationToken)
        => Ok(await dbContext.Scenarios.AsNoTracking()
            .Where(q => q.GameIdentifier == gameIdentifier && q.IsActive)
            .OrderBy(q => q.Title)
            .Select(q => new
            {
                q.PrimaryKey, q.GameIdentifier, q.Title, q.Description,
                Versions = q.Versions.OrderByDescending(v => v.VersionNumber).Select(v => new
                {
                    v.PrimaryKey, v.VersionNumber, v.VersionLabel, v.SourceKind, v.SourceIdentifier,
                    v.ArtifactIdentifier, v.ChecksumSha256, v.ArtifactSizeBytes, v.Metadata, v.Changelog,
                }),
            }).ToArrayAsync(cancellationToken).ConfigureAwait(false));

    [Authorize(Claims.Creation.ModPack)]
    [HttpPost("scenarios")]
    public async Task<IActionResult> CreateScenarioAsync(ScenarioPayload payload, CancellationToken cancellationToken)
    {
        var definition = FindDefinition(payload.GameIdentifier);
        if (definition is null) return BadRequest("Game identifier is not registered.");
        var validation = ValidateVersion(definition, "scenario", payload.Version);
        if (validation is not null) return BadRequest(validation);
        var scenario = new Scenario
        {
            PrimaryKey = Guid.NewGuid(), GameIdentifier = definition.GameIdentifier, Title = payload.Title.Trim(),
            Description = payload.Description, TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        scenario.Versions.Add(CreateScenarioVersion(scenario, 1, payload.Version));
        dbContext.Scenarios.Add(scenario);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { scenario.PrimaryKey, VersionId = scenario.Versions.Single().PrimaryKey });
    }

    [Authorize(Claims.Creation.ModPack)]
    [HttpPost("scenarios/{scenarioId:guid}/versions")]
    public async Task<IActionResult> CreateScenarioVersionAsync(
        Guid scenarioId,
        ScenarioVersionPayload payload,
        CancellationToken cancellationToken)
    {
        var scenario = await dbContext.Scenarios.Include(q => q.Versions)
            .SingleOrDefaultAsync(q => q.PrimaryKey == scenarioId, cancellationToken).ConfigureAwait(false);
        if (scenario is null) return NotFound();
        var definition = FindDefinition(scenario.GameIdentifier)!;
        var validation = ValidateVersion(definition, "scenario", payload.Version);
        if (validation is not null) return BadRequest(validation);
        if (!await RequiredPacksAreCompatible(payload.RequiredModPacks, scenario.GameIdentifier, cancellationToken))
            return BadRequest("A required modpack does not exist or belongs to another game.");
        var version = CreateScenarioVersion(
            scenario,
            scenario.Versions.DefaultIfEmpty().Max(q => q?.VersionNumber ?? 0) + 1,
            payload.Version);
        version.RequiredModPacks = payload.RequiredModPacks.Select(q => new ScenarioVersionRequiredModPack
        {
            ScenarioVersion = version, ModPackDefinitionFk = q.DefinitionId, PinnedRevisionFk = q.PinnedRevisionId,
        }).ToList();
        dbContext.ScenarioVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { version.PrimaryKey, version.VersionNumber });
    }

    [HttpGet("mods")]
    public async Task<IActionResult> GetModsAsync([FromQuery] string gameIdentifier, CancellationToken cancellationToken)
        => Ok(await dbContext.Mods.AsNoTracking().Where(q => q.GameIdentifier == gameIdentifier && q.IsActive)
            .OrderBy(q => q.Title).Select(q => new
            {
                q.PrimaryKey, q.GameIdentifier, q.Title,
                Versions = q.Versions.OrderByDescending(v => v.VersionNumber).Select(v => new
                {
                    v.PrimaryKey, v.VersionNumber, v.VersionLabel, v.SourceKind, v.SourceIdentifier, v.UsageRole,
                    v.InstallerIdentifier, v.InstallerConfiguration, v.ArtifactIdentifier, v.ChecksumSha256,
                    v.ArtifactSizeBytes,
                }),
            }).ToArrayAsync(cancellationToken).ConfigureAwait(false));

    [Authorize(Claims.Creation.ModPack)]
    [HttpPost("mods")]
    public async Task<IActionResult> CreateModAsync(ModPayload payload, CancellationToken cancellationToken)
    {
        var definition = FindDefinition(payload.GameIdentifier);
        if (definition is null) return BadRequest("Game identifier is not registered.");
        var validation = ValidateModVersion(definition, payload.Version);
        if (validation is not null) return BadRequest(validation);
        if (await dbContext.ModVersions.AnyAsync(q => q.Mod!.GameIdentifier == definition.GameIdentifier
                                                       && q.SourceKind == payload.Version.SourceKind
                                                       && q.SourceIdentifier == payload.Version.SourceIdentifier,
                cancellationToken).ConfigureAwait(false))
            return Conflict("A Mod with this game/source identity already exists.");
        var mod = new Mod
        {
            PrimaryKey = Guid.NewGuid(), GameIdentifier = definition.GameIdentifier, Title = payload.Title.Trim(),
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        mod.Versions.Add(CreateModVersion(mod, 1, payload.Version));
        dbContext.Mods.Add(mod);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { mod.PrimaryKey, VersionId = mod.Versions.Single().PrimaryKey });
    }

    [Authorize(Claims.Creation.ModPack)]
    [HttpPost("mods/{modId:guid}/versions")]
    public async Task<IActionResult> CreateModVersionAsync(Guid modId, ModVersionPayload payload, CancellationToken cancellationToken)
    {
        var mod = await dbContext.Mods.Include(q => q.Versions)
            .SingleOrDefaultAsync(q => q.PrimaryKey == modId, cancellationToken).ConfigureAwait(false);
        if (mod is null) return NotFound();
        var validation = ValidateModVersion(FindDefinition(mod.GameIdentifier)!, payload);
        if (validation is not null) return BadRequest(validation);
        var version = CreateModVersion(mod, mod.Versions.DefaultIfEmpty().Max(q => q?.VersionNumber ?? 0) + 1, payload);
        dbContext.ModVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { version.PrimaryKey, version.VersionNumber });
    }

    [Authorize(Claims.ResourceBased.ModPack.Modify)]
    [HttpPut("modpacks/{revisionId:long}/members")]
    public async Task<IActionResult> SetModPackMembersAsync(
        long revisionId,
        ModPackMembersPayload payload,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.ModPackRevisions.Include(q => q.Definition)
            .SingleOrDefaultAsync(q => q.PrimaryKey == revisionId, cancellationToken).ConfigureAwait(false);
        if (revision is null) return NotFound();
        if (payload.Mods.Select(q => q.ModId).Distinct().Count() != payload.Mods.Count
            || payload.Dependencies.Select(q => q.DefinitionId).Distinct().Count() != payload.Dependencies.Count)
            return BadRequest("Duplicate mod or nested-modpack membership is not allowed.");
        var mods = await dbContext.Mods.Where(q => payload.Mods.Select(x => x.ModId).Contains(q.PrimaryKey)).ToArrayAsync(cancellationToken);
        var definitions = await dbContext.ModPackDefinitions.Where(q => payload.Dependencies.Select(x => x.DefinitionId).Contains(q.PrimaryKey)).ToArrayAsync(cancellationToken);
        if (mods.Length != payload.Mods.Count || definitions.Length != payload.Dependencies.Count
            || mods.Any(q => q.GameIdentifier != revision.Definition!.GameIdentifier)
            || definitions.Any(q => q.GameIdentifier != revision.Definition!.GameIdentifier))
            return BadRequest("All members must exist and belong to the same game.");
        if (await WouldCreateCycle(revision.DefinitionFk, payload.Dependencies.Select(q => q.DefinitionId), cancellationToken))
            return BadRequest("Nested modpacks must not contain a cycle.");
        await dbContext.ModPackRevisionMods.Where(q => q.ModPackRevisionFk == revisionId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ModPackRevisionDependencies.Where(q => q.ParentRevisionFk == revisionId).ExecuteDeleteAsync(cancellationToken);
        dbContext.ModPackRevisionMods.AddRange(payload.Mods.Select((q, index) => new ModPackRevisionMod
        {
            ModPackRevisionFk = revisionId, ModFk = q.ModId, PinnedVersionFk = q.PinnedVersionId, Order = index,
        }));
        dbContext.ModPackRevisionDependencies.AddRange(payload.Dependencies.Select((q, index) => new ModPackRevisionDependency
        {
            ParentRevisionFk = revisionId, ChildDefinitionFk = q.DefinitionId,
            PinnedChildRevisionFk = q.PinnedRevisionId, Order = index,
        }));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private GameServerDefinition? FindDefinition(string identifier)
        => gameDefinitions.Find(identifier.Trim().ToLowerInvariant());

    private static string? ValidateVersion(GameServerDefinition definition, string kind, ContentVersionPayload payload)
    {
        if (!Enum.IsDefined(payload.SourceKind)) return "Content source kind is invalid.";
        if (!definition.ContentCapabilities.TerrainSourceKinds.Contains(payload.SourceKind.ToString(), StringComparer.OrdinalIgnoreCase))
            return "The content source is not supported by this game.";
        if (!IsValidJson(payload.Metadata)) return "Version metadata must be valid JSON.";
        if (payload.SourceKind == EContentSourceKind.UploadedArtifact
            && (string.IsNullOrWhiteSpace(payload.ArtifactIdentifier) || !Sha256Regex().IsMatch(payload.ChecksumSha256 ?? string.Empty)))
            return "Uploaded artifacts require an artifact identity and SHA-256 checksum.";
        if (definition.GameIdentifier == CounterStrike2GameServerDefinitionRegistration.GameIdentifier && kind == "terrain")
        {
            if (payload.SourceKind == EContentSourceKind.SteamWorkshop)
            {
                if (!ulong.TryParse(payload.SourceIdentifier, out var workshopId) || workshopId == 0)
                    return "Workshop maps require a positive Steam item identity.";
            }
            if (payload.SourceKind == EContentSourceKind.BuiltIn && !MapIdentifierRegex().IsMatch(payload.SourceIdentifier))
                return "Built-in CS2 map identifiers are invalid.";
        }
        if (definition.GameIdentifier == CounterStrike2GameServerDefinitionRegistration.GameIdentifier && kind == "scenario")
        {
            try
            {
                using var metadata = JsonDocument.Parse(payload.Metadata);
                var gameType = metadata.RootElement.GetProperty("gameType").GetInt32();
                var gameMode = metadata.RootElement.GetProperty("gameMode").GetInt32();
                if (!AllowedCs2Modes.Contains((gameType, gameMode))) return "CS2 game type/mode is not allowlisted.";
            }
            catch (Exception)
            {
                return "CS2 scenario metadata requires integer gameType and gameMode values.";
            }
        }
        return null;
    }

    private static string? ValidateModVersion(GameServerDefinition definition, ModVersionPayload payload)
    {
        var common = ValidateVersion(definition, "mod", payload);
        if (common is not null) return common;
        if (!definition.ContentCapabilities.ModInstallerIdentifiers.Contains(payload.InstallerIdentifier, StringComparer.Ordinal))
            return "The installer identifier is not registered for this game.";
        return IsValidJson(payload.InstallerConfiguration) ? null : "Installer configuration must be valid JSON.";
    }

    private async Task<bool> RequiredPacksAreCompatible(IReadOnlyCollection<ModPackReferencePayload> packs, string gameIdentifier, CancellationToken cancellationToken)
        => await dbContext.ModPackDefinitions.CountAsync(q => packs.Select(p => p.DefinitionId).Contains(q.PrimaryKey) && q.GameIdentifier == gameIdentifier, cancellationToken) == packs.Select(q => q.DefinitionId).Distinct().Count();

    private async Task<bool> WouldCreateCycle(long rootDefinitionId, IEnumerable<long> children, CancellationToken cancellationToken)
    {
        var edges = await dbContext.ModPackRevisionDependencies.AsNoTracking()
            .Select(q => new { q.ParentRevision!.DefinitionFk, q.ChildDefinitionFk }).ToArrayAsync(cancellationToken);
        var graph = edges.GroupBy(q => q.DefinitionFk).ToDictionary(q => q.Key, q => q.Select(x => x.ChildDefinitionFk).ToArray());
        graph[rootDefinitionId] = children.ToArray();
        var active = new HashSet<long>();
        var visited = new HashSet<long>();
        bool Visit(long node)
        {
            if (!active.Add(node)) return true;
            if (visited.Add(node) && graph.TryGetValue(node, out var next) && next.Any(Visit)) return true;
            active.Remove(node);
            return false;
        }
        return Visit(rootDefinitionId);
    }

    private static TerrainVersion CreateTerrainVersion(Terrain terrain, int number, ContentVersionPayload payload) => new()
    {
        PrimaryKey = Guid.NewGuid(), Terrain = terrain, VersionNumber = number, VersionLabel = payload.VersionLabel,
        SourceKind = payload.SourceKind, SourceIdentifier = payload.SourceIdentifier, ArtifactIdentifier = payload.ArtifactIdentifier,
        ChecksumSha256 = payload.ChecksumSha256, ArtifactSizeBytes = payload.ArtifactSizeBytes, Metadata = payload.Metadata,
        TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
    };

    private static ScenarioVersion CreateScenarioVersion(Scenario scenario, int number, ContentVersionPayload payload) => new()
    {
        PrimaryKey = Guid.NewGuid(), Scenario = scenario, ScenarioFk = scenario.PrimaryKey, VersionNumber = number,
        VersionLabel = payload.VersionLabel, SourceKind = payload.SourceKind, SourceIdentifier = payload.SourceIdentifier,
        ArtifactIdentifier = payload.ArtifactIdentifier, ChecksumSha256 = payload.ChecksumSha256,
        ArtifactSizeBytes = payload.ArtifactSizeBytes, Metadata = payload.Metadata,
        TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
    };

    private static ModVersion CreateModVersion(Mod mod, int number, ModVersionPayload payload) => new()
    {
        PrimaryKey = Guid.NewGuid(), Mod = mod, ModFk = mod.PrimaryKey, VersionNumber = number,
        VersionLabel = payload.VersionLabel, SourceKind = payload.SourceKind, SourceIdentifier = payload.SourceIdentifier,
        UsageRole = payload.UsageRole, InstallerIdentifier = payload.InstallerIdentifier,
        InstallerConfiguration = payload.InstallerConfiguration, ArtifactIdentifier = payload.ArtifactIdentifier,
        ChecksumSha256 = payload.ChecksumSha256, ArtifactSizeBytes = payload.ArtifactSizeBytes,
        TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
    };

    private static bool IsValidJson(string value)
    {
        try { using var _ = JsonDocument.Parse(value); return true; }
        catch (JsonException) { return false; }
    }

    private static readonly HashSet<(int, int)> AllowedCs2Modes = [(0, 1), (0, 0), (0, 2), (1, 2), (1, 0)];
    [GeneratedRegex("^[a-z0-9_]{1,64}$", RegexOptions.CultureInvariant)] private static partial Regex MapIdentifierRegex();
    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)] private static partial Regex Sha256Regex();
    [GeneratedRegex("^[a-zA-Z0-9._-]{1,128}$", RegexOptions.CultureInvariant)] private static partial Regex ArtifactIdentifierRegex();

    public record ContentIdentityPayload(string GameIdentifier, string Title, Guid? ImageAssetFk, ContentVersionPayload Version);
    public record ScenarioPayload(string GameIdentifier, string Title, string? Description, ContentVersionPayload Version);
    public record ContentVersionPayload(EContentSourceKind SourceKind, string SourceIdentifier, string VersionLabel,
        string Metadata = "{}", string? ArtifactIdentifier = null, string? ChecksumSha256 = null, long? ArtifactSizeBytes = null);
    public record ScenarioVersionPayload(ContentVersionPayload Version, IReadOnlyCollection<ModPackReferencePayload> RequiredModPacks);
    public record ModPayload(string GameIdentifier, string Title, ModVersionPayload Version);
    public record ModVersionPayload(EContentSourceKind SourceKind, string SourceIdentifier, string VersionLabel,
        EModUsageRole UsageRole, string InstallerIdentifier, string InstallerConfiguration = "{}", string Metadata = "{}",
        string? ArtifactIdentifier = null, string? ChecksumSha256 = null, long? ArtifactSizeBytes = null)
        : ContentVersionPayload(SourceKind, SourceIdentifier, VersionLabel, Metadata, ArtifactIdentifier, ChecksumSha256, ArtifactSizeBytes);
    public record ModPackReferencePayload(long DefinitionId, long? PinnedRevisionId);
    public record ModMembershipPayload(Guid ModId, Guid? PinnedVersionId);
    public record ModPackMembersPayload(IReadOnlyList<ModMembershipPayload> Mods, IReadOnlyList<ModPackReferencePayload> Dependencies);
}
