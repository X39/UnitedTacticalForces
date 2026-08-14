import { useQuery } from "@tanstack/react-query";
import { useMemo, useState, type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { fieldReportService, queryKeys } from "@/api/services";
import { formatDate } from "@/app/format";
import { resolveSiteImage, useSiteImage, useSiteImages } from "@/app/siteImages";
import { StatePanel } from "@/components/primitives";

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
      <section
        className="public-hero public-hero--compact editorial-hero"
        style={{ "--hero-image": `url("${headerImage.replaceAll('"', "%22")}")` } as CSSProperties}
      >
        <div className="public-hero__inner public-hero__inner--single">
          <div className="public-hero__copy">
            <span className="kicker">{t("Pages_Public_Media_Kicker")}</span>
            <h1>{t("Pages_Public_Media_Title")}</h1>
            <p>{t("Pages_Public_Media_Subtitle")}</p>
            <div className="public-hero__actions">
              <a className="button button--gold" href="#reports">
                {t("Pages_Public_Media_ReadReports")}
              </a>
              <Link className="button button--glass" to="/events/upcoming">
                {t("Pages_Public_Media_OperationsBoard")}
              </Link>
            </div>
          </div>
        </div>
      </section>

      <div className="content-section public-section field-reports-content" id="reports">
        {query.isLoading && (
          <StatePanel kind="loading">{t("Pages_Public_Media_LoadingReports")}</StatePanel>
        )}
        {query.isError && (
          <StatePanel
            kind="error"
            action={
              <button
                type="button"
                className="button button--ghost"
                onClick={() => void query.refetch()}
              >
                {t("React_UI_Retry")}
              </button>
            }
          >
            {t("Pages_Public_Media_Error_LoadFailed")}
          </StatePanel>
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
        <div
          className="filter-chips field-report-filters"
          role="group"
          aria-label={t("Pages_Public_Media_ReportFilters")}
        >
          {["", ...categories].map((value) => (
            <button
              type="button"
              className={category === value ? "active" : ""}
              aria-pressed={category === value}
              onClick={() => setCategory(value)}
              key={value || "all"}
            >
              {value || allLabel}
            </button>
          ))}
        </div>
        {!query.isLoading && !query.isError && reports.length === 0 && (
          <StatePanel kind="empty">
            {query.data?.length
              ? t("Pages_Public_Media_NoReportsMatchCategory")
              : t("Pages_Public_Media_NoReportsPublished")}
          </StatePanel>
        )}
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
