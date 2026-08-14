import {
  ArrowLeft20Regular,
  ArrowRight20Regular,
  BookOpen24Regular,
  Money24Regular,
  PeopleCommunity24Regular,
} from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import type { CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { queryKeys, vereinService } from "@/api/services";
import type { VereinDocument } from "@/api/types";
import { formatCurrency, formatDate } from "@/app/format";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { membershipStatusTranslationKey } from "@/app/membership";
import { useSiteImage } from "@/app/siteImages";
import { useAuth } from "@/auth/auth";
import { canAccessVereinLedger, isVereinMember } from "@/auth/claims";
import { Markdown, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";

export function VereinPage() {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const heroImage = useSiteImage(
    "verein-hero",
    "/media/generated/verein-governance-table-hero.jpg",
  );
  const settings = useQuery({
    queryKey: queryKeys.vereinSettings,
    queryFn: ({ signal }) => vereinService.settings(signal),
  });
  const request = useQuery({
    queryKey: queryKeys.membershipRequest,
    queryFn: ({ signal }) => vereinService.myMembershipRequest(signal),
    enabled: auth.isAuthenticated && !isVereinMember(auth.user),
  });
  const contributionText = i18n.resolvedLanguage?.startsWith("de")
    ? settings.data?.membershipContributionPublicTextDe
    : settings.data?.membershipContributionPublicTextEn;
  return (
    <div className="verein-page">
      <section
        className="editorial-hero editorial-hero--verein"
        style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
      >
        <div>
          <span className="kicker">{t("Pages_Verein_Index_Kicker")}</span>
          <h1>{t("Pages_Verein_Index_Title")}</h1>
          <p>{t("Pages_Verein_Index_Subtitle")}</p>
          <div>
            <Link className="button button--gold" to="/verein/satzung">
              {t("Pages_Verein_Index_ReadSatzung")}
            </Link>
            <Link className="button button--glass" to="/verein/beschluesse">
              {t("Pages_Verein_Index_OpenArchive")}
            </Link>
          </div>
        </div>
      </section>
      <section
        className="content-section status-overview"
        aria-label={t("Pages_Verein_Index_StatusAria")}
      >
        <div>
          <span className="kicker">{t("Pages_Verein_Index_StatusKicker")}</span>
          <h2>{t("Pages_Verein_Index_ProfileStatus")}</h2>
          {!auth.isAuthenticated ? (
            <>
              <StatusBadge>{t("Pages_Verein_Index_PublicState")}</StatusBadge>
              <p>{t("Pages_Verein_Index_SignInDetail")}</p>
              <Link className="text-link" to="/join">
                {t("Pages_Verein_Index_SignInForStatus")}
                <ArrowRight20Regular />
              </Link>
            </>
          ) : isVereinMember(auth.user) ? (
            <>
              <StatusBadge tone="success">{t("Pages_Verein_Index_ActiveMember")}</StatusBadge>
              <p>{t("Pages_Verein_Index_ActiveMemberDetail")}</p>
              {auth.user?.vereinMemberNumber && (
                <strong>
                  {formatResource(
                    t("Pages_Verein_Index_ActiveMemberNumber"),
                    auth.user.vereinMemberNumber,
                  )}
                </strong>
              )}
            </>
          ) : request.isLoading ? (
            <StatePanel kind="loading">{t("Pages_Verein_Index_Loading")}</StatePanel>
          ) : request.isError ? (
            <StatePanel
              kind="error"
              action={
                <button
                  className="button button--ghost"
                  type="button"
                  onClick={() => void request.refetch()}
                >
                  {t("React_UI_Retry")}
                </button>
              }
            >
              {t("React_Join_LoadError")}
            </StatePanel>
          ) : (
            <>
              <StatusBadge tone="info">
                {request.data
                  ? t(membershipStatusTranslationKey(request.data.status))
                  : t("Pages_Verein_Index_NoRequest")}
              </StatusBadge>
              <p>
                {request.data
                  ? formatResource(
                      t("Pages_Verein_Index_RequestUpdated"),
                      formatDate(request.data.timeStampUpdated),
                    )
                  : t("Pages_Verein_Index_NoRequestDetail")}
              </p>
              <Link className="text-link" to="/join">
                {t("Pages_Verein_Index_MembershipRequest")}
                <ArrowRight20Regular />
              </Link>
            </>
          )}
        </div>
        <div className="contribution-card">
          <Money24Regular />
          <span>{t("React_Verein_Contribution")}</span>
          <strong>
            {formatCurrency(
              settings.data?.membershipContributionMonthlyAmount,
              settings.data?.membershipContributionCurrency,
            )}{" "}
            {t("React_Verein_PerMonth")}
          </strong>
          <small>
            {formatCurrency(
              settings.data?.membershipContributionAnnualAmount,
              settings.data?.membershipContributionCurrency,
            )}{" "}
            {t("React_Verein_PerYear")}
          </small>
          {contributionText && <p>{contributionText}</p>}
        </div>
      </section>
      {settings.isError && (
        <div className="content-section">
          <StatePanel kind="warning">{t("React_Verein_LoadError")}</StatePanel>
        </div>
      )}
      <section className="content-section">
        <PageHeader
          kicker={t("Pages_Verein_Index_WorkspaceKicker")}
          title={t("Pages_Verein_Index_WorkspaceTitle")}
          description={t("Pages_Verein_Index_WorkspaceNote")}
        />
        <div className="resource-links resource-links--three">
          <Link to="/verein/satzung">
            <BookOpen24Regular />
            <h3>{t("Pages_Verein_Index_SatzungTitle")}</h3>
            <p>{t("Pages_Verein_Index_SatzungBody")}</p>
            <ArrowRight20Regular />
          </Link>
          <Link to="/verein/beschluesse">
            <PeopleCommunity24Regular />
            <h3>{t("Pages_Verein_Index_MinutesTitle")}</h3>
            <p>{t("Pages_Verein_Index_MinutesBody")}</p>
            <ArrowRight20Regular />
          </Link>
          <Link to="/verein/kassenbuch">
            <Money24Regular />
            <h3>{t("Pages_Verein_Index_KassenbuchTitle")}</h3>
            <p>{t("Pages_Verein_Index_KassenbuchBody")}</p>
            <StatusBadge tone={canAccessVereinLedger(auth.user) ? "success" : "neutral"}>
              {canAccessVereinLedger(auth.user)
                ? t("Pages_Verein_Index_LedgerAccessible")
                : t("Pages_Verein_Index_LedgerRestricted")}
            </StatusBadge>
          </Link>
        </div>
      </section>
    </div>
  );
}

function DocumentView({
  document,
  backTo,
  heroImage,
}: {
  document: VereinDocument;
  backTo: string;
  heroImage: string;
}) {
  const { t } = useTranslation();
  const revision = document.revision;
  if (!revision)
    return (
      <StatePanel kind="empty">{t("Pages_Unit_Satzung_Index_NotFoundDescription")}</StatePanel>
    );
  return (
    <article className="governance-document">
      <Link className="button button--ghost button--small" to={backTo}>
        <ArrowLeft20Regular />
        {backTo.endsWith("beschluesse")
          ? t("Pages_Unit_Beschluesse_DocumentId_BackToArchive")
          : t("Pages_Unit_Satzung_Index_BackToUnit")}
      </Link>
      <header
        className="governance-document__hero"
        style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
      >
        <span className="kicker">{t("React_Verein_PublishedRecord")}</span>
        <h1>{revision.title}</h1>
        <p>{revision.summary}</p>
        <div>
          <StatusBadge tone="success">
            {t("React_Verein_Version")} {revision.version}
          </StatusBadge>
          <span>{formatDate(revision.timeStampPublished ?? document.timeStampUpdated)}</span>
        </div>
      </header>
      <Markdown>{revision.markdown}</Markdown>
    </article>
  );
}

export function StatutesPage() {
  const { t } = useTranslation();
  const heroImage = useSiteImage(
    "satzung-hero",
    "/media/generated/verein-governance-table-hero.jpg",
  );
  const query = useQuery({
    queryKey: queryKeys.vereinStatutes,
    queryFn: ({ signal }) => vereinService.statutes(signal),
    retry: false,
  });
  if (query.isLoading)
    return (
      <div className="narrow-page">
        <StatePanel kind="loading">{t("Pages_Unit_Satzung_Index_Loading")}</StatePanel>
      </div>
    );
  if (query.isError || !query.data)
    return (
      <div className="narrow-page">
        <StatePanel
          kind={query.isError ? "error" : "empty"}
          title={t("Pages_Unit_Satzung_Index_NotFoundTitle")}
        >
          {query.isError
            ? t("Pages_Unit_Satzung_Index_Error_LoadFailed")
            : t("Pages_Unit_Satzung_Index_NotFoundDescription")}
        </StatePanel>
      </div>
    );
  return (
    <div className="page-wrap">
      <DocumentView document={query.data} backTo="/verein" heroImage={heroImage} />
    </div>
  );
}

export function MinutesPage() {
  const { t } = useTranslation();
  const heroImage = useSiteImage(
    "beschluesse-hero",
    "/media/generated/verein-governance-table-hero.jpg",
  );
  const query = useQuery({
    queryKey: queryKeys.vereinMinutes,
    queryFn: ({ signal }) => vereinService.minutes(signal),
  });
  return (
    <div
      className="page-wrap governance-list-page"
      style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
    >
      <PageHeader
        kicker={t("Pages_Unit_Beschluesse_Index_Kicker")}
        title={t("Pages_Unit_Beschluesse_Index_Title")}
        description={t("Pages_Unit_Beschluesse_Index_Subtitle")}
        actions={
          <Link className="button button--ghost" to="/verein">
            {t("Pages_Unit_Beschluesse_Index_BackToUnit")}
          </Link>
        }
      />
      {query.isLoading && (
        <StatePanel kind="loading">{t("Pages_Unit_Beschluesse_Index_Loading")}</StatePanel>
      )}
      {query.isError && (
        <StatePanel kind="error">{t("Pages_Unit_Beschluesse_Index_Error_LoadFailed")}</StatePanel>
      )}
      {!query.isLoading && !query.isError && !query.data?.length && (
        <StatePanel kind="empty" title={t("Pages_Unit_Beschluesse_Index_NoRecordsTitle")}>
          {t("Pages_Unit_Beschluesse_Index_NoRecordsDescription")}
        </StatePanel>
      )}
      <div className="document-list">
        {query.data?.map(
          (document) =>
            document.revision && (
              <Link key={document.primaryKey} to={`/verein/beschluesse/${document.primaryKey}`}>
                <span>
                  {formatDate(document.revision.timeStampPublished ?? document.timeStampUpdated)}
                </span>
                <h2>{document.revision.title}</h2>
                <p>{document.revision.summary}</p>
                <small>
                  {formatResource(
                    t("Pages_Unit_Beschluesse_Index_VersionPrefix"),
                    document.revision.version,
                  )}
                </small>
                <strong>{t("Pages_Unit_Beschluesse_Index_ReadDocument")} →</strong>
              </Link>
            ),
        )}
      </div>
    </div>
  );
}

export function MinuteDetailPage() {
  const { t } = useTranslation();
  const heroImage = useSiteImage(
    "beschluesse-hero",
    "/media/generated/verein-governance-table-hero.jpg",
  );
  const { documentId = "" } = useParams();
  const idIsValid = /^-?\d+$/.test(documentId);
  const query = useQuery({
    queryKey: queryKeys.vereinMinute(documentId),
    queryFn: ({ signal }) => vereinService.minute(documentId, signal),
    enabled: idIsValid,
    retry: false,
  });
  useDocumentTitle(
    query.data?.revision?.title ?? t("Pages_Unit_Beschluesse_DocumentId_PageTitle_Fallback"),
  );
  if (query.isLoading)
    return (
      <div className="narrow-page">
        <StatePanel kind="loading">{t("Pages_Unit_Beschluesse_DocumentId_Loading")}</StatePanel>
      </div>
    );
  if (query.isError)
    return (
      <div className="narrow-page">
        <StatePanel kind="error" title={t("Pages_Unit_Beschluesse_DocumentId_Error_LoadFailed")}>
          {t("Pages_Unit_Beschluesse_DocumentId_Error_LoadFailed")}
        </StatePanel>
      </div>
    );
  if (!idIsValid || !query.data)
    return (
      <div className="narrow-page">
        <StatePanel kind="empty" title={t("Pages_Unit_Beschluesse_DocumentId_NotFoundTitle")}>
          {t("Pages_Unit_Beschluesse_DocumentId_NotFoundDescription")}
        </StatePanel>
      </div>
    );
  return (
    <div className="page-wrap">
      <DocumentView document={query.data} backTo="/verein/beschluesse" heroImage={heroImage} />
    </div>
  );
}
