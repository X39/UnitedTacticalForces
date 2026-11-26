import i18n from "@/app/i18n";
import { currentLocale } from "@/app/format";
import type { TranslationKey } from "@/app/localizationTypes";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import { historyBucketMs } from "@/features/hosting/metricsFrames";
import type {
  GameServerMetricsHistory,
  ServerMetricAggregate,
  ServerMetricsRange,
  ServerMetricsReading,
} from "@/features/hosting/types";

/** The fixed sentence for a reason code the browser does not know and for a missing code. */
const unknownReasonKey: TranslationKey = "Server_Metrics_Reason_Unknown";

/**
 * Maps the closed list of unavailable reason codes of the control plane to their sentence keys.
 *
 * The browser never derives a reason from server status strings and never prints a raw code: a code that is not in
 * this map reads {@link reasonKey}'s fixed fallback sentence.
 */
export const metricReasonKeys: ReadonlyMap<string, TranslationKey> = new Map<
  string,
  TranslationKey
>([
  ["server-not-running", "Server_Metrics_Reason_NotRunning"],
  ["worker-offline", "Server_Metrics_Reason_WorkerOffline"],
  ["stale", "Server_Metrics_Reason_Stale"],
  ["game-does-not-report", "Server_Metrics_Reason_NoPlayers"],
  ["query-failed", "Server_Metrics_Reason_QueryFailed"],
  ["measuring", "Server_Metrics_Reason_Measuring"],
  ["unsupported-platform", "Server_Metrics_Reason_UnsupportedPlatform"],
]);

/**
 * Resolves a reason code to the key of its sentence.
 *
 * @param code The reason code from the control plane; null, undefined or an unknown code is allowed.
 * @returns The key of the mapped sentence, or the key of the generic "No reading is available." sentence.
 */
export function reasonKey(code: string | null | undefined): TranslationKey {
  if (code === null || code === undefined) return unknownReasonKey;
  return metricReasonKeys.get(code) ?? unknownReasonKey;
}

/** The four tiles of the metrics panel, in display order. */
export type MetricTileId = "uptime" | "players" | "cpu" | "memory";

/** A localized sentence to render: the key and the interpolation values. */
export interface MetricTileText {
  /** The localization key; the plural key is given without its `_one` or `_other` suffix. */
  key: "Server_Metrics_Players_Capacity" | "Server_Metrics_Memory_OfTotal";
  /** The interpolation values, including `count` for a plural key. */
  options: Record<string, unknown>;
}

/** What one tile shows: a formatted value or the word "Unavailable" with the reason sentence. */
export type MetricTileValue =
  | {
      /** A value the control plane reported. */
      kind: "value";
      /** The value already formatted for the locale. */
      text: string;
      /** The ISO instant when the value is a point in time, so it renders as a `time` element. */
      dateTime?: string;
      /** The second line, when the reading carries one. */
      detail?: MetricTileText;
    }
  | {
      /** A reading that is not available. */
      kind: "unavailable";
      /** The key of the reason sentence shown on the second line. */
      reasonKey: TranslationKey;
      /** The ISO instant of the last reading, only for the reasons that carry a last-reading line. */
      lastReading?: string;
    };

/** The presentation of one tile, derived without any framework or clock. */
export interface MetricTilePresentation {
  /** Which tile this is. */
  id: MetricTileId;
  /** The key of the tile term. */
  termKey: TranslationKey;
  /** The value or the unavailable state. */
  value: MetricTileValue;
}

/** The number of bytes in one gibibyte. */
const bytesPerGibibyte = 1024 ** 3;

/**
 * Formats a byte count as gibibytes with at most one decimal and the unit.
 *
 * @param bytes The byte count.
 * @param locale The locale of the number format.
 * @returns For example "2.5 GiB" in English and "2,5 GiB" in German.
 */
export function formatGibibytes(bytes: number, locale: string): string {
  const value = new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(
    bytes / bytesPerGibibyte,
  );
  return i18n.t("Server_Metrics_Memory_Value", { value });
}

