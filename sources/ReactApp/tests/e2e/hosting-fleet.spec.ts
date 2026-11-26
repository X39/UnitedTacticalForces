import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { fleetChangedFrame, mockFleetStream, type FleetStreamMock } from "./support/fleetStream";
import { fulfillCostRoute } from "./support/costMocks";

type Culture = "en" | "de";

const userId = "10000000-0000-4000-8000-0000000000f0";
const offId = "10000000-0000-4000-8000-0000000000f1";
const runningId = "10000000-0000-4000-8000-0000000000f2";
const unconfirmedId = "10000000-0000-4000-8000-0000000000f3";
const longId = "10000000-0000-4000-8000-0000000000f4";
const wcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];
const pageContainer = ".next-hosting-page";
const deploymentNotRunningReason = "The deployment is not running.";

/** A title of exactly 256 characters whose long unbroken tail is the worst case for wrapping. */
const longTitle = `Long title ${"x".repeat(245)}`;
/** A cost owner name without break opportunities. */
const longOwner = `Logistikabteilung${"Verwaltungsangelegenheiten".repeat(4)}`;

/** The visible copy each test reads, per culture. */
const copy = {
  en: {
    heading: "Servers",
    search: "Search servers",
    filters: "Filter servers",
    noMatch: "No servers match",
    paused: "Live updates paused",
  },
  de: {
    heading: "Server",
    search: "Server durchsuchen",
    filters: "Server filtern",
    noMatch: "Keine passenden Server",
    paused: "Live-Updates pausiert",
  },
} satisfies Record<Culture, Record<string, string>>;

const viewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

const startAction = {
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

function deployment(status: string, possibleCost: boolean) {
  return {
    status,
    stage: status === "Running" ? "Ready" : "Provisioning",
    possibleCost,
    workerStatus: "Connected",
    lastHeartbeat: "2026-11-16T10:00:00Z",
    connectionEndpoint: null,
    connectionProtocol: null,
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
}

interface ServerOptions {
  id: string;
  title: string;
  deployment: object | null;
  actions: object[];
  costOwnerName?: string;
  pendingJobCount?: number;
}

function makeServer(options: ServerOptions) {
  return {
    primaryKey: options.id,
    title: options.title,
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
    costOwnerId: userId,
    costOwnerUserId: userId,
    costOwnerName: options.costOwnerName ?? "Operations",
    createdById: userId,
    isLaunchDisabled: false,
    isArchived: false,
    canModify: true,
    timeStampCreated: "2026-11-16T10:00:00Z",
    timeStampUpdated: "2026-11-16T10:00:00Z",
    pendingJobCount: options.pendingJobCount ?? 0,
    deployment: options.deployment,
    actions: options.actions,
  };
}

type FleetServer = ReturnType<typeof makeServer>;

const offServer = () =>
  makeServer({ id: offId, title: "Alpha training", deployment: null, actions: [startAction] });
const runningServer = () =>
  makeServer({
    id: runningId,
    title: "Alpha operations",
    deployment: deployment("Running", true),
    actions: [stopAction],
  });
const unconfirmedServer = () =>
  makeServer({
    id: unconfirmedId,
    title: "Charlie reserve",
    deployment: deployment("Stopped", true),
    actions: [{ ...stopAction, enabled: false, unavailableReason: deploymentNotRunningReason }],
  });
const longTextServer = () =>
  makeServer({
    id: longId,
    title: longTitle,
    deployment: deployment("Running", true),
    actions: [stopAction],
    costOwnerName: longOwner,
  });
const populatedFleet = () => [offServer(), runningServer(), unconfirmedServer()];

/** The mutable API mock of one test. */
interface FleetApi {
  /** Requests that no mock handled; they were aborted. */
  unexpected: string[];
  /** Replaces the list `GET /api/game-servers` returns from now on. */
  setServers(servers: FleetServer[]): void;
  /** Holds every later `GET /api/game-servers` response until {@link FleetApi.releaseFleet}. */
  holdFleet(): void;
  /** Answers every held fleet request with the current list and stops holding. */
  releaseFleet(): void;
  /** Number of start requests the API accepted. */
  startRequests(): number;
}

/**
 * Installs the strict API mock set. Unmocked requests are aborted and recorded so every test can assert none
 * happened; a thrown route handler would leave the request hanging instead.
 */
async function mockApi(page: Page, culture: Culture, initial: FleetServer[]): Promise<FleetApi> {
  const unexpected: string[] = [];
  let servers = initial;
  let held = false;
  let release: (() => void) | null = null;
  let gate: Promise<void> = Promise.resolve();
  let starts = 0;

  await page.addInitScript((value) => localStorage.setItem("UtfCulture", value), culture);
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
    const serverPath = /^\/api\/game-servers\/([0-9a-f-]{36})(\/.*)?$/.exec(path);
    if (path === "/api/users/me") {
      return route.fulfill({
        json: {
          primaryKey: userId,
          nickname: "Operator",
          isBanned: false,
          isVerified: true,
          isDeleted: false,
          isVereinMember: false,
          claims: [{ identifier: "sv", value: null }],
          roles: [],
        },
      });
    }
    if (path === "/api/users/authentication-providers") {
      return route.fulfill({ json: { steam: true, discord: true } });
    }
    if (await fulfillCostRoute(route, method, path, serverPath?.[1] ?? "")) return;
    if (path === "/api/game-servers" && method === "GET") {
      if (held) await gate;
      // The page may have cancelled the request while it was held; fulfilling it then is harmless.
      return route.fulfill({ json: servers }).catch(() => undefined);
    }
    if (
      method === "GET" &&
      ["/api/site-image-settings", "/api/game-servers/notifications"].includes(path)
    ) {
      return route.fulfill({ json: [] });
    }
    if (serverPath) {
      const [, id, rest] = serverPath;
      const server = servers.find((candidate) => candidate.primaryKey === id);
      if (method === "GET" && rest === undefined && server) return route.fulfill({ json: server });
      if (method === "GET" && (rest === "/jobs" || rest === "/operations")) {
        return route.fulfill({ json: [] });
      }
      if (method === "POST" && rest === "/actions/start" && server) {
        starts += 1;
        return route.fulfill({
          json: {
            primaryKey: "20000000-0000-4000-8000-0000000000f1",
            gameServerId: id,
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
          },
        });
      }
    }
    unexpected.push(`${method} ${path}`);
    return route.abort();
  });

  return {
    unexpected,
    setServers(next) {
      servers = next;
    },
    holdFleet() {
      held = true;
      gate = new Promise<void>((resolve) => {
        release = resolve;
      });
    },
    releaseFleet() {
      held = false;
      release?.();
      release = null;
    },
    startRequests() {
      return starts;
    },
  };
}

