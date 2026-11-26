/**
 * The pure model of the automatic shutdown and schedule settings (68-UI-SPEC).
 *
 * The browser displays and validates here; it never calculates. Idle time, stop instants and next runs are
 * instants the control plane sends, and this module only parses, validates, normalizes, compares and maps API
 * values to closed key maps. The types are structural so the module does not depend on the generated contract.
 */

/** The action of a weekly rule: start the server, or shut it down and stop billing. */
export type ScheduleAction = "Start" | "Stop";

/** The limits of the automation settings, as the API sends them. */
export interface AutomationLimitsInput {
  /** The smallest idle time in minutes. */
  idleMinutesMin: number;
  /** The largest idle time in minutes. */
  idleMinutesMax: number;
  /** The smallest maximum runtime in hours. */
  maxRuntimeHoursMin: number;
  /** The largest maximum runtime in hours. */
  maxRuntimeHoursMax: number;
  /** The most schedules a server may have. */
  maxSchedules: number;
  /** How many minutes before an automatic shutdown the warning is sent. */
  warningLeadMinutes: number;
}

/** One saved weekly rule, as the API sends it. */
export interface SavedScheduleInput {
  /** The id of the rule. */
  id: string;
  /** The action; a value a newer API adds is tolerated and treated as a start. */
  action: ScheduleAction | string;
  /** The ISO weekdays, Monday is 1. */
  days: readonly number[];
  /** The wall-clock time as `HH:mm`. */
  time: string;
  /** The next run instant calculated by the control plane, or null. */
  nextRunAt: string | null;
  /** The last run, or null when the rule has not run. */
  lastRun: { at: string; outcome: string } | null;
}

/** The saved automation settings, as the API sends them. */
export interface SavedAutomationInput {
  /** Whether idle shutdown is on. */
  idleShutdownEnabled: boolean;
  /** The idle time in minutes; kept while the setting is off. */
  idleShutdownMinutes: number;
  /** Whether the maximum runtime is on. */
  maxRuntimeEnabled: boolean;
  /** The maximum runtime in hours; kept while the setting is off. */
  maxRuntimeHours: number;
  /** The IANA time zone of the rules, or null when none was saved yet. */
  timeZone: string | null;
  /** The saved rules. */
  schedules: readonly SavedScheduleInput[];
  /** The concurrency token of the settings. */
  version: number;
}

/** One rule of the draft. */
export interface RuleDraft {
  /** A local key for React lists and focus; it is not the API id. */
  key: string;
  /** The API id of a saved rule, null for a new rule. */
  id: string | null;
  /** The action. */
  action: ScheduleAction;
  /** The selected ISO weekdays, Monday is 1, in the order the viewer picked them. */
  days: number[];
  /** The wall-clock time as `HH:mm`, or an empty text while none is chosen. */
  time: string;
}

/** The editable state of the automation panel. */
export interface AutomationDraft {
  /** Whether idle shutdown is switched on. */
  idleShutdownEnabled: boolean;
  /** The idle minutes exactly as typed. */
  idleMinutes: string;
  /** Whether the maximum runtime is switched on. */
  maxRuntimeEnabled: boolean;
  /** The maximum runtime hours exactly as typed. */
  maxHours: string;
  /** The IANA time zone of the rules. */
  timeZone: string;
  /** The rules in entry order. */
  rules: RuleDraft[];
}

/** Why the text of a number cannot be saved. */
export type NumberError = "Required" | "Number" | "Min" | "Max";

/** Why one rule cannot be saved. */
export interface RuleErrors {
  /** True when no weekday is selected. */
  days: boolean;
  /** True when the time is missing or not a valid `HH:mm`. */
  time: boolean;
  /** True when another rule runs at the same time on one of the same days. */
  duplicate: boolean;
}

/** The verdict of {@link validateAutomationDraft}. */
export interface AutomationValidation {
  /** The reason the idle minutes are invalid, or null (also while the setting is off or unavailable). */
  idleMinutes: NumberError | null;
  /** The reason the runtime hours are invalid, or null (also while the limit is off). */
  maxHours: NumberError | null;
  /** One entry per rule, in rule order. */
  rules: RuleErrors[];
  /** True when the draft holds more rules than the API accepts. */
  overLimit: boolean;
  /** The number of invalid fields: the numbers, and per rule the days field and the time field. */
  invalidCount: number;
  /** True when nothing is invalid and the rule count is within the limit. */
  valid: boolean;
}

