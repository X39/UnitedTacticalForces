namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public static class GameServerActionIds
{
    public const string Start       = "start";
    public const string Stop        = "stop";
    public const string Restart     = "restart";
    public const string Install     = "install";
    public const string Update      = "update";
    public const string CollectLogs = "collect-logs";

    /// <summary>
    /// Internal bounded evidence/preservation action used only before fault-triggered provider teardown.
    /// It is never projected as a user action.
    /// </summary>
    public const string FaultStop = "fault-stop";
}