import AxeBuilder from "@axe-core/playwright";
import { test, type Locator, type Page } from "@playwright/test";
import type { KnownFinding } from "./screens/registryTypes";

/**
 * Accessibility and layout audit helpers of Phase 69 (FND-06, VER-02, VER-03).
 *
 * Every `check*` and `scanAxe` helper throws an error that names the offending selectors, so a failing audit shows
 * what to fix. Every `collect*` helper returns the same findings without throwing, which the opt-in discovery pass
 * uses to record the state of a page before the gating audits run.
 */

/** One problem an audit found: which audit, which element and what is wrong. */
export interface AuditFinding {
  /** The audit that produced the finding, for example `axe` or `reflow`. */
  check: string;
  /** A readable selector of the offending element, or a description of the page-level problem. */
  target: string;
  /** What is wrong, with the measured values. */
  detail: string;
  /** The id of the violated rule, used to match a {@link KnownFinding}; set by the keyboard walk. */
  rule?: string | undefined;
  /** The tag, id and first two classes of the offending element, used to match a {@link KnownFinding}. */
  selector?: string | undefined;
}

/**
 * Throws an error that lists every finding of an audit; returns when there is none.
 *
 * @param check The name of the audit, used as the error title.
 * @param findings The findings to report.
 * @throws Error when the list is not empty.
 */
export function assertNoFindings(check: string, findings: readonly AuditFinding[]): void {
  if (findings.length === 0) return;
  const lines = findings.map((finding) => `  - ${finding.target}: ${finding.detail}`);
  throw new Error(`${check}: ${findings.length} finding(s)\n${lines.join("\n")}`);
}

/**
 * Adds a test annotation when called inside a running test; does nothing otherwise.
 *
 * @param type The annotation type.
 * @param description The annotation text.
 */
function annotate(type: string, description: string): void {
  try {
    test.info().annotations.push({ type, description });
  } catch {
    // Called outside a test, for example from the discovery collector; there is nothing to annotate.
  }
}

/** The axe tag set of FND-06; `wcag22aa` adds the 24px `target-size` rule. */
export const axeTags: readonly string[] = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

/** One axe violation reduced to what a report and a known finding need. */
export interface AxeViolationFinding {
  /** The axe rule id, for example `color-contrast`. */
  rule: string;
  /** The target selector of the violating node, with shadow and frame hops joined by a space. */
  target: string;
  /** The axe impact level of the rule. */
  impact: string;
  /** The axe help text of the rule. */
  help: string;
}

/** The result of one axe run. */
export interface AxeCollection {
  /** Every violating node. */
  violations: AxeViolationFinding[];
  /** The number of rules that passed, which proves that rules ran at all. */
  passes: number;
}

/**
 * Runs axe at WCAG 2.2 AA without asserting anything about the result.
 *
 * @param page The page to scan.
 * @param include An optional selector that limits the scan; an include that matches no element throws, because a
 * scan of nothing would pass vacuously.
 * @returns The violating nodes and the number of passed rules.
 * @throws Error when `include` matches no element.
 */
export async function collectAxe(page: Page, include?: string | undefined): Promise<AxeCollection> {
  let builder = new AxeBuilder({ page }).withTags([...axeTags]);
  if (include !== undefined) {
    if ((await page.locator(include).count()) === 0) {
      throw new Error(`axe include selector matched no element: ${include}`);
    }
    builder = builder.include(include);
  }
  const results = await builder.analyze();
  return {
    passes: results.passes.length,
    violations: results.violations.flatMap((violation) =>
      violation.nodes.map((node) => ({
        rule: violation.id,
        target: node.target.join(" "),
        impact: violation.impact ?? "unknown",
        help: violation.help,
      })),
    ),
  };
}

/** Options of {@link scanAxe}. */
export interface AxeScanOptions {
  /** A selector that limits the scan to one container; the whole document when omitted. */
  include?: string | undefined;
  /** Recorded findings the scan subtracts; each match by rule and target adds a test annotation. */
  knownFindings?: readonly KnownFinding[] | undefined;
}

/**
 * Runs axe at WCAG 2.2 AA and fails on every violation that is not a recorded known finding.
 *
 * The scan is non-vacuous: it fails when no rule passed or when the include selector matched nothing. Only a
 * violation whose rule and target both equal a known finding is subtracted, and every subtraction is annotated on the
 * test so a recorded finding never disappears silently.
 *
 * @param page The page to scan.
 * @param options The container and the known findings.
 * @throws Error listing every remaining violation with its rule and target.
 */
export async function scanAxe(page: Page, options: AxeScanOptions = {}): Promise<void> {
  const collected = await collectAxe(page, options.include);
  if (collected.passes === 0) {
    throw new Error("axe evaluated no rule that passed; the scan would be vacuous");
  }
  const known = options.knownFindings ?? [];
  const remaining: AuditFinding[] = [];
  for (const violation of collected.violations) {
    const match = known.find(
      (finding) => finding.rule === violation.rule && finding.target === violation.target,
    );
    if (match) {
      annotate("known-finding", `${match.id}: ${match.rule} at ${match.target}`);
      continue;
    }
    remaining.push({
      check: "axe",
      target: violation.target,
      detail: `${violation.rule} (${violation.impact}): ${violation.help}`,
    });
  }
  assertNoFindings("axe", remaining);
}

/** One tab stop the in-page scan expects, or one element focus reached. */
interface StopIdentity {
  /** A structural path from the document root; two elements never share one. */
  path: string;
  /** A readable name: tag, id, classes and the accessible text. */
  label: string;
  /** The tag, id and first two classes without text, which stays equal in every culture. */
  selector: string;
}

/** The measurement of one focus ring. */
interface RingMeasurement {
  /** Whether the ring is visible, at least 2px, gold-bright and at least 3:1 against its surface. */
  ok: boolean;
  /** Why the ring failed, or a short description of the passing ring. */
  reason: string;
}

/** What the in-page scan learned about the element that currently has focus. */
interface FocusedStop extends StopIdentity {
  /** Whether focus is on the document itself, that is on nothing. */
  isBody: boolean;
  /** Whether the element is inside the scope under walk. */
  inScope: boolean;
  /** Whether the element is the skip link. */
  skipLink: boolean;
  /** Whether the element is not rendered or is invisible. */
  hidden: boolean;
  /** Whether the element sits inside an inert subtree. */
  inert: boolean;
  /** Whether the element sits inside an `aria-hidden="true"` subtree. */
  ariaHidden: boolean;
  /** Whether another element covers the element at its center, or it lies outside the viewport. */
  covered: boolean;
  /** What covers the element, with its position when it is fixed or sticky. */
  coveredBy: string;
  /** Whether the element has a positive `tabindex`. */
  positiveTabindex: boolean;
  /**
   * Whether the element is a date or time input. The browser gives such a control one tab stop per native segment
   * (hour, minute, day period) and draws the focus of a later segment itself, so a walk counts the segments of one
   * control as one stop.
   */
  segmented: boolean;
  /** The focus ring measurement. */
  ring: RingMeasurement;
}

