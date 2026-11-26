import { deriveServerState } from "@/features/hosting/serverState";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";

/*
 * Guard for the shared Wave 0 fleet fixtures: every later fleet test relies on these defaults deriving the
 * documented state through the one state model, `deriveServerState`.
 */

describe("shared fleet fixtures", () => {
  it("builds an off server with deterministic defaults and one enabled start action", () => {
    const server = fleetServer();

    expect(deriveServerState(server).state).toBe("off");
    expect(deriveServerState(server).cost).toBe("none");
    expect(server.deployment).toBeNull();
    expect(server.primaryKey).toBe(fleetServer().primaryKey);
    expect(server.title).toBe("Fleet Server 1");
    expect(server.gameIdentifier).toBe("arma3");
    expect(server.costOwnerName).toBe("Operations");
    expect(server).toMatchObject({ costOwnerUserId: null });
    expect(server.actions).toHaveLength(1);
    expect(server.actions[0]).toMatchObject({
      id: "start",
      enabled: true,
      costImpact: "StartsCost",
    });
  });

  it("derives running, unconfirmed, attention and working from the documented options", () => {
    const running = deriveServerState(fleetServer({ status: "Running" }));
    expect(running.state).toBe("running");
    expect(running.cost).toBe("live");

    expect(deriveServerState(fleetServer({ status: "Stopped", possibleCost: true })).state).toBe(
      "unconfirmed",
    );
    expect(deriveServerState(fleetServer({ status: "Failed" })).state).toBe("attention");
    expect(deriveServerState(fleetServer({ pendingJobCount: 1 })).state).toBe("working");
  });

  it("overrides only the passed action fields and keeps every other field defined", () => {
    const action = fleetAction("stop", {
      enabled: false,
      unavailableReason: "Server Operate permission is required.",
    });

    expect(action.enabled).toBe(false);
    expect(action.unavailableReason).toBe("Server Operate permission is required.");
    expect(action.id).toBe("stop");
    expect(action.costImpact).toBe("StopsCost");
    const fields = [
      "id",
      "label",
      "description",
      "icon",
      "group",
      "order",
      "enabled",
      "unavailableReason",
      "dangerous",
      "confirmationText",
      "confirmationChallenge",
      "pending",
      "expectedDurationSeconds",
      "costImpact",
      "costDescription",
    ] as const;
    for (const field of fields) expect(action[field]).not.toBeUndefined();
    expect(fleetAction("terminate").costImpact).toBe("StopsCost");
  });

  it("derives distinct keys from the index and never shares mutable defaults", () => {
    const first = fleetServer({ index: 1 });
    const second = fleetServer({ index: 2 });

    expect(first.primaryKey).not.toBe(second.primaryKey);
    expect(second.title).toBe("Fleet Server 2");
    expect(first.actions).not.toBe(second.actions);

    first.actions.push(fleetAction("stop"));
    first.title = "Changed";
    const runningFirst = fleetServer({ status: "Running" });
    const runningSecond = fleetServer({ status: "Running" });
    expect(runningFirst.deployment).not.toBe(runningSecond.deployment);

    const fresh = fleetServer({ index: 1 });
    expect(fresh.actions).toHaveLength(1);
    expect(fresh.title).toBe("Fleet Server 1");
  });
});
