import type { CostAlertSettings } from "@/features/hosting/types";

/** Why the text of one amount cannot be saved. */
export type AlertAmountError = "Required" | "Number" | "Precision" | "Min" | "Max";

/** Why one row of the draft cannot be saved: an {@link AlertAmountError} or a repeated amount. */
export type AlertRowError = AlertAmountError | "Duplicate";

/** The result of {@link parseAlertAmount}: the amount, or the reason there is none. */
export type AlertAmountParse = { value: number } | { error: AlertAmountError };

/** The limits of a save, as the API reports them with the settings. */
export type AlertLimits = Pick<CostAlertSettings, "maxThresholds" | "maxThresholdAmount">;

/** The part of the settings the draft is compared with. */
export type AlertSavedState = Pick<
  CostAlertSettings,
  "thresholds" | "discordEnabled" | "emailEnabled"
>;

/** The editable state of the settings form: the amount texts in entry order and both channel flags. */
export interface AlertDraft {
  /** The text of every amount row, exactly as typed. */
  rows: readonly string[];
  /** Whether Discord direct messages are switched on. */
  discordEnabled: boolean;
  /** Whether email is switched on. */
  emailEnabled: boolean;
}

/** The verdict of {@link validateAlertDraft}. */
export interface AlertDraftValidation {
  /** One entry per row, in row order: the reason the row is invalid, or null when it can be saved. */
  errors: readonly (AlertRowError | null)[];
  /** True when the draft holds more rows than the API accepts. */
  overLimit: boolean;
  /** True when every row is valid and the row count is within the limit. */
  valid: boolean;
}

/** Digits with at most one decimal separator, `.` or `,`, and an optional leading minus; no grouping. */
const amountPattern = /^(-?)(\d+)(?:[.,](\d+))?$/;

/** The most decimals an amount may carry. */
const maxDecimals = 2;

/**
 * Parses the text of one alert amount the way people type money.
 *
 * The text is trimmed and may use `.` or `,` as its single decimal separator; grouping separators are rejected.
 * The amount must be above 0, carry at most two decimals and be at most `max`. The rules mirror the API, so its
 * 400 answer stays a safety net and not the user experience.
 *
 * @param text The text of the row.
 * @param max The largest accepted amount, from the settings.
 * @returns The amount, or the first rule it breaks: required, number, precision, min or max.
 */
export function parseAlertAmount(text: string, max: number): AlertAmountParse {
  const trimmed = text.trim();
  if (trimmed === "") return { error: "Required" };
  const match = amountPattern.exec(trimmed);
  if (match === null) return { error: "Number" };
  const [, sign = "", whole = "", fraction = ""] = match;
  if (fraction.length > maxDecimals) return { error: "Precision" };
  const value = Number(`${whole}.${fraction === "" ? "0" : fraction}`);
  if (sign === "-" || value <= 0) return { error: "Min" };
  if (value > max) return { error: "Max" };
  return { value };
}

/**
 * Checks every row of a draft against the API's rules.
 *
 * A row repeats an amount when an earlier row parses to the same value; the later row carries the duplicate error,
 * so the first occurrence stays unmarked. Blank rows are invalid, never silently dropped.
 *
 * @param rows The text of every amount row in entry order.
 * @param limits The row and amount limits from the settings.
 * @returns The per-row reasons, whether there are too many rows, and the overall verdict.
 */
export function validateAlertDraft(
  rows: readonly string[],
  limits: AlertLimits,
): AlertDraftValidation {
  const seen = new Set<number>();
  const errors = rows.map((text): AlertRowError | null => {
    const parsed = parseAlertAmount(text, limits.maxThresholdAmount);
    if ("error" in parsed) return parsed.error;
    if (seen.has(parsed.value)) return "Duplicate";
    seen.add(parsed.value);
    return null;
  });
  const overLimit = rows.length > limits.maxThresholds;
  return { errors, overLimit, valid: !overLimit && errors.every((error) => error === null) };
}

