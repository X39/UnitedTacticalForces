import type {
  GameServerMetricsHistory,
  ServerMetricAggregate,
  ServerMetricPoint,
} from "@/features/hosting/types";

/** The id of the server the history fixtures belong to. */
export const historyServerId = "f1ee7000-0000-4000-8000-000000000001";

/**
 * Builds the aggregate of one metric in one bucket.
 *
 * @param average The average value.
 * @param minimum The lowest value; defaults to the average.
 * @param maximum The highest value; defaults to the average.
 */
export function aggregate(
  average: number,
  minimum: number = average,
  maximum: number = average,
): ServerMetricAggregate {
  return { average, minimum, maximum };
}

/** The overrides of {@link historyPoint}. */
export interface HistoryPointOptions {
  /** The CPU aggregate; null means no reading. Defaults to a reading of 40 percent. */
  cpu?: ServerMetricAggregate | null;
  /** The memory aggregate; null means no reading. Defaults to 2 GiB. */
  memoryUsed?: ServerMetricAggregate | null;
  /** The players aggregate; null means no reading. Defaults to 4 players. */
  players?: ServerMetricAggregate | null;
}

/**
 * Builds one history bucket.
 *
 * @param bucketStartMs The bucket start in epoch milliseconds.
 * @param options The aggregates; an omitted metric has a reading.
 */
export function historyPoint(
  bucketStartMs: number,
  options: HistoryPointOptions = {},
): ServerMetricPoint {
  return {
    bucketStart: new Date(bucketStartMs).toISOString(),
    cpu: options.cpu === undefined ? aggregate(40, 30, 50) : options.cpu,
    memoryUsed: options.memoryUsed === undefined ? aggregate(2 * 1024 ** 3) : options.memoryUsed,
    players: options.players === undefined ? aggregate(4) : options.players,
  };
}

/** The overrides of {@link metricsHistory}. */
export interface MetricsHistoryOptions {
  /** The range code served; defaults to `1h`. */
  range?: string;
  /** The bucket length in seconds; defaults to 15. */
  bucketSeconds?: number;
  /** The machine memory in bytes; defaults to 8 GiB. */
  memoryTotalBytes?: number | null;
  /** The player capacity; defaults to 16. */
  playerCapacity?: number | null;
}

/**
 * Builds a history response.
 *
 * @param points The buckets in ascending time order.
 * @param options The response fields around the points.
 */
export function metricsHistory(
  points: ServerMetricPoint[],
  options: MetricsHistoryOptions = {},
): GameServerMetricsHistory {
  return {
    gameServerId: historyServerId,
    range: options.range ?? "1h",
    from: "2026-11-21T13:00:00Z",
    to: "2026-11-21T14:00:00Z",
    bucketSeconds: options.bucketSeconds ?? 15,
    memoryTotalBytes:
      options.memoryTotalBytes === undefined ? 8 * 1024 ** 3 : options.memoryTotalBytes,
    playerCapacity: options.playerCapacity === undefined ? 16 : options.playerCapacity,
    points,
  } as GameServerMetricsHistory;
}
