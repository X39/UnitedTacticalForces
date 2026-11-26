import type { Page, Route } from "@playwright/test";
import { workspaceJob, workspaceOperation } from "../../../helpers/workspaceFixtures";
import {
  automationBlock,
  automationServer,
  automationServerId,
  mockAutomationRoutes,
  type AutomationRouteHandle,
  type JsonRecord,
} from "../automationMocks";
import { mockFleetStream } from "../fleetStream";
import { fulfillMetricsRoute, populatedMetricsMock } from "../metricsMocks";
import {
  auditUserId,
  installApp,
  jsonHandler,
  type ApiHandler,
  type Culture,
  type MockSession,
} from "../mockApi";

/**
 * Strict mocks of the server workspace for the registry entries of the workspace slice (plan 69-27).
 *
 * Every workspace entry installs the shared strict layer of `installApp` and adds the handlers of one workspace
 * server. The automation entries use the Phase 68 `automationMocks.ts` builders for the server and the automation
 * block, so there is one description of an automation server. Fixture strings the app prints (titles, setting names,
 * credential names) are German-neutral on purpose: the German walk would otherwise flag an English API text that no
 * locale file can translate.
 */

/** The id of the mocked workspace server; it is the id the Phase 68 automation mocks use. */
export const workspaceServerId = automationServerId;

/** The German-neutral title of the mocked workspace server. */
export const workspaceServerTitle = "Alpha Ops";

/** The base path of every workspace request. */
const base = `/api/game-servers/${workspaceServerId}`;

/** The workspace route of a tab, with the tab as a query string; the Overview has none. */
export function workspaceRoute(tab?: string | undefined): string {
  return tab === undefined
    ? `/command/servers/${workspaceServerId}`
    : `/command/servers/${workspaceServerId}?tab=${tab}`;
}

/** The accrued cost session of a running server, billed hourly, as `GET /accrued-cost` answers it. */
const runningSession = {
  deploymentId: "50000000-0000-4000-8000-000000000b66",
  amount: 0.42,
  currency: "EUR",
  asOf: "2026-11-21T10:00:00Z",
  isFinal: false,
  unavailableReason: null,
  billingUnitSeconds: 3600,
  billedThrough: "2026-11-21T10:40:00Z",
};

/** One mod pack option of the Mods panel. */
interface ModPackOption {
  primaryKey: number;
  title: string;
  isAvailable: boolean;
  unavailableReason: string | null;
}

/** The mod packs the Mods panel offers; the first is the saved one. */
const modPacks: readonly ModPackOption[] = [
  { primaryKey: 1, title: "Core", isAvailable: true, unavailableReason: null },
  { primaryKey: 2, title: "Extended", isAvailable: true, unavailableReason: null },
];

/** The flattened contents of each pack, for the pack comparison of the review dialog. */
const packContents: Readonly<Record<number, readonly { key: string; title: string }[]>> = {
  1: [
    { key: "mod-a", title: "Mod A" },
    { key: "mod-b", title: "Mod B" },
  ],
  2: [
    { key: "mod-b", title: "Mod B" },
    { key: "mod-c", title: "Mod C" },
  ],
};

/**
 * Builds one configuration entry of the mocked game; every field the API sends is present.
 *
 * @param overrides The key, the display name and any field to replace.
 * @returns The entry as the API sends it.
 */
function configurationEntry(
  overrides: { key: string; displayName: string } & JsonRecord,
): JsonRecord {
  return {
    targetPath: "server/server.cfg",
    group: "General",
    description: "",
    kind: "String",
    required: false,
    sensitive: false,
    isControlPlaneOnly: false,
    tier: "Common",
    order: 10,
    minimum: null,
    maximum: null,
    allowedValues: [],
    choices: [],
    value: null,
    source: "Default",
    isCompatible: true,
    isRedacted: false,
    warning: null,
    ...overrides,
  };
}

/** The configuration entries of the mocked game: two Common entries and two Advanced entries. */
const configurationEntries: readonly JsonRecord[] = [
  configurationEntry({
    key: "server.cfg@hostname",
    displayName: "Hostname",
    value: "Alpha",
    order: 10,
  }),
  configurationEntry({
    key: "server.cfg@maxPlayers",
    displayName: "Slots",
    kind: "Integer",
    minimum: 1,
    maximum: 128,
    value: 64,
    order: 20,
  }),
  configurationEntry({
    key: "basic.cfg@MaxMsgSend",
    displayName: "MaxMsgSend",
    kind: "Integer",
    targetPath: "server/basic.cfg",
    group: "Network",
    tier: "Advanced",
    value: 128,
    order: 10,
  }),
  configurationEntry({
    key: "basic.cfg@MaxSizeGuaranteed",
    displayName: "MaxSizeGuaranteed",
    kind: "Integer",
    targetPath: "server/basic.cfg",
    group: "Network",
    tier: "Advanced",
    value: 512,
    order: 20,
  }),
];