/**
 * Turns the valid rows of a draft into the list the API stores: parsed numbers in ascending order.
 *
 * Rows that cannot be parsed are left out here; {@link validateAlertDraft} is what reports them.
 *
 * @param rows The text of every amount row in entry order.
 * @returns The parsed amounts, ascending.
 */
export function normalizeAlertAmounts(rows: readonly string[]): number[] {
  const amounts: number[] = [];
  for (const text of rows) {
    const parsed = parseAlertAmount(text, Number.POSITIVE_INFINITY);
    if ("value" in parsed) amounts.push(parsed.value);
  }
  return amounts.sort((left, right) => left - right);
}

/**
 * Tells whether the draft differs from the saved settings.
 *
 * The comparison uses the normalized ascending amounts and both channel flags, so reordering rows or writing `5,0`
 * for a saved `5` is no change. A blank new row is not a change until it holds a value, and a row that holds text
 * which is not an amount is one.
 *
 * @param draft The editable state.
 * @param saved The settings the draft started from.
 * @returns True when saving would change something.
 */
export function isAlertDraftDirty(draft: AlertDraft, saved: AlertSavedState): boolean {
  if (draft.discordEnabled !== saved.discordEnabled) return true;
  if (draft.emailEnabled !== saved.emailEnabled) return true;
  const filled = draft.rows.filter((text) => text.trim() !== "");
  const amounts = normalizeAlertAmounts(filled);
  if (amounts.length !== filled.length) return true;
  const savedAmounts = [...saved.thresholds].sort((left, right) => left - right);
  return (
    amounts.length !== savedAmounts.length ||
    amounts.some((amount, index) => amount !== savedAmounts[index])
  );
}

/**
 * Formats a saved amount as the text of a row, with the decimal separator of the viewer's language.
 *
 * @param value The amount.
 * @param separator The decimal separator to use.
 * @returns The plain number text, without grouping and with at most the decimals the amount has.
 */
export function formatAlertAmount(value: number, separator: "." | ","): string {
  const text = String(value);
  return separator === "." ? text : text.replace(".", separator);
}

/** The two channels whose reach the settings explain. */
export type AlertChannel = "discord" | "email";

/** Whether a channel can reach the recipients right now, as the API reports it. */
export type AlertChannelAvailability = CostAlertSettings["discord"];

/** Who the alerts of an owner are addressed to, as the API reports it. */
export type AlertAudience = CostAlertSettings["audience"];

/** The locale key of every channel hint sentence. */
export type AlertChannelHintKey =
  | "Server_Alerts_Discord_Self"
  | "Server_Alerts_Discord_Group"
  | "Server_Alerts_Discord_NotLinked"
  | "Server_Alerts_Discord_NoneGroup"
  | "Server_Alerts_Discord_Unavailable"
  | "Server_Alerts_Email_Self"
  | "Server_Alerts_Email_Group"
  | "Server_Alerts_Email_NoAddress"
  | "Server_Alerts_Email_NoneGroup"
  | "Server_Alerts_Email_NotConfigured";

/** The sentence shown when no lookup entry matches; it only says that nothing can be sent right now. */
const channelHintFallback: Record<AlertChannel, AlertChannelHintKey> = {
  discord: "Server_Alerts_Discord_Unavailable",
  email: "Server_Alerts_Email_NotConfigured",
};

/** The hint sentence of every channel, availability and audience the API can report together. */
const channelHints: ReadonlyMap<string, AlertChannelHintKey> = new Map<string, AlertChannelHintKey>(
  [
    ["discord:Deliverable:Self", "Server_Alerts_Discord_Self"],
    ["discord:Deliverable:Group", "Server_Alerts_Discord_Group"],
    ["discord:NotLinked:Self", "Server_Alerts_Discord_NotLinked"],
    ["discord:NotLinked:Group", "Server_Alerts_Discord_NoneGroup"],
    ["discord:ChannelUnavailable:Self", "Server_Alerts_Discord_Unavailable"],
    ["discord:ChannelUnavailable:Group", "Server_Alerts_Discord_Unavailable"],
    ["email:Deliverable:Self", "Server_Alerts_Email_Self"],
    ["email:Deliverable:Group", "Server_Alerts_Email_Group"],
    ["email:NoAddress:Self", "Server_Alerts_Email_NoAddress"],
    ["email:NoAddress:Group", "Server_Alerts_Email_NoneGroup"],
    ["email:ChannelUnavailable:Self", "Server_Alerts_Email_NotConfigured"],
    ["email:ChannelUnavailable:Group", "Server_Alerts_Email_NotConfigured"],
  ],
);

