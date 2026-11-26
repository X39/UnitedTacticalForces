import { expect, test } from "@playwright/test";
import { mockQuietCostReads } from "./support/costMocks";
import { fulfillMetricsRoute } from "./support/metricsMocks";
import { Buffer } from "node:buffer";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const webAssetRoot = fileURLToPath(new URL("../../../WebApp/wwwroot/", import.meta.url));
const publicImageAssets = new Map<string, Buffer>(
  [
    "/Insignia-48px.png",
    "/gdpr-privacy-hero.png",
    "/media/generated/operations-convoy-hero.jpg",
    "/media/generated/unit-training-assembly-hero.jpg",
    "/media/generated/verein-governance-table-hero.jpg",
    "/media/generated/join-recruitment-staging-hero.jpg",
    "/media/generated/field-reports-table-hero.jpg",
  ].map((pathname): [string, Buffer] => [
    pathname,
    readFileSync(resolve(webAssetRoot, pathname.slice(1))),
  ]),
);

const config = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const event = {
  primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
  title: "Operation Copper Trail",
  description:
    "## Briefing\n\n**Coordinate reconnaissance** from the [published orders](/events/briefing).",
  terrainTitle: "Altis",
  modPackDefinitionTitle: "UTF Main",
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
  await mockQuietCostReads(page);
  await page.route("**/Insignia-48px.png", (route) =>
    route.fulfill({
      status: 200,
      contentType: "image/png",
      body: publicImageAssets.get("/Insignia-48px.png")!,
    }),
  );
  await page.route("**/gdpr-privacy-hero.png", (route) =>
    route.fulfill({
      status: 200,
      contentType: "image/png",
      body: publicImageAssets.get("/gdpr-privacy-hero.png")!,
    }),
  );
  await page.route("**/media/**", (route) => {
    const pathname = new URL(route.request().url()).pathname;
    const body = publicImageAssets.get(pathname);
    return body
      ? route.fulfill({ status: 200, contentType: "image/jpeg", body })
      : route.fulfill({ status: 404, body: "" });
  });
  await page.route("**/api/media-assets/*/content", (route) =>
    route.fulfill({ status: 200, contentType: "image/webp", body: "" }),
  );
  await page.route("**/api/users/me", (route) => route.fulfill({ status: 204 }));
  await page.route("**/api/users/authentication-providers", (route) =>
    route.fulfill({ json: { steam: true, discord: true } }),
  );
  await page.route("**/api/game-servers/notifications", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/game-servers", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/site-image-settings", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/events/upcoming", (route) => route.fulfill({ json: [event] }));
  await page.route("**/api/field-reports?**", (route) => route.fulfill({ json: [] }));
});

test("loads the beta shell at a deep link", async ({ page }) => {
  await page.goto("field-reports");
  await expect(page.getByRole("banner")).toBeVisible();
  await expect(page.getByRole("main")).toBeVisible();
  await expect(page.getByText("Beta", { exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Field Reports", level: 1 })).toBeVisible();
});

test("renders home operation summaries as Markdown excerpts", async ({ page }) => {
  await page.goto(".");

  const heroBriefing = page.locator(".home-hero__content .markdown--excerpt");
  const cardBriefing = page.locator(".operation-card .markdown--excerpt").first();
  await expect(heroBriefing).toContainText("Briefing");
  await expect(heroBriefing.getByText("Coordinate reconnaissance", { exact: true })).toBeVisible();
  await expect(cardBriefing).toContainText("Coordinate reconnaissance");
  await expect(cardBriefing.getByRole("link")).toHaveCount(0);
  await expect(cardBriefing).not.toContainText("**");
});

test("returns a reopened route to its classic equivalent", async ({ page }) => {
  await page.addInitScript(() => {
    document.cookie = "utf_ui_variant=react; Path=/; SameSite=Lax";
  });
  await page.goto("gdpr?section=cookies#functional-preferences");

  await expect(page.getByTestId("classic-shell")).toBeVisible();
  await expect(page).toHaveURL(/\/gdpr\?section=cookies#functional-preferences$/);
  await expect
    .poll(
      async () =>
        (await page.context().cookies()).find((cookie) => cookie.name === "utf_ui_variant")?.value,
    )
    .toBe("react");
});

test("keeps the public shell usable on a narrow keyboard viewport", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("field-reports");

  const navigation = page.getByRole("navigation", { name: "Community navigation" });
  const toggle = page.getByRole("button", { name: "Toggle navigation" });
  await expect(toggle).toBeVisible();
  await toggle.focus();
  await page.keyboard.press("Enter");
  await expect(toggle).toHaveAttribute("aria-expanded", "true");
  await expect(navigation.getByRole("link", { name: "Operations" })).toBeVisible();
  await expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth))
    .toBeLessThanOrEqual(0);
});

test("contains keyboard focus in the open community menu on mobile", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("field-reports");

  const navigation = page.getByRole("navigation", { name: "Community navigation" });
  const toggle = page.getByRole("button", { name: "Toggle navigation" });
  await toggle.focus();
  await page.keyboard.press("Enter");
  await expect(toggle).toHaveAttribute("aria-expanded", "true");

  // Opening moves focus to the first link and makes the page behind the menu inert.
  await expect(navigation.getByRole("link").first()).toBeFocused();
  await expect(page.locator("main#main-content")).toHaveAttribute("inert", "");

  // Tab from the last link reaches the toggle, not the covered page content.
  await navigation.getByRole("link").last().focus();
  await page.keyboard.press("Tab");
  await expect(toggle).toBeFocused();

  // Escape closes the menu, returns focus to the toggle and lifts the inert state.
  await page.keyboard.press("Escape");
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  await expect(toggle).toBeFocused();
  await expect(page.locator("main#main-content")).not.toHaveAttribute("inert", /.*/);
});

