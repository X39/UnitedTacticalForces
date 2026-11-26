import { workspaceCommand, workspaceJob, workspaceOperation } from "../helpers/workspaceFixtures";

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

describe("workspace fixtures", () => {
  it("builds complete objects with UUID-formatted ids", () => {
    const job = workspaceJob();
    const command = workspaceCommand();
    const operation = workspaceOperation();
    expect(job.primaryKey).toMatch(uuidPattern);
    expect(job.gameServerId).toMatch(uuidPattern);
    expect(command.primaryKey).toMatch(uuidPattern);
    expect(operation.primaryKey).toMatch(uuidPattern);
    expect(Object.values(job).every((value) => value !== undefined)).toBe(true);
    expect(Object.values(command).every((value) => value !== undefined)).toBe(true);
    expect(Object.values(operation).every((value) => value !== undefined)).toBe(true);
  });

  it("gives every call a distinct id", () => {
    const ids = [
      workspaceJob().primaryKey,
      workspaceJob().primaryKey,
      workspaceCommand().primaryKey,
      workspaceCommand().primaryKey,
      workspaceOperation().primaryKey,
      workspaceOperation().primaryKey,
    ];
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("returns fresh objects so mutating one result leaves the next untouched", () => {
    const first = workspaceJob();
    first.commands.push(workspaceCommand());
    first.status = "Failed";
    first.failureReason = "Mutated";
    const second = workspaceJob();
    expect(second).not.toBe(first);
    expect(second.commands).not.toBe(first.commands);
    expect(second.commands).toEqual([]);
    expect(second.status).toBe("Running");
    expect(second.failureReason).toBeNull();
  });

  it("applies overrides over the defaults", () => {
    expect(workspaceJob({ status: "Failed", canRetry: true, attemptCount: 3 })).toMatchObject({
      status: "Failed",
      canRetry: true,
      attemptCount: 3,
      kind: "Start",
    });
    expect(workspaceCommand({ sequence: 4, status: "Failed" })).toMatchObject({
      sequence: 4,
      status: "Failed",
    });
    expect(workspaceOperation({ summary: "Custom", actorName: "Ada" })).toMatchObject({
      summary: "Custom",
      actorName: "Ada",
    });
  });

  it("defaults the actor names to null", () => {
    expect(workspaceJob().requestedByName).toBeNull();
    expect(workspaceOperation().actorName).toBeNull();
  });

  it("defaults the job to a running start job", () => {
    expect(workspaceJob()).toMatchObject({
      kind: "Start",
      actionId: "start",
      status: "Running",
      stage: "running",
      attemptCount: 1,
      canRetry: false,
      canSkip: false,
      failureCode: null,
      failureReason: null,
      timeStampCompleted: null,
      commands: [],
    });
  });

  it("defaults the command to a running worker command with sequence 1", () => {
    expect(workspaceCommand()).toMatchObject({
      sequence: 1,
      status: "Running",
      failureCode: null,
      failureReason: null,
      timeStampCompleted: null,
    });
  });

  it("defaults the operation to a queued lifecycle job", () => {
    expect(workspaceOperation().kind).toBe("LifecycleJobQueued");
  });
});
