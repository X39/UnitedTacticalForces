using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.GameServerImplementations;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Verifies that each game definition declares its Common options, that the tier reaches the configuration
/// response, and that profile summaries carry the identifier of their latest revision.
/// </summary>
public sealed class GameConfigurationTierTests
{
    /// <summary>
    /// The Arma 3 keys that the definition declares as Common.
    /// </summary>
    private static readonly string[] Arma3CommonKeys =
    [
        Arma3ConfigurationImplementation.HeadlessClientPlacementKey,
        "server.cfg@hostname",
        "server.cfg@maxPlayers",
        "server.cfg@persistent",
        "server.cfg@forcedDifficulty",
        "server.cfg@BattlEye",
    ];

    /// <summary>
    /// A definition constructed without a tier declaration reports Advanced.
    /// </summary>
    [Fact]
    public void Definition_WithoutTierDeclaration_IsAdvanced()
    {
        var definition = new GameConfigurationDefinition(
            "test@setting",
            "test",
            "setting",
            "General",
            "Setting",
            "A setting without a tier declaration.",
            EGameConfigurationValueKind.String,
            false,
            false,
            10);

        Assert.Equal(EGameConfigurationTier.Advanced, definition.Tier);
    }

    /// <summary>
    /// Arma 3 marks exactly the documented options as Common and everything else as Advanced.
    /// </summary>
    [Fact]
    public void Arma3_DeclaresExactlyTheDocumentedCommonSet()
    {
        var common = GameConfigurationImplementations.Get(Arma3ConfigurationImplementation.Identifier)
            .Definitions
            .Where(q => q.Tier == EGameConfigurationTier.Common)
            .Select(q => q.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Arma3CommonKeys.Order(StringComparer.Ordinal).ToArray(), common);
    }

    /// <summary>
    /// No sensitive option of any registered game is Common, so the tier never promotes a secret option.
    /// </summary>
    [Fact]
    public void NoSensitiveDefinition_IsCommon()
    {
        foreach (var implementation in GameConfigurationImplementations.All)
        {
            Assert.DoesNotContain(
                implementation.Definitions,
                q => q.Sensitive && q.Tier == EGameConfigurationTier.Common);
        }
    }

    /// <summary>
    /// The configuration response carries the declared tier on every entry.
    /// </summary>
    /// <returns>A task completing after the entries were verified.</returns>
    [Fact]
    public async Task Get_ReturnsTheDeclaredTierPerEntry()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier;
        await store.DbContext.SaveChangesAsync();
        var controller = CreateController(
            store,
            HostingTestData.ServerPrincipal(
                seed.User,
                seed.Server,
                Claims.ResourceBased.Server.View,
                Claims.ResourceBased.Server.Modify));

        var result = await controller.GetAsync(seed.Server.PrimaryKey, CancellationToken.None);

