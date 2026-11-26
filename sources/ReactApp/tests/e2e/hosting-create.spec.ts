import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { fulfillCostRoute } from "./support/costMocks";
import { fulfillMetricsRoute } from "./support/metricsMocks";

const userId = "10000000-0000-4000-8000-0000000000c1";
const createdServerId = "10000000-0000-4000-8000-0000000000c2";
const personalOwnerId = "10000000-0000-4000-8000-0000000000c3";
const sharedOwnerId = "10000000-0000-4000-8000-0000000000c4";
const wcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];
const bytesPerGiB = 1_073_741_824;

type Culture = "en" | "de";

/** An Arma-shaped capability served by one available provider with two priced sizes and external storage. */
const capability = {
  gameIdentifier: "arma3",
  displayName: "Arma 3",
  description: "Dedicated Arma operations.",
  runtimeProviders: ["fake-cloud"],
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
  runtimeProviderProfiles: [
    {
      identifier: "fake-cloud",
      availabilityStatus: "available",
      profiles: [
        {
          identifier: "small",
          displayName: "Small",
          description: "Small compute profile.",
          virtualCpuCount: 2,
          memoryGiB: 4,
          diskGiB: 80,
          includedStorageBytes: 80 * bytesPerGiB,
          usableStorageBytes: 70 * bytesPerGiB,
          estimatedHourlyCostEur: 0.05,
          estimatedMonthlyCostEur: 36.5,
        },
        {
          identifier: "medium",
          displayName: "Medium",
          description: "Medium compute profile.",
          virtualCpuCount: 4,
          memoryGiB: 8,
          diskGiB: 80,
          includedStorageBytes: 80 * bytesPerGiB,
          usableStorageBytes: 70 * bytesPerGiB,
          estimatedHourlyCostEur: 0.1,
          estimatedMonthlyCostEur: 73,
        },
      ],
      storage: {
        supportsExternalBlockStorage: true,
        minimumCapacityGiB: 20,
        maximumCapacityGiB: 500,
        capacityIncrementGiB: 10,
        estimatedHourlyCostPerGiBEur: 0.001,
        deploymentMountPath: "/srv/utf",
        readinessFailure: null,
      },
    },
  ],
};

/** A storage estimate in which both sizes are sufficient on their included disk. */
const storageDemand = {
  steadyStateRequiredBytes: 40 * bytesPerGiB,
  preparationWorkspaceBytes: 10 * bytesPerGiB,
  safetyMarginPercent: 10,
  safetyMarginBytes: 5 * bytesPerGiB,
  peakPreparationRequiredBytes: 55 * bytesPerGiB,
  minimumExternalCapacityGiB: 70,
  recommendedExternalUsableBytes: 62 * bytesPerGiB,
  status: "Estimated",
  calculatedAt: "2026-08-13T12:00:00Z",
  measurementTimestamp: null,
  missingComponents: [],
  components: [],
  runtimeProfiles: ["small", "medium"].map((identifier) => ({
    runtimeProviderIdentifier: "fake-cloud",
    runtimeProfileIdentifier: identifier,
    displayName: identifier === "small" ? "Small" : "Medium",
    includedCapacityBytes: 80 * bytesPerGiB,
    usableCapacityBytes: 70 * bytesPerGiB,
    isSufficient: true,
    externalStorageRequired: false,
  })),
};

const modPackOptions = [{ primaryKey: 7, title: "Operations" }];

const profileOptions = [
  {
    primaryKey: "10000000-0000-4000-8000-0000000000c5",
    title: "Public operation",
    gameIdentifier: "arma3",
    isArchived: false,
    latestRevisionNumber: 3,
  },
];

/** The cost owners of the caller: their own (optionally frozen) and a shared one. */
function costOwners(frozenSelf: boolean) {
  return [
    {
      costOwnerId: personalOwnerId,
      displayName: "Personal budget",
      isFrozen: frozenSelf,
      isCurrentUser: true,
    },
    {
      costOwnerId: sharedOwnerId,
      displayName: "Shared budget",
      isFrozen: false,
      isCurrentUser: false,
    },
  ];
}

