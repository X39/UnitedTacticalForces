using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>Verifies the explicit startup opt-in for automatic hosting operations.</summary>
public sealed class HostingControlPlaneStateTests
{
    /// <summary>Missing configuration must preserve the safe paused startup state.</summary>
    [Fact]
    public void MissingSetting_DefaultsToPaused()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.True(new HostingControlPlaneState(configuration).IsPaused);
    }

    /// <summary>Valid explicit settings determine whether hosting background services are registered.</summary>
    /// <param name="setting">The operator's pause setting.</param>
    /// <param name="paused">Whether automatic operations must remain paused.</param>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ExplicitSetting_ControlsBackgroundServiceRegistration(string setting, bool paused)
    {
        var configuration = CreateConfiguration(setting);
        var services = new ServiceCollection();

        services.AddHostingControlPlane(configuration);

        using var provider = services.BuildServiceProvider();
        var state = provider.GetRequiredService<HostingControlPlaneState>();
        Assert.Equal(paused, state.IsPaused);
        Assert.Same(state, provider.GetRequiredService<HostingControlPlaneState>());
        var hostedServices = services.Where(q => q.ServiceType == typeof(IHostedService)).ToArray();
        if (paused)
        {
            Assert.Empty(hostedServices);
            return;
        }

        Assert.Equal(
            new[]
            {
                "DeploymentHealthReconciliationWorker",
                "GameServerControlPlaneWorker",
                "HostingCostReconciliationWorker",
                "ProviderResourceReconciliationWorker",
            },
            hostedServices.Select(q => q.ImplementationType!.Name).OrderBy(q => q).ToArray());
    }

    /// <summary>Malformed settings fail startup rather than silently enabling background work.</summary>
    /// <param name="setting">An invalid operator pause setting.</param>
    [Theory]
    [InlineData("")]
    [InlineData("yes")]
    [InlineData("0")]
    [InlineData("disabled")]
    public void MalformedSetting_FailsRegistration(string setting)
    {
        var configuration = CreateConfiguration(setting);

        Assert.Throws<InvalidOperationException>(() => new HostingControlPlaneState(configuration));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddHostingControlPlane(configuration));
    }

    /// <summary>Builds an isolated public configuration with no environment or secret sources.</summary>
    /// <param name="setting">The operator pause setting.</param>
    /// <returns>The configuration supplied to registration and state construction.</returns>
    private static IConfiguration CreateConfiguration(string setting)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [HostingControlPlaneState.ConfigurationKey] = setting,
        }).Build();
}
