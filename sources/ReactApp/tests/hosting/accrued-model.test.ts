import { afterEach, beforeAll } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import {
  accruedPresentation,
  formatAccruedAmount,
  isNewerAccrued,
  type AccruedTranslate,
} from "@/features/hosting/accruedModel";
import { formatMoney } from "@/features/hosting/model";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServerAccruedCost } from "@/features/hosting/types";
import { fleetServer } from "../helpers/fleetFixtures";

/* The pure accrued presentation (66-UI-SPEC Accrued value states and Accrued amount rule). */

const live = deriveServerState(fleetServer({ status: "Running" }));
const maybe = deriveServerState(fleetServer({ status: "Stopped", possibleCost: true }));
const none = deriveServerState(fleetServer({ status: "Stopped", possibleCost: false }));

function accrued(
  amount: number | null,
  options: {
    isFinal?: boolean;
    reason?: string | null;
    asOf?: string;
    unit?: number | null;
    billedThrough?: string | null;
  } = {},
): GameServerAccruedCost {
  return {
    gameServerId: "s1",
    session: {
      deploymentId: "d1",
      amount,
      currency: "EUR",
      asOf: options.asOf ?? "2026-11-21T14:05:00Z",
      isFinal: options.isFinal ?? false,
      unavailableReason: options.reason ?? null,
      billingUnitSeconds: options.unit ?? null,
      billedThrough: options.billedThrough ?? null,
    },
  };
}

const noSession: GameServerAccruedCost = { gameServerId: "s1", session: null };

const translate: AccruedTranslate = (key, options) => i18n.t(key, options);

describe("accruedPresentation", () => {
  it("matches loading first, even when a value is present", () => {
    expect(accruedPresentation(accrued(1), live, true).state).toBe("loading");
    expect(accruedPresentation(undefined, live, true).value).toEqual({
      kind: "sentence",
      key: "Server_Workspace_Cost_AccruedLoading",
    });
  });

  it("reads No session yet for a null session", () => {
    const result = accruedPresentation(noSession, none, false);

    expect(result.state).toBe("none");
    expect(result.value).toEqual({ kind: "sentence", key: "Server_Workspace_Cost_AccruedNone" });
    expect(result.time).toBeNull();
  });

  it("reads the unknown sentence for a null amount and for a value that could not be loaded", () => {
    expect(accruedPresentation(accrued(null), live, false).state).toBe("unknown");
    expect(accruedPresentation(undefined, live, false).state).toBe("unknown");
    expect(accruedPresentation(accrued(null), live, false).value).toEqual({
      kind: "sentence",
      key: "Server_Workspace_Cost_AccruedUnknown",
    });
  });

  it("maps only the rate reason to a sentence", () => {
    expect(accruedPresentation(accrued(null, { reason: "rate" }), live, false).notes).toEqual([
      "Server_Workspace_Cost_AccruedReason_Rate",
    ]);
    for (const reason of ["not-yet-charged", "hidden", "something-new", null]) {
      expect(accruedPresentation(accrued(null, { reason }), live, false).notes).toEqual([]);
    }
  });

  it("uses the final term and the stopped line when final and cost is none", () => {
    const result = accruedPresentation(accrued(4.2, { isFinal: true }), none, false);

    expect(result.state).toBe("final");
    expect(result.termKey).toBe("Server_Workspace_Cost_AccruedFinal");
    expect(result.time).toEqual({
      key: "Server_Workspace_Cost_AccruedStopped",
      instant: "2026-11-21T14:05:00Z",
    });
    expect(result.costing).toBe(false);
  });

  it("lets the state model win over an API-final value while cost may still run", () => {
    for (const derived of [live, maybe]) {
      const result = accruedPresentation(accrued(4.2, { isFinal: true }), derived, false);

      expect(result.state).toBe("finalActive");
      expect(result.termKey).toBe("Server_Workspace_Cost_Accrued");
      expect(result.time?.key).toBe("Server_Workspace_Cost_AccruedAsOf");
      expect(result.notes).toEqual([]);
      expect(result.costing).toBe(true);
    }
  });

  it("adds the still-counting note when costing and the machine is not confirmed gone", () => {
    const result = accruedPresentation(accrued(1.5), maybe, false);

    expect(result.state).toBe("maybe");
    expect(result.notes).toEqual(["Server_Workspace_Cost_AccruedMaybe"]);
    expect(result.time?.key).toBe("Server_Workspace_Cost_AccruedAsOf");
  });

  it("shows only As of for a live costing session", () => {
    const result = accruedPresentation(accrued(1.5), live, false);

    expect(result.state).toBe("live");
    expect(result.notes).toEqual([]);
    expect(result.value).toEqual({ kind: "amount", amount: 1.5, currency: "EUR", costing: true });
  });
});

