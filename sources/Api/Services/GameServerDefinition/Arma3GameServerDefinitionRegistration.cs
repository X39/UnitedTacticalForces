using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

[Singleton<Arma3GameServerDefinitionRegistration, IGameServerDefinitionRegistration>]
public sealed class Arma3GameServerDefinitionRegistration : IGameServerDefinitionRegistration
{
    public const string GameIdentifier = "arma3";

    public GameServerDefinition Definition { get; } = new(
        GameIdentifier,
        "Arma 3",
        [
            new(
                GameServerActionIds.Start,
                "Start",
                "Provision a dedicated cloud worker, prepare Arma 3, and start the server.",
                "play",
                "Lifecycle",
                10,
                false,
                false,
                TimeSpan.FromMinutes(30),
                ECostImpact.StartsCost,
                "Allocates a dedicated cloud runtime and starts generating provider cost."),
            new(
                GameServerActionIds.Stop,
                "Stop",
                "Save, stop, preserve selected data, and release every cloud resource.",
                "stop",
                "Lifecycle",
                20,
                false,
                true,
                TimeSpan.FromMinutes(5),
                ECostImpact.StopsCost,
                "Stops the worker and releases the deployment after preservation succeeds."),
            new(
                GameServerActionIds.Restart,
                "Restart game process",
                "Gracefully restart Arma 3 inside the existing dedicated worker.",
                "arrow-clockwise",
                "Maintenance",
                30,
                false,
                true,
                TimeSpan.FromMinutes(3),
                ECostImpact.KeepsCostRunning,
                "The cloud deployment remains allocated."),
            new(
                GameServerActionIds.Install,
                "Install game files",
                "Install missing Arma 3 dedicated-server files on the cloud worker.",
                "cloud-download",
                "Maintenance",
                40,
                false,
                true,
                TimeSpan.FromMinutes(30),
                ECostImpact.KeepsCostRunning,
                "The cloud deployment remains allocated while installation runs."),
            new(
                GameServerActionIds.Update,
                "Update game files",
                "Update and validate Arma 3 files on the cloud worker.",
                "arrow-sync",
                "Maintenance",
                50,
                false,
                true,
                TimeSpan.FromMinutes(30),
                ECostImpact.KeepsCostRunning,
                "The cloud deployment remains allocated while the update runs."),
            new(
                GameServerActionIds.CollectLogs,
                "Inspect logs",
                "Collect redacted diagnostics from the active cloud worker.",
                "document-text",
                "Diagnostics",
                60,
                false,
                false,
                TimeSpan.FromSeconds(30),
                ECostImpact.NoCostImpact,
                "Does not change cloud allocation."),
        ],
        [
            new("server/server.cfg", EFileClassification.Configuration, false, true, false, true),
            new("server/basic.cfg", EFileClassification.Configuration, false, true, false, false),
            new("server/profiles", EFileClassification.Configuration, true, true, false, true),
            new("server/battleye", EFileClassification.Configuration, true, true, false, true),
            new("mpmissions", EFileClassification.Mission, true, true, false, false),
            new("logs", EFileClassification.Log, true, false, false, false),
        ],
        [
            new("server/profiles", EPreservationScope.RecursiveFolder, EPreservationMode.CreateOrUpdate),
            new("server/battleye/bans.txt", EPreservationScope.File, EPreservationMode.CreateOrUpdate),
            new("server/battleye/scripts.txt", EPreservationScope.File, EPreservationMode.CreateOrUpdate),
        ])
    {
        InboundNetworkRequirements =
        [
            new(
                ERuntimeNetworkProtocol.Udp,
                2302,
                2306,
                "Arma 3 game traffic"),
        ],
        PrimaryConnection = new(
            ERuntimeNetworkProtocol.Udp,
            2302,
            "Arma 3 game server"),
    };
}
