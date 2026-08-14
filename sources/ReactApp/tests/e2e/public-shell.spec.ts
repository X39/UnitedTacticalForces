import { expect, test } from "@playwright/test";
import { Buffer } from "node:buffer";

const config = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const event = {
  primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
  title: "Operation Copper Trail",
  description: "A coordinated reconnaissance operation.",
  terrainTitle: "Altis",
  modPackDefinitionTitle: "UTF Main",
  modPackDefinitionIsComposition: false,
  imagePath: "",
  scheduledFor: "2026-09-12T18:00:00Z",
  scheduledForOriginal: "2026-09-12T18:00:00Z",
  timeStampCreated: "2026-08-01T10:00:00Z",
  acceptedCount: 18,
  rejectedCount: 1,
  maybeCount: 2,
  minimumAccepted: 12,
  hostedBy: {
    primaryKey: "2fdd4802-e081-4baa-9120-f1873653a057",
    nickname: "Raven",
    avatar: "",
    avatarMimeType: "image/png",
  },
  metaAcceptance: "Accepted",
};

const fleetServer = {
  primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
  title: "UTF Einsatzserver",
  gameIdentifier: "arma3",
  runtimeProviderIdentifier: "hetzner",
  runtimeProfileIdentifier: "cx32",
  runtimeProfileDisplayName: "CX32",
  runtimeStorageStrategy: "IncludedDisk",
  createdById: "00000000-0000-0000-0000-000000000001",
  costOwnerId: "00000000-0000-0000-0000-000000000001",
  costOwnerName: "Assigned owner",
  isLaunchDisabled: false,
  isArchived: false,
  canModify: false,
  timeStampCreated: "2026-08-13T10:00:00Z",
  timeStampUpdated: "2026-08-13T12:30:00Z",
  pendingJobCount: 2,
  deployment: {
    status: 3,
    stage: "worker-online",
    possibleCost: true,
    resources: [],
  },
  actions: [
    {
      id: "terminate",
      label: "Terminate",
      description: "Immediately release runtime resources.",
      icon: "delete",
      group: "Emergency",
      enabled: true,
      dangerous: true,
      pending: false,
      order: 1000,
      unavailableReason: null,
      confirmationText: "Release every provider resource now.",
      confirmationChallenge: "UTF Einsatzserver",
      expectedDurationSeconds: 120,
      costImpact: "StopsCost",
      costDescription: "Provider cost stops after verified cleanup.",
    },
  ],
};

test.beforeEach(async ({ page }) => {
  await page.route("**/api/**", (route) => {
    throw new Error(`Unexpected API request: ${route.request().method()} ${route.request().url()}`);
  });
  await page.route("**/appsettings.json", (route) => route.fulfill({ json: config }));
  await page.route("**/Insignia-48px.png", (route) =>
    route.fulfill({ status: 200, contentType: "image/png", body: "" }),
  );
  await page.route("**/media/**", (route) => route.fulfill({ status: 404, body: "" }));
  await page.route("**/api/media-assets/*/content", (route) =>
    route.fulfill({ status: 200, contentType: "image/webp", body: "" }),
  );
  await page.route("**/api/users/me", (route) => route.fulfill({ status: 204 }));
  await page.route("**/api/users/authentication-providers", (route) =>
    route.fulfill({ json: { steam: true, discord: true } }),
  );
  await page.route("**/api/site-image-settings", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/events/upcoming", (route) => route.fulfill({ json: [event] }));
  await page.route("**/api/field-reports?**", (route) => route.fulfill({ json: [] }));
});

test("loads the beta shell at a deep link", async ({ page }) => {
  await page.goto("events/upcoming?mode=board");
  await expect(page.getByRole("banner")).toBeVisible();
  await expect(page.getByRole("main")).toBeVisible();
  await expect(page.getByText("Beta", { exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: event.title })).toBeVisible();
});

test("keeps the public shell usable on a narrow keyboard viewport", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("events/upcoming?mode=board");

  const navigation = page.getByRole("navigation", { name: "Community navigation" });
  const toggle = page.getByRole("button", { name: "Toggle navigation" });
  await expect(toggle).toBeVisible();
  await toggle.focus();
  await page.keyboard.press("Enter");
  await expect(toggle).toHaveAttribute("aria-expanded", "true");
  await expect(navigation.getByRole("link", { name: "Operations" })).toBeVisible();
  await expect(page.locator("body")).toHaveJSProperty("scrollWidth", 390);
});

test("reads a public Wiki article without requesting protected history", async ({ page }) => {
  const pageId = "28d1cd18-14f9-4693-b58c-063513a76f9c";
  await page.route("**/api/wiki/all/header", (route) =>
    route.fulfill({
      json: [
        {
          primaryKey: pageId,
          name: "Training/Radio procedure",
          lastModified: "2026-08-12T10:00:00Z",
        },
      ],
    }),
  );
  await page.route(`**/api/wiki/${pageId}`, (route) =>
    route.fulfill({
      json: {
        primaryKey: pageId,
        title: "Training/Radio procedure",
        timeStampCreated: "2026-08-01T10:00:00Z",
        isDeleted: false,
        revisions: [
          {
            primaryKey: "8a2e2f0f-0bb0-4d0a-a59b-f89534b7659d",
            pageForeignKey: pageId,
            timeStampCreated: "2026-08-12T10:00:00Z",
            markdown: "# Radio procedure\n\nUse concise, confirmed calls.",
            comment: "Published guidance",
            author: { primaryKey: pageId, nickname: "Instructor" },
          },
        ],
      },
    }),
  );

  await page.goto(`wiki/${pageId}`);
  await expect(
    page.getByRole("heading", { name: "Radio procedure", exact: true }).first(),
  ).toBeVisible();
  await expect(page.getByText("Use concise, confirmed calls.").last()).toBeVisible();
});

