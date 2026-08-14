import { useQuery } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent, type KeyboardEvent, type MouseEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  loadCommunitySnapshot,
  memberAvatarSource,
  memberInitials,
  memberMatchesView,
  memberPermissions,
  memberStatus,
  sortMembers,
  type CommunityMember,
  type MemberView,
} from "@/features/admin/members";

const pageSize = 25;
const viewKeys: Record<MemberView, TranslationKey> = {
  attention: "Pages_Command_Members_FilterAttention",
  verified: "Pages_Command_Members_FilterVerified",
  roles: "Pages_Command_Members_FilterRoles",
  verein: "Pages_Command_Members_FilterVerein",
  all: "Pages_Command_Members_FilterAll",
};
const titleKeys: Record<MemberView, TranslationKey> = {
  attention: "Pages_Command_Members_ListTitleAttention",
  verified: "Pages_Command_Members_ListTitleVerified",
  roles: "Pages_Command_Members_ListTitleAuthorityAssignments",
  verein: "Pages_Command_Members_ListTitleVerein",
  all: "Pages_Command_Members_ListTitleAll",
};

function statusTranslationKey(member: CommunityMember): TranslationKey {
  switch (memberStatus(member)) {
    case "deleted":
      return "Pages_Command_Members_StatusDeleted";
    case "banned":
      return "Pages_Command_Members_StatusBanned";
    case "verified":
      return "Pages_Command_Members_StatusVerified";
    default:
      return "Pages_Command_Members_StatusUnverified";
  }
}

function statusTone(member: CommunityMember): "danger" | "success" | "warning" {
  const status = memberStatus(member);
  return status === "verified" ? "success" : status === "unverified" ? "warning" : "danger";
}

function vereinStatusKey(member: CommunityMember): TranslationKey {
  switch (member.vereinMembershipStatus?.toLocaleLowerCase()) {
    case "active":
      return "Pages_Command_Members_StatusActive";
    case "suspended":
      return "Pages_Command_Members_StatusSuspended";
    case "ended":
      return "Pages_Command_Members_StatusEnded";
    default:
      return "Pages_Command_Members_NoFormalMembership";
  }
}

