import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
import { hostingService } from "@/features/hosting/services";
import type {
  GameConfiguration,
  GameServer,
  ServerCapability,
  ServerModPackOption,
} from "@/features/hosting/types";
import { createCapability } from "../helpers/createFixtures";
import { fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { clearSettingsPanels } from "../helpers/settingsPanels";

/* The Mods panel of the Settings section: a capability-driven mod pack chooser that stages its change (CHG-01). */

const core = { primaryKey: 7, title: "Core" } as unknown as ServerModPackOption;
const extended = { primaryKey: 8, title: "Extended" } as unknown as ServerModPackOption;
const legacy = {
  primaryKey: 9,
  title: "Legacy",
  isAvailable: false,
  unavailableReason: "Two mods were removed from the catalog.",
} as unknown as ServerModPackOption;

/** Builds the server the Settings section reads, with pack 7 "Core" saved by default. */
function settingsServer(overrides: Record<string, unknown> = {}): GameServer {
  return {
    ...fleetServer({ title: "Alpha Team", gameIdentifier: "arma3" }),
    primaryKey: "server",
    selectedModPackDefinitionId: 7,
    selectedModPackDefinitionTitle: "Core",
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    canModify: true,
    canManageSecrets: false,
    ...overrides,
  } as unknown as GameServer;
}

/** Mounts the Settings section for `server`. */
function mount(server: GameServer = settingsServer()) {
  return renderWithProviders(<SettingsSection server={server} jobs={[]} />);
}

/** Mocks every service call the Settings section makes; the mod pack calls are set per test. */
function mockServices(
  options: ServerModPackOption[] = [core, extended, legacy],
  capability: ServerCapability = createCapability({ requiresModPack: false }),
): void {
  vi.spyOn(hostingService, "capabilities").mockResolvedValue([capability]);
  vi.spyOn(hostingService, "modPackOptions").mockResolvedValue(options);
  vi.spyOn(hostingService, "modPackContents").mockImplementation(async (_serverId, id) => ({
    modPackDefinitionId: id,
    isAvailable: true,
    unavailableReason: null,
    total: 0,
    items: [],
  }));
  vi.spyOn(hostingService, "changeModPack").mockResolvedValue(undefined);
  vi.spyOn(hostingService, "configuration").mockResolvedValue({
    serverId: "server",
    profileId: null,
    profileRevisionId: null,
    profileRevisionNumber: null,
    isComplete: true,
    entries: [],
  } as unknown as GameConfiguration);
  vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
  vi.spyOn(hostingService, "files").mockResolvedValue([]);
  vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as never);
  vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
}

/** The optional chooser, found by its label. */
async function chooser(): Promise<HTMLSelectElement> {
  return (await screen.findByLabelText("Mod pack")) as HTMLSelectElement;
}

/** The labels of the options of a select, in order. */
function optionLabels(select: HTMLSelectElement): string[] {
  return Array.from(select.options).map((option) => option.textContent ?? "");
}

