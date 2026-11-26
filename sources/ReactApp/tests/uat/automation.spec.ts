import { expect, test, type Locator, type Page } from "@playwright/test";
import { assertNoGermanLeak } from "../e2e/support/germanLeak";
import {
  designShot,
  evidence,
  listMail,
  openProfileMenu,
  openServer,
  readAutomationHandOver,
  uatAutomationTitles,
  useCulture,
  writeAutomationHandOver,
  type MailpitMessage,
} from "./support/uat";

/**
 * How long an automatic stop may take from saving a setting until its warning arrives: the idle clock needs one idle
 * minute (UAT timing), plus the 5 second evaluation interval and the live update.
 */
const warningWait = 4 * 60_000;

/** How long the stop that follows a warning by one lead (1 minute in the UAT profile) may take to complete. */
const stopWait = 6 * 60_000;

/** The sentence of a finished job in Activity. */
const stopFinished = "“Shut down and stop billing” finished.";

/** The trigger line of the schedule trigger. */
const scheduleTrigger = "Automatic: schedule";

/** The server ids the specs found, keyed by the `uatAutomationTitles` key. */
const serverIds: Record<string, string> = {};

/**
 * Escapes a text for use inside a regular expression.
 *
 * @param text The literal text.
 * @returns The escaped text.
 */
function escapeRegExp(text: string) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Opens a tab of the workspace of a server directly by id.
 *
 * @param page The page to navigate.
 * @param id The server id.
 * @param title The server title the heading must show.
 * @param tab The workspace tab, for example `settings` or `activity`.
 */
async function openTab(page: Page, id: string, title: string, tab: "settings" | "activity") {
  await page.goto(`command/servers/${id}?tab=${tab}`);
  await expect(page.getByRole("heading", { level: 1, name: title })).toBeVisible({
    timeout: 60_000,
  });
}

/**
 * Finds a server by title on the fleet page, remembers its id and opens a tab of its workspace.
 *
 * @param page The page to navigate.
 * @param key The `uatAutomationTitles` key of the server.
 * @param tab The workspace tab to open.
 * @returns The server id.
 */
async function openAutomationServer(
  page: Page,
  key: keyof typeof uatAutomationTitles,
  tab: "settings" | "activity" = "settings",
): Promise<string> {
  const title = uatAutomationTitles[key];
  const id = serverIds[key] ?? (await openServer(page, title));
  serverIds[key] = id;
  await openTab(page, id, title, tab);
  return id;
}

/**
 * The UTC clock time a number of whole minutes ahead, as the native time input writes it.
 *
 * The result is rounded down to the minute, so the instant lies between `minutes - 1` and `minutes` minutes ahead.
 *
 * @param minutes How many minutes ahead.
 * @returns The time as `HH:mm`.
 */
