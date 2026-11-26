import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Locator, type Page } from "@playwright/test";
import { mockFleetStream } from "./support/fleetStream";
import { fulfillCostRoute } from "./support/costMocks";
import { fulfillMetricsRoute } from "./support/metricsMocks";
import { seedSettingsPanels } from "./support/settingsPanels";

type Culture = "en" | "de";

const serverId = "10000000-0000-4000-8000-000000000b63";
const serverTitle = "Workspace test server";

/**
 * Layout boxes come back with fractional coordinates; an edge that ends less than one CSS pixel past the viewport
 * is a rounding artifact, not a clipped control (Phase 62 rule).
 */
const subpixelTolerance = 1;

/** The height of the command topbar the sticky header sits below. */
const commandTopbarHeight = 64;

/** The sticky header budget at 320 by 568 (UI-SPEC D-04): under one third of the viewport. */
const stickyHeaderBudget = 190;

/** The visible copy each test reads, per culture. */
const copy = {
  en: {
    navigation: "Server sections",
    sections: ["Overview", "Activity", "Settings", "Files", "Machine"],
    activity: "Activity",
    showDetails: "Show details",
    hideDetails: "Hide details",
    stale: "Not up to date",
    failureTitle: /did not finish/,
    steps: "Steps",
  },
  de: {
    navigation: "Serverbereiche",
    sections: ["Übersicht", "Aktivität", "Einstellungen", "Dateien", "Maschine"],
    activity: "Aktivität",
    showDetails: "Details anzeigen",
    hideDetails: "Details ausblenden",
    stale: "Nicht aktuell",
    failureTitle: /nicht abgeschlossen/,
    steps: "Schritte",
  },
} satisfies Record<Culture, Record<string, string | RegExp | string[]>>;

const wcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

/** The two viewports of the phase gate: the smallest supported phone and a common one. */
const viewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

/** The workspace page container every shell scan covers. */
const pageContainer = ".next-hosting-page";

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
const terminateAction = {
  id: "terminate",
  label: "Terminate",
  description: "Immediately releases every provider resource.",
  icon: "delete",
  group: "Emergency",
  order: 1000,
  enabled: true,
  unavailableReason: null,
  dangerous: true,
  confirmationText: "Release every provider resource now.",
  confirmationChallenge: serverTitle,
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

/** A server that is running and may still cost money, with the cost-stop and terminate actions. */
function makeServer(pendingJobCount = 0) {
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
    costOwnerId: serverId,
    costOwnerName: "Operations",
    createdById: serverId,
    isLaunchDisabled: false,
    isArchived: false,
    canModify: true,
    timeStampCreated: "2026-11-16T10:00:00Z",
    timeStampUpdated: "2026-11-16T10:00:00Z",
    pendingJobCount,
    deployment: runningDeployment,
    actions: [stopAction, terminateAction],
  };
}

interface JobOptions {
  index: number;
  kind?: string;
  status?: string;
  stage?: string;
  canRetry?: boolean;
  failureReason?: string | null;
  withCommand?: boolean;
}

/**
 * Builds one lifecycle job projection; the creation time falls `index` hours behind a fixed recent moment, so a
 * list of jobs has a stable newest-first order.
 */