/** The body of the settings update, as the API expects it. */
export interface AutomationUpdateInput {
  /** The concurrency token of the settings the draft started from. */
  version: number;
  /** Whether idle shutdown is on. */
  idleShutdownEnabled: boolean;
  /** The idle minutes: the typed number while the setting is on, else the saved one. */
  idleShutdownMinutes: number;
  /** Whether the maximum runtime is on. */
  maxRuntimeEnabled: boolean;
  /** The runtime hours: the typed number while the limit is on, else the saved one. */
  maxRuntimeHours: number;
  /** The IANA time zone of the rules. */
  timeZone: string;
  /** The rules; the id is null for a new rule. */
  schedules: { id: string | null; action: ScheduleAction; days: number[]; time: string }[];
  /** True when the viewer confirmed the scheduled starts in the consent dialog. */
  startConsent: boolean;
}

/** The result of {@link parseWholeNumber}: the number, or the reason there is none. */
type NumberParse = { value: number } | { error: NumberError };

/** Digits only: no sign, no decimals, no grouping. */
const wholeNumberPattern = /^\d+$/;

/** A time as the native time input and the API write it, with optional seconds. */
const timeInputPattern = /^(\d{1,2}):(\d{2})(?::\d{2}(?:\.\d+)?)?$/;

/** A valid wall-clock time of the form `HH:mm`. */
const validTimePattern = /^([01]\d|2[0-3]):[0-5]\d$/;

/** Every ISO weekday, Monday first. */
const isoWeekdays: readonly number[] = [1, 2, 3, 4, 5, 6, 7];

/** The ISO weekdays that make up "on weekdays". */
const workdayKey = "1,2,3,4,5";

/** The action an unknown API action is shown as; only an explicit stop is a stop. */
const fallbackAction: ScheduleAction = "Start";

/**
 * Parses the text of a whole number the way people type it.
 *
 * The text is trimmed and must be digits only. The rules mirror the API, so its 400 answer stays a safety net.
 *
 * @param text The text of the field.
 * @param min The smallest accepted number, from the limits.
 * @param max The largest accepted number, from the limits.
 * @returns The number, or the first rule it breaks: required, number, min or max.
 */
export function parseWholeNumber(text: string, min: number, max: number): NumberParse {
  const trimmed = text.trim();
  if (trimmed === "") return { error: "Required" };
  if (!wholeNumberPattern.test(trimmed)) return { error: "Number" };
  const value = Number(trimmed);
  if (!Number.isSafeInteger(value)) return { error: "Max" };
  if (value < min) return { error: "Min" };
  if (value > max) return { error: "Max" };
  return { value };
}

/** Parses digits only, with no range; null when the text is not a whole number. */
function parseLooseNumber(text: string): number | null {
  const trimmed = text.trim();
  if (!wholeNumberPattern.test(trimmed)) return null;
  const value = Number(trimmed);
  return Number.isSafeInteger(value) ? value : null;
}

/**
 * Normalizes a time text to `HH:mm`.
 *
 * @param text A time as typed or as the API sends it.
 * @returns The zero-padded `HH:mm` without seconds, the trimmed text when it is no time, or an empty text.
 */
function normalizeTime(text: string): string {
  const trimmed = text.trim();
  const match = timeInputPattern.exec(trimmed);
  if (match === null) return trimmed;
  const [, hours = "", minutes = ""] = match;
  return `${hours.padStart(2, "0")}:${minutes}`;
}

/** Sorts weekdays ascending and removes repeats. */
function normalizeDays(days: readonly number[]): number[] {
  return [...new Set(days)].sort((left, right) => left - right);
}

/** Maps an API action to the closed action set; anything but a stop is a start. */
function normalizeAction(action: string): ScheduleAction {
  return action === "Stop" ? "Stop" : fallbackAction;
}

/** Tells whether a new rule holds anything the viewer entered. */
function isFilled(rule: RuleDraft): boolean {
  return rule.days.length > 0 || rule.time.trim() !== "";
}

