namespace X39.UnitedTacticalForces.Hosting.Contracts;

/// <summary>
/// Classifies security- and lifecycle-relevant hosting audit events.
/// </summary>
public enum EHostingAuditEventKind
{
    ServerCreated = 0,
    ServerUpdated = 1,
    ServerArchived = 2,
    ServerLaunchDisabled = 3,
    ServerLaunchEnabled = 4,
    CostOwnerAssigned = 5,
    LifecycleRequested = 6,
    LifecycleStarted = 7,
    LifecycleSucceeded = 8,
    LifecycleFailed = 9,
    LifecycleRetried = 10,
    LifecycleSkipped = 11,
    LifecycleCancelledByCostStop = 12,
    TerminateRequested = 13,
    WorkerRegistered = 14,
    WorkerDegraded = 15,
    WorkerRecovered = 16,
    WorkerStopped = 17,
    WorkerCommandUpdated = 18,
    PreservationSelectionAdded = 19,
    PreservationSelectionUpdated = 20,
    PreservationSelectionRemoved = 21,
    FileCreated = 22,
    FileUpdated = 23,
    FileDeleted = 24,
    DeploymentResourceObserved = 25,
    DeploymentResourceReleased = 26,
    ManualCleanupRecorded = 27,
    CostOwnerChanged = 28,
    DeploymentCreated = 29,
    LifecycleJobQueued = 30,
    LifecycleJobClaimed = 31,
    LifecycleJobSucceeded = 32,
    LifecycleJobRetryPending = 33,
    LifecycleJobFailed = 34,
    LifecycleJobSkipped = 35,
    CommandsCancelledByCostStop = 36,
    WorkerProvisioned = 37,
    WorkerHeartbeat = 38,
    WorkerCommandQueued = 39,
    WorkerCommandSucceeded = 40,
    WorkerCommandFailed = 41,
    DeploymentStopped = 42,
    DeploymentTerminated = 43,
    AccessGranted = 44,
    AccessRevoked = 45,
    FileChanged = 46,
    PreservationPublished = 47,
    PreservationFailed = 48,
    PersistentDataDeleted = 49,
    Recovery = 50,
    ProviderReconciliationDegraded = 51,
    DeploymentResourceAdopted = 52,
    DanglingResourceDetected = 53,
    DanglingResourceCleanupFailed = 54,
    DanglingResourceCleanupSucceeded = 55,
    WorkerCredentialRotationIssued = 56,
    ProviderCostSliceCharged = 57,
    ProviderCostSliceReserved = 58,
    ProviderCostSliceReservationReleased = 59,
    BankTransferPaymentRecorded = 60,
    CostOwnerAllowanceChanged = 61,
    ExternalRefundRecorded = 62,
    DeploymentCostOwnerCorrected = 63,
    HostingLedgerEntryReversed = 64,
    DeploymentResourceProvisioningRequested = 65,
    DeploymentResourceProvisioningFailed = 66,
    DeploymentResourceReleaseRequested = 67,
    DeploymentResourceReleaseFailed = 68,
    ServerConfigurationChanged = 69,
    ServerConfigurationProfileChanged = 70,
    ConfigurationProfileCreated = 71,
    ConfigurationProfileRevisionCreated = 72,
    ConfigurationProfileArchived = 73,
    HostingFinancePolicyChanged = 74,
    ServerSecretCreated = 75,
    ServerSecretRotated = 76,
    ServerSecretRevealed = 77,
    ServerSecretUpdated = 78,
    CostOwnerCreated = 79,
    CostOwnerRenamed = 80,
    CostOwnerFrozen = 81,
    CostOwnerUnfrozen = 82,

    /// <summary>
    /// The control plane opened an automatic stop warning because an idle, maximum runtime or schedule limit
    /// was reached.
    /// </summary>
    AutomaticStopWarned = 83,

    /// <summary>
    /// The control plane queued an ordinary stop job because an automatic limit or schedule came due.
    /// </summary>
    AutomaticStopRequested = 84,

    /// <summary>
    /// An open automatic stop warning was cancelled before the stop was requested.
    /// </summary>
    AutomaticStopCancelled = 85,

    /// <summary>
    /// The control plane queued a start job because a schedule occurrence came due.
    /// </summary>
    ScheduledStartRequested = 86,

    /// <summary>
    /// A schedule occurrence was recorded without an action; the reason is in the audit summary.
    /// </summary>
    ScheduledRunSkipped = 87,

    /// <summary>
    /// A user changed the automation settings or schedule rules of a server.
    /// </summary>
    AutomationSettingsChanged = 88,

    /// <summary>
    /// A user confirmed that scheduled starts of a server may incur cost for the current cost owner.
    /// </summary>
    ScheduledStartsConfirmed = 89,
}
