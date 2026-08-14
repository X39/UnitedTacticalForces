import {
  BookOpen24Regular,
  CheckmarkCircle20Regular,
  Search20Regular,
  ShieldLock24Regular,
  Warning20Regular,
} from "@fluentui/react-icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type CSSProperties, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams } from "react-router";
import { ApiError } from "@/api/client";
import type { WikiPage, WikiPageHeader, WikiPageRevision } from "@/api/types";
import { formatDate } from "@/app/format";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { useSiteImage } from "@/app/siteImages";
import { useAuth } from "@/auth/auth";
import {
  ConfirmDialog,
  Markdown,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import {
  buildWikiTree,
  filterWikiHeaders,
  getWikiAccess,
  isWikiEditorDirty,
  latestWikiRevision,
  revisionCountLabel,
  validateWikiEditor,
  wikiArticleSummary,
  wikiCategoryPath,
  wikiLeafName,
  type WikiEditorError,
  type WikiEditorState,
  type WikiTreeNode,
} from "@/features/wiki/model";
import { wikiQueryKeys, wikiService } from "@/features/wiki/services";
import "@/features/wiki/wiki.css";

type MobileView = "pages" | "article" | "history";
type PanelMode = "details" | "history" | "edit";

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const emptyHeaders: WikiPageHeader[] = [];

function errorKind(error: unknown): "forbidden" | "not-found" | "error" {
  if (error instanceof ApiError && error.code === "forbidden") return "forbidden";
  if (error instanceof ApiError && error.code === "not-found") return "not-found";
  return "error";
}

function editorErrorKey(
  error: WikiEditorError,
):
  | "Pages_Wiki_Index_Error_TitleRequired"
  | "Pages_Wiki_Index_Error_MarkdownRequired"
  | "Pages_Wiki_Index_Error_RevisionCommentRequired"
  | "Pages_Wiki_Index_Error_NoPermissionCreate"
  | "Pages_Wiki_Index_Error_NoChanges"
  | "Pages_Wiki_Index_Error_NoPermissionRename"
  | "Pages_Wiki_Index_Error_NoPermissionEdit" {
  switch (error) {
    case "title-required":
      return "Pages_Wiki_Index_Error_TitleRequired";
    case "markdown-required":
      return "Pages_Wiki_Index_Error_MarkdownRequired";
    case "comment-required":
      return "Pages_Wiki_Index_Error_RevisionCommentRequired";
    case "create-forbidden":
      return "Pages_Wiki_Index_Error_NoPermissionCreate";
    case "no-changes":
      return "Pages_Wiki_Index_Error_NoChanges";
    case "rename-forbidden":
      return "Pages_Wiki_Index_Error_NoPermissionRename";
    case "modify-forbidden":
      return "Pages_Wiki_Index_Error_NoPermissionEdit";
  }
}

export function Component() {
  const { t } = useTranslation();
  const auth = useAuth();
  const access = useMemo(() => getWikiAccess(auth.user), [auth.user]);
  const { pageId: routePageId } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const heroImage = useSiteImage("wiki-hero", "/media/generated/wiki-reference-table-hero.jpg");
  const [search, setSearch] = useState("");
  const [mobileView, setMobileView] = useState<MobileView>("article");
  const [panelSelection, setPanelSelection] = useState<{
    pageId: string | undefined;
    mode: PanelMode;
  }>({ pageId: undefined, mode: "details" });
  const [selectedRevision, setSelectedRevision] = useState<{
    pageId: string;
    revisionId: string;
  }>();
  const [editorSession, setEditorSession] = useState<{
    pageId: string | undefined;
    editor: WikiEditorState;
  }>();
  const [editorError, setEditorError] = useState<string>();
  const [archivePageId, setArchivePageId] = useState<string>();

  const headersQuery = useQuery({
    queryKey: wikiQueryKeys.headers(access.canRestore),
    queryFn: ({ signal }) => wikiService.headers(access.canRestore, signal),
    enabled: !auth.isLoading,
  });
  const headers = headersQuery.data ?? emptyHeaders;
  const visibleHeaders = useMemo(() => filterWikiHeaders(headers, search), [headers, search]);
  const tree = useMemo(() => buildWikiTree(visibleHeaders), [visibleHeaders]);
  const validRoutePageId = routePageId && guidPattern.test(routePageId) ? routePageId : undefined;
  const selectedPageId = validRoutePageId ?? (routePageId ? undefined : headers[0]?.primaryKey);
  const panelMode = panelSelection.pageId === selectedPageId ? panelSelection.mode : "details";
  const selectedRevisionId =
    selectedRevision && selectedRevision.pageId === selectedPageId
      ? selectedRevision.revisionId
      : undefined;
  const editor =
    editorSession && editorSession.pageId === selectedPageId ? editorSession.editor : undefined;

  const pageQuery = useQuery({
    queryKey: wikiQueryKeys.page(selectedPageId ?? "none"),
    queryFn: ({ signal }) => wikiService.page(selectedPageId!, signal),
    enabled: !auth.isLoading && Boolean(selectedPageId),
    retry: false,
  });
  const revisionsQuery = useQuery({
    queryKey: wikiQueryKeys.revisions(selectedPageId ?? "none"),
    queryFn: ({ signal }) => wikiService.revisionHeaders(selectedPageId!, signal),
    enabled: !auth.isLoading && access.canViewHistory && Boolean(selectedPageId),
    retry: false,
  });
  const revisionQuery = useQuery({
    queryKey: wikiQueryKeys.revision(selectedPageId ?? "none", selectedRevisionId ?? "latest"),
    queryFn: ({ signal }) => wikiService.revision(selectedPageId!, selectedRevisionId!, signal),
    enabled: access.canViewHistory && Boolean(selectedPageId && selectedRevisionId),
    retry: false,
  });

  useUnsavedChanges(isWikiEditorDirty(editor));
  useDocumentTitle(t("Pages_Wiki_Index_PageTitle"));

  const page = pageQuery.data;
  const latestRevision = latestWikiRevision(page?.revisions);
  const activeRevision = revisionQuery.data ?? latestRevision;
  const activeMarkdown = activeRevision?.markdown ?? t("Pages_Wiki_Index_NoMarkdownContent");
  const canEditSelected = Boolean(
    page && !page.isDeleted && (access.canModify || access.canRename),
  );
  const relatedPages = page
    ? headers
        .filter(
          (header) =>
            header.primaryKey !== page.primaryKey &&
            wikiCategoryPath(header.name) === wikiCategoryPath(page.title),
        )
        .slice(0, 5)
    : [];

  const invalidatePage = async (pageId: string) => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["wiki", "headers"] }),
      queryClient.invalidateQueries({ queryKey: wikiQueryKeys.page(pageId) }),
      queryClient.invalidateQueries({ queryKey: wikiQueryKeys.revisions(pageId) }),
    ]);
  };

  const saveMutation = useMutation({
    mutationFn: async (value: WikiEditorState) => {
      const validationError = validateWikiEditor(value, access);
      if (validationError) throw new Error(editorErrorKey(validationError));
      if (value.mode === "create")
        return wikiService.create(value.title.trim(), value.markdown, value.comment.trim());

      if (!value.pageId) throw new Error("Pages_Wiki_Index_Error_EditorNotOpen");
      if (value.originalTitle !== value.title.trim())
        await wikiService.rename(value.pageId, value.title.trim());
      if (value.originalMarkdown !== value.markdown)
        await wikiService.createRevision(value.pageId, value.markdown, value.comment.trim());
      return undefined;
    },
    onSuccess: async (savedPage, value) => {
      const pageId = savedPage?.primaryKey ?? value.pageId;
      await queryClient.invalidateQueries({ queryKey: ["wiki", "headers"] });
      if (pageId) await invalidatePage(pageId);
      setSelectedRevision(undefined);
      setEditorSession(undefined);
      setEditorError(undefined);
      setPanelMode("details");
      if (savedPage?.primaryKey) navigate(`/wiki/${savedPage.primaryKey}`);
    },
  });

  const archiveMutation = useMutation({
    mutationFn: (pageId: string) => wikiService.archive(pageId),
    onSuccess: async (_, pageId) => {
      await queryClient.invalidateQueries({ queryKey: ["wiki", "headers"] });
      if (access.canRestore) {
        await invalidatePage(pageId);
        return;
      }
      const refreshed = await wikiService.headers(false);
      queryClient.setQueryData(wikiQueryKeys.headers(false), refreshed);
      const nextPage = refreshed?.find((header) => header.primaryKey !== pageId);
      navigate(nextPage ? `/wiki/${nextPage.primaryKey}` : "/wiki");
    },
    onSettled: () => setArchivePageId(undefined),
  });

  const restoreMutation = useMutation({
    mutationFn: (pageId: string) => wikiService.restore(pageId),
    onSuccess: (_, pageId) => invalidatePage(pageId),
  });

  const isSaving = saveMutation.isPending || archiveMutation.isPending || restoreMutation.isPending;

  function openCreateEditor() {
    if (!access.canCreate) return;
    setEditorSession({
      pageId: selectedPageId,
      editor: {
        mode: "create",
        originalTitle: "",
        originalMarkdown: "",
        title: "",
        markdown: t("Pages_Wiki_Index_DefaultMarkdownTemplate"),
        comment: "",
        preview: false,
      },
    });
    setEditorError(undefined);
    setPanelMode("edit");
  }

  function openEditEditor() {
    if (!canEditSelected || !page) return;
    const markdown = activeRevision?.markdown ?? "";
    setEditorSession({
      pageId: selectedPageId,
      editor: {
        mode: "edit",
        pageId: page.primaryKey,
        originalTitle: page.title,
        originalMarkdown: markdown,
        title: page.title,
        markdown,
        comment: "",
        preview: false,
      },
    });
    setEditorError(undefined);
    setPanelMode("edit");
  }

  function closeEditor() {
    setEditorSession(undefined);
    setEditorError(undefined);
    setPanelMode("details");
  }

  async function saveEditor() {
    if (!editor) return;
    const validationError = validateWikiEditor(editor, access);
    if (validationError) {
      setEditorError(t(editorErrorKey(validationError)));
      return;
    }
    setEditorError(undefined);
    try {
      await saveMutation.mutateAsync(editor);
    } catch (error) {
      const key = error instanceof Error ? error.message : "";
      setEditorError(
        key.startsWith("Pages_Wiki_Index_")
          ? t(key as "Pages_Wiki_Index_Error_EditorNotOpen")
          : t("Pages_Wiki_Index_Error_SaveFailed"),
      );
    }
  }

  function showPanel(mode: PanelMode) {
    setPanelMode(mode);
    if (mode === "history") setMobileView("history");
  }

  function setPanelMode(mode: PanelMode) {
    setPanelSelection({ pageId: selectedPageId, mode });
  }

  const pageErrorKind = routePageId && !validRoutePageId ? "not-found" : errorKind(pageQuery.error);

  return (
    <div
      className="wiki-page"
      style={{ "--wiki-hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
    >
      <header className="wiki-hero">
        <div className="wiki-hero__title">
          <span aria-hidden="true">
            <BookOpen24Regular />
          </span>
          <div>
            <h1>{t("Pages_Wiki_Index_Title")}</h1>
            <p>{t("Pages_Wiki_Index_Subtitle")}</p>
          </div>
        </div>
        <label className="wiki-search">
          <span>{t("Pages_Wiki_Index_SearchLabel")}</span>
          <span>
            <Search20Regular aria-hidden="true" />
            <input
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t("Pages_Wiki_Index_SearchPlaceholder")}
            />
          </span>
        </label>
      </header>

      <div
        className="wiki-mobile-tabs"
        role="tablist"
        aria-label={t("Pages_Wiki_Index_MobileTabsAria")}
      >
        <MobileTab
          active={mobileView === "pages"}
          controls="wiki-navigation"
          onClick={() => setMobileView("pages")}
        >
          {t("Pages_Wiki_Index_Tab_Pages")}
        </MobileTab>
        <MobileTab
          active={mobileView === "article"}
          controls="wiki-article"
          onClick={() => setMobileView("article")}
        >
          {t("Pages_Wiki_Index_Tab_Article")}
        </MobileTab>
        {access.canViewHistory && (
          <MobileTab
            active={mobileView === "history"}
            controls="wiki-intel"
            onClick={() => showPanel("history")}
          >
            {t("Pages_Wiki_Index_Tab_History")}
          </MobileTab>
        )}
      </div>

      <section className={`wiki-workspace wiki-mobile-view--${mobileView}`}>
        <aside
          id="wiki-navigation"
          className="wiki-panel wiki-navigation"
          aria-label={t("Pages_Wiki_Index_NavigationAria")}
        >
          <header className="wiki-panel__header">
            <div>
              <span className="kicker">{t("Pages_Wiki_Index_KnowledgeBase")}</span>
              <h2>{t("Pages_Wiki_Index_Pages")}</h2>
            </div>
            {access.canCreate && (
              <button
                className="button button--ghost button--small"
                type="button"
                onClick={openCreateEditor}
              >
                {t("Pages_Wiki_Index_NewPage")}
              </button>
            )}
          </header>
          <div className="wiki-navigation__body">
            {auth.isLoading || headersQuery.isLoading ? (
              <StatePanel kind="loading">{t("Pages_Wiki_Index_LoadingIndex")}</StatePanel>
            ) : headersQuery.isError ? (
              <StatePanel
                kind={errorKind(headersQuery.error) === "forbidden" ? "forbidden" : "error"}
                action={
                  <button
                    className="button button--ghost button--small"
                    type="button"
                    onClick={() => void headersQuery.refetch()}
                  >
                    {t("React_UI_Retry")}
                  </button>
                }
              >
                {t("Pages_Wiki_Index_Error_LoadHeadersFailed")}
              </StatePanel>
            ) : visibleHeaders.length === 0 ? (
              <StatePanel kind="empty">
                {search
                  ? t("Pages_Wiki_Index_NoPagesMatchSearch")
                  : t("Pages_Wiki_Index_NoPagesYet")}
              </StatePanel>
            ) : (
              <WikiTree
                node={tree}
                selectedPageId={selectedPageId}
                noRevisions={t("Components_WikiTreeNode_NoRevisions")}
                onSelect={() => setMobileView("article")}
              />
            )}
          </div>
          <div className="wiki-access-note">
            <ShieldLock24Regular aria-hidden="true" />
            <span>
              {access.canCreate || access.canModify || access.canRename
                ? t("Pages_Wiki_Index_ContributeWithPermissions")
                : t("Pages_Wiki_Index_ReadOnlyAccessNotice")}
            </span>
          </div>
        </aside>

        <article id="wiki-article" className="wiki-panel wiki-article" aria-live="polite">
          {pageQuery.isLoading ? (
            <StatePanel kind="loading">{t("Pages_Wiki_Index_LoadingArticle")}</StatePanel>
          ) : pageErrorKind === "not-found" ? (
            <StatePanel kind="empty" title={t("App_NotFound_Title")}>
              {t("App_NotFound_Message")}
            </StatePanel>
          ) : pageQuery.isError ? (
            <StatePanel
              kind={pageErrorKind === "forbidden" ? "forbidden" : "error"}
              title={t("Pages_Wiki_Index_Error_LoadPageFailed")}
              action={
                <button
                  className="button button--ghost"
                  type="button"
                  onClick={() => void pageQuery.refetch()}
                >
                  {t("React_UI_Retry")}
                </button>
              }
            >
              {t("Pages_Wiki_Index_Error_LoadPageFailed")}
            </StatePanel>
          ) : !page ? (
            <div className="wiki-empty-article">
              <BookOpen24Regular aria-hidden="true" />
              <h2>{t("Pages_Wiki_Index_NoPagesYet")}</h2>
              <p>{t("Pages_Wiki_Index_NoPagesDescription")}</p>
              {access.canCreate && (
                <button className="button button--gold" type="button" onClick={openCreateEditor}>
                  {t("Pages_Wiki_Index_NewPage")}
                </button>
              )}
            </div>
          ) : (
            <>
              <nav className="wiki-breadcrumb" aria-label={t("Pages_Wiki_Index_NavigationAria")}>
                <Link to="/wiki">{t("Pages_Wiki_Index_KnowledgeBase")}</Link>
                {wikiCategoryPath(page.title) && <span>{wikiCategoryPath(page.title)}</span>}
                <strong>{wikiLeafName(page.title, t("Pages_Wiki_Index_UntitledPage"))}</strong>
              </nav>
              <header className="wiki-article__header">
                <div>
                  <h2>{wikiLeafName(page.title, t("Pages_Wiki_Index_UntitledPage"))}</h2>
                  <p>
                    {wikiArticleSummary(
                      activeRevision?.markdown,
                      t("Pages_Wiki_Index_ArticleSummaryPlaceholder"),
                    )}
                  </p>
                </div>
                <StatusBadge tone={page.isDeleted ? "warning" : "success"}>
                  {page.isDeleted
                    ? t("Pages_Wiki_Index_StatusArchived")
                    : t("Pages_Wiki_Index_StatusVerified")}
                </StatusBadge>
              </header>
              <div className="wiki-article__meta">
                <span>
                  {formatResource(
                    t("Pages_Wiki_Index_UpdatedAt"),
                    formatWikiDate(activeRevision?.timeStampCreated ?? page.timeStampCreated),
                  )}
                </span>
                <span>
                  {formatResource(
                    t("Pages_Wiki_Index_By"),
                    activeRevision?.author?.nickname ?? t("Pages_Wiki_Index_UnknownAuthor"),
                  )}
                </span>
                {access.canViewHistory && (
                  <span>
                    {revisionCountLabel(
                      revisionsQuery.data,
                      t("Pages_Wiki_Index_NoRevisions"),
                      (count) => formatResource(t("Pages_Wiki_Index_RevisionVersion"), count),
                    )}
                  </span>
                )}
              </div>
              <div className="wiki-article__actions">
                {access.canCreate && (
                  <button className="button button--gold" type="button" onClick={openCreateEditor}>
                    {t("Pages_Wiki_Index_NewPage")}
                  </button>
                )}
                {canEditSelected && (
                  <button className="button button--ghost" type="button" onClick={openEditEditor}>
                    {t("Pages_Wiki_Index_Edit")}
                  </button>
                )}
                {access.canViewHistory && (
                  <button
                    className="button button--ghost"
                    type="button"
                    onClick={() => showPanel("history")}
                  >
                    {t("Pages_Wiki_Index_Tab_History")}
                  </button>
                )}
              </div>
              {(archiveMutation.isError || restoreMutation.isError) && (
                <StatePanel kind="error">
                  {archiveMutation.isError
                    ? t("Pages_Wiki_Index_Error_DeleteFailed")
                    : t("Pages_Wiki_Index_Error_RestoreFailed")}
                </StatePanel>
              )}
              {page.isDeleted && (
                <div className="wiki-callout">
                  <Warning20Regular aria-hidden="true" />
                  {t("Pages_Wiki_Index_ArchivedNotice")}
                </div>
              )}
              {revisionQuery.isError && (
                <StatePanel
                  kind={errorKind(revisionQuery.error) === "forbidden" ? "forbidden" : "error"}
                >
                  {t("Pages_Wiki_Index_Error_LoadPageFailed")}
                </StatePanel>
              )}
              <Markdown>{activeMarkdown}</Markdown>
              {!canEditSelected && (
                <div className="wiki-readonly-note">
                  <strong>{t("Pages_Wiki_Index_ReadOnlyAccessTitle")}</strong>
                  <span>{t("Pages_Wiki_Index_ReadOnlyAccessMessage")}</span>
                </div>
              )}
            </>
          )}
        </article>

        <aside
          id="wiki-intel"
          className={`wiki-panel wiki-intel${panelMode === "edit" ? " wiki-intel--editing" : ""}`}
          aria-label={t("Pages_Wiki_Index_ArticleDetailsAria")}
        >
          <div className="wiki-intel__tabs" role="tablist">
            <PanelTab
              active={panelMode === "details"}
              controls="wiki-intel-content"
              onClick={() => showPanel("details")}
            >
              {t("Pages_Wiki_Index_DetailsTab")}
            </PanelTab>
            {access.canViewHistory && (
              <PanelTab
                active={panelMode === "history"}
                controls="wiki-intel-content"
                onClick={() => showPanel("history")}
              >
                {t("Pages_Wiki_Index_Tab_History")}
              </PanelTab>
            )}
            {(canEditSelected || access.canCreate) && (
              <PanelTab
                active={panelMode === "edit"}
                controls="wiki-intel-content"
                onClick={() => (canEditSelected ? openEditEditor() : openCreateEditor())}
              >
                {t("Pages_Wiki_Index_Edit")}
              </PanelTab>
            )}
          </div>
          <div id="wiki-intel-content" className="wiki-intel__content">
            {panelMode === "edit" ? (
              <WikiEditor
                editor={editor}
                error={editorError}
                isSaving={isSaving}
                onChange={(value) => setEditorSession({ pageId: selectedPageId, editor: value })}
                onClose={closeEditor}
                onSave={() => void saveEditor()}
              />
            ) : panelMode === "history" && access.canViewHistory ? (
              <WikiHistory
                pageSelected={Boolean(page)}
                isLoading={revisionsQuery.isLoading}
                error={revisionsQuery.error}
                revisions={revisionsQuery.data ?? []}
                activeRevisionId={activeRevision?.primaryKey}
                onSelect={(revisionId) => {
                  if (selectedPageId) setSelectedRevision({ pageId: selectedPageId, revisionId });
                  setMobileView("article");
                }}
                onRetry={() => void revisionsQuery.refetch()}
              />
            ) : (
              <WikiDetails
                page={page}
                latestRevision={latestRevision}
                relatedPages={relatedPages}
                canDelete={Boolean(page && !page.isDeleted && access.canDelete)}
                canRestore={Boolean(page?.isDeleted && access.canRestore)}
                isSaving={isSaving}
                onSelectRelated={() => setMobileView("article")}
                onArchive={() => page && setArchivePageId(page.primaryKey)}
                onRestore={() => page && restoreMutation.mutate(page.primaryKey)}
              />
            )}
          </div>
        </aside>
      </section>

      <ConfirmDialog
        open={Boolean(page && archivePageId === page.primaryKey)}
        title={t("Pages_Wiki_Index_ArchivePage")}
        description={t("Pages_Wiki_Index_DeleteNotice")}
        confirmLabel={t("Pages_Wiki_Index_ConfirmArchive")}
        cancelLabel={t("Pages_Wiki_Index_Cancel")}
        onCancel={() => setArchivePageId(undefined)}
        onConfirm={() => page && archiveMutation.mutate(page.primaryKey)}
      />
    </div>
  );
}

