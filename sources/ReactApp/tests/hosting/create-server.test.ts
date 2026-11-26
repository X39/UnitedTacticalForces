import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import {
  combinedHourlyCost,
  createBlockReasonOrder,
  advancedCreateFields,
  createFieldOrder,
  createServerCreationSchema,
  defaultSizeIdentifier,
  deriveCreateReadiness,
  effectiveCostOwner,
  estimateCreateCost,
  hoursPerMonth,
  invalidCreateFields,
  isCostOwnerSelectionValid,
  isExternalCapacityEntryValid,
  isProviderAvailable,
  isStorageMisfit,
  orderProviders,
  resolveCreateDefaults,
  type CreateBlockReason,
  type CreateDefaultsInput,
  type CreateField,
  type CreateReadinessInput,
  type ServerCreationFormValues,
} from "@/features/hosting/createServerModel";
import { hostingService } from "@/features/hosting/services";
import type {
  RuntimeProfile,
  RuntimeProvider,
  RuntimeStorageCapabilities,
  ServerCreation,
} from "@/features/hosting/types";
import {
  counterStrikeCapability,
  createCapability,
  createContentOptions,
  createOwners,
  createProfile,
  createProvider,
  createStorageDemand,
} from "../helpers/createFixtures";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const messages = {
  titleRequired: "name-required",
  titleTooLong: "name-too-long",
  selectionRequired: "selection-required",
  invalidCapacity: "capacity-invalid",
  invalidIdentifier: "identifier-invalid",
};

describe("server creation model", () => {
  it("trims the title and validates identifiers before any request", () => {
    const schema = createServerCreationSchema(messages);
    const parsed = schema.parse({
      title: "  UTF One  ",
      gameIdentifier: "arma3",
      runtimeProviderIdentifier: "hetzner",
      runtimeProfileIdentifier: "cx32",
      runtimeStorageStrategy: "AutomaticExternal",
      externalStorageSizeGiB: null,
      modPackDefinitionId: 42,
      terrainId: null,
      scenarioId: null,
      configurationProfileId: "10000000-0000-4000-8000-000000000001",
      costOwnerId: "10000000-0000-4000-8000-000000000002",
      isLaunchDisabled: false,
    });

    expect(parsed.title).toBe("UTF One");
    expect(
      schema.safeParse({
        ...parsed,
        title: " ",
        costOwnerId: "not-a-guid",
      }).success,
    ).toBe(false);
  });

  it("excludes unavailable providers, sorts choices, and combines storage cost", () => {
    const unavailable = {
      identifier: "zulu",
      availabilityStatus: "configuration-required",
      profiles: [{}],
    } as RuntimeProvider;
    const available = {
      identifier: "alpha",
      availabilityStatus: "AVAILABLE",
      profiles: [{ estimatedHourlyCostEur: 0.1 }],
    } as RuntimeProvider;
    const storage = { estimatedHourlyCostPerGiBEur: 0.001 } as RuntimeStorageCapabilities;

    expect(isProviderAvailable(unavailable)).toBe(false);
    expect(isProviderAvailable(available)).toBe(true);
    expect(orderProviders([unavailable, available]).map((provider) => provider.identifier)).toEqual(
      ["alpha", "zulu"],
    );
    expect(
      combinedHourlyCost(
        available.profiles[0] as RuntimeProfile,
        50 * 1_073_741_824,
        "FixedExternal",
        storage,
      ),
    ).toBeCloseTo(0.15);
  });

  it("accepts only assignable, non-frozen cost owners", () => {
    const personal = {
      costOwnerId: "10000000-0000-4000-8000-000000000001",
      displayName: "Personal",
      isFrozen: false,
      isCurrentUser: true,
      kind: "User" as const,
    };
    const frozen = {
      costOwnerId: "10000000-0000-4000-8000-000000000002",
      displayName: "Frozen",
      isFrozen: true,
      isCurrentUser: false,
      kind: "Verein" as const,
    };

    expect(isCostOwnerSelectionValid(null, [personal, frozen])).toBe(true);
    expect(isCostOwnerSelectionValid(frozen.costOwnerId, [personal, frozen])).toBe(false);
    expect(isCostOwnerSelectionValid("10000000-0000-4000-8000-000000000099", [personal])).toBe(
      false,
    );
    expect(isCostOwnerSelectionValid(null, undefined)).toBe(false);
  });
});

