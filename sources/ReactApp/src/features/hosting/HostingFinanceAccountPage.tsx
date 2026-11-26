import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router";
import { useAuth } from "@/auth/auth";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { SelectField, TextAreaField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { financeTones, toneOf } from "@/components/statusTones";
import { CostAlertSettingsPanel } from "@/features/hosting/CostAlertSettingsPanel";
import { FinanceDialog } from "@/features/hosting/FinanceDialog";
import { FinanceAccountPicker } from "@/features/hosting/FinanceAccountPicker";
import { FinanceProtocol } from "@/features/hosting/FinanceProtocol";
import { getFinancePermissions } from "@/features/hosting/financeAuthorization";
import { ownerKindTranslationKey } from "@/features/hosting/financeModel";
import { financeQueryKeys, financeService } from "@/features/hosting/financeService";
import type { FinanceProtocolEntry } from "@/features/hosting/financeTypes";
import { formatMoney } from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import "@/features/hosting/cost-alert-settings.css";
import "@/features/hosting/finance.css";

type AccountAction =
  "payment" | "refund" | "allowance" | "correction" | "rename" | "freeze" | "reversal";

/** The outcome-specific dismiss label of every account action (D-11); none of them is a generic cancel. */
const dismissKeys: Record<AccountAction, TranslationKey> = {
  payment: "HostingFinance_Payment_Dismiss",
  refund: "HostingFinance_Refund_Dismiss",
  allowance: "HostingFinance_Allowance_Dismiss",
  correction: "HostingFinance_Correction_Dismiss",
  rename: "HostingFinance_Rename_Dismiss",
  freeze: "HostingFinance_Freeze_Dismiss",
  reversal: "HostingFinance_Reversal_Dismiss",
};

function localNow(): string {
  const date = new Date();
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}

/**
 * The Cost alerts section of the finance account page: the alert settings of one cost owner, loaded and edited
 * through the same panel the member page uses.
 *
 * The section has its own loading and error states, so a failing alert request never replaces the account. The API
 * decides independently whether the viewer may see and save the settings; a viewer who may see but not save gets
 * the panel read-only.
 *
 * @param props.costOwnerId The cost owner whose alert settings are shown.
 * @returns The h2 section with a loading state, an error state or the settings panel.
 */
function FinanceCostAlerts({ costOwnerId }: { costOwnerId: string }) {
  const { t } = useTranslation();
  const queryKey = hostingQueryKeys.costAlertSettings(costOwnerId);
  const settings = useQuery({
    queryKey,
    queryFn: ({ signal }) => hostingService.costAlertSettings(costOwnerId, signal),
  });
  return (
    <section className="finance-cost-alerts" aria-labelledby="finance-cost-alerts-title">
      <h2 id="finance-cost-alerts-title">{t("Server_Alerts_Page_Title")}</h2>
      {settings.data === undefined && settings.isError && (
        <ErrorState onRetry={() => void settings.refetch()}>
          {t("Server_Alerts_Page_LoadError")}
        </ErrorState>
      )}
      {settings.data === undefined && !settings.isError && (
        <LoadingState label={t("Server_Alerts_Page_Loading")} />
      )}
      {settings.data !== undefined && (
        <CostAlertSettingsPanel
          key={settings.data.costOwnerId}
          settings={settings.data}
          queryKey={queryKey}
          headingLevel={3}
        />
      )}
    </section>
  );
}

export function HostingFinanceAccountPage() {
  const { t } = useTranslation();
  const { costOwnerId = "" } = useParams();
  const auth = useAuth();
  const permissions = getFinancePermissions(auth.user);
  const queryClient = useQueryClient();
  const [action, setAction] = useState<AccountAction>();
  const [reversalEntry, setReversalEntry] = useState<FinanceProtocolEntry>();
  const [destinationId, setDestinationId] = useState("");
  const owner = useQuery({
    queryKey: ["hosting", "finance", "owner", costOwnerId],
    queryFn: ({ signal }) => financeService.owner(costOwnerId, signal),
    enabled: permissions.view && Boolean(costOwnerId),
  });
  const deployments = useQuery({
    queryKey: financeQueryKeys.correctable(costOwnerId),
    queryFn: ({ signal }) => financeService.correctableDeployments(costOwnerId, signal),
    enabled: action === "correction",
  });
  const mutation = useMutation({
    mutationFn: async (form: FormData) => {
      const value = (name: string) => String(form.get(name) ?? "");
      const effective = new Date(value("effectiveAt")).toISOString();
      switch (action) {
        case "payment":
          return financeService.recordPayment(
            costOwnerId,
            Number(value("amount")),
            value("reference"),
            value("reason") || null,
            effective,
          );
        case "refund":
          return financeService.recordRefund(
            costOwnerId,
            Number(value("amount")),
            value("reference"),
            value("reason"),
            effective,
          );
        case "allowance":
          return financeService.changeAllowance(
            costOwnerId,
            value("allowance") ? Number(value("allowance")) : null,
            value("reason"),
          );
        case "correction":
          return financeService.correctDeploymentCostOwner(
            costOwnerId,
            value("deploymentId"),
            value("destinationId"),
            Number(value("amount")),
            value("reason"),
            effective,
          );
        case "rename":
          return financeService.renameOwner(costOwnerId, value("displayName"));
        case "freeze":
          return financeService.setFrozen(costOwnerId, !owner.data?.isFrozen);
        case "reversal":
          return financeService.reverseLedgerEntry(
            costOwnerId,
            reversalEntry?.id ?? "",
            value("reason"),
            effective,
          );
        default:
          throw new Error("No finance action selected.");
      }
    },
    onSuccess: async () => {
      setAction(undefined);
      setReversalEntry(undefined);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["hosting", "finance", "owner", costOwnerId] }),
        queryClient.invalidateQueries({
          queryKey: ["hosting", "finance", "protocol", costOwnerId],
        }),
        queryClient.invalidateQueries({ queryKey: financeQueryKeys.ownersRoot }),
      ]);
    },
  });
  const openReverse = (entry: FinanceProtocolEntry) => {
    setReversalEntry(entry);
    setAction("reversal");
  };
  const close = () => {
    setAction(undefined);
    setReversalEntry(undefined);
    mutation.reset();
  };
  const actionTitleKeys: Record<AccountAction, TranslationKey> = {
    payment: "HostingFinance_PaymentTitle",
    refund: "HostingFinance_RefundTitle",
    allowance: "HostingFinance_AllowanceTitle",
    correction: "HostingFinance_CorrectionTitle",
    rename: "HostingFinance_RenameAccount",
    freeze: owner.data?.isFrozen
      ? "HostingFinance_UnfreezeAccount"
      : "HostingFinance_FreezeAccount",
    reversal: "HostingFinance_ReversalTitle",
  };
  const title = action ? t(actionTitleKeys[action]) : "";

  /** Renders the shared header above a state, so every early return has exactly one h1. */
  const framed = (body: ReactNode) => (
    <>
      <PageHeader title={t("HostingFinance_Title")} />
      <PageStack>{body}</PageStack>
    </>
  );
  if (auth.isLoading || owner.isLoading) return framed(<LoadingState />);
  if (!permissions.view)
    return framed(
      <ForbiddenState title={t("HostingFinance_NoAccessTitle")}>
        {t("HostingFinance_NoAccessMessage")}
      </ForbiddenState>,
    );
  if (owner.isError || !owner.data)
    return framed(<ErrorState onRetry={() => void owner.refetch()} />);
  const account = owner.data;
  const description =
    action === "freeze"
      ? t(
          account.isFrozen
            ? "HostingFinance_UnfreezeDescription"
            : "HostingFinance_FreezeDescription",
        )
      : account.displayName;
  return (
    <>
      <div className="finance-back">
        <ActionLink variant="ghost" size="compact" to="/command/hosting-finance">
          ← {t("HostingFinance_BackToAccounts")}
        </ActionLink>
      </div>
      <PageHeader
        kicker={t(ownerKindTranslationKey(account.kind))}
        title={account.displayName}
        description={account.costOwnerId}
        actions={
          <div className="finance-actions">
            {permissions.recordPayment && (
              <ActionButton variant="primary" onClick={() => setAction("payment")}>
                {t("HostingFinance_RecordPayment")}
              </ActionButton>
            )}
            {permissions.recordRefund && (
              <ActionButton variant="primary" onClick={() => setAction("refund")}>
                {t("HostingFinance_RecordRefund")}
              </ActionButton>
            )}
            {permissions.manageAllowance && (
              <ActionButton variant="primary" onClick={() => setAction("allowance")}>
                {t("HostingFinance_SaveAllowance")}
              </ActionButton>
            )}
            {permissions.correctLedger && (
              <ActionButton variant="primary" onClick={() => setAction("correction")}>
                {t("HostingFinance_RecordCorrection")}
              </ActionButton>
            )}
            {permissions.manageCostOwners && account.kind === "ExternalOrganization" && (
              <ActionButton variant="ghost" onClick={() => setAction("rename")}>
                {t("HostingFinance_RenameAccount")}
              </ActionButton>
            )}
            {permissions.freezeCostOwner && (
              <ActionButton
                variant={account.isFrozen ? "ghost" : "danger"}
                onClick={() => setAction("freeze")}
              >
                {t(
                  account.isFrozen
                    ? "HostingFinance_UnfreezeAccount"
                    : "HostingFinance_FreezeAccount",
                )}
              </ActionButton>
            )}
          </div>
        }
      />
      <PageStack>
        {account.isFrozen && (
          <StatePanel kind="warning">{t("HostingFinance_FrozenNotice")}</StatePanel>
        )}
        <section className="finance-position">
          <div className="finance-account-state">
            <StatusBadge tone={toneOf(financeTones, account.isFrozen ? "frozen" : "active")}>
              {t(account.isFrozen ? "HostingFinance_Frozen" : "HostingFinance_Active")}
            </StatusBadge>
          </div>
          <div className="finance-metrics">
            <Metric
              label={t("HostingFinance_Balance")}
              value={formatMoney(account.balance, account.currency)}
            />
            <Metric
              label={t("HostingFinance_Allowance")}
              value={
                account.allowance === null
                  ? t("HostingFinance_None")
                  : formatMoney(account.allowance, account.currency)
              }
            />
            <Metric
              label={t("HostingFinance_Reserved")}
              value={formatMoney(account.reservedProviderCost, account.currency)}
            />
            <Metric
              label={t("HostingFinance_Spendable")}
              value={formatMoney(account.spendableAmount, account.currency)}
            />
          </div>
        </section>
        {permissions.view && <FinanceCostAlerts costOwnerId={costOwnerId} />}
        <FinanceProtocol
          costOwnerId={costOwnerId}
          showAccount={false}
          canReverse={permissions.correctLedger}
          onReverse={openReverse}
        />
      </PageStack>
      <FinanceDialog
        open={Boolean(action)}
        title={title}
        description={description}
        submitLabel={title}
        dismissLabel={t(dismissKeys[action ?? "payment"])}
        confirmVariant={
          action === "reversal" || (action === "freeze" && !account.isFrozen) ? "danger" : "gold"
        }
        busy={mutation.isPending}
        error={mutation.isError ? t("HostingFinance_ErrorUnexpected") : undefined}
        onClose={close}
        onSubmit={(form) => mutation.mutate(form)}
      >
        {(action === "payment" || action === "refund") && (
          <>
            <TextField
              name="amount"
              type="number"
              min="0.01"
              step="0.01"
              required
              label={t("HostingFinance_Amount")}
            />
            <TextField
              name="reference"
              maxLength={128}
              required
              label={t("HostingFinance_Reference")}
            />
          </>
        )}
        {action === "allowance" && (
          <TextField
            name="allowance"
            type="number"
            min="0"
            step="0.01"
            defaultValue={account.allowance ?? ""}
            label={t("HostingFinance_AllowanceAmount")}
          />
        )}
        {action === "correction" && (
          <>
            <SelectField
              name="deploymentId"
              required
              label={t("HostingFinance_DeploymentId")}
              options={[
                { value: "", label: t("HostingFinance_SelectDeployment") },
                ...(deployments.data ?? []).map((item) => ({
                  value: item.deploymentId,
                  label: `${item.serverTitle} · ${formatMoney(
                    item.remainingCorrectableAmount,
                    item.currency,
                  )}`,
                })),
              ]}
            />
            <FinanceAccountPicker
              value={destinationId}
              name="destinationId"
              label={t("HostingFinance_DestinationAccount")}
              excludeId={costOwnerId}
              required
              onChange={setDestinationId}
            />
            <TextField
              name="amount"
              type="number"
              min="0.01"
              step="0.01"
              required
              label={t("HostingFinance_CorrectionAmount")}
            />
          </>
        )}
        {action === "rename" && (
          <TextField
            name="displayName"
            defaultValue={account.displayName}
            minLength={1}
            maxLength={256}
            required
            label={t("HostingFinance_OrganizationName")}
          />
        )}
        {(action === "payment" ||
          action === "refund" ||
          action === "correction" ||
          action === "reversal") && (
          <TextField
            id="finance-effective-at"
            name="effectiveAt"
            type="datetime-local"
            defaultValue={localNow()}
            required
            label={t("HostingFinance_EffectiveAt")}
          />
        )}
        {action !== "freeze" && action !== "rename" && (
          <TextAreaField
            id="finance-reason"
            name="reason"
            maxLength={512}
            required={action !== "payment"}
            label={t("HostingFinance_Reason")}
          />
        )}
      </FinanceDialog>
    </>
  );
}
