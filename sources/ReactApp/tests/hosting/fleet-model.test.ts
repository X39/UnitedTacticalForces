import type { components } from "@/generated/api";
import {
  countCostlyServers,
  countFilters,
  diffFleetStates,
  fleetFilters,
  fleetSearchMaxLength,
  fleetStageKey,
  foldSearchText,
  matchesFilter,
  orderFleetServers,
  parseFleetSearchParams,
  partitionFleet,
  primaryActionFor,
  searchServers,
  writeFleetSearchParams,
  type FleetPrimary,
} from "@/features/hosting/fleetModel";
import { deriveServerState, type DerivedServerState } from "@/features/hosting/serverState";
import type { GameServer } from "@/features/hosting/types";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";

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

/** Every status the exhaustive loops visit: the known ones, one the client does not know, and no deployment. */
const loopStatuses: Array<string | undefined> = [...knownStatuses, "SomethingNew", undefined];

/** Builds one server per combination of status, possible cost and pending work, with unique keys. */
function everyServerCombination(): GameServer[] {
  const servers: GameServer[] = [];
  let index = 1;
  for (const status of loopStatuses) {
    for (const possibleCost of [false, true]) {
      for (const pendingJobCount of [0, 1]) {
        servers.push(
          fleetServer({
            index,
            title: `Combination ${index.toString().padStart(3, "0")}`,
            status,
            possibleCost,
            pendingJobCount,
          }),
        );
        index += 1;
      }
    }
  }
  return servers;
}

const viewerId = "a0000000-0000-4000-8000-0000000000aa";

describe("fleet URL parameters", () => {
  it("trims and caps the query and whitelists the filter", () => {
    expect(parseFleetSearchParams(new URLSearchParams("q=%20%20alpha%20%20&filter=mine"))).toEqual({
      q: "alpha",
      filter: "mine",
    });
    expect(parseFleetSearchParams(new URLSearchParams("filter=running")).filter).toBe("running");
    expect(parseFleetSearchParams(new URLSearchParams("filter=off")).filter).toBe("off");
    expect(parseFleetSearchParams(new URLSearchParams("filter=all")).filter).toBe("all");
    expect(parseFleetSearchParams(new URLSearchParams("filter=")).filter).toBe("all");
    expect(parseFleetSearchParams(new URLSearchParams("filter=MINE")).filter).toBe("all");
    expect(parseFleetSearchParams(new URLSearchParams("filter=%3Cscript%3E")).filter).toBe("all");
    expect(parseFleetSearchParams(new URLSearchParams("")).filter).toBe("all");
    expect(parseFleetSearchParams(new URLSearchParams("")).q).toBe("");

    const long = parseFleetSearchParams(new URLSearchParams({ q: `  ${"x".repeat(500)}` }));
    expect(long.q).toHaveLength(fleetSearchMaxLength);
    expect(fleetSearchMaxLength).toBe(200);
  });

  it("never ends a capped query with half of a surrogate pair or trailing whitespace", () => {
    const emoji = parseFleetSearchParams(new URLSearchParams({ q: `${"x".repeat(199)}😀` }));
    expect(emoji.q).toBe("x".repeat(199));

    const spaced = parseFleetSearchParams(new URLSearchParams({ q: `${"x".repeat(199)} tail` }));
    expect(spaced.q).toBe("x".repeat(199));
  });

  it("writes only the provided members, omits defaults and preserves other parameters in order", () => {
    const current = new URLSearchParams("tab=files&q=old&action=terminate&filter=mine");

    const written = writeFleetSearchParams(current, { q: "  new text  " });
    expect(written.toString()).toBe("tab=files&q=new+text&action=terminate&filter=mine");
    expect(written).not.toBe(current);
    expect(current.toString()).toBe("tab=files&q=old&action=terminate&filter=mine");

    expect(writeFleetSearchParams(current, { q: "   " }).toString()).toBe(
      "tab=files&action=terminate&filter=mine",
    );
    expect(writeFleetSearchParams(current, { filter: "all" }).toString()).toBe(
      "tab=files&q=old&action=terminate",
    );
    expect(writeFleetSearchParams(current, { filter: "off" }).toString()).toBe(
      "tab=files&q=old&action=terminate&filter=off",
    );
    expect(writeFleetSearchParams(current, { q: "", filter: "all" }).toString()).toBe(
      "tab=files&action=terminate",
    );
    expect(
      writeFleetSearchParams(new URLSearchParams("tab=files"), {
        q: "alpha",
        filter: "running",
      }).toString(),
    ).toBe("tab=files&q=alpha&filter=running");
    expect(writeFleetSearchParams(current, {}).toString()).toBe(current.toString());
  });

  it("round-trips through parse and write", () => {
    for (const filter of fleetFilters) {
      const written = writeFleetSearchParams(new URLSearchParams("tab=x"), { q: "ops", filter });
      expect(parseFleetSearchParams(written)).toEqual({ q: "ops", filter });
    }
  });
});

