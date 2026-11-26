import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { ProfileMenu } from "@/components/ProfileMenu";
import {
  CostConfirmationContext,
  type CostConfirmationContextValue,
} from "@/components/server/useCostConfirmation";
import {
  costAlertRaisedMessageType,
  costAlertDismissedMessageType,
} from "@/features/hosting/costFrames";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { CostAlert, ServerNotification } from "@/features/hosting/types";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import { autoStopAlert, costAlert } from "../helpers/costAlertFixtures";
import { FakeWebSocket } from "../helpers/fakeWebSocket";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/*
 * Profile menu half of D-07 and D-08: alert rows come first inside Server attention, the badge counts distinct
 * servers, one warning toast follows each frame-delivered alert and the rows never poll.
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

const triggerName = "Open profile menu for Falcon One";

function serverIdOf(index: number): string {
  return `f1ee7000-0000-4000-8000-${String(index).padStart(12, "0")}`;
}

function alertFor(index: number, overrides: Partial<CostAlert> = {}): CostAlert {
  return costAlert({
    id: `a0000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
    serverId: serverIdOf(index),
    serverName: `Ops Night ${index}`,
    thresholdAmount: 5,
    accruedAmount: 5.02,
    createdAt: `2026-11-21T1${index}:05:00Z`,
    ...overrides,
  });
}

function notificationFor(index: number, overrides: Partial<ServerNotification> = {}) {
  return {
    serverId: serverIdOf(index),
    title: `Fleet Server ${index}`,
    statusLabel: "Raw stage text",
    possibleCost: true,
    canOperate: true,
    costStopAction: null,
    ...overrides,
  } as ServerNotification;
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

function renderMenu(request?: CostConfirmationContextValue["request"]) {
  const menu = (
    <FleetLiveUpdatesProvider configuration={configuration}>
      <ProfileMenu mode="command" />
    </FleetLiveUpdatesProvider>
  );
  return renderWithProviders(
    request ? (
      <CostConfirmationContext.Provider value={{ request }}>
        {menu}
      </CostConfirmationContext.Provider>
    ) : (
      menu
    ),
    { route: "/command" },
  );
}

async function openMenu() {
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: triggerName }));
  return user;
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

/** Opens the socket and waits until the refetch that an open triggers has settled. */
async function openSocket(queryClient: { isFetching(filters: object): number }): Promise<void> {
  act(() => latestSocket().open());
  await waitFor(() =>
    expect(queryClient.isFetching({ queryKey: hostingQueryKeys.costAlerts })).toBe(0),
  );
}

