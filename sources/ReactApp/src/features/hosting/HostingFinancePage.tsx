import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { FinanceDialog } from "@/features/hosting/FinanceDialog";
import { getFinancePermissions } from "@/features/hosting/financeAuthorization";
import { ownerKindTranslationKey } from "@/features/hosting/financeModel";
import { financeQueryKeys, financeService } from "@/features/hosting/financeService";
import type { FinanceOwnerKind } from "@/features/hosting/financeTypes";
import { formatDate, formatMoney } from "@/features/hosting/model";
import "@/features/hosting/finance.css";

const pageSize = 20;

export function HostingFinancePage() {
  const { t } = useTranslation();
  const auth = useAuth();
  const permissions = getFinancePermissions(auth.user);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [params, setParams] = useSearchParams();
  const search = params.get("search") ?? "";
  const kind = (params.get("kind") || undefined) as FinanceOwnerKind | undefined;
  const status = params.get("status") ?? "";
  const page = Math.max(0, Number(params.get("page") ?? "1") - 1);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [createKind, setCreateKind] = useState<FinanceOwnerKind>("User");
  const [candidateSearch, setCandidateSearch] = useState("");
  const [userId, setUserId] = useState("");
  const [displayName, setDisplayName] = useState("");
  useDocumentTitle(t("HostingFinance_Title"));

  const frozen = status ? status === "frozen" : undefined;
  const owners = useQuery({
    queryKey: financeQueryKeys.owners(search, page, pageSize, kind, frozen),
    queryFn: ({ signal }) =>
      financeService.owners(search, page * pageSize, pageSize, kind, frozen, signal),
    enabled: permissions.view,
  });
  const candidates = useQuery({
    queryKey: financeQueryKeys.candidates(candidateSearch),
    queryFn: ({ signal }) => financeService.userCandidates(candidateSearch, signal),
    enabled: dialogOpen && createKind === "User",
  });
  const verein = useQuery({
    queryKey: financeQueryKeys.owners("", 0, 1, "Verein"),
    queryFn: ({ signal }) => financeService.owners("", 0, 1, "Verein", undefined, signal),
    enabled: dialogOpen && createKind === "Verein",
  });
  const create = useMutation({
    mutationFn: () =>
      financeService.createOwner(
        createKind,
        createKind === "User" ? userId : undefined,
        createKind === "ExternalOrganization" ? displayName : undefined,
      ),
    onSuccess: async (owner) => {
      await queryClient.invalidateQueries({ queryKey: financeQueryKeys.ownersRoot });
      setDialogOpen(false);
      navigate(`/command/hosting-finance/accounts/${owner.costOwnerId}`);
    },
  });
  const setFilter = (name: string, value: string, resetPage = true) => {
    const next = new URLSearchParams(params);
    if (value) next.set(name, value);
    else next.delete(name);
    if (resetPage) next.delete("page");
    setParams(next);
  };
  const totalPages = Math.max(1, Math.ceil((owners.data?.totalCount ?? 0) / pageSize));
  const canCreate =
    createKind === "User"
      ? Boolean(userId)
      : createKind === "Verein"
        ? verein.data?.totalCount === 0
        : displayName.trim().length > 0 && displayName.trim().length <= 256;

  if (auth.isLoading) return <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>;
  if (!permissions.view)
    return (
      <StatePanel kind="forbidden" title={t("HostingFinance_NoAccessTitle")}>
        {t("HostingFinance_NoAccessMessage")}
      </StatePanel>
    );
  return (
    <div className="finance-page">
      <PageHeader
        kicker={t("HostingFinance_OwnersKicker")}
        title={t("HostingFinance_Title")}
        description={t("HostingFinance_OverviewDescription")}
        actions={
          <>
            <ActionLink variant="ghost" to="/command/hosting-finance/settings">
              {t("HostingFinance_GlobalSettings")}
            </ActionLink>
            {permissions.manageCostOwners && (
              <ActionButton variant="primary" onClick={() => setDialogOpen(true)}>
                {t("HostingFinance_AddAccount")}
              </ActionButton>
            )}
          </>
        }
      />
      <div className="finance-filters">
        <label className="field finance-filter--wide">
          <span>{t("HostingFinance_SearchLabel")}</span>
          <input
            value={search}
            placeholder={t("HostingFinance_SearchPlaceholder")}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        </label>
        <label className="field">
          <span>{t("HostingFinance_AccountType")}</span>
          <select value={kind ?? ""} onChange={(event) => setFilter("kind", event.target.value)}>
            <option value="">{t("HostingFinance_FilterAll")}</option>
            <option value="User">{t("HostingFinance_OwnerMember")}</option>
            <option value="Verein">{t("HostingFinance_OwnerVerein")}</option>
            <option value="ExternalOrganization">{t("HostingFinance_OwnerOrganization")}</option>
          </select>
        </label>
        <label className="field">
          <span>{t("HostingFinance_AccountStatus")}</span>
          <select value={status} onChange={(event) => setFilter("status", event.target.value)}>
            <option value="">{t("HostingFinance_FilterAll")}</option>
            <option value="active">{t("HostingFinance_Active")}</option>
            <option value="frozen">{t("HostingFinance_Frozen")}</option>
          </select>
        </label>
      </div>
      {owners.isLoading && (
        <StatePanel kind="loading">{t("HostingFinance_LoadingOwners")}</StatePanel>
      )}
      {owners.isError && <StatePanel kind="error">{t("HostingFinance_LoadError")}</StatePanel>}
      {owners.data?.owners.length === 0 && (
        <StatePanel kind="empty">{t("HostingFinance_NoOwners")}</StatePanel>
      )}
      {owners.data && owners.data.owners.length > 0 && (
        <>
          <div className="finance-table-scroll">
            <table className="finance-table finance-accounts-table">
              <thead>
                <tr>
                  <th>{t("HostingFinance_ProtocolAccount")}</th>
                  <th>{t("HostingFinance_AccountType")}</th>
                  <th>{t("HostingFinance_AccountStatus")}</th>
                  <th className="number">{t("HostingFinance_Balance")}</th>
                  <th className="number">{t("HostingFinance_Allowance")}</th>
                  <th className="number">{t("HostingFinance_Reserved")}</th>
                  <th className="number">{t("HostingFinance_Spendable")}</th>
                  <th>{t("HostingFinance_LastUpdate")}</th>
                </tr>
              </thead>
              <tbody>
                {owners.data.owners.map((owner) => (
                  <tr
                    key={owner.costOwnerId}
                    tabIndex={0}
                    onClick={() =>
                      navigate(`/command/hosting-finance/accounts/${owner.costOwnerId}`)
                    }
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ")
                        navigate(`/command/hosting-finance/accounts/${owner.costOwnerId}`);
                    }}
                  >
                    <td>
                      <strong>{owner.displayName}</strong>
                    </td>
                    <td>{t(ownerKindTranslationKey(owner.kind))}</td>
                    <td>
                      <StatusBadge tone={owner.isFrozen ? "warning" : "success"}>
                        {t(owner.isFrozen ? "HostingFinance_Frozen" : "HostingFinance_Active")}
                      </StatusBadge>
                    </td>
                    <td className="number">{formatMoney(owner.balance, owner.currency)}</td>
                    <td className="number">
                      {owner.allowance === null
                        ? t("HostingFinance_None")
                        : formatMoney(owner.allowance, owner.currency)}
                    </td>
                    <td className="number">
                      {formatMoney(owner.reservedProviderCost, owner.currency)}
                    </td>
                    <td className="number">{formatMoney(owner.spendableAmount, owner.currency)}</td>
                    <td>{formatDate(owner.timeStampUpdated)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <nav className="finance-pagination" aria-label={t("HostingFinance_PaginationAria")}>
            <ActionButton
              variant="ghost"
              size="compact"
              disabled={page === 0}
              onClick={() => setFilter("page", String(page), false)}
            >
              {t("HostingFinance_Previous")}
            </ActionButton>
            <span>{formatResource(t("HostingFinance_PageLabel"), page + 1, totalPages)}</span>
            <ActionButton
              variant="ghost"
              size="compact"
              disabled={page + 1 >= totalPages}
              onClick={() => setFilter("page", String(page + 2), false)}
            >
              {t("HostingFinance_Next")}
            </ActionButton>
          </nav>
        </>
      )}
      <FinanceDialog
        open={dialogOpen}
        title={t("HostingFinance_AddAccount")}
        submitLabel={t("HostingFinance_CreateAccount")}
        busy={create.isPending}
        onClose={() => setDialogOpen(false)}
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          if (canCreate) create.mutate();
        }}
      >
        <label className="field">
          <span>{t("HostingFinance_AccountType")}</span>
          <select
            value={createKind}
            onChange={(event) => {
              setCreateKind(event.target.value as FinanceOwnerKind);
              setUserId("");
            }}
          >
            <option value="User">{t("HostingFinance_OwnerMember")}</option>
            <option value="Verein">{t("HostingFinance_OwnerVerein")}</option>
            <option value="ExternalOrganization">{t("HostingFinance_OwnerOrganization")}</option>
          </select>
        </label>
        {createKind === "User" && (
          <>
            <label className="field">
              <span>{t("HostingFinance_SearchLabel")}</span>
              <input
                value={candidateSearch}
                onChange={(event) => setCandidateSearch(event.target.value)}
              />
            </label>
            <label className="field">
              <span>{t("HostingFinance_EligibleUser")}</span>
              <select value={userId} onChange={(event) => setUserId(event.target.value)}>
                <option value="">{t("HostingFinance_SelectUser")}</option>
                {candidates.data?.users.map((user) => (
                  <option key={user.userId} value={user.userId}>
                    {user.displayName}
                  </option>
                ))}
              </select>
            </label>
          </>
        )}
        {createKind === "Verein" && verein.data?.totalCount !== 0 && (
          <StatePanel kind="info">{t("HostingFinance_VereinUnavailable")}</StatePanel>
        )}
        {createKind === "ExternalOrganization" && (
          <label className="field">
            <span>{t("HostingFinance_OrganizationName")}</span>
            <input
              maxLength={256}
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
            />
          </label>
        )}
        {create.isError && (
          <StatePanel kind="error">{t("HostingFinance_ErrorUnexpected")}</StatePanel>
        )}
      </FinanceDialog>
    </div>
  );
}
