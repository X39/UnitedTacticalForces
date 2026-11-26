import type { TranslationKey } from "@/app/localizationTypes";
import type { components } from "@/generated/api";
import { actorFor } from "@/features/hosting/activityLabels";
import {
  activityOperationsTake,
  auditWindowBoundary,
  buildActivityEntries,
  filterCounts,
  filterEntries,
  groupEntriesByDay,
  type ActivityFilter,
  type TimelineEntry,
} from "@/features/hosting/activityModel";
import {
  workspaceAutomaticJob,
  workspaceCommand,
  workspaceJob,
  workspaceOperation,
} from "../helpers/workspaceFixtures";

/* Activity model (ACT-01, D-10): jobs, worker command logs and audit operations as one readable list. */

describe("buildActivityEntries jobs", () => {
  it("turns a running start job into one readable entry", () => {
    const job = workspaceJob({
      kind: "Start",
      status: "Running",
      requestedByName: "Falcon One",
      timeStampUpdated: "2026-08-13T12:01:00Z",
    });

    const entries = buildActivityEntries([job], []);

    expect(entries).toHaveLength(1);
    expect(entries[0]).toMatchObject({
      type: "job",
      id: `activity-job-${job.primaryKey}`,
      at: "2026-08-13T12:01:00Z",
      sentenceKey: "Server_Activity_Job_Running",
      actionKey: "Server_Action_Start_Label",
      actor: { kind: "name", name: "Falcon One" },
      problem: false,
      tone: null,
      active: true,
      echo: false,
      apiText: null,
    });
    expect(entries[0]).toHaveProperty("job", job);
  });

  it("uses the in-progress sentence for WaitingForWorker and for a status this client does not know", () => {
    const waiting = workspaceJob({ status: "WaitingForWorker" });
    const newer = workspaceJob({ status: "Paused" as never });

    const [first, second] = buildActivityEntries([waiting, newer], []);

    expect(first?.sentenceKey).toBe("Server_Activity_Job_Running");
    expect(first?.active).toBe(true);
    expect(second?.sentenceKey).toBe("Server_Activity_Job_Running");
    expect(second?.active).toBe(false);
  });

  it.each([
    ["Queued", "Server_Activity_Job_Queued"],
    ["Running", "Server_Activity_Job_Running"],
    ["RetryPending", "Server_Activity_Job_RetryPending"],
    ["WaitingForWorker", "Server_Activity_Job_Running"],
    ["Succeeded", "Server_Activity_Job_Succeeded"],
    ["Failed", "Server_Activity_Job_Failed"],
    ["Skipped", "Server_Activity_Job_Skipped"],
    ["CancelledByCostStop", "Server_Activity_Job_CancelledByCostStop"],
  ] as const)("maps the %s job status to %s", (status, sentenceKey) => {
    const [entry] = buildActivityEntries([workspaceJob({ status })], []);

    expect(entry?.sentenceKey).toBe(sentenceKey);
  });

  it("flags a Failed job as a danger problem", () => {
    const [entry] = buildActivityEntries([workspaceJob({ status: "Failed" })], []);

    expect(entry).toMatchObject({ problem: true, tone: "danger", active: false });
  });

  it("flags RetryPending as a warning only when it carries a failure reason", () => {
    const [withReason] = buildActivityEntries(
      [workspaceJob({ status: "RetryPending", failureReason: "Worker unreachable" })],
      [],
    );
    const [withoutReason] = buildActivityEntries(
      [workspaceJob({ status: "RetryPending", failureReason: null })],
      [],
    );
    const [blankReason] = buildActivityEntries(
      [workspaceJob({ status: "RetryPending", failureReason: "  " })],
      [],
    );

    expect(withReason).toMatchObject({ problem: true, tone: "warning", active: true });
    expect(withoutReason).toMatchObject({ problem: false, tone: null });
    expect(blankReason).toMatchObject({ problem: false, tone: null });
  });

  it("dates a finished job by its completion time", () => {
    const [entry] = buildActivityEntries(
      [
        workspaceJob({
          status: "Succeeded",
          timeStampUpdated: "2026-08-13T12:05:00Z",
          timeStampCompleted: "2026-08-13T12:04:00Z",
        }),
      ],
      [],
    );

    expect(entry?.at).toBe("2026-08-13T12:04:00Z");
  });

  it("derives the actor only from the requester name", () => {
    const [platform] = buildActivityEntries([workspaceJob({ requestedByName: null })], []);
    const [hidden] = buildActivityEntries([workspaceJob({ requestedByName: "Hidden member" })], []);

    expect(platform?.actor).toEqual(actorFor(null));
    expect(platform?.actor).toEqual({ kind: "platform" });
    expect(hidden?.actor).toEqual({ kind: "hidden" });
  });

  it("names an unknown job kind with the unknown action key", () => {
    const [entry] = buildActivityEntries([workspaceJob({ kind: "Teleport" as never })], []);

    expect(entry?.actionKey).toBe("Server_Activity_Kind_Unknown");
  });

  it("orders entries newest first and breaks equal times by ascending id on every call", () => {
    const older = workspaceJob({ timeStampUpdated: "2026-08-13T10:00:00Z" });
    const newer = workspaceJob({ timeStampUpdated: "2026-08-13T11:00:00Z" });
    const tieA = workspaceJob({ timeStampUpdated: "2026-08-13T11:00:00Z" });
    const jobs = [older, tieA, newer];

    const ids = buildActivityEntries(jobs, []).map((entry) => entry.id);
    const reversed = buildActivityEntries([...jobs].reverse(), []).map((entry) => entry.id);

    const tied = [newer, tieA].map((job) => `activity-job-${job.primaryKey}`).sort();
    expect(ids).toEqual([...tied, `activity-job-${older.primaryKey}`]);
    expect(reversed).toEqual(ids);
  });
});

