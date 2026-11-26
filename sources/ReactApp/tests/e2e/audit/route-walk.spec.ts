import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { expect, test, type Page, type TestInfo } from "@playwright/test";
import { navLabelContract } from "../../support/navLabelContract";
import { installApp, type Culture, type MockSession } from "../support/mockApi";
import {
  routeFixtures,
  sampleGuid,
  sampleLong,
  sampleTextParameters,
  type RouteFixture,
} from "../support/routeFixtures";

/**
 * The route walk (VER-01, CON-01, CON-03, D-15, D-AUTO-3, D-AUTO-5).
 *
 * The walk reads the shared route manifest (`sources/Common/Ui/routes.json`, the same file `routeManifest` in
 * `src/app/routes.ts` exposes) and opens every certified pattern in the browser with minimal per-route fixtures
 * (`routeFixtures.ts`). Each page must load without a page error, must not be the 404 page or the certification
 * fallback, must show exactly one `h1` that is the first heading, and must mark its navigation item `aria-current`.
 * The three uncertified routes must hand off to the classic app on the same origin. A second pass opens every detail
 * route with an unknown id against permissive 404 mocks and asserts one `h1` above the state. The navigation pass
 * asserts that the `h1` of every command center destination equals its navigation label (except the exceptions
 * recorded in the label contract) in both cultures, and the phone drawer pass asserts that German labels stay whole
 * at 320 CSS pixels.
 *
 * The community navigation targets are marketing heroes (`PublicHero`, E-01): they keep their own titles, so the
 * label equality applies to the command center, the destination of the label contract's recorded exceptions.
 */

/** One entry of the route manifest. */
interface RouteManifestEntry {
  id: string;
  canonical: string;
  aliases: string[];
  certified: boolean;
  shell: "community" | "command";
}

/** The route manifest the app serves; the walk is driven by exactly this list. */
const routeManifest = JSON.parse(
  readFileSync(
    fileURLToPath(new URL("../../../../Common/Ui/routes.json", import.meta.url)),
    "utf8",
  ),
) as { routes: RouteManifestEntry[] };

/** Every pattern of a manifest entry, the canonical one first. */
function patternsOf(route: RouteManifestEntry): string[] {
  return [route.canonical, ...route.aliases];
}

/**
 * Fills the parameters of a manifest pattern with sample values.
 *
 * @param pattern A manifest pattern such as `/command/events/{EventId:guid}`.
 * @returns The sample path, for example `/command/events/0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d`.
 */
function samplePath(pattern: string): string {
  return pattern.replace(
    /\{([^}:]+)(?::([^}]+))?\}/g,
    (_match, name: string, kind: string | undefined) =>
      kind === "guid"
        ? sampleGuid
        : kind === "long"
          ? sampleLong
          : (sampleTextParameters[name] ?? "sample-value"),
  );
}

/** The certified manifest entries; the walk opens each of their patterns. */
const certifiedRoutes = routeManifest.routes.filter((route) => route.certified);

/** The uncertified manifest entries; the walk asserts the classic hand-off for each of their patterns. */
const uncertifiedRoutes = routeManifest.routes.filter((route) => !route.certified);

/** The English and German title of the 404 page, which no certified route may show. */
const notFoundTitles = (["en", "de"] as const).map((culture) => {
  const locale = JSON.parse(
    readFileSync(
      fileURLToPath(new URL(`../../../src/locales/${culture}.json`, import.meta.url)),
      "utf8",
    ),
  ) as Record<string, string>;
  return locale.React_NotFound_Title as string;
});

/**
 * Hosting and page defects the walk found but a plan may not fix itself, keyed by `routeId` and the state the
 * walk was in (`loaded` or `not-found`). The walk subtracts the recorded number of extra `h1` elements and adds a
 * test annotation, so the finding stays visible in every report until the owner removes the record.
 */
interface KnownRouteFinding {
  /** The tracking id of the finding; it appears in the test annotation. */
  id: string;
  /** What is wrong and who owns the fix. */
  reason: string;
  /** How many `h1` elements beyond the first one the page renders because of the finding. */
  extraH1: number;
}

