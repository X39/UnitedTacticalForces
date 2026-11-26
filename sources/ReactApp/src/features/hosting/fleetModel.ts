import { currentLocale } from "@/app/format";
import type { TranslationKey } from "@/app/localizationTypes";
import { ownerFor } from "@/components/server/costConfirmationModel";
import { deriveServerState, type DerivedServerState } from "@/features/hosting/serverState";
import type { GameServer, ServerAction } from "@/features/hosting/types";

/**
 * The filters of the fleet page.
 *
 * `all` shows every visible server, `mine` the servers the viewer created or owns the cost of, `running` the
 * servers that cost money now or may still cost money, and `off` the servers whose derived state is off.
 */
export type FleetFilter = "all" | "mine" | "running" | "off";

/** Every fleet filter in display order; `all` is the default. */
export const fleetFilters: readonly FleetFilter[] = ["all", "mine", "running", "off"];

/** The fleet view state that lives in the URL. */
export interface FleetViewParams {
  /** The trimmed search text, at most {@link fleetSearchMaxLength} characters; empty matches everything. */
  q: string;
  /** The selected filter. */
  filter: FleetFilter;
}

/** Maximum number of characters of the search text kept from the URL or the search field. */
export const fleetSearchMaxLength = 200;

/** The filters that are written to the URL; the default `all` is omitted. */
const nonDefaultFilters: ReadonlySet<string> = new Set<FleetFilter>(["mine", "running", "off"]);

/** Trims a search text and caps it without leaving half of a surrogate pair or trailing whitespace. */
function normalizeQuery(value: string): string {
  let capped = value.trim().slice(0, fleetSearchMaxLength);
  const last = capped.charCodeAt(capped.length - 1);
  if (last >= 0xd800 && last <= 0xdbff) capped = capped.slice(0, -1);
  return capped.trimEnd();
}

/**
 * Reads the fleet view state from URL search parameters.
 *
 * The values come from shared links and the address bar, so they are untrusted: the filter is whitelisted and
 * anything unknown falls back to `all`; the query is trimmed and capped at {@link fleetSearchMaxLength}.
 *
 * @param params The current URL search parameters.
 * @returns The sanitized view state.
 */
export function parseFleetSearchParams(params: URLSearchParams): FleetViewParams {
  const filter = params.get("filter") ?? "";
  return {
    q: normalizeQuery(params.get("q") ?? ""),
    filter: nonDefaultFilters.has(filter) ? (filter as FleetFilter) : "all",
  };
}

/**
 * Writes fleet view state into a copy of URL search parameters.
 *
 * Only the provided members change. An empty query removes `q` and the default filter removes `filter`, so the
 * URL carries only state that differs from the default. Every other parameter keeps its value and position.
 *
 * @param current The current URL search parameters; not modified.
 * @param next The members of the view state to write.
 * @returns A new parameter instance with the update applied.
 */
export function writeFleetSearchParams(
  current: URLSearchParams,
  next: Partial<FleetViewParams>,
): URLSearchParams {
  const result = new URLSearchParams(current);
  if (next.q !== undefined) {
    const q = normalizeQuery(next.q);
    if (q) result.set("q", q);
    else result.delete("q");
  }
  if (next.filter !== undefined) {
    if (nonDefaultFilters.has(next.filter)) result.set("filter", next.filter);
    else result.delete("filter");
  }
  return result;
}

/**
 * Folds a text for case- and diacritic-insensitive matching.
 *
 * @param value The text to fold.
 * @param locale The locale whose lower-case rules apply, for example `de-DE`.
 * @returns The text decomposed, without combining marks, in lower case.
 */
export function foldSearchText(value: string, locale: string): string {
  return value
    .normalize("NFD")
    .replace(/\p{M}+/gu, "")
    .toLocaleLowerCase(locale);
}

/**
 * Builds the folded search haystack of a server.
 *
 * The cost owner is indexed only when it is a real name; the API placeholders for a hidden or unknown owner are
 * left out, so a search for "assigned" or "owner" never matches every server whose owner the viewer cannot see.
 * Fields are joined with a line break so a term never matches across two fields.
 */
function searchHaystack(server: GameServer, locale: string): string {
  const owner = ownerFor(server.costOwnerName);
  const ownerText = owner.kind === "name" ? (owner.name ?? "") : "";
  return foldSearchText([server.title, server.gameIdentifier, ownerText].join("\n"), locale);
}

/**
 * Filters servers by a free-text query over title, game identifier and the real cost owner name.
 *
 * Matching is case-insensitive, diacritic-insensitive and locale-aware. The query is split on whitespace and
 * every term must occur (AND), in any order and in any of the fields.
 *
 * @param servers The servers to search.
 * @param query The search text; blank or whitespace-only matches everything.
 * @param locale The locale used to fold both sides, normally `currentLocale()`.
 * @returns The matching servers in input order, or the input array itself when the query has no terms.
 */
