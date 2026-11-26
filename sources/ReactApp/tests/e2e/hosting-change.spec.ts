import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Locator, type Page } from "@playwright/test";
import { mockFleetStream } from "./support/fleetStream";
import { fulfillCostRoute } from "./support/costMocks";
import { fulfillMetricsRoute } from "./support/metricsMocks";
import { seedSettingsPanels } from "./support/settingsPanels";

type Culture = "en" | "de";

const serverId = "10000000-0000-4000-8000-000000000c65";
const serverTitle = "Change test server";

/** The value typed into the credential field; it must never appear in the review dialog (T-65-38). */
const typedSecret = "Tr0ub4dor-secret-value";

/**
 * Layout boxes come back with fractional coordinates; an edge that ends less than one CSS pixel past the viewport
 * is a rounding artifact, not a clipped control (Phase 62 rule).
 */
const subpixelTolerance = 1;

const wcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

/** The two viewports of the phase gate: the smallest supported phone and a common one. */
const viewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

/** The visible copy each test reads, per culture; every line is the Copywriting Contract text of that culture. */
interface Copy {
  navigation: string;
  settings: string;
  files: string;
  barLabel: string;
  bar(count: number): string;
  review: string;
  keepEditing: string;
  apply(count: number): string;
  retry(count: number): string;
  partial(applied: number, total: number): string;
  failedState: string;
  failedReason: string;
  effectOff: string;
  toast(count: number): string;
  banner: string;
  waiting: RegExp;
  added: string;
  removed: string;
  secretChanged: string;
  modPackLabel: string;
  secretLabel: string;
  advanced: string;
  leaveTitle: string;
  leaveActions: string[];
}

const copy: Record<Culture, Copy> = {
  en: {
    navigation: "Server sections",
    settings: "Settings",
    files: "Files",
    barLabel: "Pending changes",
    bar: (count) =>
      count === 1
        ? "1 change staged. Nothing is saved until you apply it."
        : `${count} changes staged. Nothing is saved until you apply them.`,
    review: "Review changes",
    keepEditing: "Keep editing",
    apply: (count) => (count === 1 ? "Apply 1 change" : `Apply ${count} changes`),
    retry: (count) => (count === 1 ? "Try 1 change again" : `Try ${count} changes again`),
    partial: (applied, total) => `${applied} of ${total} changes applied. The rest stay staged.`,
    failedState: "Not applied",
    failedReason: "The change could not be applied. Nothing else was affected.",
    effectOff: `These changes take effect the next time ${serverTitle} starts.`,
    toast: (count) =>
      count === 1
        ? `1 change applied. It takes effect the next time ${serverTitle} starts.`
        : `${count} changes applied. They take effect the next time ${serverTitle} starts.`,
    banner: `Saved changes apply the next time ${serverTitle} starts.`,
    waiting: /Waiting: Mod pack, Configuration,? and Credentials/,
    added: "Added (1)",
    removed: "Removed (1)",
    secretChanged: "Changed. The value is not shown.",
    modPackLabel: "Mod pack",
    secretLabel: "New value for Join password",
    advanced: "Advanced settings",
    leaveTitle: "Leave without applying?",
    leaveActions: ["Discard and leave", "Stay here", "Review changes"],
  },
  de: {
    navigation: "Serverbereiche",
    settings: "Einstellungen",
    files: "Dateien",
    barLabel: "Ausstehende Änderungen",
    bar: (count) =>
      count === 1
        ? "1 Änderung vorgemerkt. Nichts wird gespeichert, bis du sie anwendest."
        : `${count} Änderungen vorgemerkt. Nichts wird gespeichert, bis du sie anwendest.`,
    review: "Änderungen prüfen",
    keepEditing: "Weiter bearbeiten",
    apply: (count) => (count === 1 ? "1 Änderung anwenden" : `${count} Änderungen anwenden`),
    retry: (count) =>
      count === 1 ? "1 Änderung erneut versuchen" : `${count} Änderungen erneut versuchen`,
    partial: (applied, total) =>
      `${applied} von ${total} Änderungen angewendet. Die übrigen bleiben vorgemerkt.`,
    failedState: "Nicht angewendet",
    failedReason: "Die Änderung konnte nicht angewendet werden. Alles andere blieb unberührt.",
    effectOff: `Diese Änderungen gelten, sobald ${serverTitle} das nächste Mal startet.`,
    toast: (count) =>
      count === 1
        ? `1 Änderung angewendet. Sie gilt, sobald ${serverTitle} das nächste Mal startet.`
        : `${count} Änderungen angewendet. Sie gelten, sobald ${serverTitle} das nächste Mal startet.`,
    banner: `Gespeicherte Änderungen gelten, sobald ${serverTitle} das nächste Mal startet.`,
    waiting: /Wartet: Mod-Paket, Konfiguration und Zugangsdaten/,
    added: "Hinzugefügt (1)",
    removed: "Entfernt (1)",
    secretChanged: "Geändert. Der Wert wird nicht angezeigt.",
    modPackLabel: "Mod-Paket",
    secretLabel: "Neuer Wert für Join password",
    advanced: "Erweiterte Einstellungen",
    leaveTitle: "Verlassen, ohne anzuwenden?",
    leaveActions: ["Verwerfen und verlassen", "Hier bleiben", "Änderungen prüfen"],
  },
};