/**
 * Formats a CPU load as a whole percent.
 *
 * @param percent The load from 0 to 100.
 * @param locale The locale of the number format.
 * @returns For example "37%" in English.
 */
export function formatPercent(percent: number, locale: string): string {
  return new Intl.NumberFormat(locale, { style: "percent", maximumFractionDigits: 0 }).format(
    percent / 100,
  );
}

/** The reason codes whose tile also shows when the last reading was taken. */
const lastReadingCodes: ReadonlySet<string> = new Set(["worker-offline", "stale"]);

/**
 * Tells whether a held reading is too old to show, measured on the browser's own monotonic clock.
 *
 * The age is the time since the browser received the reading (`receivedAtMs`, taken with `performance.now()`), never
 * the difference between the browser wall clock and the API timestamp, so a skewed browser clock can neither fake
 * freshness nor fake staleness. The threshold is the control plane's `staleAfterSeconds`; the browser only compares
 * against it and never calculates or extrapolates a value.
 *
 * @param reading The held reading with the moment the browser received it.
 * @param nowMs The current time on the `performance.now()` timeline.
 * @returns True when more than `staleAfterSeconds` have passed since the browser received the reading.
 */
export function isReadingStale(reading: ServerMetricsReading, nowMs: number): boolean {
  return nowMs - reading.receivedAtMs > reading.staleAfterSeconds * 1000;
}

/** The instant of the last reading when it is known and valid, so it can be shown as a time. */
function lastReadingOf(reading: ServerMetricsReading): string | undefined {
  const instant = reading.asOf;
  if (instant === null || instant === undefined || Number.isNaN(Date.parse(instant))) {
    return undefined;
  }
  return instant;
}

/**
 * Builds the unavailable value of one tile.
 *
 * @param code The reason code, or null when no code came with the missing value.
 * @param lastReading The instant of the last reading; only attached for the codes that show it.
 */
function unavailable(code: string | null | undefined, lastReading?: string): MetricTileValue {
  const withLast = code !== null && code !== undefined && lastReadingCodes.has(code);
  return {
    kind: "unavailable",
    reasonKey: reasonKey(code),
    ...(withLast && lastReading !== undefined ? { lastReading } : {}),
  };
}

/** Whether a reason code is set; an empty code counts as missing. */
function hasReason(code: string | null | undefined): boolean {
  return code !== null && code !== undefined && code !== "";
}

/** What every tile needs to decide between a reason, a held value and a browser-aged value. */
interface TileContext {
  /** The held reading. */
  reading: ServerMetricsReading;
  /** Whether the browser age check marks the reading stale. */
  stale: boolean;
  /** The instant of the last reading, when known. */
  lastReading: string | undefined;
}

/**
 * Applies the first-match rule shared by all tiles: a server reason code wins, a missing value without a code reads
 * the generic sentence, a held value the browser aged out reads the stale sentence with the last reading, and
 * otherwise the value is shown.
 *
 * @param context The reading and its age.
 * @param code The reason code of this tile.
 * @param hasValue Whether the reading holds a value for this tile.
 * @param value Builds the value presentation; only called when the tile shows a value.
 */
function decide(
  context: TileContext,
  code: string | null | undefined,
  hasValue: boolean,
  value: () => MetricTileValue,
): MetricTileValue {
  if (hasReason(code)) return unavailable(code, context.lastReading);
  if (!hasValue) return unavailable(null);
  if (context.stale) return unavailable("stale", context.lastReading);
  return value();
}

function uptimeValue(context: TileContext): MetricTileValue {
  const { reading } = context;
  const instant = reading.runningSince;
  const text = instant === null || instant === undefined ? "" : formatInstant(instant);
  return decide(context, reading.runningSinceUnavailableReason, text !== "", () => ({
    kind: "value",
    text,
    ...(instant === null || instant === undefined ? {} : { dateTime: instant }),
  }));
}

