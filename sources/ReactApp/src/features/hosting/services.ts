import { ApiError, getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import { pickerRequestTake } from "@/features/hosting/pickerLimits";
import type {
  ConfigurationProfileAccess,
  ConfigurationProfileAccessMatrix,
  ConfigurationProfileCatalog,
  ConfigurationProfileDetails,
  ConfigurationProfileOption,
  ConfigurationProfileRoleCandidate,
  ConfigurationProfileSummary,
  ConfigurationProfileUserCandidate,
  ConfigurationValue,
  CostAlert,
  CostAlertSettings,
  CostAlertSettingsUpdate,
  GameConfiguration,
  GameContentOption,
  GameServer,
  GameServerAccruedCost,
  GameServerMetrics,
  GameServerMetricsHistory,
  GameServerSecretStatus,
  GameServerSecretValue,
  HostingFile,
  HostingFileContext,
  HostingOperation,
  LifecycleJob,
  PreservationSelection,
  PreservationSelectionInput,
  ServerAccess,
  ServerAccessRoleCandidate,
  ServerAccessUserCandidate,
  ServerCapability,
  ServerCostOwnerOption,
  ServerCreation,
  ServerAutomation,
  ServerAutomationUpdate,
  ServerCreationCostOwnerOption,
  ServerMetricsRange,
  ServerNotification,
  ServerModPackContents,
  ServerModPackOption,
  StorageDemand,
} from "@/features/hosting/types";

/**
 * Raised when saving cost alert settings is rejected because they changed elsewhere since they were loaded (HTTP
 * 409). The page keeps the draft, says so and offers to reload; it never shows the server text.
 */
export class CostAlertSettingsConflictError extends Error {
  /** Creates the error for a stale settings revision. */
  constructor() {
    super("The cost alert settings were changed elsewhere.");
    this.name = "CostAlertSettingsConflictError";
  }
}

/**
 * Raised when saving automation settings is rejected because they changed elsewhere since they were loaded (HTTP
 * 409). The panel keeps the draft, says so and offers to reload; it never shows the server text.
 */
export class AutomationSettingsConflictError extends Error {
  /** Creates the error for a stale settings version. */
  constructor() {
    super("The automation settings were changed elsewhere.");
    this.name = "AutomationSettingsConflictError";
  }
}

/**
 * Raised when saving automation settings is rejected because the viewer may no longer manage them (HTTP 403). The
 * panel switches to its read-only view; it never shows the server text.
 */
export class AutomationSettingsForbiddenError extends Error {
  /** Creates the error for a lost right. */
  constructor() {
    super("The viewer may not change the automation settings.");
    this.name = "AutomationSettingsForbiddenError";
  }
}

/**
 * Raised when the API rejects the automation settings as invalid (HTTP 400). The panel marks the listed fields; it
 * never shows the server text.
 */
export class AutomationSettingsValidationError extends Error {
  /**
   * Creates the error for a rejected draft.
   *
   * @param fields The field paths the API names as invalid; empty when it names none.
   */
  constructor(readonly fields: string[]) {
    super("The automation settings are invalid.");
    this.name = "AutomationSettingsValidationError";
  }
}

function required<T>(value: T | undefined, operation: string): T {
  if (value === undefined) throw new Error(`${operation} returned no response body.`);
  return value;
}

function normalizeServer(server: GameServer): GameServer {
  const deploymentStatuses = [
    "Provisioning",
    "WaitingForWorker",
    "Preparing",
    "Running",
    "Stopping",
    "PreservationFailed",
    "Terminating",
    "Stopped",
    "TerminatingDueToFault",
    "TerminatedDueToFault",
    "Failed",
    "Terminated",
  ] as const;
  const rawStatus = server.deployment?.status as unknown;
  const numericStatus =
    typeof rawStatus === "number"
      ? rawStatus
      : typeof rawStatus === "string" && /^\d+$/.test(rawStatus)
        ? Number(rawStatus)
        : undefined;
  const normalizedStatus =
    numericStatus === undefined ? rawStatus : (deploymentStatuses[numericStatus] ?? rawStatus);
  return {
    ...server,
    actions: server.actions ?? [],
    changesPendingStartAreas: server.changesPendingStartAreas ?? [],
    hasChangesPendingStart: server.hasChangesPendingStart ?? false,
    costAlertThresholds: server.costAlertThresholds ?? null,
    canManageCostAlerts: server.canManageCostAlerts ?? null,
    automation: server.automation ?? null,
    canManageAutomation: server.canManageAutomation ?? null,
    canManageAccess: server.canManageAccess ?? false,
    costOwnerIsFrozen: server.costOwnerIsFrozen ?? null,
    costOwnerAssignedAt: server.costOwnerAssignedAt ?? null,
    deployment: server.deployment
      ? {
          ...server.deployment,
          status: normalizedStatus as NonNullable<GameServer["deployment"]>["status"],
          resources: server.deployment.resources ?? [],
        }
      : null,
  };
}

function normalizeCapability(capability: ServerCapability): ServerCapability {
  return {
    ...capability,
    actions: capability.actions ?? [],
    runtimeProviders: capability.runtimeProviders ?? [],
    requiresTerrain: capability.requiresTerrain ?? false,
    terrainLabel: capability.terrainLabel ?? "Terrain",
    requiresScenario: capability.requiresScenario ?? false,
    scenarioLabel: capability.scenarioLabel ?? "Scenario",
    supportsModPacks: capability.supportsModPacks ?? true,
    requiresModPack: capability.requiresModPack ?? true,
    terrainSourceKinds: capability.terrainSourceKinds ?? [],
    modInstallerIdentifiers: capability.modInstallerIdentifiers ?? [],
    secrets: capability.secrets ?? [],
    runtimeProviderProfiles: (capability.runtimeProviderProfiles ?? []).map((provider) => ({
      ...provider,
      profiles: provider.profiles ?? [],
      storage: provider.storage ?? null,
    })),
  };
}

/**
 * Fills the availability fields an older payload may lack, so every pack counts as selectable by default.
 *
 * @param option The pack option as received.
 * @returns The option with `isAvailable` and `unavailableReason` set.
 */
function normalizeModPackOption(option: ServerModPackOption): ServerModPackOption {
  return {
    ...option,
    isAvailable: option.isAvailable ?? true,
    unavailableReason: option.unavailableReason ?? null,
  };
}

/**
 * Defaults the lists a degraded configuration payload may lack.
 *
 * @param configuration The configuration as received.
 * @returns The configuration with entries and every entry's choices set.
 */
function normalizeConfiguration(configuration: GameConfiguration): GameConfiguration {
  return {
    ...configuration,
    entries: (configuration.entries ?? []).map((entry) => ({
      ...entry,
      choices: entry.choices ?? [],
    })),
  };
}

/**
 * Defaults the command list of a degraded job payload. `trigger` and `triggerLimit` are kept exactly as received:
 * an older API omits them and an absent trigger stays absent, which consumers read as `Manual`.
 *
 * @param job The job as received.
 * @returns The job with `commands` set.
 */
function normalizeJob(job: LifecycleJob): LifecycleJob {
  return { ...job, commands: job.commands ?? [] };
}

function withQuery(path: string, values: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== "") query.set(key, String(value));
  });
  const suffix = query.toString();
  return suffix ? `${path}?${suffix}` : path;
}

