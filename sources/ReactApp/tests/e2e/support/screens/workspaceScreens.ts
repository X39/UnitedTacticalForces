import type { Page } from "@playwright/test";
import { costAlert } from "../../../helpers/costAlertFixtures";
import { fleetAction } from "../../../helpers/fleetFixtures";
import { workspaceCommand, workspaceJob } from "../../../helpers/workspaceFixtures";
import { automationBlock, type JsonRecord } from "../automationMocks";
import { auditUserId } from "../mockApi";
import type { AuditFlags, ScreenEntry } from "./registryTypes";
import {
  accessMockState,
  accessRoleRecord,
  accessUserRecord,
  automationHandleOf,
  buildWorkspaceServer,
  costOwnerOptionRecord,
  generatedEntryControlId,
  generatedEntrySavedValue,
  installAutomationWorkspace,
  installWorkspace,
  quietHistory,
  reloadWorkspaceServer,
  workspaceControlsOf,
  workspaceRoute,
  workspaceServerId,
} from "./workspaceMocks";

/**
 * The workspace slice of the screen registry: the workspace Overview, Activity and Settings tabs, the Automation
 * panel, the change dialogs and the workspace notices. Plan 69-27 adds the entries and empties the pending list.
 *
 * Every entry of this slice runs all six audits. Fixture strings the app prints are German-neutral (see
 * `workspaceMocks.ts`), and API prose reaches the page only through `ApiText`, which the German walk skips.
 */

/** The router manifest pattern of the server workspace. */
const workspacePattern = "/command/servers/{ServerId:guid}";

/** Every audit on: no flag of this slice is false, so none needs a justification. */
const fullAudit: AuditFlags = {
  axe: true,
  keyboard: true,
  reflow: true,
  spacing: true,
  german: true,
  headings: true,
};

/**
 * Triples the timeout of the running test.
 *
 * The keyboard walk visits every tab stop of `main` forwards and backwards, and the Settings and Activity pages hold
 * dozens, so an entry with several states or a long page needs more than the default 30 seconds on a loaded machine.
 * Playwright is imported here, not at the top, because the Vitest completeness guard loads this module and only the
 * browser run may touch the Playwright runner.
 */
async function allowLongWalk(): Promise<void> {
  const { test } = await import("@playwright/test");
  test.slow();
}

/**
 * Resolves once the workspace shell rendered: the server heading and the section navigation.
 *
 * @param page The page under audit.
 */
async function shellReady(page: Page): Promise<void> {
  await page.locator(".next-hosting-page h1").waitFor();
  await page.locator(".workspace-shell nav").first().waitFor();
}

/**
 * Resolves once the Overview section rendered with its live panels: the accrued cost, the metric tiles with a
 * value, a recent activity entry and the shell.
 *
 * @param page The page under audit.
 */
async function overviewReady(page: Page): Promise<void> {
  await shellReady(page);
  await page.locator('[data-slot="accrued-cost"] .accrued-cost__value').waitFor();
  await page.locator('[data-slot="metrics"] .metric-tile__value').first().waitFor();
  await page.locator(".activity-timeline__item").first().waitFor();
}

/**
 * Builds the automation block of the Overview: idle shutdown and a maximum runtime on, one stop rule and a next
 * scheduled action, so the Overview shows its Automation fact with all three lines.
 *
 * @returns The automation block as the API sends it.
 */
function overviewAutomation(): ReturnType<typeof automationBlock> {
  return automationBlock({
    settings: {
      idleShutdownEnabled: true,
      idleShutdownMinutes: 30,
      maxRuntimeEnabled: true,
      maxRuntimeHours: 6,
      timeZone: "Europe/Berlin",
      schedules: [
        {
          id: "a0000000-0000-4000-8000-000000000001",
          action: "Stop",
          days: [1, 2, 3, 4, 5],
          time: "23:00",
          nextRunAt: "2026-11-26T22:00:00Z",
          lastRun: null,
        },
      ],
      version: 1,
    },
    status: { nextScheduled: { action: "Stop", at: "2026-11-26T22:00:00Z" } },
  });
}

/**
 * Builds the history of the Activity section: a failed Start job with one failed worker command and its output,
 * and an older succeeded Stop job.
 *
 * @returns The jobs and no audit records.
 */
function failedHistory(): JsonRecord[] {
  const failed = workspaceJob({
    gameServerId: workspaceServerId,
    kind: "Start",
    actionId: "start",
    status: "Failed",
    stage: "worker-install",
    progressPercent: 40,
    failureCode: "worker-error",
    failureReason: "The mod download timed out.",
    requestedByName: "Alpha",
    canRetry: true,
    timeStampCreated: "2026-11-16T09:50:00Z",
    timeStampUpdated: "2026-11-16T09:58:00Z",
    timeStampStarted: "2026-11-16T09:51:00Z",
    timeStampCompleted: "2026-11-16T09:58:00Z",
    commands: [
      workspaceCommand({
        sequence: 1,
        actionId: "install",
        status: "Failed",
        stage: "failed",
        failureReason: "The mod download timed out.",
        diagnosticOutput: "download 12 of 40 failed after 3 attempts",
        timeStampCreated: "2026-11-16T09:51:00Z",
        timeStampStarted: "2026-11-16T09:52:00Z",
        timeStampCompleted: "2026-11-16T09:58:00Z",
      }),
    ],
  });
  const stopped = workspaceJob({
    gameServerId: workspaceServerId,
    kind: "Stop",
    actionId: "stop",
    status: "Succeeded",
    stage: "completed",
    progressPercent: 100,
    requestedByName: "Alpha",
    timeStampCreated: "2026-11-15T20:00:00Z",
    timeStampUpdated: "2026-11-15T20:05:00Z",
    timeStampStarted: "2026-11-15T20:01:00Z",
    timeStampCompleted: "2026-11-15T20:05:00Z",
  });
  return [failed, stopped] as unknown as JsonRecord[];
}

/** The disclosure button of the failed entry of {@link failedHistory}; only a problem entry carries the modifier. */
const failedEntrySelector = ".activity-entry--problem button[aria-expanded]";

/**
 * Resolves once the Activity timeline rendered with at least one entry and the toolbar.
 *
 * @param page The page under audit.
 */
async function activityReady(page: Page): Promise<void> {
  await shellReady(page);
  await page.locator(".activity-toolbar").waitFor();
  await page.locator(".activity-timeline .activity-entry").first().waitFor();
}

/**
 * Expands the failed entry of the timeline and waits for its details.
 *
 * @param page The page under audit.
 */
async function expandFailedEntry(page: Page): Promise<void> {
  const toggle = page.locator(failedEntrySelector).first();
  await toggle.click();
  await page.locator(".activity-entry__details").first().waitFor();
}

/** The value typed into the credential field; it must never reach the review dialog. */
const typedSecret = "Tr0ub4dor-secret-value";

/**
 * Builds the server overrides of an Arma server that is off: no deployment, a start action, and a viewer who may
 * modify it. This is the shape the Settings section needs for every editor and for deleting the record.
 *
 * @returns Members that replace the defaults of the running server.
 */
function offServer(): JsonRecord {
  return { deployment: null, actions: [fleetAction("start")], canModify: true };
}

/**
 * Resolves once the Settings section rendered every panel: the heading, the mod pack chooser, the configuration
 * field, the credential field and the server record.
 *
 * @param page The page under audit.
 */
async function settingsReady(page: Page): Promise<void> {
  await shellReady(page);
  await page.locator(".workspace-section__heading").waitFor();
  await page.locator(".mods-panel select").waitFor();
  await page.getByLabel("Slots", { exact: true }).waitFor();
  await page.locator(".credential__edit input").waitFor();
  await page.locator(".workspace-panel--record").waitFor();
}

/**
 * Stages a new value of the Slots setting. The field is scrolled into view first, like a member would before typing,
 * because the sticky bar only reaches the bottom of the viewport once the section has scrolled into it.
 *
 * @param page The page under audit.
 */
async function stageSetting(page: Page): Promise<void> {
  const field = page.getByLabel("Slots", { exact: true });
  await field.scrollIntoViewIfNeeded();
  await field.fill("80");
}

/**
 * Stages a different mod pack, a new Slots value and a new credential value: the three kinds of change the review
 * dialog lists.
 *
 * @param page The page under audit.
 */
async function stageEverything(page: Page): Promise<void> {
  await page.locator(".mods-panel select").selectOption({ value: "2" });
  await stageSetting(page);
  await page.locator(".credential__edit input").fill(typedSecret);
}

/**
 * Resolves once the Settings section shows the sticky pending changes bar.
 *
 * @param page The page under audit.
 */
async function pendingBarReady(page: Page): Promise<void> {
  await settingsReady(page);
  await page.locator(".pending-changes-bar").waitFor();
}

/** The selector of the Apply control of the open review dialog once it can be used. */
const applyReady = "dialog.dialog--review[open] button[type='submit']:not([aria-disabled='true'])";

/**
 * Stages every kind of change and opens the review dialog from the pending changes bar.
 *
 * @param page The page under audit.
 */
async function openReviewDialog(page: Page): Promise<void> {
  await stageEverything(page);
  await page.locator(".pending-changes-bar__actions button").first().click();
  await page.locator(applyReady).waitFor();
}

/**
 * Resolves once the review dialog can be applied, and fails when it shows the typed credential value.
 *
 * @param page The page under audit.
 */