describe("Mods panel", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockServices();
  });

  it("shows Mods before Configuration with the chooser and the currently applied line", async () => {
    mount();

    const mods = await screen.findByRole("heading", { level: 2, name: "Mods" });
    const configuration = await screen.findByRole("heading", { level: 2, name: "Configuration" });
    expect(
      mods.compareDocumentPosition(configuration) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    const select = await chooser();
    expect(select).toHaveValue("7");
    expect(optionLabels(select)).toEqual([
      "No mod pack",
      "Core (current)",
      "Extended",
      "Legacy (not available)",
    ]);
    expect(within(select).getByRole("option", { name: "Legacy (not available)" })).toBeDisabled();
    expect(within(select).getByRole("option", { name: "Core (current)" })).toHaveAttribute(
      "lang",
      "en",
    );
    const line = screen.getByText(/^Currently applied:/);
    expect(line).toHaveTextContent("Currently applied: Core");
    expect(within(line).getByText("Core")).toHaveAttribute("lang", "en");
    expect(hostingService.changeModPack).not.toHaveBeenCalled();
  });

  it("stages another pack at once and removes the change when the saved pack is chosen again", async () => {
    const user = userEvent.setup();
    mount();
    const select = await chooser();

    await user.selectOptions(select, "8");

    expect(screen.getByText(/^Staged:/).textContent).toBe(
      "Staged: Extended. It takes effect the next time the server starts.",
    );
    const bar = await screen.findByRole("region", { name: "Pending changes" });
    expect(
      within(bar).getByText("1 change staged. Nothing is saved until you apply it."),
    ).toBeVisible();
    const panel = select.closest("section");
    expect(within(panel as HTMLElement).getByText("1 changed")).toBeVisible();
    expect(hostingService.changeModPack).not.toHaveBeenCalled();

    await user.selectOptions(select, "7");

    expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
    expect(screen.queryByText(/^Staged:/)).not.toBeInTheDocument();
  });

  it("reviews the pack change with Before and After and applies it once", async () => {
    const user = userEvent.setup();
    mount();
    await user.selectOptions(await chooser(), "8");

    await user.click(screen.getByRole("button", { name: "Review changes" }));

    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    const item = within(dialog)
      .getByRole("button", { name: "Remove from changes: Mod pack" })
      .closest("li");
    expect(item).not.toBeNull();
    const row = within(item as HTMLElement);
    expect(row.getByText("Before").nextElementSibling).toHaveTextContent("Core");
    expect(row.getByText("After").nextElementSibling).toHaveTextContent("Extended");
    expect(hostingService.changeModPack).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() => expect(hostingService.changeModPack).toHaveBeenCalledTimes(1));
    expect(hostingService.changeModPack).toHaveBeenCalledWith("server", 8);
  });

  it("stages No mod pack when the game does not require a pack", async () => {
    const user = userEvent.setup();
    mount();
    await user.selectOptions(await chooser(), "");

    expect(screen.getByText(/^Staged:/).textContent).toBe(
      "Staged: No mod pack. It takes effect the next time the server starts.",
    );
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));
    await waitFor(() => expect(hostingService.changeModPack).toHaveBeenCalledWith("server", null));
  });

  it("leaves out No mod pack when the capability requires a pack", async () => {
    mockServices([core, extended], createCapability({ requiresModPack: true }));
    mount();

    expect(optionLabels(await chooser())).toEqual(["Core (current)", "Extended"]);
  });

  it("renders neither the panel nor a mod pack request when the capability has no mod packs", async () => {
    mockServices([core], createCapability({ supportsModPacks: false }));
    mount();

    await screen.findByRole("heading", { level: 2, name: "Settings" });
    await waitFor(() => expect(hostingService.capabilities).toHaveBeenCalled());
    expect(screen.queryByRole("heading", { level: 2, name: "Mods" })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Mod pack")).not.toBeInTheDocument();
    expect(hostingService.modPackOptions).not.toHaveBeenCalled();
  });

  it("treats an option without availability data as selectable and matches the game ignoring case", async () => {
    mockServices(
      [core, { primaryKey: 8, title: "Extended" } as unknown as ServerModPackOption],
      createCapability({ gameIdentifier: "ARMA3", requiresModPack: false }),
    );
    mount();

    const select = await chooser();
    expect(within(select).getByRole("option", { name: "Extended" })).toBeEnabled();
  });

  it("keeps the saved pack selectable when it is no longer available", async () => {
    mockServices([{ ...legacy, primaryKey: 7, title: "Core" } as ServerModPackOption, extended]);
    mount();

    const select = await chooser();
    expect(select).toHaveValue("7");
    expect(within(select).getByRole("option", { name: "Core (current)" })).toBeEnabled();
  });

  it("shows the select disabled with the read-only notice for a viewer", async () => {
    mount(settingsServer({ canModify: false }));

    const select = await chooser();
    expect(select).toBeDisabled();
    expect(screen.getByText("You can see these settings but not change them.")).toBeVisible();
  });

  it("shows the loading copy while the mod packs load", async () => {
    vi.spyOn(hostingService, "modPackOptions").mockReturnValue(new Promise(() => undefined));
    mount();

    expect(await screen.findByText("Loading mod packs…")).toBeVisible();
    expect(screen.queryByLabelText("Mod pack")).not.toBeInTheDocument();
  });

  it("shows the load error with Try again and retries the request", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "modPackOptions").mockRejectedValueOnce(new Error("boom"));
    mount();

    expect(await screen.findByText("The mod packs could not be loaded.")).toBeVisible();
    expect(screen.queryByText("boom")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Try again" }));

    expect(await chooser()).toBeVisible();
    expect(hostingService.modPackOptions).toHaveBeenCalledTimes(2);
  });

  it("shows the empty copy and no chooser when the game has no mod packs", async () => {
    mockServices([]);
    mount();

    expect(await screen.findByText("No mod packs are available for this game.")).toBeVisible();
    expect(screen.queryByLabelText("Mod pack")).not.toBeInTheDocument();
  });
});

