import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams, useSearchParams } from "react-router";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { ConfirmDialog, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { getHostingPermissions } from "@/features/hosting/authorization";
import {
  ActionConfirmation,
  HostingAccessBoundary,
  HostingError,
  HostingLoading,
  ServerActions,
  ServerStatus,
} from "@/features/hosting/components";
import { useHostingCopy } from "@/features/hosting/copy";
import { ServerConfigurationPanel } from "@/features/hosting/ServerConfigurationPanel";
import { ServerFilesPanel } from "@/features/hosting/ServerFilesPanel";
import { ServerLogsPanel } from "@/features/hosting/ServerLogsPanel";
import { ServerRuntimePanel } from "@/features/hosting/ServerRuntimePanel";
import { formatBytes, formatDate, formatMoney, humanizeToken } from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  GameServer,
  HostingOperation,
  LifecycleJob,
  ServerAction,
} from "@/features/hosting/types";
import {
  buildCommandProtocol,
  primaryServerAction,
  textDownloadUrl,
} from "@/features/hosting/workspaceModel";

type WorkspaceTab =
  "overview" | "runtime" | "cloud" | "logs" | "files" | "configuration" | "jobs" | "audit";

function JobList({
  jobs,
  busy,
  onRetry,
  onSkip,
}: {
  jobs: LifecycleJob[];
  busy: boolean;
  onRetry(jobId: string): void;
  onSkip(jobId: string): void;
}) {
  const c = useHostingCopy();
  const { t } = useTranslation();
  if (jobs.length === 0) return <StatePanel kind="empty">{c.noJobs}</StatePanel>;
  return (
    <div className="next-hosting-job-list">
      {[...jobs]
        .sort((left, right) => right.timeStampCreated.localeCompare(left.timeStampCreated))
        .map((job) => (
          <article className="next-hosting-job" key={job.primaryKey}>
            <header>
              <div>
                <strong>{humanizeToken(job.actionId)}</strong>
                <span>
                  {humanizeToken(job.kind)} · {humanizeToken(job.status)} ·{" "}
                  {humanizeToken(job.stage)}
                </span>
              </div>
              <time dateTime={job.timeStampUpdated}>{formatDate(job.timeStampUpdated)}</time>
            </header>
            <progress max={100} value={Math.min(100, Math.max(0, job.progressPercent))} />
            <div className="next-hosting-job-meta">
              <span>
                {c.progress}: {job.progressPercent}%
              </span>
              <span>
                {c.attempts}: {job.attemptCount}
              </span>
              <code>{job.primaryKey}</code>
            </div>
            {job.failureReason && (
              <p className="next-hosting-inline-error">
                {job.failureReason}
                {job.failureCode ? ` · ${job.failureCode}` : ""}
              </p>
            )}
            {(job.canRetry || job.canSkip) && (
              <div className="next-hosting-form-actions">
                {job.canRetry && (
                  <button
                    className="button button--primary"
                    disabled={busy}
                    onClick={() => onRetry(job.primaryKey)}
                  >
                    {c.retryJob}
                  </button>
                )}
                {job.canSkip && (
                  <button
                    className="button button--ghost"
                    disabled={busy}
                    onClick={() => onSkip(job.primaryKey)}
                  >
                    {c.skipJob}
                  </button>
                )}
              </div>
            )}
            {job.commands.length > 0 && (
              <details>
                <summary>
                  {formatResource(t("Hosting_Detail_Commands"), job.commands.length)}
                </summary>
                <div className="next-hosting-command-list">
                  <a
                    className="button button--ghost"
                    href={textDownloadUrl(
                      job.commands
                        .map((command) => buildCommandProtocol({ job, command }))
                        .join("\n\n---\n\n"),
                    )}
                    download={`utf-lifecycle-${job.primaryKey}.txt`}
                  >
                    {t("React_ServerWorkspace_DownloadProtocol")}
                  </a>
                  {[...job.commands]
                    .sort((left, right) => left.sequence - right.sequence)
                    .map((command) => (
                      <article key={command.primaryKey}>
                        <strong>
                          #{command.sequence} · {humanizeToken(command.actionId)}
                        </strong>
                        <span>
                          {humanizeToken(command.status)} · {humanizeToken(command.stage)}
                        </span>
                        {command.failureReason && <small>{command.failureReason}</small>}
                        {command.diagnosticOutput && (
                          <pre tabIndex={0}>{command.diagnosticOutput}</pre>
                        )}
                      </article>
                    ))}
                </div>
              </details>
            )}
          </article>
        ))}
    </div>
  );
}

