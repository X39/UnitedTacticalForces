import {
  CalendarLtr24Regular,
  ChevronRight20Regular,
  PeopleTeam24Regular,
  ShieldTask24Regular,
} from "@fluentui/react-icons";
import type { TFunction } from "i18next";
import { useQuery } from "@tanstack/react-query";
import type { CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { eventService, fieldReportService, queryKeys } from "@/api/services";
import type { UpcomingEvent } from "@/api/types";
import { formatDate, formatUtcDate, formatUtcDateTime, plainText } from "@/app/format";
import { resolveSiteImage, useSiteImages } from "@/app/siteImages";
import { eventNeedsSignups, normalizeEventAcceptance } from "@/app/upcomingEvents";
import { Metric, StatePanel, StatusBadge } from "@/components/primitives";

function attendanceTone(event: UpcomingEvent): "success" | "warning" | "danger" {
  const acceptance = normalizeEventAcceptance(event.metaAcceptance);
  if (acceptance === "Rejected") return "danger";
  if (acceptance === "Maybe" || eventNeedsSignups(event)) return "warning";
  return "success";
}

function attendanceLabel(event: UpcomingEvent, t: TFunction): string {
  switch (normalizeEventAcceptance(event.metaAcceptance)) {
    case "Accepted":
      return t("Pages_Public_Home_StatusConfirmed");
    case "Maybe":
      return t("Pages_Public_Home_UserStatusMaybe");
    case "Rejected":
      return t("Pages_Public_Home_UserStatusDeclined");
    default:
      return eventNeedsSignups(event)
        ? t("Pages_Public_Home_StatusLowSlots")
        : t("Pages_Public_Home_StatusConfirmed");
  }
}

export function Component() {
  const { t } = useTranslation();
  const siteImages = useSiteImages();
  const events = useQuery({
    queryKey: queryKeys.upcomingEvents,
    queryFn: ({ signal }) => eventService.upcoming(signal),
  });
  const reports = useQuery({
    queryKey: queryKeys.fieldReports,
    queryFn: ({ signal }) => fieldReportService.list(signal),
  });
  const upcoming = events.data ?? [];
  const primary = upcoming[0];
  const reportCards = (reports.data ?? []).slice(0, 3);
  const primaryImage =
    resolveImageUrl(primary?.imagePath) ??
    resolveSiteImage(
      siteImages,
      "home-operation-hero",
      "/media/generated/operations-convoy-hero.jpg",
    );
  const operationCardFallback = resolveSiteImage(
    siteImages,
    "home-operation-card",
    "/media/generated/mission-overwatch-banner.jpg",
  );

  return (
    <>
      <section
        className="home-hero"
        style={{ "--hero-image": `url("${primaryImage.replaceAll('"', "%22")}")` } as CSSProperties}
      >
        <div className="home-hero__scrim" />
        <div className="home-hero__content">
          <span className="kicker">{t("Pages_Public_Home_NextOperationKicker")}</span>
          <h1>{primary?.title ?? t("Pages_Public_Home_DefaultHeroTitle")}</h1>
          <p>
            {primary
              ? plainText(primary.description, 220)
              : t("Pages_Public_Home_NoOperationsPublished")}
          </p>
          <div className="home-hero__actions">
            <Link
              className="button button--gold"
              to={primary ? `/events/${primary.primaryKey}` : "/events/upcoming"}
            >
              {primary ? t("Pages_Public_Home_ViewSlots") : t("Pages_Public_Home_ViewOperations")}
              <ChevronRight20Regular />
            </Link>
            <Link className="button button--glass" to="/events/upcoming?mode=calendar">
              {t("Pages_Public_Home_ViewCalendar")}
            </Link>
          </div>
        </div>
        {primary && (
          <aside
            className="operation-brief"
            aria-label={t("Pages_Public_Home_OperationMetadataAria")}
          >
            <div>
              <span>{t("React_UI_Schedule")}</span>
              <strong>{formatUtcDateTime(primary.scheduledFor)}</strong>
            </div>
            <div>
              <span>{t("React_UI_Terrain")}</span>
              <strong>{primary.terrainTitle || t("Pages_Public_Home_NoTerrainData")}</strong>
            </div>
            <div>
              <span>{t("React_UI_ModPack")}</span>
              <strong>
                {primary.modPackDefinitionTitle || t("Pages_Public_Home_NoModsetData")}
              </strong>
            </div>
            <div>
              <span>{t("Pages_Public_Home_SlotsLabel")}</span>
              <strong>
                {primary.minimumAccepted > 0
                  ? `${primary.acceptedCount} / ${primary.minimumAccepted}`
                  : primary.acceptedCount}
              </strong>
            </div>
            <StatusBadge tone={attendanceTone(primary)}>{attendanceLabel(primary, t)}</StatusBadge>
          </aside>
        )}
      </section>

      <section className="content-section section-grid section-grid--intro">
        <div>
          <span className="kicker">{t("Pages_Public_Home_CommunityKicker")}</span>
          <h2>{t("Pages_Public_Home_CommunityTitle")}</h2>
          <p>{t("Pages_Public_Home_OpenCommunityStructureBody")}</p>
        </div>
        <div className="value-cards">
          <article>
            <ShieldTask24Regular />
            <h3>{t("Pages_Public_Home_CoopArmaOperationsTitle")}</h3>
            <p>{t("Pages_Public_Home_CoopArmaOperationsBody")}</p>
          </article>
          <article>
            <PeopleTeam24Regular />
            <h3>{t("Pages_Public_Home_OpenCommunityStructureTitle")}</h3>
            <p>{t("Pages_Public_Home_OpenCommunityStructureBody")}</p>
          </article>
          <article>
            <CalendarLtr24Regular />
            <h3>{t("Pages_Public_Home_ReadyForNextOperationTitle")}</h3>
            <p>{t("Pages_Public_Home_ReadyForNextOperationBody")}</p>
          </article>
        </div>
      </section>

      <section
        className="content-section operations-preview"
        aria-label={t("Pages_Public_Home_UpcomingOperationsAria")}
      >
        <div className="section-heading">
          <div>
            <span className="kicker">{t("Pages_Public_Home_UpcomingOperationsKicker")}</span>
            <h2>{t("Pages_Public_Home_OperationsBoardTitle")}</h2>
          </div>
          <Link className="text-link" to="/events/upcoming">
            {t("Pages_Public_Home_ViewOperations")}
            <ChevronRight20Regular />
          </Link>
        </div>
        {events.isLoading && (
          <StatePanel kind="loading">{t("Pages_Public_Home_OperationsLoading")}</StatePanel>
        )}
        {events.isError && (
          <StatePanel kind="error">{t("Pages_Public_Home_ErrorOperationsLoadFailed")}</StatePanel>
        )}
        {!events.isLoading && !events.isError && upcoming.length === 0 && (
          <StatePanel kind="empty">{t("Pages_Public_Home_UpcomingSummaryEmpty")}</StatePanel>
        )}
        <div className="operation-card-grid">
          {upcoming.slice(0, 3).map((event) => (
            <Link
              className="operation-card"
              to={`/events/${event.primaryKey}`}
              key={event.primaryKey}
            >
              <div
                className="operation-card__image"
                style={{
                  backgroundImage: `url("${resolveImageUrl(event.imagePath) ?? operationCardFallback}")`,
                }}
              >
                <StatusBadge tone={attendanceTone(event)}>{attendanceLabel(event, t)}</StatusBadge>
              </div>
              <div className="operation-card__body">
                <span>
                  {formatUtcDate(event.scheduledFor, {
                    weekday: "short",
                    month: "short",
                    day: "numeric",
                  })}
                </span>
                <h3>{event.title}</h3>
                <p>{plainText(event.description)}</p>
                <div className="metrics-inline">
                  <Metric label={t("React_UI_Terrain")} value={event.terrainTitle} />
                  <Metric
                    label={t("Pages_Public_Home_AcceptedLabel")}
                    value={event.acceptedCount}
                  />
                </div>
              </div>
            </Link>
          ))}
        </div>
      </section>

      <section
        className="content-section field-report-preview"
        aria-label={t("Pages_Public_Home_FieldReportsAria")}
      >
        <div className="section-heading">
          <div>
            <span className="kicker">{t("React_UI_AfterAction")}</span>
            <h2>{t("Pages_Public_Home_FieldReportsTitle")}</h2>
          </div>
          <Link className="text-link" to="/field-reports">
            {t("Pages_Public_Home_ReadReports")}
            <ChevronRight20Regular />
          </Link>
        </div>
        {reports.isError && (
          <StatePanel kind="warning">
            {t("Pages_Public_Home_ErrorFieldReportsLoadFailed")}
          </StatePanel>
        )}
        {!reports.isLoading && !reports.isError && reportCards.length === 0 && (
          <StatePanel kind="empty">{t("Pages_Public_Home_NoFieldReportsPublished")}</StatePanel>
        )}
        <div className="report-card-grid">
          {reportCards.map((report, index) => (
            <Link
              className="report-card"
              to={`/field-reports/${report.slug}`}
              key={report.primaryKey}
              style={
                {
                  "--report-image": `url("${
                    resolveImageUrl(report.heroImagePath) ??
                    resolveSiteImage(
                      siteImages,
                      `home-field-report-fallback-${index + 1}`,
                      "/media/generated/field-reports-table-hero.jpg",
                    )
                  }")`,
                } as CSSProperties
              }
            >
              <div className="report-card__image" />
              <div>
                <span>{report.category}</span>
                <h3>{report.title}</h3>
                <p>{report.excerpt}</p>
                <small>{formatDate(report.timeStampPublished ?? report.timeStampUpdated)}</small>
              </div>
            </Link>
          ))}
        </div>
      </section>
    </>
  );
}