const modPacks = [
  { primaryKey: 1, title: "Core", isAvailable: true, unavailableReason: null },
  { primaryKey: 2, title: "Extended", isAvailable: true, unavailableReason: null },
  {
    primaryKey: 3,
    title: "Legacy",
    isAvailable: false,
    unavailableReason: "Built for an older game version.",
  },
];

/** The flattened contents of each pack: Core holds A and B, Extended holds B and C. */
const packContents: Record<number, { key: string; title: string }[]> = {
  1: [
    { key: "mod-a", title: "Mod A" },
    { key: "mod-b", title: "Mod B" },
  ],
  2: [
    { key: "mod-b", title: "Mod B" },
    { key: "mod-c", title: "Mod C" },
  ],
  3: [],
};

/** Builds one configuration entry of the mocked game; every field the API sends is present. */
function entry(overrides: { key: string; displayName: string } & Record<string, unknown>) {
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

/** Common entries first, then three Advanced entries so the last Advanced field sits low on the page. */
const baseEntries = [
  entry({
    key: "server.cfg@hostname",
    displayName: "Hostname",
    value: "Old name",
    order: 10,
  }),
  entry({
    key: "server.cfg@maxPlayers",
    displayName: "Maximum players",
    kind: "Integer",
    minimum: 1,
    maximum: 128,
    value: 64,
    order: 20,
  }),
  entry({
    key: "basic.cfg@MaxMsgSend",
    displayName: "Message send limit",
    kind: "Integer",
    targetPath: "server/basic.cfg",
    group: "Network",
    tier: "Advanced",
    value: 128,
    order: 10,
  }),
  entry({
    key: "basic.cfg@MaxSizeGuaranteed",
    displayName: "Guaranteed size limit",
    kind: "Integer",
    targetPath: "server/basic.cfg",
    group: "Network",
    tier: "Advanced",
    value: 512,
    order: 20,
  }),
  entry({
    key: "basic.cfg@MaxSizeNonguaranteed",
    displayName: "Non-guaranteed size limit",
    kind: "Integer",
    targetPath: "server/basic.cfg",
    group: "Network",
    tier: "Advanced",
    value: 256,
    order: 30,
  }),
];

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

/** One recorded mutating request: the route below the server and the parsed body. */
interface RecordedRequest {
  method: string;
  route: string;
  body: unknown;
}

/** The mutable state behind the mocked API of one test. */
interface ChangeState {
  /** The saved mod pack of the server. */
  savedPackId: number;
  /** The saved value of every configuration entry by key. */
  values: Map<string, unknown>;
  /** The saved version of the one credential. */
  secretVersion: number;
  /** The areas the API reports as waiting for the next start. */
  pendingAreas: Set<"ModPack" | "Configuration" | "Credentials">;
  /** Every PUT and POST the page sent, in order. */
  requests: RecordedRequest[];
  /** When true the next configuration update answers 500 once, then the flag clears itself. */
  failConfigurationOnce: boolean;
}

/** Creates the state of a server with the Core pack saved and no change waiting. */
function createState(): ChangeState {
  return {
    savedPackId: 1,
    values: new Map(baseEntries.map((item) => [item.key, item.value])),
    secretVersion: 1,
    pendingAreas: new Set(),
    requests: [],
    failConfigurationOnce: false,
  };
}

/** The recorded writes as `METHOD /route` lines, which is the order the apply run sent them in. */
function writes(state: ChangeState): string[] {
  return state.requests.map((request) => `${request.method} ${request.route}`);
}

/** Builds the server projection from the current state: an Arma 3 server with no deployment. */
function serverBody(state: ChangeState) {
  const areaOrder = ["ModPack", "Configuration", "Credentials"] as const;
  const pack = modPacks.find((item) => item.primaryKey === state.savedPackId);
  return {
    primaryKey: serverId,
    title: serverTitle,
    gameIdentifier: "arma3",
    runtimeProviderIdentifier: "fake-cloud",
    runtimeProfileIdentifier: "medium",
    runtimeProfileDisplayName: "Medium",
    runtimeStorageStrategy: "IncludedDisk",
    externalStorageSizeGiB: null,
    estimatedHourlyCostEur: 0.2,
    estimatedMonthlyCostEur: 146,
    selectedModPackDefinitionId: state.savedPackId,
    selectedModPackDefinitionTitle: pack?.title ?? null,
    pinnedModPackRevisionId: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    pinnedConfigurationProfileRevisionId: null,
    costOwnerId: serverId,
    costOwnerName: "Operations",
    createdById: serverId,
    costOwnerUserId: null,
    isLaunchDisabled: false,
    changesPendingStartAreas: areaOrder.filter((area) => state.pendingAreas.has(area)),
    hasChangesPendingStart: state.pendingAreas.size > 0,
    isArchived: false,
    canModify: true,
    canManageSecrets: true,
    canRevealSecrets: true,
    timeStampCreated: "2026-11-16T10:00:00Z",
    timeStampUpdated: "2026-11-16T10:00:00Z",
    deployment: null,
    pendingJobCount: 0,
    actions: [],
  };
}

/** Builds the configuration body from the saved values. */
function configurationBody(state: ChangeState) {
  return {
    serverId,
    profileId: null,
    profileRevisionId: null,
    profileRevisionNumber: null,
    isComplete: true,
    entries: baseEntries.map((item) => ({ ...item, value: state.values.get(item.key) ?? null })),
  };
}

/** Builds the credential list: one user-provided, non-generated credential. */
function secretsBody(state: ChangeState) {
  return [
    {
      key: "join-password",
      displayName: "Join password",
      requiredForStart: false,
      generated: false,
      revealable: true,
      externalProvider: false,
      isConfigured: true,
      version: state.secretVersion,
      origin: "UserProvided",
      timeStampCreated: "2026-11-16T10:00:00Z",
    },
  ];
}

/**
 * Installs the strict API mock set and returns the requests that no mock handled.
 *
 * Reads answer from the mutable state; writes are recorded, change the state like the real API and flip the
 * pending-start signal for the area they touched. Unmocked requests are aborted and recorded so a test can assert
 * none happened (T-65-39).
 */
async function mockApi(page: Page, culture: Culture, state: ChangeState): Promise<string[]> {
  const unexpected: string[] = [];
  const base = `/api/game-servers/${serverId}`;
  await page.addInitScript((value) => localStorage.setItem("UtfCulture", value), culture);
  await seedSettingsPanels(page);
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
    const method = request.method();
    const path = new URL(request.url()).pathname;
    if (path === "/api/users/me") {
      return route.fulfill({
        json: {
          primaryKey: serverId,
          nickname: "Operator",
          isBanned: false,
          isVerified: true,
          isDeleted: false,
          isVereinMember: false,
          claims: [{ identifier: "sv", value: serverId }],
          roles: [],
        },
      });
    }
    if (path === "/api/users/authentication-providers") {
      return route.fulfill({ json: { steam: true, discord: true } });
    }
    if (method === "GET") {
      if (path === base) return route.fulfill({ json: serverBody(state) });
      if (path === "/api/game-servers") return route.fulfill({ json: [serverBody(state)] });
      if (path === "/api/game-servers/capabilities") return route.fulfill({ json: [capability] });
      if (path === `${base}/mod-pack-options`) return route.fulfill({ json: modPacks });
      const contents = new RegExp(`^${base}/mod-pack-options/(\\d+)/contents$`).exec(path);
      if (contents) {
        const packId = Number(contents[1]);
        const items = packContents[packId] ?? [];
        return route.fulfill({
          json: {
            modPackDefinitionId: packId,
            isAvailable: items.length > 0,
            unavailableReason: null,
            total: items.length,
            items,
          },
        });
      }
      if (path === `${base}/configuration`)
        return route.fulfill({ json: configurationBody(state) });
      if (path === `${base}/secrets`) return route.fulfill({ json: secretsBody(state) });
      if (path === `${base}/storage-demand`) {
        return route.fulfill({
          json: {
            placementEstimate: null,
            components: [],
            runtimeProfiles: [],
            missingComponents: [],
          },
        });
      }
      if (
        [
          "/api/site-image-settings",
          "/api/game-servers/notifications",
          `${base}/jobs`,
          `${base}/operations`,
          `${base}/configuration/profile-options`,
          `${base}/preservation-selections`,
          `${base}/files/persistent`,
          `${base}/files/live`,
        ].includes(path)
      ) {
        return route.fulfill({ json: [] });
      }
    }
    if (method === "PUT" && path.startsWith(`${base}/`)) {
      const routeName = path.slice(base.length);
      const body: unknown = request.postDataJSON();
      state.requests.push({ method, route: routeName, body });
      if (routeName === "/mod-pack") {
        state.savedPackId = (body as { modPackDefinitionId: number }).modPackDefinitionId;
        state.pendingAreas.add("ModPack");
        return route.fulfill({ status: 204 });
      }
      if (routeName === "/configuration") {
        if (state.failConfigurationOnce) {
          state.failConfigurationOnce = false;
          return route.fulfill({ status: 500, json: { title: "Internal error" } });
        }
        for (const change of (body as { values: { key: string; value: unknown }[] }).values) {
          state.values.set(change.key, change.value);
        }
        state.pendingAreas.add("Configuration");
        return route.fulfill({ json: configurationBody(state) });
      }
      if (routeName === "/secrets") {
        state.secretVersion += 1;
        state.pendingAreas.add("Credentials");
        return route.fulfill({ status: 204 });
      }
    }
    if (await fulfillMetricsRoute(route, method, path, serverId)) return;
    if (await fulfillCostRoute(route, method, path, serverId)) return;
    unexpected.push(`${method} ${path}`);
    return route.abort();
  });
  return unexpected;
}

