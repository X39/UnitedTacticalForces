import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canEditEvent, canManageEvents } from "@/auth/claims";
import { ActionLink } from "@/components/ActionControl";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  eventAdminState,
  eventMatchesFilter,
  eventMatchesSearch,
  isUnderMinimum,
  loadEventAdminSnapshot,
  slotReadinessPercentage,
  sortEventAdminRows,
  type EventAdminFilter,
  type EventAdminState,
  type PlainEvent,
} from "@/features/admin/eventAdmin";

const pageSize = 80;
const missing = "—";
const filterKeys: Record<EventAdminFilter, TranslationKey> = {
  all: "Pages_Events_All_FilterAll",
  upcoming: "Pages_Events_All_FilterUpcoming",
  "needs-slots": "Pages_Events_All_FilterNeedsSlots",
  hidden: "Pages_Events_All_FilterHidden",
  archived: "Pages_Events_All_FilterArchived",
};
const titleKeys: Record<EventAdminFilter, TranslationKey> = {
  all: "Pages_Events_All_TitleAll",
  upcoming: "Pages_Events_All_TitleUpcoming",
  "needs-slots": "Pages_Events_All_TitleNeedsSlots",
  hidden: "Pages_Events_All_TitleHidden",
  archived: "Pages_Events_All_TitleArchived",
};
const stateCopy: Record<
  EventAdminState,
  { key: TranslationKey; tone: "neutral" | "success" | "warning" | "info" }
> = {
  hidden: { key: "Pages_Events_All_StatusHidden", tone: "info" },
  archived: { key: "Pages_Events_All_StatusArchived", tone: "neutral" },
  "low-slots": { key: "Pages_Events_All_StatusLowSlots", tone: "warning" },
  ready: { key: "Pages_Events_All_StatusReady", tone: "success" },
};

