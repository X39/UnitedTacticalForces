import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useParams } from "react-router";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManagePublicPages } from "@/auth/claims";
import { useAppRouteToast, useAppToastOnChange } from "@/components/AppToasts";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { PageStack } from "@/components/PageStack";
import { Markdown, PageHeader, StatusBadge } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
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
  const location = useLocation();
  const queryClient = useQueryClient();
  const canManagePages = canManagePublicPages(user);
  const queryKey = ["command-center", "public-pages", slug] as const;
  const query = useQuery({
    queryKey,
    // A missing page is a valid answer; TanStack Query rejects `undefined` data, so it is carried as `null`.
    queryFn: async ({ signal }) => (await loadAdminPublicPage(slug, signal)) ?? null,
    enabled: canManagePages,
    retry: false,
  });
  const publication = useMutation({
    mutationFn: ({ revisionId, publish }: { revisionId: number; publish: boolean }) =>
      setPublicPageRevisionPublished(revisionId, publish),
    onSuccess: async (page) => {
      queryClient.setQueryData(queryKey, page);
      await queryClient.invalidateQueries({ queryKey: ["command-center", "public-pages"] });
    },
  });
  useAppRouteToast(
    canManagePages && (location.state as { pageSaved?: boolean } | null)?.pageSaved
      ? t("Pages_Command_Content_PageToastSaved")
      : undefined,
  );
  useAppToastOnChange(
    publication.isSuccess ? t("Pages_Command_Content_PageToastPublicationUpdated") : undefined,
  );
  useAppToastOnChange(publication.isError ? publication.error.message : undefined, "error");
  const page = query.data ?? undefined;
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
      <PageStack>
        <PageHeader title={t("Pages_Command_Content_PageDetailTitle")} />
        <ForbiddenState title={t("Pages_Command_Content_Pages_AccessRequiredTitle")}>
          {t("Pages_Command_Content_Pages_AccessRequiredDescription")}
        </ForbiddenState>
      </PageStack>
    );

  return (
    <PageStack>
      <PageHeader
        title={page ? pageTitle : t("Pages_Command_Content_PageDetailTitle")}
        description={t("Pages_Command_Content_PageDetailDescription")}
        actions={
          <>
            <ActionLink variant="ghost" to="/command/content/pages">
              {t("Pages_Command_Content_Pages_BackToList")}
            </ActionLink>
            {page && (
              <>
                <ActionLink variant="ghost" to={`/${encodeURIComponent(slug)}`}>
                  {t("Pages_Command_Content_Pages_OpenPublic")}
                </ActionLink>
                <ActionLink
                  variant="gold"
                  to={`/command/content/pages/${encodeURIComponent(slug)}/edit`}
                >
                  {t("Pages_Command_Content_Pages_OpenEdit")}
                </ActionLink>
              </>
            )}
          </>
        }
      />

      {query.isPending && <LoadingState label={t("Pages_Command_Content_Pages_Loading")} />}
      {query.isError && (
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("Pages_Command_Content_Pages_LoadError"), query.error.message)}
        </ErrorState>
      )}
      {!query.isPending && !query.isError && !page && (
        <EmptyState
          title={t("Pages_Command_Content_Pages_NotFoundTitle")}
          action={
            <ActionLink
              variant="primary"
              to={`/command/content/pages/${encodeURIComponent(slug)}/edit`}
            >
              {t("Pages_Command_Content_Pages_CreateFirstRevision")}
            </ActionLink>
          }
        >
          {t("Pages_Command_Content_Pages_NotFoundDescription")}
        </EmptyState>
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
                    <ActionButton
                      variant="ghost"
                      size="compact"
                      disabled={publication.isPending}
                      aria-label={`${t(
                        revision.isPublished
                          ? "Pages_Command_Content_PageActionUnpublish"
                          : "Pages_Command_Content_PageActionPublish",
                      )}: ${revisionLabel(revision)}`}
                      onClick={() =>
                        publication.mutate({
                          revisionId: revision.primaryKey,
                          publish: !revision.isPublished,
                        })
                      }
                    >
                      {t(
                        revision.isPublished
                          ? "Pages_Command_Content_PageActionUnpublish"
                          : "Pages_Command_Content_PageActionPublish",
                      )}
                    </ActionButton>
                  </article>
                ))}
            </div>
          </section>
        </div>
      )}
    </PageStack>
  );
}
