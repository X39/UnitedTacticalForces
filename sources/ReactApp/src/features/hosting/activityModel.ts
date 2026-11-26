import type { TranslationKey } from "@/app/localizationTypes";
import {
  actorFor,
  jobKindLabelKey,
  triggerFor,
  type ActorDisplay,
  type TriggerDisplay,
} from "@/features/hosting/activityLabels";
import { hostingAuditTone } from "@/features/hosting/HostingAuditFeed";
import { isActiveJobStatus } from "@/features/hosting/jobProgress";
import type { HostingOperation, LifecycleJob } from "@/features/hosting/types";
import { activityEntryAnchor } from "@/features/hosting/workspaceSections";

/**
 * The most audit operations the operations endpoint returns in one response.
 *
 * The Activity section requests this many so that the audit window is as long as the API allows and lines up with
 * the newest 100 jobs; {@link auditWindowBoundary} compares the response size against it.
 */
export const activityOperationsTake = 200;

/** The three sources of a timeline entry: a lifecycle job, a worker command log or an audit record. */
export type TimelineEntryType = "job" | "log" | "audit";

/** One worker command of a lifecycle job, as the API sends it. */
type JobCommand = LifecycleJob["commands"][number];

/** The fields every timeline entry carries, whichever source it came from. */
interface TimelineEntryBase {
  /** The anchor id `activity-<type>-<primaryKey>`; the Open activity details links target it. */
  id: string;
  /** The ISO instant that sorts the entry and is shown as its time. */
  at: string;
  /** Whether the entry reports something that went wrong or is about to be retried. */
  problem: boolean;
  /** The severity of a problem entry, or null when the entry is not a problem. */
  tone: "danger" | "warning" | null;
  /** Whether the entry is a lifecycle job that has not finished yet. */
  active: boolean;
  /** The localization key of the plain-language sentence. */
  sentenceKey: TranslationKey;
  /** The localization key of the `{{action}}` value of the sentence, or null when the sentence has none. */
  actionKey: TranslationKey | null;
  /** Who caused the entry; never inferred from any field other than the API actor name. */
  actor: ActorDisplay;
  /** API-originated prose shown after the sentence, or null when the sentence stands alone. */
  apiText: string | null;
  /** Whether the entry duplicates a loaded job and is therefore hidden from the All filter. */
  echo: boolean;
}

/** A timeline entry for one lifecycle job. */
export interface JobTimelineEntry extends TimelineEntryBase {
  /** Discriminates the entry source. */
  type: "job";
  /** The lifecycle job this entry describes. */
  job: LifecycleJob;
  /** The trigger line of an automatic job, which replaces the actor line, or null for a manual job. */
  trigger: TriggerDisplay | null;
}

/** A timeline entry for one worker command of a lifecycle job. */
export interface LogTimelineEntry extends TimelineEntryBase {
  /** Discriminates the entry source. */
  type: "log";
  /** The lifecycle job that issued the command. */
  job: LifecycleJob;
  /** The worker command this entry describes. */
  command: JobCommand;
}

/** A timeline entry for one hosting operation (audit record). */
export interface AuditTimelineEntry extends TimelineEntryBase {
  /** Discriminates the entry source. */
  type: "audit";
  /** The hosting operation this entry describes. */
  operation: HostingOperation;
  /** The loaded job the operation refers to, or null when it names no job or the job is not loaded. */
  job: LifecycleJob | null;
}

/** One entry of the merged activity timeline. */
export type TimelineEntry = JobTimelineEntry | LogTimelineEntry | AuditTimelineEntry;

/** Whether a text carries anything besides whitespace. */
function hasText(value: string | null | undefined): boolean {
  return value !== null && value !== undefined && value.trim() !== "";
}

/**
 * Returns the sentence key of a job status.
 *
 * A status this client does not know yet falls back to the in-progress sentence; the expanded details show the raw
 * status.
 */
