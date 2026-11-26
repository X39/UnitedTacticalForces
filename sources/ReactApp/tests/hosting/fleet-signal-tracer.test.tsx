import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen } from "@testing-library/react";
import { StrictMode, type ReactElement } from "react";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import { hostingQueryKeys } from "@/features/hosting/services";
import { useFleetLiveUpdates } from "@/features/hosting/useFleetLiveUpdates";
import { FakeWebSocket } from "../helpers/fakeWebSocket";

/*
 * React half of the live fleet signal tracer (D-13, D-16). The pinned frame below is byte for byte the frame the
 * API half (tests/Api.Tests/GameServerHosting/FleetSignalTracerTests.cs) captures from the real serializer.
 */

const pinnedFrame =
  '{"$id":"1","type":"X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage","value":{"$id":"2","serverId":"3f2c9a52-6d1e-4b7a-9f10-5a8c2d4e6b01"}}';

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

function StatusProbe() {
  const { status, disconnectEpoch } = useFleetLiveUpdates();
  return <p>{`${status}:${disconnectEpoch}`}</p>;
}

function renderProvider(options: { strict?: boolean } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(queryClient, "invalidateQueries");
  const tree: ReactElement = (
    <QueryClientProvider client={queryClient}>
      <FleetLiveUpdatesProvider configuration={configuration}>
        <StatusProbe />
      </FleetLiveUpdatesProvider>
    </QueryClientProvider>
  );
  const result = render(options.strict ? <StrictMode>{tree}</StrictMode> : tree);
  return { ...result, invalidate };
}

function liveSockets(): FakeWebSocket[] {
  return FakeWebSocket.instances.filter((socket) => !socket.closedByClient);
}

function onlySocket(): FakeWebSocket {
  const sockets = liveSockets();
  expect(sockets).toHaveLength(1);
  const [socket] = sockets;
  if (!socket) throw new Error("Expected one live socket.");
  return socket;
}

describe("fleet signal tracer", () => {
  afterEach(() => {
    authState.user = undefined;
    authState.isLoading = false;
  });

  it("turns the pinned frame into exact fleet and notifications invalidations", () => {
    authState.user = userWithClaims("sv");
    const { invalidate } = renderProvider();

    expect(FakeWebSocket.instances).toHaveLength(1);
    const socket = onlySocket();
    expect(socket.url.endsWith("update-stream/subscribe?path=game-servers%2Ffleet")).toBe(true);
    expect(screen.getByText("connecting:0")).toBeInTheDocument();

    act(() => socket.open());
    expect(screen.getByText("open:0")).toBeInTheDocument();
    act(() => socket.message(pinnedFrame));

    expect(invalidate).toHaveBeenCalledTimes(2);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: hostingQueryKeys.fleet, exact: true });
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: hostingQueryKeys.notifications,
      exact: true,
    });
  });

  it("ignores unknown, malformed and foreign frames", () => {
    authState.user = userWithClaims("sv");
    const { invalidate } = renderProvider();
    const socket = onlySocket();
    act(() => socket.open());

    act(() =>
      socket.message('{"$id":"1","value":{"serverId":"3f2c9a52-6d1e-4b7a-9f10-5a8c2d4e6b01"}}'),
    );
    act(() => socket.message("not json"));
    act(() =>
      socket.message(
        '{"$id":"1","type":"X39.UnitedTacticalForces.WebSockets.Contracts.Eventing.EventChangedMessage","value":{"$id":"2","eventId":"3f2c9a52-6d1e-4b7a-9f10-5a8c2d4e6b01"}}',
      ),
    );
    act(() =>
      socket.message(
        '{"$id":"1","type":"X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage","value":{"$id":"2"}}',
      ),
    );

    expect(invalidate).not.toHaveBeenCalled();
  });

  it("reports an unexpected close as reconnecting and starts one disconnection episode", () => {
    authState.user = userWithClaims("sv:view");
    renderProvider();
    const socket = onlySocket();

    act(() => socket.open());
    act(() => socket.serverClose());

    expect(screen.getByText("reconnecting:1")).toBeInTheDocument();
  });

  it("opens no socket for an anonymous viewer, a loading session or a viewer without server access", () => {
    authState.user = undefined;
    const anonymous = renderProvider();
    anonymous.unmount();

    authState.user = userWithClaims("sv");
    authState.isLoading = true;
    const loading = renderProvider();
    loading.unmount();

    authState.isLoading = false;
    authState.user = userWithClaims("crt:ev");
    renderProvider();

    expect(FakeWebSocket.instances).toHaveLength(0);
  });

  it("leaves exactly one live socket under StrictMode and closes it on unmount", () => {
    authState.user = userWithClaims("sv");
    const { unmount } = renderProvider({ strict: true });

    const socket = onlySocket();
    unmount();

    expect(socket.closedByClient).toBe(true);
    expect(liveSockets()).toHaveLength(0);
  });
});
