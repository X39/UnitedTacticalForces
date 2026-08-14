import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManagePublicPages } from "@/auth/claims";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  latestPublishedRevision,
  loadAdminPublicPages,
  type PublicPage,
} from "@/features/admin/publicPages";

function countLabel(count: number, translate: (key: TranslationKey) => string): string {
  return formatResource(
    translate(
      count === 1
        ? "Pages_Command_Content_Pages_Count_Singular"
        : "Pages_Command_Content_Pages_Count_Plural",
    ),
    count,
  );
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const canManagePages = canManagePublicPages(user);
  const query = useQuery({
    queryKey: ["command-center", "public-pages"],
    queryFn: ({ signal }) => loadAdminPublicPages(signal),
    enabled: canManagePages,
  });
  const pages = useMemo(
    () => [...(query.data ?? [])].sort((left, right) => left.slug.localeCompare(right.slug)),
    [query.data],
  );
  const publishedCount = pages.filter((page) => latestPublishedRevision(page)).length;
  const title = (page: PublicPage) =>
    page.revision?.title ||
    formatResource(t("Pages_Command_Content_Pages_FallbackTitle"), page.slug);
  const summary = (page: PublicPage) =>
    page.revision?.summary || t("Pages_Command_Content_Pages_NoSummary");

  if (!canManagePages)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Content_Pages_AccessRequiredTitle")}>
        {t("Pages_Command_Content_Pages_AccessRequiredDescription")}
      </StatePanel>
    );

  return (
    <section className="command-public-pages">
      <PageHeader
        title={t("Pages_Command_Content_Pages_PageTitle")}
        description={t("Pages_Command_Content_Pages_PageDescription")}
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
              {t("Common_Refresh")}
            </button>
            <Link className="button button--ghost" to="/gdpr">
              {t("Pages_Command_Content_Pages_PublicGdprLink")}
            </Link>
            <Link className="button button--primary" to="/command/content/pages/gdpr/edit">
              {t("Pages_Command_Content_Pages_EditGdpr")}
            </Link>
          </>
        }
      />

      {query.isError && (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" onClick={() => void query.refetch()}>
              {t("Common_Refresh")}
            </button>
          }
        >
          {formatResource(t("Pages_Command_Content_Pages_LoadError"), query.error.message)}
        </StatePanel>
      )}

      <section
        className="command-metrics"
        aria-label={t("Pages_Command_Content_Pages_MetricsAria")}
      >
        <Metric
          label={t("Pages_Command_Content_Pages_TotalMetricStatus")}
          value={countLabel(pages.length, (key) => t(key))}
          detail={t("Pages_Command_Content_Pages_TotalMetricDescription")}
        />
        <Metric
          label={t("Pages_Command_Content_Pages_PublishedMetricStatus")}
          value={countLabel(publishedCount, (key) => t(key))}
          detail={t("Pages_Command_Content_Pages_PublishedMetricDescription")}
        />
      </section>

      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_Content_Pages_Loading")}</StatePanel>
      ) : (
        <section className="command-panel command-public-pages__list">
          <header className="command-panel__header">
            <span className="kicker">{t("Pages_Command_Content_Pages_ListKicker")}</span>
            <h2>{t("Pages_Command_Content_Pages_ListTitle")}</h2>
            <StatusBadge>{countLabel(pages.length, (key) => t(key))}</StatusBadge>
          </header>
          {pages.length === 0 ? (
            <StatePanel>{t("Pages_Command_Content_Pages_NoRows")}</StatePanel>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table">
                <thead>
                  <tr>
                    <th>{t("Pages_Command_Content_Pages_HeaderPage")}</th>
                    <th>{t("Pages_Command_Content_Pages_HeaderPublished")}</th>
                    <th>{t("Pages_Command_Content_Pages_HeaderRevisions")}</th>
                    <th>{t("Pages_Command_Content_Pages_HeaderUpdated")}</th>
                    <th>{t("Pages_Command_Content_Pages_HeaderActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {pages.map((page) => {
                    const published = latestPublishedRevision(page);
                    const detailPath = `/command/content/pages/${encodeURIComponent(page.slug)}`;
                    return (
                      <tr
                        key={page.primaryKey || page.slug}
                        tabIndex={0}
                        aria-label={`${t("Pages_Command_Content_Pages_OpenDetail")}: ${title(page)}`}
                        onClick={() => navigate(detailPath)}
                        onKeyDown={(event) => {
                          if (event.key !== "Enter" && event.key !== " ") return;
                          event.preventDefault();
                          navigate(detailPath);
                        }}
                      >
                        <th scope="row">
                          <Link to={detailPath} onClick={(event) => event.stopPropagation()}>
                            {title(page)}
                          </Link>
                          <span>{summary(page)}</span>
                        </th>
                        <td>
                          <StatusBadge tone={published ? "success" : "warning"}>
                            {published
                              ? formatResource(
                                  t("Pages_Command_Content_Pages_PublishedVersion"),
                                  published.version,
                                )
                              : t("Pages_Command_Content_Pages_DraftOnly")}
                          </StatusBadge>
                        </td>
                        <td>{page.revisions.length}</td>
                        <td>{formatDateTime(page.timeStampUpdated)}</td>
                        <td>
                          <div
                            className="table-actions"
                            onClick={(event) => event.stopPropagation()}
                          >
                            <Link className="button button--ghost button--small" to={detailPath}>
                              {t("Pages_Command_Content_Pages_OpenDetail")}
                            </Link>
                            <Link
                              className="button button--ghost button--small"
                              to={`${detailPath}/edit`}
                            >
                              {t("Pages_Command_Content_Pages_OpenEdit")}
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
