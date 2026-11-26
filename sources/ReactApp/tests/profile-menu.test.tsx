import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
import type { GameServer, ServerAction, ServerNotification } from "@/features/hosting/types";
import { renderWithProviders } from "./helpers/renderWithProviders";

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

function serverId(index: number): string {
  return `10000000-0000-4000-8000-${String(index).padStart(12, "0")}`;
}

/** The cost-stop action the API projects: the graceful-less terminate action with a typed-title challenge. */
function projectedAction(index: number, overrides: Partial<ServerAction> = {}): ServerAction {
  return {
    id: "terminate",
    label: "Terminate",
    description: "Releases the provider resources.",
    icon: "stop",
    group: "Lifecycle",
    order: 1,
    enabled: true,
    unavailableReason: null,
    dangerous: true,
    confirmationText: null,
    confirmationChallenge: `Server ${index}`,
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
    serverId: serverId(index),
    title: `Server ${index}`,
    statusLabel: index % 2 ? "Raw stage text" : "Waiting for worker",
    possibleCost: index < 3,
    canOperate: true,
    costStopAction: null,
    ...overrides,
  } as ServerNotification;
}

function fleetServer(index: number): GameServer {
  return {
    primaryKey: serverId(index),
    title: `Server ${index}`,
    estimatedHourlyCostEur: 0.2,
    estimatedMonthlyCostEur: null,
    costOwnerName: "Falcon One",
    isLaunchDisabled: false,
    deployment: { status: "Running", possibleCost: true },
    actions: [projectedAction(index)],
    pendingJobCount: 0,
  } as unknown as GameServer;
}

const triggerName = "Open profile menu for Falcon One";

function renderProfile(mode: "community" | "command" = "community") {
  return renderWithProviders(<ProfileMenu mode={mode} />, {
    route: mode === "command" ? "/command" : "/",
  });
}

async function openMenu() {
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: triggerName }));
  return user;
}