const startAction = {
  id: "start",
  label: "Start",
  description: "Provisions a cloud machine and starts the game server.",
  icon: "play",
  group: "Lifecycle",
  order: 10,
  enabled: true,
  unavailableReason: null,
  dangerous: false,
  confirmationText: null,
  confirmationChallenge: null,
  pending: false,
  expectedDurationSeconds: 300,
  costImpact: "StartsCost",
  costDescription: "Hourly provider cost starts once the machine exists.",
};

/** The server the create endpoint returns: off, with only an enabled start action. */
const createdServer = {
  primaryKey: createdServerId,
  title: "Journey server",
  gameIdentifier: "arma3",
  runtimeProviderIdentifier: "fake-cloud",
  runtimeProfileIdentifier: "small",
  runtimeProfileDisplayName: "Small",
  runtimeStorageStrategy: "IncludedDisk",
  externalStorageSizeGiB: null,
  estimatedHourlyCostEur: 0.05,
  estimatedMonthlyCostEur: 36.5,
  selectedConfigurationProfileId: null,
  selectedConfigurationProfileTitle: null,
  costOwnerId: personalOwnerId,
  costOwnerName: "Personal budget",
  createdById: userId,
  isLaunchDisabled: false,
  isArchived: false,
  canModify: true,
  timeStampCreated: "2026-11-16T10:00:00Z",
  timeStampUpdated: "2026-11-16T10:00:00Z",
  pendingJobCount: 0,
  deployment: null,
  actions: [startAction],
};

/** What {@link mockApi} returns: the unmocked requests and the create requests that reached the mock. */
interface ApiRecord {
  /** Requests no mock handled; each test asserts this stays empty. */
  unexpected: string[];
  /** Bodies of the POST requests to the create endpoint. */
  created: unknown[];
}

/**
 * Installs the strict API mock set of the create page.
 *
 * Unmocked requests are aborted and recorded so a test can assert none happened.
 *
 * @param page The page to mock.
 * @param culture The culture stored before the app starts.
 * @param options `frozenSelf` freezes the caller's own cost owner.
 * @returns The recorded unexpected requests and create bodies.
 */
async function mockApi(
  page: Page,
  culture: Culture,
  options: { frozenSelf?: boolean } = {},
): Promise<ApiRecord> {
  const record: ApiRecord = { unexpected: [], created: [] };
  await page.addInitScript((value) => localStorage.setItem("UtfCulture", value), culture);
  await page.route("**/appsettings.json", (route) =>
    route.fulfill({
      json: {
        Api: { BaseUrl: "/api/" },
        Urls: { TeamSpeak3: "", Discord: "" },
        Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
      },
    }),
  );
  await page.route("**/Insignia-48px.png", (route) =>
    route.fulfill({ status: 200, contentType: "image/png", body: "" }),
  );
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    if (path === "/api/users/me") {
      return route.fulfill({
        json: {
          primaryKey: userId,
          nickname: "Operator",
          isBanned: false,
          isVerified: true,
          isDeleted: false,
          isVereinMember: false,
          claims: [{ identifier: "crt:sv", value: null }],
          roles: [],
        },
      });
    }
    if (path === "/api/users/authentication-providers") {
      return route.fulfill({ json: { steam: true, discord: true } });
    }
    if (method === "GET" && path === "/api/game-servers/capabilities") {
      return route.fulfill({ json: [capability] });
    }
    if (method === "GET" && path === "/api/game-servers/creation/mod-pack-options") {
      return route.fulfill({ json: modPackOptions });
    }
    if (method === "GET" && path === "/api/configuration-profiles/options") {
      return route.fulfill({ json: profileOptions });
    }
    if (method === "GET" && path === "/api/game-servers/creation/cost-owner-options") {
      return route.fulfill({ json: costOwners(options.frozenSelf === true) });
    }
    if (method === "POST" && path === "/api/game-servers/storage-demand-estimate") {
      return route.fulfill({ json: storageDemand });
    }
    if (method === "POST" && path === "/api/game-servers") {
      record.created.push(request.postDataJSON());
      return route.fulfill({ json: createdServer });
    }
    if (method === "GET" && path === `/api/game-servers/${createdServerId}`) {
      return route.fulfill({ json: createdServer });
    }
    if (method === "GET" && path === "/api/game-servers") {
      return route.fulfill({ json: [createdServer] });
    }
    if (
      method === "GET" &&
      [
        "/api/site-image-settings",
        "/api/game-servers/notifications",
        `/api/game-servers/${createdServerId}/jobs`,
        `/api/game-servers/${createdServerId}/operations`,
      ].includes(path)
    ) {
      return route.fulfill({ json: [] });
    }
    if (await fulfillMetricsRoute(route, method, path, createdServerId)) return;
    if (await fulfillCostRoute(route, method, path, createdServerId)) return;
    record.unexpected.push(`${method} ${path}`);
    return route.abort();
  });
  return record;
}