describe("create readiness", () => {
  const provider = createProvider("fake-cloud");
  const profile = createProfile("medium");

  /** A fully ready input: every blocking condition is clear. */
  function readyInput(overrides: Partial<CreateReadinessInput> = {}): CreateReadinessInput {
    return {
      costOwnerId: null,
      owners: createOwners(),
      ownersFailed: false,
      requiredOptionsLoading: false,
      requiredOptionsFailed: false,
      provider,
      profile,
      estimateFetching: false,
      estimateFailed: false,
      storageMisfit: false,
      ...overrides,
    };
  }

  /** An input in which every blocking condition holds at once. */
  function blockedInput(): CreateReadinessInput {
    return readyInput({
      owners: createOwners({ frozenSelf: true }),
      requiredOptionsLoading: true,
      requiredOptionsFailed: true,
      provider: undefined,
      estimateFetching: true,
      estimateFailed: true,
      storageMisfit: true,
    });
  }

  it("returns null when nothing blocks creation", () => {
    expect(deriveCreateReadiness(readyInput())).toBeNull();
  });

  it("walks the reasons in the documented order as conditions clear", () => {
    expect(createBlockReasonOrder).toEqual([
      "owner-frozen",
      "owner-loading",
      "owner-failed",
      "options-loading",
      "options-failed",
      "no-provider",
      "estimate-running",
      "estimate-failed",
      "storage-misfit",
    ]);

    const clearing: Partial<CreateReadinessInput>[] = [
      { owners: createOwners() },
      { requiredOptionsLoading: false },
      { requiredOptionsFailed: false },
      { provider },
      { estimateFetching: false },
      { estimateFailed: false },
      { storageMisfit: false },
    ];
    const expected: (CreateBlockReason | null)[] = [
      "owner-frozen",
      "options-loading",
      "options-failed",
      "no-provider",
      "estimate-running",
      "estimate-failed",
      "storage-misfit",
      null,
    ];
    let input = blockedInput();
    expect(deriveCreateReadiness(input)).toBe(expected[0]);
    clearing.forEach((clear, index) => {
      input = { ...input, ...clear };
      expect(deriveCreateReadiness(input)).toBe(expected[index + 1]);
    });
  });

  it("blocks a frozen caller-owned or selected owner and reports owner loading states", () => {
    const owners = createOwners({ frozenSelf: true });
    const shared = owners.find((owner) => !owner.isCurrentUser);
    expect(deriveCreateReadiness(readyInput({ owners }))).toBe("owner-frozen");
    expect(
      deriveCreateReadiness(
        readyInput({
          owners: createOwners().map((owner) => ({ ...owner, isFrozen: !owner.isCurrentUser })),
          costOwnerId: shared?.costOwnerId ?? null,
        }),
      ),
    ).toBe("owner-frozen");
    expect(deriveCreateReadiness(readyInput({ owners: undefined }))).toBe("owner-loading");
    expect(deriveCreateReadiness(readyInput({ owners: undefined, ownersFailed: true }))).toBe(
      "owner-failed",
    );
  });

  it("reports a missing provider or size and a failed estimate before a storage misfit", () => {
    expect(deriveCreateReadiness(readyInput({ provider: undefined }))).toBe("no-provider");
    expect(deriveCreateReadiness(readyInput({ profile: undefined }))).toBe("no-provider");
    expect(deriveCreateReadiness(readyInput({ estimateFailed: true, storageMisfit: true }))).toBe(
      "estimate-failed",
    );
  });

  it("resolves the effective cost owner by id, then the caller, then nothing", () => {
    const owners = createOwners();
    const [personal, shared] = owners;
    expect(
      effectiveCostOwner(shared?.costOwnerId.toLocaleUpperCase() ?? null, owners)?.displayName,
    ).toBe("Shared budget");
    expect(effectiveCostOwner(null, owners)).toBe(personal);
    expect(effectiveCostOwner("10000000-0000-4000-8000-000000000099", owners)).toBe(personal);
    expect(effectiveCostOwner(null, createOwners({ withSelf: false }))).toBeUndefined();
    expect(effectiveCostOwner(null, undefined)).toBeUndefined();
  });
});

