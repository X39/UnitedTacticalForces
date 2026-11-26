import type { Page } from "@playwright/test";
import { fleetChangedFrame } from "../fleetStream";
import { fleetAction, fleetServer } from "../../../helpers/fleetFixtures";
import { fulfillMetricsRoute } from "../metricsMocks";
import {
  auditUserId,
  installApp,
  jsonHandler,
  type ApiHandler,
  type Culture,
  type MockSession,
} from "../mockApi";
import type { ScreenEntry } from "./registryTypes";

/**
 * The fleet slice of the screen registry (plan 69-26 completes it).
 *
 * Importing only types, the strict mock layer and the type-only fixture builders keeps this module loadable by the
 * Vitest completeness guard. Readiness checks wait on page structure (classes, roles, data attributes), never on
 * translated text, so one entry serves both cultures.
 */

/**
 * Raises the timeout of the running test for an entry that visits several states on one page load.
 *
 * Every state repeats the full desktop audit (keyboard walk, zoom, reduced motion) and some states wait for a delayed
 * notice, so an entry with up to six states can exceed the 30 second default of a single test while it is healthy.
 * The Playwright runtime is imported on demand because this module is also loaded by the Vitest completeness guard,
 * which never runs a setup.
 */
async function allowLongAudit(): Promise<void> {
  const { test } = await import("@playwright/test");
  test.setTimeout(120_000);
}

/** The reason of the unconfirmed server's disabled stop action. */
const notRunningReason = "The deployment is not running.";

/** The exact API reason of an action blocked because the platform operator paused lifecycle execution. */
const pausedReason = "Hosting lifecycle execution is paused by the operator.";

/** Claim identifiers of a viewer who may see every server and create new ones. */
const creatorClaims: readonly string[] = ["sv", "crt:sv"];

/** Claim identifiers of a viewer who may see servers shared with them but may not create any. */
const memberClaims: readonly string[] = ["sv"];

/**
 * Builds the populated fleet: one server in each of the running, off, starting, attention and unconfirmed states.
 *
 * Titles are German-neutral on purpose, and the running server is owned by the signed-in user so the Mine filter
 * has a result.
 *
 * @returns Fresh server objects in the shape of `GET /api/game-servers`.
 */
function populatedFleet(pendingJobs = 1) {
  return [
    fleetServer({
      index: 1,
      title: "Alpha Ops",
      status: "Running",
      costOwnerUserId: auditUserId,
      actions: [fleetAction("stop")],
    }),
    fleetServer({ index: 2, title: "Bravo Training", actions: [fleetAction("start")] }),
    fleetServer({
      index: 3,
      title: "Charlie Reserve",
      status: "Provisioning",
      pendingJobCount: pendingJobs,
      actions: [fleetAction("stop", { enabled: false, unavailableReason: notRunningReason })],
    }),
    fleetServer({
      index: 4,
      title: "Delta Range",
      status: "Failed",
      actions: [fleetAction("stop", { enabled: false, unavailableReason: notRunningReason })],
    }),
    fleetServer({
      index: 5,
      title: "Echo Depot",
      status: "Stopped",
      possibleCost: true,
      actions: [fleetAction("stop", { enabled: false, unavailableReason: notRunningReason })],
    }),
  ];
}

/**
 * Builds the signed-in user body of `GET /api/users/me` for a claim list.
 *
 * @param claims The claim identifiers, each served with a null value.
 * @returns The user body.
 */
function viewer(claims: readonly string[]) {
  return {
    primaryKey: auditUserId,
    nickname: "Operator",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims: claims.map((identifier) => ({ identifier, value: null })),
    roles: [],
  };
}

/** The mutable answers of one fleet entry; states change them and then make the page read them again. */
interface FleetControl {
  /** The servers `GET /api/game-servers` returns. */
  servers: ReturnType<typeof populatedFleet>;
  /** When true the fleet read fails with a 404, which the query does not retry. */
  failFleet: boolean;
  /** The claim identifiers of the signed-in viewer; they take effect on the next page load. */
  claims: readonly string[];
  /** The cost notifications `GET /api/game-servers/notifications` returns. */
  notifications: readonly unknown[];
}

/**
 * Builds the handlers that serve the mutable fleet, the viewer and the notifications.
 *
 * @param control The mutable answers.
 * @returns The handler chain; install it first so it overrides the baseline reads.
 */
function fleetHandlers(control: FleetControl): ApiHandler[] {
  return [
    jsonHandler("GET", "/api/users/me", () => viewer(control.claims)),
    jsonHandler("GET", "/api/game-servers/notifications", () => control.notifications),
    async (route, method, path) => {
      if (method !== "GET" || path !== "/api/game-servers") return false;
      if (control.failFleet) {
        await route.fulfill({ status: 404, json: { title: "Not found" } });
      } else {
        await route.fulfill({ json: control.servers });
      }
      return true;
    },
  ];
}

/**
 * Installs the strict mock of a fleet screen and returns its record together with the controls states use.
 *
 * @param page The page to mock.
 * @param culture The culture of the session.
 * @param overrides Initial values that replace the defaults of the control.
 * @returns The mock session and the live control object.
 */
