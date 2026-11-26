import type { Page } from "@playwright/test";
import type { ServerAutomation } from "@/features/hosting/types";
import { fulfillCostRoute } from "./costMocks";
import { fulfillMetricsRoute } from "./metricsMocks";
import { seedSettingsPanels } from "./settingsPanels";

/** The culture of the mocked session; it is stored the way the app stores the viewer's choice. */
export type AutomationCulture = "en" | "de";

/** The id of the mocked server, shared by every automation spec. */
export const automationServerId = "10000000-0000-4000-8000-000000000d68";

/** The cost owner of the mocked server. */
export const automationCostOwnerId = "40000000-0000-4000-8000-000000000d68";

/** The title of the mocked server; the workspace heading shows it. */
export const automationServerTitle = "Automation test server";

/** A value whose members may be replaced one by one, at any depth. */
export type DeepPartial<T> = T extends readonly unknown[]
  ? T
  : T extends object
    ? { [Key in keyof T]?: DeepPartial<T[Key]> }
    : T;

/** One JSON record: the mock server, a job or an operation. The strict shape lives in the app, not in the mock. */
export type JsonRecord = Record<string, unknown>;

/** The body of the automation update the page sends. */
export interface AutomationSaveBody {
  /** The version the editor loaded. */
  version: number;
  /** Whether idle shutdown is on. */
  idleShutdownEnabled: boolean;
  /** The idle limit in minutes. */
  idleShutdownMinutes: number;
  /** Whether the maximum runtime is on. */
  maxRuntimeEnabled: boolean;
  /** The maximum runtime in hours. */
  maxRuntimeHours: number;
  /** The schedule time zone, or null. */
  timeZone: string | null;
  /** The complete rule list. */
  schedules: { id?: string | null; action: string; days: number[]; time: string }[];
  /** Whether the consent is recorded with this save. */
  startConsent: boolean;
}

/** What a custom save handler answers. */
export interface AutomationSaveAnswer {
  /** The HTTP status. */
  status: number;
  /** The JSON body, or undefined for an empty body. */
  body?: unknown;
}

/**
 * Merges a partial value into a complete one. Arrays and scalars replace; plain objects merge member by member.
 *
 * @param base The complete value.
 * @param patch The members to replace.
 * @returns A new value; the inputs are not changed.
 */
function merge<T>(base: T, patch: DeepPartial<T> | undefined): T {
  if (patch === undefined) return structuredClone(base);
  if (
    typeof base !== "object" ||
    base === null ||
    Array.isArray(base) ||
    typeof patch !== "object" ||
    patch === null ||
    Array.isArray(patch)
  ) {
    return structuredClone(patch as T);
  }
  const result: Record<string, unknown> = { ...(structuredClone(base) as Record<string, unknown>) };
  for (const [key, value] of Object.entries(patch)) {
    result[key] = merge((base as Record<string, unknown>)[key], value as DeepPartial<unknown>);
  }
  return result as T;
}

/**
 * Builds the automation block of a server: nothing saved, idle shutdown available, no pending stop and no consent
 * needed. Every call returns a fresh object.
 *
 * @param overrides Members to replace at any depth; arrays replace as a whole.
 * @returns The automation block as the API sends it.
 */
export function automationBlock(overrides?: DeepPartial<ServerAutomation>): ServerAutomation {
  const base: ServerAutomation = {
    settings: {
      idleShutdownEnabled: false,
      idleShutdownMinutes: 30,
      maxRuntimeEnabled: false,
      maxRuntimeHours: 6,
      timeZone: "UTC",
      schedules: [],
      version: 0,
    },
    limits: {
      idleMinutesMin: 5,
      idleMinutesMax: 720,
      maxRuntimeHoursMin: 1,
      maxRuntimeHoursMax: 168,
      maxSchedules: 20,
      warningLeadMinutes: 5,
    },
    idleShutdown: { available: true },
    status: {
      idle: { state: "NotRunning", since: null, stopsAt: null },
      maxRuntimeStopsAt: null,
      nextScheduled: null,
      pendingStop: null,
    },
    startConsent: { state: "NotRequired" },
  };
  return merge<ServerAutomation>(base, overrides);
}

/** The Stop action every running server of the mocks offers. */
const stopAction = {
  id: "stop",
  label: "Stop",
  description: "Saves the game state and stops the server.",
  icon: "stop",
  group: "Lifecycle",
  order: 20,
  enabled: true,
  unavailableReason: null,
  dangerous: false,
  confirmationText: "Stopping releases the cloud machine after the data is saved.",
  confirmationChallenge: null,
  pending: false,
  expectedDurationSeconds: 120,
  costImpact: "StopsCost",
  costDescription: "Provider cost stops after verified cleanup.",
};

