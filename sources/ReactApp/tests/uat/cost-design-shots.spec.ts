import { expect, test } from "@playwright/test";
import { resolve } from "node:path";
import {
  banner,
  bannerRegion,
  designShot,
  designShotsDirectory,
  evidence,
  openProfileMenu,
  openServerById,
  readHandOver,
  uatTitles,
  useCulture,
} from "./support/uat";

/** The widths the human design review (66-4) looks at. */
const viewports = [
  { name: "desktop", width: 1280, height: 900 },
  { name: "390", width: 390, height: 844 },
] as const;

/** The workspace heading of the cost alerts page per culture. */
const alertsPageTitle = { en: "Cost alerts", de: "Kostenwarnungen" } as const;

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`66-4 design screenshots (${culture}, ${viewport.name})`, () => {
      test.use({ viewport: { width: viewport.width, height: viewport.height } });

      test(`banner, menu rows and cost alerts page (${culture}, ${viewport.name}) @fresh`, async ({
        page,
      }) => {
        const { costingServerId } = readHandOver();
        await useCulture(page, culture);
        const prefix = `${culture}-${viewport.name}`;

        await openServerById(page, costingServerId, uatTitles.costing);
        await expect(banner(page).locator("li").first()).toBeVisible();
        await designShot(page, `${prefix}-workspace`);
        await bannerRegion(page).screenshot({
          path: resolve(designShotsDirectory, `${prefix}-banner.png`),
        });

        const menu = await openProfileMenu(page, culture);
        await expect(menu.locator("[data-cost-alert-id]").first()).toBeVisible();
        await designShot(page, `${prefix}-menu-open`);
        await page.locator("#account-profile-panel").screenshot({
          path: resolve(designShotsDirectory, `${prefix}-menu-rows.png`),
        });
        await page.keyboard.press("Escape");

        await page.goto("command/servers/cost-alerts");
        await expect(
          page.getByRole("heading", { level: 1, name: alertsPageTitle[culture] }),
        ).toBeVisible();
        await expect(page.getByLabel(/\(EUR\)$/).first()).toBeVisible();
        await designShot(page, `${prefix}-cost-alerts-page`);
        evidence(`screenshots ${prefix}-* written to ${designShotsDirectory}`);
      });
    });
  }
}
