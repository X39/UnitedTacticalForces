import { expect, test, type Locator, type Page } from "@playwright/test";
import { checkReflow, checkTextSpacing, scanAxe, settleScroll } from "./support/a11y";
import { auditUserId, type Culture } from "./support/mockApi";
import {
  accessMockState,
  accessRoleRecord,
  accessUserRecord,
  installWorkspace,
  workspaceRoute,
  workspaceServerTitle,
  type AccessMockCall,
  type AccessMockState,
  type WorkspaceMockOptions,
} from "./support/screens/workspaceMocks";

/**
 * Layout boxes come back with fractional coordinates; an edge that ends less than one CSS pixel past the viewport is a
 * rounding artifact, not a clipped control (Phase 62 rule).
 */
const subpixelTolerance = 1;

/** The most a control may move or change its height while a candidate is chosen and cleared (standing preference). */
const shiftTolerance = 0.5;

/** The long German role title of the narrow layouts: it has to wrap inside its row instead of clipping. */
const longRoleTitle =
  "Interessengemeinschaft Taktische Einsatzplanung und Ausbildung Nordrhein-Westfalen";

/** The long candidate name of the narrow layouts: it has to wrap inside the option row. */
const longCandidateName =
  "Christopher Maximilian Langenfeldt-Wiesenthal von Hohenzollern-Sigmaringen";

/** The two viewports of the phase gate: the smallest supported phone and a common one. */
const viewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

/** The visible copy each test reads, per culture; every line is the Copywriting Contract text of that culture. */
interface Copy {
  panelTitle: string;
  listTitle: string;
  memberChoice: string;
  roleChoice: string;
  pickerMember: string;
  pickerRole: string;
  clearSelection: string;
  give: string;
  granted(name: string): string;
  you: string;
  operate: string;
  modify: string;
  manageAccess: string;
  manageSecrets: string;
  announceOn(name: string, permission: string): string;
  announceOff(name: string, permission: string): string;
  removeAll: string;
  removeTitle: string;
  removeBody(name: string): string;
  removeConfirm: string;
  removeDismiss: string;
  removed(name: string): string;
  selfTitle: string;
  selfConfirm: string;
  selfDismiss: string;
  selfLostToast: string;
  settingsHeading: string;
  changeError: string;
}

const copy: Record<Culture, Copy> = {
  en: {
    panelTitle: "Access",
    listTitle: "People and roles with access",
    memberChoice: "Member",
    roleChoice: "Role",
    pickerMember: "Find a member",
    pickerRole: "Find a role",
    clearSelection: "Clear selection",
    give: "Give access",
    granted: (name) => `Access given to ${name}.`,
    you: "You",
    operate: "Operate",
    modify: "Change settings",
    manageAccess: "Manage access",
    manageSecrets: "Manage credentials",
    announceOn: (name, permission) => `${name}: ${permission} granted`,
    announceOff: (name, permission) => `${name}: ${permission} removed`,
    removeAll: "Remove all access",
    removeTitle: "Remove all access?",
    removeBody: (name) =>
      `${name} loses every permission on ${workspaceServerTitle}. You can give access again at any time.`,
    removeConfirm: "Remove access",
    removeDismiss: "Keep access",
    removed: (name) => `All access removed for ${name}.`,
    selfTitle: "Remove this permission from yourself?",
    selfConfirm: "Remove permission",
    selfDismiss: "Keep permission",
    selfLostToast: `You can no longer manage access to ${workspaceServerTitle}.`,
    settingsHeading: "Settings",
    changeError: "This permission was not changed. Try again.",
  },
  de: {
    panelTitle: "Zugriff",
    listTitle: "Personen und Rollen mit Zugriff",
    memberChoice: "Mitglied",
    roleChoice: "Rolle",
    pickerMember: "Mitglied suchen",
    pickerRole: "Rolle suchen",
    clearSelection: "Auswahl aufheben",
    give: "Zugriff geben",
    granted: (name) => `Zugriff für ${name} erteilt.`,
    you: "Du",
    operate: "Bedienen",
    modify: "Einstellungen ändern",
    manageAccess: "Zugriff verwalten",
    manageSecrets: "Zugangsdaten verwalten",
    announceOn: (name, permission) => `${name}: ${permission} erteilt`,
    announceOff: (name, permission) => `${name}: ${permission} entzogen`,
    removeAll: "Gesamten Zugriff entziehen",
    removeTitle: "Gesamten Zugriff entziehen?",
    removeBody: (name) =>
      `${name} verliert alle Berechtigungen für ${workspaceServerTitle}. Du kannst jederzeit erneut Zugriff geben.`,
    removeConfirm: "Zugriff entziehen",
    removeDismiss: "Zugriff behalten",
    removed: (name) => `Gesamter Zugriff für ${name} entzogen.`,
    selfTitle: "Diese Berechtigung bei dir selbst entziehen?",
    selfConfirm: "Berechtigung entziehen",
    selfDismiss: "Berechtigung behalten",
    selfLostToast: `Du kannst den Zugriff auf ${workspaceServerTitle} nicht mehr verwalten.`,
    settingsHeading: "Einstellungen",
    changeError: "Diese Berechtigung wurde nicht geändert. Versuche es erneut.",
  },
};