function jobSentenceKey(status: string): TranslationKey {
  switch (status) {
    case "Queued":
      return "Server_Activity_Job_Queued";
    case "RetryPending":
      return "Server_Activity_Job_RetryPending";
    case "Succeeded":
      return "Server_Activity_Job_Succeeded";
    case "Failed":
      return "Server_Activity_Job_Failed";
    case "Skipped":
      return "Server_Activity_Job_Skipped";
    case "CancelledByCostStop":
      return "Server_Activity_Job_CancelledByCostStop";
    default:
      return "Server_Activity_Job_Running";
  }
}

/** Returns the problem tone of a job: Failed is danger, RetryPending with a failure reason is a warning. */
function jobTone(job: LifecycleJob): "danger" | "warning" | null {
  if (job.status === "Failed") return "danger";
  if (job.status === "RetryPending" && hasText(job.failureReason)) return "warning";
  return null;
}

/** Builds the timeline entry of one lifecycle job. */
function jobEntry(job: LifecycleJob): JobTimelineEntry {
  const tone = jobTone(job);
  return {
    type: "job",
    id: activityEntryAnchor("job", job.primaryKey),
    at: job.timeStampCompleted ?? job.timeStampUpdated,
    problem: tone !== null,
    tone,
    active: isActiveJobStatus(job.status),
    sentenceKey: jobSentenceKey(job.status),
    actionKey: jobKindLabelKey(job.kind),
    actor: actorFor(job.requestedByName),
    apiText: null,
    echo: false,
    job,
    trigger: triggerFor(job.trigger, job.triggerLimit),
  };
}

/** Compares two entry ids by code unit so the tie order never depends on the locale. */
function compareIds(left: string, right: string): number {
  if (left < right) return -1;
  return left > right ? 1 : 0;
}

/** Orders entries newest first by `at`, with equal or unreadable times ordered by ascending id. */
function compareEntries(left: TimelineEntry, right: TimelineEntry): number {
  const byTime = Date.parse(right.at) - Date.parse(left.at);
  if (byTime !== 0 && !Number.isNaN(byTime)) return byTime;
  return compareIds(left.id, right.id);
}

/**
 * Returns the sentence key of a worker command status.
 *
 * `Received` shares the queued sentence. A status this client does not know yet falls back to the working sentence.
 */
function logSentenceKey(status: string): TranslationKey {
  switch (status) {
    case "Queued":
    case "Received":
      return "Server_Activity_Log_Queued";
    case "Succeeded":
      return "Server_Activity_Log_Succeeded";
    case "Failed":
      return "Server_Activity_Log_Failed";
    case "Retried":
      return "Server_Activity_Log_Retried";
    case "Skipped":
      return "Server_Activity_Log_Skipped";
    case "CancelledByCostStop":
      return "Server_Activity_Log_CancelledByCostStop";
    default:
      return "Server_Activity_Log_Running";
  }
}

/** Returns the problem tone of a command: Failed is danger, CancelledByCostStop is a warning. */
function logTone(status: string): "danger" | "warning" | null {
  if (status === "Failed") return "danger";
  if (status === "CancelledByCostStop") return "warning";
  return null;
}

/** Builds the timeline entry of one worker command; the server itself is always the actor. */
function logEntry(job: LifecycleJob, command: JobCommand): LogTimelineEntry {
  const tone = logTone(command.status);
  return {
    type: "log",
    id: activityEntryAnchor("log", command.primaryKey),
    at: command.timeStampCompleted ?? command.timeStampStarted ?? command.timeStampCreated,
    problem: tone !== null,
    tone,
    active: false,
    sentenceKey: logSentenceKey(command.status),
    actionKey: jobKindLabelKey(job.kind),
    actor: { kind: "server" },
    apiText: null,
    echo: false,
    job,
    command,
  };
}

/**
 * The operational audit kinds the operations endpoint returns and the sentence of each.
 *
 * A kind that is not in this map renders the Other sentence plus the API summary.
 */
