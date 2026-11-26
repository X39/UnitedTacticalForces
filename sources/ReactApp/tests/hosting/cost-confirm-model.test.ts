import {
  buildCostConfirmationFacts,
  hiddenCostOwnerName,
  ownerFor,
  serverCostFacts,
  unknownCostOwnerName,
} from "@/components/server/costConfirmationModel";
import type { CostConfirmationSource } from "@/components/server/useCostConfirmation";
import type { GameServer, ServerAction } from "@/features/hosting/types";

function makeServer(overrides: Partial<GameServer> = {}): GameServer {
  return {
    primaryKey: "server-1",
    title: "Alpha",
    estimatedHourlyCostEur: null,
    estimatedMonthlyCostEur: null,
    costOwnerName: "Falcon One",
    deployment: null,
    actions: [],
    pendingJobCount: 0,
    ...overrides,
  } as GameServer;
}

function makeAction(overrides: Partial<ServerAction> = {}): ServerAction {
  return {
    id: "start",
    label: "Start",
    enabled: true,
    pending: false,
    costImpact: "StartsCost",
    expectedDurationSeconds: null,
    ...overrides,
  } as ServerAction;
}

describe("buildCostConfirmationFacts", () => {
  it("prefers the deployment hourly estimate, then the server one, then null", () => {
    const deployment = { estimatedHourlyCostEur: 0.3 } as GameServer["deployment"];
    expect(
      buildCostConfirmationFacts(
        makeServer({ deployment, estimatedHourlyCostEur: 0.2 }),
        makeAction(),
      ).hourly,
    ).toBe(0.3);
    expect(
      buildCostConfirmationFacts(makeServer({ estimatedHourlyCostEur: 0.2 }), makeAction()).hourly,
    ).toBe(0.2);
    expect(buildCostConfirmationFacts(makeServer(), makeAction()).hourly).toBeNull();
  });

  it("takes the monthly estimate from the server or null", () => {
    expect(
      buildCostConfirmationFacts(makeServer({ estimatedMonthlyCostEur: 144 }), makeAction())
        .monthly,
    ).toBe(144);
    expect(buildCostConfirmationFacts(makeServer(), makeAction()).monthly).toBeNull();
  });

  it.each([
    ["NoCostImpact", "hidden"],
    ["KeepsCostRunning", "keeps-running"],
    ["Unknown", "unknown"],
    ["SomethingNew", "unknown"],
  ])("maps %s to the %s cost row", (costImpact, expected) => {
    const facts = buildCostConfirmationFacts(
      makeServer({ estimatedHourlyCostEur: 0.2 }),
      makeAction({ costImpact: costImpact as ServerAction["costImpact"] }),
    );
    expect(facts.costRow).toBe(expected);
  });

  it("shows an estimate for starts and stops only when an hourly rate exists", () => {
    const priced = makeServer({ estimatedHourlyCostEur: 0.2 });
    expect(buildCostConfirmationFacts(priced, makeAction()).costRow).toBe("estimate");
    expect(
      buildCostConfirmationFacts(priced, makeAction({ id: "stop", costImpact: "StopsCost" }))
        .costRow,
    ).toBe("estimate");
    expect(buildCostConfirmationFacts(makeServer(), makeAction()).costRow).toBe("unavailable");
  });

  it("uses the stop text only for stopping actions", () => {
    const server = makeServer();
    expect(
      buildCostConfirmationFacts(server, makeAction({ id: "stop", costImpact: "StopsCost" }))
        .howToStop,
    ).toBe("stop-text");
    expect(buildCostConfirmationFacts(server, makeAction()).howToStop).toBe("start-text");
    expect(
      buildCostConfirmationFacts(server, makeAction({ costImpact: "KeepsCostRunning" })).howToStop,
    ).toBe("start-text");
  });

  it.each([
    ["Assigned owner", { kind: "hidden" }],
    ["Unknown owner", { kind: "unknown" }],
    ["", { kind: "unknown" }],
    ["Falcon One", { kind: "name", name: "Falcon One" }],
  ])("presents the cost owner %j", (costOwnerName, expected) => {
    expect(buildCostConfirmationFacts(makeServer({ costOwnerName }), makeAction()).owner).toEqual(
      expected,
    );
  });

  it.each([
    [null, null],
    [0, null],
    [-30, null],
    [45, 1],
    [120, 2],
  ])("converts %s seconds to %s minutes", (seconds, expected) => {
    expect(
      buildCostConfirmationFacts(makeServer(), makeAction({ expectedDurationSeconds: seconds }))
        .minutes,
    ).toBe(expected);
  });
});

describe("serverCostFacts", () => {
  it("prefers the deployment hourly estimate, then the server one, then null", () => {
    const deployment = { estimatedHourlyCostEur: 0.3 } as GameServer["deployment"];
    expect(serverCostFacts(makeServer({ deployment, estimatedHourlyCostEur: 0.2 })).hourly).toBe(
      0.3,
    );
    expect(serverCostFacts(makeServer({ estimatedHourlyCostEur: 0.2 })).hourly).toBe(0.2);
    expect(serverCostFacts(makeServer()).hourly).toBeNull();
  });

  it("takes the monthly estimate from the server or null", () => {
    expect(serverCostFacts(makeServer({ estimatedMonthlyCostEur: 144 })).monthly).toBe(144);
    expect(serverCostFacts(makeServer()).monthly).toBeNull();
  });

  it("maps the cost owner through ownerFor", () => {
    expect(serverCostFacts(makeServer({ costOwnerName: "Falcon One" })).owner).toEqual({
      kind: "name",
      name: "Falcon One",
    });
    expect(serverCostFacts(makeServer({ costOwnerName: hiddenCostOwnerName })).owner).toEqual({
      kind: "hidden",
    });
    expect(serverCostFacts(makeServer({ costOwnerName: "" })).owner).toEqual({ kind: "unknown" });
  });

  it("agrees with buildCostConfirmationFacts for owner, hourly and monthly", () => {
    const server = makeServer({
      estimatedHourlyCostEur: 0.2,
      estimatedMonthlyCostEur: 144,
      costOwnerName: "Falcon One",
    });
    const facts = serverCostFacts(server);
    const confirmation = buildCostConfirmationFacts(server, makeAction());
    expect(confirmation.owner).toEqual(facts.owner);
    expect(confirmation.hourly).toBe(facts.hourly);
    expect(confirmation.monthly).toBe(facts.monthly);
  });
});

describe("CostConfirmationSource", () => {
  it("accepts the workspace header, failure, machine and create sources next to the existing ones", () => {
    const sources: CostConfirmationSource[] = [
      "workspace-primary",
      "workspace-action",
      "workspace-header",
      "workspace-failure",
      "workspace-machine",
      "fleet-card",
      "notification",
      "deep-link",
      "create",
    ];
    expect(new Set(sources).size).toBe(9);
  });
});

describe("ownerFor", () => {
  it.each([
    ["Assigned owner", { kind: "hidden" }],
    ["Unknown owner", { kind: "unknown" }],
    ["", { kind: "unknown" }],
    [null, { kind: "unknown" }],
    [undefined, { kind: "unknown" }],
    ["Falcon One", { kind: "name", name: "Falcon One" }],
  ])("maps the cost owner name %j", (name, expected) => {
    expect(ownerFor(name)).toEqual(expected);
  });

  it("matches the exported placeholder constants", () => {
    expect(ownerFor(hiddenCostOwnerName)).toEqual({ kind: "hidden" });
    expect(ownerFor(unknownCostOwnerName)).toEqual({ kind: "unknown" });
  });
});
