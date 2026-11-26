import type { UpdateStreamFrame } from "@/app/updateStream";
import type { GameServerMetrics, ServerMetricsRange } from "@/features/hosting/types";

/** Full type name of the .NET wire message that pushes the current metric readings of one server. */
export const metricsMessageType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerMetricsMessage";

/** The result of parsing one metrics frame: the server it belongs to and the readings to cache. */
export interface ParsedMetricsFrame {
  /** The id of the server the readings belong to. */
  serverId: string;
  /** The readings, shaped exactly like the REST response so they can be written into the same cache entry. */
  metrics: GameServerMetrics;
}

/**
 * Returns whether a value is a non-empty string.
 *
 * @param value The untrusted value.
 */
function isText(value: unknown): value is string {
  return typeof value === "string" && value !== "";
}

/**
 * Reads an optional nullable string; an absent property reads as null.
 *
 * @param value The untrusted value.
 * @returns The string or null, or `undefined` when the value has another type.
 */
function optionalText(value: unknown): string | null | undefined {
  if (value === null || value === undefined) return null;
  return typeof value === "string" ? value : undefined;
}

/**
 * Reads an optional nullable instant; an absent property reads as null.
 *
 * @param value The untrusted value.
 * @returns The instant string or null, or `undefined` when the value is not a parseable instant.
 */
function optionalInstant(value: unknown): string | null | undefined {
  if (value === null || value === undefined) return null;
  return typeof value === "string" && !Number.isNaN(Date.parse(value)) ? value : undefined;
}

/**
 * Reads an optional nullable finite number; an absent property reads as null.
 *
 * @param value The untrusted value.
 * @returns The number or null, or `undefined` when the value is not a finite number.
 */