describe("fleet search", () => {
  const muller = fleetServer({ index: 1, title: "Müller Operation", costOwnerName: "Ops Team" });
  const night = fleetServer({ index: 2, title: "operation night", gameIdentifier: "dayz" });
  const shouting = fleetServer({ index: 3, title: "MÜLLER OPERATION", costOwnerName: "Logistik" });
  const servers = [muller, night, shouting];

  it("folds case and diacritics with the given locale", () => {
    expect(foldSearchText("MÜLLER Ñandú", "de-DE")).toBe("muller nandu");
    expect(searchServers(servers, "muller", "en-US")).toEqual([muller, shouting]);
    expect(searchServers(servers, "OPERATION", "en-US")).toEqual([muller, night, shouting]);
    expect(searchServers([shouting], "müller", "de-DE")).toEqual([shouting]);
    expect(searchServers([shouting], "muller", "de-DE")).toEqual([shouting]);
  });

  it("requires every term across title, game identifier and cost owner in any order", () => {
    expect(searchServers(servers, "night dayz", "en-US")).toEqual([night]);
    expect(searchServers(servers, "DAYZ   night", "en-US")).toEqual([night]);
    expect(searchServers(servers, "operation team", "en-US")).toEqual([muller]);
    expect(searchServers(servers, "logistik müller", "de-DE")).toEqual([shouting]);
    expect(searchServers(servers, "night arma3", "en-US")).toEqual([]);
  });

  it("does not match a term across two fields", () => {
    const server = fleetServer({ index: 4, title: "Alpha", gameIdentifier: "arma3" });
    expect(searchServers([server], "alphaarma3", "en-US")).toEqual([]);
    expect(searchServers([server], "haar", "en-US")).toEqual([]);
  });

  it("returns the input array for a blank or whitespace-only query", () => {
    expect(searchServers(servers, "", "en-US")).toBe(servers);
    expect(searchServers(servers, "  \t \n ", "en-US")).toBe(servers);
  });

  it("never indexes the cost owner placeholders", () => {
    const hidden = fleetServer({ index: 5, title: "Hidden", costOwnerName: "Assigned owner" });
    const unknown = fleetServer({ index: 6, title: "Unknown", costOwnerName: "Unknown owner" });
    const real = fleetServer({ index: 7, title: "Real", costOwnerName: "Shipowner Guild" });
    const fleet = [hidden, unknown, real];

    expect(searchServers(fleet, "assigned", "en-US")).toEqual([]);
    expect(searchServers(fleet, "owner", "en-US")).toEqual([real]);
    expect(searchServers(fleet, "unknown owner", "en-US")).toEqual([]);
    expect(searchServers(fleet, "hidden", "en-US")).toEqual([hidden]);
  });
});

