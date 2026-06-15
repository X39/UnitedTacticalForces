using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using Xunit;

namespace Api.Tests.Authentication;

public class AuthenticationProviderAvailabilityTests
{
    [Theory]
    [InlineData(false, null, null, false)]
    [InlineData(true, null, null, false)]
    [InlineData(true, "client-id", null, false)]
    [InlineData(true, null, "client-secret", false)]
    [InlineData(true, "client-id", "client-secret", true)]
    public void DiscordRegistration_RequiresEnabledFlagAndCompleteOAuthConfiguration(
        bool enabled,
        string? clientId,
        string? clientSecret,
        bool expected)
    {
        var values = new Dictionary<string, string?>
        {
            ["Discord:Enabled"] = enabled.ToString(),
            ["Discord:OAuth:ClientId"] = clientId,
            ["Discord:OAuth:ClientSecret"] = clientSecret,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.Equal(expected, Program.IsDiscordAuthenticationEnabled(configuration));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GetAuthenticationProvidersAsync_ReportsRegisteredSchemes(bool steam, bool discord)
    {
        var controller = CreateController(CreateSchemeProvider(steam, discord));

        var result = await controller.GetAuthenticationProvidersAsync();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsType<AuthenticationProvidersDto>(ok.Value);
        Assert.Equal(steam, providers.Steam);
        Assert.Equal(discord, providers.Discord);
    }

    [Fact]
    public async Task LoginSteamAsync_ReturnsNotFoundWhenSteamIsUnavailable()
    {
        var controller = CreateController(CreateSchemeProvider(steam: false, discord: true));

        var result = await controller.LoginSteamAsync(null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task LoginDiscordAsync_ReturnsNotFoundWhenDiscordIsUnavailable()
    {
        var controller = CreateController(CreateSchemeProvider(steam: true, discord: false));

        var result = await controller.LoginDiscordAsync(null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task LoginSteamAsync_ChallengesWhenSteamIsAvailable()
    {
        var controller = CreateController(CreateSchemeProvider(steam: true, discord: false));

        var result = await controller.LoginSteamAsync(null);

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(Constants.AuthorizationSchemas.Steam, challenge.AuthenticationSchemes);
    }

    [Fact]
    public async Task LoginDiscordAsync_ChallengesWhenDiscordIsAvailable()
    {
        var controller = CreateController(CreateSchemeProvider(steam: false, discord: true));

        var result = await controller.LoginDiscordAsync(null);

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(Constants.AuthorizationSchemas.Discord, challenge.AuthenticationSchemes);
    }

    private static UsersController CreateController(IAuthenticationSchemeProvider schemeProvider)
    {
        var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var dbContext = new NodaTimeSqliteApiDbContext(options);
        return new UsersController(
            NullLogger<UsersController>.Instance,
            dbContext,
            authenticationSchemeProvider: schemeProvider
        );
    }

    private static IAuthenticationSchemeProvider CreateSchemeProvider(bool steam, bool discord)
    {
        var provider = new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions()));
        if (steam)
        {
            provider.AddScheme(new AuthenticationScheme(
                Constants.AuthorizationSchemas.Steam,
                Constants.AuthorizationSchemas.Steam,
                typeof(CookieAuthenticationHandler)
            ));
        }
        if (discord)
        {
            provider.AddScheme(new AuthenticationScheme(
                Constants.AuthorizationSchemas.Discord,
                Constants.AuthorizationSchemas.Discord,
                typeof(CookieAuthenticationHandler)
            ));
        }
        return provider;
    }
}
