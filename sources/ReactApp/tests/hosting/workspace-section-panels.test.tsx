import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import type { ReactElement } from "react";
import { beforeEach } from "vitest";
import { QueryClient } from "@tanstack/react-query";
import i18n, { i18nReady } from "@/app/i18n";
import { CostConfirmationContext } from "@/components/server/useCostConfirmation";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import { formatMoney } from "@/features/hosting/model";
import { OverviewSection } from "@/features/hosting/sections/OverviewSection";
import { MachineSection } from "@/features/hosting/sections/MachineSection";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  GameConfiguration,
  GameServer,
  GameServerMetrics,
  ServerCapability,
} from "@/features/hosting/types";
import {
  fleetAction,
  fleetServer,
  serverAutomation,
  type FleetServerOptions,
} from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { workspaceJob } from "../helpers/workspaceFixtures";
import { metricsHistory } from "../helpers/metricsHistoryFixtures";

/* The Overview, Settings and Machine sections (WSP-02, CST-02, D-01, D-12). */

/** The cloud capability the runtime panel needs so that its storage section renders. */
const capability = {
  gameIdentifier: "arma3",
  runtimeProviderProfiles: [
    {
      identifier: "hetzner",
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
} as unknown as ServerCapability;

/** Mocks the service calls the runtime panel makes while the Machine section is mounted. */
function mockRuntimeServices(): void {
  vi.spyOn(hostingService, "capabilities").mockResolvedValue([capability]);
  vi.spyOn(hostingService, "storageDemand").mockResolvedValue({
    peakPreparationRequiredBytes: 90 * 1_073_741_824,
    minimumExternalCapacityGiB: 110,
    recommendedExternalUsableBytes: 98 * 1_073_741_824,
    runtimeProfiles: [],
    components: [],
    missingComponents: [],
  } as never);
  vi.spyOn(hostingService, "modPackOptions").mockResolvedValue([]);
  vi.spyOn(hostingService, "terrains").mockResolvedValue([]);
  vi.spyOn(hostingService, "scenarios").mockResolvedValue([]);
}

/** Builds a fleet server and merges the fields of the full server projection the sections read. */
function fullServer(
  options: FleetServerOptions = {},
  overrides: Record<string, unknown> = {},
): GameServer {
  const base = fleetServer(options);
  return {
    ...base,
    runtimeProfileIdentifier: "medium",
    runtimeProfileDisplayName: "Medium",
    runtimeStorageStrategy: "IncludedDisk",
    externalStorageSizeGiB: null,
    estimatedHourlyCostEur: 0.1,
    estimatedMonthlyCostEur: 73,
    selectedModPackDefinitionId: null,
    selectedModPackDefinitionTitle: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    selectedTerrainId: null,
    selectedTerrainTitle: null,
    selectedScenarioId: null,
    selectedScenarioTitle: null,
    canModify: true,
    canManageSecrets: false,
    timeStampCreated: "2026-08-13T10:00:00Z",
    ...overrides,
  } as unknown as GameServer;
}

/** Renders `ui` with a spy that stands in for the shared cost confirmation. */
function renderWithSpy(ui: ReactElement, route = "/") {
  const request = vi.fn();
  const result = renderWithProviders(
    <CostConfirmationContext.Provider value={{ request }}>{ui}</CostConfirmationContext.Provider>,
    { route },
  );
  return { request, ...result };
}

describe("MachineSection", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockRuntimeServices();
  });

  it("warns in plain words that the server may still cost money and requests the shared shutdown", () => {
    const server = fullServer({ status: "Running", actions: [fleetAction("stop")] });
    const { request } = renderWithSpy(<MachineSection server={server} />);

    expect(
      screen.getByText(
        "This server may still be costing money. Billing stops once the cloud provider confirms the machine is gone.",
      ),
    ).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Shut down and stop billing" }));

    expect(request).toHaveBeenCalledTimes(1);
    expect(request).toHaveBeenCalledWith({
      serverId: server.primaryKey,
      actionId: "stop",
      source: "workspace-machine",
    });
  });

  it("shows only a disabled shutdown without saving with its mapped reason when nothing else stops cost", () => {
    const server = fullServer({
      status: "Running",
      actions: [
        fleetAction("terminate", {
          enabled: false,
          dangerous: true,
          unavailableReason: "Server Operate permission is required.",
        }),
      ],
    });
    const { request } = renderWithSpy(<MachineSection server={server} />);

    const button = screen.getByRole("button", { name: "Shut down without saving" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).toHaveAccessibleDescription(
      "You need permission to operate this server. Ask the server owner or an administrator.",
    );
    fireEvent.click(button);
    expect(request).not.toHaveBeenCalled();
  });

  it("shows no cost notice for an off server without a deployment, only the no-deployment panel", () => {
    const server = fullServer({ actions: [fleetAction("start")] });
    renderWithSpy(<MachineSection server={server} />);

    expect(screen.queryByText(/may still be costing money/)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Shut down/ })).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Deployment and cost" })).toBeVisible();
    expect(screen.getByText("No active deployment")).toBeVisible();
    expect(
      screen.getByText(
        "The record and its data stay available. Start the server to create a machine.",
      ),
    ).toBeVisible();
  });

  it("omits the cost button when a costing server has no cost-stop action", () => {
    const server = fullServer({ status: "Running", actions: [] });
    renderWithSpy(<MachineSection server={server} />);

    expect(screen.getByText(/may still be costing money/)).toBeVisible();
    expect(screen.queryByRole("button", { name: /Shut down/ })).not.toBeInTheDocument();
  });

  it("shows the technical status, the localized hourly amounts and never a /h suffix", () => {
    const base = fullServer({ status: "Running", actions: [fleetAction("stop")] });
    const server = {
      ...base,
      deployment: {
        ...base.deployment,
        workerStatus: "Connected",
        estimatedComputeHourlyCostEur: 0.1,
        estimatedStorageHourlyCostEur: 0.02,
        resources: [
          {
            primaryKey: "r-1",
            kind: "Server",
            state: "Running",
            possibleCost: true,
            providerResourceIdentifier: "srv-1",
            providerState: "running",
            provisionedCapacityBytes: 1_073_741_824,
            estimatedHourlyCostEur: 0.03,
          },
        ],
      },
    } as unknown as GameServer;
    const { container } = renderWithSpy(<MachineSection server={server} />);

    expect(screen.getByText("Technical status")).toBeVisible();
    expect(screen.getByText("Stage")).toBeVisible();
    expect(screen.getByText(`${formatMoney(0.1)} per hour`)).toBeVisible();
    expect(screen.getByText(`${formatMoney(0.02)} per hour`)).toBeVisible();
    expect(screen.getByText(new RegExp(`${formatMoney(0.03)} per hour`))).toBeVisible();
    const cloud = container.querySelector(".next-hosting-resource-grid");
    expect(cloud?.textContent).not.toContain("/h");
    expect(screen.queryByText(/\/h$/)).not.toBeInTheDocument();
  });

  it("states the external storage deletion in the stop-billing vocabulary", async () => {
    const server = fullServer(
      { status: "Running", actions: [fleetAction("stop")] },
      { runtimeStorageStrategy: "FixedExternal", externalStorageSizeGiB: 120 },
    );
    renderWithSpy(<MachineSection server={server} />);

    const note = await screen.findByText(
      "External storage is deleted when you shut the server down and stop billing, or shut it down without saving.",
    );
    expect(note).toBeVisible();
    await waitFor(() => expect(hostingService.capabilities).toHaveBeenCalled());
    expect(within(document.body).queryByText(/full cost-stop or termination/)).toBeNull();
  });

  it("keeps the runtime profile, storage and content panels and offers no mod pack control (D-01)", async () => {
    const server = fullServer({ actions: [fleetAction("start")] });
    renderWithSpy(<MachineSection server={server} />);

    expect(await screen.findByLabelText("Profile for the next deployment")).toBeVisible();
    expect(screen.getByLabelText("Storage strategy")).toBeVisible();
    await waitFor(() => expect(hostingService.capabilities).toHaveBeenCalled());
    expect(screen.queryByLabelText("Mod pack")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save mod pack" })).not.toBeInTheDocument();
    expect(hostingService.modPackOptions).not.toHaveBeenCalled();
  });
});

