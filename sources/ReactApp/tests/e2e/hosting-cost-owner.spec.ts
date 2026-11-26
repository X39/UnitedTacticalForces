import { expect, test, type Locator, type Page } from "@playwright/test";
import { checkReflow, checkTextSpacing, scanAxe, settleScroll } from "./support/a11y";
import type { Culture } from "./support/mockApi";
import {
  costOwnerOptionRecord,
  installWorkspace,
  workspaceControlsOf,
  workspaceRoute,
  workspaceServerTitle,
  type WorkspaceMockOptions,
} from "./support/screens/workspaceMocks";

/** The name of the cost owner the mocked server starts with; the automation mock names it "Operations". */
const currentOwner = "Operations";

/**
 * Layout boxes come back with fractional coordinates; an edge that ends less than one CSS pixel past the viewport is a
 * rounding artifact, not a clipped control (Phase 62 rule).
 */
const subpixelTolerance = 1;

/** The long German owner name of the narrow layouts: it has to wrap inside the option row instead of clipping. */
const longOwnerName =
  "Interessengemeinschaft Taktische Einsatzplanung und Ausbildung Nordrhein-Westfalen e. V.";

/** The two viewports of the phase gate: the smallest supported phone and a common one. */
const viewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

/** The visible copy each test reads, per culture; every line is the Copywriting Contract text of that culture. */
interface Copy {
  panelTitle: string;
  ownerTerm: string;
  pickerLabel: string;
  transfer: string;
  billedTo(owner: string): string;
  readOnly: string;
  unavailableRunning: string;
  unavailableJobs: string;
  unavailableChoose: string;
  dialogTitle: string;
  dialogBody(owner: string): string;
  dismiss: string;
  confirm: string;
  conflict: string;
  toast(owner: string): string;
}

const copy: Record<Culture, Copy> = {
  en: {
    panelTitle: "Cost owner",
    ownerTerm: "Cost owner",
    pickerLabel: "New cost owner",
    transfer: "Transfer cost owner",
    billedTo: (owner) => `Billed to ${owner}`,
    readOnly: "Only members who may change this server can transfer its cost owner.",
    unavailableRunning:
      "Stop the server first. The cost owner can only change while no cloud machine is running or being removed.",
    unavailableJobs: "Wait until the pending jobs of this server have finished.",
    unavailableChoose: "Choose the new cost owner first.",
    dialogTitle: "Transfer cost owner?",
    dialogBody: (owner) =>
      `From now on, ${owner} pays for every start of ${workspaceServerTitle}, not ${currentOwner}.`,
    dismiss: "Don't transfer",
    confirm: "Transfer cost owner",
    conflict:
      "The server has a cloud machine that is still running or being removed. Stop it first, then try again.",
    toast: (owner) => `${owner} now pays for ${workspaceServerTitle}.`,
  },
  de: {
    panelTitle: "Kostenträger",
    ownerTerm: "Kostenträger",
    pickerLabel: "Neuer Kostenträger",
    transfer: "Kostenträger übertragen",
    billedTo: (owner) => `Abgerechnet an ${owner}`,
    readOnly:
      "Nur Mitglieder, die diesen Server ändern dürfen, können den Kostenträger übertragen.",
    unavailableRunning:
      "Stoppe zuerst den Server. Der Kostenträger lässt sich nur ändern, solange keine Cloud-Maschine läuft oder entfernt wird.",
    unavailableJobs: "Warte, bis die ausstehenden Aufträge dieses Servers abgeschlossen sind.",
    unavailableChoose: "Wähle zuerst den neuen Kostenträger aus.",
    dialogTitle: "Kostenträger übertragen?",
    dialogBody: (owner) =>
      `Ab jetzt bezahlt ${owner} jeden Start von ${workspaceServerTitle}, nicht mehr ${currentOwner}.`,
    dismiss: "Nicht übertragen",
    confirm: "Kostenträger übertragen",
    conflict:
      "Der Server hat eine Cloud-Maschine, die noch läuft oder entfernt wird. Stoppe ihn zuerst und versuche es dann erneut.",
    toast: (owner) => `${owner} bezahlt jetzt ${workspaceServerTitle}.`,
  },
};

