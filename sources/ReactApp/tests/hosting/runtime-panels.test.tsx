import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { StorageDemandDetails } from "@/components/server/StorageDemandDetails";
import { ServerLogsPanel } from "@/features/hosting/ServerLogsPanel";
import { ServerRuntimePanel } from "@/features/hosting/ServerRuntimePanel";
import { CloudMachinePanel } from "@/features/hosting/sections/CloudMachinePanel";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, ServerCapability } from "@/features/hosting/types";
import { createStorageDemand } from "../helpers/createFixtures";
import { fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { workspaceCommand, workspaceJob } from "../helpers/workspaceFixtures";

const server = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  title: "UTF One",
  gameIdentifier: "arma3",
  runtimeProviderIdentifier: "fake-cloud",
  runtimeProfileIdentifier: "medium",
  runtimeStorageStrategy: "IncludedDisk",
  externalStorageSizeGiB: null,
  selectedTerrainId: 7,
  selectedScenarioId: "scenario-1",
  isArchived: false,
  canModify: true,
  deployment: null,
} as unknown as GameServer;

const capability = {
  gameIdentifier: "arma3",
  requiresTerrain: true,
  requiresScenario: true,
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

/** Answers every request of the runtime panel with the fixtures above. */
function mockRuntimeData() {
  vi.spyOn(hostingService, "capabilities").mockResolvedValue([capability]);
  vi.spyOn(hostingService, "storageDemand").mockResolvedValue({
    peakPreparationRequiredBytes: 90 * 1_073_741_824,
    minimumExternalCapacityGiB: 110,
    recommendedExternalUsableBytes: 98 * 1_073_741_824,
    runtimeProfiles: [],
    components: [],
    missingComponents: [],
  } as never);
  vi.spyOn(hostingService, "terrains").mockResolvedValue([
    { primaryKey: 7, title: "Altis" },
  ] as never);
  vi.spyOn(hostingService, "scenarios").mockResolvedValue([
    { primaryKey: "scenario-1", title: "Patrol" },
  ] as never);
}

describe("server runtime panel", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
    mockRuntimeData();
  });
  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("shows the content section in German without its former English literals", async () => {
    await i18n.changeLanguage("de");
    renderWithProviders(<ServerRuntimePanel server={server} />);

    expect(await screen.findByRole("heading", { name: "Karte und Szenario" })).toBeVisible();
    expect(screen.getByText("Inhalt")).toBeVisible();
    expect(screen.getByLabelText("Karte")).toBeVisible();
    expect(screen.getByLabelText("Szenario")).toBeVisible();
    expect(screen.getByText(/Nicht festgelegte Auswahlen/)).toBeVisible();
    expect(screen.getByRole("button", { name: "Inhaltsauswahl speichern" })).toBeVisible();
    for (const english of [
      "Terrain and scenario",
      "Unpinned selections follow the latest compatible version.",
      "Save content selection",
    ]) {
      expect(screen.queryByText(english)).not.toBeInTheDocument();
    }
  });

  it("renders the content section in English with labelled shared selects", async () => {
    renderWithProviders(<ServerRuntimePanel server={server} />);

    expect(await screen.findByRole("heading", { name: "Terrain and scenario" })).toBeVisible();
    const terrain = screen.getByLabelText("Terrain");
    expect(terrain.tagName).toBe("SELECT");
    expect(terrain).toHaveClass("form-field__control--select");
    expect(await within(terrain).findByRole("option", { name: "Altis" })).toBeInTheDocument();
    expect(screen.getByLabelText("Scenario")).toHaveClass("form-field__control--select");
    expect(screen.getByLabelText("Profile for the next deployment")).toHaveClass(
      "form-field__control--select",
    );
  });

  it("uses the capability supplied labels instead of the generic ones", async () => {
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([
      { ...capability, terrainLabel: "Map", scenarioLabel: "Mission" } as ServerCapability,
    ]);
    renderWithProviders(<ServerRuntimePanel server={server} />);

    expect(await screen.findByLabelText("Map")).toBeVisible();
    expect(screen.getByLabelText("Mission")).toBeVisible();
  });

  it("keeps the units literal and shows the external capacity as a labelled number field", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ServerRuntimePanel server={server} />);

    const profile = await screen.findByLabelText("Profile for the next deployment");
    expect(
      within(profile).getByRole("option", { name: /Medium · 4 vCPU · 8 GiB · .*\/h/ }),
    ).toBeInTheDocument();
    expect(screen.getByText("4 vCPU · 8 GiB RAM")).toBeVisible();
    await user.selectOptions(screen.getByLabelText("Storage strategy"), "FixedExternal");
    const capacity = screen.getByLabelText("External capacity (GiB)");
    expect(capacity).toHaveAttribute("type", "number");
    expect(capacity).toHaveClass("form-field__control");
  });

  it("saves the content selection through the shared button", async () => {
    const user = userEvent.setup();
    const change = vi.spyOn(hostingService, "changeContentSelection").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "terrains").mockResolvedValue([
      { primaryKey: 7, title: "Altis" },
      { primaryKey: 8, title: "Stratis" },
    ] as never);
    renderWithProviders(<ServerRuntimePanel server={server} />);

    const terrain = await screen.findByLabelText("Terrain");
    await within(terrain).findByRole("option", { name: "Stratis" });
    const save = screen.getByRole("button", { name: "Save content selection" });
    expect(save).toBeDisabled();
    await user.selectOptions(terrain, "8");
    await user.click(save);
    await waitFor(() => expect(change).toHaveBeenCalledWith(server.primaryKey, 8, "scenario-1"));
  });
});