describe("Mods panel as a collapsible Settings panel", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockServices();
  });

  it("renders section#server-mods with the Mods heading and the saved pack title in a bdi summary", async () => {
    mount();

    const heading = await screen.findByRole("heading", { level: 2, name: "Mods" });
    const section = heading.closest("section") as HTMLElement;
    expect(section).toHaveAttribute("id", "server-mods");
    const summary = within(section).getByText("Core", { selector: "bdi" });
    expect(summary.closest(".disclosure__summary-line")).not.toBeNull();
    expect(within(section).queryByText(/changed$/)).not.toBeInTheDocument();
  });

  it("summarizes a server without a pack with the no-pack label", async () => {
    mount(
      settingsServer({ selectedModPackDefinitionId: null, selectedModPackDefinitionTitle: null }),
    );

    const heading = await screen.findByRole("heading", { level: 2, name: "Mods" });
    const section = heading.closest("section") as HTMLElement;
    expect(within(section).getByText("No mod pack", { selector: "bdi" })).toBeVisible();
  });

  it("replaces the summary with the staged title and shows the change-count badge while staged", async () => {
    const user = userEvent.setup();
    mount();
    const heading = await screen.findByRole("heading", { level: 2, name: "Mods" });
    const section = heading.closest("section") as HTMLElement;

    await user.selectOptions(await chooser(), "8");

    expect(within(section).getByText("Extended", { selector: "bdi" })).toBeVisible();
    expect(within(section).queryByText("Core", { selector: "bdi" })).not.toBeInTheDocument();
    expect(within(section).getByText("1 changed")).toBeVisible();

    await user.selectOptions(await chooser(), "7");

    expect(within(section).getByText("Core", { selector: "bdi" })).toBeVisible();
    expect(within(section).queryByText("1 changed")).not.toBeInTheDocument();
  });

  it("keeps the content mounted while collapsed and opens the panel when a pack is staged", async () => {
    clearSettingsPanels();
    const user = userEvent.setup();
    mount();
    const heading = await screen.findByRole("heading", { level: 2, name: "Mods" });
    const toggle = within(heading).getByRole("button", { name: "Mods" });
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    const select = await chooser();
    expect(select).not.toBeVisible();

    await user.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    await user.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(await chooser()).toBe(select);

    // The select is hidden inside the collapsed panel; its mounted state is what a staged change goes through.
    await user.selectOptions(select, "8");
    await waitFor(() => expect(toggle).toHaveAttribute("aria-expanded", "true"));
    expect(select).toBeVisible();
  });

  it("renders nothing and requests no pack for a game without mod packs", async () => {
    mockServices([core], createCapability({ supportsModPacks: false }));
    mount();

    await waitFor(() => expect(hostingService.capabilities).toHaveBeenCalled());
    expect(document.getElementById("server-mods")).toBeNull();
    expect(hostingService.modPackOptions).not.toHaveBeenCalled();
  });

  it("leaves the shared select end padding to the form field surface", () => {
    const mods = readFileSync(resolve(__dirname, "../../src/components/server/mods.css"), "utf8");
    const fields = readFileSync(
      resolve(__dirname, "../../src/components/server/form-fields.css"),
      "utf8",
    );
    expect(mods).not.toMatch(/mods-panel[^{]*select/);
    expect(mods).not.toMatch(/padding-inline-end|padding-right/);
    expect(mods).not.toContain("workspace-panel__head");
    expect(fields).toMatch(
      /\.form-field__control--select\s*\{[^}]*padding-inline-end:\s*var\(--utf-space-xl\)/,
    );
  });
});