/** The mod pack chooser of the Mods panel. */
function modPackSelect(page: Page, culture: Culture): Locator {
  return page.getByRole("combobox", { name: copy[culture].modPackLabel, exact: true });
}

/** The maximum players field of the Common settings. */
function maxPlayersField(page: Page): Locator {
  return page.getByLabel("Maximum players", { exact: true });
}

/** The new-value field of the one credential. */
function secretField(page: Page, culture: Culture): Locator {
  return page.getByLabel(copy[culture].secretLabel, { exact: true });
}

/** The sticky pending-changes bar. */
function pendingBar(page: Page, culture: Culture): Locator {
  return page.getByRole("region", { name: copy[culture].barLabel });
}

/** The open review dialog. */
function reviewDialog(page: Page, culture: Culture): Locator {
  return page.getByRole("dialog", { name: copy[culture].review });
}

/** The five section links, in document order. */
function sectionLink(page: Page, culture: Culture, name: string): Locator {
  return page
    .getByRole("navigation", { name: copy[culture].navigation })
    .getByRole("link", { name });
}

/** Opens a section of the mocked server and waits until the shell rendered. */
async function openWorkspace(page: Page, search = "") {
  await page.goto(`command/servers/${serverId}${search}`);
  await expect(page.getByRole("heading", { level: 1, name: serverTitle })).toBeVisible();
}

