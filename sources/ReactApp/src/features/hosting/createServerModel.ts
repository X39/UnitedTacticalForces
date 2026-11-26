import { z } from "zod";
import { isStorageSelectionValid } from "@/features/hosting/model";
import type {
  ConfigurationProfileOption,
  GameContentOption,
  RuntimeProfile,
  RuntimeProvider,
  RuntimeStorageCapabilities,
  ServerCapability,
  ServerCreationCostOwnerOption,
  ServerModPackOption,
  StorageDemand,
} from "@/features/hosting/types";

export interface ServerCreationFormValues {
  title: string;
  gameIdentifier: string;
  runtimeProviderIdentifier: string;
  runtimeProfileIdentifier: string;
  runtimeStorageStrategy: "IncludedDisk" | "AutomaticExternal" | "FixedExternal";
  externalStorageSizeGiB: number | null;
  modPackDefinitionId: number | null;
  terrainId: number | null;
  scenarioId: string | null;
  configurationProfileId: string | null;
  costOwnerId: string | null;
  isLaunchDisabled: boolean;
}

export interface ServerCreationValidationMessages {
  titleRequired: string;
  titleTooLong: string;
  selectionRequired: string;
  invalidCapacity: string;
  invalidIdentifier: string;
}

/** Name of a field of the create form. */
export type CreateField = keyof ServerCreationFormValues;

/** Capability-driven facts the validation schema needs beyond the static field rules. */
export interface ServerCreationRequirements {
  /** Whether the selected game requires a terrain. */
  requiresTerrain: boolean;
  /** Message shown at the terrain field when it is missing. */
  terrainRequired: string;
  /** Whether the selected game requires a scenario. */
  requiresScenario: boolean;
  /** Message shown at the scenario field when it is missing. */
  scenarioRequired: string;
  /** Storage bounds of the selected provider, used to validate an explicit capacity. */
  storage: RuntimeStorageCapabilities | null | undefined;
}

/**
 * Builds the validation schema of the create form.
 *
 * @param messages Localized validation messages.
 * @param requirements Capability-driven requirements. When omitted only the static field rules apply.
 * @returns The zod object schema. A mod pack is never required here: the API accepts a server
 *   without one.
 */
export function createServerCreationSchema(
  messages: ServerCreationValidationMessages,
  requirements?: ServerCreationRequirements,
) {
  const requiredSelection = z.string().trim().min(1, messages.selectionRequired);
  const base = z.object({
    title: z.string().trim().min(1, messages.titleRequired).max(256, messages.titleTooLong),
    gameIdentifier: requiredSelection,
    runtimeProviderIdentifier: requiredSelection,
    runtimeProfileIdentifier: requiredSelection,
    runtimeStorageStrategy: z.enum(["IncludedDisk", "AutomaticExternal", "FixedExternal"]),
    externalStorageSizeGiB: z.number().int().positive(messages.invalidCapacity).nullable(),
    modPackDefinitionId: z.number().int().positive().nullable(),
    terrainId: z.number().int().positive().nullable(),
    scenarioId: z.uuid(messages.invalidIdentifier).nullable(),
    configurationProfileId: z.uuid(messages.invalidIdentifier).nullable(),
    costOwnerId: z.uuid(messages.invalidIdentifier).nullable(),
    isLaunchDisabled: z.boolean(),
  });
  if (!requirements) return base;
  // The API accepts a server without a mod pack, so requiresModPack is deliberately not enforced
  // here; enforcing it at create awaits the user's confirmation.
  return base.superRefine((value, ctx) => {
    if (requirements.requiresTerrain && value.terrainId === null) {
      ctx.addIssue({ code: "custom", path: ["terrainId"], message: requirements.terrainRequired });
    }
    if (requirements.requiresScenario && value.scenarioId === null) {
      ctx.addIssue({
        code: "custom",
        path: ["scenarioId"],
        message: requirements.scenarioRequired,
      });
    }
    if (
      value.runtimeStorageStrategy === "FixedExternal" &&
      !isExternalCapacityEntryValid(value.externalStorageSizeGiB, requirements.storage)
    ) {
      ctx.addIssue({
        code: "custom",
        path: ["externalStorageSizeGiB"],
        message: messages.invalidCapacity,
      });
    }
  });
}

