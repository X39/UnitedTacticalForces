import { expect, test, type Locator, type Page } from "@playwright/test";
import type { CostAlert, GameServer } from "@/features/hosting/types";
import { automationBlock, type AutomationSaveBody } from "../support/automationMocks";
import { costAlertRaisedFrame } from "../support/fleetStream";
import {
  installJourney,
  journeyIds,
  journeyServer,
  journeyServerTitle,
} from "../support/journeyMocks";

/** The instant the mocked automatic stop is announced for. */
const stopsAt = "2026-11-23T20:30:00Z";

/** The visible copy this journey reads, copied from the English locale resources. */
const copy = {
  panelTitle: "Automatic shutdown and schedules",
  idleEnable: "Shut down when idle",
  idleMinutes: "Idle time (minutes)",
  zone: "Time zone",
  addSchedule: "Add a schedule",
  scheduleOne: "Schedule 1",
  startRadio: "Start server",
  time: "Time",
  save: "Save automation settings",
  consentTitle: "Allow scheduled starts?",
  allow: "Allow scheduled starts",
  statusLabel: "Current automation status",
  noSchedules: "No schedules.",
  statusSaved: "Saved.",
  toastSaved: "Automation settings saved.",
  noticeTitle: /^Automatic shutdown at /,
  noticeReason: "Nobody has been on the server for 30 minutes. It shuts down unless someone joins.",
  toastWarning: /^Automatic shutdown at .*: Journey test server\./,
  toastCancelled: `Automatic shutdown cancelled: someone joined ${journeyServerTitle}.`,
} as const;

/** The cost alert that is already on the server when the journey starts: it makes the cost alert banner show. */
const thresholdAlert: CostAlert = {
  id: "60000000-0000-4000-8000-0000000000e4",
  serverId: journeyIds.running,
  serverName: journeyServerTitle,
  costOwnerId: journeyIds.owner,
  thresholdAmount: 5,
  accruedAmount: 5.2,
  currency: "EUR",
  createdAt: "2026-11-23T19:00:00Z",
  kind: "CostThreshold",
  stopsAt: null,
  stopReason: null,
  stopLimit: null,
};

/** The automatic shutdown alert the control plane raises together with the announced stop. */
const autoStopAlert: CostAlert = {
  id: "60000000-0000-4000-8000-0000000000e5",
  serverId: journeyIds.running,
  serverName: journeyServerTitle,
  costOwnerId: journeyIds.owner,
  thresholdAmount: 0,
  accruedAmount: 0,
  currency: "EUR",
  createdAt: "2026-11-23T20:25:00Z",
  kind: "AutoStop",
  stopsAt,
  stopReason: "Idle",
  stopLimit: 30,
};

/** The automation panel card. */
function panel(page: Page): Locator {
  return page.locator("#server-automation");
}

/** The warning notice of a pending automatic stop. */
function notice(page: Page): Locator {
  return page.locator(".auto-stop-notice");
}

/**
 * Returns the server with an announced idle stop, as the control plane sends it after the warning lead time.
 *
 * @param server The server as it is saved.
 * @returns A copy whose automation status carries the pending stop.
 */
function withPendingStop(server: GameServer): GameServer {
  const automation = server.automation;
  if (!automation) throw new Error("The journey server has no automation block.");
  return {
    ...server,
    automation: {
      ...automation,
      status: {
        ...automation.status,
        idle: { state: "Idle", since: "2026-11-23T20:00:00Z", stopsAt },
        pendingStop: { reason: "Idle", stopsAt, limit: 30 },
      },
    },
  };
}

/**
 * Returns the server with the announced stop cleared, as the control plane sends it when someone joined.
 *
 * @param server The server that carries a pending stop.
 * @returns A copy whose automation status has no pending stop and a running idle clock reset.
 */
function withoutPendingStop(server: GameServer): GameServer {
  const automation = server.automation;
  if (!automation) throw new Error("The journey server has no automation block.");
  return {
    ...server,
    automation: {
      ...automation,
      status: {
        ...automation.status,
        idle: { state: "PlayersOnline", since: null, stopsAt: null },
        pendingStop: null,
      },
    },
  };
}

/**
 * Tells whether the shutdown notice sits above the cost alert banner in reading order and on the screen.
 *
 * @param page The page with both panels shown.
 * @returns True when the notice precedes the banner in the document and its top edge is higher.
 */
async function noticeIsAboveBanner(page: Page): Promise<boolean> {
  return page.evaluate(() => {
    const noticeElement = document.querySelector(".auto-stop-notice");
    const bannerElement = document.querySelector(".cost-alert-banner");
    if (noticeElement === null || bannerElement === null) return false;
    const following = noticeElement.compareDocumentPosition(bannerElement);
    return (
      (following & Node.DOCUMENT_POSITION_FOLLOWING) !== 0 &&
      noticeElement.getBoundingClientRect().top < bannerElement.getBoundingClientRect().top
    );
  });
}

/**
 * J-4 (VER-02, Phase 68): a member turns on idle shutdown at 30 minutes, adds a Friday 18:00 Start rule in a time
 * zone and saves it through the scheduled-start consent. A pushed pending stop then shows the shell notice above the
 * cost alert banner and one toast, and clears with the cancelled toast, while the focused control stays focused. The
 * run covers the desktop project and the 390 phone width.
 */
