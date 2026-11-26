import { createCapability, createOwners, createStorageDemand } from "../../helpers/createFixtures";
import { costAlertSettings } from "../../helpers/costAlertFixtures";
import { fleetAction, fleetServer } from "../../helpers/fleetFixtures";
import { fulfillMetricsRoute } from "./metricsMocks";
import { auditUserId, jsonHandler, type ApiHandler } from "./mockApi";

/**
 * The minimal per-route API fixtures of the route walk (VER-01, Phase 69 plan 29).
 *
 * Every certified route id maps to the smallest valid payloads for the requests its page sends, so the page reaches
 * its loaded state instead of an error state. The walk fills every `{guid}` parameter with {@link sampleGuid} and
 * every `{long}` parameter with {@link sampleLong}, so a detail fixture only has to answer for those two ids.
 *
 * This module imports Playwright types and test helpers only; it never imports `src` at runtime.
 */

/** The value of every `{name:guid}` route parameter in the walk. */
export const sampleGuid = "0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d";

/** The value of every `{name:long}` route parameter in the walk. */
export const sampleLong = "4242";

/** The value of the `{Slug}` parameter of the command public page routes. */
export const samplePageSlug = "operations-brief";

/** The slug of the structured GDPR page, which opens a different editor than every other public page. */
export const gdprPageSlug = "gdpr";

/** Values of the non-typed route parameters, by parameter name. */
export const sampleTextParameters: Readonly<Record<string, string>> = {
  Slug: samplePageSlug,
  Group: "home",
  Section: "overview",
  action: "login",
};

/** The minimal mock of one route id. */
export interface RouteFixture {
  /** The claim identifiers of the signed-in user. Defaults to `["adm"]`, which authorizes every page. */
  claims?: readonly string[] | undefined;
  /** Handlers for the requests the page sends, tried before the built-in baseline. */
  handlers: readonly ApiHandler[];
  /** Whether the fleet update stream is mocked. Defaults to false. */
  stream?: boolean | undefined;
  /** Additional sample paths the walk opens with the same mock, for pages that branch on a parameter value. */
  extraPaths?: readonly string[] | undefined;
  /**
   * A pattern the first `h1` text must match. Set on detail routes: it proves that the entity loaded instead of the
   * not-found state.
   */
  h1?: RegExp | undefined;
}

/**
 * Builds a handler that answers one GET path with a JSON body.
 *
 * @param pattern An exact path or a regular expression for the path.
 * @param body The JSON body.
 * @param status The HTTP status. Defaults to 200.
 * @returns The handler.
 */
function get(pattern: RegExp | string, body: unknown, status?: number): ApiHandler {
  return jsonHandler("GET", pattern, body, status);
}

/**
 * Builds a handler that answers one POST path with a JSON body.
 *
 * @param pattern An exact path or a regular expression for the path.
 * @param body The JSON body.
 * @returns The handler.
 */
function post(pattern: RegExp | string, body: unknown): ApiHandler {
  return jsonHandler("POST", pattern, body);
}

/**
 * Builds a handler that answers one GET path with an empty `204 No Content` answer.
 *
 * @param pattern An exact path or a regular expression for the path.
 * @returns The handler.
 */
function noContent(pattern: RegExp | string): ApiHandler {
  return async (route, method, path) => {
    if (
      method !== "GET" ||
      (typeof pattern === "string" ? path !== pattern : !pattern.test(path))
    ) {
      return false;
    }
    await route.fulfill({ status: 204 });
    return true;
  };
}

/** Answers the image bytes of every gallery asset with an empty PNG, as the shell does for the insignia. */
const mediaContent: ApiHandler = async (route, method, path) => {
  if (method !== "GET" || !/^\/api\/media-assets\/[0-9a-f-]{36}\/content$/i.test(path))
    return false;
  await route.fulfill({ status: 200, contentType: "image/png", body: "" });
  return true;
};

const hero = "media-assets/1dadc293-2072-4e3f-87ac-480110679746/content";

