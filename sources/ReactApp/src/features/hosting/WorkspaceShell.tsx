import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { Breadcrumbs } from "@/components/Breadcrumbs";
import { StatePanel } from "@/components/primitives";
import { SectionNavigation } from "@/components/SectionNavigation";
import { CostStrip } from "@/components/server/CostStrip";
import { JobFailurePanel } from "@/components/server/JobFailurePanel";
import { JobProgressStrip } from "@/components/server/JobProgressStrip";
import { ServerEndpoint } from "@/components/server/ServerEndpoint";
import { pausedReason } from "@/components/server/unavailableReasonMapping";
import { WorkspaceHeader } from "@/components/server/WorkspaceHeader";
import "@/components/server/workspace-shell.css";
import { ActivitySection } from "@/features/hosting/sections/ActivitySection";
import { MachineSection } from "@/features/hosting/sections/MachineSection";
import { OverviewSection } from "@/features/hosting/sections/OverviewSection";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
import { ServerFilesPanel } from "@/features/hosting/ServerFilesPanel";
import type { GameServer, HostingOperation, LifecycleJob } from "@/features/hosting/types";
import {
  sectionTo,
  workspaceSectionLabelKeys,
  workspaceTabIds,
  type WorkspaceSection,
} from "@/features/hosting/workspaceSections";

/** Props of {@link WorkspaceShell}. */
export interface WorkspaceShellProps {
  /** The loaded server the workspace shows. */
  server: GameServer;
  /** The section selected by the `tab` search parameter. */
  section: WorkspaceSection;
  /** The loaded lifecycle jobs of the server; empty while they have not loaded. */
  jobs: LifecycleJob[];
  /** The loaded hosting operations of the server; empty while they have not loaded. */
  operations: HostingOperation[];
  /** Whether the jobs or operations are loading for the first time. */
  activityLoading: boolean;
  /** Whether loading the jobs or operations failed without any data to show. */
  activityFailed: boolean;
  /** Whether a background refetch of the jobs or operations is running. */
  activityFetching: boolean;
  /** The time of the newest successful fetch of the jobs and operations in epoch milliseconds; 0 when none. */
  activityUpdatedAt: number;
  /** Whether a lifecycle job is active. */
  hasActiveJob: boolean;
  /** Whether a workspace query is fetching; Refresh server is then disabled. */
  refreshing: boolean;
  /** Whether the last Refresh server request failed. */
  refreshFailed: boolean;
  /** Called when the viewer asks to refresh the server. */
  onRefresh(): void;
  /** Called when the viewer asks to load the jobs and operations again. */
  onReloadActivity(): void;
  /** Whether a retry or skip request is in flight. */
  jobBusy: boolean;
  /** Whether the last retry or skip request failed. */
  jobRequestFailed: boolean;
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
  /** Extra notices the page contributes, such as the deep-link notice. */
  notices?: ReactNode;
  /** The polite announcement of a job outcome observed while the page is open; empty when there is none. */
  announcement?: string;
}

/**
 * The shell of the server workspace: breadcrumbs, the sticky header, the details block, the section navigation and
 * the routed section content.
 *
 * The shell renders in every section; only the content below the navigation changes with `section`. The section
 * router is a closed switch over {@link WorkspaceSection}, so a value outside the registry can never select content.
 * Between the details block and the navigation it shows the paused-by-operator and archived notices, the page's own
 * notices, the progress strip of an active job and the failure panel of a failed newest job. A visually hidden
 * polite region at the end carries the announcement of a job outcome.
 *
 * @param props See {@link WorkspaceShellProps}.
 */
export function WorkspaceShell({
  server,
  section,
  jobs,
  operations,
  activityLoading,
  activityFailed,
  activityFetching,
  activityUpdatedAt,
  hasActiveJob,
  refreshing,
  refreshFailed,
  onRefresh,
  onReloadActivity,
  jobBusy,
  jobRequestFailed,
  onRetryJob,
  onSkipJob,
  notices,
  announcement = "",
}: WorkspaceShellProps) {
  const { t } = useTranslation();
  let content: ReactNode;
  switch (section) {
    case "activity":
      content = (
        <ActivitySection
          server={server}
          jobs={jobs}
          operations={operations}
          loading={activityLoading}
          failed={activityFailed}
          fetching={activityFetching}
          dataUpdatedAt={activityUpdatedAt}
          hasActiveJob={hasActiveJob}
          jobBusy={jobBusy}
          jobRequestFailed={jobRequestFailed}
          onReload={onReloadActivity}
          onRetryJob={onRetryJob}
          onSkipJob={onSkipJob}
        />
      );
      break;
    case "settings":
      content = <SettingsSection server={server} jobs={jobs} />;
      break;
    case "files":
      content = <ServerFilesPanel server={server} />;
      break;
    case "machine":
      content = <MachineSection server={server} />;
      break;
    default:
      content = <OverviewSection server={server} jobs={jobs} operations={operations} />;
      break;
  }
  return (
    <div className="workspace-shell">
      <Breadcrumbs
        label={t("Server_Workspace_Breadcrumb_Label")}
        items={[
          { label: t("Hosting_Servers_Title"), to: "/command/servers" },
          { label: server.title, to: sectionTo(server.primaryKey, "overview"), userContent: true },
          { label: t(workspaceSectionLabelKeys[section]) },
        ]}
      />
      <WorkspaceHeader server={server} />
      <section className="workspace-details" aria-label={t("Server_Workspace_Details_Label")}>
        <CostStrip server={server} />
        <ServerEndpoint server={server} />
        <div className="workspace-details__refresh">
          <ActionButton variant="ghost" disabled={refreshing} onClick={onRefresh}>
            {t("Server_Workspace_Refresh")}
          </ActionButton>
          {refreshFailed && <p role="alert">{t("Server_Failure_RequestError")}</p>}
        </div>
      </section>
      {server.actions.some((action) => action.unavailableReason === pausedReason) && (
        <StatePanel kind="info">{t("Server_Reason_Paused")}</StatePanel>
      )}
      {server.isArchived && <StatePanel kind="info">{t("Server_Workspace_Archived")}</StatePanel>}
      {notices}
      <JobProgressStrip server={server} jobs={jobs} />
      <JobFailurePanel
        server={server}
        jobs={jobs}
        busy={jobBusy}
        requestFailed={jobRequestFailed}
        onRetry={onRetryJob}
        onSkip={onSkipJob}
      />
      <SectionNavigation
        label={t("Hosting_Detail_WorkspaceNavigation")}
        currentId={section}
        items={workspaceTabIds.map((id) => ({
          id,
          label: t(workspaceSectionLabelKeys[id]),
          to: sectionTo(server.primaryKey, id),
        }))}
      />
      {content}
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {announcement}
      </div>
    </div>
  );
}
