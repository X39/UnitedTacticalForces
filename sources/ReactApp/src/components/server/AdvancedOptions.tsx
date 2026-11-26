import type { TFunction } from "i18next";
import { useState, type ReactNode } from "react";
import { useWatch, type UseFormReturn } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionButton } from "@/components/ActionControl";
import { Disclosure } from "@/components/Disclosure";
import { CheckboxField, SelectField, type SelectOption } from "@/components/FormFields";
import { StatePanel } from "@/components/primitives";
import { ApiText } from "@/components/server/ApiText";
import "@/components/server/create-options.css";
import type { CreateField, ServerCreationFormValues } from "@/features/hosting/createServerModel";
import type {
  ConfigurationProfileOption,
  GameContentOption,
  RuntimeProvider,
  ServerCapability,
  ServerCreationCostOwnerOption,
  ServerModPackOption,
} from "@/features/hosting/types";

/** A loadable option list: the data once it arrived, whether it is loading and whether loading failed. */
export interface OptionListState<T> {
  /** The loaded options, or undefined while none are available. */
  data: readonly T[] | undefined;
  /** Whether the list is being loaded for the first time. */
  loading: boolean;
  /** Whether loading failed and no data is available. */
  failed: boolean;
}

/** Props of {@link AdvancedOptions}. */
export interface AdvancedOptionsProps {
  /** The create form; fields bind to it through `register` so `setFocus` reaches them. */
  form: UseFormReturn<ServerCreationFormValues>;
  /** Whether the disclosure is open. */
  open: boolean;
  /**
   * Called when the member opens or closes the disclosure.
   *
   * @param open Whether the disclosure is now open.
   */
  onOpenChange(open: boolean): void;
  /** The capability of the selected game; it alone decides which fields exist. */
  capability: ServerCapability;
  /** Providers that can be selected for the game. */
  availableProviders: readonly RuntimeProvider[];
  /** Providers of the game that cannot be selected right now. */
  unavailableProviders: readonly RuntimeProvider[];
  /** The composed storage options element, rendered after the provider. */
  storage: ReactNode;
  /** Mod pack options of the game. */
  modPacks: OptionListState<ServerModPackOption>;
  /** Terrain options of the game. */
  terrains: OptionListState<GameContentOption>;
  /** Scenario options of the game. */
  scenarios: OptionListState<GameContentOption>;
  /** Configuration profile options of the game. */
  profiles: OptionListState<ConfigurationProfileOption>;
  /** Cost owner choices, or undefined while they are not loaded. */
  owners: readonly ServerCreationCostOwnerOption[] | undefined;
  /** Called when the member asks to load the failed option lists again. */
  onRetryOptions(): void;
  /**
   * Called after the member changed a field, so the page can mark it as touched.
   *
   * @param field The changed form field.
   */
  onMemberChange(field: CreateField): void;
  /**
   * Called when the member chose another provider.
   *
   * @param identifier The identifier of the chosen provider.
   */
  onProviderChange(identifier: string): void;
  /**
   * Called when the member toggled the launch-disabled option.
   *
   * @param disabled Whether launching is now disabled.
   */
  onLaunchDisabledChange(disabled: boolean): void;
}

/**
 * Maps a provider availability status to the copy that explains why the provider cannot be used.
 *
 * @param status The availability status reported by the API.
 * @returns The translation key of the description; unknown statuses read as an empty catalog.
 */
function providerStatusKey(
  status: string,
):
  | "Hosting_ProfileCatalog_ConfigurationRequiredDescription"
  | "Hosting_ProfileCatalog_UnavailableDescription"
  | "Hosting_ProfileCatalog_EmptyDescription" {
  switch (status.trim().toLocaleLowerCase()) {
    case "configuration-required":
      return "Hosting_ProfileCatalog_ConfigurationRequiredDescription";
    case "catalog-unavailable":
      return "Hosting_ProfileCatalog_UnavailableDescription";
    default:
      return "Hosting_ProfileCatalog_EmptyDescription";
  }
}

/**
 * Maps an empty select entry to null and any other entry to a number.
 *
 * @param value The raw select value.
 * @returns The numeric id, or null for the empty entry.
 */
function toNumberOrNull(value: unknown): number | null {
  return value === "" || value === null || value === undefined ? null : Number(value);
}

/**
 * Maps an empty select entry to null and keeps any other entry as text.
 *
 * @param value The raw select value.
 * @returns The text value, or null for the empty entry.
 */
