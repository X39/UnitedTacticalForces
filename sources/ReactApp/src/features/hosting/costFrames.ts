import type { UpdateStreamFrame } from "@/app/updateStream";
import type { CostAlert, GameServerAccruedCost } from "@/features/hosting/types";

/** Full type name of the .NET wire message that pushes the accrued cost of one costing session. */
export const accruedCostMessageType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerAccruedCostMessage";

/** Full type name of the .NET wire message that pushes one newly raised cost alert to its viewers. */
export const costAlertRaisedMessageType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.CostAlertRaisedMessage";

/** Full type name of the .NET wire message that tells the dismissing viewer's other tabs an alert was dismissed. */
export const costAlertDismissedMessageType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.CostAlertDismissedMessage";

/** The result of parsing one accrued cost frame: the server it belongs to and the value to cache. */
export interface ParsedAccruedCostFrame {
  /** The id of the server the value belongs to. */
  serverId: string;
  /** The value, shaped exactly like the REST response so it can be written into the same query cache entry. */
  accrued: GameServerAccruedCost;
}

/**
 * Returns whether a value is a non-empty string.
 *
 * @param value The untrusted value.
 */
function isText(value: unknown): value is string {
  return typeof value === "string" && value !== "";
}

/**
 * Returns whether a value is a positive whole number.
 *
 * @param value The untrusted value.
 */
function isPositiveInteger(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value > 0;
}

/**
 * Returns whether a value is a string that parses as an instant.
 *
 * @param value The untrusted value.
 */
function isInstant(value: unknown): value is string {
  return typeof value === "string" && !Number.isNaN(Date.parse(value));
}

/**
 * Validates one untrusted accrued cost frame and converts it into the REST shape.
 *
 * Every field is checked: string server and deployment ids, a finite number or null amount, a string currency, a
 * parseable as-of instant, a boolean final flag and a string or null reason. The billing facts are a positive
 * whole number of seconds or null and a parseable instant or null; an absent one reads as null and a wrong type
 * drops the frame. Extra properties, such as the reference ids the stream serializer adds, are ignored. A frame
 * that fails any check is dropped, because the browser must never show a value it cannot trust.
 *
 * @param frame The parsed, untrusted frame.
 * @returns The server id and the value, or null when the frame is not a valid accrued cost frame.
 */
export function parseAccruedCostFrame(frame: UpdateStreamFrame): ParsedAccruedCostFrame | null {
  if (frame.type !== accruedCostMessageType) return null;
  const value: unknown = frame.value;
  if (typeof value !== "object" || value === null) return null;
  const message = value as Record<string, unknown>;
  const {
    serverId,
    deploymentId,
    amount,
    currency,
    asOf,
    isFinal,
    unavailableReason,
    billingUnitSeconds,
    billedThrough,
  } = message;
  if (!isText(serverId) || !isText(deploymentId) || !isText(currency) || !isText(asOf)) return null;
  if (Number.isNaN(Date.parse(asOf))) return null;
  if (amount !== null && (typeof amount !== "number" || !Number.isFinite(amount))) return null;
  if (typeof isFinal !== "boolean") return null;
  if (unavailableReason !== null && unavailableReason !== undefined) {
    if (typeof unavailableReason !== "string") return null;
  }
  const reason = typeof unavailableReason === "string" ? unavailableReason : null;
  const unitSeconds = billingUnitSeconds ?? null;
  if (unitSeconds !== null && !isPositiveInteger(unitSeconds)) return null;
  const throughInstant = billedThrough ?? null;
  if (throughInstant !== null && !isInstant(throughInstant)) return null;
  return {
    serverId,
    accrued: {
      gameServerId: serverId,
      session: {
        deploymentId,
        amount,
        currency,
        asOf,
        isFinal,
        unavailableReason: reason,
        billingUnitSeconds: unitSeconds,
        billedThrough: throughInstant,
      },
    },
  };
}

/**
 * Returns whether a value is a finite number.
 *
 * @param value The untrusted value.
 */
function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

/**
 * Validates one untrusted cost alert frame and converts it into the REST alert shape.
 *
 * Every field is checked: string alert, server and cost owner ids, a string server name, finite threshold and
 * accrued amounts, a string currency and a parseable creation instant. The automatic shutdown members are optional:
 * a frame without a kind is a threshold alert exactly as before, and a frame with a kind may carry a parseable
 * stop instant, a string stop reason and a finite stop limit; a present but malformed member drops the frame.
 * Extra properties are ignored. A frame that fails any check is dropped, because the browser must never show an
 * alert it cannot trust.
 *
 * @param frame The parsed, untrusted frame.
 * @returns The alert, or null when the frame is not a valid raised-alert frame.
 */
export function parseCostAlertRaisedFrame(frame: UpdateStreamFrame): CostAlert | null {
  if (frame.type !== costAlertRaisedMessageType) return null;
  const value: unknown = frame.value;
  if (typeof value !== "object" || value === null) return null;
  const message = value as Record<string, unknown>;
  const {
    alertId,
    serverId,
    serverName,
    costOwnerId,
    thresholdAmount,
    accruedAmount,
    currency,
    createdAt,
    kind,
    stopsAt,
    stopReason,
    stopLimit,
  } = message;
  if (!isText(alertId) || !isText(serverId) || !isText(costOwnerId)) return null;
  if (typeof serverName !== "string" || !isText(currency) || !isText(createdAt)) return null;
  if (!isFiniteNumber(thresholdAmount) || !isFiniteNumber(accruedAmount)) return null;
  if (Number.isNaN(Date.parse(createdAt))) return null;
  const alert: CostAlert = {
    id: alertId,
    serverId,
    serverName,
    costOwnerId,
    thresholdAmount,
    accruedAmount,
    currency,
    createdAt,
  };
  if (kind === undefined || kind === null) return alert;
  if (!isText(kind)) return null;
  const stopInstant = stopsAt ?? null;
  if (stopInstant !== null && !isInstant(stopInstant)) return null;
  const reason = stopReason ?? null;
  if (reason !== null && !isText(reason)) return null;
  const limit = stopLimit ?? null;
  if (limit !== null && !isFiniteNumber(limit)) return null;
  return { ...alert, kind, stopsAt: stopInstant, stopReason: reason, stopLimit: limit };
}

/**
 * Validates one untrusted alert-dismissed frame.
 *
 * @param frame The parsed, untrusted frame.
 * @returns The id of the dismissed alert, or null when the frame is not a valid alert-dismissed frame.
 */
export function parseCostAlertDismissedFrame(frame: UpdateStreamFrame): string | null {
  if (frame.type !== costAlertDismissedMessageType) return null;
  const value: unknown = frame.value;
  if (typeof value !== "object" || value === null) return null;
  const { alertId } = value as Record<string, unknown>;
  return isText(alertId) ? alertId : null;
}