describe("cost alerts in the profile menu", () => {
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

  it("lists the alert rows first, then the server notification rows", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertFor(2), alertFor(1)]);
    vi.mocked(hostingService.notifications).mockResolvedValue([notificationFor(3)]);
    const { container } = renderMenu();
    await openMenu();

    await screen.findByText("Fleet Server 3");
    const rows = Array.from(container.ownerDocument.querySelectorAll("article"));
    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveClass("account-profile__notification--alert");
    expect(rows[1]).toHaveClass("account-profile__notification--alert");
    expect(rows[2]).not.toHaveClass("account-profile__notification--alert");

    const first = within(rows[0]!);
    const link = first.getByRole("link");
    expect(link).toHaveAttribute("href", `/command/servers/${serverIdOf(2)}`);
    expect(first.getByText("Ops Night 2").tagName).toBe("BDI");
    expect(first.getByText("Ops Night 2").parentElement?.tagName).toBe("STRONG");
    expect(first.getByText(`Cost alert: ${money(5)} reached`)).toBeVisible();
    expect(first.getByText(/this session, as of/)).toBeVisible();
  });

  it("lays a row out as an alert icon column and a text column like the server rows", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertFor(1)]);
    vi.mocked(hostingService.servers).mockResolvedValue([
      fleetServer({ index: 1, title: "Ops Night 1", status: "Running" }),
    ]);
    const { container } = renderMenu();
    await openMenu();
    await screen.findByText("Ops Night 1");

    const row = container.ownerDocument.querySelector("article") as HTMLElement;
    const [icon, content] = Array.from(row.children);
    expect(icon?.tagName.toLowerCase()).toBe("svg");
    expect(icon).toHaveAttribute("aria-hidden", "true");
    expect(row.children).toHaveLength(2);

    const text = within(content as HTMLElement);
    // The server name is the only strong text; the reached line is a plain paragraph.
    expect(content?.querySelectorAll("strong")).toHaveLength(1);
    expect(text.getByText("Ops Night 1").closest("strong")).not.toBeNull();
    expect(text.getByText(`Cost alert: ${money(5)} reached`).tagName).toBe("P");
    expect(text.getByText(/this session, as of/)).toBeVisible();

    // The actions sit in the text column, under the text, and the Dismiss button carries a hidden icon.
    const actions = content?.querySelector(".cost-alert-item__actions") as HTMLElement;
    expect(actions).not.toBeNull();
    const lastLine = content?.querySelector(".cost-alert-item__amount") as HTMLElement;
    expect(
      Boolean(lastLine.compareDocumentPosition(actions) & Node.DOCUMENT_POSITION_FOLLOWING),
    ).toBe(true);
    const dismiss = within(actions).getByRole("button", { name: /^Dismiss alert at/ });
    expect(dismiss.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
    expect(dismiss).toHaveTextContent("Dismiss alert");
    expect(
      within(actions).getByRole("button", { name: "Shut down and stop billing" }),
    ).toBeVisible();
  });

  it("shows three alert rows and one line for the rest", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([
      alertFor(4),
      alertFor(3),
      alertFor(2),
      alertFor(1),
    ]);
    const { container } = renderMenu();
    await openMenu();

    await screen.findByText("Ops Night 4");
    expect(container.ownerDocument.querySelectorAll("article")).toHaveLength(3);
    expect(screen.queryByText("Ops Night 1")).toBeNull();
    expect(screen.getByText("and 1 more cost alert. Open each server to see them.")).toBeVisible();
  });

  it("shows the empty copy only when there are no notifications and no alerts", async () => {
    renderMenu();
    await openMenu();
    expect(
      await screen.findByText("No server currently needs your operational attention."),
    ).toBeVisible();
    expect(hostingService.servers).not.toHaveBeenCalled();
  });

  it("hides the empty copy and loads the fleet when only an alert exists", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertFor(1)]);
    renderMenu();
    await openMenu();

    await screen.findByText("Ops Night 1");
    expect(screen.queryByText("No server currently needs your operational attention.")).toBeNull();
    await waitFor(() => expect(hostingService.servers).toHaveBeenCalledOnce());
  });

  it("counts distinct servers in the badge and describes it for assistive technology", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertFor(1), alertFor(2)]);
    vi.mocked(hostingService.notifications).mockResolvedValue([
      notificationFor(1),
      notificationFor(3, { possibleCost: false }),
    ]);
    const { container } = renderMenu();

    await waitFor(() =>
      expect(container.querySelector(".account-profile__attention-badge")).toHaveTextContent("2"),
    );
    const trigger = screen.getByRole("button", { name: triggerName });
    const describedBy = trigger.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    expect(document.getElementById(describedBy ?? "")).toHaveTextContent(
      "2 servers need your attention",
    );
  });

  it("has no badge description without anything needing attention", async () => {
    renderMenu();
    await waitFor(() => expect(hostingService.costAlerts).toHaveBeenCalled());
    expect(screen.getByRole("button", { name: triggerName })).not.toHaveAttribute(
      "aria-describedby",
    );
  });

  it("toasts once for a newly pushed alert and never for loaded, cached or refetched alerts", async () => {
    const known = alertFor(1);
    vi.mocked(hostingService.costAlerts).mockResolvedValue([known]);
    const { queryClient } = renderMenu();
    await waitFor(() =>
      expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([known]),
    );
    expect(screen.queryByText(/Cost alert: Ops Night/)).toBeNull();
    await openSocket(queryClient);
    expect(screen.queryByText(/Cost alert: Ops Night/)).toBeNull();

    await deliver(raisedFrame(known));
    expect(screen.queryByText(/Cost alert: Ops Night/)).toBeNull();

    const fresh = alertFor(2);
    await deliver(raisedFrame(fresh));
    const toast = await screen.findByText(`Cost alert: Ops Night 2 reached ${money(5)}.`);
    expect(toast).toBeVisible();
    await deliver(raisedFrame(fresh));
    expect(screen.getAllByText(/Cost alert: Ops Night 2 reached/)).toHaveLength(1);

    // A reconnect refetch returns the alert from the API and is silent.
    vi.mocked(hostingService.costAlerts).mockResolvedValue([fresh, known, alertFor(3)]);
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.costAlerts, exact: true });
    });
    expect(screen.queryByText(/Cost alert: Ops Night 3 reached/)).toBeNull();
    expect(screen.getAllByText(/Cost alert: Ops Night 2 reached/)).toHaveLength(1);
  });

  it("removes a row for a dismissed frame without a toast and never polls the alerts", async () => {
    const alert = alertFor(1);
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alert]);
    const { queryClient } = renderMenu();
    await openMenu();
    await screen.findByText("Ops Night 1");
    await openSocket(queryClient);

    await deliver(
      JSON.stringify({ type: costAlertDismissedMessageType, value: { alertId: alert.id } }),
    );

    await waitFor(() => expect(screen.queryByText("Ops Night 1")).toBeNull());
    expect(screen.queryByText(/Cost alert: Ops Night/)).toBeNull();
    const observers =
      queryClient.getQueryCache().find({ queryKey: hostingQueryKeys.costAlerts })?.observers ?? [];
    expect(observers.length).toBeGreaterThan(0);
    expect(observers.every((observer) => observer.options.refetchInterval === undefined)).toBe(
      true,
    );
  });

  it("dismisses a row after the API accepted it and moves focus to the next row", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertFor(2), alertFor(1)]);
    const dismiss = vi.spyOn(hostingService, "dismissCostAlert").mockResolvedValue(undefined);
    renderMenu();
    const user = await openMenu();
    await screen.findByText("Ops Night 2");

    await user.click(screen.getAllByRole("button", { name: /^Dismiss alert at/ })[0]!);

    expect(dismiss).toHaveBeenCalledWith(alertFor(2).id);
    // The success toast names the server and the amount (D-17).
    expect(
      await screen.findByText(`Cost alert dismissed: Ops Night 2, ${money(5)}.`),
    ).toBeVisible();
    await waitFor(() => expect(screen.queryByText("Ops Night 2")).toBeNull());
    await waitFor(() => expect(screen.getByRole("link", { name: "Ops Night 1" })).toHaveFocus());

    await user.click(screen.getByRole("button", { name: /^Dismiss alert at/ }));
    await waitFor(() => expect(screen.queryByText("Ops Night 1")).toBeNull());
    await waitFor(() => expect(document.querySelector(".account-profile__close")).toHaveFocus());
  });

  it("keeps the row and shows an alert line when the dismissal fails", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertFor(1)]);
    vi.spyOn(hostingService, "dismissCostAlert").mockRejectedValue(new Error("offline"));
    renderMenu();
    const user = await openMenu();
    await screen.findByText("Ops Night 1");

    await user.click(screen.getByRole("button", { name: /^Dismiss alert at/ }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "The alert could not be dismissed. Try again.",
    );
    expect(screen.getByText("Ops Night 1")).toBeVisible();
    expect(document.querySelector(".app-toast")).toBeNull();
  });

  it("offers the cost-stop button only for a server in the fleet result that has a cost-stop action", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertFor(2), alertFor(1)]);
    vi.mocked(hostingService.servers).mockResolvedValue([
      fleetServer({ index: 1, title: "Ops Night 1", status: "Running" }),
    ]);
    const execute = vi.spyOn(hostingService, "executeAction").mockResolvedValue({} as never);
    const request = vi.fn();
    renderMenu(request);
    const user = await openMenu();

    const buttons = await screen.findAllByRole("button", { name: "Shut down and stop billing" });
    expect(buttons).toHaveLength(1);
    const rowOfServerOne = buttons[0]!.closest("article");
    expect(rowOfServerOne).toHaveTextContent("Ops Night 1");
    expect(buttons[0]!.className).toContain("button--ghost");

    await user.click(buttons[0]!);

    expect(request).toHaveBeenCalledOnce();
    expect(request).toHaveBeenCalledWith(
      expect.objectContaining({
        serverId: serverIdOf(1),
        actionId: "stop",
        source: "alert-notification",
      }),
    );
    expect(execute).not.toHaveBeenCalled();
    // The menu closed so the confirmation takes the screen.
    expect(screen.queryByRole("dialog", { name: "Profile menu" })).toBeNull();
  });

  it("renders no cost-stop button for a server whose actions hold no cost-stop", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([alertFor(1)]);
    vi.mocked(hostingService.servers).mockResolvedValue([
      fleetServer({
        index: 1,
        title: "Ops Night 1",
        status: "Running",
        actions: [fleetAction("collect-logs")],
      }),
    ]);
    renderMenu();
    await openMenu();

    await screen.findByText("Ops Night 1");
    await waitFor(() => expect(hostingService.servers).toHaveBeenCalled());
    expect(screen.queryByRole("button", { name: /Shut down/ })).toBeNull();
  });
});