/** How many generated entries share one group, so the large configuration states show several groups. */
const generatedEntriesPerGroup = 8;

/**
 * Builds the key of a generated configuration entry.
 *
 * @param index The zero-based position of the generated entry.
 * @returns The key, unique per index.
 */
function generatedEntryKey(index: number): string {
  return `generated.cfg@param${String(index + 1).padStart(2, "0")}`;
}

/**
 * Builds the id of the control of a generated entry the way the app derives it from the entry key
 * (`server-configuration-` followed by the key with every character outside letters and digits replaced by a dash),
 * so a registry state can address one generated field without depending on its translated label.
 *
 * @param index The zero-based position of the generated entry.
 * @returns The DOM id of the entry control.
 */
export function generatedEntryControlId(index: number): string {
  return `server-configuration-${generatedEntryKey(index).replace(/[^a-z0-9]/gi, "-")}`;
}

/** The saved value of every generated entry; a staged value that differs from it is a change. */
export const generatedEntrySavedValue = 50;

/**
 * Builds the generated Common entries a large configuration needs: typed integers with unique keys and display
 * names, grouped by eight, with a range that holds both the saved value and the values the states stage. The names
 * are the same in every culture, so the German walk has no English text to flag.
 *
 * @param count How many entries to generate.
 * @returns The generated entries, in order.
 */
function generatedConfigurationEntries(count: number): JsonRecord[] {
  return Array.from({ length: count }, (_, index) =>
    configurationEntry({
      key: generatedEntryKey(index),
      displayName: `Parameter ${String(index + 1).padStart(2, "0")}`,
      group: `Block ${Math.floor(index / generatedEntriesPerGroup) + 1}`,
      kind: "Integer",
      minimum: 1,
      maximum: 1000,
      value: generatedEntrySavedValue,
      order: 100 + index,
    }),
  );
}

/** The one user-provided credential of the Credentials panel. */
const credentialStatuses: readonly JsonRecord[] = [
  {
    key: "join-password",
    displayName: "Join-Passwort",
    requiredForStart: false,
    generated: false,
    revealable: true,
    externalProvider: false,
    isConfigured: true,
    version: 1,
    origin: "UserProvided",
    timeStampCreated: "2026-11-16T10:00:00Z",
  },
];

/** The capability of the mocked game. */
const capability = {
  gameIdentifier: "arma3",
  displayName: "Arma 3",
  description: "Dedicated Arma operations.",
  runtimeProviders: [],
  actions: [],
  modProviderBindings: [],
  modPackFormats: [],
  requiresTerrain: false,
  terrainLabel: "Terrain",
  requiresScenario: false,
  scenarioLabel: "Mission",
  supportsModPacks: true,
  requiresModPack: true,
  defaultTerrainSourceIdentifier: null,
  defaultScenarioSourceIdentifier: null,
  runtimeProviderProfiles: [],
};

/**
 * How `PUT {base}/cost-owner` answers: `"ok"` replaces the owner and answers 204, a status answers that status with a
 * problem body, and `"hold"` parks the request until {@link WorkspaceControls.releaseTransfers} answers it with the
 * outcome set at that moment, so a state can show the dialog while the transfer is running.
 */
export type CostOwnerTransferOutcome = "ok" | 409 | 400 | 403 | "hold";