export function isProviderAvailable(provider: RuntimeProvider): boolean {
  return (
    provider.availabilityStatus.trim().toLocaleLowerCase() === "available" &&
    provider.profiles.length > 0
  );
}

export function orderProviders(providers: RuntimeProvider[]): RuntimeProvider[] {
  return [...providers].sort((left, right) =>
    left.identifier.localeCompare(right.identifier, undefined, { sensitivity: "base" }),
  );
}

export function isCostOwnerSelectionValid(
  costOwnerId: string | null,
  owners: ServerCreationCostOwnerOption[] | undefined,
): boolean {
  if (!owners) return false;
  if (costOwnerId) {
    const selected = owners.find((owner) => owner.costOwnerId === costOwnerId);
    return selected !== undefined && !selected.isFrozen;
  }
  return owners.find((owner) => owner.isCurrentUser)?.isFrozen !== true;
}

export function combinedHourlyCost(
  profile: RuntimeProfile | undefined,
  selectedStorageCapacityBytes: number | undefined,
  strategy: ServerCreationFormValues["runtimeStorageStrategy"],
  storage: RuntimeStorageCapabilities | null | undefined,
): number | undefined {
  if (profile?.estimatedHourlyCostEur == null) return undefined;
  const storageCost =
    strategy === "IncludedDisk"
      ? 0
      : ((selectedStorageCapacityBytes ?? 0) / 1_073_741_824) *
        (storage?.estimatedHourlyCostPerGiBEur ?? 0);
  return profile.estimatedHourlyCostEur + storageCost;
}

/**
 * Resolves the cost owner that will pay for a new server: the selected owner when it is listed,
 * otherwise the caller's own owner.
 *
 * @param costOwnerId The selected owner id, or null when the caller's own owner applies.
 * @param owners The loaded owner choices, or undefined while they are not available.
 * @returns The effective owner, or undefined when no owner can be determined.
 */
export function effectiveCostOwner(
  costOwnerId: string | null,
  owners: ServerCreationCostOwnerOption[] | undefined,
): ServerCreationCostOwnerOption | undefined {
  if (!owners) return undefined;
  if (costOwnerId) {
    const selected = owners.find(
      (owner) => owner.costOwnerId.toLocaleLowerCase() === costOwnerId.toLocaleLowerCase(),
    );
    if (selected) return selected;
  }
  return owners.find((owner) => owner.isCurrentUser);
}

/** The reasons that can block the create action, in the order they are reported. */
export type CreateBlockReason =
  | "owner-frozen"
  | "owner-loading"
  | "owner-failed"
  | "options-loading"
  | "options-failed"
  | "no-provider"
  | "estimate-running"
  | "estimate-failed"
  | "storage-misfit";

/** Priority order of {@link CreateBlockReason}; the first applicable reason is the one shown. */
export const createBlockReasonOrder: readonly CreateBlockReason[] = [
  "owner-frozen",
  "owner-loading",
  "owner-failed",
  "options-loading",
  "options-failed",
  "no-provider",
  "estimate-running",
  "estimate-failed",
  "storage-misfit",
];

/**
 * Facts the readiness rule needs. A frozen owner is the only cannot-pay signal; there is
 * deliberately no balance or affordability input because the API owns that decision.
 */
export interface CreateReadinessInput {
  /** The selected cost owner id, or null for the caller's own owner. */
  costOwnerId: string | null;
  /** The loaded owner choices, or undefined while they are not available. */
  owners: ServerCreationCostOwnerOption[] | undefined;
  /** Whether the owner query errored and has no data. */
  ownersFailed: boolean;
  /** Whether a capability-required option list (terrain, scenario) has no data yet. */
  requiredOptionsLoading: boolean;
  /** Whether a capability-required option list errored and has no data. */
  requiredOptionsFailed: boolean;
  /** The selected provider, if it resolves. */
  provider: RuntimeProvider | undefined;
  /** The selected size, if it resolves. */
  profile: RuntimeProfile | undefined;
  /** Whether the storage estimate is currently being fetched. */
  estimateFetching: boolean;
  /** Whether the storage estimate errored and has no data. */
  estimateFailed: boolean;
  /** Whether the selected storage cannot hold the estimated demand (see {@link isStorageMisfit}). */
  storageMisfit: boolean;
}

