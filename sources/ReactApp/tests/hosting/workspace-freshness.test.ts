import { describe, expect, it } from "vitest";
import {
  deriveFreshness,
  freshnessStaleAfterMs,
  type FreshnessInput,
} from "@/features/hosting/workspaceFreshness";

/* Truth table of the live badge (ACT-02, D-11, RESEARCH Pitfall 1). */

const now = 1_700_000_000_000;

function input(overrides: Partial<FreshnessInput>): FreshnessInput {
  return {
    status: "open",
    disconnectEpoch: 0,
    dataUpdatedAt: now,
    now,
    hasActiveJob: false,
    ...overrides,
  };
}

describe("deriveFreshness", () => {
  it("reports connecting only before the first open", () => {
    expect(deriveFreshness(input({ status: "connecting", disconnectEpoch: 0 }))).toEqual({
      state: "connecting",
    });
    expect(deriveFreshness(input({ status: "connecting", disconnectEpoch: 1 }))).toEqual({
      state: "stale",
      reason: "disconnected",
    });
  });

  it("reports stale and disconnected while reconnecting or unavailable", () => {
    for (const status of ["reconnecting", "unavailable"] as const) {
      expect(deriveFreshness(input({ status, disconnectEpoch: 1 }))).toEqual({
        state: "stale",
        reason: "disconnected",
      });
    }
  });

  it("stays live with an open socket and no active job however old the data is", () => {
    expect(
      deriveFreshness(input({ dataUpdatedAt: now - 10 * 60_000, hasActiveJob: false })),
    ).toEqual({ state: "live" });
  });

  it("treats data exactly at the stale-after age as live and one millisecond older as stale", () => {
    expect(freshnessStaleAfterMs).toBe(60_000);
    expect(
      deriveFreshness(input({ hasActiveJob: true, dataUpdatedAt: now - freshnessStaleAfterMs })),
    ).toEqual({ state: "live" });
    expect(
      deriveFreshness(
        input({ hasActiveJob: true, dataUpdatedAt: now - freshnessStaleAfterMs - 1 }),
      ),
    ).toEqual({ state: "stale", reason: "old" });
  });

  it("stays live with an active job before any data was loaded", () => {
    expect(deriveFreshness(input({ hasActiveJob: true, dataUpdatedAt: 0 }))).toEqual({
      state: "live",
    });
  });
});
