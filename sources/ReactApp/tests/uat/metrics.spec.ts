import { expect, test, type Page } from "@playwright/test";
import {
  evidence,
  fleetCard,
  metricTile,
  metricTileIds,
  metricsPanel,
  openFleet,
  openServer,
  uatAutomationTitles,
  uatTitles,
} from "./support/uat";

/** The tile sentence of a server whose game is not running. */
const notRunning = "The game is not running, so there is nothing to measure.";

/** The tile sentence of a running server without a connected worker. */
const workerOffline = "The worker of this server is not connected, so no readings arrive.";

/** How long the emulated stop may take until the deployment is reported as stopped. */
const stopWait = 6 * 60_000;

/** The automation scenario titles, which other specs start and stop and which 67-8 therefore ignores. */
const automationTitles: readonly string[] = Object.values(uatAutomationTitles);

/**
 * The titles of the seeded Phase 66 and 67 UAT cards in the fleet section whose heading matches the pattern. The
 * automation scenarios are left out: their states change while the automation spec runs.
 *
 * @param page The page that shows the fleet.
 * @param heading The section heading pattern.
 * @returns The sorted titles.
 */
async function sectionTitles(page: Page, heading: RegExp): Promise<string[]> {
  const section = page
    .locator("section.fleet-section")
    .filter({ has: page.getByRole("heading", { level: 2, name: heading }) });
  const titles = await section.locator("h3.fleet-card__title").allTextContents();
  return titles
    .map((title) => title.trim())
    .filter((title) => title.startsWith("UAT ") && !automationTitles.includes(title))
    .sort();
}

/**
 * The visible text of all four tiles, which changes whenever a live sample arrives.
 *
 * @param page The page that shows the workspace.
 * @returns The tile texts joined.
 */
async function tileSnapshot(page: Page): Promise<string> {
  const texts = await Promise.all(metricTileIds.map((id) => metricTile(page, id).innerText()));
  return texts.map((text) => text.replace(/\s+/g, " ").trim()).join(" | ");
}

