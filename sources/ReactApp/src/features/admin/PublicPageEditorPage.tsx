import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams } from "react-router";
import { ApiError, resolveImageUrl } from "@/api/client";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManagePublicPages } from "@/auth/claims";
import { useAppToastOnChange } from "@/components/AppToasts";
import {
  Markdown,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { GalleryAssetPicker } from "@/features/admin/GalleryAssetPicker";
import { GdprContentEditor } from "@/features/admin/GdprContentEditor";
import { serializeGdprContent } from "@/features/admin/gdprContent";
import {
  createPublicPageRevision,
  loadAdminPublicPage,
  type PublicPage,
  type PublicPageRevisionPayload,
} from "@/features/admin/publicPages";
import {
  activePublicPageRevision,
  createGdprEditorState,
  createPublicPageEditorSchema,
  createPublicPageEditorValues,
  createPublicPageRevisionPayload,
  maximumStructuredContentLength,
  type PublicPageEditorValues,
} from "@/features/admin/publicPageEditor";
import type { MediaAsset } from "@/features/admin/siteImages";

interface ToastFeedback {
  kind: "error" | "success" | "warning";
  text: string;
}

function fileNameFromPath(path: string): string {
  return decodeURIComponent(path.split("/").filter(Boolean).at(-1) ?? path);
}

function EditorWorkspace({ slug, page }: { slug: string; page: PublicPage | undefined }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isGdpr = slug.toLocaleLowerCase() === "gdpr";
  const sourceRevision = activePublicPageRevision(page);
  const defaults = useMemo(
    () => ({
      title: isGdpr
        ? t("Pages_Public_GDPR_PageTitle")
        : formatResource(t("Pages_Command_Content_Pages_FallbackTitle"), slug),
      summary: t("Pages_Command_Content_PageDefaultSummary"),
      heroKicker: t("Pages_Public_GDPR_Hero_Kicker"),
      heroTitle: t("Pages_Public_GDPR_Hero_Title"),
      heroDescription: t("Pages_Public_GDPR_Hero_Description"),
      markdown: isGdpr ? sourceRevision?.markdown || "# GDPR" : "# New public page",
    }),
    [isGdpr, slug, sourceRevision?.markdown, t],
  );
  const initialValues = useMemo(
    () => createPublicPageEditorValues(page, defaults),
    [defaults, page],
  );
  const schema = useMemo(
    () =>
      createPublicPageEditorSchema({
        title: t("Pages_Command_Content_PageValidationTitle"),
        summary: t("Pages_Command_Content_PageValidationSummary"),
        heroKicker: t("Pages_Command_Content_PageValidationHeroKicker"),
        heroTitle: t("Pages_Command_Content_PageValidationHeroTitle"),
        heroDescription: t("Pages_Command_Content_PageValidationHeroDescription"),
        heroImage: t("React_PublicPageEditor_HeroImageRequired"),
        markdown: t("Pages_Command_Content_PageValidationMarkdown"),
        comment: t("Pages_Command_Content_PageValidationComment"),
      }),
    [t],
  );
  const form = useForm<PublicPageEditorValues>({
    resolver: zodResolver(schema),
    defaultValues: initialValues,
    mode: "onBlur",
  });
  const initialGdprState = useMemo(() => createGdprEditorState(sourceRevision), [sourceRevision]);
  const initialGdprSnapshot = useMemo(
    () =>
      `${Number(initialGdprState.preserveLegacyMarkdown)}:${serializeGdprContent(initialGdprState.content)}`,
    [initialGdprState],
  );
  const [gdprState, setGdprState] = useState(initialGdprState);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<MediaAsset>();
  const [structuredInvalid, setStructuredInvalid] = useState(false);
  const [feedback, setFeedback] = useState<ToastFeedback>();
  const [validationNotice, setValidationNotice] = useState<string>();
  const [savedSlug, setSavedSlug] = useState<string>();
  const values = useWatch({ control: form.control });
  const currentGdprSnapshot = `${Number(gdprState.preserveLegacyMarkdown)}:${serializeGdprContent(gdprState.content)}`;
  const dirty = form.formState.isDirty || (isGdpr && currentGdprSnapshot !== initialGdprSnapshot);

  const save = useMutation({
    mutationFn: (payload: PublicPageRevisionPayload) => createPublicPageRevision(slug, payload),
    onMutate: () => setFeedback(undefined),
    onSuccess: async (savedPage) => {
      form.reset(createPublicPageEditorValues(savedPage, defaults));
      queryClient.setQueryData(["command-center", "public-pages", slug], savedPage);
      await queryClient.invalidateQueries({
        queryKey: ["command-center", "public-pages"],
        exact: true,
      });
      setSavedSlug(savedPage.slug);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.code === "conflict") {
        setFeedback({ kind: "warning", text: t("React_PublicPageEditor_Conflict") });
        return;
      }
      if (error instanceof ApiError && error.code === "not-found") {
        setFeedback({ kind: "error", text: t("React_PublicPageEditor_NotFound") });
        return;
      }
      setFeedback({
        kind: "error",
        text: formatResource(t("React_PublicPageEditor_SaveError"), error.message),
      });
    },
  });
  useAppToastOnChange(feedback?.text, feedback?.kind ?? "success");
  useUnsavedChanges(dirty && !save.isPending && !savedSlug);
  useEffect(() => {
    if (!savedSlug) return;
    navigate(`/command/content/pages/${encodeURIComponent(savedSlug)}`, {
      replace: true,
      state: { pageSaved: true },
    });
  }, [navigate, savedSlug]);

  const nextVersion =
    Math.max(0, ...(page?.revisions.map((revision) => revision.version) ?? [])) + 1;
  const nextRevisionLabel = formatResource(
    t("Pages_Command_Content_PageNextRevision"),
    nextVersion,
  );
  const sourceRevisionLabel = sourceRevision
    ? formatResource(t("Pages_Command_Content_PageRevisionVersion"), sourceRevision.version)
    : t("Pages_Command_Content_PageEditorNoSourceRevision");
  const pageTitle = page
    ? formatResource(
        t("Pages_Command_Content_PageEditTitleWithPage"),
        sourceRevision?.title || defaults.title,
      )
    : formatResource(t("Pages_Command_Content_PageEditTitleWithSlug"), slug);
  const imageUrl = resolveImageUrl(values.heroImagePath);
  const fieldError = (message: string | undefined) =>
    message ? <span className="field-error">{message}</span> : null;
  const setGalleryAsset = (asset: MediaAsset) => {
    setSelectedAsset(asset);
    form.setValue("heroImageAssetFk", asset.primaryKey, {
      shouldDirty: true,
      shouldValidate: true,
    });
    form.setValue("heroImagePath", asset.imagePath, { shouldDirty: true });
    setPickerOpen(false);
    setFeedback({
      kind: "success",
      text: t("Pages_Command_Content_PageGallerySelectedToast"),
    });
  };
  const clearGalleryAsset = () => {
    setSelectedAsset(undefined);
    form.setValue("heroImageAssetFk", "", { shouldDirty: true, shouldValidate: true });
    form.setValue("heroImagePath", "", { shouldDirty: true });
  };
  const submit = form.handleSubmit((nextValues) => {
    setFeedback(undefined);
    setValidationNotice(undefined);
    setStructuredInvalid(false);
    const payload = createPublicPageRevisionPayload(nextValues, isGdpr, gdprState, sourceRevision);
    if (isGdpr && gdprState.preserveLegacyMarkdown) {
      setStructuredInvalid(true);
      setValidationNotice(t("Pages_Command_Content_Gdpr_LegacyMarkdownSaveBlocked"));
      document.getElementById("public-page-structured-editor")?.focus();
      return;
    }
    if (!payload.markdown?.trim() || payload.markdown.trim().length > 60_000) {
      setStructuredInvalid(isGdpr);
      setValidationNotice(t("Pages_Command_Content_PageValidationMarkdown"));
      if (isGdpr) document.getElementById("public-page-structured-editor")?.focus();
      else form.setFocus("markdown");
      return;
    }
    if ((payload.contentJson?.length ?? 0) > maximumStructuredContentLength) {
      setStructuredInvalid(true);
      setValidationNotice(t("Pages_Command_Content_Gdpr_ContentValidationTooLarge"));
      document.getElementById("public-page-structured-editor")?.focus();
      return;
    }
    save.mutate(payload);
  });

  return (
    <section className="public-page-editor-page">
      <PageHeader
        kicker={t("Pages_Command_Content_PageEditorKicker")}
        title={pageTitle}
        description={t("Pages_Command_Content_PageEditDescription")}
        actions={
          <>
            <Link className="button button--ghost" to="/command/content">
              {t("Pages_Command_Content_BackToHub")}
            </Link>
            {page && (
              <Link
                className="button button--ghost"
                to={`/command/content/pages/${encodeURIComponent(slug)}`}
              >
                {t("Pages_Command_Content_Pages_OpenDetail")}
              </Link>
            )}
            <Link className="button button--ghost" to={`/${encodeURIComponent(slug)}`}>
              {t("Pages_Command_Content_Pages_OpenPublic")}
            </Link>
          </>
        }
      />

      {validationNotice && <StatePanel kind="error">{validationNotice}</StatePanel>}

      <section
        className="command-panel public-page-editor-identity"
        aria-label={t("Pages_Command_Content_PageEditorIdentityAria")}
      >
        <div>
          <span className="kicker">{t("Pages_Command_Content_PageEditorIdentityKicker")}</span>
          <h2>{sourceRevision?.title || defaults.title}</h2>
          <p>
            {t(
              isGdpr
                ? "Pages_Command_Content_Gdpr_EditorIdentityDescription"
                : "Pages_Command_Content_PageEditorIdentityDescription",
            )}
          </p>
        </div>
        <dl>
          <div>
            <dt>{t("Pages_Command_Content_PageSlug")}</dt>
            <dd>{slug}</dd>
          </div>
          <div>
            <dt>{t("Pages_Command_Content_PageEditorSourceRevision")}</dt>
            <dd>{sourceRevisionLabel}</dd>
          </div>
          <div>
            <dt>{t("Pages_Command_Content_PageEditorWorkingRevision")}</dt>
            <dd>{nextRevisionLabel}</dd>
          </div>
          <div>
            <dt>{t("Pages_Command_Content_PageEditorUpdated")}</dt>
            <dd>
              {page?.timeStampUpdated
                ? formatDateTime(page.timeStampUpdated)
                : t("Pages_Command_Content_PageEditorNeverSaved")}
            </dd>
          </div>
        </dl>
      </section>

      <div className="public-page-editor-status" aria-live="polite">
        <StatusBadge tone={dirty ? "warning" : "success"}>
          {t(
            dirty
              ? "Pages_Command_Content_PageEditorDirty"
              : "Pages_Command_Content_PageEditorClean",
          )}
        </StatusBadge>
        <StatusBadge tone={values.publish ? "success" : "neutral"}>
          {t(
            values.publish
              ? "Pages_Command_Content_PageEditorPublishOnSave"
              : "Pages_Command_Content_PageEditorSaveAsDraft",
          )}
        </StatusBadge>
      </div>

      <form
        id="public-page-editor-form"
        className="public-page-editor-shell"
        noValidate
        onSubmit={submit}
      >
        <section className="command-panel public-page-editor-metadata">
          <header className="command-panel__header">
            <div>
              <span className="kicker">{t("Pages_Command_Content_PageEditorKicker")}</span>
              <h2>{t("Pages_Command_Content_PageEditorMetadataTitle")}</h2>
              <p>{t("Pages_Command_Content_PageEditorMetadataDescription")}</p>
            </div>
          </header>
          <div className="public-page-editor-fields">
            <label>
              <span>{t("Pages_Command_Content_PageSlug")}</span>
              <input value={slug} disabled />
            </label>
            <label>
              <span>{t("Pages_Command_Content_PageFormTitle")}</span>
              <input
                {...form.register("title")}
                maxLength={180}
                disabled={save.isPending}
                aria-invalid={Boolean(form.formState.errors.title)}
              />
              {fieldError(form.formState.errors.title?.message)}
            </label>
            <label className="public-page-editor-fields__wide">
              <span>{t("Pages_Command_Content_PageFormSummary")}</span>
              <input
                {...form.register("summary")}
                maxLength={512}
                disabled={save.isPending}
                aria-invalid={Boolean(form.formState.errors.summary)}
              />
              {fieldError(form.formState.errors.summary?.message)}
            </label>
            <label>
              <span>{t("Pages_Command_Content_PageFormHeroKicker")}</span>
              <input
                {...form.register("heroKicker")}
                maxLength={96}
                disabled={save.isPending}
                aria-invalid={Boolean(form.formState.errors.heroKicker)}
              />
              {fieldError(form.formState.errors.heroKicker?.message)}
            </label>
            <label>
              <span>{t("Pages_Command_Content_PageFormHeroTitle")}</span>
              <input
                {...form.register("heroTitle")}
                maxLength={180}
                disabled={save.isPending}
                aria-invalid={Boolean(form.formState.errors.heroTitle)}
              />
              {fieldError(form.formState.errors.heroTitle?.message)}
            </label>
            <label className="public-page-editor-fields__wide">
              <span>{t("Pages_Command_Content_PageFormHeroDescription")}</span>
              <input
                {...form.register("heroDescription")}
                maxLength={512}
                disabled={save.isPending}
                aria-invalid={Boolean(form.formState.errors.heroDescription)}
              />
              {fieldError(form.formState.errors.heroDescription?.message)}
            </label>
            <section
              className="public-page-editor-gallery public-page-editor-fields__wide"
              aria-label={t("Pages_Command_Content_PageGalleryAria")}
              aria-invalid={Boolean(form.formState.errors.heroImageAssetFk)}
            >
              <header>
                <div>
                  <span className="kicker">{t("Pages_Command_Content_PageGalleryKicker")}</span>
                  <h3>{t("Pages_Command_Content_PageGalleryTitle")}</h3>
                  <p>
                    {selectedAsset?.originalFileName ||
                      (values.heroImagePath
                        ? fileNameFromPath(values.heroImagePath)
                        : t("React_PublicPageEditor_GalleryEmpty"))}
                  </p>
                </div>
                <StatusBadge tone={imageUrl ? "success" : "warning"}>
                  {imageUrl
                    ? t("Components_GalleryImagePicker_SelectionKicker")
                    : t("Components_GalleryImagePicker_NoSelectionTitle")}
                </StatusBadge>
              </header>
              <div className="public-page-editor-gallery__workspace">
                <div className="public-page-editor-gallery__actions">
                  <button
                    className="button button--ghost"
                    type="button"
                    disabled={save.isPending}
                    onClick={() => setPickerOpen(true)}
                  >
                    {t("Pages_Command_Content_PageGallerySelectLabel")}
                  </button>
                  {imageUrl && (
                    <button
                      className="button button--ghost"
                      type="button"
                      disabled={save.isPending}
                      onClick={clearGalleryAsset}
                    >
                      {t("React_PublicPageEditor_RemoveImage")}
                    </button>
                  )}
                  <Link className="button button--ghost" to="/command/content/gallery">
                    {t("Pages_Command_Content_PageGalleryOpen")}
                  </Link>
                </div>
                <div className="public-page-editor-gallery__preview">
                  {imageUrl ? (
                    <img src={imageUrl} alt={t("React_PublicPageEditor_ImagePreviewAlt")} />
                  ) : (
                    <span>{t("React_PublicPageEditor_GalleryEmpty")}</span>
                  )}
                </div>
              </div>
              {fieldError(form.formState.errors.heroImageAssetFk?.message)}
            </section>

            {isGdpr ? (
              <div className="state-panel state-panel--info public-page-editor-fields__wide">
                <div>
                  <h3>{t("Pages_Command_Content_Gdpr_TemplateNoticeTitle")}</h3>
                  <p>{t("Pages_Command_Content_Gdpr_TemplateNoticeBody")}</p>
                </div>
              </div>
            ) : (
              <label className="public-page-editor-fields__wide">
                <span>{t("Pages_Command_Content_PageFormMarkdown")}</span>
                <textarea
                  {...form.register("markdown")}
                  rows={18}
                  maxLength={60_000}
                  disabled={save.isPending}
                  aria-invalid={Boolean(form.formState.errors.markdown)}
                />
                {fieldError(form.formState.errors.markdown?.message)}
              </label>
            )}
          </div>
        </section>

        <aside className="command-panel public-page-editor-revision">
          <header className="command-panel__header">
            <div>
              <span className="kicker">
                {t("Pages_Command_Content_PageEditorPublicationKicker")}
              </span>
              <h2>{t("Pages_Command_Content_PageEditorPublicationTitle")}</h2>
              <p>{t("Pages_Command_Content_PageEditorPublicationDescription")}</p>
            </div>
          </header>
          <div className="public-page-editor-revision__body">
            <label>
              <span>{t("Pages_Command_Content_PageFormRevisionComment")}</span>
              <input
                {...form.register("comment")}
                maxLength={512}
                disabled={save.isPending}
                aria-invalid={Boolean(form.formState.errors.comment)}
              />
              {fieldError(form.formState.errors.comment?.message)}
            </label>
            <label className="public-page-editor-check">
              <input {...form.register("publish")} type="checkbox" disabled={save.isPending} />
              <span>{t("Pages_Command_Content_PageFormPublish")}</span>
            </label>
            <div className="public-page-editor-revision__context">
              <StatusBadge tone={dirty ? "warning" : "success"}>
                {t(
                  dirty
                    ? "Pages_Command_Content_PageEditorDirty"
                    : "Pages_Command_Content_PageEditorClean",
                )}
              </StatusBadge>
              <p>{t("Pages_Command_Content_PageEditorDirtyHelp")}</p>
            </div>
            <div className="public-page-editor-actions">
              <button className="button button--primary" type="submit" disabled={save.isPending}>
                {t(
                  save.isPending
                    ? "Pages_Command_Content_PageFormSaving"
                    : "Pages_Command_Content_PageSaveRevision",
                )}
              </button>
              <Link
                className="button button--ghost"
                to={`/command/content/pages/${encodeURIComponent(slug)}`}
              >
                {t("Common_Cancel")}
              </Link>
            </div>
          </div>
        </aside>

        {!isGdpr && (
          <aside className="command-panel public-page-editor-markdown-preview">
            <header className="command-panel__header">
              <div>
                <span className="kicker">{t("Pages_Command_Content_PagePreviewKicker")}</span>
                <h2>{t("Pages_Command_Content_PagePreviewTitle")}</h2>
              </div>
              <StatusBadge>{nextRevisionLabel}</StatusBadge>
            </header>
            <div>
              <Markdown>{values.markdown || t("Pages_Command_Content_PagePreviewEmpty")}</Markdown>
            </div>
          </aside>
        )}
      </form>

      {isGdpr && (
        <>
          <GdprContentEditor
            content={gdprState.content}
            revisionLabel={nextRevisionLabel}
            invalid={structuredInvalid}
            onChange={(content) => {
              setStructuredInvalid(false);
              setGdprState({ content, preserveLegacyMarkdown: false });
            }}
          />
          <section
            className="command-panel public-page-editor-bottom-actions"
            aria-label={t("Pages_Command_Content_PageEditorActionsAria")}
          >
            <StatusBadge tone={dirty ? "warning" : "success"}>
              {t(
                dirty
                  ? "Pages_Command_Content_PageEditorDirty"
                  : "Pages_Command_Content_PageEditorClean",
              )}
            </StatusBadge>
            <div>
              <Link className="button button--ghost" to="/command/content">
                {t("Pages_Command_Content_BackToHub")}
              </Link>
              <Link
                className="button button--ghost"
                to={`/command/content/pages/${encodeURIComponent(slug)}`}
              >
                {t("Common_Cancel")}
              </Link>
              <button
                className="button button--primary"
                type="submit"
                form="public-page-editor-form"
                disabled={save.isPending}
              >
                {t(
                  save.isPending
                    ? "Pages_Command_Content_PageFormSaving"
                    : "Pages_Command_Content_PageSaveRevision",
                )}
              </button>
            </div>
          </section>
        </>
      )}

      <GalleryAssetPicker
        open={pickerOpen}
        selectedAssetId={values.heroImageAssetFk ?? ""}
        uploadTags={["public-page"]}
        onSelect={setGalleryAsset}
        onCancel={() => setPickerOpen(false)}
      />
    </section>
  );
}

export function Component() {
  const { t } = useTranslation();
  const { slug = "gdpr" } = useParams();
  const { user } = useAuth();
  const canManagePages = canManagePublicPages(user);
  const query = useQuery({
    queryKey: ["command-center", "public-pages", slug],
    queryFn: ({ signal }) => loadAdminPublicPage(slug, signal),
    enabled: canManagePages,
    retry: false,
  });

  if (!canManagePages)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Content_Pages_AccessRequiredTitle")}>
        {t("Pages_Command_Content_Pages_AccessRequiredDescription")}
      </StatePanel>
    );
  if (query.isPending)
    return <StatePanel kind="loading">{t("Pages_Command_Content_Pages_Loading")}</StatePanel>;
  if (query.isError)
    return (
      <StatePanel
        kind="error"
        action={
          <button className="button button--ghost" type="button" onClick={() => query.refetch()}>
            {t("Common_Refresh")}
          </button>
        }
      >
        {formatResource(t("Pages_Command_Content_Pages_LoadError"), query.error.message)}
      </StatePanel>
    );
  return (
    <EditorWorkspace
      key={`${slug}:${query.data?.timeStampUpdated ?? "new"}`}
      slug={slug}
      page={query.data}
    />
  );
}