function AuditList({ operations }: { operations: HostingOperation[] }) {
  const c = useHostingCopy();
  const { t } = useTranslation();
  if (operations.length === 0) return <StatePanel kind="empty">{c.noAudit}</StatePanel>;
  return (
    <ol className="next-hosting-audit-list">
      {operations.map((operation) => (
        <li key={operation.primaryKey}>
          <span className="next-hosting-audit-marker" aria-hidden="true" />
          <div>
            <header>
              <strong>{operation.summary || humanizeToken(operation.kind)}</strong>
              <StatusBadge
                tone={
                  operation.status.toLowerCase().includes("fail")
                    ? "danger"
                    : operation.status.toLowerCase().includes("warning")
                      ? "warning"
                      : "neutral"
                }
              >
                {humanizeToken(operation.status)}
              </StatusBadge>
            </header>
            {operation.detail && <p>{operation.detail}</p>}
            <small>
              {formatDate(operation.timeStampCreated)} ·{" "}
              {operation.providerIdentifier || "control-plane"}
              {operation.resourceKind ? ` · ${operation.resourceKind}` : ""}
            </small>
            {operation.resourceIdentifier && <code>{operation.resourceIdentifier}</code>}
            {operation.traceId && (
              <small>
                {t("Hosting_Detail_OperationsTrace")}: <code>{operation.traceId}</code>
              </small>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}

function CloudPanel({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const c = useHostingCopy();
  const deployment = server.deployment;
  if (!deployment)
    return (
      <section className="next-hosting-panel">
        <span className="kicker">{t("Hosting_Detail_DeploymentKicker")}</span>
        <h2>{t("Hosting_Detail_DeploymentTitle")}</h2>
        <StatePanel kind="empty">{t("Hosting_Detail_NoDeployment")}</StatePanel>
      </section>
    );
  const used =
    deployment.diskTotalBytes !== null && deployment.diskAvailableBytes !== null
      ? deployment.diskTotalBytes - deployment.diskAvailableBytes
      : undefined;
  return (
    <section className="next-hosting-panel">
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">{t("Hosting_Detail_DeploymentKicker")}</span>
          <h2>{t("Hosting_Detail_DeploymentTitle")}</h2>
        </div>
        <StatusBadge tone={deployment.possibleCost ? "warning" : "neutral"}>
          {humanizeToken(deployment.status)}
        </StatusBadge>
      </header>
      <dl className="next-hosting-facts">
        <div>
          <dt>{c.stage}</dt>
          <dd>{humanizeToken(deployment.stage)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Detail_Worker")}</dt>
          <dd>{humanizeToken(deployment.workerStatus)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Detail_LastHeartbeat")}</dt>
          <dd>{formatDate(deployment.lastHeartbeat)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Detail_ConnectionEndpoint")}</dt>
          <dd>
            <code>{deployment.connectionEndpoint ?? deployment.publicAddress ?? "—"}</code>
            {deployment.connectionProtocol && ` (${deployment.connectionProtocol})`}
          </dd>
        </div>
        <div>
          <dt>{t("React_ServerWorkspace_DeploymentFilesystem")}</dt>
          <dd>
            <code>{deployment.deploymentRoot}</code>
          </dd>
        </div>
        <div>
          <dt>{t("React_ServerWorkspace_ProvisionedStorage")}</dt>
          <dd>
            {formatBytes(
              deployment.selectedExternalStorageBytes ?? deployment.includedStorageBytes,
            )}
          </dd>
        </div>
        <div>
          <dt>{t("React_ServerWorkspace_CloudComputeEstimate")}</dt>
          <dd>{formatMoney(deployment.estimatedComputeHourlyCostEur)}/h</dd>
        </div>
        <div>
          <dt>{t("React_ServerWorkspace_CloudStorageEstimate")}</dt>
          <dd>{formatMoney(deployment.estimatedStorageHourlyCostEur)}/h</dd>
        </div>
      </dl>
      {used !== undefined && deployment.diskTotalBytes !== null && (
        <section className="next-hosting-storage-telemetry">
          <h3>{t("React_ServerWorkspace_WorkerDiskTelemetry")}</h3>
          <progress max={deployment.diskTotalBytes} value={used} />
          <dl className="next-hosting-facts">
            <div>
              <dt>{t("Hosting_Detail_DiskUsed")}</dt>
              <dd>{formatBytes(used)}</dd>
            </div>
            <div>
              <dt>{t("Hosting_Detail_DiskFree")}</dt>
              <dd>{formatBytes(deployment.diskAvailableBytes)}</dd>
            </div>
            <div>
              <dt>{t("Hosting_Detail_DiskTotal")}</dt>
              <dd>{formatBytes(deployment.diskTotalBytes)}</dd>
            </div>
            <div>
              <dt>{t("Hosting_Detail_DiskObserved")}</dt>
              <dd>{formatDate(deployment.diskTimeStampObserved)}</dd>
            </div>
          </dl>
        </section>
      )}
      {deployment.possibleCost && (
        <StatePanel kind="warning">{t("Hosting_Detail_PossibleCost")}</StatePanel>
      )}
      {deployment.faultSummary && <StatePanel kind="error">{deployment.faultSummary}</StatePanel>}
      {deployment.administrativeSshCommand && (
        <details className="next-hosting-diagnostics">
          <summary>{t("React_ServerWorkspace_WorkerDiagnostics")}</summary>
          <p>
            <code>{deployment.administrativeSshCommand}</code>
          </p>
          <ul>
            <li>
              <code>sudo cloud-init status --long</code>
            </li>
            <li>
              <code>sudo journalctl -u cloud-final --no-pager -n 200</code>
            </li>
            <li>
              <code>sudo systemctl --failed; sudo systemctl list-units 'utf-*'</code>
            </li>
            <li>
              <code>
                sudo docker ps -a; sudo docker logs --tail 200 $(sudo docker ps -aq --filter
                name=utf-cloud-worker)
              </code>
            </li>
          </ul>
        </details>
      )}
      <h3>{formatResource(t("Hosting_Detail_ProviderResources"), deployment.resources.length)}</h3>
      {deployment.resources.length === 0 ? (
        <p>{t("React_ServerWorkspace_NoProviderResources")}</p>
      ) : (
        <div className="next-hosting-resource-grid">
          {deployment.resources.map((resource) => (
            <article key={resource.primaryKey}>
              <strong>{humanizeToken(resource.kind)}</strong>
              <StatusBadge tone={resource.possibleCost ? "warning" : "neutral"}>
                {humanizeToken(resource.state)}
              </StatusBadge>
              <code>{resource.providerResourceIdentifier ?? "—"}</code>
              <small>
                {resource.providerState ?? "—"} · {formatBytes(resource.provisionedCapacityBytes)} ·{" "}
                {formatMoney(resource.estimatedHourlyCostEur)}/h
              </small>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

export function ServerWorkspaceView({
  server,
  jobs,
  operations,
  actionBusy,
  jobBusy,
  onExecuteAction,
  onRetryJob,
  onSkipJob,
}: {
  server: GameServer;
  jobs: LifecycleJob[];
  operations: HostingOperation[];
  actionBusy: boolean;
  jobBusy: boolean;
  onExecuteAction(action: ServerAction, confirmation?: string): Promise<void>;
  onRetryJob(jobId: string): void;
  onSkipJob(jobId: string): void;
}) {
  const c = useHostingCopy();
  const { t } = useTranslation();
  const [tab, setTab] = useState<WorkspaceTab>("overview");
  const [selectedAction, setSelectedAction] = useState<ServerAction | null>(null);
  const [selectedActionError, setSelectedActionError] = useState<string>();
  const primary = primaryServerAction(server.actions, server.deployment?.possibleCost ?? false);
  const tabs: [WorkspaceTab, string][] = [
    ["overview", c.overview],
    ["runtime", c.runtimeTab],
    ["cloud", c.cloud],
    ["logs", t("Hosting_Detail_TabLogs")],
    ["files", t("Hosting_Detail_TabFiles")],
    ["configuration", t("React_ServerWorkspace_Configuration")],
    ["jobs", `${c.jobs}${server.pendingJobCount ? ` (${server.pendingJobCount})` : ""}`],
    ["audit", c.audit],
  ];
  return (
    <>
      <nav className="next-hosting-tabs" aria-label={t("Hosting_Detail_WorkspaceNavigation")}>
        {tabs.map(([id, label]) => (
          <button
            key={id}
            type="button"
            aria-current={tab === id ? "page" : undefined}
            className={
              tab === id ? "next-hosting-tab next-hosting-tab--active" : "next-hosting-tab"
            }
            onClick={() => setTab(id)}
          >
            {label}
          </button>
        ))}
      </nav>
      {tab === "overview" && (
        <div className="next-hosting-runtime-stack">
          {primary && (
            <section className="next-hosting-panel next-hosting-primary-action">
              <div>
                <span className="kicker">{t("Hosting_Detail_NextAction")}</span>
                <h2>{primary.label || humanizeToken(primary.id)}</h2>
                <p>{primary.description}</p>
                {primary.costDescription && <small>{primary.costDescription}</small>}
              </div>
              <button
                type="button"
                className={primary.dangerous ? "button button--danger" : "button button--primary"}
                disabled={actionBusy || !primary.enabled || primary.pending}
                onClick={() => setSelectedAction(primary)}
              >
                {primary.pending ? c.actionPending : primary.label || c.execute}
              </button>
            </section>
          )}
          <div className="next-hosting-workspace-grid">
            <section className="next-hosting-panel">
              <header className="next-hosting-section-heading">
                <div>
                  <span className="kicker">{t("Hosting_Detail_RecordKicker")}</span>
                  <h2>{t("Hosting_Detail_RecordTitle")}</h2>
                </div>
                <ServerStatus server={server} />
              </header>
              <dl className="next-hosting-facts">
                <div>
                  <dt>{c.costOwner}</dt>
                  <dd>{server.costOwnerName}</dd>
                </div>
                <div>
                  <dt>{t("Hosting_Detail_LaunchPolicy")}</dt>
                  <dd>
                    {server.isLaunchDisabled
                      ? t("Hosting_Detail_LaunchDisabled")
                      : t("Hosting_Detail_LaunchEnabled")}
                  </dd>
                </div>
                <div>
                  <dt>{t("Hosting_Detail_CloudProfile")}</dt>
                  <dd>
                    {server.runtimeProfileDisplayName}
                    <small>
                      <code>{server.runtimeProfileIdentifier}</code>
                    </small>
                  </dd>
                </div>
                <div>
                  <dt>{t("Hosting_Detail_EstimatedCost")}</dt>
                  <dd>
                    {formatMoney(server.estimatedHourlyCostEur)}/h ·{" "}
                    {formatMoney(server.estimatedMonthlyCostEur)}/month
                  </dd>
                </div>
                <div>
                  <dt>{t("React_ServerWorkspace_ConfigurationProfile")}</dt>
                  <dd>
                    {server.selectedConfigurationProfileTitle ??
                      t("React_ServerWorkspace_GameDefaults")}
                  </dd>
                </div>
                <div>
                  <dt>{t("Hosting_Detail_ModPackTitle")}</dt>
                  <dd>
                    {server.selectedModPackDefinitionTitle ?? t("React_ServerWorkspace_None")}
                  </dd>
                </div>
                <div>
                  <dt>{t("React_ServerWorkspace_Created")}</dt>
                  <dd>{formatDate(server.timeStampCreated)}</dd>
                </div>
                <div>
                  <dt>{c.updated}</dt>
                  <dd>{formatDate(server.timeStampUpdated)}</dd>
                </div>
              </dl>
            </section>
            <section className="next-hosting-panel">
              <span className="kicker">{t("Hosting_Detail_DeploymentKicker")}</span>
              <h2>
                {server.deployment
                  ? humanizeToken(server.deployment.status)
                  : t("Hosting_Detail_StateOffline")}
              </h2>
              <p>
                {server.deployment?.stage ? humanizeToken(server.deployment.stage) : c.noDeployment}
              </p>
              {server.deployment?.connectionEndpoint && (
                <code>{server.deployment.connectionEndpoint}</code>
              )}
              {server.deployment?.possibleCost && (
                <StatePanel kind="warning">{t("Hosting_Detail_PossibleCost")}</StatePanel>
              )}
            </section>
          </div>
          <section className="next-hosting-panel">
            <span className="kicker">{t("Hosting_Detail_ActionsKicker")}</span>
            <h2>{t("Hosting_Detail_ActionsTitle")}</h2>
            <ServerActions
              actions={server.actions.filter((action) => action.id !== "collect-logs")}
              busy={actionBusy}
              onExecute={onExecuteAction}
            />
          </section>
          <section className="next-hosting-panel">
            <span className="kicker">{t("React_ServerWorkspace_RecentActivity")}</span>
            <h2>{t("Hosting_Detail_OperationsTitle")}</h2>
            <AuditList operations={operations.slice(0, 6)} />
          </section>
        </div>
      )}
      {tab === "runtime" && <ServerRuntimePanel server={server} />}
      {tab === "cloud" && <CloudPanel server={server} />}
      {tab === "logs" && (
        <ServerLogsPanel
          jobs={jobs}
          collectLogsAction={server.actions.find((action) => action.id === "collect-logs")}
          busy={actionBusy}
          onCollect={setSelectedAction}
        />
      )}
      {tab === "files" && <ServerFilesPanel server={server} />}
      {tab === "configuration" && (
        <ServerConfigurationPanel serverId={server.primaryKey} canModify={server.canModify} />
      )}
      {tab === "jobs" && (
        <section className="next-hosting-panel">
          <span className="kicker">{t("Hosting_Detail_QueueKicker")}</span>
          <h2>{t("Hosting_Detail_QueueTitle")}</h2>
          <JobList jobs={jobs} busy={jobBusy} onRetry={onRetryJob} onSkip={onSkipJob} />
        </section>
      )}
      {tab === "audit" && (
        <section className="next-hosting-panel">
          <span className="kicker">{t("Hosting_Detail_OperationsKicker")}</span>
          <h2>{t("Hosting_Detail_OperationsTitle")}</h2>
          <AuditList operations={operations} />
        </section>
      )}
      <ActionConfirmation
        action={selectedAction}
        busy={actionBusy}
        {...(selectedActionError ? { error: selectedActionError } : {})}
        onCancel={() => {
          if (!actionBusy) setSelectedAction(null);
        }}
        onExecute={(confirmation) => {
          if (!selectedAction) return;
          setSelectedActionError(undefined);
          void onExecuteAction(selectedAction, confirmation)
            .then(() => setSelectedAction(null))
            .catch((error: unknown) =>
              setSelectedActionError(error instanceof Error ? error.message : c.loadFailed),
            );
        }}
      />
    </>
  );
}

export function ServerWorkspacePage() {
  const c = useHostingCopy();
  const { t } = useTranslation();
  const { serverId = "" } = useParams();
  const navigate = useNavigate();
  const [search, setSearch] = useSearchParams();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  const client = useQueryClient();
  const server = useQuery({
    queryKey: hostingQueryKeys.server(serverId),
    queryFn: ({ signal }) => hostingService.server(serverId, signal),
    enabled: permissions.accessServers && Boolean(serverId),
    refetchInterval: 5_000,
  });
  const jobs = useQuery({
    queryKey: hostingQueryKeys.jobs(serverId),
    queryFn: ({ signal }) => hostingService.jobs(serverId, signal),
    enabled: permissions.accessServers && Boolean(serverId),
    refetchInterval: 5_000,
  });
  const operations = useQuery({
    queryKey: hostingQueryKeys.operations(serverId),
    queryFn: ({ signal }) => hostingService.operations(serverId, 80, signal),
    enabled: permissions.accessServers && Boolean(serverId),
    refetchInterval: 5_000,
  });
  const [deleteOpen, setDeleteOpen] = useState(false);
  const requestedActionId = search.get("action");
  const requestedAction =
    server.data?.actions.find(
      (candidate) =>
        candidate.id.toLowerCase() === requestedActionId?.toLowerCase() &&
        candidate.enabled &&
        !candidate.pending &&
        Boolean(candidate.confirmationChallenge),
    ) ?? null;
  const refresh = async () => {
    await Promise.all([
      client.invalidateQueries({ queryKey: hostingQueryKeys.fleet }),
      client.invalidateQueries({ queryKey: hostingQueryKeys.server(serverId) }),
      client.invalidateQueries({ queryKey: hostingQueryKeys.jobs(serverId) }),
      client.invalidateQueries({ queryKey: hostingQueryKeys.operations(serverId) }),
      client.invalidateQueries({ queryKey: hostingQueryKeys.notifications }),
    ]);
  };
  const action = useMutation({
    mutationFn: ({
      item,
      confirmation,
    }: {
      item: ServerAction;
      confirmation: string | undefined;
    }) => hostingService.executeAction(serverId, item.id, confirmation),
    onSuccess: refresh,
  });
  const intervention = useMutation({
    mutationFn: ({ jobId, kind }: { jobId: string; kind: "retry" | "skip" }) =>
      kind === "retry" ? hostingService.retryJob(jobId) : hostingService.skipJob(jobId),
    onSuccess: refresh,
  });
  const deletion = useMutation({
    mutationFn: () => hostingService.delete(serverId),
    onSuccess: () => navigate("/command/servers", { replace: true }),
  });
  const closeRequestedAction = () => {
    const next = new URLSearchParams(search);
    next.delete("action");
    setSearch(next, { replace: true });
  };
  const canDelete = Boolean(
    server.data?.canModify &&
    !server.data.isArchived &&
    !server.data.deployment &&
    server.data.pendingJobCount === 0 &&
    (jobs.data ?? []).every((job) => !job.canRetry && !job.canSkip),
  );
  return (
    <div className="next-hosting-page">
      <HostingAccessBoundary allowed={permissions.accessServers}>
        {authLoading || server.isLoading ? (
          <HostingLoading />
        ) : server.isError ? (
          <HostingError message={server.error.message} onRetry={() => void server.refetch()} />
        ) : !server.data ? (
          <StatePanel
            kind="empty"
            action={
              <Link className="button button--ghost" to="/command/servers">
                {c.backToFleet}
              </Link>
            }
          >
            {c.notFound}
          </StatePanel>
        ) : (
          <>
            <PageHeader
              kicker={`${server.data.gameIdentifier} · ${server.data.runtimeProviderIdentifier}`}
              title={server.data.title}
              description={t("Hosting_Detail_Description")}
              actions={
                <>
                  <button
                    className="button button--ghost"
                    disabled={server.isFetching}
                    onClick={() => void refresh()}
                  >
                    {c.refresh}
                  </button>
                  <Link className="button button--ghost" to="/command/servers">
                    {c.backToFleet}
                  </Link>
                  {server.data.canModify && !server.data.isArchived && (
                    <button
                      className="button button--danger"
                      type="button"
                      disabled={!canDelete || deletion.isPending}
                      title={
                        canDelete
                          ? t("React_ServerWorkspace_ArchiveRecord")
                          : t("Hosting_Detail_DeleteUnavailable")
                      }
                      onClick={() => setDeleteOpen(true)}
                    >
                      {t("Hosting_Detail_DeleteServer")}
                    </button>
                  )}
                </>
              }
            />
            {(jobs.isError ||
              operations.isError ||
              action.error ||
              intervention.error ||
              deletion.error) && (
              <p className="next-hosting-inline-error" role="alert">
                {
                  (
                    jobs.error ??
                    operations.error ??
                    action.error ??
                    intervention.error ??
                    deletion.error
                  )?.message
                }
              </p>
            )}
            <ServerWorkspaceView
              server={server.data}
              jobs={jobs.data ?? []}
              operations={operations.data ?? []}
              actionBusy={action.isPending}
              jobBusy={intervention.isPending}
              onExecuteAction={async (item, confirmation) => {
                await action.mutateAsync({ item, confirmation });
              }}
              onRetryJob={(jobId) => intervention.mutate({ jobId, kind: "retry" })}
              onSkipJob={(jobId) => intervention.mutate({ jobId, kind: "skip" })}
            />
            <ActionConfirmation
              action={requestedAction}
              busy={action.isPending}
              {...(action.error?.message ? { error: action.error.message } : {})}
              onCancel={closeRequestedAction}
              onExecute={(confirmation) => {
                if (!requestedAction) return;
                action.mutate(
                  { item: requestedAction, confirmation },
                  { onSuccess: closeRequestedAction },
                );
              }}
            />
            <ConfirmDialog
              open={deleteOpen}
              title={t("Hosting_Detail_DeleteServer")}
              description={formatResource(
                t("Hosting_Detail_DeleteServerConfirm"),
                server.data.title,
              )}
              confirmLabel={
                deletion.isPending
                  ? t("React_ServerWorkspace_Deleting")
                  : t("Hosting_Detail_DeleteServer")
              }
              cancelLabel={t("Common_Cancel")}
              onCancel={() => setDeleteOpen(false)}
              onConfirm={() => deletion.mutate()}
            />
          </>
        )}
      </HostingAccessBoundary>
    </div>
  );
}