async function reviewReady(page: Page): Promise<void> {
  await page.locator(applyReady).waitFor();
  const html = await page.locator("dialog.dialog--review[open]").innerHTML();
  if (html.includes(typedSecret)) {
    throw new Error("The review dialog shows the typed credential value.");
  }
}

/**
 * Stages a setting and leaves the Settings section through the section navigation, which opens the leave dialog.
 *
 * @param page The page under audit.
 */
async function openLeaveDialog(page: Page): Promise<void> {
  await stageSetting(page);
  await page.locator(".pending-changes-bar").waitFor();
  await page.locator(".section-navigation__link").first().click();
  await page.locator("dialog.dialog--leave[open]").waitFor();
}

/**
 * Resolves once the leave dialog offers its three outcomes: stay, discard and review.
 *
 * @param page The page under audit.
 */
async function leaveReady(page: Page): Promise<void> {
  const buttons = page.locator("dialog.dialog--leave[open] .dialog__actions button");
  await buttons.nth(2).waitFor();
  const count = await buttons.count();
  if (count !== 3) {
    throw new Error(
      `The leave dialog offers ${count} controls, expected stay, discard and review.`,
    );
  }
}

/**
 * Closes the dialog that an earlier state left open, with Escape like a member would. The desktop keyboard walk has
 * closed it already; the phone projects have no walk and leave it open.
 *
 * @param page The page under audit.
 */
async function closeOpenDialog(page: Page): Promise<void> {
  const dialog = page.locator("dialog[open]");
  if ((await dialog.count()) === 0) return;
  await page.keyboard.press("Escape");
  await dialog.first().waitFor({ state: "detached" });
}

/** How many changes the base review state stages: a mod pack, the Slots value and a credential. */
const baseStagedChanges = 3;

/**
 * Builds the open function of a review dialog with an exact number of staged changes: the three base changes of
 * {@link stageEverything} and generated settings for the rest. A dialog an earlier state left open is closed first,
 * and settings that are staged already are written again with the same value, which changes nothing.
 *
 * @param total The number of staged changes the review dialog lists, at least the three base changes.
 * @returns The open function of the state.
 */
function openReviewWithChanges(total: number): (page: Page) => Promise<void> {
  return async (page) => {
    await closeOpenDialog(page);
    await stageEverything(page);
    for (let index = 0; index < total - baseStagedChanges; index += 1) {
      await page
        .locator(`#${generatedEntryControlId(index)}`)
        .fill(String(generatedEntrySavedValue + 10));
    }
    await page.locator(".pending-changes-bar__actions button").first().click();
    await page.locator(applyReady).waitFor();
  };
}

/**
 * Builds the ready function of a review dialog that lists exactly the given number of changes, can be applied and
 * never shows the typed credential value.
 *
 * @param total The number of changes the dialog lists.
 * @returns The ready function of the state.
 */
function reviewListsChanges(total: number): (page: Page) => Promise<void> {
  return async (page) => {
    await reviewReady(page);
    const rows = await page.locator("dialog.dialog--review[open] .review-group__list > li").count();
    if (rows !== total) {
      throw new Error(`The review dialog lists ${rows} changes, expected ${total}.`);
    }
  };
}

/**
 * Leaves the staged Slots change behind through Discard changes, switches the idle shutdown on in the Automation
 * panel, which only the automation draft knows about, and leaves the Settings section through the section navigation.
 *
 * @param page The page under audit.
 */
async function openAutomationOnlyLeaveDialog(page: Page): Promise<void> {
  await closeOpenDialog(page);
  await page.locator(".pending-changes-bar__actions button").nth(1).click();
  await page.locator("dialog[open] button[type='submit']").click();
  await page.locator(".pending-changes-bar").waitFor({ state: "detached" });
  await page.locator('#server-automation input[role="switch"]').first().click();
  await page.locator(".section-navigation__link").first().click();
  await page.locator("dialog.dialog--leave[open]").waitFor();
}

/**
 * Resolves once the leave dialog of an automation-only draft offers two outcomes, stay and discard, and no Review
 * because the automation draft has nothing to review.
 *
 * @param page The page under audit.
 */
async function leaveAutomationOnlyReady(page: Page): Promise<void> {
  const buttons = page.locator("dialog.dialog--leave[open] .dialog__actions button");
  await buttons.nth(1).waitFor();
  const count = await buttons.count();
  if (count !== 2) {
    throw new Error(`The leave dialog offers ${count} controls, expected stay and discard.`);
  }
}

/**
 * Opens the delete confirmation of the server record. The panel header button is skipped, so the first remaining
 * button is Delete.
 *
 * @param page The page under audit.
 */
async function openDeleteDialog(page: Page): Promise<void> {
  await page.locator(".workspace-panel--record button:not(.disclosure__summary)").first().click();
  await page.locator("dialog[open]").waitFor();
}

/**
 * Resolves once the delete confirmation is open and every input in it is described by a helper.
 *
 * The delete confirmation carries no typed challenge today; the challenge field that has a helper belongs to the
 * terminate confirmation of the O-01 entry. If an input is ever added here, it must name its helper.
 *
 * @param page The page under audit.
 */
async function deleteReady(page: Page): Promise<void> {
  const dialog = page.locator("dialog[open]");
  await dialog.waitFor();
  const undescribed = await dialog.locator("input").evaluateAll((inputs) =>
    inputs
      .filter((input) => {
        const ids = (input.getAttribute("aria-describedby") ?? "").split(/\s+/).filter(Boolean);
        return !ids.some((id) => document.getElementById(id) !== null);
      })
      .map((input) => input.id),
  );
  if (undescribed.length > 0) {
    throw new Error(`Challenge fields without a described-by helper: ${undescribed.join(", ")}`);
  }
}

/**
 * Builds the ready function of a notice that renders in the shell: the shell and the given notice selector.
 *
 * @param selector The selector of the notice.
 * @returns A ready function.
 */
function noticeReady(selector: string): (page: Page) => Promise<void> {
  return async (page) => {
    await shellReady(page);
    await page.locator(selector).first().waitFor();
  };
}

/** The route of the Automation panel: the Settings tab with the deep link that focuses the panel heading. */
const automationRoute = `${workspaceRoute("settings")}#server-automation`;

/** The instant an announced automatic stop happens in the notice fixtures. */
const announcedStopAt = "2026-11-25T20:30:00Z";

/**
 * Builds a saved owner configuration: idle shutdown and a maximum runtime on, the Berlin time zone, one Friday start
 * rule with a recorded last run, and live status lines for all three rows of the status list. One rule is enough to
 * show the rule block; every further rule adds a dozen tab stops to each keyboard walk of the page.
 *
 * @param consent The scheduled-start consent state; `Needed` shows the paused-starts notice.
 * @returns The automation block as the API sends it.
 */
function savedAutomation(consent: "Given" | "Needed"): ReturnType<typeof automationBlock> {
  return automationBlock({
    settings: {
      idleShutdownEnabled: true,
      idleShutdownMinutes: 30,
      maxRuntimeEnabled: true,
      maxRuntimeHours: 6,
      timeZone: "Europe/Berlin",
      schedules: [
        {
          id: "a0000000-0000-4000-8000-000000000001",
          action: "Start",
          days: [5],
          time: "18:00",
          nextRunAt: "2026-11-27T17:00:00Z",
          lastRun: { at: "2026-11-20T17:00:00Z", outcome: "Started" },
        },
      ],
      version: 3,
    },
    status: {
      idle: { state: "PlayersOnline", since: null, stopsAt: null },
      maxRuntimeStopsAt: "2026-11-27T04:00:00Z",
      nextScheduled: { action: "Start", at: "2026-11-27T17:00:00Z" },
    },
    startConsent: { state: consent },
  });
}

/**
 * Builds an automation block with an announced idle stop, which is what the automatic shutdown notice shows.
 *
 * @returns The automation block as the API sends it.
 */
function pendingStopAutomation(): ReturnType<typeof automationBlock> {
  return automationBlock({
    settings: { idleShutdownEnabled: true, idleShutdownMinutes: 30, version: 1 },
    status: {
      idle: { state: "Idle", since: "2026-11-25T20:00:00Z", stopsAt: announcedStopAt },
      pendingStop: { reason: "Idle", stopsAt: announcedStopAt, limit: 30 },
    },
  });
}

/**
 * Resolves once the Automation panel rendered its status list and the deep link scroll has come to rest.
 *
 * The deep link scrolls the page smoothly, and axe measures the section navigation partly behind the sticky header
 * while that scroll is under way. The wait is what keeps those measurements from reporting a small target (XPF-70-16-2).
 *
 * @param page The page under audit.
 */
async function automationReady(page: Page): Promise<void> {
  await shellReady(page);
  await page.locator("#server-automation .automation-status").waitFor();
  await waitForScrollRest(page);
}

/**
 * Resolves once the Automation panel rendered and, as the deep link requires, its header button has keyboard focus.
 *
 * @param page The page under audit.
 */
async function automationArrivalReady(page: Page): Promise<void> {
  await automationReady(page);
  await page.waitForFunction(
    () =>
      document.activeElement === document.querySelector("#server-automation .disclosure__summary"),
  );
}

/**
 * Turns the same page into the read-only view of a viewer who may see but not change the automation.
 *
 * @param page The page under audit.
 */
async function openReadOnly(page: Page): Promise<void> {
  await reloadWorkspaceServer(page, savedAutomation("Given"), false);
}

