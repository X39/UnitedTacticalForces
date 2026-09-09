import { getApiClient } from "@/api/client";
import { apiPath } from "@/api/contract";
import type { FullUser } from "@/api/types";
import { canManageModPacks, hasClaim, hasResourceClaim, isAuthenticated } from "@/auth/claims";
import type { components } from "@/generated/api";

type Schema<Name extends keyof components["schemas"]> = components["schemas"][Name];

export type GameCapability = Schema<"GameServerCapabilityDto">;
export type PlainUser = Schema<"PlainUserDto">;

export interface ModPackRevision {
  primaryKey?: number;
  timeStampCreated?: string;
  isActive?: boolean;
  updatedByFk?: string;
}

export interface FullModPackDefinition {
  primaryKey?: number;
  gameIdentifier?: string;
  title?: string;
  timeStampCreated?: string;
  isActive?: boolean;
  owner: PlainUser | null;
  modPackRevisions: ModPackRevision[];
}

export interface ModPackListOptions {
  skip: number;
  take: number;
  myModPacksOnly: boolean;
  search?: string;
  gameIdentifier?: string;
}

export interface ModPackListSnapshot {
  count: number;
  definitions: FullModPackDefinition[];
}

/** Temporary handwritten view of the normalized content API until OpenAPI is regenerated. */
export interface ModProviderBinding {
  providerIdentifier: string;
  displayName: string;
  acceptedInput: string;
  installerIdentifiers: string[];
  supportsVersionSelector: boolean;
}

/** Temporary handwritten view of a game-owned mod-pack exchange format. */
export interface ModPackFormat {
  identifier: string;
  displayName: string;
  fileExtension: string;
  mediaType: string;
  maximumSizeBytes: number;
}

export type NormalizedGameCapability = Omit<
  GameCapability,
  "modProviderBindings" | "modPackFormats"
> & {
  modProviderBindings?: ModProviderBinding[];
  modPackFormats?: ModPackFormat[];
};

export interface ModVersionCatalogItem {
  primaryKey: string;
  versionNumber: number;
  versionLabel: string;
  versionSelector: string;
  installerIdentifier: string;
  installerConfiguration: string;
  artifactIdentifier?: string | null;
  checksumSha256?: string | null;
  artifactSizeBytes?: number | null;
}

export interface ModCatalogItem {
  primaryKey: string;
  gameIdentifier: string;
  providerIdentifier: string;
  canonicalSourceIdentifier: string;
  title: string;
  usageRole: "Shared" | "ClientOnly" | "ServerOnly";
  versions: ModVersionCatalogItem[];
}

export interface ModSourceDraft {
  input: string;
  providerIdentifier: string;
  canonicalSourceIdentifier: string;
  title: string;
  versionSelector: string;
  installerIdentifiers: string[];
  existingModId: string | null;
}

export interface ModPackFormatPreview {
  title: string;
  entries: ModSourceDraft[];
  warnings: string[];
}

export interface ModPackMembership {
  mods: Array<{
    modFk: string;
    pinnedVersionFk: string | null;
    order: number;
    title: string;
    providerIdentifier: string;
    canonicalSourceIdentifier: string;
    usageRole: string;
  }>;
  dependencies?: Array<{
    childDefinitionFk: number;
    pinnedChildRevisionFk: number | null;
    order: number;
  }>;
}

export interface NormalizedModPackWorkspace {
  definitionId: number;
  gameIdentifier: string;
  title: string;
  isActive: boolean;
  revisions: Array<{
    revisionId: number;
    isActive: boolean;
    timeStampCreated: string;
    updatedByFk: string;
    directModCount: number;
    nestedModPackCount: number;
  }>;
}

export interface NormalizedModPackRevisionInput {
  mods: Array<{ modId: string; pinnedVersionId: string | null }>;
  newMods: Array<{
    title: string;
    providerIdentifier: string;
    source: string;
    versionSelector: string;
    usageRole: string;
    installerIdentifier: string;
    installerConfiguration: string;
  }>;
  dependencies: Array<{ definitionId: number; pinnedRevisionId: number | null }>;
}

