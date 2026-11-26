import { act, screen } from "@testing-library/react";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { canAccessCommandCenter, canAccessGameServers } from "@/auth/claims";
import { getCommandNavigationGroups } from "@/components/commandNavigation";
import { getHostingPermissions } from "@/features/hosting/authorization";
import { ServerFleetPage } from "@/features/hosting/FleetPages";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, ServerAction } from "@/features/hosting/types";
import { renderWithProviders } from "../helpers/renderWithProviders";

/*
 * React half of ACC-01 (D-11). This file proves that a member holding only a bare `sv` claim or only a bare
 * `sv:view` claim reaches the fleet, sees exactly what the API returns and gets disabled-with-reason controls.
 * The API half, the list endpoint response for the same claims, is proven by `GameServerListAccessTests` in
 * Plan 61-03 (tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs). The fixtures below mirror its
 * semantics: `sv:view` receives actions with `enabled: false` and the operate reason, `sv` receives enabled
 * actions. The browser never recomputes either; it only renders what the API sent.
 */

const authState = vi.hoisted(() => ({
  user: undefined as FullUser | undefined,
  isLoading: false,
}));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isAuthenticated: Boolean(authState.user),
    isLoading: authState.isLoading,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

function userWithClaims(...identifiers: string[]): FullUser {
  return {
    primaryKey: "10000000-0000-4000-8000-000000000001",
    nickname: "Member",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: true,
    claims: identifiers.map((identifier, index) => ({
      primaryKey: index + 1,
      identifier,
      value: null,
      title: identifier,
      category: "Hosting",
      description: "",
      isPrefix: false,
      valueType: null,
    })),
    roles: [],
  } as unknown as FullUser;
}

/** Builds a full lifecycle action as the API sends it to a member who may operate the server. */
function operateAction(id: "start" | "stop"): ServerAction {
  return {
    id,
    label: id === "start" ? "Start" : "Stop",
    description: "",
    icon: "",
    group: "lifecycle",
    order: id === "start" ? 1 : 2,
    enabled: true,
    unavailableReason: null,
    dangerous: false,
    confirmationText: null,
    confirmationChallenge: null,
    pending: false,
    expectedDurationSeconds: null,
    costImpact: id === "start" ? "StartsCost" : "StopsCost",
    costDescription: "",
  } as ServerAction;
}

/** Builds the same action as the API sends it to a view-only member: disabled with the operate reason. */
function viewOnlyAction(id: "start" | "stop"): ServerAction {
  return {
    ...operateAction(id),
    enabled: false,
    unavailableReason: "Server Operate permission is required.",
  } as ServerAction;
}

type AccessMode = "operate" | "view";

function apiServer(index: number, mode: AccessMode, running: boolean): GameServer {
  const actionFor = mode === "operate" ? operateAction : viewOnlyAction;
  return {
    primaryKey: `20000000-0000-4000-8000-00000000000${index}`,
    title: `Fleet Server ${index}`,
    gameIdentifier: "arma3",
    runtimeProviderIdentifier: "hetzner",
    costOwnerName: "Operations",
    pendingJobCount: 0,
    timeStampUpdated: "2026-08-13T12:30:00Z",
    actions: [actionFor(running ? "stop" : "start")],
    deployment: running
      ? { status: "Running", stage: "worker-online", possibleCost: false, resources: [] }
      : null,
  } as unknown as GameServer;
}

/** One running server carrying a stop action and one off server carrying a start action. */
function apiServers(mode: AccessMode): GameServer[] {
  return [apiServer(1, mode, true), apiServer(2, mode, false)];
}

function renderFleetPage() {
  return renderWithProviders(<ServerFleetPage />, { route: "/command/servers" });
}

const createReasonEnglish = "You need permission to create servers. Ask an administrator.";
const createReasonGerman =
  "Du brauchst die Berechtigung, Server zu erstellen. Frage eine Administration.";
const operateReasonEnglish =
  "You need permission to operate this server. Ask the server owner or an administrator.";
const operateReasonGerman =
  "Du brauchst die Berechtigung, diesen Server zu bedienen. Frage den Serverbesitzer oder eine Administration.";

async function useLanguage(language: "en" | "de") {
  await act(async () => {
    await i18n.changeLanguage(language);
  });
}

