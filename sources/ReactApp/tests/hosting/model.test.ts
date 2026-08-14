import {
  automaticExternalStorageGiB,
  buildConfigurationValues,
  configurationDraftValue,
  isStorageSelectionValid,
  validateConfigurationDraft,
} from "@/features/hosting/model";
import type {
  ConfigurationDefinition,
  RuntimeProfile,
  RuntimeStorageCapabilities,
  StorageDemand,
} from "@/features/hosting/types";

const profile = {
  identifier: "cx32",
  usableStorageBytes: 40 * 1_073_741_824,
} as RuntimeProfile;
const storage = {
  supportsExternalBlockStorage: true,
  minimumCapacityGiB: 20,
  maximumCapacityGiB: 100,
  capacityIncrementGiB: 10,
} as RuntimeStorageCapabilities;
const demand = {
  peakPreparationRequiredBytes: 41 * 1_073_741_824,
  runtimeProfiles: [{ runtimeProfileIdentifier: "cx32", isSufficient: false }],
  components: [],
  missingComponents: [],
} as unknown as StorageDemand;

describe("hosting storage and configuration models", () => {
  it("rounds automatic storage up to the provider increment", () => {
    expect(automaticExternalStorageGiB(demand, storage)).toBe(50);
    expect(isStorageSelectionValid("IncludedDisk", profile, demand, storage, undefined)).toBe(
      false,
    );
    expect(isStorageSelectionValid("AutomaticExternal", profile, demand, storage, undefined)).toBe(
      true,
    );
  });

  it("rejects fixed capacity outside provider bounds or below demand", () => {
    expect(isStorageSelectionValid("FixedExternal", profile, demand, storage, 40)).toBe(false);
    expect(isStorageSelectionValid("FixedExternal", profile, demand, storage, 45)).toBe(false);
    expect(isStorageSelectionValid("FixedExternal", profile, demand, storage, 50)).toBe(true);
    expect(isStorageSelectionValid("FixedExternal", profile, demand, storage, 110)).toBe(false);
  });

  it("parses typed configuration values and rejects missing required fields", () => {
    const definitions = [
      { key: "enabled", displayName: "Enabled", kind: "Boolean", required: true },
      { key: "players", displayName: "Players", kind: "Integer", required: true },
      { key: "name", displayName: "Name", kind: "String", required: false },
    ] as ConfigurationDefinition[];

    expect(
      buildConfigurationValues(definitions, {
        enabled: "true",
        players: "64",
        name: "UTF One",
      }),
    ).toEqual([
      { key: "enabled", value: true },
      { key: "players", value: 64 },
      { key: "name", value: "UTF One" },
    ]);
    expect(() => buildConfigurationValues(definitions, { enabled: "true" })).toThrow(
      "The configuration draft is invalid.",
    );
  });

  it("uses one array value per line and validates numeric constraints before requests", () => {
    const definitions = [
      {
        key: "motd[]",
        displayName: "Message of the day",
        kind: "StringArray",
        required: false,
        targetPath: "server.cfg",
        group: "General",
        description: "Messages",
        sensitive: false,
        order: 1,
        defaultValue: [],
        minimum: null,
        maximum: null,
        allowedValues: [],
      },
      {
        key: "players",
        displayName: "Players",
        kind: "Integer",
        required: true,
        targetPath: "server.cfg",
        group: "General",
        description: "Capacity",
        sensitive: false,
        order: 2,
        defaultValue: 32,
        minimum: 1,
        maximum: 64,
        allowedValues: [],
      },
    ] as ConfigurationDefinition[];

    expect(configurationDraftValue(definitions[0]!, ["Alpha", "Bravo"])).toBe("Alpha\nBravo");
    expect(
      buildConfigurationValues(definitions, {
        "motd[]": "Alpha\n Bravo \n",
        players: "32",
      }),
    ).toEqual([
      { key: "motd[]", value: ["Alpha", "Bravo"] },
      { key: "players", value: 32 },
    ]);
    expect(validateConfigurationDraft(definitions, { players: "65" })).toEqual([
      {
        key: "players",
        displayName: "Players",
        code: "maximum",
        limit: 64,
      },
    ]);
    expect(validateConfigurationDraft(definitions, { players: "2.5" })[0]?.code).toBe("integer");
  });
});
