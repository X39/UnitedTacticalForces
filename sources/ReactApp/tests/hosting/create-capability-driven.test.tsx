import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { readdirSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { useForm, type UseFormReturn } from "react-hook-form";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import { i18nReady } from "@/app/i18n";
import { AdvancedOptions, type AdvancedOptionsProps } from "@/components/server/AdvancedOptions";
import { CreateServerPage } from "@/features/hosting/CreateServerPage";
import type { ServerCreationFormValues } from "@/features/hosting/createServerModel";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, ServerAction } from "@/features/hosting/types";
import {
  counterStrikeCapability,
  createCapability,
  createContentOptions,
  createOwners,
  createProvider,
  createStorageDemand,
} from "../helpers/createFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isLoading: false,
    isAuthenticated: Boolean(authState.user),
  }),
}));

const emptyValues: ServerCreationFormValues = {
  title: "",
  gameIdentifier: "",
  runtimeProviderIdentifier: "fake-cloud",
  runtimeProfileIdentifier: "medium",
  runtimeStorageStrategy: "IncludedDisk",
  externalStorageSizeGiB: null,
  modPackDefinitionId: null,
  terrainId: null,
  scenarioId: null,
  configurationProfileId: null,
  costOwnerId: null,
  isLaunchDisabled: false,
};

const ready = <T,>(data: readonly T[]) => ({ data, loading: false, failed: false });

interface HarnessProps {
  overrides?: Partial<AdvancedOptionsProps>;
  initialOpen?: boolean;
  onForm?: (form: UseFormReturn<ServerCreationFormValues>) => void;
  defaultValues?: Partial<ServerCreationFormValues>;
}

function Harness({ overrides, initialOpen = true, onForm, defaultValues }: HarnessProps) {
  const form = useForm<ServerCreationFormValues>({
    defaultValues: { ...emptyValues, ...defaultValues },
  });
  onForm?.(form);
  return (
    <AdvancedOptions
      form={form}
      open={initialOpen}
      onOpenChange={() => undefined}
      capability={createCapability()}
      availableProviders={[createProvider("fake-cloud")]}
      unavailableProviders={[]}
      storage={<p>storage slot</p>}
      modPacks={ready([
        { primaryKey: 7, title: "Weekend pack" },
        { primaryKey: 9, title: "Winter pack" },
      ])}
      terrains={ready([])}
      scenarios={ready([])}
      profiles={ready([])}
      owners={createOwners()}
      onRetryOptions={() => undefined}
      onMemberChange={() => undefined}
      onProviderChange={() => undefined}
      onLaunchDisabledChange={() => undefined}
      {...overrides}
    />
  );
}

function renderAdvanced(props: HarnessProps = {}) {
  let formRef: UseFormReturn<ServerCreationFormValues> | undefined;
  const result = renderWithProviders(<Harness {...props} onForm={(form) => (formRef = form)} />);
  return { ...result, form: () => formRef! };
}

const counterStrikeOptions = {
  terrains: ready(
    createContentOptions([
      { primaryKey: "1", sourceIdentifier: "de_dust2", title: "Dust 2" },
      { primaryKey: "2", sourceIdentifier: "de_mirage", title: "Mirage" },
    ]),
  ),
  scenarios: ready(
    createContentOptions([
      { primaryKey: "20000000-0000-4000-8000-000000000001", sourceIdentifier: "competitive" },
    ]),
  ),
};

