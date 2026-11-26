import { expect, test } from "@playwright/test";
import {
  checkDialogGeometry,
  checkHeadings,
  checkReducedMotion,
  checkReflow,
  checkStickyCoverage,
  checkTextSpacing,
  checkZoomReflow,
  scanAxe,
  walkKeyboard,
} from "../support/a11y";
import type { Culture } from "../support/mockApi";
import { sliceOf } from "../support/screens/registryTypes";
import { serverScreens } from "../support/serverScreens";

/**
 * The registry-driven accessibility and layout audit (FND-06, D-12).
 *
 * Every registry entry runs once per culture and project on one page load and visits each of its states. The
 * desktop project runs axe, the heading check, the zoom reflow check, reduced motion and the keyboard walk; the phone
 * projects run axe, reflow, text spacing and sticky-bar coverage. Overlays also assert the dialog geometry. The test
 * title ends in the owning slice (`@fleet`, `@workspace` or `@adjacent`), so a slice plan can run its own entries
 * with `--grep @fleet`.
 */

const cultures: readonly Culture[] = ["en", "de"];

for (const entry of serverScreens) {
  const slice = sliceOf(entry.id);
  if (slice === undefined) {
    throw new Error(
      `Registry entry ${entry.id} is not in the required manifest (requiredRegistry).`,
    );
  }
  for (const culture of cultures) {
    test(`${entry.id} (${culture}) @${slice}`, async ({ page }, testInfo) => {
      const mobile = testInfo.project.name.startsWith("mobile-");
      const session = await entry.setup(page, culture);
      const pageErrors: string[] = [];
      page.on("pageerror", (error) => pageErrors.push(error.message));

      await page.goto(entry.route.replace(/^\//, ""));
      for (const state of entry.states) {
        await test.step(`${entry.id} ${state.name}`, async () => {
          await state.open?.(page);
          await state.ready(page);
          const { audit } = entry;
          if (audit.axe) await scanAxe(page, { knownFindings: state.knownFindings });
          if (entry.kind === "overlay") {
            const dialog = page.locator("dialog[open]");
            if ((await dialog.count()) > 0) await checkDialogGeometry(page, dialog.first());
          }
          if (mobile) {
            if (audit.reflow) await checkReflow(page);
            if (audit.spacing) await checkTextSpacing(page);
            if (audit.keyboard) await checkStickyCoverage(page);
            return;
          }
          if (audit.headings) await checkHeadings(page);
          if (audit.reflow) await checkZoomReflow(page);
          await checkReducedMotion(page);
          // The keyboard walk closes an open modal with Escape, so it runs last in a state.
          if (audit.keyboard) {
            await walkKeyboard(page, {
              knownFindings: state.knownFindings,
              closeWithEscape: state.dialogStaysOpen === true ? false : undefined,
            });
          }
        });
      }

      expect(session.unexpected).toEqual([]);
      expect(pageErrors).toEqual([]);
    });
  }
}