/**
 * Returns the first reason that blocks creating the server, in {@link createBlockReasonOrder},
 * or null when the member may create it.
 *
 * @param input The current readiness facts.
 * @returns The blocking reason, or null when nothing blocks.
 */
export function deriveCreateReadiness(input: CreateReadinessInput): CreateBlockReason | null {
  if (input.owners) {
    if (!isCostOwnerSelectionValid(input.costOwnerId, input.owners)) return "owner-frozen";
  } else {
    return input.ownersFailed ? "owner-failed" : "owner-loading";
  }
  if (input.requiredOptionsLoading) return "options-loading";
  if (input.requiredOptionsFailed) return "options-failed";
  if (!input.provider || !input.profile) return "no-provider";
  if (input.estimateFetching) return "estimate-running";
  if (input.estimateFailed) return "estimate-failed";
  if (input.storageMisfit) return "storage-misfit";
  return null;
}

/** Hours per month used to derive a monthly figure for external storage strategies. */
export const hoursPerMonth = 730;

/** The cost shown to the member: still calculating, not known, or a real figure. */
export type CreateCostEstimate =
  | { kind: "calculating" }
  | { kind: "unavailable" }
  | { kind: "known"; hourly: number; monthly: number | undefined };

/**
 * Computes the estimate shown in the cost summary without ever inventing an amount.
 *
 * @param input The selected size, storage strategy and capacity, and the estimate fetch state.
 * @returns Calculating while the storage estimate fetches, unavailable when the price or the
 *   external capacity is unknown, otherwise the hourly figure and the monthly figure when known.
 */
export function estimateCreateCost(input: {
  profile: RuntimeProfile | undefined;
  strategy: ServerCreationFormValues["runtimeStorageStrategy"];
  capacityBytes: number | undefined;
  storage: RuntimeStorageCapabilities | null | undefined;
  estimateFetching: boolean;
}): CreateCostEstimate {
  if (input.estimateFetching) return { kind: "calculating" };
  if (input.strategy !== "IncludedDisk" && input.capacityBytes === undefined) {
    return { kind: "unavailable" };
  }
  const hourly = combinedHourlyCost(
    input.profile,
    input.capacityBytes,
    input.strategy,
    input.storage,
  );
  if (hourly === undefined) return { kind: "unavailable" };
  if (input.strategy === "IncludedDisk") {
    return {
      kind: "known",
      hourly,
      monthly: input.profile?.estimatedMonthlyCostEur ?? undefined,
    };
  }
  return { kind: "known", hourly, monthly: hourly * hoursPerMonth };
}

/**
 * Checks an explicitly entered external capacity against the provider bounds.
 *
 * @param gib The entered capacity in GiB.
 * @param storage The storage capabilities of the selected provider.
 * @returns True when the provider supports external block storage and the entry is a safe
 *   integer within the provider minimum and maximum that is a multiple of the increment.
 */
export function isExternalCapacityEntryValid(
  gib: number | null | undefined,
  storage: RuntimeStorageCapabilities | null | undefined,
): boolean {
  if (gib === null || gib === undefined || !storage?.supportsExternalBlockStorage) return false;
  return (
    Number.isSafeInteger(gib) &&
    gib >= storage.minimumCapacityGiB &&
    gib <= storage.maximumCapacityGiB &&
    storage.capacityIncrementGiB > 0 &&
    gib % storage.capacityIncrementGiB === 0
  );
}

/**
 * Tells whether the selected storage cannot hold the estimated demand. An invalid explicit
 * capacity entry is field validation and therefore never counts as a misfit.
 *
 * @param input The storage selection and the loaded demand.
 * @returns True when a demand is loaded and the storage selection fails it.
 */
export function isStorageMisfit(input: {
  strategy: ServerCreationFormValues["runtimeStorageStrategy"];
  profile: RuntimeProfile | undefined;
  demand: StorageDemand | undefined;
  storage: RuntimeStorageCapabilities | null | undefined;
  fixedExternalGiB: number | null | undefined;
}): boolean {
  if (!input.demand) return false;
  if (
    input.strategy === "FixedExternal" &&
    !isExternalCapacityEntryValid(input.fixedExternalGiB, input.storage)
  ) {
    return false;
  }
  return !isStorageSelectionValid(
    input.strategy,
    input.profile,
    input.demand,
    input.storage,
    input.fixedExternalGiB ?? undefined,
  );
}

