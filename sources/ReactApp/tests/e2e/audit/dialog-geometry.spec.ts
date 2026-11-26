import { expect, test, type Locator, type Page } from "@playwright/test";
import { fleetAction, fleetServer } from "../../helpers/fleetFixtures";
import { checkDialogGeometry } from "../support/a11y";
import {
  installWorkspace,
  workspaceRoute,
  workspaceServerId,
} from "../support/screens/workspaceMocks";
import {
  auditUserId,
  installApp,
  jsonHandler,
  type ApiHandler,
  type Culture,
} from "../support/mockApi";

/**
 * Dialog shell geometry in a real browser (Phase 69, D-10, FND-06).
 *
 * jsdom has no layout, so the one shared dialog shell is measured in chromium and in both phone projects. Two
 * confirmations open from the fleet: the cost-bearing start, and the stop of a running server that carries a typed
 * challenge, which adds the field row and the destructive variant. For each, in English and German, the spec asserts
 * the shared left edge and the action layout (`checkDialogGeometry`), initial focus on the dismiss control (T-69-30),
 * a dialog that fits the viewport with its content reachable, and a page that never scrolls sideways.
 */

/** The challenge the stop action asks for before it confirms. */
const stopChallenge = "STOP ALPHA";

/** The reason copy of the stop action; it is the confirmation text the dialog shows in its server note. */
const stopConfirmation = "Stopping releases the cloud machine after the data is saved.";

/**
 * Builds the two fleet servers: one off server whose start costs money, and one running server whose stop is
 * dangerous and needs a typed challenge.
 *
 * @returns Fresh server objects in the shape of `GET /api/game-servers`.
 */
function dialogFleet() {
  return [
    fleetServer({
      index: 1,
      title: "Alpha Ops",
      status: "Running",
      costOwnerUserId: auditUserId,
      actions: [
        fleetAction("stop", {
          dangerous: true,
          confirmationText: stopConfirmation,
          confirmationChallenge: stopChallenge,
          costDescription: "Provider cost stops after verified cleanup.",
        }),
      ],
    }),
    fleetServer({
      index: 2,
      title: "Bravo Training",
      actions: [
        fleetAction("start", {
          description: "Provisions a cloud machine and starts the game server.",
          costDescription: "Hourly provider cost starts once the machine exists.",
        }),
      ],
    }),
  ];
}

/**
 * Answers the single-server read the confirmation provider sends while its dialog is open.
 *
 * @returns The handler.
 */
function serverDetailHandler(): ApiHandler {
  return jsonHandler("GET", /^\/api\/game-servers\/[0-9a-f-]{36}$/i, (url: URL) => {
    const id = url.pathname.split("/")[3];
    return dialogFleet().find((server) => server.primaryKey === id) ?? null;
  });
}

/**
 * Opens the fleet and waits for both cards.
 *
 * @param page The page to drive.
 * @param culture The culture to run in.
 * @returns The mock session whose unexpected list the test asserts empty.
 */
async function openFleet(page: Page, culture: Culture) {
  const session = await installApp(page, {
    culture,
    stream: true,
    handlers: [jsonHandler("GET", "/api/game-servers", dialogFleet), serverDetailHandler()],
  });
  await page.goto("command/servers");
  await expect(page.locator("[data-server-card]")).toHaveCount(2);
  return session;
}

/**
 * Asserts that the open dialog fits the viewport, that its last action can be brought into view and that the
 * document does not scroll sideways.
 *
 * @param page The page the dialog is on.
 */