/** The three owners the picker offers; the second one is chosen by most tests. */
const owners = [
  costOwnerOptionRecord(1, "Bravo"),
  costOwnerOptionRecord(2, "Charlie", { kind: "User", isCurrentUser: true }),
  costOwnerOptionRecord(3, "Delta", { kind: "ExternalOrganization" }),
];

/** The members of a server without a cloud machine and without pending jobs, which may be transferred. */
const offServer = { deployment: null, pendingJobCount: 0, actions: [], canModify: true };

/** The mock options of a stopped server whose cost owner a member may transfer. */
function transferable(overrides: WorkspaceMockOptions = {}): WorkspaceMockOptions {
  return {
    server: offServer,
    accrued: false,
    costOwnerOptions: owners,
    ...overrides,
  };
}

/** The Cost owner panel. */
function panelOf(page: Page): Locator {
  return page.locator("#server-cost-owner");
}

/** The toggle button in the header of the Cost owner panel. */
function headerOf(page: Page, culture: Culture): Locator {
  return panelOf(page).getByRole("button", { name: copy[culture].panelTitle, exact: true });
}

/** The owner picker input. */
function pickerOf(page: Page, culture: Culture): Locator {
  return panelOf(page).getByRole("combobox", { name: copy[culture].pickerLabel });
}

/** The owner rows of the open picker listbox of the panel. */
function optionsOf(page: Page): Locator {
  return panelOf(page).getByRole("listbox").getByRole("option");
}

/** The Transfer button of the panel. */
function transferOf(page: Page, culture: Culture): Locator {
  return panelOf(page).getByRole("button", { name: copy[culture].transfer, exact: true });
}

/** The value of the cost owner row of the cost strip. */
function stripOwnerOf(page: Page, culture: Culture): Locator {
  return page
    .locator(".cost-strip__row")
    .filter({ has: page.locator("dt", { hasText: copy[culture].ownerTerm }) })
    .locator("dd");
}

/** The open confirmation dialog. */
function dialogOf(page: Page, culture: Culture): Locator {
  return page.getByRole("dialog", { name: copy[culture].dialogTitle });
}

/**
 * Opens the Settings section on the Cost owner panel through the deep link and waits until the panel is expanded.
 *
 * @param page The page to navigate.
 * @param culture The culture of the session.
 */
async function openPanel(page: Page, culture: Culture): Promise<void> {
  await page.goto(`${workspaceRoute("settings").replace(/^\//, "")}#server-cost-owner`);
  await expect(
    panelOf(page).getByRole("heading", { level: 2, name: copy[culture].panelTitle }),
  ).toBeVisible();
  await expect(headerOf(page, culture)).toHaveAttribute("aria-expanded", "true");
}

/**
 * Chooses an owner of the picker with the pointer: the first focus lists the owners, the click on a row chooses one.
 *
 * @param page The page with the panel open.
 * @param culture The culture of the session.
 * @param name The display name of the owner to choose.
 */
async function chooseOwner(page: Page, culture: Culture, name: string): Promise<void> {
  await pickerOf(page, culture).focus();
  await optionsOf(page).filter({ hasText: name }).click();
  await expect(panelOf(page).locator(".search-picker__chosen")).toContainText(name);
}

/** Asserts that the document does not scroll sideways. */
async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + subpixelTolerance);
}

/**
 * Asserts that an element lies between the left and the right edge of the viewport, allowing the subpixel tolerance.
 *
 * @param page The page the element belongs to.
 * @param locator The element to measure; it is scrolled into view and the scroll settles first.
 * @param width The viewport width in CSS pixels.
 * @param what The name of the element for the failure message.
 */
