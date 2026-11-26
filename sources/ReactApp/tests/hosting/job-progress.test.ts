import { describe, expect, it } from "vitest";
import type { components } from "@/generated/api";
import type { LifecycleJob } from "@/features/hosting/types";
import {
  activeJobs,
  deriveJobProgress,
  failedStepLabelKey,
  hasActiveJob,
  isActiveJobStatus,
  jobTransitionAnnouncement,
  workspaceFallbackPollMs,
  type JobProgress,
  type ProgressStepId,
} from "@/features/hosting/jobProgress";
import { workspaceCommand, workspaceJob } from "../helpers/workspaceFixtures";

/* Active-job rules behind the live badge and the fallback poll (D-11). */

function job(primaryKey: string, status: string, timeStampCreated: string): LifecycleJob {
  return { primaryKey, status, timeStampCreated } as unknown as LifecycleJob;
}

describe("job progress helpers", () => {
  it("exposes the 30 second fallback poll interval", () => {
    expect(workspaceFallbackPollMs).toBe(30_000);
  });

  it("treats exactly the four API pending statuses as active", () => {
    for (const status of ["Queued", "Running", "RetryPending", "WaitingForWorker"]) {
      expect(isActiveJobStatus(status)).toBe(true);
    }
    for (const status of ["Succeeded", "Failed", "Skipped", "CancelledByCostStop", "Unknown"]) {
      expect(isActiveJobStatus(status)).toBe(false);
    }
  });

  it("returns only active jobs, newest first, ties broken by primary key", () => {
    const jobs = [
      job("b", "Running", "2026-11-19T10:00:00Z"),
      job("z", "Succeeded", "2026-11-19T12:00:00Z"),
      job("a", "Queued", "2026-11-19T10:00:00Z"),
      job("c", "WaitingForWorker", "2026-11-19T11:00:00Z"),
    ];

    expect(activeJobs(jobs).map((entry) => entry.primaryKey)).toEqual(["c", "a", "b"]);
    expect(activeJobs(undefined)).toEqual([]);
    expect(jobs).toHaveLength(4);
  });

  it("honours the pending job count, loaded active jobs and absent data", () => {
    expect(hasActiveJob({ pendingJobCount: 1 }, [])).toBe(true);
    expect(hasActiveJob({ pendingJobCount: 1 }, undefined)).toBe(true);
    expect(
      hasActiveJob({ pendingJobCount: 0 }, [job("a", "Running", "2026-11-19T10:00:00Z")]),
    ).toBe(true);
    expect(hasActiveJob({ pendingJobCount: 0 }, [job("a", "Failed", "2026-11-19T10:00:00Z")])).toBe(
      false,
    );
    expect(hasActiveJob(null, undefined)).toBe(false);
  });
});

/** The step a result points to, or undefined when it is not in steps mode. */
function currentStep(progress: JobProgress): ProgressStepId | undefined {
  return progress.mode === "steps" ? progress.steps[progress.currentIndex] : undefined;
}

