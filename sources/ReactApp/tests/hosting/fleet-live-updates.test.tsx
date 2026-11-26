import { QueryClient, QueryClientProvider, QueryObserver } from "@tanstack/react-query";
import { act, render, screen } from "@testing-library/react";
import { StrictMode, useEffect, type ReactElement } from "react";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import {
  costAlertDismissedMessageType,
  costAlertRaisedMessageType,
} from "@/features/hosting/costFrames";
import { metricsMessageType } from "@/features/hosting/metricsFrames";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import { hostingQueryKeys } from "@/features/hosting/services";
import type { CostAlert, ServerMetricsReading } from "@/features/hosting/types";
import {
  useFleetLiveUpdates,
  type FleetLiveUpdatesValue,
} from "@/features/hosting/useFleetLiveUpdates";
import { costAlert } from "../helpers/costAlertFixtures";
import { FakeWebSocket } from "../helpers/fakeWebSocket";

/*
 * Provider half of the live fleet signal (D-13, D-15): exact-key invalidation, coalescing, the open-refetch rule,
 * reconnectNow and the access gate. Math.random is fixed at 0.5 so the first retry happens after exactly 1 second.
 */

const serverId = "3f2c9a52-6d1e-4b7a-9f10-5a8c2d4e6b01";

const fleetFrame = JSON.stringify({
  $id: "1",
  type: "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage",
  value: { $id: "2", serverId },
});

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

const configuration = {
  Api: { BaseUrl: "/api" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: false, ReactBasePath: "/" },
} as PublicConfiguration;

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

function Capture({ onValue }: { onValue(value: FleetLiveUpdatesValue): void }) {
  const value = useFleetLiveUpdates();
  useEffect(() => onValue(value), [value, onValue]);
  return <p>{`${value.status}:${value.disconnectEpoch}`}</p>;
}

function renderProvider(options: { strict?: boolean; queryClient?: QueryClient } = {}) {
  const queryClient =
    options.queryClient ?? new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(queryClient, "invalidateQueries");
  const captured: { current: FleetLiveUpdatesValue | null } = { current: null };
  const onValue = (value: FleetLiveUpdatesValue) => {
    captured.current = value;
  };
  const tree = (): ReactElement => {
    const content = (
      <QueryClientProvider client={queryClient}>
        <FleetLiveUpdatesProvider configuration={configuration}>
          <Capture onValue={onValue} />
        </FleetLiveUpdatesProvider>
      </QueryClientProvider>
    );
    return options.strict ? <StrictMode>{content}</StrictMode> : content;
  };
  const result = render(tree());
  return {
    ...result,
    queryClient,
    invalidate,
    captured,
    rerenderTree: () => result.rerender(tree()),
  };
}

function latestSocket(): FakeWebSocket {
  const socket = FakeWebSocket.instances.at(-1);
  if (!socket) throw new Error("Expected a socket.");
  return socket;
}

function liveSockets(): FakeWebSocket[] {
  return FakeWebSocket.instances.filter(
    (socket) => !socket.closedByClient && socket.readyState !== FakeWebSocket.CLOSED,
  );
}

function advance(milliseconds: number) {
  act(() => {
    vi.advanceTimersByTime(milliseconds);
  });
}

