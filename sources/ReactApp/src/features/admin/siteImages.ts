import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { TranslationKey } from "@/app/localizationTypes";
import type { components } from "@/generated/api";

type SiteImageSettingDto = components["schemas"]["SiteImageSettingDto"];
type MediaAssetDto = components["schemas"]["MediaAssetDto"];
type MediaAssetUsageDto = components["schemas"]["MediaAssetUsageDto"];

export type SiteImageSetting = Required<SiteImageSettingDto>;
export type MediaAssetUsage = Required<MediaAssetUsageDto>;
export interface MediaAsset extends Omit<Required<MediaAssetDto>, "createdByName" | "usages"> {
  createdByName: string | null;
  usages: MediaAssetUsage[];
}

export interface MediaAssetFilters {
  search?: string;
  includeTags?: readonly string[];
  excludeTags?: readonly string[];
  skip?: number;
  take?: number;
}

export const siteImageGroups = [
  ["home", "Pages_Command_SiteImages_Group_Home"],
  ["media", "Pages_Command_SiteImages_Group_Media"],
  ["wiki", "Pages_Command_SiteImages_Group_Wiki"],
  ["join", "Pages_Command_SiteImages_Group_Join"],
  ["unit", "Pages_Command_SiteImages_Group_Unit"],
  ["verein", "Pages_Command_SiteImages_Group_Verein"],
  ["satzung", "Pages_Command_SiteImages_Group_Statutes"],
  ["beschluesse", "Pages_Command_SiteImages_Group_Minutes"],
  ["kassenbuch", "Pages_Command_SiteImages_Group_Ledger"],
  ["gdpr", "Pages_Command_SiteImages_Group_Gdpr"],
  ["public-pages", "Pages_Command_SiteImages_Group_PublicPages"],
] as const satisfies readonly (readonly [string, TranslationKey])[];

export type SiteImageGroup = (typeof siteImageGroups)[number][0];

export function normalizeSiteImageGroup(value: string | undefined): SiteImageGroup {
  const normalized = value?.trim().toLocaleLowerCase();
  return siteImageGroups.some(([group]) => group === normalized)
    ? (normalized as SiteImageGroup)
    : "home";
}

function normalizeSetting(setting: SiteImageSettingDto): SiteImageSetting {
  return {
    key: setting.key ?? "",
    title: setting.title ?? "",
    description: setting.description ?? "",
    group: setting.group ?? "",
    sortOrder: setting.sortOrder ?? 0,
    imageAssetFk: setting.imageAssetFk ?? "",
    imagePath: setting.imagePath ?? "",
    timeStampCreated: setting.timeStampCreated ?? "",
    timeStampUpdated: setting.timeStampUpdated ?? "",
    updatedByName: setting.updatedByName ?? null,
  };
}

function normalizeAsset(asset: MediaAssetDto): MediaAsset {
  return {
    primaryKey: asset.primaryKey ?? "",
    originalFileName: asset.originalFileName ?? "",
    contentType: asset.contentType ?? "",
    size: asset.size ?? 0,
    imagePath: asset.imagePath ?? "",
    timeStampCreated: asset.timeStampCreated ?? "",
    timeStampUpdated: asset.timeStampUpdated ?? "",
    createdByName: asset.createdByName ?? null,
    tags: asset.tags ?? [],
    usages: (asset.usages ?? []).map(normalizeUsage),
  };
}

function normalizeUsage(usage: MediaAssetUsageDto): MediaAssetUsage {
  return {
    domain: usage.domain ?? "",
    domainKey: usage.domainKey ?? "",
    displayName: usage.displayName ?? "",
    path: usage.path ?? "",
  };
}

export async function loadSiteImageSettings(
  group: SiteImageGroup,
  signal?: AbortSignal,
): Promise<SiteImageSetting[]> {
  const path = apiPathWithQuery(
    apiPath("/site-image-settings/admin"),
    new URLSearchParams({ group }).toString(),
  );
  const settings = await getApiClient().request<SiteImageSettingDto[]>(path, { signal });
  return (settings ?? [])
    .map(normalizeSetting)
    .sort(
      (left, right) => left.sortOrder - right.sortOrder || left.title.localeCompare(right.title),
    );
}

export async function updateSiteImageSetting(
  key: string,
  imageAssetFk: string,
  signal?: AbortSignal,
): Promise<SiteImageSetting> {
  const path = "/site-image-settings/admin/{key}" as const;
  const setting = await getApiClient().request<SiteImageSettingDto>(fillApiPath(path, { key }), {
    method: apiMethod(path, "put"),
    body: { imageAssetFk },
    signal,
  });
  if (!setting) throw new Error("Site image update returned no response body.");
  return normalizeSetting(setting);
}

