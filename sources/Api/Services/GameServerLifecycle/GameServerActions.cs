using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Stable identities for platform well-known and initial Arma worker actions.
/// </summary>
public static class GameServerActions
{
    public const string Start = "start";
    public const string Stop = "stop";
    public const string Terminate = "terminate";
    public const string Restart = "restart";
    public const string Install = "install";
    public const string Update = "update";
    public const string CollectLogs = "collect-logs";

    public static bool TryGetKind(string actionId, out ELifecycleJobKind kind)
    {
        kind = actionId.Trim().ToLowerInvariant() switch
        {
            Start => ELifecycleJobKind.Start,
            Stop => ELifecycleJobKind.Stop,
            Terminate => ELifecycleJobKind.Terminate,
            Restart => ELifecycleJobKind.Restart,
            Install => ELifecycleJobKind.Install,
            Update => ELifecycleJobKind.Update,
            CollectLogs => ELifecycleJobKind.CollectLogs,
            _ => ELifecycleJobKind.Custom,
        };
        return kind is not ELifecycleJobKind.Custom;
    }
}