async function expectInsideHorizontally(
  page: Page,
  locator: Locator,
  width: number,
  what: string,
): Promise<void> {
  await locator.scrollIntoViewIfNeeded();
  await settleScroll(page);
  const box = await locator.boundingBox();
  expect(box, `${what} has a box`).not.toBeNull();
  expect(box!.x, `${what} left edge`).toBeGreaterThanOrEqual(-subpixelTolerance);
  expect(box!.x + box!.width, `${what} right edge`).toBeLessThanOrEqual(width + subpixelTolerance);
}

/** The most a control may move or change its height while an owner is chosen and cleared (standing preference). */
const shiftTolerance = 0.5;

/** The top and height of an element in page coordinates, so a scroll between two readings cannot count as a shift. */
interface PageBox {
  top: number;
  height: number;
}

/**
 * Reads the page-relative top and the height of an element.
 *
 * @param locator The element to measure.
 * @returns The box in page coordinates.
 */
async function pageBoxOf(locator: Locator): Promise<PageBox> {
  return locator.evaluate((element) => {
    const rect = element.getBoundingClientRect();
    return { top: rect.top + window.scrollY, height: rect.height };
  });
}

/**
 * Chooses and clears an owner and asserts that the picker slot, the Transfer button and the panel below keep their top
 * and height. The in-flow listbox pushes everything down while it is open, so every reading is taken with it closed.
 * The Transfer button prints the reason it is unavailable under itself until an owner is chosen; when that line goes
 * the cell must not shrink, or the Settings panels below would jump (XPF-71-11-2).
 *
 * @param page The page with the Cost owner panel open on a transferable server.
 * @param culture The culture of the session.
 */
async function expectTransferFormStable(page: Page, culture: Culture): Promise<void> {
  const text = copy[culture];
  const slot = panelOf(page).locator(".cost-owner-transfer__picker .form-field").first();
  const below = page.locator("#server-record");
  const measure = async () => {
    await settleScroll(page);
    return {
      slot: await pageBoxOf(slot),
      transfer: await pageBoxOf(transferOf(page, culture)),
      below: await pageBoxOf(below),
    };
  };

  await expect(panelOf(page).getByText(text.unavailableChoose, { exact: true })).toBeVisible();
  const before = await measure();

  await pickerOf(page, culture).focus();
  await expect(optionsOf(page)).toHaveCount(owners.length);
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  await expect(panelOf(page).locator(".search-picker__chosen")).toContainText("Bravo");
  await expect(panelOf(page).getByText(text.unavailableChoose, { exact: true })).toHaveCount(0);
  const chosen = await measure();

  await panelOf(page)
    .getByRole("button", { name: /^(Clear selection|Auswahl aufheben)$/ })
    .click();
  await expect(panelOf(page).getByText(text.unavailableChoose, { exact: true })).toBeVisible();
  // Clearing returns the focus to the input, which lists the owners again; close that list before the reading.
  await page.keyboard.press("Escape");
  await expect(panelOf(page).getByRole("listbox")).toHaveCount(0);
  const cleared = await measure();

  for (const [label, reading] of [
    ["chosen", chosen],
    ["cleared", cleared],
  ] as const) {
    for (const part of ["slot", "transfer", "below"] as const) {
      expect(
        Math.abs(reading[part].top - before[part].top),
        `${part} top moved when ${label}`,
      ).toBeLessThanOrEqual(shiftTolerance);
      expect(
        Math.abs(reading[part].height - before[part].height),
        `${part} height changed when ${label}`,
      ).toBeLessThanOrEqual(shiftTolerance);
    }
  }
}

/**
 * Chooses an owner on a server that still blocks the transfer, then clears the blocker and asserts that the picker
 * slot, the Transfer button and the panel below keep their top and height. The long "cloud machine exists" reason
 * is replaced by nothing when the refetched server no longer blocks, which is driven by the server and not by the
 * member, so the cell must be tall enough for the longest reason from the start (XPF-71-REVIEW-WR-03). The refetched
 * server also changes the workspace above the panel, so every reading is taken relative to the top of the panel.
 *
 * @param page The page with the Cost owner panel open on a server that has a cloud machine.
 * @param culture The culture of the session.
 */
