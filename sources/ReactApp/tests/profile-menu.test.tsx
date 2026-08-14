import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import {
  avatarSource,
  maskAccountIdentifier,
  ProfileMenu,
  profileNotificationPollInterval,
} from "@/components/ProfileMenu";
import { hostingService } from "@/features/hosting/services";
import type { ServerAction, ServerNotification } from "@/features/hosting/types";

const authState = vi.hoisted(() => ({
  user: undefined as FullUser | undefined,
  logout: vi.fn(),
}));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isAuthenticated: Boolean(authState.user),
    isLoading: false,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: authState.logout,
  }),
}));

const operator = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Falcon One",
  eMail: "falcon@example.test",
  avatar: "AQIDBA==",
  avatarMimeType: "image/jpeg",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: true,
  vereinMembershipStatus: "Active",
  vereinMemberNumber: "UTF-42",
  steamId64: "76561198012345678",
  discordId: "123456789",
  discordUsername: "falcon.discord",
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
    {
      primaryKey: 2,
      identifier: "sv:operate",
      value: "server-1",
      title: "Operate assigned server",
      category: "Hosting",
      description: "",
      isPrefix: false,
      valueType: null,
    },
  ],
  roles: [
    {
      primaryKey: 1,
      title: "Operations Lead",
      description: "Hosts community operations",
      claims: [],
    },
  ],
} as FullUser;

function projectedAction(overrides: Partial<ServerAction> = {}): ServerAction {
  return {
    id: "stop-cost",
    label: "Stop provider cost",
    description: "Stops provider resources.",
    icon: "stop",
    group: "Lifecycle",
    order: 1,
    enabled: true,
    unavailableReason: null,
    dangerous: true,
    confirmationText: null,
    confirmationChallenge: null,
    pending: false,
    expectedDurationSeconds: 30,
    costImpact: "StopsCost",
    costDescription: "Stops hourly cost.",
    ...overrides,
  } as ServerAction;
}

function notification(
  index: number,
  overrides: Partial<ServerNotification> = {},
): ServerNotification {
  return {
    serverId: `10000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
    title: `Server ${index}`,
    statusLabel: index % 2 ? "Running" : "Waiting for worker",
    possibleCost: index < 3,
    canOperate: true,
    costStopAction: null,
    ...overrides,
  } as ServerNotification;
}

function renderProfile(mode: "community" | "command" = "community") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter([{ path: "*", element: <ProfileMenu mode={mode} /> }], {
    initialEntries: [mode === "command" ? "/command" : "/"],
  });
  const result = render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
  return { ...result, router };
}

describe("signed-in profile and operational attention", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    authState.user = operator;
    authState.logout.mockReset();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
    vi.spyOn(hostingService, "executeAction").mockResolvedValue({} as never);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  it("polls the authorized notification projection every fifteen seconds", async () => {
    vi.useFakeTimers();
    renderProfile();

    await act(async () => vi.advanceTimersByTimeAsync(1));
    expect(hostingService.notifications).toHaveBeenCalledOnce();

    await act(async () => vi.advanceTimersByTimeAsync(profileNotificationPollInterval));
    expect(hostingService.notifications).toHaveBeenCalledTimes(2);
  });

  it("restores the avatar, account status, authority and linked-account detail", async () => {
    const projectedNotifications = Array.from({ length: 7 }, (_, index) => notification(index + 1));
    vi.mocked(hostingService.notifications).mockResolvedValue(projectedNotifications);
    const { container } = renderProfile();
    const user = userEvent.setup();

    const trigger = screen.getByRole("button", { name: "Open profile menu for Falcon One" });
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    expect(container.querySelector(".account-profile__trigger img")).toHaveAttribute(
      "src",
      "data:image/jpeg;base64,AQIDBA==",
    );
    await user.click(trigger);

    expect(trigger).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("dialog", { name: "Profile menu" })).toBeInTheDocument();
    expect(screen.getAllByText("Verified").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Operations Lead").length).toBeGreaterThan(0);
    expect(screen.getByText("View servers, Operate assigned server")).toBeInTheDocument();
    expect(screen.getByText("7656...5678")).toBeInTheDocument();
    expect(screen.getByText("falcon.discord")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Command Center" })).toHaveAttribute(
      "href",
      "/command",
    );

    await waitFor(() =>
      expect(container.querySelectorAll(".account-profile__notification")).toHaveLength(5),
    );
    expect(screen.getByRole("link", { name: "View all game servers" })).toHaveAttribute(
      "href",
      "/command/servers",
    );
    expect(container.querySelector(".account-profile__attention-badge")).toHaveTextContent("2");

    await user.click(screen.getByRole("button", { name: "Sign out" }));
    expect(authState.logout).toHaveBeenCalledOnce();
  });

  it("requires server-authoritative confirmation before queueing a projected action", async () => {
    const action = projectedAction({
      confirmationText: "This releases the running provider resources.",
    });
    vi.mocked(hostingService.notifications).mockResolvedValue([
      notification(1, { costStopAction: action }),
    ]);
    renderProfile("command");
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Open profile menu for Falcon One" }));
    await screen.findByText("Server 1");
    await user.click(screen.getByRole("button", { name: "Stop provider cost" }));
    expect(hostingService.executeAction).not.toHaveBeenCalled();
    expect(screen.getByText("This releases the running provider resources.")).toBeInTheDocument();

    await user.click(screen.getAllByRole("button", { name: "Stop provider cost" })[1]!);
    await waitFor(() =>
      expect(hostingService.executeAction).toHaveBeenCalledWith(
        "10000000-0000-4000-8000-000000000001",
        "stop-cost",
      ),
    );
  });

  it("routes challenge actions to the full server workspace without executing them inline", async () => {
    const action = projectedAction({ confirmationChallenge: "TERMINATE" });
    vi.mocked(hostingService.notifications).mockResolvedValue([
      notification(9, { costStopAction: action }),
    ]);
    const { router } = renderProfile();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Open profile menu for Falcon One" }));
    await screen.findByText("Server 9");
    await user.click(screen.getByRole("button", { name: "Stop provider cost" }));

    expect(hostingService.executeAction).not.toHaveBeenCalled();
    expect(router.state.location.pathname).toBe(
      "/command/servers/10000000-0000-4000-8000-000000000009",
    );
    expect(router.state.location.search).toBe("?action=stop-cost");
  });

  it("keeps projected actions disabled when the API denies operation authority", async () => {
    vi.mocked(hostingService.notifications).mockResolvedValue([
      notification(1, { canOperate: false, costStopAction: projectedAction() }),
    ]);
    renderProfile();
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Open profile menu for Falcon One" }));
    expect(await screen.findByRole("button", { name: "Stop provider cost" })).toBeDisabled();
  });
});

describe("profile value safety", () => {
  it("uses only allow-listed image MIME types and masks long provider identifiers", () => {
    expect(avatarSource({ ...operator, avatarMimeType: "image/svg+xml" })).toBe(
      "data:image/png;base64,AQIDBA==",
    );
    expect(maskAccountIdentifier("1234567890")).toBe("1234...7890");
    expect(maskAccountIdentifier("123456")).toBe("123456");
  });
});
