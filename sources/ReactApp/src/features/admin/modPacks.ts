import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { FullUser } from "@/api/types";
import { canManageModPacks, hasClaim, hasResourceClaim, isAuthenticated } from "@/auth/claims";
import type { components } from "@/generated/api";

type Schema<Name extends keyof components["schemas"]> = components["schemas"][Name];

export type ModPackDefinition = Schema<"ModPackDefinitionDto">;
export type ModPackRevision = Schema<"PlainModPackRevisionDto">;
export type PlainModPackDefinition = Schema<"PlainModPackDefinitionDto">;
export type ModPackDownloadAudit = Schema<"ModPackDownloadAuditDto">;
export type GameCapability = Schema<"GameServerCapabilityDto">;
export type PlainUser = Schema<"PlainUserDto">;

export interface FullModPackDefinition extends Omit<
  Schema<"FullModPackDefinitionDto">,
  "owner" | "modPackRevisions"
> {
  owner: PlainUser | null;
  modPackRevisions: ModPackRevision[];
}

export interface ModPackCompositionMember extends Omit<
  Schema<"ModPackWorkspaceCompositionMemberDto">,
  "definition" | "owner" | "revision"
> {
  definition: PlainModPackDefinition;
  owner: PlainUser | null;
  revision: ModPackRevision;
}

export interface ModPackWorkspace extends Omit<
  Schema<"ModPackWorkspaceDto">,
  | "definition"
  | "owner"
  | "latestRevision"
  | "compositionMembers"
  | "revisionPreview"
  | "downloadPreview"