describe("fleet filters", () => {
  it("matches Mine on the creator or the cost owner, ignoring case", () => {
    const created = fleetServer({ index: 1, createdById: viewerId });
    const owned = fleetServer({ index: 2, costOwnerUserId: viewerId });
    const upper = fleetServer({ index: 3, createdById: viewerId.toUpperCase() });
    const upperOwner = fleetServer({ index: 4, costOwnerUserId: viewerId.toUpperCase() });
    const other = fleetServer({ index: 5 });

    expect(matchesFilter(created, "mine", viewerId)).toBe(true);
    expect(matchesFilter(owned, "mine", viewerId)).toBe(true);
    expect(matchesFilter(upper, "mine", viewerId)).toBe(true);
    expect(matchesFilter(upperOwner, "mine", viewerId)).toBe(true);
    expect(matchesFilter(other, "mine", viewerId)).toBe(false);
    for (const server of [created, owned, upper, upperOwner, other]) {
      expect(matchesFilter(server, "mine", undefined)).toBe(false);
      expect(matchesFilter(server, "mine", "")).toBe(false);
    }
  });

  it("matches Running on live or maybe cost and Off on the derived off state only", () => {
    for (const server of everyServerCombination()) {
      const derived = deriveServerState(server);
      const label = JSON.stringify({ title: server.title, derived });
      expect(matchesFilter(server, "all", viewerId), label).toBe(true);
      expect(matchesFilter(server, "running", viewerId), label).toBe(derived.cost !== "none");
      expect(matchesFilter(server, "off", viewerId), label).toBe(derived.state === "off");
    }

    const live = fleetServer({ index: 1, status: "Running" });
    const maybe = fleetServer({ index: 2, status: "Stopped", possibleCost: true });
    const none = fleetServer({ index: 3, noDeployment: true });
    expect(matchesFilter(live, "running", viewerId)).toBe(true);
    expect(matchesFilter(maybe, "running", viewerId)).toBe(true);
    expect(matchesFilter(none, "running", viewerId)).toBe(false);
    expect(matchesFilter(none, "off", viewerId)).toBe(true);
    expect(matchesFilter(maybe, "off", viewerId)).toBe(false);
  });

  it("counts every filter over the same input it is given", () => {
    const fleet = everyServerCombination().map((server, index) =>
      index % 3 === 0 ? { ...server, createdById: viewerId } : server,
    );
    const searched = searchServers(fleet, "combination 01", "en-US");
    expect(searched.length).toBeGreaterThan(0);
    expect(searched.length).toBeLessThan(fleet.length);

    for (const input of [fleet, searched, []]) {
      const counts = countFilters(input, viewerId);
      for (const filter of fleetFilters) {
        expect(counts[filter], filter).toBe(
          input.filter((server) => matchesFilter(server, filter, viewerId)).length,
        );
      }
    }
    expect(countFilters(fleet, viewerId).all).toBe(fleet.length);
    expect(countFilters(fleet, undefined).mine).toBe(0);
  });
});

describe("fleet sections", () => {
  it("puts a server in Off if and only if its derived state is off, and every server in one section", () => {
    const servers = everyServerCombination();
    const { active, off } = partitionFleet(servers);

    for (const server of servers) {
      const derived = deriveServerState(server);
      const label = JSON.stringify({ title: server.title, derived });
      const inOff = off.includes(server);
      const inActive = active.includes(server);
      expect(inOff, label).toBe(derived.state === "off");
      expect(inOff !== inActive, label).toBe(true);
      if (server.deployment?.possibleCost) expect(inOff, label).toBe(false);
    }
    expect(active.length + off.length).toBe(servers.length);
  });

  it("orders Active by possible cost, then pending work, then title, and Off by title only", () => {
    const zulu = fleetServer({ index: 1, title: "Zulu", status: "Running" });
    const mike = fleetServer({ index: 2, title: "Mike", status: "Stopped", pendingJobCount: 1 });
    const bravo = fleetServer({ index: 3, title: "bravo", status: "Failed", possibleCost: false });
    const alpha = fleetServer({
      index: 4,
      title: "Alpha",
      status: "SomethingNew",
      possibleCost: false,
    });
    const offCharlie = fleetServer({ index: 5, title: "charlie", noDeployment: true });
    const offAerger = fleetServer({ index: 6, title: "Ärger", noDeployment: true });
    const offAlpha = fleetServer({ index: 7, title: "Alpha", status: "Terminated" });

    const { active, off } = partitionFleet([
      offCharlie,
      bravo,
      offAerger,
      mike,
      alpha,
      zulu,
      offAlpha,
    ]);

    expect(active.map((server) => server.title)).toEqual(["Zulu", "Mike", "Alpha", "bravo"]);
    expect(off.map((server) => server.title)).toEqual(["Alpha", "Ärger", "charlie"]);
  });

  it("keeps the previous fleet ordering for the existing three-server example", () => {
    const charlie = fleetServer({
      index: 1,
      title: "Charlie",
      status: "Running",
      possibleCost: false,
    });
    const alpha = fleetServer({
      index: 2,
      title: "Alpha",
      status: "Running",
      possibleCost: false,
      pendingJobCount: 1,
    });
    const bravo = fleetServer({ index: 3, title: "Bravo", status: "Running", possibleCost: true });
    const input = [charlie, alpha, bravo];

    expect(orderFleetServers(input).map((server) => server.title)).toEqual([
      "Bravo",
      "Alpha",
      "Charlie",
    ]);
    expect(input.map((server) => server.title)).toEqual(["Charlie", "Alpha", "Bravo"]);
  });
});

