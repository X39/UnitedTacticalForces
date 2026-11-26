import { fleetFilters } from "@/features/hosting/fleetModel";
import { workspaceTabIds } from "@/features/hosting/workspaceSections";

/**
 * The accepted values of one query parameter.
 *
 * - A list is the closed set of accepted values.
 * - `"free"` is any text; the reading code is responsible for trimming, capping or rejecting it.
 * - `"guid"` is one GUID.
 * - `"local-path"` is a local React path that went through `localReactReturnPath`; an absolute or protocol-relative
 *   URL is never accepted.
 */
export type ParameterSpec = readonly string[] | "free" | "guid" | "local-path";

/** The URL state one route reads and writes. */
export interface RouteUrlContract {
  /** The query parameters of the route and the values each one accepts. */
  query: Readonly<Record<string, ParameterSpec>>;
  /** The hash anchors of the route: exact ids, or patterns for generated ids. */
  hash: readonly (string | RegExp)[];
}

/**
 * The ids of the server actions that a `?action=` deep link can name.
 *
 * Each id has copy in the action vocabulary; the cost confirmation dialog still guards every deep-linked action.
 */
export const serverActionIds: readonly string[] = [
  "start",
  "stop",
  "terminate",
  "restart",
  "install",
  "update",
  "collect-logs",
];

/** The values of `?area=` of the server Files section; `persistent` is the default and is not written. */
export const filesAreaIds: readonly string[] = ["persistent", "live"];

/** The ids of the sections of a configuration profile workspace, selected by `?tab=`. */
export const configurationProfileTabIds: readonly string[] = [
  "configuration",
  "revisions",
  "access",
];

/** The kinds of cost owner that the finance list can be narrowed to with `?kind=`. */
export const financeOwnerKindIds: readonly string[] = ["User", "Verein", "ExternalOrganization"];

/** The account states that the finance list can be narrowed to with `?status=`. */
export const financeStatusIds: readonly string[] = ["active", "frozen"];

/** The views of the upcoming events page, selected by `?mode=`. */
export const upcomingEventsModeIds: readonly string[] = ["board", "calendar"];

/** The accepted value of `?slotSetup=` on the event editor after a create with a failed slot setup. */
export const slotSetupIds: readonly string[] = ["failed"];

/** The anchor id that opens the automation panel of the server Settings section. */
export const serverAutomationAnchor = "server-automation";

/**
 * The anchor ids of every collapsible panel of the server Settings section. The location hash `#<id>` opens the panel,
 * so a deep link to any Settings panel is part of the URL contract.
 */
export const serverSettingsPanelAnchors: readonly string[] = [
  serverAutomationAnchor,
  "server-mods",
  "server-configuration",
  "server-credentials",
  "server-access",
  "server-cost-owner",
  "server-record",
];

/** Matches the generated anchor ids of activity timeline entries (`activity-<type>-<primaryKey>`). */
export const activityEntryAnchorPattern = /^activity-(job|log|audit)-.+$/;

/**
 * The closed list of URL state the app reads and writes, keyed by the route ids of `routes.json`.
 *
 * A route id that is not listed accepts no query parameters and no hash anchors. The pathname patterns stay in the
 * route manifest; this contract adds what the manifest cannot express. `routes.json` and its certification flags
 * are not changed by it. The tests in `tests/app` keep the code and this list equal in both directions.
 */
export const urlParameters: Readonly<Record<string, RouteUrlContract>> = {
  server: {
    query: {
      tab: workspaceTabIds,
      action: serverActionIds,
      path: "free",
      area: filesAreaIds,
    },
    hash: [...serverSettingsPanelAnchors, activityEntryAnchorPattern],
  },
  servers: {
    query: { q: "free", filter: fleetFilters },
    hash: [],
  },
  "server-cost-alerts": {
    query: { owner: "guid" },
    hash: [],
  },
  "configuration-profile": {
    query: { tab: configurationProfileTabIds },
    hash: [],
  },
  "command-hosting-finance": {
    query: {
      search: "free",
      kind: financeOwnerKindIds,
      status: financeStatusIds,
      page: "free",
    },
    hash: [],
  },
  "command-event-edit": {
    query: { slotSetup: slotSetupIds },
    hash: [],
  },
  "authentication-action": {
    query: { returnUrl: "local-path" },
    hash: [],
  },
  "events-upcoming": {
    query: { mode: upcomingEventsModeIds },
    hash: [],
  },
};

/**
 * Tells whether a query parameter is declared for a route.
 *
 * @param routeId The route id from `routes.json`.
 * @param key The query parameter name.
 * @returns True when the contract of the route declares the key; false for an unlisted route.
 */
export function isDeclaredQueryKey(routeId: string, key: string): boolean {
  if (!Object.hasOwn(urlParameters, routeId)) return false;
  return Object.hasOwn(urlParameters[routeId]!.query, key);
}
