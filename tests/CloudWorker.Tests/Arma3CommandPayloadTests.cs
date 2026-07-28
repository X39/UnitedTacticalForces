using System.Text.Json;
using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
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

    [Fact]
    public void WorkshopContent_ValidatesAndReturnsCanonicalNumericIdentifiers()
    {
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var payload = Arma3CommandPayload.Parse(command.Payload);
        payload = payload with
        {
            Command = payload.Command with
            {
                Content =
                [
                    new WorkerContentReference
                    {
                        Source = Arma3CommandPayload.SteamWorkshopContentSource,
                        Identifier = "450814997",
                        DisplayName = "CBA A3",
                    },
                    new WorkerContentReference
                    {
                        Source = Arma3CommandPayload.SteamWorkshopContentSource,
                        Identifier = "463939057",
                        DisplayName = "ACE",
                    },
                ],
            },
        };

        payload.ValidateAttribution(bootstrap, command);

        Assert.Equal([450814997UL, 463939057UL], payload.GetSteamWorkshopItemIds());
    }

    [Theory]
    [InlineData("other-source", "450814997", "CBA A3")]
    [InlineData("steam-workshop", "../450814997", "CBA A3")]
    [InlineData("steam-workshop", "450814997 -beta dev", "CBA A3")]
    [InlineData("steam-workshop", "0450814997", "CBA A3")]
    [InlineData("steam-workshop", "450814997", "../CBA A3")]
    [InlineData("steam-workshop", "450814997", "CBA\\A3")]
    public void WorkshopContent_RejectsUnsupportedOrUnsafeReferences(
        string source,
        string identifier,
        string displayName)
    {
        var bootstrap = CloudWorkerBootstrapConfigTests.CreateBootstrap();
        var command = FlatFileWorkerCommandJournalTests.CreateCommand(
            bootstrap.WorkerId,
            bootstrap.DeploymentId,
            bootstrap.ServerId);
        var payload = Arma3CommandPayload.Parse(command.Payload);
        payload = payload with
        {
            Command = payload.Command with
            {
                Content =
                [
                    new WorkerContentReference
                    {
                        Source = source,
                        Identifier = identifier,
                        DisplayName = displayName,
                    },
                ],
            },
        };

        Assert.Throws<InvalidDataException>(() => payload.ValidateAttribution(bootstrap, command));
    }
}
