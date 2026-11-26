import { useWatch, type UseFormReturn } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { ActionButton } from "@/components/ActionControl";
import { SelectField, TextField, type SelectOption } from "@/components/FormFields";
import { StatePanel } from "@/components/primitives";
import "@/components/server/create-options.css";
import { StorageDemandDetails } from "@/components/server/StorageDemandDetails";
import {
  estimateCreateCost,
  type CreateField,
  type ServerCreationFormValues,
} from "@/features/hosting/createServerModel";
import {
  formatBytes,
  formatHourlyRate,
  formatMoney,
  selectedStorageBytes,
} from "@/features/hosting/model";
import type { RuntimeProfile, RuntimeProvider, StorageDemand } from "@/features/hosting/types";

/** Props of {@link StorageOptions}. */
export interface StorageOptionsProps {
  /** The create form; the strategy and the capacity bind to it through `register`. */
  form: UseFormReturn<ServerCreationFormValues>;
  /** The selected provider, or undefined when none resolves. */
  provider: RuntimeProvider | undefined;
  /** The selected size, or undefined when none resolves. */
  profile: RuntimeProfile | undefined;
  /** The storage estimate, or undefined while none is available. */
  demand: StorageDemand | undefined;
  /** Whether the storage estimate is being fetched. */
  estimateFetching: boolean;
  /** Whether the storage estimate failed and no data is available. */
  estimateFailed: boolean;
  /** Whether the selected storage cannot hold the estimated demand. */
  misfit: boolean;
  /** Called when the member asks to fetch the failed estimate again. */
  onRetryEstimate(): void;
  /**
   * Called after the member changed a field, so the page can mark it as touched.
   *
   * @param field The changed form field.
   */
  onMemberChange(field: CreateField): void;
}

/**
 * Maps an empty entry to null and any other entry to a number.
 *
 * @param value The raw input value.
 * @returns The number, or null for an empty entry.
 */
function toNumberOrNull(value: unknown): number | null {
  return value === "" || value === null || value === undefined ? null : Number(value);
}

/**
 * Renders the storage strategy, the external capacity and the estimate facts of the create form.
 *
 * External strategies are only offered when the provider supports external block storage, the capacity field
 * exists only for the explicitly sized strategy, and a failed estimate shows fixed copy with a retry action; the
 * message of the error itself is never shown.
 */
export function StorageOptions({
  form,
  provider,
  profile,
  demand,
  estimateFetching,
  estimateFailed,
  misfit,
  onRetryEstimate,
  onMemberChange,
}: StorageOptionsProps) {
  const { t } = useTranslation();
  const [strategy, fixedExternalGiB] = useWatch({
    control: form.control,
    name: ["runtimeStorageStrategy", "externalStorageSizeGiB"],
  });
  const storage = provider?.storage;
  const strategyOptions: SelectOption[] = [
    { value: "IncludedDisk", label: t("Hosting_Create_StorageIncluded") },
  ];
  if (storage?.supportsExternalBlockStorage) {
    strategyOptions.push(
      { value: "AutomaticExternal", label: t("Hosting_Create_StorageAutomatic") },
      { value: "FixedExternal", label: t("Hosting_Create_StorageFixed") },
    );
  }

  const capacityBytes = selectedStorageBytes(
    strategy,
    profile,
    demand,
    storage,
    fixedExternalGiB ?? undefined,
  );
  const cost = estimateCreateCost({
    profile,
    strategy,
    capacityBytes,
    storage,
    estimateFetching,
  });
  const price =
    cost.kind === "calculating"
      ? t("Hosting_Create_Calculating")
      : cost.kind === "unavailable"
        ? t("Hosting_Create_PriceUnavailable")
        : cost.monthly === undefined
          ? formatHourlyRate(cost.hourly)
          : formatResource(
              t("Hosting_Create_CombinedPriceValue"),
              formatHourlyRate(cost.hourly),
              formatMoney(cost.monthly),
            );

  const changeStrategy = (value: string) => {
    form.setValue(
      "externalStorageSizeGiB",
      value === "FixedExternal" ? (storage?.minimumCapacityGiB ?? null) : null,
      { shouldDirty: true },
    );
    onMemberChange("runtimeStorageStrategy");
  };

  return (
    <div className="create-storage">
      <SelectField
        label={t("Server_Create_Storage_Label")}
        options={strategyOptions}
        {...form.register("runtimeStorageStrategy", {
          onChange: (event: { target: { value: string } }) => changeStrategy(event.target.value),
        })}
      />
      {strategy === "FixedExternal" && storage?.supportsExternalBlockStorage && (
        <TextField
          label={t("Hosting_Create_ExternalCapacity")}
          type="number"
          inputMode="numeric"
          required
          min={storage.minimumCapacityGiB}
          max={storage.maximumCapacityGiB}
          step={storage.capacityIncrementGiB}
          error={form.formState.errors.externalStorageSizeGiB?.message}
          {...form.register("externalStorageSizeGiB", {
            setValueAs: toNumberOrNull,
            onChange: () => onMemberChange("externalStorageSizeGiB"),
          })}
        />
      )}
      {profile && provider && (
        <dl className="create-storage-facts">
          <div>
            <dt>{t("Hosting_Create_IncludedProfileDisk")}</dt>
            <dd>
              {profile.diskGiB == null
                ? "—"
                : `${new Intl.NumberFormat(currentLocale(), { maximumFractionDigits: 0 }).format(profile.diskGiB)} GiB`}
            </dd>
          </div>
          <div>
            <dt>{t("Hosting_Create_EstimatedPeakRequirement")}</dt>
            <dd>
              {estimateFetching
                ? t("Hosting_Create_Calculating")
                : formatBytes(demand?.peakPreparationRequiredBytes)}
            </dd>
          </div>
          <div>
            <dt>
              {t(
                strategy === "IncludedDisk"
                  ? "Hosting_Create_SelectedDeploymentCapacity"
                  : "Hosting_Storage_SelectedRawCapacity",
              )}
            </dt>
            <dd>{formatBytes(capacityBytes)}</dd>
          </div>
          <div>
            <dt>{t("Hosting_Create_CombinedPrice")}</dt>
            <dd>{price}</dd>
          </div>
        </dl>
      )}
      {profile && provider && (
        <p className="create-storage__note">
          {strategy === "IncludedDisk"
            ? t("Hosting_Create_FilesystemIncluded")
            : formatResource(
                t("Hosting_Create_FilesystemExternal"),
                storage?.deploymentMountPath ?? "—",
              )}
        </p>
      )}
      {strategy !== "IncludedDisk" && (
        <StatePanel kind="warning">{t("Hosting_Create_ExternalStorageWarning")}</StatePanel>
      )}
      {estimateFetching && (
        <StatePanel kind="loading">{t("Hosting_Create_EstimateLoading")}</StatePanel>
      )}
      {estimateFailed && !estimateFetching && (
        <StatePanel
          kind="warning"
          action={
            <ActionButton variant="ghost" onClick={onRetryEstimate}>
              {t("Server_Create_Retry")}
            </ActionButton>
          }
        >
          {formatResource(
            t("Hosting_Create_EstimateUnavailable"),
            t("Hosting_Create_EstimateFallback"),
          )}
        </StatePanel>
      )}
      {misfit && !estimateFetching && (
        <StatePanel kind="warning">{t("Hosting_Create_InvalidStorage")}</StatePanel>
      )}
      {demand && (
        <StorageDemandDetails
          demand={demand}
          selectedProfileIdentifier={profile?.identifier ?? ""}
        />
      )}
    </div>
  );
}
