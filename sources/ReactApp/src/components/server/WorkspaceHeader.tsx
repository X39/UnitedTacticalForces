import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import "@/components/server/workspace-shell.css";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import { ServerStateSummary } from "@/components/server/ServerStateSummary";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { confirmVariantFor } from "@/features/hosting/actionVocabulary";
import { ActionLabel } from "@/features/hosting/components";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer } from "@/features/hosting/types";
import { primaryServerAction } from "@/features/hosting/workspaceModel";

/** The id of the workspace heading, so a surface that removes the control that had focus can move focus here. */
export const workspaceTitleId = "workspace-title";

/**
 * The compact sticky header of the server workspace.
 *
 * It holds the only page heading (the server name), the derived state with its cost indicator and the one
 * primary action. It is the single sticky element of the workspace shell. Activating the action only asks the
 * shared cost confirmation to open with the `workspace-header` source; this component never executes an action.
 * The title is clamped by CSS only, so the full name stays in the DOM and in the breadcrumb. The heading is
 * programmatically focusable (`tabIndex={-1}`) and has the stable id {@link workspaceTitleId}.
 *
 * @param props.server The server the workspace shows.
 */
export function WorkspaceHeader({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
  const derived = deriveServerState(server);
  const action = primaryServerAction(server.actions, server.deployment?.possibleCost ?? false);
  return (
    <header className="workspace-header">
      <div className="workspace-header__identity">
        <h1 id={workspaceTitleId} className="workspace-header__title" tabIndex={-1}>
          <bdi>{server.title}</bdi>
        </h1>
        <ServerStateSummary derived={derived} />
      </div>
      {action && (
        <div className="workspace-header__action">
          <ActionButton
            variant={action.pending ? "ghost" : confirmVariantFor(action.id)}
            unavailableReason={
              actionIsUnavailable(action) ? (
                <ActionUnavailableReason server={server} action={action} />
              ) : undefined
            }
            onClick={() =>
              request({
                serverId: server.primaryKey,
                actionId: action.id,
                source: "workspace-header",
              })
            }
          >
            {action.pending ? t("Server_Action_InProgress") : <ActionLabel action={action} />}
          </ActionButton>
        </div>
      )}
    </header>
  );
}
