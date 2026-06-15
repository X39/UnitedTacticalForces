using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

public class Arma3CommandPayloadTests
{
    [Fact]
    public void BaselinePayload_ValidatesCurrentGuidAttribution()
    {
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var payload = Arma3CommandPayload.Parse(command.Payload);

        payload.ValidateAttribution(bootstrap, command);

        Assert.Equal(bootstrap.ServerId, payload.Command.ServerId);
        Assert.Equal(2, payload.Command.GracefulStopAttempts);
    }

    [Fact]
    public void BaselinePayload_RejectsEnvelopeActionMismatch()
    {
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var payload = Arma3CommandPayload.Parse(command.Payload);
        payload = payload with { Command = payload.Command with { ActionIdentifier = "stop" } };

        Assert.Throws<InvalidDataException>(() => payload.ValidateAttribution(bootstrap, command));
    }
}
