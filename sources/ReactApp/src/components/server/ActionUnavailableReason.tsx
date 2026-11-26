import { useTranslation } from "react-i18next";
import { UnavailableReason } from "@/components/server/UnavailableReason";
import {
  deploymentStateReason,
  operationPendingReason,
} from "@/components/server/unavailableReasonMapping";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer, ServerAction } from "@/features/hosting/types";

/**
 * Tells whether the viewer cannot start an action right now.
 *
 * @param action The projected action, or any object carrying its `enabled` and `pending` flags.
 * @returns True when the action is disabled by the API or already pending.
 */
export function actionIsUnavailable(action: Pick<ServerAction, "enabled" | "pending">): boolean {
  return !action.enabled || action.pending;
}

/**
 * Shows why a projected action is unavailable for a server.
 *
 * The reason is the API reason when there is one; otherwise a pending action explains the pending operation
 * and any other unavailable action explains the deployment state. The server provides the localized state
 * label and the launch-disabled hint that refine the mapping.
 *
 * @param props.server The server the action belongs to.
 * @param props.action The projected action that is unavailable.
 */
export function ActionUnavailableReason({
  server,
  action,
}: {
  server: GameServer;
  action: ServerAction;
}) {
  const { t } = useTranslation();
  const reason =
    action.unavailableReason || (action.pending ? operationPendingReason : deploymentStateReason);
  return (
    <UnavailableReason
      reason={reason}
      stateLabel={t(deriveServerState(server).labelKey)}
      launchDisabled={action.id.toLowerCase() === "start" && server.isLaunchDisabled}
    />
  );
}