function required<T>(value: T | undefined, operation: string): T {
  if (value === undefined) throw new Error(`${operation} returned no response body.`);
  return value;
}

export function latestActiveRevision(
  definition: Pick<FullModPackDefinition, "modPackRevisions">,
): ModPackRevision | undefined {
  return [...definition.modPackRevisions]
    .filter((revision) => revision.isActive ?? true)
    .sort(
      (left, right) =>
        Date.parse(right.timeStampCreated ?? "") - Date.parse(left.timeStampCreated ?? ""),
    )[0];
}

export function modPackMatchesFilter(
  definition: FullModPackDefinition,
  filter: "all" | "no-revisions",
): boolean {
  switch (filter) {
    case "no-revisions":
      return definition.modPackRevisions.length === 0;
    default:
      return true;
  }
}

export function canModifyModPack(user: FullUser | undefined, id: number | string): boolean {
  const value = String(id);
  return (
    hasClaim(user, "adm", "adm:mp") ||
    hasResourceClaim(user, "mp", value) ||
    hasResourceClaim(user, "mp:modify", value)
  );
}

export function canDeleteModPack(user: FullUser | undefined, id: number | string): boolean {
  const value = String(id);
  return (
    hasClaim(user, "adm", "adm:mp") ||
    hasResourceClaim(user, "mp", value) ||
    hasResourceClaim(user, "mp:delete", value)
  );
}

export function canAccessModPack(user: FullUser | undefined, id: number | string): boolean {
  return (
    isAuthenticated(user) &&
    (canManageModPacks(user) || canModifyModPack(user, id) || canDeleteModPack(user, id))
  );
}

export function normalizeModPackSection(section: string | undefined): ModPackWorkspaceSection {
  const normalized = section?.trim().toLocaleLowerCase();
  return normalized === "artifact" ||
    normalized === "edit" ||
    normalized === "revisions" ||
    normalized === "downloads" ||
    normalized === "danger"
    ? normalized
    : "overview";
}

export type ModPackWorkspaceSection =
  "overview" | "artifact" | "edit" | "revisions" | "downloads" | "danger";

export const modPackQueryKeys = {
  all: ["mod-packs"] as const,
  list: (options: ModPackListOptions) => ["mod-packs", "list", options] as const,
  workspace: (id: number) => ["mod-packs", "workspace", id] as const,
  capabilities: ["mod-packs", "capabilities"] as const,
  selector: (gameIdentifier: string, search: string) =>
    ["mod-packs", "selector", gameIdentifier, search] as const,
  mods: (gameIdentifier: string, search: string) =>
    ["mod-packs", "mods", gameIdentifier, search] as const,
  members: (revisionId: number, effective: boolean) =>
    ["mod-packs", "members", revisionId, effective] as const,
};