function relativeSchedule(
  event: PlainEvent,
  translate: (key: TranslationKey) => string,
  now: number,
): string {
  if (!event.scheduledFor) return translate("Pages_Events_All_SchedulePending");
  const scheduled = Date.parse(event.scheduledFor);
  if (Number.isNaN(scheduled)) return translate("Pages_Events_All_SchedulePending");
  const deltaHours = (scheduled - now) / 3_600_000;
  if (deltaHours >= 0)
    return deltaHours < 48
      ? formatResource(translate("Pages_Events_All_TimeInHours"), Math.ceil(deltaHours))
      : formatResource(translate("Pages_Events_All_TimeInDays"), Math.ceil(deltaHours / 24));
  return Math.abs(deltaHours) < 48
    ? formatResource(translate("Pages_Events_All_TimeAgoHours"), Math.ceil(Math.abs(deltaHours)))
    : formatResource(
        translate("Pages_Events_All_TimeAgoDays"),
        Math.ceil(Math.abs(deltaHours) / 24),
      );
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const authorized = canManageEvents(user);
  const [filter, setFilter] = useState<EventAdminFilter>("all");
  const [search, setSearch] = useState("");
  const [hostedByMeOnly, setHostedByMeOnly] = useState(false);
  const [take, setTake] = useState(pageSize);
  const now = Date.now();
  const query = useQuery({
    queryKey: ["command-center", "event-admin", { take, hostedByMeOnly }],
    queryFn: ({ signal }) => loadEventAdminSnapshot(take, hostedByMeOnly, signal),
    enabled: authorized,
    placeholderData: keepPreviousData,
  });
  const snapshot = query.data;
  const stateLabel = (event: PlainEvent) => t(stateCopy[eventAdminState(event, now)].key);
  const counts = useMemo(() => {
    const events = snapshot?.events ?? [];
    return {
      all: events.length,
      upcoming: events.filter((row) => eventMatchesFilter(row.plainEvent, "upcoming", now)).length,
      "needs-slots": events.filter((row) => isUnderMinimum(row.plainEvent)).length,
      hidden: events.filter((row) => row.plainEvent.isVisible === false).length,
      archived: events.filter((row) => eventMatchesFilter(row.plainEvent, "archived", now)).length,
    } satisfies Record<EventAdminFilter, number>;
  }, [now, snapshot?.events]);
  const visibleRows = useMemo(
    () =>
      sortEventAdminRows(
        (snapshot?.events ?? [])
          .filter((row) => eventMatchesFilter(row.plainEvent, filter, now))
          .filter((row) => eventMatchesSearch(row, search, stateLabel(row.plainEvent))),
        now,
      ),
    // Translation changes rerender the component and therefore refresh the localized search terms.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [filter, now, search, snapshot?.events, t],
  );
  const nextEvent = [...(snapshot?.upcomingEvents ?? [])].sort(
    (left, right) =>
      (left.scheduledFor ? Date.parse(left.scheduledFor) : Number.MAX_SAFE_INTEGER) -
      (right.scheduledFor ? Date.parse(right.scheduledFor) : Number.MAX_SAFE_INTEGER),
  )[0];
  const canLoadMore = Boolean(snapshot && snapshot.eventCount > snapshot.events.length);
  const setHostFilter = (value: boolean) => {
    if (value === hostedByMeOnly) return;
    setTake(pageSize);
    setHostedByMeOnly(value);
  };

  if (!authorized)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Events_All_AccessDeniedTitle")}>
        {t("Pages_Events_All_AccessDeniedBody")}
      </StatePanel>
    );

  return (
    <section className="event-admin-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Events")}
        title={t("Pages_Events_All_PageTitle")}
        description={t("Pages_Events_All_PageDescription")}
        actions={
          <>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Events_All_ActionRefresh")}
            </button>
            <Link className="button button--primary" to="/command/events/create">
              {t("Pages_Events_All_ActionCreate")}
            </Link>
          </>
        }
      />

      {query.isError && (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" onClick={() => void query.refetch()}>
              {t("Pages_Events_All_ActionRefresh")}
            </button>
          }
        >
          {t("Pages_Events_All_LoadError")}
        </StatePanel>
      )}

      <section
        className="command-metrics event-admin-metrics"
        aria-label={t("Pages_Events_All_MetricsAria")}
      >
        <button
          type="button"
          className={filter === "all" ? "active" : undefined}
          aria-pressed={filter === "all"}
          onClick={() => setFilter("all")}
        >
          <Metric
            label={t("Pages_Events_All_MetricStatusArchive")}
            value={formatResource(
              t("Pages_Events_All_EventCountLoaded"),
              snapshot?.eventCount ?? 0,
            )}
            detail={formatResource(t("Pages_Events_All_MetricDescriptionReturned"), counts.all)}
          />
        </button>
        <button
          type="button"
          className={filter === "upcoming" ? "active" : undefined}
          aria-pressed={filter === "upcoming"}
          onClick={() => setFilter("upcoming")}
        >
          <Metric
            label={t("Pages_Events_All_MetricStatusUpcoming")}
            value={formatResource(t("Pages_Events_All_MetricValueUpcoming"), counts.upcoming)}
            detail={
              nextEvent
                ? `${nextEvent.title || t("Pages_Events_All_UntitledEvent")} / ${formatDateTime(nextEvent.scheduledFor)}`
                : t("Pages_Events_All_NoUpcomingReturned")
            }
          />
        </button>
        <button
          type="button"
          className={filter === "needs-slots" ? "active" : undefined}
          aria-pressed={filter === "needs-slots"}
          onClick={() => setFilter("needs-slots")}
        >
          <Metric
            label={t("Pages_Events_All_MetricStatusSlots")}
            value={formatResource(
              t("Pages_Events_All_MetricValueBelowMinimum"),
              counts["needs-slots"],
            )}
            detail={t("Pages_Events_All_MetricDescriptionNeedsSlots")}
          />
        </button>
        <button
          type="button"
          className={filter === "hidden" ? "active" : undefined}
          aria-pressed={filter === "hidden"}
          onClick={() => setFilter("hidden")}
        >
          <Metric
            label={t("Pages_Events_All_MetricStatusVisibility")}
            value={formatResource(t("Pages_Events_All_MetricValueHiddenEvents"), counts.hidden)}
            detail={t("Pages_Events_All_MetricDescriptionHidden")}
          />
        </button>
      </section>

      <section
        className="toolbar event-admin-toolbar"
        aria-label={t("Pages_Events_All_FilterToolbarAria")}
      >
        <label className="search-field">
          <span className="sr-only">{t("Pages_Events_All_FilterPlaceholder")}</span>
          <input
            value={search}
            placeholder={t("Pages_Events_All_FilterPlaceholder")}
            onChange={(event) => setSearch(event.target.value)}
          />
        </label>
        <div className="filter-chips">
          {(Object.keys(filterKeys) as EventAdminFilter[]).map((candidate) => (
            <button
              key={candidate}
              type="button"
              className={filter === candidate ? "active" : undefined}
              aria-pressed={filter === candidate}
              onClick={() => setFilter(candidate)}
            >
              {t(filterKeys[candidate])} <span>{counts[candidate]}</span>
            </button>
          ))}
          <button
            type="button"
            className={!hostedByMeOnly ? "active" : undefined}
            aria-pressed={!hostedByMeOnly}
            onClick={() => setHostFilter(false)}
          >
            {t("Pages_Events_All_FilterAllHosts")}
          </button>
          <button
            type="button"
            className={hostedByMeOnly ? "active" : undefined}
            aria-pressed={hostedByMeOnly}
            onClick={() => setHostFilter(true)}
          >
            {t("Pages_Events_All_FilterHostedByMe")}
          </button>
        </div>
      </section>

      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Events_All_LoadingState")}</StatePanel>
      ) : (
        <section className="command-panel event-admin-ledger">
          <header className="command-panel__header event-admin-ledger__header">
            <span className="kicker">{t("Pages_Events_All_Kicker")}</span>
            <h2>{t(titleKeys[filter])}</h2>
            <StatusBadge>
              {formatResource(t("Pages_Events_All_VisibleCount"), visibleRows.length)}
            </StatusBadge>
          </header>
          {visibleRows.length === 0 ? (
            <StatePanel>{t("Pages_Events_All_NoEventsMatch")}</StatePanel>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table event-admin-table">
                <thead>
                  <tr>
                    <th>{t("Pages_Events_All_TableHeaderOperation")}</th>
                    <th>{t("Pages_Events_All_TableHeaderStatus")}</th>
                    <th>{t("Pages_Events_All_TableHeaderHostOwner")}</th>
                    <th>{t("Pages_Events_All_TableHeaderSchedule")}</th>
                    <th>{t("Pages_Events_All_TableHeaderAssets")}</th>
                    <th>{t("Pages_Events_All_TableHeaderSlots")}</th>
                    <th>{t("Pages_Events_All_TableHeaderActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleRows.map((row) => {
                    const event = row.plainEvent;
                    const state = stateCopy[eventAdminState(event, now)];
                    const title = event.title || t("Pages_Events_All_UntitledEvent");
                    const canOpenEditor = Boolean(
                      event.primaryKey && canEditEvent(user, row.hostedBy?.primaryKey),
                    );
                    const editorPath = event.primaryKey
                      ? `/command/events/${event.primaryKey}`
                      : undefined;
                    const openEditor = () => {
                      if (canOpenEditor && editorPath) navigate(editorPath);
                    };
                    return (
                      <tr
                        key={event.primaryKey || event.title}
                        className={canOpenEditor ? "event-admin-row--interactive" : undefined}
                        tabIndex={canOpenEditor ? 0 : undefined}
                        onClick={canOpenEditor ? openEditor : undefined}
                        onKeyDown={
                          canOpenEditor
                            ? (keyboardEvent) => {
                                if (keyboardEvent.target !== keyboardEvent.currentTarget) return;
                                if (keyboardEvent.key !== "Enter" && keyboardEvent.key !== " ")
                                  return;
                                keyboardEvent.preventDefault();
                                openEditor();
                              }
                            : undefined
                        }
                      >
                        <th scope="row">
                          {canOpenEditor && editorPath ? (
                            <Link
                              to={editorPath}
                              onClick={(clickEvent) => clickEvent.stopPropagation()}
                            >
                              {title}
                            </Link>
                          ) : (
                            <strong>{title}</strong>
                          )}
                          <span>{event.primaryKey || missing}</span>
                        </th>
                        <td>
                          <StatusBadge tone={state.tone}>{t(state.key)}</StatusBadge>
                        </td>
                        <td>
                          <strong>{row.hostedBy?.nickname || missing}</strong>
                          <span>{row.owner?.nickname || missing}</span>
                        </td>
                        <td>
                          <strong>{formatDateTime(event.scheduledFor)}</strong>
                          <span>{relativeSchedule(event, t, now)}</span>
                        </td>
                        <td>
                          <strong>{row.terrain?.title || missing}</strong>
                          <span>{row.modPackRevision?.definitionTitle || missing}</span>
                        </td>
                        <td>
                          <strong>
                            {formatResource(
                              t("Pages_Events_All_SlotReadiness"),
                              event.acceptedCount ?? 0,
                              event.minimumAccepted ?? 0,
                            )}
                          </strong>
                          <span>
                            {formatResource(
                              t("Pages_Events_All_ParticipantBreakdown"),
                              event.maybeCount ?? 0,
                              event.rejectedCount ?? 0,
                            )}
                          </span>
                          <div className="event-admin-row-progress" aria-hidden="true">
                            <span style={{ width: `${slotReadinessPercentage(event)}%` }} />
                          </div>
                        </td>
                        <td>
                          <div className="event-admin-row-actions">
                            {canOpenEditor && editorPath && (
                              <ActionLink
                                variant="primary"
                                size="compact"
                                to={editorPath}
                                onClick={(clickEvent) => clickEvent.stopPropagation()}
                              >
                                {t("Auto_ManageEvent_0a863949")}
                              </ActionLink>
                            )}
                            {event.primaryKey && (
                              <ActionLink
                                variant="ghost"
                                size="compact"
                                to={`/events/${event.primaryKey}`}
                                onClick={(clickEvent) => clickEvent.stopPropagation()}
                              >
                                {t("Pages_Events_All_ViewSlots")}
                              </ActionLink>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
          <footer className="event-admin-footer">
            <span>
              {snapshot && snapshot.eventCount > snapshot.events.length
                ? formatResource(
                    t("Pages_Events_All_EventsLoadedOf"),
                    snapshot.events.length,
                    snapshot.eventCount,
                  )
                : formatResource(t("Pages_Events_All_EventsLoaded"), snapshot?.events.length ?? 0)}
            </span>
            <button
              className="button button--ghost"
              type="button"
              disabled={!canLoadMore || query.isFetching}
              onClick={() => setTake((current) => current + pageSize)}
            >
              {query.isFetching && canLoadMore
                ? t("Pages_Events_All_LoadingMore")
                : t("Pages_Events_All_LoadMore")}
            </button>
          </footer>
        </section>
      )}
    </section>
  );
}
