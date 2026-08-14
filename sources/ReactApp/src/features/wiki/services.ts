import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type {
  WikiPage,
  WikiPageHeader,
  WikiPageRevision,
  WikiPageRevisionHeader,
} from "@/api/types";

function wikiForm(fields: Record<string, string>): FormData {
  const form = new FormData();
  for (const [name, value] of Object.entries(fields)) form.set(name, value);
  return form;
}

export const wikiQueryKeys = {
  headers: (showDeleted: boolean) => ["wiki", "headers", { showDeleted }] as const,
  page: (pageId: string) => ["wiki", "page", pageId] as const,
  revisions: (pageId: string) => ["wiki", "page", pageId, "revisions"] as const,
  revision: (pageId: string, revisionId: string) =>
    ["wiki", "page", pageId, "revisions", revisionId] as const,
};

export const wikiService = {
  headers: (showDeleted: boolean, signal?: AbortSignal) =>
    getApiClient().request<WikiPageHeader[]>(
      showDeleted
        ? apiPathWithQuery("/wiki/all/header", "showDeleted=true")
        : apiPath("/wiki/all/header"),
      { signal },
    ),
  page: (pageId: string, signal?: AbortSignal) =>
    getApiClient().request<WikiPage>(fillApiPath("/wiki/{pageKey}", { pageKey: pageId }), {
      signal,
    }),
  revisionHeaders: (pageId: string, signal?: AbortSignal) =>
    getApiClient().request<WikiPageRevisionHeader[]>(
      fillApiPath("/wiki/{pageKey}/revisions/header", { pageKey: pageId }),
      { signal },
    ),
  revision: (pageId: string, revisionId: string, signal?: AbortSignal) =>
    getApiClient().request<WikiPageRevision>(
      fillApiPath("/wiki/{pageKey}/revisions/{revisionKey}", {
        pageKey: pageId,
        revisionKey: revisionId,
      }),
      { signal },
    ),
  create: (title: string, markdown: string, comment: string, signal?: AbortSignal) =>
    getApiClient().request<WikiPage>(apiPath("/wiki"), {
      method: apiMethod("/wiki", "post"),
      body: wikiForm({ title, markdown, comment }),
      signal,
    }),
  rename: (pageId: string, title: string, signal?: AbortSignal) =>
    getApiClient().request<WikiPage>(fillApiPath("/wiki/{pageKey}", { pageKey: pageId }), {
      method: apiMethod("/wiki/{pageKey}", "put"),
      body: wikiForm({ title }),
      signal,
    }),
  createRevision: (pageId: string, markdown: string, comment: string, signal?: AbortSignal) =>
    getApiClient().request<WikiPageRevision>(
      fillApiPath("/wiki/{pageKey}/revisions", { pageKey: pageId }),
      {
        method: apiMethod("/wiki/{pageKey}/revisions", "post"),
        body: wikiForm({ markdown, comment }),
        signal,
      },
    ),
  archive: (pageId: string, signal?: AbortSignal) =>
    getApiClient().request<void>(fillApiPath("/wiki/{pageKey}", { pageKey: pageId }), {
      method: apiMethod("/wiki/{pageKey}", "delete"),
      signal,
    }),
  restore: (pageId: string, signal?: AbortSignal) =>
    getApiClient().request<void>(fillApiPath("/wiki/{pageKey}/restore", { pageKey: pageId }), {
      method: apiMethod("/wiki/{pageKey}/restore", "post"),
      signal,
    }),
};
