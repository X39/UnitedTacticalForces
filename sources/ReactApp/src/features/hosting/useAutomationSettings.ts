import { useMutation, useQueryClient, type UseMutationResult } from "@tanstack/react-query";
import {
  AutomationSettingsConflictError,
  AutomationSettingsForbiddenError,
  AutomationSettingsValidationError,
  hostingQueryKeys,
  hostingService,
} from "@/features/hosting/services";
import type {
  GameServer,
  ServerAutomation,
  ServerAutomationUpdate,
} from "@/features/hosting/types";

/**
 * Why saving the automation settings failed, as the panel tells the viewer apart:
 *
 * - `conflict`: the settings changed elsewhere since they were loaded (HTTP 409); keep the draft and offer a reload.
 * - `forbidden`: the viewer may no longer change them (HTTP 403); switch to the read-only view.
 * - `invalid`: the API rejected the draft (HTTP 400); mark the fields the error lists.
 * - `error`: anything else, including HTTP 503 and network failures; keep the draft and offer a retry.
 */
export type AutomationSaveFailure = "conflict" | "forbidden" | "invalid" | "error";

/**
 * Classifies a rejected automation save.
 *
 * @param error The value the save mutation rejected with.
 * @returns The failure the panel reacts to; anything that is not a typed save error is `error`.
 */
export function automationSaveFailure(error: unknown): AutomationSaveFailure {
  if (error instanceof AutomationSettingsConflictError) return "conflict";
  if (error instanceof AutomationSettingsForbiddenError) return "forbidden";
  if (error instanceof AutomationSettingsValidationError) return "invalid";
  return "error";
}

/**
 * Saves the automation settings of one server with one PUT and keeps the server cache in step.
 *
 * On success the returned block replaces `automation` in the exact `hostingQueryKeys.server(serverId)` cache entry
 * (the other server fields stay as they were), and that exact key is invalidated so the next read confirms it. A
 * missing cache entry is not created. The mutation never polls or retries on its own.
 *
 * @param serverId The id of the server whose automation is saved.
 * @returns The mutation; its variable is the full update including the loaded version.
 */
export function useSaveAutomationSettings(
  serverId: string,
): UseMutationResult<ServerAutomation, Error, ServerAutomationUpdate> {
  const queryClient = useQueryClient();
  return useMutation<ServerAutomation, Error, ServerAutomationUpdate>({
    mutationFn: (update) => hostingService.saveAutomationSettings(serverId, update),
    onSuccess: (automation) => {
      const queryKey = hostingQueryKeys.server(serverId);
      queryClient.setQueryData<GameServer | undefined>(queryKey, (current) =>
        current ? { ...current, automation } : current,
      );
      void queryClient.invalidateQueries({ queryKey, exact: true });
    },
  });
}
