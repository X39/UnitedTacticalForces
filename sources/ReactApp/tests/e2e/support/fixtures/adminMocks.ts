import type { Route } from "@playwright/test";
import { jsonHandler, type ApiHandler } from "../mockApi";

/**
 * Handler factories for the four non-server smoke pages: the members list, the role detail, the public page editor
 * and the public home page.
 *
 * Every body is synthetic: proper-noun names, technical claim identifiers and short neutral texts, so a screenshot or
 * a German walk never depends on real data or reads API text as untranslated English (T-69-34).
 */

/** The claims of the viewer of the admin entries: the global administrator claim, which every admin page accepts. */
export const adminClaims: readonly string[] = ["adm"];

/** The id of the role the role detail entry opens. */
export const smokeRoleId = 7;

/** The slug of the public page the editor entry opens. */
export const smokePageSlug = "welcome";

/**
 * Builds one community member of the roster.
 *
 * @param index A running number that makes the id unique.
 * @param nickname The display name.
 * @param overrides Fields that replace the defaults.
 * @returns The member in the shape of `CommandCenterCommunityMemberDto`.
 */
function member(index: number, nickname: string, overrides: Record<string, unknown> = {}) {
  return {
    primaryKey: `20000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
    nickname,
    avatar: "",
    avatarMimeType: "image/png",
    isVerified: true,
    isBanned: false,
    isDeleted: false,
    vereinMembershipStatus: null,
    vereinMemberNumber: null,
    isActiveVereinMember: false,
    vereinMembershipStarted: null,
    vereinMembershipEnded: null,
    roleTitles: [],
    directClaimCount: 0,
    ...overrides,
  };
}

/** The roster: two members that need attention, one with a role and one deleted. */
const roster = [
  member(1, "Raven", { isVerified: false }),
  member(2, "Kestrel", { isBanned: true }),
  member(3, "Nova", { roleTitles: ["Gamma"], directClaimCount: 2 }),
  member(4, "Pax", { isDeleted: true }),
];

/**
 * Builds the community snapshot of the members list.
 *
 * @returns The snapshot in the shape of `CommandCenterCommunitySnapshotDto`.
 */
function communitySnapshot() {
  return {
    memberCount: roster.length,
    verifiedMemberCount: 2,
    unverifiedMemberCount: 1,
    bannedMemberCount: 1,
    deletedMemberCount: 1,
    activeVereinMemberCount: 0,
    suspendedVereinMemberCount: 0,
    endedVereinMemberCount: 0,
    roleCount: 1,
    claimCount: 4,
    members: roster,
  };
}

/**
 * Builds one claim of the authority snapshot.
 *
 * @param primaryKey The claim id.
 * @param identifier The claim identifier; it doubles as the title, which keeps it German-neutral.
 * @param category The claim category.
 * @returns The claim in the shape of `PlainClaimDto`.
 */
function claim(primaryKey: number, identifier: string, category: string) {
  return {
    primaryKey,
    category,
    title: identifier,
    identifier,
    description: `${identifier}.`,
    isPrefix: false,
    value: null,
    valueType: null,
  };
}

/** The role the detail entry opens, with the claims it holds. */
const smokeRole = { primaryKey: smokeRoleId, title: "Gamma", description: "G-1" };

/** The claims the snapshot lists; the first two belong to the role. */
const smokeClaims = [
  claim(1, "sv:view", "Server"),
  claim(2, "sv:operate", "Server"),
  claim(3, "usr:verify", "Mitglieder"),
  claim(4, "mp:sync", "Modpacks"),
];

/**
 * Builds the authority snapshot the role detail reads.
 *
 * @returns The snapshot in the shape of `CommandCenterAuthoritySnapshotDto`.
 */
function authoritySnapshot() {
  return {
    memberCount: roster.length,
    roleCount: 1,
    claimCount: smokeClaims.length,
    roles: [{ role: smokeRole, claims: smokeClaims.slice(0, 2), memberCount: 1 }],
    claims: smokeClaims.map((item, index) => ({
      claim: item,
      directMemberCount: 0,
      roleCount: index < 2 ? 1 : 0,
      roleTitles: index < 2 ? ["Gamma"] : [],
    })),
  };
}

/**
 * Builds the member list of the role detail.
 *
 * @returns The members in the shape of `CommandCenterAuthorityRoleMembersDto`.
 */
function roleMembers() {
  return {
    role: smokeRole,
    assignedMemberCount: 1,
    availableMemberCount: 2,
    assignedMembers: [roster[2]],
    availableMembers: [roster[0], roster[1]],
  };
}

/**
 * Builds the public page the editor opens, with one published revision.
 *
 * @returns The page in the shape of `PublicPageDto`.
 */
function publicPage() {
  const revision = {
    primaryKey: 1,
    pageFk: 1,
    version: 1,
    title: "Willkommen",
    summary: "Kurzfassung",
    heroKicker: "UTF",
    heroTitle: "Willkommen",
    heroDescription: "Beschreibung",
    heroImageAssetFk: "30000000-0000-4000-8000-000000000001",
    heroImagePath: "",
    markdown: "# Willkommen\n\nText",
    contentJson: "",
    comment: "Erste Fassung",
    isPublished: true,
    timeStampPublished: "2026-11-10T09:00:00Z",
    timeStampCreated: "2026-11-10T09:00:00Z",
    authorFk: "10000000-0000-4000-8000-000000000001",
    authorName: "Nova",
  };
  return {
    primaryKey: 1,
    slug: smokePageSlug,
    isDeleted: false,
    revision,
    revisions: [revision],
    timeStampCreated: "2026-11-10T09:00:00Z",
    timeStampUpdated: "2026-11-10T09:00:00Z",
  };
}

/**
 * Builds the handlers of the admin smoke pages: roster, authority snapshot, role members and the public page.
 *
 * Reads answer from fixed data. The write endpoints (role update, claim and member changes, revision creation) are
 * not answered: no registry state submits them, so a request to one is a recorded failure.
 *
 * @returns The handlers in the order the strict layer tries them.
 */
export function adminHandlers(): ApiHandler[] {
  return [
    jsonHandler("GET", "/api/command-center/community-snapshot", () => communitySnapshot()),
    jsonHandler("GET", "/api/command-center/authority-snapshot", () => authoritySnapshot()),
    jsonHandler("GET", `/api/command-center/authority-role/${smokeRoleId}/members`, () =>
      roleMembers(),
    ),
    jsonHandler("GET", `/api/public-pages/admin/${smokePageSlug}`, () => publicPage()),
  ];
}

/**
 * Answers the signed-out visitor of a public page: the signed-in user read returns 204.
 *
 * @param route The intercepted request.
 * @param method The HTTP method of the request.
 * @param path The URL path of the request.
 * @returns True when the request was the signed-in user read and was answered.
 */
async function signedOut(route: Route, method: string, path: string): Promise<boolean> {
  if (method !== "GET" || path !== "/api/users/me") return false;
  await route.fulfill({ status: 204 });
  return true;
}

/**
 * Builds the upcoming operation of the public home page.
 *
 * @returns The event in the shape of `UpcomingEvent`.
 */
function upcomingEvent() {
  const scheduledFor = "2026-12-05T18:00:00Z";
  return {
    primaryKey: "40000000-0000-4000-8000-000000000001",
    title: "Nordwind",
    description: "Aufklaerung",
    terrainTitle: "Altis",
    modPackDefinitionTitle: "UTF",
    imagePath: "",
    scheduledFor,
    scheduledForOriginal: scheduledFor,
    timeStampCreated: "2026-11-01T10:00:00Z",
    acceptedCount: 3,
    rejectedCount: 1,
    maybeCount: 2,
    minimumAccepted: 12,
    hostedBy: {
      primaryKey: "10000000-0000-4000-8000-000000000002",
      nickname: "Raven",
      avatar: "",
      avatarMimeType: "image/png",
    },
    metaAcceptance: null,
  };
}

/**
 * Builds the handlers of the public home page for a signed-out visitor: one upcoming operation and no field report.
 *
 * @returns The handlers in the order the strict layer tries them.
 */
export function homeHandlers(): ApiHandler[] {
  return [
    signedOut,
    jsonHandler("GET", "/api/events/upcoming", () => [upcomingEvent()]),
    jsonHandler("GET", "/api/field-reports", () => []),
  ];
}