const knownRouteFindings: Readonly<Record<string, KnownRouteFinding>> = {};

/** The command center navigation destinations in the order of the navigation, with their contract label key. */
const commandDestinations: readonly { key: string; to: string }[] = [
  { key: "Shared_CommandCenter_Nav_Overview", to: "/command" },
  { key: "Hosting_Nav_Servers", to: "/command/servers" },
  { key: "Shared_CommandCenter_Nav_CloudConfiguration", to: "/command/configuration-profiles" },
  { key: "Shared_CommandCenter_Nav_Events", to: "/command/events" },
  { key: "Shared_CommandCenter_Nav_HostingFinance", to: "/command/hosting-finance" },
  { key: "Shared_CommandCenter_Nav_ModPacks", to: "/command/mod-packs" },
  { key: "Shared_CommandCenter_Nav_Terrains", to: "/command/terrains" },
  { key: "Shared_CommandCenter_Nav_FieldReports", to: "/command/field-reports" },
  { key: "Shared_CommandCenter_Nav_Gallery", to: "/command/content/gallery" },
  { key: "Shared_CommandCenter_Nav_PublicImages", to: "/command/content/images/home" },
  { key: "Shared_CommandCenter_Nav_Members", to: "/command/members" },
  { key: "Shared_CommandCenter_Nav_Roles", to: "/command/roles" },
  { key: "Shared_CommandCenter_Nav_Claims", to: "/command/claims" },
  { key: "Shared_CommandCenter_Nav_ContentAndDocuments", to: "/command/content" },
  {
    key: "Shared_CommandCenter_Nav_VereinMembershipRequests",
    to: "/command/verein/membership-requests",
  },
  { key: "Shared_CommandCenter_Nav_VereinDocuments", to: "/command/verein/documents" },
  { key: "Shared_CommandCenter_Nav_VereinKassenbuch", to: "/command/verein/kassenbuch" },
  { key: "Shared_CommandCenter_Nav_VereinSettings", to: "/command/verein/settings" },
  { key: "Shared_CommandCenter_Nav_Settings", to: "/command/settings" },
];

/**
 * The navigation destination each command center route id belongs to, as the contract label key. A route id that is
 * missing here fails the walk, so a route added to the manifest has to declare where it sits in the navigation.
 */