const pausedReason = "Hosting lifecycle execution is paused by the operator.";
const stateReason = "This action is unavailable in the current deployment state.";

/** Narrows a primary decision to an action, failing the test otherwise. */
function expectAction(primary: FleetPrimary): Extract<FleetPrimary, { kind: "action" }> {
  expect(primary.kind).toBe("action");
  if (primary.kind !== "action") throw new Error("expected an action primary");
  return primary;
}

describe("fleet primary action", () => {
  it("offers start for an off server, enabled only when the API and launch state allow it", () => {
    const server = fleetServer({ noDeployment: true });
    expect(deriveServerState(server).state).toBe("off");
    expect(primaryActionFor(server)).toEqual({
      kind: "action",
      action: server.actions[0],
      variant: "gold",
      enabled: true,
    });

    const disabled = fleetServer({
      noDeployment: true,
      actions: [fleetAction("start", { enabled: false, unavailableReason: stateReason })],
    });
    expect(expectAction(primaryActionFor(disabled)).enabled).toBe(false);

    const pending = fleetServer({
      noDeployment: true,
      actions: [fleetAction("start", { pending: true })],
    });
    expect(expectAction(primaryActionFor(pending)).enabled).toBe(false);

    const launchDisabled = fleetServer({ noDeployment: true, isLaunchDisabled: true });
    const launchPrimary = expectAction(primaryActionFor(launchDisabled));
    expect(launchPrimary.variant).toBe("gold");
    expect(launchPrimary.enabled).toBe(false);

    for (const actions of [[], [fleetAction("stop")], [fleetAction("terminate")]]) {
      const missing = fleetServer({ noDeployment: true, actions });
      expect(primaryActionFor(missing)).toEqual({ kind: "open-only" });
    }
  });

  it("offers stop for a running server, enabled only when the API enables it and nothing is pending", () => {
    const server = fleetServer({ status: "Running" });
    expect(primaryActionFor(server)).toEqual({
      kind: "action",
      action: server.actions[0],
      variant: "danger",
      enabled: true,
    });

    const disabled = fleetServer({
      status: "Running",
      actions: [fleetAction("stop", { enabled: false, unavailableReason: stateReason })],
    });
    expect(expectAction(primaryActionFor(disabled)).enabled).toBe(false);

    const pending = fleetServer({
      status: "Running",
      actions: [fleetAction("stop", { pending: true })],
    });
    expect(expectAction(primaryActionFor(pending)).enabled).toBe(false);

    const missing = fleetServer({
      status: "Running",
      actions: [fleetAction("start"), fleetAction("terminate")],
    });
    expect(primaryActionFor(missing)).toEqual({ kind: "open-only" });
  });

  it("shows an in-progress control for starting, stopping and working servers", () => {
    const starting = fleetServer({ status: "Provisioning" });
    const stopping = fleetServer({ status: "Stopping" });
    const terminating = fleetServer({ status: "Terminating" });
    for (const server of [starting, stopping, terminating]) {
      expect(primaryActionFor(server)).toEqual({ kind: "in-progress", reason: "state" });
    }

    const queued = fleetServer({ status: "Provisioning", pendingJobCount: 1 });
    expect(primaryActionFor(queued)).toEqual({ kind: "in-progress", reason: "pending" });

    const pendingAction = fleetServer({
      status: "Stopping",
      actions: [fleetAction("stop", { pending: true })],
    });
    expect(primaryActionFor(pendingAction)).toEqual({ kind: "in-progress", reason: "pending" });

    const working = fleetServer({ status: "Stopped", pendingJobCount: 1 });
    expect(deriveServerState(working).state).toBe("working");
    expect(primaryActionFor(working)).toEqual({ kind: "in-progress", reason: "pending" });

    for (const server of everyServerCombination()) {
      const derived = deriveServerState(server);
      if (["starting", "stopping", "working"].includes(derived.state)) {
        expect(primaryActionFor(server, derived).kind, server.title).toBe("in-progress");
      }
    }
  });

  it("offers stop for attention and unconfirmed servers, mirroring the API, and never terminate", () => {
    const failed = fleetServer({
      status: "Failed",
      possibleCost: false,
      actions: [
        fleetAction("stop", { enabled: false, unavailableReason: stateReason }),
        fleetAction("terminate"),
      ],
    });
    expect(deriveServerState(failed).state).toBe("attention");
    const failedPrimary = expectAction(primaryActionFor(failed));
    expect(failedPrimary.action.id).toBe("stop");
    expect(failedPrimary.variant).toBe("danger");
    expect(failedPrimary.enabled).toBe(false);

    const unconfirmed = fleetServer({
      status: "Stopped",
      possibleCost: true,
      actions: [
        fleetAction("stop", { enabled: false, unavailableReason: stateReason }),
        fleetAction("terminate"),
      ],
    });
    expect(deriveServerState(unconfirmed).state).toBe("unconfirmed");
    const unconfirmedPrimary = expectAction(primaryActionFor(unconfirmed));
    expect(unconfirmedPrimary.action.id).toBe("stop");
    expect(unconfirmedPrimary.enabled).toBe(false);

    const enabledStop = fleetServer({ status: "PreservationFailed" });
    expect(expectAction(primaryActionFor(enabledStop)).enabled).toBe(true);

    for (const status of ["Failed", "TerminatedDueToFault", "PreservationFailed"]) {
      const onlyTerminate = fleetServer({ status, actions: [fleetAction("terminate")] });
      expect(primaryActionFor(onlyTerminate), status).toEqual({ kind: "open-only" });
    }
    const unconfirmedTerminate = fleetServer({
      status: "Stopped",
      possibleCost: true,
      actions: [fleetAction("terminate")],
    });
    expect(primaryActionFor(unconfirmedTerminate)).toEqual({ kind: "open-only" });

    for (const server of everyServerCombination()) {
      server.actions = [fleetAction("terminate"), fleetAction("start"), fleetAction("stop")];
      const primary = primaryActionFor(server);
      if (primary.kind === "action") expect(primary.action.id, server.title).not.toBe("terminate");
    }
  });

  it("shows only Open server for an unknown state and a disabled control for a paused control plane", () => {
    const unknown = fleetServer({
      status: "SomethingNew",
      actions: [fleetAction("start"), fleetAction("stop")],
    });
    expect(deriveServerState(unknown).state).toBe("unknown");
    expect(primaryActionFor(unknown)).toEqual({ kind: "open-only" });

    const pausedOff = fleetServer({
      noDeployment: true,
      actions: [
        fleetAction("start", { enabled: false, unavailableReason: pausedReason }),
        fleetAction("stop", { enabled: false, unavailableReason: pausedReason }),
      ],
    });
    const pausedOffPrimary = expectAction(primaryActionFor(pausedOff));
    expect(pausedOffPrimary.action.id).toBe("start");
    expect(pausedOffPrimary.enabled).toBe(false);

    const pausedRunning = fleetServer({
      status: "Running",
      actions: [
        fleetAction("start", { enabled: false, unavailableReason: pausedReason }),
        fleetAction("stop", { enabled: false, unavailableReason: pausedReason }),
      ],
    });
    const pausedRunningPrimary = expectAction(primaryActionFor(pausedRunning));
    expect(pausedRunningPrimary.action.id).toBe("stop");
    expect(pausedRunningPrimary.enabled).toBe(false);
  });

  it("matches action ids ignoring case", () => {
    const off = fleetServer({ noDeployment: true, actions: [fleetAction("Start")] });
    const offPrimary = expectAction(primaryActionFor(off));
    expect(offPrimary.action.id).toBe("Start");
    expect(offPrimary.enabled).toBe(true);

    const running = fleetServer({ status: "Running", actions: [fleetAction("STOP")] });
    expect(expectAction(primaryActionFor(running)).action.id).toBe("STOP");

    const terminateOnly = fleetServer({ status: "Running", actions: [fleetAction("TERMINATE")] });
    expect(primaryActionFor(terminateOnly)).toEqual({ kind: "open-only" });
  });
});