export async function loadMediaAssets(
  search: string,
  skip = 0,
  signal?: AbortSignal,
): Promise<MediaAsset[]> {
  const query = new URLSearchParams({ skip: String(skip), take: "48" });
  if (search.trim()) query.set("search", search.trim());
  const assets = await getApiClient().request<MediaAssetDto[]>(
    apiPathWithQuery(apiPath("/media-assets"), query.toString()),
    { signal },
  );
  return (assets ?? []).map(normalizeAsset);
}

export async function loadMediaAssetPage(
  filters: MediaAssetFilters,
  signal?: AbortSignal,
): Promise<MediaAsset[]> {
  const query = new URLSearchParams({
    skip: String(Math.max(0, filters.skip ?? 0)),
    take: String(Math.min(200, Math.max(1, filters.take ?? 60))),
  });
  if (filters.search?.trim()) query.set("search", filters.search.trim());
  for (const tag of filters.includeTags ?? []) if (tag.trim()) query.append("tag", tag.trim());
  for (const tag of filters.excludeTags ?? [])
    if (tag.trim()) query.append("excludeTag", tag.trim());
  const assets = await getApiClient().request<MediaAssetDto[]>(
    apiPathWithQuery(apiPath("/media-assets"), query.toString()),
    { signal },
  );
  return (assets ?? []).map(normalizeAsset);
}

/**
 * Loads one gallery asset by its identifier.
 *
 * @param assetId - Identifier of the asset to load.
 * @param signal - Optional signal that cancels the request.
 * @returns The normalized asset, or exactly `null` when the asset no longer exists (HTTP 404).
 */
export async function loadMediaAsset(
  assetId: string,
  signal?: AbortSignal,
): Promise<MediaAsset | null> {
  const path = "/media-assets/{assetId}" as const;
  const asset = await getApiClient().request<MediaAssetDto>(fillApiPath(path, { assetId }), {
    signal,
    expectedStatuses: [404],
  });
  return asset ? normalizeAsset(asset) : null;
}

export async function uploadMediaAsset(
  file: File,
  tags: readonly string[] = ["site-image"],
  signal?: AbortSignal,
): Promise<MediaAsset> {
  const body = new FormData();
  body.append("file", file, file.name);
  if (tags.length > 0) body.append("tags", tags.join(","));
  const path = "/media-assets/uploads" as const;
  const asset = await getApiClient().request<MediaAssetDto>(apiPath(path), {
    method: apiMethod(path, "post"),
    body,
    signal,
  });
  if (!asset) throw new Error("Media upload returned no response body.");
  return normalizeAsset(asset);
}

export async function replaceMediaAssetTags(
  assetId: string,
  tags: readonly string[],
  signal?: AbortSignal,
): Promise<MediaAsset> {
  const path = "/media-assets/{assetId}/tags" as const;
  const asset = await getApiClient().request<MediaAssetDto>(fillApiPath(path, { assetId }), {
    method: apiMethod(path, "put"),
    body: { tags },
    signal,
  });
  if (!asset) throw new Error("Media tag update returned no response body.");
  return normalizeAsset(asset);
}

export function parseMediaAssetTags(value: string): string[] {
  const tags: string[] = [];
  const seen = new Set<string>();
  for (const candidate of value.split(/[,;]/)) {
    const tag = candidate.trim();
    const comparison = tag.toLocaleLowerCase();
    if (!tag || seen.has(comparison)) continue;
    seen.add(comparison);
    tags.push(tag);
  }
  return tags.sort((a, b) => a.localeCompare(b, undefined, { sensitivity: "base" }));
}

export function mediaAssetMatchesUsageDomains(
  asset: MediaAsset,
  enabledDomains: ReadonlySet<string>,
): boolean {
  if (asset.usages.length === 0) return enabledDomains.has("unused");
  return asset.usages.some((usage) => enabledDomains.has(usage.domain));
}

export function mediaAssetTagsEqual(value: string, tags: readonly string[]): boolean {
  const left = parseMediaAssetTags(value).map((tag) => tag.toLocaleLowerCase());
  const right = [...tags].map((tag) => tag.toLocaleLowerCase()).sort((a, b) => a.localeCompare(b));
  return left.length === right.length && left.every((tag, index) => tag === right[index]);
}