const commandNavigationOfRoute: Readonly<Record<string, string>> = {
  command: "Shared_CommandCenter_Nav_Overview",
  servers: "Hosting_Nav_Servers",
  server: "Hosting_Nav_Servers",
  "server-create": "Hosting_Nav_Servers",
  "server-cost-alerts": "Hosting_Nav_Servers",
  "configuration-profiles": "Shared_CommandCenter_Nav_CloudConfiguration",
  "configuration-profile": "Shared_CommandCenter_Nav_CloudConfiguration",
  "configuration-profile-create": "Shared_CommandCenter_Nav_CloudConfiguration",
  "command-events": "Shared_CommandCenter_Nav_Events",
  "command-event-create": "Shared_CommandCenter_Nav_Events",
  "command-event-edit": "Shared_CommandCenter_Nav_Events",
  "command-hosting-finance": "Shared_CommandCenter_Nav_HostingFinance",
  "command-hosting-finance-account": "Shared_CommandCenter_Nav_HostingFinance",
  "command-hosting-finance-settings": "Shared_CommandCenter_Nav_HostingFinance",
  "mod-packs": "Shared_CommandCenter_Nav_ModPacks",
  "mod-pack": "Shared_CommandCenter_Nav_ModPacks",
  "mod-pack-create": "Shared_CommandCenter_Nav_ModPacks",
  terrains: "Shared_CommandCenter_Nav_Terrains",
  terrain: "Shared_CommandCenter_Nav_Terrains",
  "terrain-create": "Shared_CommandCenter_Nav_Terrains",
  "command-field-reports": "Shared_CommandCenter_Nav_FieldReports",
  "command-field-report-edit": "Shared_CommandCenter_Nav_FieldReports",
  "command-content-gallery": "Shared_CommandCenter_Nav_Gallery",
  "command-content-images": "Shared_CommandCenter_Nav_PublicImages",
  "command-members": "Shared_CommandCenter_Nav_Members",
  "command-member": "Shared_CommandCenter_Nav_Members",
  "command-roles": "Shared_CommandCenter_Nav_Roles",
  "command-role": "Shared_CommandCenter_Nav_Roles",
  "command-claims": "Shared_CommandCenter_Nav_Claims",
  "command-content": "Shared_CommandCenter_Nav_ContentAndDocuments",
  "command-content-pages": "Shared_CommandCenter_Nav_ContentAndDocuments",
  "command-content-page": "Shared_CommandCenter_Nav_ContentAndDocuments",
  "command-content-page-edit": "Shared_CommandCenter_Nav_ContentAndDocuments",
  "command-membership-requests": "Shared_CommandCenter_Nav_VereinMembershipRequests",
  "command-verein-documents": "Shared_CommandCenter_Nav_VereinDocuments",
  "command-verein-document": "Shared_CommandCenter_Nav_VereinDocuments",
  "command-verein-document-create": "Shared_CommandCenter_Nav_VereinDocuments",
  "command-verein-document-edit": "Shared_CommandCenter_Nav_VereinDocuments",
  "command-verein-kassenbuch": "Shared_CommandCenter_Nav_VereinKassenbuch",
  "command-verein-kassenbuch-create": "Shared_CommandCenter_Nav_VereinKassenbuch",
  "command-verein-kassenbuch-edit": "Shared_CommandCenter_Nav_VereinKassenbuch",
  "command-verein-settings": "Shared_CommandCenter_Nav_VereinSettings",
  "command-settings": "Shared_CommandCenter_Nav_Settings",
};

/** The community navigation targets in the order of the layout; a link is current for its path and sub paths. */
const communityTargets = [
  "/",
  "/events/upcoming",
  "/unit",
  "/verein",
  "/wiki",
  "/field-reports",
  "/join",
] as const;

/**
 * Returns the number of community navigation links that react-router marks current for a path.
 *
 * @param path The sample path of the page.
 * @returns 1 when exactly one navigation target is the path or a parent of it, otherwise 0.
 */
function expectedCommunityCurrent(path: string): number {
  return communityTargets.filter((target) =>
    target === "/" ? path === "/" : path === target || path.startsWith(`${target}/`),
  ).length;
}

/** The viewport width in CSS pixels of the `mobile-320` project, the only one that runs the drawer test. */
const narrowestPhoneWidth = 320;

/** The ready timeout of a page: the preview build lazy-loads every route chunk and CI is slow. */
const readyTimeout = 20_000;

/** The result of reading the headings of a loaded page. */
interface HeadingReport {
  /** The tag level of every visible heading in document order. */
  levels: number[];
  /** The text of the first `h1`. */
  h1Text: string;
  /** Whether a state panel precedes the first `h1` in the document. */
  stateBeforeH1: boolean;
  /** The number of state panels of the error and forbidden kind. */
  failedPanels: number;
}

/**
 * Opens a path and waits until the shell and the page body have rendered and no loading panel is left.
 *
 * @param page The page to navigate.
 * @param path The app path, starting with a slash.
 * @param shell The shell the route renders in.
 */