> {
  definition: ModPackDefinition;
  owner: PlainUser | null;
  latestRevision: ModPackRevision | null;
  compositionMembers: ModPackCompositionMember[];
  revisionPreview: ModPackRevision[];
  downloadPreview: ModPackDownloadAudit[];
  eventUsageCount: number;
  downloadRecordCount: number;
  downloadUserCount: number;
  lastDownloaded: string | null;
  canModify: boolean;
  canDelete: boolean;
  canViewLedgers: boolean;
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

function required<T>(value: T | undefined, operation: string): T {
  if (value === undefined) throw new Error(`${operation} returned no response body.`);
  return value;
}

export function normalizeFullModPack(
  definition: Schema<"FullModPackDefinitionDto">,
): FullModPackDefinition {
  return {
    ...definition,
    owner: definition.owner ?? null,
    modPackRevisions: definition.modPackRevisions ?? [],
  };
}

export function normalizeModPackWorkspace(
  workspace: Schema<"ModPackWorkspaceDto">,
): ModPackWorkspace | undefined {
  if (!workspace.definition) return undefined;
  return {
    ...workspace,
    definition: workspace.definition,
    owner: workspace.owner ?? null,
    latestRevision: workspace.latestRevision ?? null,
    compositionMembers: (workspace.compositionMembers ?? [])
      .filter(
        (
          member,
        ): member is typeof member & {
          definition: PlainModPackDefinition;
          revision: ModPackRevision;
        } => Boolean(member.definition && member.revision),
      )
      .map((member) => ({
        ...member,
        definition: member.definition,
        owner: member.owner ?? null,
        revision: member.revision,
      })),
    revisionPreview: workspace.revisionPreview ?? [],
    downloadPreview: workspace.downloadPreview ?? [],
    eventUsageCount: workspace.eventUsageCount ?? 0,
    downloadRecordCount: workspace.downloadRecordCount ?? 0,
    downloadUserCount: workspace.downloadUserCount ?? 0,
    lastDownloaded: workspace.lastDownloaded ?? null,
    canModify: workspace.canModify ?? false,
    canDelete: workspace.canDelete ?? false,
    canViewLedgers: workspace.canViewLedgers ?? false,
  };
}

function listQuery(options: ModPackListOptions): string {
  const query = new URLSearchParams({
    skip: String(options.skip),
    take: String(options.take),
    myModPacksOnly: String(options.myModPacksOnly),
  });
  if (options.search?.trim()) query.set("search", options.search.trim());
  if (options.gameIdentifier?.trim()) query.set("gameIdentifier", options.gameIdentifier.trim());
  return query.toString();
}

function countQuery(myModPacksOnly: boolean, gameIdentifier?: string): string {
  const query = new URLSearchParams({ myModPacksOnly: String(myModPacksOnly) });
  if (gameIdentifier?.trim()) query.set("gameIdentifier", gameIdentifier.trim());
  return query.toString();
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

export function isSelectableStandalone(definition: FullModPackDefinition): boolean {
  return (
    !(definition.isComposition ?? false) && latestActiveRevision(definition)?.primaryKey != null
  );
}

export function modPackMatchesFilter(
  definition: FullModPackDefinition,
  filter: "all" | "compositions" | "standalone" | "no-revisions",
): boolean {
  switch (filter) {
    case "compositions":
      return definition.isComposition ?? false;
    case "standalone":
      return !(definition.isComposition ?? false);
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

export function modPackDownloadPath(workspace: ModPackWorkspace): string {
  const id = workspace.definition.primaryKey;
  if (id == null) throw new Error("The mod pack has no identifier.");
  return workspace.definition.revisionPrimaryKey != null && !workspace.definition.isComposition
    ? fillApiPath("/mod-packs/{modPackDefinitionId}/download/{modPackRevisionId}", {
        modPackDefinitionId: id,
        modPackRevisionId: workspace.definition.revisionPrimaryKey,
      })
    : fillApiPath("/mod-packs/{modPackDefinitionId}/download/latest", {
        modPackDefinitionId: id,
      });
}

export const modPackQueryKeys = {
  all: ["mod-packs"] as const,
  list: (options: ModPackListOptions) => ["mod-packs", "list", options] as const,
  workspace: (id: number) => ["mod-packs", "workspace", id] as const,
  capabilities: ["mod-packs", "capabilities"] as const,
  selector: (gameIdentifier: string, search: string) =>
    ["mod-packs", "selector", gameIdentifier, search] as const,
};

export const modPackService = {
  async list(options: ModPackListOptions, signal?: AbortSignal): Promise<FullModPackDefinition[]> {
    const result = await getApiClient().request<Schema<"FullModPackDefinitionDto">[]>(
      apiPathWithQuery("/mod-packs/all", listQuery(options)),
      { signal },
    );
    return (result ?? []).map(normalizeFullModPack);
  },

  async count(
    myModPacksOnly: boolean,
    signal?: AbortSignal,
    gameIdentifier?: string,
  ): Promise<number> {
    return (
      (await getApiClient().request<number>(
        apiPathWithQuery("/mod-packs/all/count", countQuery(myModPacksOnly, gameIdentifier)),
        { signal },
      )) ?? 0
    );
  },

  async snapshot(options: ModPackListOptions, signal?: AbortSignal): Promise<ModPackListSnapshot> {
    const [count, definitions] = await Promise.all([
      this.count(options.myModPacksOnly, signal, options.gameIdentifier),
      this.list(options, signal),
    ]);
    return { count, definitions };
  },

  async capabilities(signal?: AbortSignal): Promise<GameCapability[]> {
    return (
      (await getApiClient().request<GameCapability[]>(apiPath("/game-servers/capabilities"), {
        signal,
      })) ?? []
    );
  },

  async workspace(id: number, signal?: AbortSignal): Promise<ModPackWorkspace | undefined> {
    const result = await getApiClient().request<Schema<"ModPackWorkspaceDto">>(
      fillApiPath("/mod-packs/{modPackId}/workspace", { modPackId: id }),
      { signal, expectedStatuses: [404] },
    );
    return result ? normalizeModPackWorkspace(result) : undefined;
  },

  async full(id: number, signal?: AbortSignal): Promise<FullModPackDefinition | undefined> {
    const result = await getApiClient().request<Schema<"FullModPackDefinitionDto">>(
      fillApiPath("/mod-packs/{modPackId}/full", { modPackId: id }),
      { signal, expectedStatuses: [404] },
    );
    return result ? normalizeFullModPack(result) : undefined;
  },

  async createStandalone(
    payload: Schema<"ModPackCreationPayload">,
    signal?: AbortSignal,
  ): Promise<PlainModPackDefinition> {
    const path = "/mod-packs/create/standalone" as const;
    const result = await getApiClient().request<PlainModPackDefinition>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: payload,
      signal,
    });
    return required(result, "Standalone mod-pack creation");
  },

  async createComposition(
    payload: Schema<"ModPackCompositionCreationPayload">,
    signal?: AbortSignal,
  ): Promise<PlainModPackDefinition> {
    const path = "/mod-packs/create/composition" as const;
    const result = await getApiClient().request<PlainModPackDefinition>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: payload,
      signal,
    });
    return required(result, "Composition mod-pack creation");
  },

  async updateStandalone(
    id: number,
    payload: Schema<"ModPackStandaloneUpdate">,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/mod-packs/{modPackId}/update/standalone" as const;
    await getApiClient().request<void>(fillApiPath(path, { modPackId: id }), {
      method: apiMethod(path, "post"),
      body: payload,
      signal,
    });
  },

  async updateComposition(
    id: number,
    payload: Schema<"ModPackCompositionUpdate">,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/mod-packs/{modPackId}/update/composition" as const;
    await getApiClient().request<void>(fillApiPath(path, { modPackId: id }), {
      method: apiMethod(path, "post"),
      body: payload,
      signal,
    });
  },

  async deactivate(id: number, signal?: AbortSignal): Promise<void> {
    const path = "/mod-packs/{modPackId}/delete" as const;
    await getApiClient().request<void>(fillApiPath(path, { modPackId: id }), {
      method: apiMethod(path, "post"),
      signal,
    });
  },

  download: (path: string, signal?: AbortSignal) => getApiClient().download(path, signal),
};