function MobileTab({
  active,
  controls,
  onClick,
  children,
}: {
  active: boolean;
  controls: string;
  onClick(): void;
  children: ReactNode;
}) {
  return (
    <button
      className={active ? "active" : ""}
      type="button"
      role="tab"
      aria-selected={active}
      aria-controls={controls}
      onClick={onClick}
    >
      {children}
    </button>
  );
}

const PanelTab = MobileTab;

function WikiTree({
  node,
  selectedPageId,
  noRevisions,
  onSelect,
}: {
  node: WikiTreeNode;
  selectedPageId: string | undefined;
  noRevisions: string;
  onSelect(): void;
}) {
  return (
    <ul className={node.path ? "wiki-tree wiki-tree--nested" : "wiki-tree"} role="list">
      {node.children.map((child) => (
        <li key={child.path}>
          <span className="wiki-tree__category">{child.name}</span>
          <WikiTree
            node={child}
            selectedPageId={selectedPageId}
            noRevisions={noRevisions}
            onSelect={onSelect}
          />
        </li>
      ))}
      {node.pages.map((page) => (
        <li key={page.primaryKey}>
          <Link
            className={
              page.primaryKey === selectedPageId ? "wiki-tree__page active" : "wiki-tree__page"
            }
            to={`/wiki/${page.primaryKey}`}
            aria-current={page.primaryKey === selectedPageId ? "page" : undefined}
            onClick={onSelect}
          >
            <span>{wikiLeafName(page.name, page.name)}</span>
            <small>{page.lastModified ? formatWikiDate(page.lastModified) : noRevisions}</small>
          </Link>
        </li>
      ))}
    </ul>
  );
}

