import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useParams } from "react-router";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManageAuthority } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppToastOnChange } from "@/components/AppToasts";
import { CommandListToolbar } from "@/components/CommandListToolbar";
import { FilterChipGroup, type FilterChipItem } from "@/components/FilterChipGroup";
import { SelectField, TextAreaField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { accountTones, toneOf } from "@/components/statusTones";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { loadAuthoritySnapshot } from "@/features/admin/authority";
import { roleClaimTones, roleTones } from "@/features/admin/authorityTones";
import {
  deleteRole,
  loadRoleMembers,
  normalizeAuthorityRole,
  roleIsEmpty,
  roleIsHighImpact,
  setRoleClaim,
  setRoleMember,
  updateRole,
  type AuthorityRole,
  type PlainClaim,
  type RoleMember,
} from "@/features/admin/roles";
import "@/features/admin/authority.css";

type ClaimView = "all" | "assigned" | "available" | "high-impact";
type MemberView = "assigned" | "available";

const claimViews: readonly ClaimView[] = ["all", "assigned", "available", "high-impact"];
const claimViewKeys: Record<ClaimView, TranslationKey> = {
  all: "Pages_Command_Roles_FilterAll",
  assigned: "Pages_Command_Roles_FilterAssigned",
  available: "Pages_Command_Roles_FilterAvailable",
  "high-impact": "Pages_Command_Roles_FilterHighImpact",
};
const roleStatusKeys = {
  empty: "Pages_Command_Roles_Status_Empty",
  "high-impact": "Pages_Command_Roles_Status_HighImpact",
  assigned: "Pages_Command_Roles_Status_Assigned",
} as const satisfies Record<string, TranslationKey>;
const memberStatusKeys: Record<ReturnType<typeof memberStatus>, TranslationKey> = {
  deleted: "Pages_Command_Members_StatusDeleted",
  banned: "Pages_Command_Members_StatusBanned",
  verified: "Pages_Command_Members_StatusVerified",
  unverified: "Pages_Command_Members_StatusUnverified",
};

function claimTitle(claim: PlainClaim, fallback: string): string {
  return claim.title?.trim() || claim.identifier?.trim() || fallback;
}

function highImpactClaim(claim: PlainClaim): boolean {
  const copy = `${claim.identifier ?? ""} ${claim.title ?? ""}`.toLowerCase();
  return copy.includes("adm") || copy.includes("admin") || copy.includes("delete");
}

function memberStatus(member: RoleMember): "deleted" | "banned" | "verified" | "unverified" {
  if (member.isDeleted) return "deleted";
  if (member.isBanned) return "banned";
  return member.isVerified ? "verified" : "unverified";
}

function roleStatus(role: AuthorityRole): keyof typeof roleStatusKeys {
  if (roleIsEmpty(role)) return "empty";
  return roleIsHighImpact(role) ? "high-impact" : "assigned";
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { roleId: roleIdParameter } = useParams();
  const roleId = Number(roleIdParameter);
  const validRoleId = Number.isSafeInteger(roleId) && roleId > 0;
  const authorized = canManageAuthority(user);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [claimView, setClaimView] = useState<ClaimView>("all");
  const [claimSearch, setClaimSearch] = useState("");
  const [category, setCategory] = useState<string>();
  const [memberView, setMemberView] = useState<MemberView>("assigned");
  const [memberSearchDraft, setMemberSearchDraft] = useState("");
  const [memberSearch, setMemberSearch] = useState("");
  const [title, setTitle] = useState<string>();
  const [description, setDescription] = useState<string>();
  const [deleteOpen, setDeleteOpen] = useState(false);

  const authorityQuery = useQuery({
    queryKey: ["command-center", "authority-snapshot", "role-detail"],
    queryFn: ({ signal }) => loadAuthoritySnapshot("", signal, 500),
    enabled: authorized && validRoleId,
  });
  const role = useMemo(
    () =>
      normalizeAuthorityRole(
        authorityQuery.data?.roles.find((entry) => entry.role?.primaryKey === roleId),
      ),
    [authorityQuery.data?.roles, roleId],
  );
  const membersQuery = useQuery({
    queryKey: ["command-center", "authority-role-members", roleId, memberSearch],
    queryFn: ({ signal }) => loadRoleMembers(roleId, memberSearch, signal),
    enabled: authorized && validRoleId && Boolean(role),
  });
  const formTitle = title ?? role?.role.title ?? "";
  const formDescription = description ?? role?.role.description ?? "";
  const dirty = Boolean(
    role &&
    (formTitle !== (role.role.title ?? "") || formDescription !== (role.role.description ?? "")),
  );
  useUnsavedChanges(dirty);

  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["command-center", "authority-snapshot"] }),
      queryClient.invalidateQueries({
        queryKey: ["command-center", "authority-role-members", roleId],
      }),
    ]);
  };
  const saveMutation = useMutation({
    mutationFn: () => updateRole(roleId, formTitle.trim(), formDescription.trim()),
    onSuccess: async () => {
      setTitle(undefined);
      setDescription(undefined);
      await invalidate();
    },
  });
  const deleteMutation = useMutation({
    mutationFn: () => deleteRole(roleId),
    onSuccess: () => navigate("/command/roles"),
  });
  const claimMutation = useMutation({
    mutationFn: ({ claimId, active }: { claimId: number; active: boolean }) =>
      setRoleClaim(roleId, claimId, active),
    onSuccess: invalidate,
  });
  const memberMutation = useMutation({
    mutationFn: ({ userId, active }: { userId: string; active: boolean }) =>
      setRoleMember(roleId, userId, active),
    onSuccess: invalidate,
  });
  useAppToastOnChange(saveMutation.isSuccess ? t("Pages_Command_Roles_SaveSuccess") : undefined);
  useAppToastOnChange(claimMutation.isSuccess ? t("Pages_Command_Roles_ClaimsSaved") : undefined);
  useAppToastOnChange(
    memberMutation.isSuccess ? t("Pages_Command_Member_AuthoritySaved") : undefined,
  );

  const claimRows = useMemo(() => {
    const assignedClaimIds = new Set(role?.claims.flatMap((claim) => claim.primaryKey ?? []) ?? []);
    const rows = (authorityQuery.data?.claims ?? [])
      .filter((entry) => entry.claim && !entry.claim.isPrefix)
      .map((entry) => ({
        claim: entry.claim!,
        assigned: entry.claim!.primaryKey != null && assignedClaimIds.has(entry.claim!.primaryKey),
      }));
    for (const claim of role?.claims ?? []) {
      if (
        claim.primaryKey != null &&
        !rows.some((entry) => entry.claim.primaryKey === claim.primaryKey)
      )
        rows.push({ claim, assigned: true });
    }
    return rows;
  }, [authorityQuery.data?.claims, role?.claims]);
  const categories = [
    ...new Set(
      claimRows.map((row) => row.claim.category?.trim() || t("Pages_Command_Roles_Uncategorized")),
    ),
  ].sort();
  const visibleClaims = claimRows.filter((row) => {
    const search = claimSearch.trim().toLowerCase();
    const claimCategory = row.claim.category?.trim() || t("Pages_Command_Roles_Uncategorized");
    return (
      (!category || claimCategory === category) &&
      (claimView === "all" ||
        (claimView === "assigned" && row.assigned) ||
        (claimView === "available" && !row.assigned) ||
        (claimView === "high-impact" && highImpactClaim(row.claim))) &&
      (!search ||
        `${row.claim.title ?? ""} ${row.claim.identifier ?? ""} ${row.claim.value ?? ""} ${row.claim.description ?? ""}`
          .toLowerCase()
          .includes(search))
    );
  });
  const claimChips: FilterChipItem<ClaimView>[] = claimViews.map((id) => ({
    id,
    label: t(claimViewKeys[id]),
    count: claimRows.filter(
      (row) =>
        id === "all" ||
        (id === "assigned" && row.assigned) ||
        (id === "available" && !row.assigned) ||
        (id === "high-impact" && highImpactClaim(row.claim)),
    ).length,
  }));
  const memberChips: FilterChipItem<MemberView>[] = [
    {
      id: "assigned",
      label: t("Pages_Command_Roles_AssignedMembers"),
      count: membersQuery.data?.assignedMemberCount ?? 0,
    },
    {
      id: "available",
      label: t("Pages_Command_Roles_AvailableMembers"),
      count: membersQuery.data?.availableMemberCount ?? 0,
    },
  ];
  const members =
    memberView === "assigned"
      ? (membersQuery.data?.assignedMembers ?? [])
      : (membersQuery.data?.availableMembers ?? []);
  const submitMemberSearch = (event: FormEvent) => {
    event.preventDefault();
    setMemberSearch(memberSearchDraft.trim());
  };
  const backToRoles = (
    <ActionLink variant="ghost" to="/command/roles">
      {t("Pages_Command_Roles_BackToRoles")}
    </ActionLink>
  );

  if (!authorized)
    return (
      <section className="role-detail-page">
        <PageHeader
          kicker={t("Shared_CommandCenter_CurrentArea_Roles")}
          title={t("Pages_Command_Roles_DetailPageTitle")}
        />
        <PageStack>
          <ForbiddenState title={t("Pages_Command_Roles_AccessRequiredTitle")}>
            {t("Pages_Command_Roles_AccessRequiredDescription")}
          </ForbiddenState>
        </PageStack>
      </section>
    );
  if (!validRoleId)
    return (
      <section className="role-detail-page">
        <PageHeader
          kicker={t("Shared_CommandCenter_CurrentArea_Roles")}
          title={t("Pages_Command_Roles_DetailPageTitle")}
        />
        <PageStack>
          <EmptyState title={t("Pages_Command_Roles_NotFoundTitle")} action={backToRoles}>
            {t("Pages_Command_Roles_NotFoundDescription")}
          </EmptyState>
        </PageStack>
      </section>
    );

  const status = role ? roleStatus(role) : undefined;
  const notFound = !authorityQuery.isPending && !authorityQuery.isError && !role;

  return (
    <section className="role-detail-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_CurrentArea_Roles")}
        title={
          role
            ? formatResource(
                t("Pages_Command_Roles_DetailPageTitleNamed"),
                role.role.title ?? roleId,
              )
            : t("Pages_Command_Roles_DetailPageTitle")
        }
        description={t("Pages_Command_Roles_DetailPageDescription")}
        actions={
          <>
            {!notFound && backToRoles}
            <ActionButton
              variant="ghost"
              disabled={authorityQuery.isFetching}
              onClick={() => void invalidate()}
            >
              {t("Pages_Command_Roles_ActionRefresh")}
            </ActionButton>
          </>
        }
      />
      <PageStack>
        {(claimMutation.isError || memberMutation.isError) && (
          <StatePanel kind="error">
            {formatResource(
              t("Pages_Command_Roles_SaveError"),
              claimMutation.error?.message ?? memberMutation.error?.message ?? "",
            )}
          </StatePanel>
        )}
        {(authorityQuery.isPending || (role && membersQuery.isPending)) && (
          <LoadingState label={t("Pages_Command_Roles_Loading")} />
        )}
        {(authorityQuery.isError || membersQuery.isError) && (
          <ErrorState
            onRetry={() => {
              if (authorityQuery.isError) void authorityQuery.refetch();
              if (membersQuery.isError) void membersQuery.refetch();
            }}
          >
            {authorityQuery.error?.message ?? membersQuery.error?.message}
          </ErrorState>
        )}
        {notFound && (
          <EmptyState title={t("Pages_Command_Roles_NotFoundTitle")} action={backToRoles}>
            {t("Pages_Command_Roles_NotFoundDescription")}
          </EmptyState>
        )}
        {role && status && (
          <>
            <section
              className="command-panel role-detail-hero"
              aria-label={t("Pages_Command_Roles_SelectedRoleDetailAria")}
            >
              <div>
                <span className="kicker">{t("Pages_Command_Roles_SelectedRoleKicker")}</span>
                <h2>{role.role.title}</h2>
                <p>
                  {role.role.description?.trim() ||
                    t("Pages_Command_Roles_NoRoleDescriptionReturned")}
                </p>
                <small>
                  {t("Pages_Command_Roles_DetailIdentifier")}: #{roleId}
                </small>
              </div>
              <div className="role-detail-facts">
                <StatusBadge tone={toneOf(roleTones, status)}>
                  {t(roleStatusKeys[status])}
                </StatusBadge>
                <strong>
                  {role.memberCount} {t("Pages_Command_Roles_Count_Plural_Member")}
                </strong>
                <strong>
                  {role.claims.length} {t("Pages_Command_Roles_Count_Plural_Claim")}
                </strong>
              </div>
            </section>

            <div className="role-detail-workspace">
              <section className="command-panel">
                <header className="command-panel__header">
                  <span className="kicker">{t("Pages_Command_Roles_ClaimsKicker")}</span>
                  <h2>{t("Pages_Command_Roles_ClaimsMatrixTitle")}</h2>
                  <StatusBadge>
                    {visibleClaims.length} {t("Pages_Command_Roles_Count_Plural_Row")}
                  </StatusBadge>
                </header>
                <div className="toolbar authority-panel-toolbar">
                  <SearchField
                    label={t("Pages_Command_Roles_SearchClaims")}
                    placeholder={t("Pages_Command_Roles_SearchClaims")}
                    clearLabel={t("Common_SearchClear")}
                    value={claimSearch}
                    onChange={setClaimSearch}
                  />
                  <SelectField
                    label={t("Pages_Command_Roles_CategoryFiltersAria")}
                    value={category ?? ""}
                    onChange={(event) => setCategory(event.target.value || undefined)}
                    options={[
                      { value: "", label: t("Pages_Command_Roles_FilterAllCategories") },
                      ...categories.map((value) => ({ value, label: value })),
                    ]}
                  />
                  <FilterChipGroup<ClaimView>
                    label={t("Pages_Command_Roles_ClaimsMatrixTitle")}
                    items={claimChips}
                    value={claimView}
                    onChange={setClaimView}
                  />
                </div>
                {visibleClaims.length === 0 ? (
                  <EmptyState title={t("Pages_Command_Roles_ClaimsEmptyTitle")}>
                    {t("Pages_Command_Roles_ClaimsEmptyBody")}
                  </EmptyState>
                ) : (
                  <div className="command-table-scroll">
                    <table className="command-table">
                      <thead>
                        <tr>
                          <th>{t("Pages_Command_Roles_ClaimState")}</th>
                          <th>{t("Pages_Command_Roles_Claim")}</th>
                          <th>{t("Pages_Command_Roles_Category")}</th>
                          <th>{t("Pages_Command_Roles_IdentifierValue")}</th>
                          <th>{t("Pages_Command_Roles_Action")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {visibleClaims.map(({ claim, assigned }) => {
                          const name = claimTitle(claim, t("Pages_Command_Roles_UnknownClaim"));
                          const actionLabel = t(assigned ? "Remove" : "Add");
                          return (
                            <tr key={claim.primaryKey ?? claim.identifier}>
                              <td>
                                <StatusBadge
                                  tone={toneOf(roleClaimTones, assigned ? "assigned" : "available")}
                                >
                                  {t(
                                    assigned
                                      ? "Pages_Command_Roles_Status_Assigned"
                                      : "Pages_Command_Roles_Status_Available",
                                  )}
                                </StatusBadge>
                              </td>
                              <td>
                                <strong>{name}</strong>
                                <span>{claim.description}</span>
                              </td>
                              <td>{claim.category || t("Pages_Command_Roles_Uncategorized")}</td>
                              <td>
                                {claim.identifier || t("Pages_Command_Roles_NoIdentifier")}
                                {claim.value ? ` = ${claim.value}` : ""}
                              </td>
                              <td>
                                <ActionButton
                                  variant={assigned ? "ghost" : "primary"}
                                  aria-label={`${actionLabel} ${name}`}
                                  disabled={claimMutation.isPending || claim.primaryKey == null}
                                  onClick={() =>
                                    claim.primaryKey != null &&
                                    claimMutation.mutate({
                                      claimId: claim.primaryKey,
                                      active: !assigned,
                                    })
                                  }
                                >
                                  {actionLabel}
                                </ActionButton>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>

              <aside
                className="command-panel authority-operations"
                aria-label={t("Pages_Command_Roles_RoleOperationsAria")}
              >
                <header className="command-panel__header">
                  <span className="kicker">{t("Pages_Command_Roles_OperationsKicker")}</span>
                  <h2>{t("Pages_Command_Roles_OperationsTitle")}</h2>
                </header>
                <form
                  className="authority-form-panel"
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (formTitle.trim()) saveMutation.mutate();
                  }}
                >
                  <div className="authority-form-fields">
                    <TextField
                      label={t("Pages_Command_Roles_FieldTitle")}
                      maxLength={1024}
                      required
                      value={formTitle}
                      onChange={(event) => setTitle(event.target.value)}
                    />
                    <TextAreaField
                      label={t("Pages_Command_Roles_FieldDescription")}
                      rows={5}
                      maxLength={1024}
                      value={formDescription}
                      onChange={(event) => setDescription(event.target.value)}
                    />
                  </div>
                  {saveMutation.isError && (
                    <div className="authority-form-fields">
                      <StatePanel kind="error">
                        {formatResource(
                          t("Pages_Command_Roles_SaveError"),
                          saveMutation.error.message,
                        )}
                      </StatePanel>
                    </div>
                  )}
                  <div className="authority-form-actions">
                    <ActionButton
                      variant="gold"
                      type="submit"
                      disabled={!dirty || !formTitle.trim() || saveMutation.isPending}
                    >
                      {t("Pages_Command_Roles_SaveRole")}
                    </ActionButton>
                    <ActionButton
                      variant="danger"
                      disabled={deleteMutation.isPending}
                      onClick={() => setDeleteOpen(true)}
                    >
                      {t("Pages_Command_Roles_DeleteRole")}
                    </ActionButton>
                  </div>
                </form>
                <nav className="role-quick-links">
                  <ActionLink variant="ghost" to="/command/claims">
                    {t("Pages_Command_Roles_ActionClaimsWorkspace")}
                  </ActionLink>
                  <ActionLink variant="ghost" to="/command/members">
                    {t("Pages_Command_Roles_ActionMembersWorkspace")}
                  </ActionLink>
                </nav>
              </aside>
            </div>

            <section
              className="command-panel role-members-panel"
              aria-label={t("Pages_Command_Roles_RoleMembersPanelAria")}
            >
              <header className="command-panel__header">
                <span className="kicker">{t("Pages_Command_Roles_MembershipKicker")}</span>
                <h2>{t("Pages_Command_Roles_MembersMatrixTitle")}</h2>
                <StatusBadge>
                  {members.length} {t("Pages_Command_Roles_Count_Plural_Row")}
                </StatusBadge>
              </header>
              <CommandListToolbar
                className="authority-panel-toolbar"
                ariaLabel={t("Pages_Command_Roles_SearchMembers")}
                onSubmit={submitMemberSearch}
                searchLabel={t("Pages_Command_Roles_SearchMembers")}
                searchValue={memberSearchDraft}
                onSearchChange={setMemberSearchDraft}
                submitLabel={t("Pages_Command_Roles_FilterMembers")}
                submitDisabled={membersQuery.isFetching}
              >
                <FilterChipGroup<MemberView>
                  label={t("Pages_Command_Roles_MembersMatrixTitle")}
                  items={memberChips}
                  value={memberView}
                  onChange={setMemberView}
                />
              </CommandListToolbar>
              {members.length === 0 ? (
                memberView === "assigned" ? (
                  <EmptyState title={t("Pages_Command_Roles_NoAssignedMembersTitle")}>
                    {t("Pages_Command_Roles_NoAssignedMembersBody")}
                  </EmptyState>
                ) : (
                  <EmptyState title={t("Pages_Command_Roles_NoAvailableMembersTitle")}>
                    {t("Pages_Command_Roles_NoAvailableMembersBody")}
                  </EmptyState>
                )
              ) : (
                <div className="command-table-scroll">
                  <table className="command-table">
                    <thead>
                      <tr>
                        <th>{t("Pages_Command_Roles_Member")}</th>
                        <th>{t("Pages_Command_Roles_HeaderStatus")}</th>
                        <th>{t("Pages_Command_Roles_Action")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {members.map((member) => {
                        const memberState = memberStatus(member);
                        const actionLabel = t(memberView === "available" ? "Add" : "Remove");
                        return (
                          <tr key={member.primaryKey}>
                            <td>
                              <strong>{member.nickname}</strong>
                              <span>{member.primaryKey}</span>
                            </td>
                            <td>
                              <StatusBadge tone={toneOf(accountTones, memberState)}>
                                {t(memberStatusKeys[memberState])}
                              </StatusBadge>
                            </td>
                            <td>
                              <ActionButton
                                variant={memberView === "available" ? "primary" : "ghost"}
                                aria-label={`${actionLabel} ${member.nickname ?? ""}`.trim()}
                                disabled={memberMutation.isPending || !member.primaryKey}
                                onClick={() =>
                                  member.primaryKey &&
                                  memberMutation.mutate({
                                    userId: member.primaryKey,
                                    active: memberView === "available",
                                  })
                                }
                              >
                                {actionLabel}
                              </ActionButton>
                              {member.primaryKey && (
                                <ActionLink
                                  variant="ghost"
                                  to={`/command/members/${member.primaryKey}`}
                                  aria-label={`${t("Pages_Command_Roles_OpenDetails")} ${member.nickname ?? ""}`.trim()}
                                >
                                  {t("Pages_Command_Roles_OpenDetails")}
                                </ActionLink>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          </>
        )}
      </PageStack>
      <ConfirmDialog
        open={deleteOpen}
        title={t("Pages_Command_Roles_DeleteRole")}
        description={t("Pages_Command_Roles_DeleteConfirm")}
        confirmLabel={t("Pages_Command_Roles_DeleteRole")}
        cancelLabel={t("Pages_Command_Roles_DeleteRole_Dismiss")}
        initialFocus="dismiss"
        busy={deleteMutation.isPending}
        error={
          deleteMutation.isError
            ? formatResource(t("Pages_Command_Roles_SaveError"), deleteMutation.error.message)
            : undefined
        }
        onCancel={() => {
          deleteMutation.reset();
          setDeleteOpen(false);
        }}
        onConfirm={() => {
          if (!deleteMutation.isPending) deleteMutation.mutate();
        }}
      />
    </section>
  );
}
