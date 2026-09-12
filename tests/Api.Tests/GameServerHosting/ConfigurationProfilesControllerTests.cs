using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.GameServerImplementations;
using Xunit;
using DatabaseUser = X39.UnitedTacticalForces.Database.Authority.User;

namespace Api.Tests.GameServerHosting;

public sealed class ConfigurationProfilesControllerTests
{
    [Fact]
    public void Definitions_AreAvailableBeforeAProfileExists()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity()));

        var result = Assert.IsType<OkObjectResult>(controller.GetDefinitions());
        var catalog = Assert.IsType<ConfigurationProfileDefinitionCatalogDto>(result.Value);

        Assert.Equal(3, catalog.Games.Count);
        var aska = Assert.Single(catalog.Games, q => q.GameIdentifier == AskaConfigurationImplementation.Identifier);
        Assert.NotEmpty(aska.Definitions);
        var arma = Assert.Single(catalog.Games, q => q.GameIdentifier == Arma3ConfigurationImplementation.Identifier);
        Assert.Equal(Arma3ConfigurationImplementation.Identifier, arma.GameIdentifier);
        Assert.NotEmpty(arma.Definitions);
        Assert.Contains(arma.Definitions, q => q.Required && q.DefaultValue.HasValue);
        var cs2 = Assert.Single(catalog.Games, q => q.GameIdentifier == CounterStrike2ConfigurationImplementation.Identifier);
        Assert.NotEmpty(cs2.Definitions);
        Assert.False(catalog.CanCreate);
    }

    [Fact]
    public void Definitions_ReportCreatePermission()
    {
        var identity = new ClaimsIdentity(
            [new Claim(X39.UnitedTacticalForces.Claims.Creation.ConfigurationProfile, string.Empty)],
            "Test");
        var controller = CreateController(new ClaimsPrincipal(identity));

        var result = Assert.IsType<OkObjectResult>(controller.GetDefinitions());
        var catalog = Assert.IsType<ConfigurationProfileDefinitionCatalogDto>(result.Value);

        Assert.True(catalog.CanCreate);
    }

    /// <summary>Verifies that publishing a revision persists its complete merged snapshot.</summary>
    [Fact]
    public async Task CreateRevisionAsync_PersistsMergedSnapshot()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var actorId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var actor = new DatabaseUser
        {
            PrimaryKey = actorId,
            Nickname = "configuration-profile-author",
        };
        var profile = new ConfigurationProfile
        {
            PrimaryKey = profileId,
            Title = "Operations",
            GameIdentifier = Arma3ConfigurationImplementation.Identifier,
            CreatedBy = actor,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            Revisions =
            [
                new ConfigurationProfileRevision
                {
                    PrimaryKey = Guid.NewGuid(),
                    RevisionNumber = 1,
                    CreatedBy = actor,
                    TimeStampCreated = now,
                    Values =
                    [
                        new ConfigurationProfileValue
                        {
                            PrimaryKey = Guid.NewGuid(),
                            DefinitionKey = "server.cfg@hostname",
                            ValueJson = JsonSerializer.Serialize("UTF"),
                        },
                    ],
                },
            ],
        };
        store.DbContext.Add(profile);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(System.Security.Claims.ClaimTypes.Name, actorId.ToString()),
                new Claim(X39.UnitedTacticalForces.Claims.Administrative.All, string.Empty),
            ],
            X39.UnitedTacticalForces.Api.Constants.AuthorizationSchemas.Api,
            System.Security.Claims.ClaimTypes.Name,
            System.Security.Claims.ClaimTypes.Role));
        var controller = new ConfigurationProfilesController(
            store.DbContext,
            new GameServerConfigurationService(store.DbContext),
            new HostingAuditWriter())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

        var result = await controller.CreateRevisionAsync(
            profileId,
            new ConfigurationProfileRevisionCreationPayload
            {
                BaseRevisionNumber = 1,
                Values =
                [
                    new ConfigurationValuePayload
                    {
                        Key = "server.cfg@maxPlayers",
                        Value = JsonSerializer.SerializeToElement(80),
                    },
                ],
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var revision = await store.DbContext.ConfigurationProfileRevisions
            .Include(q => q.Values)
            .SingleAsync(q => q.ConfigurationProfileFk == profileId && q.RevisionNumber == 2);
        Assert.Equal(2, revision.Values.Count);
    }

    /// <summary>Verifies that the declared Arma V1 surface includes every operational target and typed policies.</summary>
    [Fact]
    public void Definitions_ExposeCompleteArmaV1InventoryAndTypedPolicies()
    {
        var implementation = GameConfigurationImplementations.Get(Arma3ConfigurationImplementation.Identifier);
        var keys = implementation.Definitions.Select(q => q.Key).ToHashSet(StringComparer.Ordinal);

        var expectedKeys = new[]
        {
                    "server.cfg@hostname",
                    "server.cfg@verifySignatures",
                    "server.cfg@allowedFilePatching",
                    "server.cfg@allowedLoadFileExtensions[]",
                    "server.cfg@forcedDifficulty",
                    "basic.cfg@MaxBandwidth",
                    "basic.cfg@MinErrorToSendNear",
                    "utfserver.arma3profile@thirdPersonView",
                    "utfserver.arma3profile@aiLevelPreset",
                    "utfserver.arma3profile@skillAI",
                    "beserver.cfg@RConPassword",
                    "beserver.cfg@MaxPing",
        };
        Assert.All(expectedKeys, key => Assert.Contains(key, keys));
        Assert.Equal(
            42,
            implementation.Definitions.Count(q => q.TargetPath == Arma3ConfigurationImplementation.ServerTarget));
        Assert.Equal(
            8,
            implementation.Definitions.Count(q => q.TargetPath == Arma3ConfigurationImplementation.BasicTarget));
        Assert.Equal(
            26,
            implementation.Definitions.Count(q => q.TargetPath == Arma3ConfigurationImplementation.ProfileTarget));
        Assert.Equal(
            5,
            implementation.Definitions.Count(q => q.TargetPath == Arma3ConfigurationImplementation.BattlEyeTarget));
        Assert.Equal(4, implementation.Definitions.Select(q => q.TargetPath).Distinct().Count());

        var signatures = Assert.Single(implementation.Definitions, q => q.Setting == "verifySignatures");
        Assert.Equal(EGameConfigurationValueKind.Integer, signatures.Kind);
        Assert.Equal([0, 1, 2], signatures.Choices!.Select(q => q.Value.GetInt32()));
        Assert.Equal("Require v2 only", signatures.Choices![2].Label);
    }

    private static ConfigurationProfilesController CreateController(ClaimsPrincipal principal)
    {
        var controller = new ConfigurationProfilesController(null!, null!, null!);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
        return controller;
    }
}
