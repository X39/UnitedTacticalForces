import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type {
  ConfigurationProfileAccess,
  ConfigurationProfileAccessMatrix,
  ConfigurationProfileCatalog,
  ConfigurationProfileDetails,
  ConfigurationProfileOption,
  ConfigurationProfileRoleCandidate,
  ConfigurationProfileSummary,
  ConfigurationProfileUserCandidate,
  ConfigurationValue,
  GameConfiguration,
  GameContentOption,
  GameServer,
  GameServerSecretStatus,
  GameServerSecretValue,
  HostingFile,
  HostingFileContext,
  HostingOperation,
  LifecycleJob,
  PreservationSelection,
  PreservationSelectionInput,
  ServerCapability,
  ServerCreation,
  ServerCreationCostOwnerOption,
  ServerNotification,
  ServerModPackOption,
  StorageDemand,
} from "@/features/hosting/types";

function required<T>(value: T | undefined, operation: string): T {
  if (value === undefined) throw new Error(`${operation} returned no response body.`);
  return value;
}

function normalizeServer(server: GameServer): GameServer {
  const deploymentStatuses = [
    "Provisioning",
    "WaitingForWorker",
    "Preparing",
    "Running",
    "Stopping",
    "PreservationFailed",
    "Terminating",
    "Stopped",
    "TerminatingDueToFault",
    "TerminatedDueToFault",
    "Failed",
    "Terminated",
  ] as const;
  const rawStatus = server.deployment?.status as unknown;
  const numericStatus =
    typeof rawStatus === "number"
      ? rawStatus
      : typeof rawStatus === "string" && /^\d+$/.test(rawStatus)
        ? Number(rawStatus)
        : undefined;
  const normalizedStatus =
    numericStatus === undefined ? rawStatus : (deploymentStatuses[numericStatus] ?? rawStatus);
  return {
    ...server,
    actions: server.actions ?? [],
    deployment: server.deployment
      ? {
          ...server.deployment,
          status: normalizedStatus as NonNullable<GameServer["deployment"]>["status"],
          resources: server.deployment.resources ?? [],
        }
      : null,
  };
}

function normalizeCapability(capability: ServerCapability): ServerCapability {
  return {
    ...capability,
    actions: capability.actions ?? [],
    runtimeProviders: capability.runtimeProviders ?? [],
    requiresTerrain: capability.requiresTerrain ?? false,
    terrainLabel: capability.terrainLabel ?? "Terrain",
    requiresScenario: capability.requiresScenario ?? false,
    scenarioLabel: capability.scenarioLabel ?? "Scenario",
    supportsModPacks: capability.supportsModPacks ?? true,
    requiresModPack: capability.requiresModPack ?? true,
    terrainSourceKinds: capability.terrainSourceKinds ?? [],
    modInstallerIdentifiers: capability.modInstallerIdentifiers ?? [],
    secrets: capability.secrets ?? [],
    runtimeProviderProfiles: (capability.runtimeProviderProfiles ?? []).map((provider) => ({
      ...provider,
      profiles: provider.profiles ?? [],
      storage: provider.storage ?? null,
    })),
  };
}

function normalizeJob(job: LifecycleJob): LifecycleJob {
  return { ...job, commands: job.commands ?? [] };
}

function withQuery(path: string, values: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== "") query.set(key, String(value));
  });
  const suffix = query.toString();
  return suffix ? `${path}?${suffix}` : path;
}