type CommandStatus = components["schemas"]["EWorkerCommandStatus"];

/** The sentence key and problem tone each generated command status must map to; a new API value breaks the typecheck. */
const commandExpectations = {
  Queued: ["Server_Activity_Log_Queued", null],
  Received: ["Server_Activity_Log_Queued", null],
  Running: ["Server_Activity_Log_Running", null],
  Succeeded: ["Server_Activity_Log_Succeeded", null],
  Failed: ["Server_Activity_Log_Failed", "danger"],
  Retried: ["Server_Activity_Log_Retried", null],
  Skipped: ["Server_Activity_Log_Skipped", null],
  CancelledByCostStop: ["Server_Activity_Log_CancelledByCostStop", "warning"],
} as const satisfies Record<CommandStatus, readonly [TranslationKey, "danger" | "warning" | null]>;

/** The 26 operational audit kinds the operations endpoint returns. */
const operationalKinds = [
  "DeploymentCreated",
  "DeploymentResourceProvisioningRequested",
  "DeploymentResourceProvisioningFailed",
  "DeploymentResourceObserved",
  "DeploymentResourceAdopted",
  "DeploymentResourceReleaseRequested",
  "DeploymentResourceReleaseFailed",
  "DeploymentResourceReleased",
  "DeploymentStopped",
  "DeploymentTerminated",
  "LifecycleJobQueued",
  "LifecycleJobClaimed",
  "LifecycleJobRetryPending",
  "LifecycleJobFailed",
  "LifecycleJobSucceeded",
  "LifecycleFailed",
  "WorkerProvisioned",
  "WorkerRegistered",
  "WorkerDegraded",
  "WorkerRecovered",
  "WorkerStopped",
  "WorkerCommandQueued",
  "WorkerCommandFailed",
  "WorkerCommandSucceeded",
  "ProviderReconciliationDegraded",
  "Recovery",
] as const;

const echoKinds = [
  "LifecycleJobQueued",
  "LifecycleJobClaimed",
  "LifecycleJobRetryPending",
  "LifecycleJobFailed",
  "LifecycleJobSucceeded",
  "WorkerCommandQueued",
  "WorkerCommandFailed",
  "WorkerCommandSucceeded",
] as const;

