import { act, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import i18n from "@/app/i18n";
import { useForm, useWatch } from "react-hook-form";
import { PresetField, type PresetFieldProps } from "@/components/server/PresetField";
import { SizeField, type SizeFieldProps } from "@/components/server/SizeField";
import {
  customPresetValue,
  defaultPresetValue,
  projectCreatePresets,
  type ServerCreationFormValues,
} from "@/features/hosting/createServerModel";
import { formatHourlyRate } from "@/features/hosting/model";
import type {
  ConfigurationProfileOption,
  RuntimeProfile,
  ServerModPackOption,
} from "@/features/hosting/types";
import { createProfile, createStorageDemand } from "../helpers/createFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

const profileId = "30000000-0000-4000-8000-000000000001";

const profiles: ConfigurationProfileOption[] = [
  {
    primaryKey: profileId,
    title: "Public operation",
    gameIdentifier: "arma3",
    isArchived: false,
    latestRevisionNumber: 3,
  },
];
const modPacks = [{ primaryKey: 42, title: "Operations" }] as ServerModPackOption[];

afterEach(async () => {
  await act(async () => i18n.changeLanguage("en"));
});

describe("PresetField", () => {
  const presets = projectCreatePresets(profiles, modPacks);

  function renderPreset(overrides: Partial<PresetFieldProps> = {}) {
    const props: PresetFieldProps = {
      presets,
      value: defaultPresetValue,
      loading: false,
      failed: false,
      showAppliedLine: false,
      onRetry: vi.fn(),
      onChoose: vi.fn(),
      ...overrides,
    };
    renderWithProviders(<PresetField {...props} />);
    return props;
  }

  it("groups the game defaults, configuration profiles and mod packs in one select", () => {
    renderPreset();
    const select = screen.getByRole("combobox", { name: "Preset" });
    expect(within(select).getByRole("option", { name: "Game defaults" })).toBeInTheDocument();
    const profileGroup = within(select).getByRole("group", { name: "Configuration profiles" });
    expect(
      within(profileGroup).getByRole("option", { name: "Public operation (revision 3)" }),
    ).toBeInTheDocument();
    const modPackGroup = within(select).getByRole("group", { name: "Mod packs" });
    expect(within(modPackGroup).getByRole("option", { name: "Operations" })).toBeInTheDocument();
    expect(
      screen.getByText(
        "Start from a saved setup. You can still change every option under Advanced options.",
      ),
    ).toBeInTheDocument();
  });

  it("reports exactly the two form values a chosen preset stands for", async () => {
    const user = userEvent.setup();
    const props = renderPreset();
    const select = screen.getByRole("combobox", { name: "Preset" });

    await user.selectOptions(select, "Public operation (revision 3)");
    expect(props.onChoose).toHaveBeenLastCalledWith(
      { configurationProfileId: profileId, modPackDefinitionId: null },
      presets[1],
    );

    await user.selectOptions(select, "Operations");
    expect(props.onChoose).toHaveBeenLastCalledWith(
      { configurationProfileId: null, modPackDefinitionId: 42 },
      presets[2],
    );

    await user.selectOptions(select, "Game defaults");
    expect(props.onChoose).toHaveBeenLastCalledWith(
      { configurationProfileId: null, modPackDefinitionId: null },
      presets[0],
    );
  });

  it("shows a disabled Customized option and line when the fields match no preset", async () => {
    const user = userEvent.setup();
    const props = renderPreset({ value: customPresetValue, showAppliedLine: true });
    const select = screen.getByRole("combobox", { name: "Preset" });
    const custom = within(select).getByRole("option", { name: "Customized" });
    expect(custom).toBeDisabled();
    expect(select).toHaveValue(customPresetValue);
    expect(
      screen.getByText("Customized: your Advanced options differ from this preset."),
    ).toBeInTheDocument();
    expect(screen.queryByText(/keeps it off/)).not.toBeInTheDocument();

    await user.selectOptions(select, "Operations");
    expect(props.onChoose).toHaveBeenCalledTimes(1);
  });

  it("states that a preset keeps the server off only while asked to", () => {
    const { unmount } = renderWithProviders(
      <PresetField
        presets={presets}
        value={presets[1]!.value}
        loading={false}
        failed={false}
        showAppliedLine
        onRetry={vi.fn()}
        onChoose={vi.fn()}
      />,
    );
    expect(
      screen.getByText(
        "This preset creates the server and keeps it off. You decide when to start.",
      ),
    ).toBeInTheDocument();
    unmount();
    renderPreset({ value: presets[1]!.value, showAppliedLine: false });
    expect(screen.queryByText(/keeps it off/)).not.toBeInTheDocument();
  });

  it("renders nothing when only the built-in default exists", () => {
    renderPreset({ presets: projectCreatePresets([], []) });
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.queryByText("Preset")).not.toBeInTheDocument();
  });

  it("disables the select with the loading panel while the lists load", () => {
    renderPreset({ loading: true, presets: projectCreatePresets(undefined, undefined) });
    expect(screen.getByRole("combobox", { name: "Preset" })).toBeDisabled();
    expect(screen.getByText("Loading mod packs and configuration profiles…")).toBeInTheDocument();
  });

  it("replaces the select with the error panel and retries", async () => {
    const user = userEvent.setup();
    const props = renderPreset({ failed: true });
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(
      screen.getByText("Some options could not be loaded. Nothing is lost; try again."),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Try again" }));
    expect(props.onRetry).toHaveBeenCalledTimes(1);
  });

  it("localizes the group labels and the profile option in German", async () => {
    await act(async () => i18n.changeLanguage("de"));
    renderPreset();
    const select = screen.getByRole("combobox", { name: "Voreinstellung" });
    expect(
      within(select).getByRole("group", { name: "Konfigurationsprofile" }),
    ).toBeInTheDocument();
    expect(within(select).getByRole("group", { name: "Mod-Pakete" })).toBeInTheDocument();
    expect(
      within(select).getByRole("option", { name: "Public operation (Revision 3)" }),
    ).toBeInTheDocument();
  });
});

const medium = createProfile("medium");
const small = createProfile("small", {
  displayName: "Small",
  virtualCpuCount: 2,
  memoryGiB: 4,
  diskGiB: 40,
  estimatedHourlyCostEur: null,
});

type HarnessProps = Omit<SizeFieldProps, "form" | "selected" | "onMemberChange"> & {
  onMemberChange?: SizeFieldProps["onMemberChange"];
  onValues?: (values: ServerCreationFormValues) => void;
};

/** Binds a SizeField to react-hook-form, selecting the size the form holds. */
function SizeHarness({ profiles, strategy, demand, onMemberChange, onValues }: HarnessProps) {
  const form = useForm<ServerCreationFormValues>({
    defaultValues: { runtimeProfileIdentifier: "medium" } as ServerCreationFormValues,
  });
  const identifier = useWatch({ control: form.control, name: "runtimeProfileIdentifier" });
  onValues?.(form.getValues());
  const selected: RuntimeProfile | undefined = profiles.find(
    (profile) => profile.identifier === identifier,
  );
  return (
    <SizeField
      form={form}
      profiles={profiles}
      selected={selected}
      strategy={strategy}
      demand={demand}
      onMemberChange={onMemberChange ?? (() => undefined)}
    />
  );
}

describe("SizeField", () => {
  const profiles = [medium, small];

  it("lists every size with vCPU, memory and a price or price not available", () => {
    renderWithProviders(
      <SizeHarness profiles={profiles} strategy="AutomaticExternal" demand={undefined} />,
    );
    const select = screen.getByRole("combobox", { name: /^Size/ });
    expect(
      within(select).getByRole("option", {
        name: `Medium: 4 vCPU, 8 GiB RAM, ${formatHourlyRate(0.1)} per hour`,
      }),
    ).toBeInTheDocument();
    expect(
      within(select).getByRole("option", {
        name: "Small: 2 vCPU, 4 GiB RAM, price not available",
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("The size sets the cloud machine and its hourly price."),
    ).toBeInTheDocument();
    for (const option of within(select).getAllByRole("option")) {
      expect(option.textContent).not.toMatch(/0[.,]00\b(?!\d)/);
    }
  });

  it("never shows a zero price", () => {
    renderWithProviders(
      <SizeHarness
        profiles={[createProfile("free", { displayName: "Free", estimatedHourlyCostEur: 0 })]}
        strategy="AutomaticExternal"
        demand={undefined}
      />,
    );
    expect(screen.getByRole("option", { name: /price not available/ })).toBeInTheDocument();
  });

  it("updates the form and reports the member change", async () => {
    const user = userEvent.setup();
    const onMemberChange = vi.fn();
    let values: ServerCreationFormValues | undefined;
    renderWithProviders(
      <SizeHarness
        profiles={profiles}
        strategy="AutomaticExternal"
        demand={undefined}
        onMemberChange={onMemberChange}
        onValues={(next) => {
          values = next;
        }}
      />,
    );
    await user.selectOptions(screen.getByRole("combobox", { name: /^Size/ }), "small");
    expect(onMemberChange).toHaveBeenCalledWith("runtimeProfileIdentifier");
    expect(values?.runtimeProfileIdentifier).toBe("small");
  });

  it("shows the facts of the selected size and Price unavailable without a price", async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <SizeHarness profiles={profiles} strategy="AutomaticExternal" demand={undefined} />,
    );
    expect(screen.getByText("Virtual CPU").nextSibling).toHaveTextContent("4");
    expect(screen.getByText("Memory").nextSibling).toHaveTextContent("8 GiB");
    expect(screen.getByText("Local disk").nextSibling).toHaveTextContent("80 GiB");
    expect(screen.getByText("Hourly estimate").nextSibling).toHaveTextContent(
      formatHourlyRate(0.1),
    );
    await user.selectOptions(screen.getByRole("combobox", { name: /^Size/ }), "small");
    expect(screen.getByText("Hourly estimate").nextSibling).toHaveTextContent("Price unavailable");
    expect(screen.getByText("Memory").nextSibling).toHaveTextContent("4 GiB");
  });

  it("states the fit only for the included disk", () => {
    const view = renderWithProviders(
      <SizeHarness profiles={profiles} strategy="IncludedDisk" demand={createStorageDemand()} />,
    );
    expect(screen.getByText("Fits the game files and mods you selected.")).toBeInTheDocument();
    view.unmount();

    const insufficient = createStorageDemand({
      runtimeProfiles: [
        {
          ...createStorageDemand().runtimeProfiles![0]!,
          runtimeProfileIdentifier: "MEDIUM",
          isSufficient: false,
        },
      ],
    });
    const second = renderWithProviders(
      <SizeHarness profiles={profiles} strategy="IncludedDisk" demand={insufficient} />,
    );
    expect(
      screen.getByText(
        "Too small for this setup. Choose a larger size or external storage under Advanced options.",
      ),
    ).toBeInTheDocument();
    second.unmount();

    const unknown = createStorageDemand({ runtimeProfiles: [] });
    renderWithProviders(
      <SizeHarness profiles={profiles} strategy="IncludedDisk" demand={unknown} />,
    );
    expect(
      screen.getByText("The fit could not be checked. External storage may be needed."),
    ).toBeInTheDocument();
  });

  it("shows no fit line for external strategies or without a loaded demand", () => {
    const insufficient = createStorageDemand({
      runtimeProfiles: [{ ...createStorageDemand().runtimeProfiles![0]!, isSufficient: false }],
    });
    for (const strategy of ["AutomaticExternal", "FixedExternal"] as const) {
      const view = renderWithProviders(
        <SizeHarness profiles={profiles} strategy={strategy} demand={insufficient} />,
      );
      expect(
        screen.queryByText(/Too small|Fits the game|could not be checked/),
      ).not.toBeInTheDocument();
      view.unmount();
    }
    renderWithProviders(
      <SizeHarness profiles={profiles} strategy="IncludedDisk" demand={undefined} />,
    );
    expect(
      screen.queryByText(/Too small|Fits the game|could not be checked/),
    ).not.toBeInTheDocument();
  });

  it("localizes the option and the fit line in German", async () => {
    await act(async () => i18n.changeLanguage("de"));
    renderWithProviders(
      <SizeHarness profiles={profiles} strategy="IncludedDisk" demand={createStorageDemand()} />,
    );
    expect(
      screen.getByRole("option", {
        name: `Medium: 4 vCPU, 8 GiB RAM, ${formatHourlyRate(0.1)} pro Stunde`,
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Passt zu den ausgewählten Spieldateien und Mods."),
    ).toBeInTheDocument();
  });
});
