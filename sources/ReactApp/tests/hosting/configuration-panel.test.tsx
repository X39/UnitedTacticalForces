import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { ConfigurationEntryField } from "@/components/server/ConfigurationEntryField";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
import { hostingService } from "@/features/hosting/services";
import type { ConfigurationContributionProps } from "@/features/hosting/implementations/ConfigurationContribution";
import type {
  ConfigurationProfileSummary,
  GameConfiguration,
  GameConfigurationEntry,
  GameServer,
  StorageDemand,
} from "@/features/hosting/types";
import { configurationEntry } from "../helpers/configurationFixtures";
import { fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { clearSettingsPanels } from "../helpers/settingsPanels";

/* The Configuration panel: the API-declared Common and Advanced split, the entry field and its staging (CHG-02). */

const hostname = configurationEntry({
  key: "hostname",
  displayName: "Hostname",
  description: "Shown in the server browser.",
  tier: "Common",
  group: "Basics",
  value: "Old name",
  source: "Server",
  order: 1,
});
const maxPlayers = configurationEntry({
  key: "maxPlayers",
  displayName: "Maximum players",
  tier: "Common",
  group: "Basics",
  kind: "Integer",
  minimum: 1,
  maximum: 128,
  value: 64,
  order: 2,
});
const maxMsgSend = configurationEntry({
  key: "MaxMsgSend",
  displayName: "Maximum messages",
  description: "Messages sent per frame.",
  tier: "Advanced",
  group: "Network",
  kind: "Integer",
  minimum: 1,
  maximum: 512,
  value: 128,
  order: 1,
});

function configurationWith(
  entries: GameConfigurationEntry[],
  overrides: Partial<GameConfiguration> = {},
): GameConfiguration {
  return {
    serverId: "server",
    profileId: null,
    profileRevisionId: null,
    profileRevisionNumber: null,
    isComplete: true,
    entries,
    ...overrides,
  } as unknown as GameConfiguration;
}

/** Builds the server the Settings section reads. */
function settingsServer(
  canModify = true,
  gameIdentifier = "unknowngame",
  overrides: Record<string, unknown> = {},
): GameServer {
  return {
    ...fleetServer({ title: "Alpha Team", gameIdentifier }),
    primaryKey: "server",
    selectedModPackDefinitionId: null,
    selectedModPackDefinitionTitle: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    canModify,
    canManageSecrets: false,
    ...overrides,
  } as unknown as GameServer;
}

function mount(canModify = true, gameIdentifier = "unknowngame", overrides = {}) {
  return renderWithProviders(
    <SettingsSection server={settingsServer(canModify, gameIdentifier, overrides)} jobs={[]} />,
  );
}

/** Opens the Advanced disclosure through its summary control. */
async function openAdvanced(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByText("Advanced settings"));
  await waitFor(() => expect(screen.getByRole("group", { name: "Network" })).toBeVisible());
}