/** A published upcoming event in the shape of `GET /api/events/upcoming`. */
const upcomingEvent = {
  primaryKey: sampleGuid,
  title: "Operation Copper Trail",
  description: "Coordinate reconnaissance and report back to the group.",
  terrainTitle: "Altis",
  modPackDefinitionTitle: "UTF Main",
  imagePath: "",
  scheduledFor: "2026-12-12T18:00:00Z",
  scheduledForOriginal: "2026-12-12T18:00:00Z",
  timeStampCreated: "2026-11-01T10:00:00Z",
  acceptedCount: 18,
  rejectedCount: 1,
  maybeCount: 2,
  minimumAccepted: 12,
  hostedBy: { primaryKey: auditUserId, nickname: "Raven", avatar: "", avatarMimeType: "image/png" },
  metaAcceptance: "Accepted",
};

/** The event of the member and command event pages in the shape of `GET /api/events/{id}`. */
const fullEvent = {
  ...upcomingEvent,
  isVisible: true,
  owner: { primaryKey: auditUserId, nickname: "Planner" },
  terrainPrimaryKey: 4242,
  terrainGameIdentifier: "arma3",
  modPackDefinitionPrimaryKey: 4242,
  modPackRevisionPrimaryKey: null,
};

/** A published field report summary in the shape of `GET /api/field-reports`. */
const fieldReportSummary = {
  primaryKey: sampleGuid,
  slug: "operation-northstar",
  title: "Operation Northstar",
  excerpt: "A coordinated reconnaissance patrol.",
  category: "After Action",
  heroImageAssetFk: null,
  heroImagePath: null,
  heroImageAlt: null,
  isFeatured: false,
  sortOrder: 0,
  timeStampPublished: "2026-11-10T10:00:00Z",
  timeStampUpdated: "2026-11-10T10:00:00Z",
};

/** A field report in the shape of the command center field report reads. */
const commandFieldReport = {
  ...fieldReportSummary,
  isPublished: true,
  isDeleted: false,
  markdown: "## Situation\n\nAll objectives were secured.",
  timeStampCreated: "2026-11-09T10:00:00Z",
  createdByName: "Journal editor",
  updatedByName: "Journal editor",
  auditRecordCount: 1,
};

/** A gallery image in the shape of `GET /api/media-assets`. */
const mediaAsset = {
  primaryKey: "1dadc293-2072-4e3f-87ac-480110679746",
  originalFileName: "operation-briefing.webp",
  contentType: "image/webp",
  size: 245760,
  imagePath: hero,
  timeStampCreated: "2026-11-01T10:00:00Z",
  timeStampUpdated: "2026-11-01T10:00:00Z",
  createdByName: "Content editor",
  tags: ["briefing"],
  usages: [],
};

/** A role with one member in the shape of the authority snapshot. */
const authorityRole = {
  role: { primaryKey: Number(sampleLong), title: "Planner", description: "Plans operations" },
  claims: [{ primaryKey: 11, title: "Verify users", identifier: "usr:verify" }],
  memberCount: 1,
};

/** One claim with its usage in the shape of the authority snapshot. */
const authorityClaim = {
  claim: {
    primaryKey: 11,
    category: "Members",
    title: "Verify users",
    identifier: "usr:verify",
    description: "Allows verifying new users.",
    isPrefix: false,
    value: null,
    valueType: null,
  },
  directMemberCount: 1,
  roleCount: 1,
  roleTitles: ["Planner"],
};

/** The authority snapshot read by the claims, roles and member pages. */
const authoritySnapshot = {
  memberCount: 24,
  roleCount: 1,
  claimCount: 1,
  roles: [authorityRole],
  claims: [authorityClaim],
};

/** Contribution settings in the shape of `GET /api/verein/settings`. */
const vereinSettings = {
  membershipContributionMonthlyAmount: 5,
  membershipContributionAnnualAmount: 60,
  membershipContributionCurrency: "EUR",
  membershipContributionPublicTextDe: "Der Beitrag finanziert die Infrastruktur.",
  membershipContributionPublicTextEn: "The contribution funds infrastructure.",
  timeStampUpdated: "2026-11-01T10:00:00Z",
};