function WikiHistory({
  pageSelected,
  isLoading,
  error,
  revisions,
  activeRevisionId,
  onSelect,
  onRetry,
}: {
  pageSelected: boolean;
  isLoading: boolean;
  error: unknown;
  revisions: {
    primaryKey: string;
    timeStampCreated: string;
    authorName: string;
    comment: string;
  }[];
  activeRevisionId: string | undefined;
  onSelect(revisionId: string): void;
  onRetry(): void;
}) {
  const { t } = useTranslation();
  return (
    <section className="wiki-intel-section">
      <h2>{t("Pages_Wiki_Index_Tab_History")}</h2>
      {!pageSelected ? (
        <StatePanel kind="empty">{t("Pages_Wiki_Index_SelectPageForRevisions")}</StatePanel>
      ) : isLoading ? (
        <StatePanel kind="loading">{t("Pages_Wiki_Index_LoadingArticle")}</StatePanel>
      ) : error ? (
        <StatePanel
          kind={errorKind(error) === "forbidden" ? "forbidden" : "error"}
          action={
            <button className="button button--ghost button--small" type="button" onClick={onRetry}>
              {t("React_UI_Retry")}
            </button>
          }
        >
          {t("Pages_Wiki_Index_Error_LoadPageFailed")}
        </StatePanel>
      ) : revisions.length === 0 ? (
        <StatePanel kind="empty">{t("Pages_Wiki_Index_NoRevisionsForPage")}</StatePanel>
      ) : (
        <div className="wiki-revisions">
          {revisions.map((revision) => (
            <button
              className={revision.primaryKey === activeRevisionId ? "active" : ""}
              type="button"
              key={revision.primaryKey}
              aria-pressed={revision.primaryKey === activeRevisionId}
              onClick={() => onSelect(revision.primaryKey)}
            >
              <strong>{formatWikiDate(revision.timeStampCreated)}</strong>
              <span>{revision.comment || t("Pages_Wiki_Index_RevisionWithoutComment")}</span>
              <small>{revision.authorName || t("Pages_Wiki_Index_UnknownAuthor")}</small>
            </button>
          ))}
        </div>
      )}
    </section>
  );
}

