import { useTranslation } from "react-i18next";
import type { UseFormReturn } from "react-hook-form";
import { currentLocale } from "@/app/format";
import { SelectField } from "@/components/FormFields";
import { StatePanel } from "@/components/primitives";
import "@/components/server/create-fields.css";
import type { CreateField, ServerCreationFormValues } from "@/features/hosting/createServerModel";
import { formatHourlyRate } from "@/features/hosting/model";
import type { RuntimeProfile, StorageDemand } from "@/features/hosting/types";

/** Props of {@link SizeField}. */
export interface SizeFieldProps {
  /** The create form; the select registers on its `runtimeProfileIdentifier` field. */
  form: UseFormReturn<ServerCreationFormValues>;
  /** The sizes the selected provider offers, in API order. */
  profiles: readonly RuntimeProfile[];
  /** The currently selected size, or undefined when none matches. */
  selected: RuntimeProfile | undefined;
  /** The chosen storage strategy; the fit line only applies to the included disk. */
  strategy: ServerCreationFormValues["runtimeStorageStrategy"];
  /** The storage estimate for the current content, or undefined while none is loaded. */
  demand: StorageDemand | undefined;
  /**
   * Called when the member changes the size.
   *
   * @param field The form field the member changed.
   */
  onMemberChange(field: CreateField): void;
}

/**
 * Formats a number for display in the current locale, or an em dash when the value is unknown.
 *
 * @param value The number to format.
 * @param maximumFractionDigits The most fraction digits to show.
 * @returns The formatted number or an em dash.
 */
function formatNumber(value: number | null | undefined, maximumFractionDigits: number): string {
  if (value === null || value === undefined) return "—";
  return new Intl.NumberFormat(currentLocale(), { maximumFractionDigits }).format(value);
}

/**
 * Returns the hourly price of a size only when it is a real, positive amount. An unknown price is never shown
 * as zero.
 *
 * @param profile The size.
 * @returns The hourly price in EUR, or undefined when the size has no price.
 */
function hourlyPrice(profile: RuntimeProfile): number | undefined {
  const price = profile.estimatedHourlyCostEur;
  return typeof price === "number" && Number.isFinite(price) && price > 0 ? price : undefined;
}

/**
 * The Size decision of the create form (D-03).
 *
 * A native select lists every size with its vCPU, memory and hourly price, or "price not available" when the size
 * has none. The selected size's facts follow. Only for the included disk strategy and a loaded storage estimate, a
 * fit line states whether the size holds the selected game files and mods; external storage strategies do not
 * depend on the included disk, so no line shows for them.
 */
export function SizeField({
  form,
  profiles,
  selected,
  strategy,
  demand,
  onMemberChange,
}: SizeFieldProps) {
  const { t } = useTranslation();
  const options = profiles.map((profile) => {
    const price = hourlyPrice(profile);
    const cpu = formatNumber(profile.virtualCpuCount, 0);
    const memory = formatNumber(profile.memoryGiB, 1);
    return {
      value: profile.identifier,
      label:
        price === undefined
          ? t("Server_Create_Size_OptionNoPrice", { name: profile.displayName, cpu, memory })
          : t("Server_Create_Size_Option", {
              name: profile.displayName,
              cpu,
              memory,
              price: formatHourlyRate(price),
            }),
    };
  });
  const selectedPrice = selected === undefined ? undefined : hourlyPrice(selected);
  const showFit = strategy === "IncludedDisk" && demand !== undefined && selected !== undefined;
  const fit = showFit
    ? demand.runtimeProfiles?.find(
        (item) =>
          item.runtimeProfileIdentifier?.toLocaleLowerCase() ===
          selected.identifier.toLocaleLowerCase(),
      )?.isSufficient
    : undefined;

  return (
    <div className="create-field">
      <SelectField
        label={t("Server_Create_Size_Label")}
        hint={t("Server_Create_Size_Hint")}
        required
        options={options}
        {...form.register("runtimeProfileIdentifier", {
          onChange: () => onMemberChange("runtimeProfileIdentifier"),
        })}
      />
      {selected !== undefined && (
        <dl className="create-size__facts">
          <div className="create-size__fact">
            <dt>{t("Hosting_Profile_VirtualCpu")}</dt>
            <dd>{formatNumber(selected.virtualCpuCount, 0)}</dd>
          </div>
          <div className="create-size__fact">
            <dt>{t("Hosting_Profile_Memory")}</dt>
            <dd>
              {selected.memoryGiB == null ? "—" : `${formatNumber(selected.memoryGiB, 1)} GiB`}
            </dd>
          </div>
          <div className="create-size__fact">
            <dt>{t("Hosting_Profile_Disk")}</dt>
            <dd>{selected.diskGiB == null ? "—" : `${formatNumber(selected.diskGiB, 0)} GiB`}</dd>
          </div>
          <div className="create-size__fact">
            <dt>{t("Hosting_Profile_HourlyEstimate")}</dt>
            <dd>
              {selectedPrice === undefined
                ? t("Hosting_Create_PriceUnavailable")
                : formatHourlyRate(selectedPrice)}
            </dd>
          </div>
        </dl>
      )}
      {showFit &&
        (fit === false ? (
          <StatePanel kind="warning">{t("Server_Create_Fit_Insufficient")}</StatePanel>
        ) : (
          <p className="create-field__note">
            {fit === true ? t("Server_Create_Fit_Sufficient") : t("Server_Create_Fit_Unknown")}
          </p>
        ))}
    </div>
  );
}
