import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canCreateRoles, canManageAuthority } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppToastOnChange } from "@/components/AppToasts";
import { CommandListToolbar } from "@/components/CommandListToolbar";
import { FilterChipGroup, type FilterChipItem } from "@/components/FilterChipGroup";
import { TextAreaField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { toneOf } from "@/components/statusTones";
import {
  Metric,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { loadAuthoritySnapshot } from "@/features/admin/authority";
import { roleTones } from "@/features/admin/authorityTones";
import {
  createRole,
  normalizeAuthorityRole,
  roleIsEmpty,
  roleIsHighImpact,
  type AuthorityRole,
} from "@/features/admin/roles";
import "@/features/admin/authority.css";

type RoleView = "all" | "assigned" | "empty" | "high-impact";
type RoleStatus = "empty" | "no-claims" | "no-members" | "high-impact" | "assigned";

const roleViews: readonly RoleView[] = ["all", "assigned", "empty", "high-impact"];
const roleViewKeys: Record<RoleView, TranslationKey> = {
  all: "Pages_Command_Roles_FilterAll",
  assigned: "Pages_Command_Roles_FilterAssigned",
  empty: "Pages_Command_Roles_FilterEmpty",
  "high-impact": "Pages_Command_Roles_FilterHighImpact",
};
const listTitleKeys: Record<RoleView, TranslationKey> = {
  all: "Pages_Command_Roles_ListTitle_All",
  assigned: "Pages_Command_Roles_ListTitle_Assigned",
  empty: "Pages_Command_Roles_ListTitle_Empty",
  "high-impact": "Pages_Command_Roles_ListTitle_HighImpact",
};
const roleStatusKeys: Record<RoleStatus, TranslationKey> = {
  empty: "Pages_Command_Roles_Status_Empty",
  "no-claims": "Pages_Command_Roles_Status_NoClaims",
  "no-members": "Pages_Command_Roles_Status_NoMembers",
  "high-impact": "Pages_Command_Roles_Status_HighImpact",
  assigned: "Pages_Command_Roles_Status_Assigned",
};

function roleTitle(role: AuthorityRole, fallback: string): string {
  return role.role.title?.trim() || fallback;
}

function matchesView(role: AuthorityRole, view: RoleView): boolean {
  if (view === "assigned") return role.memberCount > 0 && role.claims.length > 0;
  if (view === "empty") return roleIsEmpty(role);
  if (view === "high-impact") return roleIsHighImpact(role);
  return true;
}

function roleStatus(role: AuthorityRole): RoleStatus {
  if (roleIsEmpty(role)) {
    if (role.memberCount === 0 && role.claims.length === 0) return "empty";
    return role.claims.length === 0 ? "no-claims" : "no-members";
  }
  return roleIsHighImpact(role) ? "high-impact" : "assigned";
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
  useAppToastOnChange(created ? t("Pages_Command_Roles_CreateSuccess") : undefined);
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
      <section>
        <PageHeader
          kicker={t("Shared_CommandCenter_CurrentArea_Roles")}
          title={t("Pages_Command_Roles_HeaderTitle")}
          description={t("Pages_Command_Roles_HeaderDescription")}
        />
        <PageStack>
          <ForbiddenState title={t("Pages_Command_Roles_AccessRequiredTitle")}>
            {t("Pages_Command_Roles_AccessRequiredDescription")}
          </ForbiddenState>
        </PageStack>
      </section>
    );

  const assignedCount = roles.reduce((sum, role) => sum + role.memberCount, 0);
  const claimCount = roles.reduce((sum, role) => sum + role.claims.length, 0);
  const emptyCount = roles.filter(roleIsEmpty).length;
  const highImpactCount = roles.filter(roleIsHighImpact).length;
  const viewChips: FilterChipItem<RoleView>[] = roleViews.map((id) => ({
    id,
    label: t(roleViewKeys[id]),
    count: roles.filter((role) => matchesView(role, id)).length,
  }));
  const failedWithoutData = query.isError && !query.data;

  return (
    <section>
      <PageHeader
        kicker={t("Shared_CommandCenter_CurrentArea_Roles")}
        title={t("Pages_Command_Roles_HeaderTitle")}
        description={t("Pages_Command_Roles_HeaderDescription")}
        actions={
          <>
            {canCreate && (
              <ActionButton
                variant="primary"
                disabled={mutation.isPending}
                onClick={() => {
                  setCreated(false);
                  setCreating((value) => !value);
                }}
              >
                {t("Pages_Command_Roles_CreateRole")}
              </ActionButton>
            )}
            <ActionButton
              variant="ghost"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Roles_ActionRefresh")}
            </ActionButton>
          </>
        }
      />

      <PageStack>
        <section
          className="command-metrics authority-metrics"
          aria-label={t("Pages_Command_Roles_MetricsAriaLabel")}
        >
          <ActionButton
            variant="ghost"
            className="authority-metric"
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
          </ActionButton>
          <ActionButton
            variant="ghost"
            className="authority-metric"
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
              detail={formatResource(
                t("Pages_Command_Roles_Metric_AssignedDescription"),
                claimCount,
              )}
            />
          </ActionButton>
          <ActionButton
            variant="ghost"
            className="authority-metric"
            aria-pressed={view === "empty"}
            onClick={() => setView("empty")}
          >
            <Metric
              label={t("Pages_Command_Roles_MetricEmptyStatus")}
              value={String(emptyCount)}
              detail={t("Pages_Command_Roles_Metric_EmptyDescription")}
            />
          </ActionButton>
          <ActionButton
            variant="ghost"
            className="authority-metric"
            aria-pressed={view === "high-impact"}
            onClick={() => setView("high-impact")}
          >
            <Metric
              label={t("Pages_Command_Roles_MetricHighImpactStatus")}
              value={String(highImpactCount)}
              detail={t("Pages_Command_Roles_Metric_HighImpactDescription")}
            />
          </ActionButton>
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
          <FilterChipGroup<RoleView>
            label={t("Pages_Command_Roles_ToolbarAriaLabel")}
            items={viewChips}
            value={view}
            onChange={setView}
          />
        </CommandListToolbar>

        {creating && (
          <form className="command-panel authority-form-panel" onSubmit={submitCreate}>
            <header className="command-panel__header">
              <span className="kicker">{t("Pages_Command_Roles_CreateRoleKicker")}</span>
              <h2>{t("Pages_Command_Roles_CreateRole")}</h2>
            </header>
            <div className="authority-form-fields">
              <TextField
                label={t("Pages_Command_Roles_FieldTitle")}
                maxLength={1024}
                required
                value={title}
                disabled={mutation.isPending}
                onChange={(event) => setTitle(event.target.value)}
              />
              <TextAreaField
                label={t("Pages_Command_Roles_FieldDescription")}
                rows={3}
                maxLength={1024}
                value={description}
                disabled={mutation.isPending}
                onChange={(event) => setDescription(event.target.value)}
              />
            </div>
            {mutation.isError && (
              <div className="authority-form-fields">
                <StatePanel kind="error">
                  {formatResource(t("Pages_Command_Roles_SaveError"), mutation.error.message)}
                </StatePanel>
              </div>
            )}
            <div className="authority-form-actions">
              <ActionButton
                variant="gold"
                type="submit"
                disabled={!title.trim() || mutation.isPending}
              >
                {t("Pages_Command_Roles_CreateRole")}
              </ActionButton>
              <ActionButton
                variant="ghost"
                disabled={mutation.isPending}
                onClick={() => setCreating(false)}
              >
                {t("Pages_Command_Roles_CreateRole_Dismiss")}
              </ActionButton>
            </div>
          </form>
        )}

        {query.isError && (
          <ErrorState onRetry={() => void query.refetch()}>{query.error.message}</ErrorState>
        )}
        {query.isPending ? (
          <LoadingState label={t("Pages_Command_Roles_Loading")} />
        ) : (
          !failedWithoutData && (
            <section className="command-panel authority-list-panel">
              <header className="command-panel__header authority-panel__header">
                <span className="kicker">{t("Pages_Command_Roles_RoleLedgerKicker")}</span>
                <h2>{t(listTitleKeys[view])}</h2>
                <StatusBadge>
                  {formatCount(
                    visibleRoles.length,
                    t("Pages_Command_Roles_Count_Singular_Row"),
                    t("Pages_Command_Roles_Count_Plural_Row"),
                  )}
                </StatusBadge>
              </header>
              {visibleRoles.length === 0 ? (
                <EmptyState title={t("Pages_Command_Roles_EmptyTitle")}>
                  {t("Pages_Command_Roles_EmptyBody")}
                </EmptyState>
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
                        const status = roleStatus(role);
                        const name = roleTitle(
                          role,
                          formatResource(t("Pages_Command_Roles_RolePrefix"), roleId ?? "?"),
                        );
                        return (
                          <tr key={roleId ?? role.role.title}>
                            <td>
                              <strong>{name}</strong>
                              <span>
                                {role.role.description?.trim() ||
                                  t("Pages_Command_Roles_NoRoleDescriptionReturned")}
                              </span>
                            </td>
                            <td>
                              <StatusBadge tone={toneOf(roleTones, status)}>
                                {t(roleStatusKeys[status])}
                              </StatusBadge>
                            </td>
                            <td>{role.memberCount.toLocaleString()}</td>
                            <td>{role.claims.length.toLocaleString()}</td>
                            <td>
                              {roleId != null && (
                                <ActionLink
                                  variant="ghost"
                                  to={`/command/roles/${roleId}`}
                                  aria-label={`${t("Pages_Command_Roles_OpenDetails")} ${name}`}
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
          )
        )}
      </PageStack>
    </section>
  );
}