test("offers full-page identity sign-in with a local React return target", async ({ page }) => {
  await page.goto("authentication/login?returnUrl=%2Fnext%2Fevents%2Fupcoming%3Fmode%3Dboard");

  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Steam/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /Discord/ })).toBeVisible();
});

test("navigates the UTC month calendar", async ({ page }) => {
  await page.goto("events/upcoming?mode=calendar");
  await page.getByRole("button", { name: "Next month" }).click();
  await expect(page.getByRole("heading", { name: event.title })).toBeVisible();
  await expect(page.getByText("18 / 12", { exact: true }).first()).toBeVisible();
});

test("renders the permission-filtered command shell for an administrator", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Operator",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "adm", value: null }],
        roles: [],
      },
    }),
  );
  await page.goto("command");
  await expect(page.getByRole("heading", { name: "Command Center" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Game servers" }).first()).toBeVisible();
  await expect(page.getByRole("link", { name: "Events" }).first()).toBeVisible();
});

test("opens and closes the command drawer with the keyboard on mobile", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Operator",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "adm", value: null }],
        roles: [],
      },
    }),
  );
  await page.goto("command");

  const navigation = page.getByRole("navigation", { name: "Command Center navigation" });
  const drawer = page.locator("#command-navigation");
  const open = page.getByRole("button", { name: "Open Command Center navigation" });
  await open.focus();
  await page.keyboard.press("Enter");
  await expect(navigation).toBeVisible();
  await drawer.getByRole("button", { name: "Close Command Center navigation" }).focus();
  await page.keyboard.press("Enter");
  await expect(navigation).not.toBeVisible();
});

test("filters the event administration ledger on canonical and legacy routes", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Planner",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "crt:ev", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/command-center/event-admin-snapshot?**", (route) => {
    expect(route.request().headers()["x-utf-ui-variant"]).toBe("react");
    return route.fulfill({
      json: {
        eventCount: 2,
        upcomingEvents: [
          {
            primaryKey: "ea928d0c-f6af-4194-9942-26031dc0ce3d",
            title: "Operation Daybreak",
            scheduledFor: "2026-09-14T18:00:00Z",
          },
        ],
        events: [
          {
            plainEvent: {
              primaryKey: "ea928d0c-f6af-4194-9942-26031dc0ce3d",
              title: "Operation Daybreak",
              scheduledFor: "2026-09-14T18:00:00Z",
              isVisible: true,
              acceptedCount: 14,
              minimumAccepted: 12,
              maybeCount: 1,
              rejectedCount: 0,
            },
            hostedBy: { nickname: "Raven" },
            owner: { nickname: "Planner" },
            terrain: { title: "Altis" },
            modPackRevision: { definitionTitle: "UTF Main" },
          },
          {
            plainEvent: {
              primaryKey: "e253f221-cad2-494b-85d0-c7008e334da1",
              title: "Hidden rehearsal",
              scheduledFor: "2026-09-16T18:00:00Z",
              isVisible: false,
              acceptedCount: 2,
              minimumAccepted: 8,
              maybeCount: 0,
              rejectedCount: 0,
            },
            hostedBy: { nickname: "Falcon" },
            owner: { nickname: "Planner" },
            terrain: { title: "Malden" },
            modPackRevision: { definitionTitle: "Training" },
          },
        ],
      },
    });
  });

  await page.goto("command/events");
  await expect(page.getByRole("heading", { name: "Event Administration" })).toBeVisible();
  await page.getByRole("button", { name: "Hidden 1", exact: true }).click();
  await expect(page.getByRole("link", { name: "Hidden rehearsal" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Operation Daybreak" })).toHaveCount(0);

  await page.goto("events/all");
  await expect(page.getByRole("heading", { name: "Event Administration" })).toBeVisible();
  await expect(page.getByText("Command Center").first()).toBeVisible();
});

test("authors and publishes a field report on canonical and legacy command routes", async ({
  page,
}) => {
  const reportId = "92f61eb8-e4aa-4e69-af79-3c472a09278c";
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Journal editor",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "adm:wiki", value: null }],
        roles: [],
      },
    }),
  );
  const savedReport = {
    primaryKey: reportId,
    slug: "operation-northstar",
    title: "Operation Northstar",
    excerpt: "A coordinated reconnaissance patrol.",
    category: "After Action",
    markdown: "# Situation\n\nAll objectives were secured.",
    isFeatured: true,
    sortOrder: 2,
    isPublished: false,
    isDeleted: false,
    timeStampCreated: "2026-08-14T09:00:00Z",
    timeStampUpdated: "2026-08-14T09:00:00Z",
    createdByName: "Journal editor",
    updatedByName: "Journal editor",
    auditRecordCount: 1,
  };
  await page.route("**/api/command-center/field-reports-snapshot?**", (route) =>
    route.fulfill({
      json: {
        fieldReportCount: 1,
        publishedFieldReportCount: 0,
        draftFieldReportCount: 1,
        deletedFieldReportCount: 0,
        reports: [savedReport],
      },
    }),
  );
  await page.route("**/api/field-reports", async (route) => {
    expect(route.request().method()).toBe("POST");
    expect(await route.request().postDataJSON()).toMatchObject({
      title: "Operation Northstar",
      category: "After Action",
      isFeatured: true,
      isPublished: false,
    });
    return route.fulfill({ json: savedReport });
  });
  await page.route(`**/api/field-reports/admin/${reportId}`, (route) =>
    route.fulfill({ json: savedReport }),
  );

  await page.goto("command/wiki-media");
  await expect(page.getByRole("heading", { name: "Field reports" })).toBeVisible();
  await expect(page.getByRole("row", { name: /Operation Northstar/ })).toBeVisible();

  await page.goto("command/field-reports/create");
  await page.getByLabel("Title").fill("Operation Northstar");
  await page.getByLabel("Excerpt").fill("A coordinated reconnaissance patrol.");
  await page.getByLabel("Category").fill("After Action");
  await page.getByLabel("Featured").check();
  await page.getByLabel("Markdown body").fill("# Situation\n\nAll objectives were secured.");
  await page.getByRole("button", { name: "Save", exact: true }).last().click();
  await expect(page).toHaveURL(new RegExp(`/next/command/field-reports/${reportId}$`));
  await expect(page.getByText("Field report saved.")).toBeVisible();
});

