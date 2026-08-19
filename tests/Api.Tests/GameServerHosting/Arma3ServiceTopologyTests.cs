using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class Arma3ServiceTopologyTests
{
    [Fact]
    public void Definition_DeclaresPrimaryServerAndDedicatedHeadlessClientNode()
    {
        var definition = new Arma3GameServerDefinitionRegistration().Definition;

        var primary = Assert.Single(definition.Nodes, q => q.IsPrimary);
        Assert.Equal(GameServerNodeDefinition.PrimaryServiceIdentifier, primary.ServiceIdentifier);
        Assert.NotEmpty(primary.InboundNetworkRequirements);

        var headlessClient = Assert.Single(
            definition.Nodes,
            q => q.ServiceIdentifier == Arma3GameServerDefinitionRegistration.HeadlessClientServiceIdentifier);
        Assert.False(headlessClient.IsPrimary);
        Assert.Equal(1, headlessClient.Count);
        Assert.Empty(headlessClient.InboundNetworkRequirements);
    }
}
