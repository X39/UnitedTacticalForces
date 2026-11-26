import type { Page } from "@playwright/test";
import type {
  CostAlert,
  GameServer,
  LifecycleJob,
  ServerAction,
  ServerAutomation,
  ServerDeployment,
} from "@/features/hosting/types";
import type { AutomationSaveBody } from "./automationMocks";
import { fleetChangedFrame, type FleetStreamMock } from "./fleetStream";
import { fulfillMetricsRoute } from "./metricsMocks";
import {
  installApp,
  jsonHandler,
  type ApiHandler,
  type Culture,
  type MockSession,
} from "./mockApi";

/**
 * The stateful API and update stream mock of the end-to-end journeys (Phase 69, VER-02).
 *
 * `installJourney` composes the strict `installApp` layer with handlers that answer from one mutable state: the
 * fleet, the lifecycle jobs of every server and the alerts. Writes change that state the way the control plane
 * does (a create adds an off server, a confirmed start or stop queues a job and moves the deployment, a mod pack
 * change marks the area as waiting for the next start, an automation save stores the rules), and `pushRunning` and
 * `pushStopped` move a server on and send the fleet-changed frame the page reacts to. Any request no handler names is
 * aborted and recorded by `installApp`, so every journey asserts `session.unexpected` is empty.
 *
 * This module imports Playwright and application types only; it never reaches a real backend.
 */

/** Ids of the servers the journeys use. */
export const journeyIds = {
  /** The server the create journey adds. */
  created: "10000000-0000-4000-8000-0000000000e1",
  /** The off server of the change journeys. */
  off: "10000000-0000-4000-8000-0000000000e2",
  /** The running server of the automation journey. */
  running: "10000000-0000-4000-8000-0000000000e4",
  /** The cost owner of every journey server. */
  owner: "40000000-0000-4000-8000-0000000000e0",
} as const;

/** The title the change and automation journeys give their server. */
export const journeyServerTitle = "Journey test server";

/** The id of the signed-in user the mock layer serves; the created server belongs to it. */
const userId = "10000000-0000-4000-8000-000000000001";

const bytesPerGiB = 1_073_741_824;

/** The instant every fixture counts from. */
const fixtureInstant = "2026-11-16T10:00:00Z";

/** The next run the control plane calculates for the Friday 18:00 rule of the automation journey (Europe/Berlin). */
const fridayEveningInstant = "2026-11-27T17:00:00Z";

/** The mutable state behind one installed journey. */
export interface JourneyState {
  /** The fleet; the detail, list and write handlers all read and change these objects. */
  servers: GameServer[];
  /** The lifecycle jobs of every server, newest first. */
  jobs: LifecycleJob[];
  /** The number of areas the servers hold back until their next start (Mods, Configuration, Credentials). */
  pendingChanges: number;
  /** The undismissed cost alerts and automatic shutdown alerts of the viewer. */
  alerts: CostAlert[];
}

/** One recorded write the page sent: the method, the path and the parsed JSON body (null when there is none). */
export interface JourneyRequest {
  /** The HTTP method. */
  method: string;
  /** The URL path. */
  path: string;
  /** The parsed JSON body, or null. */
  body: unknown;
}

/** What {@link installJourney} returns. */
export interface JourneyHandle {
  /** The record of unexpected requests of the strict mock layer. */
  session: MockSession;
  /** The mocked update stream. */
  stream: FleetStreamMock;
  /** The mutable state; replace a member and the next read answers it. */
  state: JourneyState;
  /** Every POST and PUT the page sent, in order. */
  requests: JourneyRequest[];
  /**
   * Moves a server to Running, finishes its start job and sends the fleet-changed frame.
   *
   * @param serverId The server to move on.
   */
  pushRunning(serverId: string): Promise<void>;
  /**
   * Moves a server to stopped (off, not costing), finishes its stop job and sends the fleet-changed frame.
   *
   * @param serverId The server to move on.
   */
  pushStopped(serverId: string): Promise<void>;
  /**
   * Sends the fleet-changed frame of a server without changing its state.
   *
   * @param serverId The server the frame names.
   */
  pushChanged(serverId: string): Promise<void>;
}

