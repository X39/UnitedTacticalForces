import { Dismiss16Regular } from "@fluentui/react-icons";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import "@/components/server/cost-alerts.css";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { workspaceTitleId } from "@/components/server/WorkspaceHeader";
import { confirmVariantFor } from "@/features/hosting/actionVocabulary";
import { ActionLabel } from "@/features/hosting/components";
import {
  formatAlertAmount,
  formatAlertThreshold,
  formatInstant,
} from "@/features/hosting/costAlertFormat";
import { deriveServerState } from "@/features/hosting/serverState";
import type { CostAlert, GameServer } from "@/features/hosting/types";
import {
  isAutoStopAlert,
  useCostAlerts,
  useDismissCostAlerts,
} from "@/features/hosting/useCostAlerts";
import { costStopAction } from "@/features/hosting/workspaceModel";

/** The most alert rows the banner lists; older alerts collapse into one line. */
const maxVisibleRows = 3;

/**
 * One alert row of the banner: the threshold that was reached, what the session cost then, and the dismiss control.
 * The row never repeats the server name, because the workspace heading above the banner already names it.
 */
function AlertRow({
  alert,
  divided,
  pending,
  onDismiss,
  buttonRef,
}: {
  alert: CostAlert;
  divided: boolean;
  pending: boolean;
  onDismiss(): void;
  buttonRef(element: HTMLButtonElement | null): void;
}) {
  const { t } = useTranslation();
  const threshold = formatAlertThreshold(alert);
  return (
    <li
      className={
        divided
          ? "cost-alert-banner__row cost-alert-banner__row--divided"
          : "cost-alert-banner__row"
      }
    >
      <div className="cost-alert-banner__text">
        <p className="cost-alert-banner__reached">
          {t("Server_Alerts_Banner_RowReached", { threshold })}
        </p>
        <p className="cost-alert-banner__amount">
          <time dateTime={alert.createdAt}>
            {t("Server_Alerts_Row_Amount", {
              amount: formatAlertAmount(alert),
              time: formatInstant(alert.createdAt),
            })}
          </time>
        </p>
      </div>
      <ActionButton
        ref={buttonRef}
        variant="ghost"
        size="compact"
        aria-label={t("Server_Alerts_DismissLabel", { threshold })}
        aria-disabled={pending ? true : undefined}
        onClick={() => {
          if (!pending) onDismiss();
        }}
      >
        <Dismiss16Regular aria-hidden="true" />
        {t("Server_Alerts_Dismiss")}
      </ActionButton>
    </li>
  );
}

/** Where focus goes once the dismissed alerts have left the banner. */
interface FocusRequest {
  /** The ids that must be gone before focus moves. */
  removedIds: readonly string[];
  /** The alert whose Dismiss button takes focus, when it is still listed. */
  nextId: string | null;
  /** True when focus goes to the workspace heading regardless of what remains (Dismiss all). */
  toTitle: boolean;
}

/**
 * The cost alert banner of one server workspace.
 *
 * It is one quiet warning panel. It lists the viewer's undismissed alerts of this server, newest first, up to three
 * rows separated by dividers, with one older-alerts line beyond that. Each row says which amount was reached and
 * what the session had cost at that moment; it does not repeat the server name, which the workspace heading shows.
 * When cost has stopped the banner says so; the alert itself stays until the viewer dismisses it. One footer holds
 * the controls that act on the whole banner: while cost may still run, a cost-stop button opens the shared
 * confirmation with the source `alert-banner`, followed by Dismiss all when two or more alerts are listed. The
 * banner never executes the action, renders no button when the server has no cost-stop action and renders a
 * disabled one with its reason when the action is unavailable. A row leaves only after the API accepted its
 * dismissal, which a success toast confirms (D-17); a failure keeps it and shows an alert line. After a dismissal
 * focus moves to the next row's Dismiss button, else the cost-stop button, else the workspace heading; Dismiss all
 * moves focus to the heading. Nothing
 * renders while the alerts have not loaded or when this server has no alert, so an alert of another server never
 * shows. There is no timer, no auto-dismiss and no focus stealing on arrival.
 *
 * @param props.server The server of the open workspace.
 * @returns The warning panel, or null when there is nothing to show.
 */
