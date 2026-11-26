import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import process from "node:process";
import {
  accruedCostFrame,
  costAlertRaisedFrame,
  mockFleetStream,
  type CostAlertFrameSource,
} from "./support/fleetStream";
import { fulfillMetricsRoute } from "./support/metricsMocks";

type Culture = "en" | "de";

const serverId = "10000000-0000-4000-8000-000000000b66";
const costOwnerId = "40000000-0000-4000-8000-000000000b66";
const serverTitle = "Workspace test server";

/** Layout edges come back fractional; less than one CSS pixel past the viewport is a rounding artifact. */
const subpixelTolerance = 1;

const wcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

/** The two viewports of the phase gate: the smallest supported phone and a common one. */
const viewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

/** The workspace page container every shell scan covers. */
const pageContainer = ".next-hosting-page";

/** The page container of the cost alerts page. */
const alertsPageContainer = ".cost-alerts-page";

/** The profile menu panel, which holds the alert rows. */
const profileMenuPanel = "#account-profile-panel";

/** The visible copy each test reads, per culture. Amounts are matched loosely because the separator is locale. */
const copy = {
  en: {
    trigger: "Open profile menu for Operator",
    menuName: "Profile menu",
    bannerTitle: "Cost alert",
    stop: "Shut down and stop billing",
    keepRunning: "Keep server running",
    sessionSoFar: "This session so far",
    reached: /1\.00.* reached/,
    dismiss: /^Dismiss alert at /,
    noAlerts: "No alerts set",
    addAmount: "Add an amount",
    amountLabel: "Alert amount 1 (EUR)",
    invalidNumber: "Enter a number, for example 12.50.",
    save: "Save alert settings",
    saved: "Saved.",
    pristine: "No unsaved changes.",
    email: "Email",
    readOnly: "You can see these settings but not change them.",
    asOf: /^As of /,
    billedHourNext: /Billed per started hour\. The next hour starts at /,
  },
  de: {
    trigger: "Profilmenü für Operator öffnen",
    menuName: "Profilmenü",
    bannerTitle: "Kostenwarnung",
    stop: "Herunterfahren und Abrechnung beenden",
    keepRunning: "Server weiterlaufen lassen",
    sessionSoFar: "Bisher in dieser Sitzung",
    reached: /1,00.* erreicht/,
    dismiss: /^Warnung bei .* schließen$/,
    noAlerts: "Keine Warnungen eingerichtet",
    addAmount: "Betrag hinzufügen",
    amountLabel: "Warnbetrag 1 (EUR)",
    invalidNumber: "Gib eine Zahl ein, zum Beispiel 12,50.",
    save: "Warneinstellungen speichern",
    saved: "Gespeichert.",
    pristine: "Keine ungespeicherten Änderungen.",
    email: "E-Mail",
    readOnly: "Du kannst diese Einstellungen sehen, aber nicht ändern.",
    asOf: /^Stand /,
    billedHourNext: /Abgerechnet wird jede angefangene Stunde\. Die nächste Stunde beginnt um /,
  },
} satisfies Record<Culture, Record<string, string | RegExp>>;

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

/** A running, costing server that carries an alert summary and lets the viewer manage its alerts. */
function makeServer() {
  return {
    primaryKey: serverId,
    title: serverTitle,
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
    costOwnerId,
    costOwnerName: "Operations",
    createdById: serverId,
    isLaunchDisabled: false,
    isArchived: false,
    canModify: true,
    timeStampCreated: "2026-11-16T10:00:00Z",
    timeStampUpdated: "2026-11-16T10:00:00Z",
    pendingJobCount: 0,
    costAlertThresholds: [] as number[],
    canManageCostAlerts: true,
    deployment: runningDeployment,
    actions: [stopAction],
  };
}

/**
 * The accrued cost REST value of the server: a costing session of 1.20 EUR as of a fixed instant, billed per
 * started hour with the next hour starting 40 minutes after that instant.
 */
