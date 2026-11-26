import { act, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { ProfileMenu } from "@/components/ProfileMenu";
import { CostAlertBanner } from "@/components/server/CostAlertBanner";
import {
  accruedCostMessageType,
  costAlertDismissedMessageType,
  costAlertRaisedMessageType,
} from "@/features/hosting/costFrames";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { CostAlert } from "@/features/hosting/types";
import { useCostAlerts } from "@/features/hosting/useCostAlerts";
import { costAlert } from "../helpers/costAlertFixtures";
import { FakeWebSocket } from "../helpers/fakeWebSocket";
import { fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/*
 * Pins D-17 and D-08: a cost alert leaves the viewer's surfaces only through a dismissal by this viewer (an accepted
 * request in this tab or a dismissed frame from another tab) or through the server's own list. The real
 * FleetLiveUpdatesProvider, the workspace banner and the profile menu run against a socket fake, so every path that
 * could hide or resurrect an alert is exercised end to end.
 */

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", () => ({
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

const configuration = {
  Api: { BaseUrl: "/api" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: false, ReactBasePath: "/" },
} as PublicConfiguration;

const operator = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Falcon One",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: true,
  steamId64: "76561198012345678",
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
} as unknown as FullUser;

const server = fleetServer({ index: 1, title: "Ops Night", status: "Running" });
const triggerName = "Open profile menu for Falcon One";

/** Matches the banner title for one alert ("Cost alert") and for several ("2 cost alerts"). */
const bannerHeading = /^(\d+ cost alerts|Cost alert)$/;

function alertAt(threshold: number): CostAlert {
  return costAlert({
    id: `a-${server.primaryKey}-${threshold}`,
    serverId: server.primaryKey,
    serverName: server.title,
    thresholdAmount: threshold,
    accruedAmount: threshold + 0.02,
    createdAt: `2026-11-21T14:${String(threshold).padStart(2, "0")}:00Z`,
  });
}

function money(value: number): string {
  return new Intl.NumberFormat("en-US", { style: "currency", currency: "EUR" }).format(value);
}

function raisedFrame(alert: CostAlert): string {
  return JSON.stringify({
    $id: "1",
    type: costAlertRaisedMessageType,
    value: {
      $id: "2",
      alertId: alert.id,
      serverId: alert.serverId,
      serverName: alert.serverName,
      costOwnerId: alert.costOwnerId,
      thresholdAmount: alert.thresholdAmount,
      accruedAmount: alert.accruedAmount,
      currency: alert.currency,
      createdAt: alert.createdAt,
    },
  });
}

function dismissedFrame(alertId: string): string {
  return JSON.stringify({ type: costAlertDismissedMessageType, value: { alertId } });
}

const accruedFrame = JSON.stringify({
  type: accruedCostMessageType,
  value: {
    serverId: server.primaryKey,
    deploymentId: "d0000000-0000-4000-8000-000000000001",
    amount: 5.5,
    currency: "EUR",
    asOf: "2026-11-21T14:10:00Z",
    isFinal: false,
    unavailableReason: null,
  },
});

const fleetChangedFrame = JSON.stringify({
  $id: "1",
  type: "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage",
  value: { $id: "2", serverId: server.primaryKey },
});

/** Shows what `useCostAlerts` reports so a test can read `loaded` without a visible surface. */
function LoadedProbe() {
  const { loaded, alerts } = useCostAlerts();
  return <p data-testid="probe">{`loaded:${loaded}:${alerts.length}`}</p>;
}

/** Renders the provider with the workspace banner, the profile menu and the probe on one alerts cache. */
function renderSurfaces() {
  const view = renderWithProviders(
    <FleetLiveUpdatesProvider configuration={configuration}>
      <LoadedProbe />
      <CostAlertBanner server={server} />
      <ProfileMenu mode="command" />
    </FleetLiveUpdatesProvider>,
    { route: "/command" },
  );
  return view;
}

function latestSocket(): FakeWebSocket {
  const socket = FakeWebSocket.instances.at(-1);
  if (!socket) throw new Error("Expected a socket.");
  return socket;
}

async function deliver(data: string): Promise<void> {
  await act(async () => {
    latestSocket().message(data);
    await Promise.resolve();
  });
}

/** Opens the menu and returns the user event instance. */
async function openMenu() {
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: triggerName }));
  return user;
}