/** The mods of a pack, titled `Mod 01`, `Mod 02` and so on, with the key `workshop:<number>`. */
function packMods(...numbers: number[]): { key: string; title: string }[] {
  return numbers.map((number) => ({
    key: `workshop:${number}`,
    title: `Mod ${String(number).padStart(2, "0")}`,
  }));
}

/** Serves the contents of the given packs; any other pack id answers with an empty available pack. */
function serveContents(
  contents: Record<number, { key: string; title: string }[]>,
  overrides: Record<number, Record<string, unknown>> = {},
): void {
  vi.mocked(hostingService.modPackContents).mockImplementation(async (_serverId, id) => ({
    modPackDefinitionId: id,
    isAvailable: true,
    unavailableReason: null,
    total: (contents[id] ?? []).length,
    items: contents[id] ?? [],
    ...overrides[id],
  }));
}

/** Finds the title element of a disclosure by its full text, because pack titles sit in their own elements. */
async function disclosureTitle(text: string): Promise<HTMLElement> {
  return screen.findByText(
    (_content, element) =>
      element?.classList.contains("disclosure__title") === true && element.textContent === text,
  );
}

describe("Mods panel contents and unavailable packs", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockServices();
  });

  it("keeps the contents disclosure collapsed and lists the first ten mods with Show all and Show fewer", async () => {
    const user = userEvent.setup();
    serveContents({ 7: packMods(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12) });
    mount();

    const title = await disclosureTitle("Contents of Core (12)");
    expect(within(title).getByText("Core")).toHaveAttribute("lang", "en");
    expect(screen.getByText("Mod 01")).not.toBeVisible();

    await user.click(title);

    expect(screen.getByText("Mod 10")).toBeVisible();
    expect(screen.queryByText("Mod 11")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Show all 12" }));
    expect(screen.getByText("Mod 12")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Show fewer" }));
    expect(screen.queryByText("Mod 11")).not.toBeInTheDocument();
  });

  it("shows the contents of the staged pack instead of the saved pack", async () => {
    const user = userEvent.setup();
    serveContents({ 7: packMods(1, 2), 8: packMods(2, 3, 4) });
    mount();
    await disclosureTitle("Contents of Core (2)");

    await user.selectOptions(await chooser(), "8");

    await disclosureTitle("Contents of Extended (3)");
  });

  it("explains an empty pack with one line", async () => {
    serveContents({ 7: [] });
    mount();

    expect(await screen.findByText("This mod pack has no mods.")).toBeVisible();
  });

  it("shows the loading line while the contents of the effective pack load", async () => {
    vi.mocked(hostingService.modPackContents).mockReturnValue(new Promise(() => undefined));
    mount();

    expect(await screen.findByText("Loading the contents…")).toBeVisible();
  });

  it("shows the failed and the not available lines without the error text", async () => {
    const user = userEvent.setup();
    vi.mocked(hostingService.modPackContents).mockImplementation(async (_serverId, id) => {
      if (id === 7) throw new Error("boom");
      return {
        modPackDefinitionId: id,
        isAvailable: false,
        unavailableReason: null,
        total: 0,
        items: [],
      };
    });
    mount();
    expect(await screen.findByText("The contents could not be loaded.")).toBeVisible();
    expect(screen.queryByText("boom")).not.toBeInTheDocument();

    await user.selectOptions(await chooser(), "8");

    expect(
      await screen.findByText("The contents of this mod pack are not available."),
    ).toBeVisible();
  });

  it("lists unavailable packs with their API reason or the fallback and omits the list when all are available", async () => {
    const user = userEvent.setup();
    const noReason = {
      primaryKey: 10,
      title: "Retired",
      isAvailable: false,
      unavailableReason: null,
    } as unknown as ServerModPackOption;
    mockServices([core, extended, legacy, noReason]);
    const { unmount } = mount();

    await user.click(await screen.findByText("Packs that are not available (2)"));

    const legacyRow = screen.getByText("Legacy", { selector: "[lang='en']" }).closest("li");
    expect(
      within(legacyRow as HTMLElement).getByText("Two mods were removed from the catalog."),
    ).toHaveAttribute("lang", "en");
    const retiredRow = screen.getByText("Retired", { selector: "[lang='en']" }).closest("li");
    expect(
      within(retiredRow as HTMLElement).getByText("This pack cannot be used right now."),
    ).toBeVisible();

    unmount();
    mockServices([core, extended]);
    mount();
    await chooser();
    expect(screen.queryByText(/^Packs that are not available/)).not.toBeInTheDocument();
  });
});