describe("deriveJobProgress", () => {
  it("derives the Start steps from status, stage and worker commands", () => {
    expect(currentStep(deriveJobProgress(workspaceJob({ status: "Queued", stage: "" })))).toBe(
      "queued",
    );
    expect(
      currentStep(
        deriveJobProgress(workspaceJob({ status: "Running", stage: "provider-provisioning" })),
      ),
    ).toBe("preparingMachine");
    expect(
      currentStep(
        deriveJobProgress(workspaceJob({ status: "WaitingForWorker", stage: "running" })),
      ),
    ).toBe("waitingForSoftware");
    expect(
      currentStep(
        deriveJobProgress(
          workspaceJob({ commands: [workspaceCommand({ status: "Running" })], stage: "running" }),
        ),
      ),
    ).toBe("workingOnServer");
    expect(
      currentStep(
        deriveJobProgress(
          workspaceJob({
            commands: [workspaceCommand({ status: "Succeeded" })],
            stage: "completed-by-worker",
          }),
        ),
      ),
    ).toBe("finishing");
  });

  it("derives the Stop and Terminate steps", () => {
    expect(
      currentStep(
        deriveJobProgress(
          workspaceJob({
            kind: "Stop",
            commands: [workspaceCommand({ status: "Received" })],
            stage: "running",
          }),
        ),
      ),
    ).toBe("savingData");
    expect(
      currentStep(
        deriveJobProgress(
          workspaceJob({
            kind: "Stop",
            commands: [workspaceCommand({ status: "Succeeded" })],
            stage: "provider-teardown",
          }),
        ),
      ),
    ).toBe("releasingMachine");
    expect(
      currentStep(deriveJobProgress(workspaceJob({ kind: "Terminate", stage: "claimed" }))),
    ).toBe("releasingMachine");
  });

  it("uses the step lists of the UI-SPEC table", () => {
    const stepsOf = (kind: string): readonly ProgressStepId[] => {
      const progress = deriveJobProgress(workspaceJob({ kind: kind as never, status: "Queued" }));
      return progress.mode === "steps" ? progress.steps : [];
    };
    expect(stepsOf("Start")).toHaveLength(5);
    expect(stepsOf("Stop")).toHaveLength(4);
    expect(stepsOf("Terminate")).toHaveLength(3);
    for (const kind of ["Restart", "Install", "Update", "CollectLogs", "Custom", "FromTheFuture"]) {
      expect(stepsOf(kind)).toEqual(["queued", "workingOnServer", "finishing"]);
    }
  });

  it("never carries a percentage, whatever the milestone percent of the job", () => {
    const progress = deriveJobProgress(workspaceJob({ progressPercent: 90 }));
    expect(JSON.stringify(progress)).not.toMatch(/percent|90/i);
  });
});

describe("failedStepLabelKey", () => {
  it("names the step a failed job stopped at from its stage and commands", () => {
    expect(
      failedStepLabelKey(workspaceJob({ status: "Failed", stage: "provider-provisioning" })),
    ).toBe("Server_Progress_Step_PreparingMachine");
    expect(
      failedStepLabelKey(
        workspaceJob({
          kind: "Stop",
          status: "Failed",
          stage: "running",
          commands: [workspaceCommand({ status: "Running" })],
        }),
      ),
    ).toBe("Server_Progress_Step_SavingData");
    expect(
      failedStepLabelKey(workspaceJob({ kind: "Terminate", status: "Failed", stage: "claimed" })),
    ).toBe("Server_Progress_Step_ReleasingMachine");
  });

  it("ignores the job status rows and falls back to Working when no step applies", () => {
    expect(failedStepLabelKey(workspaceJob({ status: "Failed", stage: "failed" }))).toBe(
      "Server_Progress_Step_Working",
    );
    expect(failedStepLabelKey(workspaceJob({ status: "Failed", stage: "" }))).toBe(
      "Server_Progress_Step_Working",
    );
    expect(
      failedStepLabelKey(
        workspaceJob({ kind: "Stop", status: "Failed", stage: "waiting-for-worker" }),
      ),
    ).toBe("Server_Progress_Step_Working");
  });
});

type JobKind = components["schemas"]["ELifecycleJobKind"];
type JobStatus = components["schemas"]["ELifecycleJobStatus"];

/* Records keyed by the generated unions: a value added to the API fails the typecheck here until it is listed. */
const kindFlags = {
  Start: true,
  Stop: true,
  Terminate: true,
  Restart: true,
  Install: true,
  Update: true,
  CollectLogs: true,
  Custom: true,
} as const satisfies Record<JobKind, true>;
const statusFlags = {
  Queued: true,
  Running: true,
  RetryPending: true,
  WaitingForWorker: true,
  Succeeded: true,
  Failed: true,
  Skipped: true,
  CancelledByCostStop: true,
} as const satisfies Record<JobStatus, true>;
const allKinds = Object.keys(kindFlags) as JobKind[];
const allStatuses = Object.keys(statusFlags) as JobStatus[];
const allStages = [
  "",
  "claimed",
  "provider-provisioning",
  "waiting-for-worker",
  "preservation-upload",
  "something-new",
];