/** Runs axe at WCAG 2.1 AA against one container and returns the violations. */
async function scan(page: Page, selector: string) {
  const results = await new AxeBuilder({ page }).include(selector).withTags(wcagTags).analyze();
  // A scan that evaluated no rule at all would pass vacuously, so require evidence that rules ran.
  expect(results.passes.length).toBeGreaterThan(0);
  return results.violations.map((violation) => ({
    id: violation.id,
    nodes: violation.nodes.map((node) => node.target.join(" ")),
  }));
}

/** The localized copy the states below look for, copied from the locale resources of each culture. */
const copy = {
  en: {
    title: "Create server",
    costTitle: "Estimated cost",
    perHour: /per hour while it runs/,
    advanced: "Advanced options",
    about: "What this game server can do",
    invalid: "Check the highlighted field before creating.",
    ownerFrozenLine:
      "Personal budget is frozen and cannot take on a new server. Choose another cost owner.",
    ownerFrozenReason:
      "Personal budget is frozen, so it cannot pay for a new server. Choose another cost owner.",
    startNow: /^Start it now/,
    submitStart: "Create server and start",
    notStarted: "Journey server was created and is still off.",
  },
  de: {
    title: "Server erstellen",
    costTitle: "Geschätzte Kosten",
    perHour: /pro Stunde, solange er läuft/,
    advanced: "Erweiterte Optionen",
    about: "Was dieser Spielserver kann",
    invalid: "Prüfe das markierte Feld, bevor du erstellst.",
    ownerFrozenLine:
      "Personal budget ist gesperrt und kann keinen neuen Server übernehmen. Wähle einen anderen Kostenträger.",
    ownerFrozenReason:
      "Personal budget ist gesperrt und kann keinen neuen Server bezahlen. Wähle einen anderen Kostenträger.",
    startNow: /^Jetzt starten/,
    submitStart: "Server erstellen und starten",
    notStarted: "Journey server wurde erstellt und ist noch aus.",
  },
} as const;

const viewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

/**
 * Opens the create page at a viewport and waits until the guided default view is ready.
 *
 * @param page The page to drive.
 * @param culture The culture whose copy to wait for.
 * @param viewport The viewport to apply before navigating.
 */
async function openCreatePage(
  page: Page,
  culture: Culture,
  viewport: { width: number; height: number },
) {
  await page.setViewportSize(viewport);
  await page.goto("command/servers/create");
  await expect(page.getByRole("heading", { level: 1, name: copy[culture].title })).toBeVisible();
  await expect(
    page.getByRole("heading", { level: 2, name: copy[culture].costTitle }),
  ).toBeVisible();
  await expect(page.locator("input[name=title]")).toBeVisible();
  await expect(page.locator(".create-summary__hourly")).toHaveText(copy[culture].perHour);
}