async function expectDialogFitsViewport(page: Page): Promise<void> {
  const dialog = page.locator("dialog[open]");
  const measured = await dialog.evaluate((element) => {
    const box = element.getBoundingClientRect();
    const style = getComputedStyle(element);
    return {
      top: box.top,
      bottom: box.bottom,
      height: box.height,
      viewportHeight: window.innerHeight,
      overflowY: style.overflowY,
      scrollHeight: element.scrollHeight,
      clientHeight: element.clientHeight,
    };
  });
  expect(measured.top, "dialog top edge").toBeGreaterThanOrEqual(-1);
  expect(measured.bottom, "dialog bottom edge").toBeLessThanOrEqual(measured.viewportHeight + 1);
  // Content taller than the dialog must scroll inside it, never past it.
  if (measured.scrollHeight > measured.clientHeight + 1) {
    expect(["auto", "scroll"]).toContain(measured.overflowY);
  }
  const lastAction = dialog.locator(".dialog__actions button").last();
  await lastAction.scrollIntoViewIfNeeded();
  const reached = await lastAction.evaluate((element) => {
    const box = element.getBoundingClientRect();
    return box.top >= -1 && box.bottom <= window.innerHeight + 1;
  });
  expect(reached, "last action inside the viewport after scrolling the dialog").toBe(true);
  const scroll = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(scroll.scrollWidth, "the page never scrolls sideways").toBeLessThanOrEqual(
    scroll.clientWidth,
  );
}

/**
 * Asserts the action order the viewport implies (D-10): dismiss then confirm in a row above 520px, the confirm
 * action first in a stack at or below 520px.
 *
 * @param page The page the dialog is on.
 */
async function expectActionLayout(page: Page): Promise<void> {
  const width = page.viewportSize()?.width ?? 0;
  const actions = page.locator("dialog[open] .dialog__actions");
  const dismiss = await actions.locator("[data-dialog-dismiss]").boundingBox();
  const confirm = await actions.locator("button[type=submit]").boundingBox();
  expect(dismiss, "dismiss action has a layout box").not.toBeNull();
  expect(confirm, "confirm action has a layout box").not.toBeNull();
  if (width <= 520) {
    expect(confirm!.y, "the confirm action comes first in the stack").toBeLessThan(dismiss!.y);
  } else {
    // A long label may wrap the confirm action to a second, still right-aligned row (checkDialogGeometry), so the
    // order is the reading order: the same row left to right, or dismiss on the row above.
    const sameRow = Math.abs(dismiss!.y - confirm!.y) <= 1;
    if (sameRow) expect(dismiss!.x, "dismiss sits left of confirm").toBeLessThan(confirm!.x);
    else expect(dismiss!.y, "dismiss reads before confirm").toBeLessThan(confirm!.y);
  }
}

for (const culture of ["en", "de"] as const) {
  test.describe(`dialog shell geometry (${culture})`, () => {
    test("the start cost confirmation shares one geometry", async ({ page }) => {
      const session = await openFleet(page, culture);
      const bravo = page.locator("[data-server-card] .fleet-card__title", {
        hasText: "Bravo Training",
      });
      await bravo
        .locator("xpath=ancestor::article")
        .locator(".fleet-card__actions button")
        .first()
        .click();

      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      await expect(dialog.locator(".cost-confirm__fact").first()).toBeVisible();
      await expect(dialog.locator("[data-dialog-dismiss]")).toBeFocused();

      await checkDialogGeometry(page, dialog);
      await expectActionLayout(page);
      await expectDialogFitsViewport(page);
      expect(session.unexpected).toEqual([]);
    });

    test("the destructive confirmation with a typed challenge shares one geometry", async ({
      page,
    }) => {
      const session = await openFleet(page, culture);
      const alpha = page.locator("[data-server-card] .fleet-card__title", { hasText: "Alpha Ops" });
      await alpha
        .locator("xpath=ancestor::article")
        .locator(".fleet-card__actions button")
        .first()
        .click();

      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      await expect(dialog.locator("input")).toBeVisible();
      await expect(dialog.locator("[data-dialog-dismiss]")).toBeFocused();

      await checkDialogGeometry(page, dialog);
      await expectActionLayout(page);
      await expectDialogFitsViewport(page);
      expect(session.unexpected).toEqual([]);
    });
  });
}

/** The three leave actions in document order, per culture: Discard and leave, Stay here, Review changes. */
const leaveLabels = {
  en: ["Discard and leave", "Stay here", "Review changes"],
  de: ["Verwerfen und verlassen", "Hier bleiben", "Änderungen prüfen"],
} as const satisfies Record<Culture, readonly string[]>;

