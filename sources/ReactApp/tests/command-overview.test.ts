import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createElement } from "react";
import { afterEach, beforeAll, describe, expect, it } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import {
  getCommandNavigationContext,
  getCommandNavigationGroups,
} from "@/components/commandNavigation";
import {
  commandAttentionItems,
  commandOperationalSummary,
  commandOverviewLinks,
  Component as CommandOverviewPage,
  OperationalSnapshot,
} from "@/pages/CommandOverviewPage";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, ServerNotification } from "@/features/hosting/types";
import { renderWithProviders } from "./helpers/renderWithProviders";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/auth/auth")>()),
  useAuth: () => ({
    user: authState.user,
    isAuthenticated: Boolean(authState.user),
    isLoading: false,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

function userWithClaims(...identifiers: string[]): FullUser {
  return {
    primaryKey: "00000000-0000-0000-0000-000000000001",
    nickname: "Operator",
    isBanned: false,
    isDeleted: false,
    isVerified: true,
    isVereinMember: false,
    claims: identifiers.map((identifier) => ({ identifier, value: null })),
    roles: [],
  } as FullUser;
}

function server(
  primaryKey: string,
  options: { possibleCost?: boolean; pendingJobs?: number; status?: string } = {},
): GameServer {
  return {
    primaryKey,
    title: `Server ${primaryKey}`,
    pendingJobCount: options.pendingJobs ?? 0,
    deployment: options.status
      ? {
          status: options.status,
          stage: "running",
          possibleCost: options.possibleCost ?? false,
        }
      : null,
  } as unknown as GameServer;
}

describe("command navigation manifest", () => {
  it("drives the global administrator overview from the same permission-aware entries as navigation", () => {
    const user = userWithClaims("adm");
    const overviewPaths = commandOverviewLinks(user).map((link) => link.to);
    const navigationPaths = getCommandNavigationGroups(user)
      .flatMap((group) => group.items)
      .filter((item) => item.showOnOverview)
      .map((item) => item.to);

    expect(overviewPaths).toEqual(navigationPaths);
    expect(overviewPaths).toEqual(
      expect.arrayContaining([
        "/command/servers",
        "/command/events",
        "/command/hosting-finance",
        "/command/mod-packs",
        "/command/terrains",
        "/command/content",
        "/command/verein/documents",
        "/command/verein/kassenbuch",
      ]),
    );
  });

  it("only exposes destinations authorized by granular base claims", () => {
    expect(
      commandOverviewLinks(userWithClaims("crt:ev", "trn", "verein:kasse")).map((link) => link.to),
    ).toEqual(["/command/events", "/command/terrains", "/command/verein/kassenbuch"]);
  });

  it("keeps resource-scoped asset workspaces reachable", () => {
    const user = userWithClaims();
    user.claims = [
      { identifier: "trn", value: "42" },
      { identifier: "mp:modify", value: "17" },
    ];
    const paths = getCommandNavigationGroups(user)
      .flatMap((group) => group.items)
      .map((item) => item.to);

    expect(paths).toContain("/command/terrains");
    expect(paths).toContain("/command/mod-packs");
  });

  it("resolves canonical, detail, and legacy route context without a component-side path chain", () => {
    expect(getCommandNavigationContext("/command/servers/create")?.titleKey).toBe(
      "Shared_CommandCenter_CurrentArea_CreateServer",
    );
    expect(getCommandNavigationContext("/command/servers/server-1")?.titleKey).toBe(
      "Shared_CommandCenter_CurrentArea_ServerWorkspace",
    );
    expect(getCommandNavigationContext("/data/mod-packs/42")?.titleKey).toBe(
      "Shared_CommandCenter_CurrentArea_ModPackDetail",
    );
    expect(getCommandNavigationContext("/command/content/pages/gdpr/edit")?.titleKey).toBe(
      "Shared_CommandCenter_CurrentArea_EditPublicPage",
    );
  });

  it("does not expose global settings merely because another governance area is authorized", () => {
    const items = getCommandNavigationGroups(userWithClaims("content:pages")).flatMap(
      (group) => group.items,
    );
    expect(items.map((item) => item.id)).toContain("content");
    expect(items.map((item) => item.id)).not.toContain("settings");
  });
});

describe("command operational overview", () => {
  it("summarizes active deployments, cost exposure, and pending jobs", () => {
    expect(
      commandOperationalSummary([
        server("one", { status: "Running", possibleCost: true, pendingJobs: 2 }),
        server("two", { status: "Terminated", pendingJobs: 1 }),
        server("three"),
      ]),
    ).toEqual({
      totalServers: 3,
      activeDeployments: 1,
      possibleCost: 1,
      pendingJobs: 3,
    });
  });

  it("merges cost notifications and pending work into one prioritized item per server", () => {
    const servers = [
      server("cost", { status: "Running", possibleCost: true, pendingJobs: 2 }),
      server("pending", { status: "Preparing", pendingJobs: 4 }),
    ];
    const notifications = [
      {
        serverId: "cost",
        title: "Cost server",
        statusLabel: "Running",
        possibleCost: true,
      } as ServerNotification,
    ];

    const items = commandAttentionItems(servers, notifications);
    expect(items).toHaveLength(2);
    expect(items[0]).toMatchObject({
      serverId: "cost",
      title: "Cost server",
      possibleCost: true,
      pendingJobCount: 2,
      derived: { state: "running", cost: "live" },
    });
    expect(items[1]).toMatchObject({
      serverId: "pending",
      title: "Server pending",
      possibleCost: false,
      pendingJobCount: 4,
      derived: { state: "starting", cost: "live" },
    });
    for (const item of items) expect(item).not.toHaveProperty("detail");
  });

  it("derives an unknown state with possible cost for a notification without a fleet server", () => {
    const notifications = [
      {
        serverId: "missing",
        title: "Missing server",
        statusLabel: "Stopped",
        possibleCost: true,
      } as ServerNotification,
    ];

    expect(commandAttentionItems([], notifications)).toMatchObject([
      { serverId: "missing", pendingJobCount: 0, derived: { state: "unknown", cost: "maybe" } },
    ]);
  });

  it("derives the state of a pending-only server without a deployment as working and free", () => {
    expect(commandAttentionItems([server("idle", { pendingJobs: 1 })], [])).toMatchObject([
      { serverId: "idle", possibleCost: false, derived: { state: "working", cost: "none" } },
    ]);
  });
});

describe("command overview attention rendering", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  it("shows the derived state and cost and never the raw status text", () => {
    renderWithProviders(
      createElement(OperationalSnapshot, {
        servers: [server("cost", { status: "Running", possibleCost: true, pendingJobs: 1 })],
        notifications: [
          {
            serverId: "cost",
            title: "Cost server",
            statusLabel: "Raw notification label",
            possibleCost: true,
          } as ServerNotification,
        ],
      }),
    );

    expect(screen.getByText("Cost server")).toBeInTheDocument();
    expect(screen.getByText("Running")).toBeInTheDocument();
    expect(screen.getByText("Costing now")).toBeInTheDocument();
    expect(screen.queryByText("Raw notification label")).toBeNull();
    expect(screen.queryByText("running")).toBeNull();
  });
});

describe("command overview page", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  afterEach(() => {
    authState.user = undefined;
    vi.restoreAllMocks();
  });

  it("renders one h1 above a page stack, with the shared loading state while the fleet loads", () => {
    authState.user = userWithClaims("adm");
    vi.spyOn(hostingService, "servers").mockReturnValue(new Promise(() => undefined));
    vi.spyOn(hostingService, "notifications").mockReturnValue(new Promise(() => undefined));

    const { container } = renderWithProviders(createElement(CommandOverviewPage));

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Command Center");
    expect(container.querySelector(".page-stack")).not.toBeNull();
    expect(screen.getByRole("status")).toHaveTextContent("Loading game servers");
    expect(
      container.querySelector(".page-header + .page-stack, header.page-header ~ .page-stack"),
    ).not.toBeNull();
  });

  it("renders one h1 and the work areas for a viewer without server access", () => {
    authState.user = userWithClaims("trn");
    const servers = vi.spyOn(hostingService, "servers");

    const { container } = renderWithProviders(createElement(CommandOverviewPage));

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(container.querySelector(".command-work-area-list")).not.toBeNull();
    expect(servers).not.toHaveBeenCalled();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("shows the shared error state with a retry that refetches the fleet", async () => {
    authState.user = userWithClaims("adm");
    const servers = vi.spyOn(hostingService, "servers").mockRejectedValue(new Error("down"));
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
    const user = userEvent.setup();
    renderWithProviders(createElement(CommandOverviewPage));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Game servers could not be loaded.");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const callsBefore = servers.mock.calls.length;

    servers.mockResolvedValue([server("one", { status: "Running" })]);
    await user.click(within(alert).getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(servers.mock.calls.length).toBeGreaterThan(callsBefore));
    await waitFor(() => expect(screen.queryByRole("alert")).toBeNull());
    expect(await screen.findByText("Total servers")).toBeInTheDocument();
  });

  it("keeps the loaded snapshot visible under a failed refetch", async () => {
    authState.user = userWithClaims("adm");
    const servers = vi
      .spyOn(hostingService, "servers")
      .mockResolvedValue([server("one", { status: "Running" })]);
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
    const user = userEvent.setup();
    renderWithProviders(createElement(CommandOverviewPage));

    expect(await screen.findByLabelText("Operational hosting snapshot")).toBeInTheDocument();
    servers.mockRejectedValue(new Error("down"));
    await user.click(screen.getByRole("button", { name: "Refresh" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Game servers could not be loaded.");
    expect(screen.getByLabelText("Operational hosting snapshot")).toBeInTheDocument();
  });
});
