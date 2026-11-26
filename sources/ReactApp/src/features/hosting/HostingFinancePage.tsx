import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { FilterChipGroup } from "@/components/FilterChipGroup";
import { SelectField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { financeTones, toneOf } from "@/components/statusTones";
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
  const openCreateDialog = () => {
    create.reset();
    setDialogOpen(true);
  };
  const closeCreateDialog = () => {
    create.reset();
    setDialogOpen(false);
  };
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

  if (auth.isLoading)
    return (
      <>
        <PageHeader title={t("HostingFinance_Title")} />
        <PageStack>
          <LoadingState />
        </PageStack>
      </>
    );
  if (!permissions.view)
    return (
      <>
        <PageHeader title={t("HostingFinance_Title")} />
        <PageStack>
          <ForbiddenState title={t("HostingFinance_NoAccessTitle")}>
            {t("HostingFinance_NoAccessMessage")}
          </ForbiddenState>
        </PageStack>
      </>
    );
  const kindOptions = [
    { value: "", label: t("HostingFinance_FilterAll") },
    { value: "User", label: t("HostingFinance_OwnerMember") },
    { value: "Verein", label: t("HostingFinance_OwnerVerein") },
    { value: "ExternalOrganization", label: t("HostingFinance_OwnerOrganization") },
  ];
  const statusItems = [
    { id: "", label: t("HostingFinance_FilterAll") },
    { id: "active", label: t("HostingFinance_Active") },
    { id: "frozen", label: t("HostingFinance_Frozen") },
  ];
  return (
    <>
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
              <ActionButton variant="gold" onClick={openCreateDialog}>
                {t("HostingFinance_AddAccount")}
              </ActionButton>
            )}
          </>
        }
      />
      <PageStack>
        <div className="finance-filters">
          <div className="finance-filter--wide">
            <SearchField
              label={t("HostingFinance_SearchLabel")}
              placeholder={t("HostingFinance_SearchPlaceholder")}
              clearLabel={t("Common_SearchClear")}
              value={search}
              onChange={(value) => setFilter("search", value)}
            />
          </div>
          <SelectField
            label={t("HostingFinance_AccountType")}
            value={kind ?? ""}
            options={kindOptions}
            onChange={(event) => setFilter("kind", event.target.value)}
          />
          <FilterChipGroup
            label={t("HostingFinance_AccountStatus")}
            items={statusItems}
            value={status}
            onChange={(value) => setFilter("status", value)}
          />
        </div>
        {owners.isLoading && <LoadingState label={t("HostingFinance_LoadingOwners")} />}
        {owners.isError && <ErrorState onRetry={() => void owners.refetch()} />}
        {owners.data?.owners.length === 0 && (
          <EmptyState title={t("HostingFinance_NoOwnersTitle")}>
            {t("HostingFinance_NoOwnersBody")}
          </EmptyState>
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
                        <StatusBadge
                          tone={toneOf(financeTones, owner.isFrozen ? "frozen" : "active")}
                        >
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
                      <td className="number">
                        {formatMoney(owner.spendableAmount, owner.currency)}
                      </td>
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
      </PageStack>
      <FinanceDialog
        open={dialogOpen}
        title={t("HostingFinance_AddAccount")}
        description={t("HostingFinance_OverviewDescription")}
        submitLabel={t("HostingFinance_CreateAccount")}
        dismissLabel={t("HostingFinance_AddAccount_Dismiss")}
        busy={create.isPending}
        error={create.isError ? t("HostingFinance_ErrorUnexpected") : undefined}
        onClose={closeCreateDialog}
        onSubmit={() => {
          if (canCreate) create.mutate();
        }}
      >
        <SelectField
          label={t("HostingFinance_AccountType")}
          value={createKind}
          options={kindOptions.slice(1)}
          onChange={(event) => {
            create.reset();
            setCreateKind(event.target.value as FinanceOwnerKind);
            setUserId("");
          }}
        />
        {createKind === "User" && (
          <>
            <TextField
              label={t("HostingFinance_SearchLabel")}
              value={candidateSearch}
              onChange={(event) => setCandidateSearch(event.target.value)}
            />
            <SelectField
              label={t("HostingFinance_EligibleUser")}
              value={userId}
              options={[
                { value: "", label: t("HostingFinance_SelectUser") },
                ...(candidates.data?.users ?? []).map((user) => ({
                  value: user.userId,
                  label: user.displayName,
                })),
              ]}
              onChange={(event) => setUserId(event.target.value)}
            />
          </>
        )}
        {createKind === "Verein" && verein.data?.totalCount !== 0 && (
          <StatePanel kind="info">{t("HostingFinance_VereinUnavailable")}</StatePanel>
        )}
        {createKind === "ExternalOrganization" && (
          <TextField
            label={t("HostingFinance_OrganizationName")}
            maxLength={256}
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
          />
        )}
      </FinanceDialog>
    </>
  );
}