describe("deriveJobProgress exhaustiveness", () => {
  it("covers the 8 kinds and 8 statuses of the generated unions", () => {
    expect(allKinds).toHaveLength(8);
    expect(allStatuses).toHaveLength(8);
  });

  it("returns a valid result for every kind, status, stage and command set without throwing", () => {
    const commandSets = [
      [],
      [workspaceCommand({ status: "Running" })],
      [workspaceCommand({ status: "Succeeded" })],
    ];
    let combinations = 0;
    for (const kind of allKinds) {
      for (const status of allStatuses) {
        for (const stage of allStages) {
          for (const commands of commandSets) {
            const progress = deriveJobProgress(workspaceJob({ kind, status, stage, commands }));
            combinations += 1;
            if (status === "RetryPending") expect(progress.mode).toBe("retry");
            if (status === "Queued") expect(currentStep(progress)).toBe("queued");
            if (progress.mode === "steps") {
              expect(progress.currentIndex).toBeGreaterThanOrEqual(0);
              expect(progress.currentIndex).toBeLessThan(progress.steps.length);
            }
            if (progress.mode === "generic") expect(progress.stage).toBe(stage);
            expect(JSON.stringify(progress)).not.toMatch(/percent/i);
          }
        }
      }
    }
    expect(combinations).toBe(8 * 8 * 6 * 3);
  });

  it("survives a kind and a stage this client does not know", () => {
    const progress = deriveJobProgress(
      workspaceJob({ kind: "FromTheFuture" as never, status: "Running", stage: "__proto__" }),
    );

    expect(progress).toEqual({ mode: "generic", stage: "__proto__" });
  });

  it("gives generic mode for an unknown stage without commands", () => {
    expect(
      deriveJobProgress(workspaceJob({ status: "Running", stage: "something-new", commands: [] })),
    ).toEqual({ mode: "generic", stage: "something-new" });
  });

  it("invents no step for a Stop job that waits for the worker", () => {
    expect(
      deriveJobProgress(workspaceJob({ kind: "Stop", status: "WaitingForWorker", stage: "" })),
    ).toEqual({ mode: "generic", stage: "" });
  });
});

describe("jobTransitionAnnouncement", () => {
  const running = workspaceJob({ status: "Running", timeStampCreated: "2026-08-13T12:00:00Z" });

  it("never announces the first snapshot", () => {
    expect(jobTransitionAnnouncement(undefined, [{ ...running, status: "Failed" }])).toBeNull();
  });

  it("reports a job that moved from active to Succeeded or Failed", () => {
    expect(jobTransitionAnnouncement([running], [{ ...running, status: "Succeeded" }])).toEqual({
      kind: "finished",
      job: { ...running, status: "Succeeded" },
    });
    expect(jobTransitionAnnouncement([running], [{ ...running, status: "Failed" }])?.kind).toBe(
      "failed",
    );
  });

  it("reports nothing for a job that was already final, is new, left the list or ended another way", () => {
    const failed = { ...running, status: "Failed" } as LifecycleJob;
    expect(jobTransitionAnnouncement([failed], [failed])).toBeNull();
    expect(jobTransitionAnnouncement([], [failed])).toBeNull();
    expect(jobTransitionAnnouncement([running], [])).toBeNull();
    expect(jobTransitionAnnouncement([running], [{ ...running, status: "Skipped" }])).toBeNull();
    expect(jobTransitionAnnouncement([running], [running])).toBeNull();
  });

  it("reports the newest job when two jobs transition", () => {
    const newer = workspaceJob({ status: "Queued", timeStampCreated: "2026-08-13T12:10:00Z" });

    const announcement = jobTransitionAnnouncement(
      [running, newer],
      [
        { ...running, status: "Succeeded" },
        { ...newer, status: "Failed" },
      ],
    );

    expect(announcement?.kind).toBe("failed");
    expect(announcement?.job.primaryKey).toBe(newer.primaryKey);
  });
});
