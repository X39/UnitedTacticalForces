import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { ActionUnavailableReason } from "@/components/server/ActionUnavailableReason";
import { ownerFor } from "@/components/server/costConfirmationModel";
import { ServerStateSummary } from "@/components/server/ServerStateSummary";
import { UnavailableReason } from "@/components/server/UnavailableReason";
import {
  deploymentStateReason,
  operationPendingReason,
} from "@/components/server/unavailableReasonMapping";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { ActionLabel } from "@/features/hosting/components";
import { fleetStageKey, primaryActionFor } from "@/features/hosting/fleetModel";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer } from "@/features/hosting/types";
import "@/components/server/fleet-card.css";

/** Props of {@link FleetServerCard}. */
export interface FleetServerCardProps {
  /** The server the card shows. */
  server: GameServer;
  /**
   * Whether a confirmed action of this server was accepted and the fleet has not refreshed since. While true the
   * primary control is disabled with the request-sent reason; the state badge still shows the server's data.
   */
  submitted: boolean;
  /**
   * Called with the server id once the shared cost confirmation reports that the API accepted the action.
   *
   * @param serverId Primary key of the server whose action was accepted.
   */
  onSubmitted(serverId: string): void;
}

/**
 * Shows one server of the fleet: game, title, state and cost, cost owner, one primary action and Open server.
 *
 * Every decision comes from the pure fleet model: `primaryActionFor` picks the control and `fleetStageKey` the
 * stage line, so the card never reads a raw deployment status. Activating the primary action only requests the
 * shared cost confirmation with source `fleet-card`; the card never executes an action and never opens a dialog
 * of its own. Only start and stop are offered here; every other lifecycle action stays in the server workspace.
 * A disabled primary stays focusable, shows its reason and is described by the card title.
 *
 * @param props The server, its submitted flag and the submitted callback.
 * @returns The card article.
 */
export function FleetServerCard({ server, submitted, onSubmitted }: FleetServerCardProps) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
  const derived = deriveServerState(server);
  const primary = primaryActionFor(server, derived);
  const stageKey = fleetStageKey(derived);
  const owner = ownerFor(server.costOwnerName);
  const titleId = `server-${server.primaryKey}-title`;

  let ownerText: string;
  switch (owner.kind) {
    case "name":
      ownerText = owner.name ?? t("Server_Confirm_OwnerUnknown");
      break;
    case "hidden":
      ownerText = t("Server_Confirm_OwnerHidden");
      break;
    case "unknown":
      ownerText = t("Server_Confirm_OwnerUnknown");
      break;
  }

  let primaryControl: ReactNode = null;
  if (primary.kind !== "open-only" && submitted) {
    primaryControl = (
      <ActionButton
        variant="ghost"
        fullWidth
        aria-describedby={titleId}
        unavailableReason={t("Server_Fleet_Submitted")}
      >
        {t("Server_Action_InProgress")}
      </ActionButton>
    );
  } else if (primary.kind === "in-progress") {
    primaryControl = (
      <ActionButton
        variant="ghost"
        fullWidth
        aria-describedby={titleId}
        unavailableReason={
          <UnavailableReason
            reason={primary.reason === "pending" ? operationPendingReason : deploymentStateReason}
            stateLabel={t(derived.labelKey)}
          />
        }
      >
        {t("Server_Action_InProgress")}
      </ActionButton>
    );
  } else if (primary.kind === "action" && !primary.enabled) {
    const { action } = primary;
    primaryControl = (
      <ActionButton
        variant={primary.variant}
        fullWidth
        aria-describedby={titleId}
        unavailableReason={<ActionUnavailableReason server={server} action={action} />}
      >
        {action.pending ? t("Server_Action_InProgress") : <ActionLabel action={action} />}
      </ActionButton>
    );
  } else if (primary.kind === "action") {
    const { action } = primary;
    primaryControl = (
      <ActionButton
        variant={primary.variant}
        fullWidth
        aria-describedby={titleId}
        onClick={() =>
          request({
            serverId: server.primaryKey,
            actionId: action.id,
            source: "fleet-card",
            onSubmitted: () => onSubmitted(server.primaryKey),
          })
        }
      >
        <ActionLabel action={action} />
      </ActionButton>
    );
  }

  return (
    <article className="fleet-card" aria-labelledby={titleId} data-server-card={server.primaryKey}>
      <span className="fleet-card__kicker">{server.gameIdentifier}</span>
      <h3 id={titleId} className="fleet-card__title">
        <bdi>{server.title}</bdi>
      </h3>
      <ServerStateSummary derived={derived} />
      {stageKey !== null && <p className="fleet-card__stage">{t(stageKey)}</p>}
      <dl className="fleet-card__facts">
        <div>
          <dt>{t("Hosting_Common_CostOwner")}</dt>
          <dd>{ownerText}</dd>
        </div>
      </dl>
      <div className="fleet-card__actions">
        {primaryControl}
        <ActionLink
          variant={primary.kind === "open-only" ? "primary" : "ghost"}
          fullWidth
          to={`/command/servers/${encodeURIComponent(server.primaryKey)}`}
          aria-describedby={titleId}
          data-server-id={server.primaryKey}
        >
          {t("Server_Fleet_Open")}
        </ActionLink>
      </div>
    </article>
  );
}