const auditSentenceKeys = new Map<string, TranslationKey>([
  ["DeploymentCreated", "Server_Activity_Audit_DeploymentCreated"],
  [
    "DeploymentResourceProvisioningRequested",
    "Server_Activity_Audit_DeploymentResourceProvisioningRequested",
  ],
  [
    "DeploymentResourceProvisioningFailed",
    "Server_Activity_Audit_DeploymentResourceProvisioningFailed",
  ],
  ["DeploymentResourceObserved", "Server_Activity_Audit_DeploymentResourceObserved"],
  ["DeploymentResourceAdopted", "Server_Activity_Audit_DeploymentResourceAdopted"],
  [
    "DeploymentResourceReleaseRequested",
    "Server_Activity_Audit_DeploymentResourceReleaseRequested",
  ],
  ["DeploymentResourceReleaseFailed", "Server_Activity_Audit_DeploymentResourceReleaseFailed"],
  ["DeploymentResourceReleased", "Server_Activity_Audit_DeploymentResourceReleased"],
  ["DeploymentStopped", "Server_Activity_Audit_DeploymentStopped"],
  ["DeploymentTerminated", "Server_Activity_Audit_DeploymentTerminated"],
  ["LifecycleJobQueued", "Server_Activity_Audit_LifecycleJobQueued"],
  ["LifecycleJobClaimed", "Server_Activity_Audit_LifecycleJobClaimed"],
  ["LifecycleJobRetryPending", "Server_Activity_Audit_LifecycleJobRetryPending"],
  ["LifecycleJobFailed", "Server_Activity_Audit_LifecycleJobFailed"],
  ["LifecycleJobSucceeded", "Server_Activity_Audit_LifecycleJobSucceeded"],
  ["LifecycleFailed", "Server_Activity_Audit_LifecycleFailed"],
  ["WorkerProvisioned", "Server_Activity_Audit_WorkerProvisioned"],
  ["WorkerRegistered", "Server_Activity_Audit_WorkerRegistered"],
  ["WorkerDegraded", "Server_Activity_Audit_WorkerDegraded"],
  ["WorkerRecovered", "Server_Activity_Audit_WorkerRecovered"],
  ["WorkerStopped", "Server_Activity_Audit_WorkerStopped"],
  ["WorkerCommandQueued", "Server_Activity_Audit_WorkerCommandQueued"],
  ["WorkerCommandFailed", "Server_Activity_Audit_WorkerCommandFailed"],
  ["WorkerCommandSucceeded", "Server_Activity_Audit_WorkerCommandSucceeded"],
  ["ProviderReconciliationDegraded", "Server_Activity_Audit_ProviderReconciliationDegraded"],
  ["Recovery", "Server_Activity_Audit_Recovery"],
  ["AutomaticStopWarned", "Server_Activity_Audit_AutomaticStopWarned"],
  ["AutomaticStopRequested", "Server_Activity_Audit_AutomaticStopRequested"],
  ["AutomaticStopCancelled", "Server_Activity_Audit_AutomaticStopCancelled"],
  ["ScheduledStartRequested", "Server_Activity_Audit_ScheduledStartRequested"],
  ["ScheduledRunSkipped", "Server_Activity_Audit_ScheduledRunSkipped"],
  ["AutomationSettingsChanged", "Server_Activity_Audit_AutomationSettingsChanged"],
  ["ScheduledStartsConfirmed", "Server_Activity_Audit_ScheduledStartsConfirmed"],
]);

/** The audit kinds whose API summary carries the reason and is shown after the sentence. */
const auditReasonKinds: ReadonlySet<string> = new Set(["ScheduledRunSkipped"]);

/**
 * The audit kinds that repeat what a loaded job or its commands already say.
 *
 * Their entries are hidden from the All filter while the job is loaded and stay visible in the Audit filter.
 */