/**
 * Resolves once the panel is read-only: it shows its notice and offers no save row.
 *
 * @param page The page under audit.
 */
async function readOnlyReady(page: Page): Promise<void> {
  await page.locator("#server-automation .automation-panel__notice").waitFor();
  await page.locator("#server-automation .automation-panel__save").waitFor({ state: "detached" });
}

/**
 * Turns the same page into a server whose read returned no automation block.
 *
 * @param page The page under audit.
 */
async function openUnavailable(page: Page): Promise<void> {
  await reloadWorkspaceServer(page, null, true);
}

/**
 * Resolves once the panel shows only its heading and the unavailable panel.
 *
 * @param page The page under audit.
 */
async function unavailableReady(page: Page): Promise<void> {
  await page.locator("#server-automation .state-panel--info").waitFor();
  await page.locator("#server-automation .automation-status").waitFor({ state: "detached" });
}

/**
 * Adds a Friday 18:00 start rule and saves, which opens the scheduled-start consent dialog.
 *
 * @param page The page under audit.
 */
async function openConsentDialog(page: Page): Promise<void> {
  const panel = page.locator("#server-automation");
  await panel.locator(".automation-panel__add button").click();
  const rule = panel.locator("[data-rule-key]").first();
  await rule.locator('input[type="radio"][value="Start"]').check();
  await rule.locator('input[data-field="time"]').fill("18:00");
  await rule.locator('input[data-field="days"]').nth(4).check();
  await panel.locator(".automation-panel__actions button").first().click();
  await page.locator("dialog[open]").waitFor();
}

/**
 * Resolves once the consent dialog is open with focus on Keep editing and no save with consent has been sent.
 *
 * Nothing leaves the page before the viewer allows the starts (T-69-33): the saved list of the mock stays empty.
 *
 * @param page The page under audit.
 */
async function consentReady(page: Page): Promise<void> {
  await page.locator("dialog[open]").waitFor();
  await page.waitForFunction(() => {
    const active = document.activeElement;
    return active !== null && active.hasAttribute("data-dialog-dismiss");
  });
  const saves = automationHandleOf(page).saves;
  if (saves.length > 0) {
    throw new Error(
      `The page sent ${saves.length} automation save(s) before the consent was given.`,
    );
  }
}

/**
 * Resolves once the automatic shutdown notice and the cost alert banner both render and the shutdown notice comes
 * first in the document: the shutdown is the more urgent of the two.
 *
 * @param page The page under audit.
 */
async function noticesReady(page: Page): Promise<void> {
  await shellReady(page);
  await page.locator(".auto-stop-notice").waitFor();
  await page.locator(".cost-alert-banner").waitFor();
  const ordered = await page.evaluate(() => {
    const stop = document.querySelector(".auto-stop-notice")?.closest(".state-panel");
    const alert = document.querySelector(".cost-alert-banner")?.closest(".state-panel");
    if (!stop || !alert) return false;
    return (stop.compareDocumentPosition(alert) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
  });
  if (!ordered)
    throw new Error("The automatic shutdown notice does not come before the cost alert banner.");
}

/** The selector of the toggle button in the header of one Settings panel, addressed by the anchor id of the panel. */
function panelToggle(page: Page, panelId: string): ReturnType<Page["locator"]> {
  return page.locator(`#${panelId} .disclosure__heading > button`);
}

/** The selector of every Settings panel header button. */
const allPanelToggles = "[data-settings-panel] .disclosure__heading > button";

/**
 * Resolves once the Settings section rendered all five panel headers (mods, configuration, credentials, automation
 * and the server record) with the loaded configuration summary. Unlike {@link settingsReady} it does not need any
 * panel content to be visible, because the panels start collapsed.
 *
 * @param page The page under audit.
 */
async function settingsHeadersReady(page: Page): Promise<void> {
  await shellReady(page);
  await page.locator(".workspace-section__heading").waitFor();
  await page.locator(allPanelToggles).nth(4).waitFor();
  await page
    .locator("#server-configuration .disclosure__heading .disclosure__summary-line")
    .waitFor();
}

/**
 * Counts the Settings panels whose header reports the open state.
 *
 * @param page The page under audit.
 * @returns The number of panel header buttons with `aria-expanded="true"`.
 */
async function openPanelCount(page: Page): Promise<number> {
  return page
    .locator(allPanelToggles)
    .evaluateAll(
      (buttons) =>
        buttons.filter((button) => button.getAttribute("aria-expanded") === "true").length,
    );
}

/**
 * Resolves once the page has stopped scrolling: the scroll offset and the page height stayed the same for ten
 * consecutive frames.
 *
 * The page scrolls smoothly (`scroll-behavior: smooth` unless the member prefers reduced motion), so a deep link, a
 * focus change or a scroll call is still moving when the state reports ready. Axe then measures targets that sit
 * half behind the sticky chrome for a few frames and reports them as small targets, which is a flake of the
 * measurement and not a layout problem.
 *
 * @param page The page under audit.
 */
async function waitForScrollRest(page: Page): Promise<void> {
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        const limit = performance.now() + 5000;
        let last = "";
        let stable = 0;
        const check = (): void => {
          const now = `${window.scrollY}:${document.documentElement.scrollHeight}`;
          stable = now === last ? stable + 1 : 0;
          last = now;
          if (stable >= 10 || performance.now() > limit) resolve();
          else requestAnimationFrame(check);
        };
        requestAnimationFrame(check);
      }),
  );
}

/**
 * Moves the scroll offset, by at most 1200 pixels either way, to the nearest one at which no control is cut in two by
 * a sticky bar at the top of the page or by the pending changes bar.
 *
 * A header or field with only a sliver visible beside a sticky bar is reported by axe as a target smaller than 24
 * pixels, even when the sliver is below the fold: the bar sits on top of the first panels while the section has not
 * scrolled far enough for the bar to pin to the viewport. That is a property of where the page happens to be scrolled
 * and not of the layout, and it is reproducible, so the Settings states are audited at an offset that cuts nothing.
 * A sticky element at the top counts as a bar while it is stuck, that is while its top equals its `top` offset.
 *
 * @param page The page under audit.
 */
async function clearStickyEdges(page: Page): Promise<void> {
  await page.evaluate(() => {
    const bar = document.querySelector<HTMLElement>(".pending-changes-bar");
    const targets = Array.from(
      document.querySelectorAll<HTMLElement>(
        "main button, main a[href], main input:not([type='hidden']), main select, main textarea",
      ),
    ).filter((element) => bar === null || !bar.contains(element));
    const stickyTops = Array.from(document.querySelectorAll<HTMLElement>("body *")).filter(
      (element) => {
        const style = getComputedStyle(element);
        return (
          (style.position === "sticky" || style.position === "fixed") &&
          style.top !== "auto" &&
          element !== bar &&
          !element.contains(bar)
        );
      },
    );
    // Only a bar that is visible, is not a full-height drawer and has reached its pin line covers the content below
    // it; the off-canvas navigation drawer is fixed too, but it is not shown.
    const chromeBottom = (): number =>
      stickyTops.reduce((bottom, element) => {
        const rect = element.getBoundingClientRect();
        const stuck = rect.top <= parseFloat(getComputedStyle(element).top) + 1.5;
        const pin = parseFloat(getComputedStyle(element).top);
        const topBar =
          rect.width >= 200 &&
          rect.height > 0 &&
          rect.height < innerHeight / 2 &&
          pin < innerHeight / 3;
        const shown = element.checkVisibility({ visibilityProperty: true });
        return stuck && topBar && shown ? Math.max(bottom, rect.bottom) : bottom;
      }, 0);
    const cuts = (): boolean => {
      const chrome = chromeBottom();
      const covering = bar?.getBoundingClientRect();
      return targets.some((element) => {
        if (stickyTops.some((sticky) => sticky.contains(element))) return false;
        const rect = element.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return false;
        if (rect.top < chrome && rect.bottom > chrome) return true;
        if (covering === undefined) return false;
        // The bar spans the width of the page content, so only the vertical extent decides.
        const touches = rect.bottom > covering.top + 0.5 && rect.top < covering.bottom - 0.5;
        const inside = rect.top >= covering.top - 0.5 && rect.bottom <= covering.bottom + 0.5;
        return touches && !inside;
      });
    };
    const origin = window.scrollY;
    for (let step = 0; step <= 150; step += 1) {
      for (const sign of step === 0 ? [1] : [1, -1]) {
        window.scrollTo({ top: origin + sign * step * 8, behavior: "instant" });
        if (!cuts()) return;
      }
    }
    window.scrollTo({ top: origin, behavior: "instant" });
  });
}

/**
 * Puts the page at a fixed scroll offset and waits until it rests, so the state is audited at the same place on every
 * run.
 *
 * The Settings page is thousands of pixels tall once a panel is open and where scroll anchoring leaves it after a panel
 * opens or closes is timing dependent. A member who just opened a panel sees its top below the sticky chrome, so the
 * states scroll there instead, and then clear the sticky edges.
 *
 * @param page The page under audit.
 * @param anchor The selector of the element whose top is shown below the sticky chrome; the page top when omitted.
 */
async function settleScroll(page: Page, anchor?: string | undefined): Promise<void> {
  await page.evaluate((selector) => {
    if (selector === undefined) window.scrollTo({ top: 0, behavior: "instant" });
    else document.querySelector(selector)?.scrollIntoView({ behavior: "instant" });
  }, anchor);
  await waitForScrollRest(page);
  await clearStickyEdges(page);
  await waitForScrollRest(page);
}

