import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useParams } from "react-router";
import { currentLocale, formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageVereinDocuments } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppRouteToast, useAppToastOnChange } from "@/components/AppToasts";
import { PageStack } from "@/components/PageStack";
import { Markdown, PageHeader, StatusBadge } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { reportTones, toneOf } from "@/components/statusTones";
import {
  activeVereinRevision,
  latestPublishedVereinRevision,
  loadVereinDocuments,
  setVereinRevisionPublished,
  vereinDocumentQueryKeys,
  type VereinAdminDocument,
  type VereinDocumentRevision,
} from "@/features/admin/vereinDocuments";

/** Renders the page header above a state view, so every state of the detail route keeps one `h1`. */
function DetailStateFrame({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <PageStack>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_VereinDocuments")}
        title={t("Pages_Command_Verein_DocumentsDetailPageTitle")}
        actions={
          <ActionLink variant="ghost" to="/command/verein/documents">
            {t("Pages_Command_Verein_DocumentsBackToList")}
          </ActionLink>
        }
      />
      {children}
    </PageStack>
  );
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { documentId: rawDocumentId } = useParams();
  const location = useLocation();
  const queryClient = useQueryClient();
  const documentId = Number(rawDocumentId);
  const authorized = canManageVereinDocuments(user);
  const query = useQuery({
    queryKey: vereinDocumentQueryKeys.all,
    queryFn: ({ signal }) => loadVereinDocuments(signal),
    enabled: authorized,
  });
  const document = query.data?.find((candidate) => candidate.primaryKey === documentId) ?? null;
  const activeRevision = document ? activeVereinRevision(document) : null;
  const publishedRevision = document ? latestPublishedVereinRevision(document) : null;
  const publication = useMutation({
    mutationFn: (revision: VereinDocumentRevision) =>
      setVereinRevisionPublished(revision.primaryKey, !revision.isPublished),
    onSuccess: (updated) => {
      queryClient.setQueryData<VereinAdminDocument[]>(vereinDocumentQueryKeys.all, (current) =>
        (current ?? []).map((candidate) =>
          candidate.primaryKey === updated.primaryKey ? updated : candidate,
        ),
      );
    },
  });
  useAppRouteToast(
    (location.state as { documentSaved?: boolean } | null)?.documentSaved
      ? t("Pages_Command_Verein_DocumentToastSaved")
      : undefined,
  );
  useAppToastOnChange(
    publication.isSuccess ? t("Pages_Command_Verein_DocumentToastPublicationUpdated") : undefined,
  );
  useAppToastOnChange(publication.isError ? publication.error.message : undefined, "error");
  const title =
    activeRevision?.title ||
    (document
      ? formatResource(t("Pages_Command_Verein_DocumentFallbackTitle"), document.primaryKey)
      : t("Pages_Command_Verein_DocumentsDetailPageTitle"));
  const kindLabel = document
    ? t(document.kind === "Satzung" ? "Satzung" : "Auto_BeschlussMinutes_6fa4d247")
    : "";
  const countLabel = (count: number) =>
    `${count.toLocaleString(currentLocale())} ${t(
      count === 1
        ? "Pages_Command_Verein_Count_Singular_Revision"
        : "Pages_Command_Verein_Count_Plural_Revision",
    )}`;

  if (!authorized)
    return (
      <DetailStateFrame>
        <ForbiddenState title={t("Pages_Command_Verein_DocumentsAccessRequiredTitle")}>
          {t("Pages_Command_Verein_DocumentsAccessRequiredDescription")}
        </ForbiddenState>
      </DetailStateFrame>
    );

  if (query.isPending)
    return (
      <DetailStateFrame>
        <LoadingState label={t("Pages_Command_Verein_Loading")} />
      </DetailStateFrame>
    );

  if (query.isError)
    return (
      <DetailStateFrame>
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("Pages_Command_Verein_LoadError"), query.error.message)}
        </ErrorState>
      </DetailStateFrame>
    );

  if (!Number.isSafeInteger(documentId) || !document)
    return (
      <DetailStateFrame>
        <EmptyState title={t("Pages_Command_Verein_DocumentNotFoundTitle")}>
          {t("Pages_Command_Verein_DocumentNotFoundDescription")}
        </EmptyState>
      </DetailStateFrame>
    );

  return (
    <PageStack>
      <PageHeader
        kicker={kindLabel}
        title={title}
        description={t("Pages_Command_Verein_DocumentsDetailPageDescription")}
        actions={
          <>
            <ActionLink variant="ghost" to="/command/verein/documents">
              {t("Pages_Command_Verein_DocumentsBackToList")}
            </ActionLink>
            <ActionLink variant="gold" to={`/command/verein/documents/${document.primaryKey}/edit`}>
              {t("Pages_Command_Verein_DocumentsOpenEdit")}
            </ActionLink>
          </>
        }
      />

      <div className="verein-document-detail-shell">
        <section className="command-panel verein-document-preview-panel">
          <header className="command-panel__header">
            <span className="kicker">{t("Pages_Command_Verein_DocumentPreviewKicker")}</span>
            <h2>{title}</h2>
            <StatusBadge tone={toneOf(reportTones, publishedRevision ? "published" : "draft")}>
              {publishedRevision
                ? formatResource(
                    t("Pages_Command_Verein_DocumentsPublishedVersion"),
                    publishedRevision.version,
                  )
                : t("Pages_Command_Verein_DocumentsDraftOnly")}
            </StatusBadge>
          </header>
          <div className="verein-document-preview">
            <Markdown>
              {activeRevision?.markdown || t("Pages_Command_Verein_DocumentPreviewEmpty")}
            </Markdown>
          </div>
        </section>

        <aside className="command-panel verein-document-meta-panel">
          <header className="command-panel__header">
            <span className="kicker">{t("Pages_Command_Verein_DocumentMetadataKicker")}</span>
            <h2>{t("Pages_Command_Verein_DocumentMetadataTitle")}</h2>
          </header>
          <dl className="verein-document-facts">
            <div>
              <dt>{t("Pages_Command_Verein_DocumentFormKind")}</dt>
              <dd>{kindLabel}</dd>
            </div>
            <div>
              <dt>{t("Pages_Command_Verein_DocumentMetadataLatestRevision")}</dt>
              <dd>
                {activeRevision
                  ? formatResource(
                      t("Pages_Command_Verein_RevisionVersion"),
                      activeRevision.version,
                    )
                  : t("Pages_Command_Verein_DocumentsNoPublicRevision")}
              </dd>
            </div>
            <div>
              <dt>{t("Pages_Command_Verein_DocumentMetadataLatestPublic")}</dt>
              <dd>
                {publishedRevision
                  ? formatResource(
                      t("Pages_Command_Verein_RevisionVersion"),
                      publishedRevision.version,
                    )
                  : t("Pages_Command_Verein_DocumentsNoPublicRevision")}
              </dd>
            </div>
            <div>
              <dt>{t("Pages_Command_Verein_DocumentsHeaderUpdated")}</dt>
              <dd>{formatDateTime(document.timeStampUpdated)}</dd>
            </div>
          </dl>
          {activeRevision?.summary && (
            <section className="verein-membership-request-section">
              <strong>{t("Pages_Command_Verein_DocumentFormSummary")}</strong>
              <p>{activeRevision.summary}</p>
            </section>
          )}
          <ActionLink
            variant="ghost"
            to={document.kind === "Satzung" ? "/verein/satzung" : "/verein/beschluesse"}
          >
            {t("Pages_Command_Verein_DocumentsPublicLink")}
          </ActionLink>
        </aside>

        <section className="command-panel verein-revision-panel">
          <header className="command-panel__header">
            <span className="kicker">{t("Pages_Command_Verein_RevisionsAria")}</span>
            <h2>{t("Pages_Command_Verein_RevisionsTitle")}</h2>
            <StatusBadge>{countLabel(document.revisions.length)}</StatusBadge>
          </header>
          <div className="verein-revision-stack">
            {[...document.revisions]
              .sort((left, right) => right.version - left.version)
              .map((revision) => (
                <article key={revision.primaryKey} className="verein-revision-row">
                  <div>
                    <strong>
                      {formatResource(t("Pages_Command_Verein_RevisionVersion"), revision.version)}
                    </strong>
                    <span>
                      {formatResource(
                        t("Pages_Command_Verein_RevisionMeta"),
                        revision.isPublished
                          ? t("Pages_Command_Verein_DocumentsPublished")
                          : t("Pages_Command_Verein_DocumentsDraftOnly"),
                        revision.authorName ?? t("Pages_Command_Verein_UnknownAuthor"),
                        formatDateTime(revision.timeStampCreated),
                      )}
                    </span>
                    {revision.comment && <small>{revision.comment}</small>}
                  </div>
                  <StatusBadge
                    tone={toneOf(reportTones, revision.isPublished ? "published" : "draft")}
                  >
                    {revision.isPublished
                      ? t("Pages_Command_Verein_DocumentsPublished")
                      : t("Pages_Command_Verein_DocumentsDraftOnly")}
                  </StatusBadge>
                  <ActionButton
                    variant="ghost"
                    disabled={publication.isPending}
                    onClick={() => publication.mutate(revision)}
                  >
                    {revision.isPublished
                      ? t("Pages_Command_Verein_DocumentActionUnpublishLatest")
                      : t("Pages_Command_Verein_DocumentActionPublishLatest")}
                  </ActionButton>
                </article>
              ))}
          </div>
        </section>
      </div>
    </PageStack>
  );
}