/** The own profile settings in the shape of `GET /api/users/me/settings`. */
const profileSettings = {
  primaryKey: auditUserId,
  nickname: "Operator",
  eMail: "operator@example.test",
  avatar: "",
  avatarMimeType: "image/png",
  isVerified: true,
  isVereinMember: false,
  vereinMembershipStatus: null,
  vereinMemberNumber: null,
  canDeleteAccount: true,
  accountDeletionBlockedReason: null,
  steamId64: "76561198012345678",
  discordId: null,
  discordUsername: null,
};

/** A Verein document revision with a body that starts below the page level heading. */
const documentRevision = {
  primaryKey: 7,
  documentFk: Number(sampleLong),
  version: 1,
  title: "Resolution 2026-01",
  summary: "Budget approval",
  markdown: "## Decision\n\nThe budget is approved.",
  comment: "",
  isPublished: true,
  timeStampPublished: "2026-11-01T10:00:00Z",
  timeStampCreated: "2026-11-01T09:00:00Z",
  authorFk: auditUserId,
  authorName: "Secretary",
};

/** A Verein minutes document in the admin shape. */
const adminDocument = {
  primaryKey: Number(sampleLong),
  kind: "Minutes",
  isDeleted: false,
  revision: documentRevision,
  revisions: [documentRevision],
  timeStampCreated: "2026-11-01T09:00:00Z",
  timeStampUpdated: "2026-11-01T10:00:00Z",
};

/** A published Verein document in the public shape. */
const publishedDocument = {
  primaryKey: Number(sampleLong),
  kind: "Minutes",
  timeStampUpdated: "2026-11-01T10:00:00Z",
  revision: {
    version: 1,
    title: "Resolution 2026-01",
    summary: "Budget approval",
    markdown: "## Decision\n\nThe budget is approved.",
    timeStampPublished: "2026-11-01T10:00:00Z",
  },
};

/** The ledger page in the shape of `GET /api/verein/kassenbuch`. */
const kassenbuchPage = {
  entries: [
    {
      primaryKey: Number(sampleLong),
      timeStampBooked: "2026-11-02T10:00:00Z",
      direction: "Income",
      amount: 60,
      currency: "EUR",
      category: "Contributions",
      description: "Annual contribution",
      reference: null,
      documentNumber: "2026-001",
      isDeleted: false,
      timeStampCreated: "2026-11-02T10:00:00Z",
      timeStampUpdated: "2026-11-02T10:00:00Z",
      createdByName: "Treasurer",
      updatedByName: null,
    },
  ],
  totalCount: 1,
  incomeTotal: 60,
  expenseTotal: 0,
  balance: 60,
  currency: "EUR",
};

/** A public page revision whose Markdown body starts below the page level heading. */
const pageRevision = {
  primaryKey: 11,
  pageFk: 1,
  version: 2,
  title: "Operations brief",
  summary: "Current policy",
  markdown: "## Retention policy\n\nOperational records are retained only as required.",
  contentJson: "",
  isPublished: true,
  timeStampPublished: "2026-11-10T10:00:00Z",
  timeStampCreated: "2026-11-10T09:00:00Z",
  authorName: "Policy editor",
};

/** A public page in the shape of `GET /api/public-pages/admin/{slug}`. */
const publicPage = {
  primaryKey: 1,
  slug: samplePageSlug,
  isDeleted: false,
  revision: pageRevision,
  revisions: [pageRevision],
  timeStampCreated: "2026-11-01T10:00:00Z",
  timeStampUpdated: "2026-11-10T10:00:00Z",
};

/** The structured GDPR page, which opens the GDPR content editor instead of the Markdown editor. */
const gdprRevision = {
  ...pageRevision,
  primaryKey: 12,
  pageFk: 2,
  title: "Privacy and data protection",
};
const gdprPage = {
  ...publicPage,
  primaryKey: 2,
  slug: gdprPageSlug,
  revision: gdprRevision,
  revisions: [gdprRevision],
};

/** A terrain in the shape of `GET /api/terrains/{id}`. */
const terrain = {
  primaryKey: Number(sampleLong),
  gameIdentifier: "arma3",
  title: "Altis",
  imageAssetFk: null,
  imagePath: null,
  isActive: true,
};

