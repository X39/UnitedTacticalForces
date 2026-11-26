import { Buffer } from "node:buffer";
import type { Page } from "@playwright/test";
import {
  adminClaims,
  adminHandlers,
  homeHandlers,
  smokePageSlug,
  smokeRoleId,
} from "../fixtures/adminMocks";
import { filesHandlers, filesServerId } from "../fixtures/filesMocks";
import {
  costAlertHandlers,
  financeClaims,
  financeHandlers,
  financeOwnerIds,
} from "../fixtures/financeMocks";
import { profileHandlers, profileIds } from "../fixtures/profileMocks";
import { installApp } from "../mockApi";
import type { ScreenEntry } from "./registryTypes";

/**
 * The adjacent slice of the screen registry: the Files and Machine tabs, cost alerts, configuration profiles,
 * hosting finance, their dialogs and the four non-server smoke pages. Plan 69-28 adds the entries and empties the
 * pending list.
 *
 * Importing only types, the strict mock layer and the fixture modules keeps this module loadable by the Vitest
 * completeness guard. Every `ready` waits on page structure (roles, landmarks and class names), never on translated
 * text, so one entry serves both cultures.
 */

/** The audit flags of an entry that runs every audit. */
const allAudits = {
  axe: true,
  keyboard: true,
  reflow: true,
  spacing: true,
  german: true,
  headings: true,
} as const;

/**
 * Closes an open dialog with Escape and waits until it is gone; does nothing when no dialog is open.
 *
 * The desktop keyboard walk already closes a modal, but the phone projects skip the walk, so a state that opens the
 * next overlay must not depend on it.
 *
 * @param page The page under audit.
 */
async function closeOpenDialog(page: Page): Promise<void> {
  if ((await page.locator("dialog[open]").count()) === 0) return;
  await page.keyboard.press("Escape");
  await page.locator("dialog[open]").waitFor({ state: "detached" });
}

/**
 * Triples the timeout of the running test.
 *
 * The keyboard walk settles scrolling after every key press and the Files tab holds many tab stops, so its states need
 * more than the default 30 seconds. Playwright is imported here, not at the top, because the Vitest completeness guard
 * loads this module and only the browser run may touch the Playwright runner.
 */
async function allowLongWalk(): Promise<void> {
  const { test } = await import("@playwright/test");
  test.slow();
}

/**
 * Resolves once a Files listing with rows rendered: the folder heading and at least one table row, with the longer
 * timeout the Files keyboard walk needs.
 *
 * @param page The page under audit.
 */
async function filesListed(page: Page): Promise<void> {
  await allowLongWalk();
  await page.locator(".next-hosting-file-folder-heading").first().waitFor();
  await page.locator(".next-hosting-file-table tbody tr").first().waitFor();
}

/**
 * Opens the upload dialog from the toolbar and queues three files: `server.cfg` collides with a persistent file,
 * `launch.cfg` collides with a generated target and `notes.txt` is new, so both approval checkboxes show.
 *
 * @param page The page under audit; the persistent folder `profiles` must be open.
 */
async function openUploadQueue(page: Page): Promise<void> {
  await closeOpenDialog(page);
  await page
    .getByRole("toolbar")
    .getByRole("button", { name: /^(Upload|Hochladen)$/ })
    .click();
  const dialog = page.locator("dialog[open]");
  await dialog.waitFor();
  await dialog
    .locator("input[type=file]")
    .first()
    .setInputFiles([
      { name: "server.cfg", mimeType: "text/plain", buffer: Buffer.from("hostname") },
      { name: "launch.cfg", mimeType: "text/plain", buffer: Buffer.from("generated") },
      { name: "notes.txt", mimeType: "text/plain", buffer: Buffer.from("notes") },
    ]);
  await dialog.locator("li.file-upload-queue__row").nth(2).waitFor();
  await dialog.locator(".status-badge--warning").nth(1).waitFor();
}

/**
 * Opens the new folder dialog from the toolbar.
 *
 * @param page The page under audit; the persistent folder `profiles` must be open.
 */
async function openNewFolderDialog(page: Page): Promise<void> {
  await closeOpenDialog(page);
  await page
    .getByRole("toolbar")
    .getByRole("button", { name: /^(Create folder|Ordner erstellen)$/ })
    .click();
  await page.locator("dialog[open]").waitFor();
}

/** The claims of the viewer of the profile entries: server access and the right to create and share profiles. */
const profileClaims: readonly string[] = ["sv", "adm:hosting"];

/**
 * Resolves once the cost alert editor rendered with its amount rows.
 *
 * @param page The page under audit.
 */
async function costAlertsReady(page: Page): Promise<void> {
  await page.locator(".cost-alert-settings__row input").nth(1).waitFor();
}

