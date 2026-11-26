import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { QueryClient } from "@tanstack/react-query";
import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerMetricsPanel } from "@/components/server/ServerMetricsPanel";
import { metricTiles, reasonKey } from "@/features/hosting/metricsModel";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServerMetrics, ServerMetricsReading } from "@/features/hosting/types";
import { FleetLiveUpdatesContext } from "@/features/hosting/useFleetLiveUpdates";
import { fleetServer } from "../helpers/fleetFixtures";
import { aggregate, historyPoint, metricsHistory } from "../helpers/metricsHistoryFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/*
 * D-07, D-09, MET-01, MET-02, MET-03 and MET-05: the tiles show the control plane values as sent, an unavailable
 * reading shows the word and a plain reason and never a number, and the panel handles loading and errors.
 */

const server = fleetServer({ status: "Running" });

/** A reading with a value for every metric; the overrides replace single fields. */
function reading(overrides: Partial<GameServerMetrics> = {}): GameServerMetrics {
  return {
    gameServerId: server.primaryKey,
    asOf: "2026-11-21T14:05:00Z",
    staleAfterSeconds: 45,
    runningSince: "2026-11-21T10:00:00Z",
    runningSinceUnavailableReason: null,
    cpuPercent: 37.4,
    cpuUnavailableReason: null,
    memoryUsedBytes: 2_684_354_560,
    memoryTotalBytes: 7_516_192_768,
    memoryUnavailableReason: null,
    playerCount: 4,
    playerCapacity: 16,
    playersUnavailableReason: null,
    ...overrides,
  };
}

/** A reading where every metric is unavailable for the same reason code. */
function unavailable(code: string | null): GameServerMetrics {
  return reading({
    asOf: null,
    runningSince: null,
    runningSinceUnavailableReason: code,
    cpuPercent: null,
    cpuUnavailableReason: code,
    memoryUsedBytes: null,
    memoryTotalBytes: null,
    memoryUnavailableReason: code,
    playerCount: null,
    playerCapacity: null,
    playersUnavailableReason: code,
  });
}

function renderPanel() {
  return renderWithProviders(<ServerMetricsPanel server={server} />);
}

/** Finds one tile of the rendered panel by its id. */
function tile(container: HTMLElement, id: string): HTMLElement {
  const found = container.querySelector<HTMLElement>(`[data-tile="${id}"]`);
  if (!found) throw new Error(`expected the ${id} tile`);
  return found;
}