/** Opens Settings and waits until the mods, configuration and credentials panels are ready. */
async function openSettings(page: Page, culture: Culture) {
  await openWorkspace(page, "?tab=settings");
  await expect(
    page.getByRole("heading", { level: 2, name: copy[culture].settings, exact: true }),
  ).toBeVisible();
  await expect(modPackSelect(page, culture)).toBeVisible();
  await expect(maxPlayersField(page)).toBeVisible();
  await expect(secretField(page, culture)).toBeVisible();
}

/**
 * Stages a new maximum players value, which is one Common setting change.
 *
 * The field is scrolled into view first, like a member would before typing: the sticky bar only reaches the bottom
 * of the viewport once the section has scrolled into it, so a test that types into an offscreen field would measure a
 * page position no member is ever at.
 */
async function stageMaxPlayers(page: Page) {
  const field = maxPlayersField(page);
  await field.scrollIntoViewIfNeeded();
  await field.fill("80");
}

/** One measured box in page coordinates, so a scroll between two measurements does not move it. */
interface PageBox {
  selector: string;
  index: number;
  top: number;
  height: number;
}

/**
 * Measures every configuration field and every Settings panel in page coordinates.
 *
 * Page coordinates add the scroll offset, so scrolling a field into view between two measurements does not count as a
 * layout shift; only a real change of position or size does.
 */
