import { useQuery } from "@tanstack/react-query";
import { useMemo, useState, type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { fieldReportService, queryKeys } from "@/api/services";
import { formatDate } from "@/app/format";
import { resolveSiteImage, useSiteImage, useSiteImages } from "@/app/siteImages";
import { ActionAnchor, ActionLink } from "@/components/ActionControl";
import { FilterChipGroup } from "@/components/FilterChipGroup";
import { PublicHero } from "@/components/PublicHero";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";

export function Component() {
  const { t } = useTranslation();
  const headerImage = useSiteImage("media-hero", "/media/generated/field-reports-table-hero.jpg");
  const siteImages = useSiteImages();
  const query = useQuery({
    queryKey: queryKeys.fieldReports,
    queryFn: ({ signal }) => fieldReportService.list(signal),
  });
  const [category, setCategory] = useState<string>("");
  const allLabel = t("Pages_Public_Media_AllCategory");
  const orderedReports = useMemo(
    () =>
      [...(query.data ?? [])].sort(
        (left, right) =>
          Number(right.isFeatured) - Number(left.isFeatured) ||
          left.sortOrder - right.sortOrder ||
          new Date(right.timeStampPublished ?? right.timeStampUpdated).getTime() -
            new Date(left.timeStampPublished ?? left.timeStampUpdated).getTime(),
      ),
    [query.data],
  );
  const categories = useMemo(
    () =>
      [...new Set(orderedReports.map((report) => report.category).filter(Boolean))].sort(
        (left, right) => left.localeCompare(right),
      ),
    [orderedReports],
  );
  const reports = orderedReports.filter((report) => !category || report.category === category);
  const featured = orderedReports[0];

  return (
    <div className="field-reports-page public-page">
      <PublicHero
        className="editorial-hero"
        compact
        singleColumn
        style={{ "--hero-image": `url("${headerImage.replaceAll('"', "%22")}")` } as CSSProperties}
        kicker={t("Pages_Public_Media_Kicker")}
        title={t("Pages_Public_Media_Title")}
        description={t("Pages_Public_Media_Subtitle")}
        actions={
          <>
            <ActionAnchor variant="gold" href="#reports">
              {t("Pages_Public_Media_ReadReports")}
            </ActionAnchor>
            <ActionLink variant="glass" to="/events/upcoming">
              {t("Pages_Public_Media_OperationsBoard")}
            </ActionLink>
          </>
        }
      />

      <div className="content-section public-section field-reports-content" id="reports">
        {query.isLoading && <LoadingState label={t("Pages_Public_Media_LoadingReports")} />}
        {query.isError && (
          <ErrorState onRetry={() => void query.refetch()}>
            {t("Pages_Public_Media_Error_LoadFailed")}
          </ErrorState>
        )}
        {featured && (
          <section aria-label={t("Pages_Public_Media_FeaturedReport")}>
            <Link className="featured-report" to={`/field-reports/${featured.slug}`}>
              <div
                className="featured-report__image"
                style={{
                  backgroundImage: `url("${
                    resolveImageUrl(featured.heroImagePath) ??
                    resolveSiteImage(
                      siteImages,
                      "media-field-report-card",
                      "/media/generated/field-reports-table-hero.jpg",
                    )
                  }")`,
                }}
              />
              <div>
                <span className="kicker">{t("Pages_Public_Media_FeaturedReport")}</span>
                <small>
                  {featured.category} ·{" "}
                  {formatDate(featured.timeStampPublished ?? featured.timeStampUpdated)}
                </small>
                <h2>{featured.title}</h2>
                <p>{featured.excerpt}</p>
                <strong>{t("Pages_Public_Media_ReadReport")} →</strong>
              </div>
            </Link>
          </section>
        )}
        <FilterChipGroup
          label={t("Pages_Public_Media_ReportFilters")}
          items={[
            { id: "", label: allLabel },
            ...categories.map((value) => ({ id: value, label: value })),
          ]}
          value={category}
          onChange={setCategory}
        />
        {!query.isLoading &&
          !query.isError &&
          reports.length === 0 &&
          (query.data?.length ? (
            <EmptyState title={t("Pages_Public_Media_NoMatchTitle")}>
              {t("Pages_Public_Media_NoMatchBody")}
            </EmptyState>
          ) : (
            <EmptyState title={t("Pages_Public_Media_NoReportsTitle")}>
              {t("Pages_Public_Media_NoReportsBody")}
            </EmptyState>
          ))}
        <section className="field-report-list" aria-label={t("Pages_Public_Media_ReportList")}>
          {reports.map((report, index) => (
            <Link to={`/field-reports/${report.slug}`} key={report.primaryKey}>
              <div
                className="field-report-list__image"
                style={{
                  backgroundImage: `url("${
                    resolveImageUrl(report.heroImagePath) ??
                    resolveSiteImage(
                      siteImages,
                      index === 0
                        ? "media-field-report-card"
                        : `media-field-report-fallback-${((index - 1) % 3) + 1}`,
                      "/media/generated/field-reports-table-hero.jpg",
                    )
                  }")`,
                }}
              />
              <div>
                <span>{report.category}</span>
                <small>{formatDate(report.timeStampPublished ?? report.timeStampUpdated)}</small>
                <h2>{report.title}</h2>
                <p>{report.excerpt}</p>
              </div>
            </Link>
          ))}
        </section>
      </div>
    </div>
  );
}