/**
 * Resolves once every Settings panel is collapsed and shows its summary line, which is the default of a first visit.
 *
 * @param page The page under audit.
 */
async function allCollapsedReady(page: Page): Promise<void> {
  await settingsHeadersReady(page);
  const open = await openPanelCount(page);
  if (open !== 0) throw new Error(`${open} Settings panel(s) are open, expected all collapsed.`);
  const summaries = await page
    .locator("[data-settings-panel] .disclosure__heading .disclosure__summary-line")
    .count();
  if (summaries < 5) throw new Error(`Only ${summaries} Settings panel(s) show a summary line.`);
}

/**
 * Stages the Slots value inside the Configuration panel and collapses the panel again, like a member who stages a
 * change and tidies the page: the staged badge stays on the collapsed header.
 *
 * @param page The page under audit.
 */
async function stageAndCollapseConfiguration(page: Page): Promise<void> {
  await panelToggle(page, "server-configuration").click();
  await stageSetting(page);
  await panelToggle(page, "server-configuration").click();
}

/**
 * Resolves once the Configuration panel is collapsed, its header carries the staged badge and the pending changes bar
 * is shown.
 *
 * @param page The page under audit.
 */
async function stagedBadgeReady(page: Page): Promise<void> {
  await settingsHeadersReady(page);
  await page.locator("#server-configuration .disclosure__badge").waitFor();
  await page.locator(".pending-changes-bar").waitFor();
  await page
    .locator('#server-configuration .disclosure__heading > button[aria-expanded="false"]')
    .waitFor();
  await settleScroll(page);
}

/**
 * Moves to the Automation deep link inside the page, the way a link to `#server-automation` does: the location hash
 * changes without a reload, so the staged draft stays.
 *
 * @param page The page under audit.
 */
async function openAutomationByDeepLink(page: Page): Promise<void> {
  await page.evaluate(() => {
    window.location.hash = "server-automation";
  });
}

/**
 * Resolves once the deep link opened the Automation panel and gave its header button the focus, while the
 * Configuration panel the member collapsed stays collapsed.
 *
 * @param page The page under audit.
 */
async function deepLinkReady(page: Page): Promise<void> {
  await automationArrivalReady(page);
  await page
    .locator('#server-automation .disclosure__heading > button[aria-expanded="true"]')
    .waitFor();
  await page
    .locator('#server-configuration .disclosure__heading > button[aria-expanded="false"]')
    .waitFor();
  await waitForScrollRest(page);
  await clearStickyEdges(page);
  await waitForScrollRest(page);
}

/**
 * Stages the value of a generated setting by writing it into the input and dispatching the input event, even while
 * the Configuration panel is collapsed. A hidden control cannot be focused or typed into, so this is how a change
 * reaches the draft of a collapsed panel the way a value from another source would; the draft then opens the panel
 * the member had closed, which is the rule under audit.
 *
 * @param page The page under audit.
 * @param index The zero-based position of the generated entry.
 * @param value The new raw value.
 */
async function stageWhileCollapsed(page: Page, index: number, value: string): Promise<void> {
  await page.locator(`#${generatedEntryControlId(index)}`).evaluate((input, next) => {
    const field = input as HTMLInputElement;
    const setValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")?.set;
    setValue?.call(field, next);
    field.dispatchEvent(new Event("input", { bubbles: true }));
  }, value);
}

/**
 * Stages a second setting while the Configuration panel is collapsed, which makes the panel open by itself.
 *
 * @param page The page under audit.
 */
async function stageIntoCollapsedPanel(page: Page): Promise<void> {
  await stageWhileCollapsed(page, 0, String(generatedEntrySavedValue + 10));
}

/**
 * Resolves once the Configuration panel opened by itself with both staged settings in it and the pending bar counts
 * them.
 *
 * @param page The page under audit.
 */
async function autoOpenedReady(page: Page): Promise<void> {
  await settingsHeadersReady(page);
  await page
    .locator('#server-configuration .disclosure__heading > button[aria-expanded="true"]')
    .waitFor();
  await page.locator(`#${generatedEntryControlId(0)}`).waitFor();
  await page.locator(".pending-changes-bar").waitFor();
  await settleScroll(page, "#server-configuration");
}

/**
 * Stages a Slots value above its maximum, collapses the Configuration panel and tries to review, so the review attempt
 * finds the invalid field in a collapsed panel.
 *
 * @param page The page under audit.
 */
async function reviewWithInvalidValue(page: Page): Promise<void> {
  const slots = page.getByLabel("Slots", { exact: true });
  await slots.fill("999");
  await panelToggle(page, "server-configuration").click();
  await page
    .locator('#server-configuration .disclosure__heading > button[aria-expanded="false"]')
    .waitFor();
  await page.locator(".pending-changes-bar__actions button").first().click();
}

/**
 * Resolves once the review attempt opened the Configuration panel and put the keyboard focus on the invalid field.
 *
 * @param page The page under audit.
 */
async function invalidAutoOpenedReady(page: Page): Promise<void> {
  await settingsHeadersReady(page);
  await page
    .locator('#server-configuration .disclosure__heading > button[aria-expanded="true"]')
    .waitFor();
  await page.waitForFunction(
    () =>
      document.activeElement?.closest("#server-configuration") !== null &&
      document.activeElement?.getAttribute("aria-invalid") === "true",
  );
  await waitForScrollRest(page);
}

/** The selector of the search input of the configuration filter. */
const filterInput = "#server-configuration .configuration-filter input[type='search']";

/**
 * Corrects the invalid Slots value so the review dialog is not blocked, then types a word that every generated
 * setting has into the filter.
 *
 * @param page The page under audit.
 */
async function filterWithResults(page: Page): Promise<void> {
  await page.getByLabel("Slots", { exact: true }).fill("80");
  await page.locator(filterInput).fill("Parameter");
}

/**
 * Resolves once the filter shows its result count and a list of settings, and no empty state.
 *
 * @param page The page under audit.
 */
async function filterResultsReady(page: Page): Promise<void> {
  await settingsHeadersReady(page);
  await page.locator("#server-configuration .configuration-filter__count").waitFor();
  await page.locator("#server-configuration .configuration-group").first().waitFor();
  await page.locator("#server-configuration .state-panel--empty").waitFor({ state: "detached" });
  await settleScroll(page, "#server-configuration");
}

/**
 * Types text no setting matches into the filter.
 *
 * @param page The page under audit.
 */
async function filterWithoutMatch(page: Page): Promise<void> {
  await page.locator(filterInput).fill("zzzz-no-match");
}

/**
 * Resolves once the filter shows its empty state with the clear action and no setting.
 *
 * @param page The page under audit.
 */
async function filterEmptyReady(page: Page): Promise<void> {
  await settingsHeadersReady(page);
  await page.locator("#server-configuration .state-panel--empty").waitFor();
  await page
    .locator("#server-configuration .configuration-group")
    .first()
    .waitFor({ state: "detached" });
  await settleScroll(page, "#server-configuration");
}

/** The anchor of the Access panel. */
const accessPanel = "#server-access";

/** The toggle button in the header of the Access panel; the subject rows inside it have disclosure headers of their own. */
const accessHeader = `${accessPanel} > .disclosure--panel > .disclosure__heading > button`;

/** The input of the grant form's member or role picker. */
const accessPicker = `${accessPanel} .access-grant__picker input[role="combobox"]`;

/** The in-flow listbox of the grant form's picker. */
const accessListbox = `${accessPanel} .access-grant [role="listbox"]`;

/** The search input of the access list filter. */
const accessFilter = `${accessPanel} .access-filter input[type="search"]`;

/** The toggle buttons of the subject rows that are open. */
const openAccessRows = `${accessPanel} .access-row .disclosure__heading > button[aria-expanded="true"]`;

/** The signed-in member of the Access panel states. It holds Full control, so its row shows seven included switches. */
const accessOperator = accessUserRecord(0, "Operator", ["sv"], auditUserId);

/**
 * Builds the access state of the S-15 entry: nine subjects, which is above the filter threshold of eight, with a
 * member who holds Full control (the signed-in one), a member without View, and candidates for every picker state. The
 * search texts `hold` and `fail` never answer and are refused, so the picker shows its loading and error states.
 *
 * @returns A fresh mutable access state; the state that empties the list changes it.
 */
function accessStates(): ReturnType<typeof accessMockState> {
  return accessMockState({
    users: [
      accessOperator,
      accessUserRecord(1, "Bravo", ["sv:view", "sv:operate"]),
      accessUserRecord(2, "Delta", ["sv:view"]),
      accessUserRecord(3, "Echo", ["sv:operate"]),
      accessUserRecord(4, "Foxtrot", ["sv:view", "sv:modify"]),
      accessUserRecord(5, "Golf", ["sv:view"]),
      accessUserRecord(6, "Hotel", ["sv:view", "sv:operate", "sv:modify"]),
    ],
    roles: [
      accessRoleRecord(7, "Alpha-Team", ["sv:view"], 4, "The first team"),
      accessRoleRecord(8, "Bravo-Team", ["sv:view", "sv:operate"], 3),
    ],
    userCandidates: [
      accessUserRecord(11, "Charlie"),
      accessUserRecord(12, "Chantal"),
      accessUserRecord(13, "Carlos"),
    ],
    heldSearch: "hold",
    failingSearch: "fail",
  });
}