/** Inputs of {@link resolveCreateDefaults}. */
export interface CreateDefaultsInput {
  /** All game capabilities, in API order. */
  capabilities: readonly ServerCapability[];
  /** The current form values. */
  current: ServerCreationFormValues;
  /** The fields the member has changed by hand. */
  touched: ReadonlySet<CreateField>;
  /** Terrain options of the current game, or undefined while they are not loaded. */
  terrains: readonly GameContentOption[] | undefined;
  /** Scenario options of the current game, or undefined while they are not loaded. */
  scenarios: readonly GameContentOption[] | undefined;
  /** Mod pack options of the current game, or undefined while they are not loaded. */
  modPacks: readonly ServerModPackOption[] | undefined;
  /** Configuration profile options of the current game, or undefined while they are not loaded. */
  profiles: readonly ConfigurationProfileOption[] | undefined;
  /** The storage estimate, or undefined while it is not available. */
  storageDemand: StorageDemand | undefined;
  /** The cost owner choices, or undefined while they are not loaded. */
  owners: readonly ServerCreationCostOwnerOption[] | undefined;
}

/**
 * Picks the default size of a provider: the cheapest priced size (ties keep API order), or the
 * first size when none has a price. For the included disk with a loaded demand, sizes whose fit
 * is known to be insufficient are skipped; external strategies ignore the fit.
 *
 * @param provider The selected provider.
 * @param strategy The selected storage strategy.
 * @param demand The storage estimate, if loaded.
 * @returns The size identifier, or an empty string when the provider has no size.
 */
export function defaultSizeIdentifier(
  provider: RuntimeProvider | undefined,
  strategy: ServerCreationFormValues["runtimeStorageStrategy"],
  demand: StorageDemand | undefined,
): string {
  const all = provider?.profiles ?? [];
  const fitting =
    strategy === "IncludedDisk" && demand
      ? all.filter(
          (candidate) =>
            demand.runtimeProfiles.find(
              (fit) =>
                fit.runtimeProfileIdentifier.toLocaleLowerCase() ===
                candidate.identifier.toLocaleLowerCase(),
            )?.isSufficient !== false,
        )
      : all;
  const candidates = fitting.length > 0 ? fitting : all;
  let cheapest: RuntimeProfile | undefined;
  for (const candidate of candidates) {
    if (candidate.estimatedHourlyCostEur == null) continue;
    if (
      cheapest?.estimatedHourlyCostEur == null ||
      candidate.estimatedHourlyCostEur < cheapest.estimatedHourlyCostEur
    ) {
      cheapest = candidate;
    }
  }
  return (cheapest ?? candidates[0])?.identifier ?? "";
}

/**
 * Picks the content option whose version carries the default source identifier, else the first.
 *
 * @param options The loaded options.
 * @param defaultSourceIdentifier The capability's default source identifier, if any.
 * @returns The chosen option, or undefined for an empty list.
 */
function pickDefaultContent(
  options: readonly GameContentOption[],
  defaultSourceIdentifier: string | null | undefined,
): GameContentOption | undefined {
  if (defaultSourceIdentifier) {
    const wanted = defaultSourceIdentifier.toLocaleLowerCase();
    const match = options.find((option) =>
      option.versions.some((version) => version.sourceIdentifier.toLocaleLowerCase() === wanted),
    );
    if (match) return match;
  }
  return options[0];
}

/**
 * Resolves the defaults of every create field without overwriting a still valid choice of the
 * member. Name, game and size are the only fields the member must decide; everything else gets
 * a default that the member may change.
 *
 * @param input The capabilities, the current values, the touched fields and the loaded options.
 * @returns A new set of form values; the input values are not mutated.
 */
