import type { TranslationKey } from "@/app/localizationTypes";
import type { components } from "@/generated/api";
import type { GameServer } from "@/features/hosting/types";

/** Deployment status values the API can currently report. */
type EDeploymentStatus = components["schemas"]["EDeploymentStatus"];

/** The single user-facing state of a server, shared by every surface that shows one. */
export type ServerState =
  "off" | "starting" | "running" | "stopping" | "working" | "attention" | "unconfirmed" | "unknown";

/** Whether a server is costing money now, may still cost money, or does not cost money. */
export type ServerCost = "live" | "maybe" | "none";

/** Visual tone of the state badge. */
export type ServerStateTone = "neutral" | "info" | "success" | "warning" | "danger";

/** Facts the state derivation needs, independent of where they come from. */
export interface ServerStateInput {
  /** Whether the server has a deployment record at all. */
  hasDeployment: boolean;
  /** Raw deployment status as sent by the API; may be a value this client does not know yet. */
  status: string | null | undefined;
  /** Whether the deployment may still generate provider cost. */
  possibleCost: boolean;
  /** Number of lifecycle jobs that are queued or running for the server. */
  pendingJobCount: number;
}

/** Result of the state derivation; everything a surface needs to render a state and cost. */
export interface DerivedServerState {
  state: ServerState;
  cost: ServerCost;
  tone: ServerStateTone;
  /** Localization key of the state label. */
  labelKey: TranslationKey;
  /** Localization key of the cost label. */
  costLabelKey: TranslationKey;
  /** Localization key of the technical deployment status, when the status is known. */
  detailKey?: TranslationKey;
  /** Whether a cost indicator may be shown; false only when no server data is available at all. */
  costVisible: boolean;
}

type StatusClass = "attention" | "starting" | "running" | "stopping" | "settled";

const statusClasses = {
  Provisioning: "starting",
  WaitingForWorker: "starting",
  Preparing: "starting",
  Running: "running",
  Stopping: "stopping",
  PreservationFailed: "attention",
  Terminating: "stopping",
  Stopped: "settled",
  TerminatingDueToFault: "stopping",
  TerminatedDueToFault: "attention",
  Failed: "attention",
  Terminated: "settled",
} as const satisfies Record<EDeploymentStatus, StatusClass>;

/** Statuses in which an allocated deployment exists; these always cost money. */
const liveCostStatuses = {
  Provisioning: true,
  WaitingForWorker: true,
  Preparing: true,
  Running: true,
  Stopping: true,
  PreservationFailed: false,
  Terminating: true,
  Stopped: false,
  TerminatingDueToFault: true,
  TerminatedDueToFault: false,
  Failed: false,
  Terminated: false,
} as const satisfies Record<EDeploymentStatus, boolean>;

const statusDetailKeys = {
  Provisioning: "Hosting_DeploymentStatus_Provisioning",
  WaitingForWorker: "Hosting_DeploymentStatus_WaitingForWorker",
  Preparing: "Hosting_DeploymentStatus_Preparing",
  Running: "Hosting_DeploymentStatus_Running",
  Stopping: "Hosting_DeploymentStatus_Stopping",
  PreservationFailed: "Hosting_DeploymentStatus_PreservationFailed",
  Terminating: "Hosting_DeploymentStatus_Terminating",
  Stopped: "Hosting_DeploymentStatus_Stopped",
  TerminatingDueToFault: "Hosting_DeploymentStatus_TerminatingDueToFault",
  TerminatedDueToFault: "Hosting_DeploymentStatus_TerminatedDueToFault",
  Failed: "Hosting_DeploymentStatus_Failed",
  Terminated: "Hosting_DeploymentStatus_Terminated",
} as const satisfies Record<EDeploymentStatus, TranslationKey>;

