import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, fillApiPath } from "@/api/contract";
import { normalizeLedgerKind, normalizeOwnerKind } from "@/features/hosting/financeModel";
import type {
  FinanceCostOwner,
  FinanceCostOwnerPage,
  FinanceLedgerEntry,
  FinanceOperation,
  FinancePolicy,
  FinanceProtocolEntry,
  FinanceProtocolFilters,
  FinanceProtocolPage,
  FinanceUserCandidatePage,
  FinanceCorrectableDeployment,
  FinanceOwnerKind,
} from "@/features/hosting/financeTypes";

function required<T>(value: T | undefined, operation: string): T {
  if (value === undefined) throw new Error(`${operation} returned no response body.`);
  return value;
}

function withQuery(path: string, values: object): string {
  const query = new URLSearchParams();
  Object.entries(values as Record<string, string | number | undefined>).forEach(([key, value]) => {
    if (value !== undefined && value !== "") query.set(key, String(value));
  });
  const suffix = query.toString();
  return suffix ? `${path}?${suffix}` : path;
}

function normalizeLedgerEntry(entry: FinanceLedgerEntry): FinanceLedgerEntry {
  return { ...entry, kind: normalizeLedgerKind(entry.kind) };
}

function normalizeProtocolEntry(entry: FinanceProtocolEntry): FinanceProtocolEntry {
  return {
    ...entry,
    kind: normalizeLedgerKind(entry.kind),
    costOwnerKind: normalizeOwnerKind(entry.costOwnerKind),
  };
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
  owners: (
    search: string,
    page: number,
    pageSize: number,
    kind?: FinanceOwnerKind,
    isFrozen?: boolean,
  ) => ["hosting", "finance", "owners", search, page, pageSize, kind, isFrozen] as const,
  protocol: (scope: string, filters: FinanceProtocolFilters) =>
    ["hosting", "finance", "protocol", scope, filters] as const,
  candidates: (search: string) => ["hosting", "finance", "candidates", search] as const,
  correctable: (costOwnerId: string) => ["hosting", "finance", "correctable", costOwnerId] as const,
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
    kind?: FinanceOwnerKind,
    isFrozen?: boolean,
    signal?: AbortSignal,
  ): Promise<FinanceCostOwnerPage> {
    const result = await getApiClient().request<FinanceCostOwnerPage>(
      withQuery(apiPath("/cost-owners"), {
        search: search.trim() || undefined,
        skip: Math.max(0, skip),
        take: Math.min(50, Math.max(1, take)),
        kind,
        isFrozen: isFrozen === undefined ? undefined : String(isFrozen),
      }),
      { signal },
    );
    return {
      owners: (result?.owners ?? []).map(normalizeOwner),
      totalCount: result?.totalCount ?? 0,
    };
  },

  async userCandidates(search: string, signal?: AbortSignal): Promise<FinanceUserCandidatePage> {
    const result = await getApiClient().request<FinanceUserCandidatePage>(
      withQuery(apiPath("/cost-owners/user-candidates"), {
        search: search.trim() || undefined,
        skip: 0,
        take: 20,
      }),
      { signal },
    );
    return { users: result?.users ?? [], totalCount: result?.totalCount ?? 0 };
  },

  async createOwner(
    kind: FinanceOwnerKind,
    userId?: string,
    displayName?: string,
  ): Promise<FinanceCostOwner> {
    const path = "/cost-owners" as const;
    const result = await getApiClient().request<FinanceCostOwner>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: { kind, userId: userId ?? null, displayName: displayName?.trim() || null },
    });
    return normalizeOwner(required(result, "Cost owner creation"));
  },

  async renameOwner(costOwnerId: string, displayName: string): Promise<FinanceCostOwner> {
    const path = "/cost-owners/{costOwnerId}/display-name" as const;
    const result = await getApiClient().request<FinanceCostOwner>(
      fillApiPath(path, { costOwnerId }),
      {
        method: apiMethod(path, "put"),
        body: { displayName: displayName.trim() },
      },
    );
    return normalizeOwner(required(result, "Cost owner rename"));
  },

  async setFrozen(costOwnerId: string, isFrozen: boolean): Promise<FinanceCostOwner> {
    const path = "/cost-owners/{costOwnerId}/frozen" as const;
    const result = await getApiClient().request<FinanceCostOwner>(
      fillApiPath(path, { costOwnerId }),
      {
        method: apiMethod(path, "put"),
        body: { isFrozen },
      },
    );
    return normalizeOwner(required(result, "Cost owner freeze update"));
  },

  async protocol(
    filters: FinanceProtocolFilters,
    signal?: AbortSignal,
  ): Promise<FinanceProtocolPage> {
    const result = await getApiClient().request<FinanceProtocolPage>(
      withQuery(apiPath("/cost-owners/ledger-entries"), filters),
      { signal },
    );
    return {
      entries: (result?.entries ?? []).map(normalizeProtocolEntry),
      totalCount: result?.totalCount ?? 0,
    };
  },

  async ownerProtocol(
    costOwnerId: string,
    filters: FinanceProtocolFilters,
    signal?: AbortSignal,
  ): Promise<FinanceProtocolPage> {
    const result = await getApiClient().request<FinanceProtocolPage>(
      withQuery(fillApiPath("/cost-owners/{costOwnerId}/ledger-entries", { costOwnerId }), filters),
      { signal },
    );
    return {
      entries: (result?.entries ?? []).map(normalizeProtocolEntry),
      totalCount: result?.totalCount ?? 0,
    };
  },

  async correctableDeployments(
    costOwnerId: string,
    signal?: AbortSignal,
  ): Promise<FinanceCorrectableDeployment[]> {
    return (
      (await getApiClient().request<FinanceCorrectableDeployment[]>(
        fillApiPath("/cost-owners/{costOwnerId}/correctable-deployments", { costOwnerId }),
        { signal },
      )) ?? []
    );
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
    timeStampEffectiveOrSignal?: string | AbortSignal,
    signal?: AbortSignal,
  ): Promise<FinanceCostOwner> {
    const path = "/cost-owners/{costOwnerId}/payments" as const;
    await getApiClient().request<FinanceLedgerEntry>(fillApiPath(path, { costOwnerId }), {
      method: apiMethod(path, "post"),
      body: {
        recordIdentifier: crypto.randomUUID(),
        amount,
        currency: "EUR",
        timeStampEffective:
          typeof timeStampEffectiveOrSignal === "string"
            ? timeStampEffectiveOrSignal
            : new Date().toISOString(),
        externalReference,
        reason,
      },
      signal: typeof timeStampEffectiveOrSignal === "string" ? signal : timeStampEffectiveOrSignal,
    });
    return financeService.owner(
      costOwnerId,
      typeof timeStampEffectiveOrSignal === "string" ? signal : timeStampEffectiveOrSignal,
    );
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
    timeStampEffectiveOrSignal?: string | AbortSignal,
    signal?: AbortSignal,
  ): Promise<FinanceCostOwner> {
    const path = "/cost-owners/{costOwnerId}/refunds" as const;
    await getApiClient().request<FinanceLedgerEntry>(fillApiPath(path, { costOwnerId }), {
      method: apiMethod(path, "post"),
      body: {
        recordIdentifier: crypto.randomUUID(),
        amount,
        currency: "EUR",
        timeStampEffective:
          typeof timeStampEffectiveOrSignal === "string"
            ? timeStampEffectiveOrSignal
            : new Date().toISOString(),
        externalReference,
        reason,
      },
      signal: typeof timeStampEffectiveOrSignal === "string" ? signal : timeStampEffectiveOrSignal,
    });
    return financeService.owner(
      costOwnerId,
      typeof timeStampEffectiveOrSignal === "string" ? signal : timeStampEffectiveOrSignal,
    );
  },

  async correctDeploymentCostOwner(
    sourceCostOwnerId: string,
    deploymentId: string,
    correctCostOwnerId: string,
    amount: number,
    reason: string,
    timeStampEffectiveOrSignal?: string | AbortSignal,
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
          timeStampEffective:
            typeof timeStampEffectiveOrSignal === "string"
              ? timeStampEffectiveOrSignal
              : new Date().toISOString(),
          reason,
        },
        signal:
          typeof timeStampEffectiveOrSignal === "string" ? signal : timeStampEffectiveOrSignal,
      },
    );
    return normalizeOperation(required(result, "Deployment cost-owner correction"));
  },

  async reverseLedgerEntry(
    costOwnerId: string,
    ledgerEntryId: string,
    reason: string,
    timeStampEffectiveOrSignal?: string | AbortSignal,
    signal?: AbortSignal,
  ): Promise<FinanceOperation> {
    const path = "/cost-owners/{costOwnerId}/ledger-entries/{ledgerEntryId}/reversals" as const;
    const result = await getApiClient().request<FinanceOperation>(
      fillApiPath(path, { costOwnerId, ledgerEntryId }),
      {
        method: apiMethod(path, "post"),
        body: {
          operationIdentifier: crypto.randomUUID(),
          timeStampEffective:
            typeof timeStampEffectiveOrSignal === "string"
              ? timeStampEffectiveOrSignal
              : new Date().toISOString(),
          reason,
        },
        signal:
          typeof timeStampEffectiveOrSignal === "string" ? signal : timeStampEffectiveOrSignal,
      },
    );
    return normalizeOperation(required(result, "Ledger reversal"));
  },
};
