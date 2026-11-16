namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Durable boundaries used to resume a two-process operation without repeating completed launches.</summary>
internal enum EArma3SupervisionPhase
{
    /// <summary>No preparation or launch has completed for this command.</summary>
    Received,
    /// <summary>Both consumers are stopped and shared content is being prepared.</summary>
    Preparing,
    /// <summary>All shared content and final configuration have been prepared.</summary>
    Prepared,
    /// <summary>The primary launch is in progress and must be reconciled before repeating it.</summary>
    PrimaryStarting,
    /// <summary>The primary process was observed and the dependent launch may begin.</summary>
    PrimaryRunning,
    /// <summary>The dependent launch is in progress and must be reconciled before repeating it.</summary>
    AuxiliaryStarting,
    /// <summary>Both processes were observed; application-level join remains unverified.</summary>
    Running,
    /// <summary>Shutdown has begun and no process may be launched.</summary>
    Stopping,
    /// <summary>Both processes were verified stopped.</summary>
    Stopped,
    /// <summary>The operation failed and requires a new authorized command.</summary>
    Failed,
}