export function searchServers(servers: GameServer[], query: string, locale: string): GameServer[] {
  const terms = foldSearchText(query, locale)
    .split(/\s+/)
    .filter((term) => term.length > 0);
  if (terms.length === 0) return servers;
  return servers.filter((server) => {
    const haystack = searchHaystack(server, locale);
    return terms.every((term) => haystack.includes(term));
  });
}

/** Compares two ids ignoring case, the way the API compares GUIDs. */
function sameId(left: string | null | undefined, right: string): boolean {
  return typeof left === "string" && left.toLowerCase() === right.toLowerCase();
}

/** Decides filter membership from an already derived state, so counting derives each server once. */
function matchesDerived(
  server: GameServer,
  derived: DerivedServerState,
  filter: FleetFilter,
  meId: string | undefined,
): boolean {
  switch (filter) {
    case "all":
      return true;
    case "mine":
      return !!meId && (sameId(server.createdById, meId) || sameId(server.costOwnerUserId, meId));
    case "running":
      return derived.cost !== "none";
    case "off":
      return derived.state === "off";
  }
}

/**
 * Tells whether a server belongs to a fleet filter.
 *
 * `mine` is true when the viewer created the server or is its user cost owner (ids compared ignoring case), and
 * false for everyone when the viewer id is unknown. `running` is true when the derived cost is live or maybe.
 * `off` is true only when the derived state is off, so a server that may still cost is never in it.
 *
 * @param server The server to test.
 * @param filter The filter.
 * @param meId The signed-in user id, or undefined when unknown.
 * @returns Whether the server is listed under the filter.
 */
export function matchesFilter(
  server: GameServer,
  filter: FleetFilter,
  meId: string | undefined,
): boolean {
  return matchesDerived(server, deriveServerState(server), filter, meId);
}

/**
 * Counts the servers of every fleet filter.
 *
 * Pass the search result, so each chip shows how many servers selecting it would list.
 *
 * @param servers The servers to count, normally the search result.
 * @param meId The signed-in user id, or undefined when unknown.
 * @returns The number of servers each filter matches.
 */
export function countFilters(
  servers: GameServer[],
  meId: string | undefined,
): Record<FleetFilter, number> {
  const counts: Record<FleetFilter, number> = { all: 0, mine: 0, running: 0, off: 0 };
  for (const server of servers) {
    const derived = deriveServerState(server);
    for (const filter of fleetFilters) {
      if (matchesDerived(server, derived, filter, meId)) counts[filter] += 1;
    }
  }
  return counts;
}

/** Creates the locale-aware title collator shared by both section orders. */
function titleCollator(): Intl.Collator {
  return new Intl.Collator(currentLocale(), { sensitivity: "base" });
}

/**
 * Orders servers for the Active section: servers that may still cost first, then servers with pending work,
 * then by locale-aware title.
 *
 * @param servers The servers to order; not modified.
 * @returns A new, ordered array.
 */
export function orderFleetServers(servers: GameServer[]): GameServer[] {
  const collator = titleCollator();
  return [...servers].sort(
    (left, right) =>
      Number(Boolean(right.deployment?.possibleCost)) -
        Number(Boolean(left.deployment?.possibleCost)) ||
      Number(right.pendingJobCount > 0) - Number(left.pendingJobCount > 0) ||
      collator.compare(left.title, right.title),
  );
}

/**
 * Splits servers into the Active and Off sections of the fleet page.
 *
 * Membership comes from `deriveServerState` alone: only the derived state `off` lands in Off, every other state
 * lands in Active. Because `off` is reachable only when the server cannot cost money, a server that may still
 * cost is never in Off. Active keeps {@link orderFleetServers}; Off is ordered by locale-aware title.
 *
 * @param servers The servers to split, normally the filtered search result.
 * @returns Both sections, each a new ordered array; every server appears in exactly one.
 */
export function partitionFleet(servers: GameServer[]): { active: GameServer[]; off: GameServer[] } {
  const active: GameServer[] = [];
  const off: GameServer[] = [];
  for (const server of servers) {
    if (deriveServerState(server).state === "off") off.push(server);
    else active.push(server);
  }
  const collator = titleCollator();
  return {
    active: orderFleetServers(active),
    off: off.sort((left, right) => collator.compare(left.title, right.title)),
  };
}

/**
 * The single primary control of a fleet card.
 *
 * `open-only` means the card shows no primary control and Open server becomes its only action. `in-progress`
 * is a disabled control while a change runs; `reason` tells whether pending work (`pending`) or the current state
 * (`state`) explains it. `action` is a lifecycle action from the API with its button variant; `enabled` false
 * means the control is shown disabled with the API's reason.
 */
