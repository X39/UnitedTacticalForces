import { expect, test, type Locator, type Page } from "@playwright/test";
import process from "node:process";
import {
  automaticStopJob,
  automationAuditOperations,
  automationBlock,
  automationCostOwnerId,
  automationServer,
  automationServerId,
  automationServerTitle,
  mockAutomationRoutes,
  type AutomationCulture,
  type AutomationRouteHandle,
  type AutomationSaveBody,
} from "./support/automationMocks";
import { costAlertRaisedFrame, fleetChangedFrame, mockFleetStream } from "./support/fleetStream";

/** The instant the mocked automatic stop is announced for; the text shows it in the viewer's zone. */
const stopsAt = "2026-11-23T20:30:00Z";

/** The visible copy each test reads, per culture. Times are matched loosely because their format is locale. */
const copy = {
  en: {
    panelTitle: "Automatic shutdown and schedules",
    idleEnable: "Shut down when idle",
    idleMinutes: "Idle time (minutes)",
    maxEnable: "Limit the runtime",
    maxHours: "Maximum runtime (hours)",
    addSchedule: "Add a schedule",
    time: "Time",
    startRadio: "Start server",
    stopRadio: "Shut down and stop billing",
    save: "Save automation settings",
    allow: "Allow scheduled starts",
    consentTitle: "Allow scheduled starts?",
    statusSaved: "Saved.",
    toastSaved: "Automation settings saved.",
    conflict: "These settings were changed elsewhere. Reload them and try again.",
    reload: "Reload settings",
    invalidOne: "Fix the highlighted field before saving.",
    readOnly:
      "Only the server owner or an administrator can change these settings. You can see what is set.",
    noticeTitle: /^Automatic shutdown at /,
    noticeReason:
      "Nobody has been on the server for 30 minutes. It shuts down unless someone joins.",
    toastWarning: /^Automatic shutdown at .*: Automation test server\./,
    toastCancelled: "Automatic shutdown cancelled: someone joined Automation test server.",
    menuLine: /Automatic shutdown at /,
    trigger: "Automatic: nobody was on the server for 30 minutes",
    auditEntry: "An automatic shutdown was requested.",
    auditFilter: "Audit",
    overviewFact: "Automation",
    overviewIdle: "Shuts down after 30 minutes without players",
    overviewMax: "Shuts down after 6 hours of running",
    overviewLink: "Open automation settings",
    menuTrigger: "Open profile menu for Operator",
    menuName: "Profile menu",
    scheduleOne: "Schedule 1",
    scheduleTwo: "Schedule 2",
  },
  de: {
    panelTitle: "Automatisches Herunterfahren und Zeitpläne",
    idleEnable: "Bei Leerlauf herunterfahren",
    idleMinutes: "Leerlaufzeit (Minuten)",
    maxEnable: "Laufzeit begrenzen",
    maxHours: "Maximale Laufzeit (Stunden)",
    addSchedule: "Zeitplan hinzufügen",
    time: "Uhrzeit",
    startRadio: "Server starten",
    stopRadio: "Herunterfahren und Abrechnung beenden",
    save: "Automatisierung speichern",
    allow: "Geplante Starts erlauben",
    consentTitle: "Geplante Starts erlauben?",
    statusSaved: "Gespeichert.",
    toastSaved: "Automatisierungseinstellungen gespeichert.",
    conflict: "Diese Einstellungen wurden anderswo geändert. Lade sie neu und versuche es erneut.",
    reload: "Einstellungen neu laden",
    invalidOne: "Korrigiere das markierte Feld, bevor du speicherst.",
    readOnly:
      "Nur der Serverbesitzer oder eine Administration kann diese Einstellungen ändern. Du siehst, was eingestellt ist.",
    noticeTitle: /^Automatisches Herunterfahren um /,
    noticeReason:
      "Seit 30 Minuten war niemand auf dem Server. Er fährt herunter, wenn niemand beitritt.",
    toastWarning: /^Automatisches Herunterfahren um .*: Automation test server\./,
    toastCancelled:
      "Automatisches Herunterfahren abgebrochen: Jemand ist Automation test server beigetreten.",
    menuLine: /Automatisches Herunterfahren um /,
    trigger: "Automatisch: 30 Minuten lang war niemand auf dem Server",
    auditEntry: "Ein automatisches Herunterfahren wurde angefordert.",
    auditFilter: "Audit",
    overviewFact: "Automatisierung",
    overviewIdle: "Fährt nach 30 Minuten ohne Spieler herunter",
    overviewMax: "Fährt nach 6 Stunden Laufzeit herunter",
    overviewLink: "Automatisierungseinstellungen öffnen",
    menuTrigger: "Profilmenü für Operator öffnen",
    menuName: "Profilmenü",
    scheduleOne: "Zeitplan 1",
    scheduleTwo: "Zeitplan 2",
  },
} as const;