describe("member access to the fleet", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = undefined;
    authState.isLoading = false;
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    await useLanguage("en");
  });

  it.each(["sv", "sv:view"])(
    "gives a member holding only the bare %s claim command center, fleet and navigation access",
    (claim) => {
      const user = userWithClaims(claim);
      const permissions = getHostingPermissions(user);
      const navigation = getCommandNavigationGroups(user).flatMap((group) => group.items);

      expect(canAccessCommandCenter(user)).toBe(true);
      expect(canAccessGameServers(user)).toBe(true);
      expect(permissions.accessServers).toBe(true);
      expect(permissions.createServers).toBe(false);
      expect(navigation.map((item) => item.to)).toContain("/command/servers");
    },
  );

  it("withholds server access from a user with no server claim", () => {
    const user = userWithClaims("mp");

    expect(canAccessGameServers(user)).toBe(false);
    expect(getHostingPermissions(user).accessServers).toBe(false);
    expect(
      getCommandNavigationGroups(user)
        .flatMap((group) => group.items)
        .map((item) => item.to),
    ).not.toContain("/command/servers");
  });

  it.each(["sv", "sv:view"])(
    "renders exactly the servers the API returns for a bare %s member and queries once",
    async (claim) => {
      authState.user = userWithClaims(claim);
      const servers = vi
        .spyOn(hostingService, "servers")
        .mockResolvedValue(apiServers(claim === "sv" ? "operate" : "view"));

      renderFleetPage();

      expect(await screen.findAllByRole("article")).toHaveLength(2);
      expect(screen.getByRole("heading", { name: "Fleet Server 1" })).toBeVisible();
      expect(screen.getByRole("heading", { name: "Fleet Server 2" })).toBeVisible();
      expect(servers).toHaveBeenCalledTimes(1);
    },
  );

  it("shows a bare sv member enabled start and stop controls and no terminate control", async () => {
    authState.user = userWithClaims("sv");
    vi.spyOn(hostingService, "servers").mockResolvedValue(apiServers("operate"));

    renderFleetPage();

    const start = await screen.findByRole("button", { name: "Start server" });
    const stop = screen.getByRole("button", { name: "Shut down and stop billing" });
    expect(start).not.toHaveAttribute("aria-disabled");
    expect(stop).not.toHaveAttribute("aria-disabled");
    expect(
      screen.queryByRole("button", { name: "Shut down without saving" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Shut down without saving" }),
    ).not.toBeInTheDocument();
  });

  it.each([
    ["en", operateReasonEnglish, "Start server", "Shut down and stop billing"],
    ["de", operateReasonGerman, "Server starten", "Herunterfahren und Abrechnung beenden"],
  ] as const)(
    "shows an sv:view member disabled-with-reason start and stop controls in %s",
    async (language, reason, startLabel, stopLabel) => {
      await useLanguage(language);
      authState.user = userWithClaims("sv:view");
      vi.spyOn(hostingService, "servers").mockResolvedValue(apiServers("view"));

      renderFleetPage();

      const start = await screen.findByRole("button", { name: startLabel });
      const stop = screen.getByRole("button", { name: stopLabel });
      [start, stop].forEach((button) => {
        expect(button).toHaveAttribute("aria-disabled", "true");
        expect(button).toHaveAccessibleDescription(
          expect.stringContaining(reason) as unknown as string,
        );
      });
      expect(screen.getAllByText(reason)).toHaveLength(2);
      expect(screen.queryByRole("link", { name: startLabel })).not.toBeInTheDocument();
      expect(screen.queryByRole("link", { name: stopLabel })).not.toBeInTheDocument();
    },
  );

  it.each([
    ["sv", "en", createReasonEnglish, "Create server"],
    ["sv", "de", createReasonGerman, "Server erstellen"],
    ["sv:view", "en", createReasonEnglish, "Create server"],
    ["sv:view", "de", createReasonGerman, "Server erstellen"],
  ] as const)(
    "disables the create control with a visible reason for a bare %s member in %s",
    async (claim, language, reason, label) => {
      await useLanguage(language);
      authState.user = userWithClaims(claim);
      vi.spyOn(hostingService, "servers").mockResolvedValue(
        apiServers(claim === "sv" ? "operate" : "view"),
      );

      renderFleetPage();

      await screen.findAllByRole("article");
      const create = screen.getByRole("button", { name: label });
      expect(create).toHaveAttribute("aria-disabled", "true");
      expect(create).toHaveAccessibleDescription(reason);
      expect(screen.getByText(reason)).toBeVisible();
      expect(screen.queryByRole("link", { name: label })).not.toBeInTheDocument();
    },
  );

  it("shows the access empty state, never the create-first copy, when the API returns no servers", async () => {
    authState.user = userWithClaims("sv:view");
    vi.spyOn(hostingService, "servers").mockResolvedValue([]);

    renderFleetPage();

    expect(
      await screen.findByRole("heading", { name: "No servers shared with you yet" }),
    ).toBeVisible();
    expect(
      screen.getByText(
        "Servers you are allowed to see appear here. Ask a server owner to give you access.",
      ),
    ).toBeVisible();
    expect(screen.queryByText("No game servers yet")).not.toBeInTheDocument();
    expect(screen.queryByText(/Create a durable server record first/)).not.toBeInTheDocument();
  });

  it("shows the forbidden state and never queries for an authenticated user with no server claim", async () => {
    authState.user = userWithClaims("mp");
    const servers = vi.spyOn(hostingService, "servers").mockResolvedValue(apiServers("operate"));

    renderFleetPage();

    expect(await screen.findByText("You don't have access to game servers")).toBeVisible();
    expect(screen.getByText("Ask an administrator to give you server access.")).toBeVisible();
    expect(screen.queryByRole("article")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Create server" })).not.toBeInTheDocument();
    expect(servers).toHaveBeenCalledTimes(0);
  });

  it("shows loading instead of forbidden and does not query while authentication is loading", async () => {
    authState.user = undefined;
    authState.isLoading = true;
    const servers = vi.spyOn(hostingService, "servers").mockResolvedValue(apiServers("operate"));

    renderFleetPage();

    expect(await screen.findByText("Loading game servers…")).toBeVisible();
    expect(screen.queryByText("You don't have access to game servers")).not.toBeInTheDocument();
    expect(servers).toHaveBeenCalledTimes(0);
  });

  it("shows an enabled create link to a member holding the creation claim", async () => {
    authState.user = userWithClaims("crt:sv");
    vi.spyOn(hostingService, "servers").mockResolvedValue(apiServers("operate"));

    renderFleetPage();

    await screen.findAllByRole("article");
    expect(screen.getByRole("link", { name: "Create server" })).toHaveAttribute(
      "href",
      "/command/servers/create",
    );
    expect(screen.queryByRole("button", { name: "Create server" })).not.toBeInTheDocument();
  });
});