test.describe("J-4 automation", () => {
  test("saves idle shutdown and a scheduled start, then follows a pushed pending stop", async ({
    page,
  }) => {
    const journey = await installJourney(page, {
      culture: "en",
      servers: [
        journeyServer({
          id: journeyIds.running,
          title: journeyServerTitle,
          modPackId: 1,
          running: true,
          automation: automationBlock(),
        }),
      ],
    });
    journey.state.alerts = [thresholdAlert];

    await page.goto(`command/servers/${journeyIds.running}?tab=settings#server-automation`);
    await expect(page.getByRole("heading", { level: 2, name: copy.panelTitle })).toBeVisible();
    await expect(page.locator(".cost-alert-banner")).toBeVisible();

    // The draft: idle shutdown at 30 minutes and a Friday 18:00 start in Berlin time.
    const idle = panel(page).getByRole("switch", { name: copy.idleEnable });
    await idle.check();
    await panel(page).getByLabel(copy.idleMinutes).fill("30");
    await panel(page).getByLabel(copy.zone).selectOption({ value: "Europe/Berlin" });
    await panel(page).getByRole("button", { name: copy.addSchedule }).click();
    const rule = panel(page).getByRole("group", { name: copy.scheduleOne, exact: true });
    await expect(rule).toBeVisible();
    await rule.getByRole("radio", { name: copy.startRadio }).check();
    await rule.getByLabel(copy.time, { exact: true }).fill("18:00");
    await rule.locator('input[data-field="days"]').nth(4).check();

    // The status list shows what is saved, never the draft.
    const status = panel(page).getByLabel(copy.statusLabel);
    await expect(status).toContainText(copy.noSchedules);
    expect(journey.requests).toEqual([]);

    // Saving asks for the cost consent first; nothing is sent before it.
    await panel(page).getByRole("button", { name: copy.save }).click();
    const dialog = page.locator("dialog[open]");
    await expect(dialog.getByRole("heading", { name: copy.consentTitle })).toBeVisible();
    await expect(dialog.getByText("Operations", { exact: true })).toBeVisible();
    await expect(dialog.getByText("1 schedule starts the server", { exact: true })).toBeVisible();
    await expect(dialog.getByText(/0\.05/)).toBeVisible();
    expect(journey.requests).toEqual([]);

    await dialog.getByRole("button", { name: copy.allow }).click();
    await expect(page.locator("dialog[open]")).toHaveCount(0);
    await expect(
      panel(page).getByRole("status").filter({ hasText: copy.statusSaved }),
    ).toBeVisible();
    await expect(page.locator(".app-toast").filter({ hasText: copy.toastSaved })).toHaveCount(1);
    expect(journey.requests).toHaveLength(1);
    const saved = journey.requests[0]?.body as AutomationSaveBody;
    expect(journey.requests[0]).toMatchObject({
      method: "PUT",
      path: `/api/game-servers/${journeyIds.running}/automation`,
    });
    expect(saved).toMatchObject({
      version: 0,
      idleShutdownEnabled: true,
      idleShutdownMinutes: 30,
      timeZone: "Europe/Berlin",
      startConsent: true,
    });
    expect(saved.schedules).toMatchObject([{ action: "Start", days: [5], time: "18:00" }]);

    // The status list now names the saved next action in the chosen time zone.
    await expect(status).toContainText(/Start server: .*(6:00\s?PM|18:00).* \(Europe\/Berlin\)/);
    await expect(status).not.toContainText(copy.noSchedules);

    // A pushed pending stop: the notice above the cost alert banner, one toast, and no focus change.
    await idle.focus();
    await expect(idle).toBeFocused();
    await expect(notice(page)).toHaveCount(0);
    const savedServer = journey.state.servers[0];
    if (!savedServer) throw new Error("The journey server is gone.");
    journey.state.servers = [withPendingStop(savedServer)];
    await journey.pushChanged(journeyIds.running);
    await expect(
      page
        .locator("section.state-panel")
        .filter({ has: notice(page) })
        .getByRole("heading", {
          name: copy.noticeTitle,
        }),
    ).toBeVisible();
    await expect(notice(page)).toContainText(copy.noticeReason);
    await expect(page.locator(".cost-alert-banner")).toBeVisible();
    expect(await noticeIsAboveBanner(page)).toBe(true);

    journey.stream.send(costAlertRaisedFrame(autoStopAlert));
    await expect(page.locator(".app-toast").filter({ hasText: copy.toastWarning })).toHaveCount(1);
    // From here on the REST list holds the alert too; a redelivered frame adds no second toast.
    journey.state.alerts = [thresholdAlert, autoStopAlert];
    journey.stream.send(costAlertRaisedFrame(autoStopAlert));
    await expect(page.locator(".app-toast").filter({ hasText: copy.toastWarning })).toHaveCount(1);
    await expect(idle).toBeFocused();

    // Someone joined: the stop is cleared, the notice leaves and the cancelled toast shows once.
    const pendingServer = journey.state.servers[0];
    if (!pendingServer) throw new Error("The journey server is gone.");
    journey.state.servers = [withoutPendingStop(pendingServer)];
    journey.state.alerts = [thresholdAlert];
    await journey.pushChanged(journeyIds.running);
    await expect(notice(page)).toHaveCount(0);
    await expect(page.locator(".app-toast").filter({ hasText: copy.toastCancelled })).toHaveCount(
      1,
    );
    await expect(page.locator(".cost-alert-banner")).toBeVisible();
    await expect(idle).toBeFocused();

    expect(journey.requests).toHaveLength(1);
    expect(journey.session.unexpected).toEqual([]);
  });
});