const auditEchoKinds: ReadonlySet<string> = new Set([
  "LifecycleJobQueued",
  "LifecycleJobClaimed",
  "LifecycleJobRetryPending",
  "LifecycleJobFailed",
  "LifecycleJobSucceeded",
  "WorkerCommandQueued",
  "WorkerCommandFailed",
  "WorkerCommandSucceeded",
  "AutomaticStopRequested",
  "ScheduledStartRequested",
]);

/** Builds the timeline entry of one hosting operation, resolving its job against the loaded jobs. */
function auditEntry(
  operation: HostingOperation,
  jobs: readonly LifecycleJob[],
): AuditTimelineEntry {
  const known = auditSentenceKeys.get(operation.kind);
  const job = jobs.find((candidate) => candidate.primaryKey === operation.lifecycleJobId) ?? null;
  const isEchoKind = auditEchoKinds.has(operation.kind);
  const auditTone = hostingAuditTone(operation);
  const tone = auditTone === "danger" || auditTone === "warning" ? auditTone : null;
  let actionKey: TranslationKey | null = null;
  if (isEchoKind) actionKey = job ? jobKindLabelKey(job.kind) : "Server_Activity_Kind_Unknown";
  return {
    type: "audit",
    id: activityEntryAnchor("audit", operation.primaryKey),
    at: operation.timeStampCreated,
    problem: tone !== null,
    tone,
    active: false,
    sentenceKey: known ?? "Server_Activity_Audit_Other",
    actionKey,
    actor: actorFor(operation.actorName),
    apiText:
      known === undefined || (auditReasonKinds.has(operation.kind) && hasText(operation.summary))
        ? operation.summary
        : null,
    echo: isEchoKind && job !== null,
    operation,
    job,
  };
}

/**
 * Merges the loaded jobs, their worker commands and the hosting operations into one readable timeline, newest
 * first.
 *
 * Every entry reads as a plain-language sentence from closed key maps and carries its actor. Audit entries that
 * only echo a loaded job are marked `echo`. The function is pure: it reads no clock, renders nothing and
 * translates nothing.
 *
 * @param jobs The loaded lifecycle jobs, each with its worker commands.
 * @param operations The loaded hosting operations (audit records).
 * @returns A new array of entries ordered by `at` descending, ties broken by ascending `id`.
 */
export function buildActivityEntries(
  jobs: readonly LifecycleJob[],
  operations: readonly HostingOperation[],
): TimelineEntry[] {
  const entries: TimelineEntry[] = [
    ...jobs.map(jobEntry),
    ...jobs.flatMap((job) => job.commands.map((command) => logEntry(job, command))),
    ...operations.map((operation) => auditEntry(operation, jobs)),
  ];
  return entries.sort(compareEntries);
}

/** The type filters of the Activity section. */
export type ActivityFilter = "all" | "jobs" | "logs" | "audit";

/**
 * Lists the entries one filter shows.
 *
 * All hides audit echoes of loaded jobs; Audit keeps them with their full evidence. Jobs and Logs list only their
 * own type. Problems only keeps the problem entries of whichever filter is chosen.
 *
 * @param entries The merged timeline from {@link buildActivityEntries}.
 * @param filter The chosen type filter.
 * @param problemsOnly Whether only problem entries are listed.
 * @returns A new array in the order of `entries`.
 */
export function filterEntries(
  entries: readonly TimelineEntry[],
  filter: ActivityFilter,
  problemsOnly: boolean,
): TimelineEntry[] {
  return entries.filter((entry) => {
    if (problemsOnly && !entry.problem) return false;
    switch (filter) {
      case "all":
        return !entry.echo;
      case "jobs":
        return entry.type === "job";
      case "logs":
        return entry.type === "log";
      case "audit":
        return entry.type === "audit";
    }
  });
}

/**
 * Counts what each type filter lists, with the current Problems only setting applied.
 *
 * The counts are computed through {@link filterEntries}, so a chip count can never differ from its list.
 *
 * @param entries The merged timeline from {@link buildActivityEntries}.
 * @param problemsOnly Whether only problem entries are counted.
 * @returns The number of listed entries per filter.
 */
