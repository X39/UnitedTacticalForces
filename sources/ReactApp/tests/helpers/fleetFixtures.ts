import type {
  GameServer,
  ServerAction,
  ServerAutomation,
  ServerChangeArea,
} from "@/features/hosting/types";

/** Display order of the well-known lifecycle actions; other ids sort after them. */
const actionOrder: Record<string, number> = { start: 1, stop: 2, terminate: 3 };

/** Deployment statuses in which the fixture assumes no provider cost by default. */
const settledStatuses = new Set(["Stopped", "Terminated"]);

/**
 * Returns the cost impact the API reports for a well-known action id.
 *
 * @param id The action id.
 */
function costImpactFor(id: string): ServerAction["costImpact"] {
  if (id === "start") return "StartsCost";
  if (id === "stop" || id === "terminate") return "StopsCost";
  return "NoCostImpact";
}

/**
 * Builds a complete server action as the API sends it. Every call returns a fresh object.
 *
 * @param id The action id, for example `start`, `stop` or `terminate`.
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns A fully populated action.
 */
export function fleetAction(id: string, overrides: Partial<ServerAction> = {}): ServerAction {
  return {
    id,
    label: id.charAt(0).toUpperCase() + id.slice(1),
    description: "",
    icon: "",
    group: "lifecycle",
    order: actionOrder[id] ?? 10,
    enabled: true,
    unavailableReason: null,
    dangerous: false,
    confirmationText: null,
    confirmationChallenge: null,
    pending: false,
    expectedDurationSeconds: null,
    costImpact: costImpactFor(id),
    costDescription: "",
    ...overrides,
  } as unknown as ServerAction;
}

/** Options of {@link fleetServer}; every option is optional and an omitted option uses the documented default. */
export interface FleetServerOptions {
  /** Index the primary key and default title derive from. Defaults to 1. */
  index?: number | undefined;
  /** Server title. Defaults to `Fleet Server {index}`. */
  title?: string | undefined;
  /** Game identifier. Defaults to `arma3`. */
  gameIdentifier?: string | undefined;
  /** Deployment status; a deployment exists only when a status is passed. */
  status?: string | undefined;
  /** Whether the deployment may still cost money. Defaults to true unless the status is settled. */
  possibleCost?: boolean | undefined;
  /** Number of queued or running lifecycle jobs. Defaults to 0. */
  pendingJobCount?: number | undefined;
  /** Display name of the cost owner. Defaults to `Operations`. */
  costOwnerName?: string | undefined;
  /** User id of a user-kind cost owner the viewer may see. Defaults to null. */
  costOwnerUserId?: string | null | undefined;
  /** User id of the creator. Defaults to a fixed id. */
  createdById?: string | undefined;
  /** Whether launching is disabled. Defaults to false. */
  isLaunchDisabled?: boolean | undefined;
  /** Forces a server without a deployment even when a status is passed. */
  noDeployment?: boolean | undefined;
  /** Actions; defaults to one enabled start action without a deployment, otherwise one enabled stop action. */
  actions?: ServerAction[] | undefined;
  /** Areas whose saved changes wait for the next start; the server reports a pending start when any is passed. */
  pendingStartAreas?: ServerChangeArea[] | undefined;
  /** Ascending cost alert amounts the viewer may see; null (the default) means the viewer may not see them. */
  costAlertThresholds?: number[] | null | undefined;
  /** Whether the viewer may manage the cost alerts of the cost owner. Defaults to null (not visible). */
  canManageCostAlerts?: boolean | null | undefined;
  /** The automation block of the detail read. Defaults to null (the fleet list and older APIs send none). */
  automation?: ServerAutomation | null | undefined;
  /** Whether the viewer may manage the automation. Defaults to null (not visible). */
  canManageAutomation?: boolean | null | undefined;
  /** Whether the viewer may give and remove access to the server. Defaults to false (an older payload omits it). */
  canManageAccess?: boolean | undefined;
  /** Whether the cost owner is frozen. Defaults to null (not visible). */
  costOwnerIsFrozen?: boolean | null | undefined;
  /** When the cost owner took over; the detail read only. Defaults to null (not visible or a list row). */
  costOwnerAssignedAt?: string | null | undefined;
}