describe("fleet stage line", () => {
  it("uses the deployment status key while starting or stopping and the queued change while working", () => {
    expect(fleetStageKey(deriveServerState(fleetServer({ status: "Provisioning" })))).toBe(
      "Hosting_DeploymentStatus_Provisioning",
    );
    expect(fleetStageKey(deriveServerState(fleetServer({ status: "WaitingForWorker" })))).toBe(
      "Hosting_DeploymentStatus_WaitingForWorker",
    );
    expect(fleetStageKey(deriveServerState(fleetServer({ status: "Terminating" })))).toBe(
      "Hosting_DeploymentStatus_Terminating",
    );
    expect(
      fleetStageKey(deriveServerState(fleetServer({ status: "Stopped", pendingJobCount: 1 }))),
    ).toBe("Server_Fleet_QueuedChange");
    expect(
      fleetStageKey(deriveServerState(fleetServer({ noDeployment: true, pendingJobCount: 1 }))),
    ).toBe("Server_Fleet_QueuedChange");

    const startingWithoutDetail: DerivedServerState = {
      state: "starting",
      cost: "live",
      tone: "info",
      labelKey: "Server_State_Starting",
      costLabelKey: "Server_Cost_Live",
      costVisible: true,
    };
    expect(fleetStageKey(startingWithoutDetail)).toBe("Server_Fleet_QueuedChange");
  });

  it("returns no stage line for every other state", () => {
    for (const server of everyServerCombination()) {
      const derived = deriveServerState(server);
      const inMotion = ["starting", "stopping", "working"].includes(derived.state);
      expect(fleetStageKey(derived) !== null, server.title).toBe(inMotion);
    }
  });
});