/**
 * Builds the prefix of every owner options query of one server. The search key is built from it, so a prefix
 * invalidation can never drift away from the keys it has to reach.
 *
 * @param serverId The server the owners are listed for.
 * @returns The key every owner options search of the server starts with.
 */
function costOwnerOptionsPrefixKey(serverId: string) {
  return ["hosting", "servers", serverId, "cost-owner", "options"] as const;
}

export const hostingQueryKeys = {
  fleet: ["hosting", "servers"] as const,
  notifications: ["hosting", "notifications"] as const,
  capabilities: ["hosting", "capabilities"] as const,
  server: (serverId: string) => ["hosting", "servers", serverId] as const,
  jobs: (serverId: string) => ["hosting", "servers", serverId, "jobs"] as const,
  operations: (serverId: string) => ["hosting", "servers", serverId, "operations"] as const,
  /** Query key of the viewer's undismissed cost alerts. */
  costAlerts: ["hosting", "cost-alerts"] as const,
  /**
   * Query key of the cost alert settings of one cost owner.
   *
   * @param ownerKey A cost owner id, or "mine" for the viewer's personal cost owner.
   */
  costAlertSettings: (ownerKey: string) => ["hosting", "cost-alert-settings", ownerKey] as const,
  /** Query key of the accrued session cost of one server; it holds the REST value and later pushed values. */
  accruedCost: (serverId: string) => ["hosting", "servers", serverId, "accrued-cost"] as const,
  /**
   * Query key of one owner options search of one server. It is a sub-key of {@link hostingQueryKeys.server}, so the
   * exact invalidation of a fleet frame never touches it, and the options prefix of the server reaches every search.
   *
   * @param serverId The server the owners are listed for.
   * @param search The trimmed search text.
   */
  costOwnerOptions: (serverId: string, search: string) =>
    [...costOwnerOptionsPrefixKey(serverId), search] as const,
  /**
   * The prefix of every owner options search of one server, for a prefix invalidation that reaches every search text.
   *
   * @param serverId The server the owners are listed for.
   */
  costOwnerOptionsPrefix: costOwnerOptionsPrefixKey,
  /** Query key of the current metric readings of one server; it holds the REST value and later pushed values. */
  metrics: (serverId: string) => ["hosting", "servers", serverId, "metrics"] as const,
  /** Query key of the metric history of one server for one range. */
  metricsHistory: (serverId: string, range: ServerMetricsRange) =>
    ["hosting", "servers", serverId, "metrics", "history", range] as const,
  configuration: (serverId: string) => ["hosting", "servers", serverId, "configuration"] as const,
  configurationOptions: (serverId: string) =>
    ["hosting", "servers", serverId, "configuration", "options"] as const,
  files: (serverId: string, context: HostingFileContext, path: string) =>
    ["hosting", "servers", serverId, "files", context, path] as const,
  preservation: (serverId: string) => ["hosting", "servers", serverId, "preservation"] as const,
  storageDemand: (serverId: string) => ["hosting", "servers", serverId, "storage-demand"] as const,
  modPackOptions: (serverId: string) => ["hosting", "servers", serverId, "mod-packs"] as const,
  /** Query key of the flattened contents of one mod pack for one server. */
  modPackContents: (serverId: string, modPackDefinitionId: number) =>
    ["hosting", "servers", serverId, "mod-packs", modPackDefinitionId, "contents"] as const,
  creationModPacks: (gameIdentifier: string) =>
    ["hosting", "creation", gameIdentifier, "mod-packs"] as const,
  terrains: (gameIdentifier: string) => ["hosting", "content", gameIdentifier, "terrains"] as const,
  scenarios: (gameIdentifier: string) =>
    ["hosting", "content", gameIdentifier, "scenarios"] as const,
  secrets: (serverId: string) => ["hosting", "servers", serverId, "secrets"] as const,
  /**
   * Query key of the direct access grants of one server. It is a sub-key of {@link hostingQueryKeys.server}, so the
   * exact invalidation of a fleet frame never touches it.
   *
   * @param serverId The server whose grants are read.
   */
  access: (serverId: string) => ["hosting", "servers", serverId, "access"] as const,
  /**
   * Query key of one search for members or roles that can still be given access to a server. It is a sub-key of
   * {@link hostingQueryKeys.access}, so a refetch of the access list does not discard a half-typed search.
   *
   * @param serverId The server the candidates are searched for.
   * @param kind Whether members or roles are searched.
   * @param search The trimmed search text.
   */
  accessCandidates: (serverId: string, kind: "user" | "role", search: string) =>
    ["hosting", "servers", serverId, "access", "candidates", kind, search] as const,
  profileOptions: (gameIdentifier: string) =>
    ["hosting", "profiles", "options", gameIdentifier] as const,
  storageEstimate: (gameIdentifier: string, provider: string, modPackId: number | null) =>
    ["hosting", "storage", "estimate", gameIdentifier, provider, modPackId] as const,
  profiles: ["hosting", "configuration-profiles"] as const,
  profileCatalog: ["hosting", "configuration-profiles", "catalog"] as const,
  profile: (profileId: string) => ["hosting", "configuration-profiles", profileId] as const,
  profileAccess: (profileId: string) =>
    ["hosting", "configuration-profiles", profileId, "access"] as const,
};

