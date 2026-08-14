import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, fillApiPath } from "@/api/contract";
import type { components } from "@/generated/api";

type VereinDocumentDto = components["schemas"]["VereinDocumentDto"];
type VereinDocumentRevisionDto = components["schemas"]["VereinDocumentRevisionDto"];
export type VereinDocumentRevisionPayload = components["schemas"]["VereinDocumentRevisionPayload"];

export type VereinDocumentKind = "Satzung" | "Minutes";
export type VereinDocumentFilter = "all" | VereinDocumentKind;
export type VereinDocumentValidationError =
  "satzung" | "title" | "summary" | "markdown" | "comment";

export interface VereinDocumentRevision {
  primaryKey: number;
  documentFk: number;
  version: number;
  title: string;
  summary: string;
  markdown: string;
  comment: string;
  isPublished: boolean;
  timeStampPublished: string | null;
  timeStampCreated: string;
  authorFk: string | null;
  authorName: string | null;
}

export interface VereinAdminDocument {
  primaryKey: number;
  kind: VereinDocumentKind;
  isDeleted: boolean;
  revision: VereinDocumentRevision | null;
  revisions: VereinDocumentRevision[];
  timeStampCreated: string;
  timeStampUpdated: string;
}

export const vereinDocumentQueryKeys = {
  all: ["verein", "admin", "documents"] as const,
};

export function normalizeVereinDocumentKind(value: unknown): VereinDocumentKind {
  if (value === "Minutes" || value === 1 || value === "1") return "Minutes";
  return "Satzung";
}

function normalizeRevision(revision: VereinDocumentRevisionDto): VereinDocumentRevision {
  return {
    primaryKey: revision.primaryKey ?? 0,
    documentFk: revision.documentFk ?? 0,
    version: revision.version ?? 0,
    title: revision.title ?? "",
    summary: revision.summary ?? "",
    markdown: revision.markdown ?? "",
    comment: revision.comment ?? "",
    isPublished: revision.isPublished ?? false,
    timeStampPublished: revision.timeStampPublished ?? null,
    timeStampCreated: revision.timeStampCreated ?? "",
    authorFk: revision.authorFk ?? null,
    authorName: revision.authorName ?? null,
  };
}

export function normalizeVereinDocument(document: VereinDocumentDto): VereinAdminDocument {
  return {
    primaryKey: document.primaryKey ?? 0,
    kind: normalizeVereinDocumentKind(document.kind),
    isDeleted: document.isDeleted ?? false,
    revision: document.revision ? normalizeRevision(document.revision) : null,
    revisions: (document.revisions ?? []).map(normalizeRevision),
    timeStampCreated: document.timeStampCreated ?? "",
    timeStampUpdated: document.timeStampUpdated ?? "",
  };
}

export async function loadVereinDocuments(signal?: AbortSignal): Promise<VereinAdminDocument[]> {
  const rows = await getApiClient().request<VereinDocumentDto[]>(
    apiPath("/verein/admin/documents"),
    { signal },
  );
  return (rows ?? []).map(normalizeVereinDocument);
}

export async function createVereinDocument(
  kind: VereinDocumentKind,
  payload: VereinDocumentRevisionPayload,
  signal?: AbortSignal,
): Promise<VereinAdminDocument> {
  const path = "/verein/admin/documents/{kind}" as const;
  const document = await getApiClient().request<VereinDocumentDto>(fillApiPath(path, { kind }), {
    method: apiMethod(path, "post"),
    body: payload,
    signal,
  });
  if (!document) throw new Error("Verein document creation returned no response body.");
  return normalizeVereinDocument(document);
}

export async function createVereinDocumentRevision(
  documentId: number,
  payload: VereinDocumentRevisionPayload,
  signal?: AbortSignal,
): Promise<VereinAdminDocument> {
  const path = "/verein/admin/documents/{documentId}/revisions" as const;
  const document = await getApiClient().request<VereinDocumentDto>(
    fillApiPath(path, { documentId }),
    { method: apiMethod(path, "post"), body: payload, signal },
  );
  if (!document) throw new Error("Verein document revision returned no response body.");
  return normalizeVereinDocument(document);
}

export async function setVereinRevisionPublished(
  revisionId: number,
  published: boolean,
  signal?: AbortSignal,
): Promise<VereinAdminDocument> {
  const path = published
    ? ("/verein/admin/documents/revisions/{revisionId}/publish" as const)
    : ("/verein/admin/documents/revisions/{revisionId}/unpublish" as const);
  const document = await getApiClient().request<VereinDocumentDto>(
    fillApiPath(path, { revisionId }),
    { method: apiMethod(path, "post"), signal },
  );
  if (!document) throw new Error("Verein publication update returned no response body.");
  return normalizeVereinDocument(document);
}

export function activeVereinRevision(document: VereinAdminDocument): VereinDocumentRevision | null {
  return (
    document.revision ??
    [...document.revisions].sort((left, right) => right.version - left.version)[0] ??
    null
  );
}

export function latestPublishedVereinRevision(
  document: VereinAdminDocument,
): VereinDocumentRevision | null {
  return (
    [...document.revisions]
      .filter((revision) => revision.isPublished)
      .sort(
        (left, right) =>
          Date.parse(right.timeStampPublished ?? "") - Date.parse(left.timeStampPublished ?? "") ||
          right.version - left.version,
      )[0] ?? null
  );
}

export function vereinDocumentMatches(
  document: VereinAdminDocument,
  filter: VereinDocumentFilter,
  search: string,
): boolean {
  if (filter !== "all" && document.kind !== filter) return false;
  const query = search.trim().toLocaleLowerCase();
  if (!query) return true;
  const revision = activeVereinRevision(document);
  return [revision?.title, revision?.summary, document.kind]
    .filter(Boolean)
    .some((value) => value!.toLocaleLowerCase().includes(query));
}

export function validateVereinDocument(
  payload: VereinDocumentRevisionPayload,
  kind: VereinDocumentKind,
  satzungExists: boolean,
): VereinDocumentValidationError | undefined {
  if (kind === "Satzung" && satzungExists) return "satzung";
  const title = payload.title?.trim() ?? "";
  const summary = payload.summary?.trim() ?? "";
  const markdown = payload.markdown?.trim() ?? "";
  const comment = payload.comment?.trim() ?? "";
  if (!title || title.length > 180) return "title";
  if (summary.length > 512) return "summary";
  if (!markdown || markdown.length > 60_000) return "markdown";
  if (comment.length > 512) return "comment";
  return undefined;
}

export function normalizeVereinDocumentPayload(
  payload: VereinDocumentRevisionPayload,
): VereinDocumentRevisionPayload {
  return {
    title: payload.title?.trim() ?? "",
    summary: payload.summary?.trim() ?? "",
    markdown: payload.markdown?.trim() ?? "",
    comment: payload.comment?.trim() || null,
    isPublished: payload.isPublished ?? false,
  };
}
