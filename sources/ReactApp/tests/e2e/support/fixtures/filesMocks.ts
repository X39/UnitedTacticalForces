import type { Route } from "@playwright/test";
import { createCapability, createStorageDemand } from "../../../helpers/createFixtures";
import { jsonHandler, type ApiHandler } from "../mockApi";
import { fulfillMetricsRoute } from "../metricsMocks";

/**
 * Handler factories for the server workspace screens that read files and the machine: the Files tab (persistent and
 * live listings, preservation selections, upload and folder endpoints) and the Machine tab (a running deployment
 * with disk telemetry, resources and the administrative diagnostics).
 *
 * Every body is synthetic: neutral folder names, documentation-range addresses and fixed instants, so no screenshot or
 * German walk depends on real data (T-69-34). The handlers answer the reads the workspace shell sends next to the
 * tab's own reads, so a registry entry that installs them leaves nothing for the strict layer to abort.
 */

/** The id of the server every files and machine entry opens. */
export const filesServerId = "10000000-0000-4000-8000-000000000f28";

/** The title of that server; a proper noun, so the German walk never reads it as English. */
export const filesServerTitle = "Orion-7";

/** The base path of every request about the server. */
const base = `/api/game-servers/${filesServerId}`;

/** Options of {@link filesHandlers}. */
export interface FilesMockOptions {
  /**
   * Whether the server has a running deployment, which shows the machine facts. When false the Machine tab shows the
   * no-deployment text. Defaults to true.
   */
  deployment?: boolean | undefined;
  /** Whether the viewer may modify the server, which shows the upload, new folder, delete and preservation controls. */
  canModify?: boolean | undefined;
}

/**
 * Builds one file listing entry; every field the API sends is present.
 *
 * @param path The server-relative path.
 * @param isDirectory Whether the entry is a folder.
 * @param overrides Fields that replace the defaults.
 * @returns The listing entry.
 */
function fileEntry(path: string, isDirectory: boolean, overrides: Record<string, unknown> = {}) {
  return {
    path,
    name: path.split("/").at(-1) ?? path,
    isDirectory,
    size: isDirectory ? null : 2048,
    modifiedAt: "2026-08-13T09:00:00Z",
    context: "Persistent",
    classification: isDirectory ? "Unknown" : "Configuration",
    canModify: true,
    origin: "Persistent",
    modificationMode: "RawFile",
    isGeneratedTarget: false,
    overridesGenerated: false,
    configurationStatus: "NotApplicable",
    ...overrides,
  };
}

/**
 * The persistent listings by folder path. The top level holds two folders and a file, `profiles` holds a folder, a
 * configuration file the upload dialog collides with and a generated target, and `profiles/child` is empty. Every
 * other path answers 404, like a folder that does not exist.
 */
const persistentListings: Record<string, ReturnType<typeof fileEntry>[]> = {
  "": [
    fileEntry("profiles", true),
    fileEntry("keys", true),
    fileEntry("basic.cfg", false, { size: 512 }),
  ],
  profiles: [
    fileEntry("profiles/child", true),
    fileEntry("profiles/server.cfg", false, { size: 12_288 }),
    fileEntry("profiles/launch.cfg", false, {
      size: 640,
      classification: "Generated",
      canModify: false,
      origin: "Generated",
      modificationMode: "GeneratedOnly",
      isGeneratedTarget: true,
      configurationStatus: "Complete",
    }),
  ],
  "profiles/child": [],
  keys: [fileEntry("keys/server.bikey", false, { size: 576, classification: "Mod" })],
};

/** The live listings by folder path; the live files of a running deployment cannot be changed by the viewer. */
const liveListings: Record<string, ReturnType<typeof fileEntry>[]> = {
  "": [
    fileEntry("logs", true, { context: "Live", canModify: false, origin: "Live" }),
    fileEntry("server.rpt", false, {
      context: "Live",
      canModify: false,
      origin: "Live",
      classification: "Log",
      size: 40_960,
    }),
  ],
  logs: [],
};

/**
 * Builds the server projection of the workspace: an Arma 3 server whose viewer may modify it.
 *
 * @param options Whether a running deployment exists and whether the viewer may modify the server.
 * @returns The server body in the shape of `GET /api/game-servers/{id}`.
 */
