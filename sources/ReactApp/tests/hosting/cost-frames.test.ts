import { isNewerAccrued } from "@/features/hosting/accruedModel";
import {
  accruedCostMessageType,
  costAlertDismissedMessageType,
  costAlertRaisedMessageType,
  parseAccruedCostFrame,
  parseCostAlertDismissedFrame,
  parseCostAlertRaisedFrame,
} from "@/features/hosting/costFrames";
import { insertAlert } from "@/features/hosting/costAlertFormat";
import { autoStopAlert, costAlert } from "../helpers/costAlertFixtures";

/** A valid raised-alert payload as the .NET serializer sends it (camelCase, decimals as numbers). */
function raisedValue(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    $id: "2",
    alertId: "a1000000-0000-4000-8000-000000000001",
    serverId: "f1ee7000-0000-4000-8000-000000000001",
    serverName: "Ops Night",
    costOwnerId: "c0000000-0000-4000-8000-000000000001",
    thresholdAmount: 5,
    accruedAmount: 5.03,
    currency: "EUR",
    createdAt: "2026-11-21T14:05:00Z",
    ...overrides,
  };
}

describe("parseCostAlertRaisedFrame", () => {
  it("maps a valid frame to the REST alert shape and ignores extra properties", () => {
    expect(
      parseCostAlertRaisedFrame({ type: costAlertRaisedMessageType, value: raisedValue() }),
    ).toEqual({
      id: "a1000000-0000-4000-8000-000000000001",
      serverId: "f1ee7000-0000-4000-8000-000000000001",
      serverName: "Ops Night",
      costOwnerId: "c0000000-0000-4000-8000-000000000001",
      thresholdAmount: 5,
      accruedAmount: 5.03,
      currency: "EUR",
      createdAt: "2026-11-21T14:05:00Z",
    });
  });

  it("returns null for another type, a non-object value and a missing alert id", () => {
    expect(parseCostAlertRaisedFrame({ type: "Other", value: raisedValue() })).toBeNull();
    expect(parseCostAlertRaisedFrame({ type: costAlertRaisedMessageType, value: null })).toBeNull();
    expect(
      parseCostAlertRaisedFrame({ type: costAlertRaisedMessageType, value: "text" }),
    ).toBeNull();
    expect(
      parseCostAlertRaisedFrame({
        type: costAlertRaisedMessageType,
        value: raisedValue({ alertId: undefined }),
      }),
    ).toBeNull();
    expect(
      parseCostAlertRaisedFrame({
        type: costAlertRaisedMessageType,
        value: raisedValue({ alertId: "" }),
      }),
    ).toBeNull();
  });

  it.each([
    ["a non-numeric amount", { accruedAmount: "5.03" }],
    ["a non-finite threshold", { thresholdAmount: Number.POSITIVE_INFINITY }],
    ["a missing currency", { currency: undefined }],
    ["an unreadable creation time", { createdAt: "not a time" }],
    ["a non-string server name", { serverName: 7 }],
    ["a missing server id", { serverId: undefined }],
    ["a missing cost owner id", { costOwnerId: undefined }],
  ])("returns null for %s", (_label, override) => {
    expect(
      parseCostAlertRaisedFrame({
        type: costAlertRaisedMessageType,
        value: raisedValue(override),
      }),
    ).toBeNull();
  });
});

describe("parseCostAlertRaisedFrame automatic shutdown members", () => {
  it("maps an AutoStop frame with its stop instant, reason and limit", () => {
    expect(
      parseCostAlertRaisedFrame({
        type: costAlertRaisedMessageType,
        value: raisedValue({
          thresholdAmount: 0,
          accruedAmount: 0,
          kind: "AutoStop",
          stopsAt: "2026-11-21T14:35:00+00:00",
          stopReason: "Idle",
          stopLimit: 30,
        }),
      }),
    ).toEqual(
      autoStopAlert({
        id: "a1000000-0000-4000-8000-000000000001",
        serverName: "Ops Night",
        stopsAt: "2026-11-21T14:35:00+00:00",
      }),
    );
  });

  it("maps a threshold frame of a newer API with null stop members", () => {
    expect(
      parseCostAlertRaisedFrame({
        type: costAlertRaisedMessageType,
        value: raisedValue({ kind: "CostThreshold", stopsAt: null, stopReason: null }),
      }),
    ).toMatchObject({ kind: "CostThreshold", stopsAt: null, stopReason: null, stopLimit: null });
  });

  it("maps a frame without a kind exactly as before", () => {
    const alert = parseCostAlertRaisedFrame({
      type: costAlertRaisedMessageType,
      value: raisedValue(),
    });
    expect(alert).not.toHaveProperty("kind");
    expect(alert).not.toHaveProperty("stopsAt");
  });

  it.each([
    ["a malformed stop instant", { kind: "AutoStop", stopsAt: "soon" }],
    ["a non-string stop instant", { kind: "AutoStop", stopsAt: 5 }],
    ["a non-string stop reason", { kind: "AutoStop", stopReason: 3 }],
    ["a non-finite stop limit", { kind: "AutoStop", stopLimit: Number.NaN }],
    ["a non-numeric stop limit", { kind: "AutoStop", stopLimit: "30" }],
    ["a non-string kind", { kind: 7 }],
  ])("returns null for %s", (_label, override) => {
    expect(
      parseCostAlertRaisedFrame({
        type: costAlertRaisedMessageType,
        value: raisedValue(override),
      }),
    ).toBeNull();
  });
});

