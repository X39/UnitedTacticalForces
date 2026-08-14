import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { HostingError, HostingLoading } from "@/features/hosting/components";
import { getFinancePermissions } from "@/features/hosting/financeAuthorization";
import {
  allowanceSchema,
  canReverseLedgerEntry,
  correctionSchema,
  financeErrorTranslationKey,
  ledgerKindTranslationKey,
  ownerKindTranslationKey,
  paymentSchema,
  policySchema,
  refundSchema,
  reversalSchema,
} from "@/features/hosting/financeModel";
import { financeQueryKeys, financeService } from "@/features/hosting/financeService";
import type { FinanceCostOwner, FinanceLedgerEntry } from "@/features/hosting/financeTypes";
import { formatDate, formatMoney } from "@/features/hosting/model";

const pageSize = 20;

type Confirmation =
  | { kind: "refund"; owner: FinanceCostOwner }
  | { kind: "correction"; owner: FinanceCostOwner }
  | { kind: "reversal"; owner: FinanceCostOwner; entry: FinanceLedgerEntry };

function OwnerSummary({ owner }: { owner: FinanceCostOwner }) {
  const { t } = useTranslation();
  return (
    <section className="next-hosting-panel" aria-labelledby="finance-owner-title">
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">{t(ownerKindTranslationKey(owner.kind))}</span>
          <h2 id="finance-owner-title">{owner.displayName}</h2>
          <p className="next-hosting-muted">
            {t("HostingFinance_OwnerId")}: <code>{owner.costOwnerId}</code>
          </p>
        </div>
        <StatusBadge tone={owner.isFrozen ? "warning" : "success"}>
          {owner.isFrozen ? t("HostingFinance_Frozen") : t("HostingFinance_Active")}
        </StatusBadge>
      </header>
      {owner.isFrozen && <StatePanel kind="warning">{t("HostingFinance_FrozenNotice")}</StatePanel>}
      <dl className="next-hosting-facts">
        <div>
          <dt>{t("HostingFinance_Balance")}</dt>
          <dd>{formatMoney(owner.balance, owner.currency)}</dd>
        </div>
        <div>
          <dt>{t("HostingFinance_Allowance")}</dt>
          <dd>
            {owner.allowance === null
              ? t("HostingFinance_None")
              : formatMoney(owner.allowance, owner.currency)}
          </dd>
        </div>
        <div>
          <dt>{t("HostingFinance_Reserved")}</dt>
          <dd>{formatMoney(owner.reservedProviderCost, owner.currency)}</dd>
        </div>
        <div>
          <dt>{t("HostingFinance_Spendable")}</dt>
          <dd>{formatMoney(owner.spendableAmount, owner.currency)}</dd>
        </div>
      </dl>
      <small className="next-hosting-muted">
        {formatResource(t("HostingFinance_OwnerUpdated"), formatDate(owner.timeStampUpdated))}
      </small>
    </section>
  );
}

function AuditDetails({ entry }: { entry: FinanceLedgerEntry }) {
  const { t } = useTranslation();
  const values: Array<[TranslationKey, string | null | undefined]> = [
    ["HostingFinance_EntryId", entry.id],
    ["HostingFinance_Recorded", formatDate(entry.timeStampRecorded)],
    ["HostingFinance_RecordedBy", entry.recordedById],
    ["HostingFinance_RecordIdentifier", entry.recordIdentifier],
    ["HostingFinance_FinanceOperation", entry.financeOperationId],
    ["HostingFinance_DeploymentId", entry.deploymentId],
    ["HostingFinance_ReversedEntry", entry.reversedEntryId],
  ];
  if (entry.previousAllowance !== null)
    values.push([
      "HostingFinance_PreviousAllowance",
      formatMoney(entry.previousAllowance, entry.currency),
    ]);
  if (entry.newAllowance !== null)
    values.push(["HostingFinance_NewAllowance", formatMoney(entry.newAllowance, entry.currency)]);
  return (
    <details className="next-hosting-audit-details">
      <summary>{t("HostingFinance_AuditDetails")}</summary>
      <dl>
        {values.map(([label, value]) => (
          <div key={label}>
            <dt>{t(label)}</dt>
            <dd>{value ? <code>{value}</code> : t("HostingFinance_NotAvailable")}</dd>
          </div>
        ))}
      </dl>
    </details>
  );
}

