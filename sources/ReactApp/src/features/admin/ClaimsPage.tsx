import { useQuery } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManageAuthority } from "@/auth/claims";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  claimMatchesView,
  claimStatus,
  loadAuthoritySnapshot,
  sortAuthorityClaims,
  type AuthorityClaim,
  type ClaimStatus,
  type ClaimView,
  type PlainClaim,
} from "@/features/admin/authority";

const viewKeys: Record<ClaimView, TranslationKey> = {
  all: "Pages_Command_Claims_FilterAll",
  prefix: "Pages_Command_Claims_FilterPrefix",
  "role-backed": "Pages_Command_Claims_FilterRoleBacked",
  direct: "Pages_Command_Claims_FilterDirect",
  unused: "Pages_Command_Claims_FilterUnused",
};
const titleKeys: Record<ClaimView, TranslationKey> = {
  all: "Pages_Command_Claims_ListTitle_All",
  prefix: "Pages_Command_Claims_ListTitle_Prefix",
  "role-backed": "Pages_Command_Claims_ListTitle_RoleBacked",
  direct: "Pages_Command_Claims_ListTitle_Direct",
  unused: "Pages_Command_Claims_ListTitle_Unused",
};
const statusCopy: Record<
  ClaimStatus,
  { key: TranslationKey; tone: "neutral" | "success" | "warning" | "info" }
> = {
  unused: { key: "Pages_Command_Claims_StatusUnused", tone: "neutral" },
  direct: { key: "Pages_Command_Claims_StatusDirect", tone: "warning" },
  prefix: { key: "Pages_Command_Claims_StatusPrefix", tone: "info" },
  "role-backed": { key: "Pages_Command_Claims_StatusRoleBacked", tone: "success" },
};

function stableClaimKey(row: AuthorityClaim): string {
  return String(row.claim.primaryKey ?? `${row.claim.identifier}:${row.claim.value ?? ""}`);
}

export function claimTitle(claim: PlainClaim, fallback: string): string {
  return claim.title?.trim() || claim.identifier?.trim() || fallback;
}

export function claimIdentifier(claim: PlainClaim, fallback: string): string {
  const identifier = claim.identifier?.trim() || fallback;
  return claim.value?.trim() ? `${identifier} = ${claim.value}` : identifier;
}

