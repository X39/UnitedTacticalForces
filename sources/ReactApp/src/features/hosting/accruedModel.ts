import type { TranslationKey } from "@/app/localizationTypes";
import { formatMoney } from "@/features/hosting/model";
import type { DerivedServerState } from "@/features/hosting/serverState";
import type { GameServerAccruedCost } from "@/features/hosting/types";

/** The smallest amount that renders as itself; anything below reads "Less than" this while cost may run. */
const smallestShownAmount = 0.01;

/**
 * The states of the accrued row, in the order they are matched (66-UI-SPEC, Accrued value states).
 *
 * - `loading`: the server is loaded, the accrued value is not.
 * - `none`: the server never had a session.
 * - `unknown`: the API could not calculate an amount, or the value could not be loaded.
 * - `final`: the cost stopped and the state model agrees; the amount is the last session total.
 * - `finalActive`: the API says final but the state model says cost may still run; the state model wins (FND-03).
 * - `maybe`: costing, and the cloud machine is not yet confirmed gone.
 * - `live`: costing and confirmed live.
 */
export type AccruedState =
  "loading" | "none" | "unknown" | "final" | "finalActive" | "maybe" | "live";

/** Translation keys of the row term (`dt`): active "This session so far" or final "Last session total". */
export type AccruedTermKey = "Server_Workspace_Cost_Accrued" | "Server_Workspace_Cost_AccruedFinal";

/** What line 1 of the accrued row shows: an amount to format or a state sentence. */
export type AccruedValue =
  | {
      /** An amount; format it with {@link formatAccruedAmount}. */
      kind: "amount";
      /** The amount exactly as the API sent it. */
      amount: number;
      /** The ISO currency code. */
      currency: string;
      /** Whether cost may still accrue, which decides how a zero or tiny amount reads. */
      costing: boolean;
    }
  | {
      /** A state sentence. */
      kind: "sentence";
      /** The localization key of the sentence. */
      key: TranslationKey;
    };

/** Line 2 of the accrued row: which time sentence to show and the instant the API says the value was true. */
export interface AccruedTimeLine {
  /** The key of the time sentence: "As of {{time}}" while costing, "Cost stopped: {{time}}" when final. */
  key: "Server_Workspace_Cost_AccruedAsOf" | "Server_Workspace_Cost_AccruedStopped";
  /** The ISO instant from the API; never the time the browser received it. */
  instant: string;
}

/** Translation keys of the billing line: per started hour with the next step, per started hour, per started minute. */
export type AccruedBillingKey =
  | "Server_Workspace_Cost_AccruedBilling_Hour"
  | "Server_Workspace_Cost_AccruedBilling_HourNext"
  | "Server_Workspace_Cost_AccruedBilling_Minute";

/** The billing line of the accrued row: which sentence to show and the instant the next billing unit starts. */
export interface AccruedBillingLine {
  /** The key of the billing sentence. */
  key: AccruedBillingKey;
  /** The ISO instant the API says the next unit starts, set only for the sentence that names it. */
  instant: string | null;
}

/** The presentation of the accrued row, derived without any framework or clock. */
export interface AccruedPresentation {
  /** Which row of the state table matched. */
  state: AccruedState;
  /** The key of the row term. */
  termKey: AccruedTermKey;
  /** Line 1: the amount or a state sentence. */
  value: AccruedValue;
  /** Line 2: the time line; null when there is none. */
  time: AccruedTimeLine | null;
  /** Line 3 notes in display order: the mapped unknown reason or the still-counting sentence. */
  notes: TranslationKey[];
  /** The billing line under the time line; null when the unit is unknown or the row shows no amount. */
  billing: AccruedBillingLine | null;
  /** Whether cost may still accrue; only then does a paused socket gain the stale sentence. */
  costing: boolean;
}

/**
 * Maps the API unavailable reason codes to sentences. Only `rate` has a sentence; `not-yet-charged`, `hidden`
 * and every unknown code show the bare unknown sentence, so a new server-side code never prints raw text.
 */
const reasonSentences = new Map<string, TranslationKey>([
  ["rate", "Server_Workspace_Cost_AccruedReason_Rate"],
]);

/** The billing unit lengths in seconds that have a billing sentence; any other length shows no line. */
const hourSeconds = 3600;
const minuteSeconds = 60;

/**
 * Selects the billing line from the facts the API sent. The browser only formats them: it never derives the next
 * billing instant or the unit from times or rates (D-02). A one-hour unit names the next hour while cost may
 * still run and only the unit once it stopped; a one-minute unit reads a static sentence; every other unit, and a
 * missing unit or instant, shows nothing.
 *
 * @param unitSeconds The length of the provider billing unit in seconds as the API sent it.
 * @param billedThrough The ISO instant the last started unit ends at as the API sent it.
 * @param isFinalRow Whether the row is in the final state.
 * @returns The billing line, or null when there is nothing true to say.
 */
function billingLine(
  unitSeconds: number | null | undefined,
  billedThrough: string | null | undefined,
  isFinalRow: boolean,
): AccruedBillingLine | null {
  if (unitSeconds === minuteSeconds) {
    return { key: "Server_Workspace_Cost_AccruedBilling_Minute", instant: null };
  }
  if (unitSeconds !== hourSeconds) return null;
  if (isFinalRow || billedThrough === null || billedThrough === undefined) {
    return { key: "Server_Workspace_Cost_AccruedBilling_Hour", instant: null };
  }
  return { key: "Server_Workspace_Cost_AccruedBilling_HourNext", instant: billedThrough };
}