export type FleetPrimary =
  | { kind: "open-only" }
  | { kind: "in-progress"; reason: "pending" | "state" }
  | { kind: "action"; action: ServerAction; variant: "gold" | "danger"; enabled: boolean };

/** Finds a lifecycle action by id, ignoring case the way the API compares action ids. */
function findAction(server: GameServer, id: "start" | "stop"): ServerAction | undefined {
  return server.actions.find((action) => action.id.toLowerCase() === id);
}

/** Builds the stop primary shared by the running, attention and unconfirmed states. */
function stopPrimary(server: GameServer): FleetPrimary {
  const stop = findAction(server, "stop");
  if (!stop) return { kind: "open-only" };
  return {
    kind: "action",
    action: stop,
    variant: "danger",
    enabled: stop.enabled && !stop.pending,
  };
}

/**
 * Decides the primary control of a fleet card from the derived server state.
 *
 * An off server offers `start` (gold), enabled when the API enables it, it is not pending and launching is not
 * disabled. A running server offers `stop` (danger), enabled when the API enables it and it is not pending.
 * Attention and unconfirmed servers also offer `stop` when the API sends it; because the API enables stop only
 * while the deployment is running, such a stop is usually returned disabled, so the card shows the API's reason
 * beside Open server. Starting, stopping and working servers get an in-progress control. An unknown state, or a
 * state whose needed action is absent, gets Open server only. `terminate` is never selected; it stays in the
 * server workspace.
 *
 * @param server The server of the card.
 * @param derived The derived state of the server; derived from the server when omitted.
 * @returns The primary control decision; no localization is applied.
 */
export function primaryActionFor(
  server: GameServer,
  derived: DerivedServerState = deriveServerState(server),
): FleetPrimary {
  switch (derived.state) {
    case "off": {
      const start = findAction(server, "start");
      if (!start) return { kind: "open-only" };
      return {
        kind: "action",
        action: start,
        variant: "gold",
        enabled: start.enabled && !start.pending && !server.isLaunchDisabled,
      };
    }
    case "running":
    case "attention":
    case "unconfirmed":
      return stopPrimary(server);
    case "starting":
    case "stopping":
    case "working": {
      const pending = server.pendingJobCount > 0 || server.actions.some((action) => action.pending);
      return { kind: "in-progress", reason: pending ? "pending" : "state" };
    }
    case "unknown":
      return { kind: "open-only" };
  }
}

/** Localization key of the stage line for a queued change without a known deployment step. */
const queuedChangeKey: TranslationKey = "Server_Fleet_QueuedChange";

/**
 * Returns the localization key of the stage line a fleet card shows below its state.
 *
 * Starting and stopping servers show their deployment step (the derived `detailKey`), falling back to the queued
 * change text when the step is unknown. Working servers always show the queued change text, because their
 * deployment may be settled and its label would contradict the working badge. Every other state has no stage
 * line. The key is decided from the derived state only, never from the raw API status.
 *
 * @param derived The derived state of the server.
 * @returns The localization key of the stage line, or null when no stage line is shown.
 */
export function fleetStageKey(derived: DerivedServerState): TranslationKey | null {
  switch (derived.state) {
    case "starting":
    case "stopping":
      return derived.detailKey ?? queuedChangeKey;
    case "working":
      return queuedChangeKey;
    default:
      return null;
  }
}

/**
 * Counts the servers that cost money now or may still cost money.
 *
 * @param servers The servers to count, normally the whole fleet.
 * @returns The number of servers whose derived cost is live or maybe.
 */
export function countCostlyServers(servers: GameServer[]): number {
  return servers.reduce(
    (count, server) => count + Number(deriveServerState(server).cost !== "none"),
    0,
  );
}

/** One server whose derived state changed between two fleet snapshots. */
export interface FleetStateChange {
  /** Primary key of the server. */
  serverId: string;
  /** The server title in the newer snapshot. */
  name: string;
  /** Localization key of the newer derived state label. */
  stateLabelKey: TranslationKey;
}

/**
 * Lists the servers whose derived state changed between two fleet snapshots, for polite announcements.
 *
 * Servers that appear only in one snapshot and servers whose derived state stayed the same are not reported.
 *
 * @param previous The older fleet snapshot.
 * @param next The newer fleet snapshot.
 * @returns One change per changed server, in the order of `next`.
 */
export function diffFleetStates(previous: GameServer[], next: GameServer[]): FleetStateChange[] {
  const previousStates = new Map(
    previous.map((server) => [server.primaryKey, deriveServerState(server).state]),
  );
  const changes: FleetStateChange[] = [];
  for (const server of next) {
    const before = previousStates.get(server.primaryKey);
    if (before === undefined) continue;
    const derived = deriveServerState(server);
    if (derived.state !== before) {
      changes.push({
        serverId: server.primaryKey,
        name: server.title,
        stateLabelKey: derived.labelKey,
      });
    }
  }
  return changes;
}