/**
 * Resolves once the finance owner list rendered all three accounts.
 *
 * @param page The page under audit.
 */
async function ownerListReady(page: Page): Promise<void> {
  await page.locator(".finance-accounts-table tbody tr").nth(2).waitFor();
}

/**
 * Resolves once the finance account page rendered the cost alert panel and the ledger.
 *
 * @param page The page under audit.
 */
async function accountReady(page: Page): Promise<void> {
  await page.locator(".finance-cost-alerts .cost-alert-settings__row").first().waitFor();
  await page.locator(".finance-protocol__table tbody tr").first().waitFor();
}

/** The Files entry route: the persistent folder `profiles`. */
const filesRoute = `/command/servers/${filesServerId}?tab=files&path=profiles`;

/** The registry entries of the adjacent slice. */
const adjacentEntries: readonly ScreenEntry[] = [
  {
    id: "S-08",
    kind: "screen",
    route: filesRoute,
    routePattern: "/command/servers/{ServerId:guid}",
    tab: "files",
    baseline: "K-08",
    setup: (page, culture) =>
      installApp(page, { culture, stream: true, handlers: filesHandlers() }),
    states: [
      { name: "card with rows", ready: filesListed },
      {
        name: "preservation list",
        ready: async (page) => {
          await page.locator(".next-hosting-selection-list article").nth(1).waitFor();
        },
      },
      {
        name: "upload dialog with a queue and collisions",
        open: openUploadQueue,
        ready: filesListed,
      },
      {
        name: "new folder dialog",
        open: openNewFolderDialog,
        ready: filesListed,
      },
      {
        name: "empty folder",
        open: async (page) => {
          await closeOpenDialog(page);
          await page.locator("a.next-hosting-file-name", { hasText: "child" }).click();
        },
        ready: async (page) => {
          await page.locator(".file-browser .state-panel--empty").waitFor();
        },
      },
      {
        name: "live",
        open: async (page) => {
          await closeOpenDialog(page);
          await page.getByRole("radio", { name: /^Live/ }).click();
        },
        ready: async (page) => {
          await page.getByRole("radio", { name: /^Live/, checked: true }).waitFor();
          await page.locator(".next-hosting-file-table tbody tr").nth(1).waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "S-09",
    kind: "screen",
    route: `/command/servers/${filesServerId}?tab=machine`,
    routePattern: "/command/servers/{ServerId:guid}",
    tab: "machine",
    baseline: "K-09",
    setup: (page, culture) =>
      installApp(page, { culture, stream: true, handlers: filesHandlers() }),
    states: [
      {
        name: "machine facts",
        ready: async (page) => {
          await page.locator(".next-hosting-facts").first().waitFor();
          await page.locator(".next-hosting-resource-grid article").first().waitFor();
        },
      },
      {
        name: "runtime settings",
        ready: async (page) => {
          await page
            .locator(".next-hosting-runtime-stack form, .next-hosting-runtime-stack select")
            .first()
            .waitFor();
        },
      },
      {
        name: "diagnostics",
        open: async (page) => {
          await page.locator(".next-hosting-diagnostics .disclosure__summary").click();
        },
        ready: async (page) => {
          await page.locator(".next-hosting-diagnostics code").first().waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "O-06",
    kind: "overlay",
    route: filesRoute,
    setup: (page, culture) =>
      installApp(page, { culture, stream: true, handlers: filesHandlers() }),
    states: [
      {
        name: "upload with collision approval",
        open: openUploadQueue,
        ready: filesListed,
      },
      {
        name: "new folder",
        open: async (page) => {
          await closeOpenDialog(page);
          await page
            .getByRole("toolbar")
            .getByRole("button", { name: /^(Create folder|Ordner erstellen)$/ })
            .click();
          await page.locator("dialog[open]").waitFor();
        },
        ready: async (page) => {
          await page.locator("dialog[open] input").first().waitFor();
        },
      },
      {
        name: "delete",
        open: async (page) => {
          await closeOpenDialog(page);
          await page.locator(".next-hosting-file-table .button--danger").first().click();
          await page.locator("dialog[open]").waitFor();
        },
        ready: async (page) => {
          await page.locator("dialog[open] [data-dialog-dismiss]").waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "S-10",
    kind: "screen",
    route: "/command/servers/cost-alerts",
    routePattern: "/command/servers/cost-alerts",
    baseline: "K-10",
    setup: (page, culture) =>
      installApp(page, {
        culture,
        stream: true,
        claims: financeClaims,
        handlers: costAlertHandlers(),
      }),
    states: [
      { name: "pristine", ready: costAlertsReady },
      {
        name: "dirty with one invalid amount",
        open: async (page) => {
          const amount = page.locator(".cost-alert-settings__row input").nth(1);
          await amount.fill("abc");
          await amount.blur();
        },
        ready: async (page) => {
          await page.locator(".cost-alert-settings__row input[aria-invalid=true]").waitFor();
        },
      },
      {
        name: "invalid",
        open: async (page) => {
          await page.locator(".cost-alert-settings__actions .button--gold").click();
        },
        ready: async (page) => {
          await page.locator(".cost-alert-settings__status [role=alert]").waitFor();
        },
      },
      {
        name: "conflict",
        open: async (page) => {
          await page.locator(".cost-alert-settings__row input").nth(1).fill("7");
          await page.locator(".cost-alert-settings__actions .button--gold").click();
        },
        ready: async (page) => {
          await page.locator(".cost-alert-settings__status .button").waitFor();
        },
      },
      {
        name: "read-only",
        open: async (page) => {
          // Dropping the draft first keeps the leave guard from holding the navigation.
          await page.locator(".cost-alert-settings__actions .button--ghost").click();
          await page.goto(`command/servers/cost-alerts?owner=${financeOwnerIds.readOnly}`);
        },
        ready: async (page) => {
          await page.locator(".cost-alert-settings__row input:disabled").first().waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "S-11",
    kind: "screen",
    route: "/command/configuration-profiles",
    routePattern: "/command/configuration-profiles",
    alsoCovers: ["/command/configuration-profiles/create"],
    setup: (page, culture) =>
      installApp(page, {
        culture,
        stream: true,
        claims: profileClaims,
        handlers: profileHandlers(),
      }),
    states: [
      {
        name: "library",
        ready: async (page) => {
          await page.locator(".next-hosting-profile-card").nth(2).waitFor();
        },
      },
      {
        name: "filtered empty",
        open: async (page) => {
          await page.locator(".next-hosting-profile-filters input").first().fill("zzzz");
        },
        ready: async (page) => {
          await page.waitForFunction(
            () => document.querySelectorAll(".next-hosting-profile-card").length === 0,
          );
          await page.locator(".next-hosting-profile-catalog .state-panel--empty").waitFor();
        },
      },
      {
        name: "create",
        open: async (page) => {
          await page.locator(".page-header a.button--gold").first().click();
        },
        ready: async (page) => {
          await page.locator(".next-hosting-profile-create-page input").first().waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "S-12",
    kind: "screen",
    route: `/command/configuration-profiles/${profileIds.active}`,
    routePattern: "/command/configuration-profiles/{ProfileId:guid}",
    setup: (page, culture) =>
      installApp(page, {
        culture,
        stream: true,
        claims: profileClaims,
        handlers: profileHandlers(),
      }),
    states: [
      {
        name: "edit with unsaved bar",
        open: async (page) => {
          await page
            .locator(".next-hosting-profile-configuration input[type=text]")
            .first()
            .fill("UTF-9");
        },
        ready: async (page) => {
          await page.locator(".next-hosting-profile-savebar").waitFor();
        },
      },
      {
        name: "revisions",
        open: async (page) => {
          // The unsaved draft would make the router ask first, so it is discarded before the tab changes.
          await page.locator(".next-hosting-profile-savebar .button--ghost").click();
          await page.locator(".section-navigation__link[href*='tab=revisions']").click();
        },
        ready: async (page) => {
          await page.locator(".next-hosting-revision-timeline li").nth(2).waitFor();
        },
      },
      {
        name: "access matrix",
        open: async (page) => {
          await page.locator(".section-navigation__link[href*='tab=access']").click();
        },
        ready: async (page) => {
          await page.locator(".next-hosting-access-matrix tbody tr").nth(1).waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "S-13",
    kind: "screen",
    route: "/command/hosting-finance",
    routePattern: "/command/hosting-finance",
    alsoCovers: [
      "/command/hosting-finance/accounts/{CostOwnerId:guid}",
      "/command/hosting-finance/settings",
    ],
    setup: (page, culture) =>
      installApp(page, {
        culture,
        stream: true,
        claims: financeClaims,
        handlers: financeHandlers(),
      }),
    states: [
      { name: "owner list", ready: ownerListReady },
      {
        name: "filtered by account type",
        open: async (page) => {
          await page.locator(".finance-filters select").first().selectOption("Verein");
        },
        ready: async (page) => {
          await page.waitForFunction(
            () => document.querySelectorAll(".finance-accounts-table tbody tr").length === 1,
          );
        },
      },
      {
        name: "account with ledger and cost alerts",
        open: async (page) => {
          await page.goto(`command/hosting-finance/accounts/${financeOwnerIds.personal}`);
        },
        ready: accountReady,
      },
      {
        name: "policy settings",
        open: async (page) => {
          await page.goto("command/hosting-finance/settings");
        },
        ready: async (page) => {
          await page.locator(".finance-policy__form input").first().waitFor();
          await page.locator(".finance-protocol__table tbody tr").first().waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "O-07",
    kind: "overlay",
    route: `/command/hosting-finance/accounts/${financeOwnerIds.personal}`,
    setup: (page, culture) =>
      installApp(page, {
        culture,
        stream: true,
        claims: financeClaims,
        handlers: financeHandlers(),
      }),
    states: [
      {
        name: "record payment",
        open: async (page) => {
          await closeOpenDialog(page);
          await page.locator(".finance-actions .button--primary").first().click();
          await page.locator("dialog[open]").waitFor();
        },
        ready: async (page) => {
          await page.locator("dialog[open] input[name=amount]").waitFor();
        },
      },
      {
        name: "freeze account",
        open: async (page) => {
          await closeOpenDialog(page);
          await page.locator(".finance-actions .button--danger").first().click();
          await page.locator("dialog[open]").waitFor();
        },
        ready: async (page) => {
          await page.locator("dialog[open] [data-dialog-dismiss]").waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "M-01",
    kind: "smoke",
    route: "/command/members",
    baseline: "K-15",
    setup: (page, culture) =>
      installApp(page, {
        culture,
        stream: true,
        claims: adminClaims,
        handlers: adminHandlers(),
      }),
    states: [
      {
        name: "list",
        ready: async (page) => {
          await page.locator(".members-admin-table tbody tr").nth(2).waitFor();
        },
      },
      {
        name: "state panel",
        open: async (page) => {
          // The fourth filter, formal membership, matches nobody in the fixture roster.
          await page.locator(".filter-chip-group .filter-chip").nth(3).click();
        },
        ready: async (page) => {
          await page.locator(".members-admin-roster .state-panel--empty").waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "M-02",
    kind: "smoke",
    route: `/command/roles/${smokeRoleId}`,
    setup: (page, culture) =>
      installApp(page, {
        culture,
        stream: true,
        claims: adminClaims,
        handlers: adminHandlers(),
      }),
    states: [
      {
        name: "detail",
        ready: async (page) => {
          await page.locator(".role-detail-hero").waitFor();
          await page.locator(".role-members-panel .command-table tbody tr").first().waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "M-03",
    kind: "smoke",
    route: `/command/content/pages/${smokePageSlug}/edit`,
    setup: (page, culture) =>
      installApp(page, {
        culture,
        stream: true,
        claims: adminClaims,
        handlers: adminHandlers(),
      }),
    states: [
      {
        name: "editor",
        ready: async (page) => {
          await page.locator("#public-page-editor-form input:not([disabled])").first().waitFor();
        },
      },
      {
        name: "leave dialog",
        open: async (page) => {
          await page.locator("#public-page-editor-form input:not([disabled])").first().fill("Neu");
          await page.locator(".page-header__actions a").first().click();
        },
        ready: async (page) => {
          await page.locator("dialog[open] [data-dialog-dismiss]").waitFor();
        },
      },
    ],
    audit: allAudits,
  },
  {
    id: "M-04",
    kind: "smoke",
    route: "/",
    setup: (page, culture) => installApp(page, { culture, stream: true, handlers: homeHandlers() }),
    states: [
      {
        name: "home",
        ready: async (page) => {
          await page.locator("main h1").waitFor();
          await page.locator("main a[href*='events']").first().waitFor();
        },
      },
    ],
    audit: allAudits,
  },
];

/**
 * Scrolls the page to its top at once and waits one frame.
 *
 * The audit's keyboard and sticky-bar walks scroll to the top before the first Tab. With smooth scrolling that scroll
 * animates, and a measurement that settles before the animation starts reads a focused element at the page top as
 * covered by the sticky bar or outside the viewport. A page that already sits at its top has no animation, so every
 * state leaves the page there.
 *
 * @param page The page under audit.
 */
async function scrollToTop(page: Page): Promise<void> {
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        window.scrollTo({ top: 0, left: 0, behavior: "instant" });
        requestAnimationFrame(() => resolve());
      }),
  );
}

/** The registry entries of the adjacent slice; every state leaves the page scrolled to its top. */
export const adjacentScreens: readonly ScreenEntry[] = adjacentEntries.map((entry) => ({
  ...entry,
  states: entry.states.map((state) => ({
    ...state,
    ready: async (page: Page) => {
      await state.ready(page);
      await scrollToTop(page);
    },
  })),
}));

/** Required ids the adjacent slice has not added yet; plan 69-28 empties this list. */
export const adjacentPending: readonly string[] = [];
