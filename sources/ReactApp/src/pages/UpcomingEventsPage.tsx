import {
  ArrowSync20Regular,
  CalendarMonth24Regular,
  ChevronLeft20Regular,
  ChevronRight20Regular,
  Grid24Regular,
} from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { useMemo, useState, type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { eventService, queryKeys } from "@/api/services";
import type { UpcomingEvent } from "@/api/types";
import { currentLocale, plainText } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useSiteImage } from "@/app/siteImages";
import {
  eventAttendance,
  eventReadiness,
  eventSlotProgress,
  formatEventSlotCount,
  matchesUpcomingEventFilter,
  utcDateKey,
  type UpcomingEventFilter,
} from "@/app/upcomingEvents";
import { StatePanel, StatusBadge } from "@/components/primitives";
import "./publicEvents.css";

type ViewMode = "board" | "calendar";
const preferenceKey = "UpcomingOperations.ViewMode";

interface CalendarDay {
  date: Date;
  key: string;
  inMonth: boolean;
  isToday: boolean;
  events: UpcomingEvent[];
}

function getInitialView(): ViewMode {
  const queryMode = new URLSearchParams(window.location.search).get("mode")?.toLocaleLowerCase();
  if (queryMode === "board" || queryMode === "calendar") {
    localStorage.setItem(preferenceKey, queryMode);
    return queryMode;
  }
  return localStorage.getItem(preferenceKey)?.toLocaleLowerCase() === "board"
    ? "board"
    : "calendar";
}

function currentUtcMonth(): Date {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
}

function moveUtcMonth(month: Date, offset: number): Date {
  return new Date(Date.UTC(month.getUTCFullYear(), month.getUTCMonth() + offset, 1));
}

function isInMonth(dateKey: string | undefined, month: Date): boolean {
  return (
    dateKey?.startsWith(
      `${month.getUTCFullYear()}-${String(month.getUTCMonth() + 1).padStart(2, "0")}-`,
    ) ?? false
  );
}

function formatUtc(value: string | Date, options: Intl.DateTimeFormatOptions): string {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return typeof value === "string" ? value : "—";
  return new Intl.DateTimeFormat(currentLocale(), { ...options, timeZone: "UTC" }).format(date);
}

function monthLabel(month: Date): string {
  return formatUtc(month, { month: "long", year: "numeric" });
}

function dateLabel(value: string | Date): string {
  return formatUtc(value, { weekday: "long", day: "2-digit", month: "long", year: "numeric" });
}

function eventTime(value: string): string {
  return `${formatUtc(value, { hour: "2-digit", minute: "2-digit", hour12: false })} UTC`;
}

function buildCalendarDays(month: Date, events: UpcomingEvent[]): CalendarDay[] {
  const mondayOffset = (month.getUTCDay() + 6) % 7;
  const firstDay = new Date(
    Date.UTC(month.getUTCFullYear(), month.getUTCMonth(), 1 - mondayOffset),
  );
  const eventsByDate = new Map<string, UpcomingEvent[]>();
  for (const event of events) {
    const key = utcDateKey(event.scheduledFor);
    if (key) eventsByDate.set(key, [...(eventsByDate.get(key) ?? []), event]);
  }
  const today = utcDateKey(new Date());
  return Array.from({ length: 42 }, (_, index) => {
    const date = new Date(firstDay);
    date.setUTCDate(firstDay.getUTCDate() + index);
    const key = utcDateKey(date)!;
    return {
      date,
      key,
      inMonth:
        date.getUTCFullYear() === month.getUTCFullYear() &&
        date.getUTCMonth() === month.getUTCMonth(),
      isToday: key === today,
      events: eventsByDate.get(key) ?? [],
    };
  });
}

function buildWeekStrip(selectedDate: string, events: UpcomingEvent[]): CalendarDay[] {
  const selected = new Date(`${selectedDate}T00:00:00Z`);
  const mondayOffset = (selected.getUTCDay() + 6) % 7;
  const monday = new Date(selected);
  monday.setUTCDate(selected.getUTCDate() - mondayOffset);
  const today = utcDateKey(new Date());
  return Array.from({ length: 7 }, (_, index) => {
    const date = new Date(monday);
    date.setUTCDate(monday.getUTCDate() + index);
    const key = utcDateKey(date)!;
    return {
      date,
      key,
      inMonth: true,
      isToday: key === today,
      events: events.filter((event) => utcDateKey(event.scheduledFor) === key),
    };
  });
}

