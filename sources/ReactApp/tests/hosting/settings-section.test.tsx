import { useQuery } from "@tanstack/react-query";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  GameConfiguration,
  GameServer,
  GameServerSecretStatus,
} from "@/features/hosting/types";
import { fleetServer, serverAutomation, type FleetServerOptions } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { clearSettingsPanels, seedSettingsPanels } from "../helpers/settingsPanels";

/* The Settings section as a stack of collapsible panels: summaries, Expand all, staged and invalid panels (D-09). */

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: { primaryKey: "10000000-0000-4000-8000-0000000000f1", nickname: "Falcon One" },
    isAuthenticated: true,
    isLoading: false,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

/** An access list with one member, for the tests that render the Access panel. */
const oneMemberAccess = {
  allowedPermissions: ["sv", "sv:view"],
  assignablePermissions: ["sv", "sv:view"],
  users: [
    {
      userId: "20000000-0000-4000-8000-0000000000a2",
      nickname: "Raven",
      detail: "#0000a2",
      permissions: ["sv:view"],
    },
  ],
  roles: [],
};

/** A saved credential the viewer may stage a value for. */
function secretStatus(overrides: Partial<GameServerSecretStatus>): GameServerSecretStatus {
  return {
    key: "gslt",
    displayName: "Game server login token",
    requiredForStart: true,
    generated: false,
    revealable: false,
    externalProvider: true,
    isConfigured: false,
    version: null,
    origin: null,
    timeStampCreated: null,
    acceptsChosenValue: true,
    valueFormat: "Token",
    ...overrides,
  };
}

const gslt = secretStatus({});
const rcon = secretStatus({
  key: "rcon-password",
  displayName: "RCON password",
  requiredForStart: false,
  generated: true,
  isConfigured: true,
  version: 3,
  origin: "Generated",
  acceptsChosenValue: false,
});

/** Builds the server the Settings section reads, with the rights the test needs. */
function settingsServer(
  options: FleetServerOptions = {},
  overrides: Record<string, unknown> = {},
): GameServer {
  return {
    ...fleetServer({ title: "Alpha Team", gameIdentifier: "counter-strike-2", ...options }),
    selectedModPackDefinitionId: null,
    selectedModPackDefinitionTitle: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    canModify: true,
    canManageSecrets: true,
    canRevealSecrets: false,
    canManageAutomation: true,
    automation: serverAutomation(),
    ...overrides,
  } as unknown as GameServer;
}

/** Mocks every hosting request the Settings section makes, with the given credential list. */
function mockHostingQueries(secrets: GameServerSecretStatus[] = [gslt, rcon]) {
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
  return vi.spyOn(hostingService, "secrets").mockResolvedValue(secrets);
}

/** Renders the section for the server. */
function renderSettings(server: GameServer = settingsServer()) {
  return renderWithProviders(<SettingsSection server={server} jobs={[]} />);
}

/** The header button of a panel by its anchor id. */
function panelHeader(id: string): HTMLElement {
  const owner = document.querySelector<HTMLElement>(`[data-settings-panel="${id}"]`);
  const button = owner?.querySelector<HTMLElement>("h2 > button");
  if (!button) throw new Error(`The panel ${id} is not rendered.`);
  return button;
}

/** The Expand all or Collapse all button of the heading row. */
const toggleAll = () => screen.getByRole("button", { name: /^(Expand all|Collapse all)$/ });