export function HostingFinancePage() {
  const { t } = useTranslation();
  useDocumentTitle(t("HostingFinance_PageTitle"));
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getFinancePermissions(user);
  const queryClient = useQueryClient();

  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(0);
  const [selectedId, setSelectedId] = useState<string>();
  const [minimumReserve, setMinimumReserve] = useState<number>();
  const [reservationSlice, setReservationSlice] = useState<number>();
  const [paymentAmount, setPaymentAmount] = useState(0);
  const [paymentReference, setPaymentReference] = useState("");
  const [paymentReason, setPaymentReason] = useState("");
  const [allowance, setAllowance] = useState(0);
  const [revokeAllowance, setRevokeAllowance] = useState(false);
  const [allowanceReason, setAllowanceReason] = useState("");
  const [refundAmount, setRefundAmount] = useState(0);
  const [refundReference, setRefundReference] = useState("");
  const [refundReason, setRefundReason] = useState("");
  const [deploymentId, setDeploymentId] = useState("");
  const [correctOwnerId, setCorrectOwnerId] = useState("");
  const [correctionAmount, setCorrectionAmount] = useState(0);
  const [correctionReason, setCorrectionReason] = useState("");
  const [reversalReason, setReversalReason] = useState("");
  const [reversalTarget, setReversalTarget] = useState<FinanceLedgerEntry>();
  const [confirmation, setConfirmation] = useState<Confirmation>();
  const [feedbackError, setFeedbackError] = useState<TranslationKey>();
  const [feedbackSuccess, setFeedbackSuccess] = useState<TranslationKey>();

  const policy = useQuery({
    queryKey: financeQueryKeys.policy,
    queryFn: ({ signal }) => financeService.policy(signal),
    enabled: permissions.view,
  });
  const owners = useQuery({
    queryKey: financeQueryKeys.owners(search, page, pageSize),
    queryFn: ({ signal }) => financeService.owners(search, page * pageSize, pageSize, signal),
    enabled: permissions.view,
  });
  const selected = owners.data?.owners.find((owner) => owner.costOwnerId === selectedId);
  const effectiveMinimumReserve = minimumReserve ?? policy.data?.minimumSafetyRangeEur ?? 0;
  const effectiveReservationSlice = reservationSlice ?? policy.data?.reservationSliceMinutes ?? 15;
  const pageCount = Math.max(1, Math.ceil((owners.data?.totalCount ?? 0) / pageSize));
  const hasNext = page + 1 < pageCount;

  const clearFeedback = () => {
    setFeedbackError(undefined);
    setFeedbackSuccess(undefined);
  };
  const mutationFailed = (error: unknown) => setFeedbackError(financeErrorTranslationKey(error));
  const invalidateOwners = () =>
    queryClient.invalidateQueries({ queryKey: financeQueryKeys.ownersRoot });
  const ownerMutationSucceeded = async (successKey: TranslationKey) => {
    await invalidateOwners();
    setFeedbackSuccess(successKey);
  };

  const savePolicy = useMutation({
    mutationFn: () => {
      const parsed = policySchema.safeParse({
        minimumSafetyRangeEur: effectiveMinimumReserve,
        reservationSliceMinutes: effectiveReservationSlice,
      });
      if (!parsed.success) throw new Error("invalid-policy");
      return financeService.updatePolicy(
        parsed.data.minimumSafetyRangeEur,
        parsed.data.reservationSliceMinutes,
      );
    },
    onMutate: clearFeedback,
    onSuccess: (updated) => {
      queryClient.setQueryData(financeQueryKeys.policy, updated);
      setMinimumReserve(undefined);
      setReservationSlice(undefined);
      setFeedbackSuccess("HostingFinance_PolicySaved");
    },
    onError: (error) =>
      setFeedbackError(
        error instanceof Error && error.message === "invalid-policy"
          ? "HostingFinance_PolicyValidation"
          : financeErrorTranslationKey(error),
      ),
  });
  const recordPayment = useMutation({
    mutationFn: async () => {
      if (!selected) throw new Error("missing-owner");
      const parsed = paymentSchema.safeParse({
        amount: paymentAmount,
        externalReference: paymentReference,
        reason: paymentReason,
      });
      if (!parsed.success) throw new Error("invalid-payment");
      return financeService.recordPayment(
        selected.costOwnerId,
        parsed.data.amount,
        parsed.data.externalReference,
        parsed.data.reason || null,
      );
    },
    onMutate: clearFeedback,
    onSuccess: async () => {
      setPaymentAmount(0);
      setPaymentReference("");
      setPaymentReason("");
      await ownerMutationSucceeded("HostingFinance_PaymentSaved");
    },
    onError: (error) =>
      error instanceof Error && error.message.startsWith("invalid-")
        ? setFeedbackError("HostingFinance_PaymentValidation")
        : mutationFailed(error),
  });
  const saveAllowance = useMutation({
    mutationFn: async () => {
      if (!selected) throw new Error("missing-owner");
      const parsed = allowanceSchema.safeParse({
        allowance: revokeAllowance ? null : allowance,
        reason: allowanceReason,
      });
      if (!parsed.success) throw new Error("invalid-allowance");
      return financeService.changeAllowance(
        selected.costOwnerId,
        parsed.data.allowance,
        parsed.data.reason,
      );
    },
    onMutate: clearFeedback,
    onSuccess: async () => {
      setAllowanceReason("");
      await ownerMutationSucceeded("HostingFinance_AllowanceSaved");
    },
    onError: (error) =>
      error instanceof Error && error.message.startsWith("invalid-")
        ? setFeedbackError("HostingFinance_AllowanceValidation")
        : mutationFailed(error),
  });
  const recordRefund = useMutation({
    mutationFn: async () => {
      if (!selected) throw new Error("missing-owner");
      const parsed = refundSchema.parse({
        amount: refundAmount,
        externalReference: refundReference,
        reason: refundReason,
      });
      return financeService.recordRefund(
        selected.costOwnerId,
        parsed.amount,
        parsed.externalReference,
        parsed.reason,
      );
    },
    onMutate: clearFeedback,
    onSuccess: async () => {
      setRefundAmount(0);
      setRefundReference("");
      setRefundReason("");
      await ownerMutationSucceeded("HostingFinance_RefundSaved");
    },
    onError: mutationFailed,
  });
  const correctDeployment = useMutation({
    mutationFn: async () => {
      if (!selected) throw new Error("missing-owner");
      const parsed = correctionSchema.parse({
        sourceCostOwnerId: selected.costOwnerId,
        deploymentId,
        correctCostOwnerId: correctOwnerId,
        amount: correctionAmount,
        reason: correctionReason,
      });
      return financeService.correctDeploymentCostOwner(
        parsed.sourceCostOwnerId,
        parsed.deploymentId,
        parsed.correctCostOwnerId,
        parsed.amount,
        parsed.reason,
      );
    },
    onMutate: clearFeedback,
    onSuccess: async () => {
      setDeploymentId("");
      setCorrectOwnerId("");
      setCorrectionAmount(0);
      setCorrectionReason("");
      await ownerMutationSucceeded("HostingFinance_CorrectionSaved");
    },
    onError: mutationFailed,
  });
  const reverseLedger = useMutation({
    mutationFn: async () => {
      if (!selected || !reversalTarget) throw new Error("missing-target");
      const parsed = reversalSchema.parse({ reason: reversalReason });
      return financeService.reverseLedgerEntry(
        selected.costOwnerId,
        reversalTarget.id,
        parsed.reason,
      );
    },
    onMutate: clearFeedback,
    onSuccess: async () => {
      setReversalReason("");
      setReversalTarget(undefined);
      await ownerMutationSucceeded("HostingFinance_ReversalSaved");
    },
    onError: mutationFailed,
  });

  const isSaving =
    savePolicy.isPending ||
    recordPayment.isPending ||
    saveAllowance.isPending ||
    recordRefund.isPending ||
    correctDeployment.isPending ||
    reverseLedger.isPending;
  const isDirty = useMemo(
    () =>
      minimumReserve !== undefined ||
      reservationSlice !== undefined ||
      paymentAmount > 0 ||
      Boolean(paymentReference || paymentReason) ||
      Boolean(allowanceReason) ||
      (selected !== undefined &&
        (allowance !== (selected.allowance ?? 0) ||
          revokeAllowance !== (selected.allowance === null))) ||
      refundAmount > 0 ||
      Boolean(refundReference || refundReason) ||
      correctionAmount > 0 ||
      Boolean(deploymentId || correctOwnerId || correctionReason) ||
      Boolean(reversalTarget || reversalReason),
    [
      allowance,
      allowanceReason,
      correctOwnerId,
      correctionAmount,
      correctionReason,
      deploymentId,
      minimumReserve,
      paymentAmount,
      paymentReason,
      paymentReference,
      refundAmount,
      refundReason,
      refundReference,
      reservationSlice,
      reversalReason,
      reversalTarget,
      revokeAllowance,
      selected,
    ],
  );
  useUnsavedChanges(isDirty && !isSaving);

  const selectOwner = (owner: FinanceCostOwner) => {
    setSelectedId(owner.costOwnerId);
    setAllowance(owner.allowance ?? 0);
    setRevokeAllowance(owner.allowance === null);
    setAllowanceReason("");
    setPaymentAmount(0);
    setPaymentReference("");
    setPaymentReason("");
    setRefundAmount(0);
    setRefundReference("");
    setRefundReason("");
    setDeploymentId("");
    setCorrectOwnerId("");
    setCorrectionAmount(0);
    setCorrectionReason("");
    setReversalTarget(undefined);
    setReversalReason("");
    clearFeedback();
  };

  const requestRefund = (event: FormEvent) => {
    event.preventDefault();
    clearFeedback();
    if (
      !selected ||
      !refundSchema.safeParse({
        amount: refundAmount,
        externalReference: refundReference,
        reason: refundReason,
      }).success
    ) {
      setFeedbackError("HostingFinance_RefundValidation");
      return;
    }
    setConfirmation({ kind: "refund", owner: selected });
  };
  const requestCorrection = (event: FormEvent) => {
    event.preventDefault();
    clearFeedback();
    if (
      !selected ||
      !correctionSchema.safeParse({
        sourceCostOwnerId: selected.costOwnerId,
        deploymentId,
        correctCostOwnerId: correctOwnerId,
        amount: correctionAmount,
        reason: correctionReason,
      }).success
    ) {
      setFeedbackError("HostingFinance_CorrectionValidation");
      return;
    }
    setConfirmation({ kind: "correction", owner: selected });
  };
  const requestReversal = (event: FormEvent) => {
    event.preventDefault();
    clearFeedback();
    if (
      !selected ||
      !reversalTarget ||
      !reversalSchema.safeParse({ reason: reversalReason }).success
    ) {
      setFeedbackError("HostingFinance_ReversalValidation");
      return;
    }
    setConfirmation({ kind: "reversal", owner: selected, entry: reversalTarget });
  };
  const confirmMutation = () => {
    const action = confirmation;
    setConfirmation(undefined);
    if (action?.kind === "refund") recordRefund.mutate();
    if (action?.kind === "correction") correctDeployment.mutate();
    if (action?.kind === "reversal") reverseLedger.mutate();
  };

  const confirmationCopy = (() => {
    if (confirmation?.kind === "refund")
      return {
        title: t("HostingFinance_RefundConfirmTitle"),
        description: formatResource(
          t("HostingFinance_RefundConfirmDescription"),
          formatMoney(refundAmount, confirmation.owner.currency),
          confirmation.owner.displayName,
        ),
        confirm: t("HostingFinance_RecordRefund"),
      };
    if (confirmation?.kind === "correction")
      return {
        title: t("HostingFinance_CorrectionConfirmTitle"),
        description: formatResource(
          t("HostingFinance_CorrectionConfirmDescription"),
          formatMoney(correctionAmount, confirmation.owner.currency),
          confirmation.owner.displayName,
          correctOwnerId,
        ),
        confirm: t("HostingFinance_RecordCorrection"),
      };
    return {
      title: t("HostingFinance_ReversalConfirmTitle"),
      description: formatResource(
        t("HostingFinance_ReversalConfirmDescription"),
        confirmation?.kind === "reversal" ? confirmation.entry.id : "",
      ),
      confirm: t("HostingFinance_ReverseEntry"),
    };
  })();

  return (
    <div className="next-hosting-page">
      <PageHeader
        kicker={t("HostingFinance_PolicyKicker")}
        title={t("HostingFinance_Title")}
        description={t("HostingFinance_Description")}
        actions={
          permissions.view ? (
            <button
              className="button button--ghost"
              type="button"
              disabled={owners.isFetching || policy.isFetching || isSaving}
              onClick={() => void Promise.all([policy.refetch(), owners.refetch()])}
            >
              {owners.isFetching || policy.isFetching
                ? t("HostingFinance_LoadingOwners")
                : t("HostingFinance_Refresh")}
            </button>
          ) : undefined
        }
      />
      {authLoading ? (
        <HostingLoading />
      ) : !permissions.view ? (
        <StatePanel kind="forbidden" title={t("HostingFinance_NoAccessTitle")}>
          {t("HostingFinance_NoAccessMessage")}
        </StatePanel>
      ) : policy.isLoading || owners.isLoading ? (
        <HostingLoading />
      ) : (policy.isError && !policy.data) || (owners.isError && !owners.data) ? (
        <HostingError
          message={t(financeErrorTranslationKey(policy.error ?? owners.error))}
          onRetry={() => void Promise.all([policy.refetch(), owners.refetch()])}
        />
      ) : (
        <>
          {((policy.isError && policy.data) || (owners.isError && owners.data)) && (
            <StatePanel kind="warning">
              {t(financeErrorTranslationKey(policy.error ?? owners.error))}
            </StatePanel>
          )}
          {feedbackError && (
            <p className="next-hosting-inline-error" role="alert">
              {t(feedbackError)}
            </p>
          )}
          {feedbackSuccess && <StatePanel kind="success">{t(feedbackSuccess)}</StatePanel>}
          <section className="next-hosting-panel" aria-labelledby="finance-policy-title">
            <header className="next-hosting-section-heading">
              <div>
                <span className="kicker">{t("HostingFinance_PolicyKicker")}</span>
                <h2 id="finance-policy-title">{t("HostingFinance_PolicyTitle")}</h2>
                <p>{t("HostingFinance_PolicyDescription")}</p>
              </div>
              <StatusBadge tone="info">{t("HostingFinance_Global")}</StatusBadge>
            </header>
            <form
              className="next-hosting-policy-fields"
              onSubmit={(event) => {
                event.preventDefault();
                savePolicy.mutate();
              }}
            >
              <label className="field">
                <span>{t("HostingFinance_MinimumReserve")}</span>
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  disabled={!permissions.managePolicy || savePolicy.isPending}
                  value={effectiveMinimumReserve}
                  onChange={(event) => setMinimumReserve(Number(event.target.value))}
                />
              </label>
              <label className="field">
                <span>{t("HostingFinance_ReservationSlice")}</span>
                <input
                  type="number"
                  min={1}
                  max={1440}
                  step={1}
                  disabled={!permissions.managePolicy || savePolicy.isPending}
                  value={effectiveReservationSlice}
                  onChange={(event) => setReservationSlice(Number(event.target.value))}
                />
              </label>
              {permissions.managePolicy ? (
                <button
                  className="button button--gold"
                  type="submit"
                  disabled={savePolicy.isPending}
                >
                  {savePolicy.isPending
                    ? t("HostingFinance_Saving")
                    : t("HostingFinance_SavePolicy")}
                </button>
              ) : (
                <small className="next-hosting-muted">{t("HostingFinance_ReadOnly")}</small>
              )}
            </form>
            {policy.data?.timeStampUpdated && (
              <small className="next-hosting-muted">
                {formatResource(
                  t("HostingFinance_PolicyUpdated"),
                  formatDate(policy.data.timeStampUpdated),
                  policy.data.updatedById ?? t("HostingFinance_NotAvailable"),
                )}
              </small>
            )}
          </section>
          <div className="next-hosting-finance-workspace">
            <aside
              className="next-hosting-panel next-hosting-owner-browser"
              aria-busy={owners.isFetching}
            >
              <header className="next-hosting-section-heading">
                <div>
                  <span className="kicker">{t("HostingFinance_OwnersKicker")}</span>
                  <h2>{t("HostingFinance_OwnersTitle")}</h2>
                </div>
                <StatusBadge>{owners.data?.totalCount ?? 0}</StatusBadge>
              </header>
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  setPage(0);
                  setSelectedId(undefined);
                  setSearch(searchDraft.trim());
                }}
              >
                <label className="field">
                  <span>{t("HostingFinance_SearchLabel")}</span>
                  <input
                    type="search"
                    placeholder={t("HostingFinance_SearchPlaceholder")}
                    value={searchDraft}
                    onChange={(event) => setSearchDraft(event.target.value)}
                  />
                </label>
                <button className="button button--ghost" type="submit" disabled={owners.isFetching}>
                  {t("HostingFinance_Search")}
                </button>
              </form>
              {!owners.data?.owners.length ? (
                <StatePanel kind="empty">{t("HostingFinance_NoOwners")}</StatePanel>
              ) : (
                <div
                  className="next-hosting-owner-list"
                  role="listbox"
                  aria-label={t("HostingFinance_OwnerResultsAria")}
                >
                  {owners.data.owners.map((owner) => (
                    <button
                      key={owner.costOwnerId}
                      type="button"
                      role="option"
                      aria-selected={selectedId === owner.costOwnerId}
                      onClick={() => selectOwner(owner)}
                    >
                      <span>
                        <strong>{owner.displayName}</strong>
                        <small>
                          {t(ownerKindTranslationKey(owner.kind))} ·{" "}
                          {formatMoney(owner.balance, owner.currency)}
                        </small>
                      </span>
                      {owner.isFrozen && (
                        <StatusBadge tone="warning">{t("HostingFinance_Frozen")}</StatusBadge>
                      )}
                    </button>
                  ))}
                </div>
              )}
              {pageCount > 1 && (
                <nav
                  className="next-hosting-pagination"
                  aria-label={t("HostingFinance_PaginationAria")}
                >
                  <button
                    className="button button--ghost"
                    type="button"
                    disabled={page === 0 || owners.isFetching}
                    onClick={() => {
                      setPage((current) => current - 1);
                      setSelectedId(undefined);
                    }}
                  >
                    {t("HostingFinance_Previous")}
                  </button>
                  <span>{formatResource(t("HostingFinance_PageLabel"), page + 1, pageCount)}</span>
                  <button
                    className="button button--ghost"
                    type="button"
                    disabled={!hasNext || owners.isFetching}
                    onClick={() => {
                      setPage((current) => current + 1);
                      setSelectedId(undefined);
                    }}
                  >
                    {t("HostingFinance_Next")}
                  </button>
                </nav>
              )}
            </aside>
            <main className="next-hosting-finance-detail">
              {!selected ? (
                <StatePanel kind="empty" title={t("HostingFinance_SelectOwnerTitle")}>
                  {t("HostingFinance_SelectOwnerMessage")}
                </StatePanel>
              ) : (
                <>
                  <OwnerSummary owner={selected} />
                  {(permissions.recordPayment ||
                    permissions.recordRefund ||
                    permissions.manageAllowance ||
                    permissions.correctLedger) && (
                    <div className="next-hosting-finance-actions">
                      {permissions.recordPayment && (
                        <form
                          className="next-hosting-panel next-hosting-form"
                          onSubmit={(event) => {
                            event.preventDefault();
                            recordPayment.mutate();
                          }}
                        >
                          <div>
                            <span className="kicker">{t("HostingFinance_PaymentKicker")}</span>
                            <h3>{t("HostingFinance_PaymentTitle")}</h3>
                            <p>{t("HostingFinance_PaymentDescription")}</p>
                          </div>
                          <label className="field">
                            <span>{t("HostingFinance_Amount")}</span>
                            <input
                              type="number"
                              min={0.01}
                              step="0.01"
                              value={paymentAmount || ""}
                              onChange={(event) => setPaymentAmount(Number(event.target.value))}
                            />
                          </label>
                          <label className="field">
                            <span>{t("HostingFinance_Reference")}</span>
                            <input
                              maxLength={128}
                              value={paymentReference}
                              onChange={(event) => setPaymentReference(event.target.value)}
                            />
                          </label>
                          <label className="field">
                            <span>{t("HostingFinance_Note")}</span>
                            <textarea
                              rows={3}
                              maxLength={512}
                              value={paymentReason}
                              onChange={(event) => setPaymentReason(event.target.value)}
                            />
                          </label>
                          <button
                            className="button button--gold"
                            type="submit"
                            disabled={recordPayment.isPending}
                          >
                            {t("HostingFinance_RecordPayment")}
                          </button>
                        </form>
                      )}
                      {permissions.recordRefund && (
                        <form
                          className="next-hosting-panel next-hosting-form"
                          onSubmit={requestRefund}
                        >
                          <div>
                            <span className="kicker">{t("HostingFinance_RefundKicker")}</span>
                            <h3>{t("HostingFinance_RefundTitle")}</h3>
                            <p>{t("HostingFinance_RefundDescription")}</p>
                          </div>
                          <label className="field">
                            <span>{t("HostingFinance_Amount")}</span>
                            <input
                              type="number"
                              min={0.01}
                              step="0.01"
                              value={refundAmount || ""}
                              onChange={(event) => setRefundAmount(Number(event.target.value))}
                            />
                          </label>
                          <label className="field">
                            <span>{t("HostingFinance_Reference")}</span>
                            <input
                              maxLength={128}
                              value={refundReference}
                              onChange={(event) => setRefundReference(event.target.value)}
                            />
                          </label>
                          <label className="field">
                            <span>{t("HostingFinance_Reason")}</span>
                            <textarea
                              rows={3}
                              maxLength={512}
                              value={refundReason}
                              onChange={(event) => setRefundReason(event.target.value)}
                            />
                          </label>
                          <button className="button button--danger" type="submit">
                            {t("HostingFinance_RecordRefund")}
                          </button>
                        </form>
                      )}
                      {permissions.manageAllowance && (
                        <form
                          className="next-hosting-panel next-hosting-form"
                          onSubmit={(event) => {
                            event.preventDefault();
                            saveAllowance.mutate();
                          }}
                        >
                          <div>
                            <span className="kicker">{t("HostingFinance_AllowanceKicker")}</span>
                            <h3>{t("HostingFinance_AllowanceTitle")}</h3>
                            <p>{t("HostingFinance_AllowanceDescription")}</p>
                          </div>
                          <label className="field">
                            <span>{t("HostingFinance_AllowanceAmount")}</span>
                            <input
                              type="number"
                              min={0}
                              step="0.01"
                              disabled={revokeAllowance}
                              value={allowance}
                              onChange={(event) => setAllowance(Number(event.target.value))}
                            />
                          </label>
                          <label className="next-hosting-check">
                            <input
                              type="checkbox"
                              checked={revokeAllowance}
                              onChange={(event) => setRevokeAllowance(event.target.checked)}
                            />
                            <span>{t("HostingFinance_RevokeAllowance")}</span>
                          </label>
                          <label className="field">
                            <span>{t("HostingFinance_Reason")}</span>
                            <textarea
                              rows={3}
                              maxLength={512}
                              value={allowanceReason}
                              onChange={(event) => setAllowanceReason(event.target.value)}
                            />
                          </label>
                          <button
                            className="button button--gold"
                            type="submit"
                            disabled={saveAllowance.isPending}
                          >
                            {t("HostingFinance_SaveAllowance")}
                          </button>
                        </form>
                      )}
                      {permissions.correctLedger && (
                        <form
                          className="next-hosting-panel next-hosting-form"
                          onSubmit={requestCorrection}
                        >
                          <div>
                            <span className="kicker">{t("HostingFinance_CorrectionKicker")}</span>
                            <h3>{t("HostingFinance_CorrectionTitle")}</h3>
                            <p>{t("HostingFinance_CorrectionDescription")}</p>
                          </div>
                          <label className="field">
                            <span>{t("HostingFinance_DeploymentId")}</span>
                            <input
                              autoComplete="off"
                              value={deploymentId}
                              onChange={(event) => setDeploymentId(event.target.value.trim())}
                            />
                          </label>
                          <label className="field">
                            <span>{t("HostingFinance_CorrectOwnerId")}</span>
                            <input
                              autoComplete="off"
                              value={correctOwnerId}
                              onChange={(event) => setCorrectOwnerId(event.target.value.trim())}
                            />
                          </label>
                          <label className="field">
                            <span>{t("HostingFinance_CorrectionAmount")}</span>
                            <input
                              type="number"
                              min={0.01}
                              step="0.01"
                              value={correctionAmount || ""}
                              onChange={(event) => setCorrectionAmount(Number(event.target.value))}
                            />
                          </label>
                          <label className="field">
                            <span>{t("HostingFinance_Reason")}</span>
                            <textarea
                              rows={3}
                              maxLength={512}
                              value={correctionReason}
                              onChange={(event) => setCorrectionReason(event.target.value)}
                            />
                          </label>
                          <button className="button button--danger" type="submit">
                            {t("HostingFinance_RecordCorrection")}
                          </button>
                        </form>
                      )}
                    </div>
                  )}
                  <section className="next-hosting-panel" aria-labelledby="finance-ledger-title">
                    <header className="next-hosting-section-heading">
                      <div>
                        <span className="kicker">{t("HostingFinance_LedgerKicker")}</span>
                        <h2 id="finance-ledger-title">{t("HostingFinance_LedgerTitle")}</h2>
                      </div>
                    </header>
                    {!selected.recentLedgerEntries.length ? (
                      <StatePanel kind="empty">{t("HostingFinance_NoLedgerEntries")}</StatePanel>
                    ) : (
                      <div className="next-hosting-table-scroll">
                        <table>
                          <thead>
                            <tr>
                              <th>{t("HostingFinance_ColumnEffective")}</th>
                              <th>{t("HostingFinance_ColumnKind")}</th>
                              <th>{t("HostingFinance_ColumnAmount")}</th>
                              <th>{t("HostingFinance_ColumnBalance")}</th>
                              <th>{t("HostingFinance_ColumnContext")}</th>
                              {permissions.correctLedger && (
                                <th>{t("HostingFinance_ColumnActions")}</th>
                              )}
                            </tr>
                          </thead>
                          <tbody>
                            {selected.recentLedgerEntries.map((entry) => {
                              const reversible = canReverseLedgerEntry(
                                entry,
                                selected.recentLedgerEntries,
                              );
                              return (
                                <tr key={entry.id}>
                                  <td>{formatDate(entry.timeStampEffective)}</td>
                                  <td>{t(ledgerKindTranslationKey(entry.kind))}</td>
                                  <td
                                    className={
                                      entry.amount < 0
                                        ? "next-hosting-negative"
                                        : "next-hosting-positive"
                                    }
                                  >
                                    {formatMoney(entry.amount, entry.currency)}
                                  </td>
                                  <td>{formatMoney(entry.balanceAfter, entry.currency)}</td>
                                  <td className="next-hosting-ledger-context">
                                    <span>
                                      {[entry.externalReference, entry.reason]
                                        .filter(Boolean)
                                        .join(" · ") || t("HostingFinance_NotAvailable")}
                                    </span>
                                    <AuditDetails entry={entry} />
                                  </td>
                                  {permissions.correctLedger && (
                                    <td>
                                      <button
                                        className="button button--ghost"
                                        type="button"
                                        disabled={!reversible || reverseLedger.isPending}
                                        title={
                                          reversible ? undefined : t("HostingFinance_NotReversible")
                                        }
                                        onClick={() => {
                                          setReversalTarget(entry);
                                          setReversalReason("");
                                          clearFeedback();
                                        }}
                                      >
                                        {t("HostingFinance_ReverseEntry")}
                                      </button>
                                    </td>
                                  )}
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </section>
                  {permissions.correctLedger && reversalTarget && (
                    <form
                      className="next-hosting-panel next-hosting-form next-hosting-reversal"
                      onSubmit={requestReversal}
                    >
                      <div>
                        <span className="kicker">{t("HostingFinance_ReversalKicker")}</span>
                        <h3>{t("HostingFinance_ReversalTitle")}</h3>
                        <p>
                          {formatResource(
                            t("HostingFinance_ReversalDescription"),
                            reversalTarget.id,
                          )}
                        </p>
                      </div>
                      <label className="field">
                        <span>{t("HostingFinance_Reason")}</span>
                        <textarea
                          rows={3}
                          maxLength={512}
                          value={reversalReason}
                          onChange={(event) => setReversalReason(event.target.value)}
                        />
                      </label>
                      <div className="next-hosting-form-actions">
                        <button
                          className="button button--ghost"
                          type="button"
                          onClick={() => {
                            setReversalTarget(undefined);
                            setReversalReason("");
                          }}
                        >
                          {t("Common_Cancel")}
                        </button>
                        <button className="button button--danger" type="submit">
                          {t("HostingFinance_ReverseEntry")}
                        </button>
                      </div>
                    </form>
                  )}
                </>
              )}
            </main>
          </div>
        </>
      )}
      <ConfirmDialog
        key={confirmation ? `${confirmation.kind}-${confirmation.owner.costOwnerId}` : "closed"}
        open={Boolean(confirmation)}
        title={confirmationCopy.title}
        description={confirmationCopy.description}
        confirmLabel={confirmationCopy.confirm}
        cancelLabel={t("Common_Cancel")}
        {...(confirmation ? { challenge: confirmation.owner.displayName } : {})}
        onConfirm={confirmMutation}
        onCancel={() => setConfirmation(undefined)}
      />
    </div>
  );
}