function WikiDetails({
  page,
  latestRevision,
  relatedPages,
  canDelete,
  canRestore,
  isSaving,
  onSelectRelated,
  onArchive,
  onRestore,
}: {
  page: WikiPage | undefined;
  latestRevision: WikiPageRevision | undefined;
  relatedPages: WikiPageHeader[];
  canDelete: boolean;
  canRestore: boolean;
  isSaving: boolean;
  onSelectRelated(): void;
  onArchive(): void;
  onRestore(): void;
}) {
  const { t } = useTranslation();
  return (
    <section className="wiki-intel-section">
      <h2>{t("Pages_Wiki_Index_ArticleDetailsTitle")}</h2>
      {!page ? (
        <StatePanel kind="empty">{t("Pages_Wiki_Index_SelectPageForDetails")}</StatePanel>
      ) : (
        <>
          <dl className="wiki-details">
            <dt>{t("Pages_Wiki_Index_Detail_Title")}</dt>
            <dd>{page.title}</dd>
            <dt>{t("Pages_Wiki_Index_Detail_Created")}</dt>
            <dd>{formatWikiDate(page.timeStampCreated)}</dd>
            <dt>{t("Pages_Wiki_Index_Detail_LatestRevision")}</dt>
            <dd>{formatWikiDate(latestRevision?.timeStampCreated)}</dd>
            <dt>{t("Pages_Wiki_Index_Detail_Author")}</dt>
            <dd>{latestRevision?.author?.nickname ?? t("Pages_Wiki_Index_UnknownAuthor")}</dd>
            <dt>{t("Pages_Wiki_Index_Detail_Status")}</dt>
            <dd>
              <StatusBadge tone={page.isDeleted ? "warning" : "success"}>
                {page.isDeleted
                  ? t("Pages_Wiki_Index_StatusArchived")
                  : t("Pages_Wiki_Index_StatusVerified")}
              </StatusBadge>
            </dd>
          </dl>
          {relatedPages.length > 0 && (
            <section className="wiki-related">
              <h3>{t("Pages_Wiki_Index_RelatedPages")}</h3>
              {relatedPages.map((relatedPage) => (
                <Link
                  key={relatedPage.primaryKey}
                  to={`/wiki/${relatedPage.primaryKey}`}
                  onClick={onSelectRelated}
                >
                  {wikiLeafName(relatedPage.name, t("Pages_Wiki_Index_UntitledPage"))}
                </Link>
              ))}
            </section>
          )}
          {(canDelete || canRestore) && (
            <section className="wiki-danger-zone">
              <h3>{t("Pages_Wiki_Index_PageState")}</h3>
              {canRestore && (
                <button
                  className="button button--ghost"
                  type="button"
                  disabled={isSaving}
                  onClick={onRestore}
                >
                  {t("Pages_Wiki_Index_RestorePage")}
                </button>
              )}
              {canDelete && (
                <button
                  className="button button--danger"
                  type="button"
                  disabled={isSaving}
                  onClick={onArchive}
                >
                  {t("Pages_Wiki_Index_ArchivePage")}
                </button>
              )}
            </section>
          )}
        </>
      )}
    </section>
  );
}