function countText(
  value: number,
  singular: string,
  plural: string,
  format: (key: TranslationKey) => string,
) {
  return value === 1
    ? formatResource(format("Pages_Command_Members_CountSingular"), singular)
    : formatResource(format("Pages_Command_Members_CountPlural"), value, plural);
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const permissions = memberPermissions(user);
  const [view, setView] = useState<MemberView>("attention");
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(0);
  const [failedAvatars, setFailedAvatars] = useState<Set<string>>(() => new Set());
  useDocumentTitle(t("Pages_Command_Members_PageTitle"));

  const query = useQuery({
    queryKey: ["command-center", "community-snapshot", search],
    queryFn: ({ signal }) => loadCommunitySnapshot(search, signal),
    enabled: permissions.roster,
  });
  const visibleMembers = useMemo(
    () =>
      sortMembers((query.data?.members ?? []).filter((member) => memberMatchesView(member, view))),
    [query.data?.members, view],
  );
  const pageCount = Math.max(1, Math.ceil(visibleMembers.length / pageSize));
  const currentPage = Math.min(page, pageCount - 1);
  const pagedMembers = visibleMembers.slice(currentPage * pageSize, (currentPage + 1) * pageSize);
  const setActiveView = (next: MemberView) => {
    setView(next);
    setPage(0);
  };
  const submitSearch = (event: FormEvent) => {
    event.preventDefault();
    setPage(0);
    setSearch(searchDraft.trim());
  };
  const openMember = (member: CommunityMember) => {
    if (member.primaryKey) void navigate(`/command/members/${member.primaryKey}`);
  };
  const handleRowKey = (event: KeyboardEvent<HTMLTableRowElement>, member: CommunityMember) => {
    if (event.key !== "Enter" && event.key !== " ") return;
    event.preventDefault();
    openMember(member);
  };
  const stopRowNavigation = (event: MouseEvent) => event.stopPropagation();
  const snapshot = query.data;
  const attentionCount =
    (snapshot?.unverifiedMemberCount ?? 0) +
    (snapshot?.bannedMemberCount ?? 0) +
    (snapshot?.deletedMemberCount ?? 0);
  const verifiedPercentage = snapshot?.memberCount
    ? new Intl.NumberFormat(undefined, { style: "percent", maximumFractionDigits: 0 }).format(
        snapshot.verifiedMemberCount / snapshot.memberCount,
      )
    : "0%";

  if (!permissions.roster)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Members_NoAccessTitle")}>
        {t("Pages_Command_Members_NoAccessMessage")}
      </StatePanel>
    );

  return (
    <section className="members-admin-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Members")}
        title={t("Pages_Command_Members_HeaderTitle")}
        description={t("Pages_Command_Members_HeaderDescription")}
        actions={
          <>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Members_Refresh")}
            </button>
            <Link className="button button--ghost" to="/members">
              {t("Pages_Command_Members_PublicPage")}
            </Link>
          </>
        }
      />

      {query.isError && (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" onClick={() => void query.refetch()}>
              {t("Pages_Command_Members_Refresh")}
            </button>
          }
        >
          {formatResource(t("Pages_Command_Members_ErrorLoad"), query.error.message)}
        </StatePanel>
      )}

      <section
        className="command-metrics members-admin-metrics"
        aria-label={t("Pages_Command_Members_MetricAria")}
      >
        <button
          type="button"
          className={view === "attention" ? "active" : undefined}
          aria-pressed={view === "attention"}
          onClick={() => setActiveView("attention")}
        >
          <Metric
            label={t("Pages_Command_Members_StatusAttention")}
            value={countText(
              attentionCount,
              t("Pages_Command_Members_LabelMember"),
              t("Pages_Command_Members_LabelMembers"),
              t,
            )}
            detail={formatResource(
              t("Pages_Command_Members_MetricDescriptionAttention"),
              countText(
                snapshot?.unverifiedMemberCount ?? 0,
                t("Pages_Command_Members_LabelUnverifiedMember"),
                t("Pages_Command_Members_LabelUnverifiedMembers"),
                t,
              ),
              countText(
                snapshot?.bannedMemberCount ?? 0,
                t("Pages_Command_Members_LabelBannedMember"),
                t("Pages_Command_Members_LabelBannedMembers"),
                t,
              ),
            )}
          />
        </button>
        <button
          type="button"
          className={view === "verified" ? "active" : undefined}
          aria-pressed={view === "verified"}
          onClick={() => setActiveView("verified")}
        >
          <Metric
            label={t("Pages_Command_Members_StatusVerified")}
            value={countText(
              snapshot?.verifiedMemberCount ?? 0,
              t("Pages_Command_Members_LabelMember"),
              t("Pages_Command_Members_LabelMembers"),
              t,
            )}
            detail={formatResource(
              t("Pages_Command_Members_MetricDescriptionVerified"),
              verifiedPercentage,
            )}
          />
        </button>
        <button
          type="button"
          className={view === "roles" ? "active" : undefined}
          aria-pressed={view === "roles"}
          onClick={() => setActiveView("roles")}
        >
          <Metric
            label={t("Pages_Command_Members_StatusAuthority")}
            value={countText(
              snapshot?.roleCount ?? 0,
              t("Pages_Command_Members_LabelRole"),
              t("Pages_Command_Members_LabelRoles"),
              t,
            )}
            detail={formatResource(
              t("Pages_Command_Members_MetricDescriptionRoles"),
              countText(
                snapshot?.claimCount ?? 0,
                t("Pages_Command_Members_LabelClaim"),
                t("Pages_Command_Members_LabelClaims"),
                t,
              ),
              t("Pages_Command_Members_LabelAuthority"),
            )}
          />
        </button>
        <button
          type="button"
          className={view === "verein" ? "active" : undefined}
          aria-pressed={view === "verein"}
          onClick={() => setActiveView("verein")}
        >
          <Metric
            label={t("Pages_Command_Members_StatusVerein")}
            value={countText(
              snapshot?.activeVereinMemberCount ?? 0,
              t("Pages_Command_Members_LabelMember"),
              t("Pages_Command_Members_LabelMembers"),
              t,
            )}
            detail={formatResource(
              t("Pages_Command_Members_MetricDescriptionVerein"),
              countText(
                snapshot?.suspendedVereinMemberCount ?? 0,
                t("Pages_Command_Members_LabelSuspendedMember"),
                t("Pages_Command_Members_LabelSuspendedMembers"),
                t,
              ),
              countText(
                snapshot?.endedVereinMemberCount ?? 0,
                t("Pages_Command_Members_LabelEndedMember"),
                t("Pages_Command_Members_LabelEndedMembers"),
                t,
              ),
            )}
          />
        </button>
        <button
          type="button"
          className={view === "all" ? "active" : undefined}
          aria-pressed={view === "all"}
          onClick={() => setActiveView("all")}
        >
          <Metric
            label={t("Pages_Command_Members_StatusRoster")}
            value={countText(
              snapshot?.memberCount ?? 0,
              t("Pages_Command_Members_LabelMember"),
              t("Pages_Command_Members_LabelMembers"),
              t,
            )}
            detail={formatResource(
              t("Pages_Command_Members_MetricDescriptionAll"),
              countText(
                snapshot?.deletedMemberCount ?? 0,
                t("Pages_Command_Members_LabelDeletedRecord"),
                t("Pages_Command_Members_LabelDeletedRecords"),
                t,
              ),
            )}
          />
        </button>
      </section>

      <form
        className="toolbar members-admin-toolbar"
        aria-label={t("Pages_Command_Members_FilterAria")}
        onSubmit={submitSearch}
      >
        <label className="search-field">
          <span className="sr-only">{t("Pages_Command_Members_SearchPlaceholder")}</span>
          <input
            value={searchDraft}
            placeholder={t("Pages_Command_Members_SearchPlaceholder")}
            onChange={(event) => setSearchDraft(event.target.value)}
          />
        </label>
        <button className="button button--ghost" type="submit" disabled={query.isFetching}>
          {t("Pages_Command_Members_Search")}
        </button>
        <div className="filter-chips">
          {(Object.keys(viewKeys) as MemberView[]).map((candidate) => (
            <button
              key={candidate}
              type="button"
              className={view === candidate ? "active" : undefined}
              aria-pressed={view === candidate}
              onClick={() => setActiveView(candidate)}
            >
              {t(viewKeys[candidate])}
            </button>
          ))}
        </div>
      </form>

      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_Members_Loading")}</StatePanel>
      ) : (
        <section className="command-panel members-admin-roster">
          <header className="command-panel__header members-admin-panel-header">
            <span className="kicker">{t("Pages_Command_Members_ListKicker")}</span>
            <h2>{t(titleKeys[view])}</h2>
            <StatusBadge>
              {formatResource(t("Pages_Command_Members_RowCount"), visibleMembers.length)}
            </StatusBadge>
          </header>
          {pagedMembers.length === 0 ? (
            <StatePanel>{t("Pages_Command_Members_NoRows")}</StatePanel>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table members-admin-table">
                <thead>
                  <tr>
                    <th>{t("Pages_Command_Members_ColumnMember")}</th>
                    <th>{t("Pages_Command_Members_ColumnStatus")}</th>
                    <th>{t("Pages_Command_Members_ColumnVerein")}</th>
                    <th>{t("Pages_Command_Members_ColumnAuthority")}</th>
                    <th>{t("Pages_Command_Members_ColumnDirectClaims")}</th>
                    <th>{t("Pages_Command_Members_ColumnDeleted")}</th>
                    <th>{t("Pages_Command_Members_ColumnActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedMembers.map((member) => {
                    const avatar =
                      member.primaryKey && !failedAvatars.has(member.primaryKey)
                        ? memberAvatarSource(member)
                        : undefined;
                    const nickname =
                      member.nickname?.trim() || t("Pages_Command_Member_UnknownUser");
                    const roleTitles = member.roleTitles ?? [];
                    return (
                      <tr
                        key={member.primaryKey}
                        tabIndex={0}
                        aria-label={`${t("Pages_Command_Members_ActionManage")}: ${nickname}`}
                        onClick={() => openMember(member)}
                        onKeyDown={(event) => handleRowKey(event, member)}
                      >
                        <th scope="row">
                          <div className="members-admin-identity">
                            {avatar ? (
                              <img
                                src={avatar}
                                alt={formatResource(t("Pages_Command_Members_AvatarAlt"), nickname)}
                                width="42"
                                height="42"
                                loading="lazy"
                                onError={() =>
                                  member.primaryKey &&
                                  setFailedAvatars((current) =>
                                    new Set(current).add(member.primaryKey!),
                                  )
                                }
                              />
                            ) : (
                              <span
                                role="img"
                                aria-label={formatResource(
                                  t("Pages_Command_Members_AvatarAlt"),
                                  nickname,
                                )}
                              >
                                {memberInitials(nickname)}
                              </span>
                            )}
                            <span>
                              <strong>{nickname}</strong>
                              <small>{member.primaryKey}</small>
                            </span>
                          </div>
                        </th>
                        <td>
                          <StatusBadge tone={statusTone(member)}>
                            {t(statusTranslationKey(member))}
                          </StatusBadge>
                        </td>
                        <td>{t(vereinStatusKey(member))}</td>
                        <td>
                          {roleTitles.length === 0
                            ? t("Pages_Command_Members_NoRoleAssignments")
                            : roleTitles.length <= 2
                              ? roleTitles.join(", ")
                              : formatResource(
                                  t("Pages_Command_Members_MoreRoleAssignments"),
                                  roleTitles.slice(0, 2).join(", "),
                                  roleTitles.length - 2,
                                )}
                        </td>
                        <td>{member.directClaimCount ?? 0}</td>
                        <td>
                          {t(
                            member.isDeleted
                              ? "Pages_Command_Members_Yes"
                              : "Pages_Command_Members_No",
                          )}
                        </td>
                        <td onClick={stopRowNavigation}>
                          {member.primaryKey && (
                            <Link
                              className="button button--ghost button--small"
                              to={`/command/members/${member.primaryKey}`}
                            >
                              {t("Pages_Command_Members_ActionManage")}
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
          {pageCount > 1 && (
            <nav
              className="members-admin-pagination"
              aria-label={t("Pages_Command_Members_FilterAria")}
            >
              <button
                className="button button--ghost button--small"
                type="button"
                disabled={currentPage === 0}
                onClick={() => setPage(Math.max(0, currentPage - 1))}
              >
                {t("Previous")}
              </button>
              <span>
                {t("Page")} {currentPage + 1} / {pageCount}
              </span>
              <button
                className="button button--ghost button--small"
                type="button"
                disabled={currentPage + 1 >= pageCount}
                onClick={() => setPage(Math.min(pageCount - 1, currentPage + 1))}
              >
                {t("Next")}
              </button>
            </nav>
          )}
        </section>
      )}
    </section>
  );
}