const stateLabelKeys = {
  off: "Server_State_Off",
  starting: "Server_State_Starting",
  running: "Server_State_Running",
  stopping: "Server_State_Stopping",
  working: "Server_State_Working",
  attention: "Server_State_Attention",
  unconfirmed: "Server_State_Unconfirmed",
  unknown: "Server_State_Unknown",
} as const satisfies Record<ServerState, TranslationKey>;

const costLabelKeys = {
  live: "Server_Cost_Live",
  maybe: "Server_Cost_Maybe",
  none: "Server_Cost_None",
} as const satisfies Record<ServerCost, TranslationKey>;

const stateTones = {
  off: "neutral",
  starting: "info",
  running: "success",
  stopping: "info",
  working: "info",
  attention: "danger",
  unconfirmed: "warning",
  unknown: "neutral",
} as const satisfies Record<ServerState, ServerStateTone>;

function isKnownStatus(status: string): status is EDeploymentStatus {
  return Object.hasOwn(statusClasses, status);
}

function deriveState(status: string | undefined, input: ServerStateInput): ServerState {
  if (status) {
    if (!isKnownStatus(status)) return "unknown";
    switch (statusClasses[status]) {
      case "attention":
        return "attention";
      case "starting":
        return "starting";
      case "running":
        return "running";
      case "stopping":
        return "stopping";
      case "settled":
        break;
    }
  }
  if (input.possibleCost) return "unconfirmed";
  return input.pendingJobCount > 0 ? "working" : "off";
}

function deriveCost(status: string | undefined, input: ServerStateInput): ServerCost {
  if (status && isKnownStatus(status) && liveCostStatuses[status]) return "live";
  return input.possibleCost ? "maybe" : "none";
}

/**
 * Derives the state, cost and presentation of a server from plain facts.
 *
 * The invariant of this function: a server that may still cost money is never `off` and never
 * costs `none`, and an `off` server always costs `none`.
 */
export function deriveServerStateFromInput(input: ServerStateInput): DerivedServerState {
  const status = input.hasDeployment && input.status ? input.status : undefined;
  const state = deriveState(status, input);
  const cost = deriveCost(status, input);
  const tone: ServerStateTone =
    state === "unknown" && input.possibleCost ? "warning" : stateTones[state];
  return {
    state,
    cost,
    tone,
    labelKey: stateLabelKeys[state],
    costLabelKey: costLabelKeys[cost],
    ...(status && isKnownStatus(status) ? { detailKey: statusDetailKeys[status] } : {}),
    costVisible: true,
  };
}

/**
 * Tells whether a server has an active deployment: one that exists and is not terminated.
 *
 * Keeping the status comparison here means no page compares a raw deployment status itself.
 *
 * @param server The server, or anything carrying its deployment.
 */
export function isDeploymentActive(server: Pick<GameServer, "deployment">): boolean {
  const status = server.deployment?.status.toLocaleLowerCase();
  return Boolean(status && status !== "terminated" && status !== "terminatedduetofault");
}

/**
 * Derives the badge state of a server for every surface that shows one.
 *
 * @param server The server from the fleet query, or nothing when it is not loaded yet.
 * @param options Optional fallback facts used when the server itself is not available.
 */
export function deriveServerState(
  server: GameServer | null | undefined,
  options?: { notification?: { possibleCost: boolean } },
): DerivedServerState {
  if (!server) {
    const possibleCost = options?.notification?.possibleCost ?? false;
    const cost: ServerCost = possibleCost ? "maybe" : "none";
    return {
      state: "unknown",
      cost,
      tone: possibleCost ? "warning" : stateTones.unknown,
      labelKey: stateLabelKeys.unknown,
      costLabelKey: costLabelKeys[cost],
      costVisible: possibleCost,
    };
  }
  return deriveServerStateFromInput({
    hasDeployment: server.deployment !== null,
    status: server.deployment?.status,
    possibleCost: server.deployment?.possibleCost ?? false,
    pendingJobCount: server.pendingJobCount,
  });
}