/** A fixed or sticky bar the in-page scan found in the viewport. */
interface StickyBar extends StopIdentity {
  /** The computed `position` value, `fixed` or `sticky`. */
  position: string;
  /** The height of the bar in CSS pixels. */
  height: number;
  /** The viewport height in CSS pixels. */
  viewportHeight: number;
}

/** The operations of the in-page inspector. */
type InspectArg =
  | { op: "tabbables"; scope: string }
  | { op: "focused"; scope: string }
  | { op: "bars" }
  | { op: "positive-tabindex" };

/**
 * Runs inside the page. It is a single function because Playwright serializes one function per evaluation, so the
 * helpers it needs live inside it.
 *
 * - `tabbables` lists the elements of a scope that Tab must reach, in DOM order.
 * - `focused` describes the element that currently has focus.
 * - `bars` lists the fixed and sticky bars that intersect the viewport.
 * - `positive-tabindex` lists every element with a `tabindex` above zero.
 *
 * @param arg The operation and its arguments.
 * @returns The result of the operation, typed by the caller.
 */
function inspectPage(arg: InspectArg): unknown {
  /** The gold-bright focus ring color of the design tokens (`--gold-bright`), as RGB. */
  const ringColor = [222, 201, 130];
  type Rgba = [number, number, number, number];

  const pathOf = (element: Element): string => {
    const parts: string[] = [];
    let node: Element | null = element;
    while (node && node !== document.documentElement) {
      const parent: Element | null = node.parentElement;
      const index = parent ? Array.prototype.indexOf.call(parent.children, node) + 1 : 1;
      parts.unshift(`${node.tagName.toLowerCase()}:nth-child(${index})`);
      node = parent;
    }
    return parts.join(" > ");
  };

  const selectorOf = (element: Element): string => {
    const id = element.id ? `#${element.id}` : "";
    const classes = (element.getAttribute("class") ?? "")
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((name) => `.${name}`)
      .join("");
    return `${element.tagName.toLowerCase()}${id}${classes}`;
  };

  const labelOf = (element: Element): string => {
    const text = (element.getAttribute("aria-label") ?? element.textContent ?? "")
      .replace(/\s+/g, " ")
      .trim()
      .slice(0, 40);
    return `${selectorOf(element)}${text ? ` "${text}"` : ""}`;
  };

  const identityOf = (element: Element): StopIdentity => ({
    path: pathOf(element),
    label: labelOf(element),
    selector: selectorOf(element),
  });

  const rendered = (element: Element): boolean => {
    if (typeof element.checkVisibility === "function") {
      return element.checkVisibility({ checkVisibilityCSS: true });
    }
    return element.getClientRects().length > 0;
  };

  const parseColor = (value: string): Rgba | null => {
    const match =
      /^rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)(?:\s*[,/]\s*([\d.]+%?))?\s*\)$/.exec(
        value.trim(),
      );
    if (!match) return null;
    const rawAlpha = match[4];
    const alpha =
      rawAlpha === undefined
        ? 1
        : rawAlpha.endsWith("%")
          ? parseFloat(rawAlpha) / 100
          : parseFloat(rawAlpha);
    return [Number(match[1]), Number(match[2]), Number(match[3]), alpha];
  };

  /** Composites the nearest translucent layers over the nearest opaque background, or white when none exists. */
  const backgroundOf = (start: Element | null): [number, number, number] => {
    const layers: Rgba[] = [];
    for (let node = start; node; node = node.parentElement) {
      const color = parseColor(getComputedStyle(node).backgroundColor);
      if (color && color[3] > 0) {
        layers.push(color);
        if (color[3] >= 1) break;
      }
    }
    let result: [number, number, number] = [255, 255, 255];
    for (let index = layers.length - 1; index >= 0; index -= 1) {
      const layer = layers[index] as Rgba;
      result = [
        layer[0] * layer[3] + result[0] * (1 - layer[3]),
        layer[1] * layer[3] + result[1] * (1 - layer[3]),
        layer[2] * layer[3] + result[2] * (1 - layer[3]),
      ];
    }
    return result;
  };

  const luminance = (rgb: readonly number[]): number => {
    const channel = (value: number): number => {
      const scaled = value / 255;
      return scaled <= 0.03928 ? scaled / 12.92 : Math.pow((scaled + 0.055) / 1.055, 2.4);
    };
    return (
      0.2126 * channel(rgb[0] ?? 0) + 0.7152 * channel(rgb[1] ?? 0) + 0.0722 * channel(rgb[2] ?? 0)
    );
  };

  const contrast = (a: readonly number[], b: readonly number[]): number => {
    const first = luminance(a);
    const second = luminance(b);
    return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
  };

  const isRingColor = (color: Rgba): boolean =>
    color[3] >= 1 &&
    Math.abs(color[0] - (ringColor[0] ?? 0)) <= 2 &&
    Math.abs(color[1] - (ringColor[1] ?? 0)) <= 2 &&
    Math.abs(color[2] - (ringColor[2] ?? 0)) <= 2;

  /** Splits a computed `box-shadow` list on the commas that are outside parentheses. */
  const splitShadows = (value: string): string[] => {
    const layers: string[] = [];
    let depth = 0;
    let current = "";
    for (const character of value) {
      if (character === "(") depth += 1;
      if (character === ")") depth -= 1;
      if (character === "," && depth === 0) {
        layers.push(current.trim());
        current = "";
      } else {
        current += character;
      }
    }
    if (current.trim()) layers.push(current.trim());
    return layers;
  };

  const measureRing = (element: Element, allowAncestor: boolean): RingMeasurement => {
    const style = getComputedStyle(element);
    const failures: string[] = [];
    const candidates: { source: string; width: number; color: string; inside: boolean }[] = [];
    const outlineWidth = parseFloat(style.outlineWidth) || 0;
    if (style.outlineStyle !== "none" && style.outlineStyle !== "hidden" && outlineWidth > 0) {
      candidates.push({
        source: "outline",
        width: outlineWidth,
        color: style.outlineColor,
        inside: (parseFloat(style.outlineOffset) || 0) < 0,
      });
    }
    if (style.boxShadow && style.boxShadow !== "none") {
      for (const layer of splitShadows(style.boxShadow)) {
        const colorText = /rgba?\([^)]*\)/.exec(layer)?.[0];
        if (!colorText) continue;
        const lengths = [...layer.replace(colorText, "").matchAll(/(-?[\d.]+)px/g)].map((entry) =>
          parseFloat(entry[1] ?? "0"),
        );
        const [x = 0, y = 0, blur = 0, spread = 0] = lengths;
        if (layer.includes("inset") || x !== 0 || y !== 0 || blur !== 0 || spread <= 0) continue;
        candidates.push({ source: "box-shadow", width: spread, color: colorText, inside: false });
      }
    }
    for (const candidate of candidates) {
      const color = parseColor(candidate.color);
      if (!color) {
        failures.push(`${candidate.source} color ${candidate.color} is not a plain rgb color`);
        continue;
      }
      if (candidate.width < 2) {
        failures.push(`${candidate.source} is ${candidate.width}px, below 2px`);
        continue;
      }
      if (!isRingColor(color)) {
        failures.push(`${candidate.source} color ${candidate.color} is not gold-bright`);
        continue;
      }
      const surface = backgroundOf(candidate.inside ? element : element.parentElement);
      const ratio = contrast(color, surface);
      if (ratio < 3) {
        failures.push(`${candidate.source} contrast ${ratio.toFixed(2)}:1 is below 3:1`);
        continue;
      }
      return {
        ok: true,
        reason: `${candidate.source} ${candidate.width}px at ${ratio.toFixed(2)}:1`,
      };
    }
    if (allowAncestor) {
      let ancestor = element.parentElement;
      for (let depth = 0; ancestor && depth < 3; depth += 1, ancestor = ancestor.parentElement) {
        if (!ancestor.matches(":focus-within")) continue;
        const outer = measureRing(ancestor, false);
        if (outer.ok) return { ok: true, reason: `ancestor ring: ${outer.reason}` };
      }
    }
    return { ok: false, reason: failures.length ? failures.join("; ") : "no outline or ring" };
  };

  const tabbableSelector =
    'a[href], area[href], button, input:not([type="hidden"]), select, textarea, summary, iframe, [contenteditable=""], [contenteditable="true"], [tabindex]';

  const tabbable = (element: Element): boolean => {
    const tabindex = element.getAttribute("tabindex");
    if (tabindex !== null && Number(tabindex) < 0) return false;
    if (element.matches(":disabled")) return false;
    if (element.closest("[inert]")) return false;
    if (!rendered(element)) return false;
    if (element instanceof HTMLElement && element.tagName === "SUMMARY") {
      const details = element.parentElement;
      if (!(details instanceof HTMLDetailsElement)) return false;
      if (details.querySelector(":scope > summary") !== element) return false;
    }
    if (element instanceof HTMLInputElement && element.type === "radio" && element.name) {
      const group = Array.from(document.querySelectorAll<HTMLInputElement>('input[type="radio"]'))
        .filter((radio) => radio.name === element.name && radio.form === element.form)
        .filter((radio) => !radio.disabled && rendered(radio));
      const checked = group.find((radio) => radio.checked);
      return (checked ?? group[0]) === element;
    }
    return true;
  };

  if (arg.op === "positive-tabindex") {
    return Array.from(document.querySelectorAll("[tabindex]"))
      .filter((element) => Number(element.getAttribute("tabindex")) > 0)
      .map(identityOf);
  }

  if (arg.op === "bars") {
    const bars: StickyBar[] = [];
    for (const element of document.body.querySelectorAll("*")) {
      const style = getComputedStyle(element);
      if (style.position !== "fixed" && style.position !== "sticky") continue;
      if (style.pointerEvents === "none" || !rendered(element)) continue;
      if (element.closest("dialog, .skip-link")) continue;
      const rect = element.getBoundingClientRect();
      const intersects =
        rect.bottom > 0 &&
        rect.top < window.innerHeight &&
        rect.right > 0 &&
        rect.left < window.innerWidth;
      if (!intersects || rect.width < window.innerWidth * 0.5) continue;
      bars.push({
        ...identityOf(element),
        position: style.position,
        height: rect.height,
        viewportHeight: window.innerHeight,
      });
    }
    return bars;
  }

  const scope = document.querySelector(arg.scope);

  if (arg.op === "tabbables") {
    if (!scope) return null;
    return Array.from(scope.querySelectorAll(tabbableSelector)).filter(tabbable).map(identityOf);
  }

  const active = document.activeElement;
  const empty: FocusedStop = {
    path: "",
    label: "document",
    selector: "document",
    isBody: true,
    inScope: false,
    skipLink: false,
    hidden: false,
    inert: false,
    ariaHidden: false,
    covered: false,
    coveredBy: "",
    positiveTabindex: false,
    segmented: false,
    ring: { ok: true, reason: "" },
  };
  if (!active || active === document.body || active === document.documentElement) return empty;

  const rect = active.getBoundingClientRect();
  const left = Math.max(rect.left, 0);
  const right = Math.min(rect.right, window.innerWidth);
  const top = Math.max(rect.top, 0);
  const bottom = Math.min(rect.bottom, window.innerHeight);
  let covered = false;
  let coveredBy = "";
  if (right <= left || bottom <= top) {
    covered = true;
    coveredBy = "nothing, the element lies outside the viewport";
  } else {
    const hit = document.elementFromPoint((left + right) / 2, (top + bottom) / 2);
    const labelled =
      active instanceof HTMLInputElement && hit
        ? Array.from(active.labels ?? []).some((label) => label.contains(hit))
        : false;
    if (hit && !(active.contains(hit) || hit.contains(active) || labelled)) {
      covered = true;
      let positioned: Element | null = hit;
      let position = "";
      while (positioned && positioned !== document.body) {
        const value = getComputedStyle(positioned).position;
        if (value === "fixed" || value === "sticky") {
          position = ` (${value})`;
          break;
        }
        positioned = positioned.parentElement;
      }
      coveredBy = `${labelOf(hit)}${position}`;
    }
  }
  return {
    ...identityOf(active),
    isBody: false,
    inScope: scope ? scope.contains(active) : false,
    skipLink: active.matches("a.skip-link"),
    hidden: !rendered(active),
    inert: active.closest("[inert]") !== null,
    ariaHidden: active.closest('[aria-hidden="true"]') !== null,
    covered,
    coveredBy,
    positiveTabindex: Number(active.getAttribute("tabindex")) > 0,
    segmented:
      active instanceof HTMLInputElement &&
      ["time", "date", "datetime-local", "month", "week"].includes(active.type),
    ring: measureRing(active, true),
  } satisfies FocusedStop;
}

