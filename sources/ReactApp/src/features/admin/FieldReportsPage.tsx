import { useQuery } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canCreateFieldReports, canReadFieldReports } from "@/auth/claims";
import { PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  fieldReportMatchesView,
  fieldReportState,
  loadCommandFieldReports,
  sortCommandFieldReports,
  type CommandFieldReport,
  type FieldReportState,
  type FieldReportView,
} from "@/features/admin/fieldReports";

const views: readonly FieldReportView[] = ["all", "published", "drafts", "featured", "deleted"];

function stateTone(state: FieldReportState): "success" | "warning" | "danger" | "info" {
  switch (state) {
    case "published":
      return "success";
    case "scheduled":
      return "info";
    case "draft":
      return "warning";
    case "deleted":
      return "danger";
  }
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const canRead = canReadFieldReports(user);
  const canCreate = canCreateFieldReports(user);
  const [view, setView] = useState<FieldReportView>("all");
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const query = useQuery({
    queryKey: ["command-field-reports", search],
    queryFn: ({ signal }) => loadCommandFieldReports(search, signal),
    enabled: canRead,
  });
  const reports = useMemo(
    () => sortCommandFieldReports(query.data?.reports ?? []),
    [query.data?.reports],
  );
  const visibleReports = useMemo(
    () => reports.filter((report) => fieldReportMatchesView(report, view)),
    [reports, view],
  );

  const applySearch = (event: FormEvent) => {
    event.preventDefault();
    setSearch(searchDraft.trim());
  };
  const viewLabel = (value: FieldReportView) => {
    switch (value) {
      case "all":
        return t("Pages_Command_FieldReports_FilterAll");
      case "published":
        return t("Pages_Command_FieldReports_FilterPublished");
      case "drafts":
        return t("Pages_Command_FieldReports_FilterDrafts");
      case "featured":
        return t("Pages_Command_FieldReports_FilterFeatured");
      case "deleted":
        return t("Pages_Command_FieldReports_FilterDeleted");
    }
  };
  const viewTitle = () => {
    switch (view) {
      case "all":
        return t("Pages_Command_FieldReports_ListTitle_All");
      case "published":
        return t("Pages_Command_FieldReports_ListTitle_Published");
      case "drafts":
        return t("Pages_Command_FieldReports_ListTitle_Drafts");
      case "featured":
        return t("Pages_Command_FieldReports_ListTitle_Featured");
      case "deleted":
        return t("Pages_Command_FieldReports_ListTitle_Deleted");
    }
  };
  const statusLabel = (report: CommandFieldReport) => {
    switch (fieldReportState(report)) {
      case "published":
        return t("Pages_Command_FieldReports_StatusPublished");
      case "scheduled":
        return t("Pages_Command_FieldReports_StatusScheduled");
      case "draft":
        return t("Pages_Command_FieldReports_StatusDraft");
      case "deleted":
        return t("Pages_Command_FieldReports_StatusDeleted");
    }
  };
  const countFor = (value: FieldReportView) =>
    reports.filter((report) => fieldReportMatchesView(report, value)).length;

  if (!canRead)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_FieldReports_NoAccessTitle")}>
        {t("Pages_Command_FieldReports_NoAccessMessage")}
      </StatePanel>
    );

  return (
    <section className="field-report-admin-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_FieldReports")}
        title={t("Pages_Command_FieldReports_HeaderTitle")}
        description={t("Pages_Command_FieldReports_HeaderDescription")}
        actions={
          <>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_FieldReports_Refresh")}
            </button>
            {canCreate && (
              <Link className="button button--primary" to="/command/field-reports/create">
                {t("Pages_Command_FieldReports_NewReport")}
              </Link>
            )}
            <Link className="button button--ghost" to="/field-reports">
              {t("Pages_Command_FieldReports_PublicReports")}
            </Link>
          </>
        }
      />

      <section
        className="command-metrics field-report-admin-metrics"
        aria-label={t("Pages_Command_FieldReports_SummaryAria")}
      >
        <button
          type="button"
          className={view === "all" ? "active" : undefined}
          aria-pressed={view === "all"}
          onClick={() => setView("all")}
        >
          <span>{t("Pages_Command_FieldReports_MetricStatus_Reports")}</span>
          <strong>
            {formatResource(
              t("Pages_Command_FieldReports_MetricValue_Report"),
              query.data?.fieldReportCount ?? 0,
            )}
          </strong>
          <small>
            {formatResource(t("Pages_Command_FieldReports_MetricDescription_All"), reports.length)}
          </small>
        </button>
        <button
          type="button"
          className={view === "published" ? "active" : undefined}
          aria-pressed={view === "published"}
          onClick={() => setView("published")}
        >
          <span>{t("Pages_Command_FieldReports_MetricStatus_Published")}</span>
          <strong>
            {formatResource(
              t("Pages_Command_FieldReports_MetricValue_Report"),
              query.data?.publishedFieldReportCount ?? 0,
            )}
          </strong>
          <small>{t("Pages_Command_FieldReports_MetricDescription_Published")}</small>
        </button>
        <button
          type="button"
          className={view === "drafts" ? "active" : undefined}
          aria-pressed={view === "drafts"}
          onClick={() => setView("drafts")}
        >
          <span>{t("Pages_Command_FieldReports_MetricStatus_Drafts")}</span>
          <strong>
            {formatResource(
              t("Pages_Command_FieldReports_MetricValue_Draft"),
              query.data?.draftFieldReportCount ?? 0,
            )}
          </strong>
          <small>{t("Pages_Command_FieldReports_MetricDescription_Drafts")}</small>
        </button>
        <button
          type="button"
          className={view === "deleted" ? "active" : undefined}
          aria-pressed={view === "deleted"}
          onClick={() => setView("deleted")}
        >
          <span>{t("Pages_Command_FieldReports_MetricStatus_Deleted")}</span>
          <strong>
            {formatResource(
              t("Pages_Command_FieldReports_MetricValue_Report"),
              query.data?.deletedFieldReportCount ?? 0,
            )}
          </strong>
          <small>{t("Pages_Command_FieldReports_MetricDescription_Deleted")}</small>
        </button>
      </section>

      <form
        className="toolbar field-report-admin-toolbar"
        aria-label={t("Pages_Command_FieldReports_FilterAria")}
        onSubmit={applySearch}
      >
        <label className="search-field">
          <span className="sr-only">{t("Pages_Command_FieldReports_SearchPlaceholder")}</span>
          <input
            value={searchDraft}
            placeholder={t("Pages_Command_FieldReports_SearchPlaceholder")}
            onChange={(event) => setSearchDraft(event.target.value)}
          />
        </label>
        <button className="button button--ghost" type="submit" disabled={query.isFetching}>
          {t("Pages_Command_FieldReports_Search")}
        </button>
        <div className="filter-chips">
          {views.map((value) => (
            <button
              key={value}
              className={view === value ? "active" : undefined}
              type="button"
              aria-pressed={view === value}
              onClick={() => setView(value)}
            >
              {viewLabel(value)} <span>{countFor(value)}</span>
            </button>
          ))}
        </div>
      </form>

      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_FieldReports_Loading")}</StatePanel>
      ) : query.isError ? (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" type="button" onClick={() => query.refetch()}>
              {t("Pages_Command_FieldReports_Refresh")}
            </button>
          }
        >
          {formatResource(t("Pages_Command_FieldReports_LoadError"), query.error.message)}
        </StatePanel>
      ) : (
        <section className="command-panel field-report-admin-ledger">
          <header className="command-panel__header field-report-admin-ledger__header">
            <span className="kicker">{t("Pages_Command_FieldReports_LedgerKicker")}</span>
            <h2>{viewTitle()}</h2>
            <StatusBadge>
              {formatResource(t("Pages_Command_FieldReports_RowCount"), visibleReports.length)}
            </StatusBadge>
          </header>
          {visibleReports.length === 0 ? (
            <StatePanel kind="empty">{t("Pages_Command_FieldReports_NoReports")}</StatePanel>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table field-report-admin-table">
                <caption className="sr-only">{viewTitle()}</caption>
                <thead>
                  <tr>
                    <th scope="col">{t("Pages_Command_FieldReports_TableHeader_Report")}</th>
                    <th scope="col">{t("Pages_Command_FieldReports_TableHeader_Status")}</th>
                    <th scope="col">{t("Pages_Command_FieldReports_TableHeader_Category")}</th>
                    <th scope="col">{t("Pages_Command_FieldReports_TableHeader_Updated")}</th>
                    <th scope="col">{t("Pages_Command_FieldReports_TableHeader_Flags")}</th>
                    <th scope="col">{t("Pages_Command_FieldReports_TableHeader_Audit")}</th>
                    <th scope="col">{t("Pages_Command_FieldReports_TableHeader_Actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleReports.map((report) => {
                    const state = fieldReportState(report);
                    return (
                      <tr key={report.primaryKey}>
                        <th scope="row">
                          <strong>{report.title}</strong>
                          <span>{report.slug}</span>
                        </th>
                        <td>
                          <StatusBadge tone={stateTone(state)}>{statusLabel(report)}</StatusBadge>
                        </td>
                        <td>{report.category}</td>
                        <td>
                          <strong>{formatDateTime(report.timeStampUpdated)}</strong>
                          <span>
                            {formatDateTime(report.timeStampPublished ?? report.timeStampUpdated)}
                          </span>
                        </td>
                        <td>
                          <strong>
                            {report.isFeatured
                              ? t("Pages_Command_FieldReports_FlagFeatured")
                              : t("Pages_Command_FieldReports_FlagStandard")}
                          </strong>
                          <span>
                            {report.isPublished
                              ? t("Pages_Command_FieldReports_FlagPublic")
                              : t("Pages_Command_FieldReports_FlagEditorial")}
                          </span>
                        </td>
                        <td>
                          <strong>
                            {formatResource(
                              t("Pages_Command_FieldReports_RecordCount"),
                              report.auditRecordCount,
                            )}
                          </strong>
                          <span>
                            {formatResource(
                              t("Pages_Command_FieldReports_SortValue"),
                              report.sortOrder,
                            )}
                          </span>
                        </td>
                        <td>
                          <Link
                            className="button button--ghost button--small"
                            to={`/command/field-reports/${report.primaryKey}`}
                          >
                            {t("Pages_Command_FieldReports_Edit")}
                          </Link>
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
