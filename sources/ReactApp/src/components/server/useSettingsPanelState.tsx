import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type JSX,
  type ReactNode,
} from "react";

/** The `sessionStorage` key under which the ids of the open Settings panels are kept as a JSON array. */
export const settingsPanelStorageKey = "utf.settings.open";

/** What the provider tracks: the open panel ids, the rendered panels and the panels that must stay open. */
interface PanelSnapshot {
  /** The anchor ids of the open panels, including ids of panels that are not rendered right now. */
  readonly open: ReadonlySet<string>;
  /** The anchor ids of the panels that are rendered right now. */
  readonly registered: ReadonlySet<string>;
  /** The anchor ids of the panels that Collapse all must skip. */
  readonly pinned: ReadonlySet<string>;
}

/** Operations and facts of the Settings panel open state. */
export interface SettingsPanelState {
  /**
   * Tells whether a panel is open.
   *
   * @param id The anchor id of the panel.
   * @returns True while the panel is open.
   */
  isOpen(id: string): boolean;
  /**
   * Opens a panel and remembers it for the browser session. The app never closes a panel by itself.
   *
   * @param id The anchor id of the panel.
   */
  open(id: string): void;
  /**
   * Flips a panel, as a member action, and remembers the result for the browser session.
   *
   * @param id The anchor id of the panel.
   */
  toggle(id: string): void;
  /** Opens every rendered panel. */
  expandAll(): void;
  /** Closes every rendered panel that is not pinned. */
  collapseAll(): void;
  /**
   * Announces a rendered panel, so that Expand all and Collapse all know it.
   *
   * @param id The anchor id of the panel.
   * @returns The function that withdraws the panel again.
   */
  register(id: string): () => void;
  /**
   * Pins a panel, which keeps Collapse all from closing it, or releases it.
   *
   * @param id The anchor id of the panel.
   * @param pinned True while the panel must stay open, for example because it holds staged changes.
   */
  setPinned(id: string, pinned: boolean): void;
  /** True while at least one rendered panel that is not pinned is open; drives the Collapse all label. */
  anyCollapsibleOpen: boolean;
}

/**
 * Reads the stored open panel ids.
 *
 * Whatever is stored may have been written by any script of the origin, so only a JSON array of strings is accepted.
 * An unreadable value, a value of another shape and an unavailable storage all mean that every panel is collapsed.
 *
 * @returns The stored ids, or an empty set.
 */
function readOpenIds(): ReadonlySet<string> {
  try {
    const stored = sessionStorage.getItem(settingsPanelStorageKey);
    if (stored === null) return new Set();
    const parsed: unknown = JSON.parse(stored);
    if (!Array.isArray(parsed)) return new Set();
    return new Set(parsed.filter((entry): entry is string => typeof entry === "string"));
  } catch {
    return new Set();
  }
}

/**
 * Stores the open panel ids. A storage that refuses the write only costs the session memory.
 *
 * @param ids The anchor ids of the open panels.
 */
function writeOpenIds(ids: ReadonlySet<string>): void {
  try {
    sessionStorage.setItem(settingsPanelStorageKey, JSON.stringify([...ids]));
  } catch {
    // The state stays correct in memory; only the memory across a reload is lost.
  }
}

/**
 * Returns a copy of a set with one member added or removed, or the same set when nothing changes.
 *
 * @param source The set to start from.
 * @param id The member to add or remove.
 * @param present Whether the member should be in the result.
 * @returns A new set, or `source` itself when it already had the wanted state.
 */
function withMember(
  source: ReadonlySet<string>,
  id: string,
  present: boolean,
): ReadonlySet<string> {
  if (source.has(id) === present) return source;
  const next = new Set(source);
  if (present) next.add(id);
  else next.delete(id);
  return next;
}

const SettingsPanelStateContext = createContext<SettingsPanelState | undefined>(undefined);

/**
 * Owns which Settings panels are open.
 *
 * Every panel starts collapsed on the first visit of a browser session. The open ids are kept in `sessionStorage`
 * under {@link settingsPanelStorageKey} as a JSON array of anchor ids and nothing else, never in the URL.
 *
 * @param props.children The Settings section content.
 */
export function SettingsPanelStateProvider({ children }: { children: ReactNode }): JSX.Element {
  const [snapshot, setSnapshot] = useState<PanelSnapshot>(() => ({
    open: readOpenIds(),
    registered: new Set(),
    pinned: new Set(),
  }));
  // The latest snapshot, so two calls in one tick (two effects opening two panels) both see each other's result.
  const latest = useRef(snapshot);

  const update = useCallback((change: (current: PanelSnapshot) => PanelSnapshot) => {
    const before = latest.current;
    const next = change(before);
    if (next === before) return;
    latest.current = next;
    setSnapshot(next);
    if (next.open !== before.open) writeOpenIds(next.open);
  }, []);

  const open = useCallback(
    (id: string) => update((current) => ({ ...current, open: withMember(current.open, id, true) })),
    [update],
  );
  const toggle = useCallback(
    (id: string) =>
      update((current) => ({
        ...current,
        open: withMember(current.open, id, !current.open.has(id)),
      })),
    [update],
  );
  const expandAll = useCallback(
    () =>
      update((current) => {
        const next = new Set(current.open);
        for (const id of current.registered) next.add(id);
        return next.size === current.open.size ? current : { ...current, open: next };
      }),
    [update],
  );
  const collapseAll = useCallback(
    () =>
      update((current) => {
        const next = new Set(current.open);
        for (const id of current.registered) if (!current.pinned.has(id)) next.delete(id);
        return next.size === current.open.size ? current : { ...current, open: next };
      }),
    [update],
  );
  const register = useCallback(
    (id: string) => {
      update((current) => ({
        ...current,
        registered: withMember(current.registered, id, true),
      }));
      return () =>
        update((current) => ({
          ...current,
          registered: withMember(current.registered, id, false),
        }));
    },
    [update],
  );
  const setPinned = useCallback(
    (id: string, pinned: boolean) =>
      update((current) => ({ ...current, pinned: withMember(current.pinned, id, pinned) })),
    [update],
  );

  const value = useMemo<SettingsPanelState>(() => {
    let anyCollapsibleOpen = false;
    for (const id of snapshot.registered) {
      if (snapshot.open.has(id) && !snapshot.pinned.has(id)) anyCollapsibleOpen = true;
    }
    return {
      isOpen: (id) => snapshot.open.has(id),
      open,
      toggle,
      expandAll,
      collapseAll,
      register,
      setPinned,
      anyCollapsibleOpen,
    };
  }, [snapshot, open, toggle, expandAll, collapseAll, register, setPinned]);

  return (
    <SettingsPanelStateContext.Provider value={value}>
      {children}
    </SettingsPanelStateContext.Provider>
  );
}

/**
 * Reads the open state of the surrounding Settings section.
 *
 * @returns The open state and the operations that change it.
 * @throws Error When called outside {@link SettingsPanelStateProvider}.
 */
export function useSettingsPanelState(): SettingsPanelState {
  const value = useContext(SettingsPanelStateContext);
  if (!value)
    throw new Error("useSettingsPanelState must be used inside SettingsPanelStateProvider.");
  return value;
}
