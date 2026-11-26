import { Warning16Regular } from "@fluentui/react-icons";
import { useTranslation } from "react-i18next";
import { useDelayedDisconnect } from "@/components/server/useDelayedDisconnect";
import "@/components/server/workspace-shell.css";
import { formatAccruedAmount } from "@/features/hosting/accruedModel";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import type { GameServer } from "@/features/hosting/types";
import { useAccruedPresentation } from "@/features/hosting/useAccruedLive";

/**
 * Shows the accrued cost of a server session exactly as the control plane sent it, with the time it was true.
 *
 * The first value comes from REST and later values arrive as pushed frames written into the same query cache
 * entry. Nothing here calculates, extrapolates or polls. The lines follow the accrued state table: the value or a
 * state sentence, the time line, the billing line, and notes. A server without a session reads "No session yet", an amount the API
 * could not calculate reads "Not available for this session.", a tiny amount while cost runs reads "Less than",
 * and the amount is never replaced by a zero or a dash. The billing line says how the provider bills and, for an
 * hourly provider, when the next hour starts, from the values the API sent; it is not a live region. After the shared socket has been down for 3 seconds in a
 * row, a costing row also says that live updates are paused. The value is not a live region.
 *
 * @param props.server The server whose accrued cost is shown.
 */
export function AccruedCost({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const presentation = useAccruedPresentation(server);
  const paused = useDelayedDisconnect() && presentation.costing;
  const { value, time, billing, notes } = presentation;
  return (
    <>
      {value.kind === "amount" ? (
        <p className="accrued-cost__value">
          {formatAccruedAmount(value.amount, value.currency, value.costing, t)}
        </p>
      ) : (
        <p className="accrued-cost__sentence">{t(value.key)}</p>
      )}
      {time !== null && (
        <p className="accrued-cost__line">
          <time dateTime={time.instant}>{t(time.key, { time: formatInstant(time.instant) })}</time>
        </p>
      )}
      {billing !== null && (
        <p className="accrued-cost__line">
          {billing.instant === null ? (
            t(billing.key)
          ) : (
            <time dateTime={billing.instant}>
              {t(billing.key, { time: formatInstant(billing.instant) })}
            </time>
          )}
        </p>
      )}
      {notes.map((key) => (
        <p
          key={key}
          className={
            key === "Server_Workspace_Cost_AccruedMaybe"
              ? "accrued-cost__line accrued-cost__warning"
              : "accrued-cost__line"
          }
        >
          {t(key)}
        </p>
      ))}
      {paused && (
        <p className="accrued-cost__line accrued-cost__stale">
          <Warning16Regular aria-hidden="true" />
          {t("Server_Live_StaleReason_Disconnected")}
        </p>
      )}
    </>
  );
}