describe("fleet cost count", () => {
  it("counts servers whose derived cost is live or maybe", () => {
    const fleet = [
      fleetServer({ index: 1, status: "Running" }),
      fleetServer({ index: 2, status: "Stopped", possibleCost: true }),
      fleetServer({ index: 3, noDeployment: true }),
      fleetServer({ index: 4, status: "Failed", possibleCost: false }),
    ];
    expect(countCostlyServers(fleet)).toBe(2);
    expect(countCostlyServers([])).toBe(0);

    const combinations = everyServerCombination();
    expect(countCostlyServers(combinations)).toBe(
      combinations.filter((server) => deriveServerState(server).cost !== "none").length,
    );
  });
});

describe("fleet state diff", () => {
  it("reports only servers present in both snapshots whose derived state changed, in next order", () => {
    const previous = [
      fleetServer({ index: 1, title: "Alpha", status: "Running" }),
      fleetServer({ index: 2, title: "Bravo", noDeployment: true }),
      fleetServer({ index: 3, title: "Charlie", status: "Running" }),
      fleetServer({ index: 4, title: "Delta", status: "Running" }),
      fleetServer({ index: 6, title: "Foxtrot", noDeployment: true }),
    ];
    const next = [
      fleetServer({ index: 3, title: "Charlie", status: "Stopping" }),
      fleetServer({ index: 1, title: "Alpha", status: "Running" }),
      fleetServer({ index: 2, title: "Bravo renamed", status: "Provisioning" }),
      fleetServer({ index: 5, title: "Echo", status: "Running" }),
      fleetServer({ index: 6, title: "Foxtrot renamed", noDeployment: true }),
    ];

    expect(diffFleetStates(previous, next)).toEqual([
      {
        serverId: next[0]?.primaryKey,
        name: "Charlie",
        stateLabelKey: "Server_State_Stopping",
      },
      {
        serverId: next[2]?.primaryKey,
        name: "Bravo renamed",
        stateLabelKey: "Server_State_Starting",
      },
    ]);
    expect(diffFleetStates(next, next)).toEqual([]);
    expect(diffFleetStates([], next)).toEqual([]);
    expect(diffFleetStates(previous, [])).toEqual([]);
  });
});