/**
 * Derives what the accrued row shows. The first matching row of the state table wins and the state model wins
 * over the API final flag (FND-03).
 *
 * @param accrued The accrued cost as loaded or pushed; undefined while it has not loaded or could not be loaded.
 * @param derived The derived state of the server, which decides whether cost may still run.
 * @param loading Whether the accrued value is still being loaded for the first time.
 * @returns The state, the term, the lines and whether the row counts as costing.
 */
export function accruedPresentation(
  accrued: GameServerAccruedCost | undefined,
  derived: DerivedServerState,
  loading: boolean,
): AccruedPresentation {
  const active: AccruedTermKey = "Server_Workspace_Cost_Accrued";
  const sentence = (key: TranslationKey): AccruedValue => ({ kind: "sentence", key });
  if (loading) {
    return {
      state: "loading",
      termKey: active,
      value: sentence("Server_Workspace_Cost_AccruedLoading"),
      time: null,
      notes: [],
      billing: null,
      costing: false,
    };
  }
  if (accrued !== undefined && accrued.session === null) {
    return {
      state: "none",
      termKey: active,
      value: sentence("Server_Workspace_Cost_AccruedNone"),
      time: null,
      notes: [],
      billing: null,
      costing: false,
    };
  }
  const session = accrued?.session;
  if (session === undefined || session === null || session.amount === null) {
    const reason = session?.unavailableReason;
    const reasonKey =
      reason === undefined || reason === null ? undefined : reasonSentences.get(reason);
    return {
      state: "unknown",
      termKey: active,
      value: sentence("Server_Workspace_Cost_AccruedUnknown"),
      time: null,
      notes: reasonKey === undefined ? [] : [reasonKey],
      billing: null,
      costing: false,
    };
  }
  const costing = derived.cost !== "none";
  const amount = (isCosting: boolean): AccruedValue => ({
    kind: "amount",
    amount: session.amount ?? 0,
    currency: session.currency,
    costing: isCosting,
  });
  const asOf: AccruedTimeLine = { key: "Server_Workspace_Cost_AccruedAsOf", instant: session.asOf };
  const live = billingLine(session.billingUnitSeconds, session.billedThrough, false);
  if (session.isFinal && !costing) {
    return {
      state: "final",
      termKey: "Server_Workspace_Cost_AccruedFinal",
      value: amount(false),
      time: { key: "Server_Workspace_Cost_AccruedStopped", instant: session.asOf },
      notes: [],
      billing: billingLine(session.billingUnitSeconds, session.billedThrough, true),
      costing: false,
    };
  }
  if (session.isFinal) {
    return {
      state: "finalActive",
      termKey: active,
      value: amount(true),
      time: asOf,
      notes: [],
      billing: live,
      costing: true,
    };
  }
  if (derived.cost === "maybe") {
    return {
      state: "maybe",
      termKey: active,
      value: amount(true),
      time: asOf,
      notes: ["Server_Workspace_Cost_AccruedMaybe"],
      billing: live,
      costing: true,
    };
  }
  return {
    state: "live",
    termKey: active,
    value: amount(costing),
    time: asOf,
    notes: [],
    billing: live,
    costing,
  };
}

/** The translation function {@link formatAccruedAmount} needs: only the tiny amount sentence. */
export type AccruedTranslate = (
  key: "Server_Workspace_Cost_AccruedTiny",
  options: { amount: string },
) => string;

/**
 * Formats an accrued amount for the strip and the announcement. The amount is never defaulted: at least one cent
 * renders as money; above zero and below one cent, or exactly zero while cost may still accrue, reads "Less than"
 * one cent so a running session never shows a rounded-down zero; exactly zero in a final state reads the zero
 * amount, which is true. The money placeholder dash is never produced.
 *
 * @param amount The amount exactly as the API sent it.
 * @param currency The ISO currency code.
 * @param costing Whether cost may still accrue.
 * @param t Translates the tiny amount sentence.
 * @returns The text to show.
 */
export function formatAccruedAmount(
  amount: number,
  currency: string,
  costing: boolean,
  t: AccruedTranslate,
): string {
  if (amount >= smallestShownAmount) return formatMoney(amount, currency);
  if (amount > 0 || (amount === 0 && costing)) {
    return t("Server_Workspace_Cost_AccruedTiny", {
      amount: formatMoney(smallestShownAmount, currency),
    });
  }
  return formatMoney(amount, currency);
}

/**
 * Returns whether a new accrued value should replace the cached one. An older value never wins, so a reordered
 * frame or refetch cannot move the shown amount backwards; an equal time is accepted so a corrected value of the
 * same instant lands. A missing cached value, a missing session on either side or an unreadable cached time never
 * blocks the new value; an unreadable new time does.
 *
 * @param next The new value.
 * @param current The cached value, if any.
 */
export function isNewerAccrued(
  next: GameServerAccruedCost,
  current: GameServerAccruedCost | undefined,
): boolean {
  if (current === undefined || current.session === null || next.session === null) return true;
  const nextTime = Date.parse(next.session.asOf);
  if (Number.isNaN(nextTime)) return false;
  const currentTime = Date.parse(current.session.asOf);
  return Number.isNaN(currentTime) || nextTime >= currentTime;
}
