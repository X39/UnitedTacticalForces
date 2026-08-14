import { ArrowLeft20Regular } from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import type { CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { fieldReportService, queryKeys } from "@/api/services";
import { formatDate } from "@/app/format";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { useSiteImage } from "@/app/siteImages";
import { Markdown, StatePanel } from "@/components/primitives";

export function Component() {
  const { t } = useTranslation();
  const fallbackImage = useSiteImage("media-hero", "/media/generated/field-reports-table-hero.jpg");
  const { slug = "" } = useParams();
  const query = useQuery({
    queryKey: queryKeys.fieldReport(slug),
    queryFn: ({ signal }) => fieldReportService.detail(slug, signal),
    enabled: Boolean(slug),
    retry: false,
  });
  useDocumentTitle(
    query.data?.title
      ? formatResource(t("Pages_FieldReports_Detail_PageTitle"), query.data.title)
      : t("Pages_FieldReports_Detail_PageTitle_NotFound"),
  );
  if (query.isLoading)
    return (
      <div className="narrow-page">
        <StatePanel kind="loading">{t("Pages_FieldReports_Detail_Loading")}</StatePanel>
      </div>
    );
  if (query.isError)
    return (
      <div className="narrow-page">
        <StatePanel
          kind="error"
          title={t("Pages_FieldReports_Detail_Error_LoadFailed")}
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void query.refetch()}
            >
              {t("React_UI_Retry")}
            </button>
          }
        >
          {t("Pages_FieldReports_Detail_Error_LoadFailed")}
        </StatePanel>
      </div>
    );
  if (!query.data)
    return (
      <div className="narrow-page">
        <StatePanel
          kind="empty"
          title={t("Pages_FieldReports_Detail_NotFound")}
          action={
            <Link className="button button--ghost" to="/field-reports">
              {t("Pages_FieldReports_Detail_BackToReports")}
            </Link>
          }
        >
          {t("Pages_FieldReports_Detail_NotFoundDescription")}
        </StatePanel>
      </div>
    );
  const report = query.data;
  return (
    <article className="report-detail">
      <header
        style={
          {
            "--report-image": `url("${resolveImageUrl(report.heroImagePath) ?? fallbackImage}")`,
          } as CSSProperties
        }
      >
        <div>
          <Link className="button button--glass button--small" to="/field-reports">
            <ArrowLeft20Regular />
            {t("Pages_FieldReports_Detail_BackToReports")}
          </Link>
          <span className="kicker">{report.category}</span>
          <h1>{report.title}</h1>
          <p>{report.excerpt}</p>
          <small>{formatDate(report.timeStampPublished ?? report.timeStampUpdated)}</small>
        </div>
      </header>
      <div className="report-detail__layout">
        <aside>
          <Link to="/field-reports">{t("Pages_FieldReports_Detail_AllReports")}</Link>
          <dl>
            <dt>{t("Pages_FieldReports_Detail_Category")}</dt>
            <dd>{report.category}</dd>
            <dt>{t("Pages_FieldReports_Detail_Published")}</dt>
            <dd>{formatDate(report.timeStampPublished ?? report.timeStampUpdated)}</dd>
            <dt>{t("Pages_FieldReports_Detail_ReportId")}</dt>
            <dd>{report.slug}</dd>
          </dl>
        </aside>
        <section>
          <Markdown>{report.markdown}</Markdown>
        </section>
      </div>
    </article>
  );
}
