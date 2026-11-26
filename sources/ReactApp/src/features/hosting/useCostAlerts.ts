import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "@/auth/auth";
import { useAppToast } from "@/components/AppToasts";
import {
  alertsOfServer,
  formatAlertThreshold,
  formatInstant,
} from "@/features/hosting/costAlertFormat";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { CostAlert } from "@/features/hosting/types";
import { useFleetLiveUpdates } from "@/features/hosting/useFleetLiveUpdates";

/**
 * Returns whether an alert announces an automatic shutdown rather than a reached cost threshold.
 *
 * @param alert The alert.
 * @returns True for the kind `AutoStop`; an absent or any other kind is a threshold alert.
 */
export function isAutoStopAlert(alert: CostAlert): boolean {
  return alert.kind === "AutoStop";
}

/** The viewer's undismissed cost alerts as {@link useCostAlerts} returns them. */
export interface CostAlertsResult {
  /** Every undismissed alert of the viewer, newest first; empty until the first load finished. */
  alerts: CostAlert[];
  /**
   * Whether the alerts query holds data. Surfaces render nothing while this is false. It stays true after the first
   * load even when a later refetch fails, so the last known alerts keep showing.
   */
  loaded: boolean;
  /** Whether the first load is still running; false once it finished or failed. */
  loading: boolean;
  /**
   * Returns the alerts of one server, newest first.
   *
   * @param serverId The id of the server.
   */
  forServer(serverId: string): CostAlert[];
}

/**
 * Loads the viewer's undismissed cost alerts into the one alerts cache entry the live frames write into.
 *
 * The query is enabled for every signed-in user, so a cost owner without access to the server section still sees
 * the alerts after a page load. It never polls: it has no refetch interval, is never stale on its own and does not
 * refetch on window focus. New and dismissed alerts arrive as frames written into the same cache entry by the fleet
 * socket provider, and the provider refetches the list when the socket reopens after a disconnect and on a manual
 * reconnect.
 *
 * `loaded` means the query holds data, not that its last fetch succeeded: a failed refetch, for example while the
 * API restarts, keeps the last known alerts, so an alert leaves only through a dismissal or the server's list (D-17).
 *
 * @returns The alerts, whether they loaded and a per-server filter.
 */
export function useCostAlerts(): CostAlertsResult {
  const { user } = useAuth();
  const query = useQuery({
    queryKey: hostingQueryKeys.costAlerts,
    queryFn: ({ signal }) => hostingService.costAlerts(signal),
    enabled: Boolean(user),
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    retry: false,
  });
  const data = query.data;
  const alerts = useMemo<CostAlert[]>(() => (data === undefined ? [] : [...data]), [data]);
  const forServer = useCallback((serverId: string) => alertsOfServer(alerts, serverId), [alerts]);
  return { alerts, loaded: data !== undefined, loading: query.isLoading, forServer };
}

/** What {@link useDismissCostAlerts} returns. */
export interface DismissCostAlertsResult {
  /**
   * Dismisses one alert. The alert leaves the cache only after the API accepted the request, and a success toast
   * then names the server and the amount (D-17).
   *
   * @param alert The alert to dismiss.
   * @returns True when the API accepted the dismissal; false when it failed, in which case the alert stays, no
   * toast shows and `failed` is true.
   */
  dismiss(alert: CostAlert): Promise<boolean>;
  /**
   * Dismisses every alert of one server in a single request. The alerts leave the cache only after the API
   * accepted the request, and a success toast then names the server and the number of dismissed alerts (D-17).
   *
   * @param serverId The id of the server.
   * @returns True when the API accepted the dismissal; false when it failed.
   */
  dismissAll(serverId: string): Promise<boolean>;
  /** The ids of the alerts whose dismissal is in flight; their controls are `aria-disabled`. */
  pendingIds: ReadonlySet<string>;
  /** Whether the last dismissal failed. The next dismissal clears it. */
  failed: boolean;
}

/**
 * Provides the dismissal mutations of cost alerts.
 *
 * A row is removed from the alerts cache only after the API accepted the dismissal, so a failure keeps it. While a
 * request is in flight its alert ids are pending and a second request for the same id is ignored. Dismissal is per
 * viewer: other members of a group cost owner keep their own alert. Once the API accepted a dismissal, the ids are
 * recorded in the fleet live updates context before the cache is filtered, so a late raised frame cannot bring the
 * alert back.
 *
 * The success toast is the visible and announced confirmation of a dismissal. It lives in the app-wide toast
 * region, so it stays on screen when the last alert leaves and its banner unmounts; a failure shows no toast, the
 * alert stays and the surface shows its inline alert line instead. A toast never removes an alert.
 *
 * @returns The mutations, the pending ids and the failure flag.
 */