export function filterCounts(
  entries: readonly TimelineEntry[],
  problemsOnly: boolean,
): Record<ActivityFilter, number> {
  return {
    all: filterEntries(entries, "all", problemsOnly).length,
    jobs: filterEntries(entries, "jobs", problemsOnly).length,
    logs: filterEntries(entries, "logs", problemsOnly).length,
    audit: filterEntries(entries, "audit", problemsOnly).length,
  };
}

/**
 * Finds where the audit records end.
 *
 * The operations endpoint returns at most `take` records. When it returned that many, older platform events exist
 * that are not loaded, so the timeline marks the oldest returned audit time: older entries list jobs and logs only.
 *
 * @param operations The loaded hosting operations.
 * @param take The size limit that was requested; defaults to {@link activityOperationsTake}.
 * @returns The oldest `timeStampCreated` when `take` operations were returned, otherwise null.
 */
export function auditWindowBoundary(
  operations: readonly HostingOperation[],
  take: number = activityOperationsTake,
): string | null {
  if (operations.length === 0 || operations.length < take) return null;
  let oldest = operations[0]?.timeStampCreated ?? null;
  for (const operation of operations) {
    if (oldest === null || Date.parse(operation.timeStampCreated) < Date.parse(oldest)) {
      oldest = operation.timeStampCreated;
    }
  }
  return oldest;
}

/** The calendar day of a timeline group relative to the current day. */
export type ActivityDay =
  { kind: "today" } | { kind: "yesterday" } | { kind: "date"; date: Date; sameYear: boolean };

/** The entries of one local calendar day. */
export interface ActivityDayGroup {
  /** The local calendar day as `YYYY-MM-DD`, or `unknown` for entries whose time cannot be read. */
  key: string;
  /** Whether the day is today, yesterday or a dated day. */
  day: ActivityDay;
  /** The entries of the day in the order they were given. */
  entries: TimelineEntry[];
}

/** Formats the local calendar day of a date as `YYYY-MM-DD`. */
function localDayKey(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

/**
 * Groups timeline entries by local calendar day, newest day first.
 *
 * Days are computed from the viewer's local calendar, so an entry shortly after midnight belongs to the new day.
 * Entries keep their given order inside a group; entries whose time cannot be read are grouped last.
 *
 * @param entries The entries, normally already newest first.
 * @param now The current time as epoch milliseconds or a `Date`; the function never reads the clock itself.
 * @returns The day groups, each carrying whether it is today, yesterday or a dated day of this or another year.
 */
export function groupEntriesByDay(
  entries: readonly TimelineEntry[],
  now: number | Date,
): ActivityDayGroup[] {
  const current = new Date(now);
  const todayKey = localDayKey(current);
  const yesterdayKey = localDayKey(
    new Date(current.getFullYear(), current.getMonth(), current.getDate() - 1),
  );
  const groups = new Map<string, ActivityDayGroup>();
  for (const entry of entries) {
    const time = new Date(entry.at);
    const readable = !Number.isNaN(time.getTime());
    const key = readable ? localDayKey(time) : "unknown";
    let group = groups.get(key);
    if (group === undefined) {
      let day: ActivityDay;
      if (!readable) day = { kind: "date", date: new Date(0), sameYear: false };
      else if (key === todayKey) day = { kind: "today" };
      else if (key === yesterdayKey) day = { kind: "yesterday" };
      else {
        const date = new Date(time.getFullYear(), time.getMonth(), time.getDate());
        day = { kind: "date", date, sameYear: date.getFullYear() === current.getFullYear() };
      }
      group = { key, day, entries: [] };
      groups.set(key, group);
    }
    group.entries.push(entry);
  }
  return [...groups.values()].sort((left, right) => {
    if (left.key === "unknown") return right.key === "unknown" ? 0 : 1;
    if (right.key === "unknown") return -1;
    return compareIds(right.key, left.key);
  });
}