describe("automatic shutdown alerts in the profile menu", () => {
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

  function autoStopFor(index: number, overrides: Partial<CostAlert> = {}): CostAlert {
    return autoStopAlert({
      id: `a2000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
      serverId: serverIdOf(index),
      serverName: `Ops Night ${index}`,
      createdAt: `2026-11-21T1${index}:05:00Z`,
      ...overrides,
    });
  }

  function autoStopFrame(alert: CostAlert): string {
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
        kind: alert.kind,
        stopsAt: alert.stopsAt,
        stopReason: alert.stopReason,
        stopLimit: alert.stopLimit,
      },
    });
  }

  it("renders the warning as an alert row with the time, the reason and a dismiss button but no cost-stop button", async () => {
    const warning = autoStopFor(1);
    vi.mocked(hostingService.costAlerts).mockResolvedValue([warning]);
    vi.mocked(hostingService.servers).mockResolvedValue([
      fleetServer({ index: 1, title: "Ops Night 1", status: "Running" }),
    ]);
    const { container } = renderMenu();
    await openMenu();
    await screen.findByText("Ops Night 1");
    await waitFor(() => expect(hostingService.servers).toHaveBeenCalled());

    const row = container.ownerDocument.querySelector("article") as HTMLElement;
    expect(row).toHaveClass("account-profile__notification--alert");
    expect(row).toHaveAttribute("data-auto-stop-alert-id", warning.id);
    expect(row).not.toHaveAttribute("data-cost-alert-id");
    const [icon] = Array.from(row.children);
    expect(icon?.tagName.toLowerCase()).toBe("svg");
    expect(icon).toHaveAttribute("aria-hidden", "true");

    const text = within(row);
    expect(text.getByRole("link")).toHaveAttribute("href", `/command/servers/${serverIdOf(1)}`);
    expect(text.getByText("Ops Night 1").tagName).toBe("BDI");
    const time = formatInstant(warning.stopsAt ?? "");
    expect(text.getByText(`Automatic shutdown at ${time}`)).toBeVisible();
    expect(
      text.getByText(
        "Nobody has been on the server for 30 minutes. It shuts down unless someone joins.",
      ),
    ).toBeVisible();
    const dismiss = text.getByRole("button", {
      name: `Dismiss alert: automatic shutdown at ${time}`,
    });
    expect(dismiss).toHaveTextContent("Dismiss alert");
    expect(dismiss.className).toContain("button--ghost");
    expect(dismiss.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
    expect(text.getAllByRole("button")).toHaveLength(1);
    expect(screen.queryByRole("button", { name: /Shut down/ })).toBeNull();
  });

  it("shares the three row cap with cost alerts and counts distinct servers in the badge", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([
      autoStopFor(4),
      alertFor(3),
      autoStopFor(2),
      alertFor(2),
    ]);
    const { container } = renderMenu();

    await waitFor(() =>
      expect(container.querySelector(".account-profile__attention-badge")).toHaveTextContent("3"),
    );
    await openMenu();
    await screen.findByText("Ops Night 4");
    const rows = Array.from(container.ownerDocument.querySelectorAll("article"));
    expect(rows).toHaveLength(3);
    expect(container.ownerDocument.querySelectorAll("[data-cost-alert-id]")).toHaveLength(1);
    expect(container.ownerDocument.querySelectorAll("[data-auto-stop-alert-id]")).toHaveLength(2);
    expect(screen.getByText("and 1 more cost alert. Open each server to see them.")).toBeVisible();
  });

  it("toasts once for a newly pushed warning and never for loaded or cached ones", async () => {
    const known = autoStopFor(1);
    vi.mocked(hostingService.costAlerts).mockResolvedValue([known]);
    const { queryClient } = renderMenu();
    await waitFor(() =>
      expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([known]),
    );
    await openSocket(queryClient);
    expect(screen.queryByText(/Automatic shutdown at/)).toBeNull();

    await deliver(autoStopFrame(known));
    expect(screen.queryByText(/Automatic shutdown at/)).toBeNull();

    const fresh = autoStopFor(2);
    await deliver(autoStopFrame(fresh));
    const toast = await screen.findByText(
      `Automatic shutdown at ${formatInstant(fresh.stopsAt ?? "")}: Ops Night 2.`,
    );
    expect(toast).toBeVisible();
    await deliver(autoStopFrame(fresh));
    expect(screen.getAllByText(/Automatic shutdown at .*: Ops Night 2\./)).toHaveLength(1);

    // A reconnect refetch returns the warning from the API and is silent.
    vi.mocked(hostingService.costAlerts).mockResolvedValue([fresh, known, autoStopFor(3)]);
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.costAlerts, exact: true });
    });
    expect(screen.queryByText(/: Ops Night 3\./)).toBeNull();
    expect(screen.getAllByText(/Automatic shutdown at .*: Ops Night 2\./)).toHaveLength(1);
  });

  it("keeps the threshold toast for a threshold arrival next to a warning arrival", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([]);
    const { queryClient } = renderMenu();
    await openSocket(queryClient);

    await deliver(raisedFrame(alertFor(1)));

    expect(await screen.findByText(`Cost alert: Ops Night 1 reached ${money(5)}.`)).toBeVisible();
    expect(screen.queryByText(/Automatic shutdown at/)).toBeNull();
  });

  it("dismisses only that warning, without the cost toast, and moves focus to the next row", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([autoStopFor(2), autoStopFor(1)]);
    const dismiss = vi.spyOn(hostingService, "dismissCostAlert").mockResolvedValue(undefined);
    const cancel = vi.spyOn(hostingService, "executeAction").mockResolvedValue({} as never);
    renderMenu();
    const user = await openMenu();
    await screen.findByText("Ops Night 2");

    await user.click(
      screen.getAllByRole("button", { name: /^Dismiss alert: automatic shutdown/ })[0]!,
    );

    expect(dismiss).toHaveBeenCalledExactlyOnceWith(autoStopFor(2).id);
    expect(cancel).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.queryByText("Ops Night 2")).toBeNull());
    await waitFor(() => expect(screen.getByRole("link", { name: "Ops Night 1" })).toHaveFocus());
    expect(screen.queryByText(/Cost alert dismissed/)).toBeNull();
  });

  it("keeps the row and shows an alert line when the dismissal fails", async () => {
    vi.mocked(hostingService.costAlerts).mockResolvedValue([autoStopFor(1)]);
    vi.spyOn(hostingService, "dismissCostAlert").mockRejectedValue(new Error("offline"));
    renderMenu();
    const user = await openMenu();
    await screen.findByText("Ops Night 1");

    await user.click(screen.getByRole("button", { name: /^Dismiss alert: automatic shutdown/ }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "The alert could not be dismissed. Try again.",
    );
    expect(screen.getByText("Ops Night 1")).toBeVisible();
  });
});