describe("create cost estimate", () => {
  const storage = createProvider("fake-cloud").storage;
  const gib = 1_073_741_824;

  it("reports calculating while the storage estimate fetches", () => {
    expect(
      estimateCreateCost({
        profile: undefined,
        strategy: "IncludedDisk",
        capacityBytes: undefined,
        storage,
        estimateFetching: true,
      }),
    ).toEqual({ kind: "calculating" });
  });

  it("never turns an unknown price into a number", () => {
    const unpriced = createProfile("medium", {
      estimatedHourlyCostEur: null,
      estimatedMonthlyCostEur: null,
    } as unknown as Partial<RuntimeProfile>);
    expect(
      estimateCreateCost({
        profile: unpriced,
        strategy: "IncludedDisk",
        capacityBytes: 70 * gib,
        storage,
        estimateFetching: false,
      }),
    ).toEqual({ kind: "unavailable" });
    expect(
      estimateCreateCost({
        profile: undefined,
        strategy: "IncludedDisk",
        capacityBytes: undefined,
        storage,
        estimateFetching: false,
      }),
    ).toEqual({ kind: "unavailable" });
  });

  it("uses the monthly price of the size for the included disk and omits it when unknown", () => {
    const priced = createProfile("medium", {
      estimatedHourlyCostEur: 0.42,
      estimatedMonthlyCostEur: 306.6,
    });
    expect(
      estimateCreateCost({
        profile: priced,
        strategy: "IncludedDisk",
        capacityBytes: 70 * gib,
        storage,
        estimateFetching: false,
      }),
    ).toEqual({ kind: "known", hourly: 0.42, monthly: 306.6 });
    const withoutMonthly = createProfile("medium", {
      estimatedHourlyCostEur: 0.42,
      estimatedMonthlyCostEur: null,
    } as unknown as Partial<RuntimeProfile>);
    expect(
      estimateCreateCost({
        profile: withoutMonthly,
        strategy: "IncludedDisk",
        capacityBytes: 70 * gib,
        storage,
        estimateFetching: false,
      }),
    ).toEqual({ kind: "known", hourly: 0.42, monthly: undefined });
  });

  it("derives the monthly figure of external storage from the hourly one", () => {
    const result = estimateCreateCost({
      profile: createProfile("medium", { estimatedHourlyCostEur: 0.1 }),
      strategy: "AutomaticExternal",
      capacityBytes: 100 * gib,
      storage,
      estimateFetching: false,
    });
    expect(result.kind).toBe("known");
    if (result.kind !== "known") return;
    expect(result.hourly).toBeCloseTo(0.2);
    expect(result.monthly).toBeCloseTo(0.2 * 730);
    expect(hoursPerMonth).toBe(730);
  });

  it("is unavailable for an external strategy without a capacity", () => {
    expect(
      estimateCreateCost({
        profile: createProfile("medium"),
        strategy: "FixedExternal",
        capacityBytes: undefined,
        storage,
        estimateFetching: false,
      }),
    ).toEqual({ kind: "unavailable" });
  });
});