describe("Configuration panel tiers", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "configuration").mockResolvedValue(
      configurationWith([hostname, maxPlayers, maxMsgSend]),
    );
    vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "files").mockResolvedValue([]);
    vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as StorageDemand);
    vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
    vi.spyOn(hostingService, "updateConfiguration").mockResolvedValue(configurationWith([]));
  });

  it("shows Common settings first and hides the rest in a collapsed Advanced disclosure", async () => {
    mount();

    expect(await screen.findByRole("heading", { level: 3, name: "Common settings" })).toBeVisible();
    expect(screen.getByLabelText("Hostname")).toBeVisible();
    expect(screen.getByLabelText("Maximum players")).toBeVisible();
    expect(screen.getByText("Advanced settings")).toBeVisible();
    expect(screen.getByText("1 more setting")).toBeVisible();
    expect(screen.getByLabelText("Maximum messages")).not.toBeVisible();
  });

  it("opens Advanced when a change is staged inside it and offers Undo change", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByText("Advanced settings");
    await openAdvanced(user);

    const field = screen.getByLabelText("Maximum messages");
    await user.clear(field);
    await user.type(field, "256");

    expect(await screen.findByText("1 more setting, 1 changed")).toBeVisible();
    expect(screen.getByText("Changed")).toBeVisible();
    expect(screen.getByText("1 changed")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Undo change: Maximum messages" }));
    expect(screen.getByLabelText("Maximum messages")).toHaveValue(128);
    expect(screen.queryByText("Changed")).not.toBeInTheDocument();
    expect(screen.getByText("1 more setting")).toBeVisible();
  });

  it("stays closed after the member closes it until a new trigger occurs", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByText("Advanced settings");
    await openAdvanced(user);
    const field = screen.getByLabelText("Maximum messages");
    await user.clear(field);
    await user.type(field, "0");

    await user.click(screen.getByText("Advanced settings"));
    await waitFor(() => expect(screen.getByLabelText("Maximum messages")).not.toBeVisible());
    // Typing elsewhere is not a trigger inside Advanced: it stays closed.
    await user.type(screen.getByLabelText("Hostname"), "x");
    expect(screen.getByLabelText("Maximum messages")).not.toBeVisible();

    // A review attempt with an invalid value inside is a new trigger: it opens and focuses the field.
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    await waitFor(() => expect(screen.getByLabelText("Maximum messages")).toBeVisible());
    await waitFor(() => expect(screen.getByLabelText("Maximum messages")).toHaveFocus());
    expect(screen.getByLabelText("Maximum messages")).toHaveAttribute("aria-invalid", "true");
  });

  it("opens Advanced on render when a required entry inside has no value", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith([hostname, maxPlayers, { ...maxMsgSend, required: true, value: null }]),
    );
    mount();

    expect(await screen.findByLabelText("Maximum messages")).toBeVisible();
  });

  it("renders one flat grouped list when no entry is Common", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith([maxMsgSend, { ...hostname, tier: "Advanced" }]),
    );
    mount();

    expect(await screen.findByLabelText("Maximum messages")).toBeVisible();
    expect(screen.getByLabelText("Hostname")).toBeVisible();
    expect(screen.queryByText("Common settings")).not.toBeInTheDocument();
    expect(screen.queryByText("Advanced settings")).not.toBeInTheDocument();
  });

  it("treats an entry without a tier as Advanced", async () => {
    const withoutTier = { ...maxMsgSend } as Partial<GameConfigurationEntry>;
    delete withoutTier.tier;
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith([hostname, withoutTier as GameConfigurationEntry]),
    );
    mount();

    await screen.findByText("Advanced settings");
    expect(screen.getByLabelText("Maximum messages")).not.toBeVisible();
  });

  it("stages a reset to the inherited value with its staged line and Undo change", async () => {
    const user = userEvent.setup();
    mount();

    await user.click(await screen.findByRole("button", { name: "Reset to inherited: Hostname" }));

    expect(screen.getByText("Will use the inherited value after you apply.")).toBeVisible();
    expect(screen.getByText("Changed")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Undo change: Hostname" }));
    expect(
      screen.queryByText("Will use the inherited value after you apply."),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reset to inherited: Hostname" })).toBeVisible();
  });

  it("shows the source line and the description of each entry", async () => {
    mount();

    expect(await screen.findByText("From Server")).toBeVisible();
    expect(screen.getByText("Shown in the server browser.")).toBeVisible();
  });
});