function raisedAlertFrame(alert: CostAlert): string {
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

function metricsFrame(overrides: Record<string, unknown> = {}): string {
  return JSON.stringify({
    $id: "1",
    type: metricsMessageType,
    value: {
      $id: "2",
      serverId,
      asOf: "2026-11-22T10:00:00+00:00",
      staleAfterSeconds: 45,
      runningSince: "2026-11-22T09:00:00+00:00",
      runningSinceUnavailableReason: null,
      cpuPercent: 12.5,
      cpuUnavailableReason: null,
      memoryUsedBytes: 2048,
      memoryTotalBytes: 4096,
      memoryUnavailableReason: null,
      playerCount: 3,
      playerCapacity: 32,
      playersUnavailableReason: null,
      ...overrides,
    },
  });
}

function cachedMetrics(queryClient: QueryClient): ServerMetricsReading | undefined {
  return queryClient.getQueryData<ServerMetricsReading>(hostingQueryKeys.metrics(serverId));
}

function expectPairInvalidated(invalidate: ReturnType<typeof vi.spyOn>, pairs: number) {
  expect(invalidate).toHaveBeenCalledTimes(pairs * 2);
  for (const [filters] of invalidate.mock.calls) {
    expect([
      { queryKey: hostingQueryKeys.fleet, exact: true },
      { queryKey: hostingQueryKeys.notifications, exact: true },
    ]).toContainEqual(filters);
  }
}

describe("fleet live updates provider", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(Math, "random").mockReturnValue(0.5);
    authState.user = userWithClaims("sv");
    authState.isLoading = false;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    authState.user = undefined;
    authState.isLoading = false;
  });

  it("invalidates exactly the fleet and notifications queries and never a per-server query", () => {
    const { queryClient, invalidate } = renderProvider();
    act(() => latestSocket().open());

    const seeded = [
      hostingQueryKeys.fleet,
      hostingQueryKeys.notifications,
      hostingQueryKeys.capabilities,
      hostingQueryKeys.server(serverId),
      hostingQueryKeys.jobs(serverId),
      hostingQueryKeys.operations(serverId),
    ];
    seeded.forEach((key) => queryClient.setQueryData(key, { seeded: true }));

    act(() => latestSocket().message(fleetFrame));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: hostingQueryKeys.fleet, exact: true });
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: hostingQueryKeys.notifications,
      exact: true,
    });
    expectPairInvalidated(invalidate, 1);
    expect(queryClient.getQueryState(hostingQueryKeys.fleet)?.isInvalidated).toBe(true);
    expect(queryClient.getQueryState(hostingQueryKeys.notifications)?.isInvalidated).toBe(true);
    expect(queryClient.getQueryState(hostingQueryKeys.capabilities)?.isInvalidated).toBe(false);
    expect(queryClient.getQueryState(hostingQueryKeys.server(serverId))?.isInvalidated).toBe(false);
    expect(queryClient.getQueryState(hostingQueryKeys.jobs(serverId))?.isInvalidated).toBe(false);
    expect(queryClient.getQueryState(hostingQueryKeys.operations(serverId))?.isInvalidated).toBe(
      false,
    );
  });

  it("coalesces a burst into one immediate and one trailing refetch and never polls", () => {
    const { invalidate } = renderProvider();
    act(() => latestSocket().open());
    expect(invalidate).not.toHaveBeenCalled();

    for (let index = 0; index < 5; index++) act(() => latestSocket().message(fleetFrame));
    expectPairInvalidated(invalidate, 1);

    advance(499);
    expectPairInvalidated(invalidate, 1);
    advance(1);
    expectPairInvalidated(invalidate, 2);

    advance(600);
    expectPairInvalidated(invalidate, 2);
    act(() => latestSocket().message(fleetFrame));
    expectPairInvalidated(invalidate, 3);

    advance(5 * 60_000);
    expectPairInvalidated(invalidate, 3);
    expect(FakeWebSocket.instances).toHaveLength(1);
  });

  it("keeps coalescing after the StrictMode remount", () => {
    const { invalidate } = renderProvider({ strict: true });
    const [socket] = liveSockets();
    if (!socket) throw new Error("Expected one live socket.");
    act(() => socket.open());

    act(() => socket.message(fleetFrame));
    act(() => socket.message(fleetFrame));
    expectPairInvalidated(invalidate, 1);
    advance(500);
    expectPairInvalidated(invalidate, 2);
  });

  it("refetches on open only when the cached fleet is older than the open, and again after a reconnect", () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(hostingQueryKeys.fleet, [], { updatedAt: Date.now() - 60_000 });
    const { invalidate } = renderProvider({ queryClient });

    act(() => latestSocket().open());
    expectPairInvalidated(invalidate, 1);

    act(() => latestSocket().serverClose());
    expectPairInvalidated(invalidate, 1);
    advance(1000);
    expect(FakeWebSocket.instances).toHaveLength(2);
    act(() => latestSocket().open());
    expectPairInvalidated(invalidate, 2);
  });

  it("does not refetch on open without cached fleet data or with data from the same instant", () => {
    const empty = renderProvider();
    act(() => latestSocket().open());
    expect(empty.invalidate).not.toHaveBeenCalled();
    empty.unmount();

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(hostingQueryKeys.fleet, [], { updatedAt: Date.now() });
    const fresh = renderProvider({ queryClient });
    act(() => latestSocket().open());
    expect(fresh.invalidate).not.toHaveBeenCalled();
  });

  it("reconnects immediately and refetches through reconnectNow", () => {
    const { invalidate, captured } = renderProvider();
    act(() => latestSocket().open());
    act(() => latestSocket().serverClose());
    expect(screen.getByText("reconnecting:1")).toBeInTheDocument();
    invalidate.mockClear();

    act(() => captured.current?.reconnectNow());

    expect(FakeWebSocket.instances).toHaveLength(2);
    // The fleet pair plus the cost alerts list, so a missed alert frame is recovered by a manual reconnect.
    expect(invalidate).toHaveBeenCalledTimes(3);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: hostingQueryKeys.fleet, exact: true });
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: hostingQueryKeys.notifications,
      exact: true,
    });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: hostingQueryKeys.costAlerts, exact: true });
    expect(vi.getTimerCount()).toBe(0);
  });

  it("opens no socket for an anonymous viewer, a viewer without server access or a loading session", () => {
    authState.user = undefined;
    renderProvider().unmount();

    authState.user = userWithClaims("crt:ev");
    renderProvider().unmount();

    authState.user = userWithClaims("sv");
    authState.isLoading = true;
    renderProvider().unmount();

    expect(FakeWebSocket.instances).toHaveLength(0);
  });

  it("opens exactly one socket for a server member and once auth finishes loading", () => {
    authState.isLoading = true;
    const { rerenderTree } = renderProvider();
    expect(FakeWebSocket.instances).toHaveLength(0);

    authState.isLoading = false;
    rerenderTree();

    expect(FakeWebSocket.instances).toHaveLength(1);
    expect(liveSockets()).toHaveLength(1);
    expect(latestSocket().url.endsWith("update-stream/subscribe?path=game-servers%2Ffleet")).toBe(
      true,
    );
  });

  it("reports the stream status and disconnect epoch through the context", () => {
    renderProvider();
    expect(screen.getByText("connecting:0")).toBeInTheDocument();

    act(() => latestSocket().open());
    expect(screen.getByText("open:0")).toBeInTheDocument();

    act(() => latestSocket().serverClose());
    expect(screen.getByText("reconnecting:1")).toBeInTheDocument();
  });

  it("defaults to a connected no-op outside the provider", () => {
    const captured: { current: FleetLiveUpdatesValue | null } = { current: null };
    render(
      <Capture
        onValue={(value) => {
          captured.current = value;
        }}
      />,
    );

    expect(screen.getByText("open:0")).toBeInTheDocument();
    expect(() => captured.current?.reconnectNow()).not.toThrow();
    expect(FakeWebSocket.instances).toHaveLength(0);
  });

  it("defaults markCostAlertsDismissed to a no-op outside the provider", () => {
    const captured: { current: FleetLiveUpdatesValue | null } = { current: null };
    render(
      <Capture
        onValue={(value) => {
          captured.current = value;
        }}
      />,
    );

    expect(() => captured.current?.markCostAlertsDismissed(["a1"])).not.toThrow();
  });

  it("drops a raised frame for an id marked dismissed before any cache write or listener call", () => {
    const { captured, queryClient } = renderProvider();
    act(() => latestSocket().open());
    queryClient.setQueryData<readonly CostAlert[]>(hostingQueryKeys.costAlerts, []);
    const listener = vi.fn();
    const dismissed = costAlert({ id: "dismissed-alert" });
    act(() => {
      captured.current?.subscribeCostAlerts(listener);
      captured.current?.markCostAlertsDismissed([dismissed.id]);
    });

    act(() => latestSocket().message(raisedAlertFrame(dismissed)));

    expect(listener).not.toHaveBeenCalled();
    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([]);

    const fresh = costAlert({ id: "fresh-alert" });
    act(() => latestSocket().message(raisedAlertFrame(fresh)));
    expect(listener).toHaveBeenCalledExactlyOnceWith(fresh, false);
    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([fresh]);
  });

  it("remembers the id of a dismissed frame so a later raised frame cannot bring the alert back", () => {
    const { captured, queryClient } = renderProvider();
    act(() => latestSocket().open());
    const alert = costAlert({ id: "other-tab-alert" });
    queryClient.setQueryData<readonly CostAlert[]>(hostingQueryKeys.costAlerts, [alert]);
    const listener = vi.fn();
    act(() => {
      captured.current?.subscribeCostAlerts(listener);
    });

    act(() =>
      latestSocket().message(
        JSON.stringify({ type: costAlertDismissedMessageType, value: { alertId: alert.id } }),
      ),
    );
    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([]);

    act(() => latestSocket().message(raisedAlertFrame(alert)));

    expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([]);
    expect(listener).not.toHaveBeenCalled();
  });

  it("calls subscribed listeners with the server id of each valid frame only", () => {
    const { captured } = renderProvider();
    act(() => latestSocket().open());
    const listener = vi.fn();
    act(() => {
      captured.current?.subscribe(listener);
    });

    act(() => latestSocket().message(fleetFrame));
    expect(listener).toHaveBeenCalledTimes(1);
    expect(listener).toHaveBeenCalledWith(serverId);

    act(() =>
      latestSocket().message(JSON.stringify({ type: "Some.Other.Message", value: { serverId } })),
    );
    act(() =>
      latestSocket().message(
        JSON.stringify({
          type: "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage",
          value: { serverId: 12 },
        }),
      ),
    );
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("stops calling a listener after its unsubscribe function ran", () => {
    const { captured } = renderProvider();
    act(() => latestSocket().open());
    const listener = vi.fn();
    let unsubscribe: (() => void) | undefined;
    act(() => {
      unsubscribe = captured.current?.subscribe(listener);
    });

    act(() => latestSocket().message(fleetFrame));
    expect(listener).toHaveBeenCalledTimes(1);

    act(() => unsubscribe?.());
    advance(600);
    act(() => latestSocket().message(fleetFrame));
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("isolates a throwing listener from other listeners and the fleet invalidation", () => {
    const { captured, invalidate } = renderProvider();
    act(() => latestSocket().open());
    const failing = vi.fn(() => {
      throw new Error("listener failure");
    });
    const healthy = vi.fn();
    act(() => {
      captured.current?.subscribe(failing);
      captured.current?.subscribe(healthy);
    });

    act(() => latestSocket().message(fleetFrame));

    expect(failing).toHaveBeenCalledTimes(1);
    expect(healthy).toHaveBeenCalledWith(serverId);
    expectPairInvalidated(invalidate, 1);
  });

  it("writes a metrics frame into the readings cache by server id with a fresh receive stamp and no request", () => {
    vi.spyOn(performance, "now").mockReturnValue(4321);
    const { queryClient, invalidate } = renderProvider();
    act(() => latestSocket().open());
    const fetchSpy = vi.spyOn(globalThis, "fetch");

    act(() => latestSocket().message(metricsFrame()));

    expect(cachedMetrics(queryClient)).toEqual({
      gameServerId: serverId,
      asOf: "2026-11-22T10:00:00+00:00",
      staleAfterSeconds: 45,
      runningSince: "2026-11-22T09:00:00+00:00",
      runningSinceUnavailableReason: null,
      cpuPercent: 12.5,
      cpuUnavailableReason: null,
      memoryUsedBytes: 2048,
      memoryTotalBytes: 4096,
      memoryUnavailableReason: null,
      playerCount: 3,
      playerCapacity: 32,
      playersUnavailableReason: null,
      receivedAtMs: 4321,
    });
    expect(fetchSpy).not.toHaveBeenCalled();
    expect(invalidate).not.toHaveBeenCalled();
  });

  it("keeps the cached reading for an older frame that carries values", () => {
    const { queryClient } = renderProvider();
    act(() => latestSocket().open());
    act(() => latestSocket().message(metricsFrame({ cpuPercent: 50 })));

    act(() =>
      latestSocket().message(metricsFrame({ asOf: "2026-11-22T09:59:00+00:00", cpuPercent: 1 })),
    );
    expect(cachedMetrics(queryClient)?.cpuPercent).toBe(50);

    act(() =>
      latestSocket().message(metricsFrame({ asOf: "2026-11-22T10:00:15+00:00", cpuPercent: 60 })),
    );
    expect(cachedMetrics(queryClient)?.cpuPercent).toBe(60);
  });

  it("lets a frame without an as-of time replace the reading and the first frame of the next run replace that", () => {
    const now = vi.spyOn(performance, "now").mockReturnValue(100);
    const { queryClient } = renderProvider();
    act(() => latestSocket().open());
    act(() => latestSocket().message(metricsFrame({ cpuPercent: 50 })));
    expect(cachedMetrics(queryClient)?.cpuPercent).toBe(50);

    act(() =>
      latestSocket().message(
        metricsFrame({
          asOf: null,
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
        }),
      ),
    );
    expect(cachedMetrics(queryClient)?.cpuPercent).toBeNull();
    expect(cachedMetrics(queryClient)?.cpuUnavailableReason).toBe("server-not-running");

    now.mockReturnValue(7000);
    act(() =>
      latestSocket().message(metricsFrame({ asOf: "2026-11-22T10:05:00+00:00", cpuPercent: 60 })),
    );
    expect(cachedMetrics(queryClient)?.cpuPercent).toBe(60);
    expect(cachedMetrics(queryClient)?.receivedAtMs).toBe(7000);
  });

  it("keeps the receive stamp when the same frame is delivered again", () => {
    const now = vi.spyOn(performance, "now").mockReturnValue(100);
    const { queryClient } = renderProvider();
    act(() => latestSocket().open());
    act(() => latestSocket().message(metricsFrame()));
    now.mockReturnValue(9000);

    act(() => latestSocket().message(metricsFrame()));

    expect(cachedMetrics(queryClient)?.receivedAtMs).toBe(100);
  });

  it("drops a malformed metrics frame and never writes it into the cache", () => {
    const { queryClient, invalidate } = renderProvider();
    act(() => latestSocket().open());

    act(() => latestSocket().message(metricsFrame({ cpuPercent: "12" })));
    act(() => latestSocket().message(metricsFrame({ staleAfterSeconds: 0 })));
    act(() => latestSocket().message(metricsFrame({ cpuUnavailableReason: "no-sample" })));

    expect(cachedMetrics(queryClient)).toBeUndefined();
    expect(invalidate).not.toHaveBeenCalled();
  });

  it("never triggers a fleet invalidation or a workspace listener for a metrics frame", () => {
    const { captured, invalidate } = renderProvider();
    act(() => latestSocket().open());
    const listener = vi.fn();
    act(() => {
      captured.current?.subscribe(listener);
    });

    act(() => latestSocket().message(metricsFrame()));
    advance(600);

    expect(listener).not.toHaveBeenCalled();
    expect(invalidate).not.toHaveBeenCalled();
  });

  describe("metric history refetch", () => {
    const otherServerId = "7a1d4c20-8b3e-4f65-a2d9-0c5e1b7f3a02";
    type Range = "1h" | "24h" | "7d";
    const ranges: Range[] = ["1h", "24h", "7d"];
    const bucketMs: Record<Range, number> = { "1h": 15_000, "24h": 60_000, "7d": 300_000 };

    function seedHistory(queryClient: QueryClient, id: string, ageOf: (range: Range) => number) {
      for (const range of ranges) {
        queryClient.setQueryData(
          hostingQueryKeys.metricsHistory(id, range),
          { seeded: range },
          { updatedAt: Date.now() - ageOf(range) },
        );
      }
    }

    function historyInvalidated(queryClient: QueryClient, range: Range, id = serverId) {
      const state = queryClient.getQueryState(hostingQueryKeys.metricsHistory(id, range));
      return state?.isInvalidated ?? false;
    }

    function pushedAt(second: number, id = serverId): string {
      const asOf = new Date(Date.UTC(2026, 10, 22, 10, 0, second)).toISOString();
      return metricsFrame({ serverId: id, asOf });
    }

    it("invalidates a range only once its data is as old as its bucket interval", () => {
      const { queryClient } = renderProvider();
      act(() => latestSocket().open());
      seedHistory(queryClient, serverId, (range) => bucketMs[range] - 1);
      seedHistory(queryClient, otherServerId, (range) => bucketMs[range]);

      act(() => latestSocket().message(pushedAt(1)));
      act(() => latestSocket().message(pushedAt(1, otherServerId)));

      for (const range of ranges) {
        expect(historyInvalidated(queryClient, range, serverId)).toBe(false);
        expect(historyInvalidated(queryClient, range, otherServerId)).toBe(true);
      }
    });

    it("invalidates only the ranges that aged out, with exact keys, and leaves other servers alone", () => {
      const { queryClient, invalidate } = renderProvider();
      act(() => latestSocket().open());
      seedHistory(queryClient, serverId, () => 20_000);
      seedHistory(queryClient, otherServerId, () => 400_000);
      invalidate.mockClear();

      act(() => latestSocket().message(pushedAt(1)));

      expect(historyInvalidated(queryClient, "1h")).toBe(true);
      expect(historyInvalidated(queryClient, "24h")).toBe(false);
      expect(historyInvalidated(queryClient, "7d")).toBe(false);
      for (const range of ranges) {
        expect(historyInvalidated(queryClient, range, otherServerId)).toBe(false);
      }
      expect(invalidate).toHaveBeenCalledTimes(1);
      expect(invalidate.mock.calls[0]?.[0]).toEqual({
        queryKey: hostingQueryKeys.metricsHistory(serverId, "1h"),
        exact: true,
      });
    });

    it("leaves the current readings and the fleet alone", () => {
      const { queryClient, invalidate } = renderProvider();
      act(() => latestSocket().open());
      seedHistory(queryClient, serverId, () => 400_000);
      invalidate.mockClear();

      act(() => latestSocket().message(pushedAt(1)));

      expect(invalidate).toHaveBeenCalledTimes(3);
      expect(queryClient.getQueryState(hostingQueryKeys.metrics(serverId))?.isInvalidated).toBe(
        false,
      );
    });

    it("refetches an active range once per bucket and never by a timer", async () => {
      const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      const fetcher = vi.fn(() => Promise.resolve({ fetched: true }));
      const historyKey = hostingQueryKeys.metricsHistory(serverId, "1h");
      queryClient.setQueryData(historyKey, { seeded: true });
      const observer = new QueryObserver(queryClient, {
        queryKey: historyKey,
        queryFn: fetcher,
        staleTime: Infinity,
      });
      const unsubscribe = observer.subscribe(() => undefined);
      renderProvider({ queryClient });
      act(() => latestSocket().open());
      fetcher.mockClear();

      advance(20_000);
      expect(fetcher).not.toHaveBeenCalled();

      await act(async () => {
        latestSocket().message(pushedAt(1));
        await vi.advanceTimersByTimeAsync(0);
      });
      expect(fetcher).toHaveBeenCalledTimes(1);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(5_000);
        latestSocket().message(pushedAt(2));
      });
      expect(fetcher).toHaveBeenCalledTimes(1);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(10_000);
        latestSocket().message(pushedAt(3));
        await vi.advanceTimersByTimeAsync(0);
      });
      expect(fetcher).toHaveBeenCalledTimes(2);
      unsubscribe();
    });

    it("skips a range that is still fetching", () => {
      const { queryClient, invalidate } = renderProvider();
      act(() => latestSocket().open());
      const historyKey = hostingQueryKeys.metricsHistory(serverId, "1h");
      queryClient.setQueryData(historyKey, { seeded: true });
      advance(400_000);
      invalidate.mockClear();
      void queryClient.fetchQuery({
        queryKey: historyKey,
        queryFn: () => new Promise(() => undefined),
        staleTime: 0,
      });

      act(() => latestSocket().message(pushedAt(1)));

      expect(invalidate).not.toHaveBeenCalled();
    });
  });

  it("closes the socket and cancels every timer on unmount", () => {
    const { unmount } = renderProvider();
    act(() => latestSocket().open());
    act(() => latestSocket().message(fleetFrame));
    act(() => latestSocket().message(fleetFrame));
    act(() => latestSocket().serverClose());
    expect(vi.getTimerCount()).toBe(2);

    unmount();

    expect(vi.getTimerCount()).toBe(0);
    expect(liveSockets()).toHaveLength(0);
  });
});
