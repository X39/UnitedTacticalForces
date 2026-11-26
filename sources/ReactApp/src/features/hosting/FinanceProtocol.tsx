import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { ActionButton } from "@/components/ActionControl";
import { SelectField, TextField } from "@/components/FormFields";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";
import { FinanceAccountPicker } from "@/features/hosting/FinanceAccountPicker";
import { ledgerKindTranslationKey, ownerKindTranslationKey } from "@/features/hosting/financeModel";
import { financeQueryKeys, financeService } from "@/features/hosting/financeService";
import type { FinanceLedgerKind, FinanceProtocolEntry } from "@/features/hosting/financeTypes";
import { formatDate, formatMoney } from "@/features/hosting/model";

const protocolPageSize = 50;
const ledgerKinds: FinanceLedgerKind[] = [
  "ProviderCostSlice",
  "BankTransferPayment",
  "AllowanceChanged",
  "ExternalRefund",
  "DeploymentCostCorrectionCredit",
  "DeploymentCostCorrectionDebit",
  "LedgerReversal",
];

function Detail({ label, value }: { label: string; value: string | number | null | undefined }) {
  return value === null || value === undefined || value === "" ? null : (
    <div>
      <dt>{label}</dt>
      <dd>{String(value)}</dd>
    </div>
  );
}

