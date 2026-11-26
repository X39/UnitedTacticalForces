import { Buffer } from "node:buffer";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Locator, type Page } from "@playwright/test";
import { settleScroll } from "./support/a11y";
import { mockFleetStream } from "./support/fleetStream";
import { fulfillCostRoute } from "./support/costMocks";
import { fulfillMetricsRoute } from "./support/metricsMocks";
import { seedSettingsPanels } from "./support/settingsPanels";
import { settle, stabilize } from "./support/visual";

type Culture = "en" | "de";

const serverId = "10000000-0000-4000-8000-000000000f65";
const serverTitle = "Files test server";

/**
 * Layout boxes come back with fractional coordinates; a difference of at most one CSS pixel is a rounding artifact,
 * not a moved control.
 */
const subpixelTolerance = 1;

const wcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

/** The distinctive plaintext the mocked reveal endpoint returns; it must never outlive the 30 second reveal (T-65-40). */
const revealedSecret = "s3cr3t-e2e-value";

/** The distinctive plaintext the mocked rotate endpoint generates; it is shown once and must leave no trace. */
const rotatedSecret = "Gen3rated-e2e-once-value";

/** The value a member types into a credential in the chosen value test. */
const chosenSecret = "Chosen-Value_42";

/** An unbroken value longer than any phone line, to prove a revealed value wraps instead of scrolling the page. */
const longRevealedSecret = `s3cr3t-long-${"x".repeat(160)}`;

/** The two viewports of the credential check: the smallest supported phone and a common one. */
const viewports = [
  { width: 320, height: 568 },
  { width: 390, height: 844 },
] as const;

/** The visible copy each Files test reads, per culture; every line is the Copywriting Contract text of that culture. */
interface Copy {
  pathLabel: string;
  topLevel: string;
  persistentShort: string;
  /** The context label that is the folder heading at the root of the persistent files. */
  persistentFiles: string;
  /** The context label that is the folder heading at the root of the live files. */
  liveFiles: string;
  /** The footer note that file changes take effect immediately. */
  immediateNote: string;
  /** The label of the Upload button of the toolbar and of the upload dialog confirm. */
  upload: string;
  /** The label of the choose files button of the upload dialog. */
  chooseFiles: string;
  /** The label of the choose folder button of the upload dialog. */
  chooseFolder: string;
  /** The visible reason the upload confirm is unavailable while the queue is empty. */
  uploadReason: string;
  invalidNotice: string;
  fallbackNotice: string;
  /** Visible text of the Reveal button; the accessible name appends the credential name. */
  reveal: RegExp;
  /** Visible text of the Hide button; the accessible name appends the credential name. */
  hide: RegExp;
  /** Assistive text of the masked value. */
  masked: string;
  /** Visible text of the Rotate button; the accessible name appends the credential name. */
  rotate: RegExp;
  /** Visible text of the Keep current value button; the accessible name appends the credential name. */
  keep: RegExp;
  /** Visible text of the Undo button; the accessible name appends the credential name. */
  undo: RegExp;
  /** The label of the value input of a credential. */
  valueLabel(name: string): string;
  /** The status line text of each staging state of a credential at version 1. */
  status: { saved: string; staged: string; rotating: string };
  /** The name of the Review changes button and dialog. */
  review: string;
  /** The label of the pending changes region. */
  barLabel: string;
  /** The name of the Apply button of the review dialog for one change. */
  applyOne: string;
  /** The review line of a changed credential value. */
  secretChanged: string;
  /** Visible text of the Copy value button. */
  copyValue: RegExp;
  /** Visible text of the Copy value button after a successful copy. */
  copied: string;
  /** Name of the Show typed value toggle of one credential. */
  showTyped(name: string): string;
  /** The message of a value that is not a token. */
  tokenError: string;
  /** The alert that keeps the review closed. */
  invalidAlert: string;
  /** The label that names the one-time value block of a credential. */
  onceLabel: string;
}

const copy: Record<Culture, Copy> = {
  en: {
    pathLabel: "Folder path",
    topLevel: "Top level",
    persistentShort: "Persistent",
    persistentFiles: "Persistent expected files",
    liveFiles: "Live worker files",
    immediateNote: "Changes to files take effect immediately.",
    upload: "Upload",
    chooseFiles: "Choose files",
    chooseFolder: "Choose folder",
    uploadReason: "Choose files or drop them into the explorer.",
    invalidNotice: "That folder link is not valid. Showing the top level instead.",
    fallbackNotice: "That folder was not found. Showing the top level instead.",
    reveal: /^Reveal value/,
    hide: /^Hide value/,
    masked: "Value hidden",
    rotate: /^Rotate value/,
    keep: /^Keep current value/,
    undo: /^Undo change/,
    valueLabel: (name) => `New value for ${name}`,
    status: {
      saved: "Configured, version 1",
      staged: "A new value is staged.",
      rotating: "A new value is generated on apply.",
    },
    review: "Review changes",
    barLabel: "Pending changes",
    applyOne: "Apply 1 change",
    secretChanged: "Changed. The value is not shown.",
    copyValue: /^Copy value/,
    copied: "Copied",
    showTyped: (name) => `Show typed value of ${name}`,
    tokenError: "Use only letters, digits and symbols, without spaces, quotes or backslashes.",
    invalidAlert: "Fix the highlighted setting before reviewing.",
    onceLabel: "New value. Copy it now: it is shown only this once.",
  },
  de: {
    pathLabel: "Ordnerpfad",
    topLevel: "Oberste Ebene",
    persistentShort: "Persistent",
    persistentFiles: "Persistente Solldateien",
    liveFiles: "Live-Worker-Dateien",
    immediateNote: "Änderungen an Dateien gelten sofort.",
    upload: "Hochladen",
    chooseFiles: "Dateien auswählen",
    chooseFolder: "Ordner auswählen",
    uploadReason: "Dateien auswählen oder im Explorer ablegen.",
    invalidNotice:
      "Dieser Ordnerlink ist nicht gültig. Stattdessen wird die oberste Ebene angezeigt.",
    fallbackNotice:
      "Dieser Ordner wurde nicht gefunden. Stattdessen wird die oberste Ebene angezeigt.",
    reveal: /^Wert aufdecken/,
    hide: /^Wert verbergen/,
    masked: "Wert verborgen",
    rotate: /^Wert erneuern/,
    keep: /^Aktuellen Wert behalten/,
    undo: /^Änderung zurücknehmen/,
    valueLabel: (name) => `Neuer Wert für ${name}`,
    status: {
      saved: "Eingerichtet, Version 1",
      staged: "Neuer Wert vorgemerkt.",
      rotating: "Neuer Wert entsteht beim Anwenden.",
    },
    review: "Änderungen prüfen",
    barLabel: "Ausstehende Änderungen",
    applyOne: "1 Änderung anwenden",
    secretChanged: "Geändert. Der Wert wird nicht angezeigt.",
    copyValue: /^Wert kopieren/,
    copied: "Kopiert",
    showTyped: (name) => `Eingabe anzeigen: ${name}`,
    tokenError:
      "Verwende nur Buchstaben, Ziffern und Symbole, ohne Leerzeichen, Anführungszeichen oder Backslashes.",
    invalidAlert: "Korrigiere die markierte Einstellung, bevor du prüfst.",
    onceLabel: "Neuer Wert. Kopiere ihn jetzt: Er wird nur dieses eine Mal angezeigt.",
  },
};