async function openAndSettle(
  page: Page,
  path: string,
  shell: "community" | "command",
): Promise<void> {
  await page.goto(path.replace(/^\//, ""));
  await expect(
    page.locator(shell === "command" ? "#command-navigation" : "#main-content"),
  ).toBeAttached({ timeout: readyTimeout });
  await expect(page.getByRole("heading", { level: 1 }).first()).toBeVisible({
    timeout: readyTimeout,
  });
  const loading = page.locator(".state-panel--loading:visible");
  await expect(loading).toHaveCount(0, { timeout: readyTimeout });
  await page.waitForLoadState("networkidle");
  await expect(loading).toHaveCount(0);
}

/**
 * Reads the visible headings of the page in document order.
 *
 * @param page The settled page.
 * @returns The levels, the first `h1` text and the state panel facts.
 */
async function readHeadings(page: Page): Promise<HeadingReport> {
  return page.evaluate(() => {
    const visible = (element: Element) => {
      const style = getComputedStyle(element);
      return (
        style.display !== "none" &&
        style.visibility !== "hidden" &&
        element.getClientRects().length > 0 &&
        element.closest("[aria-hidden='true'], [hidden], dialog:not([open])") === null
      );
    };
    const headings = [
      ...document.querySelectorAll("h1, h2, h3, h4, h5, h6, [role='heading']"),
    ].filter(visible);
    const levelOf = (element: Element) =>
      Number(element.getAttribute("aria-level") ?? element.tagName.slice(1)) || 2;
    const firstH1 = headings.find((heading) => levelOf(heading) === 1);
    const panel = document.querySelector(".state-panel");
    return {
      levels: headings.map(levelOf),
      h1Text: (firstH1?.textContent ?? "").replace(/\s+/g, " ").trim(),
      stateBeforeH1:
        panel !== null &&
        firstH1 !== undefined &&
        Boolean(panel.compareDocumentPosition(firstH1) & Node.DOCUMENT_POSITION_FOLLOWING),
      failedPanels: document.querySelectorAll(".state-panel--error, .state-panel--forbidden")
        .length,
    };
  });
}

/**
 * Asserts the one-h1 rule of a settled page: exactly one `h1` (plus the recorded known extras) and it is the first
 * heading.
 *
 * @param page The settled page.
 * @param label What is checked, for the failure message.
 * @param known The recorded finding of the route and state, when there is one.
 * @param testInfo The running test, which receives the annotation of a subtracted finding.
 * @returns The heading report.
 */
async function expectOneLeadingH1(
  page: Page,
  label: string,
  known: KnownRouteFinding | undefined,
  testInfo: TestInfo,
): Promise<HeadingReport> {
  const report = await readHeadings(page);
  const h1Count = report.levels.filter((level) => level === 1).length;
  const allowed = 1 + (known?.extraH1 ?? 0);
  if (known !== undefined && h1Count === allowed && allowed > 1) {
    testInfo.annotations.push({
      type: "known-route-finding",
      description: `${known.id} ${label}: ${known.reason}`,
    });
  } else {
    expect(h1Count, `${label} has exactly one h1`).toBe(1);
  }
  expect(report.levels[0], `${label} starts with the h1`).toBe(1);
  return report;
}

/**
 * Installs the strict fixture mock of one certified route.
 *
 * @param page The page to mock.
 * @param fixture The fixture of the route.
 * @param culture The culture of the page.
 * @returns The record of requests nothing answered.
 */
async function installFixture(
  page: Page,
  fixture: RouteFixture,
  culture: Culture,
): Promise<MockSession> {
  return installApp(page, {
    culture,
    claims: fixture.claims ?? ["adm"],
    handlers: fixture.handlers,
    stream: fixture.stream ?? false,
  });
}

/**
 * Counts the navigation links of the shell that carry `aria-current="page"`.
 *
 * @param page The settled page.
 * @param shell The shell the page renders in.
 * @returns The labels of the current links.
 */
async function currentNavigationLabels(
  page: Page,
  shell: "community" | "command",
): Promise<string[]> {
  const selector =
    shell === "command"
      ? "#command-navigation nav a[aria-current='page']"
      : "#community-navigation a[aria-current='page']";
  return page
    .locator(selector)
    .evaluateAll((links) =>
      links.map((link) => (link.textContent ?? "").replace(/\s+/g, " ").trim()),
    );
}

test.describe("route walk: certified routes", () => {
  test.skip(
    ({ viewport }) => viewport?.width === narrowestPhoneWidth,
    "The phone walk runs at 390; 320 only runs the drawer test.",
  );

  for (const route of certifiedRoutes) {
    test(`${route.id} renders one leading h1 and its current navigation item`, async ({
      page,
    }, testInfo) => {
      const fixture = routeFixtures[route.id];
      if (fixture === undefined) {
        throw new Error(`Certified route ${route.id} has no fixture in routeFixtures.ts.`);
      }
      const session = await installFixture(page, fixture, "en");
      const pageErrors: string[] = [];
      page.on("pageerror", (error) => pageErrors.push(error.message));
      const visited: string[] = [];

      const visits = [
        ...patternsOf(route).map((pattern) => ({
          pattern,
          path: samplePath(pattern),
          extra: false,
        })),
        ...(fixture.extraPaths ?? []).map((path) => ({ pattern: path, path, extra: true })),
      ];
      for (const { pattern, path, extra } of visits) {
        await test.step(`${pattern} -> ${path}`, async () => {
          await openAndSettle(page, path, route.shell);
          if (!extra) visited.push(pattern);

          await expect(page.locator(".route-certification-fallback")).toHaveCount(0);
          for (const title of notFoundTitles) await expect(page.getByText(title)).toHaveCount(0);
          const report = await expectOneLeadingH1(
            page,
            `${route.id} ${pattern}`,
            knownRouteFindings[`${route.id}:loaded`],
            testInfo,
          );
          expect(report.failedPanels, `${pattern} shows an error or forbidden panel`).toBe(0);
          if (fixture.h1 !== undefined) {
            expect(report.h1Text, `${pattern} h1 proves the entity loaded`).toMatch(fixture.h1);
          }

          const current = await currentNavigationLabels(page, route.shell);
          if (route.shell === "command") {
            const key = commandNavigationOfRoute[route.id];
            if (key === undefined) {
              throw new Error(`Command route ${route.id} is missing in commandNavigationOfRoute.`);
            }
            const entry = navLabelContract.find((candidate) => candidate.key === key);
            expect(current, `${pattern} marks its navigation item`).toEqual([entry?.en]);
          } else {
            expect(current.length, `${pattern} community navigation current items`).toBe(
              expectedCommunityCurrent(path),
            );
          }
        });
      }

      expect(visited, `${route.id} visited every pattern`).toEqual(patternsOf(route));
      expect(session.unexpected).toEqual([]);
      expect(pageErrors).toEqual([]);
    });
  }

  test("every certified route has a fixture and the walk plans every certified pattern", () => {
    const planned = certifiedRoutes.flatMap(patternsOf);
    const manifestPatterns = routeManifest.routes
      .filter((route) => route.certified)
      .flatMap((route) => [route.canonical, ...route.aliases]);
    expect(planned).toEqual(manifestPatterns);
    expect(planned.length).toBeGreaterThan(70);
    expect(new Set(planned).size).toBe(planned.length);
    for (const route of certifiedRoutes) expect(routeFixtures[route.id]).toBeDefined();
    expect(Object.keys(routeFixtures).sort()).toEqual(
      certifiedRoutes.map((route) => route.id).sort(),
    );
    for (const route of certifiedRoutes.filter((candidate) => candidate.shell === "command")) {
      expect(commandNavigationOfRoute[route.id], route.id).toBeDefined();
    }
  });
});

test.describe("route walk: uncertified routes", () => {
  test.skip(
    ({ viewport }) => viewport?.width === narrowestPhoneWidth,
    "The phone walk runs at 390.",
  );

  test("the three uncertified routes are field report, GDPR and Verein ledger", () => {
    expect(uncertifiedRoutes.map((route) => route.id).sort()).toEqual([
      "field-report",
      "gdpr",
      "verein-ledger",
    ]);
  });

  for (const route of uncertifiedRoutes) {
    test(`${route.id} redirects to the classic app on the same origin`, async ({
      page,
    }, testInfo) => {
      const session = await installApp(page, { culture: "en" });
      const baseOrigin = new URL(String(testInfo.project.use.baseURL)).origin;
      const origins = new Set<string>();
      page.on("request", (request) => {
        if (request.isNavigationRequest()) origins.add(new URL(request.url()).origin);
      });

      for (const pattern of patternsOf(route)) {
        const path = samplePath(pattern);
        await test.step(`${pattern} -> ${path}`, async () => {
          await page.goto(path.replace(/^\//, ""));
          await expect(page.getByTestId("classic-shell")).toBeVisible({ timeout: readyTimeout });
          const landed = new URL(page.url());
          expect(landed.origin, "the classic target is the configured origin").toBe(baseOrigin);
          expect(landed.pathname, "the classic path replaces the React path").toBe(path);
          await expect(page.getByRole("heading", { level: 1 })).toHaveCount(0);
        });
      }

      expect([...origins].filter((origin) => origin !== baseOrigin)).toEqual([]);
      expect(session.unexpected).toEqual([]);
    });
  }
});

/** The routes with an identifier parameter; an unknown id walks their not-found state. */
const detailRoutes = certifiedRoutes.filter((route) =>
  patternsOf(route).some((pattern) => /\{[^}:]+:(guid|long)\}|\{Slug\}/.test(pattern)),
);

test.describe("route walk: not-found states", () => {
  test.skip(
    ({ viewport }) => viewport?.width === narrowestPhoneWidth,
    "The phone walk runs at 390.",
  );

  for (const route of detailRoutes) {
    test(`${route.id} shows exactly one h1 above its not-found state`, async ({
      page,
    }, testInfo) => {
      const session = await installApp(page, {
        culture: "en",
        claims: ["adm"],
        permissive: true,
        stream: routeFixtures[route.id]?.stream ?? false,
      });
      const pageErrors: string[] = [];
      page.on("pageerror", (error) => pageErrors.push(error.message));

      for (const pattern of patternsOf(route).filter((candidate) => candidate.includes("{"))) {
        const path = samplePath(pattern);
        await test.step(`${pattern} -> ${path}`, async () => {
          await openAndSettle(page, path, route.shell);
          const report = await expectOneLeadingH1(
            page,
            `${route.id} ${pattern} (not found)`,
            knownRouteFindings[`${route.id}:not-found`],
            testInfo,
          );
          if (report.failedPanels > 0 || (await page.locator(".state-panel").count()) > 0) {
            expect(report.stateBeforeH1, `${pattern} h1 sits above the state panel`).toBe(false);
          }
        });
      }

      expect(pageErrors).toEqual([]);
      expect(session.unexpected).toEqual([]);
    });
  }

  test("the not-found pass visits the detail routes of every shell", () => {
    expect(detailRoutes.length).toBeGreaterThan(15);
    expect(detailRoutes.some((route) => route.shell === "community")).toBe(true);
    expect(detailRoutes.some((route) => route.shell === "command")).toBe(true);
  });
});

test.describe("route walk: navigation labels", () => {
  test.skip(
    ({ viewport }) => viewport?.width === narrowestPhoneWidth,
    "The phone walk runs at 390.",
  );

  test("the command navigation lists exactly the destinations of the label contract", async ({
    page,
  }) => {
    await installFixture(page, routeFixtures.command as RouteFixture, "en");
    await openAndSettle(page, "/command", "command");
    const links = await page.locator("#command-navigation nav a").evaluateAll((anchors) =>
      anchors.map((anchor) => ({
        href: new URL((anchor as HTMLAnchorElement).href).pathname.replace(/^\/next/, ""),
        text: (anchor.textContent ?? "").replace(/\s+/g, " ").trim(),
      })),
    );
    expect(links).toEqual(
      commandDestinations.map((destination) => ({
        href: destination.to,
        text: navLabelContract.find((entry) => entry.key === destination.key)?.en,
      })),
    );
  });

  for (const culture of ["en", "de"] as const) {
    for (const destination of commandDestinations) {
      const entry = navLabelContract.find((candidate) => candidate.key === destination.key);
      if (entry === undefined) throw new Error(`${destination.key} is not in the label contract.`);
      test(`${destination.to} (${culture}) opens a page whose h1 is its label`, async ({
        page,
      }, testInfo) => {
        const route = certifiedRoutes.find((candidate) =>
          patternsOf(candidate).some((pattern) => samplePath(pattern) === destination.to),
        );
        const fixture = route === undefined ? undefined : routeFixtures[route.id];
        if (fixture === undefined) throw new Error(`No fixture for ${destination.to}.`);
        const session = await installFixture(page, fixture, culture);
        const pageErrors: string[] = [];
        page.on("pageerror", (error) => pageErrors.push(error.message));

        await openAndSettle(page, destination.to, "command");
        const label = entry[culture];
        const link = page.locator("#command-navigation nav a", {
          hasText: new RegExp(`^\\s*${label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\s*$`),
        });
        await expect(link, `the navigation shows ${label}`).toHaveAttribute("aria-current", "page");
        expect(await currentNavigationLabels(page, "command")).toEqual([label]);

        const report = await expectOneLeadingH1(page, destination.to, undefined, testInfo);
        if (entry.h1Exception === undefined) {
          expect(report.h1Text, `${destination.to} h1 equals its label`).toBe(label);
        } else {
          testInfo.annotations.push({
            type: "label-exception",
            description: `${entry.key}: ${entry.h1Exception}`,
          });
          expect(report.h1Text.length).toBeGreaterThan(0);
        }
        expect(session.unexpected).toEqual([]);
        expect(pageErrors).toEqual([]);
      });
    }
  }

  test("the community navigation marks the current destination", async ({ page }) => {
    await installFixture(page, routeFixtures.home as RouteFixture, "en");
    const expected: Record<string, string> = {
      "/events/upcoming": "Operations",
      "/unit": "Unit",
      "/verein": "Verein",
      "/wiki": "Wiki",
      "/field-reports": "Field reports",
      "/join": "Join",
    };
    for (const [path, label] of Object.entries(expected)) {
      await page.unrouteAll({ behavior: "ignoreErrors" });
      const fixtureRoute = routeManifest.routes.find((route) => route.canonical === path);
      await installFixture(page, routeFixtures[fixtureRoute?.id ?? "home"] as RouteFixture, "en");
      await openAndSettle(page, path, "community");
      expect(await currentNavigationLabels(page, "community")).toEqual([label]);
    }
  });
});

test.describe("route walk: German drawer labels", () => {
  test.skip(
    ({ viewport }) => viewport?.width !== narrowestPhoneWidth,
    "Only the narrowest phone opens the drawer.",
  );

  test("German navigation labels stay whole inside the 320px drawer", async ({ page }) => {
    const session = await installFixture(page, routeFixtures.command as RouteFixture, "de");
    await openAndSettle(page, "/command", "command");

    await page.getByRole("button", { name: /Navigation öffnen/ }).click();
    const drawer = page.locator("#command-navigation");
    await expect(drawer).toHaveClass(/command-nav--open/);

    const longest = ["Konfigurationsprofile", "Beitragseinstellungen"];
    for (const label of longest) {
      await expect(drawer.locator("nav a > span", { hasText: label })).toBeVisible();
    }

    const labels = await drawer.locator("nav a > span").evaluateAll((spans) =>
      spans.map((span) => {
        const link = span.parentElement as HTMLElement;
        const spanStyle = getComputedStyle(span);
        const linkStyle = getComputedStyle(link);
        return {
          text: (span.textContent ?? "").trim(),
          spanOverflow: span.scrollWidth - span.clientWidth,
          linkOverflow: link.scrollWidth - link.clientWidth,
          ellipsis: spanStyle.textOverflow === "ellipsis" || linkStyle.textOverflow === "ellipsis",
          wraps: spanStyle.whiteSpace !== "nowrap",
        };
      }),
    );
    for (const label of labels) {
      expect(label.ellipsis, `${label.text} has no ellipsis`).toBe(false);
      expect(label.wraps, `${label.text} may wrap`).toBe(true);
      expect(label.spanOverflow, `${label.text} fits its span`).toBeLessThanOrEqual(0);
      expect(label.linkOverflow, `${label.text} fits its link`).toBeLessThanOrEqual(0);
    }
    for (const label of longest) expect(labels.map((entry) => entry.text)).toContain(label);

    const pageOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(pageOverflow, "the open drawer causes no horizontal page scroll").toBeLessThanOrEqual(0);
    expect(session.unexpected).toEqual([]);
  });
});
