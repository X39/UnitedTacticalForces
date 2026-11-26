import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerFilesPanel } from "@/features/hosting/ServerFilesPanel";
import { ServerRuntimePanel } from "@/features/hosting/ServerRuntimePanel";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingService } from "@/features/hosting/services";
import type {
  GameConfiguration,
  GameServer,
  HostingFile,
  HostingOperation,
  LifecycleJob,
  ServerAction,
  ServerCapability,
} from "@/features/hosting/types";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { workspaceOperation } from "../helpers/workspaceFixtures";
import { metricsHistory } from "../helpers/metricsHistoryFixtures";

const server = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  title: "UTF One",
  gameIdentifier: "arma3",
  runtimeProviderIdentifier: "fake-cloud",
  runtimeProfileIdentifier: "medium",
  runtimeProfileDisplayName: "Medium",
  runtimeStorageStrategy: "IncludedDisk",
  externalStorageSizeGiB: null,
  estimatedHourlyCostEur: 0.1,
  estimatedMonthlyCostEur: 73,
  selectedModPackDefinitionId: 1,
  selectedModPackDefinitionTitle: "Core",
  selectedConfigurationProfileId: null,
  selectedConfigurationProfileTitle: null,
  createdById: "user-1",
  costOwnerId: "owner-1",
  costOwnerName: "Operations",
  isLaunchDisabled: false,
  isArchived: false,
  canModify: true,
  timeStampCreated: "2026-08-13T10:00:00Z",
  timeStampUpdated: "2026-08-13T11:00:00Z",
  pendingJobCount: 1,
  deployment: null,
  actions: [
    {
      id: "start",
      label: "Start",
      description: "Provision and start the server.",
      icon: "play",
      group: "Lifecycle",
      order: 1,
      enabled: true,
      unavailableReason: null,
      dangerous: false,
      confirmationText: "Start provider resources.",
      confirmationChallenge: null,
      pending: false,
      expectedDurationSeconds: 600,
      costImpact: "StartsCost",
      costDescription: "Provider cost starts.",
    },
    {
      id: "collect-logs",
      label: "Collect logs",
      description: "Collect diagnostics.",
      icon: "document",
      group: "Diagnostics",
      order: 50,
      enabled: true,
      unavailableReason: null,
      dangerous: false,
      confirmationText: null,
      confirmationChallenge: null,
      pending: false,
      expectedDurationSeconds: 20,
      costImpact: "NoCostImpact",
      costDescription: "",
    },
  ],
} as unknown as GameServer;

const job = {
  primaryKey: "20000000-0000-4000-8000-000000000002",
  actionId: "prepare",
  kind: "Prepare",
  status: "Failed",
  stage: "workshop",
  progressPercent: 62,
  attemptCount: 2,
  canRetry: true,
  canSkip: true,
  failureCode: "steam-timeout",
  failureReason: "Steam did not answer.",
  timeStampCreated: "2026-08-13T10:00:00Z",
  timeStampUpdated: "2026-08-13T10:05:00Z",
  commands: [
    {
      primaryKey: "30000000-0000-4000-8000-000000000003",
      sequence: 1,
      actionId: "install-mods",
      stage: "workshop",
      status: "Failed",
      failureCode: "steam-timeout",
      failureReason: "Timed out",
      diagnosticOutput: "Retry after Steam recovers.",
      timeStampCreated: "2026-08-13T10:00:00Z",
      timeStampStarted: "2026-08-13T10:01:00Z",
      timeStampCompleted: "2026-08-13T10:05:00Z",
    },
  ],
} as unknown as LifecycleJob;

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

function renderWorkspace(element: ReactNode, initialEntry = "/command") {
  return renderWithProviders(<>{element}</>, { route: initialEntry });
}