/** Builds one file listing entry; every field the API sends is present. */
function fileEntry(path: string, isDirectory: boolean) {
  return {
    path,
    name: path.split("/").at(-1)!,
    isDirectory,
    size: isDirectory ? null : 12,
    modifiedAt: "2026-08-13T09:00:00Z",
    context: "Persistent",
    classification: "Unknown",
    canModify: true,
    origin: "Persistent",
    modificationMode: "RawFile",
    isGeneratedTarget: false,
    overridesGenerated: false,
    configurationStatus: "NotApplicable",
  };
}

/**
 * The persistent listings by folder path. The top level holds `profiles`, `profiles` holds `child` and a file and
 * `profiles/child` is empty. Every other path answers 404, like a folder that does not exist.
 */
const persistentListings: Record<string, ReturnType<typeof fileEntry>[]> = {
  "": [fileEntry("profiles", true)],
  profiles: [
    fileEntry("profiles/child", true),
    fileEntry("profiles/notes.txt", false),
    fileEntry("profiles/server.cfg", false),
  ],
  "profiles/child": [],
};

/** The mocked API of one test: the strictly unexpected requests and the files requests it has seen. */
interface MockedApi {
  /** Every request no mock handled; a test asserts this stays empty. */
  unexpected: string[];
  /** The number of files requests by `context:path`. */
  fileRequests: Map<string, number>;
  /** The number of credential reveal requests. */
  revealRequests: number;
  /** The number of credential rotate requests. */
  rotateRequests: number;
  /** The parsed body of every credential set request. */
  secretWrites: unknown[];
}

/** Options of {@link mockApi}. */
interface MockOptions {
  /** The plaintext the reveal endpoint returns. */
  revealValue?: string;
  /** The credentials of the server; defaults to the one revealable join password. */
  secrets?: SecretFixture[];
  /** Whether the member may reveal credential values; defaults to true. */
  canRevealSecrets?: boolean;
  /** The value the rotate endpoint generates. */
  rotateValue?: string;
}

/** Builds the server projection: an Arma 3 server with no deployment that the member can modify. */
function serverBody(canRevealSecrets: boolean) {
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
    selectedModPackDefinitionId: null,
    selectedModPackDefinitionTitle: null,
    pinnedModPackRevisionId: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    pinnedConfigurationProfileRevisionId: null,
    costOwnerId: serverId,
    costOwnerName: "Operations",
    createdById: serverId,
    costOwnerUserId: null,
    isLaunchDisabled: false,
    changesPendingStartAreas: [],
    hasChangesPendingStart: false,
    isArchived: false,
    canModify: true,
    canManageSecrets: true,
    canRevealSecrets,
    timeStampCreated: "2026-11-16T10:00:00Z",
    timeStampUpdated: "2026-11-16T10:00:00Z",
    deployment: null,
    pendingJobCount: 0,
    actions: [],
  };
}

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

/** One credential status of the mocked server; every field the API sends is present. */
interface SecretFixture {
  key: string;
  displayName: string;
  requiredForStart: boolean;
  generated: boolean;
  revealable: boolean;
  externalProvider: boolean;
  isConfigured: boolean;
  version: number;
  origin: "Generated" | "UserProvided";
  timeStampCreated: string;
  acceptsChosenValue: boolean;
  valueFormat: "SingleLine" | "Token";
}

/** The default credential of the mocked server: configured, generated and revealable. */
const secretStatus: SecretFixture = {
  key: "join-password",
  displayName: "Join password",
  requiredForStart: false,
  generated: true,
  revealable: true,
  externalProvider: false,
  isConfigured: true,
  version: 1,
  origin: "Generated",
  timeStampCreated: "2026-11-16T10:00:00Z",
  acceptsChosenValue: true,
  valueFormat: "SingleLine",
};

/** The Administrator password of the staging fixture: generated, takes a chosen single-line value. */
const adminPasswordStatus: SecretFixture = {
  ...secretStatus,
  key: "admin-password",
  displayName: "Administrator password",
  revealable: false,
};

/** The BattlEye RCON password of the staging fixture: generated, takes a chosen token value. */
const battleyePasswordStatus: SecretFixture = {
  ...secretStatus,
  key: "battleye-rcon-password",
  displayName: "BattlEye RCON password",
  revealable: false,
  valueFormat: "Token",
};

/** The join password of the staging fixture: provided by the member, never generated. */
const userProvidedJoinPasswordStatus: SecretFixture = {
  ...secretStatus,
  generated: false,
  revealable: false,
  origin: "UserProvided",
};

/** The credentials of the Arma 3 staging fixture, in display order. */
const stagingSecrets: SecretFixture[] = [
  adminPasswordStatus,
  battleyePasswordStatus,
  userProvidedJoinPasswordStatus,
];

/**
 * Installs the strict API mock set of the Files and credential tests.
 *
 * Reads answer from fixed data; every files request is counted by context and path so a test can prove that no
 * request loop exists. Unmocked requests are aborted and recorded so a test can assert none happened (T-65-39).
 */
