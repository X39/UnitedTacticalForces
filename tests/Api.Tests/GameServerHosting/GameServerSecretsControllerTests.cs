using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.GameServerSecrets;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Verifies the HTTP status mapping of <see cref="GameServerSecretsController"/> and that no response carries a
/// submitted secret value.
/// </summary>
public sealed class GameServerSecretsControllerTests
{
    private const string SubmittedValue = "submitted-secret-value-7f3a";

    /// <summary>Verifies that a successful set returns 204 and marks the credentials area as pending.</summary>
    [Fact]
    public async Task SetAsync_OnActiveServer_ReturnsNoContentAndMarksCredentials()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, controller) = await PrepareAsync(store, archived: false);

        var result = await controller.SetAsync(seed.Server.PrimaryKey, Payload(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var server = await store.DbContext.GameServers.AsNoTracking().SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(EGameServerChangeArea.Credentials, server.PendingStartChangeAreas);
    }

    /// <summary>Verifies that setting a secret on an archived server maps to 409 without leaking the value.</summary>
    [Fact]
    public async Task SetAsync_OnArchivedServer_ReturnsConflictWithoutTheValue()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, controller) = await PrepareAsync(store, archived: true);

        var result = await controller.SetAsync(seed.Server.PrimaryKey, Payload(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.DoesNotContain(SubmittedValue, JsonSerializer.Serialize(problem.Value), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that rotating on an active server returns 200 with the generated value and that an archived server
    /// maps to 409 without any value in the problem body.
    /// </summary>
    [Fact]
    public async Task RotateAsync_MapsSuccessAndArchivedConflict()
    {
        await using var activeStore = await HostingTestStore.CreateAsync();
        var (activeSeed, activeController) = await PrepareAsync(activeStore, archived: false);
        await using var archivedStore = await HostingTestStore.CreateAsync();
        var (archivedSeed, archivedController) = await PrepareAsync(archivedStore, archived: true);

        var success = await activeController.RotateAsync(activeSeed.Server.PrimaryKey, "rcon-password", CancellationToken.None);
        var conflict = await archivedController.RotateAsync(archivedSeed.Server.PrimaryKey, "rcon-password", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(success);
        var value = Assert.IsType<GameServerSecretValueDto>(ok.Value);
        Assert.Equal("rcon-password", value.Key);
        var problem = Assert.IsType<ObjectResult>(conflict);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        var archivedValue = await archivedStore.DbContext.GameServerSecrets.AsNoTracking()
            .Where(q => q.GameServerFk == archivedSeed.Server.PrimaryKey && q.Key == "rcon-password")
            .ToArrayAsync();
        Assert.Single(archivedValue);
        Assert.DoesNotContain(value.Value, JsonSerializer.Serialize(problem.Value), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a caller holding only the manage permission receives the rotated value once with
    /// <c>Cache-Control: no-store</c>, and that the value equals what an authorized reveal returns afterwards.
    /// </summary>
    [Fact]
    public async Task RotateAsync_WithManageOnly_ReturnsTheValueWithNoStore()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, controller, service) = await PrepareWithServiceAsync(
            store,
            archived: false,
            X39.UnitedTacticalForces.Claims.ResourceBased.Server.ManageSecrets);

        var result = await controller.RotateAsync(seed.Server.PrimaryKey, "rcon-password", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<GameServerSecretValueDto>(ok.Value);
        Assert.Equal(64, value.Value.Length);
        Assert.Equal(2, value.Version);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        var revealingController = CreateController(
            store,
            seed,
            service,
            X39.UnitedTacticalForces.Claims.ResourceBased.Server.RevealSecrets);
        var revealed = await revealingController.RevealAsync(seed.Server.PrimaryKey, "rcon-password", CancellationToken.None);
        var revealedValue = Assert.IsType<GameServerSecretValueDto>(Assert.IsType<OkObjectResult>(revealed).Value);
        Assert.Equal(value.Value, revealedValue.Value);
    }

    /// <summary>Verifies that a valid chosen value for a generated credential returns 204.</summary>
    [Fact]
    public async Task SetAsync_ChosenValueForGeneratedSecret_ReturnsNoContent()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, controller) = await PrepareAsync(store, archived: false);

        var result = await controller.SetAsync(
            seed.Server.PrimaryKey,
            new GameServerSecretValuePayload
            {
                Key = "rcon-password",
                Value = "Chosen-Value_42",
                Origin = EGameServerSecretOrigin.UserProvided,
            },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    /// <summary>Verifies that an unusable token value maps to a validation problem that does not echo the value.</summary>
    [Fact]
    public async Task SetAsync_InvalidTokenValue_ReturnsValidationProblemWithoutTheValue()
    {
        const string invalid = "bad value \"quoted\"";
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, controller) = await PrepareAsync(store, archived: false);

        var result = await controller.SetAsync(
            seed.Server.PrimaryKey,
            new GameServerSecretValuePayload
            {
                Key = "rcon-password",
                Value = invalid,
                Origin = EGameServerSecretOrigin.UserProvided,
            },
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.IsAssignableFrom<ValidationProblemDetails>(problem.Value);
        Assert.DoesNotContain("bad value", JsonSerializer.Serialize(problem.Value), StringComparison.Ordinal);
    }

    /// <summary>Verifies that reveal still returns the value of an archived server and is never cached.</summary>
    [Fact]
    public async Task RevealAsync_OnArchivedServer_StillReturnsTheValue()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, controller) = await PrepareAsync(store, archived: true);

        var result = await controller.RevealAsync(seed.Server.PrimaryKey, "rcon-password", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    private static GameServerSecretValuePayload Payload()
        => new()
        {
            Key = "gslt",
            Value = SubmittedValue,
            Origin = EGameServerSecretOrigin.ExternalProvider,
        };

    /// <summary>
    /// Seeds a Counter-Strike 2 server with generated secrets and builds a controller whose principal holds the given
    /// claims, or both the manage and the reveal claim when none are given.
    /// </summary>
    /// <param name="store">The test store.</param>
    /// <param name="archived">Whether the seeded server is archived.</param>
    /// <param name="claims">The resource-based server claims the principal holds.</param>
    /// <returns>The seed and the controller.</returns>
    private static async Task<(HostingTestSeed Seed, GameServerSecretsController Controller)> PrepareAsync(
        HostingTestStore store,
        bool archived,
        params string[] claims)
    {
        var (seed, controller, _) = await PrepareWithServiceAsync(store, archived, claims);
        return (seed, controller);
    }

    /// <summary>
    /// Seeds a Counter-Strike 2 server like <see cref="PrepareAsync"/> and also returns the service so that further
    /// controllers can share its data-protection key ring.
    /// </summary>
    /// <param name="store">The test store.</param>
    /// <param name="archived">Whether the seeded server is archived.</param>
    /// <param name="claims">The resource-based server claims the principal holds.</param>
    /// <returns>The seed, the controller and the service.</returns>
    private static async Task<(HostingTestSeed Seed, GameServerSecretsController Controller, GameServerSecretService Service)>
        PrepareWithServiceAsync(HostingTestStore store, bool archived, params string[] claims)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        store.DbContext.ChangeTracker.Clear();
        var server = await store.DbContext.GameServers.Include(q => q.Secrets).SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        server.GameIdentifier = CounterStrike2GameServerDefinitionRegistration.GameIdentifier;
        var definition = new CounterStrike2GameServerDefinitionRegistration().Definition;
        var service = CreateService(store, definition);
        service.EnsureGeneratedSecrets(server, definition, seed.User.PrimaryKey, NodaSystemClock.Instance.GetCurrentInstant());
        server.IsArchived = archived;
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        if (claims.Length is 0)
            claims =
            [
                X39.UnitedTacticalForces.Claims.ResourceBased.Server.ManageSecrets,
                X39.UnitedTacticalForces.Claims.ResourceBased.Server.RevealSecrets,
            ];
        return (seed, CreateController(store, seed, service, claims), service);
    }

    private static GameServerSecretService CreateService(HostingTestStore store, GameServerDefinition definition)
        => new(
            store.DbContext,
            new GameServerDefinitionCatalog([new Registration(definition)]),
            new EphemeralDataProtectionProvider(),
            new HostingAuditWriter());

    /// <summary>
    /// Builds a controller over an already seeded Counter-Strike 2 server whose principal holds the given claims.
    /// </summary>
    /// <param name="store">The test store.</param>
    /// <param name="seed">The seed of the server.</param>
    /// <param name="service">The secret service the controller uses.</param>
    /// <param name="claims">The resource-based server claims the principal holds.</param>
    /// <returns>The controller.</returns>
    private static GameServerSecretsController CreateController(
        HostingTestStore store,
        HostingTestSeed seed,
        GameServerSecretService service,
        params string[] claims)
    {
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, claims);
        return new GameServerSecretsController(
            service,
            new HostingAuthorizationService(store.DbContext))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    private sealed record Registration(GameServerDefinition Definition) : IGameServerDefinitionRegistration;
}