/** The viewports the leave dialog is measured at: desktop, just above the stack breakpoint, at it, and the narrowest. */
const leaveViewports = [
  { width: 1280, height: 800 },
  { width: 521, height: 800 },
  { width: 520, height: 800 },
  { width: 320, height: 568 },
] as const;

/**
 * Opens the Settings section of a stopped server at a viewport and stages a new Slots value.
 *
 * @param page The page to drive; its viewport is set before the first navigation.
 * @param culture The culture to run in.
 * @param viewport The viewport to measure at.
 * @returns The mock session whose unexpected list the test asserts empty.
 */
async function openStagedSettings(
  page: Page,
  culture: Culture,
  viewport: { width: number; height: number },
) {
  await page.setViewportSize(viewport);
  const session = await installWorkspace(page, culture, {
    server: { deployment: null, actions: [fleetAction("start")], canModify: true },
    canManageSecrets: true,
  });
  await page.goto(workspaceRoute("settings").replace(/^[/]/, ""));
  const field = page.getByLabel("Slots", { exact: true });
  await field.waitFor();
  await field.scrollIntoViewIfNeeded();
  await field.fill("80");
  await page.locator(".pending-changes-bar").waitFor();
  return session;
}

/**
 * Stages a Slots value and opens the leave dialog through the section navigation.
 *
 * @param page The page to drive; its viewport is set before the first navigation.
 * @param culture The culture to run in.
 * @param viewport The viewport to measure at.
 * @returns The mock session whose unexpected list the test asserts empty.
 */
async function openLeaveDialogAt(
  page: Page,
  culture: Culture,
  viewport: { width: number; height: number },
) {
  const session = await openStagedSettings(page, culture, viewport);
  await page.locator(".section-navigation__link").first().click();
  await page.locator("dialog.dialog--leave[open]").waitFor();
  return session;
}

/**
 * Asserts the leave dialog action layout (D-13): above 520px one row with the first action at the start edge and
 * the last at the end edge, at or below 520px a full-width stack in document order, and no clipped label.
 *
 * @param page The page the dialog is on.
 * @param culture The culture the labels are read in.
 */
async function expectLeaveLayout(page: Page, culture: Culture): Promise<void> {
  const width = page.viewportSize()?.width ?? 0;
  const actions = page.locator("dialog.dialog--leave[open] .dialog__actions");
  await expect(actions.locator("button")).toHaveText([...leaveLabels[culture]]);
  const measured = await actions.evaluate((container) => {
    const box = container.getBoundingClientRect();
    return {
      left: box.left,
      right: box.right,
      buttons: Array.from(container.querySelectorAll("button")).map((button) => {
        const rect = button.getBoundingClientRect();
        return {
          x: rect.x,
          y: rect.y,
          right: rect.right,
          width: rect.width,
          height: rect.height,
          clipped: button.scrollWidth > button.clientWidth,
          ghost: button.classList.contains("button--ghost"),
        };
      }),
    };
  });
  expect(measured.buttons, "three actions").toHaveLength(3);
  for (const button of measured.buttons) {
    expect(button.clipped, "no clipped label").toBe(false);
    expect(button.ghost, "no ghost button in the leave dialog").toBe(false);
  }
  const [discard, stay, review] = measured.buttons as [
    (typeof measured.buttons)[number],
    (typeof measured.buttons)[number],
    (typeof measured.buttons)[number],
  ];
  if (width > 520) {
    expect(Math.abs(discard.y - stay.y), "Discard and Stay share a row").toBeLessThanOrEqual(1);
    expect(Math.abs(stay.y - review.y), "Stay and Review share a row").toBeLessThanOrEqual(1);
    expect(discard.x, "Discard reads before Stay").toBeLessThan(stay.x);
    expect(stay.x, "Stay reads before Review").toBeLessThan(review.x);
    expect(Math.abs(discard.x - measured.left), "Discard at the start edge").toBeLessThanOrEqual(1);
    expect(Math.abs(review.right - measured.right), "Review at the end edge").toBeLessThanOrEqual(
      1,
    );
  } else {
    expect(discard.y, "Discard stacks first").toBeLessThan(stay.y);
    expect(stay.y, "Stay stacks second").toBeLessThan(review.y);
    for (const button of measured.buttons) {
      const containerWidth = measured.right - measured.left;
      expect(Math.abs(button.width - containerWidth), "full width").toBeLessThanOrEqual(1);
      expect(button.height, "touch height").toBeGreaterThanOrEqual(43.5);
    }
  }
}