function playersValue(context: TileContext, locale: string): MetricTileValue {
  const { reading } = context;
  const count = reading.playerCount;
  return decide(
    context,
    reading.playersUnavailableReason,
    count !== null && count !== undefined,
    () => {
      const capacity = reading.playerCapacity;
      return {
        kind: "value",
        text: new Intl.NumberFormat(locale).format(count ?? 0),
        ...(capacity !== null && capacity !== undefined
          ? { detail: { key: "Server_Metrics_Players_Capacity", options: { count: capacity } } }
          : {}),
      };
    },
  );
}

function cpuValue(context: TileContext, locale: string): MetricTileValue {
  const { reading } = context;
  const percent = reading.cpuPercent;
  return decide(
    context,
    reading.cpuUnavailableReason,
    percent !== null && percent !== undefined,
    () => ({ kind: "value", text: formatPercent(percent ?? 0, locale) }),
  );
}

function memoryValue(context: TileContext, locale: string): MetricTileValue {
  const { reading } = context;
  const used = reading.memoryUsedBytes;
  return decide(
    context,
    reading.memoryUnavailableReason,
    used !== null && used !== undefined,
    () => {
      const total = reading.memoryTotalBytes;
      return {
        kind: "value",
        text: formatGibibytes(used ?? 0, locale),
        ...(total !== null && total !== undefined
          ? {
              detail: {
                key: "Server_Metrics_Memory_OfTotal",
                options: { total: formatGibibytes(total, locale) },
              },
            }
          : {}),
      };
    },
  );
}

/**
 * Presents the four metric tiles of one reading in display order: Running since, Players, CPU, Memory.
 *
 * Each tile decides on its own, first match wins: a reason code from the control plane gives its mapped sentence, a
 * missing value without a code gives the generic sentence, a held value that the browser age check marks stale gives
 * the stale sentence, otherwise the value is shown as the control plane sent it. The worker-offline and stale
 * reasons, from the server or from the browser, also carry the instant of the last reading. An unavailable tile
 * never carries a number: no zero, no dash, no placeholder, and no value is ever extrapolated.
 *
 * @param reading The current readings with the moment the browser received them.
 * @param nowMs The current time on the `performance.now()` timeline, used for the browser age check.
 * @param locale The locale of every number format.
 * @returns The four tile presentations.
 */
export function metricTiles(
  reading: ServerMetricsReading,
  nowMs: number,
  locale: string,
): MetricTilePresentation[] {
  const context: TileContext = {
    reading,
    stale: isReadingStale(reading, nowMs),
    lastReading: lastReadingOf(reading),
  };
  return [
    { id: "uptime", termKey: "Server_Metrics_Tile_Uptime", value: uptimeValue(context) },
    {
      id: "players",
      termKey: "Server_Metrics_Tile_Players",
      value: playersValue(context, locale),
    },
    { id: "cpu", termKey: "Server_Metrics_Tile_Cpu", value: cpuValue(context, locale) },
    { id: "memory", termKey: "Server_Metrics_Tile_Memory", value: memoryValue(context, locale) },
  ];
}

/** The metrics the history charts and tables show. */
export type ChartMetric = "cpu" | "memory" | "players";

/** One accepted point of a chart series: the bucket time and the average, lowest and highest value. */
export interface ChartPoint {
  /** The bucket start in epoch milliseconds. */
  t: number;
  /** The average value of the bucket. */
  avg: number;
  /** The lowest value of the bucket. */
  min: number;
  /** The highest value of the bucket. */
  max: number;
}

/** One bucket of a series before the gap rule is applied; a null value is a bucket without a reading. */
export interface SeriesPoint {
  /** The bucket start in epoch milliseconds. */
  t: number;
  /** The aggregate of the bucket for this metric, or null when the bucket has no reading for it. */
  value: ServerMetricAggregate | null;
}

