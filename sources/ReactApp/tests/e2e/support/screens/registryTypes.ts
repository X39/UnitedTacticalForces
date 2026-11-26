import type { Page } from "@playwright/test";
import type { Culture, MockSession } from "../mockApi";

/**
 * Types and the required manifest of the server screen registry (Phase 69, D-12).
 *
 * The registry is the single list every audit iterates. This module and the slice modules import Playwright types
 * and the strict mock layer only; they never import `src` at runtime, so the Vitest completeness guard
 * (`tests/hosting/screen-registry.test.ts`) can load them.
 */

/**
 * One recorded accessibility finding that a screen may not fix itself, for example a root cause in a shared file
 * another plan owns. The audit subtracts only a finding whose rule and target both match and adds a test
 * annotation for every subtraction, so a recorded finding stays visible in the test report.
 */
export interface KnownFinding {
  /** The tracking id of the recorded finding, for example `XPF-01`; it appears in the test annotation. */
  id: string;
  /**
   * The rule of the finding: the axe rule id (for example `color-contrast`), or the keyboard walk rule id
   * (for example `focus-ring` or `focus-covered`).
   */
  rule: string;
  /**
   * The target of the finding: the axe target selector of the violating node with shadow and frame hops joined by a
   * space, or for a keyboard finding the element's tag, id and first two classes, such as `button.button.button--primary`.
   */
  target: string;
}

/** One state of a screen: how to reach it and how to know it was reached. */
export interface ScreenState {
  /** A short name of the state used in failure output, for example `populated`. */
  name: string;
  /** Moves the page from the previous state to this one; omitted for the state reached by navigation. */
  open?: ((page: Page) => Promise<void>) | undefined;
  /** Resolves once the state is fully rendered; it must wait on page structure, not on translated text. */
  ready(page: Page): Promise<void>;
  /** Findings of this state that the audit subtracts from the axe result. */
  knownFindings?: readonly KnownFinding[] | undefined;
  /**
   * Whether the modal dialog of this state cannot be closed with Escape, as a dialog that is busy with a request. The
   * keyboard walk then does not try to close it at the end and so does not report the open dialog.
   */
  dialogStaysOpen?: boolean | undefined;
  /**
   * The pixel baseline key screen id of plan 69-31 this state alone is captured as, for example `K-03`. It applies to an
   * entry whose states belong to different key screens; the entry's own `baseline` names the key screen of its first
   * state.
   */
  baseline?: string | undefined;
}

/** Which audits run for an entry. A flag that is false must be justified in the entry's slice module. */
export interface AuditFlags {
  /** Axe at WCAG 2.2 AA with the non-vacuity guard. */
  axe: boolean;
  /** The keyboard walk on desktop and the sticky-bar coverage check on the phone projects. */
  keyboard: boolean;
  /** Reflow on the phone projects and the zoom reflow check on desktop. */
  reflow: boolean;
  /** The text-spacing override on the phone projects. */
  spacing: boolean;
  /** Whether the German walk of plan 69-25 visits the entry. */
  german: boolean;
  /** Exactly one `h1`, and it is the first heading. */
  headings: boolean;
}

/** The kinds of registry entry. */
export type ScreenKind = "screen" | "overlay" | "notice" | "smoke";

/** One registry entry: a screen, an overlay, a notice or a non-server smoke page, with every state it must reach. */
export interface ScreenEntry {
  /** The registry id, for example `S-02`; it equals the id of its required item. */
  id: string;
  /** What the entry covers. */
  kind: ScreenKind;
  /** The in-app route the test opens, starting with a slash, with sample ids filled in. */
  route: string;
  /** The router manifest pattern the route belongs to; set on screens. */
  routePattern?: string | undefined;
  /** Additional manifest patterns the entry reaches by navigating from its states. */
  alsoCovers?: readonly string[] | undefined;
  /** The workspace tab the entry covers; set on workspace tab screens. */
  tab?: string | undefined;
  /** Installs the strict mock for this entry and returns its record. */
  setup(page: Page, culture: Culture): Promise<MockSession>;
  /** The states the audit visits in order on one page load; at least one. */
  states: readonly ScreenState[];
  /** Which audits run. */
  audit: AuditFlags;
  /** The pixel baseline key screen id of plan 69-31, for example `K-01`. */
  baseline?: string | undefined;
}

/** One item of the required manifest: an id the registry must cover, either with an entry or a pending declaration. */
export interface RequiredRegistryItem {
  /** The registry id. */
  id: string;
  /** What the id covers. */
  kind: ScreenKind;
  /** The hosting route manifest pattern; set on every screen so no hosting route is left out. */
  routePattern?: string | undefined;
  /** More manifest patterns covered by the same entry. */
  alsoCovers?: readonly string[] | undefined;
  /** The workspace tab; set on the workspace tab screens. */
  tab?: string | undefined;
  /** The Wave 3 slice plan that adds the entry. */
  slice: "fleet" | "workspace" | "adjacent";
}

/** The hosting route manifest patterns the screens use; they equal `hostingRouteDefinitions` in the app. */
const patterns = {
  overview: "/command",
  servers: "/command/servers",
  create: "/command/servers/create",
  costAlerts: "/command/servers/cost-alerts",
  workspace: "/command/servers/{ServerId:guid}",
  profiles: "/command/configuration-profiles",
  profileCreate: "/command/configuration-profiles/create",
  profile: "/command/configuration-profiles/{ProfileId:guid}",
  finance: "/command/hosting-finance",
  financeAccount: "/command/hosting-finance/accounts/{CostOwnerId:guid}",
  financeSettings: "/command/hosting-finance/settings",
} as const;