/** The game capabilities of a game that supports mod packs, terrains and events. */
const gameCapabilities = [
  {
    gameIdentifier: "arma3",
    displayName: "Arma 3",
    description: "Tactical shooter",
    actions: [],
    runtimeProviders: [],
    runtimeProviderProfiles: [],
    requiresTerrain: true,
    terrainLabel: "Terrain",
    requiresScenario: false,
    scenarioLabel: "Scenario",
    supportsModPacks: true,
    requiresModPack: false,
    terrainSourceKinds: [],
    modInstallerIdentifiers: [],
    modProviderBindings: [],
    modPackFormats: [],
    secrets: [],
  },
];

/** The member read of the member detail page. */
const member = {
  primaryKey: sampleGuid,
  nickname: "Raven Actual",
  eMail: "raven@example.test",
  isVerified: true,
  isBanned: false,
  isDeleted: false,
  steamId64: "76561198000000001",
  discordUsername: "raven",
};

/** The hosting server of the fleet and workspace routes with the id every guid parameter uses. */
function hostingServer() {
  const server = fleetServer({
    index: 7,
    title: "Gamma Ops",
    status: "Running",
    costOwnerUserId: auditUserId,
    actions: [fleetAction("stop")],
  });
  return {
    ...server,
    primaryKey: sampleGuid,
    runtimeProfileIdentifier: "medium",
    runtimeProfileDisplayName: "Medium",
    runtimeStorageStrategy: "IncludedDisk",
    externalStorageSizeGiB: null,
    estimatedHourlyCostEur: 0.2,
    estimatedMonthlyCostEur: 146,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    canModify: true,
    timeStampCreated: "2026-11-16T10:00:00Z",
    deployment: {
      ...server.deployment,
      stage: "Ready",
      workerStatus: "Connected",
      lastHeartbeat: "2026-11-16T10:00:00Z",
      connectionEndpoint: "203.0.113.5:2302",
      connectionProtocol: "udp",
      publicAddress: null,
      deploymentRoot: "/srv/utf",
      includedStorageBytes: 85899345920,
      selectedExternalStorageBytes: null,
      estimatedComputeHourlyCostEur: 0.1,
      estimatedStorageHourlyCostEur: 0,
      diskTotalBytes: null,
      diskAvailableBytes: null,
      diskTimeStampObserved: null,
      faultSummary: null,
      administrativeSshCommand: null,
    },
  };
}

/** A hosting finance account in the shape of `GET /api/cost-owners/{id}`. */
const financeOwner = {
  costOwnerId: sampleGuid,
  kind: "User",
  userId: auditUserId,
  displayName: "Operator",
  isFrozen: false,
  balance: 25,
  allowance: null,
  reservedProviderCost: 0,
  spendableAmount: 25,
  currency: "EUR",
  timeStampUpdated: "2026-11-20T10:00:00Z",
  recentLedgerEntries: [],
};

/** The empty protocol page of the finance ledger reads. */
const emptyProtocol = { entries: [], totalCount: 0 };

/**
 * The fixture of every certified route id. The walk fails when a certified id has no entry, so a route added to the
 * manifest cannot escape the walk.
 */
