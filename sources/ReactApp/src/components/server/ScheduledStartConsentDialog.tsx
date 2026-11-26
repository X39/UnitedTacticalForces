import { useTranslation } from "react-i18next";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionButton } from "@/components/ActionControl";
import { ConfirmDialog, StatePanel } from "@/components/primitives";
import "@/components/server/automation.css";
import { serverCostFacts } from "@/components/server/costConfirmationModel";
import "@/components/server/server.css";
import { formatHourlyRate, formatMoney } from "@/features/hosting/model";
import type { GameServer } from "@/features/hosting/types";

/** Props of {@link ScheduledStartConsentDialog}. */
export interface ScheduledStartConsentDialogProps {
  /** True while the dialog is shown. */
  open: boolean;
  /** The server whose scheduled starts are confirmed; its cost owner and estimates are the facts shown. */
  server: GameServer;
  /** How many start rules the settings being saved hold. */
  startRuleCount: number;
  /** True while the save request is in flight: both buttons are blocked and the confirm button says it is saving. */
  busy: boolean;
  /** The key of the save error to show inside the dialog, or null when there is none. */
  errorKey: TranslationKey | null;
  /** Called when the viewer allows the scheduled starts. */
  onConfirm: () => void;
  /** Called when the viewer keeps editing instead. */
  onDismiss: () => void;
}

/**
 * The scheduled-start cost consent: who pays, what a running server costs and how many rules start the server.
 *
 * A scheduled start creates a cloud machine without anyone confirming it at the time, so the settings with a new or
 * changed start rule are only sent after the viewer saw this dialog. The facts are the cost owner and the hourly and
 * monthly estimates the cost confirmation shows; a missing estimate reads "No estimate". Initial focus is on "Keep
 * editing", and a failed save is told inside the dialog, which stays open. The body is a labelled, focusable region,
 * because it scrolls inside the dialog on the smallest phone and a scrolling area must be reachable by keyboard.
 *
 * @param props The server, the rule count, the request state and the two callbacks.
 * @returns The dialog.
 */
export function ScheduledStartConsentDialog({
  open,
  server,
  startRuleCount,
  busy,
  errorKey,
  onConfirm,
  onDismiss,
}: ScheduledStartConsentDialogProps) {
  const { t } = useTranslation();
  const facts = serverCostFacts(server);
  const noEstimate = t("Server_Workspace_Cost_NoEstimate");
  const ownerText =
    facts.owner.kind === "name"
      ? (facts.owner.name ?? "")
      : facts.owner.kind === "hidden"
        ? t("Server_Confirm_OwnerHidden")
        : t("Server_Confirm_OwnerUnknown");
  return (
    <ConfirmDialog
      open={open}
      className="dialog--cost"
      kicker={t("Server_Confirm_Kicker")}
      title={t("Server_Automation_Consent_Title")}
      description={t("Server_Automation_Consent_Body")}
      bodyLabel={t("Server_Automation_Consent_StartsLabel")}
      confirmLabel={
        busy ? t("Server_Automation_Save_Pending") : t("Server_Automation_Consent_Confirm")
      }
      cancelLabel={t("Server_Change_KeepEditing")}
      confirmVariant="primary"
      initialFocus="dismiss"
      busy={busy}
      error={errorKey === null ? undefined : t(errorKey)}
      onConfirm={onConfirm}
      onCancel={onDismiss}
    >
      <p>{t("Server_Automation_Consent_Skip")}</p>
      <dl className="cost-confirm__facts">
        <div className="cost-confirm__fact">
          <dt>{t("Hosting_Common_CostOwner")}</dt>
          <dd>{ownerText}</dd>
        </div>
        <div className="cost-confirm__fact">
          <dt>{t("Server_Workspace_Cost_Hourly")}</dt>
          <dd>{facts.hourly === null ? noEstimate : formatHourlyRate(facts.hourly)}</dd>
        </div>
        <div className="cost-confirm__fact">
          <dt>{t("Server_Workspace_Cost_Monthly")}</dt>
          <dd>{facts.monthly === null ? noEstimate : formatMoney(facts.monthly)}</dd>
        </div>
        <div className="cost-confirm__fact">
          <dt>{t("Server_Automation_Consent_StartsLabel")}</dt>
          <dd>{t("Server_Automation_Consent_Starts", { count: startRuleCount })}</dd>
        </div>
      </dl>
    </ConfirmDialog>
  );
}

/** Props of {@link ScheduledStartsPausedNotice}. */
export interface ScheduledStartsPausedNoticeProps {
  /** True when the viewer may confirm the scheduled starts again. */
  canManage: boolean;
  /** Called when the viewer chooses to confirm the scheduled starts again. */
  onReconfirm: () => void;
}

/**
 * The warning that scheduled starts are paused because the cost owner changed.
 *
 * A manager gets the explanation and the button that opens the consent dialog; everyone else gets the sentence that
 * the server owner has to confirm, and no button.
 *
 * @param props Whether the viewer may confirm, and the callback of the button.
 * @returns The warning notice.
 */
export function ScheduledStartsPausedNotice({
  canManage,
  onReconfirm,
}: ScheduledStartsPausedNoticeProps) {
  const { t } = useTranslation();
  return (
    <StatePanel
      kind="warning"
      title={t("Server_Automation_Consent_NeededTitle")}
      {...(canManage
        ? {
            action: (
              <ActionButton variant="primary" onClick={onReconfirm}>
                {t("Server_Automation_Consent_Reconfirm")}
              </ActionButton>
            ),
          }
        : {})}
    >
      {canManage
        ? t("Server_Automation_Consent_NeededBody")
        : t("Server_Automation_Consent_NeededBodyViewer")}
    </StatePanel>
  );
}