/** The automation block of a pending automatic stop announced for 30 idle minutes. */
function pendingIdleStop() {
  return automationBlock({
    settings: { idleShutdownEnabled: true, idleShutdownMinutes: 30 },
    status: {
      idle: { state: "Idle", since: "2026-11-23T20:00:00Z", stopsAt },
      pendingStop: { reason: "Idle", stopsAt, limit: 30 },
    },
  });
}

/**
 * Opens the Settings section on the automation panel through the deep link and waits until the panel rendered.
 *
 * @param page The page to navigate.
 * @param culture The culture of the session.
 */
async function openAutomation(page: Page, culture: AutomationCulture) {
  await page.goto(`command/servers/${automationServerId}?tab=settings#server-automation`);
  await expect(
    page.getByRole("heading", { level: 2, name: copy[culture].panelTitle }),
  ).toBeVisible();
}

/**
 * Opens a workspace section and waits until the workspace heading rendered.
 *
 * @param page The page to navigate.
 * @param query The query string, for example `?tab=activity`.
 */
async function openWorkspace(page: Page, query = "") {
  await page.goto(`command/servers/${automationServerId}${query}`);
  await expect(page.getByRole("heading", { level: 1, name: automationServerTitle })).toBeVisible();
}

/** The automation panel card. */
function panel(page: Page): Locator {
  return page.locator("#server-automation");
}

/** One schedule rule block of the panel. */
function rule(page: Page, name: string): Locator {
  return panel(page).getByRole("group", { name, exact: true });
}

/** The warning notice of a pending automatic stop. */
function notice(page: Page): Locator {
  return page.locator(".auto-stop-notice");
}

/**
 * Waits until the page opened a new update stream connection, which is when a frame can be delivered to it.
 *
 * @param stream The mocked stream.
 * @param before The connection count before the navigation; a second navigation opens a second connection.
 */
async function waitForStream(stream: Awaited<ReturnType<typeof mockFleetStream>>, before = 0) {
  await expect.poll(() => stream.connectionCount()).toBeGreaterThan(before);
}

/**
 * Installs the mocks of one test with the default owner server and the quiet update stream.
 *
 * @param page The page to mock.
 * @param culture The culture of the session.
 * @param options Members of the route options to replace.
 */
async function install(
  page: Page,
  culture: AutomationCulture,
  options: Partial<Parameters<typeof mockAutomationRoutes>[1]> = {},
) {
  const handle = await mockAutomationRoutes(page, {
    server: automationServer(),
    canManage: true,
    culture,
    ...options,
  });
  const stream = await mockFleetStream(page);
  return { handle, stream };
}

/**
 * Sets idle shutdown to 30 minutes and the maximum runtime to 6 hours, then adds a Friday 18:00 start and a Saturday
 * 00:00 stop rule. Used by every test that needs a dirty draft.
 *
 * @param page The page with the panel open.
 * @param culture The culture of the session.
 */
async function fillOwnerDraft(page: Page, culture: AutomationCulture) {
  const text = copy[culture];
  await panel(page).getByRole("switch", { name: text.idleEnable }).check();
  await panel(page).getByLabel(text.idleMinutes).fill("30");
  await panel(page).getByRole("switch", { name: text.maxEnable }).check();
  await panel(page).getByLabel(text.maxHours).fill("6");

  await panel(page).getByRole("button", { name: text.addSchedule }).click();
  const first = rule(page, text.scheduleOne);
  await expect(first).toBeVisible();
  await first.getByRole("radio", { name: text.startRadio }).check();
  await first.getByLabel(text.time, { exact: true }).fill("18:00");
  await first.locator('input[data-field="days"]').nth(4).check();

  await panel(page).getByRole("button", { name: text.addSchedule }).click();
  const second = rule(page, text.scheduleTwo);
  await expect(second).toBeVisible();
  await second.getByRole("radio", { name: text.stopRadio }).check();
  await second.getByLabel(text.time, { exact: true }).fill("00:00");
  await second.locator('input[data-field="days"]').nth(5).check();
}

/**
 * Asserts the one PUT the owner journey sends.
 *
 * @param handle The route handle.
 */
function expectOwnerSave(handle: AutomationRouteHandle) {
  expect(handle.saves).toHaveLength(1);
  const body = handle.saves[0] as AutomationSaveBody;
  expect(body).toMatchObject({
    version: 0,
    idleShutdownEnabled: true,
    idleShutdownMinutes: 30,
    maxRuntimeEnabled: true,
    maxRuntimeHours: 6,
    startConsent: true,
  });
  expect(body.schedules).toMatchObject([
    { action: "Start", days: [5], time: "18:00" },
    { action: "Stop", days: [6], time: "00:00" },
  ]);
}