/** The signed-in member of the mock, holding Full control. */
const operator = accessUserRecord(0, "Operator", ["sv"], auditUserId);

/** The members and the role the mocked list starts with, and the candidates the picker offers. */
const bravo = accessUserRecord(1, "Bravo", ["sv:view"]);
const delta = accessUserRecord(2, "Delta", ["sv:view", "sv:operate"]);
const charlie = accessUserRecord(3, "Charlie");
const echo = accessUserRecord(4, "Echo");
const alphaTeam = accessRoleRecord(7, "Alpha-Team", ["sv:view"], 4, "The first team");

/** The access state of the manager flow: the signed-in member with Full control, two members and one role. */
function managerState(): AccessMockState {
  return accessMockState({
    users: [operator, bravo, delta],
    roles: [alphaTeam],
    userCandidates: [charlie, echo],
    roleCandidates: [accessRoleRecord(8, "Reserve", [], 2)],
  });
}

/** Installs the workspace mock with an access state and the Access panel's reads answered. */
function installAccess(
  page: Page,
  culture: Culture,
  accessState: AccessMockState,
  overrides: WorkspaceMockOptions = {},
): ReturnType<typeof installWorkspace> {
  return installWorkspace(page, culture, { accessState, ...overrides });
}

/** The Access panel. */
function panelOf(page: Page): Locator {
  return page.locator("#server-access");
}

/** The toggle button in the header of the Access panel. */
function headerOf(page: Page, culture: Culture): Locator {
  return panelOf(page).getByRole("button", { name: copy[culture].panelTitle, exact: true });
}

/** The picker input of the grant form, for the member flavor unless another label is given. */
function pickerOf(page: Page, culture: Culture, label?: string): Locator {
  return panelOf(page).getByRole("combobox", { name: label ?? copy[culture].pickerMember });
}

/** The candidate rows of the open picker listbox. */
function optionsOf(page: Page): Locator {
  return panelOf(page).getByRole("listbox").getByRole("option");
}

/** The Give access button of the grant form. */
function giveOf(page: Page, culture: Culture): Locator {
  return panelOf(page).getByRole("button", { name: copy[culture].give, exact: true });
}

/** The toggle button of one subject row, named by the subject. */
function rowButtonOf(page: Page, name: string): Locator {
  return panelOf(page).getByRole("button", { name, exact: true });
}

/** The content region of one subject row, labelled by the subject name. */
function rowRegionOf(page: Page, name: string): Locator {
  return panelOf(page).getByRole("region", { name, exact: true });
}

/** One permission switch of an open row. */
function switchOf(region: Locator, name: string): Locator {
  return region.getByRole("switch", { name, exact: true });
}

/** The polite announcement of the panel, found by its text. */
function announcementOf(page: Page, text: string): Locator {
  return panelOf(page).getByText(text, { exact: true });
}

/** The heading of the list of subjects, which takes focus after Remove all access. */
function listHeadingOf(page: Page, culture: Culture): Locator {
  return panelOf(page).getByRole("heading", { level: 3, name: copy[culture].listTitle });
}

/**
 * Opens the Settings section on the Access panel through the deep link and waits until the panel is expanded.
 *
 * @param page The page to navigate.
 * @param culture The culture of the session.
 */
