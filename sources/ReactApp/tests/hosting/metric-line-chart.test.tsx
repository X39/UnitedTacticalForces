import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { MetricLineChart } from "@/components/server/MetricLineChart";
import { formatBucketTime, type ChartMetric } from "@/features/hosting/metricsModel";
import type { GameServerMetricsHistory } from "@/features/hosting/types";
import { aggregate, historyPoint, metricsHistory } from "../helpers/metricsHistoryFixtures";

/*
 * D-08, D-09 and MET-04: the hand-built SVG chart. It names itself, breaks at gaps, draws an isolated point as a
 * marker, keeps spikes in a band, never draws zero for a missing reading and keeps its height when empty.
 */

const bucketMs = 15_000;
const now = Date.parse("2026-11-21T14:00:00Z");
const gib = 1024 ** 3;

/** Stubs `ResizeObserver` so the plot box reports a fixed width as soon as it is observed. */
function stubWidth(width: number): void {
  class FixedResizeObserver {
    private readonly callback: ResizeObserverCallback;

    constructor(callback: ResizeObserverCallback) {
      this.callback = callback;
    }

    observe(target: Element): void {
      this.callback(
        [{ target, contentRect: { width } } as unknown as ResizeObserverEntry],
        this as unknown as ResizeObserver,
      );
    }

    unobserve(): void {}

    disconnect(): void {}
  }
  vi.stubGlobal("ResizeObserver", FixedResizeObserver);
}

/** The time of the bucket that is the given number of buckets before now. */
function ago(buckets: number): number {
  return now - buckets * bucketMs;
}

function renderChart(
  history: GameServerMetricsHistory,
  options: { metric?: ChartMetric; playersNotReported?: boolean; title?: string } = {},
) {
  const metric = options.metric ?? "cpu";
  return render(
    <MetricLineChart
      metric={metric}
      title={options.title ?? "CPU load"}
      history={history}
      range="1h"
      nowMs={now}
      playersNotReported={options.playersNotReported ?? false}
    />,
  );
}

/** The `d` attributes of the paths of one class inside a container. */
function paths(container: HTMLElement, className: string): string[] {
  return [...container.querySelectorAll(`path.${className}`)].map(
    (path) => path.getAttribute("d") ?? "",
  );
}

/** The y coordinates of every absolute path command of a path, in order. */
function yCoordinates(path: string): number[] {
  return [...path.matchAll(/[ML]-?[\d.]+ (-?[\d.]+)/g)].map((match) => Number(match[1]));
}