/**
 * Runs the in-page inspector.
 *
 * @param page The page to inspect.
 * @param arg The operation to run.
 * @returns The typed result of the operation.
 */
async function inspect<T>(page: Page, arg: InspectArg): Promise<T> {
  return (await page.evaluate(inspectPage, arg)) as T;
}

/**
 * Waits until the page stopped scrolling.
 *
 * Focus moves scroll the focused element into view, and smooth scrolling would otherwise let a measurement see the
 * element before it arrived. The wait ends after six frames without a scroll change (a smooth scroll can start a few
 * frames late on a loaded machine) or after 90 frames.
 *
 * @param page The page to wait on.
 */
export async function settleScroll(page: Page): Promise<void> {
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        let last = `${window.scrollX},${window.scrollY}`;
        let stable = 0;
        let frames = 0;
        const tick = (): void => {
          const now = `${window.scrollX},${window.scrollY}`;
          stable = now === last ? stable + 1 : 0;
          last = now;
          frames += 1;
          if (stable >= 6 || frames > 90) resolve();
          else requestAnimationFrame(tick);
        };
        requestAnimationFrame(tick);
      }),
  );
}

/**
 * Waits until the finite animations and transitions of the page ended.
 *
 * A layout change such as a viewport resize starts transitions (a drawer sliding closed, for example), and a
 * measurement taken meanwhile would see an element mid-flight. Animations that never end, such as a spinner, are
 * ignored, and the wait is capped at 1.5 seconds.
 *
 * @param page The page to wait on.
 */
