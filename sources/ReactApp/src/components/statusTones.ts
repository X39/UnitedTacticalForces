/**
 * The closed badge tone lexicon.
 *
 * Every `StatusBadge` tone in the app comes from one of the maps below through {@link toneOf}, so one color
 * carries one meaning on every admin, member and server page:
 *
 * - `neutral`: inactive, archived, draft, hidden, not applicable
 * - `info`: in progress, scheduled, pending a system step
 * - `success`: healthy, complete, approved, present
 * - `warning`: needs attention soon, partial, may cost
 * - `danger`: failed, blocked, rejected, removed
 */

/** One of the five tones of the lexicon; identical to the tone union of `StatusBadge`. */
export type StatusTone = "neutral" | "info" | "success" | "warning" | "danger";

/**
 * Resolves the tone of a domain value.
 *
 * An unknown, empty or missing key yields `neutral`, so a value the API adds later never renders with a
 * misleading color.
 *
 * @param map The domain map that owns the key.
 * @param key The domain value, such as an account status or a report state.
 * @returns The mapped tone, or `neutral` when the key is not in the map.
 */
export function toneOf<K extends string>(
  map: ReadonlyMap<K, StatusTone>,
  key: K | string | null | undefined,
): StatusTone {
  if (key === null || key === undefined) return "neutral";
  return map.get(key as K) ?? "neutral";
}

/**
 * Member account status as returned by `memberStatus`: `verified`, `unverified`, `banned`, `deleted`.
 *
 * `unverified` is neutral because the lexicon lists "Not verified" as an inactive state.
 */
export const accountTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["verified", "success"],
  ["unverified", "neutral"],
  ["banned", "danger"],
  ["deleted", "danger"],
]);

/**
 * Formal Verein membership status: `Active`, `Suspended`, `Ended`.
 *
 * A member without a formal membership has no key and resolves to `neutral` ("Not a member").
 */
export const membershipTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["Active", "success"],
  ["Suspended", "warning"],
  ["Ended", "neutral"],
]);

/** Membership request status: `Submitted`, `AcceptedForContact`, `Approved`, `Rejected`, `Withdrawn`. */
export const membershipRequestTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["Submitted", "warning"],
  ["AcceptedForContact", "info"],
  ["Approved", "success"],
  ["Rejected", "danger"],
  ["Withdrawn", "neutral"],
]);

/** Viewer attendance of an operation: `Accepted`, `Maybe`, `Rejected`, `Unassigned`. */
export const attendanceTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["Accepted", "success"],
  ["Maybe", "warning"],
  ["Rejected", "danger"],
  ["Unassigned", "neutral"],
]);

/**
 * Event state in the event administration list: `ready`, `low-slots`, `hidden`, `archived`.
 *
 * `hidden` is neutral because the lexicon lists "Hidden" as an inactive state.
 */
export const eventTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["ready", "success"],
  ["low-slots", "warning"],
  ["hidden", "neutral"],
  ["archived", "neutral"],
]);

/** Operation readiness on the public pages: `ready`, `needs-signups`. */
export const operationTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["ready", "success"],
  ["needs-signups", "warning"],
]);

/**
 * Field report state: `published`, `scheduled`, `draft`, `deleted`.
 *
 * `draft` is neutral because the lexicon lists "Draft" as an inactive state.
 */
export const reportTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["published", "success"],
  ["scheduled", "info"],
  ["draft", "neutral"],
  ["deleted", "danger"],
]);

/**
 * Slot status in the event slot planner: `hidden`, `assigned`, `self-assignable` (open for self signup) and
 * `unassigned` (visible, unfilled and not self assignable).
 */
export const slotTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["hidden", "neutral"],
  ["assigned", "success"],
  ["self-assignable", "info"],
  ["unassigned", "warning"],
]);

/**
 * Operational settings feature: `off` (disabled), `configured` and `missing-configuration` (enabled but not
 * configured).
 */
export const settingTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["off", "neutral"],
  ["configured", "success"],
  ["missing-configuration", "warning"],
]);

/** Hosting finance account and ledger direction: `active`, `frozen`, `income`, `expense`. */
export const financeTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["active", "success"],
  ["frozen", "warning"],
  ["income", "success"],
  ["expense", "danger"],
]);

/** Lifecycle job status, mirroring the problem tones of the activity timeline. */
export const jobTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["Queued", "info"],
  ["Running", "info"],
  ["WaitingForWorker", "info"],
  ["RetryPending", "warning"],
  ["Succeeded", "success"],
  ["Failed", "danger"],
  ["Skipped", "neutral"],
  ["CancelledByCostStop", "neutral"],
]);