test.describe.serial("67-8 and 67-7 server metrics on the seeded stack", () => {
  test("67-8 only the intentionally off servers are off; worker and not-running sentences @fresh", async ({
    page,
  }) => {
    await openFleet(page);
    const off = await sectionTitles(page, /^Off \(\d+\)$/);
    const active = await sectionTitles(page, /^Active \(\d+\)$/);
    evidence(`Off section: ${JSON.stringify(off)}; Active section: ${JSON.stringify(active)}`);
    expect(off).toEqual([uatTitles.off, uatTitles.terminated].sort());
    expect(active).toContain(uatTitles.running);
    expect(active).toContain(uatTitles.costing);
    expect(active).toContain(uatTitles.metrics);
    await expect(fleetCard(page, uatTitles.running).locator(".server-state-badge")).toHaveText(
      "Running",
    );

    // UAT 2 runs without a worker: every tile says so and shows no number.
    await openServer(page, uatTitles.running);
    await expect(metricsPanel(page)).toBeVisible();
    for (const id of metricTileIds) {
      await expect(metricTile(page, id)).toContainText(workerOffline);
      await expect(metricTile(page, id).locator(".metric-tile__value")).toHaveText("Unavailable");
    }
    evidence(`${uatTitles.running}: all 4 tiles read Unavailable with the worker sentence`);

    // UAT 1 has never run: every tile says the game is not running and the charts are empty.
    await openServer(page, uatTitles.off);
    await expect(metricsPanel(page)).toBeVisible();
    for (const id of metricTileIds) {
      await expect(metricTile(page, id)).toContainText(notRunning);
      await expect(metricTile(page, id).locator(".metric-tile__value")).toHaveText("Unavailable");
    }
    await expect(metricsPanel(page).locator(".metric-chart")).toHaveCount(3);
    await expect(metricsPanel(page).getByText("No readings in this time range.")).toHaveCount(3);
    evidence(
      `${uatTitles.off}: 4 tiles not-running sentence, 3 charts 'No readings in this time range.'`,
    );
  });

  test("67-7 UAT 10 runs with live samples, then the stop leaves every tile Unavailable @fresh", async ({
    page,
  }) => {
    await openServer(page, uatTitles.metrics);
    await expect(page.locator(".server-state-badge").first()).toHaveText("Running");
    await expect(metricsPanel(page)).toBeVisible();
    await expect(metricTile(page, "cpu").locator(".metric-tile__value")).toHaveText(/\d+\s?%/);
    await expect(metricTile(page, "uptime").locator("time")).toHaveCount(1);

    // Live samples arrive without a reload.
    const before = await tileSnapshot(page);
    await expect
      .poll(() => tileSnapshot(page), { timeout: 90_000, intervals: [5_000] })
      .not.toBe(before);
    evidence(`live sample changed tiles: "${before}" -> "${await tileSnapshot(page)}"`);

    // History: each range draws three charts with a line, the tables view works, and a reload keeps the history.
    await expectHistoryInEveryRange(page);
    await page.reload();
    await expect(page.getByRole("heading", { level: 1, name: uatTitles.metrics })).toBeVisible();
    await expect(metricsPanel(page).locator(".metric-chart__line").first()).toBeVisible();
    evidence("after reload the 1 hour charts still draw the retained history");

    // Shut down and stop billing, confirmed, without reloading.
    await page.getByRole("button", { name: "Shut down and stop billing" }).first().click();
    const dialog = page.locator("dialog[open]");
    await expect(dialog).toBeVisible();
    // The stop asks to type the server name before it can be confirmed.
    const challenge = dialog.getByRole("textbox", { name: /^Type .* to continue$/ });
    if ((await challenge.count()) > 0) await challenge.fill(uatTitles.metrics);
    await dialog.locator("button[type=submit]").click();
    await expect(dialog).toHaveCount(0);

    // Once the stop completes every tile reads Unavailable with the not-running sentence and no number.
    for (const id of metricTileIds) {
      await expect(metricTile(page, id)).toContainText(notRunning, { timeout: stopWait });
      const value = metricTile(page, id).locator(".metric-tile__value");
      await expect(value).toHaveText("Unavailable");
      await expect(value).not.toContainText(/\d/);
    }
    await expect(metricTile(page, "uptime").locator("time")).toHaveCount(0);
    evidence(`after stop: ${await tileSnapshot(page)}`);
    evidence(
      `state badge after stop: ${await page.locator(".server-state-badge").first().innerText()}`,
    );

    // The emulator sessions are isolated: stopping UAT 10 leaves every other seeded server where it was.
    await openFleet(page);
    const off = await sectionTitles(page, /^Off \(\d+\)$/);
    evidence(`Off section after the stop: ${JSON.stringify(off)}`);
    expect(off).toEqual([uatTitles.off, uatTitles.terminated, uatTitles.metrics].sort());
    expect(await sectionTitles(page, /^Active \(\d+\)$/)).toContain(uatTitles.running);
  });
});

test.describe("67-10 metrics history after a restart", () => {
  test("67-10 UAT 10 keeps its history after the API restarts @restarted", async ({ page }) => {
    await openServer(page, uatTitles.metrics);
    await expect(metricsPanel(page)).toBeVisible();
    await expectHistoryInEveryRange(page);
  });
});

/**
 * Checks every time range: three charts that draw a line, then the same history as three tables.
 *
 * @param page The page that shows the workspace of a server with retained history.
 */
async function expectHistoryInEveryRange(page: Page) {
  const panel = metricsPanel(page);
  const ranges = panel.getByRole("group", { name: "Time range" });
  for (const label of ["24 hours", "7 days", "1 hour"]) {
    const chip = ranges.getByRole("button", { name: label, exact: true });
    await chip.click();
    await expect(chip).toHaveAttribute("aria-pressed", "true");
    await expect(panel.locator(".server-metrics__views")).toHaveAttribute("aria-busy", "false");
    await expect(panel.locator(".metric-chart")).toHaveCount(3);
    await expect(panel.locator(".metric-chart__line").first()).toBeVisible();
    evidence(`${label}: ${await panel.locator(".metric-chart__line").count()} chart line runs`);
  }
  await panel.getByRole("button", { name: "Show data as tables" }).click();
  await expect(panel.locator("table.metric-table__table")).toHaveCount(3);
  await expect(panel.locator("td.metric-table__value").first()).toBeVisible();
  evidence(`tables view: ${await panel.locator("td.metric-table__value").count()} value cells`);
  await panel.getByRole("button", { name: "Show charts" }).click();
  await expect(panel.locator(".metric-chart")).toHaveCount(3);
}