function makeJob(options: JobOptions) {
  const { index } = options;
  const created = new Date(Date.UTC(2026, 10, 19, 20, 0, 0) - index * 3_600_000).toISOString();
  const completed = new Date(Date.parse(created) + 120_000).toISOString();
  const status = options.status ?? "Succeeded";
  const terminal = ["Succeeded", "Failed", "Skipped", "CancelledByCostStop"].includes(status);
  const jobId = `20000000-0000-4000-8000-${String(index).padStart(12, "0")}`;
  return {
    primaryKey: jobId,
    gameServerId: serverId,
    deploymentId: null,
    kind: options.kind ?? "Start",
    actionId: (options.kind ?? "Start").toLowerCase(),
    status,
    stage: options.stage ?? (terminal ? "completed" : "provider-creating"),
    progressPercent: terminal ? 100 : 20,
    attemptCount: 1,
    failureCode: status === "Failed" ? "worker-error" : null,
    failureReason: options.failureReason ?? null,
    requestedByName: "Operator",
    canRetry: options.canRetry ?? false,
    canSkip: false,
    timeStampCreated: created,
    timeStampUpdated: completed,
    timeStampStarted: created,
    timeStampCompleted: terminal ? completed : null,
    commands:
      options.withCommand === false
        ? []
        : [
            {
              primaryKey: `30000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
              sequence: 1,
              actionId: "install",
              status: status === "Failed" ? "Failed" : "Succeeded",
              stage: "completed",
              failureCode: null,
              failureReason: null,
              diagnosticOutput: `Installed game files for job ${index}.`,
              timeStampCreated: created,
              timeStampStarted: created,
              timeStampCompleted: completed,
            },
          ],
  };
}

/** Thirty succeeded jobs, each with one command: enough entries to scroll the Activity section. */
function longJobHistory() {
  return Array.from({ length: 30 }, (_, index) => makeJob({ index: index + 1 }));
}

/** What the mocked API returns for one test. */
interface WorkspaceMock {
  server: object;
  jobs: object[];
}

/**
 * Installs the strict API mock set and returns the requests that no mock handled.
 *
 * Unmocked requests are aborted and recorded so a test can assert none happened.
 */
async function mockApi(page: Page, culture: Culture, mock: WorkspaceMock): Promise<string[]> {
  const unexpected: string[] = [];
  await page.addInitScript((value) => localStorage.setItem("UtfCulture", value), culture);
  await seedSettingsPanels(page);
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
    if (method === "GET" && path === `/api/game-servers/${serverId}/jobs`) {
      return route.fulfill({ json: mock.jobs });
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
    if (await fulfillCostRoute(route, method, path, serverId)) return;
    unexpected.push(`${method} ${path}`);
    return route.abort();
  });
  return unexpected;
}

/** The five section links, in document order. */
function sectionLinks(page: Page, culture: Culture) {
  return page.getByRole("navigation", { name: copy[culture].navigation }).getByRole("link");
}

/** Opens the workspace of the mocked server and waits until the shell rendered. */
async function openWorkspace(page: Page, path = "") {
  await page.goto(`command/servers/${serverId}${path}`);
  await expect(page.getByRole("heading", { level: 1, name: serverTitle })).toBeVisible();
}

/**
 * Waits until every web font the page requested has settled and reports whether the label font is active.
 *
 * The page takes its fonts from Google Fonts. The section links are set in Barlow Condensed, which is much narrower
 * than the system fallback font, so the number of rows they wrap onto depends on whether that font arrived. Measuring
 * before the font settled is a race; this settles it first.
 *
 * @returns True when Barlow Condensed is loaded, false when the page runs on the fallback font.
 */
async function settleLabelFont(page: Page): Promise<boolean> {
  return page.evaluate(async () => {
    await document.fonts.ready;
    return [...document.fonts].some(
      (face) => face.family.includes("Barlow Condensed") && face.status === "loaded",
    );
  });
}

/**
 * Asserts that the five section links lie fully inside the viewport horizontally on at most two rows.
 *
 * Tops are measured in document coordinates in one evaluation, so a scroll between two reads cannot split a row.
 * Containment in the viewport is asserted with any font. The two-row limit is asserted only while the real label
 * font is active: without network access the page falls back to a wider system font, German wraps onto a third
 * row at 320 px, and the run records an annotation instead of measuring a layout nobody ships.
 */
async function expectSectionLinksFit(page: Page, culture: Culture) {
  const viewport = page.viewportSize();
  expect(viewport).not.toBeNull();
  const links = sectionLinks(page, culture);
  await expect(links).toHaveCount(5);
  await expect(links).toHaveText(copy[culture].sections);
  const labelFontActive = await settleLabelFont(page);
  if (!labelFontActive) {
    test.info().annotations.push({
      type: "label-font-fallback",
      description: "Barlow Condensed did not load; the two-row limit was not asserted.",
    });
  }
  const boxes = await links.evaluateAll((elements) =>
    elements.map((element) => {
      const rect = element.getBoundingClientRect();
      return { left: rect.left, right: rect.right, top: Math.round(rect.top + window.scrollY) };
    }),
  );
  boxes.forEach((box, index) => {
    expect(box.left, `section link #${index} left edge`).toBeGreaterThanOrEqual(0);
    expect(box.right, `section link #${index} right edge`).toBeLessThanOrEqual(
      viewport!.width + subpixelTolerance,
    );
  });
  if (labelFontActive) {
    expect(
      new Set(boxes.map((box) => box.top)).size,
      `section link rows: ${JSON.stringify(boxes)}`,
    ).toBeLessThanOrEqual(2);
  }
}

/** Asserts that the document cannot scroll horizontally, allowing one pixel of rounding. */
async function expectNoHorizontalScroll(page: Page) {
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + subpixelTolerance);
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

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`workspace layout (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test("keeps five section links reachable, no sideways scroll and a sticky header", async ({
        page,
      }) => {
        const unexpected = await mockApi(page, culture, {
          server: makeServer(),
          jobs: longJobHistory(),
        });
        await mockFleetStream(page);

        await openWorkspace(page);
        await expectSectionLinksFit(page, culture);
        await expectNoHorizontalScroll(page);

        // The Activity section is long; the header must stay below the command topbar after scrolling it.
        await sectionLinks(page, culture).nth(1).click();
        await expect(page).toHaveURL(/[?&]tab=activity(&|$)/);
        await expect(
          page.getByRole("heading", { level: 2, name: copy[culture].activity, exact: true }),
        ).toBeVisible();
        await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
        await expect
          .poll(() => page.evaluate(() => window.scrollY))
          .toBeGreaterThan(commandTopbarHeight);
        const header = page
          .locator("header")
          .filter({ has: page.getByRole("heading", { level: 1 }) });
        await expect(header).toBeVisible();
        const box = await header.boundingBox();
        expect(box).not.toBeNull();
        expect(Math.abs(box!.y - commandTopbarHeight)).toBeLessThanOrEqual(subpixelTolerance);
        expect(box!.height).toBeLessThanOrEqual(stickyHeaderBudget);
        await expectNoHorizontalScroll(page);

        // Tab order follows the document order of the section links.
        await sectionLinks(page, culture).first().focus();
        for (const name of copy[culture].sections.slice(1)) {
          await page.keyboard.press("Tab");
          await expect(page.locator(":focus")).toHaveText(name);
        }
        expect(unexpected).toEqual([]);
      });
    });
  }

  test.describe(`workspace accessibility (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("Overview has no axe violation", async ({ page }) => {
      const unexpected = await mockApi(page, culture, {
        server: makeServer(),
        jobs: longJobHistory(),
      });
      await mockFleetStream(page);

      await openWorkspace(page);
      await expectSectionLinksFit(page, culture);
      expect(await scan(page, pageContainer)).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("Activity with an expanded entry and the stale live badge has no axe violation", async ({
      page,
    }) => {
      const unexpected = await mockApi(page, culture, {
        server: makeServer(),
        jobs: longJobHistory(),
      });
      const stream = await mockFleetStream(page);

      await openWorkspace(page, "?tab=activity");
      await expect.poll(() => stream.connectionCount()).toBeGreaterThan(0);
      await page.getByRole("button", { name: copy[culture].showDetails }).first().click();
      await expect(page.getByRole("button", { name: copy[culture].hideDetails })).toHaveCount(1);

      // The stale state appears only after 3 seconds of continuous disconnection.
      await stream.closeFromServer();
      await expect(
        page.locator(".live-badge").getByText(copy[culture].stale, { exact: true }),
      ).toBeVisible({ timeout: 10_000 });
      await expectNoHorizontalScroll(page);
      expect(await scan(page, pageContainer)).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("failure panel has no axe violation", async ({ page }) => {
      const unexpected = await mockApi(page, culture, {
        server: makeServer(),
        jobs: [
          makeJob({
            index: 1,
            status: "Failed",
            canRetry: true,
            failureReason: "The worker stopped answering.",
          }),
          ...longJobHistory().slice(1),
        ],
      });
      await mockFleetStream(page);

      await openWorkspace(page);
      const panel = page.locator(".job-failure");
      await expect(panel.getByRole("heading", { level: 2 })).toHaveText(copy[culture].failureTitle);
      await expectNoHorizontalScroll(page);
      expect(await scan(page, ".job-failure")).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("progress strip has no axe violation", async ({ page }) => {
      const unexpected = await mockApi(page, culture, {
        server: makeServer(1),
        jobs: [
          makeJob({ index: 1, status: "Running", withCommand: false }),
          ...longJobHistory().slice(1),
        ],
      });
      await mockFleetStream(page);

      await openWorkspace(page);
      const strip = page.locator(".job-progress");
      await expect(strip).toBeVisible();
      await expect(strip.getByRole("list", { name: copy[culture].steps })).toBeVisible();
      await expectNoHorizontalScroll(page);
      expect(await scan(page, ".job-progress")).toEqual([]);
      expect(unexpected).toEqual([]);
    });
  });
}

/** The German cost-stop label, the longest button label of the phase. */
const germanStopLabel = "Herunterfahren und Abrechnung beenden";

/** What one label measurement reads off a button: the box, the content width and the viewport edges. */
interface LabelMeasurement {
  scrollWidth: number;
  clientWidth: number;
  left: number;
  right: number;
  viewportWidth: number;
}

/**
 * Measures the content width and the bounding box of one button in a single evaluation.
 *
 * @param button The locator of the button to measure.
 * @returns The scroll and client width of the button and its horizontal edges next to the viewport width.
 */
function measureLabel(button: Locator): Promise<LabelMeasurement> {
  return button.evaluate((element) => {
    const rect = element.getBoundingClientRect();
    return {
      scrollWidth: element.scrollWidth,
      clientWidth: element.clientWidth,
      left: rect.left,
      right: rect.right,
      viewportWidth: document.documentElement.clientWidth,
    };
  });
}

/**
 * Asserts that a label stays inside its button and the button stays inside the viewport.
 *
 * @param measurement The measurement of the button.
 * @param name What the button is called in the failure message.
 */
function expectLabelInside(measurement: LabelMeasurement, name: string) {
  expect(
    measurement.scrollWidth,
    `${name} scrollWidth ${measurement.scrollWidth} against clientWidth ${measurement.clientWidth}`,
  ).toBeLessThanOrEqual(measurement.clientWidth + subpixelTolerance);
  expect(measurement.left, `${name} left edge`).toBeGreaterThanOrEqual(0);
  expect(measurement.right, `${name} right edge`).toBeLessThanOrEqual(
    measurement.viewportWidth + subpixelTolerance,
  );
}

test.describe("workspace header on the smallest phone (de, 320x568)", () => {
  test.use({ viewport: { width: 320, height: 568 } });

  test("the German cost-stop label wraps inside the header button and the confirmation button", async ({
    page,
  }) => {
    const unexpected = await mockApi(page, "de", { server: makeServer(), jobs: [] });
    await mockFleetStream(page);

    await openWorkspace(page);
    const headerButton = page
      .locator(".workspace-header__action")
      .getByRole("button", { name: germanStopLabel });
    await expect(headerButton).toBeVisible();
    expectLabelInside(await measureLabel(headerButton), "header button");

    await headerButton.click();
    const dialog = page.locator("dialog[open]");
    await expect(dialog).toBeVisible();
    const confirmButton = dialog.getByRole("button", { name: germanStopLabel });
    await expect(confirmButton).toBeVisible();
    expectLabelInside(await measureLabel(confirmButton), "confirmation button");

    expect(unexpected).toEqual([]);
  });
});