/** Runs axe at WCAG 2.1 AA against one container and returns the violations. */
async function scan(page: Page, selector: string) {
  const results = await new AxeBuilder({ page }).include(selector).withTags(wcagTags).analyze();
  // A scan that evaluated no rule at all would pass vacuously, so require evidence that rules ran.
  expect(results.passes.length).toBeGreaterThan(0);
  return results.violations.map((violation) => ({
    id: violation.id,
    nodes: violation.nodes.map((node) => node.target.join(" ")),
  }));
}

/** Asserts that the document cannot scroll horizontally. */
async function expectNoHorizontalScroll(page: Page) {
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth);
}

/**
 * Layout boxes come back with fractional coordinates after scrolling; an edge that ends less than one CSS pixel past
 * the viewport is a rounding artifact, not a clipped control.
 */
const subpixelTolerance = 1;

/**
 * Asserts that every matched element, once scrolled into view, lies fully inside the viewport.
 *
 * @param page The page under test.
 * @param selector The elements to check.
 * @param axes `both` checks both axes; `horizontal` checks only the horizontal axis, for elements that may be taller
 * than a small viewport, such as a whole card.
 */
async function expectFullyReachable(
  page: Page,
  selector: string,
  axes: "both" | "horizontal" = "both",
) {
  const viewport = page.viewportSize();
  expect(viewport).not.toBeNull();
  const controls = page.locator(selector);
  const count = await controls.count();
  expect(count).toBeGreaterThan(0);
  for (let index = 0; index < count; index += 1) {
    const control = controls.nth(index);
    await control.scrollIntoViewIfNeeded();
    const box = await control.boundingBox();
    expect(box, `${selector} #${index} has a layout box`).not.toBeNull();
    expect(box!.x, `${selector} #${index} left edge`).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width, `${selector} #${index} right edge`).toBeLessThanOrEqual(
      viewport!.width + subpixelTolerance,
    );
    if (axes === "horizontal") continue;
    expect(box!.y, `${selector} #${index} top edge`).toBeGreaterThanOrEqual(-subpixelTolerance);
    expect(box!.y + box!.height, `${selector} #${index} bottom edge`).toBeLessThanOrEqual(
      viewport!.height + subpixelTolerance,
    );
  }
}

