import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canCreateRoles, canManageAuthority } from "@/auth/claims";
import { CommandListToolbar } from "@/components/CommandListToolbar";
import {
  Metric,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { loadAuthoritySnapshot } from "@/features/admin/authority";
import {
  createRole,
  normalizeAuthorityRole,
  roleIsEmpty,
  roleIsHighImpact,
  type AuthorityRole,
} from "@/features/admin/roles";

type RoleView = "all" | "assigned" | "empty" | "high-impact";

function roleTitle(role: AuthorityRole, fallback: string): string {
  return role.role.title?.trim() || fallback;
}

function matchesView(role: AuthorityRole, view: RoleView): boolean {
  if (view === "assigned") return role.memberCount > 0 && role.claims.length > 0;
  if (view === "empty") return roleIsEmpty(role);
  if (view === "high-impact") return roleIsHighImpact(role);
  return true;
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const authorized = canManageAuthority(user);
  const canCreate = canCreateRoles(user);
  const [view, setView] = useState<RoleView>("all");
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [creating, setCreating] = useState(false);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [created, setCreated] = useState(false);
  useUnsavedChanges(creating && Boolean(title.trim() || description.trim()));

  const query = useQuery({
    queryKey: ["command-center", "authority-snapshot", search],
    queryFn: ({ signal }) => loadAuthoritySnapshot(search, signal),
    enabled: authorized,
  });
  const roles = useMemo(
    () =>
      (query.data?.roles ?? [])
        .map(normalizeAuthorityRole)
        .filter((role): role is AuthorityRole => Boolean(role)),
    [query.data?.roles],
  );
  const visibleRoles = useMemo(
    () =>
      roles
        .filter((role) => matchesView(role, view))
        .sort(
          (left, right) =>
            right.memberCount - left.memberCount ||
            right.claims.length - left.claims.length ||
            roleTitle(left, "").localeCompare(roleTitle(right, "")),
        ),
    [roles, view],
  );
  const mutation = useMutation({
    mutationFn: () => createRole(title.trim(), description.trim()),
    onSuccess: async () => {
      setCreated(true);
      setCreating(false);
      setTitle("");
      setDescription("");
      await queryClient.invalidateQueries({ queryKey: ["command-center", "authority-snapshot"] });
    },
  });
  const submitSearch = (event: FormEvent) => {
    event.preventDefault();
    setSearch(searchDraft.trim());
  };
  const submitCreate = (event: FormEvent) => {
    event.preventDefault();
    if (title.trim()) mutation.mutate();
  };
  const formatCount = (value: number, singular: string, plural: string) =>
    value === 1 ? `1 ${singular}` : `${value.toLocaleString()} ${plural}`;

  if (!authorized)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Roles_AccessRequiredTitle")}>
        {t("Pages_Command_Roles_AccessRequiredDescription")}
      </StatePanel>
    );

  const assignedCount = roles.reduce((sum, role) => sum + role.memberCount, 0);
  const claimCount = roles.reduce((sum, role) => sum + role.claims.length, 0);
  const emptyCount = roles.filter(roleIsEmpty).length;
  const highImpactCount = roles.filter(roleIsHighImpact).length;

  return (
    <section className="authority-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_CurrentArea_Roles")}
        title={t("Pages_Command_Roles_HeaderTitle")}
        description={t("Pages_Command_Roles_HeaderDescription")}
        actions={
          <>
            {canCreate && (
              <button
                className="button button--primary"
                type="button"
                disabled={mutation.isPending}
                onClick={() => {
                  setCreated(false);
                  setCreating((value) => !value);
                }}
              >
                {t("Pages_Command_Roles_CreateRole")}
              </button>
            )}
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Roles_ActionRefresh")}
            </button>
          </>
        }
      />

      {created && <StatePanel kind="success">{t("Pages_Command_Roles_CreateSuccess")}</StatePanel>}

      <section
        className="command-metrics authority-metrics"
        aria-label={t("Pages_Command_Roles_MetricsAriaLabel")}
      >
        <button
          type="button"
          className={view === "all" ? "active" : undefined}
          aria-pressed={view === "all"}
          onClick={() => setView("all")}
        >
          <Metric
            label={t("Pages_Command_Roles_MetricAllStatus")}
            value={formatCount(
              query.data?.roleCount ?? 0,
              t("Pages_Command_Roles_Count_Singular_Role"),
              t("Pages_Command_Roles_Count_Plural_Role"),
            )}
            detail={formatResource(t("Pages_Command_Roles_Metric_AllDescription"), roles.length)}
          />
        </button>
        <button
          type="button"
          className={view === "assigned" ? "active" : undefined}
          aria-pressed={view === "assigned"}
          onClick={() => setView("assigned")}
        >
          <Metric
            label={t("Pages_Command_Roles_MetricAssignedStatus")}
            value={formatCount(
              assignedCount,
              t("Pages_Command_Roles_Count_Singular_MemberAssignment"),
              t("Pages_Command_Roles_Count_Plural_MemberAssignment"),
            )}
            detail={formatResource(t("Pages_Command_Roles_Metric_AssignedDescription"), claimCount)}
          />
        </button>
        <button
          type="button"
          className={view === "empty" ? "active" : undefined}
          aria-pressed={view === "empty"}
          onClick={() => setView("empty")}
        >
          <Metric
            label={t("Pages_Command_Roles_MetricEmptyStatus")}
            value={String(emptyCount)}
            detail={t("Pages_Command_Roles_Metric_EmptyDescription")}
          />
        </button>
        <button
          type="button"
          className={view === "high-impact" ? "active" : undefined}
          aria-pressed={view === "high-impact"}
          onClick={() => setView("high-impact")}
        >
          <Metric
            label={t("Pages_Command_Roles_MetricHighImpactStatus")}
            value={String(highImpactCount)}
            detail={t("Pages_Command_Roles_Metric_HighImpactDescription")}
          />
        </button>
      </section>

      <CommandListToolbar
        className="authority-toolbar"
        ariaLabel={t("Pages_Command_Roles_ToolbarAriaLabel")}
        onSubmit={submitSearch}
        searchLabel={t("Pages_Command_Roles_SearchPlaceholder")}
        searchValue={searchDraft}
        onSearchChange={setSearchDraft}
        submitLabel={t("Pages_Command_Roles_ActionSearch")}
        submitDisabled={query.isFetching}
      >
        <div className="filter-chips">
          {(["all", "assigned", "empty", "high-impact"] as RoleView[]).map((candidate) => (
            <button
              key={candidate}
              type="button"
              className={view === candidate ? "active" : undefined}
              aria-pressed={view === candidate}
              onClick={() => setView(candidate)}
            >
              {t(
                candidate === "all"
                  ? "Pages_Command_Roles_FilterAll"
                  : candidate === "assigned"
                    ? "Pages_Command_Roles_FilterAssigned"
                    : candidate === "empty"
                      ? "Pages_Command_Roles_FilterEmpty"
                      : "Pages_Command_Roles_FilterHighImpact",
              )}
            </button>
          ))}
        </div>
      </CommandListToolbar>

      {creating && (
        <form className="command-panel role-create-panel" onSubmit={submitCreate}>
          <header className="command-panel__header">
            <span className="kicker">{t("Pages_Command_Roles_CreateRoleKicker")}</span>
            <h2>{t("Pages_Command_Roles_CreateRole")}</h2>
          </header>
          <label>
            <span>{t("Pages_Command_Roles_FieldTitle")}</span>
            <input
              maxLength={1024}
              required
              value={title}
              disabled={mutation.isPending}
              onChange={(event) => setTitle(event.target.value)}
            />
          </label>
          <label>
            <span>{t("Pages_Command_Roles_FieldDescription")}</span>
            <textarea
              rows={3}
              maxLength={1024}
              value={description}
              disabled={mutation.isPending}
              onChange={(event) => setDescription(event.target.value)}
            />
          </label>
          {mutation.isError && (
            <StatePanel kind="error">
              {formatResource(t("Pages_Command_Roles_SaveError"), mutation.error.message)}
            </StatePanel>
          )}
          <div className="form-actions">
            <button
              className="button button--primary"
              type="submit"
              disabled={!title.trim() || mutation.isPending}
            >
              {t("Pages_Command_Roles_CreateRole")}
            </button>
            <button
              className="button button--ghost"
              type="button"
              disabled={mutation.isPending}
              onClick={() => setCreating(false)}
            >
              {t("Common_Cancel")}
            </button>
          </div>
        </form>
      )}

      {query.isError && <StatePanel kind="error">{query.error.message}</StatePanel>}
      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_Roles_Loading")}</StatePanel>
      ) : (
        <section className="command-panel authority-list-panel">
          <header className="command-panel__header authority-panel__header">
            <span className="kicker">{t("Pages_Command_Roles_RoleLedgerKicker")}</span>
            <h2>
              {t(
                view === "all"
                  ? "Pages_Command_Roles_ListTitle_All"
                  : view === "assigned"
                    ? "Pages_Command_Roles_ListTitle_Assigned"
                    : view === "empty"
                      ? "Pages_Command_Roles_ListTitle_Empty"
                      : "Pages_Command_Roles_ListTitle_HighImpact",
              )}
            </h2>
            <StatusBadge>
              {formatCount(
                visibleRoles.length,
                t("Pages_Command_Roles_Count_Singular_Row"),
                t("Pages_Command_Roles_Count_Plural_Row"),
              )}
            </StatusBadge>
          </header>
          {visibleRoles.length === 0 ? (
            <StatePanel>{t("Pages_Command_Roles_NoRolesMatch")}</StatePanel>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table authority-table">
                <thead>
                  <tr>
                    <th>{t("Pages_Command_Roles_HeaderRole")}</th>
                    <th>{t("Pages_Command_Roles_HeaderStatus")}</th>
                    <th>{t("Pages_Command_Roles_HeaderMembers")}</th>
                    <th>{t("Pages_Command_Roles_HeaderClaims")}</th>
                    <th>{t("Pages_Command_Roles_Action")}</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleRoles.map((role) => {
                    const roleId = role.role.primaryKey;
                    const empty = roleIsEmpty(role);
                    const highImpact = roleIsHighImpact(role);
                    return (
                      <tr key={roleId ?? role.role.title}>
                        <td>
                          <strong>
                            {roleTitle(
                              role,
                              formatResource(t("Pages_Command_Roles_RolePrefix"), roleId ?? "?"),
                            )}
                          </strong>
                          <span>
                            {role.role.description?.trim() ||
                              t("Pages_Command_Roles_NoRoleDescriptionReturned")}
                          </span>
                        </td>
                        <td>
                          <StatusBadge tone={empty ? "warning" : highImpact ? "danger" : "success"}>
                            {t(
                              empty
                                ? role.memberCount === 0 && role.claims.length === 0
                                  ? "Pages_Command_Roles_Status_Empty"
                                  : role.claims.length === 0
                                    ? "Pages_Command_Roles_Status_NoClaims"
                                    : "Pages_Command_Roles_Status_NoMembers"
                                : highImpact
                                  ? "Pages_Command_Roles_Status_HighImpact"
                                  : "Pages_Command_Roles_Status_Assigned",
                            )}
                          </StatusBadge>
                        </td>
                        <td>{role.memberCount.toLocaleString()}</td>
                        <td>{role.claims.length.toLocaleString()}</td>
                        <td>
                          {roleId != null && (
                            <Link className="button button--ghost" to={`/command/roles/${roleId}`}>
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
      )}
    </section>
  );
}
