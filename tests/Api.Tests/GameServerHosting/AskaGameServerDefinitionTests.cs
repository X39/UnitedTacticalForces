using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class AskaGameServerDefinitionTests
{
    [Fact]
    public void Definition_DeclaresRuntimeNetworkSecretsAndSafePreservation()
    {
        var definition = new AskaGameServerDefinitionRegistration().Definition;

        Assert.Equal("aska", definition.GameIdentifier);
        Assert.Equal("3246670", definition.RuntimeInstallation?.ApplicationIdentifier);
        Assert.Equal("1898300", definition.RuntimeInstallation?.WorkshopApplicationIdentifier);
        Assert.Equal("aska", definition.WorkerImageIdentifier);
        Assert.Contains(definition.InboundNetworkRequirements,
            q => q.Protocol == ERuntimeNetworkProtocol.Udp && q.StartPort == 27015 && q.EndPort == 27015);
        Assert.Contains(definition.InboundNetworkRequirements,
            q => q.Protocol == ERuntimeNetworkProtocol.Udp && q.StartPort == 27016 && q.EndPort == 27016);
        var token = Assert.Single(definition.Secrets, q => q.Key == "authentication-token");
        Assert.True(token.RequiredForStart);
        Assert.True(token.ExternalProvider);
        Assert.False(token.Revealable);
        Assert.DoesNotContain(definition.FileCapabilities,
            q => q.RelativePath.Contains("properties", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(definition.PreservationSeeds,
            q => q.RelativePath.Contains("properties", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(definition.PreservationSeeds, q => q.RelativePath == "aska-state/save-id.txt");
    }
}