async function openPanel(page: Page, culture: Culture): Promise<void> {
  await page.goto(`${workspaceRoute("settings").replace(/^\//, "")}#server-access`);
  await expect(
    panelOf(page).getByRole("heading", { level: 2, name: copy[culture].panelTitle }),
  ).toBeVisible();
  await expect(headerOf(page, culture)).toHaveAttribute("aria-expanded", "true");
}

/**
 * Types a search text into the member picker and waits for the candidate rows.
 *
 * @param page The page with the panel open.
 * @param culture The culture of the session.
 * @param search The text to type.
 * @param rows How many candidate rows are expected.
 */
async function searchMembers(
  page: Page,
  culture: Culture,
  search: string,
  rows: number,
): Promise<void> {
  await pickerOf(page, culture).fill(search);
  await expect(optionsOf(page)).toHaveCount(rows);
}

/**
 * Chooses a member of the picker with the pointer.
 *
 * @param page The page with the panel open.
 * @param culture The culture of the session.
 * @param name The nickname to search for and choose.
 */
async function chooseMember(page: Page, culture: Culture, name: string): Promise<void> {
  await searchMembers(page, culture, name, 1);
  await optionsOf(page).filter({ hasText: name }).click();
  await expect(panelOf(page).locator(".search-picker__chosen")).toContainText(name);
}

/**
 * Reads the requests the mock logged so far, without the status the mock answered with.
 *
 * @param state The access state.
 * @returns The calls as kind, direction, subject and permission.
 */