for (const culture of ["en", "de"] as const) {
  test.describe(`automation owner journey (${culture})`, () => {
    test.use({ viewport: { width: 1280, height: 900 } });

    test(`the owner sets idle shutdown, maximum runtime and a schedule and saves (${culture})`, async ({
      page,
    }) => {
      const text = copy[culture];
      const { handle } = await install(page, culture);

      await openAutomation(page, culture);
      // The deep link focuses the Automation header button once the data is there.
      await expect(page.getByRole("button", { name: text.panelTitle })).toBeFocused();

      await fillOwnerDraft(page, culture);
      await panel(page).getByRole("button", { name: text.save }).click();

      // A new start rule asks for the cost consent first; nothing is sent before it.
      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole("heading", { name: text.consentTitle })).toBeVisible();
      expect(handle.saves).toEqual([]);

      await dialog.getByRole("button", { name: text.allow }).click();
      await expect(page.locator("dialog[open]")).toHaveCount(0);
      await expect(
        panel(page).getByRole("status").filter({ hasText: text.statusSaved }),
      ).toBeVisible();
      await expect(page.locator(".app-toast").filter({ hasText: text.toastSaved })).toHaveCount(1);
      expectOwnerSave(handle);
      expect(handle.unexpected).toEqual([]);
    });
  });
}

test.describe("automation save problems and rights", () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test("a conflict keeps the draft and Reload settings brings the saved state back", async ({
    page,
  }) => {
    const text = copy.en;
    const { handle } = await install(page, "en", {
      onSave: () => ({ status: 409, body: { title: "Conflict" } }),
    });

    await openAutomation(page, "en");
    await panel(page).getByRole("switch", { name: text.idleEnable }).check();
    await panel(page).getByRole("button", { name: text.save }).click();

    await expect(panel(page).getByRole("alert").filter({ hasText: text.conflict })).toBeVisible();
    // The draft survives the failed save.
    await expect(panel(page).getByRole("switch", { name: text.idleEnable })).toBeChecked();
    expect(handle.saves).toHaveLength(1);

    await panel(page).getByRole("button", { name: text.reload }).click();
    await expect(panel(page).getByRole("alert")).toHaveCount(0);
    await expect(panel(page).getByRole("switch", { name: text.idleEnable })).not.toBeChecked();
    expect(handle.unexpected).toEqual([]);
  });

  test("an invalid minutes value blocks saving and focuses the field", async ({ page }) => {
    const text = copy.en;
    const { handle } = await install(page, "en");

    await openAutomation(page, "en");
    await panel(page).getByRole("switch", { name: text.idleEnable }).check();
    const minutes = panel(page).getByLabel(text.idleMinutes);
    await minutes.fill("2");
    // Leaving the field shows its message, which moves the Save button down; click only after the layout settled.
    await minutes.blur();
    await expect(
      panel(page).getByText("Enter a whole number of minutes between 5 and 720."),
    ).toBeVisible();
    await panel(page).getByRole("button", { name: text.save }).click();

    await expect(panel(page).getByRole("alert").filter({ hasText: text.invalidOne })).toBeVisible();
    await expect(minutes).toBeFocused();
    await expect(minutes).toHaveAttribute("aria-invalid", "true");
    expect(handle.saves).toEqual([]);
    expect(handle.unexpected).toEqual([]);
  });

  test("a member without the right sees disabled controls, the notice and no Save row", async ({
    page,
  }) => {
    const text = copy.en;
    const { handle } = await install(page, "en", {
      canManage: false,
      server: automationServer({
        canManage: false,
        automation: automationBlock({
          settings: { idleShutdownEnabled: true, idleShutdownMinutes: 45, version: 2 },
        }),
      }),
    });

    await openAutomation(page, "en");
    await expect(panel(page).getByText(text.readOnly)).toBeVisible();
    await expect(panel(page).getByRole("switch", { name: text.idleEnable })).toBeDisabled();
    await expect(panel(page).getByRole("switch", { name: text.maxEnable })).toBeDisabled();
    await expect(panel(page).getByLabel(text.idleMinutes)).toBeDisabled();
    await expect(panel(page).getByLabel(text.maxHours)).toBeDisabled();
    await expect(panel(page).getByRole("button", { name: text.addSchedule })).toBeDisabled();
    await expect(panel(page).getByRole("button", { name: text.save })).toHaveCount(0);
    expect(handle.saves).toEqual([]);
    expect(handle.unexpected).toEqual([]);
  });
});

