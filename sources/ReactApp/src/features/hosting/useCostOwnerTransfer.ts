import { useMutation, useQueryClient, type UseMutationResult } from "@tanstack/react-query";
import { ApiError } from "@/api/client";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer } from "@/features/hosting/types";

/**
 * Why a cost owner transfer failed, as the panel tells the failures apart:
 *
 * - `conflict`: a deployment is unresolved, so the owner cannot change now (HTTP 409); the server is refetched.
 * - `unavailable`: the chosen owner may not be used any more or is frozen (HTTP 400); the options are refetched.
 * - `forbidden`: the viewer may not transfer this server (HTTP 403).
 * - `generic`: anything else, including HTTP 5xx and network failures.
 *
 * The server message is never shown, so the panel maps each value to fixed copy.
 */
export type CostOwnerTransferFailure = "conflict" | "unavailable" | "forbidden" | "generic";

/** The variables of one transfer. */
export interface CostOwnerTransferVariables {
  /** The cost owner that pays for the server from now on. */
  costOwnerId: string;
}

/**
 * Classifies a rejected cost owner transfer by its HTTP status.
 *
 * @param error The value the transfer mutation rejected with.
 * @returns The failure the panel reacts to; anything that is not a 409, 400 or 403 answer is `generic`.
 */
export function costOwnerTransferFailure(error: unknown): CostOwnerTransferFailure {
  if (!(error instanceof ApiError)) return "generic";
  if (error.status === 409) return "conflict";
  if (error.status === 400) return "unavailable";
  if (error.status === 403) return "forbidden";
  return "generic";
}

/**
 * Why a server cannot be transferred right now: `running` while a deployment is unresolved, `jobs` while lifecycle
 * jobs are queued or running.
 */
export type CostOwnerTransferBlocker = "running" | "jobs";

/**
 * Decides whether the cost owner of a server may be transferred now, with the rule the API enforces.
 *
 * The API answers 409 while the server has an unresolved deployment, that is any deployment that is not cleaned up,
 * including a stopped, failed or terminated one that is not resolved yet. The server projection sets `deployment`
 * exactly for those, so `deployment !== null` is the same predicate. It is deliberately not the command-center
 * deployment-active helper, which reads the status and would allow a transfer the API rejects. Pending lifecycle jobs
 * block as a precaution because they are about to create or change a deployment.
 *
 * @param server The server as loaded.
 * @returns `running` for an unresolved deployment, else `jobs` for pending jobs, else null when a transfer may run.
 */
export function transferBlocker(server: GameServer): CostOwnerTransferBlocker | null {
  if (server.deployment !== null) return "running";
  if (server.pendingJobCount > 0) return "jobs";
  return null;
}

/**
 * Transfers the cost owner of one server with one PUT and refreshes exactly what a transfer changes.
 *
 * A success invalidates the exact server entry (owner name, frozen flag and since), the exact fleet list (the owner
 * of the card), the exact accrued cost entry of the server and the owner options of the server by prefix (the
 * previous owner is listed again and the new one drops out). A transfer to the current owner answers 204 like any
 * other success, so it counts as one. A conflict refetches the exact server entry and an unavailable owner refetches
 * the options, so the panel shows the truth the API just enforced. The mutation never polls or retries on its own.
 *
 * @param serverId The server whose cost owner is transferred.
 * @returns The mutation; its variable carries the chosen cost owner id.
 */
export function useCostOwnerTransfer(
  serverId: string,
): UseMutationResult<void, Error, CostOwnerTransferVariables> {
  const queryClient = useQueryClient();
  return useMutation<void, Error, CostOwnerTransferVariables>({
    mutationFn: ({ costOwnerId }) => hostingService.changeServerCostOwner(serverId, costOwnerId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: hostingQueryKeys.server(serverId),
        exact: true,
      });
      void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet, exact: true });
      void queryClient.invalidateQueries({
        queryKey: hostingQueryKeys.accruedCost(serverId),
        exact: true,
      });
      void queryClient.invalidateQueries({
        queryKey: hostingQueryKeys.costOwnerOptionsPrefix(serverId),
      });
    },
    onError: (error) => {
      const failure = costOwnerTransferFailure(error);
      if (failure === "conflict") {
        void queryClient.invalidateQueries({
          queryKey: hostingQueryKeys.server(serverId),
          exact: true,
        });
      } else if (failure === "unavailable") {
        void queryClient.invalidateQueries({
          queryKey: hostingQueryKeys.costOwnerOptionsPrefix(serverId),
        });
      }
    },
  });
}