describe("create defaults", () => {
  const uuid = (suffix: number) => `20000000-0000-4000-8000-${String(suffix).padStart(12, "0")}`;

  /** Form values before the member or the resolver decided anything. */
  function emptyValues(
    overrides: Partial<ServerCreationFormValues> = {},
  ): ServerCreationFormValues {
    return {
      title: "",
      gameIdentifier: "",
      runtimeProviderIdentifier: "",
      runtimeProfileIdentifier: "",
      runtimeStorageStrategy: "IncludedDisk",
      externalStorageSizeGiB: null,
      modPackDefinitionId: null,
      terrainId: null,
      scenarioId: null,
      configurationProfileId: null,
      costOwnerId: null,
      isLaunchDisabled: false,
      ...overrides,
    };
  }

  function defaultsInput(overrides: Partial<CreateDefaultsInput> = {}): CreateDefaultsInput {
    return {
      capabilities: [createCapability()],
      current: emptyValues(),
      touched: new Set<CreateField>(),
      terrains: undefined,
      scenarios: undefined,
      modPacks: undefined,
      profiles: undefined,
      storageDemand: undefined,
      owners: undefined,
      ...overrides,
    };
  }

  function fit(identifier: string, isSufficient: boolean | null) {
    return {
      runtimeProviderIdentifier: "fake-cloud",
      runtimeProfileIdentifier: identifier,
      displayName: identifier,
      includedCapacityBytes: null,
      usableCapacityBytes: null,
      isSufficient,
      externalStorageRequired: false,
    } as unknown as ReturnType<typeof createStorageDemand>["runtimeProfiles"][number];
  }

  const sizedProvider = createProvider("fake-cloud", {
    profiles: [
      createProfile("cheap", { estimatedHourlyCostEur: 0.05 }),
      createProfile("mid", { estimatedHourlyCostEur: 0.1 }),
      createProfile("big", { estimatedHourlyCostEur: 0.2 }),
    ],
  });
  const sizedCapability = () => createCapability({ runtimeProviderProfiles: [sizedProvider] });
  const noExternalStorage = () =>
    ({
      ...createProvider("fake-cloud").storage,
      supportsExternalBlockStorage: false,
    }) as NonNullable<ReturnType<typeof createProvider>["storage"]>;

  it("picks the first game, the first available provider and the strategy the provider serves", () => {
    const first = createCapability({
      gameIdentifier: "alpha-game",
      runtimeProviderProfiles: [
        createProvider("zulu"),
        createProvider("Bravo"),
        createProvider("aaa-down", { availabilityStatus: "configuration-required" }),
        createProvider("aab-empty", { profiles: [] }),
      ],
    });
    const resolved = resolveCreateDefaults(
      defaultsInput({ capabilities: [first, counterStrikeCapability()] }),
    );
    expect(resolved.gameIdentifier).toBe("alpha-game");
    expect(resolved.runtimeProviderIdentifier).toBe("Bravo");
    expect(resolved.runtimeStorageStrategy).toBe("AutomaticExternal");
    expect(resolved.externalStorageSizeGiB).toBeNull();
    expect(resolved.runtimeProfileIdentifier).toBe("medium");

    const internalOnly = createCapability({
      runtimeProviderProfiles: [createProvider("only", { storage: noExternalStorage() })],
    });
    expect(
      resolveCreateDefaults(defaultsInput({ capabilities: [internalOnly] })).runtimeStorageStrategy,
    ).toBe("IncludedDisk");
  });

  it("picks the cheapest size, falling back to the first and honouring the fit only for the included disk", () => {
    expect(defaultSizeIdentifier(sizedProvider, "AutomaticExternal", undefined)).toBe("cheap");
    const tied = createProvider("p", {
      profiles: [
        createProfile("a", { estimatedHourlyCostEur: 0.3 }),
        createProfile("b", { estimatedHourlyCostEur: 0.1 }),
        createProfile("c", { estimatedHourlyCostEur: 0.1 }),
      ],
    });
    expect(defaultSizeIdentifier(tied, "IncludedDisk", undefined)).toBe("b");
    const unpriced = createProvider("p", {
      profiles: [
        createProfile("x", { estimatedHourlyCostEur: null } as never),
        createProfile("y", { estimatedHourlyCostEur: null } as never),
      ],
    });
    expect(defaultSizeIdentifier(unpriced, "IncludedDisk", undefined)).toBe("x");
    expect(defaultSizeIdentifier(undefined, "IncludedDisk", undefined)).toBe("");

    const demand = createStorageDemand({
      runtimeProfiles: [fit("cheap", false), fit("big", true)],
    });
    expect(defaultSizeIdentifier(sizedProvider, "IncludedDisk", demand)).toBe("mid");
    expect(defaultSizeIdentifier(sizedProvider, "FixedExternal", demand)).toBe("cheap");
    const allInsufficient = createStorageDemand({
      runtimeProfiles: [fit("cheap", false), fit("mid", false), fit("big", false)],
    });
    expect(defaultSizeIdentifier(sizedProvider, "IncludedDisk", allInsufficient)).toBe("cheap");
  });

  it("keeps a touched size and refines an untouched one once the demand arrives", () => {
    const touchedStrategy = new Set<CreateField>(["runtimeStorageStrategy"]);
    const before = resolveCreateDefaults(
      defaultsInput({
        capabilities: [sizedCapability()],
        touched: touchedStrategy,
      }),
    );
    expect(before.runtimeProfileIdentifier).toBe("cheap");

    const demand = createStorageDemand({ runtimeProfiles: [fit("cheap", false)] });
    const refined = resolveCreateDefaults(
      defaultsInput({
        capabilities: [sizedCapability()],
        current: before,
        touched: touchedStrategy,
        storageDemand: demand,
      }),
    );
    expect(refined.runtimeProfileIdentifier).toBe("mid");

    const kept = resolveCreateDefaults(
      defaultsInput({
        capabilities: [sizedCapability()],
        current: { ...before, runtimeProfileIdentifier: "BIG" },
        touched: new Set<CreateField>(["runtimeStorageStrategy", "runtimeProfileIdentifier"]),
        storageDemand: demand,
      }),
    );
    expect(kept.runtimeProfileIdentifier).toBe("BIG");
  });

  it("replaces choices that are no longer valid and keeps touched valid ones", () => {
    const replaced = resolveCreateDefaults(
      defaultsInput({
        current: emptyValues({
          gameIdentifier: "ghost",
          runtimeProviderIdentifier: "ghost",
          runtimeProfileIdentifier: "ghost",
        }),
        touched: new Set<CreateField>(["gameIdentifier", "runtimeProviderIdentifier"]),
      }),
    );
    expect(replaced.gameIdentifier).toBe("arma3");
    expect(replaced.runtimeProviderIdentifier).toBe("fake-cloud");
    expect(replaced.runtimeProfileIdentifier).toBe("medium");

    const noExternal = createCapability({
      runtimeProviderProfiles: [createProvider("fake-cloud", { storage: noExternalStorage() })],
    });
    const strategy = resolveCreateDefaults(
      defaultsInput({
        capabilities: [noExternal],
        current: emptyValues({
          runtimeStorageStrategy: "FixedExternal",
          externalStorageSizeGiB: 80,
        }),
        touched: new Set<CreateField>(["runtimeStorageStrategy"]),
      }),
    );
    expect(strategy.runtimeStorageStrategy).toBe("IncludedDisk");
    expect(strategy.externalStorageSizeGiB).toBeNull();

    const fixed = resolveCreateDefaults(
      defaultsInput({
        current: emptyValues({
          runtimeStorageStrategy: "FixedExternal",
          externalStorageSizeGiB: 80,
        }),
        touched: new Set<CreateField>(["runtimeStorageStrategy"]),
      }),
    );
    expect(fixed.runtimeStorageStrategy).toBe("FixedExternal");
    expect(fixed.externalStorageSizeGiB).toBe(80);
  });

  it("defaults required terrain and scenario by source identifier and keeps valid ones", () => {
    const capability = counterStrikeCapability();
    const terrains = createContentOptions([
      { primaryKey: "1", sourceIdentifier: "de_inferno" },
      { primaryKey: "2", sourceIdentifier: "DE_DUST2" },
    ]);
    const scenarios = createContentOptions([
      { primaryKey: uuid(1), sourceIdentifier: "casual" },
      { primaryKey: uuid(2), sourceIdentifier: "competitive" },
    ]);
    const resolved = resolveCreateDefaults(
      defaultsInput({ capabilities: [capability], terrains, scenarios }),
    );
    expect(resolved.terrainId).toBe(2);
    expect(resolved.scenarioId).toBe(uuid(2));

    const keptValues = resolveCreateDefaults(
      defaultsInput({
        capabilities: [capability],
        current: emptyValues({ gameIdentifier: "cs2", terrainId: 1, scenarioId: uuid(1) }),
        terrains,
        scenarios,
      }),
    );
    expect(keptValues.terrainId).toBe(1);
    expect(keptValues.scenarioId).toBe(uuid(1));

    const firstFallback = resolveCreateDefaults(
      defaultsInput({
        capabilities: [counterStrikeCapability({ defaultTerrainSourceIdentifier: "missing" })],
        terrains,
        scenarios,
      }),
    );
    expect(firstFallback.terrainId).toBe(1);

    const loading = resolveCreateDefaults(
      defaultsInput({
        capabilities: [capability],
        current: emptyValues({ gameIdentifier: "cs2", terrainId: 7, scenarioId: uuid(7) }),
      }),
    );
    expect(loading.terrainId).toBe(7);
    expect(loading.scenarioId).toBe(uuid(7));

    const notRequired = resolveCreateDefaults(
      defaultsInput({
        current: emptyValues({ gameIdentifier: "arma3", terrainId: 5, scenarioId: uuid(5) }),
        terrains,
        scenarios,
      }),
    );
    expect(notRequired.terrainId).toBeNull();
    expect(notRequired.scenarioId).toBeNull();
  });

  it("drops mod pack, profile and owner choices that the loaded lists lack", () => {
    const owners = createOwners();
    const profileId = uuid(30);
    const profiles = [
      {
        primaryKey: profileId,
        title: "Profile",
        gameIdentifier: "arma3",
        isArchived: false,
        latestRevisionNumber: 1,
      },
    ];
    const modPacks = [{ primaryKey: 42, title: "Pack" }];
    const current = emptyValues({
      gameIdentifier: "arma3",
      modPackDefinitionId: 42,
      configurationProfileId: profileId.toLocaleUpperCase(),
      costOwnerId: owners[1]?.costOwnerId ?? null,
      title: "Keep me",
      isLaunchDisabled: true,
    });
    const snapshot = structuredClone(current);

    const kept = resolveCreateDefaults(defaultsInput({ current, modPacks, profiles, owners }));
    expect(kept.modPackDefinitionId).toBe(42);
    expect(kept.configurationProfileId).toBe(profileId.toLocaleUpperCase());
    expect(kept.costOwnerId).toBe(owners[1]?.costOwnerId);
    expect(kept.title).toBe("Keep me");
    expect(kept.isLaunchDisabled).toBe(true);

    const whileLoading = resolveCreateDefaults(defaultsInput({ current }));
    expect(whileLoading.modPackDefinitionId).toBe(42);
    expect(whileLoading.configurationProfileId).toBe(profileId.toLocaleUpperCase());
    expect(whileLoading.costOwnerId).toBe(owners[1]?.costOwnerId);

    const dropped = resolveCreateDefaults(
      defaultsInput({ current, modPacks: [], profiles: [], owners: [owners[0]!] }),
    );
    expect(dropped.modPackDefinitionId).toBeNull();
    expect(dropped.configurationProfileId).toBeNull();
    expect(dropped.costOwnerId).toBeNull();

    const noModPacks = resolveCreateDefaults(
      defaultsInput({
        capabilities: [createCapability({ supportsModPacks: false })],
        current,
        modPacks,
      }),
    );
    expect(noModPacks.modPackDefinitionId).toBeNull();
    expect(current).toEqual(snapshot);
  });
});

