import { z } from "zod";
import { ApiError } from "@/api/client";
import type { TranslationKey } from "@/app/localizationTypes";
import type {
  FinanceLedgerEntry,
  FinanceLedgerKind,
  FinanceOwnerKind,
} from "@/features/hosting/financeTypes";

export const policySchema = z.object({
  minimumSafetyRangeEur: z.number().finite().min(0),
  reservationSliceMinutes: z.number().int().min(1).max(1440),
});

export const paymentSchema = z.object({
  amount: z.number().finite().positive(),
  externalReference: z.string().trim().min(1).max(128),
  reason: z.string().trim().max(512),
});

export const allowanceSchema = z.object({
  allowance: z.number().finite().min(0).nullable(),
  reason: z.string().trim().min(1).max(512),
});

export const refundSchema = z.object({
  amount: z.number().finite().positive(),
  externalReference: z.string().trim().min(1).max(128),
  reason: z.string().trim().min(1).max(512),
});

export const correctionSchema = z
  .object({
    sourceCostOwnerId: z.string().uuid(),
    deploymentId: z.string().uuid(),
    correctCostOwnerId: z.string().uuid(),
    amount: z.number().finite().positive(),
    reason: z.string().trim().min(1).max(512),
  })
  .refine((value) => value.sourceCostOwnerId !== value.correctCostOwnerId, {
    path: ["correctCostOwnerId"],
  });

export const reversalSchema = z.object({
  reason: z.string().trim().min(1).max(512),
});

const ownerKinds = ["User", "Verein", "ExternalOrganization"] as const;
const ledgerKinds = [
  "ProviderCostSlice",
  "BankTransferPayment",
  "AllowanceChanged",
  "ExternalRefund",
  "DeploymentCostCorrectionCredit",
  "DeploymentCostCorrectionDebit",
  "LedgerReversal",
] as const;

export function normalizeOwnerKind(value: unknown): FinanceOwnerKind {
  const index =
    typeof value === "number"
      ? value
      : typeof value === "string" && /^\d+$/.test(value)
        ? Number(value)
        : undefined;
  if (index !== undefined && Number.isInteger(index))
    return ownerKinds[index] ?? "ExternalOrganization";
  return ownerKinds.includes(value as (typeof ownerKinds)[number])
    ? (value as FinanceOwnerKind)
    : "ExternalOrganization";
}

export function normalizeLedgerKind(value: unknown): FinanceLedgerKind {
  const index =
    typeof value === "number"
      ? value
      : typeof value === "string" && /^\d+$/.test(value)
        ? Number(value)
        : undefined;
  if (index !== undefined && Number.isInteger(index))
    return ledgerKinds[index] ?? "ProviderCostSlice";
  return ledgerKinds.includes(value as (typeof ledgerKinds)[number])
    ? (value as FinanceLedgerKind)
    : "ProviderCostSlice";
}

export function ownerKindTranslationKey(kind: FinanceOwnerKind): TranslationKey {
  const keys: Record<FinanceOwnerKind, TranslationKey> = {
    User: "HostingFinance_OwnerMember",
    Verein: "HostingFinance_OwnerVerein",
    ExternalOrganization: "HostingFinance_OwnerOrganization",
  };
  return keys[kind];
}

export function ledgerKindTranslationKey(kind: FinanceLedgerKind): TranslationKey {
  return `HostingFinance_LedgerKind_${kind}` as TranslationKey;
}

export function canReverseLedgerEntry(
  entry: FinanceLedgerEntry,
  recentEntries: FinanceLedgerEntry[],
): boolean {
  if (entry.kind === "ProviderCostSlice" || entry.kind === "AllowanceChanged") return false;
  return !recentEntries.some((candidate) => candidate.reversedEntryId === entry.id);
}

export function financeErrorTranslationKey(error: unknown): TranslationKey {
  if (!(error instanceof ApiError)) return "HostingFinance_ErrorUnexpected";
  const keys: Record<ApiError["code"], TranslationKey> = {
    unauthorized: "HostingFinance_NoAccessMessage",
    forbidden: "HostingFinance_ErrorForbidden",
    "not-found": "HostingFinance_ErrorNotFound",
    conflict: "HostingFinance_ErrorConflict",
    "rate-limited": "HostingFinance_ErrorRateLimited",
    network: "HostingFinance_ErrorNetwork",
    server: "HostingFinance_ErrorServer",
    invalid: "HostingFinance_ErrorInvalid",
  };
  return keys[error.code];
}
