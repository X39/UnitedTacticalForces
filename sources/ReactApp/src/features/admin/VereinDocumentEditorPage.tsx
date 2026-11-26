import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate, useParams } from "react-router";
import { z } from "zod";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageVereinDocuments } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { CheckboxField, SelectField, TextAreaField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import {
  ConfirmDialog,
  Markdown,
  PageHeader,
  StatePanel,
  StatusBadge,
} from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { useLeaveGuard } from "@/components/useLeaveGuard";
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
import "@/features/admin/vereinDocuments.css";

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

/** Renders the page header above a state view, so every state of the editor route keeps one `h1`. */
function EditorStateFrame({ title, children }: { title: string; children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <PageStack>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_VereinDocuments")}
        title={title}
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
  // A saved document leaves the editor through navigate(); the ref lets that one navigation pass the guard
  // before the form reset has re-rendered the dirty flag.
  const saved = useRef(false);
  const leaveGuard = useLeaveGuard({
    dirty: form.formState.isDirty,
    shouldBlock: () => !saved.current,
  });
  const values = useWatch({ control: form.control });
  const previewMarkdown = values.markdown?.trim() || t("Pages_Command_Verein_DocumentPreviewEmpty");
  const mutation = useMutation({
    mutationFn: (formValues: DocumentForm) => {
      const payload = normalizeVereinDocumentPayload(formValues);
      return editing && documentId !== null
        ? createVereinDocumentRevision(documentId, payload)
        : createVereinDocument(formValues.kind, payload);
    },
    onSuccess: (savedDocument) => {
      queryClient.setQueryData<VereinAdminDocument[]>(vereinDocumentQueryKeys.all, (current) => {
        const remaining = (current ?? []).filter(
          (candidate) => candidate.primaryKey !== savedDocument.primaryKey,
        );
        return [...remaining, savedDocument];
      });
      form.reset(valuesForDocument(savedDocument));
      saved.current = true;
      navigate(`/command/verein/documents/${savedDocument.primaryKey}`, {
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
      <EditorStateFrame title={title}>
        <ForbiddenState title={t("Pages_Command_Verein_DocumentsAccessRequiredTitle")}>
          {t("Pages_Command_Verein_DocumentsAccessRequiredDescription")}
        </ForbiddenState>
      </EditorStateFrame>
    );

  if (query.isPending)
    return (
      <EditorStateFrame title={title}>
        <LoadingState label={t("Pages_Command_Verein_Loading")} />
      </EditorStateFrame>
    );

  if (query.isError)
    return (
      <EditorStateFrame title={title}>
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("Pages_Command_Verein_LoadError"), query.error.message)}
        </ErrorState>
      </EditorStateFrame>
    );

  if (editing && (!Number.isSafeInteger(documentId) || !document))
    return (
      <EditorStateFrame title={title}>
        <EmptyState title={t("Pages_Command_Verein_DocumentNotFoundTitle")}>
          {t("Pages_Command_Verein_DocumentNotFoundDescription")}
        </EmptyState>
      </EditorStateFrame>
    );

  const nextVersion = document
    ? Math.max(0, ...document.revisions.map((revision) => revision.version)) + 1
    : null;
  const leaveTarget =
    editing && document
      ? `/command/verein/documents/${document.primaryKey}`
      : "/command/verein/documents";

  return (
    <PageStack>
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
          <ActionLink variant="ghost" to="/command/verein/documents">
            {t("Pages_Command_Verein_DocumentsBackToList")}
          </ActionLink>
        }
      />

      {!editing && existingSatzung && (
        <StatePanel
          kind="info"
          title={t("Pages_Command_Verein_DocumentSatzungExistsTitle")}
          action={
            <ActionLink
              variant="ghost"
              to={`/command/verein/documents/${existingSatzung.primaryKey}/edit`}
            >
              {t("Pages_Command_Verein_DocumentsOpenEdit")}
            </ActionLink>
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
          <div className="verein-document-fields">
            <SelectField
              label={t("Pages_Command_Verein_DocumentFormKind")}
              disabled={editing}
              options={[
                {
                  value: "Satzung",
                  label: t("Satzung"),
                  disabled: !editing && Boolean(existingSatzung),
                },
                { value: "Minutes", label: t("Auto_BeschlussMinutes_6fa4d247") },
              ]}
              {...form.register("kind")}
            />
            <TextField
              label={t("Pages_Command_Verein_DocumentFormTitle")}
              maxLength={180}
              error={form.formState.errors.title?.message}
              {...form.register("title")}
            />
            <div className="verein-document-fields__wide">
              <TextField
                label={t("Pages_Command_Verein_DocumentFormSummary")}
                maxLength={512}
                error={form.formState.errors.summary?.message}
                {...form.register("summary")}
              />
            </div>
            <div className="verein-document-fields__wide">
              <TextAreaField
                label={t("Pages_Command_Verein_DocumentFormMarkdown")}
                rows={18}
                maxLength={60_000}
                error={form.formState.errors.markdown?.message}
                {...form.register("markdown")}
              />
            </div>
            <div className="verein-document-fields__wide">
              <TextField
                label={t("Pages_Command_Verein_DocumentFormRevisionComment")}
                maxLength={512}
                error={form.formState.errors.comment?.message}
                {...form.register("comment")}
              />
            </div>
            <div className="verein-document-fields__wide">
              <CheckboxField
                label={t("Pages_Command_Verein_DocumentFormPublish")}
                {...form.register("isPublished")}
              />
            </div>
            {form.formState.errors.root && (
              <div className="verein-document-fields__wide">
                <StatePanel kind="error">{form.formState.errors.root.message}</StatePanel>
              </div>
            )}
            {mutation.isError && (
              <div className="verein-document-fields__wide">
                <StatePanel kind="error">{mutation.error.message}</StatePanel>
              </div>
            )}
            <footer className="verein-document-fields__wide verein-document-fields__actions">
              <ActionButton variant="gold" type="submit" disabled={mutation.isPending}>
                {mutation.isPending
                  ? t("Pages_Command_Verein_DocumentFormSaving")
                  : t(
                      editing
                        ? "Pages_Command_Verein_DocumentsRevisionButton"
                        : "Pages_Command_Verein_DocumentsCreateButton",
                    )}
              </ActionButton>
              <ActionLink variant="ghost" to={leaveTarget}>
                {t("Pages_Command_Verein_DocumentEditorLeave_Dismiss")}
              </ActionLink>
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

      <ConfirmDialog
        open={leaveGuard.blocked}
        title={t("Server_Alerts_Leave_Title")}
        description={t("React_UnsavedChanges")}
        cancelLabel={t("Server_Change_KeepEditing")}
        confirmLabel={t("Pages_Command_Verein_DocumentEditorLeave_Dismiss")}
        initialFocus="dismiss"
        onCancel={leaveGuard.stay}
        onConfirm={leaveGuard.proceed}
      />
    </PageStack>
  );
}