describe("server metrics panel", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "serverMetricsHistory").mockResolvedValue(metricsHistory([]));
  });

  afterEach(async () => {
    vi.useRealTimers();
    await i18n.changeLanguage("en");
  });

  it("shows players including zero, CPU as a percent and memory in GiB of the total", async () => {
    vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading({ playerCount: 0 }));

    const { container } = renderPanel();

    expect(await screen.findByText("37%")).toBeVisible();
    const players = tile(container, "players");
    expect(within(players).getByText("0")).toBeVisible();
    expect(within(players).getByText("of 16 slots")).toBeVisible();
    const memory = tile(container, "memory");
    expect(within(memory).getByText("2.5 GiB")).toBeVisible();
    expect(within(memory).getByText("of 7 GiB")).toBeVisible();
    expect(within(memory).queryByText("Unavailable")).toBeNull();
    const since = tile(container, "uptime").querySelector("time");
    expect(since).toHaveAttribute("datetime", "2026-11-21T10:00:00Z");
  });

  it("uses the singular capacity sentence and omits the detail without a capacity", async () => {
    const load = vi
      .spyOn(hostingService, "serverMetrics")
      .mockResolvedValue(reading({ playerCapacity: 1, memoryTotalBytes: null }));

    const { container } = renderPanel();

    expect(await screen.findByText("of 1 slot")).toBeVisible();
    expect(within(tile(container, "memory")).queryByText(/^of /)).toBeNull();
    expect(load).toHaveBeenCalledTimes(1);
  });

  it("formats numbers with German separators and sentences in German", async () => {
    await i18n.changeLanguage("de");
    vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());

    const { container } = renderPanel();

    expect(await screen.findByText("2,5 GiB")).toBeVisible();
    expect(within(tile(container, "memory")).getByText("von 7 GiB")).toBeVisible();
    expect(within(tile(container, "players")).getByText("von 16 Plätzen")).toBeVisible();
    expect(within(tile(container, "cpu")).getByText(/^37\s%$/)).toBeVisible();
    expect(screen.getByText("Läuft seit")).toBeVisible();
  });

  it.each([
    ["server-not-running", "The game is not running, so there is nothing to measure."],
    ["worker-offline", "The worker of this server is not connected, so no readings arrive."],
    ["stale", "The last reading is too old to show."],
    ["game-does-not-report", "This game does not report player counts."],
    ["query-failed", "The reading failed. It will be tried again."],
    ["measuring", "Measuring. The first value appears shortly."],
    ["unsupported-platform", "This machine does not provide this reading."],
    ["a-code-from-a-newer-api", "No reading is available."],
    [null, "No reading is available."],
  ])("shows Unavailable and the plain reason for the code %s and no number", async (code, text) => {
    vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(unavailable(code));

    const { container } = renderPanel();

    expect(await screen.findAllByText("Unavailable")).toHaveLength(4);
    for (const id of ["uptime", "players", "cpu", "memory"]) {
      const element = tile(container, id);
      expect(within(element).getByText(text)).toBeVisible();
      expect(within(element).getByTestId("fluent-icon")).toBeInTheDocument();
      expect(element.querySelector("dd")?.textContent ?? "").not.toMatch(/\d/);
    }
    expect(container.textContent).not.toContain(String(code));
  });

  it("shows values and reasons side by side when only some readings are available", async () => {
    vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(
      reading({
        playerCount: null,
        playerCapacity: null,
        playersUnavailableReason: "game-does-not-report",
        cpuPercent: null,
        cpuUnavailableReason: "measuring",
      }),
    );

    const { container } = renderPanel();

    await screen.findByText("2.5 GiB");
    expect(
      await within(tile(container, "players")).findByText(
        "This game does not report player counts.",
      ),
    ).toBeVisible();
    expect(
      within(tile(container, "cpu")).getByText("Measuring. The first value appears shortly."),
    ).toBeVisible();
    expect(within(tile(container, "memory")).getByText("2.5 GiB")).toBeVisible();
    expect(within(tile(container, "uptime")).queryByText("Unavailable")).toBeNull();
    expect(screen.getAllByText("Unavailable")).toHaveLength(2);
  });

  it("lets a server reason win over a value and never invents a number for a missing value", () => {
    const withBoth = {
      ...reading({ cpuUnavailableReason: "stale" }),
      receivedAtMs: 0,
    };
    const tiles = metricTiles(withBoth, 0, "en-US");

    expect(tiles.map((entry) => entry.id)).toEqual(["uptime", "players", "cpu", "memory"]);
    expect(tiles[2]?.value).toEqual({
      kind: "unavailable",
      reasonKey: "Server_Metrics_Reason_Stale",
      lastReading: "2026-11-21T14:05:00Z",
    });
    expect(reasonKey("not-a-known-code")).toBe("Server_Metrics_Reason_Unknown");
    expect(reasonKey(undefined)).toBe("Server_Metrics_Reason_Unknown");
  });

  it("shows the three history charts below the tiles in the order CPU load, Memory used, Players", async () => {
    const at = Date.now();
    vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());
    vi.spyOn(hostingService, "serverMetricsHistory").mockResolvedValue(
      metricsHistory([
        historyPoint(at - 45_000, { cpu: aggregate(40, 30, 80) }),
        historyPoint(at - 30_000, { cpu: aggregate(42, 32, 52) }),
        historyPoint(at - 15_000),
      ]),
    );

    const { container } = renderPanel();

    expect(await screen.findByRole("img", { name: "CPU load, last hour" })).toBeVisible();
    expect(screen.getByRole("img", { name: "Memory used, last hour" })).toBeVisible();
    expect(screen.getByRole("img", { name: "Players, last hour" })).toBeVisible();
    const titles = [...container.querySelectorAll("h3")].map((heading) => heading.textContent);
    expect(titles).toEqual(["CPU load", "Memory used", "Players"]);
    const tiles = container.querySelector(".server-metrics__tiles");
    const views = container.querySelector(".server-metrics__views");
    expect(tiles?.compareDocumentPosition(views as Node)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(hostingService.serverMetricsHistory).toHaveBeenCalledWith(
      server.primaryKey,
      "1h",
      expect.anything(),
    );
  });

  it("shows the game-does-not-report sentence in an empty players chart", async () => {
    vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(
      reading({
        playerCount: null,
        playerCapacity: null,
        playersUnavailableReason: "game-does-not-report",
      }),
    );

    const { container } = renderPanel();

    await screen.findByText("37%");
    const chart = (await screen.findByRole("heading", { level: 3, name: "Players" })).closest(
      "section",
    );
    expect(chart).not.toBeNull();
    expect(
      within(chart as HTMLElement).getByText("This game does not report player counts."),
    ).toBeVisible();
    expect(container.querySelectorAll(".metric-chart")).toHaveLength(3);
  });

  it("shows one loading panel while the first load runs", () => {
    vi.spyOn(hostingService, "serverMetrics").mockReturnValue(new Promise(() => {}));

    const { container } = renderPanel();

    expect(screen.getByRole("heading", { name: "Server metrics" })).toBeVisible();
    expect(container.querySelector('p.sr-only[role="status"]')).toBeEmptyDOMElement();
    expect(screen.getByText("Loading metrics...")).toBeVisible();
    expect(container.querySelector(".metric-tile")).toBeNull();
    expect(screen.queryByText("Unavailable")).toBeNull();
  });

  it("shows the panel error with Try again when the first load fails and refetches on retry", async () => {
    const user = userEvent.setup();
    const load = vi
      .spyOn(hostingService, "serverMetrics")
      .mockRejectedValueOnce(new Error("socket hang up"))
      .mockResolvedValue(reading());

    renderPanel();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("The metrics could not be loaded.");
    expect(alert).not.toHaveTextContent("socket hang up");
    await user.click(within(alert).getByRole("button", { name: "Try again" }));

    expect(await screen.findByText("37%")).toBeVisible();
    expect(screen.queryByRole("alert")).toBeNull();
    expect(load).toHaveBeenCalledTimes(2);
  });

  it("keeps the tiles unchanged when a background refetch fails while data is held", async () => {
    const load = vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());
    const view = renderPanel();
    expect(await screen.findByText("37%")).toBeVisible();

    load.mockRejectedValue(new Error("boom"));
    await view.queryClient.invalidateQueries({
      queryKey: hostingQueryKeys.metrics(server.primaryKey),
      exact: true,
    });

    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    expect(screen.getByText("37%")).toBeVisible();
    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.queryByText("Loading metrics...")).toBeNull();
  });

  it("keeps a cached reading that is newer than the one a refetch returns", async () => {
    const load = vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());
    const view = renderPanel();
    expect(await screen.findByText("37%")).toBeVisible();

    load.mockResolvedValue(reading({ cpuPercent: 91, asOf: "2026-11-21T14:00:00Z" }));
    await view.queryClient.invalidateQueries({
      queryKey: hostingQueryKeys.metrics(server.primaryKey),
      exact: true,
    });

    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    expect(screen.getByText("37%")).toBeVisible();
    expect(screen.queryByText("91%")).toBeNull();
  });

  it("shows the not-running reason on every tile after a stop, never the cached values", async () => {
    const load = vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());
    const view = renderPanel();
    expect(await screen.findByText("37%")).toBeVisible();

    load.mockResolvedValue(unavailable("server-not-running"));
    await view.queryClient.invalidateQueries({
      queryKey: hostingQueryKeys.metrics(server.primaryKey),
      exact: true,
    });

    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.getAllByText("Unavailable")).toHaveLength(4));
    expect(
      screen.getAllByText("The game is not running, so there is nothing to measure."),
    ).toHaveLength(4);
    for (const id of ["uptime", "players", "cpu", "memory"]) {
      const element = tile(view.container, id);
      expect(element.querySelector("dd")?.textContent ?? "").not.toMatch(/\d/);
      expect(element.querySelector("time")).toBeNull();
    }
    expect(screen.queryByText(/Last reading:/)).toBeNull();
    expect(screen.queryByText("37%")).toBeNull();
    expect(screen.queryByText("2.5 GiB")).toBeNull();
  });

  it("shows measuring or not running after a restart, never the previous run's running since", async () => {
    const load = vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());
    const view = renderPanel();
    expect(await screen.findByText("37%")).toBeVisible();
    expect(view.container.querySelector('time[datetime="2026-11-21T10:00:00Z"]')).not.toBeNull();

    load.mockResolvedValue(
      reading({
        asOf: null,
        runningSince: null,
        runningSinceUnavailableReason: "server-not-running",
        cpuPercent: null,
        cpuUnavailableReason: "measuring",
        memoryUsedBytes: null,
        memoryTotalBytes: null,
        memoryUnavailableReason: "measuring",
        playerCount: null,
        playerCapacity: null,
        playersUnavailableReason: "server-not-running",
      }),
    );
    await view.queryClient.invalidateQueries({
      queryKey: hostingQueryKeys.metrics(server.primaryKey),
      exact: true,
    });

    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.getAllByText("Unavailable")).toHaveLength(4));
    expect(
      within(tile(view.container, "cpu")).getByText("Measuring. The first value appears shortly."),
    ).toBeVisible();
    expect(
      within(tile(view.container, "memory")).getByText(
        "Measuring. The first value appears shortly.",
      ),
    ).toBeVisible();
    expect(
      within(tile(view.container, "uptime")).getByText(
        "The game is not running, so there is nothing to measure.",
      ),
    ).toBeVisible();
    expect(
      within(tile(view.container, "players")).getByText(
        "The game is not running, so there is nothing to measure.",
      ),
    ).toBeVisible();
    expect(view.container.querySelector('time[datetime="2026-11-21T10:00:00Z"]')).toBeNull();
  });

  it("shows the new run's running since after a restart, never the previous run's", async () => {
    const load = vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());
    const view = renderPanel();
    expect(await screen.findByText("37%")).toBeVisible();

    load.mockResolvedValue(
      reading({
        asOf: null,
        runningSince: "2026-11-21T15:00:00Z",
        cpuPercent: null,
        cpuUnavailableReason: "measuring",
        memoryUsedBytes: null,
        memoryTotalBytes: null,
        memoryUnavailableReason: "measuring",
        playerCount: null,
        playerCapacity: null,
        playersUnavailableReason: "measuring",
      }),
    );
    await view.queryClient.invalidateQueries({
      queryKey: hostingQueryKeys.metrics(server.primaryKey),
      exact: true,
    });

    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    await waitFor(() =>
      expect(view.container.querySelector('time[datetime="2026-11-21T15:00:00Z"]')).not.toBeNull(),
    );
    expect(tile(view.container, "uptime").querySelector("time")).toHaveAttribute(
      "datetime",
      "2026-11-21T15:00:00Z",
    );
    expect(view.container.querySelector('time[datetime="2026-11-21T10:00:00Z"]')).toBeNull();
    expect(screen.queryByText("37%")).toBeNull();
  });

  it("keeps the receive stamp when a refetch returns the same reading", async () => {
    let clock = 1000;
    vi.spyOn(performance, "now").mockImplementation(() => clock);
    const load = vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());
    const view = renderPanel();
    expect(await screen.findByText("37%")).toBeVisible();
    const key = hostingQueryKeys.metrics(server.primaryKey);
    expect(view.queryClient.getQueryData<ServerMetricsReading>(key)?.receivedAtMs).toBe(1000);

    clock = 41_000;
    await view.queryClient.invalidateQueries({ queryKey: key, exact: true });
    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    expect(view.queryClient.getQueryData<ServerMetricsReading>(key)?.receivedAtMs).toBe(1000);

    clock = 42_000;
    load.mockResolvedValue(reading({ cpuPercent: 50 }));
    await view.queryClient.invalidateQueries({ queryKey: key, exact: true });
    await waitFor(() => expect(load).toHaveBeenCalledTimes(3));
    expect(await screen.findByText("50%")).toBeVisible();
    expect(view.queryClient.getQueryData<ServerMetricsReading>(key)?.receivedAtMs).toBe(42_000);
  });

  it("turns a held value unavailable with a last reading once it is older than staleAfterSeconds", async () => {
    let clock = 1000;
    vi.spyOn(performance, "now").mockImplementation(() => clock);
    vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());
    // Only the interval and the date are faked, so the query and the async matchers keep their real timers.
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval", "Date"] });
    const { container } = renderPanel();
    expect(await screen.findByText("37%")).toBeVisible();

    act(() => {
      clock = 1000 + 45_000;
      vi.advanceTimersByTime(5000);
    });
    expect(screen.getByText("37%")).toBeVisible();

    act(() => {
      clock = 1000 + 50_000;
      vi.advanceTimersByTime(5000);
    });

    const cpu = tile(container, "cpu");
    expect(within(cpu).getByText("Unavailable")).toBeVisible();
    expect(within(cpu).getByText("The last reading is too old to show.")).toBeVisible();
    expect(cpu.querySelector("dd")?.textContent ?? "").not.toContain("37");
    const last = cpu.querySelector("time");
    expect(last).toHaveAttribute("datetime", "2026-11-21T14:05:00Z");
    expect(last?.textContent).toMatch(/^Last reading: /);
    expect(screen.queryByText("37%")).toBeNull();
    expect(screen.getAllByText("Unavailable")).toHaveLength(4);
  });

  it("shows the last reading line for a worker that is offline", async () => {
    vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(
      reading({ cpuPercent: null, cpuUnavailableReason: "worker-offline" }),
    );

    const { container } = renderPanel();

    expect(
      await screen.findByText("The worker of this server is not connected, so no readings arrive."),
    ).toBeVisible();
    expect(tile(container, "cpu").querySelector("time")).toHaveAttribute(
      "datetime",
      "2026-11-21T14:05:00Z",
    );
    expect(tile(container, "memory").querySelector("time")).toBeNull();
  });

  describe("when the live socket is down", () => {
    const paused = "Live updates are paused. Reconnecting automatically.";

    /** Renders the panel over a seeded cache so the tiles are there without a request. */
    function renderSeededDisconnected() {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });
      const seeded: ServerMetricsReading = { ...reading(), receivedAtMs: performance.now() };
      queryClient.setQueryData(hostingQueryKeys.metrics(server.primaryKey), seeded);
      queryClient.setQueryData(
        hostingQueryKeys.metricsHistory(server.primaryKey, "1h"),
        metricsHistory([]),
      );
      return renderWithProviders(
        <FleetLiveUpdatesContext.Provider
          value={{
            status: "reconnecting",
            disconnectEpoch: 1,
            reconnectNow: () => undefined,
            subscribe: () => () => undefined,
            subscribeCostAlerts: () => () => undefined,
            markCostAlertsDismissed: () => undefined,
          }}
        >
          <ServerMetricsPanel server={server} />
        </FleetLiveUpdatesContext.Provider>,
        { queryClient },
      );
    }

    afterEach(() => {
      vi.useRealTimers();
    });

    it("shows the paused sentence after 3 s, keeps the values and keeps one status region", () => {
      vi.useFakeTimers();
      const { container } = renderSeededDisconnected();

      expect(screen.getByText("37%")).toBeVisible();
      act(() => vi.advanceTimersByTime(2999));
      expect(screen.queryByText(paused)).toBeNull();
      act(() => vi.advanceTimersByTime(1));

      const line = screen.getByText(paused);
      expect(line).toBeVisible();
      expect(line.closest("p")).toHaveClass("server-metrics__paused");
      expect(line.closest("[role='status']")).toBeNull();
      expect(screen.getByText("37%")).toBeVisible();
      expect(within(tile(container, "players")).getByText("4")).toBeVisible();
      expect(container.querySelectorAll("[role='status']")).toHaveLength(1);
    });
  });

  describe("range and view", () => {
    /** A history of the given range with one reading a bucket before now. */
    function historyOf(range: string, bucketSeconds: number) {
      return metricsHistory([historyPoint(Date.now() - bucketSeconds * 1000)], {
        range,
        bucketSeconds,
      });
    }

    beforeEach(() => {
      vi.spyOn(hostingService, "serverMetrics").mockResolvedValue(reading());
      vi.spyOn(hostingService, "serverMetricsHistory").mockResolvedValue(historyOf("1h", 15));
    });

    it("offers the three ranges with 1 hour pressed and a single status region", async () => {
      const { container } = renderPanel();

      await screen.findByRole("img", { name: "CPU load, last hour" });
      const group = screen.getByRole("group", { name: "Time range" });
      const chips = within(group).getAllByRole("button");
      expect(chips.map((chip) => chip.textContent)).toEqual(["1 hour", "24 hours", "7 days"]);
      expect(chips.map((chip) => chip.getAttribute("aria-pressed"))).toEqual([
        "true",
        "false",
        "false",
      ]);
      expect(container.querySelectorAll("[role='status']")).toHaveLength(1);
    });

    it("loads the chosen range, keeps the old charts busy meanwhile and announces it once", async () => {
      const user = userEvent.setup();
      let finish: (value: ReturnType<typeof historyOf>) => void = () => undefined;
      const load = vi
        .spyOn(hostingService, "serverMetricsHistory")
        .mockImplementation((_id, range) => {
          if (range === "1h") return Promise.resolve(historyOf("1h", 15));
          return new Promise((resolve) => {
            finish = resolve;
          });
        });
      const { container } = renderPanel();
      await screen.findByRole("img", { name: "CPU load, last hour" });

      await user.click(screen.getByRole("button", { name: "24 hours" }));

      const views = container.querySelector(".server-metrics__views") as HTMLElement;
      await waitFor(() => expect(views).toHaveAttribute("aria-busy", "true"));
      expect(views).toHaveClass("server-metrics__views--loading");
      expect(screen.getByRole("img", { name: "CPU load, last hour" })).toBeVisible();
      expect(screen.getByRole("button", { name: "24 hours" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
      expect(container.querySelector("p.sr-only[role='status']")).toHaveTextContent(
        "Showing the last 24 hours.",
      );
      expect(load).toHaveBeenCalledWith(server.primaryKey, "24h", expect.anything());

      act(() => finish(historyOf("24h", 60)));

      expect(await screen.findByRole("img", { name: "CPU load, last 24 hours" })).toBeVisible();
      await waitFor(() => expect(views).toHaveAttribute("aria-busy", "false"));
      expect(views).not.toHaveClass("server-metrics__views--loading");
      expect(container.querySelector("p.sr-only[role='status']")).toHaveTextContent(
        "Showing the last 24 hours.",
      );
    });

    it("does nothing when the pressed chip is chosen again", async () => {
      const user = userEvent.setup();
      const load = vi
        .spyOn(hostingService, "serverMetricsHistory")
        .mockResolvedValue(historyOf("1h", 15));
      const { container } = renderPanel();
      await screen.findByRole("img", { name: "CPU load, last hour" });

      await user.click(screen.getByRole("button", { name: "1 hour" }));

      expect(load).toHaveBeenCalledTimes(1);
      expect(container.querySelector("p.sr-only[role='status']")).toBeEmptyDOMElement();
    });

    it("shows the history error with Try again for the same range while tiles and toolbar remain", async () => {
      const user = userEvent.setup();
      const load = vi
        .spyOn(hostingService, "serverMetricsHistory")
        .mockImplementation((_id, range) =>
          range === "1h"
            ? Promise.resolve(historyOf("1h", 15))
            : Promise.reject(new Error("history down")),
        );
      renderPanel();
      await screen.findByRole("img", { name: "CPU load, last hour" });

      await user.click(screen.getByRole("button", { name: "7 days" }));

      const alert = await screen.findByRole("alert");
      expect(alert).toHaveTextContent("The history for this time range could not be loaded.");
      expect(alert).not.toHaveTextContent("history down");
      expect(screen.getByText("37%")).toBeVisible();
      expect(screen.getByRole("group", { name: "Time range" })).toBeVisible();
      expect(screen.queryByRole("img")).toBeNull();

      load.mockImplementation(() => Promise.resolve(historyOf("7d", 300)));
      await user.click(within(alert).getByRole("button", { name: "Try again" }));

      expect(await screen.findByRole("img", { name: "CPU load, last 7 days" })).toBeVisible();
      expect(load).toHaveBeenLastCalledWith(server.primaryKey, "7d", expect.anything());
      expect(screen.queryByRole("alert")).toBeNull();
    });

    it("keeps the charts when a background refetch of the history fails", async () => {
      const load = vi
        .spyOn(hostingService, "serverMetricsHistory")
        .mockResolvedValue(historyOf("1h", 15));
      const view = renderPanel();
      await screen.findByRole("img", { name: "CPU load, last hour" });

      load.mockRejectedValue(new Error("boom"));
      await view.queryClient.invalidateQueries({
        queryKey: hostingQueryKeys.metricsHistory(server.primaryKey, "1h"),
        exact: true,
      });

      await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
      expect(screen.getByRole("img", { name: "CPU load, last hour" })).toBeVisible();
      expect(screen.queryByRole("alert")).toBeNull();
    });

    it("swaps charts for tables and back with focus staying on the toggle", async () => {
      const user = userEvent.setup();
      vi.spyOn(hostingService, "serverMetricsHistory").mockResolvedValue(historyOf("1h", 15));
      const { container } = renderPanel();
      await screen.findByRole("img", { name: "CPU load, last hour" });
      const toggle = screen.getByRole("button", { name: "Show data as tables" });
      const views = container.querySelector(".server-metrics__views") as HTMLElement;
      expect(toggle).toHaveAttribute("aria-controls", views.id);
      expect(toggle).not.toHaveAttribute("aria-pressed");

      await user.click(toggle);

      expect(screen.getAllByRole("table")).toHaveLength(3);
      expect(screen.getByRole("table", { name: "CPU load, last hour" })).toBeVisible();
      expect(screen.getByRole("table", { name: "Memory used, last hour" })).toBeVisible();
      expect(screen.getByRole("table", { name: "Players, last hour" })).toBeVisible();
      expect(screen.queryByRole("img")).toBeNull();
      expect(toggle).toHaveTextContent("Show charts");
      expect(toggle).toHaveFocus();
      expect(container.querySelectorAll("[role='status']")).toHaveLength(1);

      await user.click(toggle);

      expect(screen.queryByRole("table")).toBeNull();
      expect(screen.getByRole("img", { name: "CPU load, last hour" })).toBeVisible();
      expect(toggle).toHaveTextContent("Show data as tables");
      expect(toggle).toHaveFocus();
    });

    it("applies the chosen range to the table view", async () => {
      const user = userEvent.setup();
      vi.spyOn(hostingService, "serverMetricsHistory").mockImplementation((_id, range) =>
        Promise.resolve(range === "1h" ? historyOf("1h", 15) : historyOf("24h", 60)),
      );
      renderPanel();
      await screen.findByRole("img", { name: "CPU load, last hour" });

      await user.click(screen.getByRole("button", { name: "Show data as tables" }));
      await user.click(screen.getByRole("button", { name: "24 hours" }));

      expect(await screen.findByRole("table", { name: "CPU load, last 24 hours" })).toBeVisible();
    });

    it("shows the range chips and the toggle in German", async () => {
      await i18n.changeLanguage("de");
      vi.spyOn(hostingService, "serverMetricsHistory").mockResolvedValue(historyOf("1h", 15));
      renderPanel();

      expect(await screen.findByRole("group", { name: "Zeitraum" })).toBeVisible();
      expect(screen.getByRole("button", { name: "24 Stunden" })).toBeVisible();
      expect(screen.getByRole("button", { name: "Daten als Tabellen anzeigen" })).toBeVisible();
    });
  });

  it("lays the tiles out full width with wrapping values and two columns at 520px or less", () => {
    const components = resolve(
      dirname(fileURLToPath(import.meta.url)),
      "../../src/components/server",
    );
    const metrics = readFileSync(resolve(components, "metrics.css"), "utf8");
    const sections = readFileSync(resolve(components, "sections.css"), "utf8");

    expect(sections).not.toContain("2fr 1fr");
    expect(metrics).toContain("overflow-wrap: anywhere");
    expect(metrics).not.toMatch(/text-overflow\s*:/);
    expect(metrics).toMatch(
      /@media \(max-width: 520px\)\s*\{\s*\.server-metrics__tiles\s*\{\s*grid-template-columns: repeat\(2,/,
    );
    expect(metrics).toContain("font-variant-numeric: tabular-nums");
  });

  it("never polls: the readings hook has no refetch interval and the metrics files no raw timer", () => {
    const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../src");
    const files = [
      "features/hosting/useServerMetrics.ts",
      "features/hosting/metricsModel.ts",
      "components/server/ServerMetricsPanel.tsx",
      "components/server/MetricTile.tsx",
    ];
    for (const file of files) {
      const code = readFileSync(resolve(root, file), "utf8")
        .split("\n")
        .filter((line) => !line.trim().startsWith("*") && !line.trim().startsWith("//"))
        .join("\n");
      expect(code, file).not.toMatch(/refetchInterval|setInterval|new WebSocket/);
    }
  });
});
