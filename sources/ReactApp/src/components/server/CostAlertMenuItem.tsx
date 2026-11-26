import { Alert16Regular, Dismiss16Regular } from "@fluentui/react-icons";
import { type RefObject } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { ActionButton } from "@/components/ActionControl";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import "@/components/server/cost-alerts.css";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { ActionLabel } from "@/features/hosting/components";
import {
  formatAlertAmount,
  formatAlertThreshold,
  formatInstant,
} from "@/features/hosting/costAlertFormat";
import { deriveServerState } from "@/features/hosting/serverState";
import type { CostAlert, GameServer } from "@/features/hosting/types";
import { useAutoStopReason } from "@/features/hosting/useAutoStopNotice";
import { isAutoStopAlert, useDismissCostAlerts } from "@/features/hosting/useCostAlerts";
import { costStopAction } from "@/features/hosting/workspaceModel";

/** Props of {@link CostAlertMenuItem}. */
export interface CostAlertMenuItemProps {
  /** The alert the row shows. */
  alert: CostAlert;
  /**
   * The server of the alert as the fleet query returned it, or undefined while the fleet has not loaded or does not
   * contain the server. The row never invents an action for a server it cannot see.
   */
  server: GameServer | undefined;
  /** Called when the viewer follows the link or asks for the cost-stop confirmation, so the menu can close. */
  onNavigate(): void;
  /** Element that receives focus when the shared cost confirmation closes; the profile menu trigger. */
  returnFocus?: RefObject<HTMLElement | null> | undefined;
  /** Called once after the API accepted the dismissal of this alert, so the menu can move focus. */
  onDismissed(alertId: string): void;
}

/**
 * One alert row inside the profile menu's Server attention list: a cost threshold row, or an automatic shutdown
 * warning row when `alert.kind` is `AutoStop`. The shutdown row is described on its own component below. The
 * threshold row is described here.
 *
 * The row has two columns like the server rows next to it: an alert icon in the warning color, then the text
 * column. The text column links to the server workspace with the server name in `strong` and `bdi` as its only
 * bold line, says which threshold was reached in normal weight, shows what the session had cost then in a muted
 * line and ends with the actions under the text: the cost-stop button and Dismiss alert. When the server is in the
 * fleet result and cost may still run, a ghost compact cost-stop button asks the shared confirmation to open with
 * the source `alert-notification`; the row never executes the action and never invents one for a server it cannot
 * see. A failed dismissal keeps the row and shows an inline alert line. The row is removed by the alerts cache once
 * the API accepted the dismissal, and a success toast confirms it (D-17).
 *
 * @param props See {@link CostAlertMenuItemProps}.
 */
export function CostAlertMenuItem(props: CostAlertMenuItemProps) {
  return isAutoStopAlert(props.alert) ? (
    <AutoStopAlertRow {...props} />
  ) : (
    <ThresholdAlertRow {...props} />
  );
}

/**
 * One automatic shutdown warning row inside the profile menu's Server attention list.
 *
 * The row has the same two columns as the threshold row: the alert icon, then the text column with the server name
 * link, the shutdown time line, the muted reason sentence and a Dismiss alert button. It carries
 * `data-auto-stop-alert-id` instead of `data-cost-alert-id`, so selectors of threshold rows keep counting threshold
 * rows only. There is no cost-stop button: the stop is already scheduled and the workspace owns the actions.
 * Dismissing hides the record for this viewer only and never cancels the stop. A failed dismissal keeps the row and
 * shows an inline alert line.
 *
 * @param props See {@link CostAlertMenuItemProps}; `server` and `returnFocus` are not used by this row.
 */