/**
 * Resolves once the Access panel header is on the page with its summary line, whatever the open state.
 *
 * @param page The page under audit.
 */
async function accessHeaderReady(page: Page): Promise<void> {
  await shellReady(page);
  await page.locator(accessHeader).waitFor();
  await page
    .locator(`${accessPanel} > .disclosure--panel > .disclosure__heading .disclosure__summary-line`)
    .waitFor();
}

/**
 * Resolves once the Access panel is collapsed and shows its summary line.
 *
 * @param page The page under audit.
 */
async function accessCollapsedReady(page: Page): Promise<void> {
  await accessHeaderReady(page);
  await page.locator(`${accessHeader}[aria-expanded="false"]`).waitFor();
  await settleScroll(page, accessPanel);
}

/**
 * Opens the Access panel with its header button, like a member would.
 *
 * @param page The page under audit.
 */
async function openAccessPanel(page: Page): Promise<void> {
  await page.locator(`${accessHeader}[aria-expanded="false"]`).click();
}

/**
 * Resolves once the Access panel is open with the list of subjects and the grant form.
 *
 * @param page The page under audit.
 */
async function accessOpenReady(page: Page): Promise<void> {
  await accessHeaderReady(page);
  await page.locator(`${accessHeader}[aria-expanded="true"]`).waitFor();
  await page.locator(`${accessPanel} .access-grant`).waitFor();
  await page.locator(`${accessPanel} .access-row`).first().waitFor();
  await settleScroll(page, accessPanel);
}

/**
 * Closes every open subject row and opens the row of one subject, so exactly one row is open.
 *
 * @param name The name of the subject; the toggle button of a row is named by it.
 * @returns The open function of the state.
 */
function openAccessRow(name: string): (page: Page) => Promise<void> {
  return async (page) => {
    const open = page.locator(openAccessRows);
    while ((await open.count()) > 0) await open.first().click();
    await page.locator(accessPanel).getByRole("button", { name, exact: true }).click();
  };
}

/**
 * Finds the subject row that is open.
 *
 * @param page The page under audit.
 * @returns The locator of the open rows.
 */
function openRowOf(page: Page): ReturnType<Page["locator"]> {
  return page
    .locator(`${accessPanel} .access-row`)
    .filter({ has: page.locator('.disclosure__heading > button[aria-expanded="true"]') });
}

/**
 * Builds the ready function of an open row: exactly one row is open and it shows the given number of switches, of
 * which the given number is disabled.
 *
 * @param switches How many permission switches the open row shows.
 * @param disabled How many of them are disabled.
 * @returns The ready function of the state.
 */
function accessRowReady(switches: number, disabled: number): (page: Page) => Promise<void> {
  return async (page) => {
    await accessHeaderReady(page);
    await page.locator(openAccessRows).waitFor();
    const row = page
      .locator(`${accessPanel} .access-row`)
      .filter({ has: page.locator('.disclosure__heading > button[aria-expanded="true"]') });
    await row
      .locator('input[role="switch"]')
      .nth(switches - 1)
      .waitFor();
    const found = await row.locator('input[role="switch"]').count();
    const locked = await row.locator('input[role="switch"]:disabled').count();
    if (found !== switches || locked !== disabled) {
      throw new Error(
        `The open row shows ${found} switches (${locked} disabled), expected ${switches} (${disabled}).`,
      );
    }
    await settleScroll(page, accessPanel);
  };
}

/**
 * Resolves once the open row of a member without View shows its warning.
 *
 * @param page The page under audit.
 */
async function accessNoViewReady(page: Page): Promise<void> {
  await accessRowReady(8, 0)(page);
  await page.locator(`${accessPanel} .access-row .state-panel--warning`).waitFor();
}

/**
 * Closes the open rows and types a text into the access list filter.
 *
 * @param text The filter text.
 * @returns The open function of the state.
 */
function filterAccess(text: string): (page: Page) => Promise<void> {
  return async (page) => {
    const open = page.locator(openAccessRows);
    while ((await open.count()) > 0) await open.first().click();
    await page.locator(accessFilter).fill(text);
  };
}

/**
 * Resolves once the filter shows its result line and exactly three rows: the two roles that match the filter text and
 * the member without View, whom the filter never hides because the warning must stay visible.
 *
 * @param page The page under audit.
 */
async function accessFilterResultsReady(page: Page): Promise<void> {
  await accessHeaderReady(page);
  await page.locator(`${accessPanel} .access-filter__result`).waitFor();
  const rows = page.locator(`${accessPanel} .access-row`);
  await rows.nth(2).waitFor();
  await rows.nth(3).waitFor({ state: "detached" });
  await page.locator(`${accessPanel} .state-panel--empty`).waitFor({ state: "detached" });
  await settleScroll(page, accessPanel);
}

/**
 * Gives the member without View the View permission, as another manager would, and lets the panel fetch the list
 * again, then types a filter text no subject matches. The filter never hides a subject that cannot see the server, so
 * the empty result is only reachable once nobody is left without View.
 *
 * @param page The page under audit.
 */
async function filterAccessWithoutMatch(page: Page): Promise<void> {
  const access = workspaceControlsOf(page).access;
  if (access === undefined) throw new Error("The page runs no access state.");
  for (const user of access.users) {
    if (!user.permissions.includes("sv:view")) user.permissions.push("sv:view");
  }
  const reads = access.reads;
  await page.evaluate(() =>
    document.dispatchEvent(new Event("visibilitychange", { bubbles: true })),
  );
  while (access.reads === reads) await page.waitForTimeout(50);
  await filterAccess("zzzz-no-match")(page);
}

/**
 * Resolves once the filter shows its empty state with the clear action and no row.
 *
 * @param page The page under audit.
 */
async function accessFilterEmptyReady(page: Page): Promise<void> {
  await accessHeaderReady(page);
  await page.locator(`${accessPanel} .state-panel--empty`).waitFor();
  await page.locator(`${accessPanel} .access-row`).first().waitFor({ state: "detached" });
  await settleScroll(page, accessPanel);
}

/**
 * Clears the list filter and types a search text into the grant form's picker.
 *
 * @param text The search text.
 * @returns The open function of the state.
 */
function searchAccessPicker(text: string): (page: Page) => Promise<void> {
  return async (page) => {
    const filter = page.locator(accessFilter);
    if ((await filter.inputValue()) !== "") await filter.fill("");
    await page.locator(accessPicker).fill(text);
  };
}

/**
 * Resolves once the picker's listbox shows exactly the given number of candidates.
 *
 * @param count The number of candidate rows.
 * @returns The ready function of the state.
 */
function accessCandidatesReady(count: number): (page: Page) => Promise<void> {
  return async (page) => {
    await accessHeaderReady(page);
    await page
      .locator(`${accessListbox} [role="option"]`)
      .nth(count - 1)
      .waitFor();
    const found = await page.locator(`${accessListbox} [role="option"]`).count();
    if (found !== count)
      throw new Error(`The picker lists ${found} candidates, expected ${count}.`);
    await settleScroll(page, accessPanel);
  };
}

/**
 * Resolves once the picker is loading: the input is busy and the listbox shows its loading line.
 *
 * @param page The page under audit.
 */
async function accessLoadingReady(page: Page): Promise<void> {
  await accessHeaderReady(page);
  await page.locator(`${accessPicker}[aria-busy="true"]`).waitFor();
  await page.locator(`${accessListbox} .search-picker__state`).waitFor();
  await settleScroll(page, accessPanel);
}

/**
 * Resolves once a settled search found nothing: the listbox shows only its empty line.
 *
 * @param page The page under audit.
 */
async function accessEmptyPickerReady(page: Page): Promise<void> {
  await accessHeaderReady(page);
  await page.locator(`${accessPicker}[aria-busy="true"]`).waitFor({ state: "detached" });
  await page.locator(`${accessListbox} .search-picker__state`).waitFor();
  await page.locator(`${accessListbox} [role="option"]`).first().waitFor({ state: "detached" });
  await settleScroll(page, accessPanel);
}

/**
 * Resolves once a refused search shows the alert of the picker.
 *
 * @param page The page under audit.
 */
async function accessErrorPickerReady(page: Page): Promise<void> {
  await accessHeaderReady(page);
  await page.locator(`${accessPanel} .access-grant .search-picker__state [role="alert"]`).waitFor();
  await settleScroll(page, accessPanel);
}

/**
 * Searches for candidates and chooses the first one with the pointer.
 *
 * @param page The page under audit.
 */
async function chooseAccessCandidate(page: Page): Promise<void> {
  await page.locator(accessPicker).fill("Ch");
  await page.locator(`${accessListbox} [role="option"]`).nth(1).waitFor();
  await page.locator(`${accessListbox} [role="option"]`).first().click();
}

/**
 * Resolves once the picker holds the chosen candidate as its token.
 *
 * @param page The page under audit.
 */
async function accessChosenReady(page: Page): Promise<void> {
  await accessHeaderReady(page);
  await page.locator(`${accessPanel} .search-picker__chosen`).waitFor();
  await page.locator(accessPicker).waitFor({ state: "detached" });
  await settleScroll(page, accessPanel);
}

/**
 * Takes everyone off the access list and opens the panel again: the choice is cleared, the state loses every subject,
 * the panel is closed and opened, and the open fetches the list again.
 *
 * @param page The page under audit.
 */
