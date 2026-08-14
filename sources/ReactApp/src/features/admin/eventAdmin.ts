import { getApiClient } from "@/api/client";
import { apiPath, apiPathWithQuery } from "@/api/contract";
import type { components } from "@/generated/api";

export type EventAdminFilter = "all" | "upcoming" | "needs-slots" | "hidden" | "archived";
export type EventAdminState = "hidden" | "archived" | "low-slots" | "ready";
export type PlainEvent = components["schemas"]["PlainEventDto"];
export type EventAdminRowDto = components["schemas"]["CommandCenterEventAdminEventDto"];
type EventAdminSnapshotDto = components["schemas"]["CommandCenterEventAdminSnapshotDto"];
type UpcomingEvent = components["schemas"]["UpcomingEventDto"];

export interface EventAdminRow extends Omit<EventAdminRowDto, "plainEvent"> {
  plainEvent: PlainEvent;
}

export interface EventAdminSnapshot {
  eventCount: number;
  events: EventAdminRow[];
  upcomingEvents: UpcomingEvent[];
}

export function normalizeEventAdminSnapshot(
  snapshot: EventAdminSnapshotDto | undefined,
): EventAdminSnapshot {
  return {
    eventCount: snapshot?.eventCount ?? 0,
    events: (snapshot?.events ?? [])
      .filter((row): row is EventAdminRowDto & { plainEvent: PlainEvent } =>
        Boolean(row.plainEvent),
      )
      .map((row) => ({ ...row, plainEvent: row.plainEvent })),
    upcomingEvents: snapshot?.upcomingEvents ?? [],
  };
}

export async function loadEventAdminSnapshot(
  take: number,
  hostedByMeOnly: boolean,
  signal?: AbortSignal,
): Promise<EventAdminSnapshot> {
  const query = new URLSearchParams({
    skip: "0",
    take: String(take),
    hostedByMeOnly: String(hostedByMeOnly),
  });
  const snapshot = await getApiClient().request<EventAdminSnapshotDto>(
    apiPathWithQuery(apiPath("/command-center/event-admin-snapshot"), query.toString()),
    { signal },
  );
  return normalizeEventAdminSnapshot(snapshot);
}

function scheduledTime(event: PlainEvent): number | undefined {
  if (!event.scheduledFor) return undefined;
  const value = Date.parse(event.scheduledFor);
  return Number.isNaN(value) ? undefined : value;
}

export function isUpcomingEvent(event: PlainEvent, now = Date.now()): boolean {
  const scheduled = scheduledTime(event);
  return scheduled !== undefined && scheduled >= now;
}

export function isArchivedEvent(event: PlainEvent, now = Date.now()): boolean {
  const scheduled = scheduledTime(event);
  return scheduled !== undefined && scheduled < now;
}

export function isUnderMinimum(event: PlainEvent): boolean {
  const minimum = event.minimumAccepted ?? 0;
  return minimum > 0 && (event.acceptedCount ?? 0) < minimum;
}

export function slotReadinessPercentage(event: PlainEvent): number {
  const minimum = event.minimumAccepted ?? 0;
  if (minimum <= 0) return (event.acceptedCount ?? 0) > 0 ? 100 : 0;
  return Math.max(0, Math.min(100, ((event.acceptedCount ?? 0) * 100) / minimum));
}

export function eventAdminState(event: PlainEvent, now = Date.now()): EventAdminState {
  if (event.isVisible === false) return "hidden";
  if (isArchivedEvent(event, now)) return "archived";
  return isUnderMinimum(event) ? "low-slots" : "ready";
}

export function eventMatchesFilter(
  event: PlainEvent,
  filter: EventAdminFilter,
  now = Date.now(),
): boolean {
  switch (filter) {
    case "upcoming":
      return isUpcomingEvent(event, now);
    case "needs-slots":
      return isUnderMinimum(event);
    case "hidden":
      return event.isVisible === false;
    case "archived":
      return isArchivedEvent(event, now);
    default:
      return true;
  }
}

export function eventMatchesSearch(
  row: EventAdminRow,
  search: string,
  localizedStatus: string,
): boolean {
  const query = search.trim().toLocaleLowerCase();
  if (!query) return true;
  return [
    row.plainEvent.title,
    row.plainEvent.primaryKey,
    localizedStatus,
    row.hostedBy?.nickname,
    row.owner?.nickname,
    row.terrain?.title,
    row.modPackRevision?.definitionTitle,
  ].some((value) => value?.toLocaleLowerCase().includes(query));
}

export function sortEventAdminRows(rows: EventAdminRow[], now = Date.now()): EventAdminRow[] {
  return [...rows].sort((left, right) => {
    const upcomingOrder =
      Number(isUpcomingEvent(right.plainEvent, now)) -
      Number(isUpcomingEvent(left.plainEvent, now));
    if (upcomingOrder) return upcomingOrder;
    const leftTime = scheduledTime(left.plainEvent) ?? Number.MAX_SAFE_INTEGER;
    const rightTime = scheduledTime(right.plainEvent) ?? Number.MAX_SAFE_INTEGER;
    return (
      leftTime - rightTime ||
      (left.plainEvent.title ?? "").localeCompare(right.plainEvent.title ?? "")
    );
  });
}
