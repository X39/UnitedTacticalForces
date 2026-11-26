import { useEffect, useState } from "react";
import { useFleetLiveUpdates } from "@/features/hosting/useFleetLiveUpdates";
import { deriveFreshness } from "@/features/hosting/workspaceFreshness";

/** Continuous disconnection in milliseconds after which the stale state appears, so a short blip never flickers. */
const disconnectDelayMs = 3000;

/**
 * Tells whether the shared fleet socket has been disconnected long enough to show it.
 *
 * The result is true only after the connection has been down for 3 seconds in a row. The timer is keyed by the
 * disconnect epoch, so a new disconnection episode waits again and a recovered connection never shows it; a blip
 * shorter than the delay never flips the result. The hook only reads the shared socket state and opens no socket.
 *
 * @returns Whether the socket has been disconnected for 3 seconds in a row in the current episode.
 */
export function useDelayedDisconnect(): boolean {
  const { status, disconnectEpoch } = useFleetLiveUpdates();
  const [shownEpoch, setShownEpoch] = useState<number | null>(null);
  const reported = deriveFreshness({
    status,
    disconnectEpoch,
    dataUpdatedAt: 0,
    now: 0,
    hasActiveJob: false,
  });
  const disconnected = reported.state === "stale" && reported.reason === "disconnected";

  useEffect(() => {
    if (!disconnected) return undefined;
    const timer = window.setTimeout(() => setShownEpoch(disconnectEpoch), disconnectDelayMs);
    return () => window.clearTimeout(timer);
  }, [disconnected, disconnectEpoch]);

  return disconnected && shownEpoch === disconnectEpoch;
}