/** The rules that count as part of the draft: saved rules always, new rules once they hold an entry. */
function meaningfulRules(rules: readonly RuleDraft[]): RuleDraft[] {
  return rules.filter((rule) => rule.id !== null || isFilled(rule));
}

/** Tells whether a draft rule equals the saved rule in action, days and time. */
function sameRule(rule: RuleDraft, saved: SavedScheduleInput): boolean {
  return (
    rule.action === normalizeAction(saved.action) &&
    normalizeDays(rule.days).join(",") === normalizeDays(saved.days).join(",") &&
    normalizeTime(rule.time) === normalizeTime(saved.time)
  );
}

/**
 * Builds the draft a panel starts from.
 *
 * Numbers become the text of their field, a missing zone becomes the viewer's browser zone and every rule gets a
 * fresh local key next to its API id.
 *
 * @param saved The saved settings.
 * @param browserZone The viewer's resolved browser time zone.
 * @param newKey Creates a fresh local key for a rule.
 * @returns The editable draft.
 */
export function draftFromSaved(
  saved: SavedAutomationInput,
  browserZone: string,
  newKey: () => string,
): AutomationDraft {
  return {
    idleShutdownEnabled: saved.idleShutdownEnabled,
    idleMinutes: String(saved.idleShutdownMinutes),
    maxRuntimeEnabled: saved.maxRuntimeEnabled,
    maxHours: String(saved.maxRuntimeHours),
    timeZone: saved.timeZone ?? browserZone,
    rules: saved.schedules.map((rule): RuleDraft => ({
      key: newKey(),
      id: rule.id,
      action: normalizeAction(rule.action),
      days: normalizeDays(rule.days),
      time: normalizeTime(rule.time),
    })),
  };
}

/**
 * Checks a draft against the API's rules.
 *
 * A disabled number is neither validated nor sent as enabled: the minutes are ignored while idle shutdown is off or
 * the game cannot report players, and the hours are ignored while the limit is off. A rule needs a day and a valid
 * time, and two rules that share a weekday and a time are both marked duplicate. Blank rules are invalid, never
 * silently dropped.
 *
 * @param draft The editable state.
 * @param limits The limits from the settings.
 * @param idleAvailable Whether the game reports players, so idle shutdown can be used at all.
 * @returns The per-field reasons, the rule errors, the invalid field count and the overall verdict.
 */
export function validateAutomationDraft(
  draft: AutomationDraft,
  limits: AutomationLimitsInput,
  idleAvailable: boolean,
): AutomationValidation {
  let idleMinutes: NumberError | null = null;
  if (idleAvailable && draft.idleShutdownEnabled) {
    const parsed = parseWholeNumber(
      draft.idleMinutes,
      limits.idleMinutesMin,
      limits.idleMinutesMax,
    );
    if ("error" in parsed) idleMinutes = parsed.error;
  }
  let maxHours: NumberError | null = null;
  if (draft.maxRuntimeEnabled) {
    const parsed = parseWholeNumber(
      draft.maxHours,
      limits.maxRuntimeHoursMin,
      limits.maxRuntimeHoursMax,
    );
    if ("error" in parsed) maxHours = parsed.error;
  }

  const slots = new Map<string, number[]>();
  draft.rules.forEach((rule, index) => {
    const time = normalizeTime(rule.time);
    if (!validTimePattern.test(time)) return;
    for (const day of new Set(rule.days)) {
      const slot = `${day}|${time}`;
      slots.set(slot, [...(slots.get(slot) ?? []), index]);
    }
  });
  const duplicated = new Set<number>();
  for (const indexes of slots.values()) {
    if (indexes.length > 1) for (const index of indexes) duplicated.add(index);
  }

  const rules = draft.rules.map((rule, index): RuleErrors => ({
    days: rule.days.length === 0,
    time: !validTimePattern.test(normalizeTime(rule.time)),
    duplicate: duplicated.has(index),
  }));
  const overLimit = draft.rules.length > limits.maxSchedules;
  const invalidCount =
    (idleMinutes === null ? 0 : 1) +
    (maxHours === null ? 0 : 1) +
    rules.reduce(
      (count, rule) => count + (rule.days ? 1 : 0) + (rule.time || rule.duplicate ? 1 : 0),
      0,
    );
  return {
    idleMinutes,
    maxHours,
    rules,
    overLimit,
    invalidCount,
    valid: invalidCount === 0 && !overLimit,
  };
}

