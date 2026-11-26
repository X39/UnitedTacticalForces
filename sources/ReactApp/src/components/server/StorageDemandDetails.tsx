import { useState } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { Disclosure } from "@/components/Disclosure";
import { StatePanel } from "@/components/primitives";
import "@/components/server/create-options.css";
import { formatBytes } from "@/features/hosting/model";
import type { StorageDemand } from "@/features/hosting/types";

/**
 * Formats a number for the current locale.
 *
 * @param value The number, or null or undefined when it is not known.
 * @param maximumFractionDigits The most fraction digits to show.
 * @returns The formatted number, or a dash when the value is not known.
 */
function formatNumber(value: number | null | undefined, maximumFractionDigits = 1): string {
  return value == null
    ? "—"
    : new Intl.NumberFormat(currentLocale(), { maximumFractionDigits }).format(value);
}

/** Props of {@link StorageDemandDetails}. */
export interface StorageDemandDetailsProps {
  /** The storage estimate whose breakdown is shown. */
  demand: StorageDemand;
  /** Identifier of the selected size; its capacity and fit are highlighted. */
  selectedProfileIdentifier: string;
}

/**
 * Shows the full storage demand breakdown behind a collapsed "Storage details" disclosure: the demand facts, the
 * confidence and latest measurement, missing components, the fit of the selected size and every component.
 */
export function StorageDemandDetails({
  demand,
  selectedProfileIdentifier,
}: StorageDemandDetailsProps) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const selectedFit = demand.runtimeProfiles.find(
    (candidate) =>
      candidate.runtimeProfileIdentifier.toLocaleLowerCase() ===
      selectedProfileIdentifier.toLocaleLowerCase(),
  );
  const status = String(demand.status).toLocaleLowerCase();
  const statusLabel =
    status === "measured"
      ? t("Hosting_Create_StorageStatusMeasured")
      : status === "estimated"
        ? t("Hosting_Create_StorageStatusEstimated")
        : status === "incomplete"
          ? t("Hosting_Create_StorageStatusIncomplete")
          : t("Hosting_Create_StorageStatusUnavailable");

  return (
    <Disclosure
      className="create-storage-details"
      title={t("Server_Create_StorageDetails")}
      open={open}
      onOpenChange={setOpen}
    >
      <div className="create-storage-details__body">
        <dl className="create-storage-facts">
          <div>
            <dt>{t("Hosting_Create_SteadyStateUsage")}</dt>
            <dd>{formatBytes(demand.steadyStateRequiredBytes)}</dd>
          </div>
          <div>
            <dt>{t("Hosting_Create_PreparationWorkspace")}</dt>
            <dd>{formatBytes(demand.preparationWorkspaceBytes)}</dd>
          </div>
          <div>
            <dt>
              {formatResource(
                t("Hosting_Create_SafetyMargin"),
                formatNumber(demand.safetyMarginPercent),
              )}
            </dt>
            <dd>{formatBytes(demand.safetyMarginBytes)}</dd>
          </div>
          <div>
            <dt>{t("Hosting_Create_RecommendedPeak")}</dt>
            <dd>{formatBytes(demand.peakPreparationRequiredBytes)}</dd>
          </div>
          {demand.minimumExternalCapacityGiB != null && (
            <>
              <div>
                <dt>{t("Hosting_Storage_RecommendedRawCapacity")}</dt>
                <dd>{formatNumber(demand.minimumExternalCapacityGiB, 0)} GiB</dd>
              </div>
              <div>
                <dt>{t("Hosting_Storage_RecommendedUsableCapacity")}</dt>
                <dd>{formatBytes(demand.recommendedExternalUsableBytes)}</dd>
              </div>
            </>
          )}
        </dl>
        <p className="create-storage-details__confidence">
          <strong>{t("Hosting_Create_Confidence")}</strong> {statusLabel}.
          {demand.measurementTimestamp && (
            <>
              {" "}
              {formatResource(
                t("Hosting_Create_LatestMeasurement"),
                new Intl.DateTimeFormat(currentLocale(), {
                  dateStyle: "medium",
                  timeStyle: "short",
                }).format(new Date(demand.measurementTimestamp)),
              )}
            </>
          )}
        </p>
        {demand.missingComponents.length > 0 && (
          <StatePanel kind="warning">
            {formatResource(
              t("Hosting_Create_MissingComponents"),
              demand.missingComponents.join(", "),
            )}
          </StatePanel>
        )}
        {selectedFit?.isSufficient === false && (
          <StatePanel kind="warning">{t("Hosting_Create_SelectedProfileInsufficient")}</StatePanel>
        )}
        {selectedFit?.isSufficient == null && (
          <StatePanel kind="warning">{t("Hosting_Create_SelectedProfileUnknown")}</StatePanel>
        )}
        <h3>{t("Hosting_Create_SelectedProfileCapacity")}</h3>
        {selectedFit ? (
          <div className="create-storage-fit">
            <strong>{selectedFit.displayName}</strong>
            <span>
              {t("Hosting_Create_IncludedCapacity")}:{" "}
              {formatBytes(selectedFit.includedCapacityBytes)}
            </span>
            <span>
              {t("Hosting_Create_UsableCapacity")}: {formatBytes(selectedFit.usableCapacityBytes)}
            </span>
            <strong>
              {selectedFit.isSufficient === true
                ? t("Hosting_Create_FitSufficient")
                : selectedFit.isSufficient === false
                  ? t("Hosting_Create_FitInsufficient")
                  : t("Hosting_Create_FitUnknown")}
            </strong>
          </div>
        ) : (
          <p className="create-storage-details__confidence">
            {t("Hosting_Create_ProfileCapacityUnavailable")}
          </p>
        )}
        <h3>{t("Hosting_Create_ComponentBreakdown")}</h3>
        <ul className="create-storage-components">
          {demand.components.map((component) => (
            <li key={component.identifier}>
              <span>
                <strong>{component.displayName}</strong> — {component.kind}
              </span>
              <span>
                {formatBytes(component.requiredBytes)} · {String(component.status)} ·{" "}
                {component.source}
              </span>
            </li>
          ))}
        </ul>
      </div>
    </Disclosure>
  );
}