function toTextOrNull(value: unknown): string | null {
  return value === "" || value === null || value === undefined ? null : String(value);
}

/**
 * Builds a React key that changes when an option list arrives or changes size. A native select that is
 * registered with the form is uncontrolled; remounting it makes the form write its value into the DOM once the
 * matching option exists.
 *
 * @param list The option list.
 * @returns A key that is stable while the list does not change.
 */
function listKey(list: OptionListState<unknown>): string {
  return `${list.loading ? "loading" : "ready"}:${list.data?.length ?? "none"}`;
}

/** Inputs of {@link buildSummary}. */
interface SummaryInput {
  /** Translation function. */
  t: TFunction;
  /** The current form values that the summary reflects. */
  values: Pick<
    ServerCreationFormValues,
    | "runtimeProviderIdentifier"
    | "runtimeStorageStrategy"
    | "modPackDefinitionId"
    | "terrainId"
    | "scenarioId"
    | "configurationProfileId"
  >;
  /** The capability that decides which pieces exist. */
  capability: ServerCapability;
  /** Mod pack options. */
  modPacks: readonly ServerModPackOption[] | undefined;
  /** Terrain options. */
  terrains: readonly GameContentOption[] | undefined;
  /** Scenario options. */
  scenarios: readonly GameContentOption[] | undefined;
  /** Configuration profile options. */
  profiles: readonly ConfigurationProfileOption[] | undefined;
}

/**
 * Builds the always visible summary line of the closed disclosure from the effective values.
 *
 * @param input The values, the capability and the loaded option lists.
 * @returns The pieces joined with a middle dot.
 */
function buildSummary({
  t,
  values,
  capability,
  modPacks,
  terrains,
  scenarios,
  profiles,
}: SummaryInput): string {
  const pieces: string[] = [];
  if (values.runtimeProviderIdentifier) {
    pieces.push(
      t("Server_Create_Summary_Provider", { provider: values.runtimeProviderIdentifier }),
    );
  }
  const strategyKey: TranslationKey =
    values.runtimeStorageStrategy === "AutomaticExternal"
      ? "Hosting_Create_StorageAutomatic"
      : values.runtimeStorageStrategy === "FixedExternal"
        ? "Hosting_Create_StorageFixed"
        : "Hosting_Create_StorageIncluded";
  pieces.push(t("Server_Create_Summary_Storage", { storage: t(strategyKey) }));
  if (capability.supportsModPacks !== false) {
    const modPack = modPacks?.find((item) => item.primaryKey === values.modPackDefinitionId);
    pieces.push(
      t("Server_Create_Summary_ModPack", {
        title: modPack?.title ?? t("Hosting_Create_ModPackNone"),
      }),
    );
  }
  const profile = profiles?.find(
    (item) =>
      item.primaryKey.toLocaleLowerCase() === values.configurationProfileId?.toLocaleLowerCase(),
  );
  pieces.push(
    t("Server_Create_Summary_Configuration", {
      title: profile?.title ?? t("Server_Create_Preset_Default"),
    }),
  );
  if (capability.requiresTerrain && values.terrainId !== null) {
    const terrain = terrains?.find((item) => Number(item.primaryKey) === values.terrainId);
    if (terrain) {
      pieces.push(
        t("Server_Create_Summary_Content", {
          label: capability.terrainLabel ?? "",
          title: terrain.title,
        }),
      );
    }
  }
  if (capability.requiresScenario && values.scenarioId !== null) {
    const scenario = scenarios?.find(
      (item) => item.primaryKey.toLocaleLowerCase() === values.scenarioId?.toLocaleLowerCase(),
    );
    if (scenario) {
      pieces.push(
        t("Server_Create_Summary_Content", {
          label: capability.scenarioLabel ?? "",
          title: scenario.title,
        }),
      );
    }
  }
  return pieces.join(" · ");
}

/**
 * Renders the advanced create options in a collapsed disclosure that keeps a summary line visible.
 *
 * Every field is generated from the capability flags and the option lists the page passes in; the component
 * never compares a game identifier to a literal, so a game with other requirements renders other fields with
 * the same code. Fields bind with `form.register`, so `form.setFocus` can reach them.
 */
