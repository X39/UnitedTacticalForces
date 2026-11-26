import { useEffect, useState } from "react";

/**
 * Returns the current time in epoch milliseconds and refreshes it on a coarse interval.
 *
 * Use it to keep relative times and age checks current without a per-frame timer. The interval runs only while
 * `enabled` is true and is cleared when the interval or flag changes and on unmount; a disabled clock keeps its last
 * value.
 *
 * @param intervalMs Refresh interval in milliseconds.
 * @param enabled Whether the clock ticks; defaults to true.
 */
export function useNow(intervalMs: number, enabled = true): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!enabled) return undefined;
    const timer = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(timer);
  }, [intervalMs, enabled]);

  return now;
}
