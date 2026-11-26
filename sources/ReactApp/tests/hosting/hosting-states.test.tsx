import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { CreateServerPage } from "@/features/hosting/CreateServerPage";
import { HostingOverviewPage, ServerFleetPage } from "@/features/hosting/FleetPages";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer } from "@/features/hosting/types";
import {
  counterStrikeCapability,
  createContentOptions,
  createOwners,
  createStorageDemand,
} from "../helpers/createFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/*
 * State and heading contract of the core hosting pages (Plan 69-09): the workspace, fleet and create pages use the
 * shared state views, keep one h1 above every state panel and take their copy from locale keys.
 */

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
    actions: [],
  } as unknown as GameServer;
}

/** Renders the workspace route with the three data requests stubbed; `server` is the answer of the server query. */
function renderWorkspace(server: Promise<GameServer | undefined> | GameServer | undefined) {
  const serverSpy = vi
    .spyOn(hostingService, "server")
    .mockImplementation(async () => await Promise.resolve(server));
  vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
  vi.spyOn(hostingService, "operations").mockResolvedValue([]);
  const view = renderWithProviders(<ServerWorkspacePage />, {
    path: "/command/servers/:serverId",
    route: `/command/servers/${serverId}`,
  });
  return { ...view, serverSpy };
}

/** The headings of the page in document order. */
function headings(): HTMLElement[] {
  return screen.getAllByRole("heading");
}

/** Asserts that the page has exactly one h1 and that it is the first heading. */
function expectSingleLeadingH1(): HTMLElement {
  const h1s = screen.getAllByRole("heading", { level: 1 });
  expect(h1s).toHaveLength(1);
  expect(headings()[0]).toBe(h1s[0]);
  return h1s[0]!;
}

describe("workspace page states", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    auth.claims = [{ identifier: "sv:view" }];
    await i18n.changeLanguage("en");
  });
  afterEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("shows the shared loading state with no actions while the server loads", async () => {
    renderWorkspace(new Promise<GameServer>(() => undefined));

    const status = await screen.findByRole("status");
    expect(status).toHaveTextContent("Loading...");
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expectSingleLeadingH1();
  });

  it("shows the generic error with a Try again button that refetches", async () => {
    const user = userEvent.setup();
    const { serverSpy } = renderWorkspace(Promise.reject(new Error("secret backend detail")));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("This couldn't be loaded");
    expect(alert).not.toHaveTextContent(/servers could not be loaded/);
    expect(screen.queryByText(/secret backend detail/)).not.toBeInTheDocument();
    expectSingleLeadingH1();

    await user.click(within(alert).getByRole("button", { name: "Try again" }));
    await waitFor(() => expect(serverSpy).toHaveBeenCalledTimes(2));
  });

  it("keeps the stale workspace below its header when a refetch fails", async () => {
    const { serverSpy, queryClient } = renderWorkspace(makeServer());

    const title = await screen.findByRole("heading", { level: 1, name: "UTF One" });
    serverSpy.mockRejectedValueOnce(new Error("offline"));
    await act(async () => {
      await queryClient.refetchQueries({ queryKey: hostingQueryKeys.server(serverId) });
    });

    const alert = (await screen.findByText("This couldn't be loaded")).closest("section")!;
    expect(alert).toHaveAttribute("role", "alert");
    expect(screen.getByRole("heading", { level: 1, name: "UTF One" })).toBe(title);
    expectSingleLeadingH1();
    expect(title.compareDocumentPosition(alert) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("shows the generic forbidden state under one h1 and never queries", async () => {
    auth.claims = [];
    const { serverSpy } = renderWorkspace(makeServer());

    const panel = await screen.findByRole("status");
    expect(panel).toHaveTextContent("You don't have access to this page");
    expect(panel).toHaveTextContent("Ask an administrator to give you access.");
    expectSingleLeadingH1();
    expect(serverSpy).not.toHaveBeenCalled();
  });

  it("shows the not-found state under one h1 with a link back to the fleet", async () => {
    renderWorkspace(undefined);

    expect(
      await screen.findByRole("heading", { level: 2, name: "Server not found" }),
    ).toBeVisible();
    expectSingleLeadingH1();
    expect(screen.getByRole("link", { name: "Back to servers" })).toHaveAttribute(
      "href",
      "/command/servers",
    );
  });
});

describe("hosting overview page states and copy", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    auth.claims = [{ identifier: "sv:view" }];
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
  });
  afterEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it.each([
    [
      "en",
      {
        title: "Command overview",
        labels: ["Servers", "Possible cost", "Pending work", "Cost attention"],
        none: "No authorized server currently reports possible provider cost.",
        link: "Servers",
      },
    ],
    [
      "de",
      {
        title: "Kommandoübersicht",
        labels: ["Server", "Mögliche Kosten", "Offene Arbeit", "Kostenhinweise"],
        none: "Kein autorisierter Server meldet derzeit mögliche Providerkosten.",
        link: "Server",
      },
    ],
  ] as const)("shows the former copy entries from locale keys in %s", async (language, text) => {
    await i18n.changeLanguage(language);
    vi.spyOn(hostingService, "servers").mockResolvedValue([]);

    renderWithProviders(<HostingOverviewPage />);

    expect(await screen.findByText(text.none)).toBeVisible();
    expect(screen.getByRole("heading", { level: 1, name: text.title })).toBeVisible();
    for (const label of text.labels) expect(screen.getAllByText(label).length).toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: text.link })).toHaveAttribute(
      "href",
      "/command/servers",
    );
  });

  it("keeps the fleet error copy without the raw error text and retries", async () => {
    const user = userEvent.setup();
    const servers = vi
      .spyOn(hostingService, "servers")
      .mockRejectedValue(new Error("secret backend detail"));

    renderWithProviders(<HostingOverviewPage />);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("The servers could not be loaded. Check your connection");
    expect(screen.queryByText(/secret backend detail/)).not.toBeInTheDocument();
    expectSingleLeadingH1();

    await user.click(within(alert).getByRole("button", { name: "Try again" }));
    await waitFor(() => expect(servers).toHaveBeenCalledTimes(2));
  });

  it("keeps the fleet forbidden copy under one h1 and never queries", async () => {
    auth.claims = [];
    const servers = vi.spyOn(hostingService, "servers").mockResolvedValue([]);

    renderWithProviders(<HostingOverviewPage />);

    expect(await screen.findByText("You don't have access to game servers")).toBeVisible();
    expect(screen.getByText("Ask an administrator to give you server access.")).toBeVisible();
    expectSingleLeadingH1();
    expect(servers).not.toHaveBeenCalled();
  });
});