for (const culture of ["en", "de"] as const) {
  test.describe(`Settings leave dialog layout (${culture})`, () => {
    for (const viewport of leaveViewports) {
      test(`lays the actions out at ${viewport.width}px`, async ({ page }, testInfo) => {
        test.skip(
          testInfo.project.name !== "chromium",
          "The widths are set explicitly in chromium.",
        );
        const session = await openLeaveDialogAt(page, culture, viewport);
        const dialog = page.locator("dialog.dialog--leave[open]");
        await expect(dialog.locator("[data-dialog-dismiss]")).toBeFocused();

        await checkDialogGeometry(page, dialog);
        await expectLeaveLayout(page, culture);
        await expectDialogFitsViewport(page);
        expect(session.unexpected).toEqual([]);
      });
    }

    test("tabs in visual order and closes as Stay on the backdrop", async ({ page }, testInfo) => {
      test.skip(testInfo.project.name !== "chromium", "The widths are set explicitly in chromium.");
      const session = await openLeaveDialogAt(page, culture, leaveViewports[0]);
      const dialog = page.locator("dialog.dialog--leave[open]");
      const [discard, stay, review] = leaveLabels[culture];

      // Stay holds the initial focus; the tab order follows the DOM and the visual order.
      await expect(dialog.getByRole("button", { name: stay })).toBeFocused();
      await page.keyboard.press("Shift+Tab");
      await expect(dialog.getByRole("button", { name: discard })).toBeFocused();
      await page.keyboard.press("Tab");
      await page.keyboard.press("Tab");
      await expect(dialog.getByRole("button", { name: review })).toBeFocused();

      // A click on the backdrop outside the dialog box equals Stay here.
      await page.mouse.click(5, 5);
      await expect(page.locator("dialog[open]")).toHaveCount(0);
      await expect(page).toHaveURL(/tab=settings/);
      await expect(page.locator(".pending-changes-bar")).toBeVisible();
      await expect(page.getByLabel("Slots", { exact: true })).toHaveValue("80");
      expect(session.unexpected).toEqual([]);
    });

    test("the discard dialog closes as Keep editing on the backdrop", async ({
      page,
    }, testInfo) => {
      test.skip(testInfo.project.name !== "chromium", "The widths are set explicitly in chromium.");
      const session = await openStagedSettings(page, culture, leaveViewports[0]);
      await page.locator(".pending-changes-bar__actions button").last().click();
      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      await expect(dialog.locator(".dialog__actions button")).toHaveCount(2);

      await page.mouse.click(5, 5);

      await expect(page.locator("dialog[open]")).toHaveCount(0);
      await expect(page.locator(".pending-changes-bar")).toBeVisible();
      await expect(page.getByLabel("Slots", { exact: true })).toHaveValue("80");
      expect(session.unexpected).toEqual([]);
    });
  });
}

test.describe("Settings leave dialog on the narrowest phone (de)", () => {
  test("keeps the full German labels and reaches the last action at 320x568", async ({
    page,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "mobile-390",
      "The 320px layout runs in chromium and mobile-320.",
    );
    const session = await openLeaveDialogAt(page, "de", { width: 320, height: 568 });
    const dialog = page.locator("dialog.dialog--leave[open]");

    await checkDialogGeometry(page, dialog);
    await expectLeaveLayout(page, "de");
    await expectDialogFitsViewport(page);
    const last = dialog.locator(".dialog__actions button").last();
    await expect(last).toHaveText(leaveLabels.de[2]);
    await expect(last).toBeInViewport();
    expect(session.unexpected).toEqual([]);
  });
});