describe("parseCostAlertDismissedFrame", () => {
  it("returns the alert id of a valid frame", () => {
    expect(
      parseCostAlertDismissedFrame({
        type: costAlertDismissedMessageType,
        value: { $id: "2", alertId: "a1000000-0000-4000-8000-000000000001" },
      }),
    ).toBe("a1000000-0000-4000-8000-000000000001");
  });

  it("returns null for another type, a missing id and a non-object value", () => {
    expect(parseCostAlertDismissedFrame({ type: "Other", value: { alertId: "x" } })).toBeNull();
    expect(
      parseCostAlertDismissedFrame({ type: costAlertDismissedMessageType, value: {} }),
    ).toBeNull();
    expect(
      parseCostAlertDismissedFrame({ type: costAlertDismissedMessageType, value: 3 }),
    ).toBeNull();
  });
});

describe("insertAlert", () => {
  it("keeps the list newest first and adds an id only once", () => {
    const older = costAlert({ id: "older", createdAt: "2026-11-21T13:00:00Z" });
    const newer = costAlert({ id: "newer", createdAt: "2026-11-21T14:00:00Z" });
    const list = insertAlert([older], newer);
    expect(list.map((alert) => alert.id)).toEqual(["newer", "older"]);
    expect(insertAlert(list, costAlert({ id: "newer" }))).toBe(list);
  });
});

/** A valid accrued cost payload as the .NET serializer sends it. */
function accruedValue(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    $id: "2",
    serverId: "s",
    deploymentId: "d",
    amount: 1.2,
    currency: "EUR",
    asOf: "2026-11-21T14:05:00Z",
    isFinal: false,
    unavailableReason: null,
    ...overrides,
  };
}

describe("parseAccruedCostFrame billing facts", () => {
  it("puts the billing unit and the billed-through instant into the cached REST shape", () => {
    const parsed = parseAccruedCostFrame({
      type: accruedCostMessageType,
      value: accruedValue({
        billingUnitSeconds: 3600,
        billedThrough: "2026-11-21T15:42:00+00:00",
      }),
    });

    expect(parsed?.accrued.session?.billingUnitSeconds).toBe(3600);
    expect(parsed?.accrued.session?.billedThrough).toBe("2026-11-21T15:42:00+00:00");
  });

  it("reads absent and null billing facts as null", () => {
    for (const value of [
      accruedValue(),
      accruedValue({ billingUnitSeconds: null, billedThrough: null }),
    ]) {
      const parsed = parseAccruedCostFrame({ type: accruedCostMessageType, value });

      expect(parsed?.accrued.session?.billingUnitSeconds).toBeNull();
      expect(parsed?.accrued.session?.billedThrough).toBeNull();
    }
  });

  it("drops a frame with a wrongly typed billing unit or an unparseable instant", () => {
    for (const overrides of [
      { billingUnitSeconds: "3600" },
      { billingUnitSeconds: 0 },
      { billingUnitSeconds: -60 },
      { billingUnitSeconds: 1.5 },
      { billedThrough: "soon" },
      { billedThrough: 1700000000 },
    ]) {
      expect(
        parseAccruedCostFrame({ type: accruedCostMessageType, value: accruedValue(overrides) }),
      ).toBeNull();
    }
  });

  it("lets a newer frame replace the billing line and an older frame be ignored", () => {
    const parse = (overrides: Record<string, unknown>) =>
      parseAccruedCostFrame({ type: accruedCostMessageType, value: accruedValue(overrides) })
        ?.accrued;
    const current = parse({ billingUnitSeconds: 3600, billedThrough: "2026-11-21T15:00:00Z" });
    const newer = parse({
      asOf: "2026-11-21T15:01:00Z",
      amount: 1.8,
      billingUnitSeconds: 3600,
      billedThrough: "2026-11-21T16:00:00Z",
    });
    const older = parse({ asOf: "2026-11-21T14:00:00Z", billedThrough: "2026-11-21T14:30:00Z" });

    expect(current).toBeDefined();
    expect(newer).toBeDefined();
    expect(older).toBeDefined();
    expect(isNewerAccrued(newer!, current)).toBe(true);
    expect(newer?.session?.billedThrough).toBe("2026-11-21T16:00:00Z");
    expect(isNewerAccrued(older!, newer)).toBe(false);
  });
});