/** What one workspace entry changes about the default mocked workspace. */
export interface WorkspaceMockOptions {
  /** Members of the server that replace the defaults of a running, costing server. */
  server?: JsonRecord | undefined;
  /** The automation block; defaults to nothing configured. Pass `null` for a server without a block. */
  automation?: ReturnType<typeof automationBlock> | null | undefined;
  /** The lifecycle jobs of the server; defaults to none. */
  jobs?: readonly JsonRecord[] | undefined;
  /** The hosting operations (audit records) of the server; defaults to none. */
  operations?: readonly JsonRecord[] | undefined;
  /** The undismissed cost alerts of the signed-in user; defaults to none. */
  alerts?: readonly JsonRecord[] | undefined;
  /** Whether the server has a costing session; defaults to true. */
  accrued?: boolean | undefined;
  /** Whether the viewer may manage the credentials; defaults to false. */
  canManageSecrets?: boolean | undefined;
  /** The Settings panel anchor ids to open; every panel by default, an empty array for the collapsed default. */
  settingsPanels?: readonly string[] | undefined;
  /**
   * How many generated typed integer entries to append to the four served configuration entries; none by default.
   * Eight entries or more show the filter toolbar of the Configuration panel, and a large count gives the review
   * dialog enough settings to stage dozens of changes.
   */
  configurationEntryCount?: number | undefined;
  /**
   * The owners `GET {base}/cost-owner/options` offers, as {@link costOwnerOptionRecord} builds them. The answer is
   * filtered by the `search` parameter on the display name and never lists the current owner of the mocked server,
   * as the API does; none by default.
   */
  costOwnerOptions?: readonly JsonRecord[] | undefined;
  /**
   * How `PUT {base}/cost-owner` answers: `"ok"` (the default) replaces the owner of the mocked server with the
   * chosen option and answers 204, so the refetched server and fleet list carry the new owner; a status answers
   * that status with a problem body and leaves the owner untouched, and `"hold"` parks the request.
   */
  costOwnerTransfer?: CostOwnerTransferOutcome | undefined;
  /** Receives the body of every `PUT {base}/cost-owner` the page sends, so a test can assert what was transferred. */
  costOwnerTransferBodies?: JsonRecord[] | undefined;
  /**
   * The mutable access state behind the Access panel, as {@link accessMockState} builds it. With it the mock answers
   * the access list, both candidate searches and the four grant and revoke routes from the state, and the server
   * record carries a `canManageAccess` that follows the state: true while the signed-in member holds Full control or
   * Manage access on the list. Without it the mock answers none of those routes, so an Access panel that mounts anyway
   * fills `session.unexpected`.
   */
  accessState?: AccessMockState | undefined;
  /**
   * Forces `canManageAccess` of the mocked server to true, whatever the access state holds. Without an
   * {@link accessState} the flag is only written to the server record and no access route is answered.
   */
  canManageAccess?: boolean | undefined;
}

/** The permission claims a server can delegate, in catalog order; the access state defaults to all of them. */
export const accessMockPermissions: readonly string[] = [
  "sv",
  "sv:view",
  "sv:operate",
  "sv:modify",
  "sv:manage-access",
  "sv:delete-data",
  "sv:manage-secrets",
  "sv:reveal-secrets",
];

/** One member of the mocked access list, or a candidate that holds no permission yet. */
export interface AccessMockUser {
  /** The member id. */
  userId: string;
  /** The nickname the panel prints. */
  nickname: string;
  /** The fingerprint that tells members with equal nicknames apart. */
  detail: string;
  /** The permission claim ids stored for the member; a candidate has none. */
  permissions: string[];
}

/** One role of the mocked access list, or a candidate that holds no permission yet. */
export interface AccessMockRole {
  /** The numeric role id. */
  roleId: number;
  /** The role title the panel prints. */
  title: string;
  /** The role description. */
  description: string;
  /** The number of members a grant to the role reaches. */
  memberCount: number;
  /** The permission claim ids stored for the role; a candidate has none. */
  permissions: string[];
}

/** One grant or revoke request the page sent, in the order the mock answered it. */
export interface AccessMockCall {
  /** Whether the request addressed a member or a role. */
  kind: "user" | "role";
  /** True for a grant, false for a revoke. */
  grant: boolean;
  /** The member id, or the role id as text. */
  subjectId: string;
  /** The permission claim id of the request. */
  permission: string;
  /** The status the mock answered: 204, or 500 for a permission listed as failing. */
  status: 204 | 500;
}

/** The mutable state behind the mocked Access panel; a test may change it between steps to play another manager. */
export interface AccessMockState {
  /** The members that hold at least one permission, ordered by nickname. */
  users: AccessMockUser[];
  /** The roles that hold at least one permission, ordered by title. */
  roles: AccessMockRole[];
  /** Every permission that can be delegated at all. */
  allowedPermissions: string[];
  /** The permissions the signed-in member may grant and revoke. */
  assignablePermissions: string[];
  /** The members the member search offers; a member who holds a permission is never offered. */
  userCandidates: AccessMockUser[];
  /** The roles the role search offers; a role that holds a permission is never offered. */
  roleCandidates: AccessMockRole[];
  /** Permissions whose grant and revoke the mock refuses with a 500 and leaves unchanged. */
  failingPermissions: Set<string>;
  /** Every grant and revoke request, in order. */
  calls: AccessMockCall[];
  /** How many times `GET {base}/access` was answered. */
  reads: number;
  /**
   * The search text (case-insensitive) whose candidate search is never answered, so the picker stays in its loading
   * state; null answers every search.
   */
  heldSearch: string | null;
  /** The search text (case-insensitive) whose candidate search is refused with a 403; null refuses none. */
  failingSearch: string | null;
}