test.describe("automation warning surfaces", () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test("a pending stop shows the notice, one toast and a menu row, and a cancellation shows its toast", async ({
    page,
  }) => {
    const text = copy.en;
    const { handle, stream } = await install(page, "en");
    const alert = {
      id: "60000000-0000-4000-8000-000000000d68",
      serverId: automationServerId,
      serverName: automationServerTitle,
      costOwnerId: automationCostOwnerId,
      thresholdAmount: 0,
      accruedAmount: 0,
      currency: "EUR",
      createdAt: "2026-11-23T20:25:00Z",
      kind: "AutoStop",
      stopsAt,
      stopReason: "Idle",
      stopLimit: 30,
    };

    await openWorkspace(page);
    await waitForStream(stream);
    await expect(notice(page)).toHaveCount(0);

    // The signal makes the workspace refetch the server, which now carries the announced stop.
    handle.state.server = automationServer({ automation: pendingIdleStop() });
    stream.send(fleetChangedFrame(automationServerId));
    const noticePanel = page.locator("section.state-panel").filter({ has: notice(page) });
    await expect(noticePanel.getByRole("heading", { name: text.noticeTitle })).toBeVisible();
    await expect(notice(page)).toContainText(text.noticeReason);
    await expect(notice(page)).toContainText(
      "It saves its data, stops, and releases the cloud machine.",
    );
    await expect(
      noticePanel.getByRole("link", { name: "Change automatic shutdown" }),
    ).toBeVisible();

    // The settings section shows the same notice above its panel.
    await page.goto(`command/servers/${automationServerId}?tab=settings`);
    await expect(page.getByRole("heading", { level: 2, name: text.panelTitle })).toBeVisible();
    await expect(notice(page)).toHaveCount(1);

    // The raised alert frame shows one warning toast; a redelivered frame adds no second one.
    const connections = stream.connectionCount();
    await openWorkspace(page);
    await waitForStream(stream, connections);
    stream.send(costAlertRaisedFrame(alert));
    await expect(page.locator(".app-toast").filter({ hasText: text.toastWarning })).toHaveCount(1);
    // From here on the REST list holds the alert too, as the control plane would answer a later read.
    handle.state.alerts = [alert];
    stream.send(costAlertRaisedFrame(alert));
    await expect(page.locator(".app-toast").filter({ hasText: text.toastWarning })).toHaveCount(1);

    // The profile menu lists the stop as its own row kind, not as a cost threshold row.
    await page.getByRole("button", { name: text.menuTrigger }).click();
    await expect(page.getByRole("dialog", { name: text.menuName })).toBeVisible();
    const row = page.locator("[data-auto-stop-alert-id]");
    await expect(row).toHaveCount(1);
    await expect(row).toContainText(text.menuLine);
    await expect(page.locator("[data-cost-alert-id]")).toHaveCount(0);
    await page.keyboard.press("Escape");

    // A later refetch without the stop and without a stop job tells the viewer it was cancelled.
    handle.state.server = automationServer({ automation: automationBlock() });
    stream.send(fleetChangedFrame(automationServerId));
    await expect(notice(page)).toHaveCount(0);
    await expect(page.locator(".app-toast").filter({ hasText: text.toastCancelled })).toHaveCount(
      1,
    );
    expect(handle.unexpected).toEqual([]);
  });

  test("a stop that started a Stop job is not announced as cancelled", async ({ page }) => {
    const text = copy.en;
    const { handle, stream } = await install(page, "en", {
      server: automationServer({ automation: pendingIdleStop() }),
    });

    await openWorkspace(page);
    await waitForStream(stream);
    await expect(notice(page)).toHaveCount(1);

    handle.state.jobs = [{ ...automaticStopJob("Idle", 30), status: "Running", stage: "running" }];
    handle.state.server = automationServer({
      automation: automationBlock(),
      overrides: { pendingJobCount: 1 },
    });
    stream.send(fleetChangedFrame(automationServerId));
    await expect(notice(page)).toHaveCount(0);
    await expect(page.locator(".app-toast").filter({ hasText: text.toastCancelled })).toHaveCount(
      0,
    );
    expect(handle.unexpected).toEqual([]);
  });
});