export function FinanceProtocol({
  costOwnerId,
  showAccount,
  canReverse,
  onReverse,
}: {
  costOwnerId?: string;
  showAccount: boolean;
  canReverse: boolean;
  onReverse?(entry: FinanceProtocolEntry): void;
}) {
  const { t } = useTranslation();
  const [search, setSearch] = useState("");
  const [kind, setKind] = useState<FinanceLedgerKind | "">("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [accountId, setAccountId] = useState("");
  const [page, setPage] = useState(0);
  const [selected, setSelected] = useState<FinanceProtocolEntry>();
  const filters = useMemo(
    () => ({
      search: search.trim() || undefined,
      kind: kind || undefined,
      from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
      to: to ? new Date(`${to}T00:00:00`).toISOString() : undefined,
      costOwnerId: showAccount ? accountId || undefined : undefined,
      skip: page * protocolPageSize,
      take: protocolPageSize,
    }),
    [accountId, from, kind, page, search, showAccount, to],
  );
  const query = useQuery({
    queryKey: financeQueryKeys.protocol(costOwnerId ?? "global", filters),
    queryFn: ({ signal }) =>
      costOwnerId
        ? financeService.ownerProtocol(costOwnerId, filters, signal)
        : financeService.protocol(filters, signal),
  });
  const pages = Math.max(1, Math.ceil((query.data?.totalCount ?? 0) / protocolPageSize));

  return (
    <section className="finance-protocol" aria-labelledby="finance-protocol-title">
      <header className="finance-section-heading">
        <div>
          <span className="kicker">{t("HostingFinance_LedgerKicker")}</span>
          <h2 id="finance-protocol-title">{t("HostingFinance_LedgerTitle")}</h2>
        </div>
      </header>
      <div className="finance-filters">
        <div className="finance-filter--wide">
          <SearchField
            label={t("HostingFinance_SearchLabel")}
            placeholder={t("HostingFinance_ProtocolSearchPlaceholder")}
            clearLabel={t("Common_SearchClear")}
            value={search}
            onChange={(value) => {
              setSearch(value);
              setPage(0);
            }}
          />
        </div>
        <SelectField
          label={t("HostingFinance_ColumnKind")}
          value={kind}
          options={[
            { value: "", label: t("HostingFinance_FilterAll") },
            ...ledgerKinds.map((value) => ({
              value,
              label: t(ledgerKindTranslationKey(value)),
            })),
          ]}
          onChange={(event) => {
            setKind(event.target.value as FinanceLedgerKind | "");
            setPage(0);
          }}
        />
        <TextField
          type="date"
          label={t("HostingFinance_FilterFrom")}
          value={from}
          onChange={(event) => {
            setFrom(event.target.value);
            setPage(0);
          }}
        />
        <TextField
          type="date"
          label={t("HostingFinance_FilterTo")}
          value={to}
          onChange={(event) => {
            setTo(event.target.value);
            setPage(0);
          }}
        />
        {showAccount && (
          <FinanceAccountPicker
            value={accountId}
            label={t("HostingFinance_ProtocolAccount")}
            allowAll
            onChange={(value) => {
              setAccountId(value);
              setPage(0);
            }}
          />
        )}
      </div>
      {query.isLoading && <LoadingState />}
      {query.isError && <ErrorState onRetry={() => void query.refetch()} />}
      {query.data && query.data.entries.length === 0 && (
        <EmptyState title={t("HostingFinance_NoLedgerEntriesTitle")}>
          {t("HostingFinance_NoLedgerEntriesBody")}
        </EmptyState>
      )}
      {query.data && query.data.entries.length > 0 && (
        <>
          <div className="finance-table-scroll">
            <table className="finance-table finance-protocol__table">
              <thead>
                <tr>
                  {showAccount && <th>{t("HostingFinance_ProtocolAccount")}</th>}
                  <th>{t("HostingFinance_ColumnEffective")}</th>
                  <th>{t("HostingFinance_ColumnKind")}</th>
                  <th className="number">{t("HostingFinance_ColumnAmount")}</th>
                  <th className="number">{t("HostingFinance_ColumnBalance")}</th>
                  <th>{t("HostingFinance_Reference")}</th>
                  <th>{t("HostingFinance_Reason")}</th>
                  <th>{t("HostingFinance_RelatedServer")}</th>
                </tr>
              </thead>
              <tbody>
                {query.data.entries.map((entry) => {
                  const providerCost = entry.kind === "ProviderCostSlice";
                  const reference = providerCost
                    ? entry.serverTitle || entry.deploymentId
                    : entry.externalReference;
                  const reason = providerCost
                    ? t("HostingFinance_ProviderCostReason")
                    : entry.reason;
                  return (
                    <tr
                      key={entry.id}
                      tabIndex={0}
                      onClick={() => setSelected(entry)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter" || event.key === " ") setSelected(entry);
                      }}
                    >
                      {showAccount && (
                        <td>
                          <strong>{entry.costOwnerDisplayName}</strong>
                          <small>{t(ownerKindTranslationKey(entry.costOwnerKind))}</small>
                        </td>
                      )}
                      <td>{formatDate(entry.timeStampEffective)}</td>
                      <td>{t(ledgerKindTranslationKey(entry.kind))}</td>
                      <td className="number">{formatMoney(entry.amount, entry.currency)}</td>
                      <td className="number">{formatMoney(entry.balanceAfter, entry.currency)}</td>
                      <td>{reference || t("HostingFinance_None")}</td>
                      <td>{reason || t("HostingFinance_None")}</td>
                      <td>
                        {entry.serverTitle || t("HostingFinance_None")}
                        {entry.deploymentId && <small>{entry.deploymentId}</small>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <nav className="finance-pagination" aria-label={t("HostingFinance_PaginationAria")}>
            <ActionButton
              variant="ghost"
              size="compact"
              disabled={page === 0}
              onClick={() => setPage((value) => value - 1)}
            >
              {t("HostingFinance_Previous")}
            </ActionButton>
            <span>{formatResource(t("HostingFinance_PageLabel"), page + 1, pages)}</span>
            <ActionButton
              variant="ghost"
              size="compact"
              disabled={page + 1 >= pages}
              onClick={() => setPage((value) => value + 1)}
            >
              {t("HostingFinance_Next")}
            </ActionButton>
          </nav>
        </>
      )}
      {selected && (
        <>
          <div
            className="finance-drawer-scrim"
            aria-hidden="true"
            onClick={() => setSelected(undefined)}
          />
          <aside
            className="finance-drawer"
            role="dialog"
            aria-modal="true"
            aria-labelledby="finance-entry-details"
            onKeyDown={(event) => {
              if (event.key === "Escape") setSelected(undefined);
            }}
          >
            <header>
              <div>
                <span className="kicker">{t("HostingFinance_AuditDetails")}</span>
                <h2 id="finance-entry-details">{t(ledgerKindTranslationKey(selected.kind))}</h2>
              </div>
              <ActionButton variant="ghost" size="compact" onClick={() => setSelected(undefined)}>
                {t("HostingFinance_Close")}
              </ActionButton>
            </header>
            <dl>
              <Detail label={t("HostingFinance_EntryId")} value={selected.id} />
              <Detail
                label={t("HostingFinance_ProtocolAccount")}
                value={`${selected.costOwnerDisplayName} (${selected.costOwnerId})`}
              />
              <Detail
                label={t("HostingFinance_ColumnEffective")}
                value={formatDate(selected.timeStampEffective)}
              />
              <Detail
                label={t("HostingFinance_Recorded")}
                value={formatDate(selected.timeStampRecorded)}
              />
              <Detail
                label={t("HostingFinance_RecordedBy")}
                value={selected.recordedByDisplayName || selected.recordedById}
              />
              <Detail
                label={t("HostingFinance_RecordIdentifier")}
                value={selected.recordIdentifier}
              />
              <Detail
                label={t("HostingFinance_FinanceOperation")}
                value={selected.financeOperationId}
              />
              <Detail label={t("HostingFinance_ReversedEntry")} value={selected.reversedEntryId} />
              <Detail label={t("HostingFinance_ReversalEntry")} value={selected.reversalEntryId} />
              <Detail label={t("HostingFinance_Reference")} value={selected.externalReference} />
              <Detail label={t("HostingFinance_Reason")} value={selected.reason} />
              <Detail label={t("HostingFinance_DeploymentId")} value={selected.deploymentId} />
              <Detail label={t("HostingFinance_ServerId")} value={selected.serverId} />
              <Detail
                label={t("HostingFinance_ProviderIdentifier")}
                value={selected.providerIdentifier}
              />
              <Detail
                label={t("HostingFinance_HourlyRate")}
                value={
                  selected.estimatedHourlyRate === null
                    ? null
                    : formatMoney(selected.estimatedHourlyRate, selected.currency)
                }
              />
              <Detail
                label={t("HostingFinance_SliceInterval")}
                value={
                  selected.sliceStartsAt && selected.sliceEndsAt
                    ? `${formatDate(selected.sliceStartsAt)} – ${formatDate(selected.sliceEndsAt)}`
                    : null
                }
              />
              <Detail
                label={t("HostingFinance_PreviousAllowance")}
                value={selected.previousAllowance}
              />
              <Detail label={t("HostingFinance_NewAllowance")} value={selected.newAllowance} />
              <Detail
                label={t("HostingFinance_Counterparty")}
                value={selected.counterpartyDisplayName || selected.counterpartyCostOwnerId}
              />
            </dl>
            {canReverse && selected.isReversible && onReverse && (
              <ActionButton variant="danger" onClick={() => onReverse(selected)}>
                {t("HostingFinance_ReverseEntry")}
              </ActionButton>
            )}
          </aside>
        </>
      )}
    </section>
  );
}
