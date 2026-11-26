import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render } from "@testing-library/react";
import { beforeEach, type MockInstance } from "vitest";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import { hostingQueryKeys } from "@/features/hosting/services";
import { useWorkspaceLiveUpdates } from "@/features/hosting/useWorkspaceLiveUpdates";
import { FakeWebSocket } from "../helpers/fakeWebSocket";

/*
 * Workspace half of the live signal (D-07): one fleet frame for the open server invalidates exactly its server, jobs
 * and operations queries, coalesced, recovered after a reconnect, and never through a second socket.
 */

const serverA = "3f2c9a52-6d1e-4b7a-9f10-5a8c2d4e6b01";
const serverB = "7a1d4c20-8b3e-4f65-a2d9-0c5e1b7f3a02";

function frameFor(serverId: string): string {
  return JSON.stringify({
    $id: "1",
    type: "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage",
    value: { $id: "2", serverId },
  });
}

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

function memberWithServerAccess(): FullUser {
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

function Probe({ serverId }: { serverId: string }) {
  useWorkspaceLiveUpdates(serverId);
  return null;
}

function renderWorkspace(serverId = serverA) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(queryClient, "invalidateQueries");
  const tree = (withProbe: boolean) => (
    <QueryClientProvider client={queryClient}>
      <FleetLiveUpdatesProvider configuration={configuration}>
        {withProbe ? <Probe serverId={serverId} /> : null}
      </FleetLiveUpdatesProvider>
    </QueryClientProvider>
  );
  const result = render(tree(true));
  return {
    ...result,
    queryClient,
    invalidate,
    removeProbe: () => result.rerender(tree(false)),
  };
}

function latestSocket(): FakeWebSocket {
  const socket = FakeWebSocket.instances.at(-1);
  if (!socket) throw new Error("Expected a socket.");
  return socket;
}

function advance(milliseconds: number) {
  act(() => {
    vi.advanceTimersByTime(milliseconds);
  });
}

function seed(queryClient: QueryClient) {
  const keys = {
    serverA: hostingQueryKeys.server(serverA),
    jobsA: hostingQueryKeys.jobs(serverA),
    operationsA: hostingQueryKeys.operations(serverA),
    metricsA: hostingQueryKeys.metrics(serverA),
    metricsHistoryA: hostingQueryKeys.metricsHistory(serverA, "1h"),
    filesA: hostingQueryKeys.files(serverA, "persistent", ""),
    configurationA: hostingQueryKeys.configuration(serverA),
    preservationA: hostingQueryKeys.preservation(serverA),
    secretsA: hostingQueryKeys.secrets(serverA),
    serverB: hostingQueryKeys.server(serverB),
    jobsB: hostingQueryKeys.jobs(serverB),
  };
  Object.values(keys).forEach((key) => queryClient.setQueryData(key, { seeded: true }));
  return keys;
}

function invalidated(queryClient: QueryClient, key: readonly unknown[]): boolean {
  return queryClient.getQueryState(key)?.isInvalidated ?? false;
}

type InvalidateSpy = MockInstance<QueryClient["invalidateQueries"]>;

function callsFor(invalidate: InvalidateSpy, key: readonly unknown[]): number {
  return invalidate.mock.calls.filter(
    ([filters]) => JSON.stringify(filters?.queryKey) === JSON.stringify(key),
  ).length;
}

