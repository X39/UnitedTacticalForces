import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { ApiError, resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import {
  canCreateFieldReports,
  canDeleteFieldReports,
  canModifyFieldReports,
  canReadFieldReports,
} from "@/auth/claims";
import { NotFoundPage } from "@/components/NotFoundPage";
import { useAppRouteToast, useAppToastOnChange } from "@/components/AppToasts";
import {
  ConfirmDialog,
  Markdown,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import {
  createFieldReportFormSchema,
  createFieldReportSlug,
  createNewFieldReportForm,
  fieldReportToForm,
  isGuid,
  loadCommandFieldReport,
  runFieldReportAction,
  saveCommandFieldReport,
  type CommandFieldReport,
  type FieldReportAction,
  type FieldReportFormValues,
} from "@/features/admin/fieldReports";
import { GalleryAssetPicker } from "@/features/admin/GalleryAssetPicker";
import type { MediaAsset } from "@/features/admin/siteImages";

interface ToastFeedback {
  kind: "success" | "error" | "warning";
  text: string;
}

function fileNameFromPath(path: string): string {
  return decodeURIComponent(path.split("/").filter(Boolean).at(-1) ?? path);
}

function mutationMessage(error: Error, fallback: string, conflict: string): string {
  return error instanceof ApiError && error.code === "conflict"
    ? conflict
    : formatResource(fallback, error.message);
}

function Editor({ reportId }: { reportId?: string }) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isCreate = !reportId;
  const canRead = canReadFieldReports(user);
  const canCreate = canCreateFieldReports(user);
  const canModify = canModifyFieldReports(user);
  const canDelete = canDeleteFieldReports(user);
  const canEdit = isCreate ? canCreate : canModify;
  const canOpen = isCreate ? canCreate : canRead;
  const initialValues = useMemo(
    () => createNewFieldReportForm(t("Pages_Command_FieldReports_Edit_TemplateMarkdown")),
    [t],
  );
  const schema = useMemo(
    () =>
      createFieldReportFormSchema({
        titleRequired: t("Pages_Command_FieldReports_Edit_Validation_TitleRequired"),
        titleTooLong: t("Pages_Command_FieldReports_Edit_Validation_TitleTooLong"),
        slugTooLong: t("Pages_Command_FieldReports_Edit_Validation_SlugTooLong"),
        excerptRequired: t("Pages_Command_FieldReports_Edit_Validation_ExcerptRequired"),
        excerptTooLong: t("Pages_Command_FieldReports_Edit_Validation_ExcerptTooLong"),
        categoryRequired: t("Pages_Command_FieldReports_Edit_Validation_CategoryRequired"),
        categoryTooLong: t("Pages_Command_FieldReports_Edit_Validation_CategoryTooLong"),
        bodyRequired: t("Pages_Command_FieldReports_Edit_Validation_BodyRequired"),
        bodyTooLong: t("Pages_Command_FieldReports_Edit_Validation_BodyTooLong"),
        heroAltTooLong: t("Pages_Command_FieldReports_Edit_Validation_HeroAltTooLong"),
        sortOrderInvalid: t("Pages_Command_FieldReports_Edit_Validation_SortOrderInvalid"),
        publicationDateInvalid: t(
          "Pages_Command_FieldReports_Edit_Validation_PublicationDateInvalid",
        ),
      }),
    [t],
  );
  const query = useQuery({
    queryKey: ["command-field-report", reportId],
    queryFn: ({ signal }) => loadCommandFieldReport(reportId!, signal),
    enabled: Boolean(reportId && canRead),
  });
  const formValues = useMemo(
    () => (query.data ? fieldReportToForm(query.data) : initialValues),
    [initialValues, query.data],
  );
  const form = useForm<FieldReportFormValues>({
    resolver: zodResolver(schema),
    defaultValues: initialValues,
    values: formValues,
    resetOptions: { keepDirtyValues: true },
    mode: "onBlur",
  });
  const [pickerOpen, setPickerOpen] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<MediaAsset>();
  const [previewVisible, setPreviewVisible] = useState(true);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [feedback, setFeedback] = useState<ToastFeedback>();
  const [validationNotice, setValidationNotice] = useState<string>();
  useAppRouteToast(
    canOpen && (location.state as { fieldReportSaved?: boolean } | null)?.fieldReportSaved
      ? t("Pages_Command_FieldReports_Edit_ToastSaved")
      : undefined,
  );
  useAppToastOnChange(feedback?.text, feedback?.kind ?? "success");

  const { errors, isDirty } = form.formState;
  const watchedValues = useWatch({ control: form.control });
  const values: FieldReportFormValues = {
    slug: watchedValues.slug ?? initialValues.slug,
    title: watchedValues.title ?? initialValues.title,
    excerpt: watchedValues.excerpt ?? initialValues.excerpt,
    category: watchedValues.category ?? initialValues.category,
    markdown: watchedValues.markdown ?? initialValues.markdown,
    heroImageAssetFk: watchedValues.heroImageAssetFk ?? initialValues.heroImageAssetFk,
    heroImagePath: watchedValues.heroImagePath ?? initialValues.heroImagePath,
    heroImageAlt: watchedValues.heroImageAlt ?? initialValues.heroImageAlt,
    isFeatured: watchedValues.isFeatured ?? initialValues.isFeatured,
    sortOrder: watchedValues.sortOrder ?? initialValues.sortOrder,
    isPublished: watchedValues.isPublished ?? initialValues.isPublished,
    publicationInput: watchedValues.publicationInput ?? initialValues.publicationInput,
  };
  const applySavedReport = async (report: CommandFieldReport) => {
    form.reset(fieldReportToForm(report));
    setSelectedAsset(undefined);
    queryClient.setQueryData(["command-field-report", report.primaryKey], report);
    await queryClient.invalidateQueries({ queryKey: ["command-field-reports"] });
  };

  const save = useMutation({
    mutationFn: (nextValues: FieldReportFormValues) => saveCommandFieldReport(reportId, nextValues),
    onMutate: () => setFeedback(undefined),
    onSuccess: async (report) => {
      await applySavedReport(report);
      if (!isCreate)
        setFeedback({ kind: "success", text: t("Pages_Command_FieldReports_Edit_ToastSaved") });
    },
    onError: (error) =>
      setFeedback({
        kind: "error",
        text: mutationMessage(
          error,
          t("Pages_Command_FieldReports_Edit_ErrorSave"),
          t("Pages_Command_FieldReports_Edit_ErrorConflict"),
        ),
      }),
  });
  useUnsavedChanges(isDirty && !(isCreate && save.isSuccess));
  useEffect(() => {
    if (!isCreate || !save.isSuccess || !save.data) return;
    navigate(`/command/field-reports/${save.data.primaryKey}`, {
      replace: true,
      state: { fieldReportSaved: true },
    });
  }, [isCreate, navigate, save.data, save.isSuccess]);
  const action = useMutation({
    mutationFn: (operation: FieldReportAction) => runFieldReportAction(reportId!, operation),
    onMutate: () => setFeedback(undefined),
    onSuccess: async (report, operation) => {
      await applySavedReport(report);
      const key =
        operation === "publish"
          ? "Pages_Command_FieldReports_Edit_ToastPublished"
          : operation === "unpublish"
            ? "Pages_Command_FieldReports_Edit_ToastUnpublished"
            : operation === "delete"
              ? "Pages_Command_FieldReports_Edit_ToastDeleted"
              : "Pages_Command_FieldReports_Edit_ToastRestored";
      setFeedback({ kind: "success", text: t(key) });
      setDeleteOpen(false);
    },
    onError: (error, operation) => {
      const key =
        operation === "publish"
          ? "Pages_Command_FieldReports_Edit_ErrorPublish"
          : operation === "unpublish"
            ? "Pages_Command_FieldReports_Edit_ErrorUnpublish"
            : operation === "delete"
              ? "Pages_Command_FieldReports_Edit_ErrorDelete"
              : "Pages_Command_FieldReports_Edit_ErrorRestore";
      setFeedback({
        kind: "error",
        text: mutationMessage(error, t(key), t("Pages_Command_FieldReports_Edit_ErrorConflict")),
      });
    },
  });
  const busy = save.isPending || action.isPending;

  const requestAction = (operation: FieldReportAction) => {
    if (isDirty) {
      setValidationNotice(t("Pages_Command_FieldReports_Edit_UnsavedActionMessage"));
      return;
    }
    setValidationNotice(undefined);
    if (operation === "delete") setDeleteOpen(true);
    else action.mutate(operation);
  };
  const applyAsset = (asset: MediaAsset) => {
    setSelectedAsset(asset);
    form.setValue("heroImageAssetFk", asset.primaryKey, { shouldDirty: true });
    form.setValue("heroImagePath", asset.imagePath, { shouldDirty: true });
    if (!form.getValues("heroImageAlt").trim())
      form.setValue(
        "heroImageAlt",
        fileNameFromPath(asset.originalFileName).replace(/\.[^.]+$/, ""),
        {
          shouldDirty: true,
        },
      );
    setPickerOpen(false);
    setFeedback({
      kind: "success",
      text: t("Pages_Command_FieldReports_Edit_GallerySelectedToast"),
    });
  };
  const clearAsset = () => {
    setSelectedAsset(undefined);
    form.setValue("heroImageAssetFk", "", { shouldDirty: true });
    form.setValue("heroImagePath", "", { shouldDirty: true });
  };
  const title = isCreate
    ? t("Pages_Command_FieldReports_Edit_PageTitleCreate")
    : query.data?.title
      ? formatResource(t("Pages_Command_FieldReports_Edit_PageTitleEditReport"), query.data.title)
      : t("Pages_Command_FieldReports_Edit_PageTitleEdit");
  const imageUrl = resolveImageUrl(values.heroImagePath);
  const fieldError = (message: string | undefined) =>
    message ? <span className="field-error">{message}</span> : null;

  if (!canOpen)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_FieldReports_Edit_NoAccessTitle")}>
        {t("Pages_Command_FieldReports_Edit_NoAccessMessage")}
      </StatePanel>
    );

  return (
    <section className="field-report-editor-page">
      <PageHeader
        kicker={t("Pages_Command_FieldReports_Edit_EditorKicker")}
        title={title}
        description={t("Pages_Command_FieldReports_Edit_HeaderDescription")}
        actions={
          <>
            <Link className="button button--ghost" to="/command/field-reports">
              {t("Pages_Command_FieldReports_Edit_BackToLedger")}
            </Link>
            {canEdit && (
              <button
                className="button button--primary"
                type="submit"
                form="field-report-editor-form"
                disabled={busy}
              >
                {busy
                  ? t("Pages_Command_FieldReports_Edit_Saving")
                  : t("Pages_Command_FieldReports_Edit_Save")}
              </button>
            )}
          </>
        }
      />

      {validationNotice && <StatePanel kind="warning">{validationNotice}</StatePanel>}
      {!isCreate && query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_FieldReports_Edit_Loading")}</StatePanel>
      ) : !isCreate && query.isError ? (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" type="button" onClick={() => query.refetch()}>
              {t("Pages_Command_FieldReports_Refresh")}
            </button>
          }
        >
          {formatResource(t("Pages_Command_FieldReports_Edit_ErrorLoad"), query.error.message)}
        </StatePanel>
      ) : !isCreate && query.isSuccess && !query.data ? (
        <StatePanel kind="empty" title={t("Pages_Command_FieldReports_Edit_ErrorNotFound")}>
          <Link className="button button--ghost" to="/command/field-reports">
            {t("Pages_Command_FieldReports_Edit_BackToLedger")}
          </Link>
        </StatePanel>
      ) : (
        <>
          {!canEdit && (
            <StatePanel kind="info" title={t("Pages_Command_FieldReports_Edit_ReadOnlyTitle")}>
              {t("Pages_Command_FieldReports_Edit_ReadOnlyMessage")}
            </StatePanel>
          )}
          <section className="command-panel field-report-editor-shell">
            <header className="field-report-editor-shell__header">
              <div>
                <span className="kicker">{t("Pages_Command_FieldReports_Edit_EditorKicker")}</span>
                <h2>
                  {values.title.trim() || t("Pages_Command_FieldReports_Edit_FallbackUntitled")}
                </h2>
              </div>
              <StatusBadge
                tone={query.data?.isDeleted ? "danger" : values.isPublished ? "success" : "warning"}
              >
                {query.data?.isDeleted
                  ? t("Pages_Command_FieldReports_Edit_StatusDeleted")
                  : values.isPublished
                    ? t("Pages_Command_FieldReports_Edit_StatusPublished")
                    : t("Pages_Command_FieldReports_Edit_StatusDraft")}
              </StatusBadge>
            </header>

            <form
              id="field-report-editor-form"
              className="field-report-editor-form"
              aria-label={t("Pages_Command_FieldReports_Edit_FormAria")}
              noValidate
              onSubmit={form.handleSubmit((nextValues) => {
                setFeedback(undefined);
                setValidationNotice(undefined);
                save.mutate(nextValues);
              })}
            >
              <label>
                <span>{t("Pages_Command_FieldReports_Edit_LabelTitle")}</span>
                <input
                  {...form.register("title")}
                  maxLength={150}
                  disabled={!canEdit || busy}
                  aria-invalid={Boolean(errors.title)}
                />
                {fieldError(errors.title?.message)}
              </label>
              <label>
                <span>{t("Pages_Command_FieldReports_Edit_LabelSlug")}</span>
                <input
                  {...form.register("slug")}
                  maxLength={120}
                  disabled={!canEdit || busy}
                  aria-invalid={Boolean(errors.slug)}
                  placeholder={
                    values.title.trim()
                      ? createFieldReportSlug(values.title)
                      : t("Pages_Command_FieldReports_Edit_PlaceholderSlug")
                  }
                />
                {fieldError(errors.slug?.message)}
              </label>
              <label className="field-report-editor-form__wide">
                <span>{t("Pages_Command_FieldReports_Edit_LabelExcerpt")}</span>
                <textarea
                  {...form.register("excerpt")}
                  rows={3}
                  maxLength={320}
                  disabled={!canEdit || busy}
                  aria-invalid={Boolean(errors.excerpt)}
                />
                {fieldError(errors.excerpt?.message)}
              </label>
              <div className="field-report-editor-form__grid field-report-editor-form__wide">
                <label>
                  <span>{t("Pages_Command_FieldReports_Edit_LabelCategory")}</span>
                  <input
                    {...form.register("category")}
                    maxLength={64}
                    disabled={!canEdit || busy}
                    aria-invalid={Boolean(errors.category)}
                  />
                  {fieldError(errors.category?.message)}
                </label>
                <label>
                  <span>{t("Pages_Command_FieldReports_Edit_LabelSortOrder")}</span>
                  <input
                    {...form.register("sortOrder", { valueAsNumber: true })}
                    type="number"
                    disabled={!canEdit || busy}
                    aria-invalid={Boolean(errors.sortOrder)}
                  />
                  {fieldError(errors.sortOrder?.message)}
                </label>
              </div>
              <label className="field-report-editor-form__wide">
                <span>{t("Pages_Command_FieldReports_Edit_LabelHeroImageAlt")}</span>
                <input
                  {...form.register("heroImageAlt")}
                  maxLength={200}
                  disabled={!canEdit || busy}
                  aria-invalid={Boolean(errors.heroImageAlt)}
                />
                {fieldError(errors.heroImageAlt?.message)}
              </label>

              <section
                className="field-report-image-panel field-report-editor-form__wide"
                aria-label={t("Pages_Command_FieldReports_Edit_ImageUploadAria")}
              >
                <header>
                  <div>
                    <span className="kicker">
                      {t("Pages_Command_FieldReports_Edit_ImageKicker")}
                    </span>
                    <h3>{t("Pages_Command_FieldReports_Edit_ImageTitle")}</h3>
                  </div>
                  <StatusBadge tone={imageUrl ? "success" : "neutral"}>
                    {imageUrl
                      ? t("Pages_Command_FieldReports_Edit_ImageStatusStored")
                      : t("Pages_Command_FieldReports_Edit_ImageStatusEmpty")}
                  </StatusBadge>
                </header>
                <div className="field-report-image-panel__workspace">
                  <div className="field-report-image-panel__controls">
                    <span className="kicker">
                      {t("Pages_Command_FieldReports_Edit_GalleryKicker")}
                    </span>
                    <h4>{t("Pages_Command_FieldReports_Edit_GalleryTitle")}</h4>
                    <p>
                      {imageUrl
                        ? selectedAsset?.originalFileName || fileNameFromPath(values.heroImagePath)
                        : t("Pages_Command_FieldReports_Edit_ImageNoFile")}
                    </p>
                    <div>
                      {canEdit && (
                        <button
                          className="button button--ghost"
                          type="button"
                          disabled={busy}
                          onClick={() => setPickerOpen(true)}
                        >
                          {t("Pages_Command_FieldReports_Edit_GallerySelectLabel")}
                        </button>
                      )}
                      {canEdit && imageUrl && (
                        <button
                          className="button button--ghost"
                          type="button"
                          disabled={busy}
                          onClick={clearAsset}
                        >
                          {t("Pages_Command_FieldReports_Edit_ImageRemove")}
                        </button>
                      )}
                      <Link className="button button--ghost" to="/command/content/gallery">
                        {t("Pages_Command_FieldReports_Edit_GalleryOpen")}
                      </Link>
                    </div>
                  </div>
                  <div className="field-report-image-panel__preview">
                    {imageUrl ? (
                      <img
                        src={imageUrl}
                        alt={
                          values.heroImageAlt ||
                          t("Pages_Command_FieldReports_Edit_ImagePreviewAria")
                        }
                      />
                    ) : (
                      <span>{t("Pages_Command_FieldReports_Edit_ImagePreviewEmpty")}</span>
                    )}
                    <dl>
                      <dt>{t("Pages_Command_FieldReports_Edit_ImageFile")}</dt>
                      <dd>
                        {imageUrl
                          ? selectedAsset?.originalFileName ||
                            fileNameFromPath(values.heroImagePath)
                          : t("Pages_Command_FieldReports_Edit_ImageNoFile")}
                      </dd>
                      <dt>{t("Pages_Command_FieldReports_Edit_ImageType")}</dt>
                      <dd>
                        {selectedAsset?.contentType ||
                          t("Pages_Command_FieldReports_Edit_ImageUnknownMetadata")}
                      </dd>
                    </dl>
                  </div>
                </div>
              </section>

              <label>
                <span>{t("Pages_Command_FieldReports_Edit_LabelPublicationDate")}</span>
                <input
                  {...form.register("publicationInput")}
                  type="datetime-local"
                  disabled={!canEdit || busy}
                  aria-invalid={Boolean(errors.publicationInput)}
                />
                {fieldError(errors.publicationInput?.message)}
              </label>
              <div className="field-report-editor-form__checks">
                <label>
                  <input
                    {...form.register("isFeatured")}
                    type="checkbox"
                    disabled={!canEdit || busy}
                  />
                  <span>{t("Pages_Command_FieldReports_Edit_LabelFeatured")}</span>
                </label>
                <label>
                  <input
                    {...form.register("isPublished")}
                    type="checkbox"
                    disabled={!canEdit || busy}
                  />
                  <span>{t("Pages_Command_FieldReports_Edit_LabelPublished")}</span>
                </label>
              </div>
              <label className="field-report-editor-form__wide">
                <span>{t("Pages_Command_FieldReports_Edit_LabelMarkdown")}</span>
                <textarea
                  {...form.register("markdown")}
                  rows={20}
                  maxLength={20_000}
                  disabled={!canEdit || busy}
                  aria-invalid={Boolean(errors.markdown)}
                />
                {fieldError(errors.markdown?.message)}
              </label>
            </form>

            <section
              className="field-report-editor-preview"
              aria-label={t("Pages_Command_FieldReports_Edit_PreviewAria")}
            >
              <header>
                <div>
                  <span className="kicker">
                    {t("Pages_Command_FieldReports_Edit_PreviewKicker")}
                  </span>
                  <h3>
                    {previewVisible
                      ? t("Pages_Command_FieldReports_Edit_PreviewVisible")
                      : t("Pages_Command_FieldReports_Edit_PreviewHidden")}
                  </h3>
                </div>
                <button
                  className="button button--ghost"
                  type="button"
                  aria-expanded={previewVisible}
                  onClick={() => setPreviewVisible((current) => !current)}
                >
                  {previewVisible
                    ? t("Pages_Command_FieldReports_Edit_HidePreview")
                    : t("Pages_Command_FieldReports_Edit_ShowPreview")}
                </button>
              </header>
              {previewVisible && (
                <article>
                  <span className="kicker">
                    {t("Pages_Command_FieldReports_Edit_RenderedBodyKicker")}
                  </span>
                  <h3>
                    {values.title.trim() || t("Pages_Command_FieldReports_Edit_FallbackUntitled")}
                  </h3>
                  <p>{values.excerpt}</p>
                  <Markdown>
                    {values.markdown.trim() ||
                      t("Pages_Command_FieldReports_Edit_PlaceholderPreviewMarkdown")}
                  </Markdown>
                </article>
              )}
            </section>

            <footer className="field-report-editor-actions">
              {canEdit && (
                <button
                  className="button button--primary"
                  type="submit"
                  form="field-report-editor-form"
                  disabled={busy}
                >
                  {busy
                    ? t("Pages_Command_FieldReports_Edit_ButtonSaving")
                    : t("Pages_Command_FieldReports_Edit_ButtonSave")}
                </button>
              )}
              {!isCreate && canModify && !query.data?.isPublished && !query.data?.isDeleted && (
                <button
                  className="button button--ghost"
                  type="button"
                  disabled={busy}
                  onClick={() => requestAction("publish")}
                >
                  {t("Pages_Command_FieldReports_Edit_ButtonPublish")}
                </button>
              )}
              {!isCreate && canModify && query.data?.isPublished && !query.data.isDeleted && (
                <button
                  className="button button--ghost"
                  type="button"
                  disabled={busy}
                  onClick={() => requestAction("unpublish")}
                >
                  {t("Pages_Command_FieldReports_Edit_ButtonUnpublish")}
                </button>
              )}
              {!isCreate && canDelete && !query.data?.isDeleted && (
                <button
                  className="button button--danger"
                  type="button"
                  disabled={busy}
                  onClick={() => requestAction("delete")}
                >
                  {t("Pages_Command_FieldReports_Edit_ButtonDelete")}
                </button>
              )}
              {!isCreate && canDelete && query.data?.isDeleted && (
                <button
                  className="button button--ghost"
                  type="button"
                  disabled={busy}
                  onClick={() => requestAction("restore")}
                >
                  {t("Pages_Command_FieldReports_Edit_ButtonRestore")}
                </button>
              )}
            </footer>
          </section>
        </>
      )}

      <GalleryAssetPicker
        open={pickerOpen}
        selectedAssetId={values.heroImageAssetFk}
        uploadTags={["field-report"]}
        onSelect={applyAsset}
        onCancel={() => setPickerOpen(false)}
      />
      <ConfirmDialog
        open={deleteOpen}
        title={t("Pages_Command_FieldReports_Edit_DeleteTitle")}
        description={t("Pages_Command_FieldReports_Edit_DeleteConfirm")}
        confirmLabel={t("Pages_Command_FieldReports_Edit_ButtonDelete")}
        cancelLabel={t("Pages_Command_FieldReports_Edit_ButtonCancel")}
        onConfirm={() => action.mutate("delete")}
        onCancel={() => setDeleteOpen(false)}
      />
    </section>
  );
}

export function Component() {
  const { reportId } = useParams<{ reportId?: string }>();
  if (reportId && !isGuid(reportId)) return <NotFoundPage />;
  return <Editor key={reportId ?? "create"} {...(reportId ? { reportId } : {})} />;
}