async function measureSettingsLayout(page: Page): Promise<PageBox[]> {
  return page.evaluate(() => {
    const selectors = [".configuration-field", "section.workspace-panel"];
    const boxes: PageBox[] = [];
    for (const selector of selectors) {
      document.querySelectorAll(selector).forEach((element, index) => {
        const rect = element.getBoundingClientRect();
        // A collapsed (hidden) element has no box; its top would only echo the scroll offset.
        const hasBox = rect.width > 0 || rect.height > 0;
        boxes.push({
          selector,
          index,
          top: hasBox ? rect.top + window.scrollY : 0,
          height: rect.height,
        });
      });
    }
    return boxes;
  });
}

/** Opens the review dialog from the bar and waits until Apply is available. */
async function openReview(page: Page, culture: Culture, count: number): Promise<Locator> {
  await pendingBar(page, culture).getByRole("button", { name: copy[culture].review }).click();
  const dialog = reviewDialog(page, culture);
  await expect(dialog).toBeVisible();
  const apply = dialog.getByRole("button", { name: copy[culture].apply(count) });
  await expect(apply).toBeVisible();
  await expect(apply).not.toHaveAttribute("aria-disabled", "true");
  return dialog;
}

/** Asserts that the document cannot scroll horizontally, allowing one pixel of rounding. */
async function expectNoHorizontalScroll(page: Page) {
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + subpixelTolerance);
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

/** Dispatches a cancelable beforeunload event and reports whether a handler prevented it. */
async function beforeUnloadPrevented(page: Page): Promise<boolean> {
  return page.evaluate(() => {
    const event = new Event("beforeunload", { cancelable: true });
    window.dispatchEvent(event);
    return event.defaultPrevented;
  });
}

/** Asserts that a box lies fully inside the viewport, allowing the subpixel tolerance. */
function expectInsideViewport(
  box: { x: number; y: number; width: number; height: number } | null,
  viewport: { width: number; height: number },
  what: string,
) {
  expect(box, `${what} has a box`).not.toBeNull();
  expect(box!.x, `${what} left edge`).toBeGreaterThanOrEqual(-subpixelTolerance);
  expect(box!.y, `${what} top edge`).toBeGreaterThanOrEqual(-subpixelTolerance);
  expect(box!.x + box!.width, `${what} right edge`).toBeLessThanOrEqual(
    viewport.width + subpixelTolerance,
  );
  expect(box!.y + box!.height, `${what} bottom edge`).toBeLessThanOrEqual(
    viewport.height + subpixelTolerance,
  );
}

