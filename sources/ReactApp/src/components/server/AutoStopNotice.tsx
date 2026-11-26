import { useTranslation } from "react-i18next";
import { formatRelativeTime } from "@/app/format";
import { useNow } from "@/app/useNow";
import { ActionLink } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";
import "@/components/server/cost-alerts.css";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import type { GameServer, LifecycleJob } from "@/features/hosting/types";
import { useAutoStopNotice, useAutoStopReason } from "@/features/hosting/useAutoStopNotice";

/** Props of {@link AutoStopNotice}. */
export interface AutoStopNoticeProps {
  /** The server of the open workspace. */
  server: GameServer;
  /** The lifecycle jobs of the server, used to tell a cancelled stop from one that started. */
  jobs: readonly LifecycleJob[];
}

/**
 * Warns every viewer of a server workspace that an automatic shutdown is about to happen (AUT-04, D-09).
 *
 * The notice renders from `automation.status.pendingStop` only and disappears when the API clears it; it cannot be
 * dismissed. It is one warning status panel: the stop time as its title, why the stop was announced, what the stop
 * does and a relative line. The relative line is the only text that changes on a clock; it sits in an
 * `aria-live="off"` element, so only the first appearance of the panel is announced. Viewers who may change the
 * automation also get a link to the settings panel; nobody else sees a link. The stop instant is the one the API
 * recorded and is never calculated here.
 *
 * @param props See {@link AutoStopNoticeProps}.
 * @returns The warning panel, or null when no stop is pending.
 */
export function AutoStopNotice({ server, jobs }: AutoStopNoticeProps) {
  const { t } = useTranslation();
  const now = useNow(30_000);
  const reasonSentence = useAutoStopReason();
  const { pendingStop } = useAutoStopNotice(server, jobs);
  if (pendingStop === null) return null;
  const reason = reasonSentence(pendingStop.reason, pendingStop.limit);
  return (
    <StatePanel
      kind="warning"
      title={t("Server_Automation_Notice_Title", { time: formatInstant(pendingStop.stopsAt) })}
      action={
        server.canManageAutomation === true ? (
          <ActionLink
            variant="ghost"
            size="compact"
            to={{ search: "?tab=settings", hash: "#server-automation" }}
          >
            {t("Server_Automation_Notice_Change")}
          </ActionLink>
        ) : undefined
      }
    >
      <div className="auto-stop-notice">
        {reason !== null && <p>{reason}</p>}
        <p>{t("Server_Automation_Notice_Consequence")}</p>
        <p className="auto-stop-notice__relative">
          <span aria-live="off">
            {t("Server_Automation_Notice_Relative", {
              relative: formatRelativeTime(pendingStop.stopsAt, now),
            })}
          </span>
        </p>
      </div>
    </StatePanel>
  );
}