/**
 * Every id the registry must cover, in the order of the UI-SPEC Server Screen Inventory.
 *
 * Wave 3 slice plans add an entry per id and remove the id from their slice's pending list. The Vitest guard fails
 * when an id has neither, or both, so a new screen, tab, overlay or notice cannot ship unverified.
 */
export const requiredRegistry: readonly RequiredRegistryItem[] = [
  // Screens: fleet slice (69-26).
  { id: "S-01", kind: "screen", routePattern: patterns.overview, slice: "fleet" },
  { id: "S-02", kind: "screen", routePattern: patterns.servers, slice: "fleet" },
  { id: "S-03", kind: "screen", routePattern: patterns.create, slice: "fleet" },
  // Screens: workspace slice (69-27).
  {
    id: "S-04",
    kind: "screen",
    routePattern: patterns.workspace,
    tab: "overview",
    slice: "workspace",
  },
  {
    id: "S-05",
    kind: "screen",
    routePattern: patterns.workspace,
    tab: "activity",
    slice: "workspace",
  },
  {
    id: "S-06",
    kind: "screen",
    routePattern: patterns.workspace,
    tab: "settings",
    slice: "workspace",
  },
  {
    id: "S-07",
    kind: "screen",
    routePattern: patterns.workspace,
    tab: "settings",
    slice: "workspace",
  },
  // The collapsible Settings panels (70-15): the collapsed default, the deep link, the auto-open rules and the filter.
  {
    id: "S-14",
    kind: "screen",
    routePattern: patterns.workspace,
    tab: "settings",
    slice: "workspace",
  },
  // The Phase 71 Settings panels: Access (S-15) and Cost owner (S-16). The UI-SPEC names them S-09 and S-10, which
  // belong to the Machine and Cost alerts screens, so the free ids S-15 and S-16 are used.
  {
    id: "S-15",
    kind: "screen",
    routePattern: patterns.workspace,
    tab: "settings",
    slice: "workspace",
  },
  {
    id: "S-16",
    kind: "screen",
    routePattern: patterns.workspace,
    tab: "settings",
    slice: "workspace",
  },
  // Screens: adjacent slice (69-28).
  { id: "S-08", kind: "screen", routePattern: patterns.workspace, tab: "files", slice: "adjacent" },
  {
    id: "S-09",
    kind: "screen",
    routePattern: patterns.workspace,
    tab: "machine",
    slice: "adjacent",
  },
  { id: "S-10", kind: "screen", routePattern: patterns.costAlerts, slice: "adjacent" },
  {
    id: "S-11",
    kind: "screen",
    routePattern: patterns.profiles,
    alsoCovers: [patterns.profileCreate],
    slice: "adjacent",
  },
  { id: "S-12", kind: "screen", routePattern: patterns.profile, slice: "adjacent" },
  {
    id: "S-13",
    kind: "screen",
    routePattern: patterns.finance,
    alsoCovers: [patterns.financeAccount, patterns.financeSettings],
    slice: "adjacent",
  },
  // Overlays.
  { id: "O-01", kind: "overlay", slice: "fleet" },
  { id: "O-02", kind: "overlay", slice: "workspace" },
  { id: "O-03", kind: "overlay", slice: "workspace" },
  { id: "O-04", kind: "overlay", slice: "workspace" },
  { id: "O-05", kind: "overlay", slice: "workspace" },
  { id: "O-06", kind: "overlay", slice: "adjacent" },
  { id: "O-07", kind: "overlay", slice: "adjacent" },
  { id: "O-08", kind: "overlay", slice: "fleet" },
  { id: "O-09", kind: "overlay", slice: "fleet" },
  // The Phase 71 dialogs: Transfer cost owner (O-10) and the Access panel's Remove all access and self-removal
  // dialogs (O-11). The UI-SPEC names them O-04 and O-05, which belong to the consent and delete dialogs.
  { id: "O-10", kind: "overlay", slice: "workspace" },
  { id: "O-11", kind: "overlay", slice: "workspace" },
  // Notices: fleet live (N-01), deep-link action (N-02), create start (N-03), execution paused (N-04).
  { id: "N-01", kind: "notice", slice: "fleet" },
  { id: "N-02", kind: "notice", slice: "fleet" },
  { id: "N-03", kind: "notice", slice: "fleet" },
  { id: "N-04", kind: "notice", slice: "fleet" },
  // Notices: archived server (N-05), pending start (N-06), cost alert banner (N-07), automatic shutdown (N-08),
  // consent needed (N-09), job failure (N-10), job progress (N-11).
  { id: "N-05", kind: "notice", slice: "workspace" },
  { id: "N-06", kind: "notice", slice: "workspace" },
  { id: "N-07", kind: "notice", slice: "workspace" },
  { id: "N-08", kind: "notice", slice: "workspace" },
  { id: "N-09", kind: "notice", slice: "workspace" },
  { id: "N-10", kind: "notice", slice: "workspace" },
  { id: "N-11", kind: "notice", slice: "workspace" },
  // Smoke entries for the non-server page families: members list (M-01), role detail (M-02), public page editor
  // (M-03) and home (M-04).
  { id: "M-01", kind: "smoke", slice: "adjacent" },
  { id: "M-02", kind: "smoke", slice: "adjacent" },
  { id: "M-03", kind: "smoke", slice: "adjacent" },
  { id: "M-04", kind: "smoke", slice: "adjacent" },
];

/**
 * Looks up the slice that owns a registry id.
 *
 * @param id The registry id.
 * @returns The owning slice, or undefined when the id is not in the required manifest.
 */
export function sliceOf(id: string): RequiredRegistryItem["slice"] | undefined {
  return requiredRegistry.find((item) => item.id === id)?.slice;
}