async function installFleet(
  page: Page,
  culture: Culture,
  overrides: Partial<FleetControl> = {},
): Promise<{ session: MockSession; control: FleetControl }> {
  const control: FleetControl = {
    servers: populatedFleet(),
    failFleet: false,
    claims: creatorClaims,
    notifications: [],
    ...overrides,
  };
  await allowLongAudit();
  const session = await installApp(page, {
    culture,
    stream: true,
    handlers: fleetHandlers(control),
  });
  return { session, control };
}

/**
 * Resolves once the populated fleet rendered: the page heading and all five server cards.
 *
 * @param page The page under audit.
 */
async function populatedReady(page: Page): Promise<void> {
  await page.locator(".next-hosting-page h1").waitFor();
  await page.locator("[data-server-card]").nth(4).waitFor();
}

/**
 * Asks the page to read the fleet again through the live fleet-changed frame, which is how the app refreshes it.
 *
 * @param session The mock session that owns the update stream.
 */
function refreshFleet(session: MockSession): void {
  session.stream?.send(fleetChangedFrame("f1ee7000-0000-4000-8000-000000000001"));
}

/** One GiB in bytes, for the storage figures of the create fixtures. */
const bytesPerGiB = 1_073_741_824;

/** The id of the server the create endpoint answers with. */
const createdServerId = "10000000-0000-4000-8000-0000000000c2";

/** The own cost owner of the create fixtures. */
const personalOwnerId = "10000000-0000-4000-8000-0000000000c3";

/** The shared cost owner of the create fixtures. */
const sharedOwnerId = "10000000-0000-4000-8000-0000000000c4";