async function expectBlockerClearingStable(page: Page, culture: Culture): Promise<void> {
  const text = copy[culture];
  const slot = panelOf(page).locator(".cost-owner-transfer__picker .form-field").first();
  const below = page.locator("#server-record");
  const measure = async () => {
    await settleScroll(page);
    const panel = await pageBoxOf(panelOf(page));
    const relative = async (locator: Locator): Promise<PageBox> => {
      const box = await pageBoxOf(locator);
      return { top: box.top - panel.top, height: box.height };
    };
    return {
      slot: await relative(slot),
      transfer: await relative(transferOf(page, culture)),
      below: await relative(below),
    };
  };

  await chooseOwner(page, culture, "Charlie");
  await page.keyboard.press("Escape");
  await expect(panelOf(page).getByText(text.unavailableRunning, { exact: true })).toBeVisible();
  const blocked = await measure();

  // The machine goes away elsewhere; the next read of the server shows a transferable server.
  workspaceControlsOf(page).patchServer(offServer);
  await page.locator(".workspace-details__refresh button").click();
  await expect(panelOf(page).getByText(text.unavailableRunning, { exact: true })).toHaveCount(0);
  await expect(transferOf(page, culture)).not.toHaveAttribute("aria-disabled", "true");
  const cleared = await measure();

  // The panel below shows the deployment of the server, so its own height may change; its top, which is the height of
  // this panel, may not.
  for (const part of ["slot", "transfer", "below"] as const) {
    expect(
      Math.abs(cleared[part].top - blocked[part].top),
      `${part} top moved when the blocker cleared`,
    ).toBeLessThanOrEqual(shiftTolerance);
  }
  for (const part of ["slot", "transfer"] as const) {
    expect(
      Math.abs(cleared[part].height - blocked[part].height),
      `${part} height changed when the blocker cleared`,
    ).toBeLessThanOrEqual(shiftTolerance);
  }
}