describe("buildActivityEntries logs", () => {
  it("turns every worker command of every job into a server log entry", () => {
    const first = workspaceCommand({
      sequence: 1,
      timeStampCreated: "2026-08-13T12:00:00Z",
      timeStampStarted: "2026-08-13T12:01:00Z",
      timeStampCompleted: "2026-08-13T12:02:00Z",
      status: "Succeeded",
    });
    const second = workspaceCommand({
      sequence: 2,
      timeStampCreated: "2026-08-13T12:03:00Z",
      timeStampStarted: "2026-08-13T12:04:00Z",
      timeStampCompleted: null,
    });
    const third = workspaceCommand({
      sequence: 3,
      timeStampCreated: "2026-08-13T12:06:00Z",
      timeStampStarted: null,
      timeStampCompleted: null,
      status: "Queued",
    });
    const job = workspaceJob({
      kind: "Stop",
      status: "Running",
      requestedByName: "Falcon One",
      timeStampUpdated: "2026-08-13T11:00:00Z",
      commands: [first, second, third],
    });

    const logs = buildActivityEntries([job], []).filter((entry) => entry.type === "log");

    expect(logs.map((entry) => entry.id)).toEqual([
      `activity-log-${third.primaryKey}`,
      `activity-log-${second.primaryKey}`,
      `activity-log-${first.primaryKey}`,
    ]);
    expect(logs.map((entry) => entry.at)).toEqual([
      "2026-08-13T12:06:00Z",
      "2026-08-13T12:04:00Z",
      "2026-08-13T12:02:00Z",
    ]);
    for (const entry of logs) {
      expect(entry).toMatchObject({
        actor: { kind: "server" },
        actionKey: "Server_Action_Stop_Label",
        active: false,
        echo: false,
        apiText: null,
      });
    }
  });

  it.each(Object.entries(commandExpectations))(
    "maps the %s command status to its sentence and problem tone",
    (status, [sentenceKey, tone]) => {
      const job = workspaceJob({
        commands: [workspaceCommand({ status: status as CommandStatus })],
      });

      const log = buildActivityEntries([job], []).find((entry) => entry.type === "log");

      expect(log).toMatchObject({ sentenceKey, tone, problem: tone !== null });
    },
  );

  it("falls back to the working sentence for a command status this client does not know", () => {
    const job = workspaceJob({ commands: [workspaceCommand({ status: "Paused" as never })] });

    const log = buildActivityEntries([job], []).find((entry) => entry.type === "log");

    expect(log).toMatchObject({ sentenceKey: "Server_Activity_Log_Running", problem: false });
  });
});

