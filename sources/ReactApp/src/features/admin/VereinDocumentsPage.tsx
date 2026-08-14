import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { currentLocale, formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManageVereinDocuments } from "@/auth/claims";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
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
      <StatePanel kind="forbidden" title={t("Pages_Command_Verein_DocumentsAccessRequiredTitle")}>
        {t("Pages_Command_Verein_DocumentsAccessRequiredDescription")}
      </StatePanel>
    );

  return (
    <section className="verein-documents-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_VereinDocuments")}
        title={t("Pages_Command_Verein_DocumentsPageTitle")}
        description={t("Pages_Command_Verein_DocumentsPageDescription")}
        actions={
          <>
            <Link className="button button--ghost" to="/command/content">
              {t("Pages_Command_Content_BackToHub")}
            </Link>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Verein_ActionRefresh")}
            </button>
            <Link className="button button--ghost" to="/verein/satzung">
              {t("Pages_Command_Verein_DocumentsPublicSatzungLink")}
            </Link>
            <Link className="button button--ghost" to="/verein/beschluesse">
              {t("Pages_Command_Verein_DocumentsPublicArchiveLink")}
            </Link>
            <Link className="button button--primary" to="/command/verein/documents/create">
              {t("Pages_Command_Verein_DocumentFormCreateNew")}
            </Link>
          </>
        }
      />

      {query.isError && (
        <StatePanel kind="error">
          {formatResource(t("Pages_Command_Verein_LoadError"), query.error.message)}
        </StatePanel>
      )}

      <section
        className="command-metrics verein-documents-metrics"
        aria-label={t("Pages_Command_Verein_DocumentsMetricsAria")}
      >
        {(["all", "Satzung", "Minutes"] as const).map((candidate) => (
          <button
            key={candidate}
            type="button"
            className={filter === candidate ? "active" : undefined}
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
          </button>
        ))}
      </section>

      <section
        className="toolbar verein-documents-toolbar"
        aria-label={t("Pages_Command_Verein_DocumentsToolbarAria")}
      >
        <label className="search-field">
          <span className="sr-only">{t("Pages_Command_Verein_DocumentsSearchPlaceholder")}</span>
          <input
            value={search}
            placeholder={t("Pages_Command_Verein_DocumentsSearchPlaceholder")}
            onChange={(event) => setSearch(event.target.value)}
          />
        </label>
        <div className="filter-chips">
          {(["all", "Satzung", "Minutes"] as const).map((candidate) => (
            <button
              key={candidate}
              type="button"
              className={filter === candidate ? "active" : undefined}
              aria-pressed={filter === candidate}
              onClick={() => setFilter(candidate)}
            >
              {t(filterLabels[candidate])} <span>{counts[candidate]}</span>
            </button>
          ))}
        </div>
      </section>

      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_Verein_Loading")}</StatePanel>
      ) : (
        <section className="command-panel verein-documents-list">
          <header className="command-panel__header">
            <span className="kicker">{t("Pages_Command_Verein_DocumentsKicker")}</span>
            <h2>{t(listTitles[filter])}</h2>
            <StatusBadge>{countLabel(visible.length)}</StatusBadge>
          </header>
          {visible.length === 0 ? (
            <StatePanel>{t("Pages_Command_Verein_DocumentsNoRows")}</StatePanel>
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
                          <StatusBadge tone={published ? "success" : "warning"}>
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
                            <Link
                              className="button button--ghost"
                              to={`/command/verein/documents/${document.primaryKey}`}
                            >
                              {t("Pages_Command_Verein_DocumentsOpenDetail")}
                            </Link>
                            <Link
                              className="button button--ghost"
                              to={`/command/verein/documents/${document.primaryKey}/edit`}
                            >
                              {t("Pages_Command_Verein_DocumentsOpenEdit")}
                            </Link>
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
    </section>
  );
}