/**
 * Chooses the one sentence that tells an owner whether a channel can deliver right now.
 *
 * The sentence depends on the availability the API reports and on whether the alerts go to one person or to the
 * group of people who manage the owner. A combination the table does not hold falls back to the plain "nothing
 * can be sent" sentence of the channel, so a future availability value never leaves the channel without a hint.
 *
 * @param channel The channel the hint belongs to.
 * @param availability The availability of that channel for the current recipients.
 * @param audience Whether the alerts go to the owner alone or to a group.
 * @returns The locale key of the hint sentence.
 */
export function channelHintKey(
  channel: AlertChannel,
  availability: AlertChannelAvailability,
  audience: AlertAudience,
): AlertChannelHintKey {
  return channelHints.get(`${channel}:${availability}:${audience}`) ?? channelHintFallback[channel];
}

/** Every channel a settings row can show, the always-on in-app channel included. */
export type AlertChannelRow = "inApp" | AlertChannel;

/** The locale key of every channel status badge label. */
export type AlertChannelStatusKey =
  | "Server_Alerts_Channel_Status_AlwaysOn"
  | "Server_Alerts_Channel_Status_Ready"
  | "Server_Alerts_Channel_Status_NotLinked"
  | "Server_Alerts_Channel_Status_NoAddress"
  | "Server_Alerts_Channel_Status_Unavailable";

/** The badge a channel row shows: the tone that marks it and the locale key of its one-word label. */
export interface AlertChannelStatus {
  /** The badge tone: success when the channel can deliver, warning when the owner can fix it, else neutral. */
  tone: "success" | "warning" | "neutral";
  /** The locale key of the badge label. */
  key: AlertChannelStatusKey;
}

/** The badge of the always-on in-app channel. */
const inAppStatus: AlertChannelStatus = {
  tone: "success",
  key: "Server_Alerts_Channel_Status_AlwaysOn",
};

/** The badge shown for a channel that cannot be reached for a reason the owner cannot fix, and for unknown values. */
const unavailableStatus: AlertChannelStatus = {
  tone: "neutral",
  key: "Server_Alerts_Channel_Status_Unavailable",
};

/** The badge of every availability the API can report for the Discord and email channels. */
const channelStatuses: ReadonlyMap<string, AlertChannelStatus> = new Map<
  string,
  AlertChannelStatus
>([
  ["Deliverable", { tone: "success", key: "Server_Alerts_Channel_Status_Ready" }],
  ["NotLinked", { tone: "warning", key: "Server_Alerts_Channel_Status_NotLinked" }],
  ["NoAddress", { tone: "warning", key: "Server_Alerts_Channel_Status_NoAddress" }],
  ["ChannelUnavailable", unavailableStatus],
]);

/**
 * Chooses the status badge of a channel row, so the owner sees at a glance which channels can deliver.
 *
 * The in-app channel is always on. For Discord and email the badge follows the availability the API reports; a
 * value the table does not hold falls back to the neutral "Not available" badge, so a future availability value
 * never leaves the row without a status.
 *
 * @param channel The channel the row belongs to.
 * @param availability The availability of that channel for the current recipients; ignored for the in-app channel.
 * @returns The badge tone and the locale key of its label.
 */
export function channelStatus(
  channel: AlertChannelRow,
  availability?: AlertChannelAvailability,
): AlertChannelStatus {
  if (channel === "inApp") return inAppStatus;
  return channelStatuses.get(availability ?? "") ?? unavailableStatus;
}
