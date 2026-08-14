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
export type GameServer = Omit<Required<Schema<"GameServerDto">>, "actions" | "deployment"> & {
  actions: ServerAction[];
  deployment: ServerDeployment | null;
};
export type ServerCapability = Omit<
  Required<Schema<"GameServerCapabilityDto">>,
  "runtimeProviderProfiles" | "actions"
> & {
  runtimeProviderProfiles: RuntimeProvider[];
  actions: ServerAction[];
};
export type ServerNotification = Omit<
  Required<Schema<"GameServerNotificationDto">>,
  "costStopAction"
> & {
  costStopAction: ServerAction | null;
};
export type ServerCreation = Schema<"GameServerCreationPayload">;
export type ServerCreationCostOwnerOption = Required<Schema<"GameServerCostOwnerOptionDto">>;
export type StorageDemand = Omit<
  Required<Schema<"StorageDemandDto">>,
  "runtimeProfiles" | "components"
> & {
  runtimeProfiles: Required<Schema<"RuntimeProfileStorageFitDto">>[];
  components: Required<Schema<"StorageDemandComponentDto">>[];
};
export type LifecycleJob = Omit<Required<Schema<"LifecycleJobDto">>, "commands"> & {
  commands: Required<Schema<"WorkerCommandDto">>[];
};
export type HostingOperation = Required<Schema<"HostingOperationDto">>;
export type ServerModPackOption = Required<Schema<"GameServerModPackOptionDto">>;
export type HostingFile = Required<Schema<"HostingFileDto">>;
export type PreservationSelection = Required<Schema<"PreservationSelectionDto">>;

export type HostingFileContext = "persistent" | "live";

/**
 * The configuration controller predates the generated OpenAPI response annotations. Keep the
 * project-owned shape next to the generated contract aliases until the API snapshot exposes it.
 */
export interface GameConfigurationEntry {
  key: string;
  targetPath: string;
  group: string;
  displayName: string;
  description: string;
  kind: string;
  required: boolean;
  sensitive: boolean;
  order: number;
  minimum: number | null;
  maximum: number | null;
  allowedValues: string[];
  value: unknown;
  source: string;
  isCompatible: boolean;
  isRedacted: boolean;
  warning: string | null;
}

export interface GameConfiguration {
  serverId: string;
  profileId: string | null;
  profileRevisionId: string | null;
  profileRevisionNumber: number | null;
  isComplete: boolean;
  entries: GameConfigurationEntry[];
}

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
  order: number;
  defaultValue: unknown;
  minimum: number | null;
  maximum: number | null;
  allowedValues: string[];
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
  values: Record<string, unknown>;
  redactedKeys: string[];
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