/** Renders the whole workspace page for `target` with its three data requests answered by the given data. */
function renderPage(
  target: GameServer,
  data: {
    jobs?: LifecycleJob[];
    operations?: HostingOperation[] | Promise<HostingOperation[]>;
  } = {},
  search = "",
) {
  vi.spyOn(hostingService, "server").mockResolvedValue(target);
  vi.spyOn(hostingService, "accruedCost").mockResolvedValue({
    gameServerId: target.primaryKey,
    session: null,
  });
  vi.spyOn(hostingService, "serverMetrics").mockResolvedValue({
    gameServerId: target.primaryKey,
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
  vi.spyOn(hostingService, "jobs").mockResolvedValue(data.jobs ?? []);
  vi.spyOn(hostingService, "operations").mockImplementation(async () => data.operations ?? []);
  vi.spyOn(hostingService, "serverMetricsHistory").mockResolvedValue(metricsHistory([]));
  vi.spyOn(hostingService, "files").mockResolvedValue([]);
  vi.spyOn(hostingService, "preservationSelections").mockResolvedValue([]);
  return renderWithProviders(<ServerWorkspacePage />, {
    path: "/command/servers/:serverId",
    route: `/command/servers/${target.primaryKey}${search}`,
  });
}

/** Returns the activity entry article with `id`. */
function articleById(id: string): HTMLElement {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Expected an article with id ${id}`);
  return element;
}

const terminateAction = {
  id: "terminate",
  label: "Terminate",
  description: "Release all provider resources.",
  icon: "stop",
  group: "Lifecycle",
  order: 9,
  enabled: true,
  unavailableReason: null,
  dangerous: true,
  confirmationText: "This removes the deployment.",
  confirmationChallenge: "UTF One",
  pending: false,
  expectedDurationSeconds: 60,
  costImpact: "StopsCost",
  costDescription: "Stops provider cost.",
} as unknown as ServerAction;

/**
 * Per-test timeout for the slow interaction tests below. They click through many controls with real timers and
 * pass in about two seconds alone, but exceed the default 5 s when the full suite saturates the workers.
 */
const slowTestTimeout = 15_000;

describe("certified server workspace", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
    vi.spyOn(window, "confirm").mockReturnValue(true);
  });

  it(
    "requests the shared confirmation for Start and exposes jobs, audit, and diagnostic protocols",
    async () => {
      const user = userEvent.setup();
      const execute = vi.spyOn(hostingService, "executeAction");
      const retry = vi.spyOn(hostingService, "retryJob").mockResolvedValue(undefined as never);
      const skip = vi.spyOn(hostingService, "skipJob").mockResolvedValue(undefined as never);
      const operation = workspaceOperation({
        kind: "WorkerDegraded",
        status: "warning",
        summary: "Preparation failed",
        traceId: "trace-1",
      });
      renderPage(server, { jobs: [job], operations: [operation] });

      await user.click(await screen.findByRole("button", { name: "Start server" }));
      const dialog = await screen.findByRole("dialog", { name: "Start UTF One?" });
      expect(within(dialog).queryByRole("textbox")).not.toBeInTheDocument();
      expect(execute).not.toHaveBeenCalled();
      await user.click(within(dialog).getByRole("button", { name: "Don't start server" }));
      await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
      expect(execute).not.toHaveBeenCalled();

      await user.click(
        within(screen.getByRole("navigation", { name: "Server sections" })).getByRole("link", {
          name: "Activity",
        }),
      );
      const jobEntry = articleById(`activity-job-${job.primaryKey}`);
      await user.click(within(jobEntry).getByRole("button", { name: "Show details" }));
      expect(within(jobEntry).getByText(/Steam did not answer\./)).toBeVisible();
      await user.click(within(jobEntry).getByRole("button", { name: "Try again" }));
      await waitFor(() => expect(retry).toHaveBeenCalledWith(job.primaryKey));
      await user.click(within(jobEntry).getByRole("button", { name: "Skip and continue" }));
      await waitFor(() => expect(skip).toHaveBeenCalledWith(job.primaryKey));

      const command = job.commands[0];
      if (!command) throw new Error("expected a command");
      const logEntry = articleById(`activity-log-${command.primaryKey}`);
      await user.click(within(logEntry).getByRole("button", { name: "Show details" }));
      expect(within(logEntry).getByText("Retry after Steam recovers.")).toBeVisible();
      expect(within(logEntry).getByRole("link", { name: "Download log" })).toHaveAttribute(
        "download",
        `utf-command-${command.primaryKey}.txt`,
      );
      expect(screen.getByRole("link", { name: "Download all logs" })).toHaveAttribute(
        "download",
        "utf-server-protocols.txt",
      );
      const auditEntry = articleById(`activity-audit-${operation.primaryKey}`);
      await user.click(within(auditEntry).getByRole("button", { name: "Show details" }));
      expect(within(auditEntry).getByText("Preparation failed")).toBeVisible();
      await user.click(within(auditEntry).getByText("Technical evidence"));
      expect(within(auditEntry).getByText("trace-1")).toBeVisible();
    },
    slowTestTimeout,
  );

  it("opens the typed-challenge dialog for a secondary terminate action without executing", async () => {
    const user = userEvent.setup();
    const withTerminate = {
      ...server,
      actions: [...server.actions, terminateAction],
    } as GameServer;
    const execute = vi.spyOn(hostingService, "executeAction");
    renderPage(withTerminate);

    await user.click(await screen.findByRole("button", { name: "Shut down without saving" }));
    const dialog = await screen.findByRole("dialog", {
      name: "Shut down UTF One now without saving?",
    });
    expect(within(dialog).getByRole("textbox")).toBeVisible();
    await user.click(within(dialog).getByRole("button", { name: "Keep server running" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(execute).not.toHaveBeenCalled();
  });

  it("does not present a pending audit request as an empty history", async () => {
    const user = userEvent.setup();
    renderPage(server, { operations: new Promise<HostingOperation[]>(() => {}) });

    await screen.findByRole("heading", { level: 1, name: "UTF One" });
    await user.click(
      within(screen.getByRole("navigation", { name: "Server sections" })).getByRole("link", {
        name: "Activity",
      }),
    );
    expect(screen.getByText("Loading operational activity…")).toBeVisible();
    expect(screen.queryByRole("heading", { name: "No activity yet" })).not.toBeInTheDocument();
  });

  it(
    "browses both file contexts and performs confirmed persistent file and preservation mutations",
    async () => {
      const user = userEvent.setup();
      vi.spyOn(hostingService, "files").mockResolvedValue([
        {
          path: "server.cfg",
          name: "server.cfg",
          isDirectory: false,
          size: 20,
          modifiedAt: "2026-08-13T10:00:00Z",
          context: "Persistent",
          classification: "Configuration",
          canModify: true,
          origin: "Generated",
          modificationMode: "StructuredConfiguration",
          isGeneratedTarget: true,
          overridesGenerated: false,
          configurationStatus: "Ready",
        },
        {
          path: "notes.txt",
          name: "notes.txt",
          isDirectory: false,
          size: 4,
          modifiedAt: "2026-08-13T09:00:00Z",
          context: "Persistent",
          classification: "Unknown",
          canModify: true,
          origin: "Persistent",
          modificationMode: "RawFile",
          isGeneratedTarget: false,
          overridesGenerated: false,
          configurationStatus: "NotApplicable",
        },
      ]);
      vi.spyOn(hostingService, "preservationSelections").mockResolvedValue([]);
      vi.spyOn(hostingService, "fileDownloadUrl").mockReturnValue(
        "http://localhost/api/game-servers/server/files/persistent/content?path=notes.txt",
      );
      vi.spyOn(hostingService, "uploadFile").mockResolvedValue({} as never);
      vi.spyOn(hostingService, "deleteFile").mockResolvedValue(undefined);
      vi.spyOn(hostingService, "savePreservationSelection").mockResolvedValue({} as never);
      renderWorkspace(<ServerFilesPanel server={server} />);

      expect(await screen.findByText(/notes\.txt/)).toBeVisible();
      await user.click(screen.getByRole("button", { name: "Upload" }));
      const fileInput = screen.getByLabelText("File (maximum 16 MiB)");
      await user.upload(fileInput, new File(["hostname=test"], "server.cfg"));
      await user.click(
        await screen.findByRole("checkbox", {
          name: "Replace 1 existing file(s)",
        }),
      );
      await user.click(
        screen.getByRole("checkbox", {
          name: "Create a persistent override of this generated target",
        }),
      );
      await user.click(
        within(screen.getByRole("dialog", { name: "Upload or replace file" })).getByRole("button", {
          name: "Upload",
        }),
      );
      await waitFor(() =>
        expect(hostingService.uploadFile).toHaveBeenCalledWith(
          server.primaryKey,
          "persistent",
          "server.cfg",
          expect.any(File),
          true,
          expect.any(AbortSignal),
        ),
      );
      await user.click(
        within(screen.getByRole("dialog", { name: "Upload or replace file" })).getByRole("button", {
          name: "Don't upload files",
        }),
      );

      const notesRow = screen.getByText(/notes\.txt/).closest("tr")!;
      await user.click(within(notesRow).getByRole("button", { name: "Delete notes.txt" }));
      await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Delete" }));
      await waitFor(() =>
        expect(hostingService.deleteFile).toHaveBeenCalledWith(
          server.primaryKey,
          "persistent",
          "notes.txt",
          false,
        ),
      );

      const preservationForm = screen
        .getByRole("heading", { name: "Add preservation selection" })
        .closest("form")!;
      await user.type(
        within(preservationForm).getByRole("textbox", { name: "Server-relative path" }),
        "profiles",
      );
      await user.selectOptions(
        within(preservationForm).getByLabelText("Selection scope"),
        "RecursiveFolder",
      );
      await user.click(within(preservationForm).getByRole("button", { name: "Add selection" }));
      await waitFor(() =>
        expect(hostingService.savePreservationSelection).toHaveBeenCalledWith(server.primaryKey, {
          path: "profiles",
          scope: "RecursiveFolder",
          mode: "CreateOrUpdate",
        }),
      );

      await user.click(screen.getByRole("radio", { name: "Live worker files" }));
      await waitFor(() =>
        expect(hostingService.files).toHaveBeenCalledWith(
          server.primaryKey,
          "live",
          "",
          expect.any(AbortSignal),
        ),
      );
      expect(screen.queryByRole("button", { name: "Upload" })).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: /^Delete / })).not.toBeInTheDocument();
    },
    slowTestTimeout,
  );

  it(
    "lazy-loads folders, creates in the current path, filters entries, and retries a partial folder upload",
    async () => {
      const user = userEvent.setup();
      const folder = (path: string, name: string) =>
        ({
          path,
          name,
          isDirectory: true,
          size: null,
          modifiedAt: null,
          context: "Persistent",
          classification: "Unknown",
          canModify: true,
          origin: "Persistent",
          modificationMode: "RawFile",
          isGeneratedTarget: false,
          overridesGenerated: false,
          configurationStatus: "NotApplicable",
        }) as HostingFile;
      const file = (path: string, name: string) =>
        ({
          ...folder(path, name),
          isDirectory: false,
          size: 12_700,
          modifiedAt: "2026-08-13T09:00:00Z",
        }) as HostingFile;
      vi.spyOn(hostingService, "files").mockImplementation(async (_serverId, _context, path) => {
        if (path === "") return [folder("profiles", "profiles"), file("readme.txt", "readme.txt")];
        if (path === "profiles") return [file("profiles/server.cfg", "server.cfg")];
        return [];
      });
      vi.spyOn(hostingService, "preservationSelections").mockResolvedValue([]);
      vi.spyOn(hostingService, "fileDownloadUrl").mockReturnValue("http://localhost/download");
      vi.spyOn(hostingService, "createDirectory").mockResolvedValue({} as never);
      const upload = vi
        .spyOn(hostingService, "uploadFile")
        .mockResolvedValueOnce({} as never)
        .mockRejectedValueOnce(new Error("network interruption"))
        .mockResolvedValue({} as never);
      renderWorkspace(<ServerFilesPanel server={server} />);

      const profiles = await screen.findByRole("treeitem", { name: /profiles/ });
      await user.click(profiles);
      await waitFor(() =>
        expect(hostingService.files).toHaveBeenCalledWith(
          server.primaryKey,
          "persistent",
          "profiles",
          expect.any(AbortSignal),
        ),
      );
      expect(await screen.findByText("12.4 KiB")).toBeVisible();

      await user.type(screen.getByRole("searchbox", { name: "Search this folder" }), "missing");
      expect(screen.getByText("No files match")).toBeVisible();
      await user.clear(screen.getByRole("searchbox", { name: "Search this folder" }));

      await user.click(screen.getByRole("button", { name: "Create folder" }));
      const folderDialog = screen.getByRole("dialog", { name: "New directory" });
      await user.type(
        within(folderDialog).getByRole("textbox", { name: "Folder name" }),
        "missions",
      );
      await user.click(within(folderDialog).getByRole("button", { name: "Create folder" }));
      await waitFor(() =>
        expect(hostingService.createDirectory).toHaveBeenCalledWith(
          server.primaryKey,
          "persistent",
          "profiles/missions",
        ),
      );

      await user.click(screen.getByRole("button", { name: "Upload" }));
      const uploadDialog = screen.getByRole("dialog", { name: "Upload or replace file" });
      const first = new File(["first"], "a.txt");
      const second = new File(["second"], "b.txt");
      Object.defineProperty(first, "webkitRelativePath", { value: "pack/sub/a.txt" });
      Object.defineProperty(second, "webkitRelativePath", { value: "pack/sub/b.txt" });
      const directoryInput =
        uploadDialog.querySelector<HTMLInputElement>("input[webkitdirectory]")!;
      await user.upload(directoryInput, [first, second]);
      await screen.findByText("pack/sub/a.txt");
      await user.click(within(uploadDialog).getByRole("button", { name: "Upload" }));

      await waitFor(() => expect(upload).toHaveBeenCalledTimes(2));
      expect(hostingService.createDirectory).toHaveBeenCalledWith(
        server.primaryKey,
        "persistent",
        "profiles/pack",
        expect.any(AbortSignal),
      );
      expect(hostingService.createDirectory).toHaveBeenCalledWith(
        server.primaryKey,
        "persistent",
        "profiles/pack/sub",
        expect.any(AbortSignal),
      );
      await user.click(
        await within(uploadDialog).findByRole("button", { name: "Retry failed (1)" }),
      );
      await waitFor(() => expect(upload).toHaveBeenCalledTimes(3));

      await user.click(within(uploadDialog).getByRole("button", { name: "Don't upload files" }));
      await user.click(screen.getByRole("button", { name: "Upload" }));
      const duplicateDialog = screen.getByRole("dialog", { name: "Upload or replace file" });
      const duplicateInput = within(duplicateDialog).getByLabelText("File (maximum 16 MiB)");
      await user.upload(duplicateInput, [
        new File(["one"], "duplicate.txt"),
        new File(["two"], "duplicate.txt"),
      ]);
      expect(
        await within(duplicateDialog).findByText("Another selected file has the same destination."),
      ).toBeVisible();
    },
    slowTestTimeout,
  );

  it(
    "stages typed sparse configuration overrides, applies them and a reset, and selects profile mode",
    async () => {
      const user = userEvent.setup();
      const configuration: GameConfiguration = {
        serverId: server.primaryKey,
        profileId: "profile-1",
        profileRevisionId: "revision-2",
        profileRevisionNumber: 2,
        isComplete: true,
        entries: [
          {
            key: "maxPlayers",
            targetPath: "server.cfg",
            group: "General",
            displayName: "Maximum players",
            description: "Player capacity.",
            kind: "Integer",
            required: true,
            sensitive: false,
            isControlPlaneOnly: false,
            tier: "Advanced",
            order: 1,
            minimum: 1,
            maximum: 128,
            allowedValues: [],
            choices: [],
            value: 64,
            source: "Server",
            isCompatible: true,
            isRedacted: false,
            warning: null,
          },
        ],
      };
      vi.spyOn(hostingService, "configuration").mockResolvedValue(configuration);
      vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([
        {
          primaryKey: "profile-1",
          title: "Operations",
          gameIdentifier: "arma3",
          isArchived: false,
          latestRevisionNumber: 2,
          latestRevisionId: "revision-2",
        },
      ]);
      vi.spyOn(hostingService, "files").mockResolvedValue([]);
      vi.spyOn(hostingService, "updateConfiguration").mockResolvedValue(configuration);
      vi.spyOn(hostingService, "selectConfigurationProfile").mockResolvedValue(undefined);
      renderWorkspace(<SettingsSection server={server} jobs={[]} />);

      const players = await screen.findByLabelText("Maximum players");
      await user.clear(players);
      await user.type(players, "80");
      expect(hostingService.updateConfiguration).not.toHaveBeenCalled();
      await user.click(screen.getByRole("button", { name: "Review changes" }));
      const applyDialog = await screen.findByRole("dialog", { name: "Review changes" });
      await user.click(within(applyDialog).getByRole("button", { name: "Apply 1 change" }));
      await waitFor(() =>
        expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
          server.primaryKey,
          [{ key: "maxPlayers", value: 80 }],
          [],
        ),
      );
      await waitFor(() =>
        expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
      );
      await user.click(screen.getByRole("button", { name: /^Reset to inherited/ }));
      await user.click(screen.getByRole("button", { name: "Review changes" }));
      const resetDialog = await screen.findByRole("dialog", { name: "Review changes" });
      await user.click(within(resetDialog).getByRole("button", { name: "Apply 1 change" }));
      await waitFor(() =>
        expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
          server.primaryKey,
          [],
          ["maxPlayers"],
        ),
      );
      await waitFor(() =>
        expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
      );
      // The profile and its revision mode are staged too: nothing is sent before the review is applied.
      await user.selectOptions(screen.getByLabelText("Configuration profile"), "profile-1");
      await user.click(screen.getByRole("radio", { name: "Stay on revision 2" }));
      expect(hostingService.selectConfigurationProfile).not.toHaveBeenCalled();
      await user.click(screen.getByRole("button", { name: "Review changes" }));
      const profileDialog = await screen.findByRole("dialog", { name: "Review changes" });
      await user.click(within(profileDialog).getByRole("button", { name: "Apply 1 change" }));
      await waitFor(() =>
        expect(hostingService.selectConfigurationProfile).toHaveBeenCalledWith(
          server.primaryKey,
          "profile-1",
          "revision-2",
        ),
      );
    },
    slowTestTimeout,
  );

  it("changes desired compute and storage selections without external provider calls", async () => {
    const user = userEvent.setup();
    const capability = {
      gameIdentifier: "arma3",
      runtimeProviderProfiles: [
        {
          identifier: "fake-cloud",
          availabilityStatus: "available",
          profiles: [
            {
              identifier: "medium",
              displayName: "Medium",
              virtualCpuCount: 4,
              memoryGiB: 8,
              diskGiB: 80,
              usableStorageBytes: 70 * 1_073_741_824,
              estimatedHourlyCostEur: 0.1,
              estimatedMonthlyCostEur: 73,
            },
            {
              identifier: "large",
              displayName: "Large",
              virtualCpuCount: 8,
              memoryGiB: 16,
              diskGiB: 160,
              usableStorageBytes: 150 * 1_073_741_824,
              estimatedHourlyCostEur: 0.2,
              estimatedMonthlyCostEur: 146,
            },
          ],
          storage: {
            supportsExternalBlockStorage: true,
            minimumCapacityGiB: 20,
            maximumCapacityGiB: 500,
            capacityIncrementGiB: 10,
            estimatedHourlyCostPerGiBEur: 0.001,
            deploymentMountPath: "/srv/utf",
            readinessFailure: null,
          },
        },
      ],
    } as ServerCapability;
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([capability]);
    vi.spyOn(hostingService, "storageDemand").mockResolvedValue({
      peakPreparationRequiredBytes: 90 * 1_073_741_824,
      minimumExternalCapacityGiB: 110,
      recommendedExternalUsableBytes: 98 * 1_073_741_824,
      runtimeProfiles: [],
      components: [],
      missingComponents: [],
    } as never);
    vi.spyOn(hostingService, "changeRuntimeProfile").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "changeRuntimeStorage").mockResolvedValue(undefined);
    renderWorkspace(<ServerRuntimePanel server={server} />);

    await user.selectOptions(
      await screen.findByLabelText("Profile for the next deployment"),
      "large",
    );
    await user.click(screen.getByRole("button", { name: "Save profile" }));
    await user.selectOptions(screen.getByLabelText("Storage strategy"), "FixedExternal");
    await user.clear(screen.getByLabelText("External capacity (GiB)"));
    await user.type(screen.getByLabelText("External capacity (GiB)"), "100");
    expect(screen.getByRole("button", { name: "Save deployment storage" })).toBeDisabled();
    await user.clear(screen.getByLabelText("External capacity (GiB)"));
    await user.type(screen.getByLabelText("External capacity (GiB)"), "110");
    await user.click(screen.getByRole("button", { name: "Save deployment storage" }));

    await waitFor(() => {
      expect(hostingService.changeRuntimeProfile).toHaveBeenCalledWith(server.primaryKey, "large");
      expect(hostingService.changeRuntimeStorage).toHaveBeenCalledWith(
        server.primaryKey,
        "FixedExternal",
        110,
      );
      expect(screen.queryByLabelText("Mod pack")).not.toBeInTheDocument();
    });
  });

  it("renders the certified workspace navigation in German", async () => {
    await i18n.changeLanguage("de");
    renderPage(server);

    expect(await screen.findByRole("link", { name: "Dateien" })).toBeVisible();
    expect(screen.getByRole("link", { name: "Einstellungen" })).toBeVisible();
    expect(screen.getByRole("navigation", { name: "Serverbereiche" })).toBeVisible();
  });

  it("moves between sections with links that write only the tab parameter", async () => {
    const user = userEvent.setup();
    const { router } = renderPage(server, { jobs: [job] }, "?tab=activity&action=unknown-action");

    const navigation = await screen.findByRole("navigation", { name: "Server sections" });
    expect(router.state.location.search).toBe("?tab=activity&action=unknown-action");
    await user.click(within(navigation).getByRole("link", { name: "Settings" }));
    expect(router.state.location.search).toBe("?tab=settings");
    await user.click(within(navigation).getByRole("link", { name: "Overview" }));
    expect(router.state.location.search).toBe("");
  });

  it("opens Overview for a retired tab value and leaves the URL unchanged", async () => {
    const { router } = renderPage(server, { jobs: [job] }, "?tab=jobs");

    expect(await screen.findByRole("heading", { level: 2, name: "Server details" })).toBeVisible();
    expect(router.state.location.search).toBe("?tab=jobs");
  });

  it("offers five section links in order as a nav landmark without tabs or buttons", async () => {
    renderPage(server, {}, "?tab=files");

    const navigation = await screen.findByRole("navigation", { name: "Server sections" });
    const links = within(navigation).getAllByRole("link");
    expect(links.map((link) => link.textContent)).toEqual([
      "Overview",
      "Activity",
      "Settings",
      "Files",
      "Machine",
    ]);
    expect(links.filter((link) => link.getAttribute("aria-current") === "page")).toEqual([
      within(navigation).getByRole("link", { name: "Files" }),
    ]);
    expect(within(navigation).queryByRole("tab")).not.toBeInTheDocument();
    expect(within(navigation).queryByRole("button")).not.toBeInTheDocument();
  });
});
