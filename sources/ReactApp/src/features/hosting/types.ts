import type { components } from "@/generated/api";

type Schema<Name extends keyof components["schemas"]> = components["schemas"][Name];

export type RuntimeProfile = Required<Schema<"RuntimeProfileDto">>;
export type RuntimeStorageCapabilities = Required<Schema<"RuntimeStorageCapabilitiesDto">>;
export type RuntimeProvider = Omit<
  Required<Schema<"RuntimeProviderDto">>,
  "storage" | "profiles"
> & {
  profiles: RuntimeProfile[];
  storage: RuntimeStorageCapabilities | null;
};
export type ServerAction = Required<Schema<"GameServerActionDto">>;
export type ServerDeploymentResource = Required<Schema<"GameServerDeploymentResourceDto">>;
export type ServerDeployment = Omit<Required<Schema<"GameServerDeploymentDto">>, "resources"> & {
  resources: ServerDeploymentResource[];
};
/** One weekly rule of the automation settings with its calculated next run and last run. */
export type ServerSchedule = Omit<
  Required<Schema<"ServerScheduleDto">>,
  "lastRun" | "nextRunAt"
> & {
  /** The next run instant calculated by the control plane, or null. */
  nextRunAt: string | null;
  /** The latest recorded occurrence, or null when the rule has not run. */
  lastRun: Required<Schema<"ServerScheduleLastRunDto">> | null;
};
/** The saved automation settings of one server, including its weekly rules and the concurrency version. */
export type ServerAutomationSettings = Omit<
  Required<Schema<"ServerAutomationSettingsDto">>,
  "schedules"
> & {
  schedules: ServerSchedule[];
};
/** The limits the control plane enforces on the automation settings. */
export type ServerAutomationLimits = Required<Schema<"ServerAutomationLimitsDto">>;
/** An automatic stop that was announced and has not happened yet. */
export type ServerPendingStop = Required<Schema<"ServerPendingStopDto">>;
/** The live automation status: idle clock, maximum runtime stop, next scheduled action and a pending stop. */
export type ServerAutomationStatus = {
  /** The state of the idle shutdown clock. */
  idle: Required<Schema<"ServerIdleStatusDto">>;
  /** The instant the maximum runtime stops the session, or null. */
  maxRuntimeStopsAt: string | null;
  /** The next scheduled action, or null. */
  nextScheduled: Required<Schema<"ServerNextScheduledDto">> | null;
  /** The announced automatic stop, or null. */
  pendingStop: ServerPendingStop | null;
};
/** The automation block of one server: settings, limits, idle capability, live status and start consent. */
export type ServerAutomation = Omit<
  Required<Schema<"ServerAutomationDto">>,
  "settings" | "status"
> & {
  settings: ServerAutomationSettings;
  status: ServerAutomationStatus;
};
/** A full replacement of the automation settings and weekly rules of one server. */
export type ServerAutomationUpdate = Schema<"ServerAutomationUpdate">;
/**
 * One server as the API sends it.
 *
 * `automation` and `canManageAutomation` are optional because an older API does not send them: absent means the
 * automation panel is not available, and the data layer then sets both to null. `canManageAccess` is optional for
 * the same reason: absent means the viewer may not manage access, and the data layer then sets it to false.
 */
export type GameServer = Omit<
  Required<Schema<"GameServerDto">>,
  | "actions"
  | "deployment"
  | "automation"
  | "canManageAutomation"
  | "canManageAccess"
  | "costOwnerIsFrozen"
  | "costOwnerAssignedAt"