async function mockApi(
  page: Page,
  culture: Culture,
  options: MockOptions = {},
): Promise<MockedApi> {
  const api: MockedApi = {
    unexpected: [],
    fileRequests: new Map(),
    revealRequests: 0,
    rotateRequests: 0,
    secretWrites: [],
  };
  const base = `/api/game-servers/${serverId}`;
  const canRevealSecrets = options.canRevealSecrets ?? true;
  // The saved credentials are stateful: a rotation and a set bump the version like the API does.
  const secrets = (options.secrets ?? [secretStatus]).map((secret) => ({ ...secret }));
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
    const url = new URL(request.url());
    const path = url.pathname;
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
      if (path === base) return route.fulfill({ json: serverBody(canRevealSecrets) });
      if (path === "/api/game-servers") {
        return route.fulfill({ json: [serverBody(canRevealSecrets)] });
      }
      if (path === "/api/game-servers/capabilities") return route.fulfill({ json: [capability] });
      const files = new RegExp(`^${base}/files/(persistent|live)$`).exec(path);
      if (files) {
        const context = files[1]!;
        const folder = url.searchParams.get("path") ?? "";
        const key = `${context}:${folder}`;
        api.fileRequests.set(key, (api.fileRequests.get(key) ?? 0) + 1);
        const listing = context === "persistent" ? persistentListings[folder] : undefined;
        if (listing) return route.fulfill({ json: listing });
        // A missing folder, and the live context of a server without a runtime, answer 404.
        return route.fulfill({
          status: 404,
          json: { title: "Not Found", detail: "The requested folder does not exist." },
        });
      }
      if (path === `${base}/configuration`) {
        return route.fulfill({
          json: {
            serverId,
            profileId: null,
            profileRevisionId: null,
            profileRevisionNumber: null,
            isComplete: true,
            entries: [],
          },
        });
      }
      if (path === `${base}/secrets`) return route.fulfill({ json: secrets });
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
          `${base}/preservation-selections`,
          `${base}/mod-pack-options`,
          `${base}/configuration/profile-options`,
        ].includes(path)
      ) {
        return route.fulfill({ json: [] });
      }
    }
    if (method === "POST" && path === `${base}/secrets/${secretStatus.key}/reveal`) {
      api.revealRequests += 1;
      return route.fulfill({
        json: {
          key: secretStatus.key,
          value: options.revealValue ?? revealedSecret,
          version: secretStatus.version,
        },
      });
    }
    const rotate = new RegExp(`^${base}/secrets/([^/]+)/rotate$`).exec(path);
    if (method === "POST" && rotate) {
      const target = secrets.find((secret) => secret.key === rotate[1]);
      if (target) {
        api.rotateRequests += 1;
        target.version += 1;
        return route.fulfill({
          json: {
            key: target.key,
            value: options.rotateValue ?? rotatedSecret,
            version: target.version,
          },
        });
      }
    }
    if (method === "PUT" && path === `${base}/secrets`) {
      const body = request.postDataJSON() as { key: string; value: string; origin: string };
      const target = secrets.find((secret) => secret.key === body.key);
      if (target) {
        api.secretWrites.push(body);
        target.version += 1;
        target.isConfigured = true;
        return route.fulfill({ status: 204 });
      }
    }
    if (await fulfillMetricsRoute(route, method, path, serverId)) return;
    if (await fulfillCostRoute(route, method, path, serverId)) return;
    api.unexpected.push(`${method} ${path}`);
    return route.abort();
  });
  return api;
}

/** Opens the workspace of the mocked server and waits until the shell rendered. */
async function openWorkspace(page: Page, search: string) {
  await page.goto(`command/servers/${serverId}${search}`);
  await expect(page.getByRole("heading", { level: 1, name: serverTitle })).toBeVisible();
}

/** The folder heading of the Files panel. */
function folderHeading(page: Page, name: string) {
  return page.getByRole("heading", { level: 3, name, exact: true });
}

/** The breadcrumb navigation of the Files panel. */
function breadcrumb(page: Page, culture: Culture) {
  return page.getByRole("navigation", { name: copy[culture].pathLabel });
}

