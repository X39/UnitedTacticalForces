import { ApiError } from "@/api/client";
import type { FullUser } from "@/api/types";
import { getFinancePermissions } from "@/features/hosting/financeAuthorization";
import {
  allowanceSchema,
  canReverseLedgerEntry,
  correctionSchema,
  financeErrorTranslationKey,
  normalizeLedgerKind,
  normalizeOwnerKind,
  paymentSchema,
  policySchema,
  refundSchema,
} from "@/features/hosting/financeModel";
import type { FinanceLedgerEntry } from "@/features/hosting/financeTypes";

function userWithClaims(...claims: { identifier: string; value: string | null }[]): FullUser {
  return {
    primaryKey: "10000000-0000-0000-0000-000000000001",
    nickname: "Finance operator",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims,
    roles: [],
  } as FullUser;
}

function entry(
  id: string,
  kind: FinanceLedgerEntry["kind"],
  reversedEntryId: string | null = null,
): FinanceLedgerEntry {
  return { id, kind, reversedEntryId } as FinanceLedgerEntry;
}

describe("hosting finance authorization and validation", () => {
  it("projects every finance capability independently and requires empty-valued claims", () => {
    const operator = getFinancePermissions(
      userWithClaims(
        { identifier: "fin:view-all", value: null },
        { identifier: "fin:record-refund", value: null },
        { identifier: "fin:correct-ledger", value: null },
        { identifier: "fin:manage-policy", value: "resource-value" },
      ),
    );

    expect(operator).toEqual({
      view: true,
      recordPayment: false,
      recordRefund: true,
      manageAllowance: false,
      correctLedger: true,
      managePolicy: false,
      manageCostOwners: false,
      freezeCostOwner: false,
    });
    expect(getFinancePermissions(userWithClaims({ identifier: "adm", value: null }))).toEqual({
      view: true,
      recordPayment: true,
      recordRefund: true,
      manageAllowance: true,
      correctLedger: true,
      managePolicy: true,
      manageCostOwners: true,
      freezeCostOwner: true,
    });
  });

  it("validates policy, payments, refunds, allowances, and balanced corrections", () => {
    expect(
      policySchema.safeParse({ minimumSafetyRangeEur: -1, reservationSliceMinutes: 15 }).success,
    ).toBe(false);
    expect(
      paymentSchema.safeParse({
        amount: 20,
        externalReference: "",
        reason: "",
        timeStampEffective: new Date().toISOString(),
      }).success,
    ).toBe(false);
    expect(
      refundSchema.safeParse({
        amount: 20,
        externalReference: "SEPA-1",
        reason: "",
        timeStampEffective: new Date().toISOString(),
      }).success,
    ).toBe(false);
    expect(allowanceSchema.safeParse({ allowance: null, reason: "Approved" }).success).toBe(true);
    expect(
      correctionSchema.safeParse({
        sourceCostOwnerId: "10000000-0000-0000-0000-000000000001",
        deploymentId: "20000000-0000-0000-0000-000000000001",
        correctCostOwnerId: "10000000-0000-0000-0000-000000000001",
        amount: 1,
        reason: "Wrong owner",
        timeStampEffective: new Date().toISOString(),
      }).success,
    ).toBe(false);
  });

  it("normalizes legacy numeric enums and only exposes server-supported reversals", () => {
    expect(normalizeOwnerKind(1)).toBe("Verein");
    expect(normalizeLedgerKind("3")).toBe("ExternalRefund");
    expect(canReverseLedgerEntry(entry("payment", "BankTransferPayment"), [])).toBe(true);
    expect(canReverseLedgerEntry(entry("cost", "ProviderCostSlice"), [])).toBe(false);
    expect(canReverseLedgerEntry(entry("allowance", "AllowanceChanged"), [])).toBe(false);
    expect(
      canReverseLedgerEntry(entry("payment", "BankTransferPayment"), [
        entry("reversal", "LedgerReversal", "payment"),
      ]),
    ).toBe(false);
  });

  it("maps normalized API failures without exposing raw server diagnostics", () => {
    expect(financeErrorTranslationKey(new ApiError("provider secret", 409, "conflict"))).toBe(
      "HostingFinance_ErrorConflict",
    );
    expect(financeErrorTranslationKey(new Error("raw diagnostic"))).toBe(
      "HostingFinance_ErrorUnexpected",
    );
  });
});