/** One row of the table view: a reading or one stretch without readings. */
export type TableRow =
  | {
      /** A bucket that has a reading. */
      kind: "row";
      /** The reading. */
      point: ChartPoint;
    }
  | {
      /** A stretch without readings. */
      kind: "gap";
      /** The start of the stretch in epoch milliseconds. */
      from: number;
      /** The end of the stretch in epoch milliseconds. */
      to: number;
    };

/** The keys of the range chips, in display order. */
export const metricRanges: readonly ServerMetricsRange[] = ["1h", "24h", "7d"];

/** The chip label key of each range. */
export const rangeLabelKeys: Readonly<Record<ServerMetricsRange, TranslationKey>> = {
  "1h": "Server_Metrics_Range_Hour1",
  "24h": "Server_Metrics_Range_Hours24",
  "7d": "Server_Metrics_Range_Days7",
};

/** The key of the phrase each range is spoken as, for example "last hour". */
export const rangeTextKeys: Readonly<Record<ServerMetricsRange, TranslationKey>> = {
  "1h": "Server_Metrics_RangeText_Hour1",
  "24h": "Server_Metrics_RangeText_Hours24",
  "7d": "Server_Metrics_RangeText_Days7",
};

/** The title key of each chart and table. */
export const chartTitleKeys: Readonly<Record<ChartMetric, TranslationKey>> = {
  cpu: "Server_Metrics_Chart_Cpu",
  memory: "Server_Metrics_Chart_Memory",
  players: "Server_Metrics_Chart_Players",
};

/** The three charts in display order. */
export const chartMetrics: readonly ChartMetric[] = ["cpu", "memory", "players"];

/** The length of the span of each range in milliseconds. */
const rangeSpansMs: Readonly<Record<ServerMetricsRange, number>> = {
  "1h": 3_600_000,
  "24h": 86_400_000,
  "7d": 604_800_000,
};

/**
 * Returns the span a range covers.
 *
 * @param range The history range.
 * @returns The span in milliseconds.
 */
export function rangeSpanMs(range: ServerMetricsRange): number {
  return rangeSpansMs[range];
}

/**
 * Returns the length of one history bucket of a range.
 *
 * @param range The history range.
 * @returns 15 000 for `1h`, 60 000 for `24h` and 300 000 for `7d`.
 */
export function rangeBucketMs(range: ServerMetricsRange): number {
  return historyBucketMs(range);
}

/**
 * Reads a range code of the API, falling back when the code is not one of the three known ranges.
 *
 * @param code The range code of a history response.
 * @param fallback The range to use for an unknown code.
 * @returns The known range.
 */
export function parseRange(code: string, fallback: ServerMetricsRange): ServerMetricsRange {
  return code === "1h" || code === "24h" || code === "7d" ? code : fallback;
}

/**
 * Splits a series into the runs that may be drawn as one line.
 *
 * A run ends where a bucket has no reading for the metric (a null value) and where the distance to the previous
 * bucket is more than twice the bucket length. Nothing is interpolated, no point is inserted and a null never
 * becomes zero. A run of one point is kept as its own segment; the chart draws it as a marker, not a line. This is
 * the only place that decides where a line breaks.
 *
 * @param points The buckets in ascending time order.
 * @param bucketMs The bucket length of the served tier in milliseconds.
 * @returns The runs of accepted points in time order; empty when no bucket has a reading.
 */
export function buildSegments(
  points: ReadonlyArray<SeriesPoint>,
  bucketMs: number,
): ChartPoint[][] {
  const segments: ChartPoint[][] = [];
  let current: ChartPoint[] = [];
  let last: number | null = null;
  for (const point of points) {
    const broken = point.value === null || (last !== null && point.t - last > 2 * bucketMs);
    if (broken && current.length > 0) {
      segments.push(current);
      current = [];
    }
    if (point.value !== null) {
      current.push({
        t: point.t,
        avg: point.value.average,
        min: point.value.minimum,
        max: point.value.maximum,
      });
    }
    last = point.t;
  }
  if (current.length > 0) segments.push(current);
  return segments;
}

