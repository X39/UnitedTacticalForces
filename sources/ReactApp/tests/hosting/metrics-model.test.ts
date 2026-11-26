import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import {
  buildSegments,
  formatBucketTime,
  formatGibibytes,
  formatMetricValue,
  formatPercent,
  formatXTick,
  gapRows,
  hasOpenGap,
  highestPoint,
  isReadingStale,
  metricReasonKeys,
  metricSeries,
  metricTiles,
  niceCeiling,
  parseRange,
  rangeBucketMs,
  rangeSpanMs,
  reasonKey,
  xTicks,
  yDomain,
  yTicks,
} from "@/features/hosting/metricsModel";
import type { ServerMetricsReading } from "@/features/hosting/types";
import { aggregate, historyPoint, metricsHistory } from "../helpers/metricsHistoryFixtures";

/*
 * D-09 and MET-01 to MET-03: the pure tile model. A reason from the control plane wins, a held value the browser
 * has aged out turns unavailable with the stale reason, and nothing is ever extrapolated.
 */

/** A reading received at time zero with a 45 second threshold; the overrides replace single fields. */
function reading(overrides: Partial<ServerMetricsReading> = {}): ServerMetricsReading {
  return {
    gameServerId: "f1ee7000-0000-4000-8000-000000000001",
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
    receivedAtMs: 0,
    ...overrides,
  };
}

describe("metrics tile model", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("is stale only after more than staleAfterSeconds on the performance clock", () => {
    const held = reading({ receivedAtMs: 1000 });

    expect(isReadingStale(held, 1000)).toBe(false);
    expect(isReadingStale(held, 1000 + 45_000)).toBe(false);
    expect(isReadingStale(held, 1000 + 45_001)).toBe(true);
    expect(isReadingStale(reading({ staleAfterSeconds: 10 }), 10_001)).toBe(true);
  });

  it("turns every held value into unavailable with the stale reason and the last reading once aged", () => {
    const tiles = metricTiles(reading(), 46_000, "en-US");

    expect(tiles.map((tile) => tile.id)).toEqual(["uptime", "players", "cpu", "memory"]);
    for (const tile of tiles) {
      expect(tile.value).toEqual({
        kind: "unavailable",
        reasonKey: "Server_Metrics_Reason_Stale",
        lastReading: "2026-11-21T14:05:00Z",
      });
    }
  });

  it("keeps the values while the reading is fresh", () => {
    const tiles = metricTiles(reading(), 45_000, "en-US");

    expect(tiles.every((tile) => tile.value.kind === "value")).toBe(true);
  });

  it("shows the last reading only for worker-offline and a server-sent stale, and only when asOf is known", () => {
    const offline = reading({ cpuPercent: null, cpuUnavailableReason: "worker-offline" });
    const sentStale = reading({ cpuPercent: null, cpuUnavailableReason: "stale" });
    const notRunning = reading({ cpuPercent: null, cpuUnavailableReason: "server-not-running" });
    const noInstant = reading({
      asOf: null,
      cpuPercent: null,
      cpuUnavailableReason: "worker-offline",
    });

    expect(metricTiles(offline, 0, "en-US")[2]?.value).toEqual({
      kind: "unavailable",
      reasonKey: "Server_Metrics_Reason_WorkerOffline",
      lastReading: "2026-11-21T14:05:00Z",
    });
    expect(metricTiles(sentStale, 0, "en-US")[2]?.value).toEqual({
      kind: "unavailable",
      reasonKey: "Server_Metrics_Reason_Stale",
      lastReading: "2026-11-21T14:05:00Z",
    });
    expect(metricTiles(notRunning, 0, "en-US")[2]?.value).toEqual({
      kind: "unavailable",
      reasonKey: "Server_Metrics_Reason_NotRunning",
    });
    expect(metricTiles(noInstant, 0, "en-US")[2]?.value).toEqual({
      kind: "unavailable",
      reasonKey: "Server_Metrics_Reason_WorkerOffline",
    });
  });

  it("never invents a number for a missing value and prefers a server reason over the browser check", () => {
    const missing = reading({ cpuPercent: null });
    const withReason = reading({ cpuUnavailableReason: "query-failed" });

    expect(metricTiles(missing, 99_000, "en-US")[2]?.value).toEqual({
      kind: "unavailable",
      reasonKey: "Server_Metrics_Reason_Unknown",
    });
    expect(metricTiles(withReason, 99_000, "en-US")[2]?.value).toEqual({
      kind: "unavailable",
      reasonKey: "Server_Metrics_Reason_QueryFailed",
    });
  });

  it("maps all seven reason codes and falls back to the unknown key", () => {
    expect([...metricReasonKeys.keys()].sort()).toEqual(
      [
        "game-does-not-report",
        "measuring",
        "query-failed",
        "server-not-running",
        "stale",
        "unsupported-platform",
        "worker-offline",
      ].sort(),
    );
    expect(reasonKey("measuring")).toBe("Server_Metrics_Reason_Measuring");
    expect(reasonKey("from-a-newer-api")).toBe("Server_Metrics_Reason_Unknown");
    expect(reasonKey("")).toBe("Server_Metrics_Reason_Unknown");
    expect(reasonKey(null)).toBe("Server_Metrics_Reason_Unknown");
  });

  it("formats players with zero, plural capacity, percent and GiB in English", () => {
    const zero = metricTiles(reading({ playerCount: 0, playerCapacity: 1 }), 0, "en-US");

    expect(zero[1]?.value).toEqual({
      kind: "value",
      text: "0",
      detail: { key: "Server_Metrics_Players_Capacity", options: { count: 1 } },
    });
    expect(formatPercent(37.4, "en-US")).toBe("37%");
    expect(formatPercent(0, "en-US")).toBe("0%");
    expect(formatGibibytes(2_684_354_560, "en-US")).toBe("2.5 GiB");
    expect(formatGibibytes(7_516_192_768, "en-US")).toBe("7 GiB");
  });

  it("formats numbers with German separators", () => {
    expect(formatGibibytes(2_684_354_560, "de-DE")).toBe("2,5 GiB");
    expect(formatPercent(37.4, "de-DE")).toMatch(/^37\s%$/);
    expect(metricTiles(reading({ playerCount: 1234 }), 0, "de-DE")[1]?.value).toMatchObject({
      text: "1.234",
    });
  });
});

