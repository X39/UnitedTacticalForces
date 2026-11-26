import { useId, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { StatePanel } from "@/components/primitives";
import "@/components/server/activity.css";
import {
  groupEntriesByDay,
  type ActivityDay,
  type TimelineEntry,
} from "@/features/hosting/activityModel";

/** The props of {@link ActivityTimeline}. */
export interface ActivityTimelineProps {
  /** The entries to list, normally newest first; the timeline groups them by local calendar day. */
  entries: readonly TimelineEntry[];
  /** The current time in epoch milliseconds, used to decide which day is today and yesterday. */
  now: number;
  /** The oldest loaded audit time when older platform events are not loaded, otherwise null. */
  windowBoundary: string | null;
  /** Whether a background refetch is running; the content stays in place and the list is marked busy. */
  busy: boolean;
  /**
   * Renders one entry.
   *
   * @param entry The entry to render.
   * @returns The entry element; the section uses it to control expansion.
   */
  renderEntry(entry: TimelineEntry): ReactNode;
}

/**
 * Formats the heading of a dated day group: the weekday, day and month, with the year when it is not this year.
 *
 * @param day The dated day of the group.
 * @returns The heading in the current locale.
 */
function formatDayHeading(day: Extract<ActivityDay, { kind: "date" }>): string {
  return new Intl.DateTimeFormat(currentLocale(), {
    weekday: "long",
    day: "numeric",
    month: "long",
    ...(day.sameYear ? {} : { year: "numeric" }),
  }).format(day.date);
}

/**
 * Returns the id of the first entry that is older than the audit window, or null when there is none.
 *
 * @param entries The listed entries, newest first.
 * @param boundary The oldest loaded audit time, or null when the audit window is complete.
 */
function firstEntryBeyondWindow(
  entries: readonly TimelineEntry[],
  boundary: string | null,
): string | null {
  if (boundary === null) return null;
  const limit = Date.parse(boundary);
  if (Number.isNaN(limit)) return null;
  return entries.find((entry) => Date.parse(entry.at) < limit)?.id ?? null;
}

/**
 * Lists activity entries grouped by local calendar day, newest day first.
 *
 * Each day is a labelled section with an ordered list. When the audit window is shorter than the job window, an
 * info note is inserted directly before the first entry that is older than the oldest audit record. While a
 * background refetch runs the container carries `aria-busy`, so the content never flashes.
 *
 * @param props See {@link ActivityTimelineProps}.
 */
export function ActivityTimeline({
  entries,
  now,
  windowBoundary,
  busy,
  renderEntry,
}: ActivityTimelineProps) {
  const { t } = useTranslation();
  const baseId = useId();
  const noteBeforeId = firstEntryBeyondWindow(entries, windowBoundary);
  const groups = groupEntriesByDay(entries, now);

  const renderItems = (groupEntries: readonly TimelineEntry[]) =>
    groupEntries.map((entry) => (
      <li key={entry.id} className="activity-timeline__item">
        {entry.id === noteBeforeId && (
          <div className="activity-timeline__note">
            <StatePanel kind="info">{t("Server_Activity_WindowNote")}</StatePanel>
          </div>
        )}
        {renderEntry(entry)}
      </li>
    ));

  return (
    <div className="activity-timeline" aria-busy={busy}>
      {groups.map((group) => {
        const headingId = `${baseId}-${group.key}`;
        if (group.key === "unknown") {
          return (
            <div key={group.key} className="activity-timeline__group">
              <ol className="activity-timeline__list">{renderItems(group.entries)}</ol>
            </div>
          );
        }
        let heading: string;
        if (group.day.kind === "today") heading = t("Server_Activity_Today");
        else if (group.day.kind === "yesterday") heading = t("Server_Activity_Yesterday");
        else heading = formatDayHeading(group.day);
        return (
          <section key={group.key} className="activity-timeline__group" aria-labelledby={headingId}>
            <h3 id={headingId} className="activity-timeline__heading">
              {heading}
            </h3>
            <ol className="activity-timeline__list">{renderItems(group.entries)}</ol>
          </section>
        );
      })}
    </div>
  );
}