async function settleAnimations(page: Page): Promise<void> {
  await page.evaluate(async () => {
    const finite = document
      .getAnimations()
      .filter((animation) => animation.effect?.getComputedTiming().endTime !== Infinity);
    await Promise.race([
      Promise.allSettled(finite.map((animation) => animation.finished)),
      new Promise((resolve) => setTimeout(resolve, 1500)),
    ]);
  });
}

/**
 * Presses a key that moves focus and returns what the focused element looks like once scrolling settled.
 *
 * @param page The page to drive.
 * @param key The key to press, for example `Tab` or `Shift+Tab`.
 * @param scope The selector of the container under walk.
 * @returns The description of the element that has focus afterwards.
 */
async function pressAndInspect(page: Page, key: string, scope: string): Promise<FocusedStop> {
  await page.keyboard.press(key);
  await settleScroll(page);
  return inspect<FocusedStop>(page, { op: "focused", scope });
}

/**
 * Moves the sequential focus navigation start point to a container and scrolls to the top.
 *
 * The container receives focus once, through a temporary `tabindex="-1"` when it has none, and the attribute is
 * removed again. The browser keeps the container as the starting point, so the next Tab press reaches the first
 * focusable element inside it, independent of where an earlier interaction left focus.
 *
 * @param page The page to prepare.
 * @param start A selector of the container the walk starts in; the document start when null.
 */
async function resetFocus(page: Page, start: string | null): Promise<void> {
  await page.evaluate((selector) => {
    window.scrollTo(0, 0);
    const container = ((selector === null ? null : document.querySelector(selector)) ??
      document.body) as HTMLElement;
    const hadTabindex = container.hasAttribute("tabindex");
    if (!hadTabindex) container.setAttribute("tabindex", "-1");
    container.focus({ preventScroll: true });
    if (!hadTabindex) container.removeAttribute("tabindex");
  }, start);
}

/** The most Tab presses a walk makes, a bound that also covers the extra stops of date and time input segments. */
const maxWalkPresses = 400;

/** Options of {@link walkKeyboard}. */
export interface KeyboardWalkOptions {
  /** The container to walk; the open modal dialog when one exists, otherwise `main`. */
  scope?: string | undefined;
  /** Whether the walk closes an open modal dialog with Escape at the end. Defaults to true. */
  closeWithEscape?: boolean | undefined;
  /** A selector the element focused after Escape must match; omit it to only require that focus is not lost. */
  returnFocusTo?: string | undefined;
  /**
   * Recorded findings that {@link walkKeyboard} subtracts. A keyboard finding matches when the known finding's `rule`
   * is the walk rule id (for example `focus-ring` or `focus-covered`) and its `target` is the element's tag, id and
   * first two classes, which stays equal in every culture.
   */
  knownFindings?: readonly KnownFinding[] | undefined;
}

/**
 * Walks the page with the keyboard and returns every deviation from the keyboard contract (UI-SPEC FND-06 item 2).
 *
 * Outside a modal the walk starts at the document and requires the skip link as first stop that moves focus to the
 * main landmark. It then starts inside the scope and requires that Tab reaches every tabbable element of the scope in
 * DOM order and nothing else, that Shift+Tab visits them in reverse, and that every stop is rendered, not inert, not
 * `aria-hidden`, not covered, has no positive `tabindex` and shows a focus ring of at least 2px in gold-bright with
 * 3:1 contrast against its surface (the ring of an ancestor that matches `:focus-within` counts). In a modal dialog
 * the walk requires that focus stays inside the dialog, and Escape must close it and must not lose focus.
 *
 * @param page The page to walk.
 * @param options The scope and the Escape behavior.
 * @returns The findings; empty when the page keeps the contract.
 * @throws Error when the scope does not exist, which is a broken audit and not a finding.
 */
