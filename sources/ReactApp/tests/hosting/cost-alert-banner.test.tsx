import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { I18nextProvider } from "react-i18next";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { CostAlertBanner } from "@/components/server/CostAlertBanner";
import {
  CostConfirmationContext,
  type CostConfirmationContextValue,
} from "@/components/server/useCostConfirmation";
import { workspaceTitleId } from "@/components/server/WorkspaceHeader";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import {
  costAlertRaisedMessageType,
  costAlertDismissedMessageType,
} from "@/features/hosting/costFrames";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { CostAlert } from "@/features/hosting/types";
import { useDismissCostAlerts } from "@/features/hosting/useCostAlerts";
import { autoStopAlert, costAlert } from "../helpers/costAlertFixtures";
import { FakeWebSocket } from "../helpers/fakeWebSocket";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { toastPoliteness } from "../helpers/toastQueries";

/*
 * Tracer for D-07 and D-08: a pushed alert shows up in the workspace banner of its own server without a reload,
 * is deduplicated by id, and leaves only after the API accepted its dismissal.
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

const serverA = fleetServer({ index: 1, title: "Ops Night", status: "Running" });
const serverB = fleetServer({ index: 2, title: "Other Server", status: "Running" });

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

function money(value: number): string {
  return new Intl.NumberFormat("en-US", { style: "currency", currency: "EUR" }).format(value);
}

function clock(instant: string): string {
  return new Intl.DateTimeFormat("en-US", { timeStyle: "short" }).format(new Date(instant));
}

function alertFor(
  server: { primaryKey: string; title: string },
  overrides: Partial<CostAlert> = {},
): CostAlert {
  return costAlert({
    id: `a-${server.primaryKey}-${overrides.thresholdAmount ?? 5}`,
    serverId: server.primaryKey,
    serverName: server.title,
    ...overrides,
  });
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

/** Renders the banner under the live provider, a recording confirmation entry point and a focusable heading. */
function renderBanner(
  server = serverA,
  request: CostConfirmationContextValue["request"] = vi.fn(),
  extra: ReactNode = null,
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const view = render(
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <AppToastProvider>
          <CostConfirmationContext.Provider value={{ request }}>
            <FleetLiveUpdatesProvider configuration={configuration}>
              <h1 id={workspaceTitleId} tabIndex={-1}>
                {server.title}
              </h1>
              <CostAlertBanner server={server} />
              {extra}
            </FleetLiveUpdatesProvider>
          </CostConfirmationContext.Provider>
        </AppToastProvider>
      </I18nextProvider>
    </QueryClientProvider>,
  );
  return { ...view, queryClient, request };
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

/* Pins the clock to the fixture day so time-only expectations hold on any run date. */
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"], shouldAdvanceTime: true });
  vi.setSystemTime(new Date("2026-11-21T14:30:00Z"));
});

afterEach(() => {
  vi.useRealTimers();
});

