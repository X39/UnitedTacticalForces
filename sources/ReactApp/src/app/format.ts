import i18n from "@/app/i18n";

export function currentLocale(): string {
  return i18n.resolvedLanguage?.startsWith("de") ? "de-DE" : "en-US";
}

export function formatDate(
  value: string | undefined,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(currentLocale(), options ?? { dateStyle: "medium" }).format(date);
}

export function formatDateTime(value: string | undefined): string {
  return formatDate(value, { dateStyle: "full", timeStyle: "short" });
}

export function formatUtcDateTime(value: string | undefined): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return `${new Intl.DateTimeFormat(currentLocale(), {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "UTC",
  }).format(date)} UTC`;
}

export function formatUtcDate(
  value: string | undefined,
  options: Intl.DateTimeFormatOptions = { dateStyle: "medium" },
): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(currentLocale(), { ...options, timeZone: "UTC" }).format(date);
}

export function formatCurrency(value: number | null | undefined, currency = "EUR"): string {
  if (value == null) return "—";
  return new Intl.NumberFormat(currentLocale(), { style: "currency", currency }).format(value);
}

/**
 * Formats a point in time relative to now, for example "4 minutes ago" or "vor 4 Minuten".
 *
 * The unit is seconds below one minute, minutes below one hour, hours below one day and days otherwise; past values
 * are negative. The locale is the app's current locale.
 *
 * @param value The point in time as an ISO string, epoch milliseconds or Date.
 * @param now The reference time in epoch milliseconds, supplied by the caller so renders stay deterministic.
 * @returns The relative text, or an empty string when the value cannot be parsed.
 */
export function formatRelativeTime(value: string | number | Date, now: number): string {
  const time = value instanceof Date ? value.getTime() : new Date(value).getTime();
  if (Number.isNaN(time)) return "";
  const deltaSeconds = Math.round((time - now) / 1000);
  const magnitude = Math.abs(deltaSeconds);
  const formatter = new Intl.RelativeTimeFormat(currentLocale(), { numeric: "auto" });
  if (magnitude < 60) return formatter.format(deltaSeconds, "second");
  if (magnitude < 3600) return formatter.format(Math.round(deltaSeconds / 60), "minute");
  if (magnitude < 86_400) return formatter.format(Math.round(deltaSeconds / 3600), "hour");
  return formatter.format(Math.round(deltaSeconds / 86_400), "day");
}

export function plainText(markdown: string, maximumLength = 180): string {
  const text = markdown
    .replace(/[#*_>`~[\]()!-]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
  return text.length > maximumLength ? `${text.slice(0, maximumLength - 1)}…` : text;
}
