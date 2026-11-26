import type { ComponentProps } from "react";
import type { StatusBadge } from "@/components/primitives";
import {
  accountTones,
  attendanceTones,
  eventTones,
  financeTones,
  jobTones,
  membershipRequestTones,
  membershipTones,
  operationTones,
  reportTones,
  settingTones,
  slotTones,
  toneOf,
  type StatusTone,
} from "@/components/statusTones";

type BadgeTone = NonNullable<ComponentProps<typeof StatusBadge>["tone"]>;
type Equal<A, B> =
  (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2 ? true : false;

/** Fails to compile when the lexicon tone type drifts from the StatusBadge tone union. */
const toneTypeMatchesBadge: Equal<StatusTone, BadgeTone> = true;

const allowedTones: readonly StatusTone[] = ["neutral", "info", "success", "warning", "danger"];

const domains = {
  accountTones,
  membershipTones,
  membershipRequestTones,
  attendanceTones,
  eventTones,
  operationTones,
  reportTones,
  slotTones,
  settingTones,
  financeTones,
  jobTones,
} as const;

describe("status tone lexicon", () => {
  it("uses the StatusBadge tone union as its single tone type", () => {
    expect(toneTypeMatchesBadge).toBe(true);
  });

  it.each(Object.entries(domains))("%s map holds only the five closed tones", (_name, map) => {
    expect(map.size).toBeGreaterThan(0);
    for (const tone of map.values()) expect(allowedTones).toContain(tone);
  });

  describe("toneOf", () => {
    it("returns the mapped tone for a known key", () => {
      expect(toneOf(reportTones, "published")).toBe("success");
    });

    it.each([
      ["an unmapped value", "no-such-status"],
      ["an empty string", ""],
      ["null", null],
      ["undefined", undefined],
    ])("returns neutral for %s", (_label, key) => {
      for (const map of Object.values(domains)) expect(toneOf(map, key)).toBe("neutral");
    });

    it("does not resolve inherited object properties as keys", () => {
      expect(toneOf(accountTones, "constructor")).toBe("neutral");
      expect(toneOf(accountTones, "toString")).toBe("neutral");
    });
  });

  describe("account status (members)", () => {
    it.each([
      ["verified", "success"],
      ["unverified", "neutral"],
      ["banned", "danger"],
      ["deleted", "danger"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(accountTones, key)).toBe(tone);
    });
  });

  describe("formal membership", () => {
    it.each([
      ["Active", "success"],
      ["Suspended", "warning"],
      ["Ended", "neutral"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(membershipTones, key)).toBe(tone);
    });

    it("treats a member without a formal membership as neutral", () => {
      expect(toneOf(membershipTones, undefined)).toBe("neutral");
    });
  });

  describe("attendance", () => {
    it.each([
      ["Accepted", "success"],
      ["Maybe", "warning"],
      ["Rejected", "danger"],
      ["Unassigned", "neutral"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(attendanceTones, key)).toBe(tone);
    });
  });

  describe("operation readiness", () => {
    it.each([
      ["ready", "success"],
      ["needs-signups", "warning"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(operationTones, key)).toBe(tone);
    });
  });

  describe("event admin state", () => {
    it.each([
      ["ready", "success"],
      ["low-slots", "warning"],
      ["hidden", "neutral"],
      ["archived", "neutral"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(eventTones, key)).toBe(tone);
    });
  });

  describe("slot planner status", () => {
    it.each([
      ["hidden", "neutral"],
      ["assigned", "success"],
      ["self-assignable", "info"],
      ["unassigned", "warning"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(slotTones, key)).toBe(tone);
    });
  });

  describe("field report state", () => {
    it.each([
      ["published", "success"],
      ["scheduled", "info"],
      ["draft", "neutral"],
      ["deleted", "danger"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(reportTones, key)).toBe(tone);
    });
  });

  describe("operational settings feature", () => {
    it.each([
      ["off", "neutral"],
      ["configured", "success"],
      ["missing-configuration", "warning"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(settingTones, key)).toBe(tone);
    });
  });

  describe("membership request status", () => {
    it.each([
      ["Submitted", "warning"],
      ["AcceptedForContact", "info"],
      ["Approved", "success"],
      ["Rejected", "danger"],
      ["Withdrawn", "neutral"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(membershipRequestTones, key)).toBe(tone);
    });
  });

  describe("finance", () => {
    it.each([
      ["active", "success"],
      ["frozen", "warning"],
      ["income", "success"],
      ["expense", "danger"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(financeTones, key)).toBe(tone);
    });
  });

  describe("lifecycle job status", () => {
    it.each([
      ["Queued", "info"],
      ["Running", "info"],
      ["WaitingForWorker", "info"],
      ["RetryPending", "warning"],
      ["Succeeded", "success"],
      ["Failed", "danger"],
      ["Skipped", "neutral"],
      ["CancelledByCostStop", "neutral"],
    ] as const)("%s is %s", (key, tone) => {
      expect(toneOf(jobTones, key)).toBe(tone);
    });
  });
});