describe("cost alert banner", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    authState.user = memberWithServerClaim();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  it("names the server, the threshold and the amount of this server's alert only", async () => {
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([
      alertFor(serverA, { thresholdAmount: 5, accruedAmount: 5.03 }),
      alertFor(serverB, { thresholdAmount: 10, accruedAmount: 10.01 }),
    ]);

    renderBanner();

    expect(await screen.findByRole("heading", { name: "Cost alert" })).toBeVisible();
    const row = screen.getByText(
      `${money(5.03)} this session, as of ${clock("2026-11-21T14:05:00Z")}.`,
    );
    expect(row.closest("time")).toHaveAttribute("datetime", "2026-11-21T14:05:00Z");
    const reached = screen.getByText(`${money(5)} reached`);
    expect(reached).toBeVisible();
    expect(reached.closest("li")).not.toHaveTextContent("Ops Night");
    expect(reached.closest("li")?.querySelector("bdi")).toBeNull();
    expect(
      within(reached.closest("li") as HTMLElement).getByRole("button", {
        name: `Dismiss alert at ${money(5)}`,
      }),
    ).toBeVisible();
    expect(screen.queryByText("Other Server")).toBeNull();
    expect(screen.getAllByRole("listitem")).toHaveLength(1);
    // One alert shows no Dismiss all.
    expect(screen.queryByRole("button", { name: /Dismiss all/ })).toBeNull();
  });

  it("renders nothing while the alerts have not loaded and nothing for an empty list", async () => {
    let resolve: (alerts: CostAlert[]) => void = () => undefined;
    vi.spyOn(hostingService, "costAlerts").mockReturnValue(
      new Promise<CostAlert[]>((done) => {
        resolve = done;
      }),
    );
    const { container } = renderBanner();

    expect(container.querySelector(".state-panel")).toBeNull();
    await act(async () => resolve([]));
    await waitFor(() => expect(hostingService.costAlerts).toHaveBeenCalled());
    expect(container.querySelector(".state-panel")).toBeNull();
    expect(screen.queryByText(/No alerts/i)).toBeNull();
  });

  it("shows a pushed alert without a reload, once, and lists the newest first", async () => {
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([]);
    const { queryClient } = renderBanner();
    await waitFor(() => expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([]));
    act(() => latestSocket().open());
    // Opening refetches the list once, which recovers alerts missed before the subscribe; wait for it to settle.
    await waitFor(() => expect(hostingService.costAlerts).toHaveBeenCalledTimes(2));
    await waitFor(() =>
      expect(queryClient.isFetching({ queryKey: hostingQueryKeys.costAlerts })).toBe(0),
    );
    expect(screen.queryByRole("heading", { name: "Cost alert" })).toBeNull();

    const first = alertFor(serverA, { thresholdAmount: 5, createdAt: "2026-11-21T14:05:00Z" });
    await deliver(raisedFrame(first));
    expect(await screen.findByRole("heading", { name: "Cost alert" })).toBeVisible();

    await deliver(raisedFrame(first));
    expect(screen.getAllByRole("listitem")).toHaveLength(1);

    const second = alertFor(serverA, {
      thresholdAmount: 10,
      accruedAmount: 10.02,
      createdAt: "2026-11-21T15:10:00Z",
    });
    await deliver(raisedFrame(second));
    expect(await screen.findByRole("heading", { name: "2 cost alerts" })).toBeVisible();
    const rows = screen.getAllByRole("listitem");
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent(`${money(10)} reached`);
    expect(rows[1]).toHaveTextContent(`${money(5)} reached`);
    expect(hostingService.costAlerts).toHaveBeenCalledTimes(2);
  });

  it("removes an alert when a dismissed frame arrives and ignores an unknown id", async () => {
    const alert = alertFor(serverA);
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alert]);
    renderBanner();
    expect(await screen.findByRole("heading", { name: "Cost alert" })).toBeVisible();
    act(() => latestSocket().open());

    await deliver(
      JSON.stringify({ type: costAlertDismissedMessageType, value: { alertId: "unknown" } }),
    );
    expect(screen.getByRole("heading", { name: "Cost alert" })).toBeVisible();

    await deliver(
      JSON.stringify({ type: costAlertDismissedMessageType, value: { alertId: alert.id } }),
    );
    await waitFor(() => expect(screen.queryByRole("heading", { name: "Cost alert" })).toBeNull());
  });

  it("dismisses one alert with one request, keeps the button aria-disabled meanwhile and removes the row after success", async () => {
    const user = userEvent.setup();
    const alert = alertFor(serverA, { thresholdAmount: 5 });
    const other = alertFor(serverA, { thresholdAmount: 10, createdAt: "2026-11-21T15:00:00Z" });
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([other, alert]);
    let finish: () => void = () => undefined;
    const dismiss = vi.spyOn(hostingService, "dismissCostAlert").mockReturnValue(
      new Promise<void>((done) => {
        finish = done;
      }),
    );
    renderBanner();
    const button = await screen.findByRole("button", {
      name: `Dismiss alert at ${money(5)}`,
    });

    await user.click(button);
    await user.click(button);

    expect(dismiss).toHaveBeenCalledTimes(1);
    expect(dismiss).toHaveBeenCalledWith(alert.id);
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(screen.getAllByRole("listitem")).toHaveLength(2);

    await act(async () => finish());
    await waitFor(() => expect(screen.getAllByRole("listitem")).toHaveLength(1));
    expect(screen.queryByRole("button", { name: `Dismiss alert at ${money(5)}` })).toBeNull();
  });

  it("lists three rows and collapses the rest into the older alerts line", async () => {
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue(
      [5, 10, 15, 20, 25].map((amount, index) =>
        alertFor(serverA, {
          thresholdAmount: amount,
          createdAt: `2026-11-21T1${5 - index}:00:00Z`,
        }),
      ),
    );

    renderBanner();

    expect(await screen.findByRole("heading", { name: "5 cost alerts" })).toBeVisible();
    expect(screen.getAllByRole("listitem")).toHaveLength(3);
    expect(screen.getByText("and 2 older alerts")).toBeVisible();
    const panel = screen.getByRole("heading", { name: "5 cost alerts" }).closest("section");
    expect(panel).not.toBeNull();
    // Dismiss all counts the hidden alerts too.
    expect(
      within(panel as HTMLElement).getByRole("button", { name: "Dismiss all 5 alerts" }),
    ).toBeVisible();
  });
});

