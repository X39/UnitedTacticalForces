import { useQuery } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canCreateFieldReports, canReadFieldReports } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { CommandListToolbar } from "@/components/CommandListToolbar";
import { FilterChipGroup, type FilterChipItem } from "@/components/FilterChipGroup";
import { PageStack } from "@/components/PageStack";
import { PageHeader, StatusBadge } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { reportTones, toneOf } from "@/components/statusTones";
import {
  fieldReportMatchesView,
  fieldReportState,
  loadCommandFieldReports,
  sortCommandFieldReports,
  type CommandFieldReport,
  type FieldReportView,
} from "@/features/admin/fieldReports";
import "@/features/admin/mediaAdmin.css";

const views: readonly FieldReportView[] = ["all", "published", "drafts", "featured", "deleted"];

/** Renders the page header above a state view so every state of the list keeps exactly one `h1`. */
function ReportsStateFrame({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <PageStack>
      <PageHeader title={t("Shared_CommandCenter_Nav_FieldReports")} />
      {children}
    </PageStack>
  );
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
  const viewChips: readonly FilterChipItem<FieldReportView>[] = views.map((value) => ({
    id: value,
    label: viewLabel(value),
    count: countFor(value),
  }));
  const metrics: readonly {
    view: FieldReportView;
    status: string;
    value: string;
    description: string;
  }[] = [
    {
      view: "all",
      status: t("Pages_Command_FieldReports_MetricStatus_Reports"),
      value: formatResource(
        t("Pages_Command_FieldReports_MetricValue_Report"),
        query.data?.fieldReportCount ?? 0,
      ),
      description: formatResource(
        t("Pages_Command_FieldReports_MetricDescription_All"),
        reports.length,
      ),
    },
    {
      view: "published",
      status: t("Pages_Command_FieldReports_MetricStatus_Published"),
      value: formatResource(
        t("Pages_Command_FieldReports_MetricValue_Report"),
        query.data?.publishedFieldReportCount ?? 0,
      ),
      description: t("Pages_Command_FieldReports_MetricDescription_Published"),
    },
    {
      view: "drafts",
      status: t("Pages_Command_FieldReports_MetricStatus_Drafts"),
      value: formatResource(
        t("Pages_Command_FieldReports_MetricValue_Draft"),
        query.data?.draftFieldReportCount ?? 0,
      ),
      description: t("Pages_Command_FieldReports_MetricDescription_Drafts"),
    },
    {
      view: "deleted",
      status: t("Pages_Command_FieldReports_MetricStatus_Deleted"),
      value: formatResource(
        t("Pages_Command_FieldReports_MetricValue_Report"),
        query.data?.deletedFieldReportCount ?? 0,
      ),
      description: t("Pages_Command_FieldReports_MetricDescription_Deleted"),
    },
  ];

  if (!canRead)
    return (
      <ReportsStateFrame>
        <ForbiddenState title={t("Pages_Command_FieldReports_NoAccessTitle")}>
          {t("Pages_Command_FieldReports_NoAccessMessage")}
        </ForbiddenState>
      </ReportsStateFrame>
    );

  return (
    <PageStack>
      <PageHeader
        title={t("Shared_CommandCenter_Nav_FieldReports")}
        description={t("Pages_Command_FieldReports_HeaderDescription")}
        actions={
          <>
            <ActionButton
              variant="ghost"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_FieldReports_Refresh")}
            </ActionButton>
            {canCreate && (
              <ActionLink variant="gold" to="/command/field-reports/create">
                {t("Pages_Command_FieldReports_NewReport")}
              </ActionLink>
            )}
            <ActionLink variant="ghost" to="/field-reports">
              {t("Pages_Command_FieldReports_PublicReports")}
            </ActionLink>
          </>
        }
      />

      <section className="report-metrics" aria-label={t("Pages_Command_FieldReports_SummaryAria")}>
        {metrics.map((metric) => (
          <ActionButton
            key={metric.view}
            variant="ghost"
            className="report-metric"
            aria-pressed={view === metric.view}
            onClick={() => setView(metric.view)}
          >
            <span>{metric.status}</span>
            <strong>{metric.value}</strong>
            <small>{metric.description}</small>
          </ActionButton>
        ))}
      </section>

      <CommandListToolbar
        ariaLabel={t("Pages_Command_FieldReports_FilterAria")}
        onSubmit={applySearch}
        searchLabel={t("Pages_Command_FieldReports_SearchPlaceholder")}
        searchValue={searchDraft}
        onSearchChange={setSearchDraft}
        submitLabel={t("Pages_Command_FieldReports_Search")}
        submitDisabled={query.isFetching}
      >
        <FilterChipGroup<FieldReportView>
          label={t("Pages_Command_FieldReports_FilterAria")}
          items={viewChips}
          value={view}
          onChange={setView}
        />
      </CommandListToolbar>

      {query.isError && (
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("Pages_Command_FieldReports_LoadError"), query.error.message)}
        </ErrorState>
      )}
      {query.isPending ? (
        <LoadingState label={t("Pages_Command_FieldReports_Loading")} />
      ) : query.data ? (
        <section className="command-panel">
          <header className="command-panel__header report-ledger__header">
            <div>
              <span className="kicker">{t("Pages_Command_FieldReports_LedgerKicker")}</span>
              <h2>{viewTitle()}</h2>
            </div>
            <StatusBadge>
              {formatResource(t("Pages_Command_FieldReports_RowCount"), visibleReports.length)}
            </StatusBadge>
          </header>
          {visibleReports.length === 0 ? (
            <EmptyState title={t("Pages_Command_FieldReports_EmptyTitle")}>
              {t("Pages_Command_FieldReports_EmptyBody")}
            </EmptyState>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table report-table">
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
                  {visibleReports.map((report) => (
                    <tr key={report.primaryKey}>
                      <th scope="row">
                        <strong>{report.title}</strong>
                        <span>{report.slug}</span>
                      </th>
                      <td>
                        <StatusBadge tone={toneOf(reportTones, fieldReportState(report))}>
                          {statusLabel(report)}
                        </StatusBadge>
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
                        <ActionLink
                          variant="ghost"
                          size="compact"
                          aria-label={`${t("Pages_Command_FieldReports_Edit")} ${report.title}`}
                          to={`/command/field-reports/${report.primaryKey}`}
                        >
                          {t("Pages_Command_FieldReports_Edit")}
                        </ActionLink>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      ) : null}
    </PageStack>
  );
}