describe("buildActivityEntries audit", () => {
  it.each(operationalKinds)("maps the %s kind to its audit sentence", (kind) => {
    const operation = workspaceOperation({ kind, status: "Recorded", actorName: "Falcon One" });

    const [entry] = buildActivityEntries([], [operation]);

    expect(entry).toMatchObject({
      type: "audit",
      id: `activity-audit-${operation.primaryKey}`,
      at: operation.timeStampCreated,
      sentenceKey: `Server_Activity_Audit_${kind}`,
      actor: { kind: "name", name: "Falcon One" },
      apiText: null,
      active: false,
    });
  });

  it("gives a kind outside the map the Other sentence plus the API summary", () => {
    const operation = workspaceOperation({
      kind: "SomethingNew" as never,
      summary: "The platform did something new",
    });

    const [entry] = buildActivityEntries([], [operation]);

    expect(entry).toMatchObject({
      sentenceKey: "Server_Activity_Audit_Other",
      apiText: "The platform did something new",
      echo: false,
    });
  });

  it("takes the actor only from the operation actor name", () => {
    const [platform, hidden] = buildActivityEntries(
      [],
      [
        workspaceOperation({ actorName: null, timeStampCreated: "2026-08-13T12:00:00Z" }),
        workspaceOperation({
          actorName: "Hidden member",
          timeStampCreated: "2026-08-13T11:00:00Z",
        }),
      ],
    );

    expect(platform?.actor).toEqual({ kind: "platform" });
    expect(hidden?.actor).toEqual({ kind: "hidden" });
  });

  it.each([
    ["Failed", "DeploymentResourceProvisioningFailed", "danger"],
    ["Degraded", "WorkerDegraded", "warning"],
    ["Succeeded", "DeploymentStopped", null],
    ["Queued", "DeploymentCreated", null],
  ] as const)("flags %s %s operations as %s", (status, kind, tone) => {
    const [entry] = buildActivityEntries([], [workspaceOperation({ kind, status })]);

    expect(entry).toMatchObject({ problem: tone !== null, tone });
  });

  it.each(echoKinds)("takes the action of the loaded job for the echo kind %s", (kind) => {
    const job = workspaceJob({ kind: "Restart", status: "Running" });
    const operation = workspaceOperation({ kind, lifecycleJobId: job.primaryKey });

    const audit = buildActivityEntries([job], [operation]).find((entry) => entry.type === "audit");

    expect(audit).toMatchObject({ echo: true, actionKey: "Server_Action_Restart_Label" });
    expect(audit).toHaveProperty("job", job);
  });

  it.each(echoKinds)("keeps the echo kind %s visible when its job is not loaded", (kind) => {
    const operation = workspaceOperation({
      kind,
      lifecycleJobId: "10b00000-0000-4000-8000-0000000000ff",
    });

    const [entry] = buildActivityEntries([], [operation]);

    expect(entry).toMatchObject({ echo: false, actionKey: "Server_Activity_Kind_Unknown" });
    expect(entry).toHaveProperty("job", null);
  });

  it("never marks a non-echo kind as an echo", () => {
    const job = workspaceJob();
    const operation = workspaceOperation({
      kind: "DeploymentStopped",
      lifecycleJobId: job.primaryKey,
    });

    const audit = buildActivityEntries([job], [operation]).find((entry) => entry.type === "audit");

    expect(audit).toMatchObject({ echo: false, actionKey: null });
  });
});

