import { render } from "@testing-library/react";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { MetricLineChart } from "@/components/server/MetricLineChart";
import type { GameServerMetricsHistory } from "@/features/hosting/types";
import { aggregate, historyPoint, metricsHistory } from "../helpers/metricsHistoryFixtures";

/*
 * T-67-30 and the performance backstop of the 67 UI-SPEC: rendering the largest series the API can serve (seven
 * days at 5 minute buckets, 2 016 points) at a phone width completes within 200 ms and creates no more than one
 * path per segment per layer (line and band). This is a held-out guard, not a benchmark.
 */

const bucketSeconds = 300;
const bucketMs = bucketSeconds * 1000;
const pointCount = 2016;
const now = Date.parse("2026-11-21T14:00:00Z");
const budgetMs = 200;

/** Stubs `ResizeObserver` so the plot box reports a phone width as soon as it is observed. */
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

/**
 * Builds a seven day history of the largest size.
 *
 * @param skip Decides which bucket index has no CPU reading, which splits the series into segments.
 */
function largeHistory(skip: (index: number) => boolean): GameServerMetricsHistory {
  const points = Array.from({ length: pointCount }, (_, index) => {
    const start = now - (pointCount - index) * bucketMs;
    const load = 30 + (index % 40);
    return historyPoint(start, {
      cpu: skip(index) ? null : aggregate(load, load - 10, load + 15),
    });
  });
  return metricsHistory(points, { range: "7d", bucketSeconds });
}

/** Counts the paths of one class below a container. */
function countPaths(container: HTMLElement, className: string): number {
  return container.querySelectorAll(`path.${className}`).length;
}

describe("metrics chart performance backstop", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    stubWidth(320);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders 2 016 connected points at 320px within 200 ms with one line and one band", () => {
    const history = largeHistory(() => false);

    const start = performance.now();
    const { container } = render(
      <MetricLineChart metric="cpu" title="CPU load" history={history} range="7d" nowMs={now} />,
    );
    const elapsed = performance.now() - start;

    expect(elapsed).toBeLessThan(budgetMs);
    expect(countPaths(container, "metric-chart__line")).toBe(1);
    expect(countPaths(container, "metric-chart__band")).toBe(1);
    expect(container.querySelectorAll("circle")).toHaveLength(0);
  });

  it("creates at most one line and one band per segment when the series has gaps", () => {
    const missing = (index: number) => index % 100 === 99;
    const history = largeHistory(missing);
    const segments = Math.ceil(pointCount / 100);

    const start = performance.now();
    const { container } = render(
      <MetricLineChart metric="cpu" title="CPU load" history={history} range="7d" nowMs={now} />,
    );
    const elapsed = performance.now() - start;

    expect(elapsed).toBeLessThan(budgetMs);
    expect(countPaths(container, "metric-chart__line")).toBeLessThanOrEqual(segments);
    expect(countPaths(container, "metric-chart__band")).toBeLessThanOrEqual(segments);
    expect(countPaths(container, "metric-chart__line")).toBe(segments);
  });

  it("renders the players series, which uses steps, within the same budget", () => {
    const history = largeHistory(() => false);

    const start = performance.now();
    const { container } = render(
      <MetricLineChart metric="players" title="Players" history={history} range="7d" nowMs={now} />,
    );
    const elapsed = performance.now() - start;

    expect(elapsed).toBeLessThan(budgetMs);
    expect(countPaths(container, "metric-chart__line")).toBe(1);
  });
});