        var dto = Assert.IsType<GameConfigurationDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(
            EGameConfigurationTier.Common,
            Assert.Single(dto.Entries, q => q.Key == "server.cfg@hostname").Tier);
        Assert.Equal(
            EGameConfigurationTier.Advanced,
            Assert.Single(dto.Entries, q => q.Key == "basic.cfg@MaxMsgSend").Tier);
    }

    /// <summary>
    /// ASKA and Counter-Strike 2 mark exactly their documented options as Common.
    /// </summary>
    /// <param name="gameIdentifier">The game whose definitions are inspected.</param>
    /// <param name="settings">The settings expected to be Common.</param>
    [Theory]
    [InlineData(AskaConfigurationImplementation.Identifier, new[] { "display name", "mode", "region", "server name" })]
    [InlineData(
        CounterStrike2ConfigurationImplementation.Identifier,
        new[] { "bot_quota", "hostname", "maxplayers", "mp_maxrounds" })]
    public void OtherGames_DeclareExactlyTheDocumentedCommonSet(string gameIdentifier, string[] settings)
    {
        var common = GameConfigurationImplementations.Get(gameIdentifier)
            .Definitions
            .Where(q => q.Tier == EGameConfigurationTier.Common)
            .Select(q => q.Setting)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(settings, common);
    }

    /// <summary>
    /// Every registered game with a non-sensitive option declares at least one Common option.
    /// </summary>
    [Fact]
    public void EveryGame_DeclaresAtLeastOneCommonDefinition()
    {
        foreach (var implementation in GameConfigurationImplementations.All)
        {
            if (implementation.Definitions.Any(q => !q.Sensitive))
                Assert.Contains(implementation.Definitions, q => q.Tier == EGameConfigurationTier.Common);
        }
    }

    /// <summary>
    /// The server profile options carry the identifier of the highest revision and null without revisions.
    /// </summary>
    /// <returns>A task completing after both summaries were verified.</returns>
    [Fact]
    public async Task ProfileOptions_CarryTheLatestRevisionId()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier;
        await store.DbContext.SaveChangesAsync();
        var withRevisions = await AddProfileAsync(store, seed, "Alpha", revisionCount: 2);
        var withoutRevisions = await AddProfileAsync(store, seed, "Beta", revisionCount: 0);
        var controller = CreateController(
            store,
            HostingTestData.ServerPrincipal(
                seed.User,
                seed.Server,
                Claims.ResourceBased.Server.View,
                Claims.ResourceBased.Server.Modify));

        var result = await controller.GetProfileOptionsAsync(seed.Server.PrimaryKey, CancellationToken.None);

        var summaries = Assert.IsAssignableFrom<IReadOnlyCollection<ConfigurationProfileSummaryDto>>(
            Assert.IsType<OkObjectResult>(result).Value);
        var alpha = Assert.Single(summaries, q => q.PrimaryKey == withRevisions.PrimaryKey);
        Assert.Equal(2, alpha.LatestRevisionNumber);
        Assert.Equal(
            withRevisions.Revisions.Single(q => q.RevisionNumber == 2).PrimaryKey,
            alpha.LatestRevisionId);
        var beta = Assert.Single(summaries, q => q.PrimaryKey == withoutRevisions.PrimaryKey);
        Assert.Equal(0, beta.LatestRevisionNumber);
        Assert.Null(beta.LatestRevisionId);
    }

    /// <summary>
    /// Every profile summary projection of the profiles controller sets the latest revision identifier.
    /// </summary>
    /// <returns>A task completing after the options, list, detail and creation summaries were verified.</returns>
    [Fact]
    public async Task ProfilesController_SummariesCarryTheLatestRevisionId()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var profile = await AddProfileAsync(store, seed, "Alpha", revisionCount: 2);
        var latestId = profile.Revisions.Single(q => q.RevisionNumber == 2).PrimaryKey;
        var controller = new ConfigurationProfilesController(
            store.DbContext,
            new GameServerConfigurationService(store.DbContext),
            new HostingAuditWriter())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = AdministratorPrincipal(seed) },
            },
        };

        var options = Assert.IsAssignableFrom<IEnumerable<ConfigurationProfileSummaryDto>>(
            Assert.IsType<OkObjectResult>(
                await controller.GetOptionsAsync(
                    Arma3GameServerDefinitionRegistration.GameIdentifier,
                    CancellationToken.None)).Value);
        Assert.Equal(latestId, Assert.Single(options).LatestRevisionId);

        var all = Assert.IsAssignableFrom<IEnumerable<ConfigurationProfileSummaryDto>>(
            Assert.IsType<OkObjectResult>(await controller.GetAllAsync(null, CancellationToken.None)).Value);
        Assert.Equal(latestId, Assert.Single(all).LatestRevisionId);

        var detail = Assert.IsType<OkObjectResult>(
            await controller.GetAsync(profile.PrimaryKey, CancellationToken.None));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(detail.Value));
        Assert.Equal(
            latestId,
            json.RootElement.GetProperty("Profile").GetProperty("LatestRevisionId").GetGuid());

        var created = Assert.IsType<CreatedAtActionResult>(await controller.CreateAsync(
            new ConfigurationProfileCreationPayload
            {
                Title = "Fresh",
                GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier,
            },
            CancellationToken.None));
        var summary = Assert.IsType<ConfigurationProfileSummaryDto>(created.Value);
        var firstRevision = await store.DbContext.ConfigurationProfileRevisions
            .SingleAsync(q => q.ConfigurationProfileFk == summary.PrimaryKey);
        Assert.Equal(firstRevision.PrimaryKey, summary.LatestRevisionId);
    }

    /// <summary>
    /// Creates a principal with the administrative claim for the seeded user.
    /// </summary>
    /// <param name="seed">The seed holding the user.</param>
    /// <returns>The authenticated administrator principal.</returns>
    private static ClaimsPrincipal AdministratorPrincipal(HostingTestSeed seed)
        => new(new ClaimsIdentity(
            [
                new Claim(System.Security.Claims.ClaimTypes.Name, seed.User.PrimaryKey.ToString()),
                new Claim(Claims.Administrative.All, string.Empty),
            ],
            X39.UnitedTacticalForces.Api.Constants.AuthorizationSchemas.Api,
            System.Security.Claims.ClaimTypes.Name,
            System.Security.Claims.ClaimTypes.Role));

    /// <summary>
    /// Persists an Arma 3 configuration profile with the requested number of revisions.
    /// </summary>
    /// <param name="store">The test store to write to.</param>
    /// <param name="seed">The seed providing the author.</param>
    /// <param name="title">The profile title.</param>
    /// <param name="revisionCount">The number of revisions, numbered from one.</param>
    /// <returns>The persisted profile with its revisions loaded.</returns>
    private static async Task<ConfigurationProfile> AddProfileAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        string title,
        int revisionCount)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var profile = new ConfigurationProfile
        {
            PrimaryKey = Guid.NewGuid(),
            Title = title,
            GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier,
            CreatedByFk = seed.User.PrimaryKey,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            Revisions = Enumerable.Range(1, revisionCount)
                .Select(number => new ConfigurationProfileRevision
                {
                    PrimaryKey = Guid.NewGuid(),
                    RevisionNumber = number,
                    CreatedByFk = seed.User.PrimaryKey,
                    TimeStampCreated = now,
                    Values = [],
                })
                .ToList(),
        };
        store.DbContext.Add(profile);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        return profile;
    }

    /// <summary>
    /// Creates the configuration controller bound to the store and the principal.
    /// </summary>
    /// <param name="store">The test store providing the database context.</param>
    /// <param name="principal">The principal that issues the requests.</param>
    /// <returns>The controller ready to be called directly.</returns>
    private static GameServerConfigurationController CreateController(
        HostingTestStore store,
        ClaimsPrincipal principal)
        => new(
            store.DbContext,
            new GameServerConfigurationService(store.DbContext),
            new HostingAuthorizationService(store.DbContext),
            new HostingAuditWriter())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
}