describe("Settings panels", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    clearSettingsPanels();
  });

  it("starts with every panel collapsed and a heading row with Expand all", async () => {
    mockHostingQueries();
    renderSettings();

    expect(await screen.findByText("Credentials: 2")).toBeInTheDocument();
    const settingsHeading = screen.getByRole("heading", { level: 2, name: "Settings" });
    expect(settingsHeading.parentElement).toContainElement(toggleAll());
    expect(toggleAll()).toHaveAccessibleName("Expand all");
    for (const id of ["server-credentials", "server-automation", "server-record"])
      expect(panelHeader(id)).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByText("Delete this server permanently")).toBeInTheDocument();
  });

  it("renders no credentials heading, summary or request without canManageSecrets", async () => {
    const secrets = mockHostingQueries();
    renderSettings(settingsServer({}, { canManageSecrets: false }));

    expect(await screen.findByRole("heading", { name: "Configuration" })).toBeVisible();
    expect(screen.queryByRole("heading", { name: "Credentials" })).not.toBeInTheDocument();
    expect(screen.queryByText(/^Credentials:/)).not.toBeInTheDocument();
    expect(document.querySelector('[data-settings-panel="server-credentials"]')).toBeNull();
    expect(secrets).not.toHaveBeenCalled();
  });

  it("shows no credentials summary while loading", async () => {
    mockHostingQueries();
    vi.spyOn(hostingService, "secrets").mockReturnValue(new Promise(() => undefined));
    renderSettings();

    expect(await screen.findByRole("heading", { name: "Credentials" })).toBeVisible();
    expect(screen.queryByText(/^Credentials:/)).not.toBeInTheDocument();
    expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "false");
  });

  it("keeps a failed credentials load collapsed, without a summary and with a retry inside", async () => {
    const user = userEvent.setup();
    const secrets = mockHostingQueries();
    secrets.mockRejectedValueOnce(new Error("secret backend exploded"));
    renderSettings();

    const message = await screen.findByText("The credentials could not be loaded.");
    expect(message).not.toBeVisible();
    expect(screen.queryByText(/^Credentials:/)).not.toBeInTheDocument();
    expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "false");

    await user.click(panelHeader("server-credentials"));
    secrets.mockResolvedValueOnce([gslt]);
    await user.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByText("Credentials: 1")).toBeInTheDocument();
  });

  it("swaps Expand all and Collapse all and opens or closes every rendered panel", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    renderSettings();
    await screen.findByText("Credentials: 2");

    await user.click(toggleAll());
    expect(toggleAll()).toHaveAccessibleName("Collapse all");
    for (const id of ["server-credentials", "server-automation", "server-record"])
      expect(panelHeader(id)).toHaveAttribute("aria-expanded", "true");

    await user.click(toggleAll());
    expect(toggleAll()).toHaveAccessibleName("Expand all");
    for (const id of ["server-credentials", "server-automation", "server-record"])
      expect(panelHeader(id)).toHaveAttribute("aria-expanded", "false");
  });

  it("opens and pins Credentials when a value is staged, and Collapse all keeps it open", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    renderSettings();
    await screen.findByText("Credentials: 2");
    await user.click(toggleAll());

    await user.type(screen.getByLabelText(`New value for ${gslt.displayName}`), "token-123");
    expect(within(panelHeader("server-credentials")).getByText("1 changed")).toBeInTheDocument();
    expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "true");

    await user.click(toggleAll());
    expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "true");
    expect(panelHeader("server-automation")).toHaveAttribute("aria-expanded", "false");
    expect(panelHeader("server-record")).toHaveAttribute("aria-expanded", "false");
    // Only the pinned panel is open, so nothing collapsible is open and the button offers Expand all.
    expect(toggleAll()).toHaveAccessibleName("Expand all");
  });

  it("opens a collapsed Credentials panel and focuses the invalid input when a review fails", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    renderSettings();
    await screen.findByText("Credentials: 2");
    await user.click(panelHeader("server-credentials"));
    await user.type(screen.getByLabelText(`New value for ${gslt.displayName}`), "bad token");
    // The member closes the panel; the staged change stays and so does the pending bar.
    await user.click(panelHeader("server-credentials"));
    expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "false");

    await user.click(screen.getByRole("button", { name: "Review changes" }));

    await waitFor(() =>
      expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "true"),
    );
    await waitFor(() =>
      expect(screen.getByLabelText(`New value for ${gslt.displayName}`)).toHaveFocus(),
    );
    expect(screen.getByLabelText(`New value for ${gslt.displayName}`)).toHaveAttribute(
      "aria-invalid",
      "true",
    );
  });

  it("shows the Server record panel with its summary, Delete server and the confirmation inside", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    renderSettings();
    await screen.findByText("Credentials: 2");
    expect(screen.getByText("Delete this server permanently")).toBeInTheDocument();

    await user.click(panelHeader("server-record"));
    await user.click(screen.getByRole("button", { name: "Delete server" }));

    expect(await screen.findByRole("dialog", { name: "Delete server" })).toBeVisible();
  });

  it("renders no Server record panel without canModify or on an archived server", async () => {
    mockHostingQueries();
    const { unmount } = renderSettings(settingsServer({}, { canModify: false }));
    await screen.findByText("Credentials: 2");
    expect(document.querySelector('[data-settings-panel="server-record"]')).toBeNull();
    unmount();

    renderSettings(settingsServer({}, { isArchived: true }));
    await screen.findByText("Credentials: 2");
    expect(document.querySelector('[data-settings-panel="server-record"]')).toBeNull();
  });

  it("renders Access and Cost owner after Automation and before the Server record", async () => {
    mockHostingQueries();
    // The mod pack panel exists only for games whose capability supports mod packs.
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([
      { gameIdentifier: "counter-strike-2", supportsModPacks: true } as never,
    ]);
    vi.spyOn(hostingService, "modPackOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(oneMemberAccess);
    renderSettings(settingsServer({}, { canManageAccess: true }));

    expect(await screen.findByText("Access: 1")).toBeInTheDocument();
    await waitFor(() =>
      expect(document.querySelector('[data-settings-panel="server-mods"]')).not.toBeNull(),
    );
    const panelIds = [...document.querySelectorAll("[data-settings-panel]")].map((panel) =>
      panel.getAttribute("data-settings-panel"),
    );
    expect(panelIds).toEqual([
      "server-mods",
      "server-configuration",
      "server-credentials",
      "server-automation",
      "server-access",
      "server-cost-owner",
      "server-record",
    ]);
    expect(panelHeader("server-access")).toHaveAttribute("aria-expanded", "false");
    expect(panelHeader("server-cost-owner")).toHaveAttribute("aria-expanded", "false");
  });

  it("keeps the Access panel and drops the Cost owner panel on an archived server", async () => {
    mockHostingQueries();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(oneMemberAccess);
    renderSettings(settingsServer({}, { canManageAccess: true, isArchived: true }));

    expect(await screen.findByText("Access: 1")).toBeInTheDocument();
    expect(document.querySelector('[data-settings-panel="server-access"]')).not.toBeNull();
    expect(document.querySelector('[data-settings-panel="server-cost-owner"]')).toBeNull();
  });

  it("shows the Cost owner panel to a viewer who cannot modify the server", async () => {
    mockHostingQueries();
    renderSettings(settingsServer({}, { canModify: false }));

    expect(await screen.findByText("Billed to Operations")).toBeInTheDocument();
    expect(document.querySelector('[data-settings-panel="server-cost-owner"]')).not.toBeNull();
  });

  it("opens the Cost owner panel for its deep link and counts it in Collapse all", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    renderWithProviders(<SettingsSection server={settingsServer()} jobs={[]} />, {
      route: "/?tab=settings#server-cost-owner",
    });
    await screen.findByText("Credentials: 2");
    await waitFor(() =>
      expect(panelHeader("server-cost-owner")).toHaveAttribute("aria-expanded", "true"),
    );
    expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "false");

    await user.click(toggleAll());
    expect(toggleAll()).toHaveAccessibleName("Expand all");
    expect(panelHeader("server-cost-owner")).toHaveAttribute("aria-expanded", "false");
  });

  it("renders no Access heading, summary or request without canManageAccess", async () => {
    mockHostingQueries();
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(oneMemberAccess);
    renderSettings(settingsServer({}, { canManageAccess: false }));

    expect(await screen.findByText("Credentials: 2")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Access" })).not.toBeInTheDocument();
    expect(screen.queryByText(/^Access: /)).not.toBeInTheDocument();
    expect(document.querySelector('[data-settings-panel="server-access"]')).toBeNull();
    expect(access).not.toHaveBeenCalled();
  });

  it("tells a member who can only manage access that the Access panel stays operable", async () => {
    mockHostingQueries();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(oneMemberAccess);
    renderSettings(settingsServer({}, { canModify: false, canManageAccess: true }));

    expect(await screen.findByText("Access: 1")).toBeInTheDocument();
    expect(
      screen.getByText(
        "You can see these settings but not change them. You can still manage who has access to this server.",
      ),
    ).toBeVisible();
    expect(
      screen.queryByText("You can see these settings but not change them."),
    ).not.toBeInTheDocument();
  });

  it("keeps the read-only sentence without canModify and without canManageAccess", async () => {
    mockHostingQueries();
    renderSettings(settingsServer({}, { canModify: false, canManageAccess: false }));

    expect(await screen.findByText("Credentials: 2")).toBeInTheDocument();
    expect(screen.getByText("You can see these settings but not change them.")).toBeVisible();
    expect(screen.queryByText(/You can still manage who has access/)).not.toBeInTheDocument();
  });

  it("shows no read-only banner when the member may change settings", async () => {
    mockHostingQueries();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(oneMemberAccess);
    renderSettings(settingsServer({}, { canModify: true, canManageAccess: true }));

    expect(await screen.findByText("Access: 1")).toBeInTheDocument();
    expect(screen.queryByText(/You can see these settings but not change them/)).toBeNull();
  });

  it("keeps the Access panel and its request on an archived server", async () => {
    mockHostingQueries();
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(oneMemberAccess);
    renderSettings(settingsServer({}, { canManageAccess: true, isArchived: true }));

    expect(await screen.findByText("Access: 1")).toBeInTheDocument();
    expect(document.querySelector('[data-settings-panel="server-access"]')).not.toBeNull();
    expect(access).toHaveBeenCalledTimes(1);
  });

  it("counts the Access panel in Expand all and never pins it or stages anything", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(oneMemberAccess);
    renderSettings(settingsServer({}, { canManageAccess: true }));
    await screen.findByText("Access: 1");

    await user.click(toggleAll());
    expect(panelHeader("server-access")).toHaveAttribute("aria-expanded", "true");
    expect(within(panelHeader("server-access")).queryByText(/changed|Unsaved/)).toBeNull();
    expect(screen.queryByRole("button", { name: "Review changes" })).not.toBeInTheDocument();

    await user.click(toggleAll());
    expect(panelHeader("server-access")).toHaveAttribute("aria-expanded", "false");
  });

  it("keeps panels, summaries and Expand all working on an archived server", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    renderSettings(settingsServer({}, { isArchived: true }));
    await screen.findByText("Credentials: 2");
    expect(screen.getByText("Nothing automated")).toBeInTheDocument();

    await user.click(toggleAll());

    expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "true");
    expect(panelHeader("server-automation")).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByLabelText(`New value for ${gslt.displayName}`)).toBeDisabled();
  });

  it("remembers the open panels across a remount of the section", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    const { unmount } = renderSettings();
    await screen.findByText("Credentials: 2");
    await user.click(panelHeader("server-automation"));
    expect(JSON.parse(sessionStorage.getItem("utf.settings.open") ?? "null")).toEqual([
      "server-automation",
    ]);
    unmount();

    renderSettings();
    await screen.findByText("Credentials: 2");
    expect(panelHeader("server-automation")).toHaveAttribute("aria-expanded", "true");
    expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "false");
  });

  it("opens only Automation for its deep link and leaves the other panels collapsed", async () => {
    mockHostingQueries();
    renderWithProviders(<SettingsSection server={settingsServer()} jobs={[]} />, {
      route: "/?tab=settings#server-automation",
    });
    await screen.findByText("Credentials: 2");

    expect(panelHeader("server-automation")).toHaveAttribute("aria-expanded", "true");
    expect(panelHeader("server-credentials")).toHaveAttribute("aria-expanded", "false");
    expect(panelHeader("server-record")).toHaveAttribute("aria-expanded", "false");
    await waitFor(() => expect(panelHeader("server-automation")).toHaveFocus());
  });

  it("opens Automation when its draft turns dirty and keeps it open through Collapse all", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    seedSettingsPanels(["server-automation"]);
    renderSettings();
    await screen.findByText("Credentials: 2");

    await user.click(screen.getByRole("switch", { name: "Limit the runtime" }));
    expect(within(panelHeader("server-automation")).getByText("Unsaved")).toBeInTheDocument();

    await user.click(panelHeader("server-record"));
    await user.click(toggleAll());
    expect(panelHeader("server-automation")).toHaveAttribute("aria-expanded", "true");
    expect(panelHeader("server-record")).toHaveAttribute("aria-expanded", "false");
  });
});

