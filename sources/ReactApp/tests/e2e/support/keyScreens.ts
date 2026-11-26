import { expect, type Locator, type Page } from "@playwright/test";
import type { Buffer } from "node:buffer";
import type { Culture, MockSession } from "./mockApi";
import type { ScreenEntry, ScreenState } from "./screens/registryTypes";
import { serverScreens } from "./serverScreens";

/**
 * The nineteen key screens that carry a pixel baseline (VER-03, D-16), resolved from the baseline ids of the screen
 * registry.
 *
 * `key-screens.spec.ts` compares them against the committed images and `visual-stability.spec.ts` proves that their
 * capture is deterministic, so both specs share this module and cannot drift apart on what a key screen is.
 */

/** How a key screen is captured. */
export type KeyCapture =
  /** The visible viewport of the phone. */
  | { kind: "viewport" }
  /**
   * The smallest page rectangle that holds every match of the locators: one element, or several that have no wrapper
   * element around them. It is cut out of a full-page capture taken at the top of the page.
   */
  | { kind: "element"; locators: (page: Page) => Locator[] };

/** One key screen: the registry entry and state it is captured from and the way it is captured. */
export interface KeyScreen {
  /** The baseline id, `K-01` to `K-19`; it is the first part of the image name. */
  id: string;
  /** The registry entry that sets the screen up. */
  entry: ScreenEntry;
  /** The index of the state of {@link entry} that is captured; the states before it are visited first. */
  stateIndex: number;
  /** The way the screen is captured. */
  capture: KeyCapture;
}

/** The number of key screens the UI-SPEC lists. */
export const keyScreenCount = 19;

/**
 * Key screens whose captured state is not the first state of their entry, keyed by baseline id, as the name of the
 * state. The registry carries the baseline id of the entry (its first state) or of one state; these three UI-SPEC
 * screens are a later state of an entry whose own `baseline` names it. The Phase 70 key screens "K-16" (S-14, the
 * collapsed Settings panels with a staged badge) and "K-17" (O-03, the leave dialog with three outcomes) are named by
 * the `baseline` of their registry state, so they need no entry here. So do the Phase 71 key screens "K-18" (S-15,
 * the Access panel with one row open) and "K-19" (O-10, the Transfer cost owner dialog).
 */
const stateNameOf: Readonly<Record<string, string>> = {
  "K-05": "expanded failed entry",
  "K-10": "dirty with one invalid amount",
  "K-15": "state panel",
};

/** The key screens that are captured as an element instead of the viewport. */
const captureOf: Readonly<Record<string, KeyCapture>> = {
  // The automation panel of the Settings tab, the section the deep link `#server-automation` targets.
  "K-07": { kind: "element", locators: (page) => [page.locator("#server-automation")] },
  // The Access panel of the Settings tab with one subject row open (K-18). The row opens below the fold of a phone, so
  // the whole panel is captured as an element.
  "K-18": { kind: "element", locators: (page) => [page.locator("#server-access")] },
  // The automatic shutdown notice above the cost alert banner. Both panels render straight into the workspace shell
  // with no wrapper element, so the capture is their union.
  "K-14": {
    kind: "element",
    locators: (page) => [
      page.locator(".state-panel").filter({ has: page.locator(".auto-stop-notice") }),
      page.locator(".state-panel").filter({ has: page.locator(".cost-alert-banner") }),
    ],
  },
};

/**
 * Resolves the key screens from the registry: an entry's `baseline` names the screen of its first state (or of the
 * state listed in {@link stateNameOf}), a state's own `baseline` names that state alone.
 *
 * @returns The key screens in the order of their ids.
 * @throws Error when an id is claimed twice, a named state does not exist or the count is not {@link keyScreenCount}.
 */
function resolveKeyScreens(): readonly KeyScreen[] {
  const found = new Map<string, KeyScreen>();
  const add = (id: string, entry: ScreenEntry, stateIndex: number): void => {
    if (found.has(id))
      throw new Error(`Baseline id ${id} is claimed by more than one registry state.`);
    found.set(id, { id, entry, stateIndex, capture: captureOf[id] ?? { kind: "viewport" } });
  };
  for (const entry of serverScreens) {
    if (entry.baseline !== undefined) {
      const name = stateNameOf[entry.baseline];
      const index = name === undefined ? 0 : entry.states.findIndex((state) => state.name === name);
      if (index < 0)
        throw new Error(`${entry.id} has no state named "${name}" for ${entry.baseline}.`);
      add(entry.baseline, entry, index);
    }
    entry.states.forEach((state, index) => {
      if (state.baseline !== undefined) add(state.baseline, entry, index);
    });
  }
  if (found.size !== keyScreenCount) {
    throw new Error(`The registry carries ${found.size} key screens, expected ${keyScreenCount}.`);
  }
  return [...found.values()].sort((left, right) => left.id.localeCompare(right.id));
}

/** The nineteen key screens, `K-01` to `K-19`. */
export const keyScreens: readonly KeyScreen[] = resolveKeyScreens();

/**
 * Visits the states of a key screen's entry in order, up to and including the captured one, on the page that is
 * already loaded. The states before the captured one run because a later state builds on the earlier ones.
 *
 * @param page The page, already at the route of the entry.
 * @param key The key screen.
 */
export async function reachKeyState(page: Page, key: KeyScreen): Promise<void> {
  const states: readonly ScreenState[] = key.entry.states.slice(0, key.stateIndex + 1);
  for (const state of states) {
    await state.open?.(page);
    await state.ready(page);
  }
}

/**
 * Installs the mock of a key screen's entry, opens its route and reaches the captured state.
 *
 * @param page A page prepared with `stabilize`.
 * @param key The key screen.
 * @param culture The culture the page renders in.
 * @returns The mock record; its `unexpected` list must stay empty.
 */