test.describe("automation in the activity and overview", () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test("Activity lists the automatic Stop job with its trigger line and Audit the automatic stop entry", async ({
    page,
  }) => {
    const text = copy.en;
    const { handle } = await install(page, "en", {
      jobs: [automaticStopJob("Idle", 30)],
      operations: automationAuditOperations(),
    });

    await openWorkspace(page, "?tab=activity");
    await expect(page.getByText(text.trigger, { exact: true })).toBeVisible();

    await page.getByRole("button", { name: new RegExp(`^${text.auditFilter}`) }).click();
    await expect(page.getByText(text.auditEntry, { exact: true })).toBeVisible();
    expect(handle.unexpected).toEqual([]);
  });

  test("Overview shows the Automation fact with its link to the panel", async ({ page }) => {
    const text = copy.en;
    const { handle } = await install(page, "en", {
      server: automationServer({
        automation: automationBlock({
          settings: {
            idleShutdownEnabled: true,
            idleShutdownMinutes: 30,
            maxRuntimeEnabled: true,
            maxRuntimeHours: 6,
          },
        }),
      }),
    });

    await openWorkspace(page);
    const fact = page.locator(".workspace-facts__fact").filter({ hasText: text.overviewFact });
    await expect(fact).toContainText(text.overviewIdle);
    await expect(fact).toContainText(text.overviewMax);
    await fact.getByRole("link", { name: text.overviewLink }).click();
    await expect(page).toHaveURL(/[?&]tab=settings(&|#|$)/);
    await expect(page).toHaveURL(/#server-automation$/);
    await expect(page.getByRole("button", { name: text.panelTitle })).toBeFocused();
    expect(handle.unexpected).toEqual([]);
  });
});

/** Layout edges come back fractional; less than one CSS pixel past the viewport is a rounding artifact. */
const subpixelTolerance = 1;

/** The two viewports of the phase gate: the smallest supported phone and a common one. */
const gateViewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

/**
 * Asserts that the document cannot scroll horizontally.
 *
 * @param page The page to measure.
 */
async function expectNoHorizontalScroll(page: Page) {
  const metrics = await page.evaluate(() => {
    const root = document.scrollingElement ?? document.documentElement;
    return { scrollWidth: root.scrollWidth, clientWidth: root.clientWidth };
  });
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + subpixelTolerance);
}

for (const culture of ["en", "de"] as const) {
  for (const viewport of gateViewports) {
    test.describe(`automation layout (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test(`the panel and the consent dialog have no horizontal page scroll (${culture}, ${viewport.width})`, async ({
        page,
      }) => {
        const text = copy[culture];
        const { handle } = await install(page, culture);

        await openAutomation(page, culture);
        await expectNoHorizontalScroll(page);

        // The longest state: both numbers on and two rules, one of each action.
        await fillOwnerDraft(page, culture);
        await expectNoHorizontalScroll(page);

        await panel(page).getByRole("button", { name: text.save }).click();
        const dialog = page.locator("dialog[open]");
        await expect(dialog).toBeVisible();
        await expectNoHorizontalScroll(page);
        const box = await dialog.boundingBox();
        expect(box).not.toBeNull();
        expect(box?.x ?? -1).toBeGreaterThanOrEqual(0);
        expect((box?.x ?? 0) + (box?.width ?? Infinity)).toBeLessThanOrEqual(
          viewport.width + subpixelTolerance,
        );
        expect(handle.unexpected).toEqual([]);
      });

      test(`the notice has no horizontal page scroll (${culture}, ${viewport.width})`, async ({
        page,
      }) => {
        const { handle } = await install(page, culture, {
          server: automationServer({ automation: pendingIdleStop() }),
        });

        await openWorkspace(page);
        await expect(notice(page)).toBeVisible();
        await expectNoHorizontalScroll(page);
        expect(handle.unexpected).toEqual([]);
      });
    });
  }
}

/** The viewports of the weekday chip layout and the number of rows the seven chips take in each. */
const chipViewports = [
  { width: 320, height: 568, rows: 2 },
  { width: 390, height: 844, rows: 2 },
  { width: 1280, height: 800, rows: 1 },
] as const;

for (const culture of ["en", "de"] as const) {
  for (const { rows, ...viewport } of chipViewports) {
    test.describe(`weekday chips (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test(`the seven chips take ${rows} row(s) and stay inside the grid (${culture}, ${viewport.width})`, async ({
        page,
      }) => {
        const text = copy[culture];
        const { handle } = await install(page, culture);

        await openAutomation(page, culture);
        await panel(page).getByRole("button", { name: text.addSchedule }).click();
        const block = rule(page, text.scheduleOne);
        await expect(block).toBeVisible();

        const chips = block.locator(".toggle-chip");
        await expect(chips).toHaveCount(7);
        const gridBox = await block.locator(".schedule-rule__day-grid").boundingBox();
        expect(gridBox).not.toBeNull();
        const boxes = await chips.evaluateAll((elements) =>
          elements.map((element) => {
            const rect = element.getBoundingClientRect();
            return {
              top: Math.round(rect.top),
              left: rect.left,
              right: rect.right,
              clipped: element.scrollWidth > element.clientWidth,
            };
          }),
        );
        expect(new Set(boxes.map((box) => box.top)).size).toBe(rows);
        if (rows === 2) {
          // Four chips share the first row, three the second.
          expect(boxes.filter((box) => box.top === boxes[0]?.top)).toHaveLength(4);
        }
        for (const box of boxes) {
          expect(box.left).toBeGreaterThanOrEqual((gridBox?.x ?? 0) - subpixelTolerance);
          expect(box.right).toBeLessThanOrEqual(
            (gridBox?.x ?? 0) + (gridBox?.width ?? 0) + subpixelTolerance,
          );
          expect(box.clipped).toBe(false);
        }
        await expectNoHorizontalScroll(page);
        expect(handle.unexpected).toEqual([]);
      });
    });
  }
}

test.describe("automation controls in forced colors", () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test("the switch track, a chip and a radio card keep a border and a checked mark that differs", async ({
    page,
  }) => {
    const text = copy.en;
    const { handle } = await install(page, "en");
    await page.emulateMedia({ forcedColors: "active" });

    await openAutomation(page, "en");
    expect(await page.evaluate(() => matchMedia("(forced-colors: active)").matches)).toBe(true);
    await panel(page).getByRole("button", { name: text.addSchedule }).click();
    const block = rule(page, text.scheduleOne);
    await expect(block).toBeVisible();

    /** The computed border and fill of the first element matching the selector. */
    const paint = (scope: Locator, selector: string) =>
      scope
        .locator(selector)
        .first()
        .evaluate((element) => {
          const style = getComputedStyle(element);
          return {
            width: Number.parseFloat(style.borderTopWidth),
            style: style.borderTopStyle,
            color: style.borderTopColor,
            background: style.backgroundColor,
          };
        });

    const idle = panel(page).getByRole("switch", { name: text.idleEnable });
    const trackBefore = await paint(panel(page), ".switch-track");
    expect(trackBefore.width).toBeGreaterThan(0);
    expect(trackBefore.style).not.toBe("none");
    expect(trackBefore.color).not.toBe("rgba(0, 0, 0, 0)");
    await idle.check();
    const trackAfter = await paint(panel(page), ".switch-track");
    expect(trackAfter.width).toBeGreaterThan(0);
    expect(`${trackAfter.color}|${trackAfter.background}`).not.toBe(
      `${trackBefore.color}|${trackBefore.background}`,
    );

    const chipBefore = await paint(block, ".toggle-chip");
    expect(chipBefore.width).toBeGreaterThan(0);
    expect(chipBefore.style).not.toBe("none");
    await block.locator('input[data-field="days"]').first().check();
    const chipAfter = await paint(block, ".toggle-chip");
    expect(`${chipAfter.color}|${chipAfter.background}`).not.toBe(
      `${chipBefore.color}|${chipBefore.background}`,
    );

    const card = await paint(block, ".choice-card");
    expect(card.width).toBeGreaterThan(0);
    expect(card.style).not.toBe("none");
    expect(card.color).not.toBe("rgba(0, 0, 0, 0)");
    expect(handle.unexpected).toEqual([]);
  });
});

