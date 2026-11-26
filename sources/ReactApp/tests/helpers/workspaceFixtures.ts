import type { HostingOperation, LifecycleJob } from "@/features/hosting/types";

/** One worker command of a lifecycle job, as the API sends it. */
type WorkspaceCommand = LifecycleJob["commands"][number];

/** Fixed instants every fixture uses, so rendered times never depend on the clock. */
const createdAt = "2026-08-13T12:00:00Z";
const updatedAt = "2026-08-13T12:05:00Z";
const startedAt = "2026-08-13T12:01:00Z";

/** Counter behind the deterministic ids; it advances once per built object. */
let sequence = 0;

/**
 * Returns the next deterministic UUID-formatted id of a fixture.
 *
 * @param prefix Leading hex digit group that tells the fixture kinds apart in failure output.
 */
function nextId(prefix: string): string {
  sequence += 1;
  return `${prefix}-0000-4000-8000-${sequence.toString().padStart(12, "0")}`;
}

/**
 * Builds a complete worker command as the API sends it. Every call returns a fresh object.
 *
 * Defaults to a running command with sequence 1 and no failure fields.
 *
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns A fully populated command with a deterministic id.
 */
export function workspaceCommand(overrides: Partial<WorkspaceCommand> = {}): WorkspaceCommand {
  return {
    primaryKey: nextId("c0aa0000"),
    sequence: 1,
    actionId: "start",
    status: "Running",
    stage: "running",
    failureCode: null,
    failureReason: null,
    diagnosticOutput: null,
    timeStampCreated: createdAt,
    timeStampStarted: startedAt,
    timeStampCompleted: null,
    ...overrides,
  } as WorkspaceCommand;
}

/**
 * Builds a complete lifecycle job as the API sends it. Every call returns a fresh object, including a fresh
 * `commands` array, so tests may mutate the result freely.
 *
 * Defaults to a running start job on its first attempt that can neither be retried nor skipped, was requested
 * by no named actor, and has no commands.
 *
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns A fully populated job with a deterministic id.
 */
export function workspaceJob(overrides: Partial<LifecycleJob> = {}): LifecycleJob {
  return {
    primaryKey: nextId("10b00000"),
    gameServerId: "f1ee7000-0000-4000-8000-000000000001",
    deploymentId: null,
    kind: "Start",
    actionId: "start",
    status: "Running",
    stage: "running",
    progressPercent: 0,
    attemptCount: 1,
    failureCode: null,
    failureReason: null,
    requestedByName: null,
    canRetry: false,
    canSkip: false,
    timeStampCreated: createdAt,
    timeStampUpdated: updatedAt,
    timeStampStarted: startedAt,
    timeStampCompleted: null,
    commands: [],
    ...overrides,
  } as LifecycleJob;
}

/**
 * Builds a stop job that an automatic rule queued, as the API sends it.
 *
 * @param trigger The automatic cause: `Idle`, `MaxRuntime` or `Schedule`.
 * @param triggerLimit Minutes for `Idle`, hours for `MaxRuntime`, otherwise null.
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns A completed stop job carrying the trigger and no named actor.
 */
export function workspaceAutomaticJob(
  trigger: "Idle" | "MaxRuntime" | "Schedule",
  triggerLimit: number | null,
  overrides: Partial<LifecycleJob> = {},
): LifecycleJob {
  return workspaceJob({
    kind: "Stop",
    actionId: "stop",
    status: "Succeeded",
    stage: "completed",
    trigger,
    triggerLimit,
    ...overrides,
  });
}

/**
 * Builds a complete hosting operation (an activity entry) as the API sends it. Every call returns a fresh
 * object.
 *
 * Defaults to a queued lifecycle job event without a named actor.
 *
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns A fully populated operation with a deterministic id.
 */
export function workspaceOperation(overrides: Partial<HostingOperation> = {}): HostingOperation {
  return {
    primaryKey: nextId("0e000000"),
    kind: "LifecycleJobQueued",
    status: "Queued",
    summary: "Lifecycle job queued",
    detail: null,
    providerIdentifier: null,
    resourceKind: null,
    resourceIdentifier: null,
    providerState: null,
    deploymentId: null,
    lifecycleJobId: null,
    workerId: null,
    traceId: null,
    spanId: null,
    actorName: null,
    timeStampCreated: createdAt,
    ...overrides,
  } as HostingOperation;
}