test.describe("change journey (en, 390x844)", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test("stages a pack, a setting and a credential, reviews, applies and shows the pending-start banner", async ({
    page,
  }) => {
    const culture = "en";
    const state = createState();
    const unexpected = await mockApi(page, culture, state);
    await mockFleetStream(page);

    await openSettings(page, culture);
    // Nothing is waiting for a start yet, so the banner is absent before the apply.
    await expect(page.getByRole("status").filter({ hasText: copy[culture].banner })).toHaveCount(0);

    await modPackSelect(page, culture).selectOption({ value: "2" });
    await stageMaxPlayers(page);
    await secretField(page, culture).fill(typedSecret);

    const bar = pendingBar(page, culture);
    await expect(bar).toBeVisible();
    await expect(bar.getByText(copy[culture].bar(3), { exact: true })).toBeVisible();
    expect(state.requests).toEqual([]);

    // A staged draft on the Settings section has no axe violation.
    expect(await scan(page, ".workspace-section")).toEqual([]);

    const dialog = await openReview(page, culture, 3);
    await expect(dialog.getByText(copy[culture].effectOff, { exact: true })).toBeVisible();
    // The mod contents diff is folded behind an inline disclosure until the member opens it.
    await dialog
      .getByRole("button", {
        name: culture === "en" ? "Show 2 mod changes" : "2 Mod-Änderungen anzeigen",
      })
      .click();
    await expect(dialog.getByText(copy[culture].added, { exact: true })).toBeVisible();
    await expect(dialog.getByText(copy[culture].removed, { exact: true })).toBeVisible();
    await expect(dialog.getByText("Mod C", { exact: true })).toBeVisible();
    await expect(dialog.getByText("Mod A", { exact: true })).toBeVisible();
    await expect(dialog.getByText(copy[culture].secretChanged, { exact: true })).toBeVisible();
    // The typed credential value is never part of the dialog (T-65-38).
    expect(await dialog.innerHTML()).not.toContain(typedSecret);
    expect(await dialog.innerText()).not.toContain(typedSecret);

    // The open review dialog has no axe violation.
    expect(await scan(page, "dialog[open]")).toEqual([]);

    await dialog.getByRole("button", { name: copy[culture].apply(3) }).click();

    await expect(page.getByText(copy[culture].toast(3), { exact: true })).toBeVisible();
    await expect(dialog).toBeHidden();
    await expect(bar).toHaveCount(0);

    // The apply sends the mod pack, the configuration and the credential, in that order and with these bodies.
    expect(writes(state)).toEqual(["PUT /mod-pack", "PUT /configuration", "PUT /secrets"]);
    expect(state.requests.map((request) => request.body)).toEqual([
      { modPackDefinitionId: 2 },
      { values: [{ key: "server.cfg@maxPlayers", value: 80 }], resetKeys: [] },
      { key: "join-password", value: typedSecret, origin: "UserProvided" },
    ]);

    // After the apply the page refetches the server, whose signal now reports every waiting area.
    const banner = page.getByRole("status").filter({ hasText: copy[culture].banner });
    await expect(banner).toBeVisible();
    await expect(banner).toContainText(copy[culture].waiting);
    expect(unexpected).toEqual([]);
  });
});

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`change layout (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test("keeps the bar within a third of the viewport and Apply reachable without sideways scroll", async ({
        page,
      }) => {
        const unexpected = await mockApi(page, culture, createState());
        await mockFleetStream(page);

        await openSettings(page, culture);
        await stageMaxPlayers(page);
        const bar = pendingBar(page, culture);
        await expect(bar.getByText(copy[culture].bar(1), { exact: true })).toBeVisible();
        await expectNoHorizontalScroll(page);
        const barBox = await bar.boundingBox();
        expectInsideViewport(barBox, viewport, "pending bar");
        expect(barBox!.height).toBeLessThanOrEqual(viewport.height / 3 + subpixelTolerance);
        test.info().annotations.push({
          type: "bar-height",
          description: `${barBox!.height}px of ${viewport.height}px`,
        });

        const dialog = await openReview(page, culture, 1);
        await expectNoHorizontalScroll(page);
        expectInsideViewport(await dialog.boundingBox(), viewport, "review dialog");
        const apply = dialog.getByRole("button", { name: copy[culture].apply(1) });
        await expect(apply).toBeInViewport();
        expectInsideViewport(await apply.boundingBox(), viewport, "Apply");
        expect(unexpected).toEqual([]);
      });
    });
  }

  for (const shiftViewport of [
    { width: 390, height: 844 },
    { width: 1280, height: 800 },
  ] as const) {
    test.describe(`change layout stability (${culture}, ${shiftViewport.width}x${shiftViewport.height})`, () => {
      test.use({ viewport: shiftViewport });

      test("staging a configuration value shifts no field and no later panel (D-11)", async ({
        page,
      }) => {
        const unexpected = await mockApi(page, culture, createState());
        await mockFleetStream(page);

        await openSettings(page, culture);
        // The placement estimate loads on its own; measuring while it still shows the loading line would record
        // that late layout change as if staging had caused it.
        await expect(
          page.getByText(/Calculating storage demand|Speicherbedarf wird berechnet/),
        ).toHaveCount(0);
        const before = await measureSettingsLayout(page);
        expect(
          before.filter((box) => box.selector === ".configuration-field").length,
        ).toBeGreaterThan(2);

        await stageMaxPlayers(page);
        await expect(
          pendingBar(page, culture).getByText(copy[culture].bar(1), { exact: true }),
        ).toBeVisible();
        const after = await measureSettingsLayout(page);

        expect(after).toHaveLength(before.length);
        for (const [position, box] of before.entries()) {
          const moved = after[position]!;
          expect(
            Math.abs(moved.top - box.top),
            `${box.selector} #${box.index} top`,
          ).toBeLessThanOrEqual(0.5);
          expect(
            Math.abs(moved.height - box.height),
            `${box.selector} #${box.index} height`,
          ).toBeLessThanOrEqual(0.5);
        }
        expect(unexpected).toEqual([]);
      });
    });
  }

  test.describe(`mod pack select surface (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("keeps the shared select end padding (D-06)", async ({ page }) => {
      const unexpected = await mockApi(page, culture, createState());
      await mockFleetStream(page);

      await openSettings(page, culture);
      const paddings = await modPackSelect(page, culture).evaluate((select) => {
        const probe = document.createElement("div");
        probe.style.width = "var(--utf-space-xl)";
        document.body.append(probe);
        const token = probe.getBoundingClientRect().width;
        probe.remove();
        return { actual: Number.parseFloat(getComputedStyle(select).paddingInlineEnd), token };
      });
      expect(paddings.token).toBeGreaterThan(0);
      expect(paddings.actual).toBeCloseTo(paddings.token, 1);
      expect(unexpected).toEqual([]);
    });
  });

  test.describe(`change accessibility (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("Settings with a staged draft and the review dialog have no axe violation", async ({
      page,
    }) => {
      const unexpected = await mockApi(page, culture, createState());
      await mockFleetStream(page);

      await openSettings(page, culture);
      await modPackSelect(page, culture).selectOption({ value: "2" });
      await stageMaxPlayers(page);
      await expect(
        pendingBar(page, culture).getByText(copy[culture].bar(2), { exact: true }),
      ).toBeVisible();
      expect(await scan(page, ".workspace-section")).toEqual([]);

      await openReview(page, culture, 2);
      expect(await scan(page, "dialog[open]")).toEqual([]);
      expect(unexpected).toEqual([]);
    });
  });

  test.describe(`change focus (${culture}, 320x568)`, () => {
    test.use({ viewport: { width: 320, height: 568 } });

    test("keeps the last Advanced field clear of the sticky bar when it takes focus", async ({
      page,
    }) => {
      const unexpected = await mockApi(page, culture, createState());
      await mockFleetStream(page);

      await openSettings(page, culture);
      await stageMaxPlayers(page);
      const bar = pendingBar(page, culture);
      await expect(bar).toBeVisible();

      await page.getByRole("button", { name: copy[culture].advanced }).click();
      const previous = page.getByLabel("Guaranteed size limit", { exact: true });
      const last = page.getByLabel("Non-guaranteed size limit", { exact: true });
      await expect(last).toBeVisible();
      await previous.focus();
      await page.keyboard.press("Tab");
      await expect(last).toBeFocused();

      // The comparison below is only meaningful while the bar sits at the bottom of the viewport.
      expectInsideViewport(await bar.boundingBox(), { width: 320, height: 568 }, "pending bar");
      // The browser scrolls the focused field into view above the reserve; the scroll can still be settling.
      await expect
        .poll(async () => {
          const field = await last.boundingBox();
          const barBox = await bar.boundingBox();
          if (!field || !barBox) return Number.POSITIVE_INFINITY;
          return field.y + field.height - barBox.y;
        })
        .toBeLessThanOrEqual(subpixelTolerance);
      const field = await last.boundingBox();
      expect(field).not.toBeNull();
      expect(field!.y).toBeGreaterThanOrEqual(-subpixelTolerance);
      await expectNoHorizontalScroll(page);
      expect(unexpected).toEqual([]);
    });
  });

  test.describe(`partial apply (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("keeps the failed change staged, reports the counts and retries only that change", async ({
      page,
    }) => {
      const state = createState();
      state.failConfigurationOnce = true;
      const unexpected = await mockApi(page, culture, state);
      await mockFleetStream(page);

      await openSettings(page, culture);
      await modPackSelect(page, culture).selectOption({ value: "2" });
      await stageMaxPlayers(page);
      await expect(
        pendingBar(page, culture).getByText(copy[culture].bar(2), { exact: true }),
      ).toBeVisible();

      const dialog = await openReview(page, culture, 2);
      await dialog.getByRole("button", { name: copy[culture].apply(2) }).click();

      // The mod pack was applied, the configuration answered 500: the counts are reported in an alert.
      await expect(dialog.getByRole("alert")).toHaveText(copy[culture].partial(1, 2));
      expect(writes(state)).toEqual(["PUT /mod-pack", "PUT /configuration"]);
      const failedRow = dialog.locator("li", { hasText: "Maximum players" });
      await expect(failedRow).toContainText(copy[culture].failedState);
      await expect(failedRow).toContainText(copy[culture].failedReason);
      await expect(dialog.getByText("Mod C", { exact: true })).toHaveCount(0);
      const retry = dialog.getByRole("button", { name: copy[culture].retry(1) });
      await expect(retry).toBeVisible();
      await expectNoHorizontalScroll(page);

      // The second apply sends the configuration request only: the applied mod pack left the draft.
      await retry.click();
      await expect(page.getByText(copy[culture].toast(1), { exact: true })).toBeVisible();
      await expect(dialog).toBeHidden();
      expect(writes(state)).toEqual(["PUT /mod-pack", "PUT /configuration", "PUT /configuration"]);
      expect(state.requests.at(-1)?.body).toEqual({
        values: [{ key: "server.cfg@maxPlayers", value: 80 }],
        resetKeys: [],
      });
      expect(unexpected).toEqual([]);
    });
  });

  test.describe(`leave guard (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("asks on a section switch and on Back and honors all three outcomes", async ({ page }) => {
      const unexpected = await mockApi(page, culture, createState());
      await mockFleetStream(page);

      // A prior history entry makes Back leave the Settings section.
      await openWorkspace(page);
      await sectionLink(page, culture, copy[culture].settings).click();
      await expect(page).toHaveURL(/[?&]tab=settings(&|$)/);
      await expect(maxPlayersField(page)).toBeVisible();
      expect(await beforeUnloadPrevented(page)).toBe(false);

      await stageMaxPlayers(page);
      await expect(pendingBar(page, culture)).toBeVisible();
      // A beforeunload handler exists only while changes are staged.
      expect(await beforeUnloadPrevented(page)).toBe(true);

      // A section switch asks first, with the three actions in order: Discard and leave, Stay here, Review changes.
      await sectionLink(page, culture, copy[culture].files).click();
      const leave = page.getByRole("dialog", { name: copy[culture].leaveTitle });
      await expect(leave).toBeVisible();
      await expect(leave.getByRole("button")).toHaveText(copy[culture].leaveActions);

      // Stay keeps the location and the staged change.
      await leave
        .getByRole("button", { name: copy[culture].leaveActions[1]!, exact: true })
        .click();
      await expect(leave).toBeHidden();
      await expect(page).toHaveURL(/[?&]tab=settings(&|$)/);
      await expect(pendingBar(page, culture)).toBeVisible();

      // Back asks again.
      await page.goBack();
      await expect(leave).toBeVisible();
      await expect(page).toHaveURL(/[?&]tab=settings(&|$)/);

      // Review changes keeps the location and opens the review.
      await leave
        .getByRole("button", { name: copy[culture].leaveActions[2]!, exact: true })
        .click();
      await expect(leave).toBeHidden();
      const review = reviewDialog(page, culture);
      await expect(review).toBeVisible();
      await expect(page).toHaveURL(/[?&]tab=settings(&|$)/);
      await review.getByRole("button", { name: copy[culture].keepEditing }).click();
      await expect(review).toBeHidden();
      await expect(pendingBar(page, culture)).toBeVisible();

      // Discard and leave reaches the Files section and drops the beforeunload handler.
      await sectionLink(page, culture, copy[culture].files).click();
      await expect(leave).toBeVisible();
      await leave
        .getByRole("button", { name: copy[culture].leaveActions[0]!, exact: true })
        .click();
      await expect(page).toHaveURL(/[?&]tab=files(&|$)/);
      await expect(leave).toBeHidden();
      expect(await beforeUnloadPrevented(page)).toBe(false);
      expect(unexpected).toEqual([]);
    });
  });
}