/** Builds a running server whose deployment carries the administrative diagnostics of the machine panel. */
function machineServer(): GameServer {
  const base = fleetServer({ status: "Running" });
  return {
    ...base,
    deployment: {
      ...base.deployment,
      workerStatus: "Connected",
      lastHeartbeat: null,
      connectionEndpoint: "203.0.113.5:2302",
      connectionProtocol: "udp",
      publicAddress: null,
      deploymentRoot: "/srv/utf",
      selectedExternalStorageBytes: null,
      includedStorageBytes: 80 * 1_073_741_824,
      estimatedComputeHourlyCostEur: 0.1,
      estimatedStorageHourlyCostEur: 0,
      diskTotalBytes: null,
      diskAvailableBytes: null,
      diskTimeStampObserved: null,
      faultSummary: null,
      administrativeSshCommand: "ssh root@203.0.113.5",
      resources: [],
    },
  } as unknown as GameServer;
}

describe("machine, log and storage details", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });
  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("expands the machine diagnostics through a disclosure button", async () => {
    const user = userEvent.setup();
    const { container } = renderWithProviders(<CloudMachinePanel server={machineServer()} />);

    const toggle = screen.getByRole("button", { name: "Worker failure diagnostics" });
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByText("ssh root@203.0.113.5")).not.toBeVisible();
    await user.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText("ssh root@203.0.113.5")).toBeVisible();
    expect(container.querySelector("details")).toBeNull();
  });

  it("shows the machine panel in German with the translated diagnostics and empty state", async () => {
    await i18n.changeLanguage("de");
    const { unmount } = renderWithProviders(<CloudMachinePanel server={machineServer()} />);
    expect(screen.getByRole("button", { name: "Worker-Fehlerdiagnose" })).toBeVisible();
    expect(screen.queryByText("Worker failure diagnostics")).not.toBeInTheDocument();
    unmount();

    renderWithProviders(
      <CloudMachinePanel server={{ ...machineServer(), deployment: null } as GameServer} />,
    );
    expect(screen.getByText("Keine aktive Bereitstellung")).toBeVisible();
    expect(
      screen.getByText(
        "Der Datensatz und seine Daten bleiben erhalten. Starte den Server, um eine Maschine zu erstellen.",
      ),
    ).toBeVisible();
  });

  it("opens only the first log command and toggles each one through its own button", async () => {
    const user = userEvent.setup();
    const job = workspaceJob({
      status: "Failed",
      commands: [
        workspaceCommand({
          sequence: 1,
          actionId: "install-mods",
          status: "Failed",
          diagnosticOutput: "first output",
          timeStampStarted: "2026-08-13T12:10:00Z",
          timeStampCompleted: "2026-08-13T12:11:00Z",
        }),
        workspaceCommand({
          sequence: 2,
          actionId: "start-process",
          status: "Succeeded",
          diagnosticOutput: "second output",
          timeStampStarted: "2026-08-13T12:01:00Z",
          timeStampCompleted: "2026-08-13T12:02:00Z",
        }),
      ],
    });
    const { container } = renderWithProviders(
      <ServerLogsPanel server={machineServer()} jobs={[job]} collectLogsAction={undefined} />,
    );

    expect(container.querySelector("details")).toBeNull();
    const toggles = screen
      .getAllByRole("button", { expanded: false })
      .concat(screen.getAllByRole("button", { expanded: true }))
      .filter((button) => button.classList.contains("disclosure__summary"));
    expect(toggles).toHaveLength(2);
    const open = toggles.filter((button) => button.getAttribute("aria-expanded") === "true");
    expect(open).toHaveLength(1);
    expect(screen.getByText("first output")).toBeVisible();
    expect(screen.getByText("second output")).not.toBeVisible();
    const closed = toggles.find((button) => button.getAttribute("aria-expanded") === "false")!;
    await user.click(closed);
    expect(closed).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText("second output")).toBeVisible();
    await user.click(open[0]!);
    expect(open[0]).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByText("first output")).not.toBeVisible();
  });

  it("shows the storage details disclosure in German without English text", async () => {
    await i18n.changeLanguage("de");
    const user = userEvent.setup();
    renderWithProviders(
      <StorageDemandDetails demand={createStorageDemand()} selectedProfileIdentifier="medium" />,
    );

    const toggle = screen.getByRole("button", { name: "Speicherdetails" });
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    await user.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText("Komponentenübersicht")).toBeVisible();
    expect(screen.getByText("Kapazität des ausgewählten Laufzeitprofils")).toBeVisible();
    expect(screen.queryByText("Component breakdown")).not.toBeInTheDocument();
  });
});
