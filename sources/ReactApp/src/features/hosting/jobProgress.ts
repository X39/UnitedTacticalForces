import type { TranslationKey } from "@/app/localizationTypes";
import type { GameServer, LifecycleJob } from "@/features/hosting/types";

/** Interval in milliseconds of the fallback poll that runs only while a lifecycle job is active. */
export const workspaceFallbackPollMs = 30_000;

/**
 * Statuses of a lifecycle job that count as active. They equal the API pending set in
 * `GameServerProjectionService.cs` (Queued, Running, RetryPending, WaitingForWorker).
 */
const activeJobStatuses: ReadonlySet<string> = new Set([
  "Queued",
  "Running",
  "RetryPending",
  "WaitingForWorker",
]);

/**
 * Returns whether a job status is active, that is still pending or in progress.
 *
 * @param status The lifecycle job status; unknown values are not active.
 */
export function isActiveJobStatus(status: string): boolean {
  return activeJobStatuses.has(status);
}

/**
 * Returns the active jobs, newest first.
 *
 * @param jobs The loaded jobs; undefined yields an empty list.
 * @returns A new array ordered by `timeStampCreated` descending, ties broken by `primaryKey` ascending.
 */
export function activeJobs(jobs: readonly LifecycleJob[] | undefined): LifecycleJob[] {
  return (jobs ?? [])
    .filter((job) => isActiveJobStatus(job.status))
    .sort((left, right) => {
      const byTime = Date.parse(right.timeStampCreated) - Date.parse(left.timeStampCreated);
      if (byTime !== 0 && !Number.isNaN(byTime)) return byTime;
      return left.primaryKey.localeCompare(right.primaryKey);
    });
}

/**
 * Returns whether a lifecycle job is active for the server.
 *
 * The server's `pendingJobCount` counts even when the jobs query has not loaded yet, so it is honoured as well.
 *
 * @param server The server, or null while it is not loaded.
 * @param jobs The loaded jobs, if any.
 */
export function hasActiveJob(
  server: Pick<GameServer, "pendingJobCount"> | null | undefined,
  jobs: readonly LifecycleJob[] | undefined,
): boolean {
  if ((server?.pendingJobCount ?? 0) > 0) return true;
  return (jobs ?? []).some((job) => isActiveJobStatus(job.status));
}

/** One named step of a lifecycle job as members see it. */
export type ProgressStepId =
  | "queued"
  | "preparingMachine"
  | "waitingForSoftware"
  | "workingOnServer"
  | "savingData"
  | "releasingMachine"
  | "finishing";

/**
 * What the progress strip shows for one active job.
 *
 * `steps` lists the kind's steps with the index of the current one, `retry` is a job that failed a step and will
 * be tried again, and `generic` is a job whose stage maps to no known step: the strip then shows a plain
 * "Working" line and the raw stage token. No variant carries a percentage; the API's milestone percent is a
 * coarse number that would read as a precise progress bar.
 */
export type JobProgress =
  | { mode: "steps"; steps: readonly ProgressStepId[]; currentIndex: number }
  | { mode: "retry" }
  | { mode: "generic"; stage: string };

/** The kinds that need their own step list; every other kind, known or not, shares the short list. */
type StepListKind = "start" | "stop" | "terminate" | "other";

const startSteps: readonly ProgressStepId[] = [
  "queued",
  "preparingMachine",
  "waitingForSoftware",
  "workingOnServer",
  "finishing",
];
const stopSteps: readonly ProgressStepId[] = [
  "queued",
  "savingData",
  "releasingMachine",
  "finishing",
];
const terminateSteps: readonly ProgressStepId[] = ["queued", "releasingMachine", "finishing"];
const otherSteps: readonly ProgressStepId[] = ["queued", "workingOnServer", "finishing"];

function stepListKind(kind: string): StepListKind {
  switch (kind) {
    case "Start":
      return "start";
    case "Stop":
      return "stop";
    case "Terminate":
      return "terminate";
    default:
      return "other";
  }
}

function stepsFor(kind: StepListKind): readonly ProgressStepId[] {
  switch (kind) {
    case "start":
      return startSteps;
    case "stop":
      return stopSteps;
    case "terminate":
      return terminateSteps;
    default:
      return otherSteps;
  }
}

/**
 * Returns the localization key of a progress step label.
 *
 * @param step The step id.
 * @returns The matching `Server_Progress_Step_*` key.
 */
export function progressStepLabelKey(step: ProgressStepId): TranslationKey {
  switch (step) {
    case "queued":
      return "Server_Progress_Step_Queued";
    case "preparingMachine":
      return "Server_Progress_Step_PreparingMachine";
    case "waitingForSoftware":
      return "Server_Progress_Step_WaitingForSoftware";
    case "workingOnServer":
      return "Server_Progress_Step_WorkingOnServer";
    case "savingData":
      return "Server_Progress_Step_SavingData";
    case "releasingMachine":
      return "Server_Progress_Step_ReleasingMachine";
    case "finishing":
      return "Server_Progress_Step_Finishing";
    default:
      return "Server_Progress_Step_Working";
  }
}

