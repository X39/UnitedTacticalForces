import { useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef } from "react";
import { createCoalescer } from "@/app/updateStream";
import { hostingQueryKeys } from "@/features/hosting/services";
import { useFleetLiveUpdates } from "@/features/hosting/useFleetLiveUpdates";

/** Coalescing window of workspace invalidations: a burst refetches once at once and at most once more per window. */
const invalidationCoalesceWindowMs = 500;

/**
 * Invalidates exactly the server, jobs, operations, accrued cost and current metric readings queries of one server.
 *
 * The exact-match filter is required on every call because `hostingQueryKeys.server(id)` is a prefix of every per-server key,
 * including the files, configuration, preservation and secrets keys. Without it a fleet signal would refetch
 * editor and file-browser state and discard unsaved work. Stage progress, worker commands and audit records do not
 * produce a fleet signal; the page's fallback poll covers that churn, not this function.
 *
 * @param queryClient The query client of the app.
 * @param serverId The id of the server whose workspace data changed.
 */
export function invalidateWorkspaceQueries(queryClient: QueryClient, serverId: string): void {
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.server(serverId), exact: true });
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.jobs(serverId), exact: true });
  void queryClient.invalidateQueries({
    queryKey: hostingQueryKeys.operations(serverId),
    exact: true,
  });
  void queryClient.invalidateQueries({
    queryKey: hostingQueryKeys.accruedCost(serverId),
    exact: true,
  });
  // The readings key is a prefix of the history keys; `exact: true` keeps the history ranges out of this refetch.
  void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.metrics(serverId), exact: true });
}

/**
 * Lets an open server workspace follow the one shared fleet socket for its own server.
 *
 * Every valid fleet-changed frame whose server id equals `serverId` invalidates the server, jobs, operations,
 * accrued cost and metric readings queries of that server through {@link invalidateWorkspaceQueries}, coalesced to
 * at most one round per 500 milliseconds. Frames for other servers are ignored. When the socket opens again after a disconnection episode the
 * same queries are invalidated once, which recovers changes missed while disconnected; the first open after
 * mount does not invalidate. No socket is opened here: the hook only subscribes to the provider's socket, and
 * outside the provider it is inert.
 *
 * @param serverId The id of the open server; an empty id registers nothing.
 */
export function useWorkspaceLiveUpdates(serverId: string): void {
  const queryClient = useQueryClient();
  const { subscribe, status, disconnectEpoch } = useFleetLiveUpdates();
  const coalescer = useMemo(
    () =>
      createCoalescer(
        () => invalidateWorkspaceQueries(queryClient, serverId),
        invalidationCoalesceWindowMs,
      ),
    [queryClient, serverId],
  );
  const handledEpoch = useRef(disconnectEpoch);

  useEffect(() => () => coalescer.cancel(), [coalescer]);

  useEffect(() => {
    if (serverId === "") return undefined;
    return subscribe((changedServerId) => {
      if (changedServerId === serverId) coalescer.trigger();
    });
  }, [subscribe, serverId, coalescer]);

  useEffect(() => {
    if (status !== "open" || disconnectEpoch <= handledEpoch.current) return;
    handledEpoch.current = disconnectEpoch;
    if (serverId !== "") invalidateWorkspaceQueries(queryClient, serverId);
  }, [status, disconnectEpoch, serverId, queryClient]);
}
