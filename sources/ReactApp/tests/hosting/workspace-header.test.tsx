import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, ServerAction, ServerDeployment } from "@/features/hosting/types";
import { renderWithProviders } from "../helpers/renderWithProviders";

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-4000-8000-000000000001",
      nickname: "Falcon One",
      isBanned: false,
      isDeleted: false,
      isVereinMember: true,
      claims: [
        {
          primaryKey: 1,
          identifier: "sv:view",
          value: null,
          title: "View servers",
          category: "Hosting",
          description: "",
          isPrefix: false,
          valueType: null,
        },
      ],
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

const runningDeployment = {
  status: "Running",
  stage: "Ready",
  possibleCost: true,
  workerStatus: "Connected",
  lastHeartbeat: "2026-08-13T10:00:00Z",
  connectionEndpoint: "203.0.113.5:2302",
  connectionProtocol: "udp",
  publicAddress: null,
  deploymentRoot: "/srv/utf",
  includedStorageBytes: 80 * 1_073_741_824,
  selectedExternalStorageBytes: null,
  estimatedComputeHourlyCostEur: 0.1,
  estimatedStorageHourlyCostEur: 0,
  diskTotalBytes: null,
  diskAvailableBytes: null,
  diskTimeStampObserved: null,
  faultSummary: null,
  administrativeSshCommand: null,
  resources: [],
} as unknown as ServerDeployment;

const stopAction = {
  id: "stop",
  label: "Stop",
  description: "Save the server data and release the machine.",
  icon: "stop",
  group: "Lifecycle",
  order: 2,
  enabled: true,
  unavailableReason: null,
  dangerous: false,
  confirmationText: "Saves the data first.",
  confirmationChallenge: null,
  pending: false,
  expectedDurationSeconds: 300,
  costImpact: "StopsCost",
  costDescription: "Stops provider cost.",
} as unknown as ServerAction;

function makeServer(deployment: ServerDeployment | null, actions: ServerAction[] = []): GameServer {
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
    deployment,
    actions,
  } as unknown as GameServer;
}

function renderPage(server: GameServer | undefined, search = "") {
  vi.spyOn(hostingService, "server").mockResolvedValue(server);
  vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
  vi.spyOn(hostingService, "operations").mockResolvedValue([]);
  vi.spyOn(hostingService, "files").mockResolvedValue([]);
  vi.spyOn(hostingService, "preservationSelections").mockResolvedValue([]);
  return renderWithProviders(<ServerWorkspacePage />, {
    path: "/command/servers/:serverId",
    route: `/command/servers/${serverId}${search}`,
  });
}