/** What {@link accessMockState} starts from; every member is optional. */
export interface AccessMockOptions {
  /** The members that hold permissions; the signed-in member is the one with `auditUserId`. */
  users?: readonly AccessMockUser[] | undefined;
  /** The roles that hold permissions. */
  roles?: readonly AccessMockRole[] | undefined;
  /** The delegable permissions; all eight claims by default. */
  allowedPermissions?: readonly string[] | undefined;
  /** The permissions the member may assign; all eight claims by default. */
  assignablePermissions?: readonly string[] | undefined;
  /** The members the search offers. */
  userCandidates?: readonly AccessMockUser[] | undefined;
  /** The roles the search offers. */
  roleCandidates?: readonly AccessMockRole[] | undefined;
  /** The permissions whose changes fail with a 500. */
  failingPermissions?: readonly string[] | undefined;
  /** The search text whose candidate search is never answered. */
  heldSearch?: string | undefined;
  /** The search text whose candidate search is refused. */
  failingSearch?: string | undefined;
}

/**
 * Builds one member record for the access state.
 *
 * @param index A small positive number that makes the member id unique among the members of one test.
 * @param nickname The nickname the panel prints.
 * @param permissions The stored permission claim ids; none for a candidate.
 * @param userId An explicit member id, for the signed-in member (`auditUserId`).
 * @returns The member record with a fingerprint derived from its id.
 */
export function accessUserRecord(
  index: number,
  nickname: string,
  permissions: readonly string[] = [],
  userId?: string,
): AccessMockUser {
  const id = userId ?? `20000000-0000-4000-8000-${String(index).padStart(12, "0")}`;
  return { userId: id, nickname, detail: `#${id.slice(-6)}`, permissions: [...permissions] };
}

/**
 * Builds one role record for the access state.
 *
 * @param roleId The numeric role id, unique among the roles of one test.
 * @param title The role title the panel prints.
 * @param permissions The stored permission claim ids; none for a candidate.
 * @param memberCount The number of members the role reaches.
 * @param description The role description.
 * @returns The role record.
 */
export function accessRoleRecord(
  roleId: number,
  title: string,
  permissions: readonly string[] = [],
  memberCount = 3,
  description = "",
): AccessMockRole {
  return { roleId, title, description, memberCount, permissions: [...permissions] };
}

/**
 * Builds the mutable access state of a test from copies of its options, so one record list never leaks between tests.
 *
 * @param options The listed subjects, the candidates and the failing permissions.
 * @returns The state to pass as `WorkspaceMockOptions.accessState`; read `calls` and `reads` after the run.
 */
export function accessMockState(options: AccessMockOptions = {}): AccessMockState {
  const copyUser = (user: AccessMockUser): AccessMockUser => ({
    ...user,
    permissions: [...user.permissions],
  });
  const copyRole = (role: AccessMockRole): AccessMockRole => ({
    ...role,
    permissions: [...role.permissions],
  });
  return {
    users: (options.users ?? []).map(copyUser),
    roles: (options.roles ?? []).map(copyRole),
    allowedPermissions: [...(options.allowedPermissions ?? accessMockPermissions)],
    assignablePermissions: [...(options.assignablePermissions ?? accessMockPermissions)],
    userCandidates: (options.userCandidates ?? []).map(copyUser),
    roleCandidates: (options.roleCandidates ?? []).map(copyRole),
    failingPermissions: new Set(options.failingPermissions ?? []),
    calls: [],
    reads: 0,
    heldSearch: options.heldSearch ?? null,
    failingSearch: options.failingSearch ?? null,
  };
}

/**
 * Tells whether the signed-in member may manage access according to the access state: the member holds Full control
 * or Manage access directly.
 *
 * @param state The access state.
 * @returns True while the `auditUserId` row holds `sv` or `sv:manage-access`.
 */
function holdsManageAccess(state: AccessMockState): boolean {
  return state.users.some(
    (user) =>
      user.userId === auditUserId &&
      (user.permissions.includes("sv") || user.permissions.includes("sv:manage-access")),
  );
}

/**
 * Builds the handlers of the Access panel: the list, both candidate searches and the four change routes. Every change
 * mutates the state, so the refetch that follows a toggle returns server truth.
 *
 * @param state The access state the handlers read and change.
 * @param canManage Tells whether the signed-in member may manage access right now; a member who may not gets 403.
 * @returns The handlers, to be placed before the baseline handlers.
 */