describe("Settings section losing access", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    clearSettingsPanels();
  });

  it("moves focus to the Settings heading when the Access panel goes after a change of access", async () => {
    const user = userEvent.setup();
    mockHostingQueries();
    seedSettingsPanels(["server-access"]);
    const everyPermission = [
      "sv",
      "sv:view",
      "sv:operate",
      "sv:modify",
      "sv:manage-access",
      "sv:delete-data",
      "sv:manage-secrets",
      "sv:reveal-secrets",
    ];
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue({
      ...oneMemberAccess,
      allowedPermissions: everyPermission,
      assignablePermissions: everyPermission,
    });
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const initial = settingsServer({}, { canManageAccess: true });
    let current = initial;

    /** Reads the server through a query like the workspace, so the settle step finds it in the cache. */
    function LiveSettings() {
      const server = useQuery({
        queryKey: hostingQueryKeys.server(initial.primaryKey),
        queryFn: async () => current,
      });
      return server.data ? <SettingsSection server={server.data} jobs={[]} /> : null;
    }

    renderWithProviders(<LiveSettings />);
    expect(await screen.findByText("Access: 1")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Raven" }));
    current = settingsServer({}, { canManageAccess: false });

    await user.click(screen.getByRole("switch", { name: "Operate" }));

    expect(
      await screen.findByText("You can no longer manage access to Alpha Team."),
    ).toBeInTheDocument();
    expect(document.querySelector('[data-settings-panel="server-access"]')).toBeNull();
    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 2, name: "Settings" })).toHaveFocus(),
    );
  });
});
