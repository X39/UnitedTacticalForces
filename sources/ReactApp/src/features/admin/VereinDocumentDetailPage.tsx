import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useParams } from "react-router";
import { currentLocale, formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageVereinDocuments } from "@/auth/claims";
import { Markdown, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  activeVereinRevision,
  latestPublishedVereinRevision,
  loadVereinDocuments,
  setVereinRevisionPublished,
  vereinDocumentQueryKeys,
  type VereinAdminDocument,
  type VereinDocumentRevision,
} from "@/features/admin/vereinDocuments";

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
      <StatePanel kind="forbidden" title={t("Pages_Command_Verein_DocumentsAccessRequiredTitle")}>
        {t("Pages_Command_Verein_DocumentsAccessRequiredDescription")}
      </StatePanel>
    );

  if (query.isPending)
    return <StatePanel kind="loading">{t("Pages_Command_Verein_Loading")}</StatePanel>;

  if (query.isError)
    return (
      <StatePanel kind="error">
        {formatResource(t("Pages_Command_Verein_LoadError"), query.error.message)}
      </StatePanel>
    );

  if (!Number.isSafeInteger(documentId) || !document)
    return (
      <StatePanel kind="empty" title={t("Pages_Command_Verein_DocumentNotFoundTitle")}>
        {t("Pages_Command_Verein_DocumentNotFoundDescription")}
      </StatePanel>
    );

  return (
    <section className="verein-document-detail-page">
      <PageHeader
        kicker={kindLabel}
        title={title}
        description={t("Pages_Command_Verein_DocumentsDetailPageDescription")}
        actions={
          <>
            <Link className="button button--ghost" to="/command/verein/documents">
              {t("Pages_Command_Verein_DocumentsBackToList")}
            </Link>
            <Link
              className="button button--primary"
              to={`/command/verein/documents/${document.primaryKey}/edit`}
            >
              {t("Pages_Command_Verein_DocumentsOpenEdit")}
            </Link>
          </>
        }
      />

      {Boolean((location.state as { documentSaved?: boolean } | null)?.documentSaved) && (
        <StatePanel kind="success">{t("Pages_Command_Verein_DocumentToastSaved")}</StatePanel>
      )}
      {publication.isSuccess && (
        <StatePanel kind="success">
          {t("Pages_Command_Verein_DocumentToastPublicationUpdated")}
        </StatePanel>
      )}
      {publication.isError && <StatePanel kind="error">{publication.error.message}</StatePanel>}

      <div className="verein-document-detail-shell">
        <section className="command-panel verein-document-preview-panel">
          <header className="command-panel__header">
            <span className="kicker">{t("Pages_Command_Verein_DocumentPreviewKicker")}</span>
            <h2>{title}</h2>
            <StatusBadge tone={publishedRevision ? "success" : "warning"}>
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
          <Link
            className="button button--ghost"
            to={document.kind === "Satzung" ? "/verein/satzung" : "/verein/beschluesse"}
          >
            {t("Pages_Command_Verein_DocumentsPublicLink")}
          </Link>
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
                  <StatusBadge tone={revision.isPublished ? "success" : "warning"}>
                    {revision.isPublished
                      ? t("Pages_Command_Verein_DocumentsPublished")
                      : t("Pages_Command_Verein_DocumentsDraftOnly")}
                  </StatusBadge>
                  <button
                    className="button button--ghost"
                    type="button"
                    disabled={publication.isPending}
                    onClick={() => publication.mutate(revision)}
                  >
                    {revision.isPublished
                      ? t("Pages_Command_Verein_DocumentActionUnpublishLatest")
                      : t("Pages_Command_Verein_DocumentActionPublishLatest")}
                  </button>
                </article>
              ))}
          </div>
        </section>
      </div>
    </section>
  );
}
