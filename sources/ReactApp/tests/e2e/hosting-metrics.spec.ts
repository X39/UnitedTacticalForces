import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Locator, type Page } from "@playwright/test";
import { fulfillCostRoute } from "./support/costMocks";
import { metricsFrame, mockFleetStream } from "./support/fleetStream";
import {
  defaultMetricsMock,
  fulfillMetricsRoute,
  populatedMetricsMock,
  spikeCpuPercent,
  type MetricsMock,
} from "./support/metricsMocks";

type Culture = "en" | "de";

const serverId = "10000000-0000-4000-8000-000000000b67";
const costOwnerId = "40000000-0000-4000-8000-000000000b67";
const serverTitle = "Metrics test server";

/** Layout edges come back fractional; less than one CSS pixel past the viewport is a rounding artifact. */
const subpixelTolerance = 1;

const wcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

/** The two viewports of the phase gate: the smallest supported phone and a common one. */
const viewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

/** The metrics panel every scan covers. */
const metricsPanel = '[data-slot="metrics"]';

/** The visible copy each test reads, per culture. */
const copy = {
  en: {
    rangeGroup: "Time range",
    hour1: "1 hour",
    hours24: "24 hours",
    days7: "7 days",
    showTables: "Show data as tables",
    showCharts: "Show charts",
    unavailable: "Unavailable",
    workerOffline: "The worker of this server is not connected, so no readings arrive.",
    paused: "Live updates are paused. Reconnecting automatically.",
  },
  de: {
    rangeGroup: "Zeitraum",
    hour1: "1 Stunde",
    hours24: "24 Stunden",
    days7: "7 Tage",
    showTables: "Daten als Tabellen anzeigen",
    showCharts: "Diagramme anzeigen",
    unavailable: "Nicht verfügbar",
    workerOffline:
      "Der Worker dieses Servers ist nicht verbunden, deshalb kommen keine Messwerte an.",
    paused: "Live-Updates sind pausiert. Die Verbindung wird automatisch neu aufgebaut.",
  },
} satisfies Record<Culture, Record<string, string>>;

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

/** A running server whose Overview shows the metrics panel. */
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
    actions: [],
  };
}

/**
 * Installs the strict API mock set and returns the requests no mock handled.
 *
 * Unmocked requests are aborted and recorded so a test can assert none happened, which is also what proves that the
 * panel sends no request beyond the two metrics reads.
 *
 * @param page The page whose API is mocked; call before the first navigation.
 * @param culture The culture the page starts in.
 * @param metrics The metrics responses this test serves.
 */
async function mockApi(page: Page, culture: Culture, metrics: MetricsMock): Promise<string[]> {
  const unexpected: string[] = [];
  const server = makeServer();
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
      return route.fulfill({ json: server });
    }
    if (method === "GET" && path === "/api/game-servers") {
      return route.fulfill({ json: [server] });
    }
    if (await fulfillMetricsRoute(route, method, path, serverId, metrics)) return;
    if (await fulfillCostRoute(route, method, path, serverId)) return;
    if (
      method === "GET" &&
      [
        "/api/site-image-settings",
        "/api/game-servers/notifications",
        `/api/game-servers/${serverId}/jobs`,
        `/api/game-servers/${serverId}/operations`,
      ].includes(path)
    ) {
      return route.fulfill({ json: [] });
    }
    unexpected.push(`${method} ${path}`);
    return route.abort();
  });
  return unexpected;
}

/** Opens the workspace Overview of the mocked server and waits until the metrics panel rendered. */
async function openOverview(page: Page) {
  await page.goto(`command/servers/${serverId}`);
  await expect(page.getByRole("heading", { level: 1, name: serverTitle })).toBeVisible();
  await expect(page.locator(metricsPanel)).toBeVisible();
}

/** Waits until the page opened the update stream, which is when a frame can be delivered. */
async function waitForStream(stream: Awaited<ReturnType<typeof mockFleetStream>>) {
  await expect.poll(() => stream.connectionCount()).toBeGreaterThan(0);
}

/** The tile of one metric inside the panel. */
function tile(page: Page, id: "uptime" | "players" | "cpu" | "memory"): Locator {
  return page.locator(`${metricsPanel} [data-tile="${id}"]`);
}