test.describe("automation notice on the smallest phone", () => {
  test.use({ viewport: { width: 320, height: 568 } });

  test("the German notice title and reason fit within one screen height", async ({ page }) => {
    const text = copy.de;
    const { handle } = await install(page, "de", {
      server: automationServer({ automation: pendingIdleStop() }),
    });

    await openWorkspace(page);
    const title = page.getByRole("heading", { level: 2, name: text.noticeTitle });
    const reason = notice(page).getByText(text.noticeReason, { exact: true });
    await expect(title).toBeVisible();
    await expect(reason).toBeVisible();

    // UI-SPEC: the title and the reason stay within one screen height; the rest of the page scrolls. With the notice
    // scrolled to the top of the screen both are in view together, and the page is not locked to the first screen.
    // The shell animates scrolling (scroll-behavior: smooth), so the jump must be instant or the boxes are measured
    // mid-animation.
    await title.evaluate((element) =>
      element.scrollIntoView({ block: "start", behavior: "instant" }),
    );
    const titleBox = await title.boundingBox();
    const reasonBox = await reason.boundingBox();
    expect(titleBox).not.toBeNull();
    expect(reasonBox).not.toBeNull();
    expect(titleBox?.y ?? -Infinity).toBeGreaterThanOrEqual(-subpixelTolerance);
    expect((reasonBox?.y ?? 0) + (reasonBox?.height ?? Infinity)).toBeLessThanOrEqual(568);
    const scrolls = await page.evaluate(() => {
      const root = document.scrollingElement ?? document.documentElement;
      return root.scrollHeight > root.clientHeight && getComputedStyle(root).overflowY !== "hidden";
    });
    expect(scrolls).toBe(true);
    expect(handle.unexpected).toEqual([]);
  });
});

