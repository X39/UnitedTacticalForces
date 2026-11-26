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
import { ActionLink } from "@/components/ActionControl";
import { Markdown, PageHeader } from "@/components/primitives";
import { PublicHero } from "@/components/PublicHero";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";

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
        <PageHeader title={t("Pages_Public_Media_Title")} />
        <LoadingState label={t("Pages_FieldReports_Detail_Loading")} />
      </div>
    );
  if (query.isError)
    return (
      <div className="narrow-page">
        <PageHeader title={t("Pages_Public_Media_Title")} />
        <ErrorState onRetry={() => void query.refetch()}>
          {t("Pages_FieldReports_Detail_Error_LoadFailed")}
        </ErrorState>
      </div>
    );
  if (!query.data)
    return (
      <div className="narrow-page">
        <PageHeader title={t("Pages_Public_Media_Title")} />
        <EmptyState
          title={t("Pages_FieldReports_Detail_NotFound")}
          action={
            <ActionLink variant="ghost" to="/field-reports">
              {t("Pages_FieldReports_Detail_BackToReports")}
            </ActionLink>
          }
        >
          {t("Pages_FieldReports_Detail_NotFoundDescription")}
        </EmptyState>
      </div>
    );
  const report = query.data;
  return (
    <article className="report-detail">
      <PublicHero
        className="editorial-hero"
        compact
        singleColumn
        style={
          {
            "--hero-image": `url("${(resolveImageUrl(report.heroImagePath) ?? fallbackImage).replaceAll('"', "%22")}")`,
          } as CSSProperties
        }
        kicker={report.category}
        title={report.title}
        description={
          <>
            <p>{report.excerpt}</p>
            <small>{formatDate(report.timeStampPublished ?? report.timeStampUpdated)}</small>
          </>
        }
        actions={
          <ActionLink variant="glass" size="compact" to="/field-reports">
            <ArrowLeft20Regular />
            {t("Pages_FieldReports_Detail_BackToReports")}
          </ActionLink>
        }
      />
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
