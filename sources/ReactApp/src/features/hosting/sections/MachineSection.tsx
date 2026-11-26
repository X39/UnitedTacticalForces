import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import "@/components/server/sections.css";
import { ActionLabel } from "@/features/hosting/components";
import { CloudMachinePanel } from "@/features/hosting/sections/CloudMachinePanel";
import { deriveServerState } from "@/features/hosting/serverState";
import { ServerRuntimePanel } from "@/features/hosting/ServerRuntimePanel";
import type { GameServer } from "@/features/hosting/types";
import { costStopAction } from "@/features/hosting/workspaceModel";

/**
 * The Machine section: a plain-words cost notice with the shared cost-stop, the cloud machine facts and the
 * runtime settings.
 *
 * The notice shows only while the derived cost of the server is live or maybe. Its button is the one cost-stop
 * action that {@link costStopAction} chooses, requests the shared confirmation with the source
 * `workspace-machine` and never executes anything itself; an unavailable action stays visible and explains why.
 * The button is omitted when the server projects no cost-stop action.
 *
 * @param props.server The server whose machine and runtime settings are shown.
 */
export function MachineSection({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
  const cost = deriveServerState(server).cost;
  const stop = cost === "none" ? undefined : costStopAction(server.actions);
  return (
    <div className="workspace-section">
      {cost !== "none" && (
        <StatePanel
          kind="warning"
          action={
            stop ? (
              <ActionButton
                variant="danger"
                unavailableReason={
                  actionIsUnavailable(stop) ? (
                    <ActionUnavailableReason server={server} action={stop} />
                  ) : undefined
                }
                onClick={() =>
                  request({
                    serverId: server.primaryKey,
                    actionId: stop.id,
                    source: "workspace-machine",
                  })
                }
              >
                {stop.pending ? t("Server_Action_InProgress") : <ActionLabel action={stop} />}
              </ActionButton>
            ) : undefined
          }
        >
          {t("Server_Machine_MayCost")}
        </StatePanel>
      )}
      <CloudMachinePanel server={server} />
      <ServerRuntimePanel server={server} />
    </div>
  );
}