export function CostAlertBanner({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
  const { loaded, forServer } = useCostAlerts();
  const { dismiss, dismissAll, pendingIds, failed } = useDismissCostAlerts();
  // Automatic shutdown records belong to the shutdown notice and the profile menu, never to this cost banner.
  const alerts = forServer(server.primaryKey).filter((alert) => !isAutoStopAlert(alert));
  const [focusRequest, setFocusRequest] = useState<FocusRequest | null>(null);
  const handledFocus = useRef<FocusRequest | null>(null);
  const rowButtons = useRef(new Map<string, HTMLButtonElement>());
  const stopButton = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!focusRequest || handledFocus.current === focusRequest) return;
    if (alerts.some((alert) => focusRequest.removedIds.includes(alert.id))) return;
    handledFocus.current = focusRequest;
    const title = document.getElementById(workspaceTitleId);
    if (focusRequest.toTitle) {
      title?.focus();
      return;
    }
    const next =
      focusRequest.nextId === null ? undefined : rowButtons.current.get(focusRequest.nextId);
    (next ?? (alerts.length > 0 ? stopButton.current : null) ?? title)?.focus();
  }, [focusRequest, alerts]);

  if (!loaded || alerts.length === 0) return null;

  const visible = alerts.slice(0, maxVisibleRows);
  const older = alerts.length - visible.length;
  const derived = deriveServerState(server);
  const ended = derived.cost === "none";
  // With no cost left to stop there is nothing to offer: the ended line says why.
  const stop = ended ? undefined : costStopAction(server.actions);
  const busy = pendingIds.size > 0;

  const dismissRow = async (alert: CostAlert) => {
    const index = alerts.findIndex((candidate) => candidate.id === alert.id);
    const nextId = alerts[index + 1]?.id ?? null;
    if (await dismiss(alert)) {
      setFocusRequest({ removedIds: [alert.id], nextId, toTitle: false });
    }
  };

  const dismissEvery = async () => {
    const removedIds = alerts.map((alert) => alert.id);
    if (await dismissAll(server.primaryKey)) {
      setFocusRequest({ removedIds, nextId: null, toTitle: true });
    }
  };

  const stopControl =
    stop === undefined ? null : (
      <ActionButton
        ref={stopButton}
        variant={stop.pending ? "ghost" : confirmVariantFor(stop.id)}
        unavailableReason={
          actionIsUnavailable(stop) ? (
            <ActionUnavailableReason server={server} action={stop} />
          ) : undefined
        }
        onClick={() =>
          request({
            serverId: server.primaryKey,
            actionId: stop.id,
            source: "alert-banner",
            returnFocus: stopButton,
          })
        }
      >
        {stop.pending ? t("Server_Action_InProgress") : <ActionLabel action={stop} />}
      </ActionButton>
    );
  const dismissAllControl =
    alerts.length < 2 ? null : (
      <ActionButton
        variant="ghost"
        aria-label={t("Server_Alerts_DismissAllLabel", { count: alerts.length })}
        aria-disabled={busy ? true : undefined}
        onClick={() => {
          if (!busy) void dismissEvery();
        }}
      >
        <Dismiss16Regular aria-hidden="true" />
        {t("Server_Alerts_DismissAll")}
      </ActionButton>
    );

  return (
    <StatePanel kind="warning" title={t("Server_Alerts_Banner_Title", { count: alerts.length })}>
      <div className="cost-alert-banner">
        <ul className="cost-alert-banner__list">
          {visible.map((alert, index) => (
            <AlertRow
              key={alert.id}
              alert={alert}
              divided={index > 0}
              pending={pendingIds.has(alert.id)}
              onDismiss={() => void dismissRow(alert)}
              buttonRef={(element) => {
                if (element) rowButtons.current.set(alert.id, element);
                else rowButtons.current.delete(alert.id);
              }}
            />
          ))}
        </ul>
        {older > 0 && (
          <p className="cost-alert-banner__older">
            {t("Server_Alerts_Banner_Older", { count: older })}
          </p>
        )}
        {ended && <p className="cost-alert-banner__ended">{t("Server_Alerts_Banner_Ended")}</p>}
        {(stopControl !== null || dismissAllControl !== null) && (
          <div className="cost-alert-banner__footer">
            {stopControl}
            {dismissAllControl}
          </div>
        )}
        {failed && (
          <p className="cost-alert-banner__error" role="alert">
            {t("Server_Alerts_DismissError")}
          </p>
        )}
      </div>
    </StatePanel>
  );
}