function loggedCalls(state: AccessMockState): Omit<AccessMockCall, "status">[] {
  return state.calls.map(({ kind, grant, subjectId, permission }) => ({
    kind,
    grant,
    subjectId,
    permission,
  }));
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
 * Chooses and clears a candidate and asserts that the grant form controls and the first row header keep their top and
 * height. The in-flow listbox pushes the list down while it is open, so every reading is taken with it closed.
 *
 * @param page The page with the Access panel open and at least one listed subject.
 * @param culture The culture of the session.
 */
async function expectGrantFormStable(page: Page, culture: Culture): Promise<void> {
  const text = copy[culture];
  await expect(rowButtonOf(page, "Bravo")).toBeVisible();
  const slot = panelOf(page).locator(".access-grant__picker .form-field").first();
  const firstHeader = panelOf(page).locator(".access-row").first().locator(".disclosure__summary");
  const measure = async () => {
    await settleScroll(page);
    return {
      slot: await pageBoxOf(slot),
      give: await pageBoxOf(giveOf(page, culture)),
      header: await pageBoxOf(firstHeader),
    };
  };

  await searchMembers(page, culture, "Cha", 1);
  await page.keyboard.press("Escape");
  await expect(panelOf(page).getByRole("listbox")).toHaveCount(0);
  const typed = await measure();

  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  await expect(panelOf(page).locator(".search-picker__chosen")).toContainText("Charlie");
  const chosen = await measure();

  await panelOf(page).getByRole("button", { name: text.clearSelection }).click();
  await expect(optionsOf(page)).toHaveCount(1);
  await page.keyboard.press("Escape");
  await expect(panelOf(page).getByRole("listbox")).toHaveCount(0);
  const cleared = await measure();

  for (const [label, reading] of [
    ["chosen", chosen],
    ["cleared", cleared],
  ] as const) {
    for (const part of ["slot", "give", "header"] as const) {
      expect(
        Math.abs(reading[part].top - typed[part].top),
        `${part} top moved when ${label}`,
      ).toBeLessThanOrEqual(shiftTolerance);
      expect(
        Math.abs(reading[part].height - typed[part].height),
        `${part} height changed when ${label}`,
      ).toBeLessThanOrEqual(shiftTolerance);
    }
  }
}

for (const culture of ["en", "de"] as const) {
  const text = copy[culture];

  test.describe(`server access (${culture})`, () => {
    test("gives access, switches a permission and removes all access", async ({ page }) => {
      const access = managerState();
      const session = await installAccess(page, culture, access);

      await openPanel(page, culture);
      await expect(rowButtonOf(page, "Bravo")).toHaveAttribute("aria-expanded", "false");

      // Give Charlie access through the picker: the grant is View, the announcement names Charlie, the row is closed.
      await chooseMember(page, culture, "Charlie");
      await giveOf(page, culture).click();
      await expect(announcementOf(page, text.granted("Charlie"))).toBeAttached();
      await expect(rowButtonOf(page, "Charlie")).toHaveAttribute("aria-expanded", "false");
      expect(loggedCalls(access)).toEqual([
        { kind: "user", grant: true, subjectId: charlie.userId, permission: "sv:view" },
      ]);
      await scanAxe(page, { include: "#server-access" });

      // Open the new row and switch Operate on and off; each switch is one call and the state follows the server.
      await rowButtonOf(page, "Charlie").click();
      const region = rowRegionOf(page, "Charlie");
      await expect(region).toBeVisible();
      const operate = switchOf(region, text.operate);
      await expect(operate).not.toBeChecked();

      await operate.click();
      await expect(operate).toBeChecked();
      await expect(announcementOf(page, text.announceOn("Charlie", text.operate))).toBeAttached();
      expect(loggedCalls(access).at(-1)).toEqual({
        kind: "user",
        grant: true,
        subjectId: charlie.userId,
        permission: "sv:operate",
      });

      await operate.click();
      await expect(operate).not.toBeChecked();
      await expect(announcementOf(page, text.announceOff("Charlie", text.operate))).toBeAttached();
      expect(loggedCalls(access).at(-1)).toEqual({
        kind: "user",
        grant: false,
        subjectId: charlie.userId,
        permission: "sv:operate",
      });
      expect(access.calls).toHaveLength(3);
      await scanAxe(page, { include: "#server-access" });

      // Remove all access asks first, changes nothing until confirmed and then revokes what is left.
      await region.getByRole("button", { name: text.removeAll, exact: true }).click();
      const dialog = page.getByRole("dialog", { name: text.removeTitle });
      await expect(dialog).toBeVisible();
      await expect(dialog.getByText(text.removeBody("Charlie"), { exact: true })).toBeVisible();
      await expect(dialog.getByRole("button", { name: text.removeDismiss })).toBeFocused();
      await scanAxe(page, { include: "dialog[open]" });
      expect(access.calls).toHaveLength(3);

      await dialog.getByRole("button", { name: text.removeConfirm }).click();
      await expect(dialog).toBeHidden();
      await expect(rowButtonOf(page, "Charlie")).toHaveCount(0);
      await expect(announcementOf(page, text.removed("Charlie"))).toBeAttached();
      await expect(listHeadingOf(page, culture)).toBeFocused();
      expect(loggedCalls(access).slice(3)).toEqual([
        { kind: "user", grant: false, subjectId: charlie.userId, permission: "sv:view" },
      ]);
      expect(access.users.map((user) => user.nickname)).toEqual(["Bravo", "Delta", "Operator"]);
      expect(session.unexpected).toEqual([]);
    });

    test("revokes the permissions of a role in catalog order when all access is removed", async ({
      page,
    }) => {
      const access = accessMockState({
        users: [operator],
        roles: [accessRoleRecord(7, "Alpha-Team", ["sv:modify", "sv:operate", "sv:view"], 4)],
      });
      const session = await installAccess(page, culture, access);

      await openPanel(page, culture);
      await rowButtonOf(page, "Alpha-Team").click();
      await rowRegionOf(page, "Alpha-Team")
        .getByRole("button", { name: text.removeAll, exact: true })
        .click();
      const dialog = page.getByRole("dialog", { name: text.removeTitle });
      await expect(dialog).toBeVisible();
      await dialog.getByRole("button", { name: text.removeConfirm }).click();

      await expect(dialog).toBeHidden();
      await expect(rowButtonOf(page, "Alpha-Team")).toHaveCount(0);
      expect(loggedCalls(access)).toEqual([
        { kind: "role", grant: false, subjectId: "7", permission: "sv:view" },
        { kind: "role", grant: false, subjectId: "7", permission: "sv:operate" },
        { kind: "role", grant: false, subjectId: "7", permission: "sv:modify" },
      ]);
      expect(session.unexpected).toEqual([]);
    });

    test("rolls a refused switch back and shows the alert under it", async ({ page }) => {
      const access = accessMockState({
        users: [operator, bravo],
        failingPermissions: ["sv:modify"],
      });
      const session = await installAccess(page, culture, access);

      await openPanel(page, culture);
      await rowButtonOf(page, "Bravo").click();
      const region = rowRegionOf(page, "Bravo");
      const modify = switchOf(region, text.modify);
      await modify.click();

      await expect(region.getByRole("alert")).toHaveText(text.changeError);
      await expect(modify).not.toBeChecked();
      expect(access.calls).toEqual([
        {
          kind: "user",
          grant: true,
          subjectId: bravo.userId,
          permission: "sv:modify",
          status: 500,
        },
      ]);
      expect(access.users.find((user) => user.nickname === "Bravo")?.permissions).toEqual([
        "sv:view",
      ]);
      expect(session.unexpected).toEqual([]);
    });

    test("asks before the member removes their own Manage access and leaves the panel safely", async ({
      page,
    }) => {
      const access = accessMockState({
        users: [
          accessUserRecord(0, "Operator", ["sv:view", "sv:manage-access"], auditUserId),
          bravo,
        ],
      });
      const session = await installAccess(page, culture, access);

      await openPanel(page, culture);
      await rowButtonOf(page, "Operator").click();
      await expect(
        panelOf(page).locator(".access-row").filter({ hasText: "Operator" }).getByText(text.you, {
          exact: true,
        }),
      ).toBeVisible();
      const manage = switchOf(rowRegionOf(page, "Operator"), text.manageAccess);
      await expect(manage).toBeChecked();

      await manage.click();
      const dialog = page.getByRole("dialog", { name: text.selfTitle });
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole("button", { name: text.selfDismiss })).toBeFocused();
      await expect(manage).toBeChecked();
      await scanAxe(page, { include: "dialog[open]" });
      expect(access.calls).toEqual([]);

      await dialog.getByRole("button", { name: text.selfConfirm }).click();

      // The member cannot manage access any more: the panel is gone, the toast says why, focus is on the Settings title.
      await expect(page.getByText(text.selfLostToast, { exact: true })).toBeVisible();
      await expect(panelOf(page)).toHaveCount(0);
      await expect(
        page.getByRole("heading", { level: 2, name: text.settingsHeading, exact: true }),
      ).toBeFocused();
      expect(loggedCalls(access)).toEqual([
        { kind: "user", grant: false, subjectId: auditUserId, permission: "sv:manage-access" },
      ]);
      expect(session.unexpected).toEqual([]);
    });

    test("gives access with the keyboard only", async ({ page }) => {
      const access = managerState();
      const session = await installAccess(page, culture, access);

      // The deep link focuses the header button of the panel.
      await openPanel(page, culture);
      await expect(headerOf(page, culture)).toBeFocused();

      await page.keyboard.press("Tab");
      const member = panelOf(page).getByRole("radio", { name: text.memberChoice });
      const role = panelOf(page).getByRole("radio", { name: text.roleChoice });
      await expect(member).toBeFocused();
      await page.keyboard.press("ArrowRight");
      await expect(role).toBeFocused();
      await expect(role).toBeChecked();
      await expect(pickerOf(page, culture, text.pickerRole)).toBeVisible();
      await page.keyboard.press("ArrowLeft");
      await expect(member).toBeFocused();
      await expect(member).toBeChecked();

      await page.keyboard.press("Tab");
      await expect(pickerOf(page, culture)).toBeFocused();
      await page.keyboard.type("Cha");
      await expect(optionsOf(page)).toHaveCount(1);
      await page.keyboard.press("ArrowDown");
      await page.keyboard.press("Enter");
      await expect(panelOf(page).locator(".search-picker__chosen")).toContainText("Charlie");

      // Choosing moves focus to the clear button, so the Give access button is the next stop.
      await expect(panelOf(page).getByRole("button", { name: text.clearSelection })).toBeFocused();
      await page.keyboard.press("Tab");
      await expect(giveOf(page, culture)).toBeFocused();
      await page.keyboard.press("Enter");

      await expect(announcementOf(page, text.granted("Charlie"))).toBeAttached();
      await expect(pickerOf(page, culture)).toBeFocused();
      await expect(rowButtonOf(page, "Charlie")).toBeVisible();
      expect(loggedCalls(access)).toEqual([
        { kind: "user", grant: true, subjectId: charlie.userId, permission: "sv:view" },
      ]);
      expect(session.unexpected).toEqual([]);
    });

    test("keeps the grant form and the row headers in place while a candidate is chosen and cleared", async ({
      page,
    }) => {
      const session = await installAccess(page, culture, managerState());
      await openPanel(page, culture);
      await expectGrantFormStable(page, culture);
      expect(session.unexpected).toEqual([]);
    });

    test("fetches the access list again when the page becomes visible", async ({ page }) => {
      const access = managerState();
      const session = await installAccess(page, culture, access);
      await openPanel(page, culture);
      await expect(rowButtonOf(page, "Bravo")).toBeVisible();
      await expect(rowButtonOf(page, "Foxtrot")).toHaveCount(0);
      const before = access.reads;
      expect(before).toBeGreaterThanOrEqual(1);

      // Another manager gives Foxtrot access; nothing is pushed to the open panel.
      access.users.push(accessUserRecord(9, "Foxtrot", ["sv:view"]));
      await page.evaluate(() =>
        document.dispatchEvent(new Event("visibilitychange", { bubbles: true })),
      );

      await expect.poll(() => access.reads).toBe(before + 1);
      await expect(rowButtonOf(page, "Foxtrot")).toBeVisible();
      expect(session.unexpected).toEqual([]);
    });
  });
}

