import type { UpcomingEvent } from "@/api/types";

export type UpcomingEventFilter = "all" | "confirmed" | "needs-signups" | "open";

export function normalizeEventAcceptance(
  value: UpcomingEvent["metaAcceptance"],
): "Rejected" | "Maybe" | "Accepted" | undefined {
  switch (value) {
    case -1:
    case "Rejected":
      return "Rejected";
    case 0:
    case "Maybe":
      return "Maybe";
    case 1:
    case "Accepted":
      return "Accepted";
    default:
      return undefined;
  }
}

export function eventNeedsSignups(event: UpcomingEvent): boolean {
  return event.minimumAccepted > 0 && event.acceptedCount < event.minimumAccepted;
}

export function matchesUpcomingEventFilter(
  event: UpcomingEvent,
  filter: UpcomingEventFilter,
): boolean {
  if (filter === "needs-signups") return eventNeedsSignups(event);
  const acceptance = normalizeEventAcceptance(event.metaAcceptance);
  if (filter === "confirmed") return acceptance === "Accepted";
  if (filter === "open") return acceptance !== "Rejected";
  return true;
}

export function formatEventSlotCount(event: UpcomingEvent): string {
  return event.minimumAccepted > 0
    ? `${event.acceptedCount} / ${event.minimumAccepted}`
    : String(event.acceptedCount);
}

export function utcDateKey(value: string | Date): string | undefined {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return undefined;
  return [
    date.getUTCFullYear(),
    String(date.getUTCMonth() + 1).padStart(2, "0"),
    String(date.getUTCDate()).padStart(2, "0"),
  ].join("-");
}
