import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router";
import { useAuth } from "@/auth/auth";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { FinanceDialog } from "@/features/hosting/FinanceDialog";
import { FinanceAccountPicker } from "@/features/hosting/FinanceAccountPicker";
import { FinanceProtocol } from "@/features/hosting/FinanceProtocol";
import { getFinancePermissions } from "@/features/hosting/financeAuthorization";
import { ownerKindTranslationKey } from "@/features/hosting/financeModel";
import { financeQueryKeys, financeService } from "@/features/hosting/financeService";
import type { FinanceProtocolEntry } from "@/features/hosting/financeTypes";
import { formatMoney } from "@/features/hosting/model";

type AccountAction =
  "payment" | "refund" | "allowance" | "correction" | "rename" | "freeze" | "reversal";

function localNow(): string {
  const date = new Date();
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
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

  if (auth.isLoading || owner.isLoading)
    return <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>;
  if (!permissions.view)
    return <StatePanel kind="forbidden">{t("HostingFinance_NoAccessMessage")}</StatePanel>;
  if (owner.isError || !owner.data)
    return <StatePanel kind="error">{t("HostingFinance_LoadError")}</StatePanel>;
  const account = owner.data;
  return (
    <div className="finance-page">
      <ActionLink variant="ghost" size="compact" to="/command/hosting-finance">
        ← {t("HostingFinance_BackToAccounts")}
      </ActionLink>
      <PageHeader
        kicker={t(ownerKindTranslationKey(account.kind))}
        title={account.displayName}
        description={account.costOwnerId}
        actions={
          <div className="finance-actions">
            {permissions.recordPayment && (
              <ActionButton onClick={() => setAction("payment")}>
                {t("HostingFinance_RecordPayment")}
              </ActionButton>
            )}
            {permissions.recordRefund && (
              <ActionButton onClick={() => setAction("refund")}>
                {t("HostingFinance_RecordRefund")}
              </ActionButton>
            )}
            {permissions.manageAllowance && (
              <ActionButton onClick={() => setAction("allowance")}>
                {t("HostingFinance_SaveAllowance")}
              </ActionButton>
            )}
            {permissions.correctLedger && (
              <ActionButton onClick={() => setAction("correction")}>
                {t("HostingFinance_RecordCorrection")}
              </ActionButton>
            )}
            {permissions.manageCostOwners && account.kind === "ExternalOrganization" && (
              <ActionButton onClick={() => setAction("rename")}>
                {t("HostingFinance_RenameAccount")}
              </ActionButton>
            )}
            {permissions.freezeCostOwner && (
              <ActionButton
                variant={account.isFrozen ? "default" : "danger"}
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
      {account.isFrozen && (
        <StatePanel kind="warning">{t("HostingFinance_FrozenNotice")}</StatePanel>
      )}
      <section className="finance-position">
        <div className="finance-account-state">
          <StatusBadge tone={account.isFrozen ? "warning" : "success"}>
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
      <FinanceProtocol
        costOwnerId={costOwnerId}
        showAccount={false}
        canReverse={permissions.correctLedger}
        onReverse={openReverse}
      />
      <FinanceDialog
        open={Boolean(action)}
        title={title}
        submitLabel={title}
        busy={mutation.isPending}
        onClose={close}
        onSubmit={(event: FormEvent<HTMLFormElement>) => {
          event.preventDefault();
          mutation.mutate(new FormData(event.currentTarget));
        }}
      >
        {(action === "payment" || action === "refund") && (
          <>
            <label className="field">
              <span>{t("HostingFinance_Amount")}</span>
              <input name="amount" type="number" min="0.01" step="0.01" required />
            </label>
            <label className="field">
              <span>{t("HostingFinance_Reference")}</span>
              <input name="reference" maxLength={128} required />
            </label>
          </>
        )}
        {action === "allowance" && (
          <label className="field">
            <span>{t("HostingFinance_AllowanceAmount")}</span>
            <input
              name="allowance"
              type="number"
              min="0"
              step="0.01"
              defaultValue={account.allowance ?? ""}
            />
          </label>
        )}
        {action === "correction" && (
          <>
            <label className="field">
              <span>{t("HostingFinance_DeploymentId")}</span>
              <select name="deploymentId" required>
                <option value="">{t("HostingFinance_SelectDeployment")}</option>
                {deployments.data?.map((item) => (
                  <option key={item.deploymentId} value={item.deploymentId}>
                    {item.serverTitle} ·{" "}
                    {formatMoney(item.remainingCorrectableAmount, item.currency)}
                  </option>
                ))}
              </select>
            </label>
            <FinanceAccountPicker
              value={destinationId}
              name="destinationId"
              label={t("HostingFinance_DestinationAccount")}
              excludeId={costOwnerId}
              required
              onChange={setDestinationId}
            />
            <label className="field">
              <span>{t("HostingFinance_CorrectionAmount")}</span>
              <input name="amount" type="number" min="0.01" step="0.01" required />
            </label>
          </>
        )}
        {action === "rename" && (
          <label className="field">
            <span>{t("HostingFinance_OrganizationName")}</span>
            <input
              name="displayName"
              defaultValue={account.displayName}
              minLength={1}
              maxLength={256}
              required
            />
          </label>
        )}
        {(action === "payment" ||
          action === "refund" ||
          action === "correction" ||
          action === "reversal") && (
          <label className="field">
            <span>{t("HostingFinance_EffectiveAt")}</span>
            <input name="effectiveAt" type="datetime-local" defaultValue={localNow()} required />
          </label>
        )}
        {action !== "freeze" && action !== "rename" && (
          <label className="field">
            <span>{t("HostingFinance_Reason")}</span>
            <textarea name="reason" maxLength={512} required={action !== "payment"} />
          </label>
        )}
        {action === "freeze" && (
          <p>
            {t(
              account.isFrozen
                ? "HostingFinance_UnfreezeDescription"
                : "HostingFinance_FreezeDescription",
            )}
          </p>
        )}
        {mutation.isError && (
          <StatePanel kind="error">{t("HostingFinance_ErrorUnexpected")}</StatePanel>
        )}
      </FinanceDialog>
    </div>
  );
}
