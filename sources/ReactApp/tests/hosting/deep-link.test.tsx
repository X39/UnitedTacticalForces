import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, LifecycleJob, ServerAction } from "@/features/hosting/types";
import { renderWithProviders } from "../helpers/renderWithProviders";

const auth = vi.hoisted(() => ({
  claims: [] as { identifier: string }[],
}));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-4000-8000-000000000001",
      nickname: "Falcon One",
      isBanned: false,
      isDeleted: false,
      isVereinMember: true,
      claims: auth.claims.map((claim, index) => ({
        primaryKey: index + 1,
        identifier: claim.identifier,
        value: null,
        title: claim.identifier,
        category: "Hosting",
        description: "",
        isPrefix: false,
        valueType: null,
      })),
      roles: [],
    } as unknown as FullUser,
    isAuthenticated: true,
    isLoading: false,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

const serverId = "10000000-0000-4000-8000-0000000000aa";
const serverPath = `/command/servers/${serverId}`;
const job = { primaryKey: "job-1" } as LifecycleJob;

const startAction = {
  id: "start",
  label: "Start server",
  description: "Starts the server.",
  icon: "play",
  group: "lifecycle",
  order: 1,
  enabled: true,
  unavailableReason: null,
  dangerous: false,
  confirmationText: null,
  confirmationChallenge: null,
  pending: false,
  expectedDurationSeconds: null,
  costImpact: "StartsCost",
  costDescription: "Starts billing.",
} as ServerAction;

const stopAction = {
  ...startAction,
  id: "stop",
  label: "Shut down",
  order: 2,
  costImpact: "StopsCost",
} as ServerAction;

const terminateAction = {
  ...startAction,
  id: "terminate",
  label: "Terminate",
  order: 3,
  dangerous: true,
  confirmationChallenge: "UTF One",
  costImpact: "StopsCost",
} as ServerAction;

const restartAction = {
  ...startAction,
  id: "restart",
  label: "Restart",
  order: 4,
  enabled: false,
  unavailableReason: "Server Operate permission is required.",
  costImpact: "KeepsCostRunning",
} as ServerAction;

const updateAction = {
  ...startAction,
  id: "update",
  label: "Update",
  order: 5,
  pending: true,
  costImpact: "NoCostImpact",
} as ServerAction;

function makeServer(): GameServer {
  return {
    primaryKey: serverId,
    title: "UTF One",
    gameIdentifier: "arma3",
    runtimeProviderIdentifier: "fake-cloud",
    runtimeProfileIdentifier: "medium",
    runtimeProfileDisplayName: "Medium",
    estimatedHourlyCostEur: 0.1,
    estimatedMonthlyCostEur: 73,
    selectedModPackDefinitionTitle: null,
    selectedConfigurationProfileTitle: null,
    selectedTerrainTitle: null,
    selectedScenarioTitle: null,
    costOwnerName: "Operations",
    isLaunchDisabled: false,
    isArchived: false,
    canModify: true,
    canManageSecrets: false,
    timeStampCreated: "2026-08-13T10:00:00Z",
    timeStampUpdated: "2026-08-13T11:00:00Z",
    pendingJobCount: 0,
    deployment: null,
    actions: [startAction, stopAction, terminateAction, restartAction, updateAction],
  } as unknown as GameServer;
}

function setup(
  search: string,
  options: { strict?: boolean; server?: GameServer | undefined } = {},
) {
  const serverSpy = vi
    .spyOn(hostingService, "server")
    .mockResolvedValue("server" in options ? options.server : makeServer());
  vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
  vi.spyOn(hostingService, "operations").mockResolvedValue([]);
  const executeSpy = vi.spyOn(hostingService, "executeAction").mockResolvedValue(job);
  const view = renderWithProviders(<ServerWorkspacePage />, {
    path: "/command/servers/:serverId",
    route: `${serverPath}${search}`,
    ...(options.strict ? { strict: true } : {}),
  });
  return { ...view, serverSpy, executeSpy };
}

