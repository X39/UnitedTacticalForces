import { expect, test } from "@playwright/test";
import { fleetAction, fleetServer } from "../../helpers/fleetFixtures";
import { installApp, jsonHandler } from "../support/mockApi";

/**
 * Target size of the icon-only shared controls in a real browser (Phase 69, FND-06, WCAG 2.2 target size).
 *
 * jsdom resolves neither media queries nor `var()`, so the rendered boxes are measured here: the toast dismiss control
 * and the icon-only clear control of the shared search field are at least 24 CSS pixels on desktop and at least 44 at
 * or below 800 CSS pixels (the chromium project is 1280 wide, both phone projects are narrower than 800).
 */

/** The smallest target WCAG 2.2 AA allows, in CSS pixels. */
const minimumTarget = 24;

/** The touch height of every control at or below 800 CSS pixels, in CSS pixels. */
const touchTarget = 44;

/**
 * Builds the single off server whose start the test confirms.
 *
 * @returns A fresh server in the shape of `GET /api/game-servers`.
 */
function offServer() {
  return fleetServer({
    index: 1,
    title: "Bravo Training",
    actions: [fleetAction("start", { costDescription: "Hourly provider cost starts." })],
  });
}

test("the toast dismiss and the search clear control meet the target size", async ({ page }) => {
  const server = offServer();
  const session = await installApp(page, {
    culture: "en",
    stream: true,
    handlers: [
      jsonHandler("GET", "/api/game-servers", () => [offServer()]),
      jsonHandler("GET", `/api/game-servers/${server.primaryKey}`, () => offServer()),
      jsonHandler("POST", `/api/game-servers/${server.primaryKey}/actions/start`, () => ({
        primaryKey: "20000000-0000-4000-8000-0000000000f1",
        gameServerId: server.primaryKey,
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
      })),
    ],
  });
  await page.goto("command/servers");
  await expect(page.locator("[data-server-card]")).toHaveCount(1);

  const width = page.viewportSize()?.width ?? 0;
  const expected = width <= 800 ? touchTarget : minimumTarget;

  // The search clear control only exists while the field holds a value.
  await page.getByRole("searchbox").fill("bravo");
  const clear = page.locator(".search-control__clear");
  await expect(clear).toBeVisible();
  const clearBox = await clear.boundingBox();
  expect(clearBox, "clear control has a layout box").not.toBeNull();
  expect(clearBox!.width, "clear control width").toBeGreaterThanOrEqual(expected - 0.5);
  expect(clearBox!.height, "clear control height").toBeGreaterThanOrEqual(expected - 0.5);
  await page.getByRole("searchbox").fill("");

  await page.locator(".fleet-card__actions button").first().click();
  const dialog = page.locator("dialog[open]");
  await expect(dialog).toBeVisible();
  await dialog.locator("button[type=submit]").click();

  const dismiss = page.locator(".app-toast__dismiss");
  await expect(dismiss).toBeVisible();
  const box = await dismiss.boundingBox();
  expect(box, "toast dismiss has a layout box").not.toBeNull();
  expect(box!.width, "toast dismiss width").toBeGreaterThanOrEqual(expected - 0.5);
  expect(box!.height, "toast dismiss height").toBeGreaterThanOrEqual(expected - 0.5);
  expect(session.unexpected).toEqual([]);
});
