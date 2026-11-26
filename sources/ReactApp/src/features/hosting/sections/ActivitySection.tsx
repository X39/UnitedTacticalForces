import { Checkmark16Regular, Filter20Regular } from "@fluentui/react-icons";
import { useEffect, useId, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router";
import { useNow } from "@/app/useNow";
import { ActionAnchor, ActionButton } from "@/components/ActionControl";
import { FilterChipGroup } from "@/components/FilterChipGroup";
import { StatePanel } from "@/components/primitives";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import { ActivityEntry } from "@/components/server/ActivityEntry";
import { ActivityTimeline } from "@/components/server/ActivityTimeline";
import { LiveBadge } from "@/components/server/LiveBadge";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import "@/components/server/activity.css";
import "@/components/server/fleet-toolbar.css";
import {
  auditWindowBoundary,
  buildActivityEntries,
  filterCounts,
  filterEntries,
  type ActivityFilter,
} from "@/features/hosting/activityModel";
import type { GameServer, HostingOperation, LifecycleJob } from "@/features/hosting/types";
import {
  buildCommandProtocol,
  protocolEntries,
  textDownloadUrl,
} from "@/features/hosting/workspaceModel";

/** How often the relative times of the timeline are refreshed while the section is mounted. */
const clockIntervalMs = 30_000;

/** How many entries the timeline shows at first and how many each Show more activity adds. */
const pageStep = 50;

/** The separator between the command protocols of the download of all logs. */
const protocolSeparator = "\n\n========================================\n\n";

/** The id of the projected action that asks the worker to collect fresh logs. */
const collectLogsActionId = "collect-logs";

/** The prefix every activity entry anchor starts with. */
const activityAnchorPrefix = "#activity-";

/** The localization key of each type filter. */
const filterLabelKeys = {
  all: "Server_Activity_Filter_All",
  jobs: "Server_Activity_Filter_Jobs",
  logs: "Server_Activity_Filter_Logs",
  audit: "Server_Activity_Filter_Audit",
} as const;

/** The type filters in display order. */
const filterOrder: readonly ActivityFilter[] = ["all", "jobs", "logs", "audit"];

/** A request to move focus to an entry, optionally scrolling it into view first. */
interface FocusRequest {
  /** The id of the entry article that receives focus. */
  id: string;
  /** Whether the entry is scrolled into view before it is focused. */
  scroll: boolean;
}

/** The props of {@link ActivitySection}. */
export interface ActivitySectionProps {
  /** The server the activity belongs to; gives the unavailable-reason context of the toolbar actions. */
  server: GameServer;
  /** The loaded lifecycle jobs of the server, each with its worker commands. */
  jobs: readonly LifecycleJob[];
  /** The loaded hosting operations (audit records) of the server. */
  operations: readonly HostingOperation[];
  /** Whether the jobs or operations are loading for the first time. */
  loading: boolean;
  /** Whether loading the jobs or operations failed. */
  failed: boolean;
  /** Whether a background refetch is running; the timeline stays in place and is marked busy. */
  fetching: boolean;
  /** The time of the newest successful fetch of the jobs and operations in epoch milliseconds; 0 when none. */
  dataUpdatedAt: number;
  /** Whether a lifecycle job is active; the live badge needs it to judge the age of the data. */
  hasActiveJob: boolean;
  /** Whether a retry or skip request is in flight; both actions are then unavailable. */
  jobBusy: boolean;
  /** Whether the last retry or skip request failed. */
  jobRequestFailed: boolean;
  /** Called when the viewer asks to load the jobs and operations again. */
  onReload(): void;
  /**
   * Called with the job id when the viewer asks to try a failed job again.
   *
   * @param jobId The primary key of the job.
   */
  onRetryJob(jobId: string): void;
  /**
   * Called with the job id when the viewer asks to skip a failed job.
   *
   * @param jobId The primary key of the job.
   */
  onSkipJob(jobId: string): void;
}

/**
 * The Activity section of the server workspace: one merged, day-grouped timeline of jobs, worker logs and audit
 * records.
 *
 * The toolbar filters the timeline by type and by Problems only with counts that equal what each filter lists,
 * shows how current the data is, collects logs through the shared confirmation and downloads all protocols. Loading
 * and failure replace the timeline with an honest panel while the shell stays usable; zero entries and a filter
 * without a match say so. Background refetches keep the content in place. The timeline shows 50 entries and grows
 * by 50 on request. An activity deep link (`#activity-<type>-<id>`) resets the filters, pages in until the entry is
 * shown, expands it, scrolls it into view without motion and moves focus to it; an anchor that matches nothing
 * says so and moves no focus. Filter state is local and resets when the section unmounts.
 *
 * @param props See {@link ActivitySectionProps}.
 */
export function ActivitySection({
  server,
  jobs,
  operations,
  loading,
  failed,
  fetching,
  dataUpdatedAt,
  hasActiveJob,
  jobBusy,
  jobRequestFailed,
  onReload,
  onRetryJob,
  onSkipJob,
}: ActivitySectionProps) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
  const location = useLocation();
  const headingId = useId();
  const now = useNow(clockIntervalMs);
  const [filter, setFilter] = useState<ActivityFilter>("all");
  const [problemsOnly, setProblemsOnly] = useState(false);
  const [pageSize, setPageSize] = useState(pageStep);
  const [expandedIds, setExpandedIds] = useState<ReadonlySet<string>>(() => new Set());
  const [focusRequest, setFocusRequest] = useState<FocusRequest | null>(null);
  const [appliedLocationKey, setAppliedLocationKey] = useState<string | null>(null);
  const [entryMissing, setEntryMissing] = useState(false);

  const entries = buildActivityEntries(jobs, operations);
  const dataReady = !loading && !failed;

  // Apply an arriving activity deep link while rendering: the fragment is matched against the entry ids with
  // Array.find and never used as an index. Scroll and focus follow in the effect below.
  if (dataReady && appliedLocationKey !== location.key) {
    setAppliedLocationKey(location.key);
    if (location.hash.startsWith(activityAnchorPrefix)) {
      const anchor = location.hash.slice(1);
      const target = entries.find((entry) => entry.id === anchor);
      setProblemsOnly(false);
      setEntryMissing(target === undefined);
      if (target === undefined) {
        setFilter("all");
        setPageSize(pageStep);
      } else {
        const targetFilter: ActivityFilter = target.echo ? "audit" : "all";
        const position = filterEntries(entries, targetFilter, false).findIndex(
          (entry) => entry.id === target.id,
        );
        setFilter(targetFilter);
        setPageSize(Math.max(pageStep, Math.ceil((position + 1) / pageStep) * pageStep));
        setExpandedIds((current) => new Set(current).add(target.id));
        setFocusRequest({ id: target.id, scroll: true });
      }
    } else {
      setEntryMissing(false);
    }
  }

  useEffect(() => {
    if (focusRequest === null) return;
    const element = document.getElementById(focusRequest.id);
    if (element === null) return;
    if (focusRequest.scroll) element.scrollIntoView({ behavior: "auto" });
    element.focus();
  }, [focusRequest]);

  const toggleEntry = (id: string) =>
    setExpandedIds((current) => {
      const next = new Set(current);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  const changeFilter = (next: ActivityFilter) => {
    setFilter(next);
    setPageSize(pageStep);
    setFocusRequest(null);
  };
  const toggleProblemsOnly = () => {
    setProblemsOnly((current) => !current);
    setPageSize(pageStep);
    setFocusRequest(null);
  };
  const showAll = () => {
    setFilter("all");
    setProblemsOnly(false);
    setPageSize(pageStep);
    setFocusRequest(null);
  };

  const counts = filterCounts(entries, problemsOnly);
  const visible = filterEntries(entries, filter, problemsOnly);
  const shown = visible.slice(0, pageSize);
  const showMore = () => {
    const firstNew = visible[pageSize];
    setPageSize(pageSize + pageStep);
    if (firstNew !== undefined) setFocusRequest({ id: firstNew.id, scroll: false });
  };

  const collectLogsAction = server.actions.find((action) => action.id === collectLogsActionId);
  const protocols = protocolEntries([...jobs]);

  let content: ReactNode;
  if (loading) {
    content = <StatePanel kind="loading">{t("React_ServerAudit_Loading")}</StatePanel>;
  } else if (failed) {
    content = (
      <StatePanel
        kind="error"
        action={<ActionButton onClick={onReload}>{t("Server_Access_Retry")}</ActionButton>}
      >
        {t("Server_Activity_LoadError")}
      </StatePanel>
    );
  } else if (entries.length === 0) {
    content = (
      <StatePanel kind="empty" title={t("Server_Activity_Empty_Title")}>
        {t("Server_Activity_Empty_Body")}
      </StatePanel>
    );
  } else if (visible.length === 0) {
    const noProblems = problemsOnly && filter === "all";
    content = (
      <StatePanel
        kind="empty"
        {...(noProblems ? {} : { title: t("Server_Activity_NoMatch_Title") })}
        action={<ActionButton onClick={showAll}>{t("Server_Activity_ShowAll")}</ActionButton>}
      >
        {noProblems ? t("Server_Activity_NoProblems") : t("Server_Activity_NoMatch_Body")}
      </StatePanel>
    );
  } else {
    content = (
      <>
        <ActivityTimeline
          entries={shown}
          now={now}
          windowBoundary={auditWindowBoundary(operations)}
          busy={fetching}
          renderEntry={(entry) => (
            <ActivityEntry
              entry={entry}
              now={now}
              expanded={expandedIds.has(entry.id)}
              onToggle={() => toggleEntry(entry.id)}
              jobBusy={jobBusy}
              onRetryJob={onRetryJob}
              onSkipJob={onSkipJob}
            />
          )}
        />
        {visible.length > shown.length ? (
          <div className="activity-section__more">
            <ActionButton variant="ghost" onClick={showMore}>
              {t("Server_Activity_ShowMore")}
            </ActionButton>
          </div>
        ) : null}
      </>
    );
  }

  return (
    <section className="activity-section" aria-labelledby={headingId}>
      <header className="activity-section__header">
        <h2 id={headingId}>{t("Server_Workspace_Section_Activity")}</h2>
        <p>{t("Server_Activity_Description")}</p>
      </header>
      <div className="activity-toolbar">
        <div className="activity-toolbar__filters">
          <FilterChipGroup
            label={t("Server_Activity_FilterLabel")}
            items={filterOrder.map((id) => ({
              id,
              label: t(filterLabelKeys[id]),
              ...(dataReady ? { count: counts[id] } : {}),
            }))}
            value={filter}
            onChange={changeFilter}
          />
          <ActionButton
            variant="ghost"
            className="filter-chip activity-toolbar__problems"
            aria-pressed={problemsOnly}
            onClick={toggleProblemsOnly}
          >
            {problemsOnly ? (
              <Checkmark16Regular className="filter-chip__check" aria-hidden="true" />
            ) : (
              <Filter20Regular aria-hidden="true" />
            )}
            {t("Server_Activity_ProblemsOnly")}
          </ActionButton>
        </div>
        <div className="activity-toolbar__live">
          <LiveBadge
            dataUpdatedAt={dataUpdatedAt}
            hasActiveJob={hasActiveJob}
            onRefresh={onReload}
          />
        </div>
        <div className="activity-toolbar__actions">
          {collectLogsAction ? (
            <ActionButton
              variant="ghost"
              className="activity-toolbar__action"
              unavailableReason={
                actionIsUnavailable(collectLogsAction) ? (
                  <ActionUnavailableReason server={server} action={collectLogsAction} />
                ) : undefined
              }
              onClick={() =>
                request({
                  serverId: server.primaryKey,
                  actionId: collectLogsAction.id,
                  source: "workspace-action",
                })
              }
            >
              {t("Server_Action_CollectLogs_Label")}
            </ActionButton>
          ) : null}
          {protocols.length > 0 ? (
            <ActionAnchor
              variant="ghost"
              className="activity-toolbar__action"
              download="utf-server-protocols.txt"
              href={textDownloadUrl(protocols.map(buildCommandProtocol).join(protocolSeparator))}
            >
              {t("Server_Activity_DownloadAll")}
            </ActionAnchor>
          ) : (
            <ActionButton
              variant="ghost"
              className="activity-toolbar__action"
              unavailableReason={t("React_ServerWorkspace_NoDiagnostic")}
            >
              {t("Server_Activity_DownloadAll")}
            </ActionButton>
          )}
        </div>
      </div>
      {jobRequestFailed ? (
        <p role="alert" className="activity-section__error">
          {t("Server_Failure_RequestError")}
        </p>
      ) : null}
      {entryMissing ? (
        <StatePanel kind="info">{t("Server_Activity_EntryMissing")}</StatePanel>
      ) : null}
      {content}
    </section>
  );
}