function monthSummary(count: number, t: TFunction): string {
  if (count === 0) return t("Pages_Events_Upcoming_CountNoScheduled");
  if (count === 1) return t("Pages_Events_Upcoming_CountOneScheduled");
  return formatResource(t("Pages_Events_Upcoming_CountScheduled"), count);
}

function readinessLabel(event: UpcomingEvent, t: TFunction): string {
  return eventReadiness(event) === "needs-signups"
    ? t("Pages_Events_Upcoming_StatusLowSlots")
    : t("Pages_Events_Upcoming_StatusConfirmed");
}

function attendanceLabel(event: UpcomingEvent, t: TFunction): string {
  switch (eventAttendance(event)) {
    case "Accepted":
      return t("Pages_Public_Home_UserStatusConfirmed");
    case "Rejected":
      return t("Pages_Public_Home_UserStatusDeclined");
    case "Maybe":
      return t("Pages_Public_Home_UserStatusMaybe");
    default:
      return t("Pages_Public_Home_UserStatusUnassigned");
  }
}

function attendanceTone(event: UpcomingEvent): "success" | "danger" | "warning" | "neutral" {
  switch (eventAttendance(event)) {
    case "Accepted":
      return "success";
    case "Rejected":
      return "danger";
    case "Maybe":
      return "warning";
    default:
      return "neutral";
  }
}

function ReadinessBadge({ event }: { event: UpcomingEvent }) {
  const { t } = useTranslation();
  return (
    <StatusBadge tone={eventReadiness(event) === "needs-signups" ? "warning" : "success"}>
      {readinessLabel(event, t)}
    </StatusBadge>
  );
}

function AttendanceBadge({ event }: { event: UpcomingEvent }) {
  const { t } = useTranslation();
  return <StatusBadge tone={attendanceTone(event)}>{attendanceLabel(event, t)}</StatusBadge>;
}

