import { cleanup, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { ProfileMenu } from "@/components/ProfileMenu";
import { FleetServerCard } from "@/components/server/FleetServerCard";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, ServerDeployment, ServerNotification } from "@/features/hosting/types";
import { renderWithProviders } from "../helpers/renderWithProviders";

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-4000-8000-000000000001",
      nickname: "Falcon One",
      eMail: "falcon@example.test",
      avatar: null,
      avatarMimeType: null,
      isBanned: false,
      isVerified: true,
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
const serverTitle = "UTF Surface";
const rawStatusLabel = "RAW-NOTIFICATION-STATUS-LABEL";

interface Fixture {
  name: string;
  deployment: { status: string; possibleCost: boolean } | null;
  english: { state: string; cost: string };
}

const fixtures: Fixture[] = [
  {
    name: "running with possible cost",
    deployment: { status: "Running", possibleCost: true },
    english: { state: "Running", cost: "Costing now" },
  },
  {
    name: "stopped with possible cost",
    deployment: { status: "Stopped", possibleCost: true },
    english: { state: "Stop not confirmed", cost: "May still cost" },
  },
  {
    name: "without a deployment",
    deployment: null,
    english: { state: "Off", cost: "Not costing" },
  },
];

const forbiddenOffLabels = ["Off", "Aus", "Stopped", "Terminated"];

function makeServer(fixture: Fixture): GameServer {
  const deployment = fixture.deployment
    ? ({
        stage: "Ready",
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
        ...fixture.deployment,
      } as unknown as ServerDeployment)
    : null;
  return {
    primaryKey: serverId,
    title: serverTitle,
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
    actions: [],
  } as unknown as GameServer;
}

function makeNotification(fixture: Fixture): ServerNotification {
  return {
    serverId,
    title: serverTitle,
    statusLabel: rawStatusLabel,
    possibleCost: fixture.deployment?.possibleCost ?? false,
    canOperate: true,
    costStopAction: null,
  } as ServerNotification;
}

/** The state and cost texts one surface shows inside the given root. */
function readSummary(root: Element): { state: string; cost: string } {
  const summary = root.querySelector(".server-state-summary");
  expect(summary).not.toBeNull();
  return {
    state: summary!.querySelector(".server-state-badge")?.textContent ?? "",
    cost: summary!.querySelector("[data-cost]")?.textContent ?? "",
  };
}

function mockHosting(fixture: Fixture) {
  const server = makeServer(fixture);
  vi.spyOn(hostingService, "servers").mockResolvedValue([server]);
  vi.spyOn(hostingService, "server").mockResolvedValue(server);
  vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
  vi.spyOn(hostingService, "operations").mockResolvedValue([]);
  vi.spyOn(hostingService, "notifications").mockResolvedValue([makeNotification(fixture)]);
  return server;
}

async function card(fixture: Fixture) {
  const server = mockHosting(fixture);
  const { container } = renderWithProviders(
    <FleetServerCard server={server} submitted={false} onSubmitted={() => undefined} />,
  );
  const summary = readSummary(container);
  cleanup();
  return summary;
}

async function header(fixture: Fixture) {
  mockHosting(fixture);
  renderWithProviders(<ServerWorkspacePage />, {
    path: "/command/servers/:serverId",
    route: `/command/servers/${serverId}`,
  });
  const heading = await screen.findByRole("heading", { level: 1, name: serverTitle });
  const summary = readSummary(heading.closest("header")!);
  cleanup();
  return summary;
}

async function notificationRow(fixture: Fixture) {
  mockHosting(fixture);
  const { container } = renderWithProviders(<ProfileMenu mode="community" />);
  await userEvent
    .setup()
    .click(container.querySelector<HTMLButtonElement>(".account-profile__trigger")!);
  await waitFor(() =>
    expect(
      container.querySelector(".account-profile__notification .server-state-summary"),
    ).not.toBeNull(),
  );
  const row = container.querySelector(".account-profile__notification")!;
  // The fleet join resolves after the first render; wait until the state is no longer the unknown fallback.
  await waitFor(() => expect(readSummary(row).state).not.toBe(i18n.t("Server_State_Unknown")));
  const summary = readSummary(row);
  const rowText = row.textContent ?? "";
  cleanup();
  return { summary, rowText };
}

describe("one server state across the card, the workspace header and the notification row", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it.each(fixtures)(
    "shows identical state and cost text on every surface for a server $name",
    async (fixture) => {
      const onCard = await card(fixture);
      const onHeader = await header(fixture);
      const { summary: onRow } = await notificationRow(fixture);

      expect(onCard).toEqual(fixture.english);
      expect(onHeader).toEqual(fixture.english);
      expect(onRow).toEqual(fixture.english);
    },
  );

  it("shows identical German state and cost text on every surface", async () => {
    await i18n.changeLanguage("de");
    const fixture = fixtures[0]!;
    const german = { state: "Läuft", cost: "Kosten laufen" };

    expect(await card(fixture)).toEqual(german);
    expect(await header(fixture)).toEqual(german);
    expect((await notificationRow(fixture)).summary).toEqual(german);
  });

  it("never renders the raw notification status label", async () => {
    const { rowText } = await notificationRow(fixtures[0]!);

    expect(rowText).not.toContain(rawStatusLabel);
  });

  it.each(fixtures.filter((fixture) => fixture.deployment?.possibleCost))(
    "never labels a server that may still cost as off on any surface ($name)",
    async (fixture) => {
      const surfaces = [await card(fixture), await header(fixture)];
      surfaces.push((await notificationRow(fixture)).summary);

      for (const surface of surfaces) {
        expect(forbiddenOffLabels).not.toContain(surface.state);
        expect(surface.cost).not.toBe("Not costing");
      }
    },
  );
});