describe("ConfigurationEntryField", () => {
  const noop = () => undefined;

  /** Renders the field with neutral callbacks. */
  function field(
    entry: GameConfigurationEntry,
    props: Partial<Parameters<typeof ConfigurationEntryField>[0]> = {},
  ) {
    return render(
      <ConfigurationEntryField
        entry={entry}
        value=""
        staged={false}
        resetStaged={false}
        disabled={false}
        onChange={noop}
        onBlur={noop}
        onUndo={noop}
        {...props}
      />,
    );
  }

  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("renders a boolean entry as a switch with Enabled and Disabled state words", () => {
    field(configurationEntry({ key: "flag", displayName: "Flag", kind: "Boolean" }), {
      value: "true",
    });

    const toggle = screen.getByRole("switch", { name: "Flag" });
    expect(toggle).toBeChecked();
    expect(screen.getByText("Enabled")).toBeInTheDocument();
    expect(screen.getByText("Disabled")).toBeInTheDocument();
  });

  it("renders a selection with Not set only when optional and the choice labels", () => {
    const entry = configurationEntry({
      key: "mode",
      displayName: "Mode",
      kind: "Selection",
      choices: [
        { value: "a", label: "Alpha", description: null },
        { value: "b", label: "Beta", description: null },
      ],
    });
    const rendered = field(entry);
    expect(
      within(screen.getByLabelText("Mode"))
        .getAllByRole("option")
        .map((o) => o.textContent),
    ).toEqual(["Not set", "Alpha", "Beta"]);
    rendered.unmount();

    field({ ...entry, required: true });
    expect(
      within(screen.getByLabelText("Mode"))
        .getAllByRole("option")
        .map((o) => o.textContent),
    ).toEqual(["Alpha", "Beta"]);
  });

  it("falls back to the allowed values of a selection without typed choices", () => {
    field(
      configurationEntry({
        key: "level",
        displayName: "Level",
        kind: "Selection",
        required: true,
        allowedValues: ["low", "high"],
      }),
    );

    expect(
      within(screen.getByLabelText("Level"))
        .getAllByRole("option")
        .map((o) => o.textContent),
    ).toEqual(["low", "high"]);
  });

  it("renders a string array as a multi-line field and a number with its limits", () => {
    const rendered = field(
      configurationEntry({ key: "motd", displayName: "Message lines", kind: "StringArray" }),
      { value: "one\ntwo" },
    );
    const lines = screen.getByLabelText("Message lines");
    expect(lines.tagName).toBe("TEXTAREA");
    expect(lines).toHaveValue("one\ntwo");
    rendered.unmount();

    field(
      configurationEntry({
        key: "slots",
        displayName: "Slots",
        kind: "Integer",
        minimum: 1,
        maximum: 99,
      }),
    );
    const number = screen.getByLabelText("Slots");
    expect(number).toHaveAttribute("type", "number");
    expect(number).toHaveAttribute("min", "1");
    expect(number).toHaveAttribute("max", "99");
    expect(number).toHaveAttribute("step", "1");
  });

  it("allows fractions for a number entry", () => {
    field(configurationEntry({ key: "ratio", displayName: "Ratio", kind: "Number" }));

    expect(screen.getByLabelText("Ratio")).toHaveAttribute("step", "any");
  });

  it("renders the game contribution with invalid and describedBy wired", async () => {
    const Stub = vi.fn((props: ConfigurationContributionProps) => (
      <span data-described-by={props.describedBy}>contribution control</span>
    ));
    field(configurationEntry({ key: "special", displayName: "Special", description: "Help" }), {
      contribution: Stub,
      error: "Choose one of the listed values.",
    });

    expect(screen.getByText("contribution control")).toBeVisible();
    const received = Stub.mock.calls[0]?.[0];
    expect(received).toMatchObject({ invalid: true, required: false, allowUnset: false });
    const describedBy = received?.describedBy ?? "";
    for (const id of describedBy.split(" ")) expect(document.getElementById(id)).not.toBeNull();
    expect(screen.getByRole("alert")).toHaveTextContent("Choose one of the listed values.");
    expect(screen.getByText("Special")).toBeVisible();
  });

  it("shows a sensitive entry read-only and never stages it", async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    field(
      configurationEntry({
        key: "secretPassword",
        displayName: "Admin password",
        sensitive: true,
        isRedacted: true,
        source: "Server",
      }),
      { onChange, onReset: vi.fn() },
    );

    const input = screen.getByLabelText("Admin password");
    expect(input).toBeDisabled();
    expect(input).toHaveAttribute("type", "password");
    expect(input).toHaveValue("");
    expect(screen.getByText(/A value is stored\. Enter a new one to replace it\./)).toBeVisible();
    await user.type(input, "secret");
    expect(onChange).not.toHaveBeenCalled();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("renders the carried warning and wraps API text without trimming it", () => {
    field(
      configurationEntry({
        key: "risky",
        displayName: "Risky",
        warning: "This can break clients.",
      }),
    );

    expect(screen.getByText("This can break clients.")).toBeVisible();
    expect(screen.getByText("This can break clients.")).toHaveAttribute("lang", "en");
    expect(screen.getByText("Risky")).toHaveAttribute("lang", "en");
  });
});

const profiles: ConfigurationProfileSummary[] = [
  {
    primaryKey: "profile-a",
    title: "Alpha profile",
    gameIdentifier: "unknowngame",
    isArchived: false,
    latestRevisionNumber: 3,
    latestRevisionId: "rev-a3",
  },
  {
    primaryKey: "profile-b",
    title: "Beta profile",
    gameIdentifier: "unknowngame",
    isArchived: false,
    latestRevisionNumber: 1,
    latestRevisionId: null,
  },
];

describe("Configuration panel profile and states", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "configuration").mockResolvedValue(
      configurationWith([hostname, maxPlayers]),
    );
    vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue(profiles);
    vi.spyOn(hostingService, "files").mockResolvedValue([]);
    vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as StorageDemand);
    vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
    vi.spyOn(hostingService, "updateConfiguration").mockResolvedValue(configurationWith([]));
    vi.spyOn(hostingService, "selectConfigurationProfile").mockResolvedValue(undefined);
  });

  it("stages a chosen profile without a request and offers the revision modes", async () => {
    const user = userEvent.setup();
    mount();
    const select = await screen.findByLabelText("Configuration profile");
    expect(
      within(select)
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["Game defaults only", "Alpha profile", "Beta profile"]);
    expect(screen.queryByRole("group", { name: "Revision" })).not.toBeInTheDocument();

    await user.selectOptions(select, "profile-a");

    expect(hostingService.selectConfigurationProfile).not.toHaveBeenCalled();
    const revision = screen.getByRole("group", { name: "Revision" });
    expect(
      within(revision).getByRole("radio", { name: "Follow the latest revision" }),
    ).toBeChecked();
    expect(within(revision).getByRole("radio", { name: "Stay on revision 3" })).not.toBeChecked();
    expect(screen.getByText("1 changed")).toBeVisible();
  });

  it("stages the latest revision of a newly chosen profile and applies it before the settings", async () => {
    const user = userEvent.setup();
    mount();
    await user.selectOptions(await screen.findByLabelText("Configuration profile"), "profile-a");
    await user.click(screen.getByRole("radio", { name: "Stay on revision 3" }));
    const field = screen.getByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "80");

    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    expect(within(dialog).getByText("Alpha profile, revision 3")).toBeVisible();
    await user.click(within(dialog).getByRole("button", { name: "Apply 2 changes" }));

    await waitFor(() => expect(hostingService.updateConfiguration).toHaveBeenCalledTimes(1));
    expect(hostingService.selectConfigurationProfile).toHaveBeenCalledWith(
      "server",
      "profile-a",
      "rev-a3",
    );
    expect(
      vi.mocked(hostingService.selectConfigurationProfile).mock.invocationCallOrder[0],
    ).toBeLessThan(vi.mocked(hostingService.updateConfiguration).mock.invocationCallOrder[0] ?? 0);
  });

  it("removes the staged profile when the saved profile is chosen again", async () => {
    const user = userEvent.setup();
    mount();
    const select = await screen.findByLabelText("Configuration profile");

    await user.selectOptions(select, "profile-a");
    expect(screen.getByRole("region", { name: "Pending changes" })).toBeVisible();
    await user.selectOptions(select, "");

    expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "Revision" })).not.toBeInTheDocument();
  });

  it("pins the saved profile to the current resolved revision", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith([hostname], {
        profileId: "profile-a",
        profileRevisionId: "rev-a2",
        profileRevisionNumber: 2,
      }),
    );
    const user = userEvent.setup();
    mount(true, "unknowngame", { selectedConfigurationProfileId: "profile-a" });
    const stay = await screen.findByRole("radio", { name: "Stay on revision 2" });
    expect(screen.getByRole("radio", { name: "Follow the latest revision" })).toBeChecked();

    await user.click(stay);
    expect(stay).toBeChecked();
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() =>
      expect(hostingService.selectConfigurationProfile).toHaveBeenCalledWith(
        "server",
        "profile-a",
        "rev-a2",
      ),
    );
  });

  it("keeps the saved pinned revision and unstages when the saved mode is chosen again", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith([hostname], {
        profileId: "profile-a",
        profileRevisionId: "rev-a2",
        profileRevisionNumber: 2,
      }),
    );
    const user = userEvent.setup();
    mount(true, "unknowngame", {
      selectedConfigurationProfileId: "profile-a",
      pinnedConfigurationProfileRevisionId: "rev-a2",
    });

    expect(await screen.findByRole("radio", { name: "Stay on revision 2" })).toBeChecked();
    await user.click(screen.getByRole("radio", { name: "Follow the latest revision" }));
    expect(screen.getByRole("region", { name: "Pending changes" })).toBeVisible();
    await user.click(screen.getByRole("radio", { name: "Stay on revision 2" }));

    expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
  });

  it("makes Stay on revision unavailable with its reason when the profile has no revision id", async () => {
    const user = userEvent.setup();
    mount();

    await user.selectOptions(await screen.findByLabelText("Configuration profile"), "profile-b");

    const stay = screen.getByRole("radio", { name: "Stay on revision 1" });
    expect(stay).toBeDisabled();
    expect(screen.getByText("This profile has no revision to stay on yet.")).toBeVisible();
    expect(stay).toHaveAccessibleDescription("This profile has no revision to stay on yet.");
  });

  it("replaces the group of a hand-edited generated file with a warning naming the file", async () => {
    vi.mocked(hostingService.files).mockResolvedValue([
      { path: "server/server.cfg", overridesGenerated: true } as never,
    ]);
    mount();

    expect(
      await screen.findByText("server/server.cfg was edited by hand and overrides these settings."),
    ).toBeVisible();
    expect(screen.queryByLabelText("Hostname")).not.toBeInTheDocument();
  });

  it("shows the carried warning for an incomplete configuration", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith([hostname], { isComplete: false }),
    );
    mount();

    expect(
      await screen.findByText(
        "Required values are missing or invalid. Start and generated downloads remain blocked.",
      ),
    ).toBeVisible();
  });

  it("names the written file once per target under the first legend inside Advanced", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith([
        hostname,
        { ...maxMsgSend, key: "a", displayName: "First", group: "Network", order: 1 },
        { ...maxMsgSend, key: "b", displayName: "Second", group: "Extras", order: 2 },
        {
          ...maxMsgSend,
          key: "c",
          displayName: "Plane",
          group: "Runtime",
          isControlPlaneOnly: true,
          targetPath: "",
        },
      ]),
    );
    const user = userEvent.setup();
    mount();
    await screen.findByText("Advanced settings");
    await user.click(screen.getByText("Advanced settings"));

    expect(await screen.findAllByText("Written to server/server.cfg")).toHaveLength(1);
    const network = screen.getByRole("group", { name: "Network" });
    expect(within(network).getByText("Written to server/server.cfg")).toBeVisible();
    expect(screen.getByText("Deployment settings")).toBeVisible();
    // The Common part never shows a caption.
    expect(
      within(screen.getByRole("group", { name: "Basics" })).queryByText(/Written to/),
    ).not.toBeInTheDocument();
  });

  it("shows the loading text while the configuration loads", async () => {
    vi.mocked(hostingService.configuration).mockReturnValue(new Promise(() => undefined));
    mount();

    expect(await screen.findByText("Loading configuration…")).toBeVisible();
  });

  it("shows a load error without the raw error text and retries", async () => {
    vi.mocked(hostingService.configuration).mockRejectedValueOnce(new Error("boom token=abc"));
    const user = userEvent.setup();
    mount();

    expect(await screen.findByText("The configuration could not be loaded.")).toBeVisible();
    expect(screen.queryByText(/boom/)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Try again" }));

    expect(await screen.findByLabelText("Hostname")).toBeVisible();
    expect(hostingService.configuration).toHaveBeenCalledTimes(2);
  });

  it("shows the empty game copy and still renders the profile choice", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(configurationWith([]));
    mount();

    expect(await screen.findByText("This game has no settings you can change.")).toBeVisible();
    expect(screen.getByLabelText("Configuration profile")).toBeVisible();
    expect(screen.queryByText("Common settings")).not.toBeInTheDocument();
  });

  it("disables every control for a viewer without Modify rights", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith([hostname, maxPlayers], { profileId: "profile-a" }),
    );
    mount(false, "unknowngame", { selectedConfigurationProfileId: "profile-a" });

    expect(await screen.findByLabelText("Hostname")).toBeDisabled();
    expect(screen.getByLabelText("Maximum players")).toBeDisabled();
    expect(screen.getByLabelText("Configuration profile")).toBeDisabled();
    for (const radio of screen.getAllByRole("radio")) expect(radio).toBeDisabled();
    expect(screen.queryByRole("button", { name: /^Reset to inherited/ })).not.toBeInTheDocument();
  });

  it("wraps long API text and never ellipsizes it", async () => {
    const longText = "Word ".repeat(60).trim();
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith([{ ...hostname, description: longText, warning: longText }]),
    );
    mount();

    const texts = await screen.findAllByText(longText);
    expect(texts.length).toBeGreaterThanOrEqual(2);
    for (const text of texts) expect(text).toHaveAttribute("lang", "en");
    const css = readFileSync(
      resolve(import.meta.dirname, "..", "..", "src", "components", "server", "configuration.css"),
      "utf8",
    );
    expect(css).not.toMatch(/text-overflow\s*:\s*ellipsis/);
    expect(css).toMatch(/overflow-wrap:\s*anywhere/);
  });
});