for (const culture of ["en", "de"] as const) {
  const text = copy[culture];

  test.describe(`cost owner transfer (${culture})`, () => {
    test("keeps the panel below in place when the transfer blocker clears", async ({ page }) => {
      const session = await installWorkspace(page, culture, { costOwnerOptions: owners });
      await openPanel(page, culture);
      await expectBlockerClearingStable(page, culture);
      expect(session.unexpected).toEqual([]);
    });

    test("transfers the cost owner and shows the new owner in the panel and the cost strip", async ({
      page,
    }) => {
      const bodies: Record<string, unknown>[] = [];
      const session = await installWorkspace(
        page,
        culture,
        transferable({ costOwnerTransferBodies: bodies }),
      );

      await openPanel(page, culture);
      await expect(
        panelOf(page).getByText(text.billedTo(currentOwner), { exact: true }),
      ).toBeVisible();
      await expect(stripOwnerOf(page, culture)).toContainText(currentOwner);
      await expect(transferOf(page, culture)).toHaveAttribute("aria-disabled", "true");
      await expect(panelOf(page).getByText(text.unavailableChoose, { exact: true })).toBeVisible();

      // The first focus of the picker lists the owners; the open picker and the panel have no axe violation.
      await pickerOf(page, culture).focus();
      await expect(optionsOf(page)).toHaveCount(owners.length);
      await scanAxe(page, { include: "#server-cost-owner" });

      await optionsOf(page).filter({ hasText: "Charlie" }).click();
      await expect(panelOf(page).locator(".search-picker__chosen")).toContainText("Charlie");
      await expect(transferOf(page, culture)).not.toHaveAttribute("aria-disabled", "true");
      await scanAxe(page, { include: "#server-cost-owner" });

      await transferOf(page, culture).click();
      const dialog = dialogOf(page, culture);
      await expect(dialog).toBeVisible();
      await expect(dialog.getByText(text.dialogBody("Charlie"), { exact: true })).toBeVisible();
      await expect(dialog.getByRole("button", { name: text.dismiss })).toBeFocused();
      await scanAxe(page, { include: "dialog[open]" });
      expect(bodies).toEqual([]);

      await dialog.getByRole("button", { name: text.confirm }).click();

      await expect(page.getByText(text.toast("Charlie"), { exact: true })).toBeVisible();
      await expect(dialog).toBeHidden();
      await expect(
        panelOf(page).getByText(text.billedTo("Charlie"), { exact: true }),
      ).toBeVisible();
      await expect(stripOwnerOf(page, culture)).toContainText("Charlie");
      await expect(stripOwnerOf(page, culture)).not.toContainText(currentOwner);
      expect(bodies).toEqual([{ costOwnerId: owners[1]!.costOwnerId }]);
      expect(session.unexpected).toEqual([]);
    });

    test("keeps Transfer unavailable with its reason while a cloud machine exists", async ({
      page,
    }) => {
      const bodies: Record<string, unknown>[] = [];
      // The default mocked server still has a running deployment.
      const session = await installWorkspace(page, culture, {
        costOwnerOptions: owners,
        costOwnerTransferBodies: bodies,
      });

      await openPanel(page, culture);
      await chooseOwner(page, culture, "Charlie");

      // The running reason outranks "choose an owner", even though an owner is chosen.
      await expect(panelOf(page).getByText(text.unavailableRunning, { exact: true })).toBeVisible();
      await expect(panelOf(page).getByText(text.unavailableChoose, { exact: true })).toHaveCount(0);
      await expect(transferOf(page, culture)).toHaveAttribute("aria-disabled", "true");
      // Playwright refuses to click an aria-disabled control, so the click is forced to prove it does nothing.
      await transferOf(page, culture).click({ force: true });
      await expect(page.locator("dialog[open]")).toHaveCount(0);
      expect(bodies).toEqual([]);
      expect(session.unexpected).toEqual([]);
    });

    test("keeps Transfer unavailable with its reason while jobs are pending", async ({ page }) => {
      const bodies: Record<string, unknown>[] = [];
      const session = await installWorkspace(
        page,
        culture,
        transferable({
          server: { ...offServer, pendingJobCount: 1 },
          costOwnerTransferBodies: bodies,
        }),
      );

      await openPanel(page, culture);
      await chooseOwner(page, culture, "Charlie");

      await expect(panelOf(page).getByText(text.unavailableJobs, { exact: true })).toBeVisible();
      await expect(panelOf(page).getByText(text.unavailableRunning, { exact: true })).toHaveCount(
        0,
      );
      await expect(transferOf(page, culture)).toHaveAttribute("aria-disabled", "true");
      await transferOf(page, culture).click({ force: true });
      await expect(page.locator("dialog[open]")).toHaveCount(0);
      expect(bodies).toEqual([]);
      expect(session.unexpected).toEqual([]);
    });

    test("shows the conflict message inside the open dialog when the API answers 409", async ({
      page,
    }) => {
      const bodies: Record<string, unknown>[] = [];
      const session = await installWorkspace(
        page,
        culture,
        transferable({ costOwnerTransfer: 409, costOwnerTransferBodies: bodies }),
      );

      await openPanel(page, culture);
      await chooseOwner(page, culture, "Charlie");
      await transferOf(page, culture).click();
      const dialog = dialogOf(page, culture);
      await expect(dialog).toBeVisible();
      await dialog.getByRole("button", { name: text.confirm }).click();

      // The dialog stays open with the message of its status; nothing changed and nothing was announced.
      await expect(dialog.getByRole("alert")).toHaveText(text.conflict);
      await expect(dialog).toBeVisible();
      await expect(page.getByText(text.toast("Charlie"), { exact: true })).toHaveCount(0);
      await expect(stripOwnerOf(page, culture)).toContainText(currentOwner);
      expect(bodies).toEqual([{ costOwnerId: owners[1]!.costOwnerId }]);

      // The member can dismiss the dialog and keeps the chosen owner.
      await dialog.getByRole("button", { name: text.dismiss }).click();
      await expect(dialog).toBeHidden();
      await expect(panelOf(page).locator(".search-picker__chosen")).toContainText("Charlie");
      expect(session.unexpected).toEqual([]);
    });

    test("shows the read-only sentence, no picker and no owners request without canModify", async ({
      page,
    }) => {
      const costOwnerRequests: string[] = [];
      page.on("request", (request) => {
        const path = new URL(request.url()).pathname;
        if (path.includes("/cost-owner")) costOwnerRequests.push(`${request.method()} ${path}`);
      });
      const session = await installWorkspace(page, culture, {
        server: { canModify: false },
        costOwnerOptions: owners,
      });

      await openPanel(page, culture);
      await expect(panelOf(page).getByText(text.readOnly, { exact: true })).toBeVisible();
      await expect(pickerOf(page, culture)).toHaveCount(0);
      await expect(transferOf(page, culture)).toHaveCount(0);
      await expect(
        panelOf(page).getByText(text.billedTo(currentOwner), { exact: true }),
      ).toBeVisible();

      // Walking through the panel with the keyboard never asks for owners.
      await headerOf(page, culture).focus();
      for (let step = 0; step < 6; step += 1) await page.keyboard.press("Tab");
      await scanAxe(page, { include: "#server-cost-owner" });
      expect(costOwnerRequests).toEqual([]);
      expect(session.unexpected).toEqual([]);
    });

    test("transfers with the keyboard only", async ({ page }) => {
      const bodies: Record<string, unknown>[] = [];
      const session = await installWorkspace(
        page,
        culture,
        transferable({ costOwnerTransferBodies: bodies }),
      );

      // The deep link focuses the header button of the panel.
      await openPanel(page, culture);
      await expect(headerOf(page, culture)).toBeFocused();

      await page.keyboard.press("Tab");
      await expect(pickerOf(page, culture)).toBeFocused();
      await expect(optionsOf(page)).toHaveCount(owners.length);
      await page.keyboard.press("ArrowDown");
      await page.keyboard.press("Enter");
      await expect(panelOf(page).locator(".search-picker__chosen")).toContainText("Bravo");

      await page.keyboard.press("Tab");
      await expect(transferOf(page, culture)).toBeFocused();
      await page.keyboard.press("Enter");
      const dialog = dialogOf(page, culture);
      await expect(dialog).toBeVisible();
      await expect(dialog.getByText(text.dialogBody("Bravo"), { exact: true })).toBeVisible();
      // The dismiss button holds the initial focus, so a stray Enter never transfers.
      await expect(dialog.getByRole("button", { name: text.dismiss })).toBeFocused();
      expect(bodies).toEqual([]);

      // The confirm button follows the dismiss button in the focus order.
      await page.keyboard.press("Tab");
      await expect(dialog.getByRole("button", { name: text.confirm })).toBeFocused();
      await page.keyboard.press("Enter");

      await expect(page.getByText(text.toast("Bravo"), { exact: true })).toBeVisible();
      await expect(dialog).toBeHidden();
      // Once the refetched server carries the new owner, focus moves to the owner value of the facts.
      await expect(panelOf(page).locator(".cost-owner-facts dd").first()).toBeFocused();
      await expect(panelOf(page).getByText(text.billedTo("Bravo"), { exact: true })).toBeVisible();
      expect(bodies).toEqual([{ costOwnerId: owners[0]!.costOwnerId }]);
      expect(session.unexpected).toEqual([]);
    });

    test("keeps the picker, the Transfer button and the panel below in place while an owner is chosen and cleared", async ({
      page,
    }) => {
      const session = await installWorkspace(page, culture, transferable());
      await openPanel(page, culture);
      await expectTransferFormStable(page, culture);
      expect(session.unexpected).toEqual([]);
    });
  });
}

