import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, fillApiPath } from "@/api/contract";
import type { components } from "@/generated/api";

type PublicPageDto = components["schemas"]["PublicPageDto"];
type PublicPageRevisionDto = components["schemas"]["PublicPageRevisionDto"];
export type PublicPageRevisionPayload = components["schemas"]["PublicPageRevisionPayload"];

export type PublicPageRevision = Required<PublicPageRevisionDto>;

export interface PublicPage extends Omit<Required<PublicPageDto>, "revision" | "revisions"> {
  revision: PublicPageRevision | undefined;
  revisions: PublicPageRevision[];
}

function normalizeRevision(revision: PublicPageRevisionDto): PublicPageRevision {
  return {
    primaryKey: revision.primaryKey ?? 0,
    pageFk: revision.pageFk ?? 0,
    version: revision.version ?? 0,
    title: revision.title ?? "",
    summary: revision.summary ?? "",
    heroKicker: revision.heroKicker ?? "",
    heroTitle: revision.heroTitle ?? "",
    heroDescription: revision.heroDescription ?? "",
    heroImageAssetFk: revision.heroImageAssetFk ?? "00000000-0000-0000-0000-000000000000",
    heroImagePath: revision.heroImagePath ?? "",
    markdown: revision.markdown ?? "",
    contentJson: revision.contentJson ?? "",
    comment: revision.comment ?? "",
    isPublished: Boolean(revision.isPublished),
    timeStampPublished: revision.timeStampPublished ?? null,
    timeStampCreated: revision.timeStampCreated ?? "",
    authorFk: revision.authorFk ?? null,
    authorName: revision.authorName ?? null,
  };
}

export function normalizePublicPage(page: PublicPageDto): PublicPage {
  return {
    primaryKey: page.primaryKey ?? 0,
    slug: page.slug ?? "",
    isDeleted: Boolean(page.isDeleted),
    revision: page.revision ? normalizeRevision(page.revision) : undefined,
    revisions: (page.revisions ?? []).map(normalizeRevision),
    timeStampCreated: page.timeStampCreated ?? "",
    timeStampUpdated: page.timeStampUpdated ?? "",
  };
}

export function latestPublishedRevision(page: PublicPage): PublicPageRevision | undefined {
  return [...page.revisions]
    .filter((revision) => revision.isPublished)
    .sort((left, right) => {
      const timestampOrder = (right.timeStampPublished ?? "").localeCompare(
        left.timeStampPublished ?? "",
      );
      return timestampOrder || right.version - left.version;
    })[0];
}

export async function loadAdminPublicPages(signal?: AbortSignal): Promise<PublicPage[]> {
  const pages = await getApiClient().request<PublicPageDto[]>(apiPath("/public-pages/admin"), {
    signal,
  });
  return (pages ?? []).map(normalizePublicPage);
}

export async function loadAdminPublicPage(
  slug: string,
  signal?: AbortSignal,
): Promise<PublicPage | undefined> {
  const page = await getApiClient().request<PublicPageDto>(
    fillApiPath("/public-pages/admin/{slug}", { slug }),
    { signal, expectedStatuses: [404] },
  );
  return page ? normalizePublicPage(page) : undefined;
}

export async function setPublicPageRevisionPublished(
  revisionId: number,
  published: boolean,
  signal?: AbortSignal,
): Promise<PublicPage> {
  const path = published
    ? ("/public-pages/admin/revisions/{revisionId}/publish" as const)
    : ("/public-pages/admin/revisions/{revisionId}/unpublish" as const);
  const page = await getApiClient().request<PublicPageDto>(fillApiPath(path, { revisionId }), {
    method: apiMethod(path, "post"),
    signal,
  });
  if (!page) throw new Error("Public page publication command returned no response body.");
  return normalizePublicPage(page);
}

export async function createPublicPageRevision(
  slug: string,
  payload: PublicPageRevisionPayload,
  signal?: AbortSignal,
): Promise<PublicPage> {
  const path = "/public-pages/admin/{slug}/revisions" as const;
  const page = await getApiClient().request<PublicPageDto>(fillApiPath(path, { slug }), {
    method: apiMethod(path, "post"),
    body: payload,
    signal,
  });
  if (!page) throw new Error("Public page revision save returned no response body.");
  return normalizePublicPage(page);
}