export async function collectKeyboard(
  page: Page,
  options: KeyboardWalkOptions = {},
): Promise<AuditFinding[]> {
  const findings: AuditFinding[] = [];
  const add = (rule: string, target: string, detail: string, selector?: string): void => {
    findings.push({ check: "keyboard", rule, target, detail, selector });
  };
  const addStop = (rule: string, stop: StopIdentity, detail: string): void => {
    add(rule, stop.label, detail, stop.selector);
  };
  const modal = await page.evaluate(() => document.querySelector("dialog:modal") !== null);
  const scope = options.scope ?? (modal ? "dialog:modal" : "main");

  for (const element of await inspect<StopIdentity[]>(page, { op: "positive-tabindex" })) {
    addStop("positive-tabindex", element, "has a positive tabindex; only 0 and -1 are allowed");
  }

  const expected = await inspect<StopIdentity[] | null>(page, { op: "tabbables", scope });
  if (expected === null) throw new Error(`keyboard walk scope not found: ${scope}`);
  if (expected.length === 0) {
    add("vacuous", scope, "no tabbable element in scope, so the walk would be vacuous");
    return findings;
  }

  if (!modal) {
    await resetFocus(page, null);
    const first = await pressAndInspect(page, "Tab", scope);
    if (!first.skipLink) {
      add("skip-link", "skip link", `the first tab stop is ${first.label}, not the skip link`);
    } else {
      await page.keyboard.press("Enter");
      const landed = await page.evaluate(() => {
        const href = window.location.hash;
        const target = href ? document.getElementById(decodeURIComponent(href.slice(1))) : null;
        return target !== null && document.activeElement === target;
      });
      if (!landed) {
        add("skip-link", "skip link", "activating it did not move focus to the target landmark");
      }
    }
  }

  await resetFocus(page, scope);
  const visited: FocusedStop[] = [];
  for (
    let presses = 0;
    visited.length < expected.length && presses < maxWalkPresses;
    presses += 1
  ) {
    const stop = await pressAndInspect(page, "Tab", scope);
    if (stop.isBody || !stop.inScope) {
      addStop(
        "focus-left-scope",
        stop,
        modal
          ? `focus left the dialog at stop ${visited.length + 1} of ${expected.length}`
          : `focus left ${scope} at stop ${visited.length + 1} of ${expected.length}`,
      );
      break;
    }
    // A later segment of the date or time input just visited is the same stop of the same control.
    if (stop.segmented && stop.path === visited.at(-1)?.path) continue;
    visited.push(stop);
  }

  for (const stop of visited) {
    if (stop.hidden) addStop("focus-hidden", stop, "received focus while hidden");
    if (stop.inert) addStop("focus-inert", stop, "received focus while inert");
    if (stop.ariaHidden) {
      addStop("focus-aria-hidden", stop, "received focus inside an aria-hidden subtree");
    }
    if (stop.covered) {
      addStop("focus-covered", stop, `received focus while covered by ${stop.coveredBy}`);
    }
    if (stop.positiveTabindex) addStop("positive-tabindex", stop, "has a positive tabindex");
    if (!stop.ring.ok) addStop("focus-ring", stop, `focus ring: ${stop.ring.reason}`);
  }

  const expectedPaths = expected.map((entry) => entry.path);
  const visitedPaths = visited.map((entry) => entry.path);
  for (const entry of expected) {
    if (!visitedPaths.includes(entry.path)) {
      addStop("tab-unreachable", entry, "was never reached by Tab");
    }
  }
  for (const entry of visited) {
    if (!expectedPaths.includes(entry.path)) {
      addStop("tab-unexpected", entry, "received focus but is not tabbable by the DOM scan");
    }
  }
  if (
    visited.length === expected.length &&
    visited.every((entry) => expectedPaths.includes(entry.path)) &&
    visited.some((entry, index) => entry.path !== expectedPaths[index])
  ) {
    const index = visited.findIndex((entry, position) => entry.path !== expectedPaths[position]);
    const stop = visited[index];
    add(
      "tab-order",
      stop?.label ?? scope,
      `tab order differs from DOM order at stop ${index + 1}: expected ${expected[index]?.label}`,
      stop?.selector,
    );
  }

  // The stop after the last one must not be an unexpected element of the scope; in a modal it must stay inside.
  const probe = await pressAndInspect(page, "Tab", scope);
  if (modal) {
    // A native modal hands focus to the browser interface after its last control, which reads as the document
    // having no focused element; focus on a page element outside the dialog is the escape.
    if (!probe.isBody && !probe.inScope) {
      addStop("focus-trap", probe, "focus escaped the modal dialog");
    }
  } else if (probe.inScope && !expectedPaths.includes(probe.path)) {
    addStop(
      "tab-unexpected",
      probe,
      "received focus after the last expected stop but is not tabbable by the DOM scan",
    );
  }

  // Shift+Tab visits the same stops in reverse.
  const last = visited.at(-1);
  if (last) {
    await page.evaluate(
      (path) => (document.querySelector(path) as HTMLElement | null)?.focus(),
      last.path,
    );
    for (let index = visited.length - 2; index >= 0; index -= 1) {
      let stop = await pressAndInspect(page, "Shift+Tab", scope);
      // Going back through a date or time input first passes its earlier segments.
      for (
        let segments = 0;
        stop.segmented && stop.path === visited[index + 1]?.path && segments < maxWalkPresses;
        segments += 1
      ) {
        stop = await pressAndInspect(page, "Shift+Tab", scope);
      }
      if (stop.path !== visited[index]?.path) {
        addStop(
          "shift-tab",
          stop,
          `Shift+Tab did not reverse the order: expected ${visited[index]?.label} at reverse stop ${visited.length - index}`,
        );
        break;
      }
    }
  }

  if (modal && options.closeWithEscape !== false) {
    await page.keyboard.press("Escape");
    await page
      .waitForFunction(() => document.querySelector("dialog:modal") === null, null, {
        timeout: 2000,
      })
      .catch(() => undefined);
    // An app returns focus after the dialog unmounted, typically one animation frame later and after a router
    // navigation for a deep link, so give a late focus return a moment before reading it as lost.
    await page
      .waitForFunction(
        () => document.activeElement !== null && document.activeElement !== document.body,
        null,
        { timeout: 1000 },
      )
      .catch(() => undefined);
    const after = await page.evaluate(() => ({
      open: document.querySelector("dialog:modal") !== null,
      lost: document.activeElement === null || document.activeElement === document.body,
    }));
    if (after.open) {
      add(
        "escape-close",
        "dialog",
        "Escape did not close the modal dialog; allowed only while a request is pending",
      );
    } else if (after.lost) {
      add(
        "escape-focus-lost",
        "dialog",
        "focus was lost to the document after Escape; it must return to the invoker",
      );
    } else if (
      options.returnFocusTo !== undefined &&
      !(await page.evaluate(
        (selector) => document.activeElement?.matches(selector) === true,
        options.returnFocusTo,
      ))
    ) {
      add(
        "escape-focus-return",
        "dialog",
        `focus did not return to ${options.returnFocusTo} after Escape`,
      );
    }
  }
  return findings;
}

/**
 * Walks the page with the keyboard and fails on every deviation from the keyboard contract that is not a recorded
 * known finding; each subtraction adds a test annotation.
 *
 * @param page The page to walk.
 * @param options The scope, the Escape behavior and the known findings.
 * @throws Error listing the offending elements.
 * @see collectKeyboard
 */