/** Reads the `path` search parameter of the page URL, or null when the URL has none. */
function pathParameter(page: Page): string | null {
  return new URL(page.url()).searchParams.get("path");
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

for (const culture of ["en", "de"] as const) {
  test.describe(`files folders in the URL (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("restores a folder on reload, Back and Forward and in a fresh page", async ({
      page,
      context,
    }) => {
      const api = await mockApi(page, culture);
      await mockFleetStream(page);

      await openWorkspace(page, "?tab=files&path=profiles");
      await expect(folderHeading(page, "profiles")).toBeVisible();
      await expect(page.getByRole("link", { name: "child", exact: true })).toBeVisible();
      await expect(page.getByText("server.cfg", { exact: true })).toBeVisible();
      const crumbs = breadcrumb(page, culture);
      await expect(crumbs.getByRole("link", { name: copy[culture].persistentShort })).toBeVisible();
      await expect(crumbs.locator('[aria-current="location"]')).toHaveText("profiles");
      // File operations are immediate and never staged.
      await expect(page.getByText(copy[culture].immediateNote, { exact: true })).toBeVisible();

      // A reload keeps the folder.
      await page.reload();
      await expect(folderHeading(page, "profiles")).toBeVisible();
      expect(pathParameter(page)).toBe("profiles");

      // Following a folder link pushes a history entry with the new path.
      await page.getByRole("link", { name: "child", exact: true }).click();
      await expect(page).toHaveURL((url) => url.searchParams.get("path") === "profiles/child");
      await expect(folderHeading(page, "child")).toBeVisible();
      await expect(crumbs.locator('[aria-current="location"]')).toHaveText("child");

      // Back and Forward restore the folders.
      await page.goBack();
      await expect(page).toHaveURL((url) => url.searchParams.get("path") === "profiles");
      await expect(folderHeading(page, "profiles")).toBeVisible();
      await page.goForward();
      await expect(page).toHaveURL((url) => url.searchParams.get("path") === "profiles/child");
      await expect(folderHeading(page, "child")).toBeVisible();

      // The same URL in a fresh page of the same context is a shared link.
      const shared = await context.newPage();
      const sharedApi = await mockApi(shared, culture);
      await mockFleetStream(shared);
      await shared.setViewportSize({ width: 390, height: 844 });
      await openWorkspace(shared, "?tab=files&path=profiles%2Fchild");
      await expect(folderHeading(shared, "child")).toBeVisible();
      expect(pathParameter(shared)).toBe("profiles/child");
      await expect(shared.getByText(copy[culture].invalidNotice, { exact: true })).toHaveCount(0);
      expect(sharedApi.unexpected).toEqual([]);
      await shared.close();

      expect(api.unexpected).toEqual([]);
    });

    test("falls back to the top level for invalid and missing folders without keeping the bad link", async ({
      page,
    }) => {
      const api = await mockApi(page, culture);
      await mockFleetStream(page);

      for (const [badSearch, notice] of [
        ["?tab=files&path=a/../b", copy[culture].invalidNotice],
        ["?tab=files&path=missing", copy[culture].fallbackNotice],
      ] as const) {
        // A good entry first, so Back has somewhere to go and the bad link is the entry that gets replaced.
        await openWorkspace(page, "?tab=files&path=profiles");
        await expect(folderHeading(page, "profiles")).toBeVisible();

        await openWorkspace(page, badSearch);
        await expect(page.getByText(notice, { exact: true })).toBeVisible();
        await expect(folderHeading(page, copy[culture].persistentFiles)).toBeVisible();
        await expect(page).toHaveURL((url) => url.searchParams.get("tab") === "files");
        expect(pathParameter(page)).toBeNull();

        // The dead link was replaced, so Back returns to the good folder and never to the bad link.
        await page.goBack();
        await expect(page).toHaveURL((url) => url.searchParams.get("path") === "profiles");
        await expect(folderHeading(page, "profiles")).toBeVisible();
        expect(page.url()).not.toContain("..");
        expect(page.url()).not.toContain("missing");
      }
      expect(api.unexpected).toEqual([]);
    });

    test("shows the error panel for the live context without a runtime and does not loop", async ({
      page,
    }) => {
      const api = await mockApi(page, culture);
      await mockFleetStream(page);

      await openWorkspace(page, "?tab=files&area=live");
      await expect(page.getByRole("alert")).toHaveText("The requested folder does not exist.");
      await expect(folderHeading(page, copy[culture].liveFiles)).toBeVisible();
      // The top level never falls back to itself, so the URL keeps the live context.
      expect(new URL(page.url()).searchParams.get("area")).toBe("live");
      // A request loop would keep counting; give a loop time to show before asserting the final count.
      await page.waitForTimeout(750);
      expect(api.fileRequests.get("live:") ?? 0).toBeGreaterThan(0);
      expect(api.fileRequests.get("live:") ?? 0).toBeLessThanOrEqual(2);
      expect(api.unexpected).toEqual([]);
    });

    test("has no axe violation on the Files section with a fallback notice", async ({ page }) => {
      const api = await mockApi(page, culture);
      await mockFleetStream(page);

      await openWorkspace(page, "?tab=files&path=missing");
      await expect(page.getByText(copy[culture].fallbackNotice, { exact: true })).toBeVisible();
      await expect(folderHeading(page, copy[culture].persistentFiles)).toBeVisible();
      expect(await scan(page, ".next-hosting-files-panel")).toEqual([]);
      expect(api.unexpected).toEqual([]);
    });
  });
}

/** The two viewports of the file browser card layout check: a desktop and a phone. */
const cardViewports = [
  { width: 1280, height: 800 },
  { width: 390, height: 844 },
] as const;

/** The largest gap in CSS pixels between the folder heading and the first table row (UI-SPEC list pane). */
const headingToRowGap = 24;

for (const culture of ["en", "de"] as const) {
  for (const viewport of cardViewports) {
    test.describe(`files card layout (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test("keeps the list directly under its heading, with no minimum height and no sideways scroll", async ({
        page,
      }) => {
        const api = await mockApi(page, culture);
        await mockFleetStream(page);

        await openWorkspace(page, "?tab=files&path=profiles");
        const heading = folderHeading(page, "profiles");
        await expect(heading).toBeVisible();
        const firstRow = page.locator(".file-browser__table tr").first();
        await expect(firstRow).toBeVisible();

        // Both boxes are read in one frame, so a late layout shift cannot separate them.
        await expect
          .poll(async () =>
            page.evaluate(() => {
              const headingRect = document
                .querySelector(".file-browser__list h3")!
                .getBoundingClientRect();
              const rowRect = document
                .querySelector(".file-browser__table tr")!
                .getBoundingClientRect();
              return rowRect.top - headingRect.bottom;
            }),
          )
          .toBeGreaterThanOrEqual(0);
        const gap = await page.evaluate(
          () =>
            document.querySelector(".file-browser__table tr")!.getBoundingClientRect().top -
            document.querySelector(".file-browser__list h3")!.getBoundingClientRect().bottom,
        );
        expect(gap).toBeLessThanOrEqual(headingToRowGap);

        // The card height follows its content: no 32rem minimum on the explorer.
        const explorer = page.locator(".file-browser__explorer");
        const minHeight = await explorer.evaluate((element) => getComputedStyle(element).minHeight);
        expect(["auto", "0px"]).toContain(minHeight);
        const explorerBox = (await explorer.boundingBox())!;
        expect(explorerBox.height).toBeLessThan(512);

        // The note that changes are immediate lives in the footer of the same card.
        const footer = page.locator(".file-browser > footer");
        await expect(footer).toContainText(copy[culture].immediateNote);
        await expect(page.locator(".file-browser")).toContainText("profiles");

        await expectNoHorizontalScroll(page);
        expect(api.unexpected).toEqual([]);
      });
    });
  }
}