test("keeps the German Join journey inside a narrow viewport", async ({ page }) => {
  await page.route("**/api/verein/settings", (route) =>
    route.fulfill({
      json: {
        membershipContributionMonthlyAmount: 5,
        membershipContributionAnnualAmount: 60,
        membershipContributionCurrency: "EUR",
        membershipContributionPublicTextDe: "Der Beitrag finanziert die Infrastruktur.",
        membershipContributionPublicTextEn: "The contribution funds infrastructure.",
        timeStampUpdated: "2026-08-14T10:00:00Z",
      },
    }),
  );
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("join");
  await page.getByLabel("Language").selectOption("de");

  await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1);
  await expect(page.getByRole("button", { name: "Anmelden" })).toBeVisible();
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      ),
    )
    .toBeLessThanOrEqual(0);
});

test("keeps identity and operational attention in the signed-in shell", async ({ page }) => {
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-4000-8000-000000000001",
        nickname: "Falcon One",
        eMail: "falcon@example.test",
        avatar: "AQIDBA==",
        avatarMimeType: "image/jpeg",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: true,
        vereinMembershipStatus: "Active",
        vereinMemberNumber: "UTF-42",
        steamId64: "76561198012345678",
        discordId: "123456789",
        discordUsername: "falcon.discord",
        claims: [
          {
            primaryKey: 1,
            category: "Administration",
            title: "Global administration",
            identifier: "adm",
            description: "",
            isPrefix: false,
            value: null,
            valueType: null,
          },
        ],
        roles: [
          {
            primaryKey: 1,
            title: "Operations Lead",
            description: "Hosts community operations",
            claims: [],
          },
        ],
      },
    }),
  );
  await page.unroute("**/api/game-servers/notifications");
  await page.route("**/api/game-servers/notifications", (route) =>
    route.fulfill({
      json: [
        {
          serverId: fleetServer.primaryKey,
          title: "Profile Server",
          statusLabel: "Raw API stage text",
          possibleCost: true,
          canOperate: true,
          costStopAction: null,
        },
      ],
    }),
  );

  await page.route("**/api/game-servers", (route) => route.fulfill({ json: [fleetServer] }));

  await page.goto("field-reports");
  const trigger = page.locator(".account-profile__trigger");
  await expect(trigger.locator("img")).toHaveAttribute("src", "data:image/jpeg;base64,AQIDBA==");
  await trigger.click();

  const profile = page.locator(".account-profile__popover");
  await expect(profile).toBeVisible();
  await expect(profile).toContainText("Operations Lead");
  await expect(profile).toContainText("Profile Server");
  await expect(profile).toContainText("Running");
  await expect(profile).toContainText("Costing now");
  await expect(profile).not.toContainText("Raw API stage text");
  await expect(profile).toContainText("7656...5678");
  await expect(trigger.locator(".account-profile__attention-badge")).toHaveText("1");
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
  await page.clock.setFixedTime(new Date("2026-08-12T12:00:00Z"));
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
  await expect(page.getByRole("link", { name: "Servers", exact: true }).first()).toBeVisible();
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

  // Opening moves focus into the drawer and makes the covered column inert.
  const close = drawer.getByRole("button", { name: "Close Command Center navigation" });
  await expect(close).toBeFocused();
  await expect(page.locator(".command-main")).toHaveAttribute("inert", "");

  // Tab never leaves the drawer, even one press past its last control.
  const focusableCount = await drawer.locator("a[href], button:not([disabled])").count();
  for (let press = 0; press <= focusableCount; press += 1) {
    await page.keyboard.press("Tab");
    expect(
      await page.evaluate(() =>
        document.querySelector("#command-navigation")?.contains(document.activeElement),
      ),
    ).toBe(true);
  }

  // Back on the close button, Shift+Tab wraps to the last drawer link.
  await close.focus();
  await page.keyboard.press("Shift+Tab");
  await expect(drawer.getByRole("link").last()).toBeFocused();

  // Escape closes the drawer, gives focus back to the toggle and lifts the inert state.
  await page.keyboard.press("Escape");
  await expect(navigation).not.toBeVisible();
  await expect(open).toBeFocused();
  await expect(page.locator(".command-main")).not.toHaveAttribute("inert", /.*/);

  await open.focus();
  await page.keyboard.press("Enter");
  await expect(navigation).toBeVisible();
  await close.focus();
  await page.keyboard.press("Enter");
  await expect(navigation).not.toBeVisible();
});