export async function walkKeyboard(page: Page, options: KeyboardWalkOptions = {}): Promise<void> {
  const known = options.knownFindings ?? [];
  const remaining: AuditFinding[] = [];
  const annotated = new Set<string>();
  for (const finding of await collectKeyboard(page, options)) {
    const match = known.find(
      (entry) => entry.rule === finding.rule && entry.target === finding.selector,
    );
    if (match) {
      const description = `${match.id}: ${match.rule} at ${match.target}`;
      if (!annotated.has(description)) annotate("known-finding", description);
      annotated.add(description);
      continue;
    }
    remaining.push(finding);
  }
  assertNoFindings("keyboard walk", remaining);
}

/**
 * Measures horizontal reflow (WCAG 1.4.10) without asserting.
 *
 * Reports a document wider than the viewport, a fixed element that extends outside the viewport horizontally, and the
 * outermost element that extends past the right edge without a scroll region around it. Tables and charts that scroll
 * inside their own region are not findings.
 *
 * @param page The page to measure.
 * @returns The findings.
 */
export async function collectReflow(page: Page): Promise<AuditFinding[]> {
  await settleAnimations(page);
  return page.evaluate(() => {
    const findings: { check: string; target: string; detail: string }[] = [];
    const clientWidth = document.documentElement.clientWidth;
    const scrollWidth = document.documentElement.scrollWidth;
    if (scrollWidth > clientWidth + 1) {
      findings.push({
        check: "reflow",
        target: "document",
        detail: `scrollWidth ${scrollWidth} exceeds clientWidth ${clientWidth}`,
      });
    }
    const labelOf = (element: Element): string => {
      const id = element.id ? `#${element.id}` : "";
      const classes = (element.getAttribute("class") ?? "")
        .split(/\s+/)
        .filter(Boolean)
        .slice(0, 2)
        .map((name) => `.${name}`)
        .join("");
      return `${element.tagName.toLowerCase()}${id}${classes}`;
    };
    const visible = (element: Element): boolean => {
      const style = getComputedStyle(element);
      if (style.display === "none" || style.visibility === "hidden") return false;
      const rect = element.getBoundingClientRect();
      return rect.width > 0 && rect.height > 0;
    };
    const overflowsRight = (element: Element): boolean =>
      element.getBoundingClientRect().right > clientWidth + 1;
    const clippedByScrollRegion = (element: Element): boolean => {
      for (let node = element.parentElement; node; node = node.parentElement) {
        const style = getComputedStyle(node);
        const clips = ["auto", "scroll", "hidden", "clip"].includes(style.overflowX);
        if (clips && node.getBoundingClientRect().right <= clientWidth + 1) return true;
      }
      return false;
    };
    for (const element of document.body.querySelectorAll("*")) {
      if (!visible(element)) continue;
      const rect = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      if (style.position === "fixed" && (rect.left < -1 || rect.right > clientWidth + 1)) {
        findings.push({
          check: "reflow",
          target: labelOf(element),
          detail: `fixed element spans ${Math.round(rect.left)}..${Math.round(rect.right)} in a ${clientWidth}px viewport`,
        });
        continue;
      }
      if (!overflowsRight(element) || clippedByScrollRegion(element)) continue;
      const parent = element.parentElement;
      if (parent && parent !== document.body && overflowsRight(parent)) continue;
      findings.push({
        check: "reflow",
        target: labelOf(element),
        detail: `extends to ${Math.round(rect.right)} in a ${clientWidth}px viewport without a scroll region`,
      });
    }
    return findings.slice(0, 25);
  });
}

/**
 * Asserts that the page reflows without horizontal scroll (WCAG 1.4.10).
 *
 * @param page The page to check.
 * @throws Error listing the offending elements.
 */
export async function checkReflow(page: Page): Promise<void> {
  assertNoFindings("reflow", await collectReflow(page));
}

/** The text-spacing override of WCAG 1.4.12. */
const textSpacingCss =
  "*, *::before, *::after { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; } p { margin-block-end: 2em !important; }";

/**
 * Applies the WCAG 1.4.12 text-spacing override and measures clipped text without asserting.
 *
 * Reports every button, tab, label, badge, chip and title whose own box clips (`overflow: hidden` or `clip`) and now
 * holds more text than it shows. The override is removed afterwards.
 *
 * @param page The page to measure.
 * @returns The findings.
 */
export async function collectTextSpacing(page: Page): Promise<AuditFinding[]> {
  const style = await page.addStyleTag({ content: textSpacingCss });
  try {
    return await page.evaluate(() => {
      const findings: { check: string; target: string; detail: string }[] = [];
      const selector =
        'button, [role="button"], [role="tab"], label, h1, h2, h3, h4, [class*="button"], [class*="badge"], [class*="chip"], [class*="tab"], [class*="title"], [class*="label"]';
      for (const element of document.querySelectorAll(selector)) {
        const computed = getComputedStyle(element);
        if (computed.display === "none" || computed.visibility === "hidden") continue;
        const rect = element.getBoundingClientRect();
        // A 1px box is a visually hidden label (`sr-only`); it clips on purpose and is not a visible control.
        if (rect.width <= 1 || rect.height <= 1) continue;
        const clipsX = ["hidden", "clip"].includes(computed.overflowX);
        const clipsY = ["hidden", "clip"].includes(computed.overflowY);
        const lostY = clipsY && element.scrollHeight > element.clientHeight + 1;
        const lostX = clipsX && element.scrollWidth > element.clientWidth + 1;
        if (!lostX && !lostY) continue;
        const id = element.id ? `#${element.id}` : "";
        const classes = (element.getAttribute("class") ?? "")
          .split(/\s+/)
          .filter(Boolean)
          .slice(0, 2)
          .map((name) => `.${name}`)
          .join("");
        const text = (element.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 40);
        findings.push({
          check: "text spacing",
          target: `${element.tagName.toLowerCase()}${id}${classes} "${text}"`,
          detail: `clips text: scroll ${element.scrollWidth}x${element.scrollHeight}, client ${element.clientWidth}x${element.clientHeight}`,
        });
      }
      return findings.slice(0, 25);
    });
  } finally {
    await style.evaluate((node) => (node as Element).remove());
  }
}

/**
 * Asserts that no clipping control loses text under the WCAG 1.4.12 text-spacing override.
 *
 * @param page The page to check.
 * @throws Error listing the clipped elements.
 */
export async function checkTextSpacing(page: Page): Promise<void> {
  assertNoFindings("text spacing", await collectTextSpacing(page));
}