async function emptyAccessList(page: Page): Promise<void> {
  await page.locator(`${accessPanel} .search-picker__clear`).click();
  const access = workspaceControlsOf(page).access;
  if (access === undefined) throw new Error("The page runs no access state.");
  access.users = [];
  access.roles = [];
  await page.locator(`${accessHeader}[aria-expanded="true"]`).click();
  await page.locator(`${accessHeader}[aria-expanded="false"]`).waitFor();
  await openAccessPanel(page);
}

/**
 * Resolves once the open panel shows the empty state of an access list that nobody is on.
 *
 * @param page The page under audit.
 */
async function accessEmptyReady(page: Page): Promise<void> {
  await accessHeaderReady(page);
  await page.locator(`${accessHeader}[aria-expanded="true"]`).waitFor();
  await page.locator(`${accessPanel} .state-panel--empty`).waitFor();
  await page.locator(`${accessPanel} .access-row`).first().waitFor({ state: "detached" });
  await settleScroll(page, accessPanel);
}

/** The anchor of the Cost owner panel. */
const costOwnerPanel = "#server-cost-owner";

/** The toggle button in the header of the Cost owner panel. */
const costOwnerHeader = `${costOwnerPanel} > .disclosure--panel > .disclosure__heading > button`;

/** The input of the new cost owner picker. */
const costOwnerPicker = `${costOwnerPanel} input[role="combobox"]`;

/** The owner rows of the open picker listbox. */
const costOwnerOptions = `${costOwnerPanel} [role="listbox"] [role="option"]`;

/** The visible reason under the Transfer button while it is unavailable. */
const transferReason = `${costOwnerPanel} .cost-owner-transfer__action .action-reason`;

/** The Transfer cost owner button of the panel. */
const transferButton = `${costOwnerPanel} .cost-owner-transfer__action button`;

/** The owners the picker offers: three kinds of account and a frozen one that cannot be chosen. */
const costOwners = [
  costOwnerOptionRecord(1, "Bravo"),
  costOwnerOptionRecord(2, "Charlie", { kind: "User", isCurrentUser: true }),
  costOwnerOptionRecord(3, "Delta", { kind: "ExternalOrganization" }),
  costOwnerOptionRecord(4, "Echo", { isFrozen: true }),
];

/** The reason text each page showed under the Transfer button last, so a state can wait for it to change. */
const lastTransferReason = new WeakMap<Page, string>();

/**
 * Builds the mock options of a stopped server whose cost owner a member may transfer, with the Cost owner panel as the
 * only open Settings panel.
 *
 * @param canModify Whether the viewer may change the server and so transfer its cost owner.
 * @returns The workspace mock options.
 */
function costOwnerMock(canModify: boolean): Parameters<typeof installWorkspace>[2] {
  return {
    server: { ...offServer(), canModify },
    accrued: false,
    settingsPanels: ["server-cost-owner"],
    costOwnerOptions: costOwners,
  };
}

/**
 * Asks the page for the server again through Refresh server, like a member who suspects the server changed.
 *
 * @param page The page under audit.
 */
async function refreshServer(page: Page): Promise<void> {
  await page.locator(".workspace-details__refresh button").click();
}

/**
 * Resolves once the Cost owner panel is open with its facts.
 *
 * @param page The page under audit.
 */
async function costOwnerOpenReady(page: Page): Promise<void> {
  await shellReady(page);
  await page.locator(`${costOwnerHeader}[aria-expanded="true"]`).waitFor();
  await page.locator(`${costOwnerPanel} .cost-owner-facts`).waitFor();
}

/**
 * Resolves once the panel of a viewer without the right to transfer shows only its read-only sentence.
 *
 * @param page The page under audit.
 */
async function costOwnerReadOnlyReady(page: Page): Promise<void> {
  await costOwnerOpenReady(page);
  await page.locator(`${costOwnerPanel} .cost-owner__readonly`).waitFor();
  await page.locator(`${costOwnerPanel} .cost-owner-transfer`).waitFor({ state: "detached" });
  await settleScroll(page);
}

/**
 * Turns the same page into the one of a member who may change the server, which shows the transfer block.
 *
 * @param page The page under audit.
 */
async function becomeModifier(page: Page): Promise<void> {
  workspaceControlsOf(page).patchServer({ canModify: true });
  await refreshServer(page);
}

/**
 * Resolves once the transfer block shows the picker and the unavailable Transfer button with its reason.
 *
 * @param page The page under audit.
 */
async function costOwnerTransferReady(page: Page): Promise<void> {
  await costOwnerOpenReady(page);
  await page.locator(`${costOwnerPanel} .cost-owner-transfer`).waitFor();
  await page.locator(`${costOwnerPanel} .cost-owner__readonly`).waitFor({ state: "detached" });
  await page.locator(costOwnerPicker).waitFor();
  const reason = await page.locator(transferReason).textContent();
  lastTransferReason.set(page, reason ?? "");
  await settleScroll(page);
}

/**
 * Focuses the picker, which asks for the owners and opens the listbox with the empty query.
 *
 * @param page The page under audit.
 */
async function openOwnerPicker(page: Page): Promise<void> {
  await page.locator(costOwnerPicker).click();
}

/**
 * Resolves once the open picker lists every owner of the mock, among them the frozen one with its reason.
 *
 * @param page The page under audit.
 */
async function ownerPickerReady(page: Page): Promise<void> {
  await costOwnerOpenReady(page);
  await page
    .locator(costOwnerOptions)
    .nth(costOwners.length - 1)
    .waitFor();
  const found = await page.locator(costOwnerOptions).count();
  if (found !== costOwners.length) {
    throw new Error(`The picker lists ${found} owners, expected ${costOwners.length}.`);
  }
  await page.locator(`${costOwnerPanel} .search-picker__option--disabled`).waitFor();
  await settleScroll(page);
}

/**
 * Closes the picker and turns the current owner frozen, as the API reports it after the owner was frozen.
 *
 * @param page The page under audit.
 */
async function freezeCurrentOwner(page: Page): Promise<void> {
  await page.keyboard.press("Escape");
  workspaceControlsOf(page).patchServer({ costOwnerIsFrozen: true });
  await refreshServer(page);
}

/**
 * Resolves once the panel warns that the current owner is frozen and carries the badge in its header.
 *
 * @param page The page under audit.
 */
async function frozenOwnerReady(page: Page): Promise<void> {
  await costOwnerOpenReady(page);
  await page.locator(`${costOwnerPanel} .state-panel--warning`).waitFor();
  await page.locator(`${costOwnerPanel} .disclosure__badge`).waitFor();
  await settleScroll(page);
}

/**
 * Builds the open function of a state that changes the server record and asks for it again.
 *
 * @param patch The members of the server to replace.
 * @returns The open function of the state.
 */
function patchServerThenRefresh(patch: JsonRecord): (page: Page) => Promise<void> {
  return async (page) => {
    workspaceControlsOf(page).patchServer(patch);
    await refreshServer(page);
  };
}

/**
 * Resolves once the Transfer button shows another reason than the one of the previous state, the frozen warning is gone
 * and the owner picker is still there.
 *
 * @param page The page under audit.
 */
async function transferReasonChangedReady(page: Page): Promise<void> {
  await costOwnerOpenReady(page);
  await page.locator(`${costOwnerPanel} .state-panel--warning`).waitFor({ state: "detached" });
  const previous = lastTransferReason.get(page) ?? "";
  await page.waitForFunction(
    ({ selector, before }) => {
      const reason = document.querySelector(selector);
      return reason !== null && reason.textContent !== before;
    },
    { selector: transferReason, before: previous },
  );
  await costOwnerTransferReady(page);
}

/** The server record of the state "transfer unavailable while a machine is present": a cloud machine exists. */
const machinePresent: JsonRecord = {
  costOwnerIsFrozen: false,
  deployment: buildWorkspaceServer().deployment,
};

/** The server record of the state "transfer unavailable while jobs are pending": no machine, one job pending. */
const jobsPending: JsonRecord = { deployment: null, pendingJobCount: 1 };

/** The selector of the confirm button of the open dialog. */
const dialogConfirm = "dialog[open] button[type='submit']";

/**
 * Chooses the second owner of the picker and presses Transfer, which opens the confirmation dialog.
 *
 * @param page The page under audit.
 */
async function openTransferDialog(page: Page): Promise<void> {
  await page.locator(costOwnerPicker).click();
  await page.locator(costOwnerOptions).nth(1).waitFor();
  await page.locator(costOwnerOptions).nth(1).click();
  await page.locator(transferButton).click();
  await page.locator("dialog[open]").waitFor();
}

/**
 * Resolves once a confirmation dialog is open with the keyboard focus on its dismiss button, which is the safe choice.
 *
 * @param page The page under audit.
 */
async function dismissFocusedReady(page: Page): Promise<void> {
  await page.locator("dialog[open]").waitFor();
  await page.waitForFunction(() => {
    const active = document.activeElement;
    return active !== null && active.hasAttribute("data-dialog-dismiss");
  });
}

/**
 * Resolves once the transfer dialog is open, idle and without an error.
 *
 * @param page The page under audit.
 */
async function transferIdleReady(page: Page): Promise<void> {
  await dismissFocusedReady(page);
  await page.locator('dialog[open][aria-busy="true"]').waitFor({ state: "detached" });
  await page.locator("dialog[open] [role='alert']").waitFor({ state: "detached" });
}

