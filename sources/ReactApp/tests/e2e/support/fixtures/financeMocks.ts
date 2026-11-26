import { costAlertSettings } from "../../../helpers/costAlertFixtures";
import { jsonHandler, type ApiHandler } from "../mockApi";

/**
 * Handler factories for the cost alert settings page and the hosting finance pages: the owner list with its filters,
 * one account with its ledger and cost alert panel, and the policy settings.
 *
 * Every body is synthetic: proper-noun owner names, fixed instants and invented amounts, so no screenshot or German
 * walk depends on real data (T-69-34).
 */

/** The claims of the viewer every finance and cost alert entry signs in with: server access and full finance rights. */
export const financeClaims: readonly string[] = ["sv", "fin"];

/** The cost owners of the fixtures. */
export const financeOwnerIds = {
  /** The viewer's personal owner, which the cost alert page edits without a query parameter. */
  personal: "c0000000-0000-4000-8000-000000000001",
  /** A frozen shared owner. */
  frozen: "c0000000-0000-4000-8000-000000000002",
  /** An external organization owner. */
  organization: "c0000000-0000-4000-8000-000000000003",
  /** An owner whose alert settings the viewer may read but not change. */
  readOnly: "c0000000-0000-4000-8000-000000000004",
} as const;

/** The display name of the personal owner. */
export const personalOwnerName = "Nova";

/**
 * Builds one ledger entry of the owner detail.
 *
 * @param number A running number that makes the id and the instants unique.
 * @param costOwnerId The owner the entry belongs to.
 * @param kind The ledger entry kind.
 * @param amount The signed balance delta.
 * @param balanceAfter The balance after the entry.
 * @returns The entry in the shape of `HostingLedgerEntryDto`.
 */
function ledgerEntry(
  number: number,
  costOwnerId: string,
  kind: string,
  amount: number,
  balanceAfter: number,
) {
  return {
    id: `1e000000-0000-4000-8000-${String(number).padStart(12, "0")}`,
    costOwnerId,
    deploymentId: null,
    kind,
    amount,
    balanceAfter,
    previousAllowance: null,
    newAllowance: null,
    currency: "EUR",
    timeStampEffective: `2026-11-0${number}T09:00:00Z`,
    timeStampRecorded: `2026-11-0${number}T09:00:00Z`,
    recordedById: null,
    recordIdentifier: null,
    financeOperationId: null,
    reversedEntryId: null,
    externalReference: kind === "BankTransferPayment" ? "REF-1001" : null,
    reason: null,
  };
}

/**
 * Builds one cost owner with its recent ledger.
 *
 * @param costOwnerId The owner id.
 * @param kind The owner kind.
 * @param displayName The owner name.
 * @param isFrozen Whether the owner is frozen.
 * @param balance The balance in euro.
 * @returns The owner in the shape of `HostingCostOwnerDto`.
 */
function owner(
  costOwnerId: string,
  kind: string,
  displayName: string,
  isFrozen: boolean,
  balance: number,
) {
  return {
    costOwnerId,
    kind,
    userId: kind === "User" ? "10000000-0000-4000-8000-000000000001" : null,
    displayName,
    isFrozen,
    balance,
    allowance: kind === "User" ? 20 : null,
    reservedProviderCost: 1.5,
    spendableAmount: balance - 1.5,
    currency: "EUR",
    timeStampUpdated: "2026-11-10T09:00:00Z",
    recentLedgerEntries: [
      ledgerEntry(2, costOwnerId, "ProviderCostSlice", -1.2, balance),
      ledgerEntry(1, costOwnerId, "BankTransferPayment", 50, balance + 1.2),
    ],
  };
}

/** The cost owners the list shows. */
const owners = [
  owner(financeOwnerIds.personal, "User", personalOwnerName, false, 42.5),
  owner(financeOwnerIds.frozen, "Verein", "Pax", true, 3.1),
  owner(financeOwnerIds.organization, "ExternalOrganization", "Atlas GmbH", false, 310),
];

/**
 * Builds the protocol entries of a ledger read for one owner, or for every owner.
 *
 * @param ownerId The owner the entries belong to, or null for the entries of all owners.
 * @returns The page body in the shape of `HostingFinanceProtocolPageDto`.
 */
