import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { currentLocale, formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManageVereinDocuments } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { FilterChipGroup } from "@/components/FilterChipGroup";
import { PageStack } from "@/components/PageStack";
import { Metric, PageHeader, StatusBadge } from "@/components/primitives";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { reportTones, toneOf } from "@/components/statusTones";
import {
  activeVereinRevision,
  latestPublishedVereinRevision,
  loadVereinDocuments,
  vereinDocumentMatches,
  vereinDocumentQueryKeys,
  type VereinAdminDocument,
  type VereinDocumentFilter,
  type VereinDocumentKind,
} from "@/features/admin/vereinDocuments";

const filterLabels: Record<VereinDocumentFilter, TranslationKey> = {
  all: "Pages_Command_Verein_DocumentsFilterAll",
  Satzung: "Satzung",
  Minutes: "Auto_BeschlussMinutes_6fa4d247",
};

const listTitles: Record<VereinDocumentFilter, TranslationKey> = {
  all: "Pages_Command_Verein_DocumentsListTitleAll",
  Satzung: "Pages_Command_Verein_DocumentsListTitleSatzung",
  Minutes: "Pages_Command_Verein_DocumentsListTitleMinutes",
};

const filterOrder = ["all", "Satzung", "Minutes"] as const;

function documentTitle(document: VereinAdminDocument, fallback: string): string {
  return activeVereinRevision(document)?.title || fallback;
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const authorized = canManageVereinDocuments(user);
  const [filter, setFilter] = useState<VereinDocumentFilter>("all");
  const [search, setSearch] = useState("");
  const query = useQuery({
    queryKey: vereinDocumentQueryKeys.all,
    queryFn: ({ signal }) => loadVereinDocuments(signal),
    enabled: authorized,
  });
  const documents = useMemo(() => query.data ?? [], [query.data]);
  const counts = useMemo(
    () => ({
      all: documents.length,
      Satzung: documents.filter((document) => document.kind === "Satzung").length,
      Minutes: documents.filter((document) => document.kind === "Minutes").length,
    }),
    [documents],
  );
  const visible = useMemo(
    () =>
      documents
        .filter((document) => vereinDocumentMatches(document, filter, search))
        .sort(
          (left, right) =>
            left.kind.localeCompare(right.kind) ||
            Date.parse(right.timeStampUpdated) - Date.parse(left.timeStampUpdated),
        ),
    [documents, filter, search],
  );
  const countLabel = (count: number) =>
    `${count.toLocaleString(currentLocale())} ${t(
      count === 1
        ? "Pages_Command_Verein_Count_Singular_Document"
        : "Pages_Command_Verein_Count_Plural_Document",
    )}`;
  const kindLabel = (kind: VereinDocumentKind) => t(filterLabels[kind]);

  if (!authorized)
    return (
      <PageStack>
        <PageHeader
          kicker={t("Shared_CommandCenter_Nav_VereinDocuments")}
          title={t("Pages_Command_Verein_DocumentsPageTitle")}
        />
        <ForbiddenState title={t("Pages_Command_Verein_DocumentsAccessRequiredTitle")}>
          {t("Pages_Command_Verein_DocumentsAccessRequiredDescription")}
        </ForbiddenState>
      </PageStack>
    );

  return (
    <PageStack>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_VereinDocuments")}
        title={t("Pages_Command_Verein_DocumentsPageTitle")}
        description={t("Pages_Command_Verein_DocumentsPageDescription")}
        actions={
          <>
            <ActionLink variant="ghost" to="/command/content">
              {t("Pages_Command_Content_BackToHub")}
            </ActionLink>
            <ActionButton
              variant="ghost"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Verein_ActionRefresh")}
            </ActionButton>
            <ActionLink variant="ghost" to="/verein/satzung">
              {t("Pages_Command_Verein_DocumentsPublicSatzungLink")}
            </ActionLink>
            <ActionLink variant="ghost" to="/verein/beschluesse">
              {t("Pages_Command_Verein_DocumentsPublicArchiveLink")}
            </ActionLink>
            <ActionLink variant="gold" to="/command/verein/documents/create">
              {t("Pages_Command_Verein_DocumentFormCreateNew")}
            </ActionLink>
          </>
        }
      />

      {query.isError && (
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("Pages_Command_Verein_LoadError"), query.error.message)}
        </ErrorState>
      )}

      <section
        className="command-metrics verein-documents-metrics"
        aria-label={t("Pages_Command_Verein_DocumentsMetricsAria")}
      >
        {filterOrder.map((candidate) => (
          <ActionButton
            key={candidate}
            variant="ghost"
            fullWidth
            {...(filter === candidate ? { className: "active" } : {})}
            aria-pressed={filter === candidate}
            onClick={() => setFilter(candidate)}
          >
            <Metric
              label={
                candidate === "all"
                  ? t("Pages_Command_Verein_MetricDocumentsStatus")
                  : candidate === "Satzung"
                    ? t("Pages_Command_Verein_DocumentsMetricSatzungStatus")
                    : t("Pages_Command_Verein_DocumentsMetricMinutesStatus")
              }
              value={countLabel(counts[candidate])}
              detail={
                candidate === "all"
                  ? t("Pages_Command_Verein_DocumentsMetricAllDescription")
                  : candidate === "Satzung"
                    ? t("Pages_Command_Verein_DocumentsMetricSatzungDescription")
                    : t("Pages_Command_Verein_DocumentsMetricMinutesDescription")
              }
            />
          </ActionButton>
        ))}
      </section>

      <div className="toolbar verein-documents-toolbar">
        <SearchField
          label={t("Pages_Command_Verein_DocumentsSearchPlaceholder")}
          placeholder={t("Pages_Command_Verein_DocumentsSearchPlaceholder")}
          clearLabel={t("Common_SearchClear")}
          value={search}
          onChange={setSearch}
        />
        <FilterChipGroup<VereinDocumentFilter>
          label={t("Pages_Command_Verein_DocumentsToolbarAria")}
          items={filterOrder.map((candidate) => ({
            id: candidate,
            label: t(filterLabels[candidate]),
            count: counts[candidate],
          }))}
          value={filter}
          onChange={setFilter}
        />
      </div>

      {query.isPending ? (
        <LoadingState label={t("Pages_Command_Verein_Loading")} />
      ) : (
        <section className="command-panel verein-documents-list">
          <header className="command-panel__header">
            <span className="kicker">{t("Pages_Command_Verein_DocumentsKicker")}</span>
            <h2>{t(listTitles[filter])}</h2>
            <StatusBadge>{countLabel(visible.length)}</StatusBadge>
          </header>
          {visible.length === 0 ? (
            <EmptyState title={t("Pages_Command_Verein_DocumentsEmptyTitle")}>
              {t("Pages_Command_Verein_DocumentsEmptyBody")}
            </EmptyState>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table verein-documents-table">
                <thead>
                  <tr>
                    <th>{t("Pages_Command_Verein_DocumentsHeaderDocument")}</th>
                    <th>{t("Pages_Command_Verein_DocumentsHeaderKind")}</th>
                    <th>{t("Pages_Command_Verein_DocumentsHeaderPublished")}</th>
                    <th>{t("Pages_Command_Verein_DocumentsHeaderRevisions")}</th>
                    <th>{t("Pages_Command_Verein_DocumentsHeaderUpdated")}</th>
                    <th>{t("Pages_Command_Verein_DocumentsHeaderActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.map((document) => {
                    const revision = activeVereinRevision(document);
                    const published = latestPublishedVereinRevision(document);
                    const fallback = formatResource(
                      t("Pages_Command_Verein_DocumentFallbackTitle"),
                      document.primaryKey,
                    );
                    return (
                      <tr key={document.primaryKey}>
                        <th scope="row">
                          <Link to={`/command/verein/documents/${document.primaryKey}`}>
                            {documentTitle(document, fallback)}
                          </Link>
                          <span>
                            {revision?.summary || t("Pages_Command_Verein_DocumentNoSummary")}
                          </span>
                        </th>
                        <td>{kindLabel(document.kind)}</td>
                        <td>
                          <StatusBadge
                            tone={toneOf(reportTones, published ? "published" : "draft")}
                          >
                            {published
                              ? formatResource(
                                  t("Pages_Command_Verein_DocumentsPublishedVersion"),
                                  published.version,
                                )
                              : t("Pages_Command_Verein_DocumentsDraftOnly")}
                          </StatusBadge>
                        </td>
                        <td>{document.revisions.length}</td>
                        <td>{formatDateTime(document.timeStampUpdated)}</td>
                        <td>
                          <div className="table-actions">
                            <ActionLink
                              variant="ghost"
                              to={`/command/verein/documents/${document.primaryKey}`}
                            >
                              {t("Pages_Command_Verein_DocumentsOpenDetail")}
                            </ActionLink>
                            <ActionLink
                              variant="ghost"
                              to={`/command/verein/documents/${document.primaryKey}/edit`}
                            >
                              {t("Pages_Command_Verein_DocumentsOpenEdit")}
                            </ActionLink>
                          </div>
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
    </PageStack>
  );
}
