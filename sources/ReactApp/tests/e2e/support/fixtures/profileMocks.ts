import { jsonHandler, type ApiHandler } from "../mockApi";

/**
 * Handler factories for the configuration profile screens: the library, the create wizard and the profile workspace
 * with its revision history and access matrix.
 *
 * Every body is synthetic. Definition names are technical tokens and profile, user and role names are proper nouns,
 * so a German walk never reads API data as untranslated English (T-69-34).
 */

/** The ids of the profiles the library lists; the first one is the profile the workspace entry opens. */
export const profileIds = {
  /** The active Arma 3 profile with three revisions. */
  active: "9f000000-0000-4000-8000-000000000001",
  /** The archived Arma 3 profile. */
  archived: "9f000000-0000-4000-8000-000000000002",
  /** The active Counter-Strike 2 profile. */
  other: "9f000000-0000-4000-8000-000000000003",
} as const;

/** The title of the profile the workspace entry opens. */
export const profileTitle = "Ranger-7";

/**
 * Builds the profile summary of the library and the workspace.
 *
 * @param primaryKey The profile id.
 * @param title The profile title.
 * @param gameIdentifier The game identifier.
 * @param isArchived Whether the profile is archived.
 * @param latestRevisionNumber The number of the newest revision.
 * @returns The summary in the shape of `GET /api/configuration-profiles`.
 */
function summary(
  primaryKey: string,
  title: string,
  gameIdentifier: string,
  isArchived: boolean,
  latestRevisionNumber: number,
) {
  return {
    primaryKey,
    title,
    gameIdentifier,
    isArchived,
    latestRevisionNumber,
    latestRevisionId: `${primaryKey.slice(0, 24)}${String(latestRevisionNumber).padStart(12, "0")}`,
  };
}

/** The three profiles of the library. */
const librarySummaries = [
  summary(profileIds.active, profileTitle, "arma3", false, 3),
  summary(profileIds.archived, "Ranger-3", "arma3", true, 1),
  summary(profileIds.other, "Delta-9", "cs2", false, 2),
];

/**
 * Builds one configuration definition with every field the API sends.
 *
 * @param key The definition key; it doubles as the display name, which keeps it German-neutral.
 * @param kind The value kind.
 * @param overrides Fields that replace the defaults.
 * @returns The definition.
 */
function definition(key: string, kind: string, overrides: Record<string, unknown> = {}) {
  return {
    key,
    targetPath: "server/server.cfg",
    group: "Server",
    displayName: key,
    description: `${key}.cfg`,
    kind,
    required: false,
    sensitive: false,
    isControlPlaneOnly: false,
    order: 10,
    defaultValue: null,
    minimum: null,
    maximum: null,
    allowedValues: [],
    choices: [],
    ...overrides,
  };
}

/** The definitions of the Arma 3 profile: a required text, a bounded integer, a switch, a list and a secret. */
const arma3Definitions = [
  definition("hostname", "String", { required: true, order: 10, defaultValue: "UTF-1" }),
  definition("maxPlayers", "Integer", {
    order: 20,
    minimum: 1,
    maximum: 128,
    defaultValue: 32,
  }),
  definition("verifySignatures", "Boolean", { order: 30, defaultValue: true }),
  definition("motd", "StringArray", { order: 40 }),
  definition("passwordAdmin", "String", { order: 50, sensitive: true }),
];

/**
 * Builds one profile revision.
 *
 * @param number The revision number.
 * @param values The stored values of the revision.
 * @param changes The keys the revision changed against the one before.
 * @returns The revision in the shape of the profile details.
 */
function revision(
  number: number,
  values: Record<string, unknown>,
  changes: { key: string; kind: "Added" | "Changed" | "Removed" }[],
) {
  return {
    primaryKey: `${profileIds.active.slice(0, 24)}${String(number).padStart(12, "0")}`,
    revisionNumber: number,
    timeStampCreated: `2026-11-1${number}T10:00:00Z`,
    createdById: "10000000-0000-4000-8000-000000000001",
    createdByNickname: "Nova",
    values,
    sensitiveConfiguredKeys: number >= 2 ? ["passwordAdmin"] : [],
    changes,
    redactedKeys: [],
  };
}

/**
 * Builds the details of the active profile, as the viewer who may modify, archive and share it receives them.
 *
 * @returns The details in the shape of `GET /api/configuration-profiles/{id}`.
 */
function profileDetails() {
  return {
    profile: librarySummaries[0],
    canModify: true,
    canManageAccess: true,
    canArchive: true,
    definitions: arma3Definitions,
    revisions: [
      revision(1, { hostname: "UTF-1" }, [{ key: "hostname", kind: "Added" }]),
      revision(2, { hostname: "UTF-1", maxPlayers: 40 }, [
        { key: "maxPlayers", kind: "Added" },
        { key: "passwordAdmin", kind: "Added" },
      ]),
      revision(3, { hostname: "UTF-2", maxPlayers: 40, verifySignatures: true }, [
        { key: "hostname", kind: "Changed" },
        { key: "verifySignatures", kind: "Added" },
      ]),
    ],
  };
}

/** The access grants of the active profile: one member, one role and the permissions a grant may carry. */
const profileAccess = {
  allowedPermissions: ["cfg", "cfg:view", "cfg:modify", "cfg:manage-access", "cfg:archive"],
  users: [
    { userId: "10000000-0000-4000-8000-000000000002", nickname: "Pax", permissions: ["cfg:view"] },
  ],
  roles: [
    { roleId: 7, title: "Gamma", description: "G-1", permissions: ["cfg:view", "cfg:modify"] },
  ],
};

/** The definition catalog the create wizard reads: one Arma 3 set the viewer may create profiles for. */
const profileCatalog = {
  canCreate: true,
  games: [{ gameIdentifier: "arma3", definitions: arma3Definitions }],
};

/**
 * Builds the handlers of the configuration profile screens.
 *
 * Reads answer from fixed data. The write endpoints (publish, archive, access changes, create) are not answered: no
 * registry state submits them, so a request to one is a recorded failure.
 *
 * @returns The handlers in the order the strict layer tries them.
 */
export function profileHandlers(): ApiHandler[] {
  return [
    jsonHandler("GET", "/api/configuration-profiles", () => librarySummaries),
    jsonHandler("GET", "/api/configuration-profiles/definitions", () => profileCatalog),
    jsonHandler("GET", "/api/configuration-profiles/options", () => librarySummaries),
    jsonHandler("GET", `/api/configuration-profiles/${profileIds.active}`, () => profileDetails()),
    jsonHandler(
      "GET",
      `/api/configuration-profiles/${profileIds.active}/access`,
      () => profileAccess,
    ),
  ];
}
