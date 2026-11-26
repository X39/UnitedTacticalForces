import { useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import {
  accruedPresentation,
  isNewerAccrued,
  type AccruedPresentation,
} from "@/features/hosting/accruedModel";
import { deriveServerState } from "@/features/hosting/serverState";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, GameServerAccruedCost } from "@/features/hosting/types";

/**
 * Loads the accrued session cost of one server and keeps it as the single cache entry the live frames write into.
 *
 * The first value comes from REST. Later values are written into the same query cache entry by the fleet socket
 * provider, so this query never polls: it has no refetch interval, is never stale on its own and does not refetch
 * on window focus. It refetches only through the workspace exact invalidation: a fleet signal for the server, a
 * socket reopen after a disconnect, and Refresh server. A refetch that returns an older value than the cached one
 * (a reordered response) keeps the cached value, so the shown amount never moves backwards.
 *
 * @param serverId The id of the server; an empty id disables the query.
 * @returns The query result; `data.session` is null when the server never started.
 */
export function useAccruedLive(serverId: string): UseQueryResult<GameServerAccruedCost> {
  const queryClient = useQueryClient();
  return useQuery({
    queryKey: hostingQueryKeys.accruedCost(serverId),
    queryFn: async ({ signal }) => {
      const loaded = await hostingService.accruedCost(serverId, signal);
      const cached = queryClient.getQueryData<GameServerAccruedCost>(
        hostingQueryKeys.accruedCost(serverId),
      );
      return cached !== undefined && !isNewerAccrued(loaded, cached) ? cached : loaded;
    },
    enabled: serverId !== "",
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
}

/**
 * Derives the presentation of the accrued row of one server from the shared accrued query and the derived server
 * state, so the row term and the row body can never disagree.
 *
 * @param server The server whose accrued row is presented.
 * @returns The presentation; the loading state while the first value is being fetched.
 */
export function useAccruedPresentation(server: GameServer): AccruedPresentation {
  const accrued = useAccruedLive(server.primaryKey);
  const loading = accrued.data === undefined && !accrued.isError;
  return accruedPresentation(accrued.data, deriveServerState(server), loading);
}