describe("metric line chart", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    stubWidth(600);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("names the plot group and the image and describes the image by the highest value", () => {
    const history = metricsHistory([
      historyPoint(ago(3), { cpu: aggregate(40, 30, 60) }),
      historyPoint(ago(2), { cpu: aggregate(45, 35, 80) }),
      historyPoint(ago(1), { cpu: aggregate(42, 32, 55) }),
    ]);

    renderChart(history);

    const group = screen.getByRole("group", {
      name: "CPU load chart. Use the arrow keys to read single values, or show the data as tables.",
    });
    expect(group).toHaveAttribute("tabindex", "0");
    const image = screen.getByRole("img", { name: "CPU load, last hour" });
    const summary = screen.getByText(/^Highest: 80%, /);
    expect(image).toHaveAttribute("aria-describedby", summary.id);
    expect(screen.getByRole("heading", { level: 3, name: "CPU load" })).toBeVisible();
  });

  it("draws one line and one band for a connected series and no gap note", () => {
    const history = metricsHistory([
      historyPoint(ago(3), { cpu: aggregate(40, 30, 60) }),
      historyPoint(ago(2), { cpu: aggregate(45, 35, 80) }),
      historyPoint(ago(1), { cpu: aggregate(42, 32, 55) }),
    ]);

    const { container } = renderChart(history);

    expect(paths(container, "metric-chart__line")).toHaveLength(1);
    expect(paths(container, "metric-chart__band")).toHaveLength(1);
    expect(container.querySelectorAll("circle")).toHaveLength(0);
    expect(screen.queryByText(/^Gaps mean/)).toBeNull();
  });

  it("breaks the line at a missing reading, never draws zero and explains the gap", () => {
    const history = metricsHistory([
      historyPoint(ago(5), { cpu: aggregate(40, 30, 50) }),
      historyPoint(ago(4), { cpu: aggregate(41, 31, 51) }),
      historyPoint(ago(3), { cpu: null }),
      historyPoint(ago(2), { cpu: aggregate(42, 32, 52) }),
      historyPoint(ago(1), { cpu: aggregate(43, 33, 53) }),
    ]);

    const { container } = renderChart(history);

    const lines = paths(container, "metric-chart__line");
    expect(lines).toHaveLength(2);
    const plotBottom = 160 - 24;
    for (const line of lines) {
      for (const y of yCoordinates(line)) expect(y).toBeLessThan(plotBottom - 20);
    }
    expect(
      screen.getByText("Gaps mean no reading was available. Nothing is estimated."),
    ).toBeVisible();
  });

  it("breaks at a hole wider than twice the bucket and joins at exactly twice", () => {
    const wide = metricsHistory([
      historyPoint(ago(10)),
      historyPoint(ago(9)),
      historyPoint(ago(5)),
      historyPoint(ago(4)),
    ]);
    const narrow = metricsHistory([historyPoint(ago(6)), historyPoint(ago(4))]);

    const first = renderChart(wide);
    expect(paths(first.container, "metric-chart__line")).toHaveLength(2);
    first.unmount();
    const second = renderChart(narrow);
    expect(paths(second.container, "metric-chart__line")).toHaveLength(1);
  });

  it("draws an isolated point as a marker and not as a line", () => {
    const history = metricsHistory([historyPoint(ago(20)), historyPoint(ago(1))]);

    const { container } = renderChart(history);

    expect(paths(container, "metric-chart__line")).toEqual([]);
    const markers = container.querySelectorAll("circle.metric-chart__marker");
    expect(markers).toHaveLength(2);
    expect(markers[0]).toHaveAttribute("r", "4");
  });

  it("draws no band where the lowest equals the highest", () => {
    const history = metricsHistory([
      historyPoint(ago(3), { cpu: aggregate(40) }),
      historyPoint(ago(2), { cpu: aggregate(42) }),
    ]);

    const { container } = renderChart(history);

    expect(paths(container, "metric-chart__line")).toHaveLength(1);
    expect(paths(container, "metric-chart__band")).toEqual([]);
  });

  it("draws the players series as a stepped line and band", () => {
    const history = metricsHistory([
      historyPoint(ago(3), { players: aggregate(3, 2, 4) }),
      historyPoint(ago(2), { players: aggregate(5, 4, 6) }),
      historyPoint(ago(1), { players: aggregate(5, 5, 5) }),
    ]);

    const { container } = renderChart(history, { metric: "players", title: "Players" });

    const [line] = paths(container, "metric-chart__line");
    const [band] = paths(container, "metric-chart__band");
    expect(line).toMatch(/H[\d.]+V[\d.]+/);
    expect(line).not.toMatch(/L/);
    expect(band).toMatch(/H[\d.]+V[\d.]+.*V[\d.]+H[\d.]+Z$/);
  });

  it("clamps a value above the fixed domain into the plot", () => {
    const history = metricsHistory([
      historyPoint(ago(2), { cpu: aggregate(150, 100, 200) }),
      historyPoint(ago(1), { cpu: aggregate(150, 100, 200) }),
    ]);

    const { container } = renderChart(history);

    const [line] = paths(container, "metric-chart__line");
    expect(yCoordinates(line ?? "")).toEqual([8, 8]);
  });

  it("shows four time ticks at the chart minimum width and three below it", () => {
    const history = metricsHistory([historyPoint(ago(2)), historyPoint(ago(1))]);

    const wide = renderChart(history);
    expect(wide.container.querySelectorAll("text.metric-chart__tick")).toHaveLength(3 + 4);
    wide.unmount();
    stubWidth(300);
    const narrow = renderChart(history);
    expect(narrow.container.querySelectorAll("text.metric-chart__tick")).toHaveLength(3 + 3);
  });

  it("formats the axis labels as percent, gibibytes and whole players", () => {
    const history = metricsHistory([historyPoint(ago(2)), historyPoint(ago(1))], {
      memoryTotalBytes: 8 * gib,
      playerCapacity: 16,
    });

    const cpu = renderChart(history);
    expect(screen.getByText("100%")).toBeVisible();
    expect(screen.getByText("50%")).toBeVisible();
    cpu.unmount();
    const memory = renderChart(history, { metric: "memory", title: "Memory used" });
    expect(screen.getByText("8 GiB")).toBeVisible();
    expect(screen.getByText("4 GiB")).toBeVisible();
    memory.unmount();
    renderChart(history, { metric: "players", title: "Players" });
    expect(screen.getByText("20")).toBeVisible();
    expect(screen.getByText("10")).toBeVisible();
  });

  it("explains a stale right edge with the gap note", () => {
    const history = metricsHistory([historyPoint(ago(60)), historyPoint(ago(59))]);

    renderChart(history);

    expect(screen.getByText(/^Gaps mean no reading/)).toBeVisible();
  });

  it("keeps the plot and shows the two empty sentences when there is no reading", () => {
    const { container } = renderChart(metricsHistory([]));

    expect(container.querySelector("svg")).toBeNull();
    expect(screen.getByRole("group")).toBeVisible();
    expect(screen.getByText("No readings in this time range.")).toBeVisible();
    expect(screen.getByText("Readings are collected while the server is running.")).toBeVisible();
    expect(screen.queryByText(/^Highest:/)).toBeNull();
    expect(screen.queryByText(/^Gaps mean/)).toBeNull();
  });

  it("treats a metric without any reading like an empty history", () => {
    const history = metricsHistory([
      historyPoint(ago(2), { players: null }),
      historyPoint(ago(1), { players: null }),
    ]);

    const { container } = renderChart(history, { metric: "players", title: "Players" });

    expect(container.querySelector("svg")).toBeNull();
    expect(screen.getByText("No readings in this time range.")).toBeVisible();
  });

  it("shows only the game-does-not-report sentence for an empty players chart", () => {
    renderChart(metricsHistory([]), {
      metric: "players",
      title: "Players",
      playersNotReported: true,
    });

    expect(screen.getByText("This game does not report player counts.")).toBeVisible();
    expect(screen.queryByText("No readings in this time range.")).toBeNull();
    expect(screen.queryByText("Readings are collected while the server is running.")).toBeNull();
  });

  it("uses German titles and number formats", async () => {
    await i18n.changeLanguage("de");
    const history = metricsHistory([
      historyPoint(ago(2), { memoryUsed: aggregate(2.5 * gib) }),
      historyPoint(ago(1), { memoryUsed: aggregate(2.5 * gib) }),
    ]);

    renderChart(history, { metric: "memory", title: "Belegter Arbeitsspeicher" });

    expect(
      screen.getByRole("img", { name: "Belegter Arbeitsspeicher, letzte Stunde" }),
    ).toBeVisible();
    expect(screen.getByText(/^Höchstwert: 2,5 GiB, /)).toBeVisible();
  });

  describe("reading values", () => {
    /** Three spread points: x is about 129, 360 and 590 at a width of 600. */
    const spread = () =>
      metricsHistory([
        historyPoint(ago(200), { cpu: aggregate(30, 20, 40) }),
        historyPoint(ago(100), { cpu: aggregate(45, 35, 80) }),
        historyPoint(ago(1), { cpu: aggregate(55) }),
      ]);

    /** The plot group of the chart, with a zero based bounding box so client x equals plot x. */
    function plot(): HTMLElement {
      const group = screen.getByRole("group");
      group.getBoundingClientRect = () =>
        ({ left: 0, top: 0, width: 600, height: 160, right: 600, bottom: 160 }) as DOMRect;
      return group;
    }

    /** The tooltip, found by its decoration class because it is hidden from the accessibility tree. */
    function tooltip(container: HTMLElement): HTMLElement | null {
      return container.querySelector(".metric-chart__tooltip");
    }

    it("shows no crosshair and no tooltip before anything is read", () => {
      const { container } = renderChart(spread());

      expect(container.querySelector(".metric-chart__crosshair")).toBeNull();
      expect(tooltip(container)).toBeNull();
    });

    it("places the crosshair and an aria-hidden tooltip at the point nearest in x", () => {
      const { container } = renderChart(spread());
      const group = plot();

      fireEvent.pointerMove(group, { clientX: 340, pointerType: "mouse" });

      const box = tooltip(container);
      expect(box).toHaveAttribute("aria-hidden", "true");
      expect(box).toHaveTextContent(formatBucketTime(ago(100), "1h"));
      expect(box).toHaveTextContent("Average 45%");
      expect(box).toHaveTextContent("Lowest 35%, highest 80%");
      const crosshair = container.querySelector(".metric-chart__crosshair");
      expect(Number(crosshair?.getAttribute("x1"))).toBeCloseTo(360.3, 0);
      expect(crosshair?.parentElement).toHaveAttribute("aria-hidden", "true");
    });

    it("omits the lowest and highest line when they equal the average", () => {
      const { container } = renderChart(spread());
      const group = plot();

      fireEvent.pointerMove(group, { clientX: 595, pointerType: "mouse" });

      expect(tooltip(container)).toHaveTextContent("Average 55%");
      expect(tooltip(container)).not.toHaveTextContent("Lowest");
    });

    it("flips the tooltip before the crosshair near the right edge and keeps it inside the plot", () => {
      const { container } = renderChart(spread());
      const group = plot();

      fireEvent.pointerMove(group, { clientX: 595, pointerType: "mouse" });
      const flipped = tooltip(container);
      expect(flipped).toHaveAttribute("data-side", "before");
      const left = Number(flipped?.style.left.replace("px", ""));
      expect(left).toBeGreaterThanOrEqual(0);
      expect(left + 224).toBeLessThanOrEqual(600);

      fireEvent.pointerMove(group, { clientX: 130, pointerType: "mouse" });
      expect(tooltip(container)).toHaveAttribute("data-side", "after");
    });

    it("hides the tooltip when the mouse leaves the plot", () => {
      const { container } = renderChart(spread());
      const group = plot();
      fireEvent.pointerMove(group, { clientX: 340, pointerType: "mouse" });

      fireEvent.pointerLeave(group, { pointerType: "mouse" });

      expect(tooltip(container)).toBeNull();
      expect(container.querySelector(".metric-chart__crosshair")).toBeNull();
    });

    it("keeps the tooltip after a touch ends until a tap outside the plot", () => {
      const { container } = renderChart(spread());
      const group = plot();

      fireEvent.pointerDown(group, { clientX: 340, pointerType: "touch" });
      fireEvent.pointerLeave(group, { pointerType: "touch" });
      expect(tooltip(container)).not.toBeNull();

      fireEvent.pointerDown(group, { clientX: 130, pointerType: "touch" });
      expect(tooltip(container)).toHaveTextContent("Average 30%");

      fireEvent.pointerDown(document.body, { pointerType: "touch" });
      expect(tooltip(container)).toBeNull();
    });

    it("moves with Right and Left, jumps with Home and End and hides with Escape", () => {
      const { container } = renderChart(spread());
      const group = plot();
      group.focus();

      fireEvent.keyDown(group, { key: "Home" });
      expect(tooltip(container)).toHaveTextContent("Average 30%");
      fireEvent.keyDown(group, { key: "ArrowRight" });
      expect(tooltip(container)).toHaveTextContent("Average 45%");
      fireEvent.keyDown(group, { key: "ArrowRight" });
      expect(tooltip(container)).toHaveTextContent("Average 55%");
      fireEvent.keyDown(group, { key: "ArrowRight" });
      expect(tooltip(container)).toHaveTextContent("Average 55%");
      fireEvent.keyDown(group, { key: "ArrowLeft" });
      expect(tooltip(container)).toHaveTextContent("Average 45%");
      fireEvent.keyDown(group, { key: "End" });
      expect(tooltip(container)).toHaveTextContent("Average 55%");
      fireEvent.keyDown(group, { key: "Escape" });
      expect(tooltip(container)).toBeNull();
    });

    it("starts from the newest point with Left and from the oldest with Right", () => {
      const { container } = renderChart(spread());
      const group = plot();

      fireEvent.keyDown(group, { key: "ArrowLeft" });
      expect(tooltip(container)).toHaveTextContent("Average 55%");
      fireEvent.keyDown(group, { key: "Escape" });
      fireEvent.keyDown(group, { key: "ArrowRight" });
      expect(tooltip(container)).toHaveTextContent("Average 30%");
    });

    it("never selects a bucket without a reading", () => {
      const history = metricsHistory([
        historyPoint(ago(5), { cpu: aggregate(40) }),
        historyPoint(ago(4), { cpu: aggregate(41) }),
        historyPoint(ago(3), { cpu: null }),
        historyPoint(ago(2), { cpu: aggregate(42) }),
        historyPoint(ago(1), { cpu: aggregate(43) }),
      ]);
      const { container } = renderChart(history);
      const group = plot();

      fireEvent.keyDown(group, { key: "Home" });
      const seen: string[] = [];
      for (let step = 0; step < 4; step += 1) {
        seen.push(tooltip(container)?.textContent ?? "");
        fireEvent.keyDown(group, { key: "ArrowRight" });
      }

      expect(seen.map((text) => /Average (\d+)%/.exec(text)?.[1])).toEqual([
        "40",
        "41",
        "42",
        "43",
      ]);
    });

    it("intercepts no key while the plot itself is not the target", () => {
      const { container } = renderChart(spread());
      plot();
      const image = screen.getByRole("img");

      const notPrevented = fireEvent.keyDown(image, { key: "ArrowRight" });
      const bodyNotPrevented = fireEvent.keyDown(document.body, { key: "ArrowRight" });

      expect(notPrevented).toBe(true);
      expect(bodyNotPrevented).toBe(true);
      expect(tooltip(container)).toBeNull();
    });

    it("prevents the default of a handled key and leaves other keys alone", () => {
      renderChart(spread());
      const group = plot();

      expect(fireEvent.keyDown(group, { key: "ArrowRight" })).toBe(false);
      expect(fireEvent.keyDown(group, { key: "a" })).toBe(true);
      expect(fireEvent.keyDown(group, { key: "ArrowRight", ctrlKey: true })).toBe(true);
    });

    it("hides the tooltip when the plot loses focus", () => {
      const { container } = renderChart(spread());
      const group = plot();
      group.focus();
      fireEvent.keyDown(group, { key: "Home" });
      expect(tooltip(container)).not.toBeNull();

      fireEvent.blur(group);

      expect(tooltip(container)).toBeNull();
    });

    it("offers nothing to read for an empty plot", () => {
      const { container } = renderChart(metricsHistory([]));
      const group = plot();

      fireEvent.keyDown(group, { key: "Home" });
      fireEvent.pointerMove(group, { clientX: 100, pointerType: "mouse" });

      expect(tooltip(container)).toBeNull();
    });
  });
});
