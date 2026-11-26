import { useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, type ReactNode } from "react";
import { resolveApiBaseUrl, type PublicConfiguration } from "@/app/config";
import { createCoalescer, useUpdateStream, type UpdateStreamFrame } from "@/app/updateStream";
import { useAuth } from "@/auth/auth";
import { canAccessGameServers } from "@/auth/claims";
import { isNewerAccrued } from "@/features/hosting/accruedModel";
import { insertAlert } from "@/features/hosting/costAlertFormat";
import {
  parseAccruedCostFrame,
  parseCostAlertDismissedFrame,
  parseCostAlertRaisedFrame,
} from "@/features/hosting/costFrames";
import {
  historyBucketMs,
  parseMetricsFrame,
  replacesCachedReading,
} from "@/features/hosting/metricsFrames";
import { hostingQueryKeys } from "@/features/hosting/services";
import type {
  CostAlert,
  GameServerAccruedCost,
  ServerMetricsRange,
  ServerMetricsReading,
} from "@/features/hosting/types";
import {
  FleetLiveUpdatesContext,
  type FleetLiveUpdatesValue,
} from "@/features/hosting/useFleetLiveUpdates";

/** Update stream path of the fleet signal; must equal the API's canonical fleet path. */
const fleetStreamPath = "game-servers/fleet";

/** Full type name of the .NET wire message that says one server of the fleet changed. */
const fleetChangedMessageType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage";

/** Coalescing window of fleet signals: a burst refetches once at once and at most once more per window. */
const signalCoalesceWindowMs = 500;

/**
 * Returns whether a frame is a fleet-changed message with a string server id.
 *
 * @param frame The parsed, untrusted frame.
 */
function isFleetChangedFrame(frame: UpdateStreamFrame): boolean {
  if (frame.type !== fleetChangedMessageType) return false;
  const value = frame.value;
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as { serverId?: unknown }).serverId === "string"
  );
}

/**
 * Invalidates exactly the fleet list and the notifications queries. `exact: true` matters because the fleet key is
 * a prefix of every per-server key, and a fleet signal must never refetch open workspace queries.
 *
 * @param queryClient The query client of the app.
 */
function invalidateFleetQueries(queryClient: QueryClient): void {
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet, exact: true });
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.notifications, exact: true });
}

/**
 * Invalidates exactly the alerts query. `exact: true` keeps the refetch to the one list.
 *
 * @param queryClient The query client of the app.
 */
function invalidateCostAlerts(queryClient: QueryClient): void {
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.costAlerts, exact: true });
}

/**
 * Returns the history range a query key belongs to.
 *
 * @param queryKey The key of a cached metrics history query.
 * @returns The range, or null when the key does not end in a known range.
 */
function historyRangeOf(queryKey: readonly unknown[]): ServerMetricsRange | null {
  const range = queryKey[5];
  return range === "1h" || range === "24h" || range === "7d" ? range : null;
}

/**
 * Invalidates the cached history ranges of one server whose data is older than their bucket interval.
 *
 * This is the only history refresh path besides the first REST load: the browser never merges samples and never
 * runs a timer. A range refetches at most once per bucket, because a refetch makes its data younger than the
 * bucket interval and the next frames skip it until a whole bucket has passed. A range that is loading, fetching
 * or never loaded is left alone. Only the ranges of this server are touched, with exact keys.
 *
 * @param queryClient The query client of the app.
 * @param serverId The id of the server the pushed frame belongs to.
 */
function invalidateAgedHistory(queryClient: QueryClient, serverId: string): void {
  const prefix = hostingQueryKeys.metricsHistory(serverId, "1h").slice(0, -1);
  const now = Date.now();
  for (const query of queryClient.getQueryCache().findAll({ queryKey: prefix })) {
    const range = historyRangeOf(query.queryKey);
    if (range === null) continue;
    const { dataUpdatedAt, fetchStatus } = query.state;
    if (dataUpdatedAt <= 0 || fetchStatus === "fetching") continue;
    if (now - dataUpdatedAt >= historyBucketMs(range)) {
      void queryClient.invalidateQueries({ queryKey: query.queryKey, exact: true });
    }
  }
}

/** Props of {@link FleetLiveUpdatesProvider}. */
export interface FleetLiveUpdatesProviderProps {
  /** Public configuration the API base URL is resolved from. */
  configuration: PublicConfiguration;
  /** The subtree that shares the fleet signal. */
  children: ReactNode;
}

