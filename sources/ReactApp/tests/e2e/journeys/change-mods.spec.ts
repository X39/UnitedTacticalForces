import { expect, test, type Locator, type Page } from "@playwright/test";
import {
  installJourney,
  journeyIds,
  journeyServer,
  journeyServerTitle,
} from "../support/journeyMocks";

/** The visible copy this journey reads, copied from the English locale resources. */
const copy = {
  settings: "Settings",
  files: "Files",
  navigation: "Server sections",
  modPack: "Mod pack",
  barLabel: "Pending changes",
  bar: "1 change staged. Nothing is saved until you apply it.",
  review: "Review changes",
  apply: "Apply 1 change",
  added: "Added (1)",
  removed: "Removed (1)",
  effectOff: `These changes take effect the next time ${journeyServerTitle} starts.`,
  toast: `1 change applied. It takes effect the next time ${journeyServerTitle} starts.`,
  banner: `Saved changes apply the next time ${journeyServerTitle} starts.`,
  waiting: /Waiting: Mod pack/,
  leaveTitle: "Leave without applying?",
  stay: "Stay here",
  discard: "Discard and leave",
  keepEditing: "Keep editing",
} as const;

/** The mod pack chooser of the Mods panel. */
function modPackSelect(page: Page): Locator {
  return page.getByRole("combobox", { name: copy.modPack, exact: true });
}

/** The sticky pending-changes bar. */
function pendingBar(page: Page): Locator {
  return page.getByRole("region", { name: copy.barLabel });
}

/** One workspace section link. */
function sectionLink(page: Page, name: string): Locator {
  return page
    .getByRole("navigation", { name: copy.navigation })
    .getByRole("link", { name, exact: true });
}

/**
 * Moves focus to a section link and activates it with the keyboard.
 *
 * @param page The page with the workspace open.
 * @param name The accessible name of the section link.
 */
async function openSectionByKeyboard(page: Page, name: string): Promise<void> {
  await sectionLink(page, name).focus();
  await page.keyboard.press("Enter");
}

/**
 * J-2 (VER-02): in the workspace Settings a member picks another mod pack, sees the staged count, reviews the diff,
 * applies and sees the pending-start banner; with another staged change the leave guard on a section switch offers
 * stay, review and discard, each reachable and operable by keyboard alone. The run covers the desktop project and
 * the 390 phone width.
 */
test.describe("J-2 change mods", () => {
  test("stages a pack, reviews the diff, applies it and guards the staged leave by keyboard", async ({
    page,
  }) => {
    const journey = await installJourney(page, {
      culture: "en",
      servers: [journeyServer({ id: journeyIds.off, title: journeyServerTitle, modPackId: 1 })],
    });
    await page.goto(`command/servers/${journeyIds.off}?tab=settings`);
    await expect(page.getByRole("heading", { level: 1, name: journeyServerTitle })).toBeVisible();
    await expect(modPackSelect(page)).toBeVisible();
    await expect(modPackSelect(page)).toHaveValue("1");
    await expect(page.getByRole("status").filter({ hasText: copy.banner })).toHaveCount(0);

    // Picking another pack stages one change; nothing is sent yet.
    await modPackSelect(page).selectOption({ value: "2" });
    const bar = pendingBar(page);
    await expect(bar.getByText(copy.bar, { exact: true })).toBeVisible();
    expect(journey.requests).toEqual([]);

    // The review lists what the change adds and removes.
    await bar.getByRole("button", { name: copy.review }).click();
    const dialog = page.getByRole("dialog", { name: copy.review });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText(copy.effectOff, { exact: true })).toBeVisible();
    await dialog.getByRole("button", { name: "Show 2 mod changes" }).click();
    await expect(dialog.getByText(copy.added, { exact: true })).toBeVisible();
    await expect(dialog.getByText("Mod C", { exact: true })).toBeVisible();
    await expect(dialog.getByText(copy.removed, { exact: true })).toBeVisible();
    await expect(dialog.getByText("Mod A", { exact: true })).toBeVisible();

    // Applying saves the pack, shows the toast and the pending-start banner.
    await dialog.getByRole("button", { name: copy.apply }).click();
    await expect(page.locator(".app-toast").filter({ hasText: copy.toast })).toHaveCount(1);
    await expect(dialog).toBeHidden();
    await expect(bar).toHaveCount(0);
    expect(journey.requests).toEqual([
      {
        method: "PUT",
        path: `/api/game-servers/${journeyIds.off}/mod-pack`,
        body: { modPackDefinitionId: 2 },
      },
    ]);
    const banner = page.getByRole("status").filter({ hasText: copy.banner });
    await expect(banner).toBeVisible();
    await expect(banner).toContainText(copy.waiting);
    await expect(modPackSelect(page)).toHaveValue("2");
    expect(journey.state.pendingChanges).toBe(1);

    // A new staged change makes a section switch ask first. Stay keeps the place, by keyboard.
    await modPackSelect(page).selectOption({ value: "1" });
    await expect(bar.getByText(copy.bar, { exact: true })).toBeVisible();
    await openSectionByKeyboard(page, copy.files);
    const leave = page.getByRole("dialog", { name: copy.leaveTitle });
    await expect(leave).toBeVisible();
    await expect(leave.getByRole("button")).toHaveText([copy.discard, copy.stay, copy.review]);
    await expect(leave.getByRole("button", { name: copy.stay, exact: true })).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(leave).toBeHidden();
    await expect(page).toHaveURL(/[?&]tab=settings(&|$)/);
    await expect(bar).toBeVisible();

    // Review changes is one Tab stop after Stay here and opens the review without leaving.
    await openSectionByKeyboard(page, copy.files);
    await expect(leave).toBeVisible();
    await page.keyboard.press("Tab");
    await expect(leave.getByRole("button", { name: copy.review, exact: true })).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(leave).toBeHidden();
    const review = page.getByRole("dialog", { name: copy.review });
    await expect(review).toBeVisible();
    await expect(page).toHaveURL(/[?&]tab=settings(&|$)/);
    await review.getByRole("button", { name: copy.keepEditing }).focus();
    await page.keyboard.press("Enter");
    await expect(review).toBeHidden();
    await expect(bar).toBeVisible();

    // Discard and leave sits before Stay here (Shift+Tab) and reaches the Files section with the draft gone.
    await openSectionByKeyboard(page, copy.files);
    await expect(leave).toBeVisible();
    await page.keyboard.press("Shift+Tab");
    await expect(leave.getByRole("button", { name: copy.discard, exact: true })).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(/[?&]tab=files(&|$)/);
    await expect(leave).toBeHidden();
    await expect(bar).toHaveCount(0);

    // Back on Settings the saved pack is the applied one; the discarded choice left no trace.
    await openSectionByKeyboard(page, copy.settings);
    await expect(modPackSelect(page)).toHaveValue("2");
    expect(journey.requests).toHaveLength(1);
    expect(journey.session.unexpected).toEqual([]);
  });
});
