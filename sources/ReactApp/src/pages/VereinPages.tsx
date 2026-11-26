import {
  ArrowLeft20Regular,
  ArrowRight20Regular,
  BookOpen24Regular,
  Money24Regular,
  PeopleCommunity24Regular,
} from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import type { CSSProperties, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { queryKeys, vereinService } from "@/api/services";
import type { VereinDocument } from "@/api/types";
import { formatCurrency, formatDate } from "@/app/format";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { membershipStatusTranslationKey, normalizeMembershipStatus } from "@/app/membership";
import { useSiteImage } from "@/app/siteImages";
import { useAuth } from "@/auth/auth";
import { canAccessVereinLedger, isVereinMember } from "@/auth/claims";
import { ActionLink } from "@/components/ActionControl";
import { PageStack } from "@/components/PageStack";
import { PublicHero } from "@/components/PublicHero";
import { Markdown, PageHeader, StatusBadge } from "@/components/primitives";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";
import { membershipRequestTones, toneOf } from "@/components/statusTones";
import "./publicEvents.css";

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
  const statutes = useQuery({
    queryKey: queryKeys.vereinStatutes,
    queryFn: ({ signal }) => vereinService.statutes(signal),
    retry: false,
  });
  const minutes = useQuery({
    queryKey: queryKeys.vereinMinutes,
    queryFn: ({ signal }) => vereinService.minutes(signal),
  });
  const request = useQuery({
    queryKey: queryKeys.membershipRequest,
    queryFn: ({ signal }) => vereinService.myMembershipRequest(signal),
    enabled: auth.isAuthenticated && !isVereinMember(auth.user),
  });
  const contributionText = i18n.resolvedLanguage?.startsWith("de")
    ? settings.data?.membershipContributionPublicTextDe
    : settings.data?.membershipContributionPublicTextEn;
  const canReadLedger = canAccessVereinLedger(auth.user);
  const membershipStatus = !auth.isAuthenticated
    ? t("Pages_Verein_Index_SignInForStatus")
    : isVereinMember(auth.user)
      ? auth.user?.vereinMemberNumber
        ? formatResource(t("Pages_Verein_Index_ActiveMemberNumber"), auth.user.vereinMemberNumber)
        : t("Pages_Verein_Index_ActiveMember")
      : request.data
        ? t(membershipStatusTranslationKey(request.data.status))
        : t("Pages_Verein_Index_NoRequest");
  const membershipDetail = isVereinMember(auth.user)
    ? t("Pages_Verein_Index_MembershipActiveBody")
    : request.data
      ? formatResource(
          t("Pages_Verein_Index_MembershipRequestBody"),
          formatDate(request.data.timeStampUpdated),
        )
      : t("Pages_Verein_Index_MembershipOpenBody");
  return (
    <div className="verein-page public-page">
      <PublicHero
        className="editorial-hero"
        style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
        kicker={t("Pages_Verein_Index_Kicker")}
        title={t("Pages_Verein_Index_Title")}
        description={t("Pages_Verein_Index_Subtitle")}
        actions={
          <>
            <ActionLink variant="gold" to="/join">
              {t("Pages_Verein_Index_MembershipRequest")}
            </ActionLink>
            <ActionLink variant="glass" to="/verein/satzung">
              {t("Pages_Verein_Index_ReadSatzung")}
            </ActionLink>
          </>
        }
        asideClassName="verein-status-panel"
        asideLabel={t("Pages_Verein_Index_StatusAria")}
        aside={
          <>
            <div className="public-hero__panel-heading">
              <BookOpen24Regular />
              <div>
                <span>{t("Pages_Verein_Index_StatusKicker")}</span>
                <strong>{membershipStatus}</strong>
              </div>
            </div>
            <dl className="public-hero__status-list">
              <div>
                <dt>{t("Pages_Verein_Index_ProfileStatus")}</dt>
                <dd>
                  {!auth.isAuthenticated
                    ? t("Pages_Verein_Index_SignInDetail")
                    : isVereinMember(auth.user)
                      ? t("Pages_Verein_Index_ActiveMemberDetail")
                      : request.data
                        ? formatResource(
                            t("Pages_Verein_Index_RequestUpdated"),
                            formatDate(request.data.timeStampUpdated),
                          )
                        : t("Pages_Verein_Index_NoRequestDetail")}
                </dd>
              </div>
              <div>
                <dt>{t("Pages_Verein_Index_DocumentsStatus")}</dt>
                <dd>
                  {formatResource(
                    t("Pages_Verein_Index_DocumentsStatusValue"),
                    statutes.data ? 1 : 0,
                    minutes.data?.length ?? 0,
                  )}
                </dd>
              </div>
              <div>
                <dt>{t("Pages_Verein_Index_LedgerStatus")}</dt>
                <dd>
                  {canReadLedger
                    ? t("Pages_Verein_Index_LedgerAccessible")
                    : t("Pages_Verein_Index_LedgerRestricted")}
                </dd>
              </div>
            </dl>
          </>
        }
      />
      <section
        className="content-section public-section status-overview"
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
            <LoadingState label={t("Pages_Verein_Index_Loading")} />
          ) : request.isError ? (
            <ErrorState onRetry={() => void request.refetch()}>
              {t("React_Join_LoadError")}
            </ErrorState>
          ) : (
            <>
              <StatusBadge
                tone={toneOf(
                  membershipRequestTones,
                  request.data && normalizeMembershipStatus(request.data.status),
                )}
              >
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
          <ErrorState onRetry={() => void settings.refetch()}>
            {t("React_Verein_LoadError")}
          </ErrorState>
        </div>
      )}
      <section className="content-section public-section">
        <PageHeader
          kicker={t("Pages_Verein_Index_WorkspaceKicker")}
          title={t("Pages_Verein_Index_WorkspaceTitle")}
          description={t("Pages_Verein_Index_WorkspaceNote")}
          headingLevel={2}
        />
        <div className="resource-links">
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
            <StatusBadge tone={canReadLedger ? "success" : "neutral"}>
              {canReadLedger
                ? t("Pages_Verein_Index_LedgerAccessible")
                : t("Pages_Verein_Index_LedgerRestricted")}
            </StatusBadge>
          </Link>
          <Link to={isVereinMember(auth.user) ? "/settings" : "/join"}>
            <PeopleCommunity24Regular />
            <StatusBadge
              tone={
                isVereinMember(auth.user)
                  ? "success"
                  : toneOf(
                      membershipRequestTones,
                      request.data && normalizeMembershipStatus(request.data.status),
                    )
              }
            >
              {isVereinMember(auth.user)
                ? t("Pages_Verein_Index_MemberState")
                : request.data
                  ? t(membershipStatusTranslationKey(request.data.status))
                  : t("Pages_Verein_Index_RequestStateOpen")}
            </StatusBadge>
            <h3>{t("Pages_Verein_Index_MembershipTitle")}</h3>
            <p>{membershipDetail}</p>
            <ArrowRight20Regular />
          </Link>
        </div>
      </section>
    </div>
  );
}