/** Asserts the page has no horizontal scroll, allowing one CSS pixel of subpixel rounding. */
async function expectNoHorizontalOverflow(page: Page) {
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 1);
}

/** Asserts the sticky summary stays within a third of the viewport height (plus one pixel of rounding). */
async function expectBarWithinThird(page: Page, viewportHeight: number) {
  const box = await page.locator(".create-summary").boundingBox();
  expect(box).not.toBeNull();
  expect(box!.height).toBeLessThanOrEqual(viewportHeight / 3 + 1);
}

/**
 * Waits until the page stops scrolling. The app scrolls smoothly, so the position right after a focus change is
 * still moving toward the focused control.
 */
async function settleScroll(page: Page) {
  let previous = -1;
  await expect
    .poll(async () => {
      const current = await page.evaluate(() => window.scrollY);
      const settled = current === previous;
      previous = current;
      return settled;
    })
    .toBe(true);
}

/** Opens Advanced options with the keyboard: focus the toggle button and press Enter. */
async function openAdvancedWithKeyboard(page: Page, culture: Culture) {
  const toggle = page.getByRole("button", { name: copy[culture].advanced }).first();
  await toggle.focus();
  await page.keyboard.press("Enter");
  await expect(toggle).toHaveAttribute("aria-expanded", "true");
}

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`create page at ${viewport.width}x${viewport.height} (${culture})`, () => {
      test("default state", async ({ page }) => {
        const api = await mockApi(page, culture);
        await openCreatePage(page, culture, viewport);

        await expectNoHorizontalOverflow(page);
        await expectBarWithinThird(page, viewport.height);
        expect(await scan(page, "form.create-page")).toEqual([]);
        expect(api.unexpected).toEqual([]);
      });

      test("blocked owner state", async ({ page }) => {
        const api = await mockApi(page, culture, { frozenSelf: true });
        await openCreatePage(page, culture, viewport);

        const submit = page.locator(".create-summary button[type=submit]");
        await expect(page.getByText(copy[culture].ownerFrozenLine)).toBeVisible();
        await expect(page.getByText(copy[culture].ownerFrozenReason)).toBeVisible();
        await expect(submit).toHaveAttribute("aria-disabled", "true");
        await expectNoHorizontalOverflow(page);
        await expectBarWithinThird(page, viewport.height);
        expect(await scan(page, "form.create-page")).toEqual([]);

        await page.locator("input[name=title]").fill("Blocked server");
        await page.locator("input[name=title]").press("Enter");
        // Give a wrongly submitted form time to reach the mock before asserting that nothing was sent.
        await page.waitForTimeout(300);
        expect(api.created).toEqual([]);
        expect(api.unexpected).toEqual([]);
      });

      test("advanced options open", async ({ page }) => {
        const api = await mockApi(page, culture);
        await openCreatePage(page, culture, viewport);

        await openAdvancedWithKeyboard(page, culture);
        await expectNoHorizontalOverflow(page);
        expect(await scan(page, "form.create-page")).toEqual([]);
        expect(api.unexpected).toEqual([]);
      });

      test("invalid submit state", async ({ page }) => {
        const api = await mockApi(page, culture);
        await openCreatePage(page, culture, viewport);

        await page.locator(".create-summary button[type=submit]").click();
        await expect(page.locator("input[name=title]")).toBeFocused();
        const alert = page.locator(".create-summary__alert");
        await expect(alert).toBeVisible();
        await expect(alert).toHaveText(copy[culture].invalid);
        await expectNoHorizontalOverflow(page);
        await expectBarWithinThird(page, viewport.height);
        expect(await scan(page, "form.create-page")).toEqual([]);
        expect(api.created).toEqual([]);
        expect(api.unexpected).toEqual([]);
      });
    });
  }

  test(`focus is not hidden behind the bottom bar at 320x568 (${culture})`, async ({ page }) => {
    const api = await mockApi(page, culture);
    await openCreatePage(page, culture, viewports[0]);

    await openAdvancedWithKeyboard(page, culture);
    const last = page.getByRole("button", { name: copy[culture].about });
    // Tab through the open options like a keyboard user until the last control is focused.
    for (let step = 0; step < 60; step += 1) {
      if (await last.evaluate((element) => element === document.activeElement)) break;
      await page.keyboard.press("Tab");
    }
    await expect(last).toBeFocused();
    // The browser scrolls the focused control toward the viewport; only then is the settled position meaningful.
    await expect.poll(async () => (await last.boundingBox())!.y).toBeLessThan(viewports[0].height);
    await settleScroll(page);
    const barBox = await page.locator(".create-summary").boundingBox();
    expect(barBox).not.toBeNull();
    const focusedBox = await last.boundingBox();
    expect(focusedBox).not.toBeNull();
    expect(focusedBox!.y + focusedBox!.height).toBeLessThanOrEqual(barBox!.y + 1);

    // A browser may center a focused control, which hides a missing scroll-padding. Scrolling with the nearest
    // alignment from the top is the case that depends on scroll-padding-block-end keeping the bar clear.
    await page.evaluate(() => window.scrollTo({ top: 0, behavior: "instant" }));
    await last.evaluate((element) =>
      element.scrollIntoView({ block: "nearest", behavior: "instant" }),
    );
    const nearestBox = await last.boundingBox();
    expect(nearestBox).not.toBeNull();
    expect(nearestBox!.y + nearestBox!.height).toBeLessThanOrEqual(barBox!.y + 1);
    expect(api.unexpected).toEqual([]);
  });
}