/** An intermediate width between the phone breakpoint and the wide layout, where the button column is narrow. */
const intermediateViewport = { width: 600, height: 900 } as const;

test.describe("server access layout (de, 600x900)", () => {
  test.use({ viewport: intermediateViewport });

  test("keeps the grant form and the row headers in place while a candidate is chosen and cleared", async ({
    page,
  }) => {
    const session = await installAccess(page, "de", managerState());
    await openPanel(page, "de");
    await expectGrantFormStable(page, "de");
    expect(session.unexpected).toEqual([]);
  });
});

for (const viewport of viewports) {
  test.describe(`server access layout (de, ${viewport.width}x${viewport.height})`, () => {
    test.use({ viewport });

    test("keeps the switches, the picker and a long role title inside the viewport", async ({
      page,
    }) => {
      const culture = "de";
      const text = copy[culture];
      const access = accessMockState({
        users: [operator, bravo],
        roles: [accessRoleRecord(7, longRoleTitle, ["sv:view"], 12, "Die Einsatzgruppe")],
        userCandidates: [
          charlie,
          accessUserRecord(5, longCandidateName),
          accessUserRecord(6, "Carlos"),
        ],
      });
      const session = await installAccess(page, culture, access);

      await openPanel(page, culture);
      await settleScroll(page);
      await expectNoHorizontalScroll(page);
      await expectInsideHorizontally(
        page,
        rowButtonOf(page, longRoleTitle),
        viewport.width,
        "long role row",
      );

      // A row with all eight switches; the longest label sits inside the viewport together with its switch.
      await rowButtonOf(page, "Bravo").click();
      const region = rowRegionOf(page, "Bravo");
      await expect(region.getByRole("switch")).toHaveCount(8);
      await expectInsideHorizontally(
        page,
        region.getByText(text.manageSecrets, { exact: true }),
        viewport.width,
        "Zugangsdaten verwalten label",
      );
      await expectInsideHorizontally(
        page,
        switchOf(region, text.manageSecrets),
        viewport.width,
        "Zugangsdaten verwalten switch",
      );
      await expectNoHorizontalScroll(page);

      // The picker with results, including the long candidate name.
      await searchMembers(page, culture, "C", 3);
      await settleScroll(page);
      await expectNoHorizontalScroll(page);
      for (const [index, option] of (await optionsOf(page).all()).entries()) {
        await expectInsideHorizontally(page, option, viewport.width, `candidate row ${index + 1}`);
      }
      await expectInsideHorizontally(
        page,
        giveOf(page, culture),
        viewport.width,
        "Zugriff geben button",
      );
      await checkReflow(page);
      await checkTextSpacing(page);

      await optionsOf(page).filter({ hasText: longCandidateName }).click();
      await expect(panelOf(page).locator(".search-picker__chosen")).toContainText(
        longCandidateName,
      );
      await settleScroll(page);
      await expectNoHorizontalScroll(page);
      await checkReflow(page);
      await checkTextSpacing(page);
      expect(session.unexpected).toEqual([]);
    });

    test("keeps the grant form and the row headers in place while a candidate is chosen and cleared", async ({
      page,
    }) => {
      const session = await installAccess(page, "de", managerState());
      await openPanel(page, "de");
      await expectGrantFormStable(page, "de");
      expect(session.unexpected).toEqual([]);
    });
  });
}