/**
 * Confirms the transfer while the mock parks the request, so the dialog stays busy. The desktop keyboard walk of the
 * previous state closed the dialog with Escape, which keeps the chosen owner, so the dialog is opened again first.
 *
 * @param page The page under audit.
 */
async function confirmHeldTransfer(page: Page): Promise<void> {
  workspaceControlsOf(page).setTransfer("hold");
  if ((await page.locator("dialog[open]").count()) === 0) {
    await page.locator(transferButton).click();
    await page.locator("dialog[open]").waitFor();
  }
  await page.locator(dialogConfirm).click();
}

/**
 * Resolves once the dialog is busy: it reports `aria-busy` while the transfer runs.
 *
 * @param page The page under audit.
 */
async function transferBusyReady(page: Page): Promise<void> {
  await page.locator('dialog[open][aria-busy="true"]').waitFor();
}

/**
 * Lets the parked transfer fail with a conflict, so the dialog shows the message of the status.
 *
 * @param page The page under audit.
 */
async function failHeldTransfer(page: Page): Promise<void> {
  const controls = workspaceControlsOf(page);
  controls.setTransfer(409);
  controls.releaseTransfers();
}

/**
 * Resolves once the dialog is no longer busy and shows the alert of the failed transfer.
 *
 * @param page The page under audit.
 */
async function transferErrorReady(page: Page): Promise<void> {
  await page.locator("dialog[open] [role='alert']").waitFor();
  await page.locator('dialog[open][aria-busy="true"]').waitFor({ state: "detached" });
}

/**
 * Builds the access state of the O-11 entry: the signed-in member with View and Manage access, one more member and one
 * role. Nothing is ever confirmed, so the list never changes.
 *
 * @returns A fresh mutable access state.
 */
function accessDialogStates(): ReturnType<typeof accessMockState> {
  return accessMockState({
    users: [
      accessUserRecord(0, "Operator", ["sv:view", "sv:manage-access"], auditUserId),
      accessUserRecord(1, "Bravo", ["sv:view", "sv:operate"]),
    ],
    roles: [accessRoleRecord(7, "Alpha-Team", ["sv:view"], 4, "The first team")],
  });
}

/**
 * Builds the open function of a state that closes the dialog an earlier state left open, opens the row of one subject
 * and presses its Remove all access button.
 *
 * @param name The name of the subject.
 * @returns The open function of the state.
 */
function openRemoveAllDialog(name: string): (page: Page) => Promise<void> {
  return async (page) => {
    await closeOpenDialog(page);
    await openAccessRow(name)(page);
    await openRowOf(page).locator(".access-row__remove button").click();
    await page.locator("dialog[open]").waitFor();
  };
}

/**
 * Closes the dialog an earlier state left open, opens the row of the signed-in member and switches its View permission
 * off, which asks for a confirmation because the member would remove their own permission.
 *
 * @param page The page under audit.
 */
async function openSelfRemovalDialog(page: Page): Promise<void> {
  await closeOpenDialog(page);
  await openAccessRow("Operator")(page);
  // The visible track of the switch lies over its input on the phone widths, so a pointer hits the track and toggles
  // the input through the label, as it does for a member. Playwright refuses that click unless it is forced.
  await openRowOf(page).locator('input[role="switch"]').nth(1).click({ force: true });
  await page.locator("dialog[open]").waitFor();
}

