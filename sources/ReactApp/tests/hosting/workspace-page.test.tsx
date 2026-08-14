import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import type { ReactNode } from "react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerConfigurationPanel } from "@/features/hosting/ServerConfigurationPanel";
import { ServerFilesPanel } from "@/features/hosting/ServerFilesPanel";
import { ServerRuntimePanel } from "@/features/hosting/ServerRuntimePanel";
import { ServerWorkspaceView } from "@/features/hosting/ServerWorkspacePage";
import { hostingService } from "@/features/hosting/services";
import type {
  GameConfiguration,
  GameServer,
  HostingOperation,
  LifecycleJob,
  ServerCapability,
} from "@/features/hosting/types";

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
      dangerous: true,
      confirmationText: "Start provider resources.",
      confirmationChallenge: "START",
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
      costImpact: "None",
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

function renderWorkspace(element: ReactNode, initialEntry = "/command") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter([{ path: "*", element }], { initialEntries: [initialEntry] });
  const result = render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
  return { ...result, router };
}

describe("certified server workspace", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
    vi.spyOn(window, "confirm").mockReturnValue(true);
  });

  it("requires the projected typed challenge and exposes jobs, audit, and diagnostic protocols", async () => {
    const user = userEvent.setup();
    const execute = vi.fn().mockResolvedValue(undefined);
    const retry = vi.fn();
    const skip = vi.fn();
    const operation = {
      primaryKey: "operation-1",
      kind: "LifecycleFailed",
      status: "failed",
      summary: "Preparation failed",
      detail: "Worker command failed.",
      providerIdentifier: "fake-cloud",
      resourceKind: "server",
      resourceIdentifier: "resource-1",
      traceId: "trace-1",
      timeStampCreated: "2026-08-13T10:05:00Z",
    } as HostingOperation;
    renderWorkspace(
      <ServerWorkspaceView
        server={server}
        jobs={[job]}
        operations={[operation]}
        actionBusy={false}
        jobBusy={false}
        onExecuteAction={execute}
        onRetryJob={retry}
        onSkipJob={skip}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Start" }));
    const dialog = screen.getByRole("dialog");
    await user.type(within(dialog).getByRole("textbox"), "START");
    await user.click(within(dialog).getByRole("button", { name: "Start" }));
    await waitFor(() => expect(execute).toHaveBeenCalledWith(server.actions[0], "START"));

    await user.click(screen.getByRole("button", { name: /Jobs \(1\)/ }));
    expect(screen.getByText(/Steam did not answer\./)).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Retry" }));
    await user.click(screen.getByRole("button", { name: "Skip" }));
    expect(retry).toHaveBeenCalledWith(job.primaryKey);
    expect(skip).toHaveBeenCalledWith(job.primaryKey);

    await user.click(screen.getByRole("button", { name: "Logs" }));
    expect(screen.getByText("Retry after Steam recovers.")).toBeVisible();
    expect(screen.getByRole("link", { name: "Download all protocols" })).toHaveAttribute(
      "download",
      "utf-server-protocols.txt",
    );
    await user.click(screen.getByRole("button", { name: "Audit" }));
    expect(screen.getByText("Preparation failed")).toBeVisible();
    expect(screen.getByText("trace-1")).toBeVisible();
  });

  it("browses both file contexts and performs confirmed persistent file and preservation mutations", async () => {
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
    const fileInput = screen.getByLabelText("File (maximum 16 MiB)");
    await user.upload(fileInput, new File(["hostname=test"], "server.cfg"));
    await user.click(
      screen.getByRole("checkbox", {
        name: "Create a persistent override of this generated target",
      }),
    );
    await user.click(screen.getByRole("button", { name: "Upload or replace" }));
    await waitFor(() =>
      expect(hostingService.uploadFile).toHaveBeenCalledWith(
        server.primaryKey,
        "persistent",
        "server.cfg",
        expect.any(File),
        true,
      ),
    );

    const notesRow = screen.getByText(/notes\.txt/).closest("tr")!;
    await user.click(within(notesRow).getByRole("button", { name: "Delete" }));
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
    await user.type(within(preservationForm).getByLabelText("Server-relative path"), "profiles");
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

    await user.click(screen.getByRole("tab", { name: "Live worker files" }));
    await waitFor(() =>
      expect(hostingService.files).toHaveBeenCalledWith(
        server.primaryKey,
        "live",
        "",
        expect.any(AbortSignal),
      ),
    );
    expect(screen.queryByRole("button", { name: "Upload or replace" })).not.toBeInTheDocument();
  });

  it("saves typed sparse configuration overrides, resets values, and selects profile mode", async () => {
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
          order: 1,
          minimum: 1,
          maximum: 128,
          allowedValues: [],
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
      },
    ]);
    vi.spyOn(hostingService, "files").mockResolvedValue([]);
    vi.spyOn(hostingService, "updateConfiguration").mockResolvedValue(configuration);
    vi.spyOn(hostingService, "selectConfigurationProfile").mockResolvedValue(undefined);
    renderWorkspace(<ServerConfigurationPanel serverId={server.primaryKey} canModify />);

    const players = await screen.findByLabelText("Maximum players");
    await user.clear(players);
    await user.type(players, "80");
    await user.click(screen.getByRole("button", { name: "Save overrides" }));
    await waitFor(() =>
      expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
        server.primaryKey,
        [{ key: "maxPlayers", value: 80 }],
        [],
      ),
    );
    await user.click(screen.getByRole("button", { name: "Reset to inherited" }));
    await waitFor(() =>
      expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
        server.primaryKey,
        [],
        ["maxPlayers"],
      ),
    );
    await user.click(screen.getByRole("button", { name: "Pin revision 2" }));
    expect(hostingService.selectConfigurationProfile).toHaveBeenCalledWith(
      server.primaryKey,
      "profile-1",
      "revision-2",
    );
  });

  it("changes desired compute, storage and mod-pack selections without external provider calls", async () => {
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
      runtimeProfiles: [],
      components: [],
      missingComponents: [],
    } as never);
    vi.spyOn(hostingService, "modPackOptions").mockResolvedValue([
      { primaryKey: 1, title: "Core" },
      { primaryKey: 2, title: "Extended" },
    ]);
    vi.spyOn(hostingService, "changeRuntimeProfile").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "changeRuntimeStorage").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "changeModPack").mockResolvedValue(undefined);
    renderWorkspace(<ServerRuntimePanel server={server} />);

    await user.selectOptions(
      await screen.findByLabelText("Profile for the next deployment"),
      "large",
    );
    await user.click(screen.getByRole("button", { name: "Save profile" }));
    await user.selectOptions(screen.getByLabelText("Storage strategy"), "FixedExternal");
    await user.clear(screen.getByLabelText("External capacity (GiB)"));
    await user.type(screen.getByLabelText("External capacity (GiB)"), "100");
    await user.click(screen.getByRole("button", { name: "Save deployment storage" }));
    await user.selectOptions(screen.getByLabelText("Mod pack"), "2");
    await user.click(screen.getByRole("button", { name: "Save mod pack" }));

    await waitFor(() => {
      expect(hostingService.changeRuntimeProfile).toHaveBeenCalledWith(server.primaryKey, "large");
      expect(hostingService.changeRuntimeStorage).toHaveBeenCalledWith(
        server.primaryKey,
        "FixedExternal",
        100,
      );
      expect(hostingService.changeModPack).toHaveBeenCalledWith(server.primaryKey, 2);
    });
  });

  it("renders the certified workspace navigation in German", async () => {
    await i18n.changeLanguage("de");
    renderWorkspace(
      <ServerWorkspaceView
        server={server}
        jobs={[]}
        operations={[]}
        actionBusy={false}
        jobBusy={false}
        onExecuteAction={vi.fn().mockResolvedValue(undefined)}
        onRetryJob={vi.fn()}
        onSkipJob={vi.fn()}
      />,
    );

    expect(screen.getByRole("button", { name: "Dateien" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Konfiguration" })).toBeVisible();
    expect(screen.getByText("Serverdaten")).toBeVisible();
  });

  it("deep-links workspace tabs and preserves unrelated action parameters", async () => {
    const user = userEvent.setup();
    const { router } = renderWorkspace(
      <ServerWorkspaceView
        server={server}
        jobs={[job]}
        operations={[]}
        actionBusy={false}
        jobBusy={false}
        onExecuteAction={vi.fn().mockResolvedValue(undefined)}
        onRetryJob={vi.fn()}
        onSkipJob={vi.fn()}
      />,
      "/command/servers/server-1?tab=jobs&action=start",
    );

    expect(screen.getByText(/Steam did not answer\./)).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Audit" }));
    expect(router.state.location.search).toBe("?tab=audit&action=start");
    await user.click(screen.getByRole("button", { name: "Overview" }));
    expect(router.state.location.search).toBe("?action=start");
  });
});
