import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, fillApiPath } from "@/api/contract";
import { normalizeLedgerKind, normalizeOwnerKind } from "@/features/hosting/financeModel";
import type {
  FinanceCostOwner,
  FinanceCostOwnerPage,
  FinanceLedgerEntry,
  FinanceOperation,
  FinancePolicy,
} from "@/features/hosting/financeTypes";

function required<T>(value: T | undefined, operation: string): T {
  if (value === undefined) throw new Error(`${operation} returned no response body.`);
  return value;
}

function withQuery(path: string, values: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== "") query.set(key, String(value));
  });
  const suffix = query.toString();
  return suffix ? `${path}?${suffix}` : path;
}

function normalizeLedgerEntry(entry: FinanceLedgerEntry): FinanceLedgerEntry {
  return { ...entry, kind: normalizeLedgerKind(entry.kind) };
}

function normalizeOwner(owner: FinanceCostOwner): FinanceCostOwner {
  return {
    ...owner,
    kind: normalizeOwnerKind(owner.kind),
    recentLedgerEntries: (owner.recentLedgerEntries ?? []).map(normalizeLedgerEntry),
  };
}

function normalizeOperation(operation: FinanceOperation): FinanceOperation {
  return {
    ...operation,
    affectedLedgerEntries: operation.affectedLedgerEntries ?? [],
    entries: (operation.entries ?? []).map(normalizeLedgerEntry),
  };
}

export const financeQueryKeys = {
  policy: ["hosting", "finance", "policy"] as const,
  ownersRoot: ["hosting", "finance", "owners"] as const,
  owners: (search: string, page: number, pageSize: number) =>
    ["hosting", "finance", "owners", search, page, pageSize] as const,
};

export const financeService = {
  async policy(signal?: AbortSignal): Promise<FinancePolicy> {
    const result = await getApiClient().request<FinancePolicy>(apiPath("/cost-owners/policy"), {
      signal,
    });
    return required(result, "Hosting finance policy");
  },

  async updatePolicy(
    minimumSafetyRangeEur: number,
    reservationSliceMinutes: number,
    signal?: AbortSignal,
  ): Promise<FinancePolicy> {
    const path = "/cost-owners/policy" as const;
    const result = await getApiClient().request<FinancePolicy>(apiPath(path), {
      method: apiMethod(path, "put"),
      body: { minimumSafetyRangeEur, reservationSliceMinutes },
      signal,
    });
    return required(result, "Hosting finance policy update");
  },

  async owners(
    search: string,
    skip: number,
    take: number,
    signal?: AbortSignal,
  ): Promise<FinanceCostOwnerPage> {
    const result = await getApiClient().request<FinanceCostOwnerPage>(
      withQuery(apiPath("/cost-owners"), {
        search: search.trim() || undefined,
        skip: Math.max(0, skip),
        take: Math.min(50, Math.max(1, take)),
      }),
      { signal },
    );
    return {
      owners: (result?.owners ?? []).map(normalizeOwner),
      totalCount: result?.totalCount ?? 0,
    };
  },

  async owner(costOwnerId: string, signal?: AbortSignal): Promise<FinanceCostOwner> {
    const result = await getApiClient().request<FinanceCostOwner>(
      fillApiPath("/cost-owners/{costOwnerId}", { costOwnerId }),
      { signal },
    );
    return normalizeOwner(required(result, "Cost owner"));
  },

  async recordPayment(
    costOwnerId: string,
    amount: number,
    externalReference: string,
    reason: string | null,
    signal?: AbortSignal,
  ): Promise<FinanceCostOwner> {
    const path = "/cost-owners/{costOwnerId}/payments" as const;
    await getApiClient().request<FinanceLedgerEntry>(fillApiPath(path, { costOwnerId }), {
      method: apiMethod(path, "post"),
      body: {
        recordIdentifier: crypto.randomUUID(),
        amount,
        currency: "EUR",
        timeStampEffective: new Date().toISOString(),
        externalReference,
        reason,
      },
      signal,
    });
    return financeService.owner(costOwnerId, signal);
  },

  async changeAllowance(
    costOwnerId: string,
    allowance: number | null,
    reason: string,
    signal?: AbortSignal,
  ): Promise<FinanceCostOwner> {
    const path = "/cost-owners/{costOwnerId}/allowance" as const;
    await getApiClient().request<FinanceLedgerEntry>(fillApiPath(path, { costOwnerId }), {
      method: apiMethod(path, "put"),
      body: { recordIdentifier: crypto.randomUUID(), allowance, reason },
      signal,
    });
    return financeService.owner(costOwnerId, signal);
  },

  async recordRefund(
    costOwnerId: string,
    amount: number,
    externalReference: string,
    reason: string,
    signal?: AbortSignal,
  ): Promise<FinanceCostOwner> {
    const path = "/cost-owners/{costOwnerId}/refunds" as const;
    await getApiClient().request<FinanceLedgerEntry>(fillApiPath(path, { costOwnerId }), {
      method: apiMethod(path, "post"),
      body: {
        recordIdentifier: crypto.randomUUID(),
        amount,
        currency: "EUR",
        timeStampEffective: new Date().toISOString(),
        externalReference,
        reason,
      },
      signal,
    });
    return financeService.owner(costOwnerId, signal);
  },

  async correctDeploymentCostOwner(
    sourceCostOwnerId: string,
    deploymentId: string,
    correctCostOwnerId: string,
    amount: number,
    reason: string,
    signal?: AbortSignal,
  ): Promise<FinanceOperation> {
    const path = "/cost-owners/{costOwnerId}/deployments/{deploymentId}/corrections" as const;
    const result = await getApiClient().request<FinanceOperation>(
      fillApiPath(path, { costOwnerId: sourceCostOwnerId, deploymentId }),
      {
        method: apiMethod(path, "post"),
        body: {
          operationIdentifier: crypto.randomUUID(),
          correctCostOwnerId,
          amount,
          currency: "EUR",
          timeStampEffective: new Date().toISOString(),
          reason,
        },
        signal,
      },
    );
    return normalizeOperation(required(result, "Deployment cost-owner correction"));
  },

  async reverseLedgerEntry(
    costOwnerId: string,
    ledgerEntryId: string,
    reason: string,
    signal?: AbortSignal,
  ): Promise<FinanceOperation> {
    const path = "/cost-owners/{costOwnerId}/ledger-entries/{ledgerEntryId}/reversals" as const;
    const result = await getApiClient().request<FinanceOperation>(
      fillApiPath(path, { costOwnerId, ledgerEntryId }),
      {
        method: apiMethod(path, "post"),
        body: {
          operationIdentifier: crypto.randomUUID(),
          timeStampEffective: new Date().toISOString(),
          reason,
        },
        signal,
      },
    );
    return normalizeOperation(required(result, "Ledger reversal"));
  },
};
