import type { QueryClient } from "@tanstack/react-query";
import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { pausedReason } from "@/components/server/unavailableReasonMapping";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import { workspaceFallbackPollMs } from "@/features/hosting/jobProgress";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, LifecycleJob } from "@/features/hosting/types";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";
import { FakeWebSocket } from "../helpers/fakeWebSocket";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { workspaceJob } from "../helpers/workspaceFixtures";
import { metricsHistory } from "../helpers/metricsHistoryFixtures";

/*
 * Shell behaviour of the server workspace page (D-07, D-08, D-09): the fleet signal and the active-job fallback poll,
 * Refresh server, the notices, the progress strip and failure panel, retry and the one-time job announcements.
 */

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-4000-8000-000000000001",
      nickname: "Falcon One",
      isBanned: false,
      isVerified: true,
      isDeleted: false,
      isVereinMember: true,
      claims: [
        {
          primaryKey: 1,
          identifier: "sv",
          value: null,
          title: "sv",
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

const configuration = {
  Api: { BaseUrl: "/api" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: false, ReactBasePath: "/" },
} as PublicConfiguration;

const pausedText = "Server actions are paused by the platform operator. Queued work is preserved.";
const archivedText = "This server record is archived. It can no longer be started.";
const requestErrorText = "The request could not be sent. Try again, or refresh the server.";

/** What the page reads: the server it shows and the jobs the next jobs request answers with. */
interface PageData {
  /** The server returned by the server request. */
  server: GameServer;
  /** The jobs the jobs request returns; read at request time so a test may change it. */
  jobs: () => LifecycleJob[];
}

/** Stubs every request of the page and renders it, optionally inside the fleet live updates provider. */
function renderPage(
  data: PageData,
  options: { search?: string; live?: boolean } = {},
): ReturnType<typeof renderWithProviders> {
  vi.spyOn(hostingService, "server").mockImplementation(async () => data.server);
  vi.spyOn(hostingService, "jobs").mockImplementation(async () => data.jobs());
  vi.spyOn(hostingService, "operations").mockResolvedValue([]);
  vi.spyOn(hostingService, "accruedCost").mockResolvedValue({
    gameServerId: data.server.primaryKey,
    session: null,
  });
  vi.spyOn(hostingService, "serverMetrics").mockResolvedValue({
    gameServerId: data.server.primaryKey,
    asOf: null,
    staleAfterSeconds: 45,
    runningSince: null,
    runningSinceUnavailableReason: "server-not-running",
    cpuPercent: null,
    cpuUnavailableReason: "server-not-running",
    memoryUsedBytes: null,
    memoryTotalBytes: null,
    memoryUnavailableReason: "server-not-running",
    playerCount: null,
    playerCapacity: null,
    playersUnavailableReason: "server-not-running",
  });
  vi.spyOn(hostingService, "serverMetricsHistory").mockResolvedValue(metricsHistory([]));
  vi.spyOn(hostingService, "files").mockResolvedValue([]);
  vi.spyOn(hostingService, "preservationSelections").mockResolvedValue([]);
  vi.spyOn(hostingService, "configuration").mockReturnValue(new Promise(() => {}));
  vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
  const page: ReactElement = options.live ? (
    <FleetLiveUpdatesProvider configuration={configuration}>
      <ServerWorkspacePage />
    </FleetLiveUpdatesProvider>
  ) : (
    <ServerWorkspacePage />
  );
  return renderWithProviders(page, {
    path: "/command/servers/:serverId",
    route: `/command/servers/${data.server.primaryKey}${options.search ?? ""}`,
  });
}

/** Builds page data whose jobs request returns `jobs`. */
function pageData(server: GameServer, jobs: LifecycleJob[] = []): PageData {
  return { server, jobs: () => jobs };
}

/** Evaluates the `refetchInterval` option of the observer of one query, as TanStack Query does. */
function intervalOf(queryClient: QueryClient, queryKey: readonly unknown[]): unknown {
  const query = queryClient.getQueryCache().find({ queryKey, exact: true });
  const value = query?.observers[0]?.options.refetchInterval;
  return typeof value === "function" ? value(query as never) : value;
}

/** Waits until the workspace header is rendered; the route heading above the loading state is also an h1. */
async function waitForWorkspace(): Promise<void> {
  await waitFor(() =>
    expect(screen.getByRole("heading", { level: 1 }).closest("header")).toHaveClass(
      "workspace-header",
    ),
  );
}

/** Returns the visually hidden polite region of the shell. */
function announcementRegion(): HTMLElement {
  const region = document.querySelector<HTMLElement>(".sr-only[aria-live='polite']");
  if (!region) throw new Error("Expected the announcement region.");
  return region;
}

describe("workspace shell", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("fallback poll", () => {
    it("polls the three workspace queries every 30 s only while a job is active", async () => {
      const idle = renderPage(pageData(fleetServer({ status: "Running" })));
      await waitForWorkspace();
      const idleId = serverIdOf(idle);
      expect(intervalOf(idle.queryClient, hostingQueryKeys.server(idleId))).toBe(false);
      expect(intervalOf(idle.queryClient, hostingQueryKeys.jobs(idleId))).toBe(false);
      expect(intervalOf(idle.queryClient, hostingQueryKeys.operations(idleId))).toBe(false);
      idle.unmount();

      const active = renderPage(
        pageData(fleetServer({ status: "Running", pendingJobCount: 1 }), [
          workspaceJob({ status: "Running" }),
        ]),
      );
      await waitForWorkspace();
      const activeId = serverIdOf(active);
      expect(workspaceFallbackPollMs).toBe(30_000);
      expect(intervalOf(active.queryClient, hostingQueryKeys.server(activeId))).toBe(30_000);
      expect(intervalOf(active.queryClient, hostingQueryKeys.jobs(activeId))).toBe(
        workspaceFallbackPollMs,
      );
      expect(intervalOf(active.queryClient, hostingQueryKeys.operations(activeId))).toBe(
        workspaceFallbackPollMs,
      );
    });

    it("also polls while only an active job is loaded", async () => {
      const view = renderPage(
        pageData(fleetServer({ status: "Running" }), [workspaceJob({ status: "Queued" })]),
      );
      await waitForWorkspace();

      await waitFor(() =>
        expect(intervalOf(view.queryClient, hostingQueryKeys.server(serverIdOf(view)))).toBe(
          workspaceFallbackPollMs,
        ),
      );
    });
  });

  describe("fleet signal", () => {
    function frameFor(serverId: string): string {
      return JSON.stringify({
        $id: "1",
        type: "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage",
        value: { $id: "2", serverId },
      });
    }

    it("refetches server, jobs and operations for this server's frame and never the files", async () => {
      const server = fleetServer({ status: "Running" });
      renderPage(pageData(server), { search: "?tab=files", live: true });
      await waitForWorkspace();
      await waitFor(() => expect(FakeWebSocket.instances.length).toBeGreaterThan(0));
      const socket = FakeWebSocket.instances.at(-1);
      if (!socket) throw new Error("Expected a socket.");
      act(() => socket.open());
      await waitFor(() => expect(hostingService.files).toHaveBeenCalled());
      const files = vi.mocked(hostingService.files).mock.calls.length;
      const loads = vi.mocked(hostingService.server).mock.calls.length;

      act(() => socket.message(frameFor("f1ee7000-0000-4000-8000-0000000000ff")));
      await new Promise((resolve) => setTimeout(resolve, 50));
      expect(vi.mocked(hostingService.server).mock.calls.length).toBe(loads);

      act(() => socket.message(frameFor(server.primaryKey)));
      await waitFor(() =>
        expect(vi.mocked(hostingService.server).mock.calls.length).toBeGreaterThan(loads),
      );
      await waitFor(() => expect(vi.mocked(hostingService.jobs).mock.calls.length).toBe(2));
      await waitFor(() => expect(vi.mocked(hostingService.operations).mock.calls.length).toBe(2));
      expect(vi.mocked(hostingService.files).mock.calls.length).toBe(files);
      expect(FakeWebSocket.instances).toHaveLength(1);
    });
  });

  describe("Refresh server", () => {
    it("invalidates exactly the seven keys with exact filters and surfaces a failed refetch", async () => {
      const user = userEvent.setup();
      const view = renderPage(pageData(fleetServer({ status: "Running" })));
      await waitForWorkspace();
      const id = serverIdOf(view);
      const invalidate = vi.spyOn(view.queryClient, "invalidateQueries");

      await user.click(screen.getByRole("button", { name: "Refresh server" }));

      await waitFor(() => expect(invalidate).toHaveBeenCalledTimes(7));
      expect(invalidate.mock.calls.map(([filters]) => JSON.stringify(filters?.queryKey))).toEqual(
        [
          hostingQueryKeys.fleet,
          hostingQueryKeys.server(id),
          hostingQueryKeys.jobs(id),
          hostingQueryKeys.operations(id),
          hostingQueryKeys.accruedCost(id),
          hostingQueryKeys.metrics(id),
          hostingQueryKeys.notifications,
        ].map((key) => JSON.stringify(key)),
      );
      invalidate.mock.calls.forEach(([filters, options]) => {
        expect(filters).toMatchObject({ exact: true });
        expect(options).toEqual({ throwOnError: true });
      });
      expect(screen.queryByText(requestErrorText)).not.toBeInTheDocument();

      invalidate.mockRejectedValue(new Error("refetch exploded"));
      await user.click(screen.getByRole("button", { name: "Refresh server" }));

      expect(await screen.findByRole("alert")).toHaveTextContent(requestErrorText);
      expect(screen.queryByText(/refetch exploded/)).not.toBeInTheDocument();
    });

    it("is disabled while a workspace query is fetching", async () => {
      const view = renderPage(pageData(fleetServer({ status: "Running" })));
      await waitForWorkspace();
      expect(screen.getByRole("button", { name: "Refresh server" })).toBeEnabled();

      vi.spyOn(hostingService, "jobs").mockReturnValue(new Promise(() => {}));
      act(() => {
        void view.queryClient.invalidateQueries({
          queryKey: hostingQueryKeys.jobs(serverIdOf(view)),
          exact: true,
        });
      });

      await waitFor(() =>
        expect(screen.getByRole("button", { name: "Refresh server" })).toBeDisabled(),
      );
    });
  });

  describe("notices", () => {
    it("shows the paused notice in every section and no notice otherwise", async () => {
      const paused = fleetServer({
        actions: [fleetAction("start", { enabled: false, unavailableReason: pausedReason })],
      });
      for (const search of ["", "?tab=activity", "?tab=settings"]) {
        const view = renderPage(pageData(paused), { search });
        await waitForWorkspace();
        // The paused reason also names why each action is unavailable; one of the matches is the notice.
        const matches = await screen.findAllByText(pausedText);
        expect(matches.some((match) => match.closest("[role='status']") !== null)).toBe(true);
        view.unmount();
      }

      renderPage(pageData(fleetServer()));
      await waitForWorkspace();
      expect(screen.queryByText(pausedText)).not.toBeInTheDocument();
      expect(screen.queryByText(archivedText)).not.toBeInTheDocument();
    });

    it("shows the archived notice in every section", async () => {
      const archived = { ...fleetServer(), isArchived: true } as GameServer;
      for (const search of ["", "?tab=activity", "?tab=settings"]) {
        const view = renderPage(pageData(archived), { search });
        await waitForWorkspace();
        expect(await screen.findByText(archivedText)).toBeVisible();
        view.unmount();
      }
    });
  });

  describe("progress strip and failure panel", () => {
    it("shows the progress strip above the section navigation while a job is active", async () => {
      renderPage(
        pageData(fleetServer({ status: "Running", pendingJobCount: 1 }), [
          workspaceJob({ status: "Running", kind: "Start" }),
        ]),
        { search: "?tab=activity" },
      );

      const kicker = await screen.findByText("Change in progress");
      const strip = kicker.closest("section");
      const navigation = screen.getByRole("navigation", { name: "Server sections" });
      if (!strip) throw new Error("Expected the progress strip.");
      expect(strip.compareDocumentPosition(navigation) & Node.DOCUMENT_POSITION_FOLLOWING).toBe(
        Node.DOCUMENT_POSITION_FOLLOWING,
      );
      expect(screen.queryByRole("heading", { name: /did not finish/ })).not.toBeInTheDocument();
    });

    it("shows the failure panel for a failed newest job and retries it by job id", async () => {
      const user = userEvent.setup();
      const job = workspaceJob({
        status: "Failed",
        canRetry: true,
        failureReason: "The provider rejected the request.",
        timeStampCompleted: "2026-08-13T12:04:00Z",
      });
      const retry = vi
        .spyOn(hostingService, "retryJob")
        .mockRejectedValueOnce(new Error("raw failure text"))
        .mockResolvedValue(undefined as never);
      renderPage(pageData(fleetServer({ status: "Running" }), [job]));

      const title = await screen.findByRole("heading", { name: "“Start server” did not finish" });
      const panel = title.closest("section");
      if (!panel) throw new Error("Expected the failure panel.");
      const navigation = screen.getByRole("navigation", { name: "Server sections" });
      expect(panel.compareDocumentPosition(navigation) & Node.DOCUMENT_POSITION_FOLLOWING).toBe(
        Node.DOCUMENT_POSITION_FOLLOWING,
      );

      await user.click(within(panel).getByRole("button", { name: "Try again" }));
      await waitFor(() => expect(retry).toHaveBeenCalledWith(job.primaryKey));
      await waitFor(() =>
        expect(within(panel).getByRole("alert")).toHaveTextContent(requestErrorText),
      );
      expect(screen.queryByText(/raw failure text/)).not.toBeInTheDocument();
    });
  });

  describe("job announcements", () => {
    it("announces a job that fails while the page is open exactly once, and nothing at load", async () => {
      const server = fleetServer({ status: "Running" });
      let jobs = [workspaceJob({ status: "Running", kind: "Start" })];
      const data: PageData = { server, jobs: () => jobs };
      const view = renderPage(data);
      await waitForWorkspace();
      await waitFor(() => expect(hostingService.jobs).toHaveBeenCalled());
      await screen.findByText("Change in progress");
      expect(announcementRegion()).toHaveTextContent("");

      const [running] = jobs;
      if (!running) throw new Error("Expected a job.");
      jobs = [{ ...running, status: "Failed", canRetry: true } as LifecycleJob];
      await act(async () => {
        await view.queryClient.invalidateQueries({
          queryKey: hostingQueryKeys.jobs(server.primaryKey),
          exact: true,
        });
      });

      await waitFor(() =>
        expect(announcementRegion()).toHaveTextContent("“Start server” did not finish."),
      );
      expect(announcementRegion().getAttribute("aria-live")).toBe("polite");
    });

    it("announces a job that finishes while the page is open", async () => {
      const server = fleetServer({ status: "Running" });
      let jobs = [workspaceJob({ status: "Running", kind: "Stop" })];
      const view = renderPage({ server, jobs: () => jobs });
      await screen.findByText("Change in progress");

      const [running] = jobs;
      if (!running) throw new Error("Expected a job.");
      jobs = [{ ...running, status: "Succeeded" } as LifecycleJob];
      await act(async () => {
        await view.queryClient.invalidateQueries({
          queryKey: hostingQueryKeys.jobs(server.primaryKey),
          exact: true,
        });
      });

      await waitFor(() =>
        expect(announcementRegion()).toHaveTextContent("“Shut down and stop billing” finished."),
      );
    });

    it("stays silent for a failed job that is already there when the page loads", async () => {
      renderPage(
        pageData(fleetServer({ status: "Running" }), [
          workspaceJob({ status: "Failed", canRetry: true }),
        ]),
      );

      await screen.findByRole("heading", { name: "“Start server” did not finish" });
      expect(announcementRegion()).toHaveTextContent("");
    });
  });
});

/** Reads the server id out of the route the page was rendered on. */
function serverIdOf(view: ReturnType<typeof renderWithProviders>): string {
  const match = /\/command\/servers\/([^/?]+)/.exec(view.router.state.location.pathname);
  if (!match?.[1]) throw new Error("Expected a server route.");
  return match[1];
}
