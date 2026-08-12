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

        var arma = Assert.Single(catalog.Games);
        Assert.Equal(Arma3ConfigurationImplementation.Identifier, arma.GameIdentifier);
        Assert.NotEmpty(arma.Definitions);
        Assert.Contains(arma.Definitions, q => q.Required && q.DefaultValue.HasValue);
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