/** The running deployment of the mocked server. */
const runningDeployment = {
  status: "Running",
  stage: "Ready",
  possibleCost: true,
  workerStatus: "Connected",
  lastHeartbeat: "2026-11-16T10:00:00Z",
  connectionEndpoint: "203.0.113.5:2302",
  connectionProtocol: "udp",
  publicAddress: null,
  deploymentRoot: "/srv/utf",
  includedStorageBytes: 85899345920,
  selectedExternalStorageBytes: null,
  estimatedComputeHourlyCostEur: 0.1,
  estimatedStorageHourlyCostEur: 0,
  diskTotalBytes: null,
  diskAvailableBytes: null,
  diskTimeStampObserved: null,
  faultSummary: null,
  administrativeSshCommand: null,
  resources: [],
};

/** Options of {@link automationServer}. */
export interface AutomationServerOptions {
  /** The automation block; defaults to {@link automationBlock} with nothing set. */
  automation?: ServerAutomation | null;
  /** Whether the viewer may change the automation; defaults to true. */
  canManage?: boolean;
  /** Members of the server to replace after everything else was set. */
  overrides?: JsonRecord;
}

/**
 * Builds the detail and fleet projection of a running, costing server with an automation block.
 *
 * @param options The block, the viewer's right and member overrides.
 * @returns The server as the API sends it.
 */
export function automationServer(options: AutomationServerOptions = {}): JsonRecord {
  return {
    primaryKey: automationServerId,
    title: automationServerTitle,
    gameIdentifier: "arma3",
    runtimeProviderIdentifier: "fake-cloud",
    runtimeProfileIdentifier: "medium",
    runtimeProfileDisplayName: "Medium",
    runtimeStorageStrategy: "IncludedDisk",
    externalStorageSizeGiB: null,
    estimatedHourlyCostEur: 0.2,
    estimatedMonthlyCostEur: 146,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    costOwnerId: automationCostOwnerId,
    costOwnerName: "Operations",
    createdById: automationServerId,
    isLaunchDisabled: false,
    isArchived: false,
    canModify: true,
    canManageSecrets: false,
    canRevealSecrets: false,
    canManageCostAlerts: true,
    costAlertThresholds: [],
    changesPendingStartAreas: [],
    hasChangesPendingStart: false,
    timeStampCreated: "2026-11-16T10:00:00Z",
    timeStampUpdated: "2026-11-16T10:00:00Z",
    pendingJobCount: 0,
    deployment: runningDeployment,
    actions: [stopAction],
    automation: options.automation === undefined ? automationBlock() : options.automation,
    canManageAutomation: options.canManage ?? true,
    ...options.overrides,
  };
}

/**
 * Builds a finished Stop job that an automatic rule queued.
 *
 * @param trigger The cause: `Idle`, `MaxRuntime` or `Schedule`.
 * @param triggerLimit Minutes for `Idle`, hours for `MaxRuntime`, otherwise null.
 * @returns The job as the API sends it.
 */
export function automaticStopJob(
  trigger: "Idle" | "MaxRuntime" | "Schedule",
  triggerLimit: number | null,
): JsonRecord {
  return {
    primaryKey: "10b00000-0000-4000-8000-000000000d68",
    gameServerId: automationServerId,
    deploymentId: null,
    kind: "Stop",
    actionId: "stop",
    status: "Succeeded",
    stage: "completed",
    progressPercent: 100,
    attemptCount: 1,
    failureCode: null,
    failureReason: null,
    requestedByName: null,
    trigger,
    triggerLimit,
    canRetry: false,
    canSkip: false,
    timeStampCreated: "2026-11-16T10:00:00Z",
    timeStampUpdated: "2026-11-16T10:05:00Z",
    timeStampStarted: "2026-11-16T10:01:00Z",
    timeStampCompleted: "2026-11-16T10:05:00Z",
    commands: [],
  };
}

/** The seven audit kinds Phase 68 added, in the order the control plane raises them. */
export const automationAuditKinds = [
  "AutomaticStopWarned",
  "AutomaticStopRequested",
  "AutomaticStopCancelled",
  "ScheduledStartRequested",
  "ScheduledRunSkipped",
  "AutomationSettingsChanged",
  "ScheduledStartsConfirmed",
] as const;

/**
 * Builds the audit entries of the seven automation kinds, newest first.
 *
 * @returns One operation per kind as the API sends it.
 */