test("reviews claim usage without exposing authority mutations", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Authority admin",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "adm:rls", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/command-center/authority-snapshot?**", (route) =>
    route.fulfill({
      json: {
        memberCount: 24,
        roleCount: 3,
        claimCount: 2,
        roles: [],
        claims: [
          {
            claim: {
              primaryKey: 10,
              category: "Servers",
              title: "View assigned server",
              identifier: "sv:view",
              description: "Allows read-only access to one server.",
              isPrefix: true,
              value: null,
              valueType: "Guid",
            },
            directMemberCount: 1,
            roleCount: 1,
            roleTitles: ["Game master"],
          },
          {
            claim: {
              primaryKey: 11,
              category: "Legacy",
              title: "Unused claim",
              identifier: "legacy",
              isPrefix: false,
            },
            directMemberCount: 0,
            roleCount: 0,
            roleTitles: [],
          },
        ],
      },
    }),
  );

  await page.goto("command/claims");
  await expect(page.getByRole("heading", { name: "Claims" }).first()).toBeVisible();
  await expect(page.getByRole("row", { name: /View assigned server/ })).toContainText("Direct");
  await page.getByRole("button", { name: "Unused" }).last().click();
  await expect(page.getByRole("row", { name: /Unused claim/ })).toBeVisible();
  await expect(page.getByRole("row", { name: /View assigned server/ })).toHaveCount(0);
});

test("filters the member roster and saves protected member changes", async ({ page }) => {
  const memberId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Member admin",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "adm", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/command-center/community-snapshot?**", (route) =>
    route.fulfill({
      json: {
        memberCount: 2,
        verifiedMemberCount: 1,
        unverifiedMemberCount: 1,
        bannedMemberCount: 0,
        deletedMemberCount: 0,
        activeVereinMemberCount: 1,
        suspendedVereinMemberCount: 0,
        endedVereinMemberCount: 0,
        roleCount: 1,
        claimCount: 2,
        members: [
          {
            primaryKey: memberId,
            nickname: "Raven Actual",
            isVerified: false,
            isBanned: false,
            isDeleted: false,
            roleTitles: ["Planner"],
            directClaimCount: 1,
          },
          {
            primaryKey: "a49f274a-bbad-450a-9de3-9f4cfd8f458e",
            nickname: "Falcon",
            isVerified: true,
            isBanned: false,
            isDeleted: false,
            vereinMembershipStatus: "Active",
            roleTitles: [],
            directClaimCount: 0,
          },
        ],
      },
    }),
  );
  await page.route(`**/api/users/${memberId}`, (route) =>
    route.fulfill({
      json: {
        primaryKey: memberId,
        nickname: "Raven Actual",
        eMail: "raven@example.test",
        isVerified: false,
        isBanned: false,
        isDeleted: false,
        steamId64: "76561198000000001",
        discordUsername: "raven",
      },
    }),
  );
  await page.route(`**/api/users/${memberId}/roles`, (route) =>
    route.fulfill({ json: [{ primaryKey: 7, title: "Planner", description: "Plans operations" }] }),
  );
  await page.route(`**/api/users/${memberId}/claims?**`, (route) =>
    route.fulfill({ json: [{ primaryKey: 11, title: "Verify users", identifier: "usr:verify" }] }),
  );
  await page.route("**/api/command-center/authority-snapshot?**", (route) =>
    route.fulfill({
      json: {
        roles: [{ role: { primaryKey: 7, title: "Planner" }, claims: [], memberCount: 1 }],
        claims: [{ claim: { primaryKey: 11, title: "Verify users", identifier: "usr:verify" } }],
      },
    }),
  );
  await page.route(`**/api/verein/memberships/${memberId}`, (route) =>
    route.fulfill({ status: 404 }),
  );
  await page.route(`**/api/users/${memberId}/update`, async (route) => {
    expect(route.request().method()).toBe("POST");
    expect(await route.request().postDataJSON()).toMatchObject({ isBanned: true });
    return route.fulfill({ status: 204 });
  });

  await page.goto("command/members");
  await expect(page.getByRole("heading", { name: "Members", exact: true })).toBeVisible();
  await expect(page.getByRole("row", { name: /Raven Actual/ })).toBeVisible();
  await page.getByRole("button", { name: "Verified" }).last().click();
  await expect(page.getByRole("row", { name: /Falcon/ })).toBeVisible();
  await page.getByRole("button", { name: "Attention" }).last().click();
  await page.getByRole("link", { name: "Manage" }).click();
  await expect(
    page.getByRole("heading", { name: "Raven Actual", exact: true }).first(),
  ).toBeVisible();
  await page.getByRole("checkbox", { name: "Banned" }).check();
  await page.getByRole("button", { name: "Save profile" }).click();
  await page.getByLabel(/Type/).fill("Raven Actual");
  await page.getByRole("button", { name: "Ban member" }).click();
  await expect(page.getByText("Profile saved.")).toBeVisible();
});

