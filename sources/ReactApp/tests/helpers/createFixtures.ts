import type {
  GameContentOption,
  RuntimeProfile,
  RuntimeProvider,
  ServerCapability,
  ServerCreationCostOwnerOption,
  StorageDemand,
} from "@/features/hosting/types";

const bytesPerGiB = 1_073_741_824;

/**
 * Builds a complete compute size as the API sends it. Every call returns a fresh object.
 *
 * @param identifier The size identifier, for example `medium`.
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns A fully populated size with 4 vCPU, 8 GiB memory, an 80 GiB disk of which 70 GiB is
 *   usable, 0.1 EUR per hour and 73 EUR per month by default.
 */
export function createProfile(
  identifier: string,
  overrides: Partial<RuntimeProfile> = {},
): RuntimeProfile {
  return {
    identifier,
    displayName: identifier.charAt(0).toUpperCase() + identifier.slice(1),
    description: "Test compute profile.",
    virtualCpuCount: 4,
    memoryGiB: 8,
    diskGiB: 80,
    includedStorageBytes: 80 * bytesPerGiB,
    usableStorageBytes: 70 * bytesPerGiB,
    estimatedHourlyCostEur: 0.1,
    estimatedMonthlyCostEur: 73,
    ...overrides,
  } as unknown as RuntimeProfile;
}

/**
 * Builds an available runtime provider. Every call returns a fresh object.
 *
 * @param identifier The provider identifier, for example `fake-cloud`.
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns An available provider with one `medium` size and external block storage between
 *   20 and 500 GiB in steps of 10 GiB at 0.001 EUR per GiB-hour.
 */
export function createProvider(
  identifier: string,
  overrides: Partial<RuntimeProvider> = {},
): RuntimeProvider {
  return {
    identifier,
    availabilityStatus: "available",
    profiles: [createProfile("medium")],
    storage: {
      supportsExternalBlockStorage: true,
      minimumCapacityGiB: 20,
      maximumCapacityGiB: 500,
      capacityIncrementGiB: 10,
      estimatedHourlyCostPerGiBEur: 0.001,
      deploymentMountPath: "/srv/utf",
      readinessFailure: null,
    },
    ...overrides,
  } as unknown as RuntimeProvider;
}

/**
 * Builds a game capability that needs neither a terrain nor a scenario and offers mod packs.
 * Every call returns a fresh object.
 *
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns An Arma-shaped capability served by one `fake-cloud` provider.
 */
export function createCapability(overrides: Partial<ServerCapability> = {}): ServerCapability {
  return {
    gameIdentifier: "arma3",
    displayName: "Arma 3",
    description: "Dedicated Arma operations.",
    runtimeProviders: ["fake-cloud"],
    actions: [],
    modProviderBindings: [],
    modPackFormats: [],
    requiresTerrain: false,
    terrainLabel: "Terrain",
    requiresScenario: false,
    scenarioLabel: "Mission",
    supportsModPacks: true,
    requiresModPack: true,
    defaultTerrainSourceIdentifier: null,
    defaultScenarioSourceIdentifier: null,
    runtimeProviderProfiles: [createProvider("fake-cloud")],
    ...overrides,
  } as unknown as ServerCapability;
}

/**
 * Builds a game capability that requires a terrain and a scenario but no mod pack. Every call
 * returns a fresh object.
 *
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns A Counter-Strike-shaped capability labelled `Map` and `Game mode` whose default
 *   source identifiers are `de_dust2` and `competitive`.
 */
export function counterStrikeCapability(
  overrides: Partial<ServerCapability> = {},
): ServerCapability {
  return createCapability({
    gameIdentifier: "cs2",
    displayName: "Counter-Strike 2",
    description: "Dedicated Counter-Strike operations.",
    requiresTerrain: true,
    terrainLabel: "Map",
    requiresScenario: true,
    scenarioLabel: "Game mode",
    supportsModPacks: true,
    requiresModPack: false,
    defaultTerrainSourceIdentifier: "de_dust2",
    defaultScenarioSourceIdentifier: "competitive",
    ...overrides,
  });
}

