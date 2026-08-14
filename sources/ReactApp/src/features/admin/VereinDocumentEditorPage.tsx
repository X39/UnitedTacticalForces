import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams } from "react-router";
import { z } from "zod";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageVereinDocuments } from "@/auth/claims";
import {
  Markdown,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import {
  activeVereinRevision,
  createVereinDocument,
  createVereinDocumentRevision,
  loadVereinDocuments,
  normalizeVereinDocumentPayload,
  validateVereinDocument,
  vereinDocumentQueryKeys,
  type VereinAdminDocument,
  type VereinDocumentKind,
  type VereinDocumentValidationError,
} from "@/features/admin/vereinDocuments";

interface DocumentForm {
  kind: VereinDocumentKind;
  title: string;
  summary: string;
  markdown: string;
  comment: string;
  isPublished: boolean;
}

const defaults: DocumentForm = {
  kind: "Satzung",
  title: "",
  summary: "",
  markdown: "",
  comment: "",
  isPublished: false,
};

function valuesForDocument(document: VereinAdminDocument): DocumentForm {
  const revision = activeVereinRevision(document);
  return {
    kind: document.kind,
    title: revision?.title ?? "",
    summary: revision?.summary ?? "",
    markdown: revision?.markdown ?? "",
    comment: "",
    isPublished: false,
  };
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { documentId: rawDocumentId } = useParams();
  const documentId = rawDocumentId ? Number(rawDocumentId) : null;
  const editing = documentId !== null;
  const authorized = canManageVereinDocuments(user);
  const schema = useMemo(
    () =>
      z.object({
        kind: z.enum(["Satzung", "Minutes"]),
        title: z
          .string()
          .trim()
          .min(1, t("Pages_Command_Verein_DocumentValidationTitle"))
          .max(180, t("Pages_Command_Verein_DocumentValidationTitle")),
        summary: z.string().max(512, t("Pages_Command_Verein_DocumentValidationSummary")),
        markdown: z
          .string()
          .trim()
          .min(1, t("Pages_Command_Verein_DocumentValidationMarkdown"))
          .max(60_000, t("Pages_Command_Verein_DocumentValidationMarkdown")),
        comment: z.string().max(512, t("Pages_Command_Verein_DocumentValidationComment")),
        isPublished: z.boolean(),
      }),
    [t],
  );
  const form = useForm<DocumentForm>({ resolver: zodResolver(schema), defaultValues: defaults });
  const query = useQuery({
    queryKey: vereinDocumentQueryKeys.all,
    queryFn: ({ signal }) => loadVereinDocuments(signal),
    enabled: authorized,
  });
  const documents = query.data ?? [];
  const document = editing
    ? (documents.find((candidate) => candidate.primaryKey === documentId) ?? null)
    : null;
  const existingSatzung = documents.find(
    (candidate) => candidate.kind === "Satzung" && !candidate.isDeleted,
  );
  const initialized = useRef("");
  useEffect(() => {
    if (!query.data) return;
    const key = editing
      ? `edit:${documentId}:${document?.timeStampUpdated ?? "missing"}`
      : `create:${existingSatzung?.primaryKey ?? "none"}`;
    if (initialized.current === key || form.formState.isDirty) return;
    form.reset(
      document
        ? valuesForDocument(document)
        : { ...defaults, kind: existingSatzung ? "Minutes" : "Satzung" },
    );
    initialized.current = key;
  }, [document, documentId, editing, existingSatzung, form, query.data]);
  useUnsavedChanges(form.formState.isDirty);
  const values = useWatch({ control: form.control });
  const previewMarkdown = values.markdown?.trim() || t("Pages_Command_Verein_DocumentPreviewEmpty");
  const mutation = useMutation({
    mutationFn: (formValues: DocumentForm) => {
      const payload = normalizeVereinDocumentPayload(formValues);
      return editing && documentId !== null
        ? createVereinDocumentRevision(documentId, payload)
        : createVereinDocument(formValues.kind, payload);
    },
    onSuccess: (saved) => {
      queryClient.setQueryData<VereinAdminDocument[]>(vereinDocumentQueryKeys.all, (current) => {
        const remaining = (current ?? []).filter(
          (candidate) => candidate.primaryKey !== saved.primaryKey,
        );
        return [...remaining, saved];
      });
      form.reset(valuesForDocument(saved));
      navigate(`/command/verein/documents/${saved.primaryKey}`, {
        replace: true,
        state: { documentSaved: true },
      });
    },
  });
  const validationKeys: Record<VereinDocumentValidationError, string> = {
    satzung: t("Pages_Command_Verein_DocumentValidationSatzungExists"),
    title: t("Pages_Command_Verein_DocumentValidationTitle"),
    summary: t("Pages_Command_Verein_DocumentValidationSummary"),
    markdown: t("Pages_Command_Verein_DocumentValidationMarkdown"),
    comment: t("Pages_Command_Verein_DocumentValidationComment"),
  };
  const save = (formValues: DocumentForm) => {
    const payload = normalizeVereinDocumentPayload(formValues);
    const validation = validateVereinDocument(
      payload,
      formValues.kind,
      !editing && Boolean(existingSatzung),
    );
    if (validation) {
      const field = validation === "satzung" ? "root" : validation;
      form.setError(field, { type: "manual", message: validationKeys[validation] });
      return;
    }
    mutation.mutate(formValues);
  };
  const title = editing
    ? document
      ? formatResource(
          t("Pages_Command_Verein_DocumentsEditPageTitleWithDocument"),
          activeVereinRevision(document)?.title ?? document.primaryKey,
        )
      : t("Pages_Command_Verein_DocumentsEditPageTitle")
    : t("Pages_Command_Verein_DocumentsCreatePageTitle");

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

  if (editing && (!Number.isSafeInteger(documentId) || !document))
    return (
      <StatePanel kind="empty" title={t("Pages_Command_Verein_DocumentNotFoundTitle")}>
        {t("Pages_Command_Verein_DocumentNotFoundDescription")}
      </StatePanel>
    );

  const nextVersion = document
    ? Math.max(0, ...document.revisions.map((revision) => revision.version)) + 1
    : null;

  return (
    <section className="verein-document-editor-page">
      <PageHeader
        kicker={
          editing
            ? t("Pages_Command_Verein_DocumentsEditorKickerRevision")
            : t("Pages_Command_Verein_DocumentsEditorKickerNew")
        }
        title={title}
        description={t(
          editing
            ? "Pages_Command_Verein_DocumentsEditPageDescription"
            : "Pages_Command_Verein_DocumentsCreatePageDescription",
        )}
        actions={
          <Link className="button button--ghost" to="/command/verein/documents">
            {t("Pages_Command_Verein_DocumentsBackToList")}
          </Link>
        }
      />

      {!editing && existingSatzung && (
        <StatePanel
          kind="info"
          title={t("Pages_Command_Verein_DocumentSatzungExistsTitle")}
          action={
            <Link
              className="button button--ghost"
              to={`/command/verein/documents/${existingSatzung.primaryKey}/edit`}
            >
              {t("Pages_Command_Verein_DocumentsOpenEdit")}
            </Link>
          }
        >
          {t("Pages_Command_Verein_DocumentSatzungExistsDescription")}
        </StatePanel>
      )}

      <div className="verein-document-editor-shell">
        <form
          className="command-panel verein-document-editor-form-panel"
          aria-label={t("Pages_Command_Verein_DocumentsEditorAria")}
          onSubmit={form.handleSubmit(save)}
        >
          <header className="command-panel__header">
            <span className="kicker">
              {t(
                editing
                  ? "Pages_Command_Verein_DocumentsEditorKickerRevision"
                  : "Pages_Command_Verein_DocumentsEditorKickerNew",
              )}
            </span>
            <h2>
              {editing
                ? activeVereinRevision(document!)?.title
                : t("Pages_Command_Verein_DocumentsEditorTitleNew")}
            </h2>
          </header>
          <div className="verein-document-editor-form">
            <label>
              <span>{t("Pages_Command_Verein_DocumentFormKind")}</span>
              <select disabled={editing} {...form.register("kind")}>
                <option value="Satzung" disabled={!editing && Boolean(existingSatzung)}>
                  {t("Satzung")}
                </option>
                <option value="Minutes">{t("Auto_BeschlussMinutes_6fa4d247")}</option>
              </select>
            </label>
            <label>
              <span>{t("Pages_Command_Verein_DocumentFormTitle")}</span>
              <input maxLength={180} {...form.register("title")} />
              {form.formState.errors.title && (
                <small role="alert">{form.formState.errors.title.message}</small>
              )}
            </label>
            <label className="is-wide">
              <span>{t("Pages_Command_Verein_DocumentFormSummary")}</span>
              <input maxLength={512} {...form.register("summary")} />
              {form.formState.errors.summary && (
                <small role="alert">{form.formState.errors.summary.message}</small>
              )}
            </label>
            <label className="is-wide">
              <span>{t("Pages_Command_Verein_DocumentFormMarkdown")}</span>
              <textarea rows={18} maxLength={60_000} {...form.register("markdown")} />
              {form.formState.errors.markdown && (
                <small role="alert">{form.formState.errors.markdown.message}</small>
              )}
            </label>
            <label className="is-wide">
              <span>{t("Pages_Command_Verein_DocumentFormRevisionComment")}</span>
              <input maxLength={512} {...form.register("comment")} />
              {form.formState.errors.comment && (
                <small role="alert">{form.formState.errors.comment.message}</small>
              )}
            </label>
            <label className="check-field is-wide">
              <input type="checkbox" {...form.register("isPublished")} />
              <span>{t("Pages_Command_Verein_DocumentFormPublish")}</span>
            </label>
            {form.formState.errors.root && (
              <StatePanel kind="error">{form.formState.errors.root.message}</StatePanel>
            )}
            {mutation.isError && <StatePanel kind="error">{mutation.error.message}</StatePanel>}
            <footer className="is-wide page-header__actions">
              <button
                className="button button--primary"
                type="submit"
                disabled={mutation.isPending}
              >
                {mutation.isPending
                  ? t("Pages_Command_Verein_DocumentFormSaving")
                  : t(
                      editing
                        ? "Pages_Command_Verein_DocumentsRevisionButton"
                        : "Pages_Command_Verein_DocumentsCreateButton",
                    )}
              </button>
              <Link
                className="button button--ghost"
                to={
                  editing && document
                    ? `/command/verein/documents/${document.primaryKey}`
                    : "/command/verein/documents"
                }
              >
                {t("Common_Cancel")}
              </Link>
            </footer>
          </div>
        </form>

        <aside className="command-panel verein-document-preview-panel">
          <header className="command-panel__header">
            <span className="kicker">{t("Pages_Command_Verein_DocumentPreviewKicker")}</span>
            <h2>{t("Pages_Command_Verein_DocumentPreviewTitle")}</h2>
            <StatusBadge>
              {nextVersion
                ? formatResource(t("Pages_Command_Verein_DocumentNextRevision"), nextVersion)
                : t(values.kind === "Minutes" ? "Auto_BeschlussMinutes_6fa4d247" : "Satzung")}
            </StatusBadge>
          </header>
          <div className="verein-document-preview">
            <Markdown>{previewMarkdown}</Markdown>
          </div>
        </aside>
      </div>
    </section>
  );
}