describe("create storage and capacity rules", () => {
  const storage = createProvider("fake-cloud").storage;
  const profile = createProfile("medium");

  it("validates an explicit external capacity against the provider bounds", () => {
    expect(isExternalCapacityEntryValid(30, storage)).toBe(true);
    for (const invalid of [null, 25, 10, 510, 30.5]) {
      expect(isExternalCapacityEntryValid(invalid, storage)).toBe(false);
    }
    expect(isExternalCapacityEntryValid(undefined, storage)).toBe(false);
    expect(
      isExternalCapacityEntryValid(30, { ...storage!, supportsExternalBlockStorage: false }),
    ).toBe(false);
    expect(isExternalCapacityEntryValid(30, null)).toBe(false);
  });

  it("reports a storage misfit only for a loaded demand and a valid entry", () => {
    const insufficient = createStorageDemand({
      runtimeProfiles: [
        {
          runtimeProviderIdentifier: "fake-cloud",
          runtimeProfileIdentifier: "medium",
          displayName: "Medium",
          includedCapacityBytes: null,
          usableCapacityBytes: null,
          isSufficient: false,
          externalStorageRequired: true,
        },
      ],
    });
    const base = { profile, storage, fixedExternalGiB: null };
    expect(isStorageMisfit({ ...base, strategy: "IncludedDisk", demand: insufficient })).toBe(true);
    expect(
      isStorageMisfit({ ...base, strategy: "IncludedDisk", demand: createStorageDemand() }),
    ).toBe(false);
    expect(
      isStorageMisfit({
        ...base,
        strategy: "FixedExternal",
        demand: createStorageDemand(),
        fixedExternalGiB: 30,
      }),
    ).toBe(true);
    expect(
      isStorageMisfit({
        ...base,
        strategy: "FixedExternal",
        demand: createStorageDemand(),
        fixedExternalGiB: 80,
      }),
    ).toBe(false);
    expect(
      isStorageMisfit({
        ...base,
        strategy: "FixedExternal",
        demand: createStorageDemand(),
        fixedExternalGiB: 25,
      }),
    ).toBe(false);
    expect(isStorageMisfit({ ...base, strategy: "IncludedDisk", demand: undefined })).toBe(false);
  });
});