> & {
  actions: ServerAction[];
  deployment: ServerDeployment | null;
  /** The automation block of the detail read; null on the fleet list or when the API does not send it. */
  automation?: ServerAutomation | null;
  /** Whether the viewer may change the automation; null when unknown or not applicable. */
  canManageAutomation?: boolean | null;
  /** Whether the viewer may give and remove access to this server; absent from older payloads. */
  canManageAccess?: boolean | null;
  /** Whether the cost owner rejects new cost-generating assignments; null when the viewer may not see the owner. */
  costOwnerIsFrozen?: boolean | null;
  /**
   * When the current cost owner took over; null when the viewer may not see the owner. The detail read fills it,
   * the fleet list never does.
   */
  costOwnerAssignedAt?: string | null;
};
/** One member with direct permissions on a server, as the access list sends it. */
export type ServerAccessUserGrant = Required<Schema<"GameServerUserAccessGrantDto">>;
/** One role with direct permissions on a server, as the access list sends it. */
export type ServerAccessRoleGrant = Required<Schema<"GameServerRoleAccessGrantDto">>;
/** One member who can still be given access to a server, as the candidate search sends it. */
export type ServerAccessUserCandidate = Required<Schema<"GameServerUserAccessCandidateDto">>;
/** One role that can still be given access to a server, as the candidate search sends it. */
export type ServerAccessRoleCandidate = Required<Schema<"GameServerRoleAccessCandidateDto">>;
/**
 * The direct grants of one server: the members and roles that hold permissions, the permissions that can be
 * delegated at all and the subset the viewer may assign.
 */
export type ServerAccess = Omit<
  Required<Schema<"GameServerAccessDto">>,
  "users" | "roles" | "allowedPermissions" | "assignablePermissions"
> & {
  /** The members holding direct grants, in API order (by nickname). */
  users: ServerAccessUserGrant[];
  /** The roles holding direct grants, in API order (by title). */
  roles: ServerAccessRoleGrant[];
  /** Every permission that can be delegated through the access surface. */
  allowedPermissions: string[];
  /** The permissions the viewer may grant or revoke; every permission control must use this list. */
  assignablePermissions: string[];
};
export type ServerCapability = Omit<
  Required<Schema<"GameServerCapabilityDto">>,
  | "runtimeProviderProfiles"
  | "actions"
  | "requiresTerrain"
  | "terrainLabel"
  | "requiresScenario"
  | "scenarioLabel"
  | "supportsModPacks"
  | "requiresModPack"
  | "terrainSourceKinds"
  | "modInstallerIdentifiers"
  | "secrets"
  | "defaultTerrainSourceIdentifier"
  | "defaultScenarioSourceIdentifier"
> & {
  runtimeProviderProfiles: RuntimeProvider[];
  actions: ServerAction[];
  requiresTerrain?: boolean;
  terrainLabel?: string;
  requiresScenario?: boolean;
  scenarioLabel?: string;
  supportsModPacks?: boolean;
  requiresModPack?: boolean;
  terrainSourceKinds?: string[];
  modInstallerIdentifiers?: string[];
  secrets?: Required<Schema<"GameServerSecretCapabilityDto">>[];
  defaultTerrainSourceIdentifier?: string | null;
  defaultScenarioSourceIdentifier?: string | null;
};
export type ServerNotification = Omit<
  Required<Schema<"GameServerNotificationDto">>,
  "costStopAction"
> & {
  costStopAction: ServerAction | null;
};
export type ServerCreation = Schema<"GameServerCreationPayload">;
export type ServerCreationCostOwnerOption = Required<Schema<"GameServerCostOwnerOptionDto">>;
/** One cost owner a server can be transferred to, as the owner options of the server send it. */
export type ServerCostOwnerOption = Required<Schema<"GameServerCostOwnerOptionDto">>;
export type StorageDemand = Omit<
  Required<Schema<"StorageDemandDto">>,
  "runtimeProfiles" | "components" | "placementEstimate"
> & {
  runtimeProfiles: Required<Schema<"RuntimeProfileStorageFitDto">>[];
  components: Required<Schema<"StorageDemandComponentDto">>[];
  placementEstimate?: WorkloadPlacementEstimate | null;
};
export type WorkloadPlacementEstimate = Required<Schema<"WorkloadPlacementEstimateDto">>;
/**
 * One lifecycle job as the API sends it.
 *
 * `trigger` and `triggerLimit` are optional because an older API does not send them: an absent trigger counts as
 * `Manual`.
 */
export type LifecycleJob = Omit<
  Required<Schema<"LifecycleJobDto">>,
  "commands" | "trigger" | "triggerLimit"
> & {
  commands: Required<Schema<"WorkerCommandDto">>[];
  /** What caused the job: `Manual`, `Idle`, `MaxRuntime` or `Schedule`; absent means `Manual`. */
  trigger?: Schema<"ELifecycleJobTrigger"> | (string & Record<never, never>);
  /** Minutes for an idle trigger and hours for a maximum runtime trigger, otherwise null. */
  triggerLimit?: number | null;
};
export type HostingOperation = Required<Schema<"HostingOperationDto">>;
/** The accrued cost of one hosting session exactly as the control plane calculated it. */
export type AccruedCost = Required<Schema<"AccruedCostDto">>;
/** The accrued cost of a server: the latest session, or null when the server never started. */
export type GameServerAccruedCost = Omit<
  Required<Schema<"GameServerAccruedCostDto">>,
  "session"