/** The review action of the pending changes bar, per culture. */
const reviewLabels = { en: "Review changes", de: "Änderungen prüfen" } as const satisfies Record<
  Culture,
  string
>;

/**
 * Builds the typed configuration entries the review capacity checks stage: German-neutral names, all Common so
 * every field renders in the open panel.
 *
 * @param count How many entries to serve.
 * @returns The entries in the shape of `GET /api/game-servers/{id}/configuration`.
 */
function reviewEntries(count: number) {
  return Array.from({ length: count }, (_, index) => {
    const number = String(index + 1).padStart(2, "0");
    return {
      key: `server.cfg@setting${number}`,
      targetPath: "server/server.cfg",
      group: "General",
      displayName: `Setting ${number}`,
      description: "",
      kind: "Integer",
      required: false,
      sensitive: false,
      isControlPlaneOnly: false,
      tier: "Common",
      order: index + 1,
      minimum: 1,
      maximum: 1000,
      allowedValues: [],
      choices: [],
      value: 10,
      source: "Default",
      isCompatible: true,
      isRedacted: false,
      warning: null,
    };
  });
}

/**
 * Serves a configuration with `count` typed entries, stages a new value for every one of them and opens the review
 * dialog.
 *
 * @param page The page to drive; its viewport is set before the first navigation.
 * @param culture The culture to run in.
 * @param count How many settings to stage.
 * @param viewport The viewport to open the dialog at.
 * @returns The mock session whose unexpected list the test asserts empty.
 */
async function openReviewWithSettings(
  page: Page,
  culture: Culture,
  count: number,
  viewport: { width: number; height: number },
) {
  await page.setViewportSize(viewport);
  const session = await installWorkspace(page, culture, {
    server: { deployment: null, actions: [fleetAction("start")], canModify: true },
  });
  const pattern = new RegExp(`/api/game-servers/${workspaceServerId}/configuration$`);
  await page.route(pattern, (route) =>
    route.request().method() === "GET"
      ? route.fulfill({
          json: {
            serverId: workspaceServerId,
            profileId: null,
            profileRevisionId: null,
            profileRevisionNumber: null,
            isComplete: true,
            entries: reviewEntries(count),
          },
        })
      : route.fallback(),
  );
  await page.goto(workspaceRoute("settings").replace(/^[/]/, ""));
  for (let index = 1; index <= count; index += 1) {
    const field = page.getByLabel(`Setting ${String(index).padStart(2, "0")}`, { exact: true });
    await field.fill("20");
  }
  await page.locator(".pending-changes-bar").waitFor();
  await page.getByRole("button", { name: reviewLabels[culture] }).first().click();
  await page.locator("dialog.dialog--review[open]").waitFor();
  return session;
}

/**
 * Asserts the single left edge of the review dialog: the kicker, the title, the first group heading and the rows
 * start at the same x, and the actions end at the right edge of the content column. `checkDialogGeometry` is not used
 * here because its facts probe matches the first description list, which in this dialog is a row value pair.
 *
 * @param dialog The open review dialog.
 */
async function expectReviewEdges(dialog: Locator): Promise<void> {
  const edges = await dialog.evaluate((root) => {
    const left = (selector: string) => root.querySelector(selector)?.getBoundingClientRect().left;
    const right = (selector: string) => root.querySelector(selector)?.getBoundingClientRect().right;
    return {
      kicker: left(".kicker"),
      title: left("h2"),
      group: left(".review-group__heading"),
      row: left(".change-item"),
      actions: right(".dialog__actions button:last-child"),
      container: right(".dialog__actions"),
    };
  });
  for (const name of ["title", "group", "row"] as const) {
    expect(edges[name], `${name} left edge`).toBeDefined();
    expect(
      Math.abs(edges[name]! - edges.kicker!),
      `${name} shares the kicker edge`,
    ).toBeLessThanOrEqual(1);
  }
  expect(
    Math.abs(edges.actions! - edges.container!),
    "actions end at the content edge",
  ).toBeLessThanOrEqual(1);
}