function serverBody(options: FilesMockOptions) {
  const hasDeployment = options.deployment ?? true;
  return {
    primaryKey: filesServerId,
    title: filesServerTitle,
    gameIdentifier: "arma3",
    runtimeProviderIdentifier: "fake-cloud",
    runtimeProfileIdentifier: "medium",
    runtimeProfileDisplayName: "Medium",
    runtimeStorageStrategy: "IncludedDisk",
    externalStorageSizeGiB: null,
    estimatedHourlyCostEur: 0.2,
    estimatedMonthlyCostEur: 146,
    selectedModPackDefinitionId: null,
    selectedModPackDefinitionTitle: null,
    pinnedModPackRevisionId: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    pinnedConfigurationProfileRevisionId: null,
    costOwnerId: "c0000000-0000-4000-8000-000000000001",
    costOwnerName: "Fonds Nord",
    createdById: "10000000-0000-4000-8000-000000000001",
    costOwnerUserId: null,
    isLaunchDisabled: false,
    changesPendingStartAreas: [],
    hasChangesPendingStart: false,
    isArchived: false,
    canModify: options.canModify ?? true,
    canManageSecrets: true,
    canRevealSecrets: false,
    costAlertThresholds: null,
    canManageCostAlerts: null,
    automation: null,
    canManageAutomation: null,
    timeStampCreated: "2026-11-16T10:00:00Z",
    timeStampUpdated: "2026-11-16T10:00:00Z",
    deployment: hasDeployment
      ? {
          primaryKey: "d0000000-0000-4000-8000-000000000f28",
          runtimeProfileIdentifier: "medium",
          runtimeProfileDisplayName: "Medium",
          estimatedHourlyCostEur: 0.2,
          runtimeStorageStrategy: "IncludedDisk",
          peakStorageRequiredBytes: 53_687_091_200,
          includedStorageBytes: 85_899_345_920,
          selectedExternalStorageBytes: null,
          deploymentRoot: "/srv/utf",
          estimatedComputeHourlyCostEur: 0.2,
          estimatedStorageHourlyCostEur: 0,
          status: "Running",
          stage: "Ready",
          possibleCost: true,
          provisioningBlocked: false,
          costOwnerId: "c0000000-0000-4000-8000-000000000001",
          costOwnerName: "Fonds Nord",
          workerId: "70000000-0000-4000-8000-000000000f28",
          workerStatus: "Online",
          lastHeartbeat: "2026-11-16T10:00:00Z",
          diskTotalBytes: 85_899_345_920,
          diskAvailableBytes: 42_949_672_960,
          diskTimeStampObserved: "2026-11-16T09:59:00Z",
          publicAddress: "203.0.113.5",
          connectionEndpoint: "203.0.113.5:2302",
          connectionProtocol: "udp",
          connectionPort: 2302,
          administrativeSshCommand: "ssh -i key root@203.0.113.5",
          resources: [
            {
              primaryKey: "e0000000-0000-4000-8000-000000000001",
              providerIdentifier: "fake-cloud",
              providerResourceIdentifier: "srv-4711",
              kind: "Machine",
              state: "Allocated",
              possibleCost: true,
              provisionedCapacityBytes: 85_899_345_920,
              estimatedHourlyCostEur: 0.2,
              providerState: "running",
              publicAddress: "203.0.113.5",
              timeStampObserved: "2026-11-16T09:59:00Z",
              timeStampReleased: null,
            },
          ],
          timeStampCreated: "2026-11-16T09:00:00Z",
          timeStampStarted: "2026-11-16T09:05:00Z",
          timeStampResolved: null,
          faultSummary: null,
        }
      : null,
    pendingJobCount: 0,
    actions: hasDeployment
      ? [
          {
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
          },
        ]
      : [],
  };
}

/**
 * Wraps the shared metrics route helper as a handler for the server of this module.
 *
 * @returns The handler that answers the current readings and the history of the server.
 */
function metricsHandler(): ApiHandler {
  return (route: Route, method: string, path: string) =>
    fulfillMetricsRoute(route, method, path, filesServerId);
}

/**
 * Builds the handler of the file listing endpoints, `GET .../files/persistent` and `GET .../files/live`.
 *
 * @returns A handler answering the listing of a known folder and 404 for an unknown one.
 */
function listingHandler(): ApiHandler {
  return async (route, method, path, url) => {
    const match = new RegExp(`^${base}/files/(persistent|live)$`).exec(path);
    if (method !== "GET" || match === null) return false;
    const folder = url.searchParams.get("path") ?? "";
    const listings = match[1] === "persistent" ? persistentListings : liveListings;
    const listing = listings[folder];
    if (listing !== undefined) await route.fulfill({ json: listing });
    else {
      await route.fulfill({
        status: 404,
        json: { title: "Not Found", detail: "The requested folder does not exist." },
      });
    }
    return true;
  };
}

/** The preservation selections of the server: one the member added and one the game seeded. */
const preservationSelections = [
  {
    primaryKey: "a0000000-0000-4000-8000-000000000001",
    path: "profiles",
    scope: "RecursiveFolder",
    mode: "CreateOrUpdate",
    seededByGame: false,
  },
  {
    primaryKey: "a0000000-0000-4000-8000-000000000002",
    path: "keys",
    scope: "FlatFolder",
    mode: "MirrorDeletions",
    seededByGame: true,
  },
];

/**
 * Builds the handlers of the server workspace screens that show files or the machine.
 *
 * Reads answer from fixed data. The handlers cover the server detail and list, the game capabilities, the storage
 * demand, the empty job, operation, mod pack and profile lists, the metrics and the file and preservation endpoints.
 * The write endpoints are not answered: no registry state submits them, so a request to one is a recorded failure.
 *
 * @param options Whether a running deployment exists and whether the viewer may modify the server.
 * @returns The handlers in the order the strict layer tries them.
 */
export function filesHandlers(options: FilesMockOptions = {}): ApiHandler[] {
  return [
    jsonHandler("GET", base, () => serverBody(options)),
    jsonHandler("GET", "/api/game-servers", () => [serverBody(options)]),
    jsonHandler("GET", "/api/game-servers/capabilities", () => [
      createCapability({ runtimeProviders: ["fake-cloud"] }),
    ]),
    listingHandler(),
    jsonHandler("GET", `${base}/preservation-selections`, () => preservationSelections),
    jsonHandler("GET", `${base}/storage-demand`, () => createStorageDemand()),
    jsonHandler("GET", `${base}/configuration`, {
      serverId: filesServerId,
      profileId: null,
      profileRevisionId: null,
      profileRevisionNumber: null,
      isComplete: true,
      entries: [],
    }),
    jsonHandler("GET", `${base}/secrets`, []),
    jsonHandler("GET", `${base}/jobs`, []),
    jsonHandler("GET", `${base}/operations`, []),
    jsonHandler("GET", `${base}/mod-pack-options`, []),
    jsonHandler("GET", `${base}/configuration/profile-options`, []),
    metricsHandler(),
  ];
}
