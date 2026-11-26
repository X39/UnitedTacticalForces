import { expect, test, type Page } from "@playwright/test";
import process from "node:process";
import {
  assertVendoredFonts,
  fixedNow,
  settle,
  stabilize,
  visualBaselinesEnabled,
} from "../support/visual";

/**
 * Self-test of the visual determinism helpers. It needs neither the app nor the API: the page is built with
 * `setContent`, and its stylesheet link is the one `index.html` carries, so the test proves that `stabilize` answers
 * the Google Fonts request with the vendored faces and with nothing else.
 */
test.use({ locale: "en", timezoneId: "Europe/Berlin" });

/** The stylesheet URL `index.html` loads in production. */
const googleFontsUrl =
  "https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@500;600;700;800&family=Roboto:wght@300;400;500;700;900&display=swap";

/** Roboto weights and Barlow Condensed weights `index.html` asks for. */
const robotoWeights = [300, 400, 500, 700, 900];
const barlowWeights = [500, 600, 700, 800];

/** Builds a page that uses every requested weight so that every face is loaded. */
function fontPageHtml(extraHead = "", extraBody = ""): string {
  const lines = [
    ...robotoWeights.map(
      (weight) =>
        `<p style="font-family: Roboto, sans-serif; font-weight: ${weight}">Roboto ${weight} Größe Straße Äpfel Öl Übung</p>`,
    ),
    ...barlowWeights.map(
      (weight) =>
        `<p style="font-family: 'Barlow Condensed', sans-serif; font-weight: ${weight}">Barlow ${weight} Größe Straße</p>`,
    ),
  ];
  return `<!doctype html><html><head><link rel="stylesheet" href="${googleFontsUrl}">${extraHead}</head><body>${lines.join("")}${extraBody}</body></html>`;
}

/** Reads the families and weights of the faces that finished loading. */
async function loadedFaces(page: Page): Promise<string[]> {
  return page.evaluate(async () => {
    await document.fonts.ready;
    return [...document.fonts]
      .filter((face) => face.status === "loaded")
      .map((face) => `${face.family.replace(/^["']|["']$/g, "")} ${face.weight}`)
      .sort();
  });
}

test.describe("visual determinism", () => {
  test("renders only the vendored Roboto and Barlow Condensed faces", async ({ page }) => {
    await stabilize(page, "en");
    await page.setContent(fontPageHtml());
    await settle(page);

    await assertVendoredFonts(page);
    expect(await loadedFaces(page)).toEqual([
      ...barlowWeights.map((weight) => `Barlow Condensed ${weight}`),
      ...robotoWeights.map((weight) => `Roboto ${weight}`),
    ]);
  });

  test("fails the font assertion when a page asks for a font outside the vendored set", async ({
    page,
  }) => {
    await stabilize(page, "en");
    const rogue =
      "<style>@font-face { font-family: 'Rogue'; src: url(https://fonts.gstatic.com/s/rogue/v1/rogue.woff2) format('woff2'); }</style>";
    await page.setContent(
      fontPageHtml(rogue, '<p style="font-family: Rogue, sans-serif">rogue text</p>'),
    );
    await settle(page);

    await expect(assertVendoredFonts(page)).rejects.toThrow();
  });

  test("fixes the clock and the time zone while timers keep running", async ({ page }) => {
    await stabilize(page, "de");
    await page.setContent(fontPageHtml());

    const state = await page.evaluate(async () => {
      const before = Date.now();
      await new Promise((resolve) => setTimeout(resolve, 60));
      return {
        before,
        after: Date.now(),
        iso: new Date().toISOString(),
        zone: Intl.DateTimeFormat().resolvedOptions().timeZone,
        scheme: matchMedia("(prefers-color-scheme: dark)").matches,
        reduced: matchMedia("(prefers-reduced-motion: reduce)").matches,
      };
    });

    expect(state.before).toBe(fixedNow.getTime());
    expect(state.after).toBe(fixedNow.getTime());
    expect(state.iso).toBe("2026-11-20T12:00:00.000Z");
    expect(state.zone).toBe("Europe/Berlin");
    expect(state.scheme).toBe(true);
    expect(state.reduced).toBe(true);
  });

  test("renders two consecutive screenshots byte for byte identical", async ({ page }) => {
    await stabilize(page, "en");
    await page.setContent(fontPageHtml());
    await settle(page);

    const options = { animations: "disabled", caret: "hide", scale: "css" } as const;
    const first = await page.screenshot(options);
    const second = await page.screenshot(options);

    expect(second.equals(first)).toBe(true);
  });

  test("keeps pixel comparisons opt-in", () => {
    expect(visualBaselinesEnabled()).toBe(process.env.UTF_VISUAL_BASELINES === "1");
  });
});