describe("signed-in profile and operational attention", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    authState.user = operator;
    authState.logout.mockReset();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
    vi.spyOn(hostingService, "servers").mockResolvedValue([]);
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([]);
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

    const trigger = screen.getByRole("button", { name: triggerName });
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

  it("shows the derived state and cost of the joined fleet server, never the raw status label", async () => {
    vi.mocked(hostingService.notifications).mockResolvedValue([notification(1)]);
    vi.mocked(hostingService.servers).mockResolvedValue([fleetServer(1)]);
    renderProfile();
    await openMenu();

    expect(await screen.findByText("Running")).toBeInTheDocument();
    expect(screen.getByText("Costing now")).toBeInTheDocument();
    expect(screen.queryByText("Raw stage text")).toBeNull();
  });

  it("shows an unknown state for a server missing from the fleet, with the cost decided by possibleCost", async () => {
    vi.mocked(hostingService.notifications).mockResolvedValue([
      notification(1, { possibleCost: true }),
      notification(5, { possibleCost: false }),
    ]);
    renderProfile();
    await openMenu();

    await waitFor(() => expect(screen.getAllByText("Status unknown")).toHaveLength(2));
    expect(screen.getAllByText("May still cost")).toHaveLength(1);
    expect(screen.queryByText("Off")).toBeNull();
    expect(screen.queryByText("Stopped")).toBeNull();
    expect(screen.queryByText("Aus")).toBeNull();
  });

  it("does not load the fleet for an empty notification list", async () => {
    renderProfile();
    await openMenu();

    await screen.findByText("No server currently needs your operational attention.");
    expect(hostingService.notifications).toHaveBeenCalled();
    expect(hostingService.servers).not.toHaveBeenCalled();
  });

  it("loads the fleet once for a non-empty notification list", async () => {
    vi.mocked(hostingService.notifications).mockResolvedValue([notification(1)]);
    vi.mocked(hostingService.servers).mockResolvedValue([fleetServer(1)]);
    renderProfile();
    await openMenu();

    await screen.findByText("Server 1");
    await waitFor(() => expect(hostingService.servers).toHaveBeenCalledOnce());
  });

  it("opens the shared confirmation in place and executes only after the typed title", async () => {
    vi.mocked(hostingService.notifications).mockResolvedValue([
      notification(1, { costStopAction: projectedAction(1) }),
    ]);
    vi.mocked(hostingService.servers).mockResolvedValue([fleetServer(1)]);
    const { router } = renderProfile("command");
    const user = await openMenu();

    await screen.findByText("Running");
    await user.click(screen.getByRole("button", { name: "Shut down without saving" }));

    expect(
      await screen.findByRole("dialog", { name: "Shut down Server 1 now without saving?" }),
    ).toBeVisible();
    expect(screen.queryByRole("dialog", { name: "Profile menu" })).toBeNull();
    expect(hostingService.executeAction).not.toHaveBeenCalled();
    expect(router.state.location.pathname).toBe("/command");

    await user.type(screen.getByRole("textbox"), "Server 1");
    await user.click(screen.getByRole("button", { name: "Shut down without saving" }));
    await waitFor(() =>
      expect(hostingService.executeAction).toHaveBeenCalledWith(
        serverId(1),
        "terminate",
        "Server 1",
      ),
    );
  });

  it("returns focus to the profile trigger when the confirmation is dismissed", async () => {
    vi.mocked(hostingService.notifications).mockResolvedValue([
      notification(1, { costStopAction: projectedAction(1) }),
    ]);
    vi.mocked(hostingService.servers).mockResolvedValue([fleetServer(1)]);
    renderProfile();
    const user = await openMenu();

    await screen.findByText("Running");
    await user.click(screen.getByRole("button", { name: "Shut down without saving" }));
    await user.click(await screen.findByRole("button", { name: "Keep server running" }));

    await waitFor(() =>
      expect(screen.queryByRole("dialog", { name: /Shut down Server 1/ })).toBeNull(),
    );
    await waitFor(() => expect(screen.getByRole("button", { name: triggerName })).toHaveFocus());
    expect(hostingService.executeAction).not.toHaveBeenCalled();
  });

  it.each([
    [
      "the API denies operation authority",
      { canOperate: false, costStopAction: projectedAction(1) },
      "You need permission to operate this server. Ask the server owner or an administrator.",
      "Shut down without saving",
    ],
    [
      "the action is disabled with an unknown reason",
      {
        costStopAction: projectedAction(1, {
          enabled: false,
          unavailableReason: "Disabled by a rule this client does not know.",
        }),
      },
      "Disabled by a rule this client does not know.",
      "Shut down without saving",
    ],
    [
      "the action is pending",
      {
        costStopAction: projectedAction(1, {
          pending: true,
          unavailableReason: "Another lifecycle operation is pending.",
        }),
      },
      "Another change is still running. Try again when it finishes.",
      "In progress",
    ],
  ])(
    "keeps the control aria-disabled with a visible reason when %s",
    async (_, overrides, reason, label) => {
      vi.mocked(hostingService.notifications).mockResolvedValue([notification(1, overrides)]);
      vi.mocked(hostingService.servers).mockResolvedValue([fleetServer(1)]);
      renderProfile();
      const user = await openMenu();

      const control = await screen.findByRole("button", { name: label });
      expect(control).toHaveAttribute("aria-disabled", "true");
      expect(control).not.toBeDisabled();
      expect(screen.getByText(reason, { exact: false })).toBeVisible();

      await user.click(control);
      expect(screen.getByRole("dialog", { name: "Profile menu" })).toBeInTheDocument();
      expect(screen.queryByRole("dialog", { name: /Shut down Server 1/ })).toBeNull();
      expect(hostingService.executeAction).not.toHaveBeenCalled();
    },
  );

  it("marks an action label without a vocabulary entry as English API text", async () => {
    vi.mocked(hostingService.notifications).mockResolvedValue([
      notification(1, {
        costStopAction: projectedAction(1, { id: "custom-cost-stop", label: "Release the node" }),
      }),
    ]);
    renderProfile();
    await openMenu();

    expect(await screen.findByText("Release the node")).toHaveAttribute("lang", "en");
  });

  it("renders every control of the open menu as a shared action control", async () => {
    vi.mocked(hostingService.notifications).mockResolvedValue([
      notification(1, { costStopAction: projectedAction(1) }),
    ]);
    vi.mocked(hostingService.servers).mockResolvedValue([fleetServer(1)]);
    const { container } = renderProfile();
    await openMenu();
    await screen.findByText("Running");

    const buttons = Array.from(container.querySelectorAll("button"));
    expect(buttons.length).toBeGreaterThanOrEqual(4);
    for (const button of buttons) expect(button).toHaveClass("button");
    const popover = screen.getByRole("dialog", { name: "Profile menu" });
    for (const name of ["Command Center", "Account settings"]) {
      expect(within(popover).getByRole("link", { name })).toHaveClass("button");
    }
    expect(within(popover).getByRole("button", { name: "Sign out" })).toHaveClass("button--danger");
    expect(within(popover).getByRole("button", { name: "Close profile menu" })).toHaveAttribute(
      "aria-label",
      "Close profile menu",
    );
  });

  it("moves focus through the rows with Arrow keys and Tab, wrapping at both ends", async () => {
    const { container } = renderProfile();
    const user = await openMenu();
    const popover = screen.getByRole("dialog", { name: "Profile menu" });
    const focusable = Array.from(
      popover.querySelectorAll<HTMLElement>("a[href], button:not([disabled])"),
    );
    expect(focusable.length).toBeGreaterThan(3);
    expect(focusable[0]).toHaveFocus();
    expect(container.querySelector(".account-profile__scrim")).toHaveAttribute("tabindex", "-1");

    await user.keyboard("{ArrowDown}");
    expect(focusable[1]).toHaveFocus();
    await user.keyboard("{ArrowUp}");
    expect(focusable[0]).toHaveFocus();
    await user.keyboard("{ArrowUp}");
    expect(focusable[focusable.length - 1]).toHaveFocus();
    await user.tab();
    expect(focusable[0]).toHaveFocus();
  });

  it("marks the Escape that closes the popover as handled", async () => {
    renderProfile();
    const user = await openMenu();
    const handled: boolean[] = [];
    // A window listener runs after the document listeners, where an enclosing shell navigation also listens.
    const record = (event: KeyboardEvent) => {
      if (event.key === "Escape") handled.push(event.defaultPrevented);
    };
    window.addEventListener("keydown", record);
    try {
      await user.keyboard("{Escape}");
    } finally {
      window.removeEventListener("keydown", record);
    }

    expect(screen.queryByRole("dialog", { name: "Profile menu" })).toBeNull();
    expect(handled).toEqual([true]);
  });

  it("closes with Escape and puts focus back on the avatar button", async () => {
    renderProfile();
    const user = await openMenu();
    const trigger = screen.getByRole("button", { name: triggerName });
    expect(screen.getByRole("dialog", { name: "Profile menu" })).toBeInTheDocument();

    await user.keyboard("{Escape}");

    expect(screen.queryByRole("dialog", { name: "Profile menu" })).toBeNull();
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    expect(trigger).toHaveFocus();
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