/** Options of {@link installJourney}. */
export interface InstallJourneyOptions {
  /** The culture stored before the first navigation. */
  culture: Culture;
  /** Whether the viewer may create servers. Defaults to true. */
  canCreate?: boolean | undefined;
  /** The servers the fleet starts with. Defaults to an empty fleet. */
  servers?: readonly GameServer[] | undefined;
}

/** The Start action of an off server. */
const startAction: ServerAction = {
  id: "start",
  label: "Start",
  description: "Provisions a cloud machine and starts the game server.",
  icon: "play",
  group: "Lifecycle",
  order: 10,
  enabled: true,
  unavailableReason: null,
  dangerous: false,
  confirmationText: null,
  confirmationChallenge: null,
  pending: false,
  expectedDurationSeconds: 300,
  costImpact: "StartsCost",
  costDescription: "Hourly provider cost starts once the machine exists.",
};

/** The cost-stop action of a running server. */
const stopAction: ServerAction = {
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

/** The mod packs of the change journeys: Core and Extended can be picked, Legacy cannot. */
const modPacks = [
  { primaryKey: 1, title: "Core", isAvailable: true, unavailableReason: null },
  { primaryKey: 2, title: "Extended", isAvailable: true, unavailableReason: null },
  {
    primaryKey: 3,
    title: "Legacy",
    isAvailable: false,
    unavailableReason: "Built for an older game version.",
  },
  { primaryKey: 7, title: "Operations", isAvailable: true, unavailableReason: null },
];

/** The flattened contents of each pack: Core holds A and B, Extended holds B and C. */
const packContents: Record<number, { key: string; title: string }[]> = {
  1: [
    { key: "mod-a", title: "Mod A" },
    { key: "mod-b", title: "Mod B" },
  ],
  2: [
    { key: "mod-b", title: "Mod B" },
    { key: "mod-c", title: "Mod C" },
  ],
  3: [],
  7: [{ key: "mod-ops", title: "Ops Mod" }],
};

/** An Arma-shaped capability served by one available provider with two priced sizes. */
const capability = {
  gameIdentifier: "arma3",
  displayName: "Arma 3",
  description: "Dedicated Arma operations.",
  runtimeProviders: ["fake-cloud"],
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
  runtimeProviderProfiles: [
    {
      identifier: "fake-cloud",
      availabilityStatus: "available",
      profiles: ["small", "medium"].map((identifier, index) => ({
        identifier,
        displayName: identifier === "small" ? "Small" : "Medium",
        description: `${identifier === "small" ? "Small" : "Medium"} compute profile.`,
        virtualCpuCount: 2 + index * 2,
        memoryGiB: 4 + index * 4,
        diskGiB: 80,
        includedStorageBytes: 80 * bytesPerGiB,
        usableStorageBytes: 70 * bytesPerGiB,
        estimatedHourlyCostEur: 0.05 + index * 0.05,
        estimatedMonthlyCostEur: 36.5 + index * 36.5,
      })),
      storage: {
        supportsExternalBlockStorage: true,
        minimumCapacityGiB: 20,
        maximumCapacityGiB: 500,
        capacityIncrementGiB: 10,
        estimatedHourlyCostPerGiBEur: 0.001,
        deploymentMountPath: "/srv/utf",
        readinessFailure: null,
      },
    },
  ],
};

/** A storage estimate in which both sizes are sufficient on their included disk. */
const storageDemandEstimate = {
  steadyStateRequiredBytes: 40 * bytesPerGiB,
  preparationWorkspaceBytes: 10 * bytesPerGiB,
  safetyMarginPercent: 10,
  safetyMarginBytes: 5 * bytesPerGiB,
  peakPreparationRequiredBytes: 55 * bytesPerGiB,
  minimumExternalCapacityGiB: 70,
  recommendedExternalUsableBytes: 62 * bytesPerGiB,
  status: "Estimated",
  calculatedAt: "2026-08-13T12:00:00Z",
  measurementTimestamp: null,
  missingComponents: [],
  components: [],
  runtimeProfiles: ["small", "medium"].map((identifier) => ({
    runtimeProviderIdentifier: "fake-cloud",
    runtimeProfileIdentifier: identifier,
    displayName: identifier === "small" ? "Small" : "Medium",
    includedCapacityBytes: 80 * bytesPerGiB,
    usableCapacityBytes: 70 * bytesPerGiB,
    isSufficient: true,
    externalStorageRequired: false,
  })),
};

/** The configuration read of a game without entries, enough for the Settings section to render. */
function emptyConfiguration(serverId: string) {
  return {
    serverId,
    profileId: null,
    profileRevisionId: null,
    profileRevisionNumber: null,
    isComplete: true,
    entries: [],
  };
}

/**
 * Builds the deployment record of a server in one status of its lifecycle.
 *
 * @param status The deployment status.
 * @param stage The raw stage token of the deployment.
 * @returns The deployment as the API sends it.
 */
function deployment(status: ServerDeployment["status"], stage: string): ServerDeployment {
  return {
    primaryKey: "50000000-0000-4000-8000-0000000000e5",
    runtimeProfileIdentifier: "small",
    runtimeProfileDisplayName: "Small",
    estimatedHourlyCostEur: 0.05,
    runtimeStorageStrategy: "IncludedDisk",
    peakStorageRequiredBytes: null,
    includedStorageBytes: 80 * bytesPerGiB,
    selectedExternalStorageBytes: null,
    deploymentRoot: "/srv/utf",
    estimatedComputeHourlyCostEur: 0.05,
    estimatedStorageHourlyCostEur: 0,
    status,
    stage,
    possibleCost: true,
    provisioningBlocked: false,
    costOwnerId: journeyIds.owner,
    costOwnerName: "Operations",
    workerId: null,
    workerStatus: status === "Running" ? "Online" : null,
    lastHeartbeat: status === "Running" ? fixtureInstant : null,
    diskTotalBytes: null,
    diskAvailableBytes: null,
    diskTimeStampObserved: null,
    publicAddress: null,
    connectionEndpoint: status === "Running" ? "203.0.113.5:2302" : null,
    connectionProtocol: status === "Running" ? "udp" : null,
    connectionPort: status === "Running" ? 2302 : null,
    administrativeSshCommand: null,
    resources: [],
    timeStampCreated: fixtureInstant,
    timeStampStarted: status === "Running" ? fixtureInstant : null,
    timeStampResolved: null,
    faultSummary: null,
  };
}

/** The members that identify one journey server. */
export interface JourneyServerSeed {
  /** The primary key. */
  id: string;
  /** The title the workspace heading shows. */
  title: string;
  /** The saved mod pack, or null for none. */
  modPackId?: number | null | undefined;
  /** True for a running, costing server with a Stop action; false for an off server with a Start action. */
  running?: boolean | undefined;
  /** The automation block of the detail read, or null when the API does not send one. */
  automation?: ServerAutomation | null | undefined;
}

/**
 * Builds one journey server: off with a Start action, or running with a Stop action.
 *
 * @param seed The id, title, saved mod pack, state and automation block.
 * @returns The server as the detail and list reads send it.
 */
export function journeyServer(seed: JourneyServerSeed): GameServer {
  const pack = modPacks.find((candidate) => candidate.primaryKey === seed.modPackId);
  const running = seed.running === true;
  return {
    primaryKey: seed.id,
    title: seed.title,
    gameIdentifier: "arma3",
    runtimeProviderIdentifier: "fake-cloud",
    runtimeProfileIdentifier: "small",
    runtimeProfileDisplayName: "Small",
    runtimeStorageStrategy: "IncludedDisk",
    externalStorageSizeGiB: null,
    estimatedHourlyCostEur: 0.05,
    estimatedMonthlyCostEur: 36.5,
    selectedModPackDefinitionId: seed.modPackId ?? null,
    selectedModPackDefinitionTitle: pack?.title ?? null,
    pinnedModPackRevisionId: null,
    selectedTerrainId: null,
    selectedTerrainTitle: null,
    pinnedTerrainVersionId: null,
    selectedScenarioId: null,
    selectedScenarioTitle: null,
    pinnedScenarioVersionId: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    pinnedConfigurationProfileRevisionId: null,
    createdById: userId,
    costOwnerId: journeyIds.owner,
    costOwnerName: "Operations",
    costOwnerUserId: null,
    isLaunchDisabled: false,
    costAlertThresholds: [],
    canManageCostAlerts: true,
    changesPendingStartAreas: [],
    hasChangesPendingStart: false,
    isArchived: false,
    canModify: true,
    canManageSecrets: false,
    canRevealSecrets: false,
    costOwnerIsFrozen: false,
    costOwnerAssignedAt: null,
    timeStampCreated: fixtureInstant,
    timeStampUpdated: fixtureInstant,
    deployment: running ? deployment("Running", "Ready") : null,
    pendingJobCount: 0,
    actions: [running ? stopAction : startAction],
    ...(seed.automation === undefined
      ? {}
      : { automation: seed.automation, canManageAutomation: seed.automation !== null }),
  };
}

/**
 * Builds one lifecycle job of the journeys.
 *
 * @param serverId The server the job belongs to.
 * @param kind The job kind, `Start` or `Stop`.
 * @param status The job status.
 * @param stage The raw stage token of the job.
 * @returns The job as the API sends it.
 */
function lifecycleJob(
  serverId: string,
  kind: "Start" | "Stop",
  status: LifecycleJob["status"],
  stage: string,
): LifecycleJob {
  const terminal = status === "Succeeded";
  return {
    primaryKey: `20000000-0000-4000-8000-${kind === "Start" ? "0000000000e6" : "0000000000e7"}`,
    gameServerId: serverId,
    deploymentId: null,
    kind,
    actionId: kind.toLowerCase(),
    status,
    stage,
    progressPercent: terminal ? 100 : 20,
    attemptCount: 1,
    failureCode: null,
    failureReason: null,
    requestedByName: "Operator",
    trigger: "Manual",
    triggerLimit: null,
    canRetry: false,
    canSkip: false,
    timeStampCreated: fixtureInstant,
    timeStampUpdated: fixtureInstant,
    timeStampStarted: fixtureInstant,
    timeStampCompleted: terminal ? fixtureInstant : null,
    commands: [],
  };
}

/** Counts the areas a server holds back until its next start. */
function pendingAreaCount(servers: readonly GameServer[]): number {
  return servers.reduce((sum, server) => sum + server.changesPendingStartAreas.length, 0);
}

/**
 * Applies an automation save the way the control plane does: rules get ids, the version moves on, a start rule with
 * the consent flag turns the consent state to given and the first rule becomes the next scheduled action.
 *
 * @param current The automation block before the save.
 * @param body The request body.
 * @returns The saved automation block.
 */
function applyAutomationSave(
  current: ServerAutomation,
  body: AutomationSaveBody,
): ServerAutomation {
  const schedules = body.schedules.map((rule, index) => ({
    id: rule.id ?? `a0000000-0000-4000-8000-${String(index + 1).padStart(12, "0")}`,
    action: rule.action as "Start" | "Stop",
    days: [...new Set(rule.days)].sort((left, right) => left - right),
    time: rule.time,
    nextRunAt: index === 0 ? fridayEveningInstant : null,
    lastRun: null,
  }));
  const hasStart = schedules.some((rule) => rule.action === "Start");
  const first = schedules[0];
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
    status: {
      ...current.status,
      nextScheduled: first ? { action: first.action, at: fridayEveningInstant } : null,
    },
    startConsent: {
      state: !hasStart
        ? "NotRequired"
        : body.startConsent || current.startConsent.state === "Given"
          ? "Given"
          : "Needed",
    },
  };
}

