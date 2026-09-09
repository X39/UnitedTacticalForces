using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

[Singleton<CounterStrike2GameServerDefinitionRegistration, IGameServerDefinitionRegistration>]
public sealed class CounterStrike2GameServerDefinitionRegistration : IGameServerDefinitionRegistration
{
    public const string GameIdentifier = "cs2";

    public GameServerDefinition Definition { get; } = new(
        GameIdentifier,
        "Counter-Strike 2",
        [
            Action(GameServerActionIds.Start, "Start", "Provision, prepare, and start Counter-Strike 2.", "play", 10, ECostImpact.StartsCost, TimeSpan.FromMinutes(45)),
            Action(GameServerActionIds.Stop, "Stop", "Gracefully stop and release all cloud resources.", "stop", 20, ECostImpact.StopsCost, TimeSpan.FromMinutes(5), true),
            Action(GameServerActionIds.Restart, "Restart game process", "Restart Counter-Strike 2 in the active worker.", "arrow-clockwise", 30, ECostImpact.KeepsCostRunning, TimeSpan.FromMinutes(3), true),
            Action(GameServerActionIds.Install, "Install game files", "Install missing Counter-Strike 2 server files.", "cloud-download", 40, ECostImpact.KeepsCostRunning, TimeSpan.FromMinutes(45), true),
            Action(GameServerActionIds.Update, "Update game files", "Update and validate Counter-Strike 2 server files.", "arrow-sync", 50, ECostImpact.KeepsCostRunning, TimeSpan.FromMinutes(45), true),
            Action(GameServerActionIds.CollectLogs, "Inspect logs", "Collect redacted diagnostics from the active worker.", "document-text", 60, ECostImpact.NoCostImpact, TimeSpan.FromSeconds(30)),
        ],
        [
            new("game/csgo/cfg/utf-server.cfg", EFileClassification.Configuration, false, true, false, false),
            new("game/csgo/cfg/banned_user.cfg", EFileClassification.Configuration, false, true, true, true),
            new("game/csgo/cfg/banned_ip.cfg", EFileClassification.Configuration, false, true, true, true),
            new("game/csgo/addons", EFileClassification.Mod, true, true, false, false),
            new("content-artifacts", EFileClassification.Mod, true, true, false, false),
            new("game/csgo/logs", EFileClassification.Log, true, false, false, false),
            new("logs", EFileClassification.Log, true, false, false, false),
        ],
        [
            new("game/csgo/cfg/banned_user.cfg", EPreservationScope.File, EPreservationMode.CreateOrUpdate),
            new("game/csgo/cfg/banned_ip.cfg", EPreservationScope.File, EPreservationMode.CreateOrUpdate),
            new("game/csgo/addons/counterstrikesharp/configs", EPreservationScope.RecursiveFolder, EPreservationMode.CreateOrUpdate),
            new("game/csgo/addons/counterstrikesharp/data", EPreservationScope.RecursiveFolder, EPreservationMode.CreateOrUpdate),
        ])
    {
        InboundNetworkRequirements =
        [
            new(ERuntimeNetworkProtocol.Udp, 27015, 27015, "Counter-Strike 2 game and query traffic"),
            new(ERuntimeNetworkProtocol.Tcp, 27015, 27015, "Counter-Strike 2 RCON traffic"),
        ],
        PrimaryConnection = new(ERuntimeNetworkProtocol.Udp, 27015, "Counter-Strike 2 server"),
        RuntimeInstallation = new(
            "730",
            85L * 1024 * 1024 * 1024,
            "Counter-Strike 2 dedicated server runtime",
            "730"),
        ContentCapabilities = new(
            true,
            "Map",
            true,
            "Game mode",
            true,
            false,
            ["BuiltIn", "SteamWorkshop", "UploadedArtifact"],
            ["archive-overlay", "cs2-metamod", "cs2-counterstrikesharp", "cs2-plugin"],
            "de_dust2",
            "competitive")
        {
            ModProviderBindings =
            [
                new(ModSourceProviderIdentifiers.UploadedArtifact, "Uploaded archive", "ZIP archive",
                    ["archive-overlay", "cs2-metamod", "cs2-counterstrikesharp", "cs2-plugin"], false),
                new(ModSourceProviderIdentifiers.Git, "Git repository", "Public HTTPS Git URL",
                    ["archive-overlay", "cs2-metamod", "cs2-counterstrikesharp", "cs2-plugin"], true),
            ],
        },
        Secrets =
        [
            new("rcon-password", "RCON password", true, true, true),
            new("gslt", "Steam Game Server Login Token", true, false, false, true),
            new("join-password", "Join password", false, false, true),
        ],
    };

    private static GameServerActionDefinition Action(
        string id,
        string name,
        string description,
        string icon,
        int order,
        ECostImpact costImpact,
        TimeSpan duration,
        bool confirmation = false)
        => new(
            id,
            name,
            description,
            icon,
            id is GameServerActionIds.Start or GameServerActionIds.Stop ? "Lifecycle" : "Maintenance",
            order,
            false,
            confirmation,
            duration,
            costImpact,
            costImpact switch
            {
                ECostImpact.StartsCost => "Allocates a dedicated cloud runtime and starts provider cost.",
                ECostImpact.StopsCost => "Stops the worker and releases the deployment after preservation.",
                ECostImpact.KeepsCostRunning => "The cloud deployment remains allocated.",
                _ => "Does not change cloud allocation.",
            });
}