/** Builds a catalog of the given size: the three base entries plus advanced filler entries named Filler N. */
function catalog(size: number): GameConfigurationEntry[] {
  const base = [hostname, maxPlayers, maxMsgSend];
  const fillers = Array.from({ length: Math.max(0, size - base.length) }, (_, index) =>
    configurationEntry({
      key: `filler${index}`,
      displayName: `Filler ${index + 1}`,
      description: index === 0 ? "Hidden treasure here." : "",
      tier: "Advanced",
      group: "Extras",
      order: 10 + index,
      value: "v",
    }),
  );
  return [...base, ...fillers].slice(0, size);
}

describe("Configuration search and filter chips", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "configuration").mockResolvedValue(configurationWith(catalog(8)));
    vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "files").mockResolvedValue([]);
    vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as StorageDemand);
    vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
  });

  const searchbox = () => screen.getByRole("searchbox", { name: "Search settings" });
  const visibleCount = () => document.querySelector<HTMLElement>(".configuration-filter__count");

  it("renders no toolbar with seven entries", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(configurationWith(catalog(7)));
    mount();

    expect(await screen.findByLabelText("Hostname")).toBeVisible();
    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "Filter settings" })).not.toBeInTheDocument();
  });

  it("renders the search, the three chips and the visible count with eight entries", async () => {
    mount();

    expect(await screen.findByRole("searchbox", { name: "Search settings" })).toBeVisible();
    const chips = screen.getByRole("group", { name: "Filter settings" });
    expect(within(chips).getByRole("button", { name: "All" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(within(chips).getByRole("button", { name: "Changed 0" })).toBeVisible();
    expect(within(chips).getByRole("button", { name: "Needs a value 0" })).toBeVisible();
    expect(screen.getAllByText("8 settings shown")).toHaveLength(2);
    // The toolbar comes after the profile choice and before the list.
    const profile = screen.getByLabelText("Configuration profile");
    expect(
      profile.compareDocumentPosition(searchbox()) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("shows one flat grouped list while a query is active and restores the split when cleared", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByText("Advanced settings");

    await user.type(searchbox(), "filler");

    expect(screen.queryByText("Advanced settings")).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 3, name: "Common settings" })).toBeNull();
    expect(screen.getByRole("group", { name: "Extras" })).toBeVisible();
    expect(screen.getByLabelText("Filler 1")).toBeVisible();
    expect(screen.queryByLabelText("Hostname")).not.toBeInTheDocument();
    expect(visibleCount()).toHaveTextContent("5 settings shown");

    await user.click(screen.getByRole("button", { name: "Clear search" }));

    expect(await screen.findByText("Advanced settings")).toBeVisible();
    expect(screen.getByRole("heading", { level: 3, name: "Common settings" })).toBeVisible();
    expect(screen.getByLabelText("Hostname")).toBeVisible();
  });

  it("matches a description and ignores case and blanks", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByText("Advanced settings");

    await user.type(searchbox(), "  HIDDEN TREASURE ");

    expect(screen.getByLabelText("Filler 1")).toBeVisible();
    expect(screen.queryByLabelText("Filler 2")).not.toBeInTheDocument();
    expect(visibleCount()).toHaveTextContent("1 setting shown");
  });

  it("filters through the Changed chip and keeps the staged value", async () => {
    const user = userEvent.setup();
    mount();
    await user.type(await screen.findByLabelText("Hostname"), "x");

    const chips = screen.getByRole("group", { name: "Filter settings" });
    await user.click(within(chips).getByRole("button", { name: "Changed 1" }));

    expect(screen.getByLabelText("Hostname")).toHaveValue("Old namex");
    expect(screen.queryByLabelText("Maximum players")).not.toBeInTheDocument();
    expect(visibleCount()).toHaveTextContent("1 setting shown");
    expect(screen.queryByText("Advanced settings")).not.toBeInTheDocument();
  });

  it("counts and lists entries that need a value", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith(
        catalog(8).map((entry) =>
          entry.key === "filler2" ? { ...entry, required: true, value: null } : entry,
        ),
      ),
    );
    const user = userEvent.setup();
    mount();

    const chips = await screen.findByRole("group", { name: "Filter settings" });
    await user.click(within(chips).getByRole("button", { name: "Needs a value 1" }));

    expect(screen.getByLabelText("Filler 3")).toBeVisible();
    expect(visibleCount()).toHaveTextContent("1 setting shown");
  });

  it("keeps the edited field and its focus under Needs a value while the member types", async () => {
    vi.mocked(hostingService.configuration).mockResolvedValue(
      configurationWith(
        catalog(8).map((entry) =>
          entry.key === "filler2" ? { ...entry, required: true, value: null } : entry,
        ),
      ),
    );
    const user = userEvent.setup();
    mount();

    const chips = await screen.findByRole("group", { name: "Filter settings" });
    await user.click(within(chips).getByRole("button", { name: "Needs a value 1" }));
    await user.type(screen.getByLabelText("Filler 3"), "abc");

    const field = screen.getByLabelText("Filler 3");
    expect(field).toHaveValue("abc");
    expect(field).toHaveFocus();
    // The count behind the chip stays live while the list stays put.
    expect(within(chips).getByRole("button", { name: "Needs a value 0" })).toBeVisible();

    await user.click(within(chips).getByRole("button", { name: "All" }));
    await user.click(within(chips).getByRole("button", { name: "Needs a value 0" }));
    expect(screen.queryByLabelText("Filler 3")).not.toBeInTheDocument();
  });

  it("keeps a row under Changed after its change is undone", async () => {
    const user = userEvent.setup();
    mount();
    await user.type(await screen.findByLabelText("Hostname"), "x");

    const chips = screen.getByRole("group", { name: "Filter settings" });
    await user.click(within(chips).getByRole("button", { name: "Changed 1" }));
    await user.click(screen.getByRole("button", { name: /Undo change/ }));

    expect(screen.getByLabelText("Hostname")).toHaveValue("Old name");
    expect(within(chips).getByRole("button", { name: "Changed 0" })).toBeVisible();
  });

  it("renders the empty state for no match and clears query and chip from its action", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByText("Advanced settings");

    await user.type(searchbox(), "zzzz");

    expect(screen.getByText("No settings match")).toBeVisible();
    expect(screen.getByText("Clear the search or choose All to see every setting.")).toBeVisible();
    const clears = screen.getAllByRole("button", { name: "Clear search" });
    await user.click(clears[clears.length - 1]!);

    expect(searchbox()).toHaveValue("");
    expect(await screen.findByLabelText("Hostname")).toBeVisible();
    expect(screen.queryByText("No settings match")).not.toBeInTheDocument();
  });

  it("keeps a staged value staged while its field is filtered out", async () => {
    const user = userEvent.setup();
    mount();
    await user.type(await screen.findByLabelText("Hostname"), "x");
    expect(screen.getAllByText("1 changed").length).toBeGreaterThan(0);

    await user.type(searchbox(), "filler");
    expect(screen.queryByLabelText("Hostname")).not.toBeInTheDocument();
    expect(screen.getAllByText("1 changed").length).toBeGreaterThan(0);

    await user.click(screen.getByRole("button", { name: "Clear search" }));
    expect(await screen.findByLabelText("Hostname")).toHaveValue("Old namex");
  });

  it("updates the visible count at once and settles the announced count after 300ms", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByText("Advanced settings");
    const live = () => document.querySelector<HTMLElement>(".configuration-filter [aria-live]");
    expect(live()).toHaveTextContent("8 settings shown");

    await user.type(searchbox(), "filler");

    const visible = document.querySelector<HTMLElement>(".configuration-filter__count");
    expect(visible).toHaveTextContent("5 settings shown");
    expect(live()).toHaveTextContent("8 settings shown");
    await waitFor(() => expect(live()).toHaveTextContent("5 settings shown"));
    expect(live()).toHaveAttribute("aria-live", "polite");
  });

  it("clears the filter on a review attempt when an invalid staged entry is filtered out", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByText("Advanced settings");
    await openAdvanced(user);
    const field = screen.getByLabelText("Maximum messages");
    await user.clear(field);
    await user.type(field, "0");

    await user.type(searchbox(), "hostname");
    expect(screen.queryByLabelText("Maximum messages")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Review changes" }));

    await waitFor(() => expect(searchbox()).toHaveValue(""));
    await waitFor(() => expect(screen.getByLabelText("Maximum messages")).toHaveFocus());
    expect(screen.getByLabelText("Maximum messages")).toHaveAttribute("aria-invalid", "true");
  });
});