/** Waits until no alerts fetch runs. */
async function settled(queryClient: { isFetching(filters: object): number }): Promise<void> {
  await waitFor(() =>
    expect(queryClient.isFetching({ queryKey: hostingQueryKeys.costAlerts })).toBe(0),
  );
}

/** Opens the socket after a short pause, so the open sees data older than itself, and waits for the refetch. */
async function openSocket(queryClient: { isFetching(filters: object): number }): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 5));
  act(() => latestSocket().open());
  await settled(queryClient);
}

/** The ids of the alert rows of the profile menu. */
function menuRowIds(): string[] {
  return Array.from(document.querySelectorAll<HTMLElement>("[data-cost-alert-id]")).map(
    (row) => row.dataset.costAlertId ?? "",
  );
}

/** The number of alert rows of the workspace banner. */
function bannerRowCount(): number {
  return document.querySelectorAll(".cost-alert-banner__row").length;
}

/** Whether any arrival toast of this server is on screen. */
function toastShown(): boolean {
  return screen.queryAllByText(/^Cost alert: Ops Night reached/).length > 0;
}

describe("cost alerts stay until the viewer dismisses them", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    authState.user = operator;
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
    vi.spyOn(hostingService, "servers").mockResolvedValue([]);
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([]);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  it("keeps the banner and the menu rows when a refetch after the first load fails", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertAt(5), alertAt(10)]);
    const { queryClient } = renderSurfaces();
    await openMenu();
    await screen.findByRole("heading", { name: bannerHeading });
    expect(bannerRowCount()).toBe(2);
    expect(menuRowIds()).toHaveLength(2);

    vi.mocked(hostingService.costAlerts).mockRejectedValue(new Error("API restarting"));
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.costAlerts, exact: true });
    });

    expect(queryClient.getQueryState(hostingQueryKeys.costAlerts)?.status).toBe("error");
    // The observers are notified on a later task; let React render the error state before asserting.
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 20));
    });
    expect(screen.getByRole("heading", { name: bannerHeading })).toBeVisible();
    expect(bannerRowCount()).toBe(2);
    expect(menuRowIds()).toHaveLength(2);
    expect(screen.getByTestId("probe")).toHaveTextContent("loaded:true:2");
  });

  it("renders nothing before the first load finished and no banner when the first load fails", async () => {
    let resolve: (alerts: CostAlert[]) => void = () => undefined;
    vi.mocked(hostingService.costAlerts).mockReturnValue(
      new Promise<CostAlert[]>((done) => {
        resolve = done;
      }),
    );
    const first = renderSurfaces();
    await waitFor(() => expect(hostingService.costAlerts).toHaveBeenCalled());
    expect(screen.getByTestId("probe")).toHaveTextContent("loaded:false:0");
    expect(screen.queryByRole("heading", { name: bannerHeading })).toBeNull();
    await act(async () => {
      resolve([alertAt(5)]);
      await Promise.resolve();
    });
    expect(await screen.findByRole("heading", { name: bannerHeading })).toBeVisible();
    first.unmount();

    vi.mocked(hostingService.costAlerts).mockRejectedValue(new Error("down"));
    const second = renderSurfaces();
    await settled(second.queryClient);

    expect(screen.getByTestId("probe")).toHaveTextContent("loaded:false:0");
    expect(screen.queryByRole("heading", { name: bannerHeading })).toBeNull();
  });

  it("neither re-adds nor toasts a raised frame for an alert dismissed in this tab", async () => {
    const alert = alertAt(5);
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alert]);
    const dismiss = vi.spyOn(hostingService, "dismissCostAlert").mockResolvedValue();
    const { queryClient } = renderSurfaces();
    const user = await openMenu();
    await screen.findByRole("heading", { name: bannerHeading });
    await openSocket(queryClient);

    await user.click(screen.getAllByRole("button", { name: /^Dismiss alert at/ })[0]!);
    await waitFor(() => expect(bannerRowCount()).toBe(0));
    expect(dismiss).toHaveBeenCalledWith(alert.id);

    await deliver(raisedFrame(alert));
    expect(bannerRowCount()).toBe(0);
    expect(menuRowIds()).toEqual([]);
    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([]);
    expect(toastShown()).toBe(false);

    // Control: a raised frame for an alert this viewer did not dismiss still lands and toasts.
    await deliver(raisedFrame(alertAt(20)));
    expect(await screen.findByText(`Cost alert: Ops Night reached ${money(20)}.`)).toBeVisible();
    expect(bannerRowCount()).toBe(1);
  });

  it("confirms a dismissal with a toast that never removes another alert (D-17)", async () => {
    const dismissed = alertAt(5);
    const kept = alertAt(10);
    vi.mocked(hostingService.costAlerts).mockResolvedValue([kept, dismissed]);
    vi.spyOn(hostingService, "dismissCostAlert").mockResolvedValue();
    const { queryClient } = renderSurfaces();
    const user = await openMenu();
    await screen.findByRole("heading", { name: bannerHeading });
    await openSocket(queryClient);

    await user.click(screen.getAllByRole("button", { name: `Dismiss alert at ${money(5)}` })[0]!);

    expect(await screen.findByText(`Cost alert dismissed: Ops Night, ${money(5)}.`)).toBeVisible();
    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([kept]);
    expect(bannerRowCount()).toBe(1);
    expect(menuRowIds()).toEqual([kept.id]);
    // The toast neither dismisses the remaining alert nor expires it: it stays after the toast is closed.
    await user.click(screen.getByRole("button", { name: "Close" }));
    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([kept]);
    expect(bannerRowCount()).toBe(1);
  });

  it("does not bring an alert back that a dismissed frame from another tab removed", async () => {
    const alert = alertAt(5);
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alert]);
    const { queryClient } = renderSurfaces();
    await openMenu();
    await screen.findByRole("heading", { name: bannerHeading });
    await openSocket(queryClient);

    await deliver(dismissedFrame(alert.id));
    await waitFor(() => expect(bannerRowCount()).toBe(0));

    await deliver(raisedFrame(alert));
    expect(bannerRowCount()).toBe(0);
    expect(menuRowIds()).toEqual([]);
    expect(toastShown()).toBe(false);
  });

  it("changes nothing for a raised frame of an alert that is already listed", async () => {
    const alert = alertAt(5);
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alert]);
    const { queryClient } = renderSurfaces();
    await openMenu();
    await screen.findByRole("heading", { name: bannerHeading });
    await openSocket(queryClient);
    const before = queryClient.getQueryData(hostingQueryKeys.costAlerts);

    await deliver(raisedFrame(alert));
    await deliver(raisedFrame(alert));

    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toBe(before);
    expect(bannerRowCount()).toBe(1);
    expect(toastShown()).toBe(false);
  });

  it("leaves the alerts untouched for fleet-changed and accrued frames", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertAt(5), alertAt(10)]);
    const { queryClient } = renderSurfaces();
    await openMenu();
    await screen.findByRole("heading", { name: bannerHeading });
    await openSocket(queryClient);
    const before = queryClient.getQueryData(hostingQueryKeys.costAlerts);
    const fetchesBefore = vi.mocked(hostingService.costAlerts).mock.calls.length;

    await deliver(fleetChangedFrame);
    await deliver(accruedFrame);

    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toBe(before);
    expect(vi.mocked(hostingService.costAlerts).mock.calls.length).toBe(fetchesBefore);
    expect(bannerRowCount()).toBe(2);
    expect(menuRowIds()).toHaveLength(2);
  });

  it("changes nothing for a dismissed frame of an unknown alert", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertAt(5)]);
    const { queryClient } = renderSurfaces();
    await openMenu();
    await screen.findByRole("heading", { name: bannerHeading });
    await openSocket(queryClient);
    const before = queryClient.getQueryData(hostingQueryKeys.costAlerts);

    await deliver(dismissedFrame("unknown-alert"));

    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toBe(before);
    expect(bannerRowCount()).toBe(1);
    expect(menuRowIds()).toHaveLength(1);
  });

  it("replaces the list with the server's list when a reopen refetch succeeds", async () => {
    const stays = alertAt(10);
    const dismissedElsewhere = alertAt(5);
    vi.mocked(hostingService.costAlerts).mockResolvedValue([stays, dismissedElsewhere]);
    const { queryClient } = renderSurfaces();
    await openMenu();
    await screen.findByRole("heading", { name: bannerHeading });
    expect(menuRowIds()).toHaveLength(2);

    vi.mocked(hostingService.costAlerts).mockResolvedValue([stays]);
    await openSocket(queryClient);

    await waitFor(() => expect(menuRowIds()).toEqual([stays.id]));
    expect(bannerRowCount()).toBe(1);
    expect(screen.getByTestId("probe")).toHaveTextContent("loaded:true:1");
  });
});
