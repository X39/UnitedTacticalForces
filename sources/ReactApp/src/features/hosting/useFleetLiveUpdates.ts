import { createContext, useContext } from "react";
import type { UpdateStreamStatus } from "@/app/updateStream";
import type { CostAlert } from "@/features/hosting/types";

/** The live fleet signal state provided by `FleetLiveUpdatesProvider`. */
export interface FleetLiveUpdatesValue {
  /** Connection state of the fleet subscription. */
  status: UpdateStreamStatus;
  /** Incremented once whenever a disconnection episode begins. */
  disconnectEpoch: number;
  /** Reconnects the fleet subscription immediately when it is disconnected. */
  reconnectNow(): void;
  /**
   * Registers a listener that is called once per valid fleet-changed frame with the frame's server id. A throwing
   * listener never affects other listeners or the provider's own invalidation.
   *
   * @param listener Receives the id of the server that changed.
   * @returns The function that unregisters the listener.
   */
  subscribe(listener: (serverId: string) => void): () => void;
  /**
   * Registers a listener that is called once per valid cost alert frame that raised an alert, with the alert and
   * whether the alerts cache already held its id before the frame. Only a frame that was not cached is news to the
   * viewer, so a listener that announces arrivals skips `wasCached`. Alerts loaded by a REST fetch never reach the
   * listener. A throwing listener never affects other listeners or the cache write.
   *
   * @param listener Receives the alert and whether it was already cached.
   * @returns The function that unregisters the listener.
   */
  subscribeCostAlerts(listener: (alert: CostAlert, wasCached: boolean) => void): () => void;
  /**
   * Records alert ids this viewer dismissed in this session, after the API accepted the dismissal. A later raised
   * frame for a recorded id neither re-adds the alert to the cache nor reaches the arrival listeners, so a late or
   * redelivered frame cannot resurrect a dismissed alert or toast for it.
   *
   * @param costAlertIds The ids of the dismissed alerts.
   */
  markCostAlertsDismissed(costAlertIds: readonly string[]): void;
}

/**
 * Context of the live fleet signal. The default is a connected no-op so pages render without the provider, for
 * example in tests or in trees that never subscribe.
 */
export const FleetLiveUpdatesContext = createContext<FleetLiveUpdatesValue>({
  status: "open",
  disconnectEpoch: 0,
  reconnectNow: () => undefined,
  subscribe: () => () => undefined,
  subscribeCostAlerts: () => () => undefined,
  markCostAlertsDismissed: () => undefined,
});

/** Returns the live fleet signal state of the nearest `FleetLiveUpdatesProvider`. */
export function useFleetLiveUpdates(): FleetLiveUpdatesValue {
  return useContext(FleetLiveUpdatesContext);
}
