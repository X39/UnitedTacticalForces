import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, renderHook, screen, waitFor } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import type { UpdateStreamStatus } from "@/app/updateStream";
import i18n, { i18nReady } from "@/app/i18n";
import { AccruedCost } from "@/components/server/AccruedCost";
import { accruedCostMessageType, parseAccruedCostFrame } from "@/features/hosting/costFrames";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServerAccruedCost } from "@/features/hosting/types";
import { useAccruedStoppedAnnouncement } from "@/features/hosting/useAccruedStoppedAnnouncement";
import { FleetLiveUpdatesContext } from "@/features/hosting/useFleetLiveUpdates";
import { invalidateWorkspaceQueries } from "@/features/hosting/useWorkspaceLiveUpdates";
import { FakeWebSocket } from "../helpers/fakeWebSocket";
import { fleetServer } from "../helpers/fleetFixtures";
import { beforeEach } from "vitest";

/*
 * Tracer for D-02: the REST value is shown with its as-of time, pushed frames replace it in order, malformed and
 * older frames are ignored, and no request or timer is involved after the first load.
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

const server = fleetServer({ status: "Running" });
const otherServerId = "f1ee7000-0000-4000-8000-000000000099";

function memberWithServerClaim(): FullUser {
  return {
    primaryKey: "10000000-0000-4000-8000-000000000001",
    nickname: "Member",
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
  } as unknown as FullUser;
}

function accrued(
  amount: number | null,
  asOf: string,
  options: {
    isFinal?: boolean;
    noSession?: boolean;
    billingUnitSeconds?: number | null;
    billedThrough?: string | null;
  } = {},
): GameServerAccruedCost {
  if (options.noSession === true) return { gameServerId: server.primaryKey, session: null };
  return {
    gameServerId: server.primaryKey,
    session: {
      deploymentId: "d0000000-0000-4000-8000-000000000001",
      amount,
      currency: "EUR",
      asOf,
      isFinal: options.isFinal ?? false,
      unavailableReason: null,
      billingUnitSeconds: options.billingUnitSeconds ?? null,
      billedThrough: options.billedThrough ?? null,
    },
  };
}

function frame(serverId: string | undefined, value: Record<string, unknown> = {}): string {
  return JSON.stringify({
    $id: "1",
    type: accruedCostMessageType,
    value: {
      $id: "2",
      serverId,
      deploymentId: "d0000000-0000-4000-8000-000000000001",
      amount: 1.31,
      currency: "EUR",
      asOf: "2026-11-21T14:06:00Z",
      isFinal: false,
      unavailableReason: null,
      ...value,
    },
  });
}

function money(value: number): string {
  return new Intl.NumberFormat("en-US", { style: "currency", currency: "EUR" }).format(value);
}

function clock(instant: string): string {
  return new Intl.DateTimeFormat("en-US", { timeStyle: "short" }).format(new Date(instant));
}

function renderRow() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const view = render(
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <FleetLiveUpdatesProvider configuration={configuration}>
          <AccruedCost server={server} />
        </FleetLiveUpdatesProvider>
      </I18nextProvider>
    </QueryClientProvider>,
  );
  return { ...view, queryClient };
}

function latestSocket(): FakeWebSocket {
  const socket = FakeWebSocket.instances.at(-1);
  if (!socket) throw new Error("Expected a socket.");
  return socket;
}

/** Delivers one frame and lets the query cache notify its observers (React Query batches notifications). */
async function deliver(data: string): Promise<void> {
  await act(async () => {
    latestSocket().message(data);
    await Promise.resolve();
  });
}

/* Pins the clock to the fixture day so time-only expectations hold on any run date. */
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"], shouldAdvanceTime: true });
  vi.setSystemTime(new Date("2026-11-21T14:30:00Z"));
});

afterEach(() => {
  vi.useRealTimers();
});

