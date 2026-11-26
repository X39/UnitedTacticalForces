import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useNavigate, useParams, useSearchParams } from "react-router";
import { useAuth } from "@/auth/auth";
import { ActionLink } from "@/components/ActionControl";
import { PageHeader } from "@/components/primitives";
import { actionIsUnavailable } from "@/components/server/ActionUnavailableReason";
import { AutoStopNotice } from "@/components/server/AutoStopNotice";
import { CostAlertBanner } from "@/components/server/CostAlertBanner";
import { CreateStartNotice } from "@/components/server/CreateStartNotice";
import { DeepLinkActionNotice } from "@/components/server/DeepLinkActionNotice";
import { PendingStartNotice } from "@/components/server/PendingStartNotice";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { AccessBoundary, EmptyState, ErrorState, LoadingState } from "@/components/StateViews";
import { activityOperationsTake } from "@/features/hosting/activityModel";
import { getHostingPermissions } from "@/features/hosting/authorization";
import { hasCreateStartIntent } from "@/features/hosting/createStartIntent";
import { hasActiveJob, workspaceFallbackPollMs } from "@/features/hosting/jobProgress";
import { deriveServerState } from "@/features/hosting/serverState";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, LifecycleJob } from "@/features/hosting/types";
import { useAccruedLive } from "@/features/hosting/useAccruedLive";
import { useAccruedStoppedAnnouncement } from "@/features/hosting/useAccruedStoppedAnnouncement";
import { useJobAnnouncement } from "@/features/hosting/useJobAnnouncement";
import { useWorkspaceLiveUpdates } from "@/features/hosting/useWorkspaceLiveUpdates";
import { WorkspaceShell } from "@/features/hosting/WorkspaceShell";
import { workspaceSectionFromSearch } from "@/features/hosting/workspaceSections";

/**
 * The route component of the server workspace.
 *
 * It owns the server, jobs and operations queries, the `?action=` deep link and the retry and skip mutation, and
 * hands everything the view needs to {@link WorkspaceShell}. While the server loads only the loading panel renders;
 * a load failure shows the shared error panel without the raw error text (above the stale workspace when data is
 * still held); a missing server shows the not-found panel with a link back to the fleet. Every state other than the
 * workspace itself renders the route heading above its panel.
 */
