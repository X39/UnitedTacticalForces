using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Verifies that applied changes are recorded as waiting for the next start, projected on the server for every
/// viewer, and consumed at the documented points.
/// </summary>
public sealed class PendingStartChangeTrackingTests
{
    /// <summary>
    /// A fixed instant used as the first change time in the helper tests.
    /// </summary>
    private static readonly Instant FirstChange = Instant.FromUnixTimeSeconds(1_700_000_100);

    /// <summary>
    /// A fixed instant used as a later change time in the helper tests.
    /// </summary>
    private static readonly Instant LaterChange = Instant.FromUnixTimeSeconds(1_700_000_200);

    /// <summary>
    /// A configuration update records the Configuration area and projects it to a viewer.
    /// </summary>
    /// <returns>A task completing after the persisted columns and the projection were verified.</returns>
    [Fact]
    public async Task ConfigurationUpdate_MarksConfigurationAndProjectsItToViewers()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedArmaServerAsync(store);
        var controller = CreateController(store, ModifierPrincipal(seed));

        var result = await controller.UpdateAsync(
            seed.Server.PrimaryKey,
            MaxPlayersUpdate(80),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.Configuration, persisted.PendingStartChangeAreas);
        Assert.NotNull(persisted.PendingStartSince);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);
        var dto = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetServerAsync(seed.Server.PrimaryKey, viewer);
        Assert.NotNull(dto);
        Assert.Equal([EGameServerChangeArea.Configuration], dto.ChangesPendingStartAreas);
        Assert.True(dto.HasChangesPendingStart);
    }

    /// <summary>
    /// A later change keeps the since-timestamp of the first pending change.
    /// </summary>
    /// <returns>A task completing after both updates were applied and the timestamp compared.</returns>
    [Fact]
    public async Task SecondConfigurationUpdate_KeepsFirstPendingStartSince()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedArmaServerAsync(store);
        var controller = CreateController(store, ModifierPrincipal(seed));
        await controller.UpdateAsync(seed.Server.PrimaryKey, MaxPlayersUpdate(80), CancellationToken.None);
        store.DbContext.ChangeTracker.Clear();
        var first = (await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey))
            .PendingStartSince;
        Assert.NotNull(first);

        await controller.UpdateAsync(seed.Server.PrimaryKey, MaxPlayersUpdate(90), CancellationToken.None);
        store.DbContext.ChangeTracker.Clear();

        var second = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(first, second.PendingStartSince);
        Assert.Equal(EGameServerChangeArea.Configuration, second.PendingStartChangeAreas);
    }

    /// <summary>
    /// An update that neither sets nor resets a key marks nothing.
    /// </summary>
    /// <returns>A task completing after the persisted columns were verified.</returns>
    [Fact]
    public async Task EmptyConfigurationUpdate_MarksNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedArmaServerAsync(store);
        var controller = CreateController(store, ModifierPrincipal(seed));

        var result = await controller.UpdateAsync(
            seed.Server.PrimaryKey,
            new ServerConfigurationUpdatePayload(),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.None, persisted.PendingStartChangeAreas);
        Assert.Null(persisted.PendingStartSince);
    }

    /// <summary>
    /// Selecting a different profile marks Configuration, while repeating the same selection marks nothing.
    /// </summary>
    /// <returns>A task completing after both selections were verified.</returns>
    [Fact]
    public async Task ProfileSelection_MarksOnlyWhenTheSelectionDiffers()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedArmaServerAsync(store);
        var profile = await AddProfileAsync(store, seed);
        var controller = CreateController(store, ModifierPrincipal(seed));

        var unchanged = await controller.SelectProfileAsync(
            seed.Server.PrimaryKey,
            new GameServerConfigurationProfileSelectionPayload(),
            CancellationToken.None);
        Assert.IsType<NoContentResult>(unchanged);
        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(
            EGameServerChangeArea.None,
            (await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey))
            .PendingStartChangeAreas);

        var changed = await controller.SelectProfileAsync(
            seed.Server.PrimaryKey,
            new GameServerConfigurationProfileSelectionPayload { ProfileId = profile.PrimaryKey },
            CancellationToken.None);
        Assert.IsType<NoContentResult>(changed);
        store.DbContext.ChangeTracker.Clear();
        var marked = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.Configuration, marked.PendingStartChangeAreas);

        marked.PendingStartChangeAreas = EGameServerChangeArea.None;
        marked.PendingStartSince = null;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var repeated = await controller.SelectProfileAsync(
            seed.Server.PrimaryKey,
            new GameServerConfigurationProfileSelectionPayload { ProfileId = profile.PrimaryKey },
            CancellationToken.None);
        Assert.IsType<NoContentResult>(repeated);
        store.DbContext.ChangeTracker.Clear();
        var again = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.None, again.PendingStartChangeAreas);
        Assert.Null(again.PendingStartSince);
    }

    /// <summary>
    /// A change request for an archived server is rejected and marks nothing.
    /// </summary>
    /// <returns>A task completing after the rejection and the persisted columns were verified.</returns>
    [Fact]
    public async Task ConfigurationUpdate_OnArchivedServer_ReturnsNotFoundAndMarksNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedArmaServerAsync(store);
        seed.Server.IsArchived = true;
        await store.DbContext.SaveChangesAsync();
        var controller = CreateController(store, ModifierPrincipal(seed));

        var result = await controller.UpdateAsync(
            seed.Server.PrimaryKey,
            MaxPlayersUpdate(80),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.None, persisted.PendingStartChangeAreas);
        Assert.Null(persisted.PendingStartSince);
    }

    /// <summary>
    /// Marking the None area changes nothing, and marking sets the since-timestamp only for the first change.
    /// </summary>
    [Fact]
    public void MarkChanged_IgnoresNoneAndKeepsTheFirstSinceTimestamp()
    {
        var server = new GameServer();

        PendingStartChanges.MarkChanged(server, EGameServerChangeArea.None, FirstChange);
        Assert.Equal(EGameServerChangeArea.None, server.PendingStartChangeAreas);
        Assert.Null(server.PendingStartSince);

        PendingStartChanges.MarkChanged(server, EGameServerChangeArea.ModPack, FirstChange);
        PendingStartChanges.MarkChanged(server, EGameServerChangeArea.Credentials, LaterChange);

        Assert.Equal(EGameServerChangeArea.ModPack | EGameServerChangeArea.Credentials, server.PendingStartChangeAreas);
        Assert.Equal(FirstChange, server.PendingStartSince);
    }

    /// <summary>
    /// Deployment creation consumes only the mod pack area and resets the since-timestamp once nothing remains.
    /// </summary>
    [Fact]
    public void ConsumeAtDeploymentCreation_RemovesOnlyModPackAndResetsSinceWhenEmpty()
    {
        var server = new GameServer();
        PendingStartChanges.MarkChanged(
            server,
            EGameServerChangeArea.ModPack | EGameServerChangeArea.Configuration,
            FirstChange);

        Assert.True(PendingStartChanges.ConsumeAtDeploymentCreation(server));
        Assert.Equal(EGameServerChangeArea.Configuration, server.PendingStartChangeAreas);
        Assert.Equal(FirstChange, server.PendingStartSince);

        Assert.False(PendingStartChanges.ConsumeAtDeploymentCreation(server));

        server.PendingStartChangeAreas = EGameServerChangeArea.ModPack;
        Assert.True(PendingStartChanges.ConsumeAtDeploymentCreation(server));
        Assert.Equal(EGameServerChangeArea.None, server.PendingStartChangeAreas);
        Assert.Null(server.PendingStartSince);
    }

    /// <summary>
    /// Only the primary command of a Start or Restart job consumes configuration and credentials.
    /// </summary>
    /// <param name="kind">The job kind the command belongs to.</param>
    /// <param name="actionIdentifier">The action identifier of the command.</param>
    /// <param name="jobActionIdentifier">The action identifier of the owning job.</param>
    /// <param name="consumes">Whether the command is expected to consume the areas.</param>
    [Theory]
    [InlineData(ELifecycleJobKind.Start, "start", "start", true)]
    [InlineData(ELifecycleJobKind.Restart, "restart", "restart", true)]
    [InlineData(ELifecycleJobKind.Start, "readiness", "start", false)]
    [InlineData(ELifecycleJobKind.Stop, "stop", "stop", false)]
    [InlineData(ELifecycleJobKind.CollectLogs, "collect-logs", "collect-logs", false)]
    [InlineData(ELifecycleJobKind.Install, "install", "install", false)]
    [InlineData(ELifecycleJobKind.Update, "update", "update", false)]
    [InlineData(ELifecycleJobKind.Custom, "custom", "custom", false)]
    public void ConsumeAtCommandCreation_ConsumesOnlyForPrimaryStartAndRestartCommands(
        ELifecycleJobKind kind,
        string actionIdentifier,
        string jobActionIdentifier,
        bool consumes)
    {
        var server = new GameServer();
        PendingStartChanges.MarkChanged(
            server,
            EGameServerChangeArea.ModPack | EGameServerChangeArea.Configuration | EGameServerChangeArea.Credentials,
            FirstChange);

        var changed = PendingStartChanges.ConsumeAtCommandCreation(
            server,
            kind,
            actionIdentifier,
            jobActionIdentifier);

        Assert.Equal(consumes, changed);
        if (consumes)
        {
            Assert.Equal(EGameServerChangeArea.ModPack, server.PendingStartChangeAreas);
            Assert.Equal(FirstChange, server.PendingStartSince);
        }
        else
        {
            Assert.Equal(
                EGameServerChangeArea.ModPack | EGameServerChangeArea.Configuration | EGameServerChangeArea.Credentials,
                server.PendingStartChangeAreas);
        }
    }

    /// <summary>
    /// Expanding flags yields the set areas in declaration order and an empty list for None.
    /// </summary>
    [Fact]
    public void Areas_ReturnsSetFlagsInDeclarationOrder()
    {
        Assert.Equal(
            [EGameServerChangeArea.ModPack, EGameServerChangeArea.Credentials],
            PendingStartChanges.Areas(EGameServerChangeArea.Credentials | EGameServerChangeArea.ModPack));
        Assert.Empty(PendingStartChanges.Areas(EGameServerChangeArea.None));
    }

    /// <summary>
    /// Starting a server consumes the mod pack area only and keeps the other pending areas and the since-timestamp.
    /// </summary>
    /// <returns>A task completing after the persisted columns were verified.</returns>
    [Fact]
    public async Task Start_ConsumesOnlyTheModPackArea()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.PendingStartChangeAreas = EGameServerChangeArea.ModPack | EGameServerChangeArea.Configuration;
        seed.Server.PendingStartSince = FirstChange;
        await store.DbContext.SaveChangesAsync();

        await StartAsync(store, seed);

        var persisted = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.Configuration, persisted.PendingStartChangeAreas);
        Assert.Equal(FirstChange, persisted.PendingStartSince);
    }

    /// <summary>
    /// Starting a server with only the mod pack area pending clears the areas and the since-timestamp.
    /// </summary>
    /// <returns>A task completing after the persisted columns were verified.</returns>
    [Fact]
    public async Task Start_WithOnlyModPackPending_ClearsEverything()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.PendingStartChangeAreas = EGameServerChangeArea.ModPack;
        seed.Server.PendingStartSince = FirstChange;
        await store.DbContext.SaveChangesAsync();

        await StartAsync(store, seed);

        var persisted = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.None, persisted.PendingStartChangeAreas);
        Assert.Null(persisted.PendingStartSince);
    }

    /// <summary>
    /// A rejected start leaves the pending areas untouched.
    /// </summary>
    /// <returns>A task completing after the rejection and the persisted columns were verified.</returns>
    [Fact]
    public async Task RejectedStart_LeavesPendingAreasUnchanged()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.PendingStartChangeAreas = EGameServerChangeArea.ModPack | EGameServerChangeArea.Credentials;
        seed.Server.PendingStartSince = FirstChange;
        seed.Server.IsLaunchDisabled = true;
        await store.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<HostingConflictException>(() => StartAsync(store, seed));

        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.ModPack | EGameServerChangeArea.Credentials, persisted.PendingStartChangeAreas);
        Assert.Equal(FirstChange, persisted.PendingStartSince);
    }

    /// <summary>
    /// Enqueues the start action for the seeded server and reloads the tracked state from the database.
    /// </summary>
    /// <param name="store">The test store providing the database context.</param>
    /// <param name="seed">The seed holding the user and the server.</param>
    /// <returns>A task completing after the start was enqueued and the change tracker was cleared.</returns>
    private static async Task StartAsync(HostingTestStore store, HostingTestSeed seed)
    {
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Operate);
        await HostingTestData.CreateLifecycleService(store.DbContext).EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Start,
            confirmation: null,
            principal);
        store.DbContext.ChangeTracker.Clear();
    }

    /// <summary>
    /// Seeds a server of the Arma 3 game so that configuration keys validate.
    /// </summary>
    /// <param name="store">The test store to seed.</param>
    /// <returns>The seed whose server uses the Arma 3 game identifier.</returns>
    private static async Task<HostingTestSeed> SeedArmaServerAsync(HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier;
        await store.DbContext.SaveChangesAsync();
        return seed;
    }

    /// <summary>
    /// Creates a principal that may view and modify the seeded server.
    /// </summary>
    /// <param name="seed">The seed holding the user and the server.</param>
    /// <returns>The principal carrying the server-scoped permissions.</returns>
    private static ClaimsPrincipal ModifierPrincipal(HostingTestSeed seed)
        => HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View,
            Claims.ResourceBased.Server.Modify);

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

    /// <summary>
    /// Builds an update payload that sets the maximum player count.
    /// </summary>
    /// <param name="maxPlayers">The value to set.</param>
    /// <returns>The update payload.</returns>
    private static ServerConfigurationUpdatePayload MaxPlayersUpdate(int maxPlayers)
        => new()
        {
            Values =
            [
                new ConfigurationValuePayload
                {
                    Key = "server.cfg@maxPlayers",
                    Value = JsonSerializer.SerializeToElement(maxPlayers),
                },
            ],
        };

    /// <summary>
    /// Persists an Arma 3 configuration profile with one revision.
    /// </summary>
    /// <param name="store">The test store to write to.</param>
    /// <param name="seed">The seed providing the author.</param>
    /// <returns>The persisted profile.</returns>
    private static async Task<ConfigurationProfile> AddProfileAsync(HostingTestStore store, HostingTestSeed seed)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var profile = new ConfigurationProfile
        {
            PrimaryKey = Guid.NewGuid(),
            Title = "Operations",
            GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier,
            CreatedByFk = seed.User.PrimaryKey,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            Revisions =
            [
                new ConfigurationProfileRevision
                {
                    PrimaryKey = Guid.NewGuid(),
                    RevisionNumber = 1,
                    CreatedByFk = seed.User.PrimaryKey,
                    TimeStampCreated = now,
                    Values = [],
                },
            ],
        };
        store.DbContext.Add(profile);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        return profile;
    }
}