describe("Mod pack contents diff in the review", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockServices();
  });

  /** Stages the pack with `id` and opens the review dialog. */
  async function openReview(user: ReturnType<typeof userEvent.setup>, id = "8") {
    await user.selectOptions(await chooser(), id);
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    return screen.findByRole("dialog", { name: "Review changes" });
  }

  it("shows Added, Removed and Unchanged with counts and keeps Unchanged collapsed behind Show all", async () => {
    const user = userEvent.setup();
    serveContents({ 7: packMods(1, 2), 8: packMods(2, 3) });
    mount();
    const dialog = await openReview(user);

    await user.click(await within(dialog).findByRole("button", { name: "Show 2 mod changes" }));
    const added = await within(dialog).findByText("Added (1)");
    expect(within(added.parentElement as HTMLElement).getByText("Mod 03")).toBeVisible();
    const removed = within(dialog).getByText("Removed (1)");
    expect(within(removed.parentElement as HTMLElement).getByText("Mod 01")).toBeVisible();
    const unchanged = within(dialog).getByText("Unchanged (1)");
    expect(within(unchanged.parentElement as HTMLElement).queryByText("Mod 02")).toBeNull();

    await user.click(within(dialog).getByRole("button", { name: "Show all 1" }));
    expect(within(unchanged.parentElement as HTMLElement).getByText("Mod 02")).toBeVisible();
  });

  it("lists the first ten added mods and offers Show all", async () => {
    const user = userEvent.setup();
    serveContents({ 7: [], 8: packMods(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12) });
    mount();
    const dialog = await openReview(user);

    await user.click(await within(dialog).findByRole("button", { name: "Show 12 mod changes" }));
    const added = await within(dialog).findByText("Added (12)");
    const group = added.parentElement as HTMLElement;
    expect(within(group).getByText("Mod 10")).toBeVisible();
    expect(within(group).queryByText("Mod 11")).toBeNull();
    await user.click(within(group).getByRole("button", { name: "Show all 12" }));
    expect(within(group).getByText("Mod 12")).toBeVisible();
  });

  it("blocks Apply with the reason while a contents request is pending and frees it afterwards", async () => {
    const user = userEvent.setup();
    let release: (value: Awaited<ReturnType<typeof hostingService.modPackContents>>) => void = () =>
      undefined;
    vi.mocked(hostingService.modPackContents).mockImplementation((_serverId, id) =>
      id === 8
        ? new Promise((resolve) => {
            release = resolve;
          })
        : Promise.resolve({
            modPackDefinitionId: id,
            isAvailable: true,
            unavailableReason: null,
            total: 1,
            items: packMods(1),
          }),
    );
    mount();
    const dialog = await openReview(user);

    // The item and the reason of the unavailable Apply share one key, so both lines are on screen.
    expect(await within(dialog).findAllByText("Comparing the mod pack contents…")).toHaveLength(2);
    const apply = within(dialog).getByRole("button", { name: "Apply 1 change" });
    expect(apply).toHaveAttribute("aria-disabled", "true");
    expect(apply).toHaveAccessibleDescription("Comparing the mod pack contents…");
    await user.click(apply);
    expect(hostingService.changeModPack).not.toHaveBeenCalled();

    release({
      modPackDefinitionId: 8,
      isAvailable: true,
      unavailableReason: null,
      total: 1,
      items: packMods(2),
    });

    await within(dialog).findByText("Added (1)");
    expect(within(dialog).getByRole("button", { name: "Apply 1 change" })).not.toHaveAttribute(
      "aria-disabled",
    );
  });

  it("says the contents could not be compared and keeps Apply available after a failed request", async () => {
    const user = userEvent.setup();
    vi.mocked(hostingService.modPackContents).mockImplementation(async (_serverId, id) => {
      if (id === 8) throw new Error("boom");
      return {
        modPackDefinitionId: id,
        isAvailable: true,
        unavailableReason: null,
        total: 0,
        items: [],
      };
    });
    mount();
    const dialog = await openReview(user);

    expect(
      await within(dialog).findByText(
        "The contents could not be compared. You can still apply this change.",
      ),
    ).toBeVisible();
    expect(within(dialog).queryByText(/^Added/)).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));
    await waitFor(() => expect(hostingService.changeModPack).toHaveBeenCalledWith("server", 8));
  });

  it("shows only the unavailable-contents line when a pack reports no contents", async () => {
    const user = userEvent.setup();
    serveContents({ 7: packMods(1), 8: [] }, { 8: { isAvailable: false } });
    mount();
    const dialog = await openReview(user);

    expect(
      await within(dialog).findByText("The contents of this mod pack are not available."),
    ).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Apply 1 change" })).not.toHaveAttribute(
      "aria-disabled",
    );
  });

  it("says both packs contain the same mods when the contents are equal", async () => {
    const user = userEvent.setup();
    serveContents({ 7: packMods(1, 2), 8: packMods(2, 1) });
    mount();
    const dialog = await openReview(user);

    expect(await within(dialog).findByText("Both packs contain the same mods.")).toBeVisible();
    expect(within(dialog).queryByText(/^Unchanged/)).not.toBeInTheDocument();
  });

  it("lists every current mod as removed for No mod pack and requests only the saved pack", async () => {
    const user = userEvent.setup();
    serveContents({ 7: packMods(1, 2) });
    mount();
    await disclosureTitle("Contents of Core (2)");
    const dialog = await openReview(user, "");

    await user.click(await within(dialog).findByRole("button", { name: "Show 2 mod changes" }));
    const removed = await within(dialog).findByText("Removed (2)");
    expect(within(removed.parentElement as HTMLElement).getByText("Mod 01")).toBeVisible();
    expect(within(dialog).queryByText(/^Added/)).not.toBeInTheDocument();
    const requested = vi.mocked(hostingService.modPackContents).mock.calls.map((call) => call[1]);
    expect(new Set(requested)).toEqual(new Set([7]));
  });
});

describe("Mod components source scan", () => {
  const root = resolve(import.meta.dirname, "..", "..");
  const files = [
    "src/components/server/ModPackPanel.tsx",
    "src/components/server/ModPackDiff.tsx",
    "src/features/hosting/modPackDiff.ts",
  ];

  it.each(files)("%s contains no game identifier literal and no raw select or button", (file) => {
    const source = readFileSync(resolve(root, file), "utf8");

    expect(source).not.toMatch(/arma3|aska|counterstrike2/i);
    expect(source).not.toMatch(/<select\b|<button\b/);
  });
});
