import type { Route } from "@playwright/test";

/** The three history ranges the API serves. */
export type MetricsRangeCode = "1h" | "24h" | "7d";

/** The length of one history bucket per range in seconds, equal to what the API serves. */
const bucketSecondsByRange: Record<MetricsRangeCode, number> = { "1h": 15, "24h": 60, "7d": 300 };

/** The span of every range in milliseconds. */
const spanMsByRange: Record<MetricsRangeCode, number> = {
  "1h": 3_600_000,
  "24h": 86_400_000,
  "7d": 604_800_000,
};

/** The machine memory of every fixture in bytes: 8 GiB. */
const memoryTotalBytes = 8 * 1024 ** 3;

/** The player capacity of every fixture. */
const playerCapacity = 32;

/** The CPU load of the one spike the 24 hour and 7 day histories carry, in percent. */
export const spikeCpuPercent = 95;

/** The current readings of one server in the shape of the REST response. */
export interface MetricsReadingFixture {
  gameServerId: string;
  asOf: string | null;
  staleAfterSeconds: number;
  runningSince: string | null;
  runningSinceUnavailableReason: string | null;
  cpuPercent: number | null;
  cpuUnavailableReason: string | null;
  memoryUsedBytes: number | null;
  memoryTotalBytes: number | null;
  memoryUnavailableReason: string | null;
  playerCount: number | null;
  playerCapacity: number | null;
  playersUnavailableReason: string | null;
}

/** One history bucket in the shape of the REST response. */
export interface MetricsPointFixture {
  bucketStart: string;
  cpu: { average: number; minimum: number; maximum: number } | null;
  memoryUsed: { average: number; minimum: number; maximum: number } | null;
  players: { average: number; minimum: number; maximum: number } | null;
}

/** The history of one server in the shape of the REST response. */
export interface MetricsHistoryFixture {
  gameServerId: string;
  range: MetricsRangeCode;
  from: string;
  to: string;
  bucketSeconds: number;
  memoryTotalBytes: number | null;
  playerCapacity: number | null;
  points: MetricsPointFixture[];
}

/** What the mocked metrics requests return for one test, plus what the test observed. */
export interface MetricsMock {
  /** The readings `GET /metrics` returns. */
  reading: MetricsReadingFixture;
  /** Builds the history `GET /metrics/history` returns for a range, relative to the request time. */
  history: (range: MetricsRangeCode, nowMs: number) => MetricsHistoryFixture;
  /** The number of `GET /metrics` requests answered. */
  currentReads: number;
  /** The range of every `GET /metrics/history` request answered, in order. */
  historyReads: string[];
}

/**
 * Builds a reading of a running server with every value present: running since a fixed instant, 37 percent CPU,
 * 2.5 GiB of 8 GiB memory and 7 of 32 players.
 *
 * @param serverId The server the reading belongs to.
 */
export function availableReading(serverId: string): MetricsReadingFixture {
  return {
    gameServerId: serverId,
    asOf: new Date().toISOString(),
    staleAfterSeconds: 45,
    runningSince: new Date(Date.now() - 90 * 60_000).toISOString(),
    runningSinceUnavailableReason: null,
    cpuPercent: 37,
    cpuUnavailableReason: null,
    memoryUsedBytes: 2.5 * 1024 ** 3,
    memoryTotalBytes,
    memoryUnavailableReason: null,
    playerCount: 7,
    playerCapacity,
    playersUnavailableReason: null,
  };
}

/**
 * Builds the reading of a server whose worker is not connected: no value at all, only reasons.
 *
 * @param serverId The server the reading belongs to.
 */
export function unavailableReading(serverId: string): MetricsReadingFixture {
  return {
    gameServerId: serverId,
    asOf: null,
    staleAfterSeconds: 45,
    runningSince: null,
    runningSinceUnavailableReason: "worker-offline",
    cpuPercent: null,
    cpuUnavailableReason: "worker-offline",
    memoryUsedBytes: null,
    memoryTotalBytes: null,
    memoryUnavailableReason: "worker-offline",
    playerCount: null,
    playerCapacity: null,
    playersUnavailableReason: "worker-offline",
  };
}

/** The history without a single reading, which every chart answers with its empty sentences. */
export function emptyHistory(
  serverId: string,
  range: MetricsRangeCode,
  nowMs: number,
): MetricsHistoryFixture {
  return {
    gameServerId: serverId,
    range,
    from: new Date(nowMs - spanMsByRange[range]).toISOString(),
    to: new Date(nowMs).toISOString(),
    bucketSeconds: bucketSecondsByRange[range],
    memoryTotalBytes: null,
    playerCapacity: null,
    points: [],
  };
}