function optionalNumber(value: unknown): number | null | undefined {
  if (value === null || value === undefined) return null;
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

/**
 * Validates one untrusted metrics frame and converts it into the REST shape.
 *
 * Every field is checked: a string server id, a parseable or null as-of instant, a whole `staleAfterSeconds` of at
 * least 1, finite numbers or null for the values, string or null reason codes and parseable or null instants. A
 * reading is either a value or a reason, never both, because the control plane decides which one applies. Extra
 * properties, such as the reference ids the stream serializer adds, are ignored. A frame that fails any check is
 * dropped, because the browser must never show a reading it cannot trust.
 *
 * @param frame The parsed, untrusted frame.
 * @returns The server id and the readings, or null when the frame is not a valid metrics frame.
 */
export function parseMetricsFrame(frame: UpdateStreamFrame): ParsedMetricsFrame | null {
  if (frame.type !== metricsMessageType) return null;
  const value: unknown = frame.value;
  if (typeof value !== "object" || value === null) return null;
  const message = value as Record<string, unknown>;
  const { serverId, staleAfterSeconds } = message;
  if (!isText(serverId)) return null;
  if (
    typeof staleAfterSeconds !== "number" ||
    !Number.isInteger(staleAfterSeconds) ||
    staleAfterSeconds < 1
  ) {
    return null;
  }
  const asOf = optionalInstant(message.asOf);
  const runningSince = optionalInstant(message.runningSince);
  const runningSinceReason = optionalText(message.runningSinceUnavailableReason);
  const cpuPercent = optionalNumber(message.cpuPercent);
  const cpuReason = optionalText(message.cpuUnavailableReason);
  const memoryUsedBytes = optionalNumber(message.memoryUsedBytes);
  const memoryTotalBytes = optionalNumber(message.memoryTotalBytes);
  const memoryReason = optionalText(message.memoryUnavailableReason);
  const playerCount = optionalNumber(message.playerCount);
  const playerCapacity = optionalNumber(message.playerCapacity);
  const playersReason = optionalText(message.playersUnavailableReason);
  if (
    asOf === undefined ||
    runningSince === undefined ||
    runningSinceReason === undefined ||
    cpuPercent === undefined ||
    cpuReason === undefined ||
    memoryUsedBytes === undefined ||
    memoryTotalBytes === undefined ||
    memoryReason === undefined ||
    playerCount === undefined ||
    playerCapacity === undefined ||
    playersReason === undefined
  ) {
    return null;
  }
  if (runningSince !== null && runningSinceReason !== null) return null;
  if (cpuPercent !== null && cpuReason !== null) return null;
  if (memoryUsedBytes !== null && memoryReason !== null) return null;
  if (playerCount !== null && playersReason !== null) return null;
  return {
    serverId,
    metrics: {
      gameServerId: serverId,
      asOf,
      staleAfterSeconds,
      runningSince,
      runningSinceUnavailableReason: runningSinceReason,
      cpuPercent,
      cpuUnavailableReason: cpuReason,
      memoryUsedBytes,
      memoryTotalBytes,
      memoryUnavailableReason: memoryReason,
      playerCount,
      playerCapacity,
      playersUnavailableReason: playersReason,
    },
  };
}

/**
 * Returns whether two readings carry the same facts, ignoring any browser-only stamp on either side.
 *
 * @param left The first reading.
 * @param right The second reading.
 */
function sameReadings(left: GameServerMetrics, right: GameServerMetrics): boolean {
  const keys = Object.keys(left) as (keyof GameServerMetrics)[];
  return keys.every((key) => left[key] === right[key]);
}

/**
 * Reads the as-of time of a reading as epoch milliseconds.
 *
 * @param reading The reading.
 * @returns The instant, or null when the as-of time is null, undefined or does not parse.
 */
function asOfMs(reading: GameServerMetrics): number | null {
  if (reading.asOf === null || reading.asOf === undefined) return null;
  const parsed = Date.parse(reading.asOf);
  return Number.isNaN(parsed) ? null : parsed;
}

/**
 * Returns whether a reading carries at least one value, as opposed to reason codes only.
 *
 * @param reading The reading.
 * @returns True when running since, CPU, memory used or player count is neither null nor undefined.
 */
function holdsAnyValue(reading: GameServerMetrics): boolean {
  return (
    (reading.runningSince ?? null) !== null ||
    (reading.cpuPercent ?? null) !== null ||
    (reading.memoryUsedBytes ?? null) !== null ||
    (reading.playerCount ?? null) !== null
  );
}

/**
 * Tells whether a reading from REST or from a live frame should replace the cached one.
 *
 * Rules, first match wins: with no cached reading the reading is taken; a reading that is older by as-of time and
 * still holds a value is refused, because it is a reordered sample that must never show an old number as live;
 * otherwise the reading is taken exactly when its facts differ from the cached ones.
 *
 * The control plane sends no as-of time only when the active deployment has no snapshot (stopped, provisioning, or
 * a new run before its first sample), so such a reading is the current truth and not an older sample. A reading of
 * reasons only never shows a number, so letting it win can only make tiles unavailable, which is the safe direction.
 * Equal facts keep the cached entry and its receive stamp, so neither a redelivered frame nor a refetch delays the
 * browser age check.
 *
 * @param next The reading that just arrived.
 * @param current The cached reading, if any.
 * @returns True when the arrived reading must be written into the cache.
 */
export function replacesCachedReading(
  next: GameServerMetrics,
  current: GameServerMetrics | undefined,
): boolean {
  if (current === undefined) return true;
  const nextAt = asOfMs(next);
  const currentAt = asOfMs(current);
  if (nextAt !== null && currentAt !== null && nextAt < currentAt && holdsAnyValue(next)) {
    return false;
  }
  return !sameReadings(next, current);
}

/**
 * Returns the length of one history bucket of a range in milliseconds, which is also the shortest interval after
 * which a pushed frame may refetch that range.
 *
 * @param range The history range.
 * @returns 15 000 for `1h`, 60 000 for `24h` and 300 000 for `7d`.
 */
export function historyBucketMs(range: ServerMetricsRange): number {
  if (range === "1h") return 15_000;
  if (range === "24h") return 60_000;
  return 300_000;
}
