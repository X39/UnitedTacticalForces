import type { components } from "@/generated/api";

type Schema<Name extends keyof components["schemas"]> = components["schemas"][Name];

export type FinanceOwnerKind = components["schemas"]["ECostOwnerKind"];
export type FinanceLedgerKind = components["schemas"]["EHostingLedgerEntryKind"];

export type FinanceLedgerEntry = Required<Schema<"HostingLedgerEntryDto">>;
export type FinanceCostOwner = Omit<
  Required<Schema<"HostingCostOwnerDto">>,
  "recentLedgerEntries"
> & {
  recentLedgerEntries: FinanceLedgerEntry[];
};

export interface FinanceCostOwnerPage {
  owners: FinanceCostOwner[];
  totalCount: number;
}

export type FinancePolicy = Required<Schema<"HostingFinancePolicyDto">>;
export type FinanceProtocolEntry = Required<Schema<"HostingFinanceProtocolEntryDto">>;
export type FinanceUserCandidate = Required<Schema<"HostingCostOwnerUserCandidateDto">>;
export type FinanceCorrectableDeployment = Required<Schema<"HostingCorrectableDeploymentDto">>;

export interface FinanceProtocolPage {
  entries: FinanceProtocolEntry[];
  totalCount: number;
}

export interface FinanceUserCandidatePage {
  users: FinanceUserCandidate[];
  totalCount: number;
}

export interface FinanceProtocolFilters {
  search?: string | undefined;
  kind?: FinanceLedgerKind | undefined;
  from?: string | undefined;
  to?: string | undefined;
  costOwnerId?: string | undefined;
  skip: number;
  take: number;
}
export type FinanceOperation = Omit<
  Required<Schema<"HostingFinanceOperationDto">>,
  "affectedLedgerEntries" | "entries"
> & {
  affectedLedgerEntries: Required<Schema<"HostingFinanceAffectedLedgerEntryDto">>[];
  entries: FinanceLedgerEntry[];
};