export const hostingQueryKeys = {
  fleet: ["hosting", "servers"] as const,
  notifications: ["hosting", "notifications"] as const,
  capabilities: ["hosting", "capabilities"] as const,
  server: (serverId: string) => ["hosting", "servers", serverId] as const,
  jobs: (serverId: string) => ["hosting", "servers", serverId, "jobs"] as const,
  operations: (serverId: string) => ["hosting", "servers", serverId, "operations"] as const,
  configuration: (serverId: string) => ["hosting", "servers", serverId, "configuration"] as const,
  configurationOptions: (serverId: string) =>
    ["hosting", "servers", serverId, "configuration", "options"] as const,
  files: (serverId: string, context: HostingFileContext, path: string) =>
    ["hosting", "servers", serverId, "files", context, path] as const,
  preservation: (serverId: string) => ["hosting", "servers", serverId, "preservation"] as const,
  storageDemand: (serverId: string) => ["hosting", "servers", serverId, "storage-demand"] as const,
  modPackOptions: (serverId: string) => ["hosting", "servers", serverId, "mod-packs"] as const,
  creationModPacks: (gameIdentifier: string) =>
    ["hosting", "creation", gameIdentifier, "mod-packs"] as const,
  terrains: (gameIdentifier: string) => ["hosting", "content", gameIdentifier, "terrains"] as const,
  scenarios: (gameIdentifier: string) =>
    ["hosting", "content", gameIdentifier, "scenarios"] as const,
  secrets: (serverId: string) => ["hosting", "servers", serverId, "secrets"] as const,
  profileOptions: (gameIdentifier: string) =>
    ["hosting", "profiles", "options", gameIdentifier] as const,
  storageEstimate: (gameIdentifier: string, provider: string, modPackId: number | null) =>
    ["hosting", "storage", "estimate", gameIdentifier, provider, modPackId] as const,
  profiles: ["hosting", "configuration-profiles"] as const,
  profileCatalog: ["hosting", "configuration-profiles", "catalog"] as const,
  profile: (profileId: string) => ["hosting", "configuration-profiles", profileId] as const,
  profileAccess: (profileId: string) =>
    ["hosting", "configuration-profiles", profileId, "access"] as const,
};

