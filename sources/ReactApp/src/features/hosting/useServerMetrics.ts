import { useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { replacesCachedReading } from "@/features/hosting/metricsFrames";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  GameServerMetricsHistory,
  ServerMetricsRange,
  ServerMetricsReading,
} from "@/features/hosting/types";

/**
 * Loads the current metric readings of one server and keeps them as the single cache entry later live frames can
 * write into.
 *
 * The first value comes from REST. The query never polls: it has no refetch interval, is never stale on its own and
 * does not refetch on window focus. Every loaded reading is stamped with `performance.now()` so the browser age check
 * does not depend on the browser clock. A refetch replaces the cached reading unless it is an older sample that still
 * carries a value (a reordered response); a reading without an as-of time, which the control plane sends for a
 * stopped or restarted server, always replaces it. An unchanged reading keeps its receive stamp.
 *
 * @param serverId The id of the server; an empty id disables the query.
 * @returns The query result; each reading of `data` is a value or an unavailable reason code.
 */
export function useServerMetrics(serverId: string): UseQueryResult<ServerMetricsReading> {
  const queryClient = useQueryClient();
  return useQuery({
    queryKey: hostingQueryKeys.metrics(serverId),
    queryFn: async ({ signal }) => {
      const loaded = await hostingService.serverMetrics(serverId, signal);
      const cached = queryClient.getQueryData<ServerMetricsReading>(
        hostingQueryKeys.metrics(serverId),
      );
      if (cached !== undefined && !replacesCachedReading(loaded, cached)) return cached;
      return { ...loaded, receivedAtMs: performance.now() };
    },
    enabled: serverId !== "",
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
}

/**
 * Loads the retained metric history of one server for one range.
 *
 * The history is never polled and never merged in the browser: it is loaded once per range, then refetched only
 * when the live provider invalidates it (at most once per bucket interval) or when the user asks to try again.
 * While a different range of the same server loads, the previous range's data stays as placeholder data so the
 * charts do not jump; the data of another server is never used as a placeholder.
 *
 * @param serverId The id of the server; an empty id disables the query.
 * @param range The range to load: `1h`, `24h` or `7d`.
 * @returns The query result; `isPlaceholderData` is true while the previous range is shown.
 */
export function useServerMetricsHistory(
  serverId: string,
  range: ServerMetricsRange,
): UseQueryResult<GameServerMetricsHistory> {
  return useQuery({
    queryKey: hostingQueryKeys.metricsHistory(serverId, range),
    queryFn: ({ signal }) => hostingService.serverMetricsHistory(serverId, range, signal),
    enabled: serverId !== "",
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    placeholderData: (previous, previousQuery) =>
      previousQuery?.queryKey[2] === serverId ? previous : undefined,
  });
}