describe("accrued cost in the workspace strip", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(() => {
    authState.user = memberWithServerClaim();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  it("shows the REST amount with the time it was true", async () => {
    vi.spyOn(hostingService, "accruedCost").mockResolvedValue(
      accrued(1.23, "2026-11-21T14:05:00Z"),
    );

    renderRow();

    expect(await screen.findByText(money(1.23))).toBeInTheDocument();
    const time = screen.getByText(`As of ${clock("2026-11-21T14:05:00Z")}`);
    expect(time.tagName).toBe("TIME");
    expect(time).toHaveAttribute("datetime", "2026-11-21T14:05:00Z");
  });

  it("follows a newer frame without a REST call and ignores older and foreign frames", async () => {
    const load = vi
      .spyOn(hostingService, "accruedCost")
      .mockResolvedValue(accrued(1.23, "2026-11-21T14:05:00Z"));
    renderRow();
    await screen.findByText(money(1.23));
    act(() => latestSocket().open());

    await deliver(frame(server.primaryKey, { amount: 1.31 }));
    expect(await screen.findByText(money(1.31))).toBeInTheDocument();
    expect(screen.getByText(`As of ${clock("2026-11-21T14:06:00Z")}`)).toBeInTheDocument();

    await deliver(frame(server.primaryKey, { amount: 1.25, asOf: "2026-11-21T14:05:30Z" }));
    expect(screen.getByText(money(1.31))).toBeInTheDocument();

    await deliver(frame(otherServerId, { amount: 9.99 }));
    expect(screen.getByText(money(1.31))).toBeInTheDocument();
    expect(screen.queryByText(money(9.99))).toBeNull();
    expect(load).toHaveBeenCalledTimes(1);
  });

  it("ignores malformed frames without an error", async () => {
    vi.spyOn(hostingService, "accruedCost").mockResolvedValue(
      accrued(1.23, "2026-11-21T14:05:00Z"),
    );
    renderRow();
    await screen.findByText(money(1.23));
    act(() => latestSocket().open());

    await deliver(frame(undefined));
    await deliver(frame(server.primaryKey, { amount: "lots" }));
    await deliver(frame(server.primaryKey, { asOf: "yesterday" }));
    await deliver(frame(server.primaryKey, { isFinal: "no" }));

    expect(screen.getByText(money(1.23))).toBeInTheDocument();
  });

  it("renders No session yet for a null session and the unknown sentence for a null amount", async () => {
    const load = vi.spyOn(hostingService, "accruedCost");
    load.mockResolvedValue(accrued(null, "", { noSession: true }));
    const none = renderRow();
    expect(await screen.findByText("No session yet")).toBeInTheDocument();
    expect(screen.queryByText("—")).toBeNull();
    none.unmount();

    load.mockResolvedValue(accrued(null, "2026-11-21T14:05:00Z"));
    renderRow();
    expect(await screen.findByText("Not available for this session.")).toBeInTheDocument();
    expect(screen.queryByText(money(0))).toBeNull();
  });

  it("invalidates the accrued key of exactly that server", () => {
    const queryClient = new QueryClient();
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");

    invalidateWorkspaceQueries(queryClient, server.primaryKey);

    expect(invalidate).toHaveBeenCalledWith({
      queryKey: hostingQueryKeys.accruedCost(server.primaryKey),
      exact: true,
    });
  });

  it("refetches once when the workspace invalidates the accrued key", async () => {
    const load = vi
      .spyOn(hostingService, "accruedCost")
      .mockResolvedValue(accrued(1.23, "2026-11-21T14:05:00Z"));
    const { queryClient } = renderRow();
    await screen.findByText(money(1.23));

    await act(async () => {
      invalidateWorkspaceQueries(queryClient, server.primaryKey);
    });

    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
  });
});

describe("parseAccruedCostFrame", () => {
  it("returns null for other message types", () => {
    expect(parseAccruedCostFrame({ type: "Other", value: {} })).toBeNull();
  });

  it("returns the REST shape and ignores extra properties", () => {
    const parsed = parseAccruedCostFrame({
      type: accruedCostMessageType,
      value: {
        $id: "2",
        serverId: "s",
        deploymentId: "d",
        amount: null,
        currency: "EUR",
        asOf: "2026-11-21T14:06:00Z",
        isFinal: true,
        unavailableReason: "rate",
        extra: 1,
      },
    });

    expect(parsed).toEqual({
      serverId: "s",
      accrued: {
        gameServerId: "s",
        session: {
          deploymentId: "d",
          amount: null,
          currency: "EUR",
          asOf: "2026-11-21T14:06:00Z",
          isFinal: true,
          unavailableReason: "rate",
          billingUnitSeconds: null,
          billedThrough: null,
        },
      },
    });
  });
});

/** Seeds the accrued cache so the row renders synchronously without a request. */
function renderSeeded(
  serverUnderTest: ReturnType<typeof fleetServer>,
  value: GameServerAccruedCost,
  socket: { status: UpdateStreamStatus; disconnectEpoch: number } = {
    status: "open",
    disconnectEpoch: 0,
  },
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(hostingQueryKeys.accruedCost(serverUnderTest.primaryKey), value);
  const element = (state: typeof socket) => (
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <FleetLiveUpdatesContext.Provider
          value={{
            ...state,
            reconnectNow: () => undefined,
            subscribe: () => () => undefined,
            subscribeCostAlerts: () => () => undefined,
            markCostAlertsDismissed: () => undefined,
          }}
        >
          <AccruedCost server={serverUnderTest} />
        </FleetLiveUpdatesContext.Provider>
      </I18nextProvider>
    </QueryClientProvider>
  );
  const view = render(element(socket));
  return { ...view, update: (state: typeof socket) => view.rerender(element(state)) };
}

describe("accrued cost states in the row", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("shows the stopped line for a final session whose cost has stopped", () => {
    const stopped = fleetServer({ status: "Stopped", possibleCost: false });
    renderSeeded(stopped, accrued(4.2, "2026-11-21T14:05:00Z", { isFinal: true }));

    expect(screen.getByText(money(4.2))).toBeInTheDocument();
    expect(screen.getByText(`Cost stopped: ${clock("2026-11-21T14:05:00Z")}`)).toBeInTheDocument();
    expect(screen.queryByText(/^As of/)).toBeNull();
  });

  it("says that the provider bills every started hour and when the next hour starts", () => {
    renderSeeded(
      server,
      accrued(1.2, "2026-11-21T14:05:00Z", {
        billingUnitSeconds: 3600,
        billedThrough: "2026-11-21T15:42:00Z",
      }),
    );

    const line = screen.getByText(
      `Billed per started hour. The next hour starts at ${clock("2026-11-21T15:42:00Z")}.`,
    );
    expect(line.tagName).toBe("TIME");
    expect(line).toHaveAttribute("datetime", "2026-11-21T15:42:00Z");
    expect(line.closest("[aria-live]")).toBeNull();
  });

  it("says the same in German", async () => {
    await act(async () => i18n.changeLanguage("de"));
    try {
      renderSeeded(
        server,
        accrued(1.2, "2026-11-21T14:05:00Z", {
          billingUnitSeconds: 3600,
          billedThrough: "2026-11-21T15:42:00Z",
        }),
      );

      expect(
        screen.getByText(
          /^Abgerechnet wird jede angefangene Stunde\. Die nächste Stunde beginnt um /,
        ),
      ).toBeInTheDocument();
    } finally {
      await act(async () => i18n.changeLanguage("en"));
    }
  });

  it("shows only the unit for a final session and a per-minute provider", () => {
    const stopped = fleetServer({ status: "Stopped", possibleCost: false });
    const final = renderSeeded(
      stopped,
      accrued(4.2, "2026-11-21T14:05:00Z", {
        isFinal: true,
        billingUnitSeconds: 3600,
        billedThrough: "2026-11-21T15:42:00Z",
      }),
    );
    expect(screen.getByText("Billed per started hour.")).toBeInTheDocument();
    expect(screen.queryByText(/next hour/)).toBeNull();
    final.unmount();

    renderSeeded(
      server,
      accrued(1.2, "2026-11-21T14:05:00Z", {
        billingUnitSeconds: 60,
        billedThrough: "2026-11-21T14:06:00Z",
      }),
    );
    expect(screen.getByText("Billed per started minute.")).toBeInTheDocument();
  });

  it("shows no billing line for a per-second provider or an unavailable amount", () => {
    const perSecond = renderSeeded(
      server,
      accrued(1.2, "2026-11-21T14:05:00Z", {
        billingUnitSeconds: 1,
        billedThrough: "2026-11-21T14:05:00Z",
      }),
    );
    expect(screen.queryByText(/Billed per/)).toBeNull();
    perSecond.unmount();

    renderSeeded(server, accrued(null, "2026-11-21T14:05:00Z", { billingUnitSeconds: 3600 }));
    expect(screen.queryByText(/Billed per/)).toBeNull();
  });

  it("reads Less than for a tiny amount while costing, never a rounded-down zero", () => {
    renderSeeded(server, accrued(0.004, "2026-11-21T14:05:00Z"));

    expect(screen.getByText(`Less than ${money(0.01)}`)).toBeInTheDocument();
    expect(screen.queryByText(money(0))).toBeNull();
  });

  it("adds the still-counting sentence while the machine is not confirmed gone", () => {
    const unconfirmed = fleetServer({ status: "Stopped", possibleCost: true });
    renderSeeded(unconfirmed, accrued(1.5, "2026-11-21T14:05:00Z"));

    expect(
      screen.getByText("Still counting until the cloud machine is confirmed gone."),
    ).toBeInTheDocument();
  });

  it("shows the paused sentence after 3 s of continuous disconnect and removes it on reconnect", () => {
    vi.useFakeTimers();
    const view = renderSeeded(server, accrued(1.5, "2026-11-21T14:05:00Z"));
    const paused = "Live updates are paused. Reconnecting automatically.";

    view.update({ status: "reconnecting", disconnectEpoch: 1 });
    act(() => vi.advanceTimersByTime(2999));
    expect(screen.queryByText(paused)).toBeNull();
    act(() => vi.advanceTimersByTime(1));
    expect(screen.getByText(paused)).toBeInTheDocument();
    expect(screen.getByText(money(1.5))).toBeInTheDocument();

    view.update({ status: "open", disconnectEpoch: 1 });
    expect(screen.queryByText(paused)).toBeNull();
  });

  it("shows nothing for a one second blip and nothing when the cost has stopped", () => {
    vi.useFakeTimers();
    const blip = renderSeeded(server, accrued(1.5, "2026-11-21T14:05:00Z"));
    const paused = "Live updates are paused. Reconnecting automatically.";

    blip.update({ status: "reconnecting", disconnectEpoch: 1 });
    act(() => vi.advanceTimersByTime(1000));
    blip.update({ status: "open", disconnectEpoch: 1 });
    act(() => vi.advanceTimersByTime(5000));
    expect(screen.queryByText(paused)).toBeNull();
    blip.unmount();

    const stopped = fleetServer({ status: "Stopped", possibleCost: false });
    const final = renderSeeded(stopped, accrued(4.2, "2026-11-21T14:05:00Z", { isFinal: true }));
    final.update({ status: "reconnecting", disconnectEpoch: 1 });
    act(() => vi.advanceTimersByTime(4000));
    expect(screen.queryByText(paused)).toBeNull();
  });
});