/**
 * Derives the step that the job stage and its worker commands point to, ignoring the job status.
 *
 * This is the stage and command part of the UI-SPEC derivation table, first match wins. The result is not yet
 * checked against the kind's step list.
 */
function stepFromStageAndCommands(job: LifecycleJob): ProgressStepId | undefined {
  const kind = stepListKind(job.kind);
  const stage = typeof job.stage === "string" ? job.stage : "";
  const statuses = job.commands.map((command) => command.status);
  const workerActive = statuses.some((status) => status === "Received" || status === "Running");
  const workerSucceeded = statuses.some((status) => status === "Succeeded");
  const stopsOrTerminates = kind === "stop" || kind === "terminate";

  if (stage === "waiting-for-worker")
    return kind === "start" ? "waitingForSoftware" : "workingOnServer";

  const providerStage =
    stage.startsWith("provider-") || stage === "claimed" || stage === "recovered";
  if (providerStage && !workerActive && !workerSucceeded) {
    if (kind === "start") return "preparingMachine";
    return stopsOrTerminates ? "releasingMachine" : "workingOnServer";
  }
  if (workerActive) return kind === "stop" ? "savingData" : "workingOnServer";
  if (stopsOrTerminates && stage.includes("preservation")) return "savingData";
  if (stopsOrTerminates && workerSucceeded && stage.startsWith("provider-"))
    return "releasingMachine";
  if (workerSucceeded && isActiveJobStatus(job.status)) return "finishing";
  return undefined;
}

/**
 * Derives what the progress strip shows for a lifecycle job.
 *
 * Stages come only from the job kind, status, stage string and worker command statuses. The function is total:
 * every kind, status and stage, including values a newer API may send, yields a result without throwing. A step
 * that the kind's list does not contain, and every combination the derivation table does not cover, yields the
 * generic mode with the raw stage instead of an invented step.
 *
 * @param job The lifecycle job.
 * @returns Retry mode for `RetryPending`, a step list with the current index when a step applies, otherwise the
 *   generic mode carrying the raw stage.
 */
export function deriveJobProgress(job: LifecycleJob): JobProgress {
  const steps = stepsFor(stepListKind(job.kind));
  const stage = typeof job.stage === "string" ? job.stage : "";
  let step: ProgressStepId | undefined;
  switch (job.status) {
    case "Queued":
      step = "queued";
      break;
    case "RetryPending":
      return { mode: "retry" };
    case "WaitingForWorker":
      step = stepListKind(job.kind) === "start" ? "waitingForSoftware" : "workingOnServer";
      break;
    default:
      step = stepFromStageAndCommands(job);
      break;
  }
  const currentIndex = step === undefined ? -1 : steps.indexOf(step);
  if (currentIndex < 0) return { mode: "generic", stage };
  return { mode: "steps", steps, currentIndex };
}

/**
 * Returns the label key of the step a job was in when it stopped.
 *
 * Only the stage and command rows of the derivation apply; the job status rows are ignored because the job has
 * already failed.
 *
 * @param job The lifecycle job, normally a failed one.
 * @returns The step label key, or `Server_Progress_Step_Working` when no step of the kind's list applies.
 */
export function failedStepLabelKey(job: LifecycleJob): TranslationKey {
  const step = stepFromStageAndCommands(job);
  if (step === undefined || !stepsFor(stepListKind(job.kind)).includes(step)) {
    return "Server_Progress_Step_Working";
  }
  return progressStepLabelKey(step);
}

/** A job that moved from an active status to a final one between two snapshots. */
export interface JobTransitionAnnouncement {
  /** Whether the job finished successfully or failed. */
  kind: "finished" | "failed";
  /** The job as it is in the newer snapshot. */
  job: LifecycleJob;
}

/**
 * Finds the job transition that the shell should announce between two job snapshots.
 *
 * Only a job whose previous status was active and whose new status is `Succeeded` or `Failed` counts, matched by
 * primary key. A job that was already final, a job that is new, and a job that left the list are never reported.
 * The first snapshot has nothing to compare with, so a page that loads with a finished or failed job stays silent.
 *
 * @param previous The earlier snapshot of the jobs, or undefined before the first load completed.
 * @param next The newer snapshot of the jobs.
 * @returns The newest transitioned job by `timeStampCreated` with its outcome, or null when nothing changed.
 */
export function jobTransitionAnnouncement(
  previous: readonly LifecycleJob[] | undefined,
  next: readonly LifecycleJob[],
): JobTransitionAnnouncement | null {
  if (previous === undefined) return null;
  const previousStatuses = new Map(previous.map((job) => [job.primaryKey, job.status] as const));
  let newest: JobTransitionAnnouncement | null = null;
  for (const job of next) {
    const before = previousStatuses.get(job.primaryKey);
    if (before === undefined || !isActiveJobStatus(before)) continue;
    let kind: JobTransitionAnnouncement["kind"];
    switch (job.status) {
      case "Succeeded":
        kind = "finished";
        break;
      case "Failed":
        kind = "failed";
        break;
      default:
        continue;
    }
    if (
      newest === null ||
      Date.parse(job.timeStampCreated) > Date.parse(newest.job.timeStampCreated)
    ) {
      newest = { kind, job };
    }
  }
  return newest;
}