describe("workspace action deep link", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    auth.claims = [{ identifier: "sv:view" }];
    await i18n.changeLanguage("en");
  });
  afterEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("opens the confirmation for an enabled start action without executing it", async () => {
    const { executeSpy } = setup("?action=start");

    const dialog = await screen.findByRole("dialog", { name: "Start UTF One?" });
    expect(dialog).toBeVisible();
    expect(screen.queryByText(/isn't available right now/)).not.toBeInTheDocument();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("opens exactly one dialog although the sticky header offers the same Start button", async () => {
    const { executeSpy } = setup("?action=start");

    const dialog = await screen.findByRole("dialog", { name: "Start UTF One?" });
    expect(screen.getAllByRole("dialog")).toHaveLength(1);
    const header = screen.getByRole("heading", { level: 1, name: "UTF One" }).closest("header")!;
    expect(
      within(header).getByRole("button", { name: "Start server", hidden: true }),
    ).toBeVisible();
    expect(dialog).toBeVisible();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("matches the action id case-insensitively", async () => {
    const { executeSpy } = setup("?action=STOP");

    expect(
      await screen.findByRole("dialog", { name: "Shut down UTF One and stop billing?" }),
    ).toBeVisible();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("opens the terminate confirmation with the typed challenge field", async () => {
    const { executeSpy } = setup("?action=terminate");

    const dialog = await screen.findByRole("dialog", {
      name: "Shut down UTF One now without saving?",
    });
    expect(within(dialog).getByRole("textbox")).toBeVisible();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("renders nothing action-related while the server data is loading", async () => {
    vi.spyOn(hostingService, "server").mockReturnValue(new Promise(() => undefined));
    vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
    vi.spyOn(hostingService, "operations").mockResolvedValue([]);
    renderWithProviders(<ServerWorkspacePage />, {
      path: "/command/servers/:serverId",
      route: `${serverPath}?action=start`,
    });

    expect(await screen.findByText("Loading...")).toBeVisible();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByText(/isn't available right now/)).not.toBeInTheDocument();
    expect(screen.queryByText(/has no action called/)).not.toBeInTheDocument();
  });

  it("explains a disabled action with the mapped reason in English and German", async () => {
    const view = setup("?action=restart");

    const notice = (await screen.findByText(/isn't available right now/)).closest("section")!;
    expect(notice).toHaveAttribute("role", "status");
    expect(notice).toHaveTextContent('"Restart game" isn\'t available right now.');
    expect(notice).toHaveTextContent("You need permission to operate this server.");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(notice).not.toContainElement(document.activeElement as HTMLElement);
    view.unmount();

    await i18n.changeLanguage("de");
    setup("?action=restart");
    const germanNotice = (await screen.findByText(/ist gerade nicht verfügbar/)).closest(
      "section",
    )!;
    expect(germanNotice).toHaveTextContent(
      "Du brauchst die Berechtigung, diesen Server zu bedienen.",
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("explains a pending action with the pending reason and opens no dialog", async () => {
    const { executeSpy } = setup("?action=update");

    const notice = (await screen.findByText(/isn't available right now/)).closest("section")!;
    expect(notice).toHaveTextContent("Another change is still running.");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("shows the unknown id as text and truncates a 100 character id to 40", async () => {
    setup(`?action=${"a".repeat(100)}`);

    const notice = (await screen.findByText(/has no action called/)).closest("section")!;
    expect(notice).toHaveAttribute("role", "status");
    expect(notice).toHaveTextContent(`has no action called "${"a".repeat(40)}…"`);
    expect(notice.textContent).not.toMatch(/a{41}/);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it.each(["constructor", "__proto__", "toString"])(
    "handles the prototype key %s as an unknown id",
    async (id) => {
      setup(`?action=${id}`);

      expect(await screen.findByText(new RegExp(`has no action called "${id}"`))).toBeVisible();
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    },
  );

  it("renders markup in the id as plain text", async () => {
    const markup = "<img src=x onerror=alert(1)>";
    setup(`?action=${encodeURIComponent(markup)}`);

    const notice = (await screen.findByText(/has no action called/)).closest("section")!;
    expect(notice).toHaveTextContent(markup);
    expect(document.querySelector('img[src="x"]')).toBeNull();
    expect(notice.querySelector("img")).toBeNull();
    expect(notice.querySelector("a")).toBeNull();
  });

  it("shows only the forbidden panel to a viewer without a server claim", async () => {
    auth.claims = [];
    const { serverSpy, executeSpy } = setup("?action=start");

    expect(await screen.findByText("You don't have access to this page")).toBeVisible();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByText(/isn't available right now/)).not.toBeInTheDocument();
    expect(screen.queryByText(/has no action called/)).not.toBeInTheDocument();
    expect(serverSpy).not.toHaveBeenCalled();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("shows only the not-found panel for a server that does not exist", async () => {
    const { executeSpy } = setup("?action=restart", { server: undefined });

    expect(await screen.findByRole("heading", { name: "Server not found" })).toBeVisible();
    expect(
      screen.getByText("This server does not exist or is no longer shared with you."),
    ).toBeVisible();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByText(/isn't available right now/)).not.toBeInTheDocument();
    expect(screen.queryByText(/has no action called/)).not.toBeInTheDocument();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("removes only the action parameter when the notice is dismissed", async () => {
    const { router } = setup("?tab=activity&action=restart");

    await screen.findByText(/isn't available right now/);
    await userEvent.setup().click(screen.getByRole("button", { name: "Dismiss" }));

    await waitFor(() => expect(router.state.location.search).toBe("?tab=activity"));
    expect(router.state.historyAction).toBe("REPLACE");
    expect(screen.queryByText(/isn't available right now/)).not.toBeInTheDocument();
  });

  it("removes only the action parameter when the dialog is dismissed", async () => {
    const { router, executeSpy } = setup("?tab=activity&action=start");

    const dialog = await screen.findByRole("dialog", { name: "Start UTF One?" });
    await userEvent
      .setup()
      .click(within(dialog).getByRole("button", { name: "Don't start server" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(router.state.location.search).toBe("?tab=activity");
    expect(router.state.historyAction).toBe("REPLACE");
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("removes only the action parameter when the dialog is confirmed", async () => {
    const { router, executeSpy } = setup("?tab=activity&action=start");

    const dialog = await screen.findByRole("dialog", { name: "Start UTF One?" });
    await userEvent.setup().click(within(dialog).getByRole("button", { name: "Start server" }));

    await waitFor(() => expect(executeSpy).toHaveBeenCalledWith(serverId, "start", undefined));
    await waitFor(() => expect(router.state.location.search).toBe("?tab=activity"));
    expect(router.state.historyAction).toBe("REPLACE");
  });

  it("opens a single dialog under StrictMode and again for a repeated link", async () => {
    const { router, executeSpy } = setup("?action=start", { strict: true });

    const dialog = await screen.findByRole("dialog", { name: "Start UTF One?" });
    expect(screen.getAllByRole("dialog")).toHaveLength(1);
    expect(executeSpy).not.toHaveBeenCalled();

    await userEvent
      .setup()
      .click(within(dialog).getByRole("button", { name: "Don't start server" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(router.state.location.search).toBe("");

    await act(async () => {
      await router.navigate(`${serverPath}?action=start`);
    });
    expect(await screen.findByRole("dialog", { name: "Start UTF One?" })).toBeVisible();
    expect(screen.getAllByRole("dialog")).toHaveLength(1);
    expect(executeSpy).not.toHaveBeenCalled();
  });
});