export function Component() {
  const { t } = useTranslation();
  const heroImage = useSiteImage(
    "home-operation-hero",
    "/media/generated/operations-convoy-hero.jpg",
  );
  const cardImage = useSiteImage(
    "home-operation-card",
    "/media/generated/mission-overwatch-banner.jpg",
  );
  const query = useQuery({
    queryKey: queryKeys.upcomingEvents,
    queryFn: ({ signal }) => eventService.upcoming(signal),
  });
  const [view, setView] = useState<ViewMode>(getInitialView);
  const [filter, setFilter] = useState<UpcomingEventFilter>("all");
  const [displayedMonth, setDisplayedMonth] = useState(currentUtcMonth);
  const [selectedDate, setSelectedDate] = useState<string>();
  const [selectedEventId, setSelectedEventId] = useState<string>();

  const sortedEvents = useMemo(
    () =>
      [...(query.data ?? [])].sort(
        (left, right) =>
          new Date(left.scheduledFor).getTime() - new Date(right.scheduledFor).getTime(),
      ),
    [query.data],
  );
  const boardEvents = useMemo(
    () => sortedEvents.filter((event) => matchesUpcomingEventFilter(event, filter)),
    [filter, sortedEvents],
  );
  const displayedEvents = useMemo(
    () => sortedEvents.filter((event) => isInMonth(utcDateKey(event.scheduledFor), displayedMonth)),
    [displayedMonth, sortedEvents],
  );
  const effectiveSelectedDate =
    selectedDate && isInMonth(selectedDate, displayedMonth)
      ? selectedDate
      : utcDateKey(displayedEvents[0]?.scheduledFor ?? displayedMonth)!;
  const selectedEvent =
    displayedEvents.find((event) => event.primaryKey === selectedEventId) ??
    displayedEvents.find((event) => utcDateKey(event.scheduledFor) === effectiveSelectedDate) ??
    displayedEvents[0];
  const calendarDays = useMemo(
    () => buildCalendarDays(displayedMonth, displayedEvents),
    [displayedEvents, displayedMonth],
  );
  const weekdayLabels = Array.from({ length: 7 }, (_, index) =>
    formatUtc(new Date(Date.UTC(2024, 0, 1 + index)), { weekday: "short" }),
  );
  const agendaGroups = useMemo(() => {
    const groups = new Map<string, UpcomingEvent[]>();
    for (const event of displayedEvents) {
      const key = utcDateKey(event.scheduledFor)!;
      groups.set(key, [...(groups.get(key) ?? []), event]);
    }
    return [...groups.entries()];
  }, [displayedEvents]);

  const setViewMode = (mode: ViewMode) => {
    setView(mode);
    localStorage.setItem(preferenceKey, mode);
  };
  const showMonth = (offset: number) => {
    setDisplayedMonth((month) => moveUtcMonth(month, offset));
    setSelectedDate(undefined);
    setSelectedEventId(undefined);
  };
  const selectDate = (date: string) => {
    setSelectedDate(date);
    setSelectedEventId(
      displayedEvents.find((event) => utcDateKey(event.scheduledFor) === date)?.primaryKey,
    );
  };
  const selectEvent = (event: UpcomingEvent) => {
    setSelectedDate(utcDateKey(event.scheduledFor));
    setSelectedEventId(event.primaryKey);
  };

  return (
    <div
      className="operations-page public-page"
      style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
    >
      <section className="public-hero public-hero--compact editorial-hero operations-hero">
        <div className="public-hero__inner public-hero__inner--single">
          <div className="public-hero__copy">
            <Link className="operations-hero__back" to="/">
              <ChevronLeft20Regular /> {t("Auto_BackToHome_76c5505f")}
            </Link>
            <span className="kicker">{t("Shared_CommunityNav_Operations")}</span>
            <h1>{t("Auto_UpcomingOperations_f2355c40")}</h1>
            <p>{t("Auto_FindPlannedMissionsTrainingWindowsTerrainRequirementsModSets_8b14be0c")}</p>
          </div>
        </div>
      </section>

      <section className="content-section public-section operations-workspace">
        <div className="operations-commandbar">
          <div
            className="view-toggle"
            role="tablist"
            aria-label={t("Auto_OperationsViewMode_c333479b")}
          >
            <button
              className={view === "calendar" ? "active" : ""}
              type="button"
              role="tab"
              aria-selected={view === "calendar"}
              onClick={() => setViewMode("calendar")}
            >
              <CalendarMonth24Regular /> {t("Calendar")}
            </button>
            <button
              className={view === "board" ? "active" : ""}
              type="button"
              role="tab"
              aria-selected={view === "board"}
              onClick={() => setViewMode("board")}
            >
              <Grid24Regular /> {t("Board")}
            </button>
          </div>
          <div className="operations-commandbar__meta">
            <span>{t("Auto_AllTimesInUTC_3b1ff9db")}</span>
            <button
              className="button button--ghost button--small"
              type="button"
              onClick={() => void query.refetch()}
            >
              <ArrowSync20Regular /> {t("Common_Refresh")}
            </button>
          </div>
        </div>

        {view === "board" && (
          <div className="operations-filters">
            <span className="kicker">{t("React_UI_StatusFilter")}</span>
            <div className="filter-chips" role="group" aria-label={t("React_UI_StatusFilter")}>
              {(["all", "confirmed", "needs-signups", "open"] as const).map((value) => (
                <button
                  key={value}
                  type="button"
                  className={filter === value ? "active" : ""}
                  aria-pressed={filter === value}
                  onClick={() => setFilter(value)}
                >
                  {value === "all"
                    ? t("Pages_Events_Upcoming_FilterAll")
                    : value === "confirmed"
                      ? t("Pages_Events_Upcoming_FilterConfirmed")
                      : value === "needs-signups"
                        ? t("Pages_Events_Upcoming_FilterNeedsSignups")
                        : t("Pages_Events_Upcoming_FilterOpen")}
                </button>
              ))}
            </div>
          </div>
        )}

        {query.isLoading && <OperationsSkeleton view={view} />}
        {query.isError && (
          <StatePanel
            kind="error"
            action={
              <button
                className="button button--ghost"
                type="button"
                onClick={() => void query.refetch()}
              >
                {t("React_UI_Retry")}
              </button>
            }
          >
            {t("Pages_Events_Upcoming_ErrorLoadFailed")}
          </StatePanel>
        )}
        {!query.isLoading &&
          !query.isError &&
          view === "board" &&
          (boardEvents.length ? (
            <div className="operation-board">
              {boardEvents.map((event, index) => (
                <OperationCard
                  event={event}
                  fallbackImage={cardImage}
                  featured={index === 0}
                  key={event.primaryKey}
                />
              ))}
            </div>
          ) : (
            <StatePanel kind="empty" title={t("Pages_Events_Upcoming_CountNoScheduled")}>
              {t("Auto_NoOperationsMatchTheSelectedFilter_eaf9aeab")}
            </StatePanel>
          ))}
        {!query.isLoading && !query.isError && view === "calendar" && (
          <CalendarWorkspace
            month={displayedMonth}
            events={displayedEvents}
            days={calendarDays}
            weekdayLabels={weekdayLabels}
            selectedDate={effectiveSelectedDate}
            selectedEvent={selectedEvent}
            agendaGroups={agendaGroups}
            onPrevious={() => showMonth(-1)}
            onNext={() => showMonth(1)}
            onSelectDate={selectDate}
            onSelectEvent={selectEvent}
          />
        )}
      </section>
    </div>
  );
}