describe("create validation requirements", () => {
  const requirements = {
    requiresTerrain: true,
    terrainRequired: "terrain-required",
    requiresScenario: true,
    scenarioRequired: "scenario-required",
    storage: createProvider("fake-cloud").storage,
  };
  const valid = {
    title: "UTF One",
    gameIdentifier: "cs2",
    runtimeProviderIdentifier: "fake-cloud",
    runtimeProfileIdentifier: "medium",
    runtimeStorageStrategy: "AutomaticExternal",
    externalStorageSizeGiB: null,
    modPackDefinitionId: null,
    terrainId: 1,
    scenarioId: "20000000-0000-4000-8000-000000000001",
    configurationProfileId: null,
    costOwnerId: null,
    isLaunchDisabled: false,
  };
  const issues = (result: { error?: { issues: { path: PropertyKey[]; message: string }[] } }) =>
    (result.error?.issues ?? []).map((issue) => `${issue.path.join(".")}:${issue.message}`);

  it("reports a missing required terrain or scenario at the field", () => {
    const schema = createServerCreationSchema(messages, requirements);
    expect(schema.safeParse(valid).success).toBe(true);
    expect(
      issues(schema.safeParse({ ...valid, terrainId: null, scenarioId: null })).sort(),
    ).toEqual(["scenarioId:scenario-required", "terrainId:terrain-required"]);
  });

  it("reports an invalid explicit capacity and still validates the other fields", () => {
    const schema = createServerCreationSchema(messages, requirements);
    expect(
      issues(
        schema.safeParse({
          ...valid,
          runtimeStorageStrategy: "FixedExternal",
          externalStorageSizeGiB: 25,
        }),
      ),
    ).toEqual(["externalStorageSizeGiB:capacity-invalid"]);
    expect(
      schema.safeParse({
        ...valid,
        runtimeStorageStrategy: "FixedExternal",
        externalStorageSizeGiB: 30,
      }).success,
    ).toBe(true);
    expect(issues(schema.safeParse({ ...valid, title: " ", terrainId: null })).sort()).toEqual([
      "terrainId:terrain-required",
      "title:name-required",
    ]);
  });

  it("keeps the static behaviour without requirements and never requires a mod pack", () => {
    const plain = createServerCreationSchema(messages);
    expect(plain.safeParse({ ...valid, terrainId: null, scenarioId: null }).success).toBe(true);
    const guarded = createServerCreationSchema(messages, requirements);
    expect(guarded.safeParse({ ...valid, modPackDefinitionId: null }).success).toBe(true);
  });
});

