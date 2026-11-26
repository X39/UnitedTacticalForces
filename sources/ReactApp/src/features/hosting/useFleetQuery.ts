import { useQuery } from "@tanstack/react-query";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";

/**
 * Loads the fleet of game servers the viewer may see.
 *
 * This is the one place the fleet query options live, so the fleet page, the profile menu and
 * the overview share one cache entry and never diverge. The query does not poll: updates arrive
 * through the live fleet signal, which refetches this query, and through a manual refresh.
 *
 * @param enabled Whether the query may run, for example only for users with server access.
 */
export function useFleetQuery(enabled: boolean) {
  return useQuery({
    queryKey: hostingQueryKeys.fleet,
    queryFn: ({ signal }) => hostingService.servers(signal),
    enabled,
  });
}