export function resolveCreateDefaults(input: CreateDefaultsInput): ServerCreationFormValues {
  const { current, touched } = input;
  const result: ServerCreationFormValues = { ...current };
  const currentCapability = input.capabilities.find(
    (item) =>
      item.gameIdentifier.toLocaleLowerCase() === current.gameIdentifier.toLocaleLowerCase(),
  );
  const capability = currentCapability ?? input.capabilities[0];
  if (!capability) return result;
  if (!currentCapability) result.gameIdentifier = capability.gameIdentifier;

  const providers = orderProviders(capability.runtimeProviderProfiles.filter(isProviderAvailable));
  const currentProvider = providers.find(
    (item) =>
      item.identifier.toLocaleLowerCase() === current.runtimeProviderIdentifier.toLocaleLowerCase(),
  );
  const provider = currentProvider ?? providers[0];
  if (!currentProvider) result.runtimeProviderIdentifier = provider?.identifier ?? "";

  const defaultStrategy: ServerCreationFormValues["runtimeStorageStrategy"] = provider?.storage
    ?.supportsExternalBlockStorage
    ? "AutomaticExternal"
    : "IncludedDisk";
  const strategyServable =
    current.runtimeStorageStrategy === "IncludedDisk" ||
    provider?.storage?.supportsExternalBlockStorage === true;
  const strategy =
    touched.has("runtimeStorageStrategy") && strategyServable
      ? current.runtimeStorageStrategy
      : defaultStrategy;
  result.runtimeStorageStrategy = strategy;

  const touchedSizeStillExists =
    touched.has("runtimeProfileIdentifier") &&
    provider?.profiles.some(
      (item) =>
        item.identifier.toLocaleLowerCase() ===
        current.runtimeProfileIdentifier.toLocaleLowerCase(),
    ) === true;
  if (!touchedSizeStillExists) {
    result.runtimeProfileIdentifier = defaultSizeIdentifier(
      provider,
      strategy,
      input.storageDemand,
    );
  }

  result.externalStorageSizeGiB =
    strategy === "FixedExternal" ? current.externalStorageSizeGiB : null;

  if (capability.requiresTerrain !== true) {
    result.terrainId = null;
  } else if (input.terrains) {
    const exists =
      current.terrainId !== null &&
      input.terrains.some((option) => Number(option.primaryKey) === current.terrainId);
    if (!exists) {
      const picked = pickDefaultContent(input.terrains, capability.defaultTerrainSourceIdentifier);
      const id = picked === undefined ? Number.NaN : Number(picked.primaryKey);
      result.terrainId = Number.isSafeInteger(id) ? id : null;
    }
  }

  if (capability.requiresScenario !== true) {
    result.scenarioId = null;
  } else if (input.scenarios) {
    const exists =
      current.scenarioId !== null &&
      input.scenarios.some(
        (option) =>
          option.primaryKey.toLocaleLowerCase() === current.scenarioId?.toLocaleLowerCase(),
      );
    if (!exists) {
      result.scenarioId =
        pickDefaultContent(input.scenarios, capability.defaultScenarioSourceIdentifier)
          ?.primaryKey ?? null;
    }
  }

  if (capability.supportsModPacks === false) {
    result.modPackDefinitionId = null;
  } else if (
    input.modPacks &&
    current.modPackDefinitionId !== null &&
    !input.modPacks.some((option) => option.primaryKey === current.modPackDefinitionId)
  ) {
    result.modPackDefinitionId = null;
  }

  if (
    input.profiles &&
    current.configurationProfileId !== null &&
    !input.profiles.some(
      (option) =>
        option.primaryKey.toLocaleLowerCase() ===
        current.configurationProfileId?.toLocaleLowerCase(),
    )
  ) {
    result.configurationProfileId = null;
  }

  if (
    input.owners &&
    current.costOwnerId !== null &&
    !input.owners.some(
      (owner) => owner.costOwnerId.toLocaleLowerCase() === current.costOwnerId?.toLocaleLowerCase(),
    )
  ) {
    result.costOwnerId = null;
  }

  return result;
}

/** The create fields in the order they appear on the page. */
export const createFieldOrder: readonly CreateField[] = [
  "title",
  "gameIdentifier",
  "runtimeProfileIdentifier",
  "runtimeProviderIdentifier",
  "runtimeStorageStrategy",
  "externalStorageSizeGiB",
  "modPackDefinitionId",
  "terrainId",
  "scenarioId",
  "configurationProfileId",
  "costOwnerId",
  "isLaunchDisabled",
];

/** The create fields that live inside the Advanced disclosure. */
export const advancedCreateFields: ReadonlySet<CreateField> = new Set<CreateField>([
  "runtimeProviderIdentifier",
  "runtimeStorageStrategy",
  "externalStorageSizeGiB",
  "modPackDefinitionId",
  "terrainId",
  "scenarioId",
  "configurationProfileId",
  "costOwnerId",
  "isLaunchDisabled",
]);