describe("fleet page states", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    auth.claims = [{ identifier: "sv:view" }];
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
  });
  afterEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("shows the fleet error title and a retry that refetches, under one h1", async () => {
    const user = userEvent.setup();
    const servers = vi.spyOn(hostingService, "servers").mockRejectedValue(new Error("offline"));

    renderWithProviders(<ServerFleetPage />);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("The servers could not be loaded. Check your connection");
    expectSingleLeadingH1();
    await user.click(within(alert).getByRole("button", { name: "Try again" }));
    await waitFor(() => expect(servers).toHaveBeenCalledTimes(2));
  });

  it("shows the fleet forbidden title and body under one h1", async () => {
    auth.claims = [];
    const servers = vi.spyOn(hostingService, "servers").mockResolvedValue([]);

    renderWithProviders(<ServerFleetPage />);

    expect(await screen.findByText("You don't have access to game servers")).toBeVisible();
    expect(screen.getByText("Ask an administrator to give you server access.")).toBeVisible();
    expectSingleLeadingH1();
    expect(servers).not.toHaveBeenCalled();
  });

  it("shows the loading state under the fleet heading", async () => {
    vi.spyOn(hostingService, "servers").mockReturnValue(new Promise<GameServer[]>(() => undefined));

    renderWithProviders(<ServerFleetPage />);

    expect(await screen.findByRole("status")).toHaveTextContent("Loading game servers…");
    expectSingleLeadingH1();
  });
});

describe("create server page states and content labels", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    auth.claims = [{ identifier: "crt:sv" }];
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([]);
    vi.spyOn(hostingService, "creationModPacks").mockResolvedValue([]);
    vi.spyOn(hostingService, "profileOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "terrains").mockResolvedValue([]);
    vi.spyOn(hostingService, "scenarios").mockResolvedValue([]);
    vi.spyOn(hostingService, "estimateStorage").mockResolvedValue(createStorageDemand());
    vi.spyOn(hostingService, "creationCostOwners").mockResolvedValue(createOwners());
  });
  afterEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("renders its own forbidden copy under one h1 and never loads a catalog", async () => {
    auth.claims = [];

    renderWithProviders(<CreateServerPage />);

    expect(
      await screen.findByRole("heading", { level: 2, name: "Server creation access required" }),
    ).toBeVisible();
    expectSingleLeadingH1();
    expect(hostingService.capabilities).not.toHaveBeenCalled();
  });

  it("renders the loading state under one h1 while the capabilities load", async () => {
    vi.mocked(hostingService.capabilities).mockReturnValue(new Promise(() => undefined));

    renderWithProviders(<CreateServerPage />);

    expect(await screen.findByRole("status")).toHaveTextContent("Loading game capabilities…");
    expectSingleLeadingH1();
  });

  it("renders the shared error state with its own retry label under one h1", async () => {
    vi.mocked(hostingService.capabilities).mockRejectedValueOnce(new Error("offline"));

    renderWithProviders(<CreateServerPage />);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Server creation options could not be loaded.");
    expect(within(alert).getByRole("button", { name: "Try again" })).toBeVisible();
    expectSingleLeadingH1();
  });

  it.each([
    ["en", "Choose a Terrain to continue."],
    ["de", "Wähle Karte, um fortzufahren."],
  ] as const)(
    "falls back to the locale key for a missing terrain label in %s",
    async (language, message) => {
      await i18n.changeLanguage(language);
      vi.mocked(hostingService.capabilities).mockResolvedValue([
        counterStrikeCapability({ terrainLabel: undefined as unknown as string }),
      ]);
      vi.mocked(hostingService.scenarios).mockResolvedValue(
        createContentOptions([
          { primaryKey: "20000000-0000-4000-8000-000000000001", sourceIdentifier: "competitive" },
        ]),
      );

      renderWithProviders(<CreateServerPage />);

      expect(await screen.findByText(message)).toBeVisible();
    },
  );
});