/**
 * Builds a storage estimate in which the `medium` size of `fake-cloud` is sufficient. Every call
 * returns a fresh object.
 *
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns An estimated demand with a 70 GiB minimum external capacity.
 */
export function createStorageDemand(overrides: Partial<StorageDemand> = {}): StorageDemand {
  return {
    steadyStateRequiredBytes: 40 * bytesPerGiB,
    preparationWorkspaceBytes: 10 * bytesPerGiB,
    safetyMarginPercent: 10,
    safetyMarginBytes: 5 * bytesPerGiB,
    peakPreparationRequiredBytes: 55 * bytesPerGiB,
    minimumExternalCapacityGiB: 70,
    recommendedExternalUsableBytes: 62 * bytesPerGiB,
    status: "Estimated",
    calculatedAt: "2026-08-13T12:00:00Z",
    measurementTimestamp: null,
    missingComponents: [],
    components: [],
    runtimeProfiles: [
      {
        runtimeProviderIdentifier: "fake-cloud",
        runtimeProfileIdentifier: "medium",
        displayName: "Medium",
        includedCapacityBytes: 80 * bytesPerGiB,
        usableCapacityBytes: 70 * bytesPerGiB,
        isSufficient: true,
        externalStorageRequired: false,
      },
    ],
    ...overrides,
  } as unknown as StorageDemand;
}

/** Options of {@link createOwners}. */
export interface CreateOwnersOptions {
  /** Whether the caller's own owner is frozen. Defaults to false. */
  frozenSelf?: boolean | undefined;
  /** Whether the caller's own owner is listed at all. Defaults to true. */
  withSelf?: boolean | undefined;
}

/**
 * Builds the cost-owner choices of a member. Every call returns fresh objects.
 *
 * @param options Whether the caller's own owner is frozen or missing.
 * @returns The caller's `Personal budget` (unless omitted) followed by a `Shared budget`.
 */
export function createOwners(options: CreateOwnersOptions = {}): ServerCreationCostOwnerOption[] {
  const personal: ServerCreationCostOwnerOption = {
    costOwnerId: "10000000-0000-4000-8000-000000000010",
    displayName: "Personal budget",
    isFrozen: options.frozenSelf === true,
    isCurrentUser: true,
    kind: "User",
  };
  const shared: ServerCreationCostOwnerOption = {
    costOwnerId: "10000000-0000-4000-8000-000000000011",
    displayName: "Shared budget",
    isFrozen: false,
    isCurrentUser: false,
    kind: "Verein",
  };
  return options.withSelf === false ? [shared] : [personal, shared];
}

/** One entry accepted by {@link createContentOptions}. */
export interface CreateContentItem {
  /** Primary key of the option: a numeric string for terrains and a GUID for scenarios. */
  primaryKey: string;
  /** Source identifier carried by the option's single version. */
  sourceIdentifier: string;
  /** Display title. Defaults to the source identifier. */
  title?: string | undefined;
}

/**
 * Builds terrain or scenario options with one version each. Every call returns fresh objects.
 *
 * @param items The options to build, in API order.
 * @param gameIdentifier The owning game. Defaults to `cs2`.
 * @returns One content option per item, each with a single version.
 */
export function createContentOptions(
  items: readonly CreateContentItem[],
  gameIdentifier = "cs2",
): GameContentOption[] {
  return items.map((item) => ({
    primaryKey: item.primaryKey,
    gameIdentifier,
    title: item.title ?? item.sourceIdentifier,
    description: null,
    versions: [
      {
        primaryKey: `${item.primaryKey}-version`,
        versionNumber: 1,
        versionLabel: "1",
        sourceKind: "Test",
        sourceIdentifier: item.sourceIdentifier,
      },
    ],
  }));
}