test("selects and saves a reusable public image assignment", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Content editor",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "content:pages", value: null }],
        roles: [],
      },
    }),
  );
  let currentAsset = "1dadc293-2072-4e3f-87ac-480110679746";
  const setting = () => ({
    key: "home-hero",
    title: "Home hero",
    description: "Primary public landing-page imagery.",
    group: "home",
    sortOrder: 1,
    imageAssetFk: currentAsset,
    imagePath: `media-assets/${currentAsset}/content`,
    timeStampCreated: "2026-08-01T10:00:00Z",
    timeStampUpdated: "2026-08-10T10:00:00Z",
    updatedByName: "Content editor",
  });
  const replacement = {
    primaryKey: "9d3d30aa-f42e-4192-874d-f4ef8c902d69",
    originalFileName: "new-home-hero.webp",
    contentType: "image/webp",
    size: 245760,
    imagePath: "media-assets/9d3d30aa-f42e-4192-874d-f4ef8c902d69/content",
    timeStampCreated: "2026-08-13T10:00:00Z",
    timeStampUpdated: "2026-08-13T10:00:00Z",
    createdByName: "Content editor",
    tags: ["site-image"],
    usages: [],
  };
  const current = {
    ...replacement,
    primaryKey: currentAsset,
    originalFileName: "current-home-hero.webp",
    imagePath: `media-assets/${currentAsset}/content`,
  };
  await page.route("**/api/site-image-settings/admin?**", (route) =>
    route.fulfill({ json: [setting()] }),
  );
  await page.route("**/api/media-assets?**", (route) =>
    route.fulfill({ json: [current, replacement] }),
  );
  await page.route(`**/api/media-assets/${currentAsset}`, (route) =>
    route.fulfill({ json: current }),
  );
  await page.route("**/api/site-image-settings/admin/home-hero", async (route) => {
    expect(route.request().method()).toBe("PUT");
    expect(await route.request().postDataJSON()).toEqual({ imageAssetFk: replacement.primaryKey });
    currentAsset = replacement.primaryKey;
    return route.fulfill({ json: setting() });
  });

  await page.goto("command/content/images/home");
  await expect(page.getByRole("heading", { name: "Home images" })).toBeVisible();
  await page.getByRole("button", { name: "Change image" }).click();
  await page.getByRole("button", { name: /new-home-hero\.webp/ }).click();
  await page.getByRole("button", { name: "Apply" }).click();
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByText("Image slot updated.")).toBeVisible();
});

test("manages reusable gallery assets and their tracked usage", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Content editor",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "content:pages", value: null }],
        roles: [],
      },
    }),
  );
  let tags = ["briefing"];
  const galleryAsset = () => ({
    primaryKey: "9d3d30aa-f42e-4192-874d-f4ef8c902d69",
    originalFileName: "operation-briefing.webp",
    contentType: "image/webp",
    size: 245760,
    imagePath: "media-assets/9d3d30aa-f42e-4192-874d-f4ef8c902d69/content",
    timeStampCreated: "2026-08-13T10:00:00Z",
    timeStampUpdated: "2026-08-13T10:00:00Z",
    createdByName: "Content editor",
    tags,
    usages: [
      {
        domain: "public-page",
        domainKey: "gdpr",
        displayName: "Privacy and data protection",
        path: "/command/content/pages/gdpr",
      },
    ],
  });
  await page.route("**/api/media-assets?**", (route) => route.fulfill({ json: [galleryAsset()] }));
  await page.route(`**/api/media-assets/${galleryAsset().primaryKey}/tags`, async (route) => {
    expect(route.request().method()).toBe("PUT");
    expect(await route.request().postDataJSON()).toEqual({ tags: ["briefing", "night"] });
    tags = ["briefing", "night"];
    return route.fulfill({ json: galleryAsset() });
  });

  await page.goto("command/content/gallery");
  await expect(page.getByRole("heading", { name: "Image gallery" })).toBeVisible();
  await expect(page.getByRole("option", { name: /operation-briefing\.webp/ })).toBeVisible();
  await expect(page.getByRole("link", { name: "Open usage" })).toHaveAttribute(
    "href",
    "/next/command/content/pages/gdpr",
  );
  await page.getByLabel("Replace tags").fill("briefing, night");
  await page.getByRole("button", { name: "Save tags" }).click();
  await expect(page.getByText("Tags saved.")).toBeVisible();
});

test("renders localized, redacted operational settings for an administrator", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Operator",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "adm", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/command-center/settings-snapshot", (route) =>
    route.fulfill({
      json: {
        apiBaseUrlConfigured: true,
        clientBaseUrlConfigured: true,
        basePathConfigured: true,
        autoVerifyNewUsers: false,
        features: [
          {
            area: "Discord OAuth",
            isEnabled: true,
            isConfigured: false,
            detail: "This legacy API copy should not be rendered.",
          },
        ],
      },
    }),
  );

  await page.goto("command/settings");
  await expect(page.getByRole("heading", { name: "Operational settings" })).toBeVisible();
  await expect(
    page.getByText("OAuth client credentials are redacted; only readiness is exposed.").first(),
  ).toBeVisible();
  await expect(page.getByText("This legacy API copy should not be rendered.")).toHaveCount(0);
});