describe("workspace live updates", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(Math, "random").mockReturnValue(0.5);
    authState.user = memberWithServerAccess();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  it("invalidates exactly the server, jobs, operations and accrued cost queries of the frame's server", () => {
    const { queryClient } = renderWorkspace();
    act(() => latestSocket().open());
    const keys = seed(queryClient);

    act(() => latestSocket().message(frameFor(serverA)));

    expect(invalidated(queryClient, keys.serverA)).toBe(true);
    expect(invalidated(queryClient, keys.jobsA)).toBe(true);
    expect(invalidated(queryClient, keys.operationsA)).toBe(true);
    expect(invalidated(queryClient, keys.metricsA)).toBe(true);
    expect(invalidated(queryClient, keys.metricsHistoryA)).toBe(false);
    expect(invalidated(queryClient, keys.filesA)).toBe(false);
    expect(invalidated(queryClient, keys.configurationA)).toBe(false);
    expect(invalidated(queryClient, keys.preservationA)).toBe(false);
    expect(invalidated(queryClient, keys.secretsA)).toBe(false);
    expect(invalidated(queryClient, keys.serverB)).toBe(false);
    expect(invalidated(queryClient, keys.jobsB)).toBe(false);
  });

  it("uses exact filters for every workspace invalidation", () => {
    const { queryClient, invalidate } = renderWorkspace();
    act(() => latestSocket().open());
    seed(queryClient);
    invalidate.mockClear();

    act(() => latestSocket().message(frameFor(serverA)));

    const workspaceCalls = invalidate.mock.calls.filter(([filters]) =>
      JSON.stringify(filters?.queryKey).includes(serverA),
    );
    // server, jobs, operations, the accrued cost and the current metric readings of the server
    expect(workspaceCalls).toHaveLength(5);
    workspaceCalls.forEach(([filters]) => expect(filters).toMatchObject({ exact: true }));
  });

  it("leaves every key of the open server valid for a frame of another server", () => {
    const { queryClient } = renderWorkspace();
    act(() => latestSocket().open());
    const keys = seed(queryClient);

    act(() => latestSocket().message(frameFor(serverB)));

    Object.values(keys).forEach((key) => expect(invalidated(queryClient, key)).toBe(false));
  });

  it("coalesces five frames inside the window into a leading and one trailing round", () => {
    const { queryClient, invalidate } = renderWorkspace();
    act(() => latestSocket().open());
    seed(queryClient);
    invalidate.mockClear();

    for (let index = 0; index < 5; index++) act(() => latestSocket().message(frameFor(serverA)));
    expect(callsFor(invalidate, hostingQueryKeys.server(serverA))).toBe(1);

    advance(499);
    expect(callsFor(invalidate, hostingQueryKeys.server(serverA))).toBe(1);
    advance(1);
    expect(callsFor(invalidate, hostingQueryKeys.server(serverA))).toBe(2);
    expect(callsFor(invalidate, hostingQueryKeys.jobs(serverA))).toBe(2);
    expect(callsFor(invalidate, hostingQueryKeys.operations(serverA))).toBe(2);

    advance(5_000);
    expect(callsFor(invalidate, hostingQueryKeys.server(serverA))).toBe(2);
  });

  it("recovers missed changes once after a reconnect and not on the first open", () => {
    const { queryClient, invalidate } = renderWorkspace();
    act(() => latestSocket().open());
    seed(queryClient);
    expect(callsFor(invalidate, hostingQueryKeys.server(serverA))).toBe(0);

    act(() => latestSocket().serverClose());
    expect(callsFor(invalidate, hostingQueryKeys.server(serverA))).toBe(0);
    advance(1000);
    expect(FakeWebSocket.instances).toHaveLength(2);
    act(() => latestSocket().open());

    expect(callsFor(invalidate, hostingQueryKeys.server(serverA))).toBe(1);
    expect(callsFor(invalidate, hostingQueryKeys.jobs(serverA))).toBe(1);
    expect(callsFor(invalidate, hostingQueryKeys.operations(serverA))).toBe(1);
    expect(callsFor(invalidate, hostingQueryKeys.metrics(serverA))).toBe(1);
    expect(invalidated(queryClient, hostingQueryKeys.metrics(serverA))).toBe(true);
    expect(invalidated(queryClient, hostingQueryKeys.metricsHistory(serverA, "1h"))).toBe(false);
    expect(invalidated(queryClient, hostingQueryKeys.files(serverA, "persistent", ""))).toBe(false);
  });

  it("refetches the readings of the open server for a fleet signal and never those of another", () => {
    const { queryClient, invalidate } = renderWorkspace();
    act(() => latestSocket().open());
    seed(queryClient);
    queryClient.setQueryData(hostingQueryKeys.metrics(serverB), { seeded: true });
    invalidate.mockClear();

    act(() => latestSocket().message(frameFor(serverA)));

    expect(callsFor(invalidate, hostingQueryKeys.metrics(serverA))).toBe(1);
    expect(invalidated(queryClient, hostingQueryKeys.metrics(serverA))).toBe(true);
    expect(invalidated(queryClient, hostingQueryKeys.metrics(serverB))).toBe(false);
  });

  it("never opens a second socket", () => {
    renderWorkspace();
    act(() => latestSocket().open());
    advance(60_000);

    expect(FakeWebSocket.instances).toHaveLength(1);
  });

  it("invalidates nothing of the server after the workspace unmounts", () => {
    const { queryClient, removeProbe } = renderWorkspace();
    act(() => latestSocket().open());
    const keys = seed(queryClient);

    removeProbe();
    act(() => latestSocket().message(frameFor(serverA)));
    advance(1_000);

    expect(invalidated(queryClient, keys.serverA)).toBe(false);
    expect(invalidated(queryClient, keys.jobsA)).toBe(false);
    expect(invalidated(queryClient, keys.operationsA)).toBe(false);
  });

  it("registers nothing for an empty server id", () => {
    const { queryClient, invalidate } = renderWorkspace("");
    act(() => latestSocket().open());
    seed(queryClient);
    invalidate.mockClear();

    act(() => latestSocket().message(frameFor(serverA)));
    act(() => latestSocket().message(frameFor("")));

    expect(callsFor(invalidate, hostingQueryKeys.server(serverA))).toBe(0);
    expect(callsFor(invalidate, hostingQueryKeys.server(""))).toBe(0);
  });
});