export function automationAuditOperations(): JsonRecord[] {
  return automationAuditKinds.map((kind, index) => ({
    primaryKey: `0e000000-0000-4000-8000-${String(index + 1).padStart(12, "0")}`,
    kind,
    status: "Succeeded",
    summary: kind,
    detail: null,
    providerIdentifier: null,
    resourceKind: null,
    resourceIdentifier: null,
    providerState: null,
    deploymentId: null,
    lifecycleJobId: null,
    workerId: null,
    traceId: null,
    spanId: null,
    actorName: null,
    timeStampCreated: new Date(Date.UTC(2026, 10, 16, 10, 10 - index, 0)).toISOString(),
  }));
}

/** Options of {@link mockAutomationRoutes}. */
export interface AutomationRouteOptions {
  /** The server the page loads; the handle keeps it so a test can replace it before a refetch. */
  server: JsonRecord;
  /** Whether the viewer may change the automation; applied to the server as `canManageAutomation`. */
  canManage: boolean;
  /**
   * Answers a save instead of the default behavior, which stores the body like the control plane does and answers
   * the new automation block.
   *
   * @param body The parsed request body.
   * @returns The status and body to answer with.
   */
  onSave?: (body: unknown) => AutomationSaveAnswer;
  /** The culture of the session; defaults to English. */
  culture?: AutomationCulture;
  /** The lifecycle jobs of the server; defaults to none. */
  jobs?: JsonRecord[];
  /** The hosting operations (the Audit tab entries) of the server; defaults to none. */
  operations?: JsonRecord[];
  /** The undismissed cost alerts of the viewer; defaults to none. */
  alerts?: JsonRecord[];
  /**
   * The Settings panel anchor ids to open before the first navigation; every panel by default, an empty array for the
   * collapsed first-visit default.
   */
  settingsPanels?: readonly string[] | undefined;
}

/** What a test can read and change while the mocked routes are installed. */
export interface AutomationRouteHandle {
  /** The parsed body of every automation PUT, in order. */
  saves: unknown[];
  /** Requests no mock handled; the route aborts them and every test asserts this list is empty. */
  unexpected: string[];
  /** The mutable state behind the routes; replace a member and the next read answers it. */
  state: {
    /** The server projection. */
    server: JsonRecord;
    /** The lifecycle jobs. */
    jobs: JsonRecord[];
    /** The hosting operations. */
    operations: JsonRecord[];
    /** The undismissed alerts. */
    alerts: JsonRecord[];
  };
}

/** The configuration read of a game without entries, enough for the Settings section to render. */
const emptyConfiguration = {
  serverId: automationServerId,
  profileId: null,
  profileRevisionId: null,
  profileRevisionNumber: null,
  isComplete: true,
  entries: [],
};

/** The capability of the mocked game. */
const capability = {
  gameIdentifier: "arma3",
  displayName: "Arma 3",
  description: "Dedicated Arma operations.",
  runtimeProviders: [],
  actions: [],
  modProviderBindings: [],
  modPackFormats: [],
  requiresTerrain: false,
  terrainLabel: "Terrain",
  requiresScenario: false,
  scenarioLabel: "Mission",
  supportsModPacks: true,
  requiresModPack: true,
  defaultTerrainSourceIdentifier: null,
  defaultScenarioSourceIdentifier: null,
  runtimeProviderProfiles: [],
};

/** The weekdays of a saved rule in ISO order with duplicates removed. */
function normalizedDays(days: number[]): number[] {
  return [...new Set(days)].sort((left, right) => left - right);
}

/**
 * Applies a save the way the control plane does: the rules get ids, the version moves on and a start rule with the
 * consent flag turns the consent state to given.
 *
 * @param current The automation block before the save.
 * @param body The request body.
 * @returns The saved automation block.
 */
function applySave(current: ServerAutomation, body: AutomationSaveBody): ServerAutomation {
  const schedules = body.schedules.map((rule, index) => ({
    id: rule.id ?? `a0000000-0000-4000-8000-${String(index + 1).padStart(12, "0")}`,
    action: rule.action,
    days: normalizedDays(rule.days),
    time: rule.time,
    nextRunAt: null,
    lastRun: null,
  }));
  const hasStart = schedules.some((rule) => rule.action === "Start");
  return {
    ...current,
    settings: {
      idleShutdownEnabled: body.idleShutdownEnabled,
      idleShutdownMinutes: body.idleShutdownMinutes,
      maxRuntimeEnabled: body.maxRuntimeEnabled,
      maxRuntimeHours: body.maxRuntimeHours,
      timeZone: body.timeZone,
      schedules,
      version: current.settings.version + 1,
    },
    startConsent: {
      state: !hasStart
        ? "NotRequired"
        : body.startConsent || current.startConsent.state === "Given"
          ? "Given"
          : "Needed",
    },
  } as ServerAutomation;
}

