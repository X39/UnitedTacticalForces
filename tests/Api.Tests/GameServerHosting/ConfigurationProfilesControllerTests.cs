using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.GameServerImplementations;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class ConfigurationProfilesControllerTests
{
    [Fact]
    public void Definitions_AreAvailableBeforeAProfileExists()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity()));

        var result = Assert.IsType<OkObjectResult>(controller.GetDefinitions());
        var catalog = Assert.IsType<ConfigurationProfileDefinitionCatalogDto>(result.Value);

        Assert.Equal(2, catalog.Games.Count);
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