test("keeps keyboard focus in the open command drawer after a pointer click on its surface", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ reducedMotion: "reduce" });
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

  const drawer = page.locator("#command-navigation");
  const open = page.getByRole("button", { name: "Open Command Center navigation" });
  const skipLink = page.locator("a.skip-link");
  const scrim = page.locator(".command-scrim");
  await open.click();
  await expect(
    drawer.getByRole("button", { name: "Close Command Center navigation" }),
  ).toBeFocused();

  // While the drawer is open the skip link and the scrim are not tab stops.
  await expect(skipLink).toHaveAttribute("inert", "");
  await expect(scrim).toHaveAttribute("tabindex", "-1");

  /** Whether focus is inside the drawer and on neither the skip link nor the scrim. */
  const focusStaysInDrawer = () =>
    page.evaluate(() => {
      const active = document.activeElement;
      return (
        document.querySelector("#command-navigation")?.contains(active) === true &&
        !active?.matches("a.skip-link, .command-scrim")
      );
    });

  // A click on the brand text leaves focus on the body; Shift+Tab and Tab must still stay in the drawer.
  const brand = page.locator(".command-nav__brand strong");
  await brand.click();
  await page.keyboard.press("Shift+Tab");
  expect(await focusStaysInDrawer()).toBe(true);
  await expect(drawer.getByRole("link").last()).toBeFocused();
  await brand.click();
  await page.keyboard.press("Tab");
  expect(await focusStaysInDrawer()).toBe(true);

  // A click on the blank drawer surface below the last link is the path that reached the scrim.
  await drawer.getByRole("link").last().scrollIntoViewIfNeeded();
  const blank = await page.evaluate(() => {
    const aside = document.querySelector("#command-navigation")!;
    const links = aside.querySelectorAll("a");
    const lastLink = links[links.length - 1]!.getBoundingClientRect();
    const area = aside.getBoundingClientRect();
    const bottom = Math.min(area.bottom, window.innerHeight);
    const x = area.left + area.width / 2;
    const y = (lastLink.bottom + bottom) / 2;
    const hit = document.elementFromPoint(x, y);
    return {
      x,
      y,
      gap: bottom - lastLink.bottom,
      inside: aside.contains(hit),
      interactive: hit?.closest("a, button") != null,
    };
  });
  expect(blank.gap).toBeGreaterThan(8);
  expect(blank.inside).toBe(true);
  expect(blank.interactive).toBe(false);
  await page.mouse.click(blank.x, blank.y);
  await page.keyboard.press("Tab");
  expect(await focusStaysInDrawer()).toBe(true);

  // Escape closes the drawer, returns focus to the toggle and lifts the skip link's inert state.
  await page.keyboard.press("Escape");
  await expect(open).toBeFocused();
  await expect(skipLink).not.toHaveAttribute("inert", /.*/);
});

test("keeps the profile popover's Tab order and closes one layer per Escape over the open community menu", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.unroute("**/api/users/me");
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: "00000000-0000-4000-8000-000000000001",
        nickname: "Falcon One",
        eMail: "falcon@example.test",
        avatar: "AQIDBA==",
        avatarMimeType: "image/jpeg",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        steamId64: null,
        discordId: null,
        discordUsername: null,
        claims: [],
        roles: [],
      },
    }),
  );
  await page.goto("field-reports");

  const toggle = page.getByRole("button", { name: "Toggle navigation" });
  await toggle.click();
  await expect(toggle).toHaveAttribute("aria-expanded", "true");

  // The popover opens by pointer over the open menu; no force option, so a covered trigger would fail here.
  await page.locator(".account-profile__trigger").click();
  const popover = page.getByRole("dialog", { name: "Profile menu" });
  await expect(popover).toBeVisible();
  await expect(popover.getByRole("button", { name: "Close profile menu" })).toBeFocused();

  // Tab moves to the next popover control and never to the navigation toggle.
  for (let press = 0; press < 2; press += 1) {
    await page.keyboard.press("Tab");
    expect(
      await page.evaluate(
        () =>
          document.querySelector("#account-profile-panel")?.contains(document.activeElement) ===
          true,
      ),
    ).toBe(true);
    await expect(toggle).not.toBeFocused();
  }

  // The first Escape closes only the popover; the menu stays open and the page stays inert.
  await page.keyboard.press("Escape");
  await expect(popover).toBeHidden();
  await expect(toggle).toHaveAttribute("aria-expanded", "true");
  await expect(page.locator("main#main-content")).toHaveAttribute("inert", "");

  // The second Escape closes the menu.
  await page.keyboard.press("Escape");
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  await expect(page.locator("main#main-content")).not.toHaveAttribute("inert", /.*/);
});

