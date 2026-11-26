import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import type { ReactElement } from "react";
import { beforeEach } from "vitest";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import type { PublicConfiguration } from "@/app/config";
import { AppToastProvider } from "@/components/AppToasts";
import { CommandCenterLayout } from "@/components/CommandCenterLayout";
import { CommunityLayout } from "@/components/CommunityLayout";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { hostingService } from "@/features/hosting/services";
import { FakeWebSocket } from "../helpers/fakeWebSocket";

const member = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Falcon One",
  eMail: "falcon@example.test",
  avatar: null,
  avatarMimeType: null,
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  steamId64: null,
  discordId: null,
  discordUsername: null,
  claims: [
    {
      primaryKey: 1,
      identifier: "sv",
      value: null,
      title: "Servers",
      category: "Hosting",
      description: "",
      isPrefix: false,
      valueType: null,
    },
  ],
  roles: [],
} as unknown as FullUser;

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

function Probe() {
  useCostConfirmation();
  return <p>provider ready</p>;
}

function renderLayout(layout: ReactElement, path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      {
        path: path === "/" ? "/" : path,
        element: layout,
        children: [{ index: true, element: <Probe /> }],
      },
    ],
    { initialEntries: [path] },
  );
  return render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe("route layouts provide the cost confirmation", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = member;
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
  });

  afterEach(() => {
    authState.user = undefined;
    vi.restoreAllMocks();
  });

  it("mounts the provider inside the command center layout", () => {
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");
    expect(screen.getByText("provider ready")).toBeInTheDocument();
  });

  it("mounts the provider inside the community layout", () => {
    renderLayout(<CommunityLayout configuration={configuration} />, "/");
    expect(screen.getByText("provider ready")).toBeInTheDocument();
  });
});

describe("route layouts keep the fleet cache live", () => {
  const fleetSocketSuffix = "update-stream/subscribe?path=game-servers%2Ffleet";

  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = member;
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
  });

  afterEach(() => {
    authState.user = undefined;
    vi.restoreAllMocks();
  });

  it("subscribes to the fleet signal once inside the command center layout", () => {
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");

    expect(screen.getByText("provider ready")).toBeInTheDocument();
    expect(FakeWebSocket.instances).toHaveLength(1);
    expect(FakeWebSocket.instances[0]?.url.endsWith(fleetSocketSuffix)).toBe(true);
  });

  it("subscribes to the fleet signal once inside the community layout", () => {
    renderLayout(<CommunityLayout configuration={configuration} />, "/");

    expect(screen.getByText("provider ready")).toBeInTheDocument();
    expect(FakeWebSocket.instances).toHaveLength(1);
    expect(FakeWebSocket.instances[0]?.url.endsWith(fleetSocketSuffix)).toBe(true);
  });

  it("opens no socket for an anonymous community visitor", () => {
    authState.user = undefined;
    renderLayout(<CommunityLayout configuration={configuration} />, "/");

    expect(screen.getByText("provider ready")).toBeInTheDocument();
    expect(FakeWebSocket.instances).toHaveLength(0);
  });
});