/** The header button of the Configuration panel. */
function configurationHeader(): HTMLElement {
  const owner = document.querySelector<HTMLElement>('[data-settings-panel="server-configuration"]');
  const button = owner?.querySelector<HTMLElement>("h2 > button");
  if (!button) throw new Error("The Configuration panel is not rendered.");
  return button;
}

describe("Configuration as a collapsible Settings panel", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    clearSettingsPanels();
    vi.spyOn(hostingService, "configuration").mockResolvedValue(
      configurationWith([hostname, maxPlayers, maxMsgSend]),
    );
    vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue(profiles);
    vi.spyOn(hostingService, "files").mockResolvedValue([]);
    vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as StorageDemand);
    vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
  });

  it("renders the anchor, the heading and the summary once loaded, collapsed by default", async () => {
    mount();

    expect(await screen.findByText("Settings: 3")).toBeVisible();
    const owner = document.querySelector('[data-settings-panel="server-configuration"]');
    expect(owner).toHaveAttribute("id", "server-configuration");
    expect(screen.getByRole("heading", { level: 2, name: "Configuration" })).toBeVisible();
    expect(configurationHeader()).toHaveAttribute("aria-expanded", "false");
  });

  it("shows no count while loading and the loading state in the body", async () => {
    vi.mocked(hostingService.configuration).mockReturnValue(new Promise(() => undefined));
    mount();

    expect(await screen.findByRole("heading", { level: 2, name: "Configuration" })).toBeVisible();
    expect(screen.queryByText(/^Settings: /)).not.toBeInTheDocument();
    expect(screen.getByText("Loading configuration…")).not.toBeVisible();
  });

  it("keeps a failed load collapsed and retries inside the panel", async () => {
    vi.mocked(hostingService.configuration).mockRejectedValueOnce(new Error("boom"));
    const user = userEvent.setup();
    mount();

    const message = await screen.findByText("The configuration could not be loaded.");
    expect(message).not.toBeVisible();
    expect(screen.queryByText(/^Settings: /)).not.toBeInTheDocument();
    expect(configurationHeader()).toHaveAttribute("aria-expanded", "false");

    await user.click(configurationHeader());
    await user.click(screen.getByRole("button", { name: "Try again" }));

    expect(await screen.findByText("Settings: 3")).toBeVisible();
    expect(await screen.findByLabelText("Hostname")).toBeVisible();
  });

  it("opens a collapsed panel and shows the change badge when a value is staged", async () => {
    mount();
    await screen.findByText("Settings: 3");
    expect(configurationHeader()).toHaveAttribute("aria-expanded", "false");

    // The body stays mounted while collapsed, so a staged value can arrive without the member opening the panel.
    fireEvent.change(screen.getByLabelText("Hostname"), { target: { value: "New name" } });

    await waitFor(() => expect(configurationHeader()).toHaveAttribute("aria-expanded", "true"));
    expect(screen.getAllByText("1 changed").length).toBeGreaterThan(0);
  });

  it("opens a collapsed panel when a reset is staged", async () => {
    mount();
    await screen.findByText("Settings: 3");

    fireEvent.click(
      screen.getByRole("button", { name: "Reset to inherited: Hostname", hidden: true }),
    );

    await waitFor(() => expect(configurationHeader()).toHaveAttribute("aria-expanded", "true"));
    expect(screen.getAllByText("1 changed").length).toBeGreaterThan(0);
  });

  it("opens a collapsed panel when a profile change is staged", async () => {
    const user = userEvent.setup({ pointerEventsCheck: 0 });
    mount();
    await screen.findByText("Settings: 3");
    expect(configurationHeader()).toHaveAttribute("aria-expanded", "false");

    await user.selectOptions(screen.getByLabelText("Configuration profile"), "profile-a");

    await waitFor(() => expect(configurationHeader()).toHaveAttribute("aria-expanded", "true"));
    expect(screen.getAllByText("1 changed").length).toBeGreaterThan(0);
  });

  it("renders the revision mode as a segmented radio group", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByText("Settings: 3");
    await user.click(configurationHeader());
    await user.selectOptions(screen.getByLabelText("Configuration profile"), "profile-a");

    const revision = screen.getByRole("group", { name: "Revision" });
    expect(revision.closest('[data-layout="segmented"]') ?? revision).toHaveAttribute(
      "data-layout",
      "segmented",
    );
  });
});