function AutoStopAlertRow({ alert, onNavigate, onDismissed }: CostAlertMenuItemProps) {
  const { t } = useTranslation();
  const { dismiss, pendingIds, failed } = useDismissCostAlerts();
  const reasonSentence = useAutoStopReason();
  // The API sends the stop instant; the creation instant stands in only for a record that carries none.
  const time = formatInstant(alert.stopsAt ?? alert.createdAt);
  const reason = reasonSentence(alert.stopReason, alert.stopLimit);
  const pending = pendingIds.has(alert.id);
  return (
    <article
      className="account-profile__notification account-profile__notification--alert"
      data-auto-stop-alert-id={alert.id}
    >
      <Alert16Regular className="cost-alert-item__icon" aria-hidden="true" />
      <div className="cost-alert-item__content">
        <Link
          className="account-profile__notification-body"
          to={`/command/servers/${encodeURIComponent(alert.serverId)}`}
          onClick={onNavigate}
        >
          <strong>
            <bdi>{alert.serverName}</bdi>
          </strong>
        </Link>
        <p className="cost-alert-item__reached">
          <time dateTime={alert.stopsAt ?? alert.createdAt}>
            {t("Server_Automation_Menu_Line", { time })}
          </time>
        </p>
        {reason !== null && <p className="cost-alert-item__amount">{reason}</p>}
        <div className="cost-alert-item__actions">
          <ActionButton
            variant="ghost"
            size="compact"
            aria-label={t("Server_Automation_Menu_DismissLabel", { time })}
            aria-disabled={pending ? true : undefined}
            onClick={() => {
              if (pending) return;
              void dismiss(alert).then((accepted) => {
                if (accepted) onDismissed(alert.id);
              });
            }}
          >
            <Dismiss16Regular aria-hidden="true" />
            {t("Server_Alerts_Dismiss")}
          </ActionButton>
        </div>
        {failed && (
          <p className="cost-alert-item__error" role="alert">
            {t("Server_Alerts_DismissError")}
          </p>
        )}
      </div>
    </article>
  );
}

/**
 * One threshold alert row; see {@link CostAlertMenuItem} for what it shows.
 *
 * @param props See {@link CostAlertMenuItemProps}.
 */
function ThresholdAlertRow({
  alert,
  server,
  onNavigate,
  returnFocus,
  onDismissed,
}: CostAlertMenuItemProps) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
  const { dismiss, pendingIds, failed } = useDismissCostAlerts();
  const stop =
    server === undefined || deriveServerState(server).cost === "none"
      ? undefined
      : costStopAction(server.actions);
  const threshold = formatAlertThreshold(alert);
  const pending = pendingIds.has(alert.id);
  return (
    <article
      className="account-profile__notification account-profile__notification--alert"
      data-cost-alert-id={alert.id}
    >
      <Alert16Regular className="cost-alert-item__icon" aria-hidden="true" />
      <div className="cost-alert-item__content">
        <Link
          className="account-profile__notification-body"
          to={`/command/servers/${encodeURIComponent(alert.serverId)}`}
          onClick={onNavigate}
        >
          <strong>
            <bdi>{alert.serverName}</bdi>
          </strong>
        </Link>
        <p className="cost-alert-item__reached">{t("Server_Alerts_Menu_Reached", { threshold })}</p>
        <p className="cost-alert-item__amount">
          <time dateTime={alert.createdAt}>
            {t("Server_Alerts_Row_Amount", {
              amount: formatAlertAmount(alert),
              time: formatInstant(alert.createdAt),
            })}
          </time>
        </p>
        <div className="cost-alert-item__actions">
          {server !== undefined && stop !== undefined && (
            <ActionButton
              variant="ghost"
              size="compact"
              unavailableReason={
                actionIsUnavailable(stop) ? (
                  <ActionUnavailableReason server={server} action={stop} />
                ) : undefined
              }
              onClick={() => {
                onNavigate();
                request({
                  serverId: server.primaryKey,
                  actionId: stop.id,
                  source: "alert-notification",
                  returnFocus,
                });
              }}
            >
              {stop.pending ? t("Server_Action_InProgress") : <ActionLabel action={stop} />}
            </ActionButton>
          )}
          <ActionButton
            variant="ghost"
            size="compact"
            aria-label={t("Server_Alerts_DismissLabel", { threshold })}
            aria-disabled={pending ? true : undefined}
            onClick={() => {
              if (pending) return;
              void dismiss(alert).then((accepted) => {
                if (accepted) onDismissed(alert.id);
              });
            }}
          >
            <Dismiss16Regular aria-hidden="true" />
            {t("Server_Alerts_Dismiss")}
          </ActionButton>
        </div>
        {failed && (
          <p className="cost-alert-item__error" role="alert">
            {t("Server_Alerts_DismissError")}
          </p>
        )}
      </div>
    </article>
  );
}