test("keeps Tab inside the sign-in dialog opened over the open community menu", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("field-reports");

  const toggle = page.getByRole("button", { name: "Toggle navigation" });
  await toggle.click();
  await expect(toggle).toHaveAttribute("aria-expanded", "true");

  // The sign-in dialog opens by pointer over the open menu; no force option, so a covered trigger would fail here.
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  const dialog = page.locator("dialog[open]");
  await expect(dialog).toBeVisible();

  // Chromium focuses the dialog element itself on a blank click. Blurring afterwards puts focus on the body, which is
  // the state where the drawer cycle used to take Tab over and push focus into the menu behind the dialog.
  const box = (await dialog.boundingBox())!;
  await page.mouse.click(box.x + 4, box.y + 4);
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  expect(await page.evaluate(() => document.activeElement === document.body)).toBe(true);
  await page.keyboard.press("Tab");

  expect(
    await page.evaluate(
      () => document.querySelector("dialog[open]")?.contains(document.activeElement) === true,
    ),
  ).toBe(true);
  await expect(toggle).not.toBeFocused();
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
            hostedBy: {
              primaryKey: "00000000-0000-0000-0000-000000000001",
              nickname: "Raven",
            },
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
            hostedBy: {
              primaryKey: "00000000-0000-0000-0000-000000000001",
              nickname: "Falcon",
            },
            owner: { nickname: "Planner" },
            terrain: { title: "Malden" },
            modPackRevision: { definitionTitle: "Training" },
          },
        ],
      },
    });
  });

  await page.goto("command/events");
  await expect(page.getByRole("heading", { level: 1, name: "Events", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Hidden 1", exact: true }).click();
  await expect(page.getByRole("link", { name: "Hidden rehearsal" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Operation Daybreak" })).toHaveCount(0);

  await page.goto("events/all");
  await expect(page.getByRole("heading", { level: 1, name: "Events", exact: true })).toBeVisible();
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
  await page.getByRole("button", { name: "Ban member", exact: true }).click();
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
  await expect(page.getByRole("heading", { level: 1, name: "Gallery", exact: true })).toBeVisible();
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
  await expect(page.getByRole("heading", { name: "Server", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: fleetServer.title })).toBeVisible();
  await expect(page.getByText("Läuft", { exact: true })).toBeVisible();
  await expect(
    page.getByText(
      "1 Server verursacht Kosten oder könnte noch Kosten verursachen. Fahre Server herunter, die du nicht nutzt.",
      { exact: true },
    ),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: "Server öffnen" })).toHaveAttribute(
    "href",
    `/next/command/servers/${fleetServer.primaryKey}`,
  );
  await expect(page.getByRole("link", { name: "Ohne Speichern herunterfahren" })).toHaveCount(0);
  await expect(
    page.getByText("Diese Bereitstellung kann weiterhin Kosten verursachen."),
  ).toHaveCount(0);

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
  await page.route(`**/api/game-servers/${fleetServer.primaryKey}/metrics**`, async (route) => {
    const path = new URL(route.request().url()).pathname;
    await fulfillMetricsRoute(route, route.request().method(), path, fleetServer.primaryKey);
  });
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
  await expect(page.getByRole("link", { name: "Settings", exact: true })).toBeVisible();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("textbox").fill(fleetServer.title);
  await dialog.getByRole("button", { name: "Shut down without saving" }).click();
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
  await expect(page.getByRole("heading", { name: "Terrains", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Missing image", exact: true }).last().click();
  await expect(page.getByRole("row", { name: /Stratis/ })).toBeVisible();
  await expect(page.getByRole("row", { name: /Altis/ })).toHaveCount(0);

  await page.goto("data/terrains/all");
  await expect(page.getByRole("heading", { name: "Terrains", exact: true })).toBeVisible();
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
  await expect(page.getByRole("heading", { name: "Create terrain", exact: true })).toBeVisible();
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