/**
 * Measures the heading structure without asserting: exactly one visible `h1`, and it is the first visible heading.
 *
 * @param page The page to measure.
 * @returns The findings.
 */
export async function collectHeadings(page: Page): Promise<AuditFinding[]> {
  return page.evaluate(() => {
    const findings: { check: string; target: string; detail: string }[] = [];
    const visible = (element: Element): boolean =>
      typeof element.checkVisibility === "function"
        ? element.checkVisibility({ checkVisibilityCSS: true })
        : element.getClientRects().length > 0;
    const describe = (element: Element): string =>
      `${element.tagName.toLowerCase()} "${(element.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 40)}"`;
    const headings = Array.from(
      document.querySelectorAll('h1, h2, h3, h4, h5, h6, [role="heading"]'),
    ).filter(visible);
    const h1s = headings.filter(
      (element) =>
        element.tagName === "H1" ||
        (element.getAttribute("role") === "heading" && element.getAttribute("aria-level") === "1"),
    );
    if (h1s.length !== 1) {
      findings.push({
        check: "headings",
        target: "document",
        detail: `expected exactly one visible h1, found ${h1s.length}${h1s.length ? `: ${h1s.map(describe).join(", ")}` : ""}`,
      });
    } else if (headings[0] !== h1s[0]) {
      findings.push({
        check: "headings",
        target: headings[0] ? describe(headings[0]) : "document",
        detail: "a heading comes before the h1",
      });
    }
    return findings;
  });
}

/**
 * Asserts exactly one visible `h1` that is the first visible heading.
 *
 * @param page The page to check.
 * @throws Error describing the heading problem.
 */
export async function checkHeadings(page: Page): Promise<void> {
  assertNoFindings("headings", await collectHeadings(page));
}

/**
 * Emulates `prefers-reduced-motion: reduce` and reports every element or pseudo-element whose computed animation or
 * transition duration is above 0.01ms, then restores the emulation.
 *
 * @param page The page to measure.
 * @returns The findings.
 */
export async function collectReducedMotion(page: Page): Promise<AuditFinding[]> {
  await page.emulateMedia({ reducedMotion: "reduce" });
  try {
    return await page.evaluate(() => {
      const findings: { check: string; target: string; detail: string }[] = [];
      const toMs = (value: string): number[] =>
        value
          .split(",")
          .map((part) => part.trim())
          .filter(Boolean)
          .map((part) => (part.endsWith("ms") ? parseFloat(part) : parseFloat(part) * 1000));
      const tooLong = (durations: string): number | null => {
        const longest = Math.max(0, ...toMs(durations));
        return longest > 0.01 ? longest : null;
      };
      const elements = Array.from(document.querySelectorAll("*"));
      for (const element of elements) {
        for (const pseudo of [null, "::before", "::after"]) {
          const style = getComputedStyle(element, pseudo);
          const animated = style.animationName !== "none" ? tooLong(style.animationDuration) : null;
          const transitioned =
            style.transitionProperty !== "none" ? tooLong(style.transitionDuration) : null;
          const longest = animated ?? transitioned;
          if (longest === null) continue;
          const id = element.id ? `#${element.id}` : "";
          const classes = (element.getAttribute("class") ?? "")
            .split(/\s+/)
            .filter(Boolean)
            .slice(0, 2)
            .map((name) => `.${name}`)
            .join("");
          findings.push({
            check: "reduced motion",
            target: `${element.tagName.toLowerCase()}${id}${classes}${pseudo ?? ""}`,
            detail: `${animated !== null ? "animation" : "transition"} lasts ${longest}ms under reduced motion`,
          });
        }
      }
      return findings.slice(0, 25);
    });
  } finally {
    await page.emulateMedia({ reducedMotion: null });
  }
}

/**
 * Asserts that nothing animates or transitions under `prefers-reduced-motion: reduce`.
 *
 * @param page The page to check.
 * @throws Error listing the animated elements.
 */
export async function checkReducedMotion(page: Page): Promise<void> {
  assertNoFindings("reduced motion", await collectReducedMotion(page));
}

/**
 * Resizes the viewport to 640x450 CSS pixels, which is 200 percent zoom of a 1280 wide window, measures horizontal
 * reflow and restores the viewport.
 *
 * @param page The page to measure.
 * @returns The findings.
 */
export async function collectZoomReflow(page: Page): Promise<AuditFinding[]> {
  const original = page.viewportSize();
  await page.setViewportSize({ width: 640, height: 450 });
  try {
    await page.evaluate(
      () => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())),
    );
    return (await collectReflow(page)).map((finding) => ({ ...finding, check: "zoom reflow" }));
  } finally {
    if (original) await page.setViewportSize(original);
  }
}

/**
 * Asserts that the page reflows at 200 percent zoom (640 CSS pixels wide) without horizontal scroll.
 *
 * @param page The page to check.
 * @throws Error listing the offending elements.
 */
export async function checkZoomReflow(page: Page): Promise<void> {
  assertNoFindings("zoom reflow", await collectZoomReflow(page));
}

/**
 * Measures the geometry of a dialog without asserting (UI-SPEC D-10).
 *
 * The kicker, title, body text, facts, fields and error slot share one left edge within 1px. Above 520 CSS pixels of
 * viewport the actions are right-aligned; at or below 520 they are stacked, each as wide as the actions container and
 * at least 44px tall. The dialog lies inside the viewport.
 *
 * @param page The page the dialog is on.
 * @param dialog The dialog element.
 * @returns The findings.
 */