describe("buildActivityEntries automation", () => {
  const automationKinds = [
    "AutomaticStopWarned",
    "AutomaticStopRequested",
    "AutomaticStopCancelled",
    "ScheduledStartRequested",
    "ScheduledRunSkipped",
    "AutomationSettingsChanged",
    "ScheduledStartsConfirmed",
  ] as const;

  it("gives an automatic job its trigger line and keeps the sentence and action of a manual stop", () => {
    const [entry] = buildActivityEntries([workspaceAutomaticJob("MaxRuntime", 6)], []);

    expect(entry).toMatchObject({
      type: "job",
      sentenceKey: "Server_Activity_Job_Succeeded",
      actionKey: "Server_Action_Stop_Label",
      trigger: { key: "Server_Activity_Trigger_Max", count: 6 },
    });
  });

  it("carries no trigger for a manual job, a job without trigger and an unknown trigger", () => {
    const entries = buildActivityEntries(
      [
        workspaceJob({ trigger: "Manual", timeStampUpdated: "2026-08-13T12:03:00Z" }),
        workspaceJob({ timeStampUpdated: "2026-08-13T12:02:00Z" }),
        workspaceJob({
          trigger: "SomethingNew" as never,
          triggerLimit: 3,
          timeStampUpdated: "2026-08-13T12:01:00Z",
        }),
      ],
      [],
    );

    expect(entries).toHaveLength(3);
    for (const entry of entries) expect(entry).toHaveProperty("trigger", null);
  });

  it.each(automationKinds)("reads the audit kind %s as its own sentence", (kind) => {
    const [entry] = buildActivityEntries([], [workspaceOperation({ kind: kind as never })]);

    expect(entry).toMatchObject({
      type: "audit",
      sentenceKey: `Server_Activity_Audit_${kind}`,
      // Only the skipped run adds its API summary, the reason, after the sentence.
      apiText: kind === "ScheduledRunSkipped" ? "Lifecycle job queued" : null,
    });
  });

  it.each(["AutomaticStopRequested", "ScheduledStartRequested"] as const)(
    "hides the echo kind %s from All when its job is loaded and keeps it in Audit",
    (kind) => {
      const job = workspaceAutomaticJob("Schedule", null);
      const entries = buildActivityEntries(
        [job],
        [workspaceOperation({ kind: kind as never, lifecycleJobId: job.primaryKey })],
      );

      const audit = entries.find((entry) => entry.type === "audit");
      expect(audit).toMatchObject({ echo: true, actionKey: "Server_Action_Stop_Label" });
      expect(filterEntries(entries, "all", false).map((entry) => entry.type)).toEqual(["job"]);
      expect(filterEntries(entries, "audit", false)).toHaveLength(1);
    },
  );

  it.each(["AutomaticStopRequested", "ScheduledStartRequested"] as const)(
    "lists the echo kind %s in All when its job is not loaded",
    (kind) => {
      const entries = buildActivityEntries(
        [],
        [
          workspaceOperation({
            kind: kind as never,
            lifecycleJobId: "10b00000-0000-4000-8000-0000000000ff",
          }),
        ],
      );

      expect(filterEntries(entries, "all", false)).toHaveLength(1);
    },
  );

  it.each(["AutomaticStopWarned", "AutomaticStopCancelled", "AutomationSettingsChanged"] as const)(
    "does not treat %s as an echo",
    (kind) => {
      const job = workspaceJob();
      const [audit] = buildActivityEntries(
        [],
        [workspaceOperation({ kind: kind as never, lifecycleJobId: job.primaryKey })],
      );

      expect(audit).toMatchObject({ echo: false, actionKey: null });
    },
  );

  it("lists a skipped scheduled run under Problems as a warning with its reason", () => {
    const skipped = workspaceOperation({
      kind: "ScheduledRunSkipped" as never,
      status: "warning",
      summary: "The cost owner cannot pay for the start.",
    });
    const cancelled = workspaceOperation({
      kind: "AutomaticStopCancelled" as never,
      status: "succeeded",
      summary: "Cancelled.",
    });

    const entries = buildActivityEntries([], [skipped, cancelled]);
    const problems = filterEntries(entries, "all", true);

    expect(problems).toHaveLength(1);
    expect(problems[0]).toMatchObject({
      problem: true,
      tone: "warning",
      sentenceKey: "Server_Activity_Audit_ScheduledRunSkipped",
      apiText: "The cost owner cannot pay for the start.",
    });
  });

  it("shows no reason text for a skipped run whose summary is blank", () => {
    const [entry] = buildActivityEntries(
      [],
      [
        workspaceOperation({
          kind: "ScheduledRunSkipped" as never,
          status: "warning",
          summary: " ",
        }),
      ],
    );

    expect(entry).toMatchObject({ apiText: null });
  });
});

describe("merged activity order", () => {
  it("lists jobs, logs and audit together newest first and in a stable order for equal times", () => {
    const command = workspaceCommand({
      timeStampCompleted: "2026-08-13T12:30:00Z",
      status: "Succeeded",
    });
    const job = workspaceJob({
      status: "Succeeded",
      timeStampCompleted: "2026-08-13T12:30:00Z",
      commands: [command],
    });
    const operation = workspaceOperation({
      kind: "DeploymentStopped",
      timeStampCreated: "2026-08-13T12:30:00Z",
    });
    const newest = workspaceOperation({
      kind: "Recovery",
      timeStampCreated: "2026-08-13T13:00:00Z",
    });
    const oldest = workspaceOperation({
      kind: "Recovery",
      timeStampCreated: "2026-08-13T08:00:00Z",
    });

    const forward = buildActivityEntries([job], [operation, newest, oldest]).map(
      (entry) => entry.id,
    );
    const backward = buildActivityEntries([job], [oldest, newest, operation]).map(
      (entry) => entry.id,
    );

    const tied = [
      `activity-job-${job.primaryKey}`,
      `activity-log-${command.primaryKey}`,
      `activity-audit-${operation.primaryKey}`,
    ].sort();
    expect(forward).toEqual([
      `activity-audit-${newest.primaryKey}`,
      ...tied,
      `activity-audit-${oldest.primaryKey}`,
    ]);
    expect(backward).toEqual(forward);
  });
});