function accessHandlers(state: AccessMockState, canManage: () => boolean): ApiHandler[] {
  const refuse = async (route: Route, status: number, title: string): Promise<boolean> => {
    await route.fulfill({ status, json: { title, status } });
    return true;
  };
  const listHandler: ApiHandler = async (route, method, path) => {
    if (method !== "GET" || path !== `${base}/access`) return false;
    if (!canManage()) return refuse(route, 403, "Access refused");
    state.reads += 1;
    await route.fulfill({
      json: {
        allowedPermissions: state.allowedPermissions,
        assignablePermissions: state.assignablePermissions,
        users: state.users,
        roles: state.roles,
      },
    });
    return true;
  };
  const searchOf = (url: URL): { search: string; take: number } => ({
    search: (url.searchParams.get("search") ?? "").trim().toLowerCase(),
    take: Number(url.searchParams.get("take") ?? "20"),
  });
  // A held search is never answered and a failing one is refused, so the picker shows its loading and error states.
  const gated =
    (path: string, answer: (url: URL) => unknown): ApiHandler =>
    async (route, method, requestPath, url) => {
      if (method !== "GET" || requestPath !== path) return false;
      const { search } = searchOf(url);
      if (state.failingSearch !== null && search === state.failingSearch.toLowerCase()) {
        return refuse(route, 403, "Search refused");
      }
      if (state.heldSearch !== null && search === state.heldSearch.toLowerCase()) {
        await new Promise<void>(() => undefined);
      }
      await route.fulfill({ json: answer(url) });
      return true;
    };
  const userOptions = gated(`${base}/access/users/options`, (url: URL) => {
    const { search, take } = searchOf(url);
    return state.userCandidates
      .filter(
        (candidate) =>
          !state.users.some((user) => user.userId === candidate.userId) &&
          candidate.nickname.toLowerCase().includes(search),
      )
      .slice(0, take)
      .map(({ userId, nickname, detail }) => ({ userId, nickname, detail }));
  });
  const roleOptions = gated(`${base}/access/roles/options`, (url: URL) => {
    const { search, take } = searchOf(url);
    return state.roleCandidates
      .filter(
        (candidate) =>
          !state.roles.some((role) => role.roleId === candidate.roleId) &&
          candidate.title.toLowerCase().includes(search),
      )
      .slice(0, take)
      .map(({ roleId, title, description, memberCount }) => ({
        roleId,
        title,
        description,
        memberCount,
      }));
  });
  const changeHandler = (kind: "user" | "role", grant: boolean): ApiHandler => {
    const route = `${base}/access/${kind}s/${grant ? "grant" : "revoke"}`;
    return async (request, method, path) => {
      if (method !== "POST" || path !== route) return false;
      if (!canManage()) return refuse(request, 403, "Access refused");
      const body = request.request().postDataJSON() as {
        userId?: string;
        roleId?: number;
        permission: string;
      };
      const subjectId = String(kind === "user" ? body.userId : body.roleId);
      const failing = state.failingPermissions.has(body.permission);
      state.calls.push({
        kind,
        grant,
        subjectId,
        permission: body.permission,
        status: failing ? 500 : 204,
      });
      if (failing) return refuse(request, 500, "Access change failed");
      const known = applyAccessChange(state, kind, subjectId, body.permission, grant);
      if (!known) return refuse(request, 404, "Subject not found");
      await request.fulfill({ status: 204 });
      return true;
    };
  };
  return [
    listHandler,
    userOptions,
    roleOptions,
    changeHandler("user", true),
    changeHandler("user", false),
    changeHandler("role", true),
    changeHandler("role", false),
  ];
}

/**
 * Applies one grant or revoke to the access state the way the API stores it: a granted candidate becomes a listed
 * subject, a subject that loses its last permission leaves the list and becomes a candidate again, and repeating a
 * change changes nothing.
 *
 * @param state The access state to change.
 * @param kind Whether the subject is a member or a role.
 * @param subjectId The member id, or the role id as text.
 * @param permission The permission claim id.
 * @param grant True to grant, false to revoke.
 * @returns False when no listed subject or candidate has that id.
 */
function applyAccessChange(
  state: AccessMockState,
  kind: "user" | "role",
  subjectId: string,
  permission: string,
  grant: boolean,
): boolean {
  if (kind === "user") {
    const listed = state.users.find((user) => user.userId === subjectId);
    const candidate = state.userCandidates.find((user) => user.userId === subjectId);
    const subject = listed ?? candidate;
    if (subject === undefined) return false;
    const next = grant
      ? [...new Set([...subject.permissions, permission])]
      : subject.permissions.filter((held) => held !== permission);
    if (next.length > 0 && listed === undefined) {
      state.users.push({ ...subject, permissions: next });
      state.users.sort((a, b) => a.nickname.localeCompare(b.nickname));
    } else if (next.length > 0) listed!.permissions = next;
    else if (listed !== undefined) {
      state.users = state.users.filter((user) => user !== listed);
      if (candidate === undefined) state.userCandidates.push({ ...listed, permissions: [] });
    }
    return true;
  }
  const listed = state.roles.find((role) => String(role.roleId) === subjectId);
  const candidate = state.roleCandidates.find((role) => String(role.roleId) === subjectId);
  const subject = listed ?? candidate;
  if (subject === undefined) return false;
  const next = grant
    ? [...new Set([...subject.permissions, permission])]
    : subject.permissions.filter((held) => held !== permission);
  if (next.length > 0 && listed === undefined) {
    state.roles.push({ ...subject, permissions: next });
    state.roles.sort((a, b) => a.title.localeCompare(b.title));
  } else if (next.length > 0) listed!.permissions = next;
  else if (listed !== undefined) {
    state.roles = state.roles.filter((role) => role !== listed);
    if (candidate === undefined) state.roleCandidates.push({ ...listed, permissions: [] });
  }
  return true;
}