for (const culture of ["en", "de"] as const) {
  test.describe(`files upload dialog (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("opens on a drop zone with stacked choose buttons, focus on the first and a gold confirm that explains itself", async ({
      page,
    }) => {
      const api = await mockApi(page, culture);
      await mockFleetStream(page);

      await openWorkspace(page, "?tab=files&path=profiles");
      await expect(folderHeading(page, "profiles")).toBeVisible();
      await page
        .getByRole("toolbar")
        .getByRole("button", { name: copy[culture].upload, exact: true })
        .click();
      const dialog = page.getByRole("dialog");
      await expect(dialog).toBeVisible();

      // The zone has a dashed control border; the choose buttons stack, one under the other, at 390px.
      const zone = dialog.locator(".file-dropzone");
      await expect(zone).toBeVisible();
      expect(await zone.evaluate((element) => getComputedStyle(element).borderTopStyle)).toBe(
        "dashed",
      );
      const chooseFiles = dialog.getByRole("button", {
        name: copy[culture].chooseFiles,
        exact: true,
      });
      const chooseFolder = dialog.getByRole("button", {
        name: copy[culture].chooseFolder,
        exact: true,
      });
      const filesBox = (await chooseFiles.boundingBox())!;
      const folderBox = (await chooseFolder.boundingBox())!;
      expect(folderBox.y).toBeGreaterThanOrEqual(filesBox.y + filesBox.height - subpixelTolerance);
      expect(Math.abs(folderBox.x - filesBox.x)).toBeLessThanOrEqual(subpixelTolerance);
      // Stacked buttons share the zone width, so each is much wider than its label.
      expect(Math.abs(folderBox.width - filesBox.width)).toBeLessThanOrEqual(subpixelTolerance);
      expect(filesBox.width).toBeGreaterThan(240);

      // Focus starts on the first choose control.
      await expect(chooseFiles).toBeFocused();

      // The confirm is gold and unavailable with a visible reason until a file is queued.
      const confirm = dialog.getByRole("button", { name: copy[culture].upload, exact: true });
      await expect(confirm).toHaveClass(/button--gold/);
      await expect(confirm).toHaveAttribute("aria-disabled", "true");
      await expect(dialog.getByText(copy[culture].uploadReason, { exact: true })).toBeVisible();

      // The dialog never scrolls sideways.
      const metrics = await dialog.evaluate((element) => ({
        scrollWidth: element.scrollWidth,
        clientWidth: element.clientWidth,
      }));
      expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 1);
      await expectNoHorizontalScroll(page);
      expect(api.unexpected).toEqual([]);
    });
  });
}

/** A file name longer than any phone line, to prove a queued path wraps instead of scrolling the dialog. */
const longUploadName = `upload-with-a-very-long-name-${"x".repeat(90)}.txt`;

for (const culture of ["en", "de"] as const) {
  test.describe(`files upload queue (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("lists five files with two collisions as compact rows without sideways scroll and keeps the actions reachable", async ({
      page,
    }) => {
      const api = await mockApi(page, culture);
      await mockFleetStream(page);

      await openWorkspace(page, "?tab=files&path=profiles");
      await expect(folderHeading(page, "profiles")).toBeVisible();
      await page
        .getByRole("toolbar")
        .getByRole("button", { name: copy[culture].upload, exact: true })
        .click();
      const dialog = page.getByRole("dialog");
      await expect(dialog).toBeVisible();

      const text = (name: string) => ({ name, mimeType: "text/plain", buffer: Buffer.from(name) });
      await dialog
        .locator("input[type=file]")
        .first()
        .setInputFiles([
          text("server.cfg"),
          text("notes.txt"),
          text("fresh-one.txt"),
          text("fresh-two.txt"),
          text(longUploadName),
        ]);
      const rows = dialog.locator("li.file-upload-queue__row");
      await expect(rows).toHaveCount(5);

      // Every row carries a status badge, and exactly the two colliding rows carry the replace badge.
      await expect(rows.locator(".status-badge--neutral")).toHaveCount(5);
      await expect(rows.locator(".status-badge--warning")).toHaveCount(2);

      // The queue and its rows never scroll sideways, and the long name wraps within its row.
      const overflow = await dialog.evaluate((element) => {
        const list = element.querySelector<HTMLElement>(".file-upload-queue")!;
        return {
          dialog: element.scrollWidth - element.clientWidth,
          list: list.scrollWidth - list.clientWidth,
          rows: Array.from(list.querySelectorAll<HTMLElement>("li")).map(
            (row) => row.scrollWidth - row.clientWidth,
          ),
        };
      });
      expect(overflow.dialog).toBeLessThanOrEqual(1);
      expect(overflow.list).toBeLessThanOrEqual(1);
      for (const rowOverflow of overflow.rows) expect(rowOverflow).toBeLessThanOrEqual(1);
      await expectNoHorizontalScroll(page);

      // The approval and the actions stay reachable inside the dialog; the confirm waits for the approval.
      const approval = dialog.getByRole("checkbox");
      await approval.scrollIntoViewIfNeeded();
      await expect(approval).toBeVisible();
      const confirm = dialog.getByRole("button", { name: copy[culture].upload, exact: true });
      await confirm.scrollIntoViewIfNeeded();
      await expect(confirm).toBeVisible();
      await expect(confirm).toHaveAttribute("aria-disabled", "true");
      const box = (await confirm.boundingBox())!;
      const dialogBox = (await dialog.boundingBox())!;
      expect(box.y).toBeGreaterThanOrEqual(dialogBox.y - subpixelTolerance);
      expect(box.y + box.height).toBeLessThanOrEqual(
        dialogBox.y + dialogBox.height + subpixelTolerance,
      );
      expect(box.y + box.height).toBeLessThanOrEqual(844 + subpixelTolerance);

      await approval.check();
      await expect(confirm).not.toHaveAttribute("aria-disabled", "true");
      expect(api.unexpected).toEqual([]);
    });
  });
}

/** The revealed value element of the one credential. */
function revealedCode(page: Page) {
  return page.locator("code.credential__code");
}

/** Collects every console message and page error of the page, as plain text. */
function collectConsole(page: Page): string[] {
  const messages: string[] = [];
  page.on("console", (message) => messages.push(message.text()));
  page.on("pageerror", (error) => messages.push(error.message));
  return messages;
}

/** Dumps every localStorage and sessionStorage entry of the page as one JSON text. */
async function dumpStorage(page: Page): Promise<string> {
  return page.evaluate(() =>
    JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }),
  );
}

/**
 * Asserts that a value is in none of the places a secret must never reach: the DOM, both storages, the cookies, the
 * URL and every console message (T-65-40).
 */
async function expectNoTrace(page: Page, value: string, console: string[]) {
  expect(await page.content()).not.toContain(value);
  expect(await dumpStorage(page)).not.toContain(value);
  expect(JSON.stringify(await page.context().cookies())).not.toContain(value);
  expect(page.url()).not.toContain(value);
  expect(console.join("\n")).not.toContain(value);
}

/** Asserts that the document cannot scroll horizontally, allowing one pixel of rounding. */
async function expectNoHorizontalScroll(page: Page) {
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 1);
}