export function AdvancedOptions({
  form,
  open,
  onOpenChange,
  capability,
  availableProviders,
  unavailableProviders,
  storage,
  modPacks,
  terrains,
  scenarios,
  profiles,
  owners,
  onRetryOptions,
  onMemberChange,
  onProviderChange,
  onLaunchDisabledChange,
}: AdvancedOptionsProps) {
  const { t } = useTranslation();
  const [aboutOpen, setAboutOpen] = useState(false);
  const [
    runtimeProviderIdentifier,
    runtimeStorageStrategy,
    modPackDefinitionId,
    terrainId,
    scenarioId,
    configurationProfileId,
  ] = useWatch({
    control: form.control,
    name: [
      "runtimeProviderIdentifier",
      "runtimeStorageStrategy",
      "modPackDefinitionId",
      "terrainId",
      "scenarioId",
      "configurationProfileId",
    ],
  });
  const errors = form.formState.errors;
  const supportsModPacks = capability.supportsModPacks !== false;
  const terrainLabel = capability.terrainLabel ?? "Terrain";
  const scenarioLabel = capability.scenarioLabel ?? "Scenario";

  const summary = buildSummary({
    t,
    values: {
      runtimeProviderIdentifier,
      runtimeStorageStrategy,
      modPackDefinitionId,
      terrainId,
      scenarioId,
      configurationProfileId,
    },
    capability,
    modPacks: modPacks.data,
    terrains: terrains.data,
    scenarios: scenarios.data,
    profiles: profiles.data,
  });

  // The retry panel is shown once, at the first failed list in field order, instead of once per list.
  const failedLists: Array<"modPack" | "terrain" | "scenario" | "profile"> = [];
  if (supportsModPacks && modPacks.failed) failedLists.push("modPack");
  if (capability.requiresTerrain && terrains.failed) failedLists.push("terrain");
  if (capability.requiresScenario && scenarios.failed) failedLists.push("scenario");
  if (profiles.failed) failedLists.push("profile");
  const retryPanel = (list: "modPack" | "terrain" | "scenario" | "profile") =>
    failedLists[0] === list ? (
      <StatePanel
        kind="warning"
        action={
          <ActionButton variant="ghost" onClick={onRetryOptions}>
            {t("Server_Create_Retry")}
          </ActionButton>
        }
      >
        {t("Server_Create_Options_LoadFailed")}
      </StatePanel>
    ) : null;

  const contentOptions = (
    list: OptionListState<GameContentOption>,
    label: string,
  ): SelectOption[] => [
    { value: "", label: t("Server_Create_Content_Choose", { label }) },
    ...(list.data ?? []).map((item) => ({ value: item.primaryKey, label: item.title })),
  ];

  const selfOwner = owners?.find((owner) => owner.isCurrentUser);
  const selfLabel = `${
    selfOwner
      ? formatResource(t("Hosting_Create_CostOwnerSelfExisting"), selfOwner.displayName)
      : t("Hosting_Create_CostOwnerSelf")
  }${selfOwner?.isFrozen ? ` — ${t("Hosting_Create_CostOwnerFrozen")}` : ""}`;
  const ownerOptions: SelectOption[] = [
    { value: "", label: selfLabel },
    ...(owners ?? [])
      .filter((owner) => !owner.isCurrentUser)
      .map((owner) => ({
        value: owner.costOwnerId,
        label: owner.isFrozen
          ? `${owner.displayName} — ${t("Hosting_Create_CostOwnerFrozen")}`
          : owner.displayName,
        disabled: owner.isFrozen === true,
      })),
  ];

  const actions = [...capability.actions].sort((left, right) => left.order - right.order);
  const hasAbout = Boolean(capability.description) || actions.length > 0;

  return (
    <Disclosure
      className="create-advanced"
      title={t("Server_Create_Advanced_Title")}
      summary={<span className="create-advanced__summary">{summary}</span>}
      open={open}
      onOpenChange={onOpenChange}
    >
      <div className="create-advanced__content">
        {availableProviders.length > 1 && (
          <SelectField
            label={t("Hosting_Create_Runtime")}
            options={availableProviders.map((item) => ({
              value: item.identifier,
              label: item.identifier,
            }))}
            {...form.register("runtimeProviderIdentifier", {
              onChange: (event: { target: { value: string } }) =>
                onProviderChange(event.target.value),
            })}
          />
        )}
        {availableProviders.length === 1 && availableProviders[0] && (
          <p className="create-advanced__provider">
            {t("Server_Create_Summary_Provider", { provider: availableProviders[0].identifier })}
          </p>
        )}
        {unavailableProviders.length > 0 && (
          <section className="create-advanced__unavailable">
            <h3>{t("Server_Create_Providers_Unavailable")}</h3>
            <ul>
              {unavailableProviders.map((item) => (
                <li key={item.identifier}>
                  <strong>{item.identifier}</strong>{" "}
                  <span>
                    {item.availabilityStatus === "paused"
                      ? t("Server_Reason_Paused")
                      : formatResource(
                          t(providerStatusKey(item.availabilityStatus)),
                          item.identifier,
                        )}
                  </span>
                </li>
              ))}
            </ul>
          </section>
        )}
        {storage}
        {supportsModPacks && (
          <>
            {/* A mod pack is never required at create, even when requiresModPack is true: orchestrator
                decision OQ1, an assumption the user still has to confirm. */}
            <SelectField
              key={listKey(modPacks)}
              label={t("Server_Create_ModPack_Label")}
              hint={t("Hosting_Create_ModPackHint")}
              disabled={modPacks.loading || modPacks.failed}
              options={[
                { value: "", label: t("Hosting_Create_ModPackNone") },
                ...(modPacks.data ?? []).map((item) => ({
                  value: String(item.primaryKey),
                  label: item.title,
                })),
              ]}
              {...form.register("modPackDefinitionId", {
                setValueAs: toNumberOrNull,
                onChange: () => onMemberChange("modPackDefinitionId"),
              })}
            />
            {retryPanel("modPack")}
          </>
        )}
        {capability.requiresTerrain && (
          <>
            <SelectField
              key={listKey(terrains)}
              label={<ApiText>{terrainLabel}</ApiText>}
              required
              disabled={terrains.loading || terrains.failed}
              error={errors.terrainId?.message}
              options={contentOptions(terrains, terrainLabel)}
              {...form.register("terrainId", {
                setValueAs: toNumberOrNull,
                onChange: () => onMemberChange("terrainId"),
              })}
            />
            {retryPanel("terrain")}
          </>
        )}
        {capability.requiresScenario && (
          <>
            <SelectField
              key={listKey(scenarios)}
              label={<ApiText>{scenarioLabel}</ApiText>}
              required
              disabled={scenarios.loading || scenarios.failed}
              error={errors.scenarioId?.message}
              options={contentOptions(scenarios, scenarioLabel)}
              {...form.register("scenarioId", {
                setValueAs: toTextOrNull,
                onChange: () => onMemberChange("scenarioId"),
              })}
            />
            {retryPanel("scenario")}
          </>
        )}
        <SelectField
          key={listKey(profiles)}
          label={t("Server_Create_Profile_Label")}
          hint={t("Hosting_Create_ConfigurationProfileHint")}
          disabled={profiles.loading || profiles.failed}
          options={[
            { value: "", label: t("Server_Create_Preset_Default") },
            ...(profiles.data ?? []).map((item) => ({
              value: item.primaryKey,
              label: t("Server_Create_Preset_ProfileOption", {
                title: item.title,
                revision: item.latestRevisionNumber,
              }),
            })),
          ]}
          {...form.register("configurationProfileId", {
            setValueAs: toTextOrNull,
            onChange: () => onMemberChange("configurationProfileId"),
          })}
        />
        {retryPanel("profile")}
        <SelectField
          key={owners ? `owners:${owners.length}` : "owners:none"}
          label={t("Hosting_Common_CostOwner")}
          hint={t("Hosting_Create_CostOwnerHint")}
          disabled={owners === undefined}
          options={ownerOptions}
          {...form.register("costOwnerId", {
            setValueAs: toTextOrNull,
            onChange: () => onMemberChange("costOwnerId"),
          })}
        />
        <CheckboxField
          label={t("Hosting_Create_LaunchDisabled")}
          hint={t("Hosting_Create_LaunchDisabledHint")}
          {...form.register("isLaunchDisabled", {
            onChange: (event: { target: { checked: boolean } }) => {
              onMemberChange("isLaunchDisabled");
              onLaunchDisabledChange(event.target.checked);
            },
          })}
        />
        {hasAbout && (
          <Disclosure
            className="create-advanced__about"
            title={t("Server_Create_About_Title")}
            open={aboutOpen}
            onOpenChange={setAboutOpen}
          >
            {capability.description && (
              <p>
                <ApiText>{capability.description}</ApiText>
              </p>
            )}
            {actions.length > 0 && (
              <ul className="create-advanced__actions">
                {actions.map((action) => (
                  <li key={action.id}>
                    <ApiText>{action.label}</ApiText>
                  </li>
                ))}
              </ul>
            )}
          </Disclosure>
        )}
      </div>
    </Disclosure>
  );
}
