using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Registers the ASKA dedicated-server capabilities exposed by the control plane.</summary>
[Singleton<AskaGameServerDefinitionRegistration, IGameServerDefinitionRegistration>]
public sealed class AskaGameServerDefinitionRegistration : IGameServerDefinitionRegistration
{
    /// <summary>Gets the stable ASKA game identifier.</summary>
    public const string GameIdentifier = "aska";

    /// <inheritdoc />
    public GameServerDefinition Definition { get; } = new(
        GameIdentifier,
        "ASKA",
        [
            Action(GameServerActionIds.Start, "Start", "Provision, prepare, and start ASKA.", "play", 10, ECostImpact.StartsCost, TimeSpan.FromMinutes(45)),
            Action(GameServerActionIds.Stop, "Stop", "Save the world, stop ASKA, preserve data, and release cloud resources.", "stop", 20, ECostImpact.StopsCost, TimeSpan.FromMinutes(5), true),
            Action(GameServerActionIds.Restart, "Restart game process", "Gracefully restart ASKA in the active worker.", "arrow-clockwise", 30, ECostImpact.KeepsCostRunning, TimeSpan.FromMinutes(5), true),
            Action(GameServerActionIds.Install, "Install game files", "Install missing ASKA dedicated-server files.", "cloud-download", 40, ECostImpact.KeepsCostRunning, TimeSpan.FromMinutes(45), true),
            Action(GameServerActionIds.Update, "Update game files", "Update and validate ASKA dedicated-server files.", "arrow-sync", 50, ECostImpact.KeepsCostRunning, TimeSpan.FromMinutes(45), true),
            Action(GameServerActionIds.CollectLogs, "Inspect logs", "Collect redacted ASKA diagnostics from the active worker.", "document-text", 60, ECostImpact.NoCostImpact, TimeSpan.FromSeconds(30)),
        ],
        [
            new("wine-prefix/drive_c/users/utfgame/AppData/LocalLow/Sand Sailor Studio/Aska/data/server", EFileClassification.SaveFile, true, true, false, true),
            new("aska-state/save-id.txt", EFileClassification.SaveFile, false, false, false, true),
            new("logs", EFileClassification.Log, true, false, false, false),
        ],
        [
            new("wine-prefix/drive_c/users/utfgame/AppData/LocalLow/Sand Sailor Studio/Aska/data/server", EPreservationScope.RecursiveFolder, EPreservationMode.CreateOrUpdate),
            new("aska-state/save-id.txt", EPreservationScope.File, EPreservationMode.CreateOrUpdate),
        ])
    {
        InboundNetworkRequirements =
        [
            new(ERuntimeNetworkProtocol.Udp, 27015, 27015, "ASKA gameplay traffic"),
            new(ERuntimeNetworkProtocol.Udp, 27016, 27016, "ASKA server-browser query traffic"),
        ],
        PrimaryConnection = new(ERuntimeNetworkProtocol.Udp, 27015, "ASKA server"),
        RuntimeInstallation = new("3246670", 8L * 1024 * 1024 * 1024, "ASKA dedicated server runtime", "1898300"),
        WorkerImageIdentifier = GameIdentifier,
        Secrets =
        [
            new("authentication-token", "Steam authentication token", true, false, false, true),
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
                ECostImpact.StopsCost => "Releases the deployment after world preservation succeeds.",
                ECostImpact.KeepsCostRunning => "The cloud deployment remains allocated.",
                _ => "Does not change cloud allocation.",
            });
}