/*
 * D-08 and D-09: the chart model. A line breaks at a null and at a gap wider than twice the bucket, nothing is
 * interpolated and a null never becomes zero; the y domains are fixed and the highest point is a selection.
 */

const bucketMs = 15_000;
const base = Date.parse("2026-11-21T14:00:00Z");

/** A bucket with the given offset in buckets; a null value is a bucket without a reading. */
function bucket(index: number, value: ReturnType<typeof aggregate> | null) {
  return { t: base + index * bucketMs, value };
}

describe("metric chart model", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("keeps connected buckets in one segment", () => {
    const segments = buildSegments(
      [bucket(0, aggregate(10, 5, 15)), bucket(1, aggregate(20)), bucket(2, aggregate(30))],
      bucketMs,
    );

    expect(segments).toHaveLength(1);
    expect(segments[0]).toEqual([
      { t: base, avg: 10, min: 5, max: 15 },
      { t: base + bucketMs, avg: 20, min: 20, max: 20 },
      { t: base + 2 * bucketMs, avg: 30, min: 30, max: 30 },
    ]);
  });

  it("breaks at a null value and never turns it into zero or a bridged point", () => {
    const segments = buildSegments(
      [bucket(0, aggregate(10)), bucket(1, null), bucket(2, aggregate(30))],
      bucketMs,
    );

    expect(segments).toHaveLength(2);
    expect(segments.flat().map((point) => point.avg)).toEqual([10, 30]);
  });

  it("breaks only when the distance is more than twice the bucket", () => {
    const joined = buildSegments([bucket(0, aggregate(1)), bucket(2, aggregate(2))], bucketMs);
    const broken = buildSegments(
      [bucket(0, aggregate(1)), { t: base + 2 * bucketMs + 1, value: aggregate(2) }],
      bucketMs,
    );

    expect(joined).toHaveLength(1);
    expect(broken).toHaveLength(2);
  });

  it("keeps an isolated point as its own segment and returns nothing for an empty series", () => {
    const segments = buildSegments(
      [bucket(0, aggregate(1)), bucket(5, aggregate(2)), bucket(10, aggregate(3))],
      bucketMs,
    );

    expect(segments.map((segment) => segment.length)).toEqual([1, 1, 1]);
    expect(buildSegments([], bucketMs)).toEqual([]);
    expect(buildSegments([bucket(0, null), bucket(1, null)], bucketMs)).toEqual([]);
  });

  it("reads one metric out of a history and skips buckets with an invalid time", () => {
    const history = metricsHistory([
      historyPoint(base, { cpu: aggregate(10), memoryUsed: null }),
      { ...historyPoint(base + bucketMs), bucketStart: "not a time" },
    ]);

    expect(metricSeries("cpu", history)).toEqual([{ t: base, value: aggregate(10) }]);
    expect(metricSeries("memory", history)).toEqual([{ t: base, value: null }]);
  });

  it("rounds up to nice numbers", () => {
    expect(niceCeiling(0)).toBe(1);
    expect(niceCeiling(-3)).toBe(1);
    expect(niceCeiling(0.3)).toBeCloseTo(0.5);
    expect(niceCeiling(1)).toBe(1);
    expect(niceCeiling(1.2)).toBe(2);
    expect(niceCeiling(3)).toBe(5);
    expect(niceCeiling(5.1)).toBe(10);
    expect(niceCeiling(16)).toBe(20);
    expect(niceCeiling(240)).toBe(500);
  });

  it("uses fixed y domains: CPU 0 to 100, memory 0 to the total, players with whole-number ticks", () => {
    const gib = 1024 ** 3;
    const history = metricsHistory(
      [
        historyPoint(base, {
          memoryUsed: aggregate(2 * gib, gib, 3 * gib),
          players: aggregate(3, 1, 7),
        }),
      ],
      { memoryTotalBytes: 8 * gib, playerCapacity: 4 },
    );

    expect(yDomain("cpu", history)).toEqual([0, 100]);
    expect(yDomain("memory", history)).toEqual([0, 8 * gib]);
    expect(yDomain("players", history)).toEqual([0, 10]);

    const unknownTotal = metricsHistory(history.points, {
      memoryTotalBytes: null,
      playerCapacity: null,
    });
    expect(yDomain("memory", unknownTotal)).toEqual([0, 5 * gib]);
    expect(yDomain("players", unknownTotal)).toEqual([0, 10]);
  });

  it("makes the players ceiling even so the middle tick is a whole number", () => {
    const empty = metricsHistory([], { playerCapacity: null });
    const small = metricsHistory([historyPoint(base, { players: aggregate(3) })], {
      playerCapacity: null,
    });

    expect(yDomain("players", empty)).toEqual([0, 2]);
    expect(yDomain("players", small)).toEqual([0, 6]);
    expect(yTicks(yDomain("players", small))).toEqual([0, 3, 6]);
  });

  it("returns four or three time ticks across the range and three y ticks", () => {
    expect(xTicks(0, 3000, 4)).toEqual([0, 1000, 2000, 3000]);
    expect(xTicks(0, 3000, 3)).toEqual([0, 1500, 3000]);
    expect(yTicks([0, 100])).toEqual([0, 50, 100]);
  });

  it("selects the point with the largest maximum, the earliest on a tie", () => {
    const segments = buildSegments(
      [
        bucket(0, aggregate(10, 5, 60)),
        bucket(1, aggregate(20, 5, 90)),
        bucket(2, aggregate(30, 5, 90)),
      ],
      bucketMs,
    );

    expect(highestPoint(segments)).toMatchObject({ t: base + bucketMs, max: 90 });
    expect(highestPoint([])).toBeNull();
  });

  it("reports an open gap for a broken series and a stale right edge but not for a connected one", () => {
    const connected = buildSegments([bucket(0, aggregate(1)), bucket(1, aggregate(2))], bucketMs);
    const broken = buildSegments([bucket(0, aggregate(1)), bucket(5, aggregate(2))], bucketMs);

    expect(hasOpenGap(connected, bucketMs, base + bucketMs)).toBe(false);
    expect(hasOpenGap(connected, bucketMs, base + bucketMs + 2 * bucketMs)).toBe(false);
    expect(hasOpenGap(connected, bucketMs, base + bucketMs + 2 * bucketMs + 1)).toBe(true);
    expect(hasOpenGap(broken, bucketMs, base + 5 * bucketMs)).toBe(true);
    expect(hasOpenGap([], bucketMs, base)).toBe(false);
  });

  it("builds table rows newest first with one gap row per stretch including the stretch up to now", () => {
    const rows = gapRows(
      [bucket(0, aggregate(1)), bucket(1, aggregate(2)), bucket(6, aggregate(3))],
      bucketMs,
      base + 12 * bucketMs,
    );

    expect(rows.map((row) => row.kind)).toEqual(["gap", "row", "gap", "row", "row"]);
    expect(rows[0]).toEqual({ kind: "gap", from: base + 7 * bucketMs, to: base + 12 * bucketMs });
    expect(rows[2]).toEqual({ kind: "gap", from: base + 2 * bucketMs, to: base + 6 * bucketMs });
    expect(rows[4]).toMatchObject({ kind: "row", point: { t: base } });
  });

  it("treats a bucket without a reading inside a run as a gap and produces no row for it", () => {
    const rows = gapRows(
      [bucket(0, aggregate(1)), bucket(1, null), bucket(2, aggregate(3))],
      bucketMs,
      base + 2 * bucketMs,
    );

    expect(rows.map((row) => row.kind)).toEqual(["row", "gap", "row"]);
    expect(rows[1]).toEqual({ kind: "gap", from: base + bucketMs, to: base + 2 * bucketMs });
    expect(gapRows([bucket(0, null)], bucketMs, base + 100 * bucketMs)).toEqual([]);
  });

  it("maps ranges to buckets and spans and unknown codes to the fallback", () => {
    expect(rangeBucketMs("1h")).toBe(15_000);
    expect(rangeBucketMs("24h")).toBe(60_000);
    expect(rangeBucketMs("7d")).toBe(300_000);
    expect(rangeSpanMs("1h")).toBe(3_600_000);
    expect(rangeSpanMs("7d")).toBe(7 * 24 * 3_600_000);
    expect(parseRange("24h", "1h")).toBe("24h");
    expect(parseRange("1w", "7d")).toBe("7d");
  });

  it("formats values and ticks for the locale", async () => {
    expect(formatMetricValue("cpu", 37.4, "en-US")).toBe("37%");
    expect(formatMetricValue("memory", 2_684_354_560, "en-US")).toBe("2.5 GiB");
    expect(formatMetricValue("players", 3.4, "en-US")).toBe("3");
    expect(formatMetricValue("players", 1234, "de-DE")).toBe("1.234");
    await i18n.changeLanguage("de");
    expect(formatXTick(Date.parse("2026-11-21T14:05:00Z"), "7d")).toMatch(/Sa/);
    expect(formatBucketTime(Date.parse("2026-01-02T03:04:00Z"), "7d")).toMatch(/\d/);
  });
});