describe("capability-driven advanced options", () => {
  it("renders an optional mod pack and no content fields for the Arma-shaped capability", () => {
    renderAdvanced();

    const modPack = screen.getByLabelText("Mod pack");
    expect(modPack).not.toBeRequired();
    expect(within(modPack).getByRole("option", { name: "No mod pack" })).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Map/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^Game mode/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^Terrain/)).not.toBeInTheDocument();
  });

  it("renders required labelled terrain and scenario fields for the Counter-Strike-shaped capability", () => {
    renderAdvanced({
      overrides: { capability: counterStrikeCapability(), ...counterStrikeOptions },
    });

    const map = screen.getByLabelText(/^Map/);
    const mode = screen.getByLabelText(/^Game mode/);
    expect(map).toBeRequired();
    expect(mode).toBeRequired();
    expect(screen.getByText("Map").closest("[lang='en']")).not.toBeNull();
    expect(screen.getByText("Game mode").closest("[lang='en']")).not.toBeNull();
    expect((within(map).getAllByRole("option")[0] as HTMLOptionElement).textContent).toBe(
      "Choose Map",
    );
    expect(within(map).getAllByRole("option")[0]).toHaveValue("");
    expect(within(mode).getAllByRole("option")[0]).toHaveTextContent("Choose Game mode");
    expect(within(map).getByRole("option", { name: "Dust 2" })).toBeInTheDocument();
    expect(screen.getByLabelText("Mod pack")).not.toBeRequired();
  });

  it("renders no mod pack field when the capability does not support mod packs", () => {
    renderAdvanced({ overrides: { capability: createCapability({ supportsModPacks: false }) } });

    expect(screen.queryByLabelText("Mod pack")).not.toBeInTheDocument();
  });

  it("shows a single available provider as a read-only line", () => {
    renderAdvanced();

    expect(screen.getByText("Runs on fake-cloud")).toBeInTheDocument();
    expect(screen.queryByLabelText("Runtime provider")).not.toBeInTheDocument();
  });

  it("offers a provider select for several providers and reports the chosen identifier", async () => {
    const onProviderChange = vi.fn();
    renderAdvanced({
      overrides: {
        availableProviders: [createProvider("fake-cloud"), createProvider("other-cloud")],
        onProviderChange,
      },
    });

    await userEvent.selectOptions(screen.getByLabelText("Runtime provider"), "other-cloud");

    expect(onProviderChange).toHaveBeenCalledWith("other-cloud");
  });

  it("lists unavailable providers with their mapped status copy", () => {
    renderAdvanced({
      overrides: {
        unavailableProviders: [
          createProvider("needs-setup", { availabilityStatus: "configuration-required" }),
          createProvider("on-hold", { availabilityStatus: "paused" }),
        ],
      },
    });

    expect(
      screen.getByRole("heading", { name: "Providers that are not available" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/needs-setup is registered, but its provider credentials/),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Server actions are paused by the platform operator/),
    ).toBeInTheDocument();
  });

  it("keeps a summary line visible while the disclosure is closed", () => {
    const { container } = renderAdvanced({
      initialOpen: false,
      overrides: { capability: counterStrikeCapability(), ...counterStrikeOptions },
      defaultValues: { terrainId: 2 },
    });

    const disclosure = container.querySelector(".disclosure.create-advanced") as HTMLElement;
    expect(screen.getByRole("button", { name: "Advanced options" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
    const line = disclosure.querySelector(".disclosure__summary-line") as HTMLElement;
    expect(line).toHaveTextContent("Runs on fake-cloud");
    expect(line).toHaveTextContent("Storage: Included profile disk only");
    expect(line).toHaveTextContent("Mod pack: No mod pack");
    expect(line).toHaveTextContent("Configuration: Game defaults");
    expect(line).toHaveTextContent("Map: Mirage");
    expect(disclosure.querySelector("select")).not.toBeNull();
  });

  it("keeps the field order: provider, storage, mod pack, terrain, scenario, profile, owner, launch, about", () => {
    const { container } = renderAdvanced({
      overrides: {
        capability: counterStrikeCapability({
          actions: [{ id: "start", label: "Start", order: 1 } as unknown as ServerAction],
        }),
        availableProviders: [createProvider("fake-cloud"), createProvider("other-cloud")],
        ...counterStrikeOptions,
      },
    });

    const markers = [
      "Runtime provider",
      "storage slot",
      "Mod pack",
      "Map",
      "Game mode",
      "Configuration profile",
      "Cost owner",
      "Keep launch disabled",
      "What this game server can do",
    ];
    const text = container.querySelector(".create-advanced__content")?.textContent ?? "";
    const positions = markers.map((marker) => text.indexOf(marker));
    expect(positions.every((position) => position >= 0)).toBe(true);
    expect([...positions].sort((a, b) => a - b)).toEqual(positions);
  });

  it("sets the mod pack id as a number and null for the empty option", async () => {
    const onMemberChange = vi.fn();
    const { form } = renderAdvanced({ overrides: { onMemberChange } });
    const select = screen.getByLabelText("Mod pack");

    await userEvent.selectOptions(select, "Winter pack");
    expect(form().getValues("modPackDefinitionId")).toBe(9);
    expect(onMemberChange).toHaveBeenCalledWith("modPackDefinitionId");

    await userEvent.selectOptions(select, "No mod pack");
    expect(form().getValues("modPackDefinitionId")).toBeNull();
  });

  it("offers the caller as the empty option and focuses the cost owner through the form", async () => {
    const { form } = renderAdvanced({
      overrides: { owners: createOwners({ frozenSelf: true }) },
    });
    const select = screen.getByLabelText("Cost owner");

    const options = within(select).getAllByRole("option");
    expect(options[0]).toHaveValue("");
    expect(options[0]).toHaveTextContent("My cost owner — Personal budget (default) — frozen");
    expect(options[1]).toHaveTextContent("Shared budget");

    act(() => form().setFocus("costOwnerId"));
    await waitFor(() => expect(select).toHaveFocus());
  });

  it("reports the launch-disabled toggle", async () => {
    const onLaunchDisabledChange = vi.fn();
    const { form } = renderAdvanced({ overrides: { onLaunchDisabledChange } });

    await userEvent.click(screen.getByLabelText("Keep launch disabled"));

    expect(form().getValues("isLaunchDisabled")).toBe(true);
    expect(onLaunchDisabledChange).toHaveBeenCalledWith(true);
  });

  it("explains a failed option list and offers to retry", async () => {
    const onRetryOptions = vi.fn();
    renderAdvanced({
      overrides: {
        capability: counterStrikeCapability(),
        terrains: { data: undefined, loading: false, failed: true },
        scenarios: counterStrikeOptions.scenarios,
        onRetryOptions,
      },
    });

    expect(
      screen.getByText("Some options could not be loaded. Nothing is lost; try again."),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(onRetryOptions).toHaveBeenCalledTimes(1);
  });

  it("shows a validation error at the terrain field", async () => {
    const { form } = renderAdvanced({
      overrides: { capability: counterStrikeCapability(), ...counterStrikeOptions },
    });

    act(() => form().setError("terrainId", { message: "Choose a Map to continue." }));

    expect(await screen.findByText("Choose a Map to continue.")).toBeInTheDocument();
    expect(screen.getByLabelText(/^Map/)).toBeInvalid();
  });

  it("keeps the about section collapsed and lists the actions by order", async () => {
    const { container } = renderAdvanced({
      overrides: {
        capability: createCapability({
          actions: [
            { id: "b", label: "Stop", order: 2 },
            { id: "a", label: "Start", order: 1 },
          ] as unknown as ServerAction[],
        }),
      },
    });

    const about = container.querySelector(".create-advanced__about") as HTMLElement;
    expect(
      within(about).getByRole("button", { name: "What this game server can do" }),
    ).toHaveAttribute("aria-expanded", "false");
    const items = within(about).getAllByRole("listitem", { hidden: true });
    expect(items.map((item) => item.textContent)).toEqual(["Start", "Stop"]);
    expect(within(about).getByText("Dedicated Arma operations.")).toBeInTheDocument();
  });
});

describe("capability-driven create page", () => {
  const scenarioId = "20000000-0000-4000-8000-000000000001";

  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = {
      primaryKey: "10000000-0000-4000-8000-000000000001",
      nickname: "Creator",
      isBanned: false,
      isVerified: true,
      isDeleted: false,
      isVereinMember: false,
      claims: [{ identifier: "crt:sv", value: null }],
      roles: [],
    } as FullUser;
    vi.spyOn(hostingService, "creationModPacks").mockResolvedValue([]);
    vi.spyOn(hostingService, "profileOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "terrains").mockResolvedValue(
      createContentOptions([
        { primaryKey: "1", sourceIdentifier: "de_mirage", title: "Mirage" },
        { primaryKey: "2", sourceIdentifier: "de_dust2", title: "Dust 2" },
      ]),
    );
    vi.spyOn(hostingService, "scenarios").mockResolvedValue(
      createContentOptions([{ primaryKey: scenarioId, sourceIdentifier: "competitive" }]),
    );
    vi.spyOn(hostingService, "estimateStorage").mockResolvedValue(createStorageDemand());
    vi.spyOn(hostingService, "creationCostOwners").mockResolvedValue(createOwners());
    vi.spyOn(hostingService, "create").mockResolvedValue({
      primaryKey: "10000000-0000-4000-8000-000000000099",
      title: "Created",
      actions: [],
      deployment: null,
    } as unknown as GameServer);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  it("offers an optional mod pack and no content fields for the Arma-shaped capability", async () => {
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([createCapability()]);
    renderWithProviders(<CreateServerPage />);

    const modPack = await screen.findByLabelText("Mod pack");
    expect(modPack).not.toBeRequired();
    expect(screen.queryByLabelText(/^Map/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^Game mode/)).not.toBeInTheDocument();
  });

  it("offers a Map and Game mode filled with the defaults for the Counter-Strike-shaped capability", async () => {
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([counterStrikeCapability()]);
    renderWithProviders(<CreateServerPage />);

    await waitFor(() => expect(screen.getByLabelText(/^Map/)).toHaveValue("2"));
    expect(screen.getByLabelText(/^Game mode/)).toHaveValue(scenarioId);
    expect(screen.getByLabelText(/^Map/)).toBeRequired();
    expect(screen.getByLabelText(/^Game mode/)).toBeRequired();
  });

  it("creates without a mod pack although the capability requires one", async () => {
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([
      createCapability({ requiresModPack: true }),
    ]);
    vi.mocked(hostingService.creationModPacks).mockResolvedValue([
      { primaryKey: 42, title: "Operations" },
    ]);
    const user = userEvent.setup();
    renderWithProviders(<CreateServerPage />);

    await user.type(await screen.findByLabelText(/^Server name/), "No pack");
    const submit = await waitFor(() => {
      const button = screen.getByRole("button", { name: "Create server and keep off" });
      expect(button).not.toHaveAttribute("aria-disabled");
      return button;
    });
    await user.click(submit);

    await waitFor(() => expect(hostingService.create).toHaveBeenCalledTimes(1));
    expect(hostingService.create).toHaveBeenCalledWith(
      expect.objectContaining({ modPackDefinitionId: null }),
    );
    expect(screen.queryByText(/Choose a Mod pack/)).not.toBeInTheDocument();
  });
});

describe("create files stay game-agnostic", () => {
  const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");
  const serverDirectory = join(sourceRoot, "components", "server");
  const prefixes = /^(Create|create|Preset|Size|CostOwner|Advanced|Storage|AfterCreating)/;
  const gameIdentifier = /\b(arma3|aska|counterstrike2)\b/i;

  function createFiles(): string[] {
    return [
      join(sourceRoot, "features", "hosting", "CreateServerPage.tsx"),
      join(sourceRoot, "features", "hosting", "createServerModel.ts"),
      ...readdirSync(serverDirectory)
        .filter((name) => prefixes.test(name) && /\.(tsx?|css)$/.test(name))
        .map((name) => join(serverDirectory, name)),
    ];
  }

  it("scans the create files and finds no game identifier literal", () => {
    const files = createFiles();
    expect(files.length).toBeGreaterThanOrEqual(3);
    const findings = files.flatMap((file) =>
      readFileSync(file, "utf8")
        .split(/\r?\n/)
        .flatMap((line, index) =>
          gameIdentifier.test(line) ? [`${file}:${index + 1}: ${line.trim()}`] : [],
        ),
    );
    expect(findings).toEqual([]);
  });

  it("detects a game identifier in a bad source", () => {
    expect(gameIdentifier.test('if (game === "arma3") return;')).toBe(true);
    expect(gameIdentifier.test("const label = capability.terrainLabel;")).toBe(false);
  });
});