test.describe("automation keyboard order", () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test("Tab follows the DOM order through the panel and every stop shows the focus ring", async ({
    page,
  }) => {
    const { handle } = await install(page, "en", {
      server: automationServer({
        automation: automationBlock({
          settings: {
            idleShutdownEnabled: true,
            maxRuntimeEnabled: true,
            schedules: [
              {
                id: "a0000000-0000-4000-8000-000000000001",
                action: "Start",
                days: [5],
                time: "18:00",
                nextRunAt: "2026-11-27T18:00:00Z",
                lastRun: null,
              },
            ],
            version: 3,
          },
          startConsent: { state: "Given" },
        }),
      }),
    });

    await openAutomation(page, "en");
    await expect(page.getByRole("button", { name: copy.en.panelTitle })).toBeFocused();

    // The ring is the shared 2px solid gold token. Drawn marks carry it on an ancestor of the invisible input,
    // every other control on itself. The primary Save button keeps the command shell's own, thicker ring.
    const ring = await page.evaluate(() => {
      const probe = document.createElement("span");
      probe.style.color = "var(--gold-bright)";
      document.body.append(probe);
      const color = getComputedStyle(probe).color;
      probe.remove();
      return color;
    });

    const stops: {
      position: number;
      tag: string;
      type: string | null;
      primary: boolean;
      nativePart: boolean;
      ownerIsKnown: boolean;
      style: string;
      width: string;
      color: string;
    }[] = [];
    for (let press = 0; press < 80; press += 1) {
      await page.keyboard.press("Tab");
      const stop = await page.evaluate(() => {
        const panelElement = document.querySelector("#server-automation");
        const active = document.activeElement;
        if (!panelElement || !active || !panelElement.contains(active)) return null;
        const position = [...panelElement.querySelectorAll("*")].indexOf(active);
        // A drawn mark keeps its native input invisible; the ring sits on an ancestor of the input (the mark
        // wrapper, the whole card, the chip or the segment), so the owner is the first ancestor that draws it.
        const ringOwners =
          ".check-mark, .switch-control, .toggle-chip, .choice-card, .segmented__option, .form-field__check, .form-field__radio-row";
        let owner: Element = active;
        for (
          let ancestor: Element | null = active.classList.contains("check-mark__input")
            ? active.parentElement
            : active;
          ancestor !== null && ancestor !== panelElement;
          ancestor = ancestor.parentElement
        ) {
          if (getComputedStyle(ancestor).outlineStyle !== "none") {
            owner = ancestor;
            break;
          }
        }
        const ownerIsKnown = owner === active || owner.matches(ringOwners);
        const style = getComputedStyle(owner);
        return {
          position,
          tag: active.tagName.toLowerCase(),
          type: active.getAttribute("type"),
          primary: active.classList.contains("button--primary"),
          // A time input has native segments and a picker button; the browser draws their focus, not the page.
          nativePart: active.getAttribute("type") === "time" && !active.matches(":focus-visible"),
          ownerIsKnown,
          style: style.outlineStyle,
          width: style.outlineWidth,
          color: style.outlineColor,
        };
      });
      if (stop === null) break;
      stops.push(stop);
    }

    // Every control of the panel is reached in DOM order: the cost alert link, two switches and two numbers, the
    // zone, the rule's remove button, action radio, time and seven days, Add, Save and Discard. A time input has one
    // stop per native segment, so its position repeats; every other control is reached exactly once.
    expect(stops.length).toBeGreaterThanOrEqual(12);
    const positions = stops.map((stop) => stop.position);
    expect(positions).toEqual([...positions].sort((left, right) => left - right));
    const repeated = stops.filter((stop, index) => stop.position === stops[index - 1]?.position);
    expect(repeated.every((stop) => stop.type === "time")).toBe(true);
    for (const stop of stops.filter((candidate) => !candidate.nativePart)) {
      expect(stop.ownerIsKnown).toBe(true);
      expect(stop.style).toBe("solid");
      if (stop.primary) {
        expect(Number.parseFloat(stop.width)).toBeGreaterThanOrEqual(2);
      } else {
        expect(stop.width).toBe("2px");
        expect(stop.color).toBe(ring);
      }
    }
    expect(handle.unexpected).toEqual([]);
  });
});

