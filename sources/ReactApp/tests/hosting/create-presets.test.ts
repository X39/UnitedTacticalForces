import {
  customPresetValue,
  defaultPresetValue,
  presetSelection,
  presetSelectValue,
  projectCreatePresets,
} from "@/features/hosting/createServerModel";
import type { ConfigurationProfileOption, ServerModPackOption } from "@/features/hosting/types";

const first = "30000000-0000-4000-8000-000000000001";
const second = "30000000-0000-4000-8000-000000000002";
const archived = "30000000-0000-4000-8000-000000000003";

const profiles: ConfigurationProfileOption[] = [
  {
    primaryKey: first,
    title: "Casual",
    gameIdentifier: "g",
    isArchived: false,
    latestRevisionNumber: 3,
  },
  {
    primaryKey: archived,
    title: "Old",
    gameIdentifier: "g",
    isArchived: true,
    latestRevisionNumber: 1,
  },
  {
    primaryKey: second,
    title: "Ranked",
    gameIdentifier: "g",
    isArchived: false,
    latestRevisionNumber: 5,
  },
];
const modPacks = [
  { primaryKey: 42, title: "Core pack" },
  { primaryKey: 43, title: "Extra pack" },
] as ServerModPackOption[];

describe("create presets", () => {
  it("projects only the built-in default while nothing is loaded", () => {
    expect(projectCreatePresets(undefined, undefined)).toEqual([
      { kind: "default", value: defaultPresetValue },
    ]);
  });

  it("lists profiles then mod packs in API order and drops archived profiles", () => {
    const presets = projectCreatePresets(profiles, modPacks);
    expect(presets.map((preset) => preset.value)).toEqual([
      "default",
      `profile:${first}`,
      `profile:${second}`,
      "modPack:42",
      "modPack:43",
    ]);
    expect(presets[1]).toEqual({
      kind: "profile",
      value: `profile:${first}`,
      profileId: first,
      title: "Casual",
      revision: 3,
    });
    expect(presets[3]).toEqual({
      kind: "modPack",
      value: "modPack:42",
      modPackId: 42,
      title: "Core pack",
    });
  });

  it("derives the select value from the two form fields", () => {
    const presets = projectCreatePresets(profiles, modPacks);
    const none = { configurationProfileId: null, modPackDefinitionId: null };
    expect(presetSelectValue(none, presets)).toBe(defaultPresetValue);
    expect(
      presetSelectValue({ ...none, configurationProfileId: second.toLocaleUpperCase() }, presets),
    ).toBe(`profile:${second}`);
    expect(presetSelectValue({ ...none, modPackDefinitionId: 43 }, presets)).toBe("modPack:43");
    expect(
      presetSelectValue({ configurationProfileId: first, modPackDefinitionId: 42 }, presets),
    ).toBe(customPresetValue);
    expect(
      presetSelectValue(
        { ...none, configurationProfileId: "30000000-0000-4000-8000-0000000000ff" },
        presets,
      ),
    ).toBe(customPresetValue);
    expect(presetSelectValue({ ...none, configurationProfileId: archived }, presets)).toBe(
      customPresetValue,
    );
    expect(presetSelectValue({ ...none, modPackDefinitionId: 99 }, presets)).toBe(
      customPresetValue,
    );
  });

  it("maps a preset value to the form fields it sets", () => {
    const presets = projectCreatePresets(profiles, modPacks);
    expect(presetSelection("default", presets)).toEqual({
      configurationProfileId: null,
      modPackDefinitionId: null,
    });
    expect(presetSelection(`profile:${first}`, presets)).toEqual({
      configurationProfileId: first,
      modPackDefinitionId: null,
    });
    expect(presetSelection("modPack:42", presets)).toEqual({
      configurationProfileId: null,
      modPackDefinitionId: 42,
    });
  });

  it("never selects anything for the custom value or an untrusted value", () => {
    const presets = projectCreatePresets(profiles, modPacks);
    for (const value of [
      "custom",
      "profile:unknown",
      "modPack:99",
      "",
      "__proto__",
      "constructor",
      "toString",
    ]) {
      expect(presetSelection(value, presets)).toBeUndefined();
    }
  });
});