> & {
  session: AccruedCost | null;
};
/** The current metric readings of one server exactly as the control plane decided them. */
export type GameServerMetrics = Required<Schema<"GameServerMetricsDto">>;
/** The average, lowest and highest value of one metric inside one history bucket. */
export type ServerMetricAggregate = Required<Schema<"ServerMetricAggregateDto">>;
/** One history bucket of a server; a metric without a sample in the bucket is null. */
export type ServerMetricPoint = {
  /** The inclusive start of the bucket as an ISO instant. */
  bucketStart: string;
  /** The CPU load aggregate in percent, or null when the bucket holds no sample. */
  cpu: ServerMetricAggregate | null;
  /** The used memory aggregate in bytes, or null when the bucket holds no sample. */
  memoryUsed: ServerMetricAggregate | null;
  /** The human player count aggregate, or null when the bucket holds no sample. */
  players: ServerMetricAggregate | null;
};
/** The retained metric history of one server for one range. */
export type GameServerMetricsHistory = Omit<
  Required<Schema<"GameServerMetricsHistoryDto">>,
  "points"
> & {
  points: ServerMetricPoint[];
};
/** The time ranges the history can be read for. */
export type ServerMetricsRange = "1h" | "24h" | "7d";
/**
 * The current readings together with the moment the browser received them, measured with `performance.now()` so
 * that a skewed browser clock cannot fake freshness.
 */
export type ServerMetricsReading = GameServerMetrics & { receivedAtMs: number };
/**
 * One undismissed in-app alert of the viewer.
 *
 * The kind and stop members are optional because an older API does not send them: an absent kind counts as
 * `CostThreshold`.
 */
export type CostAlert = Omit<
  Required<Schema<"CostAlertDto">>,
  "kind" | "stopsAt" | "stopReason" | "stopLimit"
> & {
  /** What the alert announces: `CostThreshold` or `AutoStop`; absent means `CostThreshold`. */
  kind?: Schema<"ECostAlertKind"> | (string & Record<never, never>);
  /** The instant the announced automatic stop follows, or null for a threshold alert. */
  stopsAt?: string | null;
  /** Why the stop was announced: `Idle`, `MaxRuntime` or `Schedule`, or null for a threshold alert. */
  stopReason?: Schema<"EAutoStopReason"> | (string & Record<never, never>) | null;
  /** Minutes for an idle stop and hours for a maximum runtime stop, or null. */
  stopLimit?: number | null;
};
/** The cost alert configuration of one cost owner with what the viewer may do and which channels can deliver. */
export type CostAlertSettings = Required<Schema<"CostAlertSettingsDto">>;
/** A full replacement of the cost alert configuration of one cost owner. */
export type CostAlertSettingsUpdate = Required<Schema<"CostAlertSettingsUpdate">>;
/** Whether one delivery channel can reach the current recipients. */
export type CostAlertChannelAvailability = Schema<"ECostAlertChannelAvailability">;
/** One area of a server whose saved changes wait for the next start. */
export type ServerChangeArea = Schema<"EGameServerChangeArea">;
export type ServerModPackOption = Omit<
  Required<Schema<"GameServerModPackOptionDto">>,
  "isAvailable" | "unavailableReason"
> & {
  isAvailable?: boolean;
  unavailableReason?: string | null;
};
/** One item of a flattened mod pack. */
export type ServerModPackContentItem = Required<Schema<"GameServerModPackContentItemDto">>;
/** The flattened contents of one mod pack as the next Start would resolve them. */
export type ServerModPackContents = Omit<
  Required<Schema<"GameServerModPackContentsDto">>,
  "items"
> & {
  items: ServerModPackContentItem[];
};
export type HostingFile = Required<Schema<"HostingFileDto">>;
export type PreservationSelection = Required<Schema<"PreservationSelectionDto">>;

export type HostingFileContext = "persistent" | "live";