export function useDismissCostAlerts(): DismissCostAlertsResult {
  const { t } = useTranslation();
  const { showToast } = useAppToast();
  const queryClient = useQueryClient();
  const { markCostAlertsDismissed } = useFleetLiveUpdates();
  const [pendingIds, setPendingIds] = useState<ReadonlySet<string>>(() => new Set());
  const [failed, setFailed] = useState(false);

  const run = useCallback(
    async (
      ids: readonly string[],
      send: () => Promise<void>,
      confirmation: () => string | null,
    ): Promise<boolean> => {
      setFailed(false);
      setPendingIds((current) => new Set([...current, ...ids]));
      try {
        await send();
        markCostAlertsDismissed(ids);
        queryClient.setQueryData<readonly CostAlert[]>(hostingQueryKeys.costAlerts, (current) =>
          current?.filter((alert) => !ids.includes(alert.id)),
        );
        const message = confirmation();
        if (message !== null) showToast(message, "success");
        return true;
      } catch {
        setFailed(true);
        return false;
      } finally {
        setPendingIds((current) => new Set([...current].filter((id) => !ids.includes(id))));
      }
    },
    [queryClient, markCostAlertsDismissed, showToast],
  );

  const dismiss = useCallback(
    (alert: CostAlert) => {
      if (pendingIds.has(alert.id)) return Promise.resolve(false);
      return run(
        [alert.id],
        () => hostingService.dismissCostAlert(alert.id),
        // The dismissed toast words a cost threshold; an automatic shutdown row leaves without one.
        () =>
          isAutoStopAlert(alert)
            ? null
            : t("Server_Alerts_Dismissed_Toast", {
                name: alert.serverName,
                threshold: formatAlertThreshold(alert),
              }),
      );
    },
    [pendingIds, run, t],
  );

  const dismissAll = useCallback(
    (serverId: string) => {
      const cached = queryClient.getQueryData<readonly CostAlert[]>(hostingQueryKeys.costAlerts);
      // Dismiss all belongs to the cost banner, which never lists automatic shutdown records.
      const dismissed = alertsOfServer(cached ?? [], serverId).filter(
        (alert) => !isAutoStopAlert(alert),
      );
      const ids = dismissed.map((alert) => alert.id);
      return run(
        ids,
        () => hostingService.dismissServerCostAlerts(serverId, ids),
        () =>
          t("Server_Alerts_DismissedAll_Toast", {
            count: ids.length,
            name: dismissed[0]?.serverName ?? "",
          }),
      );
    },
    [queryClient, run, t],
  );

  return { dismiss, dismissAll, pendingIds, failed };
}

/**
 * Shows one warning toast for every alert that newly arrives as a socket frame.
 *
 * The toast is only a pointer to the persistent surfaces, the workspace banner and the profile menu rows. It is
 * never shown for alerts loaded by the first fetch or by a refetch after a reconnect, because those never reach the
 * frame listener, and never for a frame whose id the alerts cache already held. A ref of announced ids keeps a
 * redelivered frame from toasting twice, even after the alert was dismissed in between. Mount it once, in the
 * profile menu.
 */
export function useCostAlertArrivalToasts(): void {
  const { t } = useTranslation();
  const { showToast } = useAppToast();
  const { subscribeCostAlerts } = useFleetLiveUpdates();
  const announced = useRef(new Set<string>());
  useEffect(
    () =>
      subscribeCostAlerts((alert, wasCached) => {
        if (wasCached || announced.current.has(alert.id)) return;
        announced.current.add(alert.id);
        showToast(
          isAutoStopAlert(alert)
            ? t("Server_Automation_Toast_Warning", {
                name: alert.serverName,
                time: formatInstant(alert.stopsAt ?? alert.createdAt),
              })
            : t("Server_Alerts_Toast", {
                name: alert.serverName,
                threshold: formatAlertThreshold(alert),
              }),
          "warning",
        );
      }),
    [subscribeCostAlerts, showToast, t],
  );
}