/**
 * Subscribes to the live fleet signal and turns fleet-changed frames into exact-key invalidations of the fleet and
 * notifications queries. The frame carries only a server id; the data is refetched through the authorized
 * endpoints.
 *
 * A burst of frames refetches once immediately and at most once more per 500 milliseconds. Every open, including
 * the first one and every reconnect, refetches when the cached fleet data is older than the open, which recovers
 * changes missed while disconnected or between page load and subscribe. `reconnectNow` also refetches, so a
 * dropped socket never leaves a silently stale fleet.
 *
 * Accrued cost frames are validated and written into the accrued cost query of their server, newest as-of time
 * winning, with no request and no timer. They never trigger a fleet invalidation.
 *
 * Metrics frames are validated and written into the readings query of their server with a fresh
 * `performance.now()` receive stamp, newest as-of time winning, with no request and no timer. The browser never
 * merges or extrapolates samples: a frame replaces the cached reading. The cached history ranges of the server
 * are invalidated when their data is older than their bucket interval (15, 60 and 300 seconds), which is one
 * coalesced refetch per bucket and the only history refresh besides REST. Metrics frames never trigger a fleet invalidation and
 * never call the workspace listeners.
 *
 * Cost alert frames (`CostAlertRaisedMessage` and `CostAlertDismissedMessage`) are validated and written into the one
 * alerts query by id: a raised alert is added once, newest first, and a dismissed alert is removed. When the alerts
 * have not loaded yet the frame only invalidates the alerts query, because writing a one-entry list would hide the
 * alerts the first load is about to return. A reopen after a disconnect and `reconnectNow` refetch the alerts, so a
 * missed frame is recovered without any interval.
 *
 * The provider remembers, for its lifetime, the ids of alerts this viewer dismissed: through
 * `markCostAlertsDismissed` after an accepted dismissal in this tab, and through a dismissed frame from another tab.
 * A raised frame for a remembered id is dropped before any cache write or listener call, so a late frame cannot bring
 * a dismissed alert back or toast for it. A redelivered raised frame for a listed alert changes nothing, and
 * fleet-changed and accrued frames never touch the alerts.
 *
 * Other parts of the app, such as an open server workspace, follow the same single socket through `subscribe`: every
 * valid frame calls the registered listeners with the server id, so no second WebSocket is ever opened.
 *
 * No socket is opened while the session is loading, for an anonymous viewer, or for a viewer without access to
 * the server section. It must be mounted inside the query client and the auth provider.
 */