/**
 * Reads the series of one metric out of a history response.
 *
 * @param metric The metric.
 * @param history The history response.
 * @returns One entry per bucket with a valid start time, in the order the API sent them.
 */
export function metricSeries(
  metric: ChartMetric,
  history: GameServerMetricsHistory,
): SeriesPoint[] {
  const field = metric === "memory" ? "memoryUsed" : metric;
  return history.points.flatMap((point) => {
    const t = Date.parse(point.bucketStart);
    return Number.isNaN(t) ? [] : [{ t, value: point[field] }];
  });
}

/**
 * Rounds a value up to a nice number: 1, 2 or 5 times a power of ten.
 *
 * @param value The value to round up.
 * @returns The nice number that is at least the value; 1 for a value of zero or less.
 */
export function niceCeiling(value: number): number {
  if (!Number.isFinite(value) || value <= 0) return 1;
  const base = 10 ** Math.floor(Math.log10(value));
  const fraction = value / base;
  const step = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
  return step * base;
}

/** The highest `max` of a metric over all buckets with a reading, or zero when there is none. */
function highestMaximum(metric: ChartMetric, history: GameServerMetricsHistory): number {
  let highest = 0;
  for (const { value } of metricSeries(metric, history)) {
    if (value !== null && value.maximum > highest) highest = value.maximum;
  }
  return highest;
}

/**
 * Returns the fixed y domain of one chart.
 *
 * CPU is 0 to 100 percent. Memory is 0 to the machine's total memory, or, when the total is unknown, 0 to the nice
 * ceiling of the highest maximum counted in gibibytes. Players is 0 to the nice ceiling of the larger of the
 * capacity and the highest maximum, made even so the middle tick is a whole number. A point above the domain is
 * clamped by the chart, never changed here.
 *
 * @param metric The metric.
 * @param history The history response, which carries the capacity and the memory total.
 * @returns The lower and upper bound.
 */
export function yDomain(metric: ChartMetric, history: GameServerMetricsHistory): [number, number] {
  if (metric === "cpu") return [0, 100];
  if (metric === "memory") {
    const total = history.memoryTotalBytes;
    if (total !== null && total !== undefined && total > 0) return [0, total];
    const highest = highestMaximum("memory", history);
    return [0, niceCeiling(highest / bytesPerGibibyte) * bytesPerGibibyte];
  }
  const capacity = history.playerCapacity ?? 0;
  const ceiling = niceCeiling(Math.max(capacity, highestMaximum("players", history)));
  return [0, Math.max(2, Math.ceil(ceiling / 2) * 2)];
}

/**
 * Returns evenly spaced time ticks including both ends of the range.
 *
 * @param from The start of the x domain in epoch milliseconds.
 * @param to The end of the x domain in epoch milliseconds.
 * @param count Four ticks at or above the chart minimum width, three below.
 * @returns The tick times in ascending order.
 */
export function xTicks(from: number, to: number, count: 3 | 4): number[] {
  return Array.from({ length: count }, (_, index) => from + ((to - from) * index) / (count - 1));
}

/**
 * Returns the three horizontal gridline values of a domain.
 *
 * @param domain The lower and upper bound.
 * @returns The bottom, middle and top value.
 */
export function yTicks(domain: [number, number]): [number, number, number] {
  return [domain[0], (domain[0] + domain[1]) / 2, domain[1]];
}

/**
 * Selects the point with the largest maximum, which is a selection and never arithmetic.
 *
 * @param segments The runs of a series.
 * @returns The point with the largest `max`, the earliest one on a tie; null when there is no point.
 */
export function highestPoint(
  segments: ReadonlyArray<ReadonlyArray<ChartPoint>>,
): ChartPoint | null {
  let best: ChartPoint | null = null;
  for (const segment of segments) {
    for (const point of segment) {
      if (best === null || point.max > best.max) best = point;
    }
  }
  return best;
}