/**
 * Installs the strict API mock set of an automation journey with `page.route`.
 *
 * The server detail and the fleet list answer from one mutable server, so a refetch after a save or a replaced
 * server shows the new state. The Settings section reads its other endpoints; they answer the quiet state of a game
 * without entries. Requests nothing handles are aborted and recorded, which also proves the page made no request the
 * test did not expect.
 *
 * @param page The page to mock; call before the first navigation.
 * @param options The server, the viewer's right and the optional handlers and lists.
 * @returns The saved bodies, the unexpected requests and the mutable state.
 */
export async function mockAutomationRoutes(
  page: Page,
  options: AutomationRouteOptions,
): Promise<AutomationRouteHandle> {
  const handle: AutomationRouteHandle = {
    saves: [],
    unexpected: [],
    state: {
      server: { ...options.server, canManageAutomation: options.canManage },
      jobs: options.jobs ?? [],
      operations: options.operations ?? [],
      alerts: options.alerts ?? [],
    },
  };
  const base = `/api/game-servers/${automationServerId}`;
  await page.addInitScript(
    (value) => localStorage.setItem("UtfCulture", value),
    options.culture ?? "en",
  );
  await seedSettingsPanels(page, options.settingsPanels);
  await page.route("**/appsettings.json", (route) =>
    route.fulfill({
      json: {
        Api: { BaseUrl: "/api/" },
        Urls: { TeamSpeak3: "", Discord: "" },
        Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
      },
    }),
  );
  await page.route("**/Insignia-48px.png", (route) =>
    route.fulfill({ status: 200, contentType: "image/png", body: "" }),
  );
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const method = request.method();
    const path = new URL(request.url()).pathname;
    if (path === "/api/users/me") {
      return route.fulfill({
        json: {
          primaryKey: automationServerId,
          nickname: "Operator",
          isBanned: false,
          isVerified: true,
          isDeleted: false,
          isVereinMember: false,
          claims: [{ identifier: "sv", value: automationServerId }],
          roles: [],
        },
      });
    }
    if (path === "/api/users/authentication-providers") {
      return route.fulfill({ json: { steam: true, discord: true } });
    }
    if (method === "PUT" && path === `${base}/automation`) {
      const body = request.postDataJSON() as AutomationSaveBody;
      handle.saves.push(body);
      if (options.onSave) {
        const answer = options.onSave(body);
        return route.fulfill({
          status: answer.status,
          ...(answer.body === undefined ? {} : { json: answer.body }),
        });
      }
      const current = handle.state.server["automation"] as ServerAutomation;
      const saved = applySave(current, body);
      handle.state.server = { ...handle.state.server, automation: saved };
      return route.fulfill({ json: saved });
    }
    if (method === "GET") {
      if (path === base) return route.fulfill({ json: handle.state.server });
      if (path === "/api/game-servers") return route.fulfill({ json: [handle.state.server] });
      if (path === `${base}/jobs`) return route.fulfill({ json: handle.state.jobs });
      if (path === `${base}/operations`) return route.fulfill({ json: handle.state.operations });
      if (path === "/api/cost-alerts") return route.fulfill({ json: handle.state.alerts });
      if (path === `${base}/accrued-cost`) {
        return route.fulfill({ json: { gameServerId: automationServerId, session: null } });
      }
      if (path === "/api/game-servers/capabilities") return route.fulfill({ json: [capability] });
      // The Cost owner picker requests owners on its first focus, which the keyboard walks reach.
      if (path === `${base}/cost-owner/options`) return route.fulfill({ json: [] });
      if (path === `${base}/configuration`) return route.fulfill({ json: emptyConfiguration });
      if (path === `${base}/storage-demand`) {
        return route.fulfill({
          json: {
            placementEstimate: null,
            components: [],
            runtimeProfiles: [],
            missingComponents: [],
          },
        });
      }
      if (
        [
          "/api/site-image-settings",
          "/api/game-servers/notifications",
          `${base}/mod-pack-options`,
          `${base}/secrets`,
          `${base}/configuration/profile-options`,
          `${base}/preservation-selections`,
          `${base}/files/persistent`,
          `${base}/files/live`,
        ].includes(path)
      ) {
        return route.fulfill({ json: [] });
      }
    }
    if (await fulfillMetricsRoute(route, method, path, automationServerId)) return;
    if (await fulfillCostRoute(route, method, path, automationServerId)) return;
    handle.unexpected.push(`${method} ${path}`);
    return route.abort();
  });
  return handle;
}