/** The fixed id prefix of the cost owner options a test builds with {@link costOwnerOptionRecord}. */
const costOwnerOptionIdPrefix = "40000000-0000-4000-8000-0000000";

/**
 * Builds one cost owner option as `GET /cost-owner/options` answers it.
 *
 * @param index A small positive number that makes the owner id unique among the options of one test.
 * @param displayName The owner name the picker, the dialog, the toast and the summary print.
 * @param overrides Members to replace, for example `isFrozen: true`, `isCurrentUser: true` or another `kind`.
 * @returns The option record.
 */
export function costOwnerOptionRecord(
  index: number,
  displayName: string,
  overrides: JsonRecord = {},
): JsonRecord {
  return {
    costOwnerId: `${costOwnerOptionIdPrefix}${String(index).padStart(5, "0")}`,
    displayName,
    isFrozen: false,
    isCurrentUser: false,
    kind: "Verein",
    ...overrides,
  };
}

/**
 * Builds the detail and fleet projection of the mocked workspace server.
 *
 * @param options The members to replace.
 * @returns The server as the API sends it.
 */
export function buildWorkspaceServer(options: WorkspaceMockOptions = {}): JsonRecord {
  return automationServer({
    automation: options.automation === undefined ? automationBlock() : options.automation,
    overrides: {
      title: workspaceServerTitle,
      canManageSecrets: options.canManageSecrets ?? false,
      canRevealSecrets: options.canManageSecrets ?? false,
      selectedModPackDefinitionId: 1,
      selectedModPackDefinitionTitle: "Core",
      pinnedModPackRevisionId: null,
      pinnedConfigurationProfileRevisionId: null,
      ...options.server,
    },
  });
}

/**
 * Builds a Start job that finished and an operation that recorded it, so the recent activity of the Overview and
 * the Activity timeline have entries.
 *
 * @returns The succeeded job and one audit operation, newest first.
 */
export function quietHistory(): { jobs: JsonRecord[]; operations: JsonRecord[] } {
  const job = workspaceJob({
    gameServerId: workspaceServerId,
    kind: "Start",
    actionId: "start",
    status: "Succeeded",
    stage: "completed",
    progressPercent: 100,
    requestedByName: "Alpha",
    timeStampCreated: "2026-11-16T09:50:00Z",
    timeStampUpdated: "2026-11-16T09:55:00Z",
    timeStampStarted: "2026-11-16T09:51:00Z",
    timeStampCompleted: "2026-11-16T09:55:00Z",
  });
  const operation = workspaceOperation({
    kind: "LifecycleJobQueued",
    status: "Succeeded",
    summary: "Lifecycle job queued",
    lifecycleJobId: job.primaryKey,
    actorName: "Alpha",
    timeStampCreated: "2026-11-16T09:50:00Z",
  });
  return { jobs: [job as unknown as JsonRecord], operations: [operation as unknown as JsonRecord] };
}

/**
 * The handle a registry state uses to change what the mocked workspace answers after the page loaded, so one page load
 * can walk through several states of the same screen.
 */
export interface WorkspaceControls {
  /** The access state behind the Access panel, or undefined when the workspace was installed without one. */
  access: AccessMockState | undefined;
  /**
   * Replaces members of the mocked server record. The page shows the change after its next read of the server, for
   * example through the Refresh server button.
   *
   * @param patch The members to replace.
   */
  patchServer(patch: JsonRecord): void;
  /**
   * Sets how `PUT {base}/cost-owner` answers from now on.
   *
   * @param outcome The new outcome.
   */
  setTransfer(outcome: CostOwnerTransferOutcome): void;
  /** Answers every parked transfer request with the outcome that is set now. */
  releaseTransfers(): void;
}

/**
 * Builds the handlers that answer every request the workspace sends for the mocked server.
 *
 * @param options The members that replace the defaults.
 * @returns The handler chain, in the order `installApp` tries them, and the controls over what it answers.
 */