/**
 * Tells whether the plotted range contains an empty stretch: a broken line or an old last point that leaves the
 * right edge empty.
 *
 * @param segments The runs of a series.
 * @param bucketMs The bucket length in milliseconds.
 * @param nowMs The right edge of the plot in epoch milliseconds.
 * @returns True when the gap note applies.
 */
export function hasOpenGap(
  segments: ReadonlyArray<ReadonlyArray<ChartPoint>>,
  bucketMs: number,
  nowMs: number,
): boolean {
  if (segments.length === 0) return false;
  if (segments.length > 1) return true;
  const only = segments[0] ?? [];
  const last = only[only.length - 1];
  return last !== undefined && nowMs - last.t > 2 * bucketMs;
}

/**
 * Builds the rows of the table view, newest first, with one row for every stretch without readings.
 *
 * A stretch is a bucket without a reading for the metric, a distance of more than twice the bucket length between
 * two buckets, or the distance from the last bucket to now. The rule matches {@link buildSegments}.
 *
 * @param points The buckets in ascending time order.
 * @param bucketMs The bucket length in milliseconds.
 * @param nowMs The current time in epoch milliseconds, which closes a trailing stretch.
 * @returns The rows, newest first; empty when no bucket has a reading.
 */
export function gapRows(
  points: ReadonlyArray<SeriesPoint>,
  bucketMs: number,
  nowMs: number,
): TableRow[] {
  const rows: TableRow[] = [];
  let previous: ChartPoint | null = null;
  let gapFrom: number | null = null;
  for (const point of points) {
    if (point.value === null) {
      if (previous !== null && gapFrom === null) gapFrom = previous.t + bucketMs;
      continue;
    }
    const chartPoint: ChartPoint = {
      t: point.t,
      avg: point.value.average,
      min: point.value.minimum,
      max: point.value.maximum,
    };
    if (previous !== null && (gapFrom !== null || point.t - previous.t > 2 * bucketMs)) {
      rows.push({ kind: "gap", from: gapFrom ?? previous.t + bucketMs, to: point.t });
    }
    gapFrom = null;
    rows.push({ kind: "row", point: chartPoint });
    previous = chartPoint;
  }
  if (previous !== null && (gapFrom !== null || nowMs - previous.t > 2 * bucketMs)) {
    const from: number = gapFrom ?? previous.t + bucketMs;
    if (nowMs > from) rows.push({ kind: "gap", from, to: nowMs });
  }
  return rows.reverse();
}

/**
 * Formats one metric value for an axis, a summary or a table cell.
 *
 * @param metric The metric: percent, gibibytes or a whole number.
 * @param value The value in percent, bytes or players.
 * @param locale The locale of the number format.
 * @returns The formatted value.
 */
export function formatMetricValue(metric: ChartMetric, value: number, locale: string): string {
  if (metric === "cpu") return formatPercent(value, locale);
  if (metric === "memory") return formatGibibytes(value, locale);
  return new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }).format(value);
}

/**
 * Formats the time of a bucket for a summary or a table cell: the clock time, with the short date added when the
 * time is not today or the range is seven days.
 *
 * @param t The time in epoch milliseconds.
 * @param range The range being shown.
 * @returns The formatted time.
 */
export function formatBucketTime(t: number, range: ServerMetricsRange): string {
  if (range !== "7d") return formatInstant(new Date(t).toISOString());
  return new Intl.DateTimeFormat(currentLocale(), {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(t));
}

/**
 * Formats an x axis tick: the clock time for one hour and 24 hours, the short weekday and date for seven days.
 *
 * @param t The tick time in epoch milliseconds.
 * @param range The range being shown.
 * @returns The formatted tick.
 */
export function formatXTick(t: number, range: ServerMetricsRange): string {
  const options: Intl.DateTimeFormatOptions =
    range === "7d"
      ? { weekday: "short", day: "numeric", month: "numeric" }
      : { timeStyle: "short" };
  return new Intl.DateTimeFormat(currentLocale(), options).format(new Date(t));
}