export function FleetLiveUpdatesProvider({
  configuration,
  children,
}: FleetLiveUpdatesProviderProps) {
  const auth = useAuth();
  const queryClient = useQueryClient();
  const enabled = !auth.isLoading && canAccessGameServers(auth.user);
  const baseUrl = useMemo(() => resolveApiBaseUrl(configuration), [configuration]);
  const signalCoalescer = useMemo(
    () => createCoalescer(() => invalidateFleetQueries(queryClient), signalCoalesceWindowMs),
    [queryClient],
  );

  useEffect(() => () => signalCoalescer.cancel(), [signalCoalescer]);

  const listeners = useRef(new Set<(serverId: string) => void>());

  const subscribe = useCallback((listener: (serverId: string) => void) => {
    const registry = listeners.current;
    registry.add(listener);
    return () => {
      registry.delete(listener);
    };
  }, []);

  const alertListeners = useRef(new Set<(alert: CostAlert, wasCached: boolean) => void>());

  const dismissedAlertIds = useRef(new Set<string>());

  const markCostAlertsDismissed = useCallback((costAlertIds: readonly string[]) => {
    for (const id of costAlertIds) dismissedAlertIds.current.add(id);
  }, []);

  const subscribeCostAlerts = useCallback(
    (listener: (alert: CostAlert, wasCached: boolean) => void) => {
      const registry = alertListeners.current;
      registry.add(listener);
      return () => {
        registry.delete(listener);
      };
    },
    [],
  );

  const onMessage = useCallback(
    (frame: UpdateStreamFrame) => {
      const accrued = parseAccruedCostFrame(frame);
      if (accrued !== null) {
        // The value is written as received: the browser never calculates or extrapolates the amount (D-02).
        queryClient.setQueryData<GameServerAccruedCost>(
          hostingQueryKeys.accruedCost(accrued.serverId),
          (current) => (isNewerAccrued(accrued.accrued, current) ? accrued.accrued : current),
        );
        return;
      }
      const metrics = parseMetricsFrame(frame);
      if (metrics !== null) {
        // The reading is written as received (D-09, MET-05): readings change only through frames or REST.
        queryClient.setQueryData<ServerMetricsReading>(
          hostingQueryKeys.metrics(metrics.serverId),
          (current) =>
            replacesCachedReading(metrics.metrics, current)
              ? { ...metrics.metrics, receivedAtMs: performance.now() }
              : current,
        );
        invalidateAgedHistory(queryClient, metrics.serverId);
        return;
      }
      const raisedAlert = parseCostAlertRaisedFrame(frame);
      if (raisedAlert !== null) {
        // The viewer dismissed this alert already: a late or redelivered frame must neither bring it back nor toast.
        if (dismissedAlertIds.current.has(raisedAlert.id)) return;
        const cached = queryClient.getQueryData<readonly CostAlert[]>(hostingQueryKeys.costAlerts);
        // Computed before the write: only an alert the cache did not hold yet is news to the viewer.
        const wasCached = cached?.some((alert) => alert.id === raisedAlert.id) ?? false;
        if (cached === undefined) invalidateCostAlerts(queryClient);
        else if (!wasCached) {
          queryClient.setQueryData<readonly CostAlert[]>(
            hostingQueryKeys.costAlerts,
            insertAlert(cached, raisedAlert),
          );
        }
        for (const listener of [...alertListeners.current]) {
          try {
            listener(raisedAlert, wasCached);
          } catch {
            // A failing listener must not block the other listeners.
          }
        }
        return;
      }
      const dismissedAlertId = parseCostAlertDismissedFrame(frame);
      if (dismissedAlertId !== null) {
        dismissedAlertIds.current.add(dismissedAlertId);
        const cached = queryClient.getQueryData<readonly CostAlert[]>(hostingQueryKeys.costAlerts);
        if (cached === undefined) invalidateCostAlerts(queryClient);
        else if (cached.some((alert) => alert.id === dismissedAlertId)) {
          queryClient.setQueryData<readonly CostAlert[]>(
            hostingQueryKeys.costAlerts,
            cached.filter((alert) => alert.id !== dismissedAlertId),
          );
        }
        return;
      }
      if (!isFleetChangedFrame(frame)) return;
      signalCoalescer.trigger();
      const changedServerId = (frame.value as { serverId: string }).serverId;
      for (const listener of [...listeners.current]) {
        try {
          listener(changedServerId);
        } catch {
          // A failing listener must not block the other listeners or the fleet invalidation.
        }
      }
    },
    [signalCoalescer, queryClient],
  );

  const onOpen = useCallback(() => {
    const alertsState = queryClient.getQueryState(hostingQueryKeys.costAlerts);
    if (alertsState && alertsState.dataUpdatedAt > 0 && alertsState.dataUpdatedAt < Date.now()) {
      invalidateCostAlerts(queryClient);
    }
    const fleetState = queryClient.getQueryState(hostingQueryKeys.fleet);
    if (!fleetState || fleetState.dataUpdatedAt <= 0) return;
    if (fleetState.dataUpdatedAt < Date.now()) invalidateFleetQueries(queryClient);
  }, [queryClient]);

  const {
    status,
    disconnectEpoch,
    reconnectNow: reconnectStream,
  } = useUpdateStream({ baseUrl, path: fleetStreamPath, enabled, onMessage, onOpen });

  const reconnectNow = useCallback(() => {
    reconnectStream();
    invalidateFleetQueries(queryClient);
    invalidateCostAlerts(queryClient);
  }, [reconnectStream, queryClient]);

  const value = useMemo<FleetLiveUpdatesValue>(
    () => ({
      status,
      disconnectEpoch,
      reconnectNow,
      subscribe,
      subscribeCostAlerts,
      markCostAlertsDismissed,
    }),
    [
      status,
      disconnectEpoch,
      reconnectNow,
      subscribe,
      subscribeCostAlerts,
      markCostAlertsDismissed,
    ],
  );

  return (
    <FleetLiveUpdatesContext.Provider value={value}>{children}</FleetLiveUpdatesContext.Provider>
  );
}
