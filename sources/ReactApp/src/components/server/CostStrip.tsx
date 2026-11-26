import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { ActionLink } from "@/components/ActionControl";
import { AccruedCost } from "@/components/server/AccruedCost";
import { CostIndicator } from "@/components/server/CostIndicator";
import { serverCostFacts } from "@/components/server/costConfirmationModel";
import "@/components/server/workspace-shell.css";
import { formatHourlyRate, formatMoney } from "@/features/hosting/model";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer } from "@/features/hosting/types";
import { useAccruedPresentation } from "@/features/hosting/useAccruedLive";

/** The most alert amounts the strip lists before it summarizes the rest as "and {{count}} more". */
const listedAlertAmounts = 3;

/**
 * Shows who pays for a server and what it costs, in five rows that are always rendered and a sixth for owners.
 * The cost owner row ends with a "Change cost owner" link to the Cost owner panel of Settings, rendered only for a
 * member who may modify a server that is not archived.
 *
 * The rows are the cost owner, the hourly rate, the monthly cost if the server is left running, the cost
 * indicator for the derived state and the accrued session cost. A sixth Cost alerts row renders only when the
 * server carries an alert summary (`costAlertThresholds` is an array), which the API sends only to viewers who
 * may see the cost owner; an absent summary omits the row and never invents a value. It reads "None set" for an
 * empty list, otherwise the first three amounts in the locale money format joined by `Intl.ListFormat` plus
 * "and {{count}} more" beyond three. A viewer who may manage the alerts gets a link to the cost alerts page of the
 * owner on its own line ("Set an alert" when none are set, otherwise "Manage alerts"). Owner and estimates come from
 * `serverCostFacts`, the rule the cost confirmation dialog uses, so the two can never disagree. A missing
 * estimate reads "No estimate", never an amount of zero. The accrued row wraps `AccruedCost` in the
 * `data-slot="accrued-cost"` element and shows the control plane value, never a client calculation; its term is
 * "This session so far" while cost may run and "Last session total" once the session cost has stopped. The
 * retired "Not available yet" placeholder is gone.
 *
 * @param props.server The server whose cost facts are shown.
 */
export function CostStrip({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const { owner, hourly, monthly } = serverCostFacts(server);
  const derived = deriveServerState(server);
  const accrued = useAccruedPresentation(server);
  const noEstimate = t("Server_Workspace_Cost_NoEstimate");
  let ownerText: ReactNode;
  if (owner.kind === "name") ownerText = <bdi>{owner.name ?? ""}</bdi>;
  else if (owner.kind === "hidden") ownerText = t("Server_Confirm_OwnerHidden");
  else ownerText = t("Server_Confirm_OwnerUnknown");
  // The Cost owner panel of Settings changes the owner; it exists for a member who may modify a non-archived server.
  const canChangeOwner = server.canModify && !server.isArchived;
  const alertAmounts = server.costAlertThresholds;
  let alertSummary: ReactNode = null;
  if (Array.isArray(alertAmounts)) {
    if (alertAmounts.length === 0) alertSummary = t("Server_Workspace_Cost_AlertsNone");
    else {
      const amounts = new Intl.ListFormat(currentLocale(), { type: "unit" }).format(
        alertAmounts.slice(0, listedAlertAmounts).map((amount) => formatMoney(amount)),
      );
      alertSummary =
        alertAmounts.length > listedAlertAmounts
          ? t("Server_Workspace_Cost_AlertsListMore", {
              amounts,
              count: alertAmounts.length - listedAlertAmounts,
            })
          : t("Server_Workspace_Cost_AlertsList", { amounts });
    }
  }
  return (
    <dl className="cost-strip">
      <div className="cost-strip__row">
        <dt>{t("Hosting_Common_CostOwner")}</dt>
        <dd>
          {canChangeOwner ? <p className="cost-strip__alerts">{ownerText}</p> : ownerText}
          {canChangeOwner && (
            <ActionLink
              variant="ghost"
              size="compact"
              to={{ search: "?tab=settings", hash: "#server-cost-owner" }}
            >
              {t("Server_Create_BilledTo_Change")}
            </ActionLink>
          )}
        </dd>
      </div>
      <div className="cost-strip__row">
        <dt>{t("Server_Workspace_Cost_Hourly")}</dt>
        <dd className="cost-strip__amount">
          {hourly === null ? noEstimate : formatHourlyRate(hourly)}
        </dd>
      </div>
      <div className="cost-strip__row">
        <dt>{t("Server_Workspace_Cost_Monthly")}</dt>
        <dd className="cost-strip__amount">
          {monthly === null ? noEstimate : formatMoney(monthly)}
        </dd>
      </div>
      <div className="cost-strip__row">
        <dt>{t("Server_Workspace_Cost_Status")}</dt>
        <dd>
          <CostIndicator derived={derived} />
        </dd>
      </div>
      <div className="cost-strip__row">
        <dt>{t(accrued.termKey)}</dt>
        <dd>
          <div className="accrued-cost" data-slot="accrued-cost">
            <AccruedCost server={server} />
          </div>
        </dd>
      </div>
      {Array.isArray(alertAmounts) && (
        <div className="cost-strip__row">
          <dt>{t("Server_Workspace_Cost_Alerts")}</dt>
          <dd>
            <p className="cost-strip__alerts">{alertSummary}</p>
            {server.canManageCostAlerts === true && (
              <ActionLink
                variant="ghost"
                size="compact"
                to={`/command/servers/cost-alerts?owner=${server.costOwnerId}`}
              >
                {alertAmounts.length === 0
                  ? t("Server_Workspace_Cost_AlertsSet")
                  : t("Server_Workspace_Cost_AlertsManage")}
              </ActionLink>
            )}
          </dd>
        </div>
      )}
    </dl>
  );
}
