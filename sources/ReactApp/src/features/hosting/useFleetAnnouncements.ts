import { useState } from "react";
import { useTranslation } from "react-i18next";
import { diffFleetStates, type FleetStateChange } from "@/features/hosting/fleetModel";
import type { GameServer } from "@/features/hosting/types";

/** Number of changed servers that get their own sentence before the rest is summarized. */
const announcedServerLimit = 3;

/** The fleet snapshot the current announcement was computed against, plus the changes it produced. */
interface AnnouncementState {
  /** The fleet snapshot seen last; undefined before the first data arrived. */
  servers: GameServer[] | undefined;
  /** The fleet query's `dataUpdatedAt` seen last. */
  updatedAt: number;
  /** The announced changes between the snapshot before `servers` and `servers`. */
  changes: readonly FleetStateChange[];
}

/** Shared empty change list so an unchanged announcement keeps a stable value. */
const noChanges: readonly FleetStateChange[] = [];

/**
 * Builds the polite live region message for fleet state changes that arrived through a refetch (UI-SPEC
 * "State-change announcements").
 *
 * The hook compares consecutive fleet snapshots with `diffFleetStates` and announces one sentence per changed
 * server, up to three, followed by one summary sentence for the rest. Nothing is announced for the first load, for
 * servers that appear or disappear, for a refetch whose data did not change, or for a change the viewer caused:
 * a change is skipped when its server was marked submitted at or after the previous snapshot landed. Every
 * successful fetch replaces the message, so a refetch without changes clears it. The snapshot comparison runs
 * during render under a guard (the "adjust state when a prop changed" pattern), never in an effect, and the hook
 * never moves focus. The message is rebuilt on every render, so a language switch translates it immediately.
 * Server titles are passed as interpolation values and must be rendered as text.
 *
 * @param servers The current fleet snapshot, or undefined while the fleet has not loaded.
 * @param dataUpdatedAt The fleet query's `dataUpdatedAt` timestamp in milliseconds.
 * @param markedAt The `dataUpdatedAt` recorded for each server the viewer submitted, from `useFleetSubmitted`.
 * @returns The message for the polite live region, or an empty string when there is nothing to announce.
 */
export function useFleetAnnouncements(
  servers: GameServer[] | undefined,
  dataUpdatedAt: number,
  markedAt: ReadonlyMap<string, number>,
): string {
  const { t } = useTranslation();
  const [state, setState] = useState<AnnouncementState>(() => ({
    servers,
    updatedAt: dataUpdatedAt,
    changes: noChanges,
  }));

  let current = state;
  if (servers !== state.servers || dataUpdatedAt !== state.updatedAt) {
    const previousServers = state.servers;
    const previousUpdatedAt = state.updatedAt;
    let changes = noChanges;
    if (servers !== previousServers && previousServers !== undefined && servers !== undefined) {
      const announced = diffFleetStates(previousServers, servers).filter((change) => {
        const marked = markedAt.get(change.serverId);
        return marked === undefined || marked < previousUpdatedAt;
      });
      if (announced.length > 0) changes = announced;
    }
    current = { servers, updatedAt: dataUpdatedAt, changes };
    // Adjust state while rendering (no effect) because the fleet snapshot changed.
    setState(current);
  }

  if (current.changes.length === 0) return "";
  const sentences = current.changes
    .slice(0, announcedServerLimit)
    .map((change) =>
      t("Server_Fleet_Changed", { name: change.name, state: t(change.stateLabelKey) }),
    );
  const remaining = current.changes.length - announcedServerLimit;
  if (remaining > 0) sentences.push(t("Server_Fleet_ChangedMore", { count: remaining }));
  return sentences.join(" ");
}