describe("cost alert banner actions", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    authState.user = memberWithServerClaim();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  it("opens the shared confirmation from the cost-stop button and never runs the action", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alertFor(serverA)]);
    const execute = vi.spyOn(hostingService, "executeAction").mockResolvedValue({} as never);
    const request = vi.fn();
    renderBanner(serverA, request);

    const button = await screen.findByRole("button", { name: "Shut down and stop billing" });
    await user.click(button);

    expect(request).toHaveBeenCalledOnce();
    expect(request).toHaveBeenCalledWith(
      expect.objectContaining({
        serverId: serverA.primaryKey,
        actionId: "stop",
        source: "alert-banner",
      }),
    );
    expect(execute).not.toHaveBeenCalled();
  });

  it("keeps the cost-stop button and Dismiss all together in one footer after the list, cost-stop first", async () => {
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue(
      [5, 10].map((amount) => alertFor(serverA, { thresholdAmount: amount })),
    );
    const { container } = renderBanner();

    await screen.findByRole("heading", { name: "2 cost alerts" });

    expect(container.querySelector(".state-panel__action")).toBeNull();
    const footers = container.querySelectorAll(".cost-alert-banner__footer");
    expect(footers).toHaveLength(1);
    const buttons = within(footers[0] as HTMLElement).getAllByRole("button");
    expect(buttons.map((button) => button.textContent)).toEqual([
      "Shut down and stop billing",
      "Dismiss all alerts",
    ]);
    const list = container.querySelector(".cost-alert-banner__list") as HTMLElement;
    expect(
      Boolean(list.compareDocumentPosition(footers[0]!) & Node.DOCUMENT_POSITION_FOLLOWING),
    ).toBe(true);
    // The row buttons are inside the rows, not in the footer.
    expect(within(list).getAllByRole("button", { name: /^Dismiss alert at/ })).toHaveLength(2);
  });

  it("marks only the rows after the first as divided and gives every Dismiss button an aria-hidden icon", async () => {
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue(
      [5, 10, 15].map((amount, index) =>
        alertFor(serverA, { thresholdAmount: amount, createdAt: `2026-11-21T1${index}:00:00Z` }),
      ),
    );
    renderBanner();

    await screen.findByRole("heading", { name: "3 cost alerts" });

    const rows = screen.getAllByRole("listitem");
    expect(rows.map((row) => row.classList.contains("cost-alert-banner__row--divided"))).toEqual([
      false,
      true,
      true,
    ]);
    for (const row of rows) {
      const button = within(row).getByRole("button", { name: /^Dismiss alert at/ });
      expect(button.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
      expect(button).toHaveTextContent("Dismiss alert");
    }
  });

  it("shows only Dismiss all in the footer when the server has no cost-stop action", async () => {
    const noStop = fleetServer({
      index: 1,
      title: "Ops Night",
      status: "Running",
      actions: [fleetAction("collect-logs")],
    });
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue(
      [5, 10].map((amount) => alertFor(noStop, { thresholdAmount: amount })),
    );
    const { container, unmount } = renderBanner(noStop);

    await screen.findByRole("heading", { name: "2 cost alerts" });
    const footer = container.querySelector(".cost-alert-banner__footer") as HTMLElement;
    expect(within(footer).getAllByRole("button")).toHaveLength(1);
    expect(within(footer).getByRole("button", { name: "Dismiss all 2 alerts" })).toBeVisible();
    unmount();

    // One alert and nothing to stop leaves nothing for a footer.
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alertFor(noStop)]);
    const single = renderBanner(noStop);
    await screen.findByRole("heading", { name: "Cost alert" });
    expect(single.container.querySelector(".cost-alert-banner__footer")).toBeNull();
  });

  it("renders the cost-stop button disabled with its reason when the action is unavailable", async () => {
    const user = userEvent.setup();
    const blocked = fleetServer({
      index: 1,
      title: "Ops Night",
      status: "Running",
      actions: [fleetAction("stop", { enabled: false, unavailableReason: "A job is running." })],
    });
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alertFor(blocked)]);
    const request = vi.fn();
    renderBanner(blocked, request);

    const button = await screen.findByRole("button", { name: "Shut down and stop billing" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).toHaveAccessibleDescription(/A job is running\./);
    await user.click(button);
    expect(request).not.toHaveBeenCalled();
  });

  it("renders no cost-stop button when the server has no cost-stop action", async () => {
    const noStop = fleetServer({
      index: 1,
      title: "Ops Night",
      status: "Running",
      actions: [fleetAction("collect-logs")],
    });
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alertFor(noStop)]);
    renderBanner(noStop);

    await screen.findByRole("heading", { name: "Cost alert" });
    expect(screen.queryByRole("button", { name: /Shut down/ })).toBeNull();
  });

  it("says the session ended only when cost has stopped and keeps the alert", async () => {
    const stopped = fleetServer({ index: 1, title: "Ops Night", status: "Stopped" });
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alertFor(stopped)]);
    const { unmount } = renderBanner(stopped);

    expect(
      await screen.findByText("This session has ended and billing has stopped."),
    ).toBeVisible();
    expect(screen.getByRole("heading", { name: "Cost alert" })).toBeVisible();
    expect(screen.queryByRole("button", { name: /Shut down/ })).toBeNull();
    unmount();

    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alertFor(serverA)]);
    renderBanner(serverA);
    await screen.findByRole("heading", { name: "Cost alert" });
    expect(screen.queryByText("This session has ended and billing has stopped.")).toBeNull();
  });

  it("dismisses every alert of the server in one request and focuses the workspace heading", async () => {
    const user = userEvent.setup();
    const alerts = [5, 10, 15, 20].map((amount, index) =>
      alertFor(serverA, { thresholdAmount: amount, createdAt: `2026-11-21T1${index}:00:00Z` }),
    );
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue(alerts);
    const dismissAll = vi.spyOn(hostingService, "dismissServerCostAlerts").mockResolvedValue();
    const dismissOne = vi.spyOn(hostingService, "dismissCostAlert").mockResolvedValue();
    renderBanner();

    await user.click(await screen.findByRole("button", { name: "Dismiss all 4 alerts" }));

    expect(dismissAll).toHaveBeenCalledOnce();
    const [serverId, sentIds] = dismissAll.mock.calls[0] ?? [];
    expect(serverId).toBe(serverA.primaryKey);
    expect([...(sentIds ?? [])].sort()).toEqual(alerts.map((alert) => alert.id).sort());
    expect(dismissOne).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.queryByRole("heading", { name: /cost alerts?/ })).toBeNull());
    await waitFor(() => expect(screen.getByRole("heading", { name: "Ops Night" })).toHaveFocus());
  });

  it("sends only the ids of the shown alerts of that server when dismissing all (D-17)", async () => {
    const user = userEvent.setup();
    const shown = [5, 10].map((amount) => alertFor(serverA, { thresholdAmount: amount }));
    const otherServer = alertFor(serverB, { thresholdAmount: 5 });
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([...shown, otherServer]);
    const dismissAll = vi.spyOn(hostingService, "dismissServerCostAlerts").mockResolvedValue();
    renderBanner();

    await user.click(await screen.findByRole("button", { name: "Dismiss all 2 alerts" }));

    expect(dismissAll).toHaveBeenCalledOnce();
    const [serverId, sentIds] = dismissAll.mock.calls[0] ?? [];
    expect(serverId).toBe(serverA.primaryKey);
    expect([...(sentIds ?? [])].sort()).toEqual(shown.map((alert) => alert.id).sort());
    expect(sentIds).not.toContain(otherServer.id);
  });

  it("moves focus to the next row, then the cost-stop button, then the workspace heading", async () => {
    const user = userEvent.setup();
    const newest = alertFor(serverA, { thresholdAmount: 10, createdAt: "2026-11-21T15:00:00Z" });
    const oldest = alertFor(serverA, { thresholdAmount: 5, createdAt: "2026-11-21T14:00:00Z" });
    const middle = alertFor(serverA, { thresholdAmount: 7, createdAt: "2026-11-21T14:30:00Z" });
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([newest, middle, oldest]);
    vi.spyOn(hostingService, "dismissCostAlert").mockResolvedValue();
    renderBanner();

    // The first row is dismissed: focus goes to the Dismiss button of the row that followed it.
    await user.click(await screen.findByRole("button", { name: `Dismiss alert at ${money(10)}` }));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: `Dismiss alert at ${money(7)}` })).toHaveFocus(),
    );
    expect(await screen.findByText(`Cost alert dismissed: Ops Night, ${money(10)}.`)).toBeVisible();

    // The last row is dismissed while another remains: focus goes to the cost-stop button.
    await user.click(screen.getByRole("button", { name: `Dismiss alert at ${money(5)}` }));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Shut down and stop billing" })).toHaveFocus(),
    );

    // The only alert is dismissed: the banner is gone and focus goes to the workspace heading.
    await user.click(screen.getByRole("button", { name: `Dismiss alert at ${money(7)}` }));
    await waitFor(() => expect(screen.queryByRole("heading", { name: /cost alert/ })).toBeNull());
    await waitFor(() => expect(screen.getByRole("heading", { name: "Ops Night" })).toHaveFocus());
  });

  it("confirms the dismissal of the last alert with a toast that outlives the banner (D-17)", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alertFor(serverA)]);
    vi.spyOn(hostingService, "dismissCostAlert").mockResolvedValue();
    renderBanner();

    await user.click(await screen.findByRole("button", { name: `Dismiss alert at ${money(5)}` }));

    await waitFor(() => expect(screen.queryByRole("heading", { name: "Cost alert" })).toBeNull());
    const toast = await screen.findByText(`Cost alert dismissed: Ops Night, ${money(5)}.`);
    expect(toast).toBeVisible();
    const toastElement = toast.closest<HTMLElement>(".app-toast");
    expect(toastElement).not.toBeNull();
    expect(toastPoliteness(toastElement as HTMLElement)).toBe("polite");
    expect(document.querySelectorAll(".app-toast")).toHaveLength(1);
    await waitFor(() => expect(screen.getByRole("heading", { name: "Ops Night" })).toHaveFocus());
    // The toast is the one announcement: the banner renders no screen-reader-only line of its own.
    expect(document.querySelector(".sr-only")).toBeNull();
  });

  it("toasts the dismissed alert of two and moves focus to the next row", async () => {
    const user = userEvent.setup();
    const newest = alertFor(serverA, { thresholdAmount: 10, createdAt: "2026-11-21T15:00:00Z" });
    const oldest = alertFor(serverA, { thresholdAmount: 5, createdAt: "2026-11-21T14:00:00Z" });
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([newest, oldest]);
    vi.spyOn(hostingService, "dismissCostAlert").mockResolvedValue();
    renderBanner();

    await user.click(await screen.findByRole("button", { name: `Dismiss alert at ${money(10)}` }));

    expect(await screen.findByText(`Cost alert dismissed: Ops Night, ${money(10)}.`)).toBeVisible();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: `Dismiss alert at ${money(5)}` })).toHaveFocus(),
    );
    expect(screen.getByRole("heading", { name: "Cost alert" })).toBeVisible();
  });

  it("toasts the number of alerts that Dismiss all removed", async () => {
    const user = userEvent.setup();
    const alerts = [5, 10, 15].map((amount, index) =>
      alertFor(serverA, { thresholdAmount: amount, createdAt: `2026-11-21T1${index}:00:00Z` }),
    );
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue(alerts);
    vi.spyOn(hostingService, "dismissServerCostAlerts").mockResolvedValue();
    renderBanner();

    await user.click(await screen.findByRole("button", { name: "Dismiss all 3 alerts" }));

    expect(await screen.findByText("3 cost alerts for Ops Night dismissed.")).toBeVisible();
    await waitFor(() => expect(screen.queryByRole("heading", { name: /cost alerts?/ })).toBeNull());
    expect(screen.getByText("3 cost alerts for Ops Night dismissed.")).toBeVisible();
    await waitFor(() => expect(screen.getByRole("heading", { name: "Ops Night" })).toHaveFocus());
  });

  it("names the server in the singular and plural dismiss-all toast in German", async () => {
    await i18n.changeLanguage("de");
    const user = userEvent.setup();
    const only = alertFor(serverA);
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([only]);
    vi.spyOn(hostingService, "dismissServerCostAlerts").mockResolvedValue();
    // One alert never offers Dismiss all in the banner, so the hook is driven by a probe button.
    function DismissAllProbe() {
      const { dismissAll } = useDismissCostAlerts();
      return (
        <button type="button" onClick={() => void dismissAll(serverA.primaryKey)}>
          probe
        </button>
      );
    }
    renderBanner(serverA, vi.fn(), <DismissAllProbe />);
    await screen.findByRole("heading", { name: "Kostenwarnung" });

    await user.click(screen.getByRole("button", { name: "probe" }));

    expect(await screen.findByText("Kostenwarnung für Ops Night geschlossen.")).toBeVisible();
    expect(i18n.t("Server_Alerts_DismissedAll_Toast", { count: 2, name: "Ops Night" })).toBe(
      "2 Kostenwarnungen für Ops Night geschlossen.",
    );
  });

  it("keeps the row and shows the alert line when a dismissal fails", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alertFor(serverA)]);
    vi.spyOn(hostingService, "dismissCostAlert").mockRejectedValue(new Error("offline"));
    renderBanner();

    await user.click(await screen.findByRole("button", { name: /^Dismiss alert at/ }));

    const line = await screen.findByRole("alert");
    expect(line).toHaveTextContent("The alert could not be dismissed. Try again.");
    expect(line.closest("section")).toBe(
      screen.getByRole("heading", { name: "Cost alert" }).closest("section"),
    );
    expect(screen.getAllByRole("listitem")).toHaveLength(1);
    expect(document.querySelector(".app-toast")).toBeNull();
    expect(screen.queryByText(/^Cost alert dismissed/)).toBeNull();
  });

  it("keeps the German banner compact: title and first row come before the action, text wraps", async () => {
    await i18n.changeLanguage("de");
    const longName =
      "Ein sehr langer Servername der auf einem schmalen Telefon umbrechen muss ".repeat(3);
    const long = { primaryKey: serverA.primaryKey, title: longName };
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue(
      [5, 10, 15].map((amount, index) =>
        alertFor(long, { thresholdAmount: amount, createdAt: `2026-11-21T1${5 - index}:00:00Z` }),
      ),
    );
    const { container } = renderBanner(serverA);

    const title = await screen.findByRole("heading", { name: "3 Kostenwarnungen" });
    const rows = screen.getAllByRole("listitem");
    expect(rows).toHaveLength(3);
    const action = screen.getByRole("button", { name: "Herunterfahren und Abrechnung beenden" });
    expect(action.closest(".cost-alert-banner__footer")).not.toBeNull();
    const follows = (earlier: Element, later: Element) =>
      Boolean(earlier.compareDocumentPosition(later) & Node.DOCUMENT_POSITION_FOLLOWING);
    expect(follows(title, rows[0]!)).toBe(true);
    expect(follows(rows[0]!, action)).toBe(true);
    // The long server name is not repeated in the rows, so nothing in the banner can overflow because of it.
    expect(container.querySelectorAll("bdi")).toHaveLength(0);
    expect(screen.getAllByText(/ erreicht$/)).toHaveLength(3);
    expect(screen.getAllByRole("button", { name: /^Warnung bei .* schließen$/ })).toHaveLength(3);

    // jsdom has no layout, so the wrapping contract is asserted on the stylesheet that implements it.
    const css = readFileSync(
      resolve(import.meta.dirname, "..", "..", "src", "components", "server", "cost-alerts.css"),
      "utf8",
    );
    expect(css).toContain("overflow-wrap: anywhere");
    expect(css).not.toContain("text-overflow: ellipsis");
    expect(css).toMatch(
      /@media \(max-width: 520px\)[\s\S]*cost-alert-banner__footer \.button[\s\S]*width: 100%/,
    );
    expect(css).toContain("cost-alert-banner__row--divided");
  });
});

