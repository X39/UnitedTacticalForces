import { currentLocale } from "@/app/format";
import { formatMoney } from "@/features/hosting/model";
import type { CostAlert } from "@/features/hosting/types";

/**
 * Formats the instant the API says a value was true as an absolute clock time in the current locale, with the
 * short date added when the instant is not today. The time is never relative and never ticks.
 *
 * @param instant The ISO instant from the API.
 * @returns The localized time text; an unreadable instant yields an empty string.
 */
export function formatInstant(instant: string): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return "";
  const today = new Date();
  const sameDay =
    date.getFullYear() === today.getFullYear() &&
    date.getMonth() === today.getMonth() &&
    date.getDate() === today.getDate();
  return new Intl.DateTimeFormat(
    currentLocale(),
    sameDay ? { timeStyle: "short" } : { dateStyle: "short", timeStyle: "short" },
  ).format(date);
}

/**
 * Formats the threshold an alert was raised for in the currency of the alert.
 *
 * @param alert The alert.
 * @returns The localized money text, for example "5.00 EUR" in the style of the current locale.
 */
export function formatAlertThreshold(alert: CostAlert): string {
  return formatMoney(alert.thresholdAmount, alert.currency);
}

/**
 * Formats the amount the session had accrued when an alert was raised.
 *
 * @param alert The alert.
 * @returns The localized money text.
 */
export function formatAlertAmount(alert: CostAlert): string {
  return formatMoney(alert.accruedAmount, alert.currency);
}

/**
 * Returns the alerts of one server, keeping the newest-first order of the list.
 *
 * @param alerts The alerts of the viewer.
 * @param serverId The id of the server.
 */
export function alertsOfServer(alerts: readonly CostAlert[], serverId: string): CostAlert[] {
  return alerts.filter((alert) => alert.serverId === serverId);
}

/**
 * Inserts an alert into a list that is ordered newest first. An alert whose id is already listed changes nothing.
 *
 * @param alerts The current list, newest first.
 * @param alert The alert to add.
 * @returns The same list when the id is listed, otherwise a new list with the alert at its place by `createdAt`.
 */
export function insertAlert(alerts: readonly CostAlert[], alert: CostAlert): readonly CostAlert[] {
  if (alerts.some((existing) => existing.id === alert.id)) return alerts;
  return [...alerts, alert]
    .map((entry, index) => ({ entry, index, time: Date.parse(entry.createdAt) }))
    .sort((left, right) => right.time - left.time || left.index - right.index)
    .map(({ entry }) => entry);
}