/**
 * Builds the history of a running server for one range: a reading in every bucket except one stretch of about ten
 * minutes that ended thirty minutes ago, plus, for the 24 hour and 7 day ranges, one CPU spike of
 * {@link spikeCpuPercent} percent five hours ago.
 *
 * @param serverId The server the history belongs to.
 * @param range The requested range.
 * @param nowMs The request time in epoch milliseconds; the newest bucket ends just before it.
 */
export function populatedHistory(
  serverId: string,
  range: MetricsRangeCode,
  nowMs: number,
): MetricsHistoryFixture {
  const bucketSeconds = bucketSecondsByRange[range];
  const bucketMs = bucketSeconds * 1000;
  const newest = Math.floor(nowMs / bucketMs) * bucketMs - bucketMs;
  const oldest = Math.ceil((nowMs - spanMsByRange[range]) / bucketMs) * bucketMs;
  const gapStart = nowMs - 40 * 60_000;
  const gapEnd = nowMs - 30 * 60_000;
  const spikeAt = Math.floor((nowMs - 5 * 3_600_000) / bucketMs) * bucketMs;
  const points: MetricsPointFixture[] = [];
  for (let bucket = oldest; bucket <= newest; bucket += bucketMs) {
    if (bucket >= gapStart && bucket < gapEnd) continue;
    const wave = 30 + ((bucket / bucketMs) % 7) * 3;
    const isSpike = range !== "1h" && bucket === spikeAt;
    const cpu = isSpike ? spikeCpuPercent : wave;
    points.push({
      bucketStart: new Date(bucket).toISOString(),
      cpu: { average: cpu, minimum: cpu - 2, maximum: cpu + (isSpike ? 0 : 4) },
      memoryUsed: {
        average: 2.2 * 1024 ** 3,
        minimum: 2.1 * 1024 ** 3,
        maximum: 2.4 * 1024 ** 3,
      },
      players: { average: 6, minimum: 4, maximum: 9 },
    });
  }
  return {
    gameServerId: serverId,
    range,
    from: new Date(nowMs - spanMsByRange[range]).toISOString(),
    to: new Date(nowMs).toISOString(),
    bucketSeconds,
    memoryTotalBytes,
    playerCapacity,
    points,
  };
}

/**
 * Builds the default mock of a spec that does not test metrics: every reading unavailable with the worker-offline
 * reason and no history, so the panel renders its quiet state and still sends only mocked requests.
 *
 * @param serverId The server the mock answers for.
 */
export function defaultMetricsMock(serverId: string): MetricsMock {
  return {
    reading: unavailableReading(serverId),
    history: (range, nowMs) => emptyHistory(serverId, range, nowMs),
    currentReads: 0,
    historyReads: [],
  };
}

/**
 * Builds the mock of a running server with readings and history.
 *
 * @param serverId The server the mock answers for.
 */
export function populatedMetricsMock(serverId: string): MetricsMock {
  return {
    reading: availableReading(serverId),
    history: (range, nowMs) => populatedHistory(serverId, range, nowMs),
    currentReads: 0,
    historyReads: [],
  };
}

/**
 * Answers the two metrics requests of one server when the request is one of them.
 *
 * Call it before the unexpected-request fallback of a spec's API mock, so the fallback keeps recording only requests
 * that nothing mocked.
 *
 * @param route The intercepted request.
 * @param method The HTTP method of the request.
 * @param path The URL path of the request.
 * @param serverId The server whose metrics are mocked.
 * @param mock The responses to serve; defaults to the all-unavailable reading with an empty history.
 * @returns True when the request was a metrics request and was answered.
 */
export async function fulfillMetricsRoute(
  route: Route,
  method: string,
  path: string,
  serverId: string,
  mock: MetricsMock = defaultMetricsMock(serverId),
): Promise<boolean> {
  if (method !== "GET") return false;
  if (path === `/api/game-servers/${serverId}/metrics`) {
    mock.currentReads += 1;
    await route.fulfill({ json: mock.reading });
    return true;
  }
  if (path === `/api/game-servers/${serverId}/metrics/history`) {
    const range = new URL(route.request().url()).searchParams.get("range") ?? "1h";
    mock.historyReads.push(range);
    const code: MetricsRangeCode = range === "24h" || range === "7d" ? range : "1h";
    await route.fulfill({ json: mock.history(code, Date.now()) });
    return true;
  }
  return false;
}