/** A fixed set of entries with every source, one echo and a problem of each tone. */
function mixedEntries(): TimelineEntry[] {
  const failed = workspaceJob({
    status: "Failed",
    timeStampUpdated: "2026-08-13T12:10:00Z",
    commands: [
      workspaceCommand({ status: "Failed", timeStampCompleted: "2026-08-13T12:09:00Z" }),
      workspaceCommand({ status: "Succeeded", timeStampCompleted: "2026-08-13T12:08:00Z" }),
    ],
  });
  const running = workspaceJob({
    status: "Running",
    timeStampUpdated: "2026-08-13T12:20:00Z",
    commands: [
      workspaceCommand({
        status: "CancelledByCostStop",
        timeStampCompleted: "2026-08-13T12:19:00Z",
      }),
    ],
  });
  const operations = [
    workspaceOperation({
      kind: "LifecycleJobFailed",
      status: "Failed",
      lifecycleJobId: failed.primaryKey,
      timeStampCreated: "2026-08-13T12:11:00Z",
    }),
    workspaceOperation({
      kind: "LifecycleJobQueued",
      lifecycleJobId: "unloaded",
      timeStampCreated: "2026-08-13T12:05:00Z",
    }),
    workspaceOperation({
      kind: "WorkerDegraded",
      status: "Degraded",
      timeStampCreated: "2026-08-13T12:15:00Z",
    }),
    workspaceOperation({
      kind: "DeploymentStopped",
      status: "Succeeded",
      timeStampCreated: "2026-08-13T12:25:00Z",
    }),
  ];
  return buildActivityEntries([failed, running], operations);
}

const filters: readonly ActivityFilter[] = ["all", "jobs", "logs", "audit"];

describe("filterEntries and filterCounts", () => {
  it("hides echo entries from All but keeps them in Audit", () => {
    const entries = mixedEntries();

    const all = filterEntries(entries, "all", false);
    const audit = filterEntries(entries, "audit", false);

    expect(all.some((entry) => entry.echo)).toBe(false);
    expect(audit.filter((entry) => entry.echo)).toHaveLength(1);
    expect(audit).toHaveLength(4);
    expect(all).toHaveLength(entries.length - 1);
  });

  it("returns only the entries of the chosen type", () => {
    const entries = mixedEntries();

    expect(filterEntries(entries, "jobs", false).every((entry) => entry.type === "job")).toBe(true);
    expect(filterEntries(entries, "logs", false).every((entry) => entry.type === "log")).toBe(true);
    expect(filterEntries(entries, "audit", false).every((entry) => entry.type === "audit")).toBe(
      true,
    );
    expect(filterEntries(entries, "jobs", false)).toHaveLength(2);
    expect(filterEntries(entries, "logs", false)).toHaveLength(3);
  });

  it("keeps only problem entries with Problems only", () => {
    const entries = mixedEntries();

    for (const filter of filters) {
      const listed = filterEntries(entries, filter, true);
      expect(listed.every((entry) => entry.problem)).toBe(true);
    }
    expect(filterEntries(entries, "jobs", true)).toHaveLength(1);
    expect(filterEntries(entries, "logs", true)).toHaveLength(2);
    expect(filterEntries(entries, "audit", true)).toHaveLength(2);
    // One problem job, two problem logs and the degraded worker; the failed-job echo is hidden from All.
    expect(filterEntries(entries, "all", true)).toHaveLength(4);
  });

  it.each([false, true])(
    "counts exactly what each filter lists with problemsOnly=%s",
    (problemsOnly) => {
      const entries = mixedEntries();

      const counts = filterCounts(entries, problemsOnly);

      for (const filter of filters) {
        expect(counts[filter]).toBe(filterEntries(entries, filter, problemsOnly).length);
      }
    },
  );

  it("counts zero for every filter of an empty timeline", () => {
    expect(filterCounts([], false)).toEqual({ all: 0, jobs: 0, logs: 0, audit: 0 });
    expect(filterCounts([], true)).toEqual({ all: 0, jobs: 0, logs: 0, audit: 0 });
  });

  it("does not change the input list", () => {
    const entries = mixedEntries();
    const before = entries.map((entry) => entry.id);

    filterEntries(entries, "all", true);
    filterCounts(entries, true);

    expect(entries.map((entry) => entry.id)).toEqual(before);
  });
});

