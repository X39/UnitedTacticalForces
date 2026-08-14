import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManagePublicPages } from "@/auth/claims";
import { Markdown, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  latestPublishedRevision,
  loadAdminPublicPage,
  setPublicPageRevisionPublished,
  type PublicPage,
  type PublicPageRevision,
} from "@/features/admin/publicPages";
import { parseStructuredContent, StructuredContent } from "@/pages/GdprPage";

export function latestRevision(page: PublicPage): PublicPageRevision | undefined {
  return page.revision ?? [...page.revisions].sort((a, b) => b.version - a.version)[0];
}

export function Component() {
  const { t } = useTranslation();
  const { slug = "gdpr" } = useParams();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const canManagePages = canManagePublicPages(user);
  const queryKey = ["command-center", "public-pages", slug] as const;
  const query = useQuery({
    queryKey,
    queryFn: ({ signal }) => loadAdminPublicPage(slug, signal),
    enabled: canManagePages,
    retry: false,
  });
  const [success, setSuccess] = useState(false);
  const publication = useMutation({
    mutationFn: ({ revisionId, publish }: { revisionId: number; publish: boolean }) =>
      setPublicPageRevisionPublished(revisionId, publish),
    onSuccess: async (page) => {
      queryClient.setQueryData(queryKey, page);
      await queryClient.invalidateQueries({ queryKey: ["command-center", "public-pages"] });
      setSuccess(true);
    },
  });
  const page = query.data;
  const activeRevision = page ? latestRevision(page) : undefined;
  const publishedRevision = page ? latestPublishedRevision(page) : undefined;
  const structuredContent = useMemo(
    () =>
      slug.toLocaleLowerCase() === "gdpr"
        ? parseStructuredContent(activeRevision?.contentJson)
        : undefined,
    [activeRevision?.contentJson, slug],
  );
  const pageTitle =
    activeRevision?.title || formatResource(t("Pages_Command_Content_Pages_FallbackTitle"), slug);
  const revisionLabel = (revision: PublicPageRevision | undefined) =>
    revision
      ? formatResource(t("Pages_Command_Content_PageRevisionVersion"), revision.version)
      : t("Pages_Command_Content_PageNoPublicRevision");

  if (!canManagePages)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Content_Pages_AccessRequiredTitle")}>
        {t("Pages_Command_Content_Pages_AccessRequiredDescription")}
      </StatePanel>
    );

  return (
    <section className="command-public-page-detail">
      <PageHeader
        title={page ? pageTitle : t("Pages_Command_Content_PageDetailTitle")}
        description={t("Pages_Command_Content_PageDetailDescription")}
        actions={
          <>
            <Link className="button button--ghost" to="/command/content/pages">
              {t("Pages_Command_Content_Pages_BackToList")}
            </Link>
            {page && (
              <>
                <Link className="button button--ghost" to={`/${encodeURIComponent(slug)}`}>
                  {t("Pages_Command_Content_Pages_OpenPublic")}
                </Link>
                <Link
                  className="button button--primary"
                  to={`/command/content/pages/${encodeURIComponent(slug)}/edit`}
                >
                  {t("Pages_Command_Content_Pages_OpenEdit")}
                </Link>
              </>
            )}
          </>
        }
      />

      {query.isPending && (
        <StatePanel kind="loading">{t("Pages_Command_Content_Pages_Loading")}</StatePanel>
      )}
      {query.isError && (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" onClick={() => void query.refetch()}>
              {t("Common_Refresh")}
            </button>
          }
        >
          {formatResource(t("Pages_Command_Content_Pages_LoadError"), query.error.message)}
        </StatePanel>
      )}
      {!query.isPending && !query.isError && !page && (
        <StatePanel kind="empty" title={t("Pages_Command_Content_Pages_NotFoundTitle")}>
          <p>{t("Pages_Command_Content_Pages_NotFoundDescription")}</p>
          <Link
            className="button button--primary"
            to={`/command/content/pages/${encodeURIComponent(slug)}/edit`}
          >
            {t("Pages_Command_Content_Pages_CreateFirstRevision")}
          </Link>
        </StatePanel>
      )}
      {publication.isError && <StatePanel kind="error">{publication.error.message}</StatePanel>}
      {success && (
        <StatePanel kind="success">
          {t("Pages_Command_Content_PageToastPublicationUpdated")}
        </StatePanel>
      )}

      {page && (
        <div className="command-public-page-detail__grid">
          <section className="command-panel command-public-page-detail__preview">
            <header className="command-panel__header">
              <span className="kicker">{t("Pages_Command_Content_PagePreviewKicker")}</span>
              <h2>{pageTitle}</h2>
              <StatusBadge tone={publishedRevision ? "success" : "warning"}>
                {publishedRevision
                  ? formatResource(
                      t("Pages_Command_Content_Pages_PublishedVersion"),
                      publishedRevision.version,
                    )
                  : t("Pages_Command_Content_Pages_DraftOnly")}
              </StatusBadge>
            </header>
            <div className="command-public-page-detail__content">
              {structuredContent ? (
                <StructuredContent content={structuredContent} />
              ) : (
                <Markdown>
                  {activeRevision?.markdown || t("Pages_Command_Content_PagePreviewEmpty")}
                </Markdown>
              )}
            </div>
          </section>

          <aside className="command-panel command-public-page-detail__metadata">
            <header className="command-panel__header">
              <span className="kicker">{t("Pages_Command_Content_PageMetadataKicker")}</span>
              <h2>{t("Pages_Command_Content_PageMetadataTitle")}</h2>
            </header>
            <dl className="command-detail-list">
              <dt>{t("Pages_Command_Content_PageSlug")}</dt>
              <dd>{page.slug}</dd>
              <dt>{t("Pages_Command_Content_PageLatestRevision")}</dt>
              <dd>{revisionLabel(activeRevision)}</dd>
              <dt>{t("Pages_Command_Content_PageLatestPublic")}</dt>
              <dd>{revisionLabel(publishedRevision)}</dd>
              <dt>{t("Pages_Command_Content_Pages_HeaderUpdated")}</dt>
              <dd>{formatDateTime(page.timeStampUpdated)}</dd>
              <dt>{t("Pages_Command_Content_PageHeroImage")}</dt>
              <dd>{activeRevision?.heroImagePath || "-"}</dd>
            </dl>
            {activeRevision?.summary && (
              <div className="command-public-page-detail__summary">
                <strong>{t("Pages_Command_Content_PageFormSummary")}</strong>
                <p>{activeRevision.summary}</p>
              </div>
            )}
          </aside>

          <section className="command-panel command-public-page-detail__revisions">
            <header className="command-panel__header">
              <span className="kicker">{t("Pages_Command_Content_PageRevisionsKicker")}</span>
              <h2>{t("Pages_Command_Content_PageRevisionsTitle")}</h2>
              <StatusBadge>
                {formatResource(
                  t(
                    page.revisions.length === 1
                      ? "Pages_Command_Content_PageRevisionCountSingular"
                      : "Pages_Command_Content_PageRevisionCountPlural",
                  ),
                  page.revisions.length,
                )}
              </StatusBadge>
            </header>
            <div className="command-public-page-detail__revision-list">
              {[...page.revisions]
                .sort((left, right) => right.version - left.version)
                .map((revision) => (
                  <article key={revision.primaryKey}>
                    <div>
                      <strong>{revisionLabel(revision)}</strong>
                      <span>
                        {formatResource(
                          t("Pages_Command_Content_PageRevisionMeta"),
                          revision.isPublished
                            ? t("Pages_Command_Content_Pages_Published")
                            : t("Pages_Command_Content_Pages_DraftOnly"),
                          revision.authorName || t("Pages_Command_Content_PageUnknownAuthor"),
                          formatDateTime(revision.timeStampCreated),
                        )}
                      </span>
                    </div>
                    <StatusBadge tone={revision.isPublished ? "success" : "warning"}>
                      {t(
                        revision.isPublished
                          ? "Pages_Command_Content_Pages_Published"
                          : "Pages_Command_Content_Pages_DraftOnly",
                      )}
                    </StatusBadge>
                    <button
                      className="button button--ghost button--small"
                      type="button"
                      disabled={publication.isPending}
                      onClick={() => {
                        setSuccess(false);
                        publication.mutate({
                          revisionId: revision.primaryKey,
                          publish: !revision.isPublished,
                        });
                      }}
                    >
                      {t(
                        revision.isPublished
                          ? "Pages_Command_Content_PageActionUnpublish"
                          : "Pages_Command_Content_PageActionPublish",
                      )}
                    </button>
                  </article>
                ))}
            </div>
          </section>
        </div>
      )}
    </section>
  );
}