function protocolPage(ownerId: string | null) {
  const target = owners.filter((item) => ownerId === null || item.costOwnerId === ownerId);
  const entries = target.flatMap((item) =>
    item.recentLedgerEntries.map((entry) => ({
      ...entry,
      costOwnerDisplayName: item.displayName,
      costOwnerKind: item.kind,
      serverId: entry.kind === "ProviderCostSlice" ? "f1ee7000-0000-4000-8000-000000000001" : null,
      serverTitle: entry.kind === "ProviderCostSlice" ? "Orion-7" : null,
      recordedByDisplayName: null,
      reversalEntryId: null,
      isReversible: entry.kind === "BankTransferPayment",
      providerIdentifier: null,
      estimatedHourlyRate: null,
      sliceStartsAt: null,
      sliceEndsAt: null,
      counterpartyCostOwnerId: null,
      counterpartyDisplayName: null,
    })),
  );
  return { entries, totalCount: entries.length };
}

/** The finance policy the settings page edits. */
const policy = {
  minimumSafetyRangeEur: 5,
  reservationSliceMinutes: 60,
  timeStampUpdated: "2026-11-10T09:00:00Z",
  updatedById: "10000000-0000-4000-8000-000000000001",
};

/** Options of {@link costAlertHandlers}. */
export interface CostAlertMockOptions {
  /** Whether saving answers 409, which the page shows as a conflict. Defaults to true. */
  conflictOnSave?: boolean | undefined;
}

/**
 * Builds the handlers of the cost alert settings endpoints.
 *
 * The personal owner and the shared organization owner can be changed, the read-only owner can only be read, and a
 * save answers 409 unless the options say otherwise, which is how the conflict state is reached.
 *
 * @param options Whether a save conflicts.
 * @returns The handlers in the order the strict layer tries them.
 */
export function costAlertHandlers(options: CostAlertMockOptions = {}): ApiHandler[] {
  const personal = () =>
    costAlertSettings({ costOwnerId: financeOwnerIds.personal, costOwnerName: personalOwnerName });
  const organization = () =>
    costAlertSettings({
      costOwnerId: financeOwnerIds.organization,
      costOwnerName: "Atlas GmbH",
      audience: "Group",
    });
  const readOnly = () =>
    costAlertSettings({
      costOwnerId: financeOwnerIds.readOnly,
      costOwnerName: "Pax",
      canManage: false,
      audience: "Group",
    });
  return [
    jsonHandler("GET", "/api/cost-owners/mine/alert-settings", personal),
    jsonHandler("GET", `/api/cost-owners/${financeOwnerIds.personal}/alert-settings`, personal),
    jsonHandler(
      "GET",
      `/api/cost-owners/${financeOwnerIds.organization}/alert-settings`,
      organization,
    ),
    jsonHandler("GET", `/api/cost-owners/${financeOwnerIds.readOnly}/alert-settings`, readOnly),
    jsonHandler(
      "PUT",
      `/api/cost-owners/${financeOwnerIds.personal}/alert-settings`,
      { title: "Conflict" },
      options.conflictOnSave === false ? 200 : 409,
    ),
  ];
}

/**
 * Builds the handlers of the hosting finance pages: policy, owner list with its filters, owner detail and ledger.
 *
 * The owner list honors the `kind`, `isFrozen` and `search` query parameters, so a filter state shows a different
 * set. The write endpoints (payment, refund, allowance, correction, rename, freeze, reversal, policy update, owner
 * creation) are not answered: no registry state submits them, so a request to one is a recorded failure.
 *
 * @returns The handlers in the order the strict layer tries them.
 */
export function financeHandlers(): ApiHandler[] {
  return [
    jsonHandler("GET", "/api/cost-owners/policy", () => policy),
    jsonHandler("GET", "/api/cost-owners/ledger-entries", () => protocolPage(null)),
    jsonHandler("GET", "/api/cost-owners", (url: URL) => {
      const kind = url.searchParams.get("kind");
      const frozen = url.searchParams.get("isFrozen");
      const search = (url.searchParams.get("search") ?? "").toLocaleLowerCase();
      const matching = owners.filter(
        (item) =>
          (kind === null || item.kind === kind) &&
          (frozen === null || String(item.isFrozen) === frozen) &&
          (search === "" || item.displayName.toLocaleLowerCase().includes(search)),
      );
      return { owners: matching, totalCount: matching.length };
    }),
    ...owners.flatMap((item) => [
      jsonHandler("GET", `/api/cost-owners/${item.costOwnerId}`, () => item),
      jsonHandler("GET", `/api/cost-owners/${item.costOwnerId}/ledger-entries`, () =>
        protocolPage(item.costOwnerId),
      ),
    ]),
    ...costAlertHandlers(),
  ];
}