const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");

/** The files of the Configuration panel that must stay free of raw controls, error text and game literals. */
const scannedFiles = [
  "features/hosting/ServerConfigurationPanel.tsx",
  "components/server/ConfigurationEntryField.tsx",
];

/** Removes block and line comments so documentation prose cannot trigger or hide a finding. */
function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
}

/**
 * Lists the forbidden patterns of a source text: a raw input, select, textarea or button element, the text of an
 * error, or a game identifier literal.
 *
 * @param source The source text to scan.
 * @returns The findings as `name: matched text`.
 */
function findViolations(source: string): string[] {
  const code = stripComments(source);
  const found: string[] = [];
  const raw = /<(input|select|textarea|button)\b/.exec(code);
  if (raw) found.push(`raw control: ${raw[0]}`);
  const message = /\.\s*message\b/.exec(code);
  if (message) found.push(`error text: ${message[0]}`);
  const game = /arma3|aska|counterstrike2/i.exec(code);
  if (game) found.push(`game literal: ${game[0]}`);
  return found;
}

describe("Configuration panel source scan", () => {
  it("finds each forbidden pattern in an inline bad sample, so the scan is live", () => {
    expect(findViolations('<input type="text" />')).toHaveLength(1);
    expect(findViolations("<select><option /></select>")).toHaveLength(1);
    expect(findViolations("<textarea rows={3} />")).toHaveLength(1);
    expect(findViolations("<button onClick={go}>Go</button>")).toHaveLength(1);
    expect(findViolations("setText(error.message);")).toHaveLength(1);
    expect(findViolations('const id = "arma3";')).toHaveLength(1);
    expect(findViolations("// <input /> and error.message and arma3 in a comment")).toHaveLength(0);
  });

  it.each(scannedFiles)("finds nothing forbidden in %s", (file) => {
    expect(findViolations(readFileSync(resolve(sourceRoot, file), "utf8"))).toEqual([]);
  });
});