function WikiEditor({
  editor,
  error,
  isSaving,
  onChange,
  onClose,
  onSave,
}: {
  editor: WikiEditorState | undefined;
  error: string | undefined;
  isSaving: boolean;
  onChange(editor: WikiEditorState): void;
  onClose(): void;
  onSave(): void;
}) {
  const { t } = useTranslation();
  if (!editor)
    return <StatePanel kind="empty">{t("Pages_Wiki_Index_Error_EditorNotOpen")}</StatePanel>;
  const update = (change: Partial<WikiEditorState>) => onChange({ ...editor, ...change });
  return (
    <section className="wiki-intel-section wiki-editor">
      <header>
        <div>
          <h2>
            {editor.mode === "create"
              ? t("Pages_Wiki_Index_NewPage")
              : t("Pages_Wiki_Index_EditArticle")}
          </h2>
          <p>
            {editor.mode === "create"
              ? t("Pages_Wiki_Index_CreateArticleSubtitle")
              : editor.originalTitle || t("Pages_Wiki_Index_SelectedPageFallback")}
          </p>
        </div>
        <button
          className="wiki-editor__close"
          type="button"
          aria-label={t("Pages_Wiki_Index_CloseEditor")}
          onClick={onClose}
        >
          ×
        </button>
      </header>
      <label className="field">
        <span>{t("Pages_Wiki_Index_Editor_TitleLabel")}</span>
        <input value={editor.title} onChange={(event) => update({ title: event.target.value })} />
      </label>
      <label className="field">
        <span>{t("Pages_Wiki_Index_Editor_CommentLabel")}</span>
        <textarea
          rows={3}
          value={editor.comment}
          onChange={(event) => update({ comment: event.target.value })}
        />
      </label>
      <div className="wiki-editor__tabs" role="tablist">
        <PanelTab
          active={!editor.preview}
          controls="wiki-editor-body"
          onClick={() => update({ preview: false })}
        >
          {t("Pages_Wiki_Index_Editor_Tab_Markdown")}
        </PanelTab>
        <PanelTab
          active={editor.preview}
          controls="wiki-editor-body"
          onClick={() => update({ preview: true })}
        >
          {t("Pages_Wiki_Index_Editor_Tab_Preview")}
        </PanelTab>
      </div>
      <div id="wiki-editor-body" className="wiki-editor__body">
        {editor.preview ? (
          <Markdown>
            {editor.markdown.trim()
              ? editor.markdown
              : t("Pages_Wiki_Index_Editor_PreviewPlaceholder")}
          </Markdown>
        ) : (
          <label className="field">
            <span className="sr-only">{t("Pages_Wiki_Index_Editor_Tab_Markdown")}</span>
            <textarea
              className="wiki-editor__markdown"
              rows={18}
              value={editor.markdown}
              onChange={(event) => update({ markdown: event.target.value })}
            />
          </label>
        )}
      </div>
      <div
        className={error ? "wiki-validation wiki-validation--error" : "wiki-validation"}
        role={error ? "alert" : "status"}
      >
        {error ? (
          <Warning20Regular aria-hidden="true" />
        ) : (
          <CheckmarkCircle20Regular aria-hidden="true" />
        )}
        {error ?? t("Pages_Wiki_Index_EditorValidationValid")}
      </div>
      <div className="wiki-editor__actions">
        <button
          className="button button--ghost"
          type="button"
          disabled={isSaving}
          onClick={onClose}
        >
          {t("Pages_Wiki_Index_Cancel")}
        </button>
        <button className="button button--gold" type="button" disabled={isSaving} onClick={onSave}>
          {isSaving ? t("Pages_Wiki_Index_Saving") : t("Pages_Wiki_Index_SaveChanges")}
        </button>
      </div>
    </section>
  );
}

function formatWikiDate(value: string | undefined): string {
  return formatDate(value, {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