function OperationsSkeleton({ view }: { view: ViewMode }) {
  const { t } = useTranslation();
  return (
    <div className={`operations-skeleton operations-skeleton--${view}`} role="status">
      <span className="sr-only">{t("Pages_Public_Home_OperationsLoading")}</span>
      {Array.from({ length: view === "board" ? 4 : 3 }, (_, index) => (
        <span aria-hidden="true" key={index} />
      ))}
    </div>
  );
}

function CalendarWorkspace({
  month,
  events,
  days,
  weekdayLabels,
  selectedDate,
  selectedEvent,
  agendaGroups,
  onPrevious,
  onNext,
  onSelectDate,
  onSelectEvent,
}: {
  month: Date;
  events: UpcomingEvent[];
  days: CalendarDay[];
  weekdayLabels: string[];
  selectedDate: string;
  selectedEvent: UpcomingEvent | undefined;
  agendaGroups: [string, UpcomingEvent[]][];
  onPrevious(): void;
  onNext(): void;
  onSelectDate(date: string): void;
  onSelectEvent(event: UpcomingEvent): void;
}) {
  const { t } = useTranslation();
  const label = monthLabel(month);
  const summary = monthSummary(events.length, t);
  const weekDays = buildWeekStrip(selectedDate, events);
  return (
    <section className="calendar-workspace" aria-label={t("Auto_OperationsCalendar_aaaf0488")}>
      <div className="calendar-main">
        <div className="calendar-monthbar">
          <button type="button" aria-label={t("Auto_PreviousMonth_6a276950")} onClick={onPrevious}>
            <ChevronLeft20Regular />
          </button>
          <div>
            <strong>{label}</strong>
            <span>{summary}</span>
          </div>
          <button type="button" aria-label={t("Auto_NextMonth_74e53211")} onClick={onNext}>
            <ChevronRight20Regular />
          </button>
        </div>

        <div className="calendar-week-strip" aria-label={dateLabel(selectedDate)}>
          {weekDays.map((day) => (
            <button
              className={`${day.key === selectedDate ? "selected" : ""}${day.isToday ? " today" : ""}`}
              type="button"
              aria-pressed={day.key === selectedDate}
              onClick={() => onSelectDate(day.key)}
              key={day.key}
            >
              <span>{formatUtc(day.date, { weekday: "short" })}</span>
              <strong>{day.date.getUTCDate()}</strong>
              <i className={day.events.length ? "has-events" : ""} aria-hidden="true" />
            </button>
          ))}
        </div>

        <div className="calendar-overview">
          <span>
            <CalendarMonth24Regular /> {t("Auto_MonthOverview_32b62dd1")}
          </span>
          <strong>{summary}</strong>
        </div>
        <div className="calendar-grid" role="grid" aria-label={label}>
          {weekdayLabels.map((weekday) => (
            <div className="calendar-weekday" role="columnheader" key={weekday}>
              {weekday}
            </div>
          ))}
          {days.map((day) => (
            <div
              className={`calendar-day${day.inMonth ? "" : " outside"}${day.isToday ? " today" : ""}${day.key === selectedDate ? " selected" : ""}`}
              role="gridcell"
              aria-selected={day.key === selectedDate}
              key={day.key}
            >
              <button
                className="calendar-day__number"
                type="button"
                aria-label={dateLabel(day.date)}
                onClick={() => onSelectDate(day.key)}
              >
                {day.date.getUTCDate()}
              </button>
              <div className="calendar-day__events">
                {day.events.slice(0, 3).map((event) => (
                  <button
                    type="button"
                    className={`${event.primaryKey === selectedEvent?.primaryKey ? "active" : ""} ${eventReadiness(event) === "needs-signups" ? "needs-signups" : "ready"}`}
                    aria-pressed={event.primaryKey === selectedEvent?.primaryKey}
                    onClick={() => onSelectEvent(event)}
                    key={event.primaryKey}
                  >
                    <span>{eventTime(event.scheduledFor)}</span>
                    <strong>{event.title}</strong>
                  </button>
                ))}
                {day.events.length > 3 && (
                  <small>
                    {formatResource(t("Pages_Events_Upcoming_CountMore"), day.events.length - 3)}
                  </small>
                )}
              </div>
            </div>
          ))}
        </div>

        <div className="calendar-agenda" aria-label={t("Auto_CalendarAgenda_62c712a6")}>
          {agendaGroups.length === 0 && (
            <StatePanel kind="empty">
              {formatResource(t("Pages_Events_Upcoming_NoScheduledInMonth"), label)}
            </StatePanel>
          )}
          {agendaGroups.map(([date, groupEvents]) => (
            <section key={date} aria-label={dateLabel(`${date}T00:00:00Z`)}>
              <h2>{dateLabel(`${date}T00:00:00Z`)}</h2>
              {groupEvents.map((event) => (
                <article
                  className={event.primaryKey === selectedEvent?.primaryKey ? "active" : ""}
                  key={event.primaryKey}
                >
                  <button type="button" onClick={() => onSelectEvent(event)}>
                    <span>{eventTime(event.scheduledFor)}</span>
                    <strong>{event.title}</strong>
                  </button>
                  <div className="calendar-agenda__status">
                    <ReadinessBadge event={event} />
                    <AttendanceBadge event={event} />
                  </div>
                  <div className="calendar-agenda__slots">
                    <span>
                      {formatEventSlotCount(event)} {t("Pages_Events_Upcoming_SlotsLabel")}
                    </span>
                    <div className="slot-meter" aria-label={formatEventSlotCount(event)}>
                      <span style={{ width: `${eventSlotProgress(event)}%` }} />
                    </div>
                  </div>
                  <Link
                    className="button button--ghost button--small"
                    to={`/events/${event.primaryKey}`}
                  >
                    {t("Auto_ViewSlots_ddcd3598")}
                  </Link>
                </article>
              ))}
            </section>
          ))}
        </div>
      </div>
      <aside className="calendar-detail" aria-label={t("Auto_SelectedOperation_3716235b")}>
        {selectedEvent ? (
          <>
            <span className="kicker">{t("Auto_SelectedOperation_3716235b")}</span>
            <h2>{selectedEvent.title}</h2>
            <div className="calendar-detail__statuses">
              <div>
                <small>{t("Auto_SignupReadiness_bde4bfc8")}</small>
                <ReadinessBadge event={selectedEvent} />
              </div>
              <div>
                <small>{t("Auto_YourAttendance_71dff290")}</small>
                <AttendanceBadge event={selectedEvent} />
              </div>
            </div>
            <dl>
              <div>
                <dt>{t("Date")}</dt>
                <dd>{dateLabel(selectedEvent.scheduledFor)}</dd>
              </div>
              <div>
                <dt>{t("Time")}</dt>
                <dd>{eventTime(selectedEvent.scheduledFor)}</dd>
              </div>
              <div>
                <dt>{t("Terrain")}</dt>
                <dd>{selectedEvent.terrainTitle || t("Pages_Events_Upcoming_TerrainPending")}</dd>
              </div>
              <div>
                <dt>{t("Auto_ModPack_f151fb94")}</dt>
                <dd>
                  {selectedEvent.modPackDefinitionTitle ||
                    t("Pages_Events_Upcoming_ModPackPending")}
                </dd>
              </div>
              <div>
                <dt>{t("Host")}</dt>
                <dd>{selectedEvent.hostedBy.nickname || t("Pages_Events_Upcoming_HostPending")}</dd>
              </div>
            </dl>
            <div className="calendar-detail__progress">
              <span>{t("Auto_SlotProgress_ef2c2303")}</span>
              <strong>{formatEventSlotCount(selectedEvent)}</strong>
              <div className="slot-meter" aria-label={formatEventSlotCount(selectedEvent)}>
                <span style={{ width: `${eventSlotProgress(selectedEvent)}%` }} />
              </div>
              <small>{formatResource(t("Auto_Pending_7948f853"), selectedEvent.maybeCount)}</small>
            </div>
            <p>
              {plainText(selectedEvent.description) || t("Pages_Events_Upcoming_NoBriefingSummary")}
            </p>
            <Link className="button button--gold" to={`/events/${selectedEvent.primaryKey}`}>
              {t("Auto_ViewSlots_ddcd3598")} <ChevronRight20Regular />
            </Link>
          </>
        ) : (
          <StatePanel kind="empty" title={t("Auto_NoOperationSelected_98bff4dc")}>
            {formatResource(t("Auto_NoScheduledOperationIsPublishedInUseTheMonth_f3ebefc3"), label)}
          </StatePanel>
        )}
      </aside>
    </section>
  );
}