test.describe("create then start journey", () => {
  test("Start it now opens the shared start confirmation over the new workspace", async ({
    page,
  }) => {
    const api = await mockApi(page, "en");
    await openCreatePage(page, "en", viewports[1]);

    await page.locator("input[name=title]").fill("Journey server");
    await page.getByRole("radio", { name: copy.en.startNow }).check();
    await page.getByRole("button", { name: copy.en.submitStart }).click();

    await expect(page).toHaveURL(new RegExp(`/command/servers/${createdServerId}$`));
    const dialog = page.locator("dialog[open]");
    await expect(dialog).toBeVisible();
    await expect(dialog.locator("[data-dialog-dismiss]")).toBeFocused();
    expect(api.created).toHaveLength(1);

    await dialog.locator("[data-dialog-dismiss]").click();
    await expect(page.locator("dialog[open]")).toHaveCount(0);
    await expect(page.getByText(copy.en.notStarted)).toBeVisible();
    expect(api.unexpected).toEqual([]);
  });
});

test.describe("guided create page layout", () => {
  test("default create page fits 320 by 568 in English", async ({ page }) => {
    const api = await mockApi(page, "en");
    await page.setViewportSize({ width: 320, height: 568 });

    await page.goto("command/servers/create");
    await expect(page.getByRole("heading", { level: 1, name: "Create server" })).toBeVisible();
    await expect(page.getByRole("heading", { level: 2, name: "Estimated cost" })).toBeVisible();
    await expect(page.getByText(/per hour while it runs/)).toBeVisible();

    const metrics = await page.evaluate(() => ({
      scrollWidth: document.documentElement.scrollWidth,
      clientWidth: document.documentElement.clientWidth,
    }));
    expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 1);
    const summary = await page.locator(".create-summary").boundingBox();
    expect(summary).not.toBeNull();
    expect(summary!.height).toBeLessThanOrEqual(568 / 3 + 1);
    expect(await scan(page, "form.create-page")).toEqual([]);
    expect(api.unexpected).toEqual([]);
  });
});
