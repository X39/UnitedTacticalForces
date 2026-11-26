import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import "@/components/server/configuration.css";
import { formatMoney } from "./model";
import type { StorageDemand } from "./types";

/**
 * The estimate of the next deployment placement.
 *
 * The estimate always describes the saved settings. While changes are staged it stays visible and a muted line says
 * that it does not include them, so a member never reads a stale figure as the result of the staged changes.
 *
 * @param props.estimate The estimate from the storage demand, or null when none is available.
 * @param props.stagedNote Whether changes are staged and the saved-settings note is shown with the estimate.
 * @param props.pending Whether the estimate is being refreshed; the old figure is hidden meanwhile.
 * @param props.unavailable Whether the estimate could not be loaded.
 */
export function PlacementEstimate({
  estimate,
  stagedNote = false,
  pending = false,
  unavailable = false,
}: {
  estimate: StorageDemand["placementEstimate"];
  stagedNote?: boolean;
  pending?: boolean;
  unavailable?: boolean;
}) {
  const { t } = useTranslation();
  return (
    <section aria-label={t("React_Placement_Estimate")} aria-live="polite">
      <h3>{t("React_Placement_Estimate")}</h3>
      {pending ? (
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
          {stagedNote && (
            <p className="configuration-panel__estimate-note">
              {t("Server_Settings_EstimateSaved")}
            </p>
          )}
        </>
      )}
    </section>
  );
}