/** Opens Settings on a fake clock and waits until the credential row is ready. */
async function openCredentials(page: Page, culture: Culture) {
  await page.clock.install();
  await openWorkspace(page, "?tab=settings");
  await expect(page.getByRole("button", { name: copy[culture].reveal })).toBeVisible();
}

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`credential reveal (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test("hides after 30 seconds and when the tab is hidden and leaves no trace", async ({
        page,
      }) => {
        const api = await mockApi(page, culture);
        await mockFleetStream(page);
        const console = collectConsole(page);

        await openCredentials(page, culture);
        await expect(page.getByText(copy[culture].masked, { exact: true })).toHaveCount(1);
        await expect(revealedCode(page)).toHaveCount(0);

        // Reveal shows the value in the row's code element and offers Hide instead of Reveal.
        await page.getByRole("button", { name: copy[culture].reveal }).click();
        await expect(revealedCode(page)).toHaveText(revealedSecret);
        await expect(page.getByRole("button", { name: copy[culture].hide })).toBeVisible();
        await expect(page.getByRole("button", { name: copy[culture].reveal })).toHaveCount(0);
        expect(api.revealRequests).toBe(1);
        // While shown, the value is already absent from storage, cookies, the URL and the console.
        expect(await dumpStorage(page)).not.toContain(revealedSecret);
        expect(page.url()).not.toContain(revealedSecret);
        expect(console.join("\n")).not.toContain(revealedSecret);

        // Just before the 30 seconds are over the value is still shown.
        await page.clock.fastForward(29_000);
        await expect(revealedCode(page)).toHaveText(revealedSecret);

        // After 30 seconds the value is gone and the masked text is back.
        await page.clock.fastForward(1_500);
        await expect(revealedCode(page)).toHaveCount(0);
        await expect(page.getByText(copy[culture].masked, { exact: true })).toHaveCount(1);
        await expect(page.getByRole("button", { name: copy[culture].reveal })).toBeVisible();
        await expectNoTrace(page, revealedSecret, console);

        // Reveal again: a hidden tab hides the value at once.
        await page.getByRole("button", { name: copy[culture].reveal }).click();
        await expect(revealedCode(page)).toHaveText(revealedSecret);
        expect(api.revealRequests).toBe(2);
        await page.evaluate(() => {
          Object.defineProperty(document, "visibilityState", {
            configurable: true,
            get: () => "hidden",
          });
          document.dispatchEvent(new Event("visibilitychange"));
        });
        await expect(revealedCode(page)).toHaveCount(0);
        await expect(page.getByText(copy[culture].masked, { exact: true })).toHaveCount(1);
        await expectNoTrace(page, revealedSecret, console);
        expect(api.unexpected).toEqual([]);
      });
    });
  }

  test.describe(`credential layout (${culture}, 320x568)`, () => {
    test.use({ viewport: { width: 320, height: 568 } });

    test("wraps a long revealed value without horizontal scroll", async ({ page }) => {
      const api = await mockApi(page, culture, { revealValue: longRevealedSecret });
      await mockFleetStream(page);

      await openCredentials(page, culture);
      await expectNoHorizontalScroll(page);
      await page.getByRole("button", { name: copy[culture].reveal }).click();
      await expect(revealedCode(page)).toHaveText(longRevealedSecret);
      await expectNoHorizontalScroll(page);
      const box = await revealedCode(page).boundingBox();
      expect(box).not.toBeNull();
      expect(box!.x + box!.width).toBeLessThanOrEqual(320 + 1);
      expect(api.unexpected).toEqual([]);
    });
  });
}

/** The article of one credential, found by the heading with its name. */
function credentialRow(page: Page, name: string) {
  return page.locator("article.credential", {
    has: page.getByRole("heading", { level: 3, name, exact: true }),
  });
}

/** The size of a row and the position of each of its buttons relative to the row's own top-left corner. */
interface RowLayout {
  height: number;
  buttons: { x: number; y: number }[];
}

/** Measures a credential row; positions are relative to the row so page scrolling cannot change them. */
async function measureRow(row: Locator): Promise<RowLayout> {
  const box = await row.boundingBox();
  expect(box).not.toBeNull();
  const buttons: RowLayout["buttons"] = [];
  for (const button of await row.locator("button").all()) {
    const buttonBox = await button.boundingBox();
    expect(buttonBox).not.toBeNull();
    buttons.push({ x: buttonBox!.x - box!.x, y: buttonBox!.y - box!.y });
  }
  return { height: box!.height, buttons };
}

/** Asserts that a row keeps its height and every button position within one pixel of the baseline. */
function expectSteady(layout: RowLayout, baseline: RowLayout) {
  expect(layout.buttons).toHaveLength(baseline.buttons.length);
  expect(Math.abs(layout.height - baseline.height)).toBeLessThanOrEqual(subpixelTolerance);
  layout.buttons.forEach((button, index) => {
    expect(Math.abs(button.x - baseline.buttons[index]!.x)).toBeLessThanOrEqual(subpixelTolerance);
    expect(Math.abs(button.y - baseline.buttons[index]!.y)).toBeLessThanOrEqual(subpixelTolerance);
  });
}

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`credential staging layout (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test("keeps the row height and every button in place across staging states", async ({
        page,
      }) => {
        // Production loads its fonts from Google Fonts. Without the vendored files the label font falls back at an
        // unpredictable moment, which re-wraps a label by a line fraction and reads as a layout shift.
        await stabilize(page, culture);
        const api = await mockApi(page, culture, {
          secrets: stagingSecrets,
          canRevealSecrets: false,
        });
        await mockFleetStream(page);
        await openWorkspace(page, "?tab=settings");

        const name = "Administrator password";
        const text = copy[culture];
        const row = credentialRow(page, name);
        const status = row.locator(".credential__status");
        const input = row.getByLabel(text.valueLabel(name));
        await expect(status).toHaveText(text.status.saved);
        // A web font that loads later re-wraps text and would look like a layout shift, so measure after it settled.
        await settle(page);
        const baseline = await measureRow(row);
        expect(baseline.buttons).toHaveLength(2);

        // A staged rotation.
        await row.getByRole("button", { name: text.rotate }).click();
        await expect(status).toHaveText(text.status.rotating);
        expectSteady(await measureRow(row), baseline);

        // Keep current value.
        await row.getByRole("button", { name: text.keep }).click();
        await expect(status).toHaveText(text.status.saved);
        expectSteady(await measureRow(row), baseline);

        // A valid typed value.
        await input.fill("Chosen-Value_42");
        await expect(status).toHaveText(text.status.staged);
        expectSteady(await measureRow(row), baseline);

        // Undo change.
        await row.getByRole("button", { name: text.undo }).click();
        await expect(status).toHaveText(text.status.saved);
        expectSteady(await measureRow(row), baseline);
        expect(api.unexpected).toEqual([]);
      });
    });
  }
}
/** The credentials of the grid checks: two with a Reveal button and one without, each with a typed value input. */
const gridSecrets: SecretFixture[] = [
  secretStatus,
  { ...secretStatus, key: "rcon-password", displayName: "RCON password" },
  adminPasswordStatus,
];