/**
 * Tells whether a draft rule no longer matches the saved rule it started from.
 *
 * The next-run line of a rule describes the saved rule, so a rule that is new or edited shows "Not saved yet."
 * until the next save.
 *
 * @param rule The draft rule.
 * @param saved The saved rule with the same id, or null when there is none.
 * @returns True when there is no saved rule or its action, days or time differ.
 */
export function ruleDiffersFromSaved(rule: RuleDraft, saved: SavedScheduleInput | null): boolean {
  return saved === null || !sameRule(rule, saved);
}

/**
 * Tells whether the draft differs from the saved settings.
 *
 * The comparison uses normalized values: numbers as parsed integers (a disabled number is ignored), the zone with
 * the browser zone standing in for a missing saved one, and rules by id with sorted day sets and `HH:mm` times.
 * A new rule counts only once it holds a day or a time, and a saved rule that was removed counts as a change.
 *
 * @param draft The editable state.
 * @param saved The settings the draft started from.
 * @param browserZone The viewer's resolved browser time zone.
 * @returns True when saving would change something.
 */
export function isAutomationDraftDirty(
  draft: AutomationDraft,
  saved: SavedAutomationInput,
  browserZone: string,
): boolean {
  if (draft.timeZone !== (saved.timeZone ?? browserZone)) return true;
  if (draft.idleShutdownEnabled !== saved.idleShutdownEnabled) return true;
  if (
    draft.idleShutdownEnabled &&
    parseLooseNumber(draft.idleMinutes) !== saved.idleShutdownMinutes
  ) {
    return true;
  }
  if (draft.maxRuntimeEnabled !== saved.maxRuntimeEnabled) return true;
  if (draft.maxRuntimeEnabled && parseLooseNumber(draft.maxHours) !== saved.maxRuntimeHours) {
    return true;
  }

  const rules = meaningfulRules(draft.rules);
  if (rules.length !== saved.schedules.length) return true;
  const savedById = new Map(saved.schedules.map((rule) => [rule.id, rule]));
  return rules.some((rule) => {
    if (rule.id === null) return true;
    const match = savedById.get(rule.id);
    return match === undefined || !sameRule(rule, match);
  });
}

/**
 * Turns a validated draft into the body of the settings update.
 *
 * A disabled number sends the saved number, so toggling a setting off and on again never loses it. Days go out in
 * ascending order, times as `HH:mm` and new rules with an id of null.
 *
 * @param draft The validated draft.
 * @param saved The settings the draft started from.
 * @param startConsent Whether the viewer confirmed the scheduled starts.
 * @param idleAvailable Whether the game reports players; idle shutdown is never sent as enabled otherwise.
 * @returns The update body.
 */
export function toAutomationUpdate(
  draft: AutomationDraft,
  saved: SavedAutomationInput,
  startConsent: boolean,
  idleAvailable = true,
): AutomationUpdateInput {
  const idleEnabled = idleAvailable && draft.idleShutdownEnabled;
  return {
    version: saved.version,
    idleShutdownEnabled: idleEnabled,
    idleShutdownMinutes: idleEnabled
      ? (parseLooseNumber(draft.idleMinutes) ?? saved.idleShutdownMinutes)
      : saved.idleShutdownMinutes,
    maxRuntimeEnabled: draft.maxRuntimeEnabled,
    maxRuntimeHours: draft.maxRuntimeEnabled
      ? (parseLooseNumber(draft.maxHours) ?? saved.maxRuntimeHours)
      : saved.maxRuntimeHours,
    timeZone: draft.timeZone,
    schedules: draft.rules.map((rule) => ({
      id: rule.id,
      action: rule.action,
      days: normalizeDays(rule.days),
      time: normalizeTime(rule.time),
    })),
    startConsent,
  };
}

