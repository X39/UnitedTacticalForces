import { expect, type Page, type Request, type Route } from "@playwright/test";
import { Buffer } from "node:buffer";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import process from "node:process";

/** One vendored font file as recorded in `tests/e2e/fonts/MANIFEST.json`. */
interface ManifestFont {
  file: string;
  family: string;
  weight: number;
  sha256: string;
}

/** What the tests need of `MANIFEST.json`. */
interface FontManifest {
  fonts: ManifestFont[];
}

/** What the font routes of one page served and refused. */
interface FontRecord {
  /** Names of the vendored files and URLs of the stylesheet that were fulfilled from disk. */
  served: string[];
  /** URLs of font requests that were aborted because they are not part of the vendored set. */
  blocked: string[];
  /** Requests that have started and not yet finished or failed. */
  inflight: Set<Request>;
}

/** The directory of the vendored test fonts. */
const fontDirectory = join(import.meta.dirname, "..", "fonts");

/** The fake path on `fonts.gstatic.com` the generated stylesheet points at; the route serves it from disk. */
const vendoredPath = "/vendored/";

/** Resource types of requests that stay open on purpose and therefore never count as pending. */
const longLivedResourceTypes = new Set(["eventsource", "websocket"]);

/** The fonts and their records per page, filled by {@link stabilize}. */
const records = new WeakMap<Page, FontRecord>();

/** File contents of the vendored fonts, read once per worker. */
const fontBytes = new Map<string, Buffer>();

/** The fixed instant every visual test runs at: 2026-11-20T12:00:00Z. */
export const fixedNow: Date = new Date("2026-11-20T12:00:00Z");

/**
 * Tells whether pixel comparisons are enabled for this run.
 *
 * Baselines are generated and compared in one pinned Linux container (`scripts/Update-VisualBaselines.ps1`), which
 * sets `UTF_VISUAL_BASELINES=1`. Everywhere else a spec skips its `toHaveScreenshot` assertions and keeps the layout
 * metrics, which do not depend on the platform.
 *
 * @returns True when `UTF_VISUAL_BASELINES` is `1`.
 */
export function visualBaselinesEnabled(): boolean {
  return process.env.UTF_VISUAL_BASELINES === "1";
}

/** Reads and parses the manifest of the vendored fonts. */
function readManifest(): FontManifest {
  return JSON.parse(readFileSync(join(fontDirectory, "MANIFEST.json"), "utf8")) as FontManifest;
}

/** Builds the stylesheet the Google Fonts API would return, with every face pointing at the vendored path. */
function vendoredStylesheet(manifest: FontManifest): string {
  return manifest.fonts
    .map(
      (font) =>
        `@font-face {\n` +
        `  font-family: "${font.family}";\n` +
        `  font-style: normal;\n` +
        `  font-weight: ${font.weight};\n` +
        `  font-display: swap;\n` +
        `  src: url(https://fonts.gstatic.com${vendoredPath}${font.file}) format("woff2");\n` +
        `}\n`,
    )
    .join("");
}

/** Tells whether a request is a web font request: the two Google Fonts hosts or a font file by extension. */
function isFontRequest(url: URL): boolean {
  return (
    url.hostname === "fonts.googleapis.com" ||
    url.hostname === "fonts.gstatic.com" ||
    /\.(woff2?|ttf|otf|eot)$/i.test(url.pathname)
  );
}

/** Reads one vendored file, caching the bytes. */
function readFont(file: string): Buffer {
  let bytes = fontBytes.get(file);
  if (bytes === undefined) {
    bytes = readFileSync(join(fontDirectory, file));
    fontBytes.set(file, bytes);
  }
  return bytes;
}

/**
 * Makes a page render deterministically. Call it once per page, before `page.goto` or `page.setContent`.
 *
 * - Answers the Google Fonts stylesheet with `@font-face` rules for the vendored Roboto and Barlow Condensed files,
 *   serves those files from `tests/e2e/fonts/` and aborts every other font request, recording it for
 *   {@link assertVendoredFonts}. Production keeps loading the real fonts from Google Fonts; the vendored files are
 *   test assets only.
 * - Fixes `Date.now()` and `new Date()` at {@link fixedNow}; timers keep running.
 * - Emulates the dark colour scheme and reduced motion.
 * - Selects the culture the app reads from `localStorage` (`UtfCulture`).
 * - Hides every element marked `data-visual-mask`, and the toast region when {@link visualBaselinesEnabled}.
 *
 * The browser locale and the time zone are context options, not page options. A spec sets them with
 * `test.use({ locale: culture, timezoneId: "Europe/Berlin" })`, so they are in force before the page exists.
 *
 * @param page The page to stabilize.
 * @param culture The culture the app should start in.
 */
