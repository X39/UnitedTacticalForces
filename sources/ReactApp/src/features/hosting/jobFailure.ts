import type { TranslationKey } from "@/app/localizationTypes";
import { operatePermissionReason } from "@/components/server/unavailableReasonMapping";
import { deriveServerState, type ServerCost } from "@/features/hosting/serverState";
import type { GameServer, LifecycleJob, ServerAction } from "@/features/hosting/types";
import { isActiveJobStatus } from "@/features/hosting/jobProgress";
import { costStopAction } from "@/features/hosting/workspaceModel";

/**
 * The one next step the failure panel recommends.
 *
 * `stopBilling` comes first because a server that may still cost money matters more than a retry; `retry` is
 * offered when the API allows retrying or skipping; `askOwner` when the viewer lacks the operate permission;
 * `details` otherwise.
 */
export type FailureNextStep = "stopBilling" | "retry" | "askOwner" | "details";

/** What the failure panel states about the newest failed job. */
export interface JobFailure {
  /** The failed job. */
  job: LifecycleJob;
  /** Whether the server is costing money now, may still cost money or does not cost money. */
  cost: ServerCost;
  /** The cost-stop action to offer, or undefined when the cost is none or the server has no cost-stop action. */
  costStop: ServerAction | undefined;
  /** The recommended next step. */
  nextStep: FailureNextStep;
  /** Localization key of the next step sentence. */
  nextStepKey: TranslationKey;
}

/**
 * Returns the localization key of the cost sentence of a failure.
 *
 * @param cost The derived cost of the server.
 * @returns The `Server_Failure_Cost_*` key for live, maybe and none.
 */
export function costSentenceKey(cost: ServerCost): TranslationKey {
  switch (cost) {
    case "live":
      return "Server_Failure_Cost_Live";
    case "maybe":
      return "Server_Failure_Cost_Maybe";
    default:
      return "Server_Failure_Cost_None";
  }
}

function nextStepKeyFor(step: FailureNextStep): TranslationKey {
  switch (step) {
    case "stopBilling":
      return "Server_Failure_Next_StopBilling";
    case "retry":
      return "Server_Failure_Next_Retry";
    case "askOwner":
      return "Server_Failure_Next_AskOwner";
    default:
      return "Server_Failure_Next_Details";
  }
}

/** Whether a status is a terminal one: the job finished, failed, was skipped or was cancelled by a cost-stop. */
function isTerminalStatus(status: string): boolean {
  return (
    status === "Succeeded" ||
    status === "Failed" ||
    status === "Skipped" ||
    status === "CancelledByCostStop"
  );
}

function newestFirst(left: LifecycleJob, right: LifecycleJob): number {
  const byTime = Date.parse(right.timeStampCreated) - Date.parse(left.timeStampCreated);
  if (byTime !== 0 && !Number.isNaN(byTime)) return byTime;
  return left.primaryKey.localeCompare(right.primaryKey);
}

/**
 * Derives what the failure panel shows.
 *
 * The panel exists only while no job is active and the newest terminal job is `Failed`; a newer job of any kind
 * supersedes an older failure. Cost comes from `deriveServerState`, so a server that may still cost money is never
 * described as not costing money.
 *
 * @param server The server the jobs belong to.
 * @param jobs The loaded jobs; undefined yields no failure.
 * @returns The failure, or null when a job is active, there is no terminal job or the newest one did not fail.
 */
export function deriveFailure(
  server: GameServer,
  jobs: readonly LifecycleJob[] | undefined,
): JobFailure | null {
  const all = jobs ?? [];
  if (all.some((job) => isActiveJobStatus(job.status))) return null;
  const job = all.filter((entry) => isTerminalStatus(entry.status)).sort(newestFirst)[0];
  if (job?.status !== "Failed") return null;

  const cost = deriveServerState(server).cost;
  const costStop = cost === "none" ? undefined : costStopAction(server.actions);
  let nextStep: FailureNextStep = "details";
  if (costStop) nextStep = "stopBilling";
  else if (job.canRetry || job.canSkip) nextStep = "retry";
  else if (server.actions.some((action) => action.unavailableReason === operatePermissionReason)) {
    nextStep = "askOwner";
  }
  return { job, cost, costStop, nextStep, nextStepKey: nextStepKeyFor(nextStep) };
}
