import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useId, useMemo, useState, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useLocation, useNavigate, useParams } from "react-router";
import { ApiError, resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import {
  canCreateFieldReports,
  canDeleteFieldReports,
  canModifyFieldReports,
  canReadFieldReports,
} from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { NotFoundPage } from "@/components/NotFoundPage";
import { useAppRouteToast, useAppToastOnChange } from "@/components/AppToasts";
import { CheckboxField, TextAreaField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import {
  ConfirmDialog,
  Markdown,
  PageHeader,
  StatePanel,
  StatusBadge,
} from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { reportTones, toneOf } from "@/components/statusTones";
import { useLeaveGuard } from "@/components/useLeaveGuard";
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
import "@/features/admin/mediaAdmin.css";

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

/** Renders the page header above a state view so every state of the editor route keeps exactly one `h1`. */
function EditorStateFrame({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <PageStack>
      <PageHeader title={t("Shared_CommandCenter_Nav_FieldReports")} />
      {children}
    </PageStack>
  );
}

function Editor({ reportId }: { reportId?: string }) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const previewId = useId();
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
    // A missing report resolves `undefined`, which a query cannot hold, so it maps to `null` and reaches the
    // not-found state instead of surfacing as a query error.
    queryFn: async ({ signal }) => (await loadCommandFieldReport(reportId!, signal)) ?? null,
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
  const leaveGuard = useLeaveGuard({ dirty: isDirty && !(isCreate && save.isSuccess) });
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
  const reportState = query.data?.isDeleted
    ? "deleted"
    : values.isPublished
      ? "published"
      : "draft";
  const showForm = isCreate || (query.isSuccess && Boolean(query.data));

  if (!canOpen)
    return (
      <EditorStateFrame>
        <ForbiddenState title={t("Pages_Command_FieldReports_Edit_NoAccessTitle")}>
          {t("Pages_Command_FieldReports_Edit_NoAccessMessage")}
        </ForbiddenState>
      </EditorStateFrame>
    );

  return (
    <PageStack>
      <PageHeader
        kicker={t("Pages_Command_FieldReports_Edit_EditorKicker")}
        title={title}
        description={t("Pages_Command_FieldReports_Edit_HeaderDescription")}
        actions={
          <>
            <ActionLink variant="ghost" to="/command/field-reports">
              {t("Pages_Command_FieldReports_Edit_BackToLedger")}
            </ActionLink>
            {canEdit && showForm && (
              <ActionButton
                variant="gold"
                type="submit"
                form="field-report-editor-form"
                disabled={busy}
              >
                {busy
                  ? t("Pages_Command_FieldReports_Edit_Saving")
                  : t("Pages_Command_FieldReports_Edit_Save")}
              </ActionButton>
            )}
          </>
        }
      />

      {validationNotice && <StatePanel kind="warning">{validationNotice}</StatePanel>}
      {!isCreate && query.isPending ? (
        <LoadingState label={t("Pages_Command_FieldReports_Edit_Loading")} />
      ) : !isCreate && query.isError ? (
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("Pages_Command_FieldReports_Edit_ErrorLoad"), query.error.message)}
        </ErrorState>
      ) : !isCreate && query.isSuccess && !query.data ? (
        <EmptyState title={t("Pages_Command_FieldReports_Edit_ErrorNotFound")}>
          <ActionLink variant="ghost" to="/command/field-reports">
            {t("Pages_Command_FieldReports_Edit_BackToLedger")}
          </ActionLink>
        </EmptyState>
      ) : (
        <>
          {!canEdit && (
            <StatePanel kind="info" title={t("Pages_Command_FieldReports_Edit_ReadOnlyTitle")}>
              {t("Pages_Command_FieldReports_Edit_ReadOnlyMessage")}
            </StatePanel>
          )}
          <section className="command-panel report-editor-shell">
            <header className="report-editor-shell__header">
              <div>
                <span className="kicker">{t("Pages_Command_FieldReports_Edit_EditorKicker")}</span>
                <h2>
                  {values.title.trim() || t("Pages_Command_FieldReports_Edit_FallbackUntitled")}
                </h2>
              </div>
              <StatusBadge tone={toneOf(reportTones, reportState)}>
                {reportState === "deleted"
                  ? t("Pages_Command_FieldReports_Edit_StatusDeleted")
                  : reportState === "published"
                    ? t("Pages_Command_FieldReports_Edit_StatusPublished")
                    : t("Pages_Command_FieldReports_Edit_StatusDraft")}
              </StatusBadge>
            </header>

            <form
              id="field-report-editor-form"
              className="report-editor-form"
              aria-label={t("Pages_Command_FieldReports_Edit_FormAria")}
              noValidate
              onSubmit={form.handleSubmit((nextValues) => {
                setFeedback(undefined);
                setValidationNotice(undefined);
                save.mutate(nextValues);
              })}
            >
              <TextField
                {...form.register("title")}
                label={t("Pages_Command_FieldReports_Edit_LabelTitle")}
                maxLength={150}
                disabled={!canEdit || busy}
                error={errors.title?.message}
              />
              <TextField
                {...form.register("slug")}
                label={t("Pages_Command_FieldReports_Edit_LabelSlug")}
                maxLength={120}
                disabled={!canEdit || busy}
                error={errors.slug?.message}
                placeholder={
                  values.title.trim()
                    ? createFieldReportSlug(values.title)
                    : t("Pages_Command_FieldReports_Edit_PlaceholderSlug")
                }
              />
              <div className="report-editor-form__wide">
                <TextAreaField
                  {...form.register("excerpt")}
                  label={t("Pages_Command_FieldReports_Edit_LabelExcerpt")}
                  rows={3}
                  maxLength={320}
                  disabled={!canEdit || busy}
                  error={errors.excerpt?.message}
                />
              </div>
              <div className="report-editor-form__grid report-editor-form__wide">
                <TextField
                  {...form.register("category")}
                  label={t("Pages_Command_FieldReports_Edit_LabelCategory")}
                  maxLength={64}
                  disabled={!canEdit || busy}
                  error={errors.category?.message}
                />
                <TextField
                  {...form.register("sortOrder", { valueAsNumber: true })}
                  label={t("Pages_Command_FieldReports_Edit_LabelSortOrder")}
                  type="number"
                  disabled={!canEdit || busy}
                  error={errors.sortOrder?.message}
                />
              </div>
              <div className="report-editor-form__wide">
                <TextField
                  {...form.register("heroImageAlt")}
                  label={t("Pages_Command_FieldReports_Edit_LabelHeroImageAlt")}
                  maxLength={200}
                  disabled={!canEdit || busy}
                  error={errors.heroImageAlt?.message}
                />
              </div>

              <section
                className="report-image-panel report-editor-form__wide"
                aria-label={t("Pages_Command_FieldReports_Edit_ImageUploadAria")}
              >
                <header className="report-image-panel__header">
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
                <div className="report-image-panel__workspace">
                  <div className="report-image-panel__controls">
                    <span className="kicker">
                      {t("Pages_Command_FieldReports_Edit_GalleryKicker")}
                    </span>
                    <h4>{t("Pages_Command_FieldReports_Edit_GalleryTitle")}</h4>
                    <p>
                      {imageUrl
                        ? selectedAsset?.originalFileName || fileNameFromPath(values.heroImagePath)
                        : t("Pages_Command_FieldReports_Edit_ImageNoFile")}
                    </p>
                    <div className="report-image-panel__buttons">
                      {canEdit && (
                        <ActionButton
                          variant="ghost"
                          disabled={busy}
                          onClick={() => setPickerOpen(true)}
                        >
                          {t("Pages_Command_FieldReports_Edit_GallerySelectLabel")}
                        </ActionButton>
                      )}
                      {canEdit && imageUrl && (
                        <ActionButton variant="ghost" disabled={busy} onClick={clearAsset}>
                          {t("Pages_Command_FieldReports_Edit_ImageRemove")}
                        </ActionButton>
                      )}
                      <ActionLink variant="ghost" to="/command/content/gallery">
                        {t("Pages_Command_FieldReports_Edit_GalleryOpen")}
                      </ActionLink>
                    </div>
                  </div>
                  <div className="report-image-panel__preview">
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
                    <dl className="media-admin-facts">
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

              <TextField
                {...form.register("publicationInput")}
                label={t("Pages_Command_FieldReports_Edit_LabelPublicationDate")}
                type="datetime-local"
                disabled={!canEdit || busy}
                error={errors.publicationInput?.message}
              />
              <div className="report-editor-form__checks">
                <CheckboxField
                  {...form.register("isFeatured")}
                  label={t("Pages_Command_FieldReports_Edit_LabelFeatured")}
                  disabled={!canEdit || busy}
                />
                <CheckboxField
                  {...form.register("isPublished")}
                  label={t("Pages_Command_FieldReports_Edit_LabelPublished")}
                  disabled={!canEdit || busy}
                />
              </div>
              <div className="report-editor-form__wide">
                <TextAreaField
                  {...form.register("markdown")}
                  label={t("Pages_Command_FieldReports_Edit_LabelMarkdown")}
                  rows={20}
                  maxLength={20_000}
                  disabled={!canEdit || busy}
                  error={errors.markdown?.message}
                />
              </div>
            </form>

            <section
              className="report-editor-preview"
              aria-label={t("Pages_Command_FieldReports_Edit_PreviewAria")}
            >
              <header className="report-editor-preview__header">
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
                <ActionButton
                  variant="ghost"
                  aria-expanded={previewVisible}
                  aria-controls={previewId}
                  onClick={() => setPreviewVisible((current) => !current)}
                >
                  {previewVisible
                    ? t("Pages_Command_FieldReports_Edit_HidePreview")
                    : t("Pages_Command_FieldReports_Edit_ShowPreview")}
                </ActionButton>
              </header>
              {previewVisible && (
                <article id={previewId}>
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

            <footer className="report-editor-actions">
              {canEdit && (
                <ActionButton
                  variant="primary"
                  type="submit"
                  form="field-report-editor-form"
                  disabled={busy}
                >
                  {busy
                    ? t("Pages_Command_FieldReports_Edit_ButtonSaving")
                    : t("Pages_Command_FieldReports_Edit_ButtonSave")}
                </ActionButton>
              )}
              {!isCreate && canModify && !query.data?.isPublished && !query.data?.isDeleted && (
                <ActionButton
                  variant="ghost"
                  disabled={busy}
                  onClick={() => requestAction("publish")}
                >
                  {t("Pages_Command_FieldReports_Edit_ButtonPublish")}
                </ActionButton>
              )}
              {!isCreate && canModify && query.data?.isPublished && !query.data.isDeleted && (
                <ActionButton
                  variant="ghost"
                  disabled={busy}
                  onClick={() => requestAction("unpublish")}
                >
                  {t("Pages_Command_FieldReports_Edit_ButtonUnpublish")}
                </ActionButton>
              )}
              {!isCreate && canDelete && !query.data?.isDeleted && (
                <ActionButton
                  variant="danger"
                  disabled={busy}
                  onClick={() => requestAction("delete")}
                >
                  {t("Pages_Command_FieldReports_Edit_ButtonDelete")}
                </ActionButton>
              )}
              {!isCreate && canDelete && query.data?.isDeleted && (
                <ActionButton
                  variant="ghost"
                  disabled={busy}
                  onClick={() => requestAction("restore")}
                >
                  {t("Pages_Command_FieldReports_Edit_ButtonRestore")}
                </ActionButton>
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
        cancelLabel={t("Pages_Command_FieldReports_Edit_DeleteReport_Dismiss")}
        initialFocus="dismiss"
        busy={action.isPending}
        onConfirm={() => action.mutate("delete")}
        onCancel={() => setDeleteOpen(false)}
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
  const { reportId } = useParams<{ reportId?: string }>();
  if (reportId && !isGuid(reportId)) return <NotFoundPage />;
  return <Editor key={reportId ?? "create"} {...(reportId ? { reportId } : {})} />;
}