export const routeFixtures: Readonly<Record<string, RouteFixture>> = {
  home: {
    handlers: [
      get("/api/events/upcoming", [upcomingEvent]),
      get("/api/field-reports", [fieldReportSummary]),
    ],
  },
  "authentication-action": { handlers: [], h1: /sign in/i },
  "events-upcoming": { handlers: [get("/api/events/upcoming", [upcomingEvent])] },
  "event-detail": {
    handlers: [
      get(`/api/events/${sampleGuid}`, fullEvent),
      get(`/api/events/${sampleGuid}/slotting/all`, []),
      get(`/api/events/${sampleGuid}/mod-pack-formats`, []),
    ],
    h1: /Operation Copper Trail/,
  },
  wiki: {
    handlers: [
      get("/api/wiki/all/header", [
        {
          primaryKey: sampleGuid,
          name: "Training/Radio procedure",
          lastModified: "2026-11-12T10:00:00Z",
        },
      ]),
      get(`/api/wiki/${sampleGuid}`, {
        primaryKey: sampleGuid,
        title: "Training/Radio procedure",
        timeStampCreated: "2026-11-01T10:00:00Z",
        isDeleted: false,
        revisions: [
          {
            primaryKey: "8a2e2f0f-0bb0-4d0a-a59b-f89534b7659d",
            pageForeignKey: sampleGuid,
            timeStampCreated: "2026-11-12T10:00:00Z",
            markdown: "## Calls\n\nUse concise, confirmed calls.",
            comment: "Published guidance",
            author: { primaryKey: auditUserId, nickname: "Instructor" },
          },
        ],
      }),
      get(`/api/wiki/${sampleGuid}/revisions/header`, []),
    ],
  },
  "field-reports": { handlers: [get("/api/field-reports", [fieldReportSummary])] },
  join: {
    handlers: [
      get("/api/verein/settings", vereinSettings),
      noContent("/api/verein/membership-requests/me"),
      get("/api/users/me/settings", profileSettings),
    ],
  },
  unit: { handlers: [] },
  verein: {
    handlers: [
      get("/api/verein/settings", vereinSettings),
      get("/api/verein/documents/satzung", { ...publishedDocument, kind: "Satzung" }),
      get("/api/verein/documents/minutes", [publishedDocument]),
      noContent("/api/verein/membership-requests/me"),
    ],
  },
  "verein-statutes": {
    handlers: [get("/api/verein/documents/satzung", { ...publishedDocument, kind: "Satzung" })],
  },
  "verein-minutes": { handlers: [get("/api/verein/documents/minutes", [publishedDocument])] },
  "verein-minute": {
    handlers: [get(`/api/verein/documents/minutes/${sampleLong}`, publishedDocument)],
    h1: /Resolution 2026-01/,
  },
  "account-settings": { handlers: [get("/api/users/me/settings", profileSettings)] },

  command: { handlers: [get("/api/game-servers", [hostingServer()])] },
  "command-claims": {
    handlers: [get("/api/command-center/authority-snapshot", authoritySnapshot)],
  },
  "command-roles": { handlers: [get("/api/command-center/authority-snapshot", authoritySnapshot)] },
  "command-role": {
    handlers: [
      get("/api/command-center/authority-snapshot", authoritySnapshot),
      get(`/api/command-center/authority-role/${sampleLong}/members`, {
        role: authorityRole.role,
        assignedMemberCount: 0,
        availableMemberCount: 0,
        assignedMembers: [],
        availableMembers: [],
      }),
    ],
    h1: /Planner/,
  },
  "command-members": {
    handlers: [
      get("/api/command-center/community-snapshot", {
        memberCount: 1,
        verifiedMemberCount: 1,
        unverifiedMemberCount: 0,
        bannedMemberCount: 0,
        deletedMemberCount: 0,
        activeVereinMemberCount: 0,
        suspendedVereinMemberCount: 0,
        endedVereinMemberCount: 0,
        roleCount: 1,
        claimCount: 1,
        members: [
          {
            primaryKey: sampleGuid,
            nickname: "Raven Actual",
            isVerified: true,
            isBanned: false,
            isDeleted: false,
            roleTitles: ["Planner"],
            directClaimCount: 1,
          },
        ],
      }),
    ],
  },
  "command-member": {
    handlers: [
      get(`/api/users/${sampleGuid}`, member),
      get(`/api/users/${sampleGuid}/roles`, [
        { primaryKey: Number(sampleLong), title: "Planner", description: "Plans operations" },
      ]),
      get(new RegExp(`^/api/users/${sampleGuid}/claims$`), [
        { primaryKey: 11, title: "Verify users", identifier: "usr:verify" },
      ]),
      get("/api/command-center/authority-snapshot", authoritySnapshot),
      get(`/api/verein/memberships/${sampleGuid}`, { title: "Not found" }, 404),
    ],
    h1: /Raven Actual/,
  },
  "command-settings": {
    handlers: [
      get("/api/command-center/settings-snapshot", {
        apiBaseUrlConfigured: true,
        clientBaseUrlConfigured: true,
        basePathConfigured: true,
        autoVerifyNewUsers: false,
        features: [{ area: "Discord OAuth", isEnabled: true, isConfigured: false, detail: "" }],
      }),
    ],
  },
  "command-content": { handlers: [] },
  "command-content-pages": {
    handlers: [get("/api/public-pages/admin", [publicPage, gdprPage])],
  },
  "command-content-page": {
    handlers: [
      get(`/api/public-pages/admin/${samplePageSlug}`, publicPage),
      get(`/api/public-pages/admin/${gdprPageSlug}`, gdprPage),
    ],
    extraPaths: [`/command/content/pages/${gdprPageSlug}`],
    h1: /Privacy and data protection|Operations brief/,
  },
  "command-content-page-edit": {
    handlers: [
      get(`/api/public-pages/admin/${samplePageSlug}`, publicPage),
      get(`/api/public-pages/admin/${gdprPageSlug}`, gdprPage),
    ],
    extraPaths: [`/command/content/pages/${gdprPageSlug}/edit`],
    h1: /Operations brief|Privacy and data protection/i,
  },
  "command-content-gallery": {
    handlers: [get("/api/media-assets", [mediaAsset]), mediaContent],
  },
  "command-content-images": {
    handlers: [
      get("/api/site-image-settings/admin", [
        {
          key: "home-hero",
          title: "Home hero",
          description: "Primary public landing-page imagery.",
          group: "home",
          sortOrder: 1,
          imageAssetFk: mediaAsset.primaryKey,
          imagePath: hero,
          timeStampCreated: "2026-11-01T10:00:00Z",
          timeStampUpdated: "2026-11-10T10:00:00Z",
          updatedByName: "Content editor",
        },
      ]),
      get("/api/media-assets", [mediaAsset]),
      mediaContent,
    ],
  },
  "command-field-reports": {
    handlers: [
      get("/api/command-center/field-reports-snapshot", {
        fieldReportCount: 1,
        publishedFieldReportCount: 1,
        draftFieldReportCount: 0,
        deletedFieldReportCount: 0,
        reports: [commandFieldReport],
      }),
    ],
  },
  "command-field-report-edit": {
    handlers: [get(`/api/field-reports/admin/${sampleGuid}`, commandFieldReport)],
  },
  "command-events": {
    handlers: [
      get("/api/command-center/event-admin-snapshot", {
        eventCount: 1,
        upcomingEvents: [upcomingEvent],
        events: [
          {
            plainEvent: { ...fullEvent, hostedBy: undefined, owner: undefined },
            hostedBy: upcomingEvent.hostedBy,
            owner: { nickname: "Planner" },
            terrain: { title: "Altis" },
            modPackRevision: { definitionTitle: "UTF Main" },
          },
        ],
      }),
    ],
  },
  "command-event-create": { handlers: [get("/api/game-servers/capabilities", gameCapabilities)] },
  "command-event-edit": {
    handlers: [
      get(`/api/events/${sampleGuid}`, fullEvent),
      get(`/api/events/${sampleGuid}/slotting/all`, []),
      get("/api/game-servers/capabilities", gameCapabilities),
    ],
  },
  terrains: {
    handlers: [post("/api/terrains/all", [terrain]), post("/api/terrains/all/count", 1)],
  },
  terrain: { handlers: [get(`/api/terrains/${sampleLong}`, terrain)], h1: /Altis/ },
  "terrain-create": { handlers: [get("/api/game-servers/capabilities", gameCapabilities)] },
  "mod-packs": {
    handlers: [
      get("/api/game-servers/capabilities", gameCapabilities),
      get("/api/game-content/mods", []),
      get("/api/game-content/mod-packs", [
        {
          definitionId: Number(sampleLong),
          gameIdentifier: "arma3",
          title: "UTF Main",
          latestRevisionId: null,
        },
      ]),
    ],
  },
  "mod-pack": {
    handlers: [
      get("/api/game-servers/capabilities", gameCapabilities),
      get("/api/game-content/mods", []),
      get(`/api/game-content/mod-packs/${sampleLong}`, {
        definitionId: Number(sampleLong),
        gameIdentifier: "arma3",
        title: "UTF Main",
        isActive: true,
        revisions: [],
      }),
    ],
    h1: /UTF Main/,
  },
  "mod-pack-create": {
    handlers: [
      get("/api/game-servers/capabilities", gameCapabilities),
      get("/api/game-content/mods", []),
      get("/api/game-content/mod-packs", []),
    ],
  },
  "command-verein-settings": { handlers: [get("/api/verein/admin/settings", vereinSettings)] },
  "command-membership-requests": { handlers: [get("/api/verein/admin/membership-requests", [])] },
  "command-verein-documents": { handlers: [get("/api/verein/admin/documents", [adminDocument])] },
  "command-verein-document": {
    handlers: [get("/api/verein/admin/documents", [adminDocument])],
    h1: /Resolution 2026-01/,
  },
  "command-verein-document-create": {
    handlers: [get("/api/verein/admin/documents", [adminDocument])],
  },
  "command-verein-document-edit": {
    handlers: [get("/api/verein/admin/documents", [adminDocument])],
  },
  "command-verein-kassenbuch": { handlers: [get("/api/verein/kassenbuch", kassenbuchPage)] },
  "command-verein-kassenbuch-create": { handlers: [] },
  "command-verein-kassenbuch-edit": { handlers: [get("/api/verein/kassenbuch", kassenbuchPage)] },

  servers: { handlers: [get("/api/game-servers", [hostingServer()])], stream: true },
  "server-create": {
    handlers: [
      get("/api/game-servers/capabilities", [createCapability()]),
      get("/api/game-servers/creation/cost-owner-options", createOwners()),
      get("/api/game-servers/creation/mod-pack-options", []),
      get("/api/configuration-profiles/options", []),
      post("/api/game-servers/storage-demand-estimate", createStorageDemand()),
    ],
  },
  "server-cost-alerts": {
    handlers: [get("/api/cost-owners/mine/alert-settings", costAlertSettings())],
  },
  server: {
    handlers: [
      get(`/api/game-servers/${sampleGuid}`, hostingServer()),
      get(`/api/game-servers/${sampleGuid}/accrued-cost`, {
        gameServerId: sampleGuid,
        session: null,
      }),
      get(`/api/game-servers/${sampleGuid}/jobs`, []),
      get(`/api/game-servers/${sampleGuid}/operations`, []),
      (route, method, path) => fulfillMetricsRoute(route, method, path, sampleGuid),
    ],
    stream: true,
    h1: /Gamma Ops/,
  },
  "configuration-profiles": { handlers: [get("/api/configuration-profiles", [])] },
  "configuration-profile-create": {
    claims: ["adm:hosting"],
    handlers: [
      get("/api/configuration-profiles/definitions", {
        canCreate: true,
        games: [{ gameIdentifier: "arma3", definitions: [] }],
      }),
    ],
  },
  "configuration-profile": {
    claims: ["adm:hosting"],
    handlers: [
      get(`/api/configuration-profiles/${sampleGuid}`, {
        profile: {
          primaryKey: sampleGuid,
          title: "Training profile",
          gameIdentifier: "arma3",
          isArchived: false,
          latestRevisionNumber: 1,
          latestRevisionId: null,
        },
        canModify: true,
        canManageAccess: false,
        canArchive: false,
        definitions: [],
        revisions: [],
      }),
    ],
    h1: /Training profile/,
  },
  "command-hosting-finance": {
    handlers: [
      get("/api/cost-owners", { owners: [financeOwner], totalCount: 1 }),
      get("/api/cost-owners/policy", {
        minimumSafetyRangeEur: 5,
        reservationSliceMinutes: 60,
        timeStampUpdated: null,
        updatedById: null,
      }),
      get("/api/cost-owners/ledger-entries", emptyProtocol),
    ],
  },
  "command-hosting-finance-account": {
    handlers: [
      get(`/api/cost-owners/${sampleGuid}`, financeOwner),
      get(`/api/cost-owners/${sampleGuid}/ledger-entries`, emptyProtocol),
      get(`/api/cost-owners/${sampleGuid}/correctable-deployments`, []),
      get(`/api/cost-owners/${sampleGuid}/alert-settings`, costAlertSettings()),
    ],
    h1: /Operator/,
  },
  "command-hosting-finance-settings": {
    handlers: [
      get("/api/cost-owners", { owners: [financeOwner], totalCount: 1 }),
      get("/api/cost-owners/policy", {
        minimumSafetyRangeEur: 5,
        reservationSliceMinutes: 60,
        timeStampUpdated: null,
        updatedById: null,
      }),
      get("/api/cost-owners/ledger-entries", emptyProtocol),
    ],
  },
};