describe("accrued stopped announcement", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  const running = fleetServer({ status: "Running" });
  const stopped = { ...running, ...fleetServer({ status: "Stopped", possibleCost: false }) };
  const costingSession = accrued(4.2, "2026-11-21T14:05:00Z");
  const finalSession = accrued(4.2, "2026-11-21T14:09:00Z", { isFinal: true });

  function renderAnnouncement(initial: {
    server: ReturnType<typeof fleetServer> | undefined;
    accrued: GameServerAccruedCost | undefined;
  }) {
    return renderHook(
      (props: typeof initial) => useAccruedStoppedAnnouncement(props.server, props.accrued),
      {
        initialProps: initial,
        wrapper: ({ children }) => <I18nextProvider i18n={i18n}>{children}</I18nextProvider>,
      },
    );
  }

  it("announces once when a costing session stops while the workspace is open", () => {
    const hook = renderAnnouncement({ server: running, accrued: costingSession });
    expect(hook.result.current).toBeUndefined();

    hook.rerender({ server: stopped, accrued: finalSession });
    expect(hook.result.current).toBe(`Cost has stopped. This session cost ${money(4.2)}.`);

    hook.rerender({ server: stopped, accrued: finalSession });
    expect(hook.result.current).toBe(`Cost has stopped. This session cost ${money(4.2)}.`);
  });

  it("waits for the final amount instead of announcing a lower cached total", () => {
    const hook = renderAnnouncement({ server: running, accrued: costingSession });

    hook.rerender({ server: stopped, accrued: accrued(3.9, "2026-11-21T14:05:00Z") });
    expect(hook.result.current).toBeUndefined();

    hook.rerender({ server: stopped, accrued: finalSession });
    expect(hook.result.current).toBe(`Cost has stopped. This session cost ${money(4.2)}.`);
  });

  it("stays silent on the first load of an already final session", () => {
    const hook = renderAnnouncement({ server: undefined, accrued: undefined });
    hook.rerender({ server: stopped, accrued: finalSession });
    expect(hook.result.current).toBeUndefined();

    const direct = renderAnnouncement({ server: stopped, accrued: finalSession });
    expect(direct.result.current).toBeUndefined();
  });
});
