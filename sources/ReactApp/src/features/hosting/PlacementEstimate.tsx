import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { formatMoney } from "./model";
import type { StorageDemand } from "./types";

export function PlacementEstimate({
  estimate,
  dirty = false,
  pending = false,
  unavailable = false,
}: {
  estimate: StorageDemand["placementEstimate"];
  dirty?: boolean;
  pending?: boolean;
  unavailable?: boolean;
}) {
  const { t } = useTranslation();
  return (
    <section aria-label={t("React_Placement_Estimate")} aria-live="polite">
      <h3>{t("React_Placement_Estimate")}</h3>
      {dirty ? (
        <p>{t("React_Placement_SaveEstimate")}</p>
      ) : pending ? (
        <p>{t("Hosting_Create_EstimateLoading")}</p>
      ) : unavailable || !estimate ? (
        <p>{t("React_Placement_Unavailable")}</p>
      ) : (
        <>
          <p>{formatResource(t("React_Placement_HostCount"), estimate.hostCount)}</p>
          <dl>
            <dt>{t("Hosting_Profile_HourlyEstimate")}</dt>
            <dd>{formatMoney(estimate.estimatedHourlyCostEur)}</dd>
            <dt>{t("Hosting_Profile_MonthlyEstimate")}</dt>
            <dd>
              {estimate.estimatedMonthlyCostEur == null
                ? t("React_Placement_Unavailable")
                : formatMoney(estimate.estimatedMonthlyCostEur)}
            </dd>
          </dl>
        </>
      )}
    </section>
  );
}
