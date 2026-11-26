import type { UpdateStreamStatus } from "@/app/updateStream";

/** Age in milliseconds above which workspace data counts as old while a job is active. */
export const freshnessStaleAfterMs = 60_000;

/**
 * How current the workspace data is, as shown by the live badge.
 *
 * - `connecting`: the live socket has not opened yet in this page session.
 * - `live`: changes arrive as they happen, or nothing is in flight that could change unnoticed.
 * - `stale` with reason `disconnected`: the socket is down, so signals may be missing.
 * - `stale` with reason `old`: the socket is open, a job is active and the newest data is older than
 *   {@link freshnessStaleAfterMs}.
 */
export type WorkspaceFreshness =
  { state: "connecting" } | { state: "live" } | { state: "stale"; reason: "disconnected" | "old" };

/** Input of {@link deriveFreshness}. */
export interface FreshnessInput {
  /** Connection state of the shared fleet socket. */
  status: UpdateStreamStatus;
  /** Disconnect epoch of the socket; above zero once the socket was disconnected at least once. */
  disconnectEpoch: number;
  /** Time of the newest successful fetch of the jobs and operations data in epoch milliseconds; 0 when none. */
  dataUpdatedAt: number;
  /** The current time in epoch milliseconds, supplied by the caller. */
  now: number;
  /** Whether a lifecycle job is active, see `hasActiveJob`. */
  hasActiveJob: boolean;
}

/**
 * Derives the freshness of the workspace data from the socket state and the data age.
 *
 * `connecting` applies only before the first open: a socket that is connecting again after a disconnection episode
 * is reported as stale (disconnected). With the socket open, data is live when no job is active, because every
 * state change that matters then arrives as a fleet signal, or when the newest data is at most
 * {@link freshnessStaleAfterMs} old. The badge must never claim live for output that only arrives through the
 * fallback poll: while a job is active, data age decides, not the socket alone. The function is pure; it reads no
 * clock and the 3 second anti-flicker delay belongs to the badge.
 *
 * @param input The socket state, data age inputs and active-job flag.
 * @returns The freshness to display.
 */
export function deriveFreshness(input: FreshnessInput): WorkspaceFreshness {
  const { status, disconnectEpoch, dataUpdatedAt, now, hasActiveJob } = input;
  if (status === "connecting") {
    return disconnectEpoch > 0
      ? { state: "stale", reason: "disconnected" }
      : { state: "connecting" };
  }
  if (status === "reconnecting" || status === "unavailable") {
    return { state: "stale", reason: "disconnected" };
  }
  if (!hasActiveJob || dataUpdatedAt <= 0) return { state: "live" };
  return now - dataUpdatedAt <= freshnessStaleAfterMs
    ? { state: "live" }
    : { state: "stale", reason: "old" };
}