test("updates Verein contribution settings shared with the Join workflow", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Verein admin",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: true,
        claims: [{ identifier: "verein:members", value: null }],
        roles: [],
      },
    }),
  );
  let settings = {
    membershipContributionMonthlyAmount: 12.5,
    membershipContributionAnnualAmount: 150,
    membershipContributionCurrency: "EUR",
    membershipContributionPublicTextDe: "Deutscher Hinweis",
    membershipContributionPublicTextEn: "English notice",
    timeStampUpdated: "2026-08-13T10:00:00Z",
  };
  await page.route("**/api/verein/admin/settings", async (route) => {
    if (route.request().method() === "GET") return route.fulfill({ json: settings });
    expect(route.request().method()).toBe("PUT");
    expect(await route.request().postDataJSON()).toEqual({
      membershipContributionMonthlyAmount: 20.5,
      membershipContributionAnnualAmount: 246,
      membershipContributionCurrency: "EUR",
      membershipContributionPublicTextDe: "Deutscher Hinweis",
      membershipContributionPublicTextEn: "English notice",
    });
    settings = {
      ...settings,
      membershipContributionMonthlyAmount: 20.5,
      membershipContributionAnnualAmount: 246,
    };
    return route.fulfill({ json: settings });
  });

  await page.goto("command/settings/verein");
  await expect(page.getByRole("heading", { name: "Verein contribution settings" })).toBeVisible();
  await page.getByLabel("Amount per month").fill("20.50");
  await page.getByLabel("Amount per year").fill("246");
  await page.getByRole("button", { name: "Save settings" }).click();
  await expect(page.getByText("Verein settings saved.")).toBeVisible();
});

test("reviews and approves a Verein membership request", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Verein admin",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: true,
        claims: [{ identifier: "verein:members", value: null }],
        roles: [],
      },
    }),
  );
  const request = {
    primaryKey: 41,
    userFk: "20000000-0000-4000-8000-000000000002",
    userNickname: "Falcon",
    firstName: "Ada",
    lastName: "Lovelace",
    dateOfBirth: "1992-04-18",
    email: "ada@example.test",
    phoneNumber: "+49 1234",
    addressLine1: "Main road 5",
    addressLine2: "",
    postalCode: "10115",
    city: "Berlin",
    country: "Germany",
    motivation: "I want to support the association.",
    acknowledgesMembershipSeparation: true,
    acknowledgesDataProcessing: true,
    acknowledgesSatzung: true,
    status: "Submitted",
    decisionReason: "",
    reviewedByFk: null,
    reviewedByName: null,
    timeStampCreated: "2026-08-12T10:00:00Z",
    timeStampUpdated: "2026-08-12T10:00:00Z",
    timeStampResolved: null,
  };
  await page.route("**/api/verein/admin/membership-requests?**", (route) =>
    route.fulfill({ json: [request] }),
  );
  await page.route("**/api/verein/admin/membership-requests/41/transition", async (route) => {
    expect(route.request().method()).toBe("POST");
    expect(await route.request().postDataJSON()).toEqual({
      status: "Approved",
      reason: "Application reviewed",
      memberNumber: "UTF-42",
      notes: "Welcome pack pending",
    });
    return route.fulfill({
      json: {
        ...request,
        status: "Approved",
        decisionReason: "Application reviewed",
        reviewedByName: "Verein admin",
        timeStampUpdated: "2026-08-13T10:00:00Z",
        timeStampResolved: "2026-08-13T10:00:00Z",
      },
    });
  });

  await page.goto("command/verein/membership-requests");
  await expect(page.getByRole("heading", { name: "Membership requests" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Ada Lovelace" })).toBeVisible();
  await page.getByLabel("Decision reason").fill("Application reviewed");
  await page.getByLabel("Member number").fill("UTF-42");
  await page.getByLabel("Internal notes").fill("Welcome pack pending");
  await page.getByRole("button", { name: "Approve", exact: true }).click();
  await expect(page.getByText("Membership request updated.")).toBeVisible();
  await expect(page.getByText("Approved").last()).toBeVisible();
});

test("reviews public-page publication state from the command center", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Editor",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "content:pages", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/public-pages/admin", (route) =>
    route.fulfill({
      json: [
        {
          primaryKey: 1,
          slug: "gdpr",
          isDeleted: false,
          revision: { title: "Privacy and data protection", summary: "Published policy" },
          revisions: [
            {
              primaryKey: 11,
              pageFk: 1,
              version: 2,
              title: "Privacy and data protection",
              summary: "Published policy",
              isPublished: true,
              timeStampPublished: "2026-08-10T10:00:00Z",
              timeStampCreated: "2026-08-10T09:00:00Z",
            },
          ],
          timeStampCreated: "2026-08-01T10:00:00Z",
          timeStampUpdated: "2026-08-10T10:00:00Z",
        },
      ],
    }),
  );

  await page.goto("command/content/pages");
  await expect(page.getByRole("heading", { name: "Public pages", exact: true })).toBeVisible();
  await expect(page.getByRole("row", { name: /Privacy and data protection/ })).toContainText(
    "v2 published",
  );
});