function makeAccrued() {
  return {
    gameServerId: serverId,
    session: {
      deploymentId: "50000000-0000-4000-8000-000000000b66",
      amount: 1.2,
      currency: "EUR",
      asOf: "2026-11-21T10:00:00Z",
      isFinal: false,
      unavailableReason: null,
      billingUnitSeconds: 3600,
      billedThrough: "2026-11-21T10:40:00Z",
    },
  };
}

/**
 * Builds one cost alert of the server.
 *
 * @param index Makes the id unique and moves the creation instant, so a list has a stable newest-first order.
 * @param thresholdAmount The configured amount that was reached.
 */
function makeAlert(index: number, thresholdAmount: number): CostAlertFrameSource {
  return {
    id: `60000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
    serverId,
    serverName: serverTitle,
    costOwnerId,
    thresholdAmount,
    accruedAmount: thresholdAmount + 0.04,
    currency: "EUR",
    createdAt: new Date(Date.UTC(2026, 10, 21, 10, 0, 0) + index * 60_000).toISOString(),
  };
}

/** The personal cost alert settings the page loads, editable by the viewer. */
function makeSettings(canManage = true) {
  return {
    costOwnerId,
    costOwnerName: "Operations",
    revision: 3,
    thresholds: [] as number[],
    discordEnabled: false,
    emailEnabled: false,
    currency: "EUR",
    maxThresholds: 10,
    maxThresholdAmount: 10000,
    canManage,
    audience: "Self",
    discord: "NotLinked",
    email: "Deliverable",
    messageLanguage: null as string | null,
  };
}

/** What the mocked API returns for one test, plus what the test observed. */
interface CostApiMock {
  /** The server DTO. */
  server: ReturnType<typeof makeServer>;
  /** The accrued cost REST value. */
  accrued: ReturnType<typeof makeAccrued>;
  /** The undismissed alerts the REST list returns; dismissals remove from it. */
  alerts: CostAlertFrameSource[];
  /** The settings the page loads and a save replaces. */
  settings: ReturnType<typeof makeSettings>;
  /** When true, `GET /api/cost-alerts` answers 500, which is how a refetch fails. */
  alertsFail: boolean;
}

/** The observations of one test, read after the interaction. */
interface CostApiObserved {
  /** Requests no mock handled; the route aborts them and every test asserts this list is empty. */
  unexpected: string[];
  /** The number of REST reads of the accrued cost. */
  accruedReads: () => number;
  /** The JSON bodies of the settings saves, in order. */
  saves: unknown[];
  /** The ids of the alerts dismissed through the API. */
  dismissed: string[];
  /** The JSON bodies of the dismiss-all requests, in order. */
  dismissedAll: { gameServerId: string; costAlertIds: string[] }[];
  /** The number of REST reads of the alert list that answered 500 because `alertsFail` was set. */
  failedAlertReads: () => number;
}

/** The default state of a test: a costing server without alerts and empty settings. */
function defaultMock(): CostApiMock {
  return {
    server: makeServer(),
    accrued: makeAccrued(),
    alerts: [],
    settings: makeSettings(),
    alertsFail: false,
  };
}

/**
 * Installs the strict API mock set and returns what the test can observe.
 *
 * Unmocked requests are aborted and recorded so a test can assert none happened, which is also what proves that no
 * cost action request was sent.
 */
async function mockApi(page: Page, culture: Culture, mock: CostApiMock): Promise<CostApiObserved> {
  const observed: CostApiObserved = {
    unexpected: [],
    accruedReads: () => accruedReads,
    saves: [],
    dismissed: [],
    dismissedAll: [],
    failedAlertReads: () => failedAlertReads,
  };
  let accruedReads = 0;
  let failedAlertReads = 0;
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
    if (path === "/api/users/me") {
      return route.fulfill({
        json: {
          primaryKey: serverId,
          nickname: "Operator",
          isBanned: false,
          isVerified: true,
          isDeleted: false,
          isVereinMember: false,
          claims: [{ identifier: "sv", value: serverId }],
          roles: [],
        },
      });
    }
    if (path === "/api/users/authentication-providers") {
      return route.fulfill({ json: { steam: true, discord: true } });
    }
    if (method === "GET" && path === `/api/game-servers/${serverId}`) {
      return route.fulfill({ json: mock.server });
    }
    if (method === "GET" && path === "/api/game-servers") {
      return route.fulfill({ json: [mock.server] });
    }
    if (method === "GET" && path === `/api/game-servers/${serverId}/accrued-cost`) {
      accruedReads += 1;
      return route.fulfill({ json: mock.accrued });
    }
    if (method === "GET" && path === `/api/game-servers/${serverId}/jobs`) {
      return route.fulfill({ json: [] });
    }
    if (method === "GET" && path === "/api/cost-alerts") {
      if (mock.alertsFail) {
        failedAlertReads += 1;
        return route.fulfill({ status: 500, json: { title: "Internal Server Error" } });
      }
      return route.fulfill({ json: mock.alerts });
    }
    if (method === "POST" && path === "/api/cost-alerts/dismissals") {
      const body = request.postDataJSON() as { gameServerId: string; costAlertIds: string[] };
      observed.dismissedAll.push(body);
      mock.alerts = mock.alerts.filter((alert) => !body.costAlertIds.includes(alert.id));
      return route.fulfill({ status: 204 });
    }
    const dismissal = /^\/api\/cost-alerts\/([^/]+)\/dismissal$/.exec(path);
    if (method === "POST" && dismissal) {
      const alertId = decodeURIComponent(dismissal[1] ?? "");
      observed.dismissed.push(alertId);
      mock.alerts = mock.alerts.filter((alert) => alert.id !== alertId);
      return route.fulfill({ status: 204 });
    }
    if (method === "GET" && path === "/api/cost-owners/mine/alert-settings") {
      return route.fulfill({ json: mock.settings });
    }
    if (method === "GET" && path === `/api/cost-owners/${costOwnerId}/alert-settings`) {
      return route.fulfill({ json: mock.settings });
    }
    if (method === "PUT" && path === `/api/cost-owners/${costOwnerId}/alert-settings`) {
      const body = request.postDataJSON() as {
        revision: number;
        thresholds: number[];
        discordEnabled: boolean;
        emailEnabled: boolean;
      };
      observed.saves.push(body);
      mock.settings = {
        ...mock.settings,
        revision: body.revision + 1,
        thresholds: [...body.thresholds].sort((left, right) => left - right),
        discordEnabled: body.discordEnabled,
        emailEnabled: body.emailEnabled,
        messageLanguage: "en",
      };
      return route.fulfill({ json: mock.settings });
    }
    if (
      method === "GET" &&
      [
        "/api/site-image-settings",
        "/api/game-servers/notifications",
        `/api/game-servers/${serverId}/operations`,
      ].includes(path)
    ) {
      return route.fulfill({ json: [] });
    }
    if (await fulfillMetricsRoute(route, method, path, serverId)) return;
    observed.unexpected.push(`${method} ${path}`);
    return route.abort();
  });
  return observed;
}

/**
 * Opens the workspace of the mocked server and waits until the shell rendered.
 *
 * @param page The page to navigate.
 * @param query An optional query string such as `?tab=activity`.
 */
async function openWorkspace(page: Page, query = "") {
  await page.goto(`command/servers/${serverId}${query}`);
  await expect(page.getByRole("heading", { level: 1, name: serverTitle })).toBeVisible();
}

/** The accrued row value wrapper of the cost strip. */
function accruedRow(page: Page) {
  return page.locator('[data-slot="accrued-cost"]');
}

/** The banner panel: the warning status region that holds the alert list. */
function banner(page: Page) {
  return page.locator(".cost-alert-banner");
}

/** The whole banner status region, which holds the alert list and the footer with the cost-stop button. */
function bannerRegion(page: Page) {
  return page.locator("section.state-panel").filter({ has: page.locator(".cost-alert-banner") });
}

/** Asserts that the document cannot scroll horizontally, allowing one pixel of rounding. */
async function expectNoHorizontalScroll(page: Page) {
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
  }));
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.innerWidth + subpixelTolerance);
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

/** Waits until the page opened the update stream, which is when a frame can be delivered. */
async function waitForStream(stream: Awaited<ReturnType<typeof mockFleetStream>>) {
  await expect.poll(() => stream.connectionCount()).toBeGreaterThan(0);
}

/** Opens the profile menu and waits until its panel shows. */
async function openProfileMenu(page: Page, culture: Culture) {
  await page.getByRole("button", { name: copy[culture].trigger }).click();
  await expect(page.getByRole("dialog", { name: copy[culture].menuName })).toBeVisible();
}

test.describe("cost alerts journey (en)", () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test("accrued cost follows the REST value and a pushed frame without another request", async ({
    page,
  }) => {
    const observed = await mockApi(page, "en", defaultMock());
    const stream = await mockFleetStream(page);

    await openWorkspace(page);
    const row = accruedRow(page);
    await expect(row).toContainText(/1\.20/);
    const asOfLine = row.locator("time").first();
    const billingLine = row.locator("time").nth(1);
    await expect(asOfLine).toHaveAttribute("datetime", "2026-11-21T10:00:00Z");
    await expect(asOfLine).toHaveText(copy.en.asOf);
    await expect(page.getByText(copy.en.sessionSoFar, { exact: true })).toBeVisible();
    await expect(billingLine).toHaveText(copy.en.billedHourNext);
    await expect(billingLine).toHaveAttribute("datetime", "2026-11-21T10:40:00Z");
    expect(observed.accruedReads()).toBe(1);

    await waitForStream(stream);
    stream.send(
      accruedCostFrame(serverId, {
        amount: 1.55,
        asOf: "2026-11-21T10:01:00Z",
        billingUnitSeconds: 3600,
        billedThrough: "2026-11-21T10:40:00Z",
      }),
    );
    await expect(row).toContainText(/1\.55/);
    await expect(asOfLine).toHaveAttribute("datetime", "2026-11-21T10:01:00Z");
    await expect(billingLine).toHaveAttribute("datetime", "2026-11-21T10:40:00Z");

    // An older frame never moves the shown amount backwards.
    stream.send(accruedCostFrame(serverId, { amount: 1.3, asOf: "2026-11-21T10:00:30Z" }));
    stream.send(accruedCostFrame(serverId, { amount: 1.6, asOf: "2026-11-21T10:02:00Z" }));
    await expect(row).toContainText(/1\.60/);
    await expect(row).not.toContainText(/1\.30/);

    // The amount steps to the next started hour in place, with the new next-hour instant and no extra REST read.
    stream.send(
      accruedCostFrame(serverId, {
        amount: 1.8,
        asOf: "2026-11-21T10:41:00Z",
        billingUnitSeconds: 3600,
        billedThrough: "2026-11-21T11:40:00Z",
      }),
    );
    await expect(row).toContainText(/1\.80/);
    await expect(billingLine).toHaveAttribute("datetime", "2026-11-21T11:40:00Z");
    await expect(billingLine).toHaveText(copy.en.billedHourNext);

    expect(observed.accruedReads()).toBe(1);
    expect(observed.unexpected).toEqual([]);
  });

  test("a pushed alert shows in the banner and menu with one toast, links to the cost-stop confirmation and dismisses", async ({
    page,
  }) => {
    const observed = await mockApi(page, "en", defaultMock());
    const stream = await mockFleetStream(page);
    const alert = makeAlert(1, 1);

    await openWorkspace(page);
    await waitForStream(stream);
    await expect(banner(page)).toHaveCount(0);

    stream.send(costAlertRaisedFrame(alert));
    const panel = banner(page);
    await expect(
      page.getByRole("heading", { level: 2, name: copy.en.bannerTitle, exact: true }),
    ).toBeVisible();
    await expect(panel).toContainText(copy.en.reached);
    await expect(panel).toContainText(/1\.04/);
    // The row does not repeat the server name; the workspace heading already names it.
    await expect(panel.locator("li")).not.toContainText(serverTitle);

    // One warning toast, and a redelivered frame for the same alert adds neither a row nor a second toast.
    await expect(page.locator(".app-toast")).toHaveCount(1);
    await expect(page.locator(".app-toast")).toContainText(`${serverTitle} reached`);
    stream.send(costAlertRaisedFrame(alert));
    await expect(panel.locator("li")).toHaveCount(1);
    await expect(page.locator(".app-toast")).toHaveCount(1);

    // The profile menu lists the same alert and the avatar badge counts the server once.
    await openProfileMenu(page, "en");
    const menuRow = page.locator("[data-cost-alert-id]");
    await expect(menuRow).toHaveCount(1);
    await expect(menuRow).toContainText(/Cost alert: .*1\.00.* reached/);
    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog", { name: copy.en.menuName })).toHaveCount(0);

    // The banner's cost-stop button opens the shared confirmation with the accrued fact and sends no action.
    await bannerRegion(page).getByRole("button", { name: copy.en.stop }).click();
    const dialog = page.locator("dialog[open]");
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(copy.en.sessionSoFar);
    await expect(dialog).toContainText(/1\.20/);
    expect(observed.unexpected).toEqual([]);
    await dialog.locator("[data-dialog-dismiss]").click();
    await expect(page.locator("dialog[open]")).toHaveCount(0);
    expect(observed.unexpected).toEqual([]);

    // Dismissal leaves only after the API accepted it; focus lands on the workspace heading.
    await panel.getByRole("button", { name: copy.en.dismiss }).click();
    await expect(banner(page)).toHaveCount(0);
    await expect(page.locator("#workspace-title")).toBeFocused();
    expect(observed.dismissed).toEqual([alert.id]);
    // The toast confirms the dismissal by name and amount and is still on screen after the banner is gone (D-17).
    const dismissedToast = page
      .locator(".app-toast")
      .filter({ hasText: `Cost alert dismissed: ${serverTitle}, ` });
    await expect(dismissedToast).toHaveCount(1);
    await expect(dismissedToast).toContainText(/1\.00/);
    // Announcements run through the persistent polite live region, and a toast has no role of its own.
    await expect(
      page
        .locator('.app-toast-region[aria-live="polite"] .app-toast')
        .filter({ hasText: `Cost alert dismissed: ${serverTitle}, ` }),
    ).toHaveCount(1);
    await expect(dismissedToast).not.toHaveAttribute("role", /.*/);
    await openProfileMenu(page, "en");
    await expect(page.locator("[data-cost-alert-id]")).toHaveCount(0);
    expect(observed.unexpected).toEqual([]);
  });

  test("gap closure: alerts leave only through the viewer's dismissal (en)", async ({ page }) => {
    const mock = defaultMock();
    const first = makeAlert(1, 1);
    const second = makeAlert(2, 2);
    const third = makeAlert(3, 5);
    const fourth = makeAlert(4, 2.5);
    mock.alerts = [second, first];
    const observed = await mockApi(page, "en", mock);
    const stream = await mockFleetStream(page);

    // The Activity tab hosts the live badge whose reconnect control the failing-refetch step uses.
    await openWorkspace(page, "?tab=activity");
    await waitForStream(stream);
    const panel = banner(page);
    await expect(panel.locator("li")).toHaveCount(2);

    // A third alert arrives by frame; all three rows show.
    stream.send(costAlertRaisedFrame(third));
    await expect(panel.locator("li")).toHaveCount(3);

    // Dismiss all sends exactly the ids of the three alerts shown, and its plural toast outlives the banner.
    await bannerRegion(page).getByRole("button", { name: "Dismiss all 3 alerts" }).click();
    await expect(banner(page)).toHaveCount(0);
    const allToast = page
      .locator(".app-toast")
      .filter({ hasText: `3 cost alerts for ${serverTitle} dismissed.` });
    await expect(allToast).toHaveCount(1);
    expect(observed.dismissedAll).toHaveLength(1);
    expect(observed.dismissedAll[0]?.gameServerId).toBe(serverId);
    expect([...(observed.dismissedAll[0]?.costAlertIds ?? [])].sort()).toEqual(
      [first.id, second.id, third.id].sort(),
    );
    expect(observed.dismissed).toEqual([]);

    // A redelivered frame for a dismissed alert brings back neither the banner nor a toast. A later accrued frame
    // proves the redelivered one was processed first, because frames are handled in order.
    const toastCount = await page.locator(".app-toast").count();
    stream.send(costAlertRaisedFrame(third));
    stream.send(
      accruedCostFrame(serverId, {
        amount: 2,
        asOf: "2026-11-21T10:05:00Z",
        billingUnitSeconds: 3600,
        billedThrough: "2026-11-21T10:40:00Z",
      }),
    );
    await expect(accruedRow(page)).toContainText(/2\.00/);
    await expect(banner(page)).toHaveCount(0);
    await expect(page.locator(".app-toast")).toHaveCount(toastCount);

    // One more alert arrives, then the connection drops and the alert list refetch fails: the banner stays.
    mock.alerts = [fourth];
    stream.send(costAlertRaisedFrame(fourth));
    await expect(panel.locator("li")).toHaveCount(1);
    await stream.closeFromServer();
    mock.alertsFail = true;
    // The paused-live recovery appears only after 3 seconds of continuous disconnection.
    const reconnect = page.locator(".live-badge").getByRole("button", { name: "Reconnect now" });
    await expect(reconnect).toBeVisible({ timeout: 10_000 });
    await reconnect.click();
    await expect.poll(() => observed.failedAlertReads()).toBeGreaterThan(0);
    await expect(panel.locator("li")).toHaveCount(1);
    await expect(panel).toContainText(/2\.50.* reached/);

    // The viewer dismisses it: the single-alert toast confirms it.
    await panel.getByRole("button", { name: /^Dismiss alert at / }).click();
    await expect(banner(page)).toHaveCount(0);
    const singleToast = page
      .locator(".app-toast")
      .filter({ hasText: `Cost alert dismissed: ${serverTitle}, ` });
    await expect(singleToast).toHaveCount(1);
    await expect(singleToast).toContainText(/2\.50/);
    expect(observed.dismissed).toEqual([fourth.id]);
    expect(observed.unexpected).toEqual([]);
  });

  test("the cost alerts page blocks invalid amounts and saves a valid one with the channel flags", async ({
    page,
  }) => {
    const observed = await mockApi(page, "en", defaultMock());
    await mockFleetStream(page);

    await page.goto("command/servers/cost-alerts");
    await expect(page.getByRole("heading", { level: 1, name: "Cost alerts" })).toBeVisible();
    await expect(page.getByText(copy.en.noAlerts, { exact: true })).toBeVisible();
    await expect(page.getByRole("status").filter({ hasText: copy.en.pristine })).toBeVisible();

    await page.getByRole("button", { name: copy.en.addAmount }).click();
    const field = page.getByLabel(copy.en.amountLabel);
    await expect(field).toBeFocused();
    await field.fill("abc");
    // The message waits for the blur; it never shows while the first character is typed.
    await expect(page.getByText(copy.en.invalidNumber)).toHaveCount(0);
    await field.blur();
    await expect(page.getByText(copy.en.invalidNumber)).toBeVisible();

    await page.getByRole("button", { name: copy.en.save }).click();
    await expect(
      page.getByRole("alert").filter({ hasText: /Fix the highlighted amount/ }),
    ).toBeVisible();
    expect(observed.saves).toEqual([]);

    await field.fill("12.5");
    await page.getByLabel(copy.en.email).check();
    await page.getByRole("button", { name: copy.en.save }).click();
    await expect(
      page.locator(".cost-alert-settings__status").getByText(copy.en.saved, { exact: true }),
    ).toBeVisible();
    expect(observed.saves).toEqual([
      {
        revision: 3,
        thresholds: [12.5],
        discordEnabled: false,
        emailEnabled: true,
        language: "en",
      },
    ]);
    expect(observed.unexpected).toEqual([]);
  });
});

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`cost alert layout (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test("strip, banner, menu rows and settings page have no horizontal page scroll", async ({
        page,
      }) => {
        const mock = defaultMock();
        mock.alerts = [makeAlert(3, 5), makeAlert(2, 2.5), makeAlert(1, 1)];
        mock.server.costAlertThresholds = [1, 2.5, 5, 10];
        const observed = await mockApi(page, culture, mock);
        await mockFleetStream(page);

        await openWorkspace(page);
        await expect(banner(page)).toBeVisible();
        await expect(accruedRow(page)).toContainText(/1[.,]20/);
        await expect(accruedRow(page)).toContainText(copy[culture].billedHourNext);
        await expectNoHorizontalScroll(page);

        await openProfileMenu(page, culture);
        await expect(page.locator("[data-cost-alert-id]")).toHaveCount(3);
        await expectNoHorizontalScroll(page);
        await page.keyboard.press("Escape");

        await page.goto("command/servers/cost-alerts");
        await expect(page.getByRole("button", { name: copy[culture].addAmount })).toBeVisible();
        await expectNoHorizontalScroll(page);
        await page.getByRole("button", { name: copy[culture].addAmount }).click();
        await expectNoHorizontalScroll(page);
        expect(observed.unexpected).toEqual([]);
      });
    });
  }

  test.describe(`cost alert accessibility (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("settings page has no axe violation when pristine, dirty and invalid", async ({
      page,
    }) => {
      const observed = await mockApi(page, culture, defaultMock());
      await mockFleetStream(page);

      await page.goto("command/servers/cost-alerts");
      await expect(page.getByText(copy[culture].noAlerts, { exact: true })).toBeVisible();
      expect(await scan(page, alertsPageContainer)).toEqual([]);

      await page.getByRole("button", { name: copy[culture].addAmount }).click();
      await page.getByLabel(copy[culture].amountLabel).fill("5");
      await expect(page.getByRole("status").filter({ hasText: /\S/ }).last()).toBeVisible();
      expect(await scan(page, alertsPageContainer)).toEqual([]);

      await page.getByLabel(copy[culture].amountLabel).fill("abc");
      await page.getByLabel(copy[culture].amountLabel).blur();
      await expect(page.getByText(copy[culture].invalidNumber)).toBeVisible();
      expect(await scan(page, alertsPageContainer)).toEqual([]);
      expect(observed.unexpected).toEqual([]);
    });

    test("read-only settings page has no axe violation", async ({ page }) => {
      const mock = defaultMock();
      mock.settings = { ...makeSettings(false), thresholds: [5, 10] };
      const observed = await mockApi(page, culture, mock);
      await mockFleetStream(page);

      await page.goto("command/servers/cost-alerts");
      await expect(page.getByText(copy[culture].readOnly, { exact: true })).toBeVisible();
      await expect(page.getByLabel(copy[culture].amountLabel)).toBeDisabled();
      expect(await scan(page, alertsPageContainer)).toEqual([]);
      expect(observed.unexpected).toEqual([]);
    });

    test("workspace with a banner and the open profile menu with alert rows have no axe violation", async ({
      page,
    }) => {
      const mock = defaultMock();
      mock.alerts = [makeAlert(2, 2.5), makeAlert(1, 1)];
      mock.server.costAlertThresholds = [1, 2.5];
      const observed = await mockApi(page, culture, mock);
      await mockFleetStream(page);

      await openWorkspace(page);
      await expect(banner(page)).toBeVisible();
      expect(await scan(page, pageContainer)).toEqual([]);

      await openProfileMenu(page, culture);
      await expect(page.locator("[data-cost-alert-id]")).toHaveCount(2);
      expect(await scan(page, profileMenuPanel)).toEqual([]);
      expect(observed.unexpected).toEqual([]);
    });
  });
}

/** The viewports of the review screenshots: a desktop column and a common phone. */
const screenViewports = [
  { width: 1280, height: 900 },
  { width: 390, height: 844 },
] as const;

/**
 * Review captures of the settings page: full-page PNGs per state, language and width, written to
 * `test-results/cost-alert-screens`. They exist for the design review, so they run only when
 * `UTF_CAPTURE_SCREENS=1` is set and are never part of the normal e2e run.
 */
test.describe("cost alert screens", () => {
  test.skip(
    process.env.UTF_CAPTURE_SCREENS !== "1",
    "Set UTF_CAPTURE_SCREENS=1 to capture screens.",
  );

  for (const culture of ["en", "de"] as const) {
    for (const viewport of screenViewports) {
      for (const state of ["pristine", "readonly"] as const) {
        test(`settings ${state} (${culture}, ${viewport.width})`, async ({ page }) => {
          await page.setViewportSize(viewport);
          const mock = defaultMock();
          mock.settings = { ...makeSettings(state === "pristine"), thresholds: [5, 10] };
          const observed = await mockApi(page, culture, mock);
          await mockFleetStream(page);

          await page.goto("command/servers/cost-alerts");
          await expect(page.getByLabel(copy[culture].amountLabel)).toBeVisible();
          await page.evaluate(() => document.fonts.ready);
          await page.screenshot({
            path: `test-results/cost-alert-screens/settings-${state}-${culture}-${viewport.width}.png`,
            fullPage: true,
          });
          expect(observed.unexpected).toEqual([]);
        });
      }

      test(`strip (${culture}, ${viewport.width})`, async ({ page }) => {
        await page.setViewportSize(viewport);
        const mock = defaultMock();
        mock.server.costAlertThresholds = [5, 10];
        const observed = await mockApi(page, culture, mock);
        await mockFleetStream(page);

        await openWorkspace(page);
        await expect(accruedRow(page)).toContainText(copy[culture].billedHourNext);
        await page.evaluate(() => document.fonts.ready);
        await page.locator("dl.cost-strip").screenshot({
          path: `test-results/cost-alert-screens/strip-${culture}-${viewport.width}.png`,
        });
        expect(observed.unexpected).toEqual([]);
      });
    }
  }
});

/**
 * Review captures of the banner and the profile menu rows: one PNG per alert count, language and width, written to
 * `test-results/cost-alert-screens`. They run only with `UTF_CAPTURE_SCREENS=1`.
 */
test.describe("cost alert banner and menu screens", () => {
  test.skip(
    process.env.UTF_CAPTURE_SCREENS !== "1",
    "Set UTF_CAPTURE_SCREENS=1 to capture screens.",
  );

  for (const culture of ["en", "de"] as const) {
    for (const viewport of screenViewports) {
      for (const count of [1, 3] as const) {
        test(`banner ${count} (${culture}, ${viewport.width})`, async ({ page }) => {
          await page.setViewportSize(viewport);
          const mock = defaultMock();
          mock.alerts = [makeAlert(3, 5), makeAlert(2, 2.5), makeAlert(1, 1)].slice(0, count);
          const observed = await mockApi(page, culture, mock);
          await mockFleetStream(page);

          await openWorkspace(page);
          await expect(banner(page)).toBeVisible();
          await page.evaluate(() => document.fonts.ready);
          await bannerRegion(page).screenshot({
            path: `test-results/cost-alert-screens/banner-${count}-${culture}-${viewport.width}.png`,
          });
          expect(observed.unexpected).toEqual([]);
        });
      }

      test(`menu rows (${culture}, ${viewport.width})`, async ({ page }) => {
        await page.setViewportSize(viewport);
        const mock = defaultMock();
        mock.alerts = [makeAlert(2, 2.5), makeAlert(1, 1)];
        const observed = await mockApi(page, culture, mock);
        await mockFleetStream(page);

        await openWorkspace(page);
        await openProfileMenu(page, culture);
        await expect(page.locator("[data-cost-alert-id]")).toHaveCount(2);
        await page.evaluate(() => document.fonts.ready);
        await page.locator(profileMenuPanel).screenshot({
          path: `test-results/cost-alert-screens/menu-${culture}-${viewport.width}.png`,
        });
        expect(observed.unexpected).toEqual([]);
      });
    }
  }
});
