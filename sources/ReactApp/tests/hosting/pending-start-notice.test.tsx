import { screen, waitFor } from "@testing-library/react";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, ServerChangeArea } from "@/features/hosting/types";
import { fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-4000-8000-0000000000f1",
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

/** Builds a server with the given pending-start areas, optionally running. */
function pendingServer(areas: ServerChangeArea[], status?: string): GameServer {
  return fleetServer({ title: "UTF One", pendingStartAreas: areas, status });
}

/** Renders the whole workspace page for `target` with the requests of the Overview and Settings sections answered. */
function renderPage(target: GameServer, search = "") {
  vi.spyOn(hostingService, "server").mockResolvedValue(target);
  vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
  vi.spyOn(hostingService, "operations").mockResolvedValue([]);
  vi.spyOn(hostingService, "files").mockResolvedValue([]);
  vi.spyOn(hostingService, "preservationSelections").mockResolvedValue([]);
  vi.spyOn(hostingService, "configuration").mockResolvedValue({
    serverId: target.primaryKey,
    profileId: null,
    profileRevisionId: null,
    profileRevisionNumber: null,
    isComplete: true,
    entries: [],
  });
  vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
  vi.spyOn(hostingService, "modPackOptions").mockResolvedValue([]);
  vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
  vi.spyOn(hostingService, "storageDemand").mockRejectedValue(new Error("not needed"));
  return renderWithProviders(<ServerWorkspacePage />, {
    path: "/command/servers/:serverId",
    route: `/command/servers/${target.primaryKey}${search}`,
  });
}

describe("pending-start banner", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });

  it("states that saved changes apply at the next start and names the waiting area", async () => {
    renderPage(pendingServer(["Configuration"]));

    expect(
      await screen.findByText("Saved changes apply the next time UTF One starts."),
    ).toBeVisible();
    expect(screen.getByText("Waiting: Configuration")).toBeVisible();
  });

  it("shows the same banner on the Settings section", async () => {
    renderPage(pendingServer(["Configuration"]), "?tab=settings");

    expect(
      await screen.findByText("Saved changes apply the next time UTF One starts."),
    ).toBeVisible();
    expect(screen.getByText("Waiting: Configuration")).toBeVisible();
  });

  it("uses the running wording while the deployment may still be active", async () => {
    renderPage(pendingServer(["Configuration"], "Running"));

    expect(
      await screen.findByText(
        "Saved changes are waiting for the next start. UTF One is still running with the previous setup.",
      ),
    ).toBeVisible();
  });

  it("joins several areas with a localized conjunction", async () => {
    renderPage(pendingServer(["ModPack", "Credentials"]));

    expect(await screen.findByText("Waiting: Mod pack and Credentials")).toBeVisible();
    await i18n.changeLanguage("de");
    expect(await screen.findByText("Wartet: Mod-Paket und Zugangsdaten")).toBeVisible();
  });

  it("renders no banner when the signal is false or the fixture lacks the fields", async () => {
    const { container } = renderPage(pendingServer([]));
    await screen.findByRole("heading", { name: "UTF One" });

    expect(screen.queryByText(/Saved changes/)).not.toBeInTheDocument();
    expect(container.querySelector(".pending-start-notice__areas")).toBeNull();
  });

  it("renders no banner for a server without the pending-start fields", async () => {
    const legacy = pendingServer([]) as Partial<GameServer> as GameServer;
    delete (legacy as Partial<GameServer>).changesPendingStartAreas;
    delete (legacy as Partial<GameServer>).hasChangesPendingStart;
    renderPage(legacy);
    await screen.findByRole("heading", { name: "UTF One" });

    expect(screen.queryByText(/Saved changes/)).not.toBeInTheDocument();
  });

  it("removes the banner without a reload when the cached server stops reporting a pending start", async () => {
    const target = pendingServer(["Configuration"]);
    const { queryClient } = renderPage(target);
    expect(
      await screen.findByText("Saved changes apply the next time UTF One starts."),
    ).toBeVisible();

    queryClient.setQueryData(hostingQueryKeys.server(target.primaryKey), pendingServer([]));

    await waitFor(() =>
      expect(screen.queryByText("Saved changes apply the next time UTF One starts.")).toBeNull(),
    );
  });

  it("has no control inside the banner", async () => {
    renderPage(pendingServer(["Configuration"]));
    const text = await screen.findByText("Saved changes apply the next time UTF One starts.");
    const panel = text.closest("section");

    expect(panel).not.toBeNull();
    expect(panel?.querySelector("button, a, input")).toBeNull();
  });
});