/**
 * Builds the automation block of a server as the detail endpoint sends it. Every call returns fresh objects, so
 * tests may mutate the result freely.
 *
 * Defaults to nothing saved (both limits off, no zone, no rules, version 0), an idle-capable game, a server that is
 * not running and no scheduled start needing consent.
 *
 * @param overrides Top-level members that replace the defaults; nothing else changes.
 * @returns A fully populated automation block.
 */
export function serverAutomation(overrides: Partial<ServerAutomation> = {}): ServerAutomation {
  return {
    settings: {
      idleShutdownEnabled: false,
      idleShutdownMinutes: 30,
      maxRuntimeEnabled: false,
      maxRuntimeHours: 6,
      timeZone: null,
      schedules: [],
      version: 0,
    },
    limits: {
      idleMinutesMin: 5,
      idleMinutesMax: 720,
      maxRuntimeHoursMin: 1,
      maxRuntimeHoursMax: 168,
      maxSchedules: 20,
      warningLeadMinutes: 5,
    },
    idleShutdown: { available: true },
    status: {
      idle: { state: "NotRunning", since: null, stopsAt: null },
      maxRuntimeStopsAt: null,
      nextScheduled: null,
      pendingStop: null,
    },
    startConsent: { state: "NotRequired" },
    ...overrides,
  } as ServerAutomation;
}

/**
 * Builds a server as the fleet endpoint returns it. Every call builds fresh objects, so tests may mutate the
 * result freely.
 *
 * @param options Overrides of the documented defaults.
 * @returns A server whose state derives from the passed status, cost and pending options.
 */
export function fleetServer(options: FleetServerOptions = {}): GameServer {
  const index = options.index ?? 1;
  const suffix = index.toString().padStart(12, "0");
  const primaryKey = `f1ee7000-0000-4000-8000-${suffix}`;
  const status = options.noDeployment ? undefined : options.status;
  const costOwnerName = options.costOwnerName ?? "Operations";
  const deployment =
    status === undefined
      ? null
      : {
          primaryKey: `d0000000-0000-4000-8000-${suffix}`,
          status,
          stage: status.toLowerCase(),
          possibleCost: options.possibleCost ?? !settledStatuses.has(status),
          costOwnerId: "c0000000-0000-4000-8000-000000000001",
          costOwnerName,
          resources: [],
        };
  return {
    primaryKey,
    title: options.title ?? `Fleet Server ${index}`,
    gameIdentifier: options.gameIdentifier ?? "arma3",
    runtimeProviderIdentifier: "hetzner",
    createdById: options.createdById ?? "10000000-0000-4000-8000-000000000001",
    costOwnerId: "c0000000-0000-4000-8000-000000000001",
    costOwnerName,
    costOwnerUserId: options.costOwnerUserId ?? null,
    isLaunchDisabled: options.isLaunchDisabled ?? false,
    isArchived: false,
    pendingJobCount: options.pendingJobCount ?? 0,
    timeStampUpdated: "2026-08-13T12:30:00Z",
    changesPendingStartAreas: options.pendingStartAreas ?? [],
    hasChangesPendingStart: (options.pendingStartAreas ?? []).length > 0,
    costAlertThresholds: options.costAlertThresholds ?? null,
    canManageCostAlerts: options.canManageCostAlerts ?? null,
    automation: options.automation ?? null,
    canManageAutomation: options.canManageAutomation ?? null,
    canManageAccess: options.canManageAccess ?? false,
    costOwnerIsFrozen: options.costOwnerIsFrozen ?? null,
    costOwnerAssignedAt: options.costOwnerAssignedAt ?? null,
    deployment,
    actions: options.actions ?? [fleetAction(deployment === null ? "start" : "stop")],
  } as unknown as GameServer;
}