describe("cost alert banner in the workspace", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    authState.user = memberWithServerClaim();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  it("is the first notice, above the pending-start notice", async () => {
    const target = fleetServer({
      index: 1,
      title: "Ops Night",
      status: "Running",
      pendingStartAreas: ["Configuration"],
    });
    vi.spyOn(hostingService, "server").mockResolvedValue(target);
    vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
    vi.spyOn(hostingService, "operations").mockResolvedValue([]);
    vi.spyOn(hostingService, "accruedCost").mockResolvedValue({
      gameServerId: target.primaryKey,
      session: null,
    });
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([alertFor(target)]);

    renderWithProviders(<ServerWorkspacePage />, {
      path: "/command/servers/:serverId",
      route: `/command/servers/${target.primaryKey}`,
    });

    const banner = (await screen.findByRole("heading", { name: "Cost alert" })).closest("section");
    const pending = (
      await screen.findByText(
        "Saved changes are waiting for the next start. Ops Night is still running with the previous setup.",
      )
    ).closest("section");
    expect(banner).not.toBeNull();
    expect(pending).not.toBeNull();
    expect(
      (banner as HTMLElement).compareDocumentPosition(pending as HTMLElement) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });
});

describe("cost alert banner and automatic shutdown alerts", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    authState.user = memberWithServerClaim();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  function autoStopFor(server: { primaryKey: string; title: string }): CostAlert {
    return autoStopAlert({
      id: `auto-${server.primaryKey}`,
      serverId: server.primaryKey,
      serverName: server.title,
    });
  }

  it("never renders an AutoStop alert as a threshold row", async () => {
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([
      autoStopFor(serverA),
      alertFor(serverA, { thresholdAmount: 5 }),
    ]);
    renderBanner();

    expect(await screen.findByRole("heading", { name: "Cost alert" })).toBeVisible();
    expect(screen.getAllByRole("button", { name: /^Dismiss alert at/ })).toHaveLength(1);
    expect(screen.queryByText(/Automatic shutdown/)).toBeNull();
  });

  it("renders nothing when the only alert of the server is an AutoStop alert", async () => {
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([autoStopFor(serverA)]);
    const { container } = renderBanner();

    await waitFor(() => expect(hostingService.costAlerts).toHaveBeenCalled());
    expect(container.querySelector(".state-panel")).toBeNull();
  });

  it("leaves an AutoStop record out of Dismiss all", async () => {
    const user = userEvent.setup();
    const thresholds = [5, 10].map((amount) => alertFor(serverA, { thresholdAmount: amount }));
    const autoStop = autoStopFor(serverA);
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([autoStop, ...thresholds]);
    const dismissAll = vi.spyOn(hostingService, "dismissServerCostAlerts").mockResolvedValue();
    const { queryClient } = renderBanner();

    await user.click(await screen.findByRole("button", { name: "Dismiss all 2 alerts" }));

    const [, sentIds] = dismissAll.mock.calls[0] ?? [];
    expect([...(sentIds ?? [])].sort()).toEqual(thresholds.map((alert) => alert.id).sort());
    expect(
      queryClient.getQueryData<CostAlert[]>(hostingQueryKeys.costAlerts)?.map((alert) => alert.id),
    ).toEqual([autoStop.id]);
  });

  it("keeps a dismissed warning record visible to another recipient of a group cost owner", async () => {
    const user = userEvent.setup();
    const warning = autoStopFor(serverA);
    const other = new QueryClient();
    other.setQueryData(hostingQueryKeys.costAlerts, [warning]);
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([warning]);
    vi.spyOn(hostingService, "dismissCostAlert").mockResolvedValue();
    function DismissProbe() {
      const { dismiss } = useDismissCostAlerts();
      return (
        <button type="button" onClick={() => void dismiss(warning)}>
          probe
        </button>
      );
    }
    const { queryClient } = renderBanner(serverA, vi.fn(), <DismissProbe />);
    await waitFor(() =>
      expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([warning]),
    );

    await user.click(screen.getByRole("button", { name: "probe" }));

    await waitFor(() => expect(queryClient.getQueryData(hostingQueryKeys.costAlerts)).toEqual([]));
    expect(hostingService.dismissCostAlert).toHaveBeenCalledExactlyOnceWith(warning.id);
    expect(other.getQueryData(hostingQueryKeys.costAlerts)).toEqual([warning]);
    // The cost dismissal toast words a threshold; an AutoStop record leaves without it.
    expect(document.querySelector(".app-toast")).toBeNull();
  });
});