function workspaceHandlers(options: WorkspaceMockOptions): {
  handlers: ApiHandler[];
  controls: WorkspaceControls;
} {
  // One mutable record behind the detail and the fleet list, so a cost owner transfer shows in every later read.
  let server = buildWorkspaceServer(options);
  let transfer: CostOwnerTransferOutcome = options.costOwnerTransfer ?? "ok";
  const parked: (() => void)[] = [];
  const controls: WorkspaceControls = {
    access: options.accessState,
    patchServer: (patch) => {
      server = { ...server, ...patch };
    },
    setTransfer: (outcome) => {
      transfer = outcome;
    },
    releaseTransfers: () => {
      for (const release of parked.splice(0)) release();
    },
  };
  const accessState = options.accessState;
  // The record the page reads: with an access state, the right to manage access follows what the state holds.
  const manages = (): boolean =>
    options.canManageAccess === true ||
    (accessState !== undefined && holdsManageAccess(accessState));
  const current = (): JsonRecord => {
    if (accessState !== undefined) return { ...server, canManageAccess: manages() };
    return options.canManageAccess === undefined
      ? server
      : { ...server, canManageAccess: options.canManageAccess };
  };
  const owners = options.costOwnerOptions ?? [];
  const ownerOptionsHandler = jsonHandler("GET", `${base}/cost-owner/options`, (url: URL) => {
    const search = (url.searchParams.get("search") ?? "").trim().toLowerCase();
    return owners.filter(
      (owner) =>
        owner.costOwnerId !== server.costOwnerId &&
        String(owner.displayName).toLowerCase().includes(search),
    );
  });
  const transferHandler: ApiHandler = async (route, method, path) => {
    if (method !== "PUT" || path !== `${base}/cost-owner`) return false;
    const body = route.request().postDataJSON() as JsonRecord;
    options.costOwnerTransferBodies?.push(body);
    if (transfer === "hold") await new Promise<void>((resolve) => parked.push(resolve));
    const outcome = transfer === "hold" ? "ok" : transfer;
    if (outcome !== "ok") {
      await route.fulfill({
        status: outcome,
        json: { title: "Cost owner transfer refused", status: outcome },
      });
      return true;
    }
    const chosen = owners.find((owner) => owner.costOwnerId === body.costOwnerId);
    server = {
      ...server,
      costOwnerId: body.costOwnerId,
      costOwnerName: chosen?.displayName ?? server.costOwnerName,
    };
    await route.fulfill({ status: 204 });
    return true;
  };
  const entries = [
    ...configurationEntries,
    ...generatedConfigurationEntries(options.configurationEntryCount ?? 0),
  ];
  const metrics = populatedMetricsMock(workspaceServerId);
  const metricsHandler: ApiHandler = (route, method, path) =>
    fulfillMetricsRoute(route, method, path, workspaceServerId, metrics);
  const handlers = [
    jsonHandler("GET", base, () => current()),
    jsonHandler("GET", "/api/game-servers", () => [current()]),
    ...(accessState === undefined ? [] : accessHandlers(accessState, manages)),
    ownerOptionsHandler,
    transferHandler,
    jsonHandler("GET", `${base}/jobs`, options.jobs ?? []),
    jsonHandler("GET", `${base}/operations`, options.operations ?? []),
    jsonHandler("GET", "/api/cost-alerts", options.alerts ?? []),
    jsonHandler(
      "GET",
      `${base}/accrued-cost`,
      options.accrued === false
        ? { gameServerId: workspaceServerId, session: null }
        : { gameServerId: workspaceServerId, session: runningSession },
    ),
    metricsHandler,
    jsonHandler("GET", "/api/game-servers/capabilities", [capability]),
    jsonHandler("GET", `${base}/mod-pack-options`, modPacks),
    jsonHandler("GET", /\/mod-pack-options\/\d+\/contents$/, (url: URL) => {
      const packId = Number(url.pathname.split("/")[5]);
      const items = packContents[packId] ?? [];
      return {
        modPackDefinitionId: packId,
        isAvailable: items.length > 0,
        unavailableReason: null,
        total: items.length,
        items,
      };
    }),
    jsonHandler("GET", `${base}/configuration`, {
      serverId: workspaceServerId,
      profileId: null,
      profileRevisionId: null,
      profileRevisionNumber: null,
      isComplete: true,
      entries,
    }),
    jsonHandler("GET", `${base}/secrets`, credentialStatuses),
    jsonHandler("GET", `${base}/storage-demand`, {
      placementEstimate: null,
      components: [],
      runtimeProfiles: [],
      missingComponents: [],
    }),
    jsonHandler("GET", `${base}/configuration/profile-options`, []),
    jsonHandler("GET", `${base}/preservation-selections`, []),
    jsonHandler("GET", `${base}/files/persistent`, []),
    jsonHandler("GET", `${base}/files/live`, []),
  ];
  return { handlers, controls };
}

/** The controls of the workspace mock each page runs, so a state can change what the mock answers. */
const workspaceControls = new WeakMap<Page, WorkspaceControls>();