/**
 * Tells whether saving the draft needs the scheduled-start cost consent (D-08).
 *
 * That is the case for a start rule that is new, or whose action, days or time differ from the saved rule with the
 * same id. Stop rules and unchanged start rules never need it.
 *
 * @param draft The editable state.
 * @param saved The settings the draft started from.
 * @returns True when a start rule is new or changed.
 */
export function needsStartConsent(draft: AutomationDraft, saved: SavedAutomationInput): boolean {
  const savedById = new Map(saved.schedules.map((rule) => [rule.id, rule]));
  return meaningfulRules(draft.rules).some((rule) => {
    if (rule.action !== "Start") return false;
    if (rule.id === null) return true;
    const match = savedById.get(rule.id);
    return match === undefined || !sameRule(rule, match);
  });
}

/** Sorts after every weekday; stands in for the first day of a rule that has none. */
const noDayRank = 8;

/**
 * Orders rules for display: by the first selected weekday, then by time.
 *
 * Rules keep their entry order while they are edited; the list is shown in this order after a save. The sort is
 * stable, so rules with the same first day and time keep their relative order.
 *
 * @param rules The rules to order.
 * @returns A new array in display order.
 */
export function sortRulesForDisplay<T extends { days: readonly number[]; time: string }>(
  rules: readonly T[],
): T[] {
  const firstDay = (rule: T): number =>
    rule.days.length === 0 ? noDayRank : Math.min(...rule.days);
  return [...rules].sort((left, right) => {
    const byDay = firstDay(left) - firstDay(right);
    if (byDay !== 0) return byDay;
    const leftTime = normalizeTime(left.time);
    const rightTime = normalizeTime(right.time);
    if (leftTime === rightTime) return 0;
    return leftTime < rightTime ? -1 : 1;
  });
}

/** The closed set of idle status sentences of the panel's status list. */
export type IdleStatusKey = "NotAvailable" | "Off" | "Unknown" | "Players" | "Running" | "Armed";

/** The status sentence of every live idle state the API reports. */
const idleStates: ReadonlyMap<string, IdleStatusKey> = new Map<string, IdleStatusKey>([
  ["PlayersOnline", "Players"],
  ["Idle", "Running"],
  ["NotRunning", "Armed"],
]);

/**
 * Chooses the sentence of the idle row of the status list, first match wins.
 *
 * An idle state the table does not hold, including `Unknown` and values a newer API adds, maps to the fixed
 * "player count not available" sentence; API text is never rendered from this field.
 *
 * @param input Whether idle shutdown is available for the game, whether it is on, and the live idle state.
 * @returns The closed key of the sentence.
 */
export function idleStatusKey(input: {
  available: boolean;
  enabled: boolean;
  state: string;
}): IdleStatusKey {
  if (!input.available) return "NotAvailable";
  if (!input.enabled) return "Off";
  return idleStates.get(input.state) ?? "Unknown";
}

/** The closed set of last-run sentences of a rule. */
export type LastRunKey =
  | "None"
  | "Started"
  | "Stopped"
  | "SkippedRunning"
  | "SkippedOff"
  | "SkippedCost"
  | "SkippedConsent"
  | "SkippedMissed"
  | "SkippedBusy"
  | "SkippedStopUnfinished"
  | "WaitingForStop"
  | "Failed"
  | "Other";

/** The last-run sentence of every outcome code the API reports. */
const lastRunOutcomes: ReadonlyMap<string, LastRunKey> = new Map<string, LastRunKey>([
  ["Started", "Started"],
  ["Stopped", "Stopped"],
  ["SkippedRunning", "SkippedRunning"],
  ["SkippedOff", "SkippedOff"],
  ["SkippedCost", "SkippedCost"],
  ["SkippedConsent", "SkippedConsent"],
  ["SkippedMissed", "SkippedMissed"],
  ["SkippedBusy", "SkippedBusy"],
  ["SkippedStopUnfinished", "SkippedStopUnfinished"],
  ["WaitingForStop", "WaitingForStop"],
  ["Failed", "Failed"],
]);

/**
 * Chooses the last-run sentence of a rule.
 *
 * A rule that has not run maps to `None`; an outcome code the table does not hold maps to `Other`, the plain
 * "Last run: {time}" sentence, never to API text.
 *
 * @param lastRun The last run of the rule, or null.
 * @returns The closed key of the sentence.
 */