export async function collectDialogGeometry(page: Page, dialog: Locator): Promise<AuditFinding[]> {
  return dialog.evaluate((root) => {
    const findings: { check: string; target: string; detail: string }[] = [];
    const add = (target: string, detail: string): void => {
      findings.push({ check: "dialog geometry", target, detail });
    };
    const visible = (element: Element): boolean =>
      typeof element.checkVisibility === "function"
        ? element.checkVisibility({ checkVisibilityCSS: true })
        : element.getClientRects().length > 0;
    const first = (selector: string): Element | null =>
      Array.from(root.querySelectorAll(selector)).find(visible) ?? null;
    const rows: [string, Element | null][] = [
      ["kicker", first(".kicker")],
      ["title", first("h2, h1")],
      ["body", first(".dialog__body, p")],
      // A before or after pair of a review row is a cell of that row's grid, not a facts block of the dialog.
      ["facts", first(".dialog__facts, dl:not(.change-item__pair)")],
      ["field", first(".field, label")],
      ["error slot", first(".dialog__error")],
    ];
    const present = rows.filter((row): row is [string, Element] => row[1] !== null);
    const baseline = present[0];
    if (baseline) {
      const edge = baseline[1].getBoundingClientRect().left;
      for (const [name, element] of present) {
        const left = element.getBoundingClientRect().left;
        if (Math.abs(left - edge) > 1) {
          add(
            name,
            `left edge ${left.toFixed(1)} differs from the ${baseline[0]} edge ${edge.toFixed(1)}`,
          );
        }
      }
    }
    const box = root.getBoundingClientRect();
    if (box.left < -1 || box.right > window.innerWidth + 1) {
      add(
        "dialog",
        `spans ${Math.round(box.left)}..${Math.round(box.right)} in a ${window.innerWidth}px viewport`,
      );
    }
    const actions = first(".dialog__actions");
    if (!actions) {
      add("actions", "the dialog has no .dialog__actions container");
      return findings;
    }
    const container = actions.getBoundingClientRect();
    const buttons = Array.from(actions.querySelectorAll("button, a")).filter(visible);
    if (buttons.length === 0) add("actions", "the actions container has no visible control");
    if (window.innerWidth > 520) {
      const rightmost = Math.max(...buttons.map((button) => button.getBoundingClientRect().right));
      if (buttons.length > 0 && Math.abs(rightmost - container.right) > 1) {
        add(
          "actions",
          `not right-aligned: last control ends at ${rightmost.toFixed(1)}, container at ${container.right.toFixed(1)}`,
        );
      }
    } else {
      // The shell stacks with `column-reverse` (the confirm action first), so the visual order is not the DOM
      // order: walk the controls top to bottom and require that no control overlaps the one above it.
      const ordered = [...buttons].sort(
        (left, right) => left.getBoundingClientRect().top - right.getBoundingClientRect().top,
      );
      let previousBottom = -Infinity;
      for (const button of ordered) {
        const rect = button.getBoundingClientRect();
        const name = (button.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 40);
        if (Math.abs(rect.width - container.width) > 1) {
          add(
            `action "${name}"`,
            `width ${rect.width.toFixed(1)} is not the full container width ${container.width.toFixed(1)}`,
          );
        }
        if (rect.height < 43.5)
          add(`action "${name}"`, `height ${rect.height.toFixed(1)} is below 44px`);
        if (rect.top < previousBottom - 1)
          add(`action "${name}"`, "is not stacked below the previous action");
        previousBottom = rect.bottom;
      }
    }
    return findings;
  });
}

/**
 * Asserts the dialog geometry rules of the shared dialog shell.
 *
 * @param page The page the dialog is on.
 * @param dialog The dialog element.
 * @throws Error listing the geometry problems.
 */
export async function checkDialogGeometry(page: Page, dialog: Locator): Promise<void> {
  assertNoFindings("dialog geometry", await collectDialogGeometry(page, dialog));
}

/** The most Tab stops the sticky coverage check visits, to bound the time on long pages. */
const stickyStopLimit = 60;

/**
 * Measures sticky and fixed bars without asserting: every bar is at most one third of the viewport tall, and no bar
 * covers the element that has keyboard focus while Tab moves through the scope.
 *
 * @param page The page to measure.
 * @returns The findings.
 */
export async function collectStickyCoverage(page: Page): Promise<AuditFinding[]> {
  const findings: AuditFinding[] = [];
  const modal = await page.evaluate(() => document.querySelector("dialog:modal") !== null);
  const scope = modal ? "dialog:modal" : "main";
  const expected = await inspect<StopIdentity[] | null>(page, { op: "tabbables", scope });
  if (expected === null) throw new Error(`sticky coverage scope not found: ${scope}`);
  const reported = new Set<string>();
  const checkBars = async (): Promise<void> => {
    for (const bar of await inspect<StickyBar[]>(page, { op: "bars" })) {
      if (reported.has(bar.path)) continue;
      reported.add(bar.path);
      if (bar.height > bar.viewportHeight / 3 + 1) {
        findings.push({
          check: "sticky coverage",
          target: bar.label,
          detail: `${bar.position} bar is ${Math.round(bar.height)}px tall, more than one third of the ${bar.viewportHeight}px viewport`,
        });
      }
    }
  };
  await checkBars();
  await resetFocus(page, scope);
  let stops = 0;
  let previousPath = "";
  for (let presses = 0; stops < Math.min(expected.length, stickyStopLimit); presses += 1) {
    if (presses >= maxWalkPresses) break;
    const stop = await pressAndInspect(page, "Tab", scope);
    if (stop.isBody || !stop.inScope) break;
    // A later segment of a date or time input is the same stop of the same control.
    if (stop.segmented && stop.path === previousPath) continue;
    previousPath = stop.path;
    stops += 1;
    if (stop.covered) {
      findings.push({
        check: "sticky coverage",
        target: stop.label,
        detail: `focused element is covered by ${stop.coveredBy}`,
      });
    }
    await checkBars();
  }
  return findings;
}

/**
 * Asserts that sticky bars stay at most one third of the viewport and never cover the focused element.
 *
 * @param page The page to check.
 * @throws Error listing the bars and covered elements.
 */
export async function checkStickyCoverage(page: Page): Promise<void> {
  assertNoFindings("sticky coverage", await collectStickyCoverage(page));
}

/** The focus ring measurement of one tab stop. */
export interface FocusRingSample {
  /** A readable name of the focused element. */
  label: string;
  /** The tag, id and first two classes of the focused element. */
  selector: string;
  /** Whether the ring is visible, at least 2px, gold-bright and at least 3:1 against its surface. */
  ok: boolean;
  /** The measured ring, or why it failed. */
  reason: string;
}

/**
 * Samples the focus ring of the first tab stops of the document, starting at the skip link.
 *
 * The discovery pass uses it to record which controls draw an incomplete ring; it never asserts.
 *
 * @param page The page to sample.
 * @param count The number of tab stops to sample. Defaults to 10.
 * @returns One sample per tab stop, in tab order; shorter when focus leaves the document earlier.
 */
export async function collectFocusRings(page: Page, count = 10): Promise<FocusRingSample[]> {
  const samples: FocusRingSample[] = [];
  await resetFocus(page, null);
  for (let index = 0; index < count; index += 1) {
    const stop = await pressAndInspect(page, "Tab", "body");
    if (stop.isBody) break;
    samples.push({
      label: stop.label,
      selector: stop.selector,
      ok: stop.ring.ok,
      reason: stop.ring.reason,
    });
  }
  return samples;
}