export const hostingService = {
  async servers(signal?: AbortSignal): Promise<GameServer[]> {
    const result = await getApiClient().request<GameServer[]>(apiPath("/game-servers"), { signal });
    return (result ?? []).map(normalizeServer);
  },

  async notifications(signal?: AbortSignal): Promise<ServerNotification[]> {
    return (
      (await getApiClient().request<ServerNotification[]>(apiPath("/game-servers/notifications"), {
        signal,
      })) ?? []
    );
  },

  async capabilities(signal?: AbortSignal): Promise<ServerCapability[]> {
    const result = await getApiClient().request<ServerCapability[]>(
      apiPath("/game-servers/capabilities"),
      { signal },
    );
    return (result ?? []).map(normalizeCapability);
  },

  async server(serverId: string, signal?: AbortSignal): Promise<GameServer | undefined> {
    const result = await getApiClient().request<GameServer>(
      fillApiPath("/game-servers/{gameServerId}", { gameServerId: serverId }),
      { signal, expectedStatuses: [404] },
    );
    return result ? normalizeServer(result) : undefined;
  },

  async jobs(serverId: string, signal?: AbortSignal): Promise<LifecycleJob[]> {
    const result = await getApiClient().request<LifecycleJob[]>(
      fillApiPath("/game-servers/{gameServerId}/jobs", { gameServerId: serverId }),
      { signal },
    );
    return (result ?? []).map(normalizeJob);
  },

  async operations(serverId: string, take = 80, signal?: AbortSignal): Promise<HostingOperation[]> {
    const path = fillApiPath("/game-servers/{gameServerId}/operations", {
      gameServerId: serverId,
    });
    const result = await getApiClient().request<HostingOperation[]>(withQuery(path, { take }), {
      signal,
    });
    return result ?? [];
  },

  async configuration(serverId: string, signal?: AbortSignal): Promise<GameConfiguration> {
    const result = await getApiClient().request<GameConfiguration>(
      fillApiPath("/game-servers/{gameServerId}/configuration", { gameServerId: serverId }),
      { signal },
    );
    const value = required(result, "Server configuration");
    return { ...value, entries: value.entries ?? [] };
  },

  async updateConfiguration(
    serverId: string,
    values: ConfigurationValue[],
    resetKeys: string[],
    signal?: AbortSignal,
  ): Promise<GameConfiguration> {
    const path = "/game-servers/{gameServerId}/configuration" as const;
    const result = await getApiClient().request<GameConfiguration>(
      fillApiPath(path, { gameServerId: serverId }),
      { method: apiMethod(path, "put"), body: { values, resetKeys }, signal },
    );
    const value = required(result, "Configuration update");
    return { ...value, entries: value.entries ?? [] };
  },

  async selectConfigurationProfile(
    serverId: string,
    profileId: string | null,
    pinnedRevisionId: string | null,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/configuration/profile" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { profileId, pinnedRevisionId },
      signal,
    });
  },

  async serverConfigurationProfileOptions(
    serverId: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileOption[]> {
    const result = await getApiClient().request<ConfigurationProfileOption[]>(
      fillApiPath("/game-servers/{gameServerId}/configuration/profile-options", {
        gameServerId: serverId,
      }),
      { signal },
    );
    return result ?? [];
  },

  async storageDemand(serverId: string, signal?: AbortSignal): Promise<StorageDemand> {
    const result = await getApiClient().request<StorageDemand>(
      fillApiPath("/game-servers/{gameServerId}/storage-demand", { gameServerId: serverId }),
      { signal },
    );
    const demand = required(result, "Server storage estimate");
    return {
      ...demand,
      components: demand.components ?? [],
      runtimeProfiles: demand.runtimeProfiles ?? [],
      missingComponents: demand.missingComponents ?? [],
    };
  },

  async changeRuntimeProfile(
    serverId: string,
    runtimeProfileIdentifier: string,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/runtime-profile" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { runtimeProfileIdentifier },
      signal,
    });
  },

  async changeRuntimeStorage(
    serverId: string,
    strategy: string,
    externalStorageSizeGiB: number | null,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/runtime-storage" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { strategy, externalStorageSizeGiB },
      signal,
    });
  },

  async modPackOptions(serverId: string, signal?: AbortSignal): Promise<ServerModPackOption[]> {
    const result = await getApiClient().request<ServerModPackOption[]>(
      fillApiPath("/game-servers/{gameServerId}/mod-pack-options", { gameServerId: serverId }),
      { signal },
    );
    return result ?? [];
  },

  async changeModPack(
    serverId: string,
    modPackDefinitionId: number | null,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/mod-pack" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { modPackDefinitionId },
      signal,
    });
  },

  async changeContentSelection(
    serverId: string,
    terrainId: number | null,
    scenarioId: string | null,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/content-selection" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { terrainId, terrainVersionId: null, scenarioId, scenarioVersionId: null },
      signal,
    });
  },

  async files(
    serverId: string,
    context: HostingFileContext,
    filePath = "",
    signal?: AbortSignal,
  ): Promise<HostingFile[]> {
    const base = fillApiPath("/game-servers/{gameServerId}/files/{context}", {
      gameServerId: serverId,
      context,
    });
    const result = await getApiClient().request<HostingFile[]>(
      withQuery(base, { path: filePath }),
      {
        signal,
      },
    );
    return result ?? [];
  },

  fileDownloadUrl(serverId: string, context: HostingFileContext, filePath: string): string {
    const base = fillApiPath("/game-servers/{gameServerId}/files/{context}/content", {
      gameServerId: serverId,
      context,
    });
    return getApiClient()
      .url(withQuery(base, { path: filePath }))
      .toString();
  },

  async uploadFile(
    serverId: string,
    context: HostingFileContext,
    filePath: string,
    file: Blob,
    confirmGeneratedOverride: boolean,
    signal?: AbortSignal,
  ): Promise<HostingFile> {
    const base = fillApiPath("/game-servers/{gameServerId}/files/{context}", {
      gameServerId: serverId,
      context,
    });
    const path = withQuery(base, {
      path: filePath,
      confirmGeneratedOverride: String(confirmGeneratedOverride),
    });
    const result = await getApiClient().request<HostingFile>(path, {
      method: "PUT",
      body: file,
      signal,
    });
    return required(result, "File upload");
  },

  async createDirectory(
    serverId: string,
    context: HostingFileContext,
    filePath: string,
    signal?: AbortSignal,
  ): Promise<HostingFile> {
    const path = "/game-servers/{gameServerId}/files/{context}" as const;
    const result = await getApiClient().request<HostingFile>(
      fillApiPath(path, { gameServerId: serverId, context }),
      { method: apiMethod(path, "post"), body: { path: filePath }, signal },
    );
    return required(result, "Directory creation");
  },

  async deleteFile(
    serverId: string,
    context: HostingFileContext,
    filePath: string,
    recursive: boolean,
    signal?: AbortSignal,
  ): Promise<void> {
    const template = "/game-servers/{gameServerId}/files/{context}" as const;
    const base = fillApiPath(template, { gameServerId: serverId, context });
    await getApiClient().request<void>(
      withQuery(base, { path: filePath, recursive: String(recursive) }),
      { method: apiMethod(template, "delete"), signal },
    );
  },

  async preservationSelections(
    serverId: string,
    signal?: AbortSignal,
  ): Promise<PreservationSelection[]> {
    const result = await getApiClient().request<PreservationSelection[]>(
      fillApiPath("/game-servers/{gameServerId}/preservation-selections", {
        gameServerId: serverId,
      }),
      { signal },
    );
    return result ?? [];
  },

  async savePreservationSelection(
    serverId: string,
    payload: PreservationSelectionInput,
    signal?: AbortSignal,
  ): Promise<PreservationSelection> {
    const path = "/game-servers/{gameServerId}/preservation-selections" as const;
    const result = await getApiClient().request<PreservationSelection>(
      fillApiPath(path, { gameServerId: serverId }),
      { method: apiMethod(path, "post"), body: payload, signal },
    );
    return required(result, "Preservation selection");
  },

  async deletePreservationSelection(
    serverId: string,
    selectionId: string,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/preservation-selections/{selectionId}" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId, selectionId }), {
      method: apiMethod(path, "delete"),
      signal,
    });
  },

  async executeAction(
    serverId: string,
    actionId: string,
    confirmation?: string,
    signal?: AbortSignal,
  ): Promise<LifecycleJob> {
    const path = "/game-servers/{gameServerId}/actions/{actionId}" as const;
    const result = await getApiClient().request<LifecycleJob>(
      fillApiPath(path, { gameServerId: serverId, actionId }),
      {
        method: apiMethod(path, "post"),
        body: { confirmation: confirmation ?? null },
        signal,
      },
    );
    return normalizeJob(required(result, "Server action"));
  },

  async retryJob(jobId: string, signal?: AbortSignal): Promise<LifecycleJob> {
    const path = "/jobs/{lifecycleJobId}/retry" as const;
    const result = await getApiClient().request<LifecycleJob>(
      fillApiPath(path, { lifecycleJobId: jobId }),
      { method: apiMethod(path, "post"), signal },
    );
    return normalizeJob(required(result, "Job retry"));
  },

  async skipJob(jobId: string, signal?: AbortSignal): Promise<LifecycleJob> {
    const path = "/jobs/{lifecycleJobId}/skip" as const;
    const result = await getApiClient().request<LifecycleJob>(
      fillApiPath(path, { lifecycleJobId: jobId }),
      { method: apiMethod(path, "post"), signal },
    );
    return normalizeJob(required(result, "Job skip"));
  },

  async create(payload: ServerCreation, signal?: AbortSignal): Promise<GameServer> {
    const path = "/game-servers" as const;
    const result = await getApiClient().request<GameServer>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: payload,
      signal,
    });
    return normalizeServer(required(result, "Server creation"));
  },

  async delete(serverId: string, signal?: AbortSignal): Promise<void> {
    const path = "/game-servers/{gameServerId}" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "delete"),
      signal,
    });
  },

  async creationModPacks(gameIdentifier: string, signal?: AbortSignal) {
    const path = apiPathWithQuery(
      "/game-servers/creation/mod-pack-options",
      new URLSearchParams({ gameIdentifier }).toString(),
    );
    return (
      (await getApiClient().request<{ primaryKey: number; title: string }[]>(path, { signal })) ??
      []
    );
  },

  async terrains(gameIdentifier: string, signal?: AbortSignal): Promise<GameContentOption[]> {
    const path = apiPathWithQuery(
      "/game-content/terrains",
      new URLSearchParams({ gameIdentifier }).toString(),
    );
    const result = await getApiClient().request<GameContentOption[]>(path, { signal });
    return (result ?? []).map((item) => ({ ...item, versions: item.versions ?? [] }));
  },

  async scenarios(gameIdentifier: string, signal?: AbortSignal): Promise<GameContentOption[]> {
    const path = apiPathWithQuery(
      "/game-content/scenarios",
      new URLSearchParams({ gameIdentifier }).toString(),
    );
    const result = await getApiClient().request<GameContentOption[]>(path, { signal });
    return (result ?? []).map((item) => ({ ...item, versions: item.versions ?? [] }));
  },

  async secrets(serverId: string, signal?: AbortSignal): Promise<GameServerSecretStatus[]> {
    return (
      (await getApiClient().request<GameServerSecretStatus[]>(
        fillApiPath("/game-servers/{gameServerId}/secrets", { gameServerId: serverId }),
        { signal },
      )) ?? []
    );
  },

  async setSecret(
    serverId: string,
    key: string,
    value: string,
    externalProvider: boolean,
  ): Promise<void> {
    const path = "/game-servers/{gameServerId}/secrets" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId }), {
      method: apiMethod(path, "put"),
      body: { key, value, origin: externalProvider ? "ExternalProvider" : "UserProvided" },
    });
  },

  async rotateSecret(serverId: string, key: string): Promise<void> {
    const path = "/game-servers/{gameServerId}/secrets/{key}/rotate" as const;
    await getApiClient().request<void>(fillApiPath(path, { gameServerId: serverId, key }), {
      method: apiMethod(path, "post"),
    });
  },

  async revealSecret(serverId: string, key: string): Promise<GameServerSecretValue> {
    const path = "/game-servers/{gameServerId}/secrets/{key}/reveal" as const;
    const result = await getApiClient().request<GameServerSecretValue>(
      fillApiPath(path, { gameServerId: serverId, key }),
      { method: apiMethod(path, "post") },
    );
    return required(result, "Secret reveal");
  },

  async creationCostOwners(signal?: AbortSignal): Promise<ServerCreationCostOwnerOption[]> {
    return (
      (await getApiClient().request<ServerCreationCostOwnerOption[]>(
        apiPath("/game-servers/creation/cost-owner-options"),
        { signal },
      )) ?? []
    );
  },

  async profileOptions(
    gameIdentifier: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileOption[]> {
    const path = apiPathWithQuery(
      "/configuration-profiles/options",
      new URLSearchParams({ gameIdentifier }).toString(),
    );
    return (await getApiClient().request<ConfigurationProfileOption[]>(path, { signal })) ?? [];
  },

  async estimateStorage(
    gameIdentifier: string,
    runtimeProviderIdentifier: string,
    modPackDefinitionId: number | null,
    signal?: AbortSignal,
  ): Promise<StorageDemand> {
    const path = "/game-servers/storage-demand-estimate" as const;
    const result = await getApiClient().request<StorageDemand>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: { gameIdentifier, runtimeProviderIdentifier, modPackDefinitionId },
      signal,
    });
    const demand = required(result, "Storage estimate");
    return {
      ...demand,
      components: demand.components ?? [],
      runtimeProfiles: demand.runtimeProfiles ?? [],
      missingComponents: demand.missingComponents ?? [],
    };
  },

  async profiles(signal?: AbortSignal): Promise<ConfigurationProfileSummary[]> {
    return (
      (await getApiClient().request<ConfigurationProfileSummary[]>(
        apiPath("/configuration-profiles"),
        { signal },
      )) ?? []
    );
  },

  async profileCatalog(signal?: AbortSignal): Promise<ConfigurationProfileCatalog> {
    const result = await getApiClient().request<ConfigurationProfileCatalog>(
      apiPath("/configuration-profiles/definitions"),
      { signal },
    );
    return required(result, "Configuration profile catalog");
  },

  async profile(profileId: string, signal?: AbortSignal): Promise<ConfigurationProfileDetails> {
    const result = await getApiClient().request<ConfigurationProfileDetails>(
      fillApiPath("/configuration-profiles/{profileId}", { profileId }),
      { signal },
    );
    return required(result, "Configuration profile");
  },

  async profileAccess(
    profileId: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileAccess> {
    const result = await getApiClient().request<ConfigurationProfileAccess>(
      fillApiPath("/configuration-profiles/{profileId}/access", { profileId }),
      { signal },
    );
    const access = required(result, "Configuration profile access");
    return {
      allowedPermissions: access.allowedPermissions ?? [],
      users: (access.users ?? []).map((user) => ({ ...user, permissions: user.permissions ?? [] })),
      roles: (access.roles ?? []).map((role) => ({ ...role, permissions: role.permissions ?? [] })),
    };
  },

  async createProfile(
    title: string,
    gameIdentifier: string,
    values: ConfigurationValue[],
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileSummary> {
    const path = "/configuration-profiles" as const;
    const result = await getApiClient().request<ConfigurationProfileSummary>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: { title, gameIdentifier, values },
      signal,
    });
    return required(result, "Configuration profile creation");
  },

  async createProfileRevision(
    profileId: string,
    baseRevisionNumber: number,
    values: ConfigurationValue[],
    resetKeys: string[],
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/configuration-profiles/{profileId}/revisions" as const;
    await getApiClient().request<void>(fillApiPath(path, { profileId }), {
      method: apiMethod(path, "post"),
      body: { baseRevisionNumber, values, resetKeys },
      signal,
    });
  },

  async profileUserCandidates(
    profileId: string,
    search: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileUserCandidate[]> {
    const route = "/configuration-profiles/{profileId}/access/users/options" as const;
    const path = fillApiPath(route, { profileId });
    const query = new URLSearchParams({ search, take: "20" });
    return (
      (await getApiClient().request<ConfigurationProfileUserCandidate[]>(
        `${path}?${query.toString()}`,
        { method: apiMethod(route, "get"), signal },
      )) ?? []
    );
  },

  async profileRoleCandidates(
    profileId: string,
    search: string,
    signal?: AbortSignal,
  ): Promise<ConfigurationProfileRoleCandidate[]> {
    const route = "/configuration-profiles/{profileId}/access/roles/options" as const;
    const path = fillApiPath(route, { profileId });
    const query = new URLSearchParams({ search, take: "20" });
    return (
      (await getApiClient().request<ConfigurationProfileRoleCandidate[]>(
        `${path}?${query.toString()}`,
        { method: apiMethod(route, "get"), signal },
      )) ?? []
    );
  },

  async updateProfileAccessMatrix(
    profileId: string,
    matrix: ConfigurationProfileAccessMatrix,
    signal?: AbortSignal,
  ): Promise<void> {
    const route = "/configuration-profiles/{profileId}/access/matrix" as const;
    await getApiClient().request<void>(fillApiPath(route, { profileId }), {
      method: apiMethod(route, "post"),
      body: matrix,
      signal,
    });
  },

  async archiveProfile(profileId: string, signal?: AbortSignal): Promise<void> {
    const path = "/configuration-profiles/{profileId}/archive" as const;
    await getApiClient().request<void>(fillApiPath(path, { profileId }), {
      method: apiMethod(path, "post"),
      signal,
    });
  },

  async changeProfileUserAccess(
    profileId: string,
    userId: string,
    permission: string,
    grant: boolean,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = grant
      ? ("/configuration-profiles/{profileId}/access/users/grant" as const)
      : ("/configuration-profiles/{profileId}/access/users/revoke" as const);
    await getApiClient().request<void>(fillApiPath(path, { profileId }), {
      method: apiMethod(path, "post"),
      body: { userId, permission },
      signal,
    });
  },

  async changeProfileRoleAccess(
    profileId: string,
    roleId: number,
    permission: string,
    grant: boolean,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = grant
      ? ("/configuration-profiles/{profileId}/access/roles/grant" as const)
      : ("/configuration-profiles/{profileId}/access/roles/revoke" as const);
    await getApiClient().request<void>(fillApiPath(path, { profileId }), {
      method: apiMethod(path, "post"),
      body: { roleId, permission },
      signal,
    });
  },
};

export type HostingService = typeof hostingService;
