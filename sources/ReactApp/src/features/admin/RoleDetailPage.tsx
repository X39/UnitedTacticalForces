import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams } from "react-router";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageAuthority } from "@/auth/claims";
import { useAppToastOnChange } from "@/components/AppToasts";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { loadAuthoritySnapshot } from "@/features/admin/authority";
import {
  deleteRole,
  loadRoleMembers,
  normalizeAuthorityRole,
  roleIsEmpty,
  roleIsHighImpact,
  setRoleClaim,
  setRoleMember,
  updateRole,
  type PlainClaim,
  type RoleMember,
} from "@/features/admin/roles";

type ClaimView = "all" | "assigned" | "available" | "high-impact";
type MemberView = "assigned" | "available";

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
  const members =
    memberView === "assigned"
      ? (membersQuery.data?.assignedMembers ?? [])
      : (membersQuery.data?.availableMembers ?? []);
  const submitMemberSearch = (event: FormEvent) => {
    event.preventDefault();
    setMemberSearch(memberSearchDraft.trim());
  };

  if (!authorized)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Roles_AccessRequiredTitle")}>
        {t("Pages_Command_Roles_AccessRequiredDescription")}
      </StatePanel>
    );
  if (!validRoleId)
    return (
      <StatePanel title={t("Pages_Command_Roles_NotFoundTitle")}>
        {t("Pages_Command_Roles_NotFoundDescription")}
      </StatePanel>
    );

  return (
    <section className="authority-page role-detail-page">
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
            <Link className="button button--ghost" to="/command/roles">
              {t("Pages_Command_Roles_BackToRoles")}
            </Link>
            <button
              className="button button--ghost"
              type="button"
              disabled={authorityQuery.isFetching}
              onClick={() => void invalidate()}
            >
              {t("Pages_Command_Roles_ActionRefresh")}
            </button>
          </>
        }
      />
      {(claimMutation.isError || memberMutation.isError || deleteMutation.isError) && (
        <StatePanel kind="error">
          {formatResource(
            t("Pages_Command_Roles_SaveError"),
            claimMutation.error?.message ??
              memberMutation.error?.message ??
              deleteMutation.error?.message ??
              "",
          )}
        </StatePanel>
      )}
      {(authorityQuery.isPending || (role && membersQuery.isPending)) && (
        <StatePanel kind="loading">{t("Pages_Command_Roles_Loading")}</StatePanel>
      )}
      {(authorityQuery.isError || membersQuery.isError) && (
        <StatePanel kind="error">
          {authorityQuery.error?.message ?? membersQuery.error?.message}
        </StatePanel>
      )}
      {!authorityQuery.isPending && !authorityQuery.isError && !role && (
        <StatePanel title={t("Pages_Command_Roles_NotFoundTitle")}>
          {t("Pages_Command_Roles_NotFoundDescription")}
        </StatePanel>
      )}
      {role && (
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
              <StatusBadge
                tone={roleIsEmpty(role) ? "warning" : roleIsHighImpact(role) ? "danger" : "success"}
              >
                {t(
                  roleIsEmpty(role)
                    ? "Pages_Command_Roles_Status_Empty"
                    : roleIsHighImpact(role)
                      ? "Pages_Command_Roles_Status_HighImpact"
                      : "Pages_Command_Roles_Status_Assigned",
                )}
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
              <div className="toolbar role-detail-toolbar">
                <label className="search-field">
                  <span className="sr-only">{t("Pages_Command_Roles_SearchClaims")}</span>
                  <input
                    placeholder={t("Pages_Command_Roles_SearchClaims")}
                    value={claimSearch}
                    onChange={(event) => setClaimSearch(event.target.value)}
                  />
                </label>
                <div className="filter-chips">
                  {(["all", "assigned", "available", "high-impact"] as ClaimView[]).map(
                    (candidate) => (
                      <button
                        key={candidate}
                        type="button"
                        className={claimView === candidate ? "active" : undefined}
                        aria-pressed={claimView === candidate}
                        onClick={() => setClaimView(candidate)}
                      >
                        {t(
                          candidate === "all"
                            ? "Pages_Command_Roles_FilterAll"
                            : candidate === "assigned"
                              ? "Pages_Command_Roles_FilterAssigned"
                              : candidate === "available"
                                ? "Pages_Command_Roles_FilterAvailable"
                                : "Pages_Command_Roles_FilterHighImpact",
                        )}
                      </button>
                    ),
                  )}
                </div>
                <select
                  aria-label={t("Pages_Command_Roles_CategoryFiltersAria")}
                  value={category ?? ""}
                  onChange={(event) => setCategory(event.target.value || undefined)}
                >
                  <option value="">{t("Pages_Command_Roles_FilterAllCategories")}</option>
                  {categories.map((value) => (
                    <option key={value}>{value}</option>
                  ))}
                </select>
              </div>
              {visibleClaims.length === 0 ? (
                <StatePanel>{t("Pages_Command_Roles_NoClaimsMatch")}</StatePanel>
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
                      {visibleClaims.map(({ claim, assigned }) => (
                        <tr key={claim.primaryKey ?? claim.identifier}>
                          <td>
                            <StatusBadge tone={assigned ? "success" : "info"}>
                              {t(
                                assigned
                                  ? "Pages_Command_Roles_Status_Assigned"
                                  : "Pages_Command_Roles_Status_Available",
                              )}
                            </StatusBadge>
                          </td>
                          <td>
                            <strong>
                              {claimTitle(claim, t("Pages_Command_Roles_UnknownClaim"))}
                            </strong>
                            <span>{claim.description}</span>
                          </td>
                          <td>{claim.category || t("Pages_Command_Roles_Uncategorized")}</td>
                          <td>
                            {claim.identifier || t("Pages_Command_Roles_NoIdentifier")}
                            {claim.value ? ` = ${claim.value}` : ""}
                          </td>
                          <td>
                            <button
                              className={
                                assigned ? "button button--ghost" : "button button--primary"
                              }
                              type="button"
                              disabled={claimMutation.isPending || claim.primaryKey == null}
                              onClick={() =>
                                claim.primaryKey != null &&
                                claimMutation.mutate({
                                  claimId: claim.primaryKey,
                                  active: !assigned,
                                })
                              }
                            >
                              {t(assigned ? "Remove" : "Add")}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>

            <aside
              className="command-panel role-detail-operations"
              aria-label={t("Pages_Command_Roles_RoleOperationsAria")}
            >
              <header className="command-panel__header">
                <span className="kicker">{t("Pages_Command_Roles_OperationsKicker")}</span>
                <h2>{t("Pages_Command_Roles_OperationsTitle")}</h2>
              </header>
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  if (formTitle.trim()) saveMutation.mutate();
                }}
              >
                <label>
                  <span>{t("Pages_Command_Roles_FieldTitle")}</span>
                  <input
                    maxLength={1024}
                    required
                    value={formTitle}
                    onChange={(event) => setTitle(event.target.value)}
                  />
                </label>
                <label>
                  <span>{t("Pages_Command_Roles_FieldDescription")}</span>
                  <textarea
                    rows={5}
                    maxLength={1024}
                    value={formDescription}
                    onChange={(event) => setDescription(event.target.value)}
                  />
                </label>
                {saveMutation.isError && (
                  <StatePanel kind="error">
                    {formatResource(t("Pages_Command_Roles_SaveError"), saveMutation.error.message)}
                  </StatePanel>
                )}
                <div className="form-actions">
                  <button
                    className="button button--primary"
                    type="submit"
                    disabled={!dirty || !formTitle.trim() || saveMutation.isPending}
                  >
                    {t("Pages_Command_Roles_SaveRole")}
                  </button>
                  <button
                    className="button button--danger"
                    type="button"
                    disabled={deleteMutation.isPending}
                    onClick={() => setDeleteOpen(true)}
                  >
                    {t("Pages_Command_Roles_DeleteRole")}
                  </button>
                </div>
              </form>
              <nav className="role-quick-links">
                <Link className="button button--ghost" to="/command/claims">
                  {t("Pages_Command_Roles_ActionClaimsWorkspace")}
                </Link>
                <Link className="button button--ghost" to="/command/members">
                  {t("Pages_Command_Roles_ActionMembersWorkspace")}
                </Link>
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
            <form className="toolbar role-detail-toolbar" onSubmit={submitMemberSearch}>
              <div className="filter-chips">
                <button
                  type="button"
                  className={memberView === "assigned" ? "active" : undefined}
                  aria-pressed={memberView === "assigned"}
                  onClick={() => setMemberView("assigned")}
                >
                  {t("Pages_Command_Roles_AssignedMembers")} (
                  {membersQuery.data?.assignedMemberCount ?? 0})
                </button>
                <button
                  type="button"
                  className={memberView === "available" ? "active" : undefined}
                  aria-pressed={memberView === "available"}
                  onClick={() => setMemberView("available")}
                >
                  {t("Pages_Command_Roles_AvailableMembers")} (
                  {membersQuery.data?.availableMemberCount ?? 0})
                </button>
              </div>
              <label className="search-field">
                <span className="sr-only">{t("Pages_Command_Roles_SearchMembers")}</span>
                <input
                  placeholder={t("Pages_Command_Roles_SearchMembers")}
                  value={memberSearchDraft}
                  onChange={(event) => setMemberSearchDraft(event.target.value)}
                />
              </label>
              <button
                className="button button--ghost"
                type="submit"
                disabled={membersQuery.isFetching}
              >
                {t("Pages_Command_Roles_FilterMembers")}
              </button>
            </form>
            {members.length === 0 ? (
              <StatePanel>
                {t(
                  memberView === "assigned"
                    ? "Pages_Command_Roles_NoAssignedMembers"
                    : "Pages_Command_Roles_NoAvailableMembers",
                )}
              </StatePanel>
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
                      const status = memberStatus(member);
                      return (
                        <tr key={member.primaryKey}>
                          <td>
                            <strong>{member.nickname}</strong>
                            <span>{member.primaryKey}</span>
                          </td>
                          <td>
                            <StatusBadge
                              tone={
                                status === "verified"
                                  ? "success"
                                  : status === "unverified"
                                    ? "warning"
                                    : "danger"
                              }
                            >
                              {t(
                                status === "deleted"
                                  ? "Pages_Command_Members_StatusDeleted"
                                  : status === "banned"
                                    ? "Pages_Command_Members_StatusBanned"
                                    : status === "verified"
                                      ? "Pages_Command_Members_StatusVerified"
                                      : "Pages_Command_Members_StatusUnverified",
                              )}
                            </StatusBadge>
                          </td>
                          <td>
                            <button
                              className={
                                memberView === "available"
                                  ? "button button--primary"
                                  : "button button--ghost"
                              }
                              type="button"
                              disabled={memberMutation.isPending || !member.primaryKey}
                              onClick={() =>
                                member.primaryKey &&
                                memberMutation.mutate({
                                  userId: member.primaryKey,
                                  active: memberView === "available",
                                })
                              }
                            >
                              {t(memberView === "available" ? "Add" : "Remove")}
                            </button>
                            {member.primaryKey && (
                              <Link
                                className="button button--ghost"
                                to={`/command/members/${member.primaryKey}`}
                              >
                                {t("Pages_Command_Roles_OpenDetails")}
                              </Link>
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
      <ConfirmDialog
        open={deleteOpen}
        title={t("Pages_Command_Roles_DeleteRole")}
        description={t("Pages_Command_Roles_DeleteConfirm")}
        confirmLabel={t("Pages_Command_Roles_DeleteRole")}
        cancelLabel={t("Common_Cancel")}
        onCancel={() => setDeleteOpen(false)}
        onConfirm={() => {
          if (!deleteMutation.isPending) deleteMutation.mutate();
        }}
      />
    </section>
  );
}