/**
 * Returns the controls of the workspace mock a page runs.
 *
 * @param page The page the mock was installed on.
 * @returns The controls over the mocked server record, the transfer outcome and the access state.
 * @throws Error when the page runs no workspace mock, which is a broken entry and not a finding.
 */
export function workspaceControlsOf(page: Page): WorkspaceControls {
  const controls = workspaceControls.get(page);
  if (controls === undefined) throw new Error("The page runs no workspace mock.");
  return controls;
}

/**
 * Installs the strict mock of the workspace of the mocked server on a page.
 *
 * Call it before the first navigation. The update stream is mocked and quiet, so the live badge and the cost line
 * never wait on a real socket.
 *
 * @param page The page to mock.
 * @param culture The culture the session runs in.
 * @param options The members that replace the defaults.
 * @returns The record of requests nothing answered; every audit asserts it is empty.
 */
export function installWorkspace(
  page: Page,
  culture: Culture,
  options: WorkspaceMockOptions = {},
): Promise<MockSession> {
  const { handlers, controls } = workspaceHandlers(options);
  workspaceControls.set(page, controls);
  return installApp(page, {
    culture,
    stream: true,
    handlers,
    settingsPanels: options.settingsPanels,
  });
}

/** The route handle of the automation mock each page runs, so a state can read what the page sent. */
const automationHandles = new WeakMap<Page, AutomationRouteHandle>();

/** What an automation entry changes about the default automation server. */
export interface AutomationMockOptions {
  /** The automation block; `null` is a server without a block (an older API or a failed read). */
  automation: ReturnType<typeof automationBlock> | null;
  /** Whether the viewer may change the automation; defaults to true. */
  canManage?: boolean | undefined;
  /** The undismissed cost alerts of the signed-in user; defaults to none. */
  alerts?: readonly JsonRecord[] | undefined;
  /** The Settings panel anchor ids to open; every panel by default, an empty array for the collapsed default. */
  settingsPanels?: readonly string[] | undefined;
}

/**
 * Builds the server of an automation entry: the Phase 68 automation server with the German-neutral workspace title.
 *
 * @param automation The automation block, or null.
 * @param canManage Whether the viewer may change the automation.
 * @returns The server as the API sends it.
 */
export function buildAutomationServer(
  automation: ReturnType<typeof automationBlock> | null,
  canManage: boolean,
): JsonRecord {
  return automationServer({
    automation,
    canManage,
    overrides: { title: workspaceServerTitle },
  });
}

/**
 * Installs the Phase 68 automation mock of the workspace on a page: one mutable server behind the detail and the
 * fleet list, the Settings reads in their quiet state and a recorded automation save.
 *
 * Call it before the first navigation. The mock aborts and records every request it does not know, so the returned
 * session is as strict as the one of {@link installWorkspace}. The route handle is kept for
 * {@link automationHandleOf}.
 *
 * @param page The page to mock.
 * @param culture The culture the session runs in.
 * @param options The automation block, the viewer's right and the alerts.
 * @returns The record of requests nothing answered; every audit asserts it is empty.
 */
export async function installAutomationWorkspace(
  page: Page,
  culture: Culture,
  options: AutomationMockOptions,
): Promise<MockSession> {
  const canManage = options.canManage ?? true;
  const handle = await mockAutomationRoutes(page, {
    server: buildAutomationServer(options.automation, canManage),
    canManage,
    culture,
    alerts: [...(options.alerts ?? [])],
    settingsPanels: options.settingsPanels,
  });
  automationHandles.set(page, handle);
  const stream = await mockFleetStream(page);
  return { unexpected: handle.unexpected, served: [], stream };
}

/**
 * Returns the route handle of the automation mock a page runs.
 *
 * @param page The page the mock was installed on.
 * @returns The handle with the recorded saves.
 * @throws Error when the page runs no automation mock, which is a broken entry and not a finding.
 */
export function automationHandleOf(page: Page): AutomationRouteHandle {
  const handle = automationHandles.get(page);
  if (handle === undefined) throw new Error("The page runs no automation mock.");
  return handle;
}

/**
 * Replaces the server the workspace detail read answers and reloads it through Refresh server, so a state of an
 * entry can show the same page after a change of rights or settings without a second page load.
 *
 * @param page The page with the workspace open.
 * @param automation The new automation block, or null.
 * @param canManage Whether the viewer may change the automation after the change.
 */
export async function reloadWorkspaceServer(
  page: Page,
  automation: ReturnType<typeof automationBlock> | null,
  canManage: boolean,
): Promise<void> {
  const server = buildAutomationServer(automation, canManage);
  await page.route(`**${base}`, (route) => route.fulfill({ json: server }));
  await page.locator(".workspace-details__refresh button").click();
}