export const hostingService = {
  async servers(signal?: AbortSignal): Promise<GameServer[]> {
    const result = await getApiClient().request<GameServer[]>(apiPath("/game-servers"), { signal });
    return (result ?? []).map(normalizeServer);
  },

  async notifications(signal?: AbortSignal): Promise<ServerNotification[]> {
    return (
      (await getApiClient().request<ServerNotification[]>(apiPath("/game-servers/notifications"), {
        signal,
      })) ?? []
    );
  },

  async capabilities(signal?: AbortSignal): Promise<ServerCapability[]> {
    const result = await getApiClient().request<ServerCapability[]>(
      apiPath("/game-servers/capabilities"),
      { signal },
    );
    return (result ?? []).map(normalizeCapability);
  },

  async server(serverId: string, signal?: AbortSignal): Promise<GameServer | undefined> {
    const result = await getApiClient().request<GameServer>(
      fillApiPath("/game-servers/{gameServerId}", { gameServerId: serverId }),
      { signal, expectedStatuses: [404] },
    );
    return result ? normalizeServer(result) : undefined;
  },

  /**
   * Loads the accrued cost of the server's latest session as the control plane calculated it.
   *
   * @param serverId The server whose accrued cost is loaded.
   * @param signal Optional abort signal of the request.
   * @returns The accrued cost; `session` is null when the server never started.
   */
  async accruedCost(serverId: string, signal?: AbortSignal): Promise<GameServerAccruedCost> {
    const result = await getApiClient().request<GameServerAccruedCost>(
      fillApiPath("/game-servers/{gameServerId}/accrued-cost", { gameServerId: serverId }),
      { signal },
    );
    const value = required(result, "Accrued cost");
    return { ...value, session: value.session ?? null };
  },

  /**
   * Loads the current metric readings of one server as the control plane decided them.
   *
   * @param serverId The server whose readings are loaded.
   * @param signal Optional abort signal of the request.
   * @returns The readings; every reading is either a value or an unavailable reason code.
   */
  async serverMetrics(serverId: string, signal?: AbortSignal): Promise<GameServerMetrics> {
    const result = await getApiClient().request<GameServerMetrics>(
      fillApiPath("/game-servers/{gameServerId}/metrics", { gameServerId: serverId }),
      { signal },
    );
    return required(result, "Server metrics");
  },

  /**
   * Loads the retained metric history of one server for one range.
   *
   * @param serverId The server whose history is loaded.
   * @param range The range code: `1h`, `24h` or `7d`.
   * @param signal Optional abort signal of the request.
   * @returns The history; `points` is empty when nothing was reported in the range.
   */
  async serverMetricsHistory(
    serverId: string,
    range: ServerMetricsRange,
    signal?: AbortSignal,
  ): Promise<GameServerMetricsHistory> {
    const result = await getApiClient().request<GameServerMetricsHistory>(
      withQuery(
        fillApiPath("/game-servers/{gameServerId}/metrics/history", { gameServerId: serverId }),
        { range },
      ),
      { signal },
    );
    const value = required(result, "Server metrics history");
    return { ...value, points: value.points ?? [] };
  },

  /**
   * Loads the viewer's undismissed cost alerts, newest first.
   *
   * @param signal Optional abort signal of the request.
   * @returns The alerts; empty when there are none.
   */
  async costAlerts(signal?: AbortSignal): Promise<CostAlert[]> {
    return (await getApiClient().request<CostAlert[]>(apiPath("/cost-alerts"), { signal })) ?? [];
  },

  /**
   * Dismisses one cost alert for the viewer; dismissing an already dismissed alert succeeds.
   *
   * @param costAlertId The id of the alert.
   */
  async dismissCostAlert(costAlertId: string): Promise<void> {
    const path = "/cost-alerts/{costAlertId}/dismissal" as const;
    await getApiClient().request<void>(fillApiPath(path, { costAlertId }), {
      method: apiMethod(path, "post"),
    });
  },

  /**
   * Dismisses the listed cost alerts of one server for the viewer in one request.
   *
   * The API dismisses only the given ids, so an alert raised after the viewer's list was loaded stays undismissed.
   * It answers 400 for an empty list or for more than 50 ids.
   *
   * @param serverId The id of the server whose alerts are dismissed.
   * @param costAlertIds The ids of the alerts the viewer sees for that server (1 to 50).
   */
  async dismissServerCostAlerts(serverId: string, costAlertIds: readonly string[]): Promise<void> {
    const path = "/cost-alerts/dismissals" as const;
    await getApiClient().request<void>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: { gameServerId: serverId, costAlertIds: [...costAlertIds] },
    });
  },

  /**
   * Loads the cost alert settings of one cost owner.
   *
   * A viewer who may not see the owner (403) or an unknown owner (404) makes this reject with an `ApiError`
   * whose `status` and `code` the page maps to its forbidden and not-found panels.
   *
   * @param costOwnerId The id of the cost owner.
   * @param signal Optional abort signal of the request.
   * @returns The settings with what the viewer may do.
   */
  async costAlertSettings(costOwnerId: string, signal?: AbortSignal): Promise<CostAlertSettings> {
    const result = await getApiClient().request<CostAlertSettings>(
      fillApiPath("/cost-owners/{costOwnerId}/alert-settings", { costOwnerId }),
      { signal },
    );
    return required(result, "Cost alert settings");
  },

  /**
   * Loads the cost alert settings of the viewer's personal cost owner.
   *
   * @param signal Optional abort signal of the request.
   * @returns The settings, or null when the viewer has no personal cost owner (HTTP 204).
   */
  async personalCostAlertSettings(signal?: AbortSignal): Promise<CostAlertSettings | null> {
    const result = await getApiClient().request<CostAlertSettings>(
      apiPath("/cost-owners/mine/alert-settings"),
      { signal, expectedStatuses: [204] },
    );
    return result ?? null;
  },

  /**
   * Saves the complete cost alert configuration of one cost owner.
   *
   * @param costOwnerId The id of the cost owner.
   * @param update The full replacement, including the revision the editor loaded and the viewer's language.
   * @returns The saved settings.
   * @throws CostAlertSettingsConflictError When the settings changed elsewhere since they were loaded (HTTP 409).
   */
  async updateCostAlertSettings(
    costOwnerId: string,
    update: CostAlertSettingsUpdate,
  ): Promise<CostAlertSettings> {
    const path = "/cost-owners/{costOwnerId}/alert-settings" as const;
    try {
      const result = await getApiClient().request<CostAlertSettings>(
        fillApiPath(path, { costOwnerId }),
        { method: apiMethod(path, "put"), body: update },
      );
      return required(result, "Cost alert settings update");
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        throw new CostAlertSettingsConflictError();
      }
      throw error;
    }
  },

  /**
   * Saves the complete automation of one server in one request: idle shutdown, maximum runtime, time zone, weekly
   * rules and the scheduled-start consent.
   *
   * @param serverId The id of the server.
   * @param update The full replacement, including the version the editor loaded.
   * @returns The saved automation block as the control plane projects it.
   * @throws AutomationSettingsConflictError When the settings changed elsewhere since they were loaded (HTTP 409).
   * @throws AutomationSettingsForbiddenError When the viewer may not change the settings (HTTP 403).
   * @throws AutomationSettingsValidationError When the API rejects the draft (HTTP 400); `fields` lists the fields.
   */
  async saveAutomationSettings(
    serverId: string,
    update: ServerAutomationUpdate,
  ): Promise<ServerAutomation> {
    const path = "/game-servers/{gameServerId}/automation" as const;
    try {
      const result = await getApiClient().request<ServerAutomation>(
        fillApiPath(path, { gameServerId: serverId }),
        { method: apiMethod(path, "put"), body: update },
      );
      return required(result, "Automation settings update");
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.status === 409) throw new AutomationSettingsConflictError();
        if (error.status === 403) throw new AutomationSettingsForbiddenError();
        if (error.status === 400) throw new AutomationSettingsValidationError(error.fields ?? []);
      }
      throw error;
    }
  },

  async jobs(serverId: string, signal?: AbortSignal): Promise<LifecycleJob[]> {
    const result = await getApiClient().request<LifecycleJob[]>(
      fillApiPath("/game-servers/{gameServerId}/jobs", { gameServerId: serverId }),
      { signal },
    );
    return (result ?? []).map(normalizeJob);
  },

  async operations(serverId: string, take = 80, signal?: AbortSignal): Promise<HostingOperation[]> {
    const path = fillApiPath("/game-servers/{gameServerId}/operations", {
      gameServerId: serverId,
    });
    const result = await getApiClient().request<HostingOperation[]>(withQuery(path, { take }), {
      signal,
    });
    return result ?? [];
  },

  async configuration(serverId: string, signal?: AbortSignal): Promise<GameConfiguration> {
    const result = await getApiClient().request<GameConfiguration>(
      fillApiPath("/game-servers/{gameServerId}/configuration", { gameServerId: serverId }),
      { signal },
    );
    const value = required(result, "Server configuration");
    return normalizeConfiguration(value);
  },

  async updateConfiguration(
    serverId: string,
    values: ConfigurationValue[],
    resetKeys: string[],
    signal?: AbortSignal,
  ): Promise<GameConfiguration> {
    const path = "/game-servers/{gameServerId}/configuration" as const;
    const result = await getApiClient().request<GameConfiguration>(
      fillApiPath(path, { gameServerId: serverId }),
      { method: apiMethod(path, "put"), body: { values, resetKeys }, signal },
    );
    const value = required(result, "Configuration update");
    return normalizeConfiguration(value);
  },

  async selectConfigurationProfile(
    serverId: string,
    profileId: string | null,
    pinnedRevisionId: string | null,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/configuration/profile" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { profileId, pinnedRevisionId },
      signal,
    });
  },

  async serverConfigurationProfileOptions(
    serverId: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileOption[]> {
    const result = await getApiClient().request<ConfigurationProfileOption[]>(
      fillApiPath("/game-servers/{gameServerId}/configuration/profile-options", {
        gameServerId: serverId,
      }),
      { signal },
    );
    return result ?? [];
  },

  async storageDemand(serverId: string, signal?: AbortSignal): Promise<StorageDemand> {
    const result = await getApiClient().request<StorageDemand>(
      fillApiPath("/game-servers/{gameServerId}/storage-demand", { gameServerId: serverId }),
      { signal },
    );
    const demand = required(result, "Server storage estimate");
    return {
      ...demand,
      placementEstimate: demand.placementEstimate ?? null,
      components: demand.components ?? [],
      runtimeProfiles: demand.runtimeProfiles ?? [],
      missingComponents: demand.missingComponents ?? [],
    };
  },

  async changeRuntimeProfile(
    serverId: string,
    runtimeProfileIdentifier: string,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/runtime-profile" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { runtimeProfileIdentifier },
      signal,
    });
  },

  async changeRuntimeStorage(
    serverId: string,
    strategy: string,
    externalStorageSizeGiB: number | null,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/runtime-storage" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { strategy, externalStorageSizeGiB },
      signal,
    });
  },

  async modPackOptions(serverId: string, signal?: AbortSignal): Promise<ServerModPackOption[]> {
    const result = await getApiClient().request<ServerModPackOption[]>(
      fillApiPath("/game-servers/{gameServerId}/mod-pack-options", { gameServerId: serverId }),
      { signal },
    );
    return (result ?? []).map(normalizeModPackOption);
  },

  /**
   * Loads the flattened contents of one mod pack as the next Start would resolve them for the server.
   *
   * @param serverId The server the pack is selected for.
   * @param modPackDefinitionId The mod pack definition to load.
   * @param signal Optional abort signal of the request.
   * @returns The pack contents; items is an empty list when the pack cannot be resolved.
   */
  async modPackContents(
    serverId: string,
    modPackDefinitionId: number,
    signal?: AbortSignal,
  ): Promise<ServerModPackContents> {
    const result = await getApiClient().request<ServerModPackContents>(
      fillApiPath("/game-servers/{gameServerId}/mod-pack-options/{modPackDefinitionId}/contents", {
        gameServerId: serverId,
        modPackDefinitionId,
      }),
      { signal },
    );
    const value = required(result, "Mod pack contents");
    return { ...value, items: value.items ?? [] };
  },

  async changeModPack(
    serverId: string,
    modPackDefinitionId: number | null,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/mod-pack" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { modPackDefinitionId },
      signal,
    });
  },

  async changeContentSelection(
    serverId: string,
    terrainId: number | null,
    scenarioId: string | null,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/content-selection" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { terrainId, terrainVersionId: null, scenarioId, scenarioVersionId: null },
      signal,
    });
  },

  async files(
    serverId: string,
    context: HostingFileContext,
    filePath = "",
    signal?: AbortSignal,
  ): Promise<HostingFile[]> {
    const base = fillApiPath("/game-servers/{gameServerId}/files/{context}", {
      gameServerId: serverId,
      context,
    });
    const result = await getApiClient().request<HostingFile[]>(
      withQuery(base, { path: filePath }),
      {
        signal,
      },
    );
    return result ?? [];
  },

  fileDownloadUrl(serverId: string, context: HostingFileContext, filePath: string): string {
    const base = fillApiPath("/game-servers/{gameServerId}/files/{context}/content", {
      gameServerId: serverId,
      context,
    });
    return getApiClient()
      .url(withQuery(base, { path: filePath }))
      .toString();
  },

  async uploadFile(
    serverId: string,
    context: HostingFileContext,
    filePath: string,
    file: Blob,
    confirmGeneratedOverride: boolean,
    signal?: AbortSignal,
  ): Promise<HostingFile> {
    const base = fillApiPath("/game-servers/{gameServerId}/files/{context}", {
      gameServerId: serverId,
      context,
    });
    const path = withQuery(base, {
      path: filePath,
      confirmGeneratedOverride: String(confirmGeneratedOverride),
    });
    const result = await getApiClient().request<HostingFile>(path, {
      method: "PUT",
      body: file,
      signal,
    });
    return required(result, "File upload");
  },

  async createDirectory(
    serverId: string,
    context: HostingFileContext,
    filePath: string,
    signal?: AbortSignal,
  ): Promise<HostingFile> {
    const path = "/game-servers/{gameServerId}/files/{context}" as const;
    const result = await getApiClient().request<HostingFile>(
      fillApiPath(path, { gameServerId: serverId, context }),
      { method: apiMethod(path, "post"), body: { path: filePath }, signal },
    );
    return required(result, "Directory creation");
  },

  async deleteFile(
    serverId: string,
    context: HostingFileContext,
    filePath: string,
    recursive: boolean,
    signal?: AbortSignal,
  ): Promise<void> {
    const template = "/game-servers/{gameServerId}/files/{context}" as const;
    const base = fillApiPath(template, { gameServerId: serverId, context });
    await getApiClient().request<void>(
      withQuery(base, { path: filePath, recursive: String(recursive) }),
      { method: apiMethod(template, "delete"), signal },
    );
  },

  async preservationSelections(
    serverId: string,
    signal?: AbortSignal,
  ): Promise<PreservationSelection[]> {
    const result = await getApiClient().request<PreservationSelection[]>(
      fillApiPath("/game-servers/{gameServerId}/preservation-selections", {
        gameServerId: serverId,
      }),
      { signal },
    );
    return result ?? [];
  },

  async savePreservationSelection(
    serverId: string,
    payload: PreservationSelectionInput,
    signal?: AbortSignal,
  ): Promise<PreservationSelection> {
    const path = "/game-servers/{gameServerId}/preservation-selections" as const;
    const result = await getApiClient().request<PreservationSelection>(
      fillApiPath(path, { gameServerId: serverId }),
      { method: apiMethod(path, "post"), body: payload, signal },
    );
    return required(result, "Preservation selection");
  },

  async deletePreservationSelection(
    serverId: string,
    selectionId: string,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/preservation-selections/{selectionId}" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId, selectionId }), {
      method: apiMethod(path, "delete"),
      signal,
    });
  },

  async executeAction(
    serverId: string,
    actionId: string,
    confirmation?: string,
    signal?: AbortSignal,
  ): Promise<LifecycleJob> {
    const path = "/game-servers/{gameServerId}/actions/{actionId}" as const;
    const result = await getApiClient().request<LifecycleJob>(
      fillApiPath(path, { gameServerId: serverId, actionId }),
      {
        method: apiMethod(path, "post"),
        body: { confirmation: confirmation ?? null },
        signal,
      },
    );
    return normalizeJob(required(result, "Server action"));
  },

  async retryJob(jobId: string, signal?: AbortSignal): Promise<LifecycleJob> {
    const path = "/jobs/{lifecycleJobId}/retry" as const;
    const result = await getApiClient().request<LifecycleJob>(
      fillApiPath(path, { lifecycleJobId: jobId }),
      { method: apiMethod(path, "post"), signal },
    );
    return normalizeJob(required(result, "Job retry"));
  },

  async skipJob(jobId: string, signal?: AbortSignal): Promise<LifecycleJob> {
    const path = "/jobs/{lifecycleJobId}/skip" as const;
    const result = await getApiClient().request<LifecycleJob>(
      fillApiPath(path, { lifecycleJobId: jobId }),
      { method: apiMethod(path, "post"), signal },
    );
    return normalizeJob(required(result, "Job skip"));
  },

  async create(payload: ServerCreation, signal?: AbortSignal): Promise<GameServer> {
    const path = "/game-servers" as const;
    const result = await getApiClient().request<GameServer>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: payload,
      signal,
    });
    return normalizeServer(required(result, "Server creation"));
  },

  async delete(serverId: string, signal?: AbortSignal): Promise<void> {
    const path = "/game-servers/{gameServerId}" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "delete"),
      signal,
    });
  },

  async creationModPacks(gameIdentifier: string, signal?: AbortSignal) {
    const path = apiPathWithQuery(
      "/game-servers/creation/mod-pack-options",
      new URLSearchParams({ gameIdentifier }).toString(),
    );
    const result = await getApiClient().request<ServerModPackOption[]>(path, { signal });
    return (result ?? []).map(normalizeModPackOption);
  },

  async terrains(gameIdentifier: string, signal?: AbortSignal): Promise<GameContentOption[]> {
    const path = apiPathWithQuery(
      "/game-content/terrains",
      new URLSearchParams({ gameIdentifier }).toString(),
    );
    const result = await getApiClient().request<GameContentOption[]>(path, { signal });
    return (result ?? []).map((item) => ({ ...item, versions: item.versions ?? [] }));
  },

  async scenarios(gameIdentifier: string, signal?: AbortSignal): Promise<GameContentOption[]> {
    const path = apiPathWithQuery(
      "/game-content/scenarios",
      new URLSearchParams({ gameIdentifier }).toString(),
    );
    const result = await getApiClient().request<GameContentOption[]>(path, { signal });
    return (result ?? []).map((item) => ({ ...item, versions: item.versions ?? [] }));
  },

  async secrets(serverId: string, signal?: AbortSignal): Promise<GameServerSecretStatus[]> {
    return (
      (await getApiClient().request<GameServerSecretStatus[]>(
        fillApiPath("/game-servers/{gameServerId}/secrets", { gameServerId: serverId }),
        { signal },
      )) ?? []
    );
  },

  async setSecret(
    serverId: string,
    key: string,
    value: string,
    externalProvider: boolean,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/secrets" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { key, value, origin: externalProvider ? "ExternalProvider" : "UserProvided" },
    });
  },

  async rotateSecret(serverId: string, key: string): Promise<GameServerSecretValue> {
    const path = "/game-servers/{gameServerId}/secrets/{key}/rotate" as const;
    const result = await getApiClient().request<GameServerSecretValue>(
      fillApiPath(path, { gameServerId: serverId, key }),
      { method: apiMethod(path, "post") },
    );
    return required(result, "Secret rotation");
  },

  async revealSecret(serverId: string, key: string): Promise<GameServerSecretValue> {
    const path = "/game-servers/{gameServerId}/secrets/{key}/reveal" as const;
    const result = await getApiClient().request<GameServerSecretValue>(
      fillApiPath(path, { gameServerId: serverId, key }),
      { method: apiMethod(path, "post") },
    );
    return required(result, "Secret reveal");
  },

  /**
   * Lists the cost owners the caller may transfer one server to: everyone for an administrator, otherwise the
   * caller's own owner and the owners the caller holds a use grant for. The current owner is never listed and
   * frozen owners are listed with their frozen flag. An empty list is a normal answer, not an error.
   *
   * @param serverId The server the owners are listed for.
   * @param search The owner display-name search text; blank matches every owner.
   * @param signal Aborts the request.
   * @returns At most 20 owners, or an empty list.
   */
  async serverCostOwnerOptions(
    serverId: string,
    search: string,
    signal?: AbortSignal,
  ): Promise<ServerCostOwnerOption[]> {
    const route = "/game-servers/{gameServerId}/cost-owner/options" as const;
    const path = fillApiPath(route, { gameServerId: serverId });
    const query = new URLSearchParams({ search, take: String(pickerRequestTake) });
    return (
      (await getApiClient().request<ServerCostOwnerOption[]>(`${path}?${query.toString()}`, {
        method: apiMethod(route, "get"),
        signal,
      })) ?? []
    );
  },

  /**
   * Transfers the cost owner of one server. The API accepts a transfer to the current owner as a no-op (HTTP 204), so
   * a repeated request never fails; it rejects a transfer while a deployment is unresolved (HTTP 409), an owner the
   * caller may not use or a frozen owner (HTTP 400) and a caller without the right (HTTP 403).
   *
   * @param serverId The server whose cost owner changes.
   * @param costOwnerId The cost owner that pays for the server from now on.
   * @param signal Aborts the request.
   */
  async changeServerCostOwner(
    serverId: string,
    costOwnerId: string,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/cost-owner" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { costOwnerId },
      signal,
    });
  },

  async creationCostOwners(signal?: AbortSignal): Promise<ServerCreationCostOwnerOption[]> {
    return (
      (await getApiClient().request<ServerCreationCostOwnerOption[]>(
        apiPath("/game-servers/creation/cost-owner-options"),
        { signal },
      )) ?? []
    );
  },

  async profileOptions(
    gameIdentifier: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileOption[]> {
    const path = apiPathWithQuery(
      "/configuration-profiles/options",
      new URLSearchParams({ gameIdentifier }).toString(),
    );
    return (await getApiClient().request<ConfigurationProfileOption[]>(path, { signal })) ?? [];
  },

  async estimateStorage(
    gameIdentifier: string,
    runtimeProviderIdentifier: string,
    modPackDefinitionId: number | null,
    signal?: AbortSignal,
  ): Promise<StorageDemand> {
    const path = "/game-servers/storage-demand-estimate" as const;
    const result = await getApiClient().request<StorageDemand>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: { gameIdentifier, runtimeProviderIdentifier, modPackDefinitionId },
      signal,
    });
    const demand = required(result, "Storage estimate");
    return {
      ...demand,
      components: demand.components ?? [],
      runtimeProfiles: demand.runtimeProfiles ?? [],
      missingComponents: demand.missingComponents ?? [],
    };
  },

  async profiles(signal?: AbortSignal): Promise<ConfigurationProfileSummary[]> {
    return (
      (await getApiClient().request<ConfigurationProfileSummary[]>(
        apiPath("/configuration-profiles"),
        { signal },
      )) ?? []
    );
  },

  async profileCatalog(signal?: AbortSignal): Promise<ConfigurationProfileCatalog> {
    const result = await getApiClient().request<ConfigurationProfileCatalog>(
      apiPath("/configuration-profiles/definitions"),
      { signal },
    );
    return required(result, "Configuration profile catalog");
  },

  async profile(profileId: string, signal?: AbortSignal): Promise<ConfigurationProfileDetails> {
    const result = await getApiClient().request<ConfigurationProfileDetails>(
      fillApiPath("/configuration-profiles/{profileId}", { profileId }),
      { signal },
    );
    return required(result, "Configuration profile");
  },

  async profileAccess(
    profileId: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileAccess> {
    const result = await getApiClient().request<ConfigurationProfileAccess>(
      fillApiPath("/configuration-profiles/{profileId}/access", { profileId }),
      { signal },
    );
    const access = required(result, "Configuration profile access");
    return {
      allowedPermissions: access.allowedPermissions ?? [],
      users: (access.users ?? []).map((user) => ({ ...user, permissions: user.permissions ?? [] })),
      roles: (access.roles ?? []).map((role) => ({ ...role, permissions: role.permissions ?? [] })),
    };
  },

  async createProfile(
    title: string,
    gameIdentifier: string,
    values: ConfigurationValue[],
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileSummary> {
    const path = "/configuration-profiles" as const;
    const result = await getApiClient().request<ConfigurationProfileSummary>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: { title, gameIdentifier, values },
      signal,
    });
    return required(result, "Configuration profile creation");
  },

  async createProfileRevision(
    profileId: string,
    baseRevisionNumber: number,
    values: ConfigurationValue[],
    resetKeys: string[],
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/configuration-profiles/{profileId}/revisions" as const;
    await getApiClient().request<void>(fillApiPath(path, { profileId }), {
      method: apiMethod(path, "post"),
      body: { baseRevisionNumber, values, resetKeys },
      signal,
    });
  },

  async profileUserCandidates(
    profileId: string,
    search: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileUserCandidate[]> {
    const route = "/configuration-profiles/{profileId}/access/users/options" as const;
    const path = fillApiPath(route, { profileId });
    const query = new URLSearchParams({ search, take: "20" });
    return (
      (await getApiClient().request<ConfigurationProfileUserCandidate[]>(
        `${path}?${query.toString()}`,
        { method: apiMethod(route, "get"), signal },
      )) ?? []
    );
  },

  async profileRoleCandidates(
    profileId: string,
    search: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileRoleCandidate[]> {
    const route = "/configuration-profiles/{profileId}/access/roles/options" as const;
    const path = fillApiPath(route, { profileId });
    const query = new URLSearchParams({ search, take: "20" });
    return (
      (await getApiClient().request<ConfigurationProfileRoleCandidate[]>(
        `${path}?${query.toString()}`,
        { method: apiMethod(route, "get"), signal },
      )) ?? []
    );
  },

  async updateProfileAccessMatrix(
    profileId: string,
    matrix: ConfigurationProfileAccessMatrix,
    signal?: AbortSignal,
  ): Promise<void> {
    const route = "/configuration-profiles/{profileId}/access/matrix" as const;
    await getApiClient().request<void>(fillApiPath(route, { profileId }), {
      method: apiMethod(route, "post"),
      body: matrix,
      signal,
    });
  },

  async archiveProfile(profileId: string, signal?: AbortSignal): Promise<void> {
    const path = "/configuration-profiles/{profileId}/archive" as const;
    await getApiClient().request<void>(fillApiPath(path, { profileId }), {
      method: apiMethod(path, "post"),
      signal,
    });
  },

  async changeProfileUserAccess(
    profileId: string,
    userId: string,
    permission: string,
    grant: boolean,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = grant
      ? ("/configuration-profiles/{profileId}/access/users/grant" as const)
      : ("/configuration-profiles/{profileId}/access/users/revoke" as const);
    await getApiClient().request<void>(fillApiPath(path, { profileId }), {
      method: apiMethod(path, "post"),
      body: { userId, permission },
      signal,
    });
  },

  async changeProfileRoleAccess(
    profileId: string,
    roleId: number,
    permission: string,
    grant: boolean,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = grant
      ? ("/configuration-profiles/{profileId}/access/roles/grant" as const)
      : ("/configuration-profiles/{profileId}/access/roles/revoke" as const);
    await getApiClient().request<void>(fillApiPath(path, { profileId }), {
      method: apiMethod(path, "post"),
      body: { roleId, permission },
      signal,
    });
  },

  /**
   * Loads the direct access grants of one server: the members and roles that hold permissions, the permissions that
   * can be delegated and the subset the caller may assign. Only members who may manage access can read it.
   *
   * @param serverId The server whose grants are read.
   * @param signal Aborts the request.
   * @returns The grants with every list defaulted, so a degraded payload never breaks the panel.
   */
  async serverAccess(serverId: string, signal?: AbortSignal): Promise<ServerAccess> {
    const result = await getApiClient().request<ServerAccess>(
      fillApiPath("/game-servers/{gameServerId}/access", { gameServerId: serverId }),
      { signal },
    );
    const access = required(result, "Server access");
    return {
      allowedPermissions: access.allowedPermissions ?? [],
      assignablePermissions: access.assignablePermissions ?? [],
      users: (access.users ?? []).map((user) => ({ ...user, permissions: user.permissions ?? [] })),
      roles: (access.roles ?? []).map((role) => ({ ...role, permissions: role.permissions ?? [] })),
    };
  },

  /**
   * Searches the members that can still be given access to a server: verified members who are neither deleted nor
   * banned and hold no direct grant on it. Only members who may manage access can search.
   *
   * @param serverId The server the members would get access to.
   * @param search The nickname search text; blank matches every member.
   * @param signal Aborts the request.
   * @returns At most 20 candidates ordered by nickname, or an empty list.
   */
  async serverUserCandidates(
    serverId: string,
    search: string,
    signal?: AbortSignal,
  ): Promise<ServerAccessUserCandidate[]> {
    const route = "/game-servers/{gameServerId}/access/users/options" as const;
    const path = fillApiPath(route, { gameServerId: serverId });
    const query = new URLSearchParams({ search, take: String(pickerRequestTake) });
    return (
      (await getApiClient().request<ServerAccessUserCandidate[]>(`${path}?${query.toString()}`, {
        method: apiMethod(route, "get"),
        signal,
      })) ?? []
    );
  },

  /**
   * Searches the roles that can still be given access to a server: roles that hold no direct grant on it. Only
   * members who may manage access can search.
   *
   * @param serverId The server the roles would get access to.
   * @param search The role title search text; blank matches every role.
   * @param signal Aborts the request.
   * @returns At most 20 candidates ordered by title, or an empty list.
   */
  async serverRoleCandidates(
    serverId: string,
    search: string,
    signal?: AbortSignal,
  ): Promise<ServerAccessRoleCandidate[]> {
    const route = "/game-servers/{gameServerId}/access/roles/options" as const;
    const path = fillApiPath(route, { gameServerId: serverId });
    const query = new URLSearchParams({ search, take: String(pickerRequestTake) });
    return (
      (await getApiClient().request<ServerAccessRoleCandidate[]>(`${path}?${query.toString()}`, {
        method: apiMethod(route, "get"),
        signal,
      })) ?? []
    );
  },

  /**
   * Grants or revokes one permission of one member on a server. Both calls are idempotent: granting a held
   * permission and revoking a missing one succeed and change nothing.
   *
   * @param serverId The server the permission applies to.
   * @param userId The member who receives or loses the permission.
   * @param permission The exact permission claim id, for example `sv:view`.
   * @param grant True to grant, false to revoke.
   * @param signal Aborts the request.
   */
  async changeServerUserAccess(
    serverId: string,
    userId: string,
    permission: string,
    grant: boolean,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = grant
      ? ("/game-servers/{gameServerId}/access/users/grant" as const)
      : ("/game-servers/{gameServerId}/access/users/revoke" as const);
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "post"),
      body: { userId, permission },
      signal,
    });
  },

  /**
   * Grants or revokes one permission of one role on a server. Both calls are idempotent: granting a held
   * permission and revoking a missing one succeed and change nothing.
   *
   * @param serverId The server the permission applies to.
   * @param roleId The role that receives or loses the permission.
   * @param permission The exact permission claim id, for example `sv:view`.
   * @param grant True to grant, false to revoke.
   * @param signal Aborts the request.
   */
  async changeServerRoleAccess(
    serverId: string,
    roleId: number,
    permission: string,
    grant: boolean,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = grant
      ? ("/game-servers/{gameServerId}/access/roles/grant" as const)
      : ("/game-servers/{gameServerId}/access/roles/revoke" as const);
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "post"),
      body: { roleId, permission },
      signal,
    });
  },
};

export type HostingService = typeof hostingService;