test("previews a public page revision and updates its publication state", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Editor",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "content:pages", value: null }],
        roles: [],
      },
    }),
  );
  let isPublished = true;
  const publicPage = () => ({
    primaryKey: 1,
    slug: "gdpr",
    isDeleted: false,
    revision: {
      primaryKey: 11,
      pageFk: 1,
      version: 2,
      title: "Privacy and data protection",
      summary: "Current policy",
      markdown: "## Retention policy\n\nOperational records are retained only as required.",
      contentJson: "",
      isPublished,
      timeStampPublished: isPublished ? "2026-08-10T10:00:00Z" : null,
      timeStampCreated: "2026-08-10T09:00:00Z",
      authorName: "Policy editor",
    },
    revisions: [
      {
        primaryKey: 11,
        pageFk: 1,
        version: 2,
        title: "Privacy and data protection",
        summary: "Current policy",
        markdown: "## Retention policy\n\nOperational records are retained only as required.",
        contentJson: "",
        isPublished,
        timeStampPublished: isPublished ? "2026-08-10T10:00:00Z" : null,
        timeStampCreated: "2026-08-10T09:00:00Z",
        authorName: "Policy editor",
      },
    ],
    timeStampCreated: "2026-08-01T10:00:00Z",
    timeStampUpdated: "2026-08-10T10:00:00Z",
  });
  await page.route("**/api/public-pages/admin/gdpr", (route) =>
    route.fulfill({ json: publicPage() }),
  );
  await page.route("**/api/public-pages/admin/revisions/11/unpublish", (route) => {
    expect(route.request().method()).toBe("POST");
    isPublished = false;
    return route.fulfill({ json: publicPage() });
  });

  await page.goto("command/content/pages/gdpr");
  await expect(
    page.getByRole("heading", { name: "Privacy and data protection", exact: true }).first(),
  ).toBeVisible();
  await expect(page.getByText("Operational records are retained only as required.")).toBeVisible();
  await page.getByRole("button", { name: "Unpublish" }).click();
  await expect(page.getByText("Publication state updated.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Publish" })).toBeVisible();
});

test("loads the localized, resource-filtered server fleet at a deep link", async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem("UtfCulture", "de"));
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Operator",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "sv:view", value: fleetServer.primaryKey }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/game-servers", (route) => {
    expect(route.request().headers()["x-utf-ui-variant"]).toBe("react");
    return route.fulfill({ json: [fleetServer] });
  });

  await page.goto("command/servers");
  await expect(page.getByRole("heading", { name: "Spielserver" })).toBeVisible();
  await expect(page.getByRole("heading", { name: fleetServer.title })).toBeVisible();
  await expect(page.getByText("Läuft")).toBeVisible();
  await expect(
    page.getByText("Diese Bereitstellung kann weiterhin Kosten verursachen."),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: "Jetzt deprovisionieren" })).toHaveAttribute(
    "href",
    `/next/command/servers/${fleetServer.primaryKey}?action=terminate`,
  );

  await page.reload();
  await expect(page.getByRole("heading", { name: fleetServer.title })).toBeVisible();
});

test("opens the certified server workspace and submits its projected challenge", async ({
  page,
}) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Operator",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "sv:view", value: fleetServer.primaryKey }],
        roles: [],
      },
    }),
  );
  await page.route(`**/api/game-servers/${fleetServer.primaryKey}/jobs`, (route) =>
    route.fulfill({ json: [] }),
  );
  await page.route(`**/api/game-servers/${fleetServer.primaryKey}/operations?**`, (route) =>
    route.fulfill({ json: [] }),
  );
  await page.route(
    `**/api/game-servers/${fleetServer.primaryKey}/actions/terminate`,
    async (route) => {
      expect(route.request().method()).toBe("POST");
      expect(route.request().postDataJSON()).toEqual({ confirmation: fleetServer.title });
      return route.fulfill({
        json: {
          primaryKey: "20000000-0000-4000-8000-000000000002",
          gameServerId: fleetServer.primaryKey,
          actionId: "terminate",
          kind: "Terminate",
          status: "Queued",
          stage: "queued",
          progressPercent: 0,
          attemptCount: 0,
          canRetry: false,
          canSkip: false,
          failureCode: null,
          failureReason: null,
          timeStampCreated: "2026-08-14T00:00:00Z",
          timeStampUpdated: "2026-08-14T00:00:00Z",
          commands: [],
        },
      });
    },
  );
  await page.route(`**/api/game-servers/${fleetServer.primaryKey}`, (route) =>
    route.fulfill({ json: fleetServer }),
  );

  await page.goto(`command/servers/${fleetServer.primaryKey}?action=terminate`);
  await expect(page.getByRole("heading", { name: fleetServer.title }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Configuration" })).toBeVisible();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("textbox").fill(fleetServer.title);
  await dialog.getByRole("button", { name: "Terminate" }).click();
  await expect(dialog).not.toBeVisible();
  await expect(page).toHaveURL(new RegExp(`/next/command/servers/${fleetServer.primaryKey}$`));

  await page.reload();
  await expect(page.getByRole("heading", { name: fleetServer.title }).first()).toBeVisible();
});

test("supports German localization", async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem("UtfCulture", "de"));
  await page.goto("");
  await expect(page.locator("html")).toHaveAttribute("lang", "de");
  await expect(page.getByRole("button", { name: "Klassische Oberfläche verwenden" })).toBeVisible();
});