/**
 * Lists the fields that have a validation error in the order they appear on the page.
 *
 * @param errors The form errors keyed by field name.
 * @returns The invalid fields in {@link createFieldOrder}.
 */
export function invalidCreateFields(errors: Partial<Record<CreateField, unknown>>): CreateField[] {
  return createFieldOrder.filter((field) => errors[field] !== undefined);
}

/** Select value of the built-in preset that applies the game's own defaults. */
export const defaultPresetValue = "default";

/** Select value shown when the form fields match no preset. It never selects anything. */
export const customPresetValue = "custom";

/**
 * A preset the member can pick in the create form. Presets are a client-side projection of the
 * configuration profile and mod pack options; there is no preset entity on the API.
 */
export type CreatePreset =
  | { kind: "default"; value: typeof defaultPresetValue }
  | { kind: "profile"; value: string; profileId: string; title: string; revision: number }
  | { kind: "modPack"; value: string; modPackId: number; title: string };

/**
 * Projects the loaded configuration profiles and mod packs into the preset list: the built-in
 * game defaults first, then every non-archived profile, then every mod pack, each in API order.
 *
 * @param profiles The configuration profile options, or undefined while they are not loaded.
 * @param modPacks The mod pack options, or undefined while they are not loaded.
 * @returns The presets, always starting with the built-in default.
 */
export function projectCreatePresets(
  profiles: readonly ConfigurationProfileOption[] | undefined,
  modPacks: readonly ServerModPackOption[] | undefined,
): CreatePreset[] {
  const presets: CreatePreset[] = [{ kind: "default", value: defaultPresetValue }];
  for (const profile of profiles ?? []) {
    if (profile.isArchived) continue;
    presets.push({
      kind: "profile",
      value: `profile:${profile.primaryKey}`,
      profileId: profile.primaryKey,
      title: profile.title,
      revision: profile.latestRevisionNumber,
    });
  }
  for (const modPack of modPacks ?? []) {
    presets.push({
      kind: "modPack",
      value: `modPack:${modPack.primaryKey}`,
      modPackId: modPack.primaryKey,
      title: modPack.title,
    });
  }
  return presets;
}

/**
 * Derives the preset select value from the two form fields it controls.
 *
 * @param values The configuration profile and mod pack fields of the form.
 * @param presets The projected presets.
 * @returns The default value when both fields are empty, the matching preset value when exactly
 *   one listed field is set, otherwise the custom value.
 */
export function presetSelectValue(
  values: Pick<ServerCreationFormValues, "configurationProfileId" | "modPackDefinitionId">,
  presets: readonly CreatePreset[],
): string {
  const { configurationProfileId, modPackDefinitionId } = values;
  if (configurationProfileId === null && modPackDefinitionId === null) return defaultPresetValue;
  if (configurationProfileId !== null && modPackDefinitionId === null) {
    const wanted = configurationProfileId.toLocaleLowerCase();
    const match = presets.find(
      (preset) => preset.kind === "profile" && preset.profileId.toLocaleLowerCase() === wanted,
    );
    return match?.value ?? customPresetValue;
  }
  if (configurationProfileId === null && modPackDefinitionId !== null) {
    const match = presets.find(
      (preset) => preset.kind === "modPack" && preset.modPackId === modPackDefinitionId,
    );
    return match?.value ?? customPresetValue;
  }
  return customPresetValue;
}

/**
 * Maps a preset select value to the form fields it sets. The value is untrusted DOM input and
 * is only compared with the projected presets; it is never parsed or used as an object key.
 *
 * @param value The select value.
 * @param presets The projected presets.
 * @returns The configuration profile and mod pack fields to set, or undefined for the custom
 *   value and any unknown value.
 */
export function presetSelection(
  value: string,
  presets: readonly CreatePreset[],
): Pick<ServerCreationFormValues, "configurationProfileId" | "modPackDefinitionId"> | undefined {
  const preset = presets.find((candidate) => candidate.value === value);
  if (!preset) return undefined;
  switch (preset.kind) {
    case "default":
      return { configurationProfileId: null, modPackDefinitionId: null };
    case "profile":
      return { configurationProfileId: preset.profileId, modPackDefinitionId: null };
    case "modPack":
      return { configurationProfileId: null, modPackDefinitionId: preset.modPackId };
  }
}
