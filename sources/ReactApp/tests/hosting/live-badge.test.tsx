import { act, render, screen } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import { beforeEach } from "vitest";
import type { UpdateStreamStatus } from "@/app/updateStream";
import i18n, { i18nReady } from "@/app/i18n";
import { LiveBadge } from "@/components/server/LiveBadge";
import {
  FleetLiveUpdatesContext,
  type FleetLiveUpdatesValue,
} from "@/features/hosting/useFleetLiveUpdates";

/* The live badge (ACT-02, D-11): honest freshness from the socket state and the age of the data. */

/** The fixed "now" of every test. */
const nowIso = "2026-08-13T12:10:00Z";
const now = Date.parse(nowIso);

/** What the test controls about the socket. */
interface SocketState {
  status: UpdateStreamStatus;
  disconnectEpoch: number;
}

/** What the test controls about the badge. */
interface BadgeState {
  dataUpdatedAt?: number;
  hasActiveJob?: boolean;
}

/** Renders the badge under a controlled socket state; `update` swaps the socket state and badge props. */
function renderBadge(socket: SocketState, badge: BadgeState = {}) {
  const reconnectNow = vi.fn();
  const onRefresh = vi.fn();
  const element = (nextSocket: SocketState, nextBadge: BadgeState) => {
    const value: FleetLiveUpdatesValue = {
      ...nextSocket,
      reconnectNow,
      subscribe: () => () => undefined,
      subscribeCostAlerts: () => () => undefined,
      markCostAlertsDismissed: () => undefined,
    };
    return (
      <I18nextProvider i18n={i18n}>
        <FleetLiveUpdatesContext.Provider value={value}>
          <LiveBadge
            dataUpdatedAt={nextBadge.dataUpdatedAt ?? now}
            hasActiveJob={nextBadge.hasActiveJob ?? false}
            onRefresh={onRefresh}
          />
        </FleetLiveUpdatesContext.Provider>
      </I18nextProvider>
    );
  };
  const view = render(element(socket, badge));
  return {
    ...view,
    reconnectNow,
    onRefresh,
    update: (nextSocket: SocketState, nextBadge: BadgeState = badge) =>
      view.rerender(element(nextSocket, nextBadge)),
  };
}

/** Advances the fake clock inside `act`. */
function advance(milliseconds: number): void {
  act(() => {
    vi.advanceTimersByTime(milliseconds);
  });
}

describe("LiveBadge", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(nowIso));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows Connecting while the socket has never opened", () => {
    renderBadge({ status: "connecting", disconnectEpoch: 0 });

    expect(screen.getByRole("status")).toHaveTextContent("Connecting");
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("shows Live for an open socket without an active job", () => {
    renderBadge({ status: "open", disconnectEpoch: 0 });

    expect(screen.getByRole("status")).toHaveTextContent("Live");
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("shows Live for an open socket with an active job and fresh data", () => {
    renderBadge(
      { status: "open", disconnectEpoch: 0 },
      { hasActiveJob: true, dataUpdatedAt: now - 30_000 },
    );

    expect(screen.getByRole("status")).toHaveTextContent("Live");
  });

  it("shows Not up to date with the old-data reason and Refresh activity for a running job", () => {
    const { onRefresh, reconnectNow } = renderBadge(
      { status: "open", disconnectEpoch: 0 },
      { hasActiveJob: true, dataUpdatedAt: now - 120_000 },
    );

    expect(screen.getByRole("status")).toHaveTextContent("Not up to date");
    expect(
      screen.getByText("No update for over a minute while a change is running."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reconnect now" })).not.toBeInTheDocument();

    act(() => screen.getByRole("button", { name: "Refresh activity" }).click());

    expect(onRefresh).toHaveBeenCalledTimes(1);
    expect(reconnectNow).not.toHaveBeenCalled();
  });

  it("keeps the previous badge for 3 seconds of disconnection and then shows the stale state", () => {
    const { reconnectNow, onRefresh } = renderBadge({ status: "reconnecting", disconnectEpoch: 1 });

    expect(screen.getByRole("status")).toHaveTextContent("Live");
    advance(2999);
    expect(screen.getByRole("status")).toHaveTextContent("Live");
    expect(screen.queryByRole("button", { name: "Reconnect now" })).not.toBeInTheDocument();

    advance(1);

    expect(screen.getByRole("status")).toHaveTextContent("Not up to date");
    expect(
      screen.getByText("Live updates are paused. Reconnecting automatically."),
    ).toBeInTheDocument();
    act(() => screen.getByRole("button", { name: "Reconnect now" }).click());
    expect(reconnectNow).toHaveBeenCalledTimes(1);
    expect(onRefresh).not.toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: "Refresh activity" })).not.toBeInTheDocument();
  });

  it("treats an unavailable socket the same way after the delay", () => {
    renderBadge({ status: "unavailable", disconnectEpoch: 1 });

    advance(3000);

    expect(screen.getByRole("status")).toHaveTextContent("Not up to date");
    expect(screen.getByRole("button", { name: "Reconnect now" })).toBeInTheDocument();
  });

  it("treats connecting again after a disconnection as a disconnected, delayed stale state", () => {
    renderBadge({ status: "connecting", disconnectEpoch: 2 });

    expect(screen.getByRole("status")).toHaveTextContent("Live");
    advance(3000);

    expect(screen.getByRole("status")).toHaveTextContent("Not up to date");
  });

  it("never shows the stale state when the connection returns within 3 seconds", () => {
    const { update } = renderBadge({ status: "reconnecting", disconnectEpoch: 1 });
    advance(2000);

    update({ status: "open", disconnectEpoch: 1 });
    advance(5000);

    expect(screen.getByRole("status")).toHaveTextContent("Live");
    expect(screen.queryByRole("button", { name: "Reconnect now" })).not.toBeInTheDocument();
  });

  it("waits for the full delay again in a new disconnection episode", () => {
    const { update } = renderBadge({ status: "reconnecting", disconnectEpoch: 1 });
    advance(3000);
    expect(screen.getByRole("status")).toHaveTextContent("Not up to date");

    update({ status: "open", disconnectEpoch: 1 });
    expect(screen.getByRole("status")).toHaveTextContent("Live");

    update({ status: "reconnecting", disconnectEpoch: 2 });
    expect(screen.getByRole("status")).toHaveTextContent("Live");
    advance(2999);
    expect(screen.getByRole("status")).toHaveTextContent("Live");
    advance(1);
    expect(screen.getByRole("status")).toHaveTextContent("Not up to date");
  });

  it("keeps the updated time outside the polite status region", () => {
    renderBadge({ status: "open", disconnectEpoch: 0 }, { dataUpdatedAt: now - 120_000 });

    const status = screen.getByRole("status");
    expect(status.textContent).toBe("Live");
    const time = screen.getByText("2 minutes ago");
    expect(time.tagName).toBe("TIME");
    expect(time).toHaveAttribute("dateTime", new Date(now - 120_000).toISOString());
    expect(status).not.toContainElement(time);
    expect(time.parentElement).toHaveTextContent("Updated 2 minutes ago");
  });

  it("omits the updated time while no data has arrived", () => {
    const { container } = renderBadge({ status: "open", disconnectEpoch: 0 }, { dataUpdatedAt: 0 });

    expect(container.querySelector("time")).toBeNull();
    expect(screen.queryByText(/Updated/)).not.toBeInTheDocument();
  });

  it("puts nothing but the state word inside the status region in the stale state", () => {
    renderBadge({ status: "reconnecting", disconnectEpoch: 1 });
    advance(3000);

    expect(screen.getByRole("status").textContent).toBe("Not up to date");
  });
});