function OperationCard({
  event,
  fallbackImage,
  featured,
}: {
  event: UpcomingEvent;
  fallbackImage: string;
  featured: boolean;
}) {
  const { t } = useTranslation();
  const resolvedImage = resolveImageUrl(event.imagePath) ?? fallbackImage;
  return (
    <article className={`operation-board-card${featured ? " operation-board-card--featured" : ""}`}>
      <div
        className="operation-board-card__media"
        style={{ backgroundImage: `url("${resolvedImage.replaceAll('"', "%22")}")` }}
      >
        <div className="operation-board-card__statuses">
          <ReadinessBadge event={event} />
          <span className="operation-board-card__slot-count">
            {formatEventSlotCount(event)} {t("Pages_Events_Upcoming_SlotsLabel")}
          </span>
        </div>
        <time dateTime={event.scheduledFor}>
          {dateLabel(event.scheduledFor)} · {eventTime(event.scheduledFor)}
        </time>
      </div>
      <div className="operation-board-card__body">
        <div className="operation-board-card__heading">
          <div>
            <span className="kicker">{t("Pages_Events_Upcoming_TypeOperation")}</span>
            <h2>
              <Link to={`/events/${event.primaryKey}`}>{event.title}</Link>
            </h2>
          </div>
          <div className="operation-board-card__attendance">
            <small>{t("Auto_YourAttendance_71dff290")}</small>
            <AttendanceBadge event={event} />
          </div>
        </div>
        <p>{plainText(event.description) || t("Pages_Events_Upcoming_NoBriefingSummary")}</p>
        <dl>
          <div>
            <dt>{t("Terrain")}</dt>
            <dd>{event.terrainTitle || t("Pages_Events_Upcoming_TerrainPending")}</dd>
          </div>
          <div>
            <dt>{t("Auto_ModPack_f151fb94")}</dt>
            <dd>{event.modPackDefinitionTitle || t("Pages_Events_Upcoming_ModPackPending")}</dd>
          </div>
          <div>
            <dt>{t("Host")}</dt>
            <dd>{event.hostedBy.nickname || t("Pages_Events_Upcoming_HostPending")}</dd>
          </div>
        </dl>
        <div className="operation-board-card__progress">
          <span>{t("Auto_SlotProgress_ef2c2303")}</span>
          <strong>{formatEventSlotCount(event)}</strong>
          <div className="slot-meter" aria-label={formatEventSlotCount(event)}>
            <span style={{ width: `${eventSlotProgress(event)}%` }} />
          </div>
        </div>
        <div className="card-footer">
          <span>{eventTime(event.scheduledFor)}</span>
          <Link className="button button--ghost button--small" to={`/events/${event.primaryKey}`}>
            {t("Pages_Events_Upcoming_ActionViewSlots")}
          </Link>
        </div>
      </div>
    </article>
  );
}
