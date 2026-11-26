import { expect, test } from "@playwright/test";
import { installWorkspace, workspaceRoute } from "../support/screens/workspaceMocks";

/**
 * Target size of the shared buttons in a real browser (Phase 70, D-16 refresh target, WCAG 2.2 target size).
 *
 * jsdom resolves neither media queries nor `var()`, so the rendered box is measured here: the workspace details
 * Refresh button on the Overview tab is at least 44 CSS pixels high at or below 800 CSS pixels (both phone projects
 * are narrower than 800, the chromium project is 1280 wide).
 */

/** The touch height of every control at or below 800 CSS pixels, in CSS pixels. */
const touchTarget = 44;

/** The subpixel tolerance the Phase 62 target-size specs allow. */
const subpixelTolerance = 0.5;

test("the workspace details Refresh button meets the touch target height", async ({
  page,
}, testInfo) => {
  test.skip(testInfo.project.name === "chromium", "touch height applies at or below 800px");
  const session = await installWorkspace(page, "en");

  await page.goto(workspaceRoute().replace(/^\//, ""));
  const refresh = page.locator(".workspace-details__refresh .button");
  await expect(refresh).toBeVisible();

  const box = await refresh.boundingBox();
  expect(box, "refresh button has a layout box").not.toBeNull();
  expect(box!.height, "refresh button height").toBeGreaterThanOrEqual(
    touchTarget - subpixelTolerance,
  );
  expect(session.unexpected).toEqual([]);
});
