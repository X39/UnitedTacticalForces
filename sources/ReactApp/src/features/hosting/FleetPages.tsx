import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { currentLocale } from "@/app/format";
import { useDocumentTitle } from "@/app/documentTitle";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { useAuth } from "@/auth/auth";
import { getHostingPermissions } from "@/features/hosting/authorization";
import {
  HostingAccessBoundary,
  HostingError,
  HostingLoading,
  ServerCard,
} from "@/features/hosting/components";
import { useHostingCopy } from "@/features/hosting/copy";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, ServerNotification } from "@/features/hosting/types";

function useFleet(enabled: boolean) {
  return useQuery({
    queryKey: hostingQueryKeys.fleet,
    queryFn: ({ signal }) => hostingService.servers(signal),
    refetchInterval: 10_000,
    enabled,
  });
}

export function HostingOverviewView({
  servers,
  notifications,
  canViewFinance,
  canCreate,
}: {
  servers: GameServer[];
  notifications: ServerNotification[];
  canViewFinance: boolean;
  canCreate: boolean;
}) {
  const c = useHostingCopy();
  const possibleCost = servers.filter((server) => server.deployment?.possibleCost).length;
  const pending = servers.reduce((count, server) => count + server.pendingJobCount, 0);
  const attention = notifications.filter((notification) => notification.possibleCost);
  return (
    <>
      <section className="next-hosting-metrics" aria-label={c.commandTitle}>
        <Metric label={c.totalServers} value={servers.length} />
        <Metric label={c.possibleCost} value={possibleCost} />
        <Metric label={c.pendingJobs} value={pending} />
        <Metric label={c.attention} value={attention.length} />
      </section>

      <section className="next-hosting-dashboard-grid">
        <article className="next-hosting-panel">
          <span className="kicker">{c.attention}</span>
          <h2>{c.possibleCost}</h2>
          {attention.length === 0 ? (
            <p className="next-hosting-muted">{c.noAttention}</p>
          ) : (
            <ul className="next-hosting-attention-list">
              {attention.map((notification) => (
                <li key={notification.serverId}>
                  <Link to={`/command/servers/${notification.serverId}`}>
                    <strong>{notification.title}</strong>
                    <span>{notification.statusLabel}</span>
                  </Link>
                  <StatusBadge tone="warning">{c.possibleCost}</StatusBadge>
                </li>
              ))}
            </ul>
          )}
        </article>

        <nav className="next-hosting-panel next-hosting-shortcuts" aria-label={c.commandTitle}>
          <span className="kicker">{c.commandTitle}</span>
          <Link to="/command/servers">{c.fleet}</Link>
          <Link to="/command/configuration-profiles">{c.configurationProfiles}</Link>
          {canViewFinance && <Link to="/command/hosting-finance">{c.finance}</Link>}
          {canCreate && <Link to="/command/servers/create">{c.createServer}</Link>}
        </nav>
      </section>
    </>
  );
}

export function HostingOverviewPage() {
  const c = useHostingCopy();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  const fleet = useFleet(permissions.accessServers);
  const notifications = useQuery({
    queryKey: hostingQueryKeys.notifications,
    queryFn: ({ signal }) => hostingService.notifications(signal),
    refetchInterval: 10_000,
    enabled: permissions.accessServers,
  });
  return (
    <div className="next-hosting-page">
      <PageHeader title={c.commandTitle} description={c.commandDescription} />
      <HostingAccessBoundary allowed={permissions.accessServers || permissions.viewFinance}>
        {authLoading || fleet.isLoading ? (
          <HostingLoading />
        ) : fleet.isError ? (
          <HostingError
            message={fleet.error.message}
            onRetry={() => void Promise.all([fleet.refetch(), notifications.refetch()])}
          />
        ) : (
          <HostingOverviewView
            servers={fleet.data ?? []}
            notifications={notifications.data ?? []}
            canViewFinance={permissions.viewFinance}
            canCreate={permissions.createServers}
          />
        )}
      </HostingAccessBoundary>
    </div>
  );
}

export function ServerFleetView({
  servers,
  canCreate,
}: {
  servers: GameServer[];
  canCreate: boolean;
}) {
  const { t } = useTranslation();
  const ordered = orderFleetServers(servers);
  if (servers.length === 0) {
    return (
      <StatePanel
        kind="empty"
        title={t("Hosting_Servers_EmptyTitle")}
        action={
          canCreate ? (
            <Link className="button button--gold" to="/command/servers/create">
              {t("Hosting_Servers_Create")}
            </Link>
          ) : undefined
        }
      >
        {t(
          canCreate ? "Hosting_Servers_EmptyDescription" : "Hosting_Servers_EmptyAccessDescription",
        )}
      </StatePanel>
    );
  }
  return (
    <>
      <section className="next-hosting-metrics" aria-label={t("Hosting_Servers_Summary")}>
        <Metric label={t("Hosting_Servers_Total")} value={servers.length} />
        <Metric
          label={t("Hosting_Servers_PossibleCost")}
          value={servers.filter((server) => server.deployment?.possibleCost).length}
        />
        <Metric
          label={t("Hosting_Servers_PendingWork")}
          value={servers.reduce((count, server) => count + server.pendingJobCount, 0)}
        />
      </section>
      <section className="next-hosting-card-grid" aria-label={t("Hosting_Servers_Title")}>
        {ordered.map((server) => (
          <ServerCard key={server.primaryKey} server={server} />
        ))}
      </section>
    </>
  );
}

export function orderFleetServers(servers: GameServer[]): GameServer[] {
  const collator = new Intl.Collator(currentLocale(), { sensitivity: "base" });
  return [...servers].sort(
    (left, right) =>
      Number(Boolean(right.deployment?.possibleCost)) -
        Number(Boolean(left.deployment?.possibleCost)) ||
      Number(right.pendingJobCount > 0) - Number(left.pendingJobCount > 0) ||
      collator.compare(left.title, right.title),
  );
}

export function ServerFleetPage() {
  const { t } = useTranslation();
  useDocumentTitle(t("Hosting_Servers_Title"));
  const auth = useAuth();
  const permissions = getHostingPermissions(auth.user);
  const fleet = useFleet(!auth.isLoading && auth.isAuthenticated);
  const hasFleetData = fleet.data !== undefined;
  const loadError = (
    <StatePanel
      kind="error"
      action={
        <button className="button button--ghost" type="button" onClick={() => void fleet.refetch()}>
          {t("Hosting_Common_Refresh")}
        </button>
      }
    >
      {t("Hosting_Servers_LoadError")}
    </StatePanel>
  );
  return (
    <div className="next-hosting-page" aria-busy={fleet.isFetching}>
      <PageHeader
        title={t("Hosting_Servers_Title")}
        description={t("Hosting_Servers_Description")}
        actions={
          <>
            <button
              className="button button--ghost"
              type="button"
              disabled={fleet.isFetching}
              onClick={() => void fleet.refetch()}
            >
              {t("Hosting_Common_Refresh")}
            </button>
            {permissions.createServers && (
              <Link className="button button--gold" to="/command/servers/create">
                {t("Hosting_Servers_Create")}
              </Link>
            )}
          </>
        }
      />
      {auth.isLoading || fleet.isPending ? (
        <StatePanel kind="loading">{t("Hosting_Servers_Loading")}</StatePanel>
      ) : fleet.isError && !hasFleetData ? (
        loadError
      ) : (
        <>
          {fleet.isError && loadError}
          <ServerFleetView servers={fleet.data ?? []} canCreate={permissions.createServers} />
        </>
      )}
    </div>
  );
}