export async function stabilize(page: Page, culture: "en" | "de"): Promise<void> {
  const manifest = readManifest();
  const stylesheet = vendoredStylesheet(manifest);
  const record: FontRecord = { served: [], blocked: [], inflight: new Set() };
  records.set(page, record);

  page.on("request", (request) => record.inflight.add(request));
  page.on("requestfinished", (request) => record.inflight.delete(request));
  page.on("requestfailed", (request) => record.inflight.delete(request));

  await page.route(
    (url) => isFontRequest(url),
    async (route: Route) => {
      const url = new URL(route.request().url());
      const cors = { "access-control-allow-origin": "*" };
      if (url.hostname === "fonts.googleapis.com") {
        record.served.push(url.href);
        await route.fulfill({
          status: 200,
          contentType: "text/css",
          headers: cors,
          body: stylesheet,
        });
        return;
      }
      if (url.hostname === "fonts.gstatic.com" && url.pathname.startsWith(vendoredPath)) {
        const file = decodeURIComponent(url.pathname.slice(vendoredPath.length));
        if (manifest.fonts.some((font) => font.file === file)) {
          record.served.push(file);
          await route.fulfill({
            status: 200,
            contentType: "font/woff2",
            headers: cors,
            body: readFont(file),
          });
          return;
        }
      }
      record.blocked.push(url.href);
      await route.abort();
    },
  );

  await page.emulateMedia({ colorScheme: "dark", reducedMotion: "reduce" });
  await page.clock.setFixedTime(fixedNow);
  await page.addInitScript(
    ({ value, maskToasts }) => {
      try {
        localStorage.setItem("UtfCulture", value);
      } catch {
        // about:blank documents of setContent pages have no storage; the culture only matters for the app.
      }
      const selectors = maskToasts
        ? "[data-visual-mask], .app-toast-viewport"
        : "[data-visual-mask]";
      document.addEventListener("DOMContentLoaded", () => {
        const style = document.createElement("style");
        style.textContent = `${selectors} { visibility: hidden !important; }`;
        document.documentElement.appendChild(style);
      });
    },
    { value: culture, maskToasts: visualBaselinesEnabled() },
  );
}

/**
 * Waits until the page has stopped changing: the web fonts it asked for are loaded, no request is pending (event
 * streams and web sockets excepted), and no spinner or busy region is left.
 *
 * @param page A page prepared with {@link stabilize}.
 */
export async function settle(page: Page): Promise<void> {
  const record = records.get(page);
  if (record === undefined)
    throw new Error("settle(page) needs stabilize(page, culture) to run first.");
  await expect
    .poll(
      () =>
        [...record.inflight].filter((r) => !longLivedResourceTypes.has(r.resourceType())).length,
      { message: "requests still pending" },
    )
    .toBe(0);
  await expect(page.locator(".fui-Spinner, [aria-busy='true']")).toHaveCount(0);
  await page.evaluate(async () => {
    await document.fonts.ready;
  });
}

/**
 * Asserts that every web font the page loaded is one of the vendored faces and that no other font request was made.
 *
 * It fails when a request reached a font host outside the vendored set (it was aborted and recorded), when no face is
 * loaded at all, or when a loaded face belongs to a family that is not in `MANIFEST.json`.
 *
 * @param page A page prepared with {@link stabilize}.
 */
export async function assertVendoredFonts(page: Page): Promise<void> {
  const record = records.get(page);
  if (record === undefined)
    throw new Error("assertVendoredFonts(page) needs stabilize(page, culture) first.");
  const families = new Set(readManifest().fonts.map((font) => font.family));
  const loaded = await page.evaluate(async () => {
    await document.fonts.ready;
    return [...document.fonts]
      .filter((face) => face.status === "loaded")
      .map((face) => face.family.replace(/^["']|["']$/g, ""));
  });
  expect(record.blocked, "font requests outside the vendored set").toEqual([]);
  expect(loaded.length, "loaded font faces").toBeGreaterThan(0);
  expect(
    loaded.filter((family) => !families.has(family)),
    "loaded faces outside the vendored families",
  ).toEqual([]);
}
