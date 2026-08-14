import {
  ArrowSync20Regular,
  CalendarMonth24Regular,
  ChevronLeft20Regular,
  ChevronRight20Regular,
  Grid24Regular,
  Search20Regular,
} from "@fluentui/react-icons";
import type { TFunction } from "i18next";
import { useQuery } from "@tanstack/react-query";
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
  eventNeedsSignups,
  formatEventSlotCount,
  matchesUpcomingEventFilter,
  normalizeEventAcceptance,
  utcDateKey,
  type UpcomingEventFilter,
} from "@/app/upcomingEvents";
import { PageHeader, StatePanel, StatusBadge } from "@/components/primitives";

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

function monthSummary(count: number, t: TFunction): string {
  if (count === 0) return t("Pages_Events_Upcoming_CountNoScheduled");
  if (count === 1) return t("Pages_Events_Upcoming_CountOneScheduled");
  return formatResource(t("Pages_Events_Upcoming_CountScheduled"), count);
}

function attendanceLabel(event: UpcomingEvent, t: TFunction): string {
  switch (normalizeEventAcceptance(event.metaAcceptance)) {
    case "Accepted":
      return t("Pages_Events_Upcoming_StatusConfirmed");
    case "Rejected":
      return t("Pages_Events_Upcoming_StatusDeclined");
    case "Maybe":
      return t("Pages_Events_Upcoming_StatusMaybe");
    default:
      return eventNeedsSignups(event)
        ? t("Pages_Events_Upcoming_StatusLowSlots")
        : t("Pages_Events_Upcoming_StatusOpenSlots");
  }
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
  const [search, setSearch] = useState("");
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
    () =>
      sortedEvents.filter(
        (event) =>
          matchesUpcomingEventFilter(event, filter) &&
          `${event.title} ${event.terrainTitle} ${event.modPackDefinitionTitle}`
            .toLocaleLowerCase()
            .includes(search.trim().toLocaleLowerCase()),
      ),
    [filter, search, sortedEvents],
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
      className="page-wrap operations-page"
      style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
    >
      <PageHeader
        kicker={t("Shared_CommunityNav_Operations")}
        title={t("Auto_UpcomingOperations_f2355c40")}
        description={t(
          "Auto_FindPlannedMissionsTrainingWindowsTerrainRequirementsModSets_8b14be0c",
        )}
        actions={
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
        }
      />

      {view === "board" ? (
        <div className="toolbar">
          <label className="search-field">
            <Search20Regular />
            <span className="sr-only">{t("React_UI_SearchOperations")}</span>
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t("React_UI_SearchOperations")}
            />
          </label>
          <div className="filter-chips" role="group" aria-label={t("React_UI_StatusFilter")}>
            {(["all", "confirmed", "needs-signups", "open"] as const).map((value) => (
              <button
                key={value}
                type="button"
                className={filter === value ? "active" : ""}
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
      ) : (
        <div className="calendar-toolbar">
          <span>{t("Auto_AllTimesInUTC_3b1ff9db")}</span>
          <button
            className="button button--ghost button--small"
            type="button"
            onClick={() => void query.refetch()}
          >
            <ArrowSync20Regular /> {t("Common_Refresh")}
          </button>
        </div>
      )}

      {query.isLoading && (
        <StatePanel kind="loading">{t("Pages_Public_Home_OperationsLoading")}</StatePanel>
      )}
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
            {boardEvents.map((event) => (
              <OperationCard event={event} fallbackImage={cardImage} key={event.primaryKey} />
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
                    className={event.primaryKey === selectedEvent?.primaryKey ? "active" : ""}
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
                  <span>{event.terrainTitle || t("Pages_Events_Upcoming_TerrainPending")}</span>
                  <span>
                    {formatEventSlotCount(event)} {t("Pages_Events_Upcoming_SlotsLabel")}
                  </span>
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
            <div className="calendar-detail__heading">
              <span className="kicker">{t("Auto_SelectedOperation_3716235b")}</span>
              <StatusBadge tone={eventNeedsSignups(selectedEvent) ? "warning" : "success"}>
                {attendanceLabel(selectedEvent, t)}
              </StatusBadge>
            </div>
            <h2>{selectedEvent.title}</h2>
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
                <span
                  style={{
                    width: `${selectedEvent.minimumAccepted > 0 ? Math.min(100, (selectedEvent.acceptedCount / selectedEvent.minimumAccepted) * 100) : selectedEvent.acceptedCount > 0 ? 100 : 0}%`,
                  }}
                />
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

function OperationCard({ event, fallbackImage }: { event: UpcomingEvent; fallbackImage: string }) {
  const { t } = useTranslation();
  return (
    <article className="operation-board-card">
      <div
        className="operation-board-card__media"
        style={{ backgroundImage: `url("${resolveImageUrl(event.imagePath) ?? fallbackImage}")` }}
      >
        <StatusBadge tone={eventNeedsSignups(event) ? "warning" : "success"}>
          {attendanceLabel(event, t)}
        </StatusBadge>
        <time dateTime={event.scheduledFor}>
          {dateLabel(event.scheduledFor)} · {eventTime(event.scheduledFor)}
        </time>
      </div>
      <div className="operation-board-card__body">
        <span className="kicker">{t("Pages_Events_Upcoming_TypeOperation")}</span>
        <h2>
          <Link to={`/events/${event.primaryKey}`}>{event.title}</Link>
        </h2>
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
        <div className="card-footer">
          <span>
            {t("Pages_Events_Upcoming_SlotsLabel")}: {formatEventSlotCount(event)}
          </span>
          <Link className="button button--ghost button--small" to={`/events/${event.primaryKey}`}>
            {t("Pages_Events_Upcoming_ActionViewSlots")}
          </Link>
        </div>
      </div>
    </article>
  );
}
