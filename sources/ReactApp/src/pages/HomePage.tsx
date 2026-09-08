import {
  CalendarLtr24Regular,
  ChevronRight20Regular,
  PeopleTeam24Regular,
  ShieldTask24Regular,
} from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { useEffect, useState, type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { eventService, fieldReportService, queryKeys } from "@/api/services";
import type { UpcomingEvent } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import { formatDate, formatUtcDateTime, plainText } from "@/app/format";
import { resolveSiteImage, useSiteImages } from "@/app/siteImages";
import { eventNeedsSignups, normalizeEventAcceptance } from "@/app/upcomingEvents";
import { ActionLink } from "@/components/ActionControl";
import { PublicHero } from "@/components/PublicHero";
import { Metric, StatePanel, StatusBadge } from "@/components/primitives";

function operationTone(event: UpcomingEvent): "success" | "warning" {
  return eventNeedsSignups(event) ? "warning" : "success";
}

function operationStatusLabel(event: UpcomingEvent, t: TFunction): string {
  return eventNeedsSignups(event)
    ? t("Pages_Public_Home_StatusLowSlots")
    : t("Pages_Public_Home_StatusConfirmed");
}

function attendanceTone(event: UpcomingEvent): "neutral" | "success" | "warning" | "danger" {
  switch (normalizeEventAcceptance(event.metaAcceptance)) {
    case "Accepted":
      return "success";
    case "Maybe":
      return "warning";
    case "Rejected":
      return "danger";
    default:
      return "neutral";
  }
}

function attendanceLabel(event: UpcomingEvent, t: TFunction): string {
  switch (normalizeEventAcceptance(event.metaAcceptance)) {
    case "Accepted":
      return t("Pages_Public_Home_UserStatusConfirmed");
    case "Maybe":
      return t("Pages_Public_Home_UserStatusMaybe");
    case "Rejected":
      return t("Pages_Public_Home_UserStatusDeclined");
    default:
      return t("Pages_Public_Home_UserStatusUnassigned");
  }
}

function timeRemaining(scheduledFor: string, now: number, t: TFunction): string {
  const scheduled = new Date(scheduledFor).getTime();
  if (Number.isNaN(scheduled)) return t("Pages_Public_Home_TimeRemainingUnknown");
  const remaining = scheduled - now;
  if (remaining <= 0) return t("Pages_Public_Home_TimeRemainingLiveOrArchived");
  const minutes = Math.floor(remaining / 60_000);
  const days = Math.floor(minutes / 1_440);
  const hours = Math.floor((minutes % 1_440) / 60);
  return `${days}d ${hours}h ${minutes % 60}m`;
}

function slotProgress(event: UpcomingEvent): number {
  if (event.minimumAccepted <= 0) return event.acceptedCount > 0 ? 100 : 0;
  return Math.min(100, Math.round((event.acceptedCount / event.minimumAccepted) * 100));
}

export function Component() {
  const { t } = useTranslation();
  const configuration = useOutletContext<PublicConfiguration>();
  const siteImages = useSiteImages();
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 60_000);
    return () => window.clearInterval(timer);
  }, []);
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
  const hasCommunityChannels = Boolean(configuration.Urls.Discord || configuration.Urls.TeamSpeak3);

  return (
    <div className="home-page public-page">
      <PublicHero
        className="public-hero--home home-hero"
        innerClassName="home-hero__inner"
        copyClassName="home-hero__content"
        actionsClassName="home-hero__actions"
        style={{ "--hero-image": `url("${primaryImage.replaceAll('"', "%22")}")` } as CSSProperties}
        kicker={t("Pages_Public_Home_NextOperationKicker")}
        title={primary?.title ?? t("Pages_Public_Home_DefaultHeroTitle")}
        description={
          primary
            ? plainText(primary.description, 220)
            : t("Pages_Public_Home_NoOperationsPublished")
        }
        actions={
          <>
            <ActionLink
              variant="gold"
              to={primary ? `/events/${primary.primaryKey}` : "/events/upcoming"}
            >
              {primary ? t("Pages_Public_Home_ViewSlots") : t("Pages_Public_Home_ViewOperations")}
              <ChevronRight20Regular />
            </ActionLink>
            <ActionLink variant="glass" to="/events/upcoming?mode=calendar">
              {t("Pages_Public_Home_ViewCalendar")}
            </ActionLink>
          </>
        }
        asideClassName="operation-brief"
        asideLabel={t("Pages_Public_Home_OperationStatusPanelAria")}
        aside={
          <>
            {primary ? (
              <>
                <dl className="operation-brief__facts">
                  <div>
                    <dt>{t("React_UI_Schedule")}</dt>
                    <dd>{formatUtcDateTime(primary.scheduledFor)}</dd>
                  </div>
                  <div>
                    <dt>{t("React_UI_Terrain")}</dt>
                    <dd>{primary.terrainTitle || t("Pages_Public_Home_NoTerrainData")}</dd>
                  </div>
                  <div>
                    <dt>{t("React_UI_ModPack")}</dt>
                    <dd>{primary.modPackDefinitionTitle || t("Pages_Public_Home_NoModsetData")}</dd>
                  </div>
                  <div>
                    <dt>{t("React_UI_Host")}</dt>
                    <dd>{primary.hostedBy?.nickname || t("Pages_Public_Home_NoHostData")}</dd>
                  </div>
                  <div>
                    <dt>{t("Pages_Public_Home_SlotsLabel")}</dt>
                    <dd>
                      {primary.minimumAccepted > 0
                        ? `${primary.acceptedCount} / ${primary.minimumAccepted}`
                        : primary.acceptedCount}
                    </dd>
                  </div>
                </dl>
                <div className="operation-brief__status">
                  <div>
                    <span>{t("Pages_Public_Home_OperationStatusLabel")}</span>
                    <StatusBadge tone={operationTone(primary)}>
                      {operationStatusLabel(primary, t)}
                    </StatusBadge>
                  </div>
                  <div>
                    <span>{t("Pages_Public_Home_YourStatusLabel")}</span>
                    <StatusBadge tone={attendanceTone(primary)}>
                      {attendanceLabel(primary, t)}
                    </StatusBadge>
                  </div>
                  <div>
                    <span>{t("Pages_Public_Home_TimeRemainingLabel")}</span>
                    <strong>{timeRemaining(primary.scheduledFor, now, t)}</strong>
                  </div>
                </div>
              </>
            ) : (
              <StatePanel kind={events.isError ? "warning" : "empty"}>
                {events.isError
                  ? t("Pages_Public_Home_UpcomingSummaryError")
                  : t("Pages_Public_Home_UpcomingSummaryEmpty")}
              </StatePanel>
            )}
            {hasCommunityChannels && (
              <nav
                className="operation-brief__comms"
                aria-label={t("Pages_Public_Home_CommunityChannelsLabel")}
              >
                {configuration.Urls.Discord && (
                  <a href={configuration.Urls.Discord} target="_blank" rel="noreferrer">
                    <span>{t("Pages_Public_Home_DiscordLabel")}</span>
                    <strong>{t("Pages_Public_Home_ChannelJoin")}</strong>
                  </a>
                )}
                {configuration.Urls.TeamSpeak3 && (
                  <a href={configuration.Urls.TeamSpeak3} target="_blank" rel="noreferrer">
                    <span>{t("Pages_Public_Home_TeamSpeakLabel")}</span>
                    <strong>{t("Pages_Public_Home_ChannelConnect")}</strong>
                  </a>
                )}
              </nav>
            )}
          </>
        }
      />

      <section
        className="content-section public-section section-grid section-grid--intro"
        aria-label={t("Pages_Public_Home_CommunityPreviewAria")}
      >
        <div>
          <span className="kicker">{t("Pages_Public_Home_CommunityKicker")}</span>
          <h2>{t("Pages_Public_Home_CommunityTitle")}</h2>
          <p>{t("Pages_Public_Home_OpenCommunityStructureBody")}</p>
          <Link className="text-link" to="/unit">
            {t("Pages_Public_Home_ReadAboutUnit")}
            <ChevronRight20Regular />
          </Link>
        </div>
        <div className="value-cards">
          <article>
            <ShieldTask24Regular />
            <h3>{t("Pages_Public_Home_CoopArmaOperationsTitle")}</h3>
            <p>{t("Pages_Public_Home_CoopArmaOperationsBody")}</p>
            <Link className="text-link" to="/unit">
              {t("Pages_Public_Home_UnitOverview")}
              <ChevronRight20Regular />
            </Link>
          </article>
          <article>
            <PeopleTeam24Regular />
            <h3>{t("Pages_Public_Home_VereinHubTitle")}</h3>
            <p>{t("Pages_Public_Home_VereinHubBody")}</p>
            <Link className="text-link" to="/verein">
              {t("Pages_Public_Home_VereinHub")}
              <ChevronRight20Regular />
            </Link>
          </article>
          <article>
            <CalendarLtr24Regular />
            <h3>{t("Pages_Public_Home_ReadyForNextOperationTitle")}</h3>
            <p>{t("Pages_Public_Home_ReadyForNextOperationBody")}</p>
            <Link className="text-link" to="/join">
              {t("Pages_Public_Home_JoinUtf")}
              <ChevronRight20Regular />
            </Link>
          </article>
        </div>
      </section>

      <section
        className="content-section public-section operations-preview"
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
                <div className="operation-card__badges">
                  <StatusBadge tone={operationTone(event)}>
                    {operationStatusLabel(event, t)}
                  </StatusBadge>
                  <span className="operation-card__slot-count">
                    {event.minimumAccepted > 0 ? event.minimumAccepted : event.acceptedCount}{" "}
                    {t("Pages_Public_Home_SlotsLabel")}
                  </span>
                </div>
              </div>
              <div className="operation-card__body">
                <span>{formatUtcDateTime(event.scheduledFor)}</span>
                <h3>{event.title}</h3>
                <p>{plainText(event.description)}</p>
                <div className="operation-card__metadata">
                  <Metric
                    label={t("React_UI_Terrain")}
                    value={event.terrainTitle || t("Pages_Public_Home_NoTerrainData")}
                  />
                  <Metric
                    label={t("React_UI_ModPack")}
                    value={event.modPackDefinitionTitle || t("Pages_Public_Home_NoModsetData")}
                  />
                  <Metric
                    label={t("React_UI_Host")}
                    value={event.hostedBy?.nickname || t("Pages_Public_Home_NoHostData")}
                  />
                </div>
                <div className="operation-card__progress">
                  <span>{t("Pages_Public_Home_SlotProgressLabel")}</span>
                  <div aria-hidden="true">
                    <span style={{ width: `${slotProgress(event)}%` }} />
                  </div>
                  <strong>
                    {event.minimumAccepted > 0
                      ? `${event.acceptedCount} / ${event.minimumAccepted}`
                      : event.acceptedCount}
                  </strong>
                  <StatusBadge tone={attendanceTone(event)}>
                    {attendanceLabel(event, t)}
                  </StatusBadge>
                </div>
              </div>
            </Link>
          ))}
        </div>
      </section>

      <section
        className="content-section public-section field-report-preview"
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
        {reports.isLoading && <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>}
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
    </div>
  );
}
