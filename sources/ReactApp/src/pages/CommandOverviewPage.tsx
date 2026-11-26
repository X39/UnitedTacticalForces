import { ArrowRight20Regular, CheckmarkCircle24Regular } from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { FullUser } from "@/api/types";
import { useAuth } from "@/auth/auth";
import {
  getCommandNavigationGroups,
  getCommandOverviewItems,
} from "@/components/commandNavigation";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { PageStack } from "@/components/PageStack";
import { Metric, PageHeader, StatusBadge } from "@/components/primitives";
import { ServerStateSummary } from "@/components/server/ServerStateSummary";
import { ErrorState, LoadingState } from "@/components/StateViews";
import { getHostingPermissions } from "@/features/hosting/authorization";
import {
  deriveServerState,
  isDeploymentActive,
  type DerivedServerState,
} from "@/features/hosting/serverState";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, ServerNotification } from "@/features/hosting/types";

export function commandOverviewLinks(user: FullUser | undefined) {
  return getCommandOverviewItems(user).map(({ to, labelKey }) => ({ to, labelKey }));
}

export interface CommandOperationalSummary {
  totalServers: number;
  activeDeployments: number;
  possibleCost: number;
  pendingJobs: number;
}

export interface CommandAttentionItem {
  serverId: string;
  title: string;
  /** State and cost shown for the server; the same derivation every other server surface uses. */
  derived: DerivedServerState;
  possibleCost: boolean;
  pendingJobCount: number;
}

export function commandOperationalSummary(servers: GameServer[]): CommandOperationalSummary {
  return {
    totalServers: servers.length,
    activeDeployments: servers.filter(isDeploymentActive).length,
    possibleCost: servers.filter((server) => server.deployment?.possibleCost).length,
    pendingJobs: servers.reduce((count, server) => count + server.pendingJobCount, 0),
  };
}

export function commandAttentionItems(
  servers: GameServer[],
  notifications: ServerNotification[],
): CommandAttentionItem[] {
  const items = new Map<string, CommandAttentionItem>();
  for (const notification of notifications) {
    if (!notification.possibleCost) continue;
    const server = servers.find((candidate) => candidate.primaryKey === notification.serverId);
    items.set(notification.serverId, {
      serverId: notification.serverId,
      title: notification.title,
      derived: deriveServerState(server, { notification }),
      possibleCost: true,
      pendingJobCount: server?.pendingJobCount ?? 0,
    });
  }
  for (const server of servers) {
    if (server.pendingJobCount < 1) continue;
    const existing = items.get(server.primaryKey);
    if (existing) {
      items.set(server.primaryKey, { ...existing, pendingJobCount: server.pendingJobCount });
      continue;
    }
    items.set(server.primaryKey, {
      serverId: server.primaryKey,
      title: server.title,
      derived: deriveServerState(server),
      possibleCost: Boolean(server.deployment?.possibleCost),
      pendingJobCount: server.pendingJobCount,
    });
  }
  return [...items.values()].sort(
    (left, right) =>
      Number(right.possibleCost) - Number(left.possibleCost) ||
      right.pendingJobCount - left.pendingJobCount ||
      left.title.localeCompare(right.title),
  );
}

/**
 * Shows the fleet counters and the servers that need attention.
 *
 * @param props.servers Servers from the fleet query.
 * @param props.notifications Cost notifications from the notification query.
 */