for (const culture of ["en", "de"] as const) {
  test.describe(`Review changes dialog capacity (${culture})`, () => {
    test("shows ten staged settings in one view with compact rows", async ({ page }, testInfo) => {
      test.skip(testInfo.project.name !== "chromium", "The widths are set explicitly in chromium.");
      const session = await openReviewWithSettings(page, culture, 10, { width: 1280, height: 900 });
      const dialog = page.locator("dialog.dialog--review[open]");
      await expect(dialog.locator(".change-item")).toHaveCount(10);

      const measured = await dialog.locator(".dialog__body").evaluate((body) => ({
        scrollHeight: body.scrollHeight,
        clientHeight: body.clientHeight,
      }));
      expect(measured.scrollHeight, "the body does not scroll with ten rows").toBeLessThanOrEqual(
        measured.clientHeight + 1,
      );
      const heights = await dialog
        .locator(".change-item")
        .evaluateAll((rows) => rows.map((row) => row.getBoundingClientRect().height));
      for (const height of heights) {
        expect(height, "a single-line row is at most 48px high").toBeLessThanOrEqual(48);
      }
      await expect(dialog.locator(".change-item").last()).toBeInViewport();
      await expectReviewEdges(dialog);
      expect(session.unexpected).toEqual([]);
    });

    test("scrolls forty staged settings inside the body with the actions in place", async ({
      page,
    }, testInfo) => {
      test.skip(testInfo.project.name !== "chromium", "The widths are set explicitly in chromium.");
      const session = await openReviewWithSettings(page, culture, 40, { width: 1280, height: 900 });
      const dialog = page.locator("dialog.dialog--review[open]");
      await expect(dialog.locator(".change-item")).toHaveCount(40);
      const body = dialog.locator(".dialog__body");

      const measure = () =>
        dialog.evaluate((root) => {
          const body = root.querySelector(".dialog__body") as HTMLElement;
          const title = (root.querySelector("h2") as HTMLElement).getBoundingClientRect();
          const actions = (
            root.querySelector(".dialog__actions") as HTMLElement
          ).getBoundingClientRect();
          return {
            bodyScrollHeight: body.scrollHeight,
            bodyClientHeight: body.clientHeight,
            bodyOverflowY: getComputedStyle(body).overflowY,
            dialogScrollHeight: root.scrollHeight,
            dialogClientHeight: root.clientHeight,
            titleTop: title.top,
            actionsTop: actions.top,
            actionsBottom: actions.bottom,
            viewportHeight: window.innerHeight,
          };
        });
      const before = await measure();
      expect(before.bodyScrollHeight, "the body scrolls with forty rows").toBeGreaterThan(
        before.bodyClientHeight + 1,
      );
      expect(before.bodyOverflowY).toBe("auto");
      expect(before.dialogScrollHeight, "the dialog itself does not scroll").toBeLessThanOrEqual(
        before.dialogClientHeight + 1,
      );
      expect(before.actionsBottom, "the actions are inside the viewport").toBeLessThanOrEqual(
        before.viewportHeight + 1,
      );

      await body.evaluate((element) => element.scrollTo(0, element.scrollHeight));
      await expect(dialog.locator(".change-item").last()).toBeInViewport();
      const after = await measure();
      expect(after.titleTop, "the title stays in place").toBeCloseTo(before.titleTop, 0);
      expect(after.actionsTop, "the actions stay in place").toBeCloseTo(before.actionsTop, 0);

      await page.setViewportSize({ width: 320, height: 568 });
      await expectDialogFitsViewport(page);
      const apply = dialog.locator(".dialog__actions button[type=submit]");
      await expect(apply).toBeInViewport();
      expect(session.unexpected).toEqual([]);
    });
  });
}