export function ServerWorkspacePage() {
  const { t } = useTranslation();
  const { serverId = "" } = useParams();
  const [search, setSearch] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { request } = useCostConfirmation();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  const client = useQueryClient();
  useWorkspaceLiveUpdates(serverId);
  // The fallback poll covers what no signal announces (stage changes, command output, audit records). It runs only
  // while a job is active, judged from the cached server and jobs at each evaluation.
  const pollWhileActive = () =>
    hasActiveJob(
      client.getQueryData<GameServer | null>(hostingQueryKeys.server(serverId)),
      client.getQueryData<LifecycleJob[]>(hostingQueryKeys.jobs(serverId)),
    )
      ? workspaceFallbackPollMs
      : false;
  const server = useQuery({
    queryKey: hostingQueryKeys.server(serverId),
    // A missing server resolves to null: query data must not be undefined, and null selects the not-found panel.
    queryFn: async ({ signal }) => (await hostingService.server(serverId, signal)) ?? null,
    enabled: permissions.accessServers && Boolean(serverId),
    refetchInterval: pollWhileActive,
  });
  const jobs = useQuery({
    queryKey: hostingQueryKeys.jobs(serverId),
    queryFn: ({ signal }) => hostingService.jobs(serverId, signal),
    enabled: permissions.accessServers && Boolean(serverId),
    refetchInterval: pollWhileActive,
  });
  const operations = useQuery({
    queryKey: hostingQueryKeys.operations(serverId),
    queryFn: ({ signal }) => hostingService.operations(serverId, activityOperationsTake, signal),
    enabled: permissions.accessServers && Boolean(serverId),
    refetchInterval: pollWhileActive,
  });

  // The `action` parameter is untrusted: actions are found with Array.find, never by indexing with it.
  const requestedActionId = search.get("action") || null;
  const requestedAction =
    requestedActionId === null
      ? undefined
      : server.data?.actions.find(
          (candidate) => candidate.id.toLowerCase() === requestedActionId.toLowerCase(),
        );
  const requestedActionOpenable =
    requestedAction !== undefined && !actionIsUnavailable(requestedAction);
  const handledDeepLink = useRef<string | null>(null);
  useEffect(() => {
    if (requestedActionId === null) {
      handledDeepLink.current = null;
      return;
    }
    if (!server.data || !requestedAction || !requestedActionOpenable) return;
    const key = `${server.data.primaryKey}|${requestedAction.id.toLowerCase()}`;
    if (handledDeepLink.current === key) return;
    handledDeepLink.current = key;
    request({
      serverId: server.data.primaryKey,
      actionId: requestedAction.id,
      source: "deep-link",
    });
  }, [requestedActionId, requestedAction, requestedActionOpenable, server.data, request]);
  const dismissDeepLink = () => {
    setSearch(
      (current) => {
        const next = new URLSearchParams(current);
        next.delete("action");
        return next;
      },
      { replace: true },
    );
  };
  // The create page hands over the start intent through router state (D-07). The state is untrusted, so only the
  // literal intent counts; it is adopted during render when a later location carries it, and removed from the
  // history entry right after it is read so a reload never repeats the start.
  const createIntent = hasCreateStartIntent(location.state);
  const [createStart, setCreateStart] = useState(createIntent);
  const [seenLocationKey, setSeenLocationKey] = useState(location.key);
  if (seenLocationKey !== location.key) {
    setSeenLocationKey(location.key);
    if (createIntent) setCreateStart(true);
  }
  const { pathname, search: locationSearch, hash } = location;
  useEffect(() => {
    if (!createIntent) return;
    void navigate({ pathname, search: locationSearch, hash }, { replace: true, state: null });
  }, [createIntent, pathname, locationSearch, hash, navigate]);
  const createStartAction = server.data?.actions.find(
    (candidate) => candidate.id.toLowerCase() === "start",
  );
  // The guard records the server a request was issued for, not that this effect ran: StrictMode re-runs the effect
  // after clearing the first timer, and the cleanup of that first run must not leave the request unissued.
  const startIssued = useRef<string | null>(null);
  useEffect(() => {
    if (!createStart || !server.data || !createStartAction) return;
    if (actionIsUnavailable(createStartAction)) return;
    const createdServerId = server.data.primaryKey;
    if (startIssued.current === createdServerId) return;
    // Pitfall 1: the confirmation provider records the pathname in an effect that runs after the effects of its
    // children in the same commit. A request issued right after the navigation would capture the create page path
    // and be closed at once, so it is deferred until that commit has settled.
    const timer = window.setTimeout(() => {
      startIssued.current = createdServerId;
      request({
        serverId: createdServerId,
        actionId: createStartAction.id,
        source: "create",
        onSubmitted: () => setCreateStart(false),
      });
    }, 0);
    return () => window.clearTimeout(timer);
  }, [createStart, server.data, createStartAction, request]);
  const showCreateNotice =
    createStart && server.data !== undefined && deriveServerState(server.data).state === "off";
  const [refreshFailed, setRefreshFailed] = useState(false);
  // Exact keys only: the server key is a prefix of every per-server key (files, configuration, secrets, ...) and
  // the fleet key a prefix of all of them, and a refresh must not refetch editor or file-browser state.
  const refresh = async () => {
    setRefreshFailed(false);
    try {
      await Promise.all(
        [
          hostingQueryKeys.fleet,
          hostingQueryKeys.server(serverId),
          hostingQueryKeys.jobs(serverId),
          hostingQueryKeys.operations(serverId),
          hostingQueryKeys.accruedCost(serverId),
          hostingQueryKeys.metrics(serverId),
          hostingQueryKeys.notifications,
        ].map((queryKey) =>
          client.invalidateQueries({ queryKey, exact: true }, { throwOnError: true }),
        ),
      );
    } catch {
      setRefreshFailed(true);
    }
  };
  const intervention = useMutation({
    mutationFn: ({ jobId, kind }: { jobId: string; kind: "retry" | "skip" }) =>
      kind === "retry" ? hostingService.retryJob(jobId) : hostingService.skipJob(jobId),
    onSuccess: refresh,
  });
  const activityUpdatedAt = Math.max(jobs.dataUpdatedAt, operations.dataUpdatedAt);
  const announcement = useJobAnnouncement(jobs.data, activityUpdatedAt);
  const accrued = useAccruedLive(server.data ? serverId : "");
  const stoppedAnnouncement = useAccruedStoppedAnnouncement(server.data, accrued.data);
  // The shell renders its own header with the server name. Every other state renders the route heading first, so
  // each state panel sits below exactly one h1 (D-AUTO-3).
  const showShell = permissions.accessServers && !authLoading && Boolean(server.data);
  const refetchServer = () => void server.refetch();
  let body: ReactNode;
  if (authLoading || server.isLoading) {
    body = <LoadingState />;
  } else if (server.isError && !server.data) {
    body = <ErrorState onRetry={refetchServer} />;
  } else if (!server.data) {
    body = (
      <EmptyState
        title={t("Server_Workspace_NotFound_Title")}
        action={
          <ActionLink variant="ghost" to="/command/servers">
            {t("Server_Workspace_BackToServers")}
          </ActionLink>
        }
      >
        {t("Server_Workspace_NotFound_Body")}
      </EmptyState>
    );
  } else {
    body = (
      <WorkspaceShell
        server={server.data}
        section={workspaceSectionFromSearch(search.get("tab"))}
        jobs={jobs.data ?? []}
        operations={operations.data ?? []}
        activityLoading={jobs.isLoading || operations.isLoading}
        activityFailed={
          (jobs.isError && jobs.data === undefined) ||
          (operations.isError && operations.data === undefined)
        }
        activityFetching={jobs.isFetching || operations.isFetching}
        activityUpdatedAt={activityUpdatedAt}
        hasActiveJob={hasActiveJob(server.data, jobs.data)}
        refreshing={server.isFetching || jobs.isFetching || operations.isFetching}
        refreshFailed={refreshFailed}
        onRefresh={() => void refresh()}
        onReloadActivity={() => {
          void jobs.refetch();
          void operations.refetch();
        }}
        jobBusy={intervention.isPending}
        jobRequestFailed={intervention.isError}
        announcement={stoppedAnnouncement ?? announcement}
        onRetryJob={(jobId) => intervention.mutate({ jobId, kind: "retry" })}
        onSkipJob={(jobId) => intervention.mutate({ jobId, kind: "skip" })}
        notices={
          <>
            {server.isError && <ErrorState onRetry={refetchServer} />}
            <AutoStopNotice server={server.data} jobs={jobs.data ?? []} />
            <CostAlertBanner server={server.data} />
            {showCreateNotice && (
              <CreateStartNotice
                server={server.data}
                startAction={createStartAction}
                onDismiss={() => setCreateStart(false)}
              />
            )}
            {requestedActionId !== null &&
              (requestedAction ? (
                requestedActionOpenable ? null : (
                  <DeepLinkActionNotice
                    kind="unavailable"
                    server={server.data}
                    action={requestedAction}
                    onDismiss={dismissDeepLink}
                  />
                )
              ) : (
                <DeepLinkActionNotice
                  kind="unknown"
                  actionId={requestedActionId}
                  onDismiss={dismissDeepLink}
                />
              ))}
            <PendingStartNotice server={server.data} />
          </>
        }
      />
    );
  }
  return (
    <div className="next-hosting-page">
      {!showShell && <PageHeader title={t("Hosting_Servers_Title")} />}
      <AccessBoundary allowed={permissions.accessServers}>{body}</AccessBoundary>
    </div>
  );
}
