import { useCallback, useEffect, useMemo, useRef, useState } from "react";

/** The submitted-card state of the fleet page, as returned by {@link useFleetSubmitted}. */
export interface FleetSubmitted {
  /** Primary keys of the servers whose card shows its working state because an accepted action awaits fresh data. */
  submittedIds: ReadonlySet<string>;
  /**
   * The fleet query's `dataUpdatedAt` that was current when each server was marked. The announcements hook uses it
   * to recognize changes the viewer caused. Marks older than the current data are pruned on the next mark.
   */
  markedAt: ReadonlyMap<string, number>;
  /**
   * Marks a server as submitted. Call it from the confirmation's `onSubmitted` callback, which runs once the API
   * accepted the action and before the fleet query is invalidated. The function keeps its identity across renders.
   *
   * @param serverId Primary key of the server whose action was accepted.
   */
  markSubmitted(serverId: string): void;
}

/** Shared empty map used as the initial state so the first render allocates nothing. */
const noMarks: ReadonlyMap<string, number> = new Map<string, number>();

/**
 * Tracks the fleet cards whose confirmed action awaits the next fleet fetch (D-12).
 *
 * A server is submitted from the moment it was marked until the fleet query's `dataUpdatedAt` advances past the
 * value recorded at the mark, and never while the query is in error. Because `dataUpdatedAt` advances on every
 * successful fetch, even when the returned data is structurally equal, a card falls back to its original action
 * when the refetch changed nothing, so it can never stay stuck. Clearing is derived during render: there is no
 * effect that clears anything and no timer. Servers marked independently never affect each other.
 *
 * @param dataUpdatedAt The fleet query's `dataUpdatedAt` timestamp in milliseconds.
 * @param isError Whether the fleet query is currently in error.
 * @returns The submitted servers, the mark timestamps and the stable mark function.
 */
export function useFleetSubmitted(dataUpdatedAt: number, isError: boolean): FleetSubmitted {
  const [marks, setMarks] = useState<ReadonlyMap<string, number>>(noMarks);
  const latestUpdatedAt = useRef(dataUpdatedAt);

  useEffect(() => {
    latestUpdatedAt.current = dataUpdatedAt;
  });

  const markSubmitted = useCallback((serverId: string) => {
    const current = latestUpdatedAt.current;
    setMarks((previous) => {
      const next = new Map<string, number>();
      for (const [id, recorded] of previous) {
        // A mark older than the current data can no longer explain a future change; drop it.
        if (recorded >= current) next.set(id, recorded);
      }
      next.set(serverId, current);
      return next;
    });
  }, []);

  const submittedIds = useMemo<ReadonlySet<string>>(() => {
    const ids = new Set<string>();
    if (isError) return ids;
    for (const [id, recorded] of marks) {
      if (dataUpdatedAt <= recorded) ids.add(id);
    }
    return ids;
  }, [marks, dataUpdatedAt, isError]);

  return { submittedIds, markedAt: marks, markSubmitted };
}