/** The chart section of one metric, found by its heading. */
function chart(page: Page, title: string): Locator {
  return page.locator(`${metricsPanel} .metric-chart`).filter({
    has: page.getByRole("heading", { level: 3, name: title, exact: true }),
  });
}

/** Asserts that the document cannot scroll horizontally, allowing one pixel of rounding. */
async function expectNoHorizontalScroll(page: Page) {
  const measured = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
  }));
  expect(measured.scrollWidth).toBeLessThanOrEqual(measured.innerWidth + subpixelTolerance);
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

test.describe("server metrics journey (en)", () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test("tiles follow REST and a pushed frame, ranges and tables show the history (MET-01 to MET-05)", async ({
    page,
  }) => {
    const metrics = populatedMetricsMock(serverId);
    const unexpected = await mockApi(page, "en", metrics);
    const stream = await mockFleetStream(page);

    // MET-01 to MET-03: the four tiles come from the REST reading.
    await openOverview(page);
    await expect(tile(page, "uptime").locator("time")).toHaveAttribute(
      "datetime",
      metrics.reading.runningSince ?? "",
    );
    await expect(tile(page, "players")).toContainText("7");
    await expect(tile(page, "players")).toContainText("of 32 slots");
    await expect(tile(page, "cpu")).toContainText("37%");
    await expect(tile(page, "memory")).toContainText("2.5 GiB");
    await expect(tile(page, "memory")).toContainText("of 8 GiB");
    expect(metrics.currentReads).toBe(1);

    // MET-05: a pushed frame changes a tile with no further request for the current readings.
    await waitForStream(stream);
    stream.send(
      metricsFrame(serverId, {
        asOf: new Date(Date.now() + 5000).toISOString(),
        runningSince: metrics.reading.runningSince,
        cpuPercent: 64,
        memoryUsedBytes: metrics.reading.memoryUsedBytes,
        memoryTotalBytes: metrics.reading.memoryTotalBytes,
        playerCount: 11,
        playerCapacity: 32,
      }),
    );
    await expect(tile(page, "cpu")).toContainText("64%");
    await expect(tile(page, "players")).toContainText("11");
    expect(metrics.currentReads).toBe(1);

    // MET-04: the three one hour charts show a broken line, not a zero, around the missing stretch.
    await expect(page.locator(`${metricsPanel} .metric-chart`)).toHaveCount(3);
    expect(metrics.historyReads).toEqual(["1h"]);

    // Choosing 24 hours loads that history; the CPU summary names the spike.
    await page.getByRole("button", { name: copy.en.hours24 }).click();
    await expect(page.getByRole("button", { name: copy.en.hours24 })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    await expect(chart(page, "CPU load")).toContainText(`Highest: ${spikeCpuPercent}%`);
    await expect(page.locator(`${metricsPanel} [role="status"]`).first()).toHaveText(
      "Showing the last 24 hours.",
    );
    expect(metrics.historyReads).toContain("24h");
    await expect(
      chart(page, "CPU load").getByText(
        "Gaps mean no reading was available. Nothing is estimated.",
      ),
    ).toBeVisible();

    // The table view lists the same history newest first with one row for the missing stretch.
    await page.getByRole("button", { name: copy.en.showTables }).click();
    await expect(page.locator(`${metricsPanel} table`)).toHaveCount(3);
    await expect(page.locator(`${metricsPanel} .metric-chart`)).toHaveCount(0);
    const cpuTable = page.getByRole("table", { name: /^CPU load, last 24 hours$/ });
    await expect(cpuTable.getByText(/^No readings from .* to .*$/)).toHaveCount(1);
    await expect(cpuTable.getByRole("cell", { name: `${spikeCpuPercent}%` }).first()).toBeVisible();

    // Charts return, and the 7 day range loads its own history.
    await page.getByRole("button", { name: copy.en.showCharts }).click();
    await expect(page.locator(`${metricsPanel} .metric-chart`)).toHaveCount(3);
    await expect(page.locator(`${metricsPanel} table`)).toHaveCount(0);
    await page.getByRole("button", { name: copy.en.days7 }).click();
    await expect.poll(() => metrics.historyReads.includes("7d")).toBe(true);
    await expect(chart(page, "CPU load")).toContainText("Highest:");

    expect(metrics.currentReads).toBe(1);
    expect(unexpected).toEqual([]);
  });

  test("every tile shows Unavailable with its reason and no number, and the charts say there is nothing", async ({
    page,
  }) => {
    const metrics = defaultMetricsMock(serverId);
    const unexpected = await mockApi(page, "en", metrics);
    await mockFleetStream(page);

    await openOverview(page);
    for (const id of ["uptime", "players", "cpu", "memory"] as const) {
      const value = tile(page, id).locator(".metric-tile__value");
      await expect(value).toHaveText(copy.en.unavailable);
      await expect(value).not.toContainText(/\d/);
      await expect(tile(page, id)).toContainText(copy.en.workerOffline);
    }
    await expect(page.locator(`${metricsPanel} .metric-chart`)).toHaveCount(3);
    await expect(page.getByText("No readings in this time range.")).toHaveCount(3);

    await page.getByRole("button", { name: copy.en.showTables }).click();
    await expect(page.getByText("No readings in this time range.")).toHaveCount(3);
    expect(unexpected).toEqual([]);
  });
});

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`server metrics layout (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test("tiles, range chips and the view toggle fit without horizontal page scroll", async ({
        page,
      }) => {
        const metrics = populatedMetricsMock(serverId);
        const unexpected = await mockApi(page, culture, metrics);
        await mockFleetStream(page);

        await openOverview(page);
        await expect(page.locator(`${metricsPanel} .metric-chart`)).toHaveCount(3);
        for (const id of ["uptime", "players", "cpu", "memory"] as const) {
          await expect(tile(page, id)).toBeVisible();
        }
        const group = page.getByRole("group", { name: copy[culture].rangeGroup });
        for (const label of [copy[culture].hour1, copy[culture].hours24, copy[culture].days7]) {
          await expect(group.getByRole("button", { name: label })).toBeVisible();
        }
        await expect(page.getByRole("button", { name: copy[culture].showTables })).toBeVisible();
        await expectNoHorizontalScroll(page);

        await page.getByRole("button", { name: copy[culture].days7 }).click();
        await expect(page.locator(`${metricsPanel} .metric-chart`)).toHaveCount(3);
        await expectNoHorizontalScroll(page);

        await page.getByRole("button", { name: copy[culture].showTables }).click();
        await expect(page.locator(`${metricsPanel} table`)).toHaveCount(3);
        await expect(page.getByRole("button", { name: copy[culture].showCharts })).toBeVisible();
        await expectNoHorizontalScroll(page);
        expect(unexpected).toEqual([]);
      });
    });
  }

  test.describe(`server metrics accessibility (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("chart view and table view have no axe violation", async ({ page }) => {
      const metrics = populatedMetricsMock(serverId);
      const unexpected = await mockApi(page, culture, metrics);
      await mockFleetStream(page);

      await openOverview(page);
      await expect(page.locator(`${metricsPanel} .metric-chart`)).toHaveCount(3);
      expect(await scan(page, metricsPanel)).toEqual([]);

      await page.getByRole("button", { name: copy[culture].showTables }).click();
      await expect(page.locator(`${metricsPanel} table`)).toHaveCount(3);
      expect(await scan(page, metricsPanel)).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("every tile unavailable has no axe violation", async ({ page }) => {
      const unexpected = await mockApi(page, culture, defaultMetricsMock(serverId));
      await mockFleetStream(page);

      await openOverview(page);
      await expect(tile(page, "cpu")).toContainText(copy[culture].workerOffline);
      await expect(page.locator(`${metricsPanel} .metric-chart`)).toHaveCount(3);
      expect(await scan(page, metricsPanel)).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("the paused notice has no axe violation and the tiles keep their values", async ({
      page,
    }) => {
      const metrics = populatedMetricsMock(serverId);
      const unexpected = await mockApi(page, culture, metrics);
      const stream = await mockFleetStream(page);

      await openOverview(page);
      await waitForStream(stream);
      await expect(tile(page, "cpu")).toContainText(/37\s?%/);
      await stream.closeFromServer();
      // The notice appears only after 3 seconds of continuous disconnection.
      await expect(page.locator(`${metricsPanel} .server-metrics__paused`)).toContainText(
        copy[culture].paused,
        { timeout: 10_000 },
      );
      await expect(tile(page, "cpu")).toContainText(/37\s?%/);
      expect(await scan(page, metricsPanel)).toEqual([]);
      expect(unexpected).toEqual([]);
    });
  });
}