export function OperationalSnapshot({
  servers,
  notifications,
}: {
  servers: GameServer[];
  notifications: ServerNotification[];
}) {
  const { t } = useTranslation();
  const summary = commandOperationalSummary(servers);
  const attention = commandAttentionItems(servers, notifications);
  return (
    <section
      className="command-operational-snapshot"
      aria-label={t("React_CommandOverview_OperationalSnapshot")}
    >
      <div className="command-operational-snapshot__metrics">
        <Metric label={t("Hosting_Servers_Total")} value={summary.totalServers} />
        <Metric
          label={t("React_CommandOverview_ActiveDeployments")}
          value={summary.activeDeployments}
        />
        <Metric label={t("Hosting_Servers_PossibleCost")} value={summary.possibleCost} />
        <Metric label={t("Hosting_Servers_PendingWork")} value={summary.pendingJobs} />
      </div>
      <article className="command-overview-panel">
        <header className="command-overview-panel__heading">
          <div>
            <span className="kicker">{t("React_CommandOverview_AttentionKicker")}</span>
            <h2>{t("React_CommandOverview_AttentionTitle")}</h2>
          </div>
          {attention.length > 0 && <StatusBadge tone="warning">{attention.length}</StatusBadge>}
        </header>
        {attention.length === 0 ? (
          <div className="command-overview-all-clear">
            <CheckmarkCircle24Regular aria-hidden="true" />
            <strong>{t("React_CommandOverview_AllClearTitle")}</strong>
            <span>{t("React_CommandOverview_AllClearDescription")}</span>
          </div>
        ) : (
          <ul className="command-attention-list">
            {attention.map((item) => (
              <li key={item.serverId}>
                <Link className="command-attention-item" to={`/command/servers/${item.serverId}`}>
                  <span>
                    <strong>{item.title}</strong>
                  </span>
                  <span>
                    <ServerStateSummary derived={item.derived} />
                    {item.pendingJobCount > 0 && (
                      <StatusBadge tone="info">
                        {item.pendingJobCount} {t("Hosting_Servers_PendingWork")}
                      </StatusBadge>
                    )}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </article>
    </section>
  );
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const permissions = getHostingPermissions(user);
  const navigationGroups = getCommandNavigationGroups(user);
  const workAreas = navigationGroups.flatMap((group) =>
    group.items
      .filter((item) => item.showOnOverview)
      .map((item) => ({ item, groupLabelKey: group.labelKey })),
  );
  const fleet = useQuery({
    queryKey: hostingQueryKeys.fleet,
    queryFn: ({ signal }) => hostingService.servers(signal),
    refetchInterval: 10_000,
    enabled: permissions.accessServers,
  });
  const notifications = useQuery({
    queryKey: hostingQueryKeys.notifications,
    queryFn: ({ signal }) => hostingService.notifications(signal),
    refetchInterval: 10_000,
    enabled: permissions.accessServers,
  });
  const refresh = () => void Promise.all([fleet.refetch(), notifications.refetch()]);
  const hostingLoading = fleet.isPending || notifications.isPending;
  const hostingError = fleet.isError || notifications.isError;
  // Rows that were loaded earlier stay visible under a failed refetch; the error panel above them offers the retry.
  const hasHostingData = fleet.data !== undefined && notifications.data !== undefined;

  return (
    <section aria-busy={fleet.isFetching || notifications.isFetching}>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Overview")}
        title={t("Shared_CommandCenter_HeaderTitle")}
        description={t("React_CommandOverview_Description")}
        actions={
          permissions.accessServers ? (
            <>
              <ActionButton
                variant="ghost"
                type="button"
                disabled={fleet.isFetching || notifications.isFetching}
                onClick={refresh}
              >
                {t("Hosting_Common_Refresh")}
              </ActionButton>
              {permissions.createServers && (
                <ActionLink variant="gold" to="/command/servers/create">
                  {t("Hosting_Servers_Create")}
                </ActionLink>
              )}
            </>
          ) : undefined
        }
      />

      <PageStack>
        {permissions.accessServers && hostingLoading && (
          <LoadingState label={t("Hosting_Servers_Loading")} />
        )}
        {permissions.accessServers && !hostingLoading && hostingError && (
          <ErrorState onRetry={refresh}>{t("Hosting_Servers_LoadError")}</ErrorState>
        )}
        {permissions.accessServers && !hostingLoading && hasHostingData && (
          <OperationalSnapshot
            servers={fleet.data ?? []}
            notifications={notifications.data ?? []}
          />
        )}

        <section className="command-overview-panel">
          <header className="command-overview-panel__heading">
            <div>
              <span className="kicker">{t("React_CommandOverview_WorkAreasKicker")}</span>
              <h2>{t("React_CommandOverview_WorkAreasTitle")}</h2>
            </div>
          </header>
          <ul className="command-work-area-list">
            {workAreas.map(({ item, groupLabelKey }) => {
              const ItemIcon = item.icon;
              return (
                <li key={item.id}>
                  <Link className="command-work-area" to={item.to}>
                    <ItemIcon aria-hidden="true" />
                    <span>
                      <strong>{t(item.labelKey)}</strong>
                      <small>{t(groupLabelKey)}</small>
                    </span>
                    <ArrowRight20Regular className="command-work-area__arrow" aria-hidden="true" />
                  </Link>
                </li>
              );
            })}
          </ul>
        </section>
      </PageStack>
    </section>
  );
}