/** The digits 0 to 9 anywhere in a text. */

describe("SettingsSection", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "configuration").mockResolvedValue({
      serverId: "s",
      profileId: null,
      profileRevisionId: null,
      profileRevisionNumber: null,
      isComplete: true,
      entries: [],
    } as unknown as GameConfiguration);
    vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "files").mockResolvedValue([]);
    vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as never);
    vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
  });

  it("shows the configuration panel and, without canManageSecrets, no credentials at all", async () => {
    renderWithSpy(<SettingsSection server={fullServer()} jobs={[]} />);

    expect(await screen.findByRole("heading", { name: "Configuration" })).toBeVisible();
    expect(screen.queryByRole("heading", { name: "Credentials" })).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Server credentials" })).not.toBeInTheDocument();
    expect(hostingService.secrets).not.toHaveBeenCalled();
  });

  it("shows the credentials panel for a member who may manage secrets", async () => {
    renderWithSpy(
      <SettingsSection server={fullServer({}, { canManageSecrets: true })} jobs={[]} />,
    );

    expect(screen.getByRole("heading", { name: "Credentials" })).toBeVisible();
    expect(await screen.findByText(/Values are encrypted at rest/)).toBeVisible();
    expect(screen.queryByRole("heading", { name: "Server credentials" })).not.toBeInTheDocument();
    expect(hostingService.secrets).toHaveBeenCalled();
  });

  it("mounts the automation panel between the credentials and the server record", () => {
    const { container } = renderWithSpy(<SettingsSection server={fullServer()} jobs={[]} />);

    expect(screen.getByRole("heading", { name: "Automatic shutdown and schedules" })).toBeVisible();
    const slot = container.querySelector('[data-slot="automation"]');
    expect(slot).not.toBeNull();
    // The collapsible panel around the slot carries the anchor id of the deep link.
    expect(slot?.closest("section#server-automation")).not.toBeNull();
    // fullServer carries no automation block, so the panel shows its unavailable state and no form.
    expect(slot?.textContent).toContain(
      "Automatic shutdown and schedules are not available for this server right now.",
    );
  });

  it("places the automation panel after the credentials and before the server record", async () => {
    renderWithSpy(
      <SettingsSection server={fullServer({}, { canManageSecrets: true })} jobs={[]} />,
    );

    const credentials = await screen.findByRole("heading", { name: "Credentials" });
    const automation = screen.getByRole("heading", { name: "Automatic shutdown and schedules" });
    const record = screen.getByRole("heading", { name: "Server record" });
    expect(credentials.compareDocumentPosition(automation)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(automation.compareDocumentPosition(record)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  it("disables Delete server with the existing reason while a deployment exists", () => {
    const server = fullServer({ status: "Running", actions: [fleetAction("stop")] });
    renderWithSpy(<SettingsSection server={server} jobs={[]} />);

    const button = screen.getByRole("button", { name: "Delete server" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).not.toHaveAttribute("title");
    expect(button).toHaveAccessibleDescription(
      "Fully stop the server and wait for lifecycle work to finish before deleting it.",
    );
    fireEvent.click(button);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("disables Delete server while a job can still be retried", () => {
    const job = { primaryKey: "j", canRetry: true, canSkip: false } as never;
    renderWithSpy(<SettingsSection server={fullServer()} jobs={[job]} />);

    expect(screen.getByRole("button", { name: "Delete server" })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
  });

  it("deletes a modifiable idle server after confirmation and returns to the fleet", async () => {
    const remove = vi.spyOn(hostingService, "delete").mockResolvedValue(undefined);
    const server = fullServer();
    const { router } = renderWithSpy(<SettingsSection server={server} jobs={[]} />, "/command/x");

    fireEvent.click(screen.getByRole("button", { name: "Delete server" }));
    const dialog = await screen.findByRole("dialog", { name: "Delete server" });
    expect(within(dialog).getByRole("button", { name: "Keep server record" })).toBeVisible();
    expect(remove).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole("button", { name: "Delete server" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/command/servers"));
    expect(remove).toHaveBeenCalledTimes(1);
    expect(remove.mock.calls[0]?.[0]).toBe(server.primaryKey);
  });

  it("shows a role=alert message when the deletion fails", async () => {
    vi.spyOn(hostingService, "delete").mockRejectedValue(new Error("boom"));
    renderWithSpy(<SettingsSection server={fullServer()} jobs={[]} />);

    fireEvent.click(screen.getByRole("button", { name: "Delete server" }));
    const dialog = await screen.findByRole("dialog", { name: "Delete server" });
    fireEvent.click(within(dialog).getByRole("button", { name: "Delete server" }));

    const alert = await screen.findByText("The server could not be deleted. Try again.");
    expect(alert).toHaveAttribute("role", "alert");
  });

  it("hides the server record for an archived server and for a viewer without canModify", () => {
    const archived = renderWithSpy(
      <SettingsSection server={fullServer({}, { isArchived: true })} jobs={[]} />,
    );
    expect(screen.queryByRole("heading", { name: "Server record" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete server" })).not.toBeInTheDocument();
    archived.unmount();

    renderWithSpy(<SettingsSection server={fullServer({}, { canModify: false })} jobs={[]} />);
    expect(screen.queryByRole("heading", { name: "Server record" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete server" })).not.toBeInTheDocument();
  });
});

/** Builds `count` finished jobs, the first being the oldest and the last the newest. */
function finishedJobs(count: number) {
  return Array.from({ length: count }, (_, index) => {
    const at = `2026-08-13T${String(10 + index).padStart(2, "0")}:00:00Z`;
    return workspaceJob({
      kind: index % 2 === 0 ? "Start" : "Stop",
      status: "Succeeded",
      timeStampCreated: at,
      timeStampUpdated: at,
      timeStampCompleted: at,
    });
  });
}

/** A current reading with a value for every metric. */
const currentMetrics: GameServerMetrics = {
  gameServerId: "s",
  asOf: "2026-11-21T14:05:00Z",
  staleAfterSeconds: 45,
  runningSince: "2026-11-21T10:00:00Z",
  runningSinceUnavailableReason: null,
  cpuPercent: 37.4,
  cpuUnavailableReason: null,
  memoryUsedBytes: 2_684_354_560,
  memoryTotalBytes: 7_516_192_768,
  memoryUnavailableReason: null,
  playerCount: 4,
  playerCapacity: 16,
  playersUnavailableReason: null,
};

describe("OverviewSection", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(currentMetrics);
    vi.spyOn(hostingService, "serverMetricsHistory").mockResolvedValue(metricsHistory([]));
  });

  it("lists the server details without a cost owner or a cost and with defaults for unset values", () => {
    const server = fullServer({ costOwnerName: "Operations" });
    const { container } = renderWithSpy(
      <OverviewSection server={server} jobs={[]} operations={[]} />,
    );

    const details = screen.getByRole("heading", { name: "Server details" }).closest("section");
    if (!details) throw new Error("expected the details panel");
    const scope = within(details);
    expect(scope.getByText("Game")).toBeVisible();
    expect(scope.getByText("Arma 3")).toBeVisible();
    expect(scope.getByText("Cloud provider")).toBeVisible();
    expect(scope.getByText("hetzner")).toHaveAttribute("lang", "en");
    expect(scope.getByText("Cloud profile")).toBeVisible();
    expect(scope.getByText("Medium")).toBeVisible();
    expect(scope.getByText("medium").tagName).toBe("CODE");
    expect(scope.getByText("Configuration profile")).toBeVisible();
    expect(scope.getByText("Game defaults")).toBeVisible();
    expect(scope.getByText("Mod pack")).toBeVisible();
    expect(scope.getByText("None")).toBeVisible();
    expect(scope.getByText("Launch policy")).toBeVisible();
    expect(scope.getByText("Enabled")).toBeVisible();
    expect(scope.getByText("Created")).toBeVisible();
    expect(scope.getByText("Last changed")).toBeVisible();
    expect(scope.queryByText("Terrain")).not.toBeInTheDocument();
    expect(scope.queryByText("Scenario")).not.toBeInTheDocument();
    expect(container.textContent).not.toMatch(/Cost owner|Estimated cost|Operations/);
  });

  it("shows terrain and scenario only when they are set", () => {
    const server = fullServer(
      {},
      {
        selectedTerrainTitle: "Altis",
        selectedScenarioTitle: "Escape",
        selectedConfigurationProfileTitle: "Ops profile",
        selectedModPackDefinitionTitle: "Core",
        isLaunchDisabled: true,
      },
    );
    renderWithSpy(<OverviewSection server={server} jobs={[]} operations={[]} />);

    expect(screen.getByText("Terrain")).toBeVisible();
    expect(screen.getByText("Altis")).toBeVisible();
    expect(screen.getByText("Scenario")).toBeVisible();
    expect(screen.getByText("Escape")).toBeVisible();
    expect(screen.getByText("Ops profile")).toBeVisible();
    expect(screen.getByText("Core")).toBeVisible();
    expect(screen.getByText("Disabled")).toBeVisible();
  });

  describe("Automation fact", () => {
    /** Builds an automation block with the given saved settings over the nothing-saved defaults. */
    function automationWith(
      settings: Partial<ReturnType<typeof serverAutomation>["settings"]>,
      nextScheduled: { action: "Start" | "Stop"; at: string } | null = null,
    ) {
      const base = serverAutomation();
      return serverAutomation({
        settings: { ...base.settings, ...settings },
        status: { ...base.status, nextScheduled },
      } as never);
    }

    /** Renders the Overview and returns the details panel. */
    function renderDetails(server: GameServer, title = "Server details"): HTMLElement {
      renderWithSpy(<OverviewSection server={server} jobs={[]} operations={[]} />);
      const details = screen.getByRole("heading", { name: title }).closest("section");
      if (!details) throw new Error("expected the details panel");
      return details;
    }

    it("renders no row without an automation block", () => {
      const details = renderDetails(fullServer({ automation: null }));

      expect(within(details).queryByText("Automation")).not.toBeInTheDocument();
      expect(
        within(details).queryByRole("link", { name: "Open automation settings" }),
      ).not.toBeInTheDocument();
    });

    it("renders no row when idle shutdown, the runtime limit and the schedules are all off", () => {
      const details = renderDetails(fullServer({ automation: automationWith({}) }));

      expect(within(details).queryByText("Automation")).not.toBeInTheDocument();
      expect(within(details).queryByText(/Shuts down after/)).not.toBeInTheDocument();
    });

    it("shows the idle line only when only idle shutdown is on, after Last changed", () => {
      const details = renderDetails(
        fullServer({
          automation: automationWith({ idleShutdownEnabled: true, idleShutdownMinutes: 30 }),
        }),
      );

      expect(within(details).getByText("Automation")).toBeVisible();
      expect(
        within(details).getByText("Shuts down after 30 minutes without players"),
      ).toBeVisible();
      expect(within(details).queryByText(/of running/)).not.toBeInTheDocument();
      expect(within(details).queryByText(/Next:/)).not.toBeInTheDocument();
      const terms = Array.from(details.querySelectorAll("dt")).map((term) => term.textContent);
      expect(terms.at(-2)).toBe("Last changed");
      expect(terms.at(-1)).toBe("Automation");
    });

    it("uses the singular form for one hour of running and omits the idle line when it is off", () => {
      const details = renderDetails(
        fullServer({
          automation: automationWith({
            maxRuntimeEnabled: true,
            maxRuntimeHours: 1,
            idleShutdownEnabled: false,
            idleShutdownMinutes: 45,
          }),
        }),
      );

      expect(within(details).getByText("Shuts down after 1 hour of running")).toBeVisible();
      expect(within(details).queryByText(/without players/)).not.toBeInTheDocument();
    });

    it("shows all three lines with the next action in the saved time zone and the settings link", () => {
      const details = renderDetails(
        fullServer({
          automation: automationWith(
            {
              idleShutdownEnabled: true,
              idleShutdownMinutes: 30,
              maxRuntimeEnabled: true,
              maxRuntimeHours: 6,
              timeZone: "Europe/Berlin",
            },
            { action: "Start", at: "2026-08-14T18:00:00Z" },
          ),
        }),
      );

      expect(
        within(details).getByText("Shuts down after 30 minutes without players"),
      ).toBeVisible();
      expect(within(details).getByText("Shuts down after 6 hours of running")).toBeVisible();
      const next = within(details).getByText(/^Next: Start server, /);
      expect(next).toHaveTextContent(/Fri, Aug 14, (8:00 PM|20:00)/);
      const link = within(details).getByRole("link", { name: "Open automation settings" });
      expect(link).toHaveAttribute(
        "href",
        `/command/servers/${fleetServer().primaryKey}?tab=settings#server-automation`,
      );
    });

    it("shows the row with the link only when a schedule exists but nothing is due next", () => {
      const details = renderDetails(
        fullServer({
          automation: automationWith({
            schedules: [
              {
                id: "s1",
                action: "Start",
                days: [5],
                time: "18:00",
                nextRunAt: null,
                lastRun: null,
              },
            ] as never,
          }),
        }),
      );

      expect(within(details).getByText("Automation")).toBeVisible();
      expect(within(details).queryByText(/Next:/)).not.toBeInTheDocument();
      expect(within(details).getByRole("link", { name: "Open automation settings" })).toBeVisible();
    });

    it("renders the German lines", async () => {
      await i18n.changeLanguage("de");
      const details = renderDetails(
        fullServer({
          automation: automationWith({ idleShutdownEnabled: true, idleShutdownMinutes: 30 }),
        }),
        "Serverdetails",
      );

      expect(within(details).getByText("Automatisierung")).toBeVisible();
      expect(
        within(details).getByText("Fährt nach 30 Minuten ohne Spieler herunter"),
      ).toBeVisible();
      expect(
        within(details).getByRole("link", { name: "Automatisierungseinstellungen öffnen" }),
      ).toBeVisible();
    });
  });

  it("shows the metrics panel with four tiles in order after the details panel", async () => {
    const { container } = renderWithSpy(
      <OverviewSection server={fullServer()} jobs={[]} operations={[]} />,
    );

    expect(screen.getByRole("heading", { name: "Server metrics" })).toBeVisible();
    const panel = container.querySelector('[data-slot="metrics"]');
    if (!panel) throw new Error("expected the metrics panel");
    expect(panel.tagName).toBe("SECTION");
    expect(panel).toHaveTextContent("How the server is doing right now and over time.");
    expect(panel).not.toHaveTextContent("Not available yet");

    const details = screen.getByRole("heading", { name: "Server details" }).closest("section");
    const recent = screen.getByRole("heading", { name: "Recent activity" }).closest("section");
    if (!details || !recent) throw new Error("expected the neighbouring panels");
    expect(details.compareDocumentPosition(panel) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(panel.compareDocumentPosition(recent) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    expect(await within(panel as HTMLElement).findByText("37%")).toBeVisible();
    const terms = Array.from(panel.querySelectorAll("dt")).map((term) => term.textContent);
    expect(terms).toEqual(["Running since", "Players", "CPU", "Memory"]);
    const values = Array.from(panel.querySelectorAll("dd")).map((value) => value.textContent);
    expect(values).toEqual([
      formatInstant("2026-11-21T10:00:00Z"),
      "4of 16 slots",
      "37%",
      "2.5 GiBof 7 GiB",
    ]);
    expect(hostingService.serverMetrics).toHaveBeenCalledTimes(1);
  });

  it("shows the five newest merged entries compactly and links to the activity section", () => {
    const server = fullServer();
    const jobs = finishedJobs(8);
    renderWithSpy(<OverviewSection server={server} jobs={jobs} operations={[]} />);

    const recent = screen.getByRole("heading", { name: "Recent activity" }).closest("section");
    if (!recent) throw new Error("expected the recent activity panel");
    expect(within(recent).getAllByRole("article")).toHaveLength(5);
    expect(within(recent).queryByRole("button", { name: "Show details" })).toBeNull();
    for (const article of within(recent).getAllByRole("article")) {
      expect(article).not.toHaveAttribute("id");
      expect(article).not.toHaveAttribute("tabindex");
    }
    const link = within(recent).getByRole("link", { name: "Show all activity" });
    expect(link.getAttribute("href")).toContain("tab=activity");
  });

  it("shows the empty text and no link when there is no activity", () => {
    renderWithSpy(<OverviewSection server={fullServer()} jobs={[]} operations={[]} />);

    expect(
      screen.getByText(
        "Jobs, server logs, and platform events for this server appear here once something happens.",
      ),
    ).toBeVisible();
    expect(screen.queryByRole("link", { name: "Show all activity" })).not.toBeInTheDocument();
  });

  it("lists the remaining actions, leaves out collect logs and the header primary and opens the typed confirmation", async () => {
    const terminate = fleetAction("terminate", {
      dangerous: true,
      confirmationChallenge: "Fleet Server 1",
      confirmationText: "This removes the deployment.",
    });
    const server = fullServer({
      status: "Running",
      actions: [fleetAction("stop"), terminate, fleetAction("collect-logs")],
    });
    vi.spyOn(hostingService, "server").mockResolvedValue(server);
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    queryClient.setQueryData(hostingQueryKeys.fleet, [server]);
    renderWithProviders(<OverviewSection server={server} jobs={[]} operations={[]} />, {
      queryClient,
    });

    const panel = screen.getByRole("heading", { name: "More actions" }).closest("section");
    if (!panel) throw new Error("expected the actions panel");
    expect(within(panel).getByRole("button", { name: "Shut down without saving" })).toBeVisible();
    expect(within(panel).queryByText("Shut down and stop billing")).not.toBeInTheDocument();
    expect(within(panel).queryByText("Collect logs")).not.toBeInTheDocument();
    fireEvent.click(within(panel).getByRole("button", { name: "Shut down without saving" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("textbox")).toBeVisible();
  });

  it("omits the more actions panel when nothing is left after the primary action", () => {
    const server = fullServer({ actions: [fleetAction("start"), fleetAction("collect-logs")] });
    renderWithSpy(<OverviewSection server={server} jobs={[]} operations={[]} />);

    expect(screen.queryByRole("heading", { name: "More actions" })).not.toBeInTheDocument();
  });
});