/** An Arma-shaped capability served by one available provider with two priced sizes and external storage. */
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
      profiles: [
        {
          identifier: "small",
          displayName: "Small",
          description: "Small compute profile.",
          virtualCpuCount: 2,
          memoryGiB: 4,
          diskGiB: 80,
          includedStorageBytes: 80 * bytesPerGiB,
          usableStorageBytes: 70 * bytesPerGiB,
          estimatedHourlyCostEur: 0.05,
          estimatedMonthlyCostEur: 36.5,
        },
        {
          identifier: "medium",
          displayName: "Medium",
          description: "Medium compute profile.",
          virtualCpuCount: 4,
          memoryGiB: 8,
          diskGiB: 80,
          includedStorageBytes: 80 * bytesPerGiB,
          usableStorageBytes: 70 * bytesPerGiB,
          estimatedHourlyCostEur: 0.1,
          estimatedMonthlyCostEur: 73,
        },
      ],
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
const storageDemand = {
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

/** The mod pack choices of the create page. */
const modPackOptions = [{ primaryKey: 7, title: "Operations" }];

/** The configuration profile choices of the create page. */
const profileOptions = [
  {
    primaryKey: "10000000-0000-4000-8000-0000000000c5",
    title: "Public operation",
    gameIdentifier: "arma3",
    isArchived: false,
    latestRevisionNumber: 3,
  },
];

/**
 * Builds the cost owners of the viewer: their own, optionally frozen, and a shared one.
 *
 * @param frozenSelf Whether the viewer's own cost owner is frozen, which blocks creation.
 * @returns The choices in the shape of the creation cost owner options.
 */
function costOwners(frozenSelf: boolean) {
  return [
    {
      costOwnerId: personalOwnerId,
      displayName: "Alpha Budget",
      isFrozen: frozenSelf,
      isCurrentUser: true,
    },
    {
      costOwnerId: sharedOwnerId,
      displayName: "Bravo Budget",
      isFrozen: false,
      isCurrentUser: false,
    },
  ];
}

/**
 * Builds the full read of a server as `GET /api/game-servers/{id}` returns it: the fleet shape plus the detail members.
 *
 * @param title The server title.
 * @param actions The actions of the server.
 * @returns The server body of an off server without a deployment.
 */
function detailServer(title: string, actions: ReturnType<typeof fleetAction>[]) {
  const server = fleetServer({ index: 7, title, actions });
  return {
    ...server,
    primaryKey: createdServerId,
    costOwnerId: personalOwnerId,
    costOwnerName: "Alpha Budget",
    createdById: auditUserId,
    runtimeProfileIdentifier: "small",
    runtimeProfileDisplayName: "Small",
    runtimeStorageStrategy: "IncludedDisk",
    externalStorageSizeGiB: null,
    estimatedHourlyCostEur: 0.05,
    estimatedMonthlyCostEur: 36.5,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    canModify: true,
    timeStampCreated: "2026-11-16T10:00:00Z",
  };
}

/** The mutable answers of the create screens. */
interface CreateControl {
  /** Whether the viewer's own cost owner is frozen. */
  frozenSelf: boolean;
  /** The actions of the server the create endpoint and the workspace answer with. */
  actions: ReturnType<typeof fleetAction>[];
}

/**
 * Builds the handlers of the create page and of the workspace it opens after a create.
 *
 * @param control The mutable answers.
 * @returns The handler chain, in the order the mock tries them.
 */
function createHandlers(control: CreateControl): ApiHandler[] {
  const created = () => detailServer("Juno Ops", control.actions);
  return [
    jsonHandler("GET", "/api/users/me", () => viewer(creatorClaims)),
    jsonHandler("GET", "/api/game-servers/capabilities", [capability]),
    jsonHandler("GET", "/api/game-servers/creation/mod-pack-options", modPackOptions),
    jsonHandler("GET", "/api/configuration-profiles/options", profileOptions),
    jsonHandler("GET", "/api/game-servers/creation/cost-owner-options", () =>
      costOwners(control.frozenSelf),
    ),
    jsonHandler("POST", "/api/game-servers/storage-demand-estimate", storageDemand),
    jsonHandler("POST", "/api/game-servers", created),
    jsonHandler("GET", `/api/game-servers/${createdServerId}`, created),
    jsonHandler("GET", "/api/game-servers", () => [created()]),
    jsonHandler("GET", `/api/game-servers/${createdServerId}/jobs`, []),
    jsonHandler("GET", `/api/game-servers/${createdServerId}/operations`, []),
    (route, method, path) => fulfillMetricsRoute(route, method, path, createdServerId),
  ];
}

/**
 * Installs the strict mock of the create screens.
 *
 * @param page The page to mock.
 * @param culture The culture of the session.
 * @param overrides Initial values that replace the defaults of the control.
 * @returns The mock session and the live control object.
 */
async function installCreate(
  page: Page,
  culture: Culture,
  overrides: Partial<CreateControl> = {},
): Promise<{ session: MockSession; control: CreateControl }> {
  const control: CreateControl = {
    frozenSelf: false,
    actions: [fleetAction("start")],
    ...overrides,
  };
  await allowLongAudit();
  const session = await installApp(page, {
    culture,
    stream: true,
    handlers: createHandlers(control),
  });
  return { session, control };
}

/**
 * Resolves once the guided create page rendered with a known hourly cost, which is the default view.
 *
 * @param page The page under audit.
 */
async function createReady(page: Page): Promise<void> {
  await page.locator("main h1").waitFor();
  await page.locator("form.create-page input[name=title]").waitFor();
  await page.waitForFunction(
    () => (document.querySelector(".create-summary__hourly")?.textContent ?? "").trim() !== "",
  );
}

/** Selector of the Advanced options toggle button. */
const advancedToggle = ".create-advanced > .disclosure__summary";

/** The id of the running server of the overlay fixtures: the first server of the fleet fixtures. */
const runningServerId = "f1ee7000-0000-4000-8000-000000000001";

/** The id of the off server of the overlay fixtures. */
const offServerId = "f1ee7000-0000-4000-8000-000000000002";

/** The exact API reason of a viewer who lacks the Server Operate permission. */
const operateReason = "Server Operate permission is required.";

/**
 * Builds the two servers of the overlay fixtures: a running server whose stop and terminate actions need a
 * confirmation (terminate carries a typed challenge) and an off server with an enabled start action.
 *
 * Description and confirmation texts are English API prose, which the pages render inside `lang="en"` elements.
 *
 * @returns Fresh server objects in the shape of `GET /api/game-servers`.
 */
function overlayFleet() {
  return [
    fleetServer({
      index: 1,
      title: "Alpha Ops",
      status: "Running",
      costOwnerUserId: auditUserId,
      actions: [
        fleetAction("stop", {
          description: "Saves the game state and stops the server.",
          confirmationText: "Stopping releases the cloud machine after the data is saved.",
          expectedDurationSeconds: 120,
          costDescription: "Provider cost stops after verified cleanup.",
        }),
        fleetAction("terminate", {
          description: "Immediately releases every provider resource.",
          group: "Emergency",
          order: 1000,
          dangerous: true,
          confirmationText: "Release every provider resource now.",
          confirmationChallenge: "Alpha Ops",
          expectedDurationSeconds: 120,
          costDescription: "Provider cost stops after verified cleanup.",
        }),
      ],
    }),
    fleetServer({
      index: 2,
      title: "Bravo Training",
      actions: [
        fleetAction("start", {
          description: "Provisions a cloud machine and starts the game server.",
          expectedDurationSeconds: 300,
          costDescription: "Hourly provider cost starts once the machine exists.",
        }),
      ],
    }),
  ];
}

/**
 * Adds the members only the detail read carries to a fleet server.
 *
 * @param server A server of the overlay fixtures.
 * @returns The body of `GET /api/game-servers/{id}`.
 */
function withDetail(server: ReturnType<typeof overlayFleet>[number]) {
  return {
    ...server,
    runtimeProfileIdentifier: "medium",
    runtimeProfileDisplayName: "Medium",
    runtimeStorageStrategy: "IncludedDisk",
    externalStorageSizeGiB: null,
    estimatedHourlyCostEur: 0.2,
    estimatedMonthlyCostEur: 146,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    canModify: true,
    timeStampCreated: "2026-11-16T10:00:00Z",
    deployment:
      server.deployment === null
        ? null
        : {
            ...server.deployment,
            stage: "Ready",
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
          },
  };
}

/** The accepted lifecycle job the API answers a confirmed action with. */
const queuedJob = {
  primaryKey: "20000000-0000-4000-8000-0000000000f1",
  gameServerId: runningServerId,
  actionId: "start",
  kind: "Start",
  status: "Queued",
  stage: "queued",
  progressPercent: 0,
  attemptCount: 0,
  canRetry: false,
  canSkip: false,
  failureCode: null,
  failureReason: null,
  timeStampCreated: "2026-11-16T10:00:00Z",
  timeStampUpdated: "2026-11-16T10:00:00Z",
  commands: [],
};

/** The mutable answers of the overlay screens. */
interface OverlayControl {
  /** The servers the fleet and the detail reads return. */
  servers: ReturnType<typeof overlayFleet>;
  /** The cost notifications of the profile menu. */
  notifications: readonly unknown[];
  /** The undismissed cost alerts of the profile menu. */
  alerts: readonly unknown[];
  /** Every action request the mock accepted, as `POST <path>`; nothing may be listed before a confirmation. */
  actionRequests: string[];
}

/**
 * Builds the handlers of the overlay screens: the fleet, the detail read of each server, the quiet workspace reads and
 * the accepted action request.
 *
 * @param control The mutable answers.
 * @returns The handler chain, in the order the mock tries them.
 */
function overlayHandlers(control: OverlayControl): ApiHandler[] {
  return [
    jsonHandler("GET", "/api/users/me", () => viewer(creatorClaims)),
    jsonHandler("GET", "/api/game-servers/notifications", () => control.notifications),
    jsonHandler("GET", "/api/cost-alerts", () => control.alerts),
    jsonHandler("GET", "/api/game-servers", () => control.servers),
    async (route, method, path) => {
      const match = /^\/api\/game-servers\/([0-9a-f-]{36})(\/.*)?$/i.exec(path);
      const server = control.servers.find((candidate) => candidate.primaryKey === match?.[1]);
      if (match === null || server === undefined) return false;
      const rest = match[2];
      if (method === "GET" && rest === undefined) {
        await route.fulfill({ json: withDetail(server) });
        return true;
      }
      if (method === "GET" && (rest === "/jobs" || rest === "/operations")) {
        await route.fulfill({ json: [] });
        return true;
      }
      if (method === "POST" && rest?.startsWith("/actions/") === true) {
        control.actionRequests.push(`POST ${path}`);
        await route.fulfill({ json: { ...queuedJob, gameServerId: server.primaryKey } });
        return true;
      }
      return fulfillMetricsRoute(route, method, path, server.primaryKey);
    },
  ];
}

/**
 * Installs the strict mock of the overlay screens.
 *
 * @param page The page to mock.
 * @param culture The culture of the session.
 * @param overrides Initial values that replace the defaults of the control.
 * @returns The mock session and the live control object.
 */
async function installOverlay(
  page: Page,
  culture: Culture,
  overrides: Partial<OverlayControl> = {},
): Promise<{ session: MockSession; control: OverlayControl }> {
  const control: OverlayControl = {
    servers: overlayFleet(),
    notifications: [],
    alerts: [],
    actionRequests: [],
    ...overrides,
  };
  await allowLongAudit();
  const session = await installApp(page, {
    culture,
    stream: true,
    handlers: overlayHandlers(control),
  });
  return { session, control };
}

/**
 * Builds the cost notification of the running server, whose stop action the profile menu offers.
 *
 * @returns The notification in the shape of `GET /api/game-servers/notifications`.
 */
function runningNotification() {
  return {
    serverId: runningServerId,
    title: "Alpha Ops",
    statusLabel: "Running",
    possibleCost: true,
    canOperate: true,
    costStopAction: overlayFleet()[0]?.actions[0] ?? null,
  };
}

/**
 * Builds the two alert rows of the profile menu: a reached cost threshold and an announced automatic shutdown.
 *
 * @returns The alerts in the shape of `GET /api/cost-alerts`.
 */
function menuAlerts() {
  const common = {
    serverId: runningServerId,
    serverName: "Alpha Ops",
    costOwnerId: "c0000000-0000-4000-8000-000000000001",
    currency: "EUR",
  };
  return [
    {
      ...common,
      id: "a0000000-0000-4000-8000-000000000a01",
      thresholdAmount: 10,
      accruedAmount: 10.4,
      createdAt: "2026-11-25T10:00:00Z",
    },
    {
      ...common,
      id: "a0000000-0000-4000-8000-000000000a02",
      thresholdAmount: 0,
      accruedAmount: 0,
      createdAt: "2026-11-25T11:00:00Z",
      kind: "AutoStop",
      stopsAt: "2026-11-25T18:30:00Z",
      stopReason: "Idle",
      stopLimit: 30,
    },
  ];
}

/**
 * Closes a native dialog that a previous state left open, so the next state can reach the page behind it.
 *
 * The phone projects skip the keyboard walk that normally closes the dialog with Escape.
 *
 * @param page The page under audit.
 */
async function closeOpenDialog(page: Page): Promise<void> {
  if ((await page.locator("dialog[open]").count()) === 0) return;
  await page.keyboard.press("Escape");
  await page.locator("dialog[open]").waitFor({ state: "detached" });
}

/**
 * Resolves once the fleet of the overlay fixtures rendered its two cards.
 *
 * @param page The page under audit.
 */
async function overlayFleetReady(page: Page): Promise<void> {
  await page.locator(".next-hosting-page h1").waitFor();
  await page.locator("[data-server-card]").nth(1).waitFor();
}

/**
 * Resolves once the cost confirmation is open with focus on its dismiss control, and fails when the page already sent
 * an action request, because nothing may reach the API before the member confirms (T-69-32).
 *
 * @param page The page under audit.
 * @param control The overlay control that records accepted action requests.
 */
async function costDialogReady(page: Page, control: OverlayControl | undefined): Promise<void> {
  await page.locator("dialog[open]").waitFor();
  await page.waitForFunction(
    () => document.activeElement?.hasAttribute("data-dialog-dismiss") === true,
  );
  // The open dialog re-reads its server, which can add facts a moment later and move the dialog; measuring colors
  // while it still changes yields transient results, so wait until its markup and box stop changing.
  let previous = "";
  let stable = 0;
  for (let attempt = 0; attempt < 40 && stable < 3; attempt += 1) {
    const snapshot = await page.evaluate(() => {
      const dialog = document.querySelector("dialog[open]");
      const box = dialog?.getBoundingClientRect();
      return `${dialog?.innerHTML.length ?? 0}|${box?.top}|${box?.height}`;
    });
    stable = snapshot === previous ? stable + 1 : 0;
    previous = snapshot;
    await page.waitForTimeout(80);
  }
  if (control === undefined) throw new Error("The overlay entry was not set up.");
  if (control.actionRequests.length > 0) {
    throw new Error(
      `An action was requested before the confirmation: ${control.actionRequests.join(", ")}`,
    );
  }
}

/**
 * Clicks the primary action of one fleet card, which asks for the shared cost confirmation.
 *
 * @param page The page under audit.
 * @param serverId The id of the server whose card is used.
 */
async function clickCardAction(page: Page, serverId: string): Promise<void> {
  await page
    .locator(`[data-server-card="${serverId}"] .fleet-card__actions button`)
    .first()
    .click();
}

/** The controls the profile menu moves between with the arrow keys; it equals the selector of the menu itself. */
const menuControls =
  'a[href], button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Walks the open profile menu with the arrow keys and fails when focus leaves it, a control is skipped, or Tab and
 * Shift+Tab disagree. The walk starts and ends on the close button.
 *
 * @param page The page whose profile menu is open.
 */
async function assertMenuKeyboard(page: Page): Promise<void> {
  await page.waitForFunction(
    () => document.activeElement?.classList.contains("account-profile__close") === true,
  );
  const total = await page
    .locator(".account-profile__popover")
    .evaluate((popover, selector) => popover.querySelectorAll(selector).length, menuControls);
  const indexOfFocus = (): Promise<number> =>
    page.evaluate((selector) => {
      const popover = document.querySelector(".account-profile__popover");
      const focused = document.activeElement;
      if (popover === null || focused === null || !popover.contains(focused)) return -1;
      return Array.from(popover.querySelectorAll(selector)).indexOf(focused);
    }, menuControls);
  const visited = new Set<number>();
  for (let step = 0; step < total; step += 1) {
    const index = await indexOfFocus();
    if (index < 0) throw new Error(`Focus left the profile menu at arrow step ${step + 1}.`);
    visited.add(index);
    await page.keyboard.press("ArrowDown");
  }
  if (visited.size !== total) {
    throw new Error(`The arrow keys reached ${visited.size} of ${total} profile menu controls.`);
  }
  if ((await indexOfFocus()) !== 0) throw new Error("ArrowDown did not wrap to the first control.");
  await page.keyboard.press("Tab");
  if ((await indexOfFocus()) !== 1) throw new Error("Tab did not move to the second control.");
  await page.keyboard.press("Shift+Tab");
  if ((await indexOfFocus()) !== 0)
    throw new Error("Shift+Tab did not return to the first control.");
  const rowControls = await page
    .locator(
      ".account-profile__popover [data-cost-alert-id] a, .account-profile__popover [data-auto-stop-alert-id] a",
    )
    .count();
  if (rowControls < 2)
    throw new Error("The cost alert and automatic shutdown rows are not in the menu.");
}

/** The registry entries of the fleet slice. */
export const fleetScreens: readonly ScreenEntry[] = [
  (() => {
    let control: FleetControl | undefined;
    return {
      id: "S-01",
      kind: "screen",
      route: "/command",
      routePattern: "/command",
      setup: async (page, culture) => {
        const installed = await installFleet(page, culture, { servers: populatedFleet(0) });
        control = installed.control;
        return installed.session;
      },
      states: [
        {
          name: "populated, no attention",
          ready: async (page) => {
            await page.locator("main h1").waitFor();
            await page
              .locator(".command-operational-snapshot .command-overview-all-clear")
              .waitFor();
          },
        },
        {
          name: "attention list",
          open: async (page) => {
            const server = control?.servers[4];
            if (control !== undefined) control.servers = populatedFleet();
            if (control === undefined || server === undefined)
              throw new Error("S-01 was not set up.");
            control.notifications = [
              {
                serverId: server.primaryKey,
                title: server.title,
                statusLabel: "Stopped",
                possibleCost: true,
                canOperate: true,
                costStopAction: null,
              },
            ];
            await page.reload();
          },
          ready: async (page) => {
            await page.locator(".command-attention-list").waitFor();
          },
        },
      ],
      audit: {
        axe: true,
        keyboard: true,
        reflow: true,
        spacing: true,
        german: true,
        headings: true,
      },
    } satisfies ScreenEntry;
  })(),
  (() => {
    let control: FleetControl | undefined;
    let session: MockSession | undefined;
    const current = (): { control: FleetControl; session: MockSession } => {
      if (control === undefined || session === undefined) throw new Error("S-02 was not set up.");
      return { control, session };
    };
    return {
      id: "S-02",
      kind: "screen",
      route: "/command/servers",
      routePattern: "/command/servers",
      baseline: "K-01",
      setup: async (page, culture) => {
        const installed = await installFleet(page, culture);
        control = installed.control;
        session = installed.session;
        return installed.session;
      },
      states: [
        {
          name: "populated mixed states",
          ready: populatedReady,
        },
        {
          name: "no match",
          open: async (page) => {
            await page.getByRole("searchbox").fill("zzz-no-server-has-this-name");
          },
          ready: async (page) => {
            await page.locator(".fleet-sections .state-panel--empty").waitFor();
            await page.locator("[data-server-card]").first().waitFor({ state: "detached" });
          },
        },
        {
          name: "load error with stale data",
          open: async (page) => {
            const { control: live, session: mock } = current();
            await page.getByRole("searchbox").fill("");
            await page.locator("[data-server-card]").nth(4).waitFor();
            live.failFleet = true;
            refreshFleet(mock);
          },
          ready: async (page) => {
            await page.locator(".next-hosting-page .state-panel--error").waitFor();
            await page.locator("[data-server-card]").nth(4).waitFor();
          },
        },
        {
          name: "disconnected live notice",
          open: async (page) => {
            const { control: live, session: mock } = current();
            live.failFleet = false;
            refreshFleet(mock);
            await page
              .locator(".next-hosting-page .state-panel--error")
              .waitFor({ state: "detached" });
            await mock.stream?.closeFromServer();
          },
          ready: async (page) => {
            // The notice appears only after three seconds of continuous disconnection.
            await page.locator(".fleet-live-notice").waitFor({ timeout: 15_000 });
          },
        },
        {
          name: "creator empty",
          open: async (page) => {
            const { control: live } = current();
            live.servers = [];
            live.claims = creatorClaims;
            await page.reload();
          },
          ready: async (page) => {
            await page.locator(".next-hosting-page .state-panel--empty").waitFor();
            await page
              .locator('.next-hosting-page .state-panel--empty a[href$="/command/servers/create"]')
              .waitFor();
          },
        },
        {
          name: "member empty",
          open: async (page) => {
            const { control: live } = current();
            live.servers = [];
            live.claims = memberClaims;
            await page.reload();
          },
          ready: async (page) => {
            await page.locator(".next-hosting-page .state-panel--empty").waitFor();
            await page
              .locator('.next-hosting-page a[href$="/command/servers/create"]')
              .waitFor({ state: "detached" });
          },
        },
      ],
      audit: {
        axe: true,
        keyboard: true,
        reflow: true,
        spacing: true,
        german: true,
        headings: true,
      },
    } satisfies ScreenEntry;
  })(),
  (() => {
    let session: MockSession | undefined;
    return {
      id: "N-01",
      kind: "notice",
      route: "/command/servers",
      setup: async (page, culture) => {
        session = (await installFleet(page, culture)).session;
        return session;
      },
      states: [
        {
          name: "live updates paused",
          open: async (page) => {
            await page.locator("[data-server-card]").nth(4).waitFor();
            await session?.stream?.closeFromServer();
          },
          ready: async (page) => {
            // The notice appears only after three seconds of continuous disconnection.
            await page.locator(".fleet-live-notice").waitFor({ timeout: 15_000 });
          },
        },
      ],
      audit: {
        axe: true,
        keyboard: true,
        reflow: true,
        spacing: true,
        german: true,
        headings: true,
      },
    } satisfies ScreenEntry;
  })(),
  {
    id: "N-04",
    kind: "notice",
    route: "/command/servers",
    setup: async (page, culture) =>
      (
        await installFleet(page, culture, {
          servers: [
            fleetServer({
              index: 2,
              title: "Bravo Training",
              actions: [fleetAction("start", { enabled: false, unavailableReason: pausedReason })],
            }),
          ],
        })
      ).session,
    states: [
      {
        name: "execution paused",
        ready: async (page) => {
          await page.locator("[data-server-card]").first().waitFor();
          await page.locator("[data-server-card] .action-reason").waitFor();
        },
      },
    ],
    audit: { axe: true, keyboard: true, reflow: true, spacing: true, german: true, headings: true },
  },
  (() => {
    let control: CreateControl | undefined;
    return {
      id: "S-03",
      kind: "screen",
      route: "/command/servers/create",
      routePattern: "/command/servers/create",
      baseline: "K-02",
      setup: async (page, culture) => {
        const installed = await installCreate(page, culture);
        control = installed.control;
        return installed.session;
      },
      states: [
        { name: "default", ready: createReady },
        {
          name: "advanced options open",
          baseline: "K-03",
          open: async (page) => {
            await page.locator(advancedToggle).click();
          },
          ready: async (page) => {
            await page
              .locator(".create-advanced[data-open=true] .create-advanced__content")
              .waitFor();
            await page.locator(".create-advanced__content select").first().waitFor();
          },
        },
        {
          name: "invalid submit",
          open: async (page) => {
            await page.reload();
            await createReady(page);
            await page.locator(".create-summary button[type=submit]").click();
          },
          ready: async (page) => {
            await page.locator(".create-summary__alert").waitFor();
            await page.waitForFunction(
              () => document.activeElement === document.querySelector("input[name=title]"),
            );
            // The invalid alert makes the fixed create bar taller, so a control that straddles the bar at the current
            // scroll position reads as a partly covered, too small target to axe. At the end of the page the bar
            // overlaps only the bottom padding the layout reserves for it, so every control is fully visible; the
            // focus stays on the title.
            await page.evaluate(() =>
              window.scrollTo({ top: document.documentElement.scrollHeight, behavior: "instant" }),
            );
          },
        },
        {
          name: "blocked owner",
          open: async (page) => {
            if (control === undefined) throw new Error("S-03 was not set up.");
            control.frozenSelf = true;
            await page.reload();
          },
          ready: async (page) => {
            await createReady(page);
            await page.locator(".create-summary button[type=submit][aria-disabled=true]").waitFor();
          },
        },
        {
          name: "start choice",
          open: async (page) => {
            if (control === undefined) throw new Error("S-03 was not set up.");
            control.frozenSelf = false;
            await page.reload();
            await createReady(page);
            await page.locator("input[name=title]").fill("Juno Ops");
            await page.locator("input[type=radio]").nth(1).check();
          },
          ready: async (page) => {
            await page.locator(".create-summary button[type=submit]").waitFor();
            await page.waitForFunction(
              () => document.querySelectorAll("input[type=radio]")[1]?.matches(":checked") === true,
            );
          },
        },
      ],
      audit: {
        axe: true,
        keyboard: true,
        reflow: true,
        spacing: true,
        german: true,
        headings: true,
      },
    } satisfies ScreenEntry;
  })(),
  {
    id: "N-03",
    kind: "notice",
    route: "/command/servers/create",
    setup: async (page, culture) => (await installCreate(page, culture)).session,
    states: [
      {
        name: "created, start not confirmed",
        open: async (page) => {
          await createReady(page);
          await page.locator("input[name=title]").fill("Juno Ops");
          await page.locator("input[type=radio]").nth(1).check();
          await page.locator(".create-summary button[type=submit]").click();
          // The start confirmation opens over the new workspace; dismissing it leaves the create notice behind.
          await page.locator("dialog[open] [data-dialog-dismiss]").click();
          await page.locator("dialog[open]").waitFor({ state: "detached" });
        },
        ready: async (page) => {
          await page.locator("main h1").waitFor();
          await page.locator(".state-panel--info .create-notice__name").waitFor();
          // The creation toast overlaps the page for a few seconds by design; O-09 audits the toast itself, so this
          // entry waits until it is gone and audits the notice. A pointer resting on the toast pauses its timer, so the
          // pointer of the earlier click is moved off the viewport corner first.
          await page.mouse.move(1, 1);
          await page.locator(".app-toast").first().waitFor({ state: "detached", timeout: 12_000 });
        },
      },
    ],
    audit: { axe: true, keyboard: true, reflow: true, spacing: true, german: true, headings: true },
  },
  {
    id: "N-02",
    kind: "notice",
    route: `/command/servers/${createdServerId}?action=start`,
    setup: async (page, culture) =>
      (
        await installCreate(page, culture, {
          actions: [fleetAction("start", { enabled: false, unavailableReason: operateReason })],
        })
      ).session,
    states: [
      {
        name: "linked action is unavailable",
        ready: async (page) => {
          await page.locator("main h1").waitFor();
          await page.locator(".state-panel--warning").waitFor();
          await page.locator("dialog[open]").waitFor({ state: "detached" });
        },
      },
      {
        name: "linked action is unknown",
        open: async (page) => {
          await page.goto(`command/servers/${createdServerId}?action=no-such-action`);
        },
        ready: async (page) => {
          await page.locator("main h1").waitFor();
          await page.locator(".state-panel--info").waitFor();
          await page.locator("dialog[open]").waitFor({ state: "detached" });
        },
      },
    ],
    audit: { axe: true, keyboard: true, reflow: true, spacing: true, german: true, headings: true },
  },
  (() => {
    let control: OverlayControl | undefined;
    return {
      id: "O-01",
      kind: "overlay",
      route: "/command/servers",
      baseline: "K-11",
      setup: async (page, culture) => {
        const installed = await installOverlay(page, culture, {
          notifications: [runningNotification()],
        });
        control = installed.control;
        return installed.session;
      },
      states: [
        {
          name: "start from a fleet card",
          open: async (page) => {
            await overlayFleetReady(page);
            await clickCardAction(page, offServerId);
          },
          ready: (page) => costDialogReady(page, control),
        },
        {
          name: "stop from a fleet card",
          open: async (page) => {
            await closeOpenDialog(page);
            await clickCardAction(page, runningServerId);
          },
          ready: (page) => costDialogReady(page, control),
        },
        {
          name: "terminate with a challenge from a deep link",
          open: async (page) => {
            await page.goto(`command/servers/${runningServerId}?action=terminate`);
          },
          ready: async (page) => {
            await costDialogReady(page, control);
            // The typed challenge is required before confirming, and its helper text describes the field.
            await page.waitForFunction(() => {
              const dialog = document.querySelector("dialog[open]");
              const field = dialog?.querySelector("input[type=text], input:not([type])");
              const submit = dialog?.querySelector("button[type=submit]");
              const describedBy = field?.getAttribute("aria-describedby") ?? "";
              return (
                submit?.getAttribute("aria-disabled") === "true" &&
                describedBy !== "" &&
                describedBy.split(" ").every((id) => document.getElementById(id) !== null)
              );
            });
          },
        },
        {
          name: "stop from the profile menu",
          open: async (page) => {
            await page.goto("command/servers");
            await overlayFleetReady(page);
            await page.locator(".account-profile__trigger").click();
            await page.locator(".account-profile__notification button").first().click();
          },
          ready: (page) => costDialogReady(page, control),
        },
      ],
      audit: {
        axe: true,
        keyboard: true,
        reflow: true,
        spacing: true,
        german: true,
        headings: true,
      },
    } satisfies ScreenEntry;
  })(),
  {
    id: "O-08",
    kind: "overlay",
    route: "/command/servers",
    setup: async (page, culture) =>
      (
        await installOverlay(page, culture, {
          notifications: [runningNotification()],
          alerts: menuAlerts(),
        })
      ).session,
    states: [
      {
        name: "menu open with the attention list",
        open: async (page) => {
          await overlayFleetReady(page);
          await page.locator(".account-profile__trigger").click();
        },
        ready: async (page) => {
          await page.locator(".account-profile__popover [data-cost-alert-id]").waitFor();
          await page.locator(".account-profile__popover [data-auto-stop-alert-id]").waitFor();
          await assertMenuKeyboard(page);
        },
      },
      {
        name: "escape closes the menu and returns focus to the avatar",
        open: async (page) => {
          await page.keyboard.press("Escape");
        },
        ready: async (page) => {
          await page.locator(".account-profile__popover").waitFor({ state: "detached" });
          await page.waitForFunction(
            () => document.activeElement?.classList.contains("account-profile__trigger") === true,
          );
        },
      },
    ],
    // The profile menu is a popover, not a native modal: the keyboard walk and the sticky coverage check scope to the
    // page behind it and would report the menu scrim as covering every control. The state's own readiness steps
    // assert the menu contract instead (arrow keys, Tab, Shift+Tab, Escape and the focus return). The heading check is
    // off because the menu is a labelled dialog that sits in the topbar before the page content, so its own `h2` is
    // the first heading in DOM order while it is open; the page heading structure is audited by the screen entries.
    audit: {
      axe: true,
      keyboard: false,
      reflow: true,
      spacing: true,
      german: true,
      headings: false,
    },
  },
  (() => {
    let control: OverlayControl | undefined;
    return {
      id: "O-09",
      kind: "overlay",
      route: "/command/servers",
      setup: async (page, culture) => {
        const installed = await installOverlay(page, culture);
        control = installed.control;
        return installed.session;
      },
      states: [
        {
          name: "toast after a confirmed action",
          open: async (page) => {
            await overlayFleetReady(page);
            await clickCardAction(page, offServerId);
            await page.locator("dialog[open] button[type=submit]").click();
          },
          ready: async (page) => {
            const toast = page.locator('.app-toast-region[aria-live="polite"] .app-toast');
            await toast.waitFor();
            // A pointer resting on the toast pauses its timer, so it stays while the audit runs.
            await toast.hover();
            if (control?.actionRequests.length !== 1) {
              throw new Error("The confirmed action was not requested exactly once.");
            }
            const stoleFocus = await page.evaluate(
              () => document.activeElement?.closest(".app-toast") != null,
            );
            if (stoleFocus) throw new Error("The toast moved keyboard focus into itself.");
            // Its dismiss control is reachable by Tab from the page content.
            for (let step = 0; step < 120; step += 1) {
              const onDismiss = await page.evaluate(
                () => document.activeElement?.classList.contains("app-toast__dismiss") === true,
              );
              if (onDismiss) return;
              await page.keyboard.press("Tab");
            }
            throw new Error("The toast dismiss control was not reached by Tab.");
          },
        },
      ],
      // The toast overlaps the bottom of the page for a few seconds by design, so the keyboard walk and the sticky
      // coverage check would report page controls as covered. The readiness step asserts the toast contract
      // instead: polite live region, no focus theft and a dismiss control that Tab reaches.
      audit: {
        axe: true,
        keyboard: false,
        reflow: true,
        spacing: true,
        german: true,
        headings: true,
      },
    } satisfies ScreenEntry;
  })(),
];

/** Required ids the fleet slice has not added yet; plan 69-26 empties this list. */
export const fleetPending: readonly string[] = [];