/**
 * Waits until the page opened an update stream connection, which is when a frame can be delivered to it.
 *
 * @param stream The mocked stream.
 */
async function waitForConnection(stream: FleetStreamMock): Promise<void> {
  const deadline = Date.now() + 15_000;
  while (stream.connectionCount() === 0) {
    if (Date.now() > deadline) throw new Error("The page never opened the update stream.");
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
}

/**
 * Installs the stateful journey mock on a page; call it before the first navigation.
 *
 * @param page The page to mock.
 * @param options The culture, the viewer's right to create servers and the starting fleet.
 * @returns The strict-layer record, the stream, the mutable state, the recorded writes and the push helpers.
 */
export async function installJourney(
  page: Page,
  options: InstallJourneyOptions,
): Promise<JourneyHandle> {
  const state: JourneyState = {
    servers: options.servers ? structuredClone([...options.servers]) : [],
    jobs: [],
    pendingChanges: 0,
    alerts: [],
  };
  const requests: JourneyRequest[] = [];

  const find = (serverId: string): GameServer | undefined =>
    state.servers.find((candidate) => candidate.primaryKey === serverId);
  const replace = (serverId: string, change: (server: GameServer) => GameServer): void => {
    state.servers = state.servers.map((server) =>
      server.primaryKey === serverId ? change(server) : server,
    );
    state.pendingChanges = pendingAreaCount(state.servers);
  };
  const jobsOf = (serverId: string): LifecycleJob[] =>
    state.jobs.filter((job) => job.gameServerId === serverId);

  const handlers: ApiHandler[] = [
    jsonHandler("GET", "/api/game-servers", () => state.servers),
    jsonHandler("GET", "/api/game-servers/capabilities", [capability]),
    jsonHandler("GET", "/api/game-servers/creation/mod-pack-options", [
      { primaryKey: 7, title: "Operations" },
    ]),
    jsonHandler("GET", "/api/game-servers/creation/cost-owner-options", [
      {
        costOwnerId: journeyIds.owner,
        displayName: "Personal budget",
        isFrozen: false,
        isCurrentUser: true,
      },
    ]),
    jsonHandler("GET", "/api/configuration-profiles/options", []),
    jsonHandler("POST", "/api/game-servers/storage-demand-estimate", storageDemandEstimate),
    jsonHandler("GET", "/api/cost-alerts", () => state.alerts),
    async (route, method, path) => {
      if (method !== "POST" || path !== "/api/game-servers") return false;
      const body = route.request().postDataJSON() as {
        title: string;
        modPackDefinitionId?: number | null;
      };
      requests.push({ method, path, body });
      const created = journeyServer({
        id: journeyIds.created,
        title: body.title,
        modPackId: body.modPackDefinitionId ?? null,
      });
      state.servers = [...state.servers, created];
      await route.fulfill({ json: created });
      return true;
    },
    async (route, method, path) => {
      const match = /^\/api\/game-servers\/([0-9a-f-]{36})(\/.*)?$/i.exec(path);
      if (!match) return false;
      const serverId = match[1] ?? "";
      const rest = match[2] ?? "";
      const server = find(serverId);
      if (!server) return false;

      if (method === "GET") {
        if (rest === "") {
          await route.fulfill({ json: server });
          return true;
        }
        if (rest === "/jobs") {
          await route.fulfill({ json: jobsOf(serverId) });
          return true;
        }
        if (rest === "/mod-pack-options") {
          await route.fulfill({ json: modPacks });
          return true;
        }
        const contents = /^\/mod-pack-options\/(\d+)\/contents$/.exec(rest);
        if (contents) {
          const packId = Number(contents[1]);
          const items = packContents[packId] ?? [];
          await route.fulfill({
            json: {
              modPackDefinitionId: packId,
              isAvailable: items.length > 0,
              unavailableReason: null,
              total: items.length,
              items,
            },
          });
          return true;
        }
        if (rest === "/configuration") {
          await route.fulfill({ json: emptyConfiguration(serverId) });
          return true;
        }
        if (rest === "/storage-demand") {
          await route.fulfill({
            json: {
              placementEstimate: null,
              components: [],
              runtimeProfiles: [],
              missingComponents: [],
            },
          });
          return true;
        }
        if (
          [
            "/operations",
            "/secrets",
            "/configuration/profile-options",
            "/preservation-selections",
            "/files/persistent",
            "/files/live",
          ].includes(rest)
        ) {
          await route.fulfill({ json: [] });
          return true;
        }
        return fulfillMetricsRoute(route, method, path, serverId);
      }

      const body: unknown = route.request().postData() ? route.request().postDataJSON() : null;
      if (method === "POST" && /^\/actions\/(start|stop)$/.test(rest)) {
        const actionId = rest.endsWith("start") ? "start" : "stop";
        requests.push({ method, path, body });
        const starting = actionId === "start";
        const job = lifecycleJob(
          serverId,
          starting ? "Start" : "Stop",
          "Running",
          starting ? "provider-creating" : "provider-deleting",
        );
        state.jobs = [
          job,
          ...state.jobs.filter((existing) => existing.primaryKey !== job.primaryKey),
        ];
        replace(serverId, (current) => ({
          ...current,
          pendingJobCount: 1,
          deployment: starting
            ? deployment("Provisioning", "provider-creating")
            : deployment("Stopping", "provider-deleting"),
          actions: [
            {
              ...(starting ? startAction : stopAction),
              enabled: false,
              pending: true,
            },
          ],
        }));
        await route.fulfill({ json: job });
        return true;
      }
      if (method === "PUT" && rest === "/mod-pack") {
        requests.push({ method, path, body });
        const packId = (body as { modPackDefinitionId: number }).modPackDefinitionId;
        const pack = modPacks.find((candidate) => candidate.primaryKey === packId);
        replace(serverId, (current) => ({
          ...current,
          selectedModPackDefinitionId: packId,
          selectedModPackDefinitionTitle: pack?.title ?? null,
          changesPendingStartAreas: ["ModPack"],
          hasChangesPendingStart: true,
        }));
        await route.fulfill({ status: 204 });
        return true;
      }
      if (method === "PUT" && rest === "/automation") {
        requests.push({ method, path, body });
        const current = server.automation;
        if (!current) return false;
        const saved = applyAutomationSave(current, body as AutomationSaveBody);
        replace(serverId, (existing) => ({ ...existing, automation: saved }));
        await route.fulfill({ json: saved });
        return true;
      }
      return false;
    },
  ];

  const session = await installApp(page, {
    culture: options.culture,
    claims: options.canCreate === false ? ["sv"] : ["crt:sv"],
    handlers,
    stream: true,
  });
  const stream = session.stream;
  if (!stream) throw new Error("installApp did not mock the update stream.");

  const pushChanged = async (serverId: string): Promise<void> => {
    await waitForConnection(stream);
    stream.send(fleetChangedFrame(serverId));
  };

  return {
    session,
    stream,
    state,
    requests,
    pushChanged,
    async pushRunning(serverId) {
      state.jobs = state.jobs.map((job) =>
        job.gameServerId === serverId && job.kind === "Start"
          ? { ...job, status: "Succeeded", stage: "completed", progressPercent: 100 }
          : job,
      );
      replace(serverId, (current) => ({
        ...current,
        pendingJobCount: 0,
        deployment: deployment("Running", "Ready"),
        actions: [stopAction],
      }));
      await pushChanged(serverId);
    },
    async pushStopped(serverId) {
      state.jobs = state.jobs.map((job) =>
        job.gameServerId === serverId && job.kind === "Stop"
          ? { ...job, status: "Succeeded", stage: "completed", progressPercent: 100 }
          : job,
      );
      replace(serverId, (current) => ({
        ...current,
        pendingJobCount: 0,
        deployment: null,
        actions: [startAction],
      }));
      await pushChanged(serverId);
    },
  };
}
