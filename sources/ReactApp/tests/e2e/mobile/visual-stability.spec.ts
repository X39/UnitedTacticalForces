import { expect, test } from "@playwright/test";
import {
  captureKeyScreen,
  keyScreens,
  openKeyScreen,
  reachKeyState,
  type KeyScreen,
} from "../support/keyScreens";
import type { Culture } from "../support/mockApi";
import { assertVendoredFonts, settle, stabilize } from "../support/visual";

/**
 * Proves, without any committed image, that the capture of the key screens is deterministic (VER-03, D-17).
 *
 * K-01 (fleet), K-04 (workspace Overview) and K-11 (cost confirmation dialog) are the three screens with the most
 * moving parts: accrued cost, relative times, cards in several states and a modal. Each is stabilized, reached,
 * settled and captured twice in a row, then the page is reloaded, the state reached again and captured a third time.
 * All three captures must be byte for byte identical, in English and in German. The test runs everywhere, because it
 * compares two renderings of the same machine with each other and needs no baseline; it runs in `mobile-390` only,
 * where the capture frames are the phone width the baselines use.
 */

/** The key screens the stability test covers. */
const probedIds = ["K-01", "K-04", "K-11"] as const;

/** The key screens, in the order of {@link probedIds}. */
const probed: readonly KeyScreen[] = probedIds.map((id) => {
  const key = keyScreens.find((candidate) => candidate.id === id);
  if (key === undefined) throw new Error(`The registry carries no key screen ${id}.`);
  return key;
});

const cultures: readonly Culture[] = ["en", "de"];

for (const culture of cultures) {
  test.describe(`capture stability (${culture})`, () => {
    test.use({ locale: culture, timezoneId: "Europe/Berlin" });

    for (const key of probed) {
      test(`${key.id} (${culture}) captures identically before and after a reload`, async ({
        page,
      }, testInfo) => {
        test.skip(
          testInfo.project.name !== "mobile-390",
          "the stability check runs in mobile-390 only",
        );
        await stabilize(page, culture);
        const session = await openKeyScreen(page, key, culture);
        const pageErrors: string[] = [];
        page.on("pageerror", (error) => pageErrors.push(error.message));
        await settle(page);
        await assertVendoredFonts(page);

        const first = await captureKeyScreen(page, key);
        const second = await captureKeyScreen(page, key);
        expect(second.equals(first), `${key.id} differs between two consecutive captures`).toBe(
          true,
        );

        await page.reload();
        await reachKeyState(page, key);
        await settle(page);
        await assertVendoredFonts(page);
        const reloaded = await captureKeyScreen(page, key);
        expect(reloaded.equals(first), `${key.id} differs after a reload`).toBe(true);

        expect(session.unexpected).toEqual([]);
        expect(pageErrors).toEqual([]);
      });
    }
  });
}

test.describe("key screen registry", () => {
  test("resolves the nineteen key screens K-01 to K-19", () => {
    expect(keyScreens.map((key) => key.id)).toEqual(
      Array.from({ length: 19 }, (_, index) => `K-${String(index + 1).padStart(2, "0")}`),
    );
  });
});