export function lastRunKey(lastRun: { outcome: string } | null): LastRunKey {
  if (lastRun === null) return "None";
  return lastRunOutcomes.get(lastRun.outcome) ?? "Other";
}

/** The closed set of rule summary sentences. */
export type RuleSummaryKey = "Daily" | "Weekdays" | "Days" | "Incomplete";

/**
 * Chooses the summary sentence of a rule.
 *
 * @param days The selected ISO weekdays.
 * @param time The wall-clock time, or an empty text.
 * @returns `Incomplete` without days or time, `Daily` for all seven days, `Weekdays` for Monday to Friday, else `Days`.
 */
export function ruleSummaryKey(days: readonly number[], time: string): RuleSummaryKey {
  const unique = normalizeDays(days);
  if (unique.length === 0 || time.trim() === "") return "Incomplete";
  if (unique.length === isoWeekdays.length) return "Daily";
  if (unique.join(",") === workdayKey) return "Weekdays";
  return "Days";
}

/** A year whose first of January is a Monday, so day 1 to 7 of that month in UTC names Monday to Sunday. */
const mondayYear = 2024;

/**
 * Names the seven weekdays in a locale, Monday first.
 *
 * @param locale The locale of the names.
 * @param style The name length: `short` for the checkbox labels, `long` for the summary sentence.
 * @returns One entry per ISO weekday, 1 to 7.
 */
export function weekdayLabels(
  locale: string,
  style: "short" | "long",
): { day: number; label: string }[] {
  const formatter = new Intl.DateTimeFormat(locale, { weekday: style, timeZone: "UTC" });
  return isoWeekdays.map((day) => ({
    day,
    label: formatter.format(new Date(Date.UTC(mondayYear, 0, day))),
  }));
}

/** The time zone that is always offered first. */
const utcZone = "UTC";

/** Compares two ids in plain ordinal order. */
function compareOrdinal(left: string, right: string): number {
  if (left === right) return 0;
  return left < right ? -1 : 1;
}

/**
 * Builds the options of the time zone select (D-06).
 *
 * UTC comes first. The other zones are grouped by region, the part of the IANA id before the first `/`, with groups
 * and zones in plain alphabetical order of the id. A label shows the id with underscores replaced by spaces. The
 * saved zone is always present, even when the runtime does not list it.
 *
 * @param supported The zones the runtime supports.
 * @param savedZone The saved zone of the server, or null.
 * @returns The UTC id and the region groups.
 */
export function timeZoneOptions(
  supported: readonly string[],
  savedZone: string | null,
): { utc: string; groups: { region: string; zones: { id: string; label: string }[] }[] } {
  const ids = new Set(supported);
  if (savedZone !== null && savedZone.trim() !== "") ids.add(savedZone);
  ids.delete(utcZone);

  const regions = new Map<string, string[]>();
  for (const id of [...ids].sort(compareOrdinal)) {
    const region = id.split("/")[0] ?? id;
    regions.set(region, [...(regions.get(region) ?? []), id]);
  }
  const groups = [...regions.keys()].sort(compareOrdinal).map((region) => ({
    region,
    zones: (regions.get(region) ?? []).map((id) => ({ id, label: id.replaceAll("_", " ") })),
  }));
  return { utc: utcZone, groups };
}

/**
 * Formats an instant that the control plane sent, in a time zone.
 *
 * This is formatting only: the instant is never changed. An instant or a zone that cannot be formatted gives an
 * empty text, so a damaged value never shows a wrong time.
 *
 * @param instant The ISO instant from the API.
 * @param timeZone The IANA zone to show the instant in.
 * @param locale The locale of the text.
 * @param options The `Intl.DateTimeFormat` options; the zone is set from `timeZone`.
 * @returns The formatted text, or an empty text.
 */
export function formatWallTime(
  instant: string,
  timeZone: string,
  locale: string,
  options: Intl.DateTimeFormatOptions,
): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return "";
  try {
    return new Intl.DateTimeFormat(locale, { ...options, timeZone }).format(date);
  } catch (error) {
    if (error instanceof RangeError) return "";
    throw error;
  }
}