/** The left edge of every box that matches the selector, in page pixels. */
async function leftEdges(page: Page, selector: string): Promise<number[]> {
  return page
    .locator(selector)
    .evaluateAll((elements) => elements.map((element) => element.getBoundingClientRect().left));
}

/** Asserts that every edge is within one pixel of the first one. */
function expectSameEdge(edges: number[]) {
  expect(edges.length).toBeGreaterThan(1);
  for (const edge of edges)
    expect(Math.abs(edge - edges[0]!)).toBeLessThanOrEqual(subpixelTolerance);
}

for (const culture of ["en", "de"] as const) {
  test.describe(`credential row grid (${culture}, 1280x720)`, () => {
    test.use({ viewport: { width: 1280, height: 720 } });

    test("starts the action column at the same x in every row", async ({ page }) => {
      const api = await mockApi(page, culture, { secrets: gridSecrets, canRevealSecrets: true });
      await mockFleetStream(page);
      await openWorkspace(page, "?tab=settings");
      await expect(page.getByRole("button", { name: copy[culture].reveal })).toHaveCount(2);

      const toggles = await leftEdges(page, ".credential__toggle-slot");
      const values = await leftEdges(page, ".credential__value-actions");
      expect(toggles).toHaveLength(3);
      expect(values).toHaveLength(2);
      expectSameEdge([...toggles, ...values]);
      await expectNoHorizontalScroll(page);
      expect(api.unexpected).toEqual([]);
    });

    test("keeps the action column aligned while a row shows its one-time value", async ({
      page,
    }) => {
      const api = await mockApi(page, culture, { secrets: gridSecrets, canRevealSecrets: false });
      await mockFleetStream(page);
      await openWorkspace(page, "?tab=settings");
      await expect(credentialRow(page, "Administrator password")).toBeVisible();

      await applyAdminRotation(page, culture);

      await expect(page.locator("code.credential__code--once")).toHaveText(rotatedSecret);
      const toggles = await leftEdges(page, ".credential__toggle-slot");
      const values = await leftEdges(page, ".credential__value-actions");
      expect(toggles).toHaveLength(3);
      expect(values).toHaveLength(1);
      expectSameEdge([...toggles, ...values]);
      expect(api.unexpected).toEqual([]);
    });
  });

  for (const viewport of viewports) {
    test.describe(`credential row grid (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport });

      test("lays each row out in one column with the actions below the value", async ({ page }) => {
        const api = await mockApi(page, culture, { secrets: gridSecrets, canRevealSecrets: true });
        await mockFleetStream(page);
        await openWorkspace(page, "?tab=settings");
        await expect(page.getByRole("button", { name: copy[culture].reveal })).toHaveCount(2);

        const rows = await page.locator("article.credential").evaluateAll((articles) =>
          articles.map((article) => {
            const rect = (selector: string) =>
              article.querySelector(selector)?.getBoundingClientRect() ?? null;
            return {
              text: rect(".credential__value-text"),
              actions: rect(".credential__value-actions"),
              toggle: rect(".credential__toggle-slot"),
              row: article.getBoundingClientRect(),
            };
          }),
        );
        expect(rows).toHaveLength(3);
        for (const { text, actions, toggle, row } of rows) {
          expect(toggle).not.toBeNull();
          expect(Math.abs(toggle!.left - row.left)).toBeLessThanOrEqual(subpixelTolerance);
          if (!text || !actions) continue;
          expect(Math.abs(actions.left - text.left)).toBeLessThanOrEqual(subpixelTolerance);
          expect(actions.top).toBeGreaterThanOrEqual(text.bottom - subpixelTolerance);
          expect(actions.right).toBeLessThanOrEqual(row.right + subpixelTolerance);
        }
        await expectNoHorizontalScroll(page);
        expect(api.unexpected).toEqual([]);
      });
    });
  }
}

/** Asserts that a value is in no persistent place: both storages, the cookies, the URL and every console message. */
async function expectNoPersistentTrace(page: Page, value: string, console: string[]) {
  expect(await dumpStorage(page)).not.toContain(value);
  expect(JSON.stringify(await page.context().cookies())).not.toContain(value);
  expect(page.url()).not.toContain(value);
  expect(console.join("\n")).not.toContain(value);
}

/** Stages a rotation of the Administrator password, reviews it and applies it. */
async function applyAdminRotation(page: Page, culture: Culture) {
  const text = copy[culture];
  await credentialRow(page, "Administrator password")
    .getByRole("button", { name: text.rotate })
    .click();
  await page
    .getByRole("region", { name: text.barLabel })
    .getByRole("button", { name: text.review })
    .click();
  const dialog = page.getByRole("dialog", { name: text.review });
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: text.applyOne }).click();
  await expect(dialog).toBeHidden();
}

/** Opens Settings for a member who cannot reveal values and waits until the Administrator password row is there. */
async function openStagingCredentials(page: Page, culture: Culture, options: MockOptions = {}) {
  const api = await mockApi(page, culture, {
    secrets: stagingSecrets,
    canRevealSecrets: false,
    ...options,
  });
  await mockFleetStream(page);
  await openWorkspace(page, "?tab=settings");
  await expect(credentialRow(page, "Administrator password")).toBeVisible();
  return api;
}

for (const culture of ["en", "de"] as const) {
  for (const viewport of viewports) {
    test.describe(`credential values without reveal (${culture}, ${viewport.width}x${viewport.height})`, () => {
      test.use({ viewport, permissions: ["clipboard-read", "clipboard-write"] });

      test("shows an applied rotation once with Copy value and leaves no trace", async ({
        page,
      }) => {
        const text = copy[culture];
        const console = collectConsole(page);
        const api = await openStagingCredentials(page, culture);
        const row = credentialRow(page, "Administrator password");

        await applyAdminRotation(page, culture);

        const once = row.locator("code.credential__code--once");
        await expect(once).toHaveText(rotatedSecret);
        await expect(row.getByRole("button", { name: text.copyValue })).toBeVisible();
        await expect(row.getByRole("button", { name: text.hide })).toBeVisible();
        await expect(page.getByRole("button", { name: text.reveal })).toHaveCount(0);
        expect(api.rotateRequests).toBe(1);
        await expectNoPersistentTrace(page, rotatedSecret, console);

        // Copy value puts the value on the clipboard.
        await row.getByRole("button", { name: text.copyValue }).click();
        await expect(row.getByRole("button", { name: text.copied })).toBeVisible();
        expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(rotatedSecret);
        await expectNoPersistentTrace(page, rotatedSecret, console);

        // Hide value removes it from the DOM too.
        await row.getByRole("button", { name: text.hide }).click();
        await expect(once).toHaveCount(0);
        await expect(row.getByText(text.masked, { exact: true })).toHaveCount(1);
        await expectNoTrace(page, rotatedSecret, console);

        // A value that is shown again after another rotation is gone after a reload.
        await applyAdminRotation(page, culture);
        await expect(once).toHaveText(rotatedSecret);
        expect(api.rotateRequests).toBe(2);
        await page.reload();
        await expect(credentialRow(page, "Administrator password")).toBeVisible();
        await expect(once).toHaveCount(0);
        await expectNoTrace(page, rotatedSecret, console);
        expect(api.unexpected).toEqual([]);
      });

      test("sends a typed chosen value once as a member-provided value", async ({ page }) => {
        const text = copy[culture];
        const console = collectConsole(page);
        const api = await openStagingCredentials(page, culture);
        const row = credentialRow(page, "Administrator password");
        const input = row.getByLabel(text.valueLabel("Administrator password"));

        await input.fill(chosenSecret);
        await row.getByRole("button", { name: text.showTyped("Administrator password") }).click();
        await expect(input).toHaveAttribute("type", "text");
        await expect(input).toHaveValue(chosenSecret);
        await page
          .getByRole("region", { name: text.barLabel })
          .getByRole("button", { name: text.review })
          .click();
        const dialog = page.getByRole("dialog", { name: text.review });
        await expect(dialog.getByText(text.secretChanged, { exact: true })).toBeVisible();
        expect(await dialog.innerHTML()).not.toContain(chosenSecret);
        expect(await dialog.innerText()).not.toContain(chosenSecret);

        await dialog.getByRole("button", { name: text.applyOne }).click();
        await expect(dialog).toBeHidden();

        expect(api.secretWrites).toEqual([
          { key: "admin-password", value: chosenSecret, origin: "UserProvided" },
        ]);
        expect(api.rotateRequests).toBe(0);
        await expect(input).toHaveValue("");
        await expect(row.locator("code.credential__code--once")).toHaveCount(0);
        await expectNoTrace(page, chosenSecret, console);
        expect(api.unexpected).toEqual([]);
      });

      test("keeps the review closed for a value that is not a token", async ({ page }) => {
        const text = copy[culture];
        const api = await openStagingCredentials(page, culture);
        const row = credentialRow(page, "BattlEye RCON password");
        const input = row.getByLabel(text.valueLabel("BattlEye RCON password"));

        await input.fill("two words");
        await input.blur();
        await expect(row.getByText(text.tokenError, { exact: true })).toBeVisible();
        await page
          .getByRole("region", { name: text.barLabel })
          .getByRole("button", { name: text.review })
          .click();

        await expect(page.getByRole("dialog", { name: text.review })).toHaveCount(0);
        await expect(page.getByRole("alert").filter({ hasText: text.invalidAlert })).toBeVisible();
        expect(api.secretWrites).toEqual([]);
        expect(api.unexpected).toEqual([]);
      });
    });
  }

  test.describe(`one-time value layout and accessibility (${culture})`, () => {
    test("wraps a long one-time value inside its code element at 320x568", async ({ page }) => {
      await page.setViewportSize({ width: 320, height: 568 });
      const longValue = `Gen3rated-long-${"y".repeat(160)}`;
      const api = await openStagingCredentials(page, culture, { rotateValue: longValue });

      await applyAdminRotation(page, culture);

      const once = credentialRow(page, "Administrator password").locator(
        "code.credential__code--once",
      );
      await expect(once).toHaveText(longValue);
      await expectNoHorizontalScroll(page);
      const box = await once.boundingBox();
      expect(box).not.toBeNull();
      expect(box!.x + box!.width).toBeLessThanOrEqual(320 + subpixelTolerance);
      expect(api.unexpected).toEqual([]);
    });

    test("has no axe violation on the credentials panel showing the one-time value at 390x844", async ({
      page,
    }) => {
      await page.setViewportSize({ width: 390, height: 844 });
      const api = await openStagingCredentials(page, culture);

      await applyAdminRotation(page, culture);

      await expect(page.locator("code.credential__code--once")).toHaveText(rotatedSecret);
      expect(await scan(page, ".credentials")).toEqual([]);
      expect(api.unexpected).toEqual([]);
    });

    test("focuses the one-time block in view above the pending bar at 390x844 and leaves no trace", async ({
      page,
    }) => {
      await page.setViewportSize({ width: 390, height: 844 });
      const text = copy[culture];
      const console = collectConsole(page);
      const api = await openStagingCredentials(page, culture);
      const row = credentialRow(page, "Administrator password");

      await applyAdminRotation(page, culture);

      const block = row.locator(".credential__value");
      await expect(row.locator("code.credential__code--once")).toHaveText(rotatedSecret);
      await expect(block).toBeFocused();
      await expect(block).toHaveAttribute("tabindex", "-1");
      await expect(block).toHaveAccessibleName(text.onceLabel);
      expect(
        await block.evaluate((element) => {
          const style = getComputedStyle(element);
          return style.outlineStyle !== "none" && parseFloat(style.outlineWidth) > 0;
        }),
      ).toBe(true);
      // The focus move scrolls the block into view smoothly; measure it only once the page has stopped moving.
      await settleScroll(page);
      const box = await block.boundingBox();
      expect(box).not.toBeNull();
      expect(box!.y).toBeGreaterThanOrEqual(0);
      expect(box!.y + box!.height).toBeLessThanOrEqual(844);
      const bar = page.getByRole("region", { name: text.barLabel });
      if ((await bar.count()) > 0) {
        const barBox = await bar.boundingBox();
        expect(box!.y + box!.height).toBeLessThanOrEqual(barBox!.y + subpixelTolerance);
      }
      // The value shows in its row only: one element holds it, and nothing persistent or logged does.
      await expect(page.getByText(rotatedSecret)).toHaveCount(1);
      await expectNoPersistentTrace(page, rotatedSecret, console);
      expect(api.unexpected).toEqual([]);
    });
  });
}