describe("accruedPresentation billing line", () => {
  const through = "2026-11-21T14:42:00Z";

  it("names the next hour for a live hourly session", () => {
    for (const derived of [live, maybe]) {
      const result = accruedPresentation(
        accrued(1.5, { unit: 3600, billedThrough: through }),
        derived,
        false,
      );

      expect(result.billing).toEqual({
        key: "Server_Workspace_Cost_AccruedBilling_HourNext",
        instant: through,
      });
    }
  });

  it("names the next hour while the state model overrules an API-final value", () => {
    const result = accruedPresentation(
      accrued(1.5, { isFinal: true, unit: 3600, billedThrough: through }),
      live,
      false,
    );

    expect(result.state).toBe("finalActive");
    expect(result.billing?.key).toBe("Server_Workspace_Cost_AccruedBilling_HourNext");
  });

  it("says only that the hour is started once the session is final", () => {
    const result = accruedPresentation(
      accrued(1.5, { isFinal: true, unit: 3600, billedThrough: through }),
      none,
      false,
    );

    expect(result.billing).toEqual({
      key: "Server_Workspace_Cost_AccruedBilling_Hour",
      instant: null,
    });
  });

  it("reads the static sentence for a per-minute provider", () => {
    const result = accruedPresentation(
      accrued(1.5, { unit: 60, billedThrough: through }),
      live,
      false,
    );

    expect(result.billing).toEqual({
      key: "Server_Workspace_Cost_AccruedBilling_Minute",
      instant: null,
    });
  });

  it("shows no billing line for another unit, a missing unit or a state sentence", () => {
    for (const unit of [1, 900, 7200, null]) {
      expect(
        accruedPresentation(accrued(1.5, { unit, billedThrough: through }), live, false).billing,
      ).toBeNull();
    }
    expect(accruedPresentation(accrued(1.5), live, false).billing).toBeNull();
    expect(
      accruedPresentation(accrued(null, { unit: 3600, billedThrough: through }), live, false)
        .billing,
    ).toBeNull();
    expect(accruedPresentation(noSession, none, false).billing).toBeNull();
    expect(accruedPresentation(undefined, live, true).billing).toBeNull();
    expect(accruedPresentation(undefined, live, false).billing).toBeNull();
  });
});

describe("formatAccruedAmount", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("formats one cent and above as money", () => {
    expect(formatAccruedAmount(0.01, "EUR", true, translate)).toBe(formatMoney(0.01));
    expect(formatAccruedAmount(1234.5, "EUR", false, translate)).toBe(formatMoney(1234.5));
  });

  it("reads Less than for an amount below one cent while costing and for zero while costing", () => {
    const tiny = `Less than ${formatMoney(0.01)}`;

    expect(formatAccruedAmount(0.004, "EUR", true, translate)).toBe(tiny);
    expect(formatAccruedAmount(0.0099, "EUR", false, translate)).toBe(tiny);
    expect(formatAccruedAmount(0, "EUR", true, translate)).toBe(tiny);
  });

  it("reads the zero amount for zero in a final state and never the dash", () => {
    const text = formatAccruedAmount(0, "EUR", false, translate);

    expect(text).toBe(formatMoney(0));
    expect(text).not.toContain("—");
  });

  it("formats in the German locale", async () => {
    await i18n.changeLanguage("de");

    expect(formatAccruedAmount(1.5, "EUR", true, translate)).toBe(formatMoney(1.5));
    expect(formatAccruedAmount(1.5, "EUR", true, translate)).toContain("1,50");
    expect(formatAccruedAmount(0, "EUR", true, translate)).toBe(`Weniger als ${formatMoney(0.01)}`);
  });
});

describe("isNewerAccrued", () => {
  it("accepts a value when nothing is cached", () => {
    expect(isNewerAccrued(accrued(1), undefined)).toBe(true);
  });

  it("accepts a later or equal as-of time and rejects an older one", () => {
    const current = accrued(1, { asOf: "2026-11-21T14:05:00Z" });

    expect(isNewerAccrued(accrued(2, { asOf: "2026-11-21T14:06:00Z" }), current)).toBe(true);
    expect(isNewerAccrued(accrued(2, { asOf: "2026-11-21T14:05:00Z" }), current)).toBe(true);
    expect(isNewerAccrued(accrued(0.5, { asOf: "2026-11-21T14:04:59Z" }), current)).toBe(false);
  });

  it("accepts a null session in the new value and over a null cached session", () => {
    expect(isNewerAccrued(noSession, accrued(1))).toBe(true);
    expect(isNewerAccrued(accrued(1), noSession)).toBe(true);
  });

  it("rejects an unreadable new time", () => {
    expect(isNewerAccrued(accrued(1, { asOf: "later" }), accrued(1))).toBe(false);
  });
});