export function claimValue(claim: PlainClaim, fallback: string): string {
  if (!claim.value?.trim()) return fallback;
  return claim.valueType?.trim() ? `${claim.value} (${claim.valueType})` : claim.value;
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const authorized = canManageAuthority(user);
  const [view, setView] = useState<ClaimView>("all");
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [selectedKey, setSelectedKey] = useState<string>();
  const query = useQuery({
    queryKey: ["command-center", "authority-snapshot", search],
    queryFn: ({ signal }) => loadAuthoritySnapshot(search, signal),
    enabled: authorized,
  });
  const snapshot = query.data;
  const counts = useMemo(() => {
    const claims = snapshot?.claims ?? [];
    return {
      prefix: claims.filter((row) => row.claim.isPrefix).length,
      roleBacked: claims.filter((row) => row.roleCount > 0).length,
      roleAttachments: claims.reduce((sum, row) => sum + row.roleCount, 0),
      directAssignments: claims.reduce((sum, row) => sum + row.directMemberCount, 0),
    };
  }, [snapshot?.claims]);
  const visibleClaims = useMemo(
    () =>
      sortAuthorityClaims((snapshot?.claims ?? []).filter((row) => claimMatchesView(row, view))),
    [snapshot?.claims, view],
  );
  const selectedClaim =
    visibleClaims.find((row) => stableClaimKey(row) === selectedKey) ?? visibleClaims[0];
  const submitSearch = (event: FormEvent) => {
    event.preventDefault();
    setSelectedKey(undefined);
    setSearch(searchDraft.trim());
  };
  const formatCount = (value: number, singular: string) =>
    value === 1
      ? formatResource(t("Pages_Command_Claims_CountSingular"), singular)
      : formatResource(t("Pages_Command_Claims_CountPlural"), value, singular);

  if (!authorized)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Claims_NoAccessTitle")}>
        {t("Pages_Command_Claims_NoAccessMessage")}
      </StatePanel>
    );

  return (
    <section className="authority-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_CurrentArea_Claims")}
        title={t("Pages_Command_Claims_HeaderTitle")}
        description={t("Pages_Command_Claims_HeaderDescription")}
        actions={
          <button
            className="button button--ghost"
            type="button"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            {t("Pages_Command_Claims_Refresh")}
          </button>
        }
      />

      {query.isError && (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" onClick={() => void query.refetch()}>
              {t("Pages_Command_Claims_Refresh")}
            </button>
          }
        >
          {formatResource(t("Pages_Command_Claims_LoadError"), query.error.message)}
        </StatePanel>
      )}

      <section
        className="command-metrics authority-metrics"
        aria-label={t("Pages_Command_Claims_SummaryAria")}
      >
        <button
          type="button"
          className={view === "all" ? "active" : undefined}
          aria-pressed={view === "all"}
          onClick={() => setView("all")}
        >
          <Metric
            label={t("Pages_Command_Claims_MetricStatus_Claims")}
            value={formatResource(
              t("Pages_Command_Claims_MetricValue_Claims"),
              snapshot?.claimCount ?? 0,
            )}
            detail={formatResource(
              t("Pages_Command_Claims_MetricDescription_All"),
              snapshot?.claims.length ?? 0,
            )}
          />
        </button>
        <button
          type="button"
          className={view === "prefix" ? "active" : undefined}
          aria-pressed={view === "prefix"}
          onClick={() => setView("prefix")}
        >
          <Metric
            label={t("Pages_Command_Claims_MetricStatus_Prefix")}
            value={formatResource(
              t("Pages_Command_Claims_MetricValue_PrefixClaims"),
              counts.prefix,
            )}
            detail={t("Pages_Command_Claims_MetricDescription_Prefix")}
          />
        </button>
        <button
          type="button"
          className={view === "role-backed" ? "active" : undefined}
          aria-pressed={view === "role-backed"}
          onClick={() => setView("role-backed")}
        >
          <Metric
            label={t("Pages_Command_Claims_MetricStatus_RoleBacked")}
            value={formatResource(
              t("Pages_Command_Claims_MetricValue_RoleBackedClaims"),
              counts.roleBacked,
            )}
            detail={formatResource(
              t("Pages_Command_Claims_MetricDescription_RoleBacked"),
              counts.roleAttachments,
            )}
          />
        </button>
        <button
          type="button"
          className={view === "direct" ? "active" : undefined}
          aria-pressed={view === "direct"}
          onClick={() => setView("direct")}
        >
          <Metric
            label={t("Pages_Command_Claims_MetricStatus_Direct")}
            value={formatResource(
              t("Pages_Command_Claims_MetricValue_DirectClaims"),
              counts.directAssignments,
            )}
            detail={t("Pages_Command_Claims_MetricDescription_Direct")}
          />
        </button>
      </section>

      <form
        className="toolbar authority-toolbar"
        aria-label={t("Pages_Command_Claims_FilterAria")}
        onSubmit={submitSearch}
      >
        <label className="search-field">
          <span className="sr-only">{t("Pages_Command_Claims_SearchPlaceholder")}</span>
          <input
            value={searchDraft}
            placeholder={t("Pages_Command_Claims_SearchPlaceholder")}
            onChange={(event) => setSearchDraft(event.target.value)}
          />
        </label>
        <button className="button button--ghost" type="submit" disabled={query.isFetching}>
          {t("Pages_Command_Claims_Search")}
        </button>
        <div className="filter-chips">
          {(Object.keys(viewKeys) as ClaimView[]).map((candidate) => (
            <button
              key={candidate}
              type="button"
              className={view === candidate ? "active" : undefined}
              aria-pressed={view === candidate}
              onClick={() => setView(candidate)}
            >
              {t(viewKeys[candidate])}
            </button>
          ))}
        </div>
      </form>

      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_Claims_Loading")}</StatePanel>
      ) : (
        <div className="authority-workspace">
          <section className="command-panel authority-list-panel">
            <header className="command-panel__header authority-panel__header">
              <span className="kicker">{t("Pages_Command_Claims_ListKicker")}</span>
              <h2>{t(titleKeys[view])}</h2>
              <StatusBadge>
                {formatResource(t("Pages_Command_Claims_RowCount"), visibleClaims.length)}
              </StatusBadge>
            </header>
            {visibleClaims.length === 0 ? (
              <StatePanel>{t("Pages_Command_Claims_NoClaims")}</StatePanel>
            ) : (
              <div className="command-table-scroll">
                <table className="command-table authority-table">
                  <thead>
                    <tr>
                      <th>{t("Pages_Command_Claims_TableHeader_Claim")}</th>
                      <th>{t("Pages_Command_Claims_TableHeader_Status")}</th>
                      <th>{t("Pages_Command_Claims_TableHeader_Identifier")}</th>
                      <th>{t("Pages_Command_Claims_TableHeader_Category")}</th>
                      <th>{t("Pages_Command_Claims_TableHeader_Usage")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleClaims.map((row) => {
                      const key = stableClaimKey(row);
                      const status = statusCopy[claimStatus(row)];
                      const isSelected = selectedClaim
                        ? key === stableClaimKey(selectedClaim)
                        : false;
                      return (
                        <tr
                          key={key}
                          tabIndex={0}
                          aria-selected={isSelected}
                          className={isSelected ? "is-selected" : undefined}
                          onClick={() => setSelectedKey(key)}
                          onKeyDown={(event) => {
                            if (event.key !== "Enter" && event.key !== " ") return;
                            event.preventDefault();
                            setSelectedKey(key);
                          }}
                        >
                          <th scope="row">
                            <strong>
                              {claimTitle(row.claim, t("Pages_Command_Claims_FallbackUnamedClaim"))}
                            </strong>
                            <span>
                              {claimValue(
                                row.claim,
                                t("Pages_Command_Claims_FallbackNoFixedValue"),
                              )}
                            </span>
                          </th>
                          <td>
                            <StatusBadge tone={status.tone}>{t(status.key)}</StatusBadge>
                          </td>
                          <td>
                            {claimIdentifier(
                              row.claim,
                              t("Pages_Command_Claims_FallbackNoIdentifier"),
                            )}
                          </td>
                          <td>{row.claim.category || t("Pages_Command_Claims_NotCategorized")}</td>
                          <td>
                            {formatResource(
                              t("Pages_Command_Claims_UsageSummary"),
                              formatCount(row.roleCount, t("Pages_Command_Claims_LabelRole")),
                              formatCount(
                                row.directMemberCount,
                                t("Pages_Command_Claims_LabelDirectMember"),
                              ),
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

          <aside
            className="command-panel authority-detail-panel"
            aria-label={t("Pages_Command_Claims_DetailAria")}
          >
            {!selectedClaim ? (
              <StatePanel>{t("Pages_Command_Claims_SelectClaim")}</StatePanel>
            ) : (
              <>
                <header className="command-panel__header">
                  <span className="kicker">{t("Pages_Command_Claims_SelectedKicker")}</span>
                  <h2>
                    {claimTitle(selectedClaim.claim, t("Pages_Command_Claims_FallbackUnamedClaim"))}
                  </h2>
                  <StatusBadge tone={statusCopy[claimStatus(selectedClaim)].tone}>
                    {t(statusCopy[claimStatus(selectedClaim)].key)}
                  </StatusBadge>
                </header>
                <dl className="command-detail-list">
                  <dt>{t("Pages_Command_Claims_Detail_LabelIdentifier")}</dt>
                  <dd>
                    {selectedClaim.claim.identifier || t("Pages_Command_Claims_NoIdentifier")}
                  </dd>
                  <dt>{t("Pages_Command_Claims_Detail_LabelCategory")}</dt>
                  <dd>
                    {selectedClaim.claim.category || t("Pages_Command_Claims_NotCategorized")}
                  </dd>
                  <dt>{t("Pages_Command_Claims_Detail_LabelValue")}</dt>
                  <dd>
                    {claimValue(
                      selectedClaim.claim,
                      t("Pages_Command_Claims_FallbackNoFixedValue"),
                    )}
                  </dd>
                  <dt>{t("Pages_Command_Claims_Detail_LabelPrefix")}</dt>
                  <dd>
                    {t(
                      selectedClaim.claim.isPrefix
                        ? "Pages_Command_Claims_Yes"
                        : "Pages_Command_Claims_No",
                    )}
                  </dd>
                  <dt>{t("Pages_Command_Claims_Detail_LabelDirectMembers")}</dt>
                  <dd>
                    {formatCount(
                      selectedClaim.directMemberCount,
                      t("Pages_Command_Claims_LabelDirectMembers"),
                    )}
                  </dd>
                  <dt>{t("Pages_Command_Claims_Detail_LabelRoles")}</dt>
                  <dd>
                    {formatCount(selectedClaim.roleCount, t("Pages_Command_Claims_LabelRoles"))}
                  </dd>
                  <dt>{t("Pages_Command_Claims_Detail_LabelDescription")}</dt>
                  <dd>
                    {selectedClaim.claim.description || t("Pages_Command_Claims_NoDescription")}
                  </dd>
                </dl>
                <section
                  className="authority-role-coverage"
                  aria-label={t("Pages_Command_Claims_RolesAria")}
                >
                  <h3>{t("Pages_Command_Claims_RoleCoverage")}</h3>
                  {selectedClaim.roleTitles.length === 0 ? (
                    <p>{t("Pages_Command_Claims_NoRoleCoverage")}</p>
                  ) : (
                    <div>
                      {selectedClaim.roleTitles.map((role) => (
                        <span key={role}>{role}</span>
                      ))}
                    </div>
                  )}
                </section>
                <div className="authority-actions">
                  <Link className="button button--ghost" to="/command/roles">
                    {t("Pages_Command_Claims_OpenRolesWorkspace")}
                  </Link>
                  <Link className="button button--ghost" to="/command/members">
                    {t("Pages_Command_Claims_OpenMembersAuthority")}
                  </Link>
                </div>
              </>
            )}
          </aside>
        </div>
      )}
    </section>
  );
}