describe("auditWindowBoundary", () => {
  const operations = (count: number) =>
    Array.from({ length: count }, (_, index) =>
      workspaceOperation({
        timeStampCreated: new Date(Date.UTC(2026, 7, 13, 12, 0, 0) - index * 60_000).toISOString(),
      }),
    );

  it("defaults the take to 200, the API maximum", () => {
    expect(activityOperationsTake).toBe(200);
  });

  it("returns null when fewer operations than the take were returned", () => {
    expect(auditWindowBoundary(operations(199))).toBeNull();
    expect(auditWindowBoundary([])).toBeNull();
    expect(auditWindowBoundary(operations(2), 3)).toBeNull();
  });

  it("returns the oldest audit time when exactly the take were returned", () => {
    const full = operations(activityOperationsTake);

    expect(auditWindowBoundary(full)).toBe(full.at(-1)?.timeStampCreated);
    expect(auditWindowBoundary([...full].reverse())).toBe(full.at(-1)?.timeStampCreated);
    expect(auditWindowBoundary(operations(3), 3)).toBe(operations(3).at(-1)?.timeStampCreated);
  });
});

describe("groupEntriesByDay", () => {
  /** A job entry whose time is the given local date. */
  function entryAt(date: Date): TimelineEntry {
    const [entry] = buildActivityEntries(
      [workspaceJob({ timeStampUpdated: date.toISOString() })],
      [],
    );
    if (!entry) throw new Error("expected one entry");
    return entry;
  }

  const now = new Date(2026, 7, 13, 15, 0, 0).getTime();

  it("groups by local calendar day, today first, then yesterday, then dated days", () => {
    const entries = buildActivityEntries(
      [
        workspaceJob({ timeStampUpdated: new Date(2026, 7, 13, 14, 0, 0).toISOString() }),
        workspaceJob({ timeStampUpdated: new Date(2026, 7, 13, 0, 5, 0).toISOString() }),
        workspaceJob({ timeStampUpdated: new Date(2026, 7, 12, 23, 55, 0).toISOString() }),
        workspaceJob({ timeStampUpdated: new Date(2026, 7, 10, 9, 0, 0).toISOString() }),
        workspaceJob({ timeStampUpdated: new Date(2025, 11, 31, 10, 0, 0).toISOString() }),
      ],
      [],
    );

    const groups = groupEntriesByDay(entries, now);

    expect(groups.map((group) => group.day.kind)).toEqual(["today", "yesterday", "date", "date"]);
    expect(groups.map((group) => group.entries.length)).toEqual([2, 1, 1, 1]);
    expect(groups.map((group) => group.key)).toEqual([
      "2026-08-13",
      "2026-08-12",
      "2026-08-10",
      "2025-12-31",
    ]);
    const dated = groups.slice(2).map((group) => group.day);
    expect(dated[0]).toMatchObject({ kind: "date", sameYear: true });
    expect(dated[1]).toMatchObject({ kind: "date", sameYear: false });
    const first = dated[0];
    expect(first?.kind === "date" ? first.date.getDate() : null).toBe(10);
  });

  it("preserves the entry order inside a group and accepts a Date as now", () => {
    const entries = [
      entryAt(new Date(2026, 7, 13, 14, 0, 0)),
      entryAt(new Date(2026, 7, 13, 9, 0, 0)),
    ];

    const groups = groupEntriesByDay(entries, new Date(now));

    expect(groups).toHaveLength(1);
    expect(groups[0]?.entries.map((entry) => entry.id)).toEqual(entries.map((entry) => entry.id));
  });

  it("returns no groups for no entries", () => {
    expect(groupEntriesByDay([], now)).toEqual([]);
  });
});
