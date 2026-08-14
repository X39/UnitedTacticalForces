import { z } from "zod";
import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { components } from "@/generated/api";

type PlainTerrainDto = components["schemas"]["PlainTerrainDto"];
type TerrainCreationPayload = components["schemas"]["TerrainCreationPayload"];
type TerrainUpdate = components["schemas"]["TerrainUpdate"];

export interface Terrain {
  gameIdentifier: string;
  primaryKey: number;
  title: string;
  imageAssetFk: string;
  imagePath: string;
  isActive: boolean;
}

export interface TerrainWindow {
  terrains: Terrain[];
  totalCount: number;
}

export interface TerrainFormValues {
  gameIdentifier: string;
  title: string;
}

export interface TerrainUpdateFormValues {
  title: string;
}

export const terrainQueryKeys = {
  all: ["terrains"] as const,
  list: (search: string, take: number) => ["terrains", "list", search, take] as const,
  detail: (terrainId: string) => ["terrains", "detail", terrainId] as const,
};

export function createTerrainFormSchema(messages: {
  gameRequired: string;
  titleRequired: string;
  titleTooLong: string;
}) {
  return z.object({
    gameIdentifier: z.string().trim().min(1, messages.gameRequired),
    title: z.string().trim().min(1, messages.titleRequired).max(1024, messages.titleTooLong),
  });
}

export function createTerrainUpdateFormSchema(messages: {
  titleRequired: string;
  titleTooLong: string;
}) {
  return z.object({
    title: z.string().trim().min(1, messages.titleRequired).max(1024, messages.titleTooLong),
  });
}

export function isLongRouteValue(value: string | undefined): value is string {
  if (!value || !/^-?\d+$/.test(value)) return false;
  try {
    const parsed = BigInt(value);
    return parsed >= -9_223_372_036_854_775_808n && parsed <= 9_223_372_036_854_775_807n;
  } catch {
    return false;
  }
}

function normalizeTerrain(terrain: PlainTerrainDto): Terrain {
  return {
    gameIdentifier: terrain.gameIdentifier ?? "",
    primaryKey: terrain.primaryKey ?? 0,
    title: terrain.title ?? "",
    imageAssetFk: terrain.imageAssetFk ?? "",
    imagePath: terrain.imagePath ?? "",
    isActive: terrain.isActive ?? false,
  };
}

function requiredTerrain(terrain: PlainTerrainDto | undefined, operation: string): Terrain {
  if (!terrain) throw new Error(`${operation} returned no response body.`);
  return normalizeTerrain(terrain);
}

function terrainListPath(search: string, take: number): string {
  const query = new URLSearchParams({ skip: "0", take: String(take) });
  if (search.trim()) query.set("search", search.trim());
  return apiPathWithQuery(apiPath("/terrains/all"), query.toString());
}

export async function loadTerrainWindow(
  search: string,
  take: number,
  signal?: AbortSignal,
): Promise<TerrainWindow> {
  const [terrains, totalCount] = await Promise.all([
    getApiClient().request<PlainTerrainDto[]>(terrainListPath(search, take), {
      method: apiMethod("/terrains/all", "post"),
      signal,
    }),
    getApiClient().request<number>(apiPath("/terrains/all/count"), {
      method: apiMethod("/terrains/all/count", "post"),
      signal,
    }),
  ]);
  return {
    terrains: (terrains ?? []).map(normalizeTerrain),
    totalCount: totalCount ?? 0,
  };
}

export async function loadTerrain(
  terrainId: string,
  signal?: AbortSignal,
): Promise<Terrain | undefined> {
  const path = "/terrains/{terrainId}" as const;
  const terrain = await getApiClient().request<PlainTerrainDto>(fillApiPath(path, { terrainId }), {
    signal,
    expectedStatuses: [404],
  });
  return terrain ? normalizeTerrain(terrain) : undefined;
}

export async function createTerrain(
  values: TerrainFormValues,
  imageAssetFk: string,
  signal?: AbortSignal,
): Promise<Terrain> {
  const payload: TerrainCreationPayload = {
    gameIdentifier: values.gameIdentifier.trim(),
    title: values.title.trim(),
    imageAssetFk,
  };
  const path = "/terrains/create" as const;
  const terrain = await getApiClient().request<PlainTerrainDto>(apiPath(path), {
    method: apiMethod(path, "post"),
    body: payload,
    signal,
  });
  return requiredTerrain(terrain, "Terrain creation");
}

export async function updateTerrain(
  terrainId: string,
  payload: TerrainUpdate,
  signal?: AbortSignal,
): Promise<void> {
  const path = "/terrains/{terrainId}/update" as const;
  await getApiClient().request<void>(fillApiPath(path, { terrainId }), {
    method: apiMethod(path, "post"),
    body: payload,
    signal,
  });
}

export async function deactivateTerrain(terrainId: string, signal?: AbortSignal): Promise<void> {
  const path = "/terrains/{terrainId}/delete" as const;
  await getApiClient().request<void>(fillApiPath(path, { terrainId }), {
    method: apiMethod(path, "post"),
    signal,
  });
}

export function terrainHasImage(terrain: Pick<Terrain, "imagePath">): boolean {
  return Boolean(terrain.imagePath.trim());
}

export function terrainMatchesImageFilter(
  terrain: Pick<Terrain, "imagePath">,
  filter: "all" | "missing-image",
): boolean {
  return filter === "all" || !terrainHasImage(terrain);
}

export function formatTerrainTitle(terrain: Pick<Terrain, "title">, fallback: string): string {
  return terrain.title.trim() || fallback;
}