/** The viewports of the review screenshots: a desktop column and a common phone. */
const screenViewports = [
  { width: 1280, height: 900 },
  { width: 390, height: 844 },
] as const;

/**
 * Review captures of the automation surfaces: the panel, one rule block, the consent dialog and the notice per
 * language and width, written to `test-results/automation-screens`. They exist for the owner's alignment review
 * (dialog alignment and select padding), so they run only when `UTF_CAPTURE_SCREENS=1` is set and are never part of
 * the normal e2e run.
 */
test.describe("automation screens", () => {
  test.skip(
    process.env["UTF_CAPTURE_SCREENS"] !== "1",
    "Set UTF_CAPTURE_SCREENS=1 to capture screens.",
  );

  for (const culture of ["en", "de"] as const) {
    for (const viewport of screenViewports) {
      const suffix = `${culture}-${viewport.width}`;

      test(`panel and rule block (${suffix})`, async ({ page }) => {
        await page.setViewportSize(viewport);
        const { handle } = await install(page, culture, {
          server: automationServer({
            automation: automationBlock({
              settings: {
                idleShutdownEnabled: true,
                maxRuntimeEnabled: true,
                schedules: [
                  {
                    id: "a0000000-0000-4000-8000-000000000001",
                    action: "Start",
                    days: [5],
                    time: "18:00",
                    nextRunAt: "2026-11-27T18:00:00Z",
                    lastRun: null,
                  },
                ],
                version: 3,
              },
            }),
          }),
        });

        await openAutomation(page, culture);
        await page.evaluate(() => document.fonts.ready);
        await panel(page).screenshot({
          path: `test-results/automation-screens/panel-${suffix}.png`,
        });
        await rule(page, copy[culture].scheduleOne).screenshot({
          path: `test-results/automation-screens/rule-${suffix}.png`,
        });
        expect(handle.unexpected).toEqual([]);
      });

      test(`consent dialog (${suffix})`, async ({ page }) => {
        await page.setViewportSize(viewport);
        const { handle } = await install(page, culture);

        await openAutomation(page, culture);
        await fillOwnerDraft(page, culture);
        await panel(page).getByRole("button", { name: copy[culture].save }).click();
        const dialog = page.locator("dialog[open]");
        await expect(dialog).toBeVisible();
        await page.evaluate(() => document.fonts.ready);
        await page.screenshot({ path: `test-results/automation-screens/consent-${suffix}.png` });
        expect(handle.unexpected).toEqual([]);
      });

      test(`notice (${suffix})`, async ({ page }) => {
        await page.setViewportSize(viewport);
        const { handle } = await install(page, culture, {
          server: automationServer({ automation: pendingIdleStop() }),
        });

        await openWorkspace(page);
        await expect(notice(page)).toBeVisible();
        await page.evaluate(() => document.fonts.ready);
        await page
          .locator("section.state-panel")
          .filter({ has: notice(page) })
          .screenshot({ path: `test-results/automation-screens/notice-${suffix}.png` });
        expect(handle.unexpected).toEqual([]);
      });
    }
  }
});

test.describe("automation deep link into collapsed Settings panels", () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test("the deep link opens only the collapsed Automation panel below the sticky header", async ({
    page,
  }) => {
    // The empty seed keeps every panel closed, so the page starts from the collapsed first-visit default.
    const { handle } = await install(page, "en", { settingsPanels: [] });

    await openAutomation(page, "en");

    const panels = page.locator("[data-settings-panel]");
    const automationHeader = page.locator('[data-settings-panel="server-automation"] h2 > button');
    await expect(automationHeader).toHaveAttribute("aria-expanded", "true");
    await expect(automationHeader).toBeFocused();
    // The deep link opens one panel; every other panel header stays collapsed.
    const count = await panels.count();
    expect(count).toBeGreaterThan(0);
    for (let index = 0; index < count; index += 1) {
      const owner = panels.nth(index);
      const id = await owner.getAttribute("data-settings-panel");
      if (id === "server-automation") continue;
      await expect(owner.locator("h2 > button")).toHaveAttribute("aria-expanded", "false");
    }

    // The scroll is smooth, so wait until the panel top rests at or below the bottom of the sticky header.
    await expect
      .poll(async () => {
        const stickyBox = await page.locator(".workspace-header").boundingBox();
        const panelBox = await panel(page).boundingBox();
        if (stickyBox === null || panelBox === null) return Number.NEGATIVE_INFINITY;
        return panelBox.y - (stickyBox.y + stickyBox.height);
      })
      .toBeGreaterThanOrEqual(-1);
    expect(handle.unexpected).toEqual([]);
  });
});