test("shows the React 404 for an unknown route without switching variants", async ({ page }) => {
  await page.goto("unknown/workflow?from=beta#missing");
  await expect(page.getByRole("heading", { name: "Route not found" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Return home" })).toHaveAttribute("href", "/next");
  await expect
    .poll(
      async () =>
        (await page.context().cookies()).find((cookie) => cookie.name === "utf_ui_variant")?.value,
    )
    .not.toBe("classic");
});

test("operates the terrain catalog and resource workspace on canonical and legacy routes", async ({
  page,
}) => {
  // Vite's development module URLs contain `/src/api/`; the production E2E server does not.
  await page.unroute("**/api/**");
  let title = "Altis";
  const terrains = () => [
    {
      primaryKey: 41,
      gameIdentifier: "arma3",
      title,
      imageAssetFk: "c9a541fe-e715-4643-b085-d655514e518b",
      imagePath: "media-assets/c9a541fe-e715-4643-b085-d655514e518b/content",
      isActive: true,
    },
    {
      primaryKey: 42,
      gameIdentifier: "arma3",
      title: "Stratis",
      imageAssetFk: null,
      imagePath: null,
      isActive: true,
    },
  ];
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Terrain operator",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "adm:trn", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/terrains/all/count", (route) => route.fulfill({ json: 2 }));
  await page.route("**/api/terrains/all?**", (route) => {
    expect(route.request().method()).toBe("POST");
    expect(route.request().headers()["x-utf-ui-variant"]).toBe("react");
    return route.fulfill({ json: terrains() });
  });
  await page.route("**/api/terrains/41/update", async (route) => {
    const payload = route.request().postDataJSON() as { title?: string };
    title = payload.title ?? title;
    return route.fulfill({ status: 204 });
  });
  await page.route("**/api/terrains/41/delete", (route) => route.fulfill({ status: 204 }));
  await page.route("**/api/terrains/41", (route) => route.fulfill({ json: terrains()[0] }));

  await page.goto("command/terrains");
  await expect(page.getByRole("heading", { name: "All Terrains", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Missing image", exact: true }).last().click();
  await expect(page.getByRole("row", { name: /Stratis/ })).toBeVisible();
  await expect(page.getByRole("row", { name: /Altis/ })).toHaveCount(0);

  await page.goto("data/terrains/all");
  await expect(page.getByRole("heading", { name: "All Terrains", exact: true })).toBeVisible();
  await page.goto("data/terrains/41");
  await expect(page.getByRole("heading", { name: "Altis", exact: true }).first()).toBeVisible();
  const titleInput = page.getByRole("textbox", { name: "Title" });
  await titleInput.fill("Altis 2035");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page.getByText("Terrain changes saved.")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Altis 2035", exact: true }).first(),
  ).toBeVisible();

  await page.getByRole("button", { name: "Deactivate" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByRole("dialog").getByRole("button", { name: "Delete" }).click();
  await expect(page).toHaveURL(/\/next\/command\/terrains$/);
});

test("creates a terrain with a registered game and gallery image", async ({ page }) => {
  await page.unroute("**/api/**");
  const imageAsset = {
    primaryKey: "c9a541fe-e715-4643-b085-d655514e518b",
    originalFileName: "terrain.webp",
    contentType: "image/webp",
    size: 524288,
    imagePath: "media-assets/c9a541fe-e715-4643-b085-d655514e518b/content",
    timeStampCreated: "2026-08-14T00:00:00Z",
    timeStampUpdated: "2026-08-14T00:00:00Z",
    createdByName: "Terrain operator",
    tags: ["terrain"],
    usages: [],
  };
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Terrain operator",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "crt:trn", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/game-servers/capabilities", (route) =>
    route.fulfill({
      json: [
        {
          gameIdentifier: "arma3",
          displayName: "Arma 3",
          actions: [],
          runtimeProviders: [],
          runtimeProviderProfiles: [],
        },
      ],
    }),
  );
  await page.route("**/api/media-assets?**", (route) => route.fulfill({ json: [imageAsset] }));
  await page.route("**/api/terrains/create", async (route) => {
    expect(route.request().method()).toBe("POST");
    expect(route.request().postDataJSON()).toEqual({
      gameIdentifier: "arma3",
      title: "Tanoa",
      imageAssetFk: imageAsset.primaryKey,
    });
    return route.fulfill({
      json: {
        primaryKey: 43,
        gameIdentifier: "arma3",
        title: "Tanoa",
        imageAssetFk: imageAsset.primaryKey,
        imagePath: imageAsset.imagePath,
        isActive: true,
      },
    });
  });
  await page.route("**/api/terrains/43", (route) =>
    route.fulfill({
      json: {
        primaryKey: 43,
        gameIdentifier: "arma3",
        title: "Tanoa",
        imageAssetFk: imageAsset.primaryKey,
        imagePath: imageAsset.imagePath,
        isActive: true,
      },
    }),
  );

  await page.goto("data/terrains/create");
  await expect(page.getByRole("heading", { name: "Create Terrain", exact: true })).toBeVisible();
  await page.getByRole("textbox", { name: "Title" }).fill("Tanoa");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.getByRole("button", { name: "Select image" }).click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: /terrain\.webp/ })
    .click();
  await page.getByRole("dialog").getByRole("button", { name: "Apply" }).click();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  const createButton = page.getByRole("button", { name: "Create terrain", exact: true });
  await expect(createButton).toBeEnabled();
  await createButton.evaluate((button: HTMLButtonElement) => button.click());
  await expect(page).toHaveURL(/\/next\/command\/terrains\/43$/);
  await expect(page.getByRole("heading", { name: "Tanoa", exact: true }).first()).toBeVisible();
});

test("operates mod packs on canonical and legacy command routes", async ({ page }) => {
  await page.unroute("**/api/**");
  let title = "UTF Main";
  const definitions = () => [
    {
      primaryKey: 42,
      gameIdentifier: "arma3",
      title,
      owner: { primaryKey: "00000000-0000-0000-0000-000000000001", nickname: "Operator" },
      isActive: true,
      isComposition: false,
      timeStampCreated: "2026-08-01T10:00:00Z",
      modPackRevisions: [
        {
          primaryKey: 8,
          definitionFk: 42,
          isActive: true,
          timeStampCreated: "2026-08-13T10:00:00Z",
        },
      ],
    },
    {
      primaryKey: 43,
      gameIdentifier: "arma3",
      title: "Joint Operations",
      owner: { primaryKey: "00000000-0000-0000-0000-000000000001", nickname: "Operator" },
      isActive: true,
      isComposition: true,
      timeStampCreated: "2026-08-02T10:00:00Z",
      modPackRevisions: [],
    },
  ];
  const workspace = () => ({
    definition: {
      primaryKey: 42,
      gameIdentifier: "arma3",
      title,
      ownerFk: "00000000-0000-0000-0000-000000000001",
      isActive: true,
      isComposition: false,
      revisionPrimaryKey: 8,
      revisionHtml: "<h1>UTF Main</h1><script>window.__unsafe = true</script>",
      timeStampCreated: "2026-08-01T10:00:00Z",
    },
    owner: { primaryKey: "00000000-0000-0000-0000-000000000001", nickname: "Operator" },
    latestRevision: {
      primaryKey: 8,
      definitionFk: 42,
      isActive: true,
      timeStampCreated: "2026-08-13T10:00:00Z",
      updatedByFk: "00000000-0000-0000-0000-000000000001",
    },
    compositionMembers: [],
    revisionPreview: [
      {
        primaryKey: 8,
        definitionFk: 42,
        isActive: true,
        timeStampCreated: "2026-08-13T10:00:00Z",
        updatedByFk: "00000000-0000-0000-0000-000000000001",
      },
    ],
    downloadPreview: [],
    eventUsageCount: 3,
    downloadRecordCount: 0,
    downloadUserCount: 0,
    lastDownloaded: null,
    canModify: true,
    canDelete: true,
    canViewLedgers: true,
  });
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Operator",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "adm:mp", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/mod-packs/all/count?**", (route) => route.fulfill({ json: 2 }));
  await page.route("**/api/mod-packs/all?**", (route) => {
    expect(route.request().headers()["x-utf-ui-variant"]).toBe("react");
    return route.fulfill({ json: definitions() });
  });
  await page.route("**/api/mod-packs/42/workspace", (route) =>
    route.fulfill({ json: workspace() }),
  );
  await page.route("**/api/mod-packs/42/update/standalone", async (route) => {
    const payload = route.request().postDataJSON() as { title?: string | null };
    title = payload.title ?? title;
    return route.fulfill({ status: 204 });
  });

  await page.goto("command/mod-packs");
  await expect(page.getByRole("heading", { name: "All mod packs", exact: true })).toBeVisible();
  await page
    .getByRole("button", { name: /Compositions/ })
    .last()
    .click();
  await expect(page.getByText("Joint Operations", { exact: true })).toBeVisible();
  await expect(page.getByText("UTF Main", { exact: true })).toHaveCount(0);

  await page.goto("data/mod-packs/all");
  await expect(page.getByRole("heading", { name: "All mod packs", exact: true })).toBeVisible();
  await page.goto("data/mod-packs/42");
  await expect(page.getByRole("heading", { name: "UTF Main", exact: true }).first()).toBeVisible();
  await page.getByRole("link", { name: "Edit", exact: true }).last().click();
  await page.getByRole("textbox", { name: "Title" }).fill("UTF Main 2026");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page.getByText("Mod pack changes were saved.")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "UTF Main 2026", exact: true }).first(),
  ).toBeVisible();

  await page.getByRole("link", { name: "Revisions", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Owned revisions", exact: true })).toBeVisible();
});

test("creates a standalone mod pack through the certified legacy route", async ({ page }) => {
  await page.unroute("**/api/**");
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-0000-0000-000000000001",
        nickname: "Operator",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "crt:mp", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/game-servers/capabilities", (route) =>
    route.fulfill({
      json: [
        {
          gameIdentifier: "arma3",
          displayName: "Arma 3",
          actions: [],
          runtimeProviders: [],
          runtimeProviderProfiles: [],
        },
      ],
    }),
  );
  await page.route("**/api/mod-packs/create/standalone", async (route) => {
    expect(route.request().method()).toBe("POST");
    expect(route.request().postDataJSON()).toEqual({
      gameIdentifier: "arma3",
      title: "UTF Training",
      html: "<h1>Training</h1>",
    });
    return route.fulfill({ json: { primaryKey: 44, title: "UTF Training" } });
  });
  await page.route("**/api/mod-packs/44/workspace", (route) =>
    route.fulfill({
      json: {
        definition: {
          primaryKey: 44,
          gameIdentifier: "arma3",
          title: "UTF Training",
          ownerFk: "00000000-0000-0000-0000-000000000001",
          isActive: true,
          isComposition: false,
          revisionPrimaryKey: 9,
          revisionHtml: "<h1>Training</h1>",
        },
        owner: { nickname: "Operator" },
        latestRevision: { primaryKey: 9, definitionFk: 44, isActive: true },
        compositionMembers: [],
        revisionPreview: [],
        downloadPreview: [],
        eventUsageCount: 0,
        downloadRecordCount: 0,
        downloadUserCount: 0,
        canModify: true,
        canDelete: true,
        canViewLedgers: true,
      },
    }),
  );

  await page.goto("data/mod-packs/create?kind=standalone");
  await expect(page.getByRole("heading", { name: "Create Mod Pack", exact: true })).toBeVisible();
  await page.getByRole("textbox", { name: "Title" }).fill("UTF Training");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.locator('input[type="file"]').setInputFiles({
    name: "training.html",
    mimeType: "text/html",
    buffer: Buffer.from("<h1>Training</h1>"),
  });
  await page.getByRole("button", { name: "Next", exact: true }).click();
  const createButton = page.getByRole("button", { name: "Create mod pack", exact: true });
  await expect(createButton).toBeEnabled();
  await createButton.click();
  await expect(page).toHaveURL(/\/next\/command\/mod-packs\/44$/);
  await expect(
    page.getByRole("heading", { name: "UTF Training", exact: true }).first(),
  ).toBeVisible();
});