describe("workspace header and panels", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the derived state and the live cost for a running server that may cost", async () => {
    renderPage(makeServer(runningDeployment, [stopAction]));

    const heading = await screen.findByRole("heading", { level: 1, name: "UTF One" });
    const header = heading.closest("header")!;
    expect(within(header).getByText("Running")).toBeVisible();
    expect(within(header).getByText("Costing now")).toBeVisible();
    expect(
      within(header).getByRole("button", { name: "Shut down and stop billing" }),
    ).toBeVisible();
    // The overview details panel lists the server record without repeating the cost.
    const details = screen
      .getByRole("heading", { level: 2, name: "Server details" })
      .closest("section")!;
    expect(within(details).queryByText("Costing now")).not.toBeInTheDocument();
  });

  it("renders breadcrumbs from the servers list to the current section", async () => {
    renderPage(makeServer(runningDeployment), "?tab=activity");

    await screen.findByRole("heading", { level: 1, name: "UTF One" });
    const breadcrumbs = screen.getByRole("navigation", { name: "Breadcrumb" });
    expect(within(breadcrumbs).getByRole("link", { name: "Servers" })).toHaveAttribute(
      "href",
      "/command/servers",
    );
    expect(within(breadcrumbs).getByRole("link", { name: "UTF One" })).toHaveAttribute(
      "href",
      `/command/servers/${serverId}`,
    );
    const current = within(breadcrumbs).getByText("Activity");
    expect(current).toHaveAttribute("aria-current", "page");
    expect(current.tagName).toBe("SPAN");
  });

  it("shows the cost rows, the endpoint and Refresh server in the details block", async () => {
    renderPage(makeServer(runningDeployment));

    await screen.findByRole("heading", { level: 1, name: "UTF One" });
    const details = screen.getByRole("region", { name: "Cost and connection" });
    for (const label of [
      "Cost owner",
      "Per hour",
      "Per month if left running",
      "Cost now",
      "This session so far",
    ]) {
      expect(within(details).getByText(label)).toBeVisible();
    }
    expect(within(details).getByText("203.0.113.5:2302")).toBeVisible();
    expect(within(details).getByRole("button", { name: "Copy address" })).toBeVisible();
    expect(within(details).getByRole("button", { name: "Refresh server" })).toBeEnabled();
  });

  it("keeps the shell in every section and swaps only the section content", async () => {
    const user = userEvent.setup();
    const { router } = renderPage(makeServer(runningDeployment, [stopAction]));

    await screen.findByRole("heading", { level: 1, name: "UTF One" });
    const sections = [
      ["Activity", "?tab=activity"],
      ["Settings", "?tab=settings"],
      ["Files", "?tab=files"],
      ["Machine", "?tab=machine"],
      ["Overview", ""],
    ] as const;
    for (const [label, search] of sections) {
      await user.click(
        within(screen.getByRole("navigation", { name: "Server sections" })).getByRole("link", {
          name: label,
        }),
      );
      expect(router.state.location.search).toBe(search);
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("heading", { level: 1, name: "UTF One" })).toBeVisible();
      expect(
        within(screen.getByRole("navigation", { name: "Breadcrumb" })).getByText(label),
      ).toHaveAttribute("aria-current", "page");
      expect(screen.getByRole("region", { name: "Cost and connection" })).toBeVisible();
      expect(screen.getByRole("button", { name: "Refresh server" })).toBeVisible();
    }
  });

  it("opens Overview for a retired tab value and keeps the search untouched", async () => {
    const { router } = renderPage(makeServer(runningDeployment), "?tab=jobs");

    expect(await screen.findByRole("heading", { level: 2, name: "Server details" })).toBeVisible();
    expect(router.state.location.search).toBe("?tab=jobs");
    expect(
      within(screen.getByRole("navigation", { name: "Breadcrumb" })).getByText("Overview"),
    ).toHaveAttribute("aria-current", "page");
  });

  it("shows Server not found with a way back for a missing server", async () => {
    renderPage(undefined);

    expect(await screen.findByRole("heading", { name: "Server not found" })).toBeVisible();
    expect(
      screen.getByText("This server does not exist or is no longer shared with you."),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Back to servers" })).toHaveAttribute(
      "href",
      "/command/servers",
    );
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Servers" })).toBeVisible();
  });

  it("shows the error panel with Try again and never the raw error text", async () => {
    const user = userEvent.setup();
    const load = vi
      .spyOn(hostingService, "server")
      .mockRejectedValueOnce(new Error("secret backend detail"))
      .mockResolvedValue(makeServer(runningDeployment));
    vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
    vi.spyOn(hostingService, "operations").mockResolvedValue([]);
    renderWithProviders(<ServerWorkspacePage />, {
      path: "/command/servers/:serverId",
      route: `/command/servers/${serverId}`,
    });

    await screen.findByText("This couldn't be loaded");
    expect(screen.queryByText(/secret backend detail/)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByRole("heading", { level: 1, name: "UTF One" })).toBeVisible();
    expect(load).toHaveBeenCalledTimes(2);
  });

  it("shows off and not costing for a server without a deployment", async () => {
    renderPage(makeServer(null));

    const heading = await screen.findByRole("heading", { level: 1, name: "UTF One" });
    const header = heading.closest("header")!;
    expect(within(header).getByText("Off")).toBeVisible();
    expect(within(header).getByText("Not costing")).toBeVisible();
    expect(screen.queryByText("Offline")).not.toBeInTheDocument();
  });

  it("shows the cloud panel state from the same derivation with the technical status as detail", async () => {
    renderPage(makeServer(runningDeployment), "?tab=machine");

    const cloud = (
      await screen.findByRole("heading", { level: 2, name: "Deployment and cost" })
    ).closest("section")!;
    expect(within(cloud).getAllByText("Running")).toHaveLength(2);
    expect(within(cloud).getByText("Costing now")).toBeVisible();
    const technical = within(cloud).getByText("Technical status");
    expect(technical.tagName).toBe("DT");
    expect(technical.nextElementSibling).toHaveTextContent("Running");
  });

  it("does not render a raw deployment status word as the state label", async () => {
    renderPage(
      makeServer({ ...runningDeployment, status: "TerminatedDueToFault", possibleCost: false }),
    );

    const heading = await screen.findByRole("heading", { level: 1, name: "UTF One" });
    const header = heading.closest("header")!;
    expect(within(header).getByText("Needs attention")).toBeVisible();
    expect(within(header).queryByText(/Terminated/)).not.toBeInTheDocument();
    expect(screen.queryByText("TerminatedDueToFault")).not.toBeInTheDocument();
  });
});
