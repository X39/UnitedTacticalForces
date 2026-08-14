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

export function plainText(markdown: string, maximumLength = 180): string {
  const text = markdown
    .replace(/[#*_>`~[\]()!-]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
  return text.length > maximumLength ? `${text.slice(0, maximumLength - 1)}…` : text;
}