/** An intermediate width between the phone breakpoint and the wide layout, where the button column is narrow. */
const intermediateViewport = { width: 600, height: 900 } as const;

test.describe("cost owner layout (de, 600x900)", () => {
  test.use({ viewport: intermediateViewport });

  test("keeps the picker, the Transfer button and the panel below in place while an owner is chosen and cleared", async ({
    page,
  }) => {
    const session = await installWorkspace(page, "de", transferable());
    await openPanel(page, "de");
    await expectTransferFormStable(page, "de");
    expect(session.unexpected).toEqual([]);
  });

  test("keeps the panel below in place when the transfer blocker clears", async ({ page }) => {
    const session = await installWorkspace(page, "de", { costOwnerOptions: owners });
    await openPanel(page, "de");
    await expectBlockerClearingStable(page, "de");
    expect(session.unexpected).toEqual([]);
  });
});

for (const viewport of viewports) {
  test.describe(`cost owner layout (de, ${viewport.width}x${viewport.height})`, () => {
    test.use({ viewport });

    test("keeps the picker, the long owner name and the dialog inside the viewport", async ({
      page,
    }) => {
      const culture = "de";
      const text = copy[culture];
      const session = await installWorkspace(
        page,
        culture,
        transferable({
          costOwnerOptions: [
            owners[0]!,
            costOwnerOptionRecord(4, longOwnerName, { kind: "ExternalOrganization" }),
            owners[2]!,
          ],
        }),
      );

      await openPanel(page, culture);
      await pickerOf(page, culture).focus();
      await expect(optionsOf(page)).toHaveCount(3);
      await settleScroll(page);

      await expectNoHorizontalScroll(page);
      await expectInsideHorizontally(
        page,
        panelOf(page).getByText(text.pickerLabel, { exact: true }).first(),
        viewport.width,
        "picker label",
      );
      for (const [index, option] of (await optionsOf(page).all()).entries()) {
        await expectInsideHorizontally(page, option, viewport.width, `option row ${index + 1}`);
      }
      await expectInsideHorizontally(
        page,
        transferOf(page, culture),
        viewport.width,
        "Transfer button",
      );
      await checkReflow(page);
      await checkTextSpacing(page);

      await optionsOf(page).filter({ hasText: longOwnerName }).click();
      await expect(panelOf(page).locator(".search-picker__chosen")).toContainText(longOwnerName);
      await expectNoHorizontalScroll(page);
      await checkReflow(page);
      await checkTextSpacing(page);

      await transferOf(page, culture).click();
      const dialog = dialogOf(page, culture);
      await expect(dialog).toBeVisible();
      await settleScroll(page);
      const box = await dialog.boundingBox();
      expect(box, "dialog has a box").not.toBeNull();
      expect(box!.x, "dialog left edge").toBeGreaterThanOrEqual(-subpixelTolerance);
      expect(box!.x + box!.width, "dialog right edge").toBeLessThanOrEqual(
        viewport.width + subpixelTolerance,
      );
      expect(box!.y + box!.height, "dialog bottom edge").toBeLessThanOrEqual(
        viewport.height + subpixelTolerance,
      );
      await expect(dialog.getByRole("button", { name: text.confirm })).toBeInViewport();
      await expectNoHorizontalScroll(page);
      await checkReflow(page);
      await checkTextSpacing(page);
      expect(session.unexpected).toEqual([]);
    });

    test("keeps the picker, the Transfer button and the panel below in place while an owner is chosen and cleared", async ({
      page,
    }) => {
      const session = await installWorkspace(page, "de", transferable());
      await openPanel(page, "de");
      await expectTransferFormStable(page, "de");
      expect(session.unexpected).toEqual([]);
    });
  });
}
