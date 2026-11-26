import type { components } from "@/generated/api";
import {
  deriveServerState,
  deriveServerStateFromInput,
  isDeploymentActive,
  type ServerStateInput,
} from "@/features/hosting/serverState";
import type { GameServer } from "@/features/hosting/types";

type EDeploymentStatus = components["schemas"]["EDeploymentStatus"];

// A Record keeps this list exhaustive at compile time when the API adds a status.
const statusRegistry: Record<EDeploymentStatus, true> = {
  Provisioning: true,
  WaitingForWorker: true,
  Preparing: true,
  Running: true,
  Stopping: true,
  PreservationFailed: true,
  Terminating: true,
  Stopped: true,
  TerminatingDueToFault: true,
  TerminatedDueToFault: true,
  Failed: true,
  Terminated: true,
};
const knownStatuses = Object.keys(statusRegistry) as EDeploymentStatus[];

function derive(
  status: string | null | undefined,
  options: { possibleCost?: boolean; pendingJobCount?: number; hasDeployment?: boolean } = {},
) {
  const input: ServerStateInput = {
    hasDeployment: options.hasDeployment ?? status !== undefined,
    status,
    possibleCost: options.possibleCost ?? false,
    pendingJobCount: options.pendingJobCount ?? 0,
  };
  return deriveServerStateFromInput(input);
}

describe("deriveServerState", () => {
  it("never reports a server that may still cost money as off or not costing", () => {
    const statuses: Array<string | undefined> = [...knownStatuses, "SomethingNew", undefined];
    for (const status of statuses) {
      for (const possibleCost of [false, true]) {
        for (const pendingJobCount of [0, 1]) {
          for (const hasDeployment of [false, true]) {
            const derived = derive(status, { possibleCost, pendingJobCount, hasDeployment });
            const label = JSON.stringify({ status, possibleCost, pendingJobCount, hasDeployment });
            if (possibleCost) {
              expect(derived.state, label).not.toBe("off");
              expect(derived.cost, label).not.toBe("none");
            }
            if (derived.state === "off") expect(derived.cost, label).toBe("none");
          }
        }
      }
    }
  });

  it("applies the state rules in order", () => {
    for (const status of ["Failed", "PreservationFailed", "TerminatedDueToFault"]) {
      expect(derive(status)).toMatchObject({ state: "attention", tone: "danger" });
    }
    for (const status of ["Provisioning", "WaitingForWorker", "Preparing"]) {
      expect(derive(status)).toMatchObject({ state: "starting", tone: "info" });
    }
    expect(derive("Running")).toMatchObject({ state: "running", tone: "success" });
    for (const status of ["Stopping", "Terminating", "TerminatingDueToFault"]) {
      expect(derive(status)).toMatchObject({ state: "stopping", tone: "info" });
    }
    for (const status of ["Stopped", "Terminated", undefined]) {
      expect(derive(status, { possibleCost: true })).toMatchObject({
        state: "unconfirmed",
        tone: "warning",
      });
      expect(derive(status, { pendingJobCount: 2 })).toMatchObject({
        state: "working",
        tone: "info",
      });
      expect(derive(status)).toMatchObject({ state: "off", tone: "neutral" });
    }
  });

  it("derives the cost independently of the state", () => {
    for (const status of [
      "Provisioning",
      "WaitingForWorker",
      "Preparing",
      "Running",
      "Stopping",
      "Terminating",
      "TerminatingDueToFault",
    ]) {
      expect(derive(status, { possibleCost: false }).cost).toBe("live");
      expect(derive(status, { possibleCost: true }).cost).toBe("live");
    }
    for (const status of [
      "Stopped",
      "Terminated",
      "Failed",
      "PreservationFailed",
      "TerminatedDueToFault",
    ]) {
      expect(derive(status, { possibleCost: true }).cost).toBe("maybe");
      expect(derive(status, { possibleCost: false }).cost).toBe("none");
    }
    expect(derive(undefined, { possibleCost: true }).cost).toBe("maybe");
    expect(derive(undefined).cost).toBe("none");
  });

  it("falls back to unknown for a status the API added later", () => {
    expect(derive("SomethingNew")).toMatchObject({
      state: "unknown",
      tone: "neutral",
      cost: "none",
    });
    expect(derive("SomethingNew", { possibleCost: true })).toMatchObject({
      state: "unknown",
      tone: "warning",
      cost: "maybe",
    });
    expect(derive("SomethingNew").detailKey).toBeUndefined();
  });

  it("derives unknown, never off, when the server is not loaded", () => {
    expect(deriveServerState(undefined, { notification: { possibleCost: true } })).toMatchObject({
      state: "unknown",
      costVisible: true,
      cost: "maybe",
    });
    expect(deriveServerState(null, { notification: { possibleCost: false } })).toMatchObject({
      state: "unknown",
      costVisible: false,
      cost: "none",
    });
    expect(deriveServerState(undefined)).toMatchObject({ state: "unknown", costVisible: false });
  });

  it("derives from a server record", () => {
    const stopped = {
      pendingJobCount: 0,
      deployment: { status: "Stopped", possibleCost: true },
    } as unknown as GameServer;
    const never = { pendingJobCount: 1, deployment: null } as unknown as GameServer;

    expect(deriveServerState(stopped)).toMatchObject({
      state: "unconfirmed",
      cost: "maybe",
      detailKey: "Hosting_DeploymentStatus_Stopped",
    });
    expect(deriveServerState(never)).toMatchObject({ state: "working", cost: "none" });
  });

  it("treats inherited object keys as unknown statuses", () => {
    for (const status of ["constructor", "__proto__", "toString"]) {
      expect(derive(status)).toMatchObject({ state: "unknown", cost: "none" });
    }
  });
});

describe("isDeploymentActive", () => {
  function serverWith(status: string | null): Pick<GameServer, "deployment"> {
    return {
      deployment: status === null ? null : { status },
    } as unknown as Pick<GameServer, "deployment">;
  }

  it("is false when the server has no deployment", () => {
    expect(isDeploymentActive(serverWith(null))).toBe(false);
  });

  it.each(["Terminated", "TerminatedDueToFault", "terminated", "TERMINATEDDUETOFAULT"])(
    "is false for the terminated status %s",
    (status) => {
      expect(isDeploymentActive(serverWith(status))).toBe(false);
    },
  );

  it.each([...knownStatuses.filter((status) => !status.startsWith("Terminated"))])(
    "is true for the status %s",
    (status) => {
      expect(isDeploymentActive(serverWith(status))).toBe(true);
    },
  );

  it("is true for Stopped and Failed", () => {
    expect(isDeploymentActive(serverWith("Stopped"))).toBe(true);
    expect(isDeploymentActive(serverWith("Failed"))).toBe(true);
  });
});
