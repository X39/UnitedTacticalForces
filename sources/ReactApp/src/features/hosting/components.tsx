import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import { ApiText } from "@/components/server/ApiText";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { gridVariantFor, lookupActionVocabulary } from "@/features/hosting/actionVocabulary";
import { humanizeToken } from "@/features/hosting/model";
import type { GameServer, ServerAction } from "@/features/hosting/types";

/**
 * Shows the localized label of an action.
 *
 * Known actions use the translated vocabulary label; any other action shows the API label (or the humanized
 * id) inside a `lang="en"` element because API prose is English.
 *
 * @param props.action The projected action to name.
 */
export function ActionLabel({ action }: { action: Pick<ServerAction, "id" | "label"> }) {
  const { t } = useTranslation();
  const vocabulary = lookupActionVocabulary(action.id);
  if (vocabulary) return <>{t(vocabulary.label)}</>;
  return <ApiText>{action.label || humanizeToken(action.id)}</ApiText>;
}

/**
 * Lists the actions of a server. Every button requests the shared cost confirmation; this component never
 * executes an action itself.
 *
 * @param props.server The server the actions belong to.
 * @param props.actions The projected actions to list.
 */
export function ServerActions({
  server,
  actions,
}: {
  server: GameServer;
  actions: ServerAction[];
}) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
  if (actions.length === 0)
    return <p className="next-hosting-muted">{t("Hosting_Detail_NoActions")}</p>;
  return (
    <div className="next-hosting-action-grid">
      {[...actions]
        .sort((left, right) => left.order - right.order)
        .map((action) => (
          <article key={action.id} className="next-hosting-action-card">
            <div>
              <strong>
                <ActionLabel action={action} />
              </strong>
              {action.description && (
                <p>
                  <ApiText>{action.description}</ApiText>
                </p>
              )}
            </div>
            <ActionButton
              variant={gridVariantFor(action.id)}
              unavailableReason={
                actionIsUnavailable(action) ? (
                  <ActionUnavailableReason server={server} action={action} />
                ) : undefined
              }
              onClick={() =>
                request({
                  serverId: server.primaryKey,
                  actionId: action.id,
                  source: "workspace-action",
                })
              }
            >
              {action.pending ? t("Server_Action_InProgress") : <ActionLabel action={action} />}
            </ActionButton>
          </article>
        ))}
    </div>
  );
}