/** A seeded linear congruential generator, so the generated fleet is identical on every run. */
function seededRandom(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    return state / 0x1_0000_0000;
  };
}

describe("fleet model at the expected fleet size", () => {
  it("searches, filters, partitions and counts 150 servers correctly and quickly", () => {
    const random = seededRandom(6207);
    const pick = <T>(values: readonly T[]): T => {
      const value = values[Math.floor(random() * values.length)];
      if (value === undefined) throw new Error("empty pick");
      return value;
    };
    const words = ["Alpha", "Bravo", "Night", "Ops", "Liberation", "Training", "Zeus", "Antistasi"];
    const owners = [
      "Assigned owner",
      "Unknown owner",
      "Alpha Company",
      "Quartermaster",
      "Logistics",
    ];
    const fleet = Array.from({ length: 150 }, (_, offset) =>
      fleetServer({
        index: offset + 1,
        title: `${pick(words)} ${pick(words)} ${offset + 1}`,
        gameIdentifier: pick(["arma3", "dayz", "reforger"]),
        status: loopStatuses[Math.floor(random() * loopStatuses.length)],
        possibleCost: random() < 0.5,
        pendingJobCount: random() < 0.2 ? 1 : 0,
        costOwnerName: pick(owners),
        createdById: random() < 0.25 ? viewerId : undefined,
      }),
    );

    const started = performance.now();
    const searched = searchServers(fleet, "alpha", "en-US");
    const counts = countFilters(searched, viewerId);
    const filtered = searched.filter((server) => matchesFilter(server, "running", viewerId));
    const whole = partitionFleet(fleet);
    const sections = partitionFleet(filtered);
    const costly = countCostlyServers(fleet);
    const elapsed = performance.now() - started;

    expect(elapsed).toBeLessThan(1000);

    const expectedSearch = fleet.filter((server) => {
      const owner = ["Assigned owner", "Unknown owner"].includes(server.costOwnerName)
        ? ""
        : server.costOwnerName;
      return [server.title, server.gameIdentifier, owner]
        .join("\n")
        .toLowerCase()
        .includes("alpha");
    });
    expect(searched).toEqual(expectedSearch);
    expect(counts.all).toBe(searched.length);
    expect(counts.running).toBe(filtered.length);
    expect(sections.active.length + sections.off.length).toBe(filtered.length);
    expect(sections.off).toEqual([]);

    const ids = [...whole.active, ...whole.off].map((server) => server.primaryKey);
    expect(ids).toHaveLength(150);
    expect(new Set(ids).size).toBe(150);
    expect(whole.off.every((server) => deriveServerState(server).state === "off")).toBe(true);
    expect(costly).toBe(fleet.filter((server) => deriveServerState(server).cost !== "none").length);
  });
});