function utcTimeAhead(minutes: number): string {
  const target = new Date(Math.floor(Date.now() / 60_000) * 60_000 + minutes * 60_000);
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${pad(target.getUTCHours())}:${pad(target.getUTCMinutes())}`;
}

/**
 * Saves the automation panel and waits for the confirmation, allowing scheduled starts when the dialog asks.
 *
 * @param page The page that shows the Settings tab.
 */
async function saveAutomation(page: Page) {
  await page.getByRole("button", { name: "Save automation settings" }).click();
  const dialog = page.locator("dialog[open]");
  if (await dialog.isVisible({ timeout: 3_000 }).catch(() => false)) {
    await expect(dialog.getByRole("heading", { name: "Allow scheduled starts?" })).toBeVisible();
    await dialog.getByRole("button", { name: "Allow scheduled starts" }).click();
    await expect(dialog).toHaveCount(0);
  }
  await expect(
    page.locator(".automation-panel__status").getByText("Saved.", { exact: true }),
  ).toBeVisible();
}

/**
 * Adds one schedule rule in UTC through the panel.
 *
 * @param page The page that shows the Settings tab.
 * @param action The label of the action radio, `Start server` or `Shut down and stop billing`.
 * @param time The UTC time as `HH:mm`.
 * @returns The rule group.
 */
async function addRule(page: Page, action: string, time: string): Promise<Locator> {
  await page.getByLabel("Time zone", { exact: true }).selectOption("UTC");
  await page.getByRole("button", { name: "Add a schedule" }).click();
  const rule = page.getByRole("group", { name: "Schedule 1", exact: true });
  await expect(rule).toBeVisible();
  await rule.getByRole("radio", { name: new RegExp(`^${escapeRegExp(action)}`) }).check();
  await rule.getByLabel("Time", { exact: true }).fill(time);
  const days = rule.getByRole("checkbox");
  await expect(days).toHaveCount(7);
  for (let index = 0; index < 7; index += 1) await days.nth(index).check();
  return rule;
}

/**
 * The automatic shutdown notice of the workspace.
 *
 * @param page The page that shows the workspace.
 * @returns The locator of the warning panel.
 */
function stopNotice(page: Page): Locator {
  return page
    .locator("section.state-panel--warning")
    .filter({ has: page.getByRole("heading", { level: 2, name: /^Automatic shutdown at / }) });
}

/**
 * The warning toast of an automatic shutdown of one server.
 *
 * @param page The page that shows the workspace.
 * @param title The server title.
 * @returns The locator of the toast.
 */
function stopToast(page: Page, title: string): Locator {
  return page.locator(".app-toast").filter({
    hasText: new RegExp(`^Automatic shutdown at .*: ${escapeRegExp(title)}\\.`),
  });
}

/**
 * The activity entries of the open Activity tab.
 *
 * @param page The page that shows the Activity tab.
 * @returns The locator of all entries.
 */
function activityEntries(page: Page): Locator {
  return page.locator("article.activity-entry");
}

/**
 * Waits until an activity entry appears, reloading the page between attempts so the wait does not depend on a live
 * update.
 *
 * @param page The page that shows the Activity tab.
 * @param entry The entry locator to wait for.
 * @param timeout How long to wait in total.
 */
async function waitForEntry(page: Page, entry: Locator, timeout: number) {
  await expect(async () => {
    await page.reload();
    await expect(entry.first()).toBeVisible({ timeout: 8_000 });
  }).toPass({ timeout, intervals: [4_000] });
}

/**
 * Shows the Audit filter of the Activity tab.
 *
 * @param page The page that shows the Activity tab.
 */
async function showAudit(page: Page) {
  await page.getByRole("button", { name: /^Audit/ }).click();
  await expect(page.getByRole("button", { name: /^Audit/ })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
}

/**
 * The automatic shutdown emails in the Mailpit inbox that name a server.
 *
 * @param page The page whose request context is used.
 * @param title The server title, or null for every automatic shutdown email.
 * @returns The messages, newest first.
 */
async function stopMail(page: Page, title: string | null): Promise<MailpitMessage[]> {
  const messages = await listMail(page.request);
  return messages.filter((message) =>
    title === null
      ? message.Subject.startsWith("Automatic shutdown of ")
      : message.Subject.startsWith(`Automatic shutdown of ${title} at `),
  );
}

/**
 * Asserts the whole observable chain of one automatic stop that was warned for: notice, toast, menu row, mail, the
 * finished job with its trigger line and the server turning Off.
 *
 * @param page The page that shows the Settings tab of the server with the stop already configured.
 * @param key The `uatAutomationTitles` key of the server.
 * @param reasonNotice The reason sentence the notice shows.
 * @param trigger The trigger line the stop job shows in Activity.
 * @param shot The base name of the design screenshots.
 */
async function observeAutomaticStop(
  page: Page,
  key: keyof typeof uatAutomationTitles,
  reasonNotice: string,
  trigger: string,
  shot: string,
) {
  const title = uatAutomationTitles[key];
  const id = serverIds[key] ?? "";
  expect(id, `id of ${title}`).not.toBe("");

  // The warning: a toast, the notice with its reason and the menu row arrive without a reload.
  const toast = stopToast(page, title);
  await expect(toast).toBeVisible({ timeout: warningWait });
  expect(await toast.first().getAttribute("data-intent")).toBe("warning");
  evidence(`toast: ${(await toast.first().textContent())?.trim()}`);
  const notice = stopNotice(page);
  await expect(notice).toBeVisible({ timeout: warningWait });
  await expect(notice).toContainText(reasonNotice);
  await expect(notice).toContainText("It saves its data, stops, and releases the cloud machine.");
  evidence(`notice: ${(await notice.innerText()).replace(/\s+/g, " ")}`);
  await designShot(page, `${shot}-notice`);

  const menu = await openProfileMenu(page);
  const rows = menu.locator("[data-auto-stop-alert-id]").filter({ hasText: title });
  await expect(rows).toHaveCount(1);
  await expect(rows).toContainText(/Automatic shutdown at /);
  evidence(`menu row: ${(await rows.innerText()).replace(/\s+/g, " ")}`);
  await page.keyboard.press("Escape");

  // The email to the cost owner names the server.
  await expect.poll(async () => (await stopMail(page, title)).length, { timeout: 90_000 }).toBe(1);
  evidence(`Mailpit: "${(await stopMail(page, title))[0]?.Subject}"`);

  // One lead later the automatic job runs and the server turns Off; the notice leaves.
  await openTab(page, id, title, "activity");
  const job = activityEntries(page).filter({ hasText: stopFinished }).filter({ hasText: trigger });
  await waitForEntry(page, job, stopWait);
  await expect(job).toHaveCount(1);
  evidence(`job: ${(await job.first().innerText()).replace(/\s+/g, " ")}`);
  await expect(page.locator(".server-state-badge").first()).toHaveText("Off", {
    timeout: 60_000,
  });
  await expect(stopNotice(page)).toHaveCount(0);

  // Exactly one warning and one request in the audit, and no terminate escalation (D-AUTO-2).
  await showAudit(page);
  await expect(
    activityEntries(page).filter({
      hasText: "A warning was sent that the server will shut down automatically.",
    }),
  ).toHaveCount(1);
  await expect(
    activityEntries(page).filter({ hasText: "An automatic shutdown was requested." }),
  ).toHaveCount(1);
  await expect(page.locator("section.activity-section")).not.toContainText(/terminat/i);
  await designShot(page, `${shot}-audit`);
}

test.describe.serial("68 automatic shutdown and schedules on the seeded stack", () => {
  test("68-1 the cost owner turns the email channel on from the cost strip @fresh", async ({
    page,
  }) => {
    expect(await stopMail(page, null), "no automatic shutdown email before any rule").toHaveLength(
      0,
    );
    const id = await openServer(page, uatAutomationTitles.idle);
    serverIds.idle = id;
    await page
      .locator("dl.cost-strip")
      .getByRole("link", { name: /^(Set an alert|Manage alerts)$/ })
      .click();
    await expect(page.getByRole("heading", { level: 1, name: "Cost alerts" })).toBeVisible();
    const email = page.getByRole("switch", { name: /^Email/ });
    if (!(await email.isChecked())) await email.check();
    await page.getByRole("button", { name: "Save alert settings" }).click();
    await expect(
      page.locator(".cost-alert-settings__status").getByText("Saved.", { exact: true }),
    ).toBeVisible();
    await page.reload();
    await expect(page.getByRole("switch", { name: /^Email/ })).toBeChecked();
    evidence("email channel is on for the UAT cost owner (no amounts set)");
  });

  test("68-2 UAT 11: idle shutdown warns, then shuts the idle server down once @fresh", async ({
    page,
  }) => {
    await openAutomationServer(page, "idle");
    await expect(page.locator("[data-slot=automation]")).toBeVisible();

    // The owner switches idle shutdown on at the minimum of one minute through the panel.
    await page.getByLabel("Shut down when idle").check();
    await page.getByLabel("Idle time (minutes)").fill("1");
    await saveAutomation(page);
    await expect(page.getByText(/Idle since /)).toBeVisible({ timeout: 90_000 });
    evidence(
      `status: ${(
        await page
          .getByText(/Idle since /)
          .first()
          .innerText()
      ).trim()}`,
    );
    await designShot(page, "68-idle-panel");

    await observeAutomaticStop(
      page,
      "idle",
      "Nobody has been on the server for 1 minute. It shuts down unless someone joins.",
      "Automatic: nobody was on the server for 1 minute",
      "68-idle",
    );
  });

  test("68-3 UAT 12: a maximum runtime below the elapsed runtime stops although players are online @fresh", async ({
    page,
  }) => {
    await openAutomationServer(page, "maxRuntime");
    await page.getByLabel("Limit the runtime").check();
    await page.getByLabel("Maximum runtime (hours)").fill("1");
    await saveAutomation(page);

    await observeAutomaticStop(
      page,
      "maxRuntime",
      "The server reached its maximum runtime of 1 hour.",
      "Automatic: maximum runtime of 1 hour reached",
      "68-maximum-runtime",
    );
  });

  test("68-4 UAT 13: a stop rule warns one lead before and stops at its time @fresh", async ({
    page,
  }) => {
    await openAutomationServer(page, "scheduledStop");
    await addRule(page, "Shut down and stop billing", utcTimeAhead(3));
    await saveAutomation(page);
    await expect(page.getByText("Next scheduled action")).toBeVisible();
    evidence(
      `status: ${(await page.getByText("Next scheduled action").locator("xpath=..").innerText()).replace(/\s+/g, " ")}`,
    );
    await designShot(page, "68-schedule-panel");

    await observeAutomaticStop(
      page,
      "scheduledStop",
      "A schedule shuts the server down at this time.",
      scheduleTrigger,
      "68-scheduled-stop",
    );

    // The rule remembers what it did.
    await openTab(
      page,
      serverIds.scheduledStop ?? "",
      uatAutomationTitles.scheduledStop,
      "settings",
    );
    await expect(page.getByText(/Last run, .*: shut the server down\./)).toBeVisible({
      timeout: 60_000,
    });
    evidence(`rule: ${await page.getByText(/Last run, .*: shut the server down\./).innerText()}`);
  });

  test("68-5 UAT 14: a start rule with consent queues a Start server job by the schedule @fresh", async ({
    page,
  }) => {
    await openAutomationServer(page, "scheduledStart");
    await addRule(page, "Start server", utcTimeAhead(2));
    await saveAutomation(page);
    await expect(page.getByText("Next scheduled action")).toBeVisible();

    await openTab(
      page,
      serverIds.scheduledStart ?? "",
      uatAutomationTitles.scheduledStart,
      "activity",
    );
    const start = activityEntries(page)
      .filter({ hasText: "“Start server”" })
      .filter({ hasText: scheduleTrigger });
    await waitForEntry(page, start, 4 * 60_000);
    evidence(`job: ${(await start.first().innerText()).replace(/\s+/g, " ")}`);
    // The start passes its preflight: it is waiting for its worker or in progress, not failed right away.
    await expect(start.first()).not.toContainText("did not finish");
    await showAudit(page);
    await expect(
      activityEntries(page).filter({ hasText: "A scheduled start was requested." }),
    ).toHaveCount(1);
    await designShot(page, "68-scheduled-start");
    evidence(
      "Deferred follow-up: the completion of this start is not asserted, because the UAT worker emulation registers no worker for a freshly provisioned deployment (milestone-end real-environment check).",
    );
  });

  test("68-6 UAT 15 shows its skipped start and UAT 16 its paused starts @fresh", async ({
    page,
  }) => {
    await openAutomationServer(page, "startSkipped");
    await expect(
      page.getByText(/Last run, .*: skipped, the cost owner could not pay for a start\./),
    ).toBeVisible({
      timeout: 5 * 60_000,
    });
    evidence(
      `UAT 15: ${await page.getByText(/Last run, .*: skipped, the cost owner could not pay for a start\./).innerText()}`,
    );
    await designShot(page, "68-start-skipped");

    await openAutomationServer(page, "startsPaused");
    await expect(page.getByText("Scheduled starts are paused", { exact: true })).toBeVisible();
    await expect(page.getByText("Paused until scheduled starts are confirmed.")).toBeVisible();
    evidence("UAT 16: 'Scheduled starts are paused' notice and the paused rule line are shown");
    await designShot(page, "68-starts-paused");

    // Hand the observations to the restarted phase.
    for (const key of ["idle", "maxRuntime", "scheduledStop", "scheduledStart"] as const) {
      expect(serverIds[key], `id of ${key}`).toBeTruthy();
    }
    writeAutomationHandOver({
      serverIds: { ...serverIds },
      finishedAt: new Date().toISOString(),
      autoStopSubjects: (await stopMail(page, null)).map((message) => message.Subject),
    });
  });

  test("69-32 German pass: the Automation panel of UAT 16 shows no English text @fresh", async ({
    page,
  }) => {
    await useCulture(page, "de");
    await openAutomationServer(page, "startsPaused");
    // UAT 16 has a schedule rule with paused starts, so the panel shows its rule, notice and status lines.
    const panel = page.locator(".automation-panel");
    await expect(panel).toBeVisible();
    await expect(panel.locator(".automation-panel__subsection").first()).toBeVisible();
    // The seeded server title is user content; the workspace header and the breadcrumb isolate it in `bdi`, so the
    // detector skips it and every text on the page must pass.
    await assertNoGermanLeak(page);
    evidence("UAT 16 Settings in German: the Automation panel shows no English text");
  });

  test("68-7 after a restart every automatic stop is in Activity once and no email is sent again @restarted", async ({
    page,
  }) => {
    const handOver = readAutomationHandOver();
    // Let the restarted evaluator run several cycles (5 s each) before looking.
    await page.waitForTimeout(30_000);

    for (const key of ["idle", "maxRuntime", "scheduledStop"] as const) {
      const title = uatAutomationTitles[key];
      const id = handOver.serverIds[key] ?? "";
      expect(id, `hand-over id of ${title}`).not.toBe("");
      await openTab(page, id, title, "activity");
      await expect(page.locator(".server-state-badge").first()).toHaveText("Off");

      const jobs = activityEntries(page)
        .filter({ hasText: "“Shut down and stop billing”" })
        .filter({ hasText: "Automatic:" });
      await expect(jobs).toHaveCount(1);
      await showAudit(page);
      await expect(
        activityEntries(page).filter({ hasText: "An automatic shutdown was requested." }),
      ).toHaveCount(1);
      await expect(
        activityEntries(page).filter({
          hasText: "A warning was sent that the server will shut down automatically.",
        }),
      ).toHaveCount(1);
      await expect(page.locator("section.activity-section")).not.toContainText(/terminat/i);
      evidence(
        `${title}: Off, 1 automatic stop job, 1 AutomaticStopRequested, 1 AutomaticStopWarned`,
      );
    }

    // The rule of UAT 13 still lists what it did.
    await openTab(
      page,
      handOver.serverIds.scheduledStop ?? "",
      uatAutomationTitles.scheduledStop,
      "settings",
    );
    await expect(page.getByText(/Last run, .*: shut the server down\./)).toBeVisible();

    // No automatic shutdown email arrived after the fresh phase finished, and none is in the inbox twice. The inbox
    // itself may be empty after the restart (Mailpit keeps its messages in memory), so only repeats are asserted.
    const messages = await stopMail(page, null);
    const after = messages.filter(
      (message) => Date.parse(message.Created) > Date.parse(handOver.finishedAt),
    );
    expect(after, "no automatic shutdown email after the restart").toHaveLength(0);
    const subjects = messages.map((message) => message.Subject);
    expect(new Set(subjects).size, "no automatic shutdown email is in the inbox twice").toBe(
      subjects.length,
    );
    expect(
      subjects.filter((subject) => !handOver.autoStopSubjects.includes(subject)),
      "every email in the inbox was already sent in the fresh phase",
    ).toEqual([]);
    evidence(
      `Mailpit: ${subjects.length} automatic shutdown emails (${handOver.autoStopSubjects.length} in the fresh phase), 0 since ${handOver.finishedAt}`,
    );
  });

  test("68-8 an automatic stop of a real worker on a real Hetzner machine @fresh", () => {
    test.skip(
      true,
      "Deferred follow-up: real worker and real Hetzner automatic stop, run at the milestone end.",
    );
  });
});