export interface GameContentVersionOption {
  primaryKey: string;
  versionNumber: number;
  versionLabel: string;
  sourceKind: string;
  sourceIdentifier: string;
}

export interface GameContentOption {
  primaryKey: string;
  gameIdentifier: string;
  title: string;
  description?: string | null;
  versions: GameContentVersionOption[];
}

/**
 * The saved status of one credential as the API sends it, including whether a member may choose its value and which
 * characters the game can use in it. It never carries the value.
 */
export type GameServerSecretStatus = Required<Schema<"GameServerSecretStatusDto">>;

/** A credential value as the API returns it once: after a reveal or a rotation. */
export type GameServerSecretValue = Required<Schema<"GameServerSecretValueDto">>;

/** One configuration entry as the API sends it; the generated schema carries the Common or Advanced tier. */
export type GameConfigurationEntry = Omit<
  Required<Schema<"GameConfigurationEntryDto">>,
  "choices"
> & {
  choices: ConfigurationChoice[];
};

/** The effective configuration of one server as the API sends it. */
export type GameConfiguration = Omit<Required<Schema<"GameConfigurationDto">>, "entries"> & {
  entries: GameConfigurationEntry[];
};

export interface PreservationSelectionInput {
  path: string;
  scope: "File" | "FlatFolder" | "RecursiveFolder";
  mode: "CreateOrUpdate" | "MirrorDeletions";
}

export interface ConfigurationProfileSummary {
  primaryKey: string;
  title: string;
  gameIdentifier: string;
  isArchived: boolean;
  latestRevisionNumber: number;
  /** The newest revision, so a newly chosen profile can be pinned to it; null when the profile has none. */
  latestRevisionId?: string | null;
}

export interface ConfigurationDefinition {
  key: string;
  targetPath: string;
  group: string;
  displayName: string;
  description: string;
  kind: string;
  required: boolean;
  sensitive: boolean;
  isControlPlaneOnly?: boolean;
  order: number;
  defaultValue: unknown;
  minimum: number | null;
  maximum: number | null;
  allowedValues: string[];
  choices?: ConfigurationChoice[];
}

export interface ConfigurationChoice {
  value: unknown;
  label: string;
  description: string | null;
}

export interface ConfigurationDefinitionSet {
  gameIdentifier: string;
  definitions: ConfigurationDefinition[];
}

export interface ConfigurationProfileCatalog {
  canCreate: boolean;
  games: ConfigurationDefinitionSet[];
}

export interface ConfigurationProfileRevision {
  primaryKey: string;
  revisionNumber: number;
  timeStampCreated: string;
  createdById?: string;
  createdByNickname?: string;
  values: Record<string, unknown>;
  sensitiveConfiguredKeys?: string[];
  changes?: ConfigurationProfileRevisionChange[];
  redactedKeys?: string[];
}

export interface ConfigurationProfileRevisionChange {
  key: string;
  kind: "Added" | "Changed" | "Removed";
}

export interface ConfigurationProfileDetails {
  profile: ConfigurationProfileSummary;
  canModify: boolean;
  canManageAccess: boolean;
  canArchive: boolean;
  definitions: ConfigurationDefinition[];
  revisions: ConfigurationProfileRevision[];
}

export interface ConfigurationProfileUserAccessGrant {
  userId: string;
  nickname: string;
  permissions: string[];
}

export interface ConfigurationProfileRoleAccessGrant {
  roleId: number;
  title: string;
  description: string;
  permissions: string[];
}

export interface ConfigurationProfileAccess {
  allowedPermissions: string[];
  users: ConfigurationProfileUserAccessGrant[];
  roles: ConfigurationProfileRoleAccessGrant[];
}

export interface ConfigurationProfileUserCandidate {
  userId: string;
  nickname: string;
}

export interface ConfigurationProfileRoleCandidate {
  roleId: number;
  title: string;
  description: string;
}

export interface ConfigurationProfileAccessMatrix {
  users: Array<{ userId: string; permissions: string[] }>;
  roles: Array<{ roleId: number; permissions: string[] }>;
}

export type ConfigurationProfileOption = ConfigurationProfileSummary;

export interface ConfigurationValue {
  key: string;
  value: unknown;
}

export interface HostingWorkspaceSnapshot {
  server: GameServer | undefined;
  jobs: LifecycleJob[];
  operations: HostingOperation[];
}