describe("create field order", () => {
  it("lists the fields in page order with the advanced ones marked", () => {
    expect(createFieldOrder).toEqual([
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
    ]);
    expect(advancedCreateFields.size).toBe(9);
    for (const field of ["title", "gameIdentifier", "runtimeProfileIdentifier"] as const) {
      expect(advancedCreateFields.has(field)).toBe(false);
    }
    expect(advancedCreateFields.has("costOwnerId")).toBe(true);
    expect(
      invalidCreateFields({ isLaunchDisabled: {}, terrainId: {}, title: {}, costOwnerId: {} }),
    ).toEqual(["title", "terrainId", "costOwnerId", "isLaunchDisabled"]);
    expect(invalidCreateFields({})).toEqual([]);
  });
});

describe("server creation service", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("loads only the scoped cost-owner choices required by the creation form", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify([
          {
            costOwnerId: "10000000-0000-4000-8000-000000000002",
            displayName: "Shared budget",
            isFrozen: false,
            isCurrentUser: false,
          },
        ]),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const owners = await hostingService.creationCostOwners();

    expect(owners[0]?.displayName).toBe("Shared budget");
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/game-servers/creation/cost-owner-options");
    expect(init.credentials).toBe("include");
    expect(new Headers(init.headers).get("X-UTF-UI-Variant")).toBe("react");
  });

  it("posts the complete creation payload without contacting a runtime provider", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          primaryKey: "10000000-0000-4000-8000-000000000003",
          actions: null,
          deployment: null,
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);
    const payload = {
      title: "UTF One",
      gameIdentifier: "arma3",
      runtimeProviderIdentifier: "hetzner",
      runtimeProfileIdentifier: "cx32",
      runtimeStorageStrategy: "FixedExternal",
      externalStorageSizeGiB: 80,
      modPackDefinitionId: 42,
      configurationProfileId: "10000000-0000-4000-8000-000000000001",
      configurationProfileRevisionId: null,
      costOwnerId: "10000000-0000-4000-8000-000000000002",
      isLaunchDisabled: true,
    } as ServerCreation;

    await hostingService.create(payload);

    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/game-servers");
    expect(init.method).toBe("POST");
    expect(JSON.parse(String(init.body))).toEqual(payload);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