/** The registry entries of the workspace slice. */
export const workspaceScreens: readonly ScreenEntry[] = [
  {
    id: "S-04",
    kind: "screen",
    route: workspaceRoute(),
    routePattern: workspacePattern,
    tab: "overview",
    setup: (page, culture) =>
      installWorkspace(page, culture, { automation: overviewAutomation(), ...quietHistory() }),
    states: [{ name: "running", ready: overviewReady }],
    audit: fullAudit,
    baseline: "K-04",
  },
  {
    id: "S-05",
    kind: "screen",
    route: workspaceRoute("activity"),
    routePattern: workspacePattern,
    tab: "activity",
    setup: async (page, culture) => {
      await allowLongWalk();
      return installWorkspace(page, culture, {
        jobs: failedHistory(),
        server: { actions: [fleetAction("stop"), fleetAction("collect-logs")] },
      });
    },
    states: [
      { name: "timeline", ready: activityReady },
      { name: "expanded failed entry", open: expandFailedEntry, ready: activityReady },
    ],
    audit: fullAudit,
    baseline: "K-05",
  },
  {
    id: "S-06",
    kind: "screen",
    route: workspaceRoute("settings"),
    routePattern: workspacePattern,
    tab: "settings",
    setup: async (page, culture) => {
      await allowLongWalk();
      return installWorkspace(page, culture, { server: offServer(), canManageSecrets: true });
    },
    // One state on purpose: the staged draft shows every panel the unstaged section shows plus the pending bar,
    // and each state costs a full keyboard walk, which two states pushed to two thirds of the test timeout.
    states: [{ name: "staged draft", open: stageSetting, ready: pendingBarReady }],
    audit: fullAudit,
    baseline: "K-06",
  },
  {
    id: "S-14",
    kind: "screen",
    route: workspaceRoute("settings"),
    routePattern: workspacePattern,
    tab: "settings",
    setup: async (page, culture) => {
      await allowLongWalk();
      return installWorkspace(page, culture, {
        server: offServer(),
        canManageSecrets: true,
        settingsPanels: [],
        configurationEntryCount: 12,
      });
    },
    // The states follow one member from the collapsed default to the filter, on one page load. The keyboard walk is off
    // on purpose: S-06 walks the fully open Settings page, which holds every tab stop these states show and more, and
    // a walk per state of this entry would exceed the test timeout (see allowLongWalk, RESEARCH Pitfall 10). Axe, reflow,
    // spacing, German and the heading outline still visit every state.
    states: [
      { name: "all panels collapsed", ready: allCollapsedReady },
      {
        name: "staged badge on a collapsed panel",
        open: stageAndCollapseConfiguration,
        ready: stagedBadgeReady,
        baseline: "K-16",
      },
      { name: "one panel open by deep link", open: openAutomationByDeepLink, ready: deepLinkReady },
      {
        name: "staged draft with a panel auto-opened",
        open: stageIntoCollapsedPanel,
        ready: autoOpenedReady,
      },
      {
        name: "invalid field auto-opened",
        open: reviewWithInvalidValue,
        ready: invalidAutoOpenedReady,
      },
      {
        name: "configuration filter with results",
        open: filterWithResults,
        ready: filterResultsReady,
      },
      { name: "configuration filter with none", open: filterWithoutMatch, ready: filterEmptyReady },
    ],
    audit: {
      axe: true,
      keyboard: false,
      reflow: true,
      spacing: true,
      german: true,
      headings: true,
    },
  },
  {
    id: "S-15",
    kind: "screen",
    route: workspaceRoute("settings"),
    routePattern: workspacePattern,
    tab: "settings",
    setup: async (page, culture) => {
      await allowLongWalk();
      return installWorkspace(page, culture, {
        server: offServer(),
        settingsPanels: [],
        accessState: accessStates(),
        // Forced on, because the last state takes every subject, the signed-in member included, off the list.
        canManageAccess: true,
      });
    },
    // The states follow one member through the Access panel on one page load. The keyboard walk is off on purpose:
    // hosting-access.spec.ts walks the panel with the keyboard (grant form, switches, both dialogs) and a walk per state
    // of this entry would exceed the test timeout (see allowLongWalk, RESEARCH Pitfall 10). Axe, reflow, spacing,
    // German and the heading outline still visit every state.
    states: [
      { name: "collapsed with summary", ready: accessCollapsedReady },
      { name: "open with members and roles", open: openAccessPanel, ready: accessOpenReady },
      {
        name: "one row open with all switches",
        open: openAccessRow("Bravo"),
        ready: accessRowReady(8, 0),
        baseline: "K-18",
      },
      {
        name: "a subject with Full control",
        open: openAccessRow("Operator"),
        ready: accessRowReady(8, 7),
      },
      {
        name: "a row with the no-View warning",
        open: openAccessRow("Echo"),
        ready: accessNoViewReady,
      },
      {
        name: "filter with results",
        open: filterAccess("Team"),
        ready: accessFilterResultsReady,
      },
      {
        name: "filter with none",
        open: filterAccessWithoutMatch,
        ready: accessFilterEmptyReady,
      },
      {
        name: "picker open with results",
        open: searchAccessPicker("Ch"),
        ready: accessCandidatesReady(2),
      },
      { name: "picker loading", open: searchAccessPicker("hold"), ready: accessLoadingReady },
      { name: "picker empty", open: searchAccessPicker("zzz"), ready: accessEmptyPickerReady },
      { name: "picker error", open: searchAccessPicker("fail"), ready: accessErrorPickerReady },
      {
        name: "picker with a chosen token",
        open: chooseAccessCandidate,
        ready: accessChosenReady,
      },
      { name: "open empty", open: emptyAccessList, ready: accessEmptyReady },
    ],
    audit: {
      axe: true,
      keyboard: false,
      reflow: true,
      spacing: true,
      german: true,
      headings: true,
    },
  },
  {
    id: "S-16",
    kind: "screen",
    route: workspaceRoute("settings"),
    routePattern: workspacePattern,
    tab: "settings",
    setup: async (page, culture) => {
      await allowLongWalk();
      return installWorkspace(page, culture, costOwnerMock(false));
    },
    // The states follow the Cost owner panel on one page load. The keyboard walk is off on purpose: hosting-cost-owner.spec.ts
    // transfers the cost owner with the keyboard only (picker, Transfer button, dialog) and a walk per state of this entry
    // would exceed the test timeout (see allowLongWalk, RESEARCH Pitfall 10). Axe, reflow, spacing, German and the heading
    // outline still visit every state.
    states: [
      { name: "viewer without canModify", ready: costOwnerReadOnlyReady },
      {
        name: "modifier with the picker closed",
        open: becomeModifier,
        ready: costOwnerTransferReady,
      },
      {
        name: "picker open with the empty query",
        open: openOwnerPicker,
        ready: ownerPickerReady,
      },
      { name: "frozen current owner", open: freezeCurrentOwner, ready: frozenOwnerReady },
      {
        name: "transfer unavailable while a machine is present",
        open: patchServerThenRefresh(machinePresent),
        ready: transferReasonChangedReady,
      },
      {
        name: "transfer unavailable while jobs are pending",
        open: patchServerThenRefresh(jobsPending),
        ready: transferReasonChangedReady,
      },
    ],
    audit: {
      axe: true,
      keyboard: false,
      reflow: true,
      spacing: true,
      german: true,
      headings: true,
    },
  },
  {
    id: "O-02",
    kind: "overlay",
    route: workspaceRoute("settings"),
    setup: async (page, culture) => {
      // Forty rows add forty remove buttons to each keyboard walk of the dialog.
      await allowLongWalk();
      return installWorkspace(page, culture, {
        server: offServer(),
        canManageSecrets: true,
        configurationEntryCount: 40,
      });
    },
    states: [
      { name: "review changes", open: openReviewDialog, ready: reviewReady },
      { name: "10 staged changes", open: openReviewWithChanges(10), ready: reviewListsChanges(10) },
      { name: "40 staged changes", open: openReviewWithChanges(40), ready: reviewListsChanges(40) },
    ],
    audit: fullAudit,
    baseline: "K-12",
  },
  {
    id: "O-03",
    kind: "overlay",
    route: workspaceRoute("settings"),
    setup: (page, culture) =>
      installWorkspace(page, culture, { server: offServer(), canManageSecrets: true }),
    states: [
      {
        name: "leave with staged changes",
        open: openLeaveDialog,
        ready: leaveReady,
        baseline: "K-17",
      },
      {
        name: "leave with automation changes only",
        open: openAutomationOnlyLeaveDialog,
        ready: leaveAutomationOnlyReady,
      },
    ],
    audit: fullAudit,
  },
  {
    id: "O-05",
    kind: "overlay",
    route: workspaceRoute("settings"),
    setup: (page, culture) =>
      installWorkspace(page, culture, { server: offServer(), canManageSecrets: true }),
    states: [{ name: "delete server", open: openDeleteDialog, ready: deleteReady }],
    audit: fullAudit,
  },
  {
    id: "O-10",
    kind: "overlay",
    route: workspaceRoute("settings"),
    setup: async (page, culture) => {
      await allowLongWalk();
      return installWorkspace(page, culture, costOwnerMock(true));
    },
    states: [
      {
        name: "idle",
        open: openTransferDialog,
        ready: transferIdleReady,
        baseline: "K-19",
      },
      // A busy dialog ignores Escape on purpose, so the keyboard walk leaves it open for the next state.
      {
        name: "busy",
        open: confirmHeldTransfer,
        ready: transferBusyReady,
        dialogStaysOpen: true,
      },
      { name: "409 error", open: failHeldTransfer, ready: transferErrorReady },
    ],
    audit: fullAudit,
  },
  {
    id: "O-11",
    kind: "overlay",
    route: workspaceRoute("settings"),
    setup: async (page, culture) => {
      await allowLongWalk();
      return installWorkspace(page, culture, {
        server: offServer(),
        settingsPanels: ["server-access"],
        accessState: accessDialogStates(),
      });
    },
    states: [
      {
        name: "remove all access",
        open: openRemoveAllDialog("Bravo"),
        ready: dismissFocusedReady,
      },
      {
        name: "remove all access on the own row",
        open: openRemoveAllDialog("Operator"),
        ready: dismissFocusedReady,
      },
      { name: "self-removal", open: openSelfRemovalDialog, ready: dismissFocusedReady },
    ],
    audit: fullAudit,
  },
  {
    id: "N-05",
    kind: "notice",
    route: workspaceRoute(),
    setup: (page, culture) =>
      installWorkspace(page, culture, {
        server: { isArchived: true, deployment: null, actions: [], canModify: false },
        accrued: false,
      }),
    states: [{ name: "archived", ready: noticeReady(".workspace-shell > .state-panel--info") }],
    audit: fullAudit,
  },
  {
    id: "N-06",
    kind: "notice",
    route: workspaceRoute(),
    setup: (page, culture) =>
      installWorkspace(page, culture, {
        server: {
          ...offServer(),
          hasChangesPendingStart: true,
          changesPendingStartAreas: ["ModPack", "Configuration", "Credentials"],
        },
      }),
    states: [{ name: "pending start", ready: noticeReady(".pending-start-notice__areas") }],
    audit: fullAudit,
  },
  {
    id: "N-07",
    kind: "notice",
    route: workspaceRoute(),
    setup: (page, culture) =>
      installWorkspace(page, culture, {
        alerts: [
          costAlert({
            id: "a1000000-0000-4000-8000-000000000001",
            serverId: workspaceServerId,
            serverName: "Alpha Ops",
          }),
          costAlert({
            id: "a1000000-0000-4000-8000-000000000002",
            serverId: workspaceServerId,
            serverName: "Alpha Ops",
            thresholdAmount: 10,
            accruedAmount: 10.03,
            createdAt: "2026-11-21T15:05:00Z",
          }),
        ] as unknown as JsonRecord[],
      }),
    states: [{ name: "cost alert banner", ready: noticeReady(".cost-alert-banner") }],
    audit: fullAudit,
  },
  {
    id: "S-07",
    kind: "screen",
    route: automationRoute,
    routePattern: workspacePattern,
    tab: "settings",
    setup: async (page, culture) => {
      await allowLongWalk();
      return installAutomationWorkspace(page, culture, { automation: savedAutomation("Given") });
    },
    // The time zone chooser is a native select. Its closed state is audited here; the open list is browser UI that
    // adds no page content, and every option it lists is already in the DOM that axe scans.
    states: [
      { name: "saved settings", ready: automationArrivalReady },
      { name: "read-only", open: openReadOnly, ready: readOnlyReady },
      { name: "unavailable", open: openUnavailable, ready: unavailableReady },
    ],
    audit: fullAudit,
    baseline: "K-07",
  },
  {
    id: "O-04",
    kind: "overlay",
    route: automationRoute,
    setup: (page, culture) =>
      installAutomationWorkspace(page, culture, { automation: automationBlock() }),
    states: [{ name: "scheduled-start consent", open: openConsentDialog, ready: consentReady }],
    audit: fullAudit,
    baseline: "K-13",
  },
  {
    id: "N-08",
    kind: "notice",
    route: workspaceRoute(),
    setup: (page, culture) =>
      installAutomationWorkspace(page, culture, {
        automation: pendingStopAutomation(),
        alerts: [
          costAlert({
            id: "a1000000-0000-4000-8000-000000000001",
            serverId: workspaceServerId,
            serverName: "Alpha Ops",
          }),
        ] as unknown as JsonRecord[],
      }),
    states: [{ name: "automatic shutdown above cost alert", ready: noticesReady }],
    audit: fullAudit,
    baseline: "K-14",
  },
  {
    id: "N-09",
    kind: "notice",
    route: automationRoute,
    setup: (page, culture) =>
      installAutomationWorkspace(page, culture, { automation: savedAutomation("Needed") }),
    states: [
      {
        name: "consent needed",
        ready: async (page) => {
          await automationReady(page);
          await page.locator("#server-automation .state-panel--warning").waitFor();
        },
      },
    ],
    audit: fullAudit,
  },
  {
    id: "N-10",
    kind: "notice",
    route: workspaceRoute(),
    setup: (page, culture) =>
      installWorkspace(page, culture, { jobs: failedHistory(), operations: [] }),
    states: [
      {
        name: "job failure",
        ready: async (page) => {
          await shellReady(page);
          await page.locator(".job-failure").waitFor();
        },
      },
    ],
    audit: fullAudit,
  },
  {
    id: "N-11",
    kind: "notice",
    route: workspaceRoute(),
    setup: (page, culture) =>
      installWorkspace(page, culture, {
        jobs: [
          workspaceJob({
            gameServerId: workspaceServerId,
            kind: "Start",
            actionId: "start",
            status: "Running",
            stage: "provider-creating",
            progressPercent: 20,
            requestedByName: "Alpha",
            timeStampCreated: "2026-11-16T09:50:00Z",
            timeStampUpdated: "2026-11-16T09:55:00Z",
            timeStampStarted: "2026-11-16T09:51:00Z",
          }),
        ] as unknown as JsonRecord[],
        server: { pendingJobCount: 1 },
      }),
    states: [
      {
        name: "job progress",
        ready: async (page) => {
          await shellReady(page);
          await page.locator(".job-progress").waitFor();
        },
      },
    ],
    audit: fullAudit,
  },
];

/** Required ids the workspace slice has not added yet; plan 69-27 empties this list. */
export const workspacePending: readonly string[] = [];
