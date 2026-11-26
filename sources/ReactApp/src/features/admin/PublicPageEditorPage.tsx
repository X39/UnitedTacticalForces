import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate, useParams } from "react-router";
import { ApiError, resolveImageUrl } from "@/api/client";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManagePublicPages } from "@/auth/claims";
import { useAppToastOnChange } from "@/components/AppToasts";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { CheckboxField, FieldError, TextAreaField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import {
  ConfirmDialog,
  Markdown,
  PageHeader,
  StatePanel,
  StatusBadge,
} from "@/components/primitives";
import { ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { useLeaveGuard } from "@/components/useLeaveGuard";
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

/**
 * The heading levels the preview moves the page content down. The preview sits below its own `h2`, and the page has
 * exactly one `h1`, so a `# Title` in the content must not become a second `h1` of the editor.
 */
const previewHeadingShift = 2;

/**
 * Moves every heading of the page markdown down for the preview, so the editor keeps one `h1` and the preview
 * headings stay below the preview `h2`.
 *
 * ATX headings (`# Title`) gain {@link previewHeadingShift} marks up to level 6, and a level one setext heading
 * (`Title` over `=====`) becomes an ATX heading of the shifted level. Lines inside fenced code blocks are left
 * alone, so a shell comment in a code sample stays a comment. Only the preview text changes; the saved markdown is
 * never touched.
 *
 * @param markdown The markdown the editor holds.
 * @returns The markdown to render in the preview.
 */
function demotePreviewHeadings(markdown: string): string {
  const output: string[] = [];
  let fence: string | undefined;
  for (const line of markdown.split("\n")) {
    const fenceMarker = /^\s{0,3}(`{3,}|~{3,})/.exec(line)?.[1]?.[0];
    if (fenceMarker !== undefined) {
      fence = fence === undefined ? fenceMarker : fence === fenceMarker ? undefined : fence;
      output.push(line);
      continue;
    }
    if (fence !== undefined) {
      output.push(line);
      continue;
    }
    const previous = output.at(-1);
    if (
      /^\s{0,3}=+\s*$/.test(line) &&
      previous !== undefined &&
      previous.trim() !== "" &&
      !previous.trimStart().startsWith("#")
    ) {
      output[output.length - 1] = `${"#".repeat(1 + previewHeadingShift)} ${previous.trim()}`;
      continue;
    }
    const heading = /^(\s{0,3})(#{1,6})(?=\s|$)/.exec(line);
    if (heading?.[1] === undefined || heading[2] === undefined) {
      output.push(line);
      continue;
    }
    const level = Math.min(6, heading[2].length + previewHeadingShift);
    output.push(`${heading[1]}${"#".repeat(level)}${line.slice(heading[0].length)}`);
  }
  return output.join("\n");
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
      markdown: isGdpr
        ? sourceRevision?.markdown || "## GDPR"
        : t("Pages_Command_Content_PageDefaultMarkdown"),
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
  const leaveGuard = useLeaveGuard({ dirty: dirty && !save.isPending && !savedSlug });
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
    <PageStack>
      <PageHeader
        kicker={t("Pages_Command_Content_PageEditorKicker")}
        title={pageTitle}
        description={t("Pages_Command_Content_PageEditDescription")}
        actions={
          <>
            <ActionLink variant="ghost" to="/command/content">
              {t("Pages_Command_Content_BackToHub")}
            </ActionLink>
            {page && (
              <ActionLink variant="ghost" to={`/command/content/pages/${encodeURIComponent(slug)}`}>
                {t("Pages_Command_Content_Pages_OpenDetail")}
              </ActionLink>
            )}
            <ActionLink variant="ghost" to={`/${encodeURIComponent(slug)}`}>
              {t("Pages_Command_Content_Pages_OpenPublic")}
            </ActionLink>
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
            <TextField label={t("Pages_Command_Content_PageSlug")} value={slug} disabled />
            <TextField
              {...form.register("title")}
              label={t("Pages_Command_Content_PageFormTitle")}
              maxLength={180}
              disabled={save.isPending}
              error={form.formState.errors.title?.message}
            />
            <div className="public-page-editor-fields__wide">
              <TextField
                {...form.register("summary")}
                label={t("Pages_Command_Content_PageFormSummary")}
                maxLength={512}
                disabled={save.isPending}
                error={form.formState.errors.summary?.message}
              />
            </div>
            <TextField
              {...form.register("heroKicker")}
              label={t("Pages_Command_Content_PageFormHeroKicker")}
              maxLength={96}
              disabled={save.isPending}
              error={form.formState.errors.heroKicker?.message}
            />
            <TextField
              {...form.register("heroTitle")}
              label={t("Pages_Command_Content_PageFormHeroTitle")}
              maxLength={180}
              disabled={save.isPending}
              error={form.formState.errors.heroTitle?.message}
            />
            <div className="public-page-editor-fields__wide">
              <TextField
                {...form.register("heroDescription")}
                label={t("Pages_Command_Content_PageFormHeroDescription")}
                maxLength={512}
                disabled={save.isPending}
                error={form.formState.errors.heroDescription?.message}
              />
            </div>
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
                  <ActionButton
                    variant="ghost"
                    disabled={save.isPending}
                    onClick={() => setPickerOpen(true)}
                  >
                    {t("Pages_Command_Content_PageGallerySelectLabel")}
                  </ActionButton>
                  {imageUrl && (
                    <ActionButton
                      variant="ghost"
                      disabled={save.isPending}
                      onClick={clearGalleryAsset}
                    >
                      {t("React_PublicPageEditor_RemoveImage")}
                    </ActionButton>
                  )}
                  <ActionLink variant="ghost" to="/command/content/gallery">
                    {t("Pages_Command_Content_PageGalleryOpen")}
                  </ActionLink>
                </div>
                <div className="public-page-editor-gallery__preview">
                  {imageUrl ? (
                    <img src={imageUrl} alt={t("React_PublicPageEditor_ImagePreviewAlt")} />
                  ) : (
                    <span>{t("React_PublicPageEditor_GalleryEmpty")}</span>
                  )}
                </div>
              </div>
              {form.formState.errors.heroImageAssetFk?.message && (
                <FieldError
                  id="public-page-hero-image-error"
                  message={form.formState.errors.heroImageAssetFk.message}
                />
              )}
            </section>

            {isGdpr ? (
              <div className="public-page-editor-fields__wide">
                <StatePanel kind="info" title={t("Pages_Command_Content_Gdpr_TemplateNoticeTitle")}>
                  {t("Pages_Command_Content_Gdpr_TemplateNoticeBody")}
                </StatePanel>
              </div>
            ) : (
              <div className="public-page-editor-fields__wide">
                <TextAreaField
                  {...form.register("markdown")}
                  label={t("Pages_Command_Content_PageFormMarkdown")}
                  rows={18}
                  maxLength={60_000}
                  disabled={save.isPending}
                  error={form.formState.errors.markdown?.message}
                />
              </div>
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
            <TextField
              {...form.register("comment")}
              label={t("Pages_Command_Content_PageFormRevisionComment")}
              maxLength={512}
              disabled={save.isPending}
              error={form.formState.errors.comment?.message}
            />
            <CheckboxField
              {...form.register("publish")}
              label={t("Pages_Command_Content_PageFormPublish")}
              disabled={save.isPending}
            />
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
              <ActionButton variant="primary" type="submit" disabled={save.isPending}>
                {t(
                  save.isPending
                    ? "Pages_Command_Content_PageFormSaving"
                    : "Pages_Command_Content_PageSaveRevision",
                )}
              </ActionButton>
              <ActionLink variant="ghost" to={`/command/content/pages/${encodeURIComponent(slug)}`}>
                {t("Pages_Command_Content_PageEditorLeave_Dismiss")}
              </ActionLink>
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
              <Markdown>
                {values.markdown
                  ? demotePreviewHeadings(values.markdown)
                  : t("Pages_Command_Content_PagePreviewEmpty")}
              </Markdown>
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
              <ActionLink variant="ghost" to="/command/content">
                {t("Pages_Command_Content_BackToHub")}
              </ActionLink>
              <ActionLink variant="ghost" to={`/command/content/pages/${encodeURIComponent(slug)}`}>
                {t("Pages_Command_Content_PageEditorLeave_Dismiss")}
              </ActionLink>
              <ActionButton
                variant="gold"
                type="submit"
                form="public-page-editor-form"
                disabled={save.isPending}
              >
                {t(
                  save.isPending
                    ? "Pages_Command_Content_PageFormSaving"
                    : "Pages_Command_Content_PageSaveRevision",
                )}
              </ActionButton>
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
      <ConfirmDialog
        open={leaveGuard.blocked}
        title={t("Server_Alerts_Leave_Title")}
        description={t("React_UnsavedChanges")}
        cancelLabel={t("Server_Change_KeepEditing")}
        confirmLabel={t("Pages_Command_Content_PageEditorLeave_Dismiss")}
        initialFocus="dismiss"
        onCancel={leaveGuard.stay}
        onConfirm={leaveGuard.proceed}
      />
    </PageStack>
  );
}

export function Component() {
  const { t } = useTranslation();
  const { slug = "gdpr" } = useParams();
  const { user } = useAuth();
  const canManagePages = canManagePublicPages(user);
  const query = useQuery({
    queryKey: ["command-center", "public-pages", slug],
    // A missing page is a valid answer; TanStack Query rejects `undefined` data, so it is carried as `null`.
    queryFn: async ({ signal }) => (await loadAdminPublicPage(slug, signal)) ?? null,
    enabled: canManagePages,
    retry: false,
  });

  const stateShell = (state: ReactNode) => (
    <PageStack>
      <PageHeader
        kicker={t("Pages_Command_Content_PageEditorKicker")}
        title={formatResource(t("Pages_Command_Content_PageEditTitleWithSlug"), slug)}
      />
      {state}
    </PageStack>
  );

  if (!canManagePages)
    return stateShell(
      <ForbiddenState title={t("Pages_Command_Content_Pages_AccessRequiredTitle")}>
        {t("Pages_Command_Content_Pages_AccessRequiredDescription")}
      </ForbiddenState>,
    );
  if (query.isPending)
    return stateShell(<LoadingState label={t("Pages_Command_Content_Pages_Loading")} />);
  if (query.isError)
    return stateShell(
      <ErrorState onRetry={() => void query.refetch()}>
        {formatResource(t("Pages_Command_Content_Pages_LoadError"), query.error.message)}
      </ErrorState>,
    );
  return (
    <EditorWorkspace
      key={`${slug}:${query.data?.timeStampUpdated ?? "new"}`}
      slug={slug}
      page={query.data ?? undefined}
    />
  );
}
