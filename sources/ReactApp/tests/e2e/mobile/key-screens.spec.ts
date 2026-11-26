import { expect, test } from "@playwright/test";
import {
  expectKeyScreenshot,
  keyScreens,
  openKeyScreen,
  type KeyScreen,
} from "../support/keyScreens";
import type { Culture } from "../support/mockApi";
import { assertVendoredFonts, settle, stabilize, visualBaselinesEnabled } from "../support/visual";

/**
 * The pixel baselines of the nineteen key screens, K-01 to K-19, in `mobile-320` and `mobile-390`, in English and in
 * German: four images per key screen, named `{id}-{culture}.png` under `__screenshots__/{projectName}/` (VER-03, D-16).
 *
 * Every test carries the `@visual` tag and runs only when `UTF_VISUAL_BASELINES=1`, which `scripts/Update-VisualBaselines.ps1`
 * sets inside the one pinned container image that generates and compares the images (D-AUTO-1). Everywhere else, the
 * default suite and CI included, the tests are listed and skipped. Which registry entry and state a key screen is, and
 * how it is framed (the viewport; the `#server-automation` section for K-07; the notices region for K-14), comes from
 * `support/keyScreens.ts`, which reads the `baseline` ids of the screen registry.
 *
 * Each test stabilizes the page (vendored fonts, fixed clock, reduced motion, dark colour scheme, toasts hidden),
 * opens the state of its registry entry, waits for it to settle, and compares it with the Playwright `mask` option
 * painting over the values that follow the clock or the accrued cost. The unconditional proof that the capture is
 * deterministic is `visual-stability.spec.ts`.
 */

/** The reason a skipped test reports. */
const optInReason = "visual baselines are opt-in";

/** The cultures every key screen is captured in. */
const cultures: readonly Culture[] = ["en", "de"];

/**
 * Describes what a key screen shows, for the test title.
 *
 * @param key The key screen.
 * @returns The registry id and state name, such as `S-02 populated mixed states`.
 */
function describeKeyScreen(key: KeyScreen): string {
  const state = key.entry.states[key.stateIndex];
  return `${key.entry.id} ${state?.name ?? ""}`.trim();
}

for (const culture of cultures) {
  test.describe(`key screens (${culture})`, () => {
    test.use({ locale: culture, timezoneId: "Europe/Berlin" });

    for (const key of keyScreens) {
      test(`${key.id} ${describeKeyScreen(key)} (${culture}) @visual`, async ({ page }) => {
        test.skip(!visualBaselinesEnabled(), optInReason);
        await stabilize(page, culture);
        const session = await openKeyScreen(page, key, culture);
        await settle(page);
        await assertVendoredFonts(page);

        await expectKeyScreenshot(page, key, culture);

        expect(session.unexpected).toEqual([]);
      });
    }
  });
}