export async function openKeyScreen(
  page: Page,
  key: KeyScreen,
  culture: Culture,
): Promise<MockSession> {
  const session = await key.entry.setup(page, culture);
  await page.goto(key.entry.route.replace(/^\//, ""));
  await reachKeyState(page, key);
  return session;
}

/**
 * The elements whose content follows the clock or the accrued cost; the Playwright `mask` option paints over them in
 * the committed images so a counter ticking does not decide a comparison. A selector that matches nothing masks
 * nothing.
 *
 * @param page The page being captured.
 * @returns The locators to mask.
 */
export function liveMasks(page: Page): Locator[] {
  return [
    page.locator('[data-slot="accrued-cost"] .accrued-cost__value'),
    page.locator(".live-badge__updated time"),
    page.locator(".auto-stop-notice__relative"),
  ];
}

/**
 * Computes the page rectangle of an element capture: the union of the boxes of every match of the locators.
 *
 * The page is scrolled to its top first. An element taller than the phone viewport is captured beyond the viewport,
 * and whatever is fixed or sticky to the viewport (the skip link parked above it, the sticky header) is then drawn
 * wherever the current scroll offset puts it, in the middle of the element. At the top of the page those overlays sit
 * at their natural place, outside the rectangle.
 *
 * @param page The page being captured.
 * @param locators The locators whose matches form the rectangle.
 * @returns The rectangle in page coordinates.
 * @throws Error when no locator matches an element.
 */
async function elementClip(
  page: Page,
  locators: readonly Locator[],
): Promise<{ x: number; y: number; width: number; height: number }> {
  await page.evaluate(() => window.scrollTo(0, 0));
  const boxes: { left: number; top: number; right: number; bottom: number }[] = [];
  for (const locator of locators) {
    boxes.push(
      ...(await locator.evaluateAll((elements) =>
        elements.map((element) => {
          const rect = element.getBoundingClientRect();
          return {
            left: rect.left + window.scrollX,
            top: rect.top + window.scrollY,
            right: rect.right + window.scrollX,
            bottom: rect.bottom + window.scrollY,
          };
        }),
      )),
    );
  }
  if (boxes.length === 0) throw new Error("The key screen element capture matches no element.");
  const left = Math.min(...boxes.map((box) => box.left));
  const top = Math.min(...boxes.map((box) => box.top));
  const right = Math.max(...boxes.map((box) => box.right));
  const bottom = Math.max(...boxes.map((box) => box.bottom));
  return { x: left, y: top, width: right - left, height: bottom - top };
}

/**
 * Forces the page to rasterize from scratch, so a capture does not depend on the paint history of the page load.
 *
 * Chromium repaints only the invalidated part of a tile when a web font swaps in or data arrives. The edge pixels of a
 * rounded, semi-transparent element such as the profile avatar blend one level differently depending on whether the
 * tile was painted once or repainted, and which of the two happens varies from one page load to the next (measured
 * on K-01 and K-04: about one load in three). Taking the document out of rendering and back drops every painted
 * tile, so the next frame paints all of them fresh. Scroll offsets and the focused element are put back, so the page
 * looks the way the state left it.
 *
 * @param page The page about to be captured.
 */
export async function repaintFresh(page: Page): Promise<void> {
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        const scrolled: { element: Element; left: number; top: number }[] = [];
        for (const element of document.querySelectorAll("*")) {
          if (element.scrollTop > 0 || element.scrollLeft > 0) {
            scrolled.push({ element, left: element.scrollLeft, top: element.scrollTop });
          }
        }
        const windowLeft = window.scrollX;
        const windowTop = window.scrollY;
        const active = document.activeElement;
        document.body.style.display = "none";
        void document.body.offsetHeight;
        requestAnimationFrame(() => {
          document.body.style.display = "";
          window.scrollTo(windowLeft, windowTop);
          for (const { element, left, top } of scrolled) {
            element.scrollLeft = left;
            element.scrollTop = top;
          }
          if (active instanceof HTMLElement && active !== document.body) {
            active.focus({ preventScroll: true });
          }
          requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
        });
      }),
  );
}

/** The screenshot options every key screen capture shares. */
const captureOptions = { animations: "disabled", caret: "hide", scale: "css" } as const;

/**
 * Takes the raw PNG of a key screen the way the baseline comparison frames it, without masks.
 *
 * @param page A page showing the key screen.
 * @param key The key screen.
 * @returns The PNG bytes.
 */
export async function captureKeyScreen(page: Page, key: KeyScreen): Promise<Buffer> {
  await repaintFresh(page);
  const { capture } = key;
  switch (capture.kind) {
    case "viewport":
      return page.screenshot(captureOptions);
    case "element":
      return page.screenshot({
        ...captureOptions,
        fullPage: true,
        clip: await elementClip(page, capture.locators(page)),
      });
  }
}

/**
 * Compares a key screen with its committed baseline `{id}-{culture}.png`: the viewport, or the element for K-07 and
 * K-14, with the live values masked.
 *
 * @param page A page showing the key screen.
 * @param key The key screen.
 * @param culture The culture the page renders in; it is the second part of the image name.
 */
export async function expectKeyScreenshot(
  page: Page,
  key: KeyScreen,
  culture: Culture,
): Promise<void> {
  const name = `${key.id}-${culture}.png`;
  await repaintFresh(page);
  const { capture } = key;
  switch (capture.kind) {
    case "viewport":
      await expect(page).toHaveScreenshot(name, { mask: liveMasks(page) });
      return;
    case "element":
      await expect(page).toHaveScreenshot(name, {
        mask: liveMasks(page),
        fullPage: true,
        clip: await elementClip(page, capture.locators(page)),
      });
      return;
  }
}