export const modPackService = {
  async list(options: ModPackListOptions, signal?: AbortSignal): Promise<FullModPackDefinition[]> {
    const games = options.gameIdentifier
      ? [options.gameIdentifier]
      : (await this.capabilities(signal))
          .filter((item) => item.supportsModPacks)
          .map((item) => item.gameIdentifier ?? "")
          .filter(Boolean);
    const results = await Promise.all(
      games.map(async (gameIdentifier) => {
        const query = new URLSearchParams({ gameIdentifier });
        return (
          (await getApiClient().request<
            Array<{
              definitionId: number;
              gameIdentifier: string;
              title: string;
              latestRevisionId: number | null;
            }>
          >(`/game-content/mod-packs?${query}`, { signal })) ?? []
        );
      }),
    );
    return results
      .flat()
      .filter(
        (item) =>
          !options.search?.trim() ||
          item.title.toLocaleLowerCase().includes(options.search.trim().toLocaleLowerCase()),
      )
      .slice(options.skip, options.skip + options.take)
      .map((item) => ({
        primaryKey: item.definitionId,
        gameIdentifier: item.gameIdentifier,
        title: item.title,
        isActive: true,
        owner: null,
        modPackRevisions:
          item.latestRevisionId == null
            ? []
            : [{ primaryKey: item.latestRevisionId, isActive: true }],
      }));
  },

  async count(
    myModPacksOnly: boolean,
    signal?: AbortSignal,
    gameIdentifier?: string,
  ): Promise<number> {
    return (
      await this.list(
        { skip: 0, take: 10000, myModPacksOnly, ...(gameIdentifier ? { gameIdentifier } : {}) },
        signal,
      )
    ).length;
  },

  async snapshot(options: ModPackListOptions, signal?: AbortSignal): Promise<ModPackListSnapshot> {
    const [count, definitions] = await Promise.all([
      this.count(options.myModPacksOnly, signal, options.gameIdentifier),
      this.list(options, signal),
    ]);
    return { count, definitions };
  },

  async capabilities(signal?: AbortSignal): Promise<NormalizedGameCapability[]> {
    return (
      (await getApiClient().request<NormalizedGameCapability[]>(
        apiPath("/game-servers/capabilities"),
        {
          signal,
        },
      )) ?? []
    );
  },

  async mods(
    gameIdentifier: string,
    search: string,
    signal?: AbortSignal,
  ): Promise<ModCatalogItem[]> {
    const query = new URLSearchParams({ gameIdentifier, search, skip: "0", take: "100" });
    return (
      (await getApiClient().request<ModCatalogItem[]>(`/game-content/mods?${query.toString()}`, {
        signal,
      })) ?? []
    );
  },

  async previewSources(
    gameIdentifier: string,
    sources: Array<{ input: string; providerIdentifier?: string; versionSelector?: string }>,
    signal?: AbortSignal,
  ): Promise<ModSourceDraft[]> {
    const result =
      (await getApiClient().request<
        Array<{
          source: {
            providerIdentifier: string;
            canonicalSourceIdentifier: string;
            title: string;
            versionSelector: string;
          };
          existingModId?: string | null;
          installerIdentifiers?: string[];
        }>
      >("/game-content/mods/source-preview", {
        method: "POST",
        body: { gameIdentifier, sources },
        signal,
      })) ?? [];
    return result.map((item, index) => ({
      input: sources[index]?.input ?? item.source.canonicalSourceIdentifier,
      ...item.source,
      existingModId: item.existingModId ?? null,
      installerIdentifiers: item.installerIdentifiers ?? [],
    }));
  },

  async previewFormat(
    gameIdentifier: string,
    formatIdentifier: string,
    file: File,
    signal?: AbortSignal,
  ): Promise<ModPackFormatPreview> {
    const body = new FormData();
    body.append("file", file);
    const result = required(
      await getApiClient().request<{
        title: string;
        entries: Array<{
          title: string;
          source: {
            providerIdentifier: string;
            canonicalSourceIdentifier: string;
            title: string;
            versionSelector: string;
          };
          existingModId?: string | null;
        }>;
        warnings?: string[];
      }>(
        `/game-content/mod-packs/formats/${encodeURIComponent(gameIdentifier)}/${encodeURIComponent(formatIdentifier)}/preview`,
        { method: "POST", body, signal },
      ),
      "Mod-pack format preview",
    );
    return {
      title: result.title,
      warnings: result.warnings ?? [],
      entries: result.entries.map((entry) => ({
        input: entry.source.canonicalSourceIdentifier,
        ...entry.source,
        title: entry.title || entry.source.title,
        existingModId: entry.existingModId ?? null,
        installerIdentifiers: [],
      })),
    };
  },

  formatDownloadPath(revisionId: number, formatIdentifier: string): string {
    return `/game-content/mod-packs/revisions/${encodeURIComponent(String(revisionId))}/formats/${encodeURIComponent(formatIdentifier)}`;
  },

  async createMod(
    payload: {
      gameIdentifier: string;
      title: string;
      providerIdentifier: string;
      source: string;
      usageRole: string;
      version: {
        versionLabel: string;
        versionSelector: string;
        installerIdentifier: string;
        installerConfiguration: string;
        artifactIdentifier?: string;
        checksumSha256?: string;
        artifactSizeBytes?: number;
      };
    },
    signal?: AbortSignal,
  ): Promise<{ primaryKey: string; versionId: string }> {
    return required(
      await getApiClient().request<{ primaryKey: string; versionId: string }>(
        "/game-content/mods",
        { method: "POST", body: payload, signal },
      ),
      "Mod creation",
    );
  },

  async createUploadedArtifact(
    payload: {
      gameIdentifier: string;
      title: string;
      usageRole: string;
      installerIdentifier: string;
      file: File;
    },
    signal?: AbortSignal,
  ): Promise<{ primaryKey: string; versionId: string; artifactIdentifier: string }> {
    const body = new FormData();
    body.append("gameIdentifier", payload.gameIdentifier);
    body.append("title", payload.title);
    body.append("usageRole", payload.usageRole);
    body.append("installerIdentifier", payload.installerIdentifier);
    body.append("file", payload.file);
    return required(
      await getApiClient().request<{
        primaryKey: string;
        versionId: string;
        artifactIdentifier: string;
      }>("/game-content/mods/uploaded-artifact", { method: "POST", body, signal }),
      "Uploaded artifact creation",
    );
  },

  async createNormalized(
    payload: { gameIdentifier: string; title: string; revision: NormalizedModPackRevisionInput },
    signal?: AbortSignal,
  ): Promise<{ definitionId: number; revisionId: number }> {
    return required(
      await getApiClient().request<{ definitionId: number; revisionId: number }>(
        "/game-content/mod-packs",
        { method: "POST", body: payload, signal },
      ),
      "Mod-pack creation",
    );
  },

  async appendRevision(
    modPackId: number,
    payload: NormalizedModPackRevisionInput,
    signal?: AbortSignal,
  ): Promise<{ revisionId: number }> {
    return required(
      await getApiClient().request<{ revisionId: number }>(
        `/game-content/mod-packs/${encodeURIComponent(String(modPackId))}/revisions`,
        { method: "POST", body: payload, signal },
      ),
      "Mod-pack revision creation",
    );
  },

  async normalizedWorkspace(
    modPackId: number,
    signal?: AbortSignal,
  ): Promise<NormalizedModPackWorkspace | undefined> {
    return await getApiClient().request<NormalizedModPackWorkspace>(
      `/game-content/mod-packs/${encodeURIComponent(String(modPackId))}`,
      { signal, expectedStatuses: [404] },
    );
  },

  async members(
    revisionId: number,
    effective: boolean,
    signal?: AbortSignal,
  ): Promise<ModPackMembership> {
    const result = required(
      await getApiClient().request<
        Omit<ModPackMembership, "mods"> & {
          mods: Array<
            | ModPackMembership["mods"][number]
            | { member: ModPackMembership["mods"][number]; effectiveOrder: number }
          >;
        }
      >(
        `/game-content/mod-packs/revisions/${encodeURIComponent(String(revisionId))}/members?effective=${effective}`,
        { signal },
      ),
      "Mod-pack membership",
    );
    return {
      ...result,
      mods: result.mods.map((item) => ("member" in item ? item.member : item)),
    };
  },

  async deactivate(id: number, signal?: AbortSignal): Promise<void> {
    await getApiClient().request<void>(`/mod-packs/${encodeURIComponent(String(id))}/delete`, {
      method: "POST",
      signal,
    });
  },

  download: (path: string, signal?: AbortSignal) => getApiClient().download(path, signal),
};