/** Props of {@link DocumentPageFrame}. */
interface DocumentPageFrameProps {
  /** Router target of the back action in the hero. */
  backTo: string;
  /** Hero background image URL. */
  heroImage: string;
  /** The page title, rendered as the page's only h1. */
  title: string;
  /** Short label above the title. */
  kicker: string;
  /** Optional lede below the title. */
  description?: ReactNode | undefined;
  /** The page body below the hero. */
  children: ReactNode;
}

/**
 * The frame of a published Verein document page: one hero with the h1, then the body.
 *
 * The loading, error and not-found states render inside the same frame, so every state of the page shows the same
 * single h1 above its state view.
 *
 * @param props.backTo Router target of the back action in the hero.
 * @param props.heroImage Hero background image URL.
 * @param props.title The page title, rendered as the h1.
 * @param props.kicker Short label above the title.
 * @param props.description Optional lede below the title.
 * @param props.children The page body below the hero.
 */
function DocumentPageFrame({
  backTo,
  heroImage,
  title,
  kicker,
  description,
  children,
}: DocumentPageFrameProps) {
  const { t } = useTranslation();
  return (
    <div className="verein-document-page public-page">
      <PublicHero
        compact
        singleColumn
        className="editorial-hero"
        style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
        kicker={kicker}
        title={title}
        description={description}
        actions={
          <ActionLink variant="glass" to={backTo}>
            <ArrowLeft20Regular />
            {backTo.endsWith("beschluesse")
              ? t("Pages_Unit_Beschluesse_DocumentId_BackToArchive")
              : t("Pages_Unit_Satzung_Index_BackToUnit")}
          </ActionLink>
        }
      />
      <div className="page-wrap">{children}</div>
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
      <DocumentPageFrame
        backTo={backTo}
        heroImage={heroImage}
        kicker={t("Pages_Verein_Index_Kicker")}
        title={t("Pages_Verein_Index_SatzungTitle")}
      >
        <EmptyState title={t("Pages_Unit_Satzung_Index_EmptyTitle")}>
          {t("Pages_Unit_Satzung_Index_EmptyBody")}
        </EmptyState>
      </DocumentPageFrame>
    );
  return (
    <DocumentPageFrame
      backTo={backTo}
      heroImage={heroImage}
      kicker={t("React_Verein_PublishedRecord")}
      title={revision.title}
      description={
        <>
          <p>{revision.summary}</p>
          <span className="verein-document-page__meta">
            <StatusBadge tone="success">
              {t("React_Verein_Version")} {revision.version}
            </StatusBadge>
            <span>{formatDate(revision.timeStampPublished ?? document.timeStampUpdated)}</span>
          </span>
        </>
      }
    >
      <article className="governance-document">
        <Markdown>{revision.markdown}</Markdown>
      </article>
    </DocumentPageFrame>
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
  if (query.isLoading || query.isError || !query.data)
    return (
      <DocumentPageFrame
        backTo="/verein"
        heroImage={heroImage}
        kicker={t("Pages_Verein_Index_Kicker")}
        title={t("Pages_Verein_Index_SatzungTitle")}
      >
        {query.isLoading ? (
          <LoadingState label={t("Pages_Unit_Satzung_Index_Loading")} />
        ) : query.isError ? (
          <ErrorState onRetry={() => void query.refetch()}>
            {t("Pages_Unit_Satzung_Index_Error_LoadFailed")}
          </ErrorState>
        ) : (
          <EmptyState title={t("Pages_Unit_Satzung_Index_EmptyTitle")}>
            {t("Pages_Unit_Satzung_Index_EmptyBody")}
          </EmptyState>
        )}
      </DocumentPageFrame>
    );
  return <DocumentView document={query.data} backTo="/verein" heroImage={heroImage} />;
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
          <ActionLink variant="ghost" to="/verein">
            {t("Pages_Unit_Beschluesse_Index_BackToUnit")}
          </ActionLink>
        }
      />
      <PageStack>
        {query.isLoading && <LoadingState label={t("Pages_Unit_Beschluesse_Index_Loading")} />}
        {query.isError && (
          <ErrorState onRetry={() => void query.refetch()}>
            {t("Pages_Unit_Beschluesse_Index_Error_LoadFailed")}
          </ErrorState>
        )}
        {!query.isLoading && !query.isError && !query.data?.length && (
          <EmptyState title={t("Pages_Unit_Beschluesse_Index_NoRecordsTitle")}>
            {t("Pages_Unit_Beschluesse_Index_NoRecordsDescription")}
          </EmptyState>
        )}
        {query.data?.length ? (
          <div className="document-list">
            {query.data.map(
              (document) =>
                document.revision && (
                  <Link key={document.primaryKey} to={`/verein/beschluesse/${document.primaryKey}`}>
                    <span>
                      {formatDate(
                        document.revision.timeStampPublished ?? document.timeStampUpdated,
                      )}
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
        ) : null}
      </PageStack>
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
  if (query.isLoading || query.isError || !idIsValid || !query.data)
    return (
      <DocumentPageFrame
        backTo="/verein/beschluesse"
        heroImage={heroImage}
        kicker={t("Pages_Verein_Index_Kicker")}
        title={t("Pages_Unit_Beschluesse_DocumentId_PageTitle_Fallback")}
      >
        {query.isLoading ? (
          <LoadingState label={t("Pages_Unit_Beschluesse_DocumentId_Loading")} />
        ) : query.isError ? (
          <ErrorState onRetry={() => void query.refetch()}>
            {t("Pages_Unit_Beschluesse_DocumentId_Error_LoadFailed")}
          </ErrorState>
        ) : (
          <EmptyState title={t("Pages_Unit_Beschluesse_DocumentId_NotFoundTitle")}>
            {t("Pages_Unit_Beschluesse_DocumentId_NotFoundDescription")}
          </EmptyState>
        )}
      </DocumentPageFrame>
    );
  return <DocumentView document={query.data} backTo="/verein/beschluesse" heroImage={heroImage} />;
}