/** Asserts that no matched element clips its own content horizontally. */
async function expectNoInnerOverflow(page: Page, selector: string) {
  const elements = page.locator(selector);
  const count = await elements.count();
  expect(count).toBeGreaterThan(0);
  for (let index = 0; index < count; index += 1) {
    const fits = await elements
      .nth(index)
      .evaluate((element) => element.scrollWidth <= element.clientWidth);
    expect(fits, `${selector} #${index} fits its box`).toBe(true);
  }
}

/** Opens the fleet and waits until the given number of cards rendered and the stream connected. */
async function openFleet(page: Page, stream: FleetStreamMock, cards: number, path = "") {
  await page.goto(`command/servers${path}`);
  await expect(page.locator("[data-server-card]")).toHaveCount(cards);
  await expect.poll(() => stream.connectionCount()).toBeGreaterThan(0);
}

function card(page: Page, serverId: string) {
  return page.locator(`[data-server-card="${serverId}"]`);
}

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`fleet layout (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test("populated fleet has no horizontal scroll, reachable actions and no axe violation", async ({
        page,
      }) => {
        const api = await mockApi(page, culture, populatedFleet());
        const stream = await mockFleetStream(page);

        await openFleet(page, stream, 3);
        await expect(
          page.getByRole("heading", { level: 1, name: copy[culture].heading, exact: true }),
        ).toBeVisible();
        await expectNoHorizontalScroll(page);
        await expectFullyReachable(page, ".fleet-card__actions button");
        await expectFullyReachable(page, ".fleet-card__actions a");
        const chips = `[role="group"][aria-label="${copy[culture].filters}"] button`;
        await expectFullyReachable(page, chips);
        await expectNoInnerOverflow(page, chips);
        await page.evaluate(() => window.scrollTo(0, 0));
        expect(await scan(page, pageContainer)).toEqual([]);
        expect(api.unexpected).toEqual([]);
      });

      test("no-match panel has no horizontal scroll and no axe violation", async ({ page }) => {
        const api = await mockApi(page, culture, populatedFleet());
        const stream = await mockFleetStream(page);

        await openFleet(page, stream, 3);
        await page
          .getByRole("searchbox", { name: copy[culture].search, exact: true })
          .fill("zzz no server has this name");
        await expect(page.getByRole("heading", { name: copy[culture].noMatch })).toBeVisible();
        await expect(page.locator("[data-server-card]")).toHaveCount(0);
        await expectNoHorizontalScroll(page);
        await expectFullyReachable(page, ".fleet-sections button");
        expect(await scan(page, pageContainer)).toEqual([]);
        expect(api.unexpected).toEqual([]);
      });

      test("disconnected notice has no horizontal scroll and no axe violation", async ({
        page,
      }) => {
        const api = await mockApi(page, culture, populatedFleet());
        const stream = await mockFleetStream(page);

        await openFleet(page, stream, 3);
        await stream.closeFromServer();
        // The notice appears only after 3 seconds of continuous disconnection.
        const notice = page.locator(".fleet-live-notice");
        await expect(notice.getByText(copy[culture].paused, { exact: true })).toBeVisible({
          timeout: 10_000,
        });
        await expectNoHorizontalScroll(page);
        await expectFullyReachable(page, ".fleet-live-notice button");
        expect(await scan(page, pageContainer)).toEqual([]);
        expect(api.unexpected).toEqual([]);
      });

      test("a 256-character title and a long cost owner do not overflow", async ({ page }) => {
        expect(longTitle).toHaveLength(256);
        const api = await mockApi(page, culture, [longTextServer()]);
        const stream = await mockFleetStream(page);

        await openFleet(page, stream, 1);
        const longCard = card(page, longId);
        await expect(longCard.getByRole("heading", { level: 3 })).toHaveText(longTitle);
        await expect(longCard.getByText(longOwner, { exact: true })).toBeVisible();
        await expectNoHorizontalScroll(page);
        await expectNoInnerOverflow(page, ".fleet-card__title");
        await expectNoInnerOverflow(page, ".fleet-card__facts dd");
        await expectFullyReachable(page, ".fleet-card", "horizontal");
        await expectFullyReachable(page, ".fleet-card__actions button");
        await expectFullyReachable(page, ".fleet-card__actions a");
        expect(api.unexpected).toEqual([]);
      });
    });
  }
}

test.describe("fleet behavior (en, 390x844)", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test("search and filter live in the URL without growing the history", async ({ page }) => {
    const api = await mockApi(page, "en", populatedFleet());
    const stream = await mockFleetStream(page);

    await openFleet(page, stream, 1, "?q=alpha&filter=running");
    const search = page.getByRole("searchbox", { name: "Search servers", exact: true });
    const runningChip = page
      .getByRole("group", { name: "Filter servers" })
      .getByRole("button")
      .filter({ hasText: "Running" });
    await expect(search).toHaveValue("alpha");
    await expect(runningChip).toHaveAttribute("aria-pressed", "true");
    await expect(card(page, runningId)).toBeVisible();

    const historyLength = await page.evaluate(() => window.history.length);
    await search.fill("alpha oper");
    await expect(page).toHaveURL(/[?&]q=alpha(\+|%20)oper(&|$)/);
    await expect(page).toHaveURL(/[?&]filter=running(&|$)/);
    expect(await page.evaluate(() => window.history.length)).toBe(historyLength);
    await expect(page.locator("[data-server-card]")).toHaveCount(1);

    await page.reload();
    await expect(search).toHaveValue("alpha oper");
    await expect(runningChip).toHaveAttribute("aria-pressed", "true");
    await expect(page.locator("[data-server-card]")).toHaveCount(1);
    await expect(card(page, runningId)).toBeVisible();
    expect(api.unexpected).toEqual([]);
  });

  test("a pushed fleet frame updates the card without a reload", async ({ page }) => {
    const api = await mockApi(page, "en", [offServer()]);
    const stream = await mockFleetStream(page);
    let navigations = 0;
    page.on("framenavigated", (frame) => {
      if (frame === page.mainFrame()) navigations += 1;
    });

    await openFleet(page, stream, 1);
    const badge = card(page, offId).locator(".server-state-badge");
    await expect(badge).toHaveText("Off");
    const navigationsBefore = navigations;

    api.setServers([{ ...offServer(), deployment: deployment("Running", true), actions: [] }]);
    stream.send(fleetChangedFrame(offId));
    await expect(badge).toHaveText("Running");
    expect(navigations).toBe(navigationsBefore);
    expect(stream.paths()).toContain("game-servers/fleet");
    expect(new Set(stream.paths())).toEqual(new Set(["game-servers/fleet"]));
    expect(api.unexpected).toEqual([]);
  });

  test("quick start shows the submitted state, then the reported state", async ({ page }) => {
    const api = await mockApi(page, "en", [offServer()]);
    const stream = await mockFleetStream(page);

    await openFleet(page, stream, 1);
    const offCard = card(page, offId);
    const badge = offCard.locator(".server-state-badge");
    await offCard.getByRole("button", { name: "Start server" }).click();
    const dialog = page.locator("dialog[open]");
    await expect(dialog).toBeVisible();

    // Hold the refetch the accepted action triggers so the submitted state stays observable.
    api.holdFleet();
    await dialog.locator("button[type=submit]").click();
    await expect(dialog).toHaveCount(0);
    expect(api.startRequests()).toBe(1);
    const inProgress = offCard.getByRole("button", { name: "In progress" });
    await expect(inProgress).toHaveAttribute("aria-disabled", "true");
    await expect(
      offCard.getByText("Request sent. This card updates as soon as the server changes."),
    ).toBeVisible();
    await expect(badge).toHaveText("Off");

    api.setServers([
      {
        ...offServer(),
        deployment: deployment("Provisioning", true),
        pendingJobCount: 1,
        actions: [{ ...startAction, enabled: false, pending: true }],
      },
    ]);
    api.releaseFleet();
    stream.send(fleetChangedFrame(offId));
    await expect(badge).toHaveText("Starting");
    await expect(offCard.getByText("Request sent.", { exact: false })).toHaveCount(0);
    expect(api.unexpected).toEqual([]);
  });

  test("the Command Center nav and the fleet page read Servers", async ({ page }) => {
    const api = await mockApi(page, "en", populatedFleet());
    const stream = await mockFleetStream(page);

    await openFleet(page, stream, 3);
    await expect(
      page.getByRole("heading", { level: 1, name: "Servers", exact: true }),
    ).toBeVisible();
    await expect(page).toHaveTitle(/Servers/);
    await page.getByRole("button", { name: "Open Command Center navigation" }).click();
    const navLink = page
      .locator("#command-navigation")
      .getByRole("link", { name: "Servers", exact: true });
    await expect(navLink).toBeVisible();
    await expect(navLink).toHaveAttribute("href", "/next/command/servers");
    expect(api.unexpected).toEqual([]);
  });
});
