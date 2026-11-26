import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { fleetAction, fleetServer } from "../../helpers/fleetFixtures";
import {
  collectAxe,
  collectFocusRings,
  collectHeadings,
  collectReflow,
  collectTextSpacing,
  type AuditFinding,
  type AxeViolationFinding,
  type FocusRingSample,
} from "../support/a11y";
import { installApp, jsonHandler, type Culture } from "../support/mockApi";
import { fulfillMetricsRoute } from "../support/metricsMocks";

/**
 * The opt-in accessibility discovery pass (research finding F13).
 *
 * It visits every hosting route (with sample ids), every workspace tab and four non-server smoke routes with permissive
 * mocks, and records what axe at WCAG 2.2 AA, the reflow check, the text-spacing check, the heading check and the
 * focus ring sample of the first ten tab stops find. It never asserts a finding; it fails only when a page throws.
 * The findings go to `test-results/a11y-discovery-<project>.json`, and the table in the plan summary is built from
 * them, so the fix plans of the next wave start from a recorded list instead of finding issues in their own gate.
 *
 * Run it with `UTF_A11Y_DISCOVERY=1 npm run test:e2e -- tests/e2e/audit/a11y-discovery.spec.ts`. The hosting routes
 * and workspace tabs are read from the app source as text, because spec files never import the app at runtime.
 */

const discoveryEnabled = process.env["UTF_A11Y_DISCOVERY"] === "1";

const serverFixture = fleetServer({
  index: 1,
  title: "Alpha Ops",
  status: "Running",
  actions: [fleetAction("stop")],
});
const stoppedFixture = fleetServer({ index: 2, title: "Bravo Training" });
const serverId = serverFixture.primaryKey;
const sampleGuid = "5a3b0000-0000-4000-8000-000000000001";

/**
 * Reads a source file of the app as text.
 *
 * @param relativePath The path below `src`.
 * @returns The file content.
 */
function readSource(relativePath: string): string {
  return readFileSync(
    fileURLToPath(new URL(`../../../src/${relativePath}`, import.meta.url)),
    "utf8",
  );
}

/** `/command` (routed in `App.tsx`) plus the patterns of `hostingRouteDefinitions` in `routes.ts`. */
const hostingPatterns = [
  "/command",
  ...readSource("features/hosting/routes.ts").matchAll(/manifestPattern:\s*"([^"]+)"/g),
].map((match) => match[1] ?? "");

/** The workspace tab ids of `workspaceTabIds`, read from `src/features/hosting/workspaceSections.ts`. */
const workspaceTabs = [
  ...(
    /workspaceTabIds[^=]*=\s*\[([^\]]+)\]/.exec(
      readSource("features/hosting/workspaceSections.ts"),
    )?.[1] ?? ""
  ).matchAll(/"([a-z]+)"/g),
].map((match) => match[1] ?? "");

/**
 * Fills the route parameters of a manifest pattern with sample ids.
 *
 * @param pattern The manifest pattern, for example `/command/servers/{ServerId:guid}`.
 * @returns The in-app path without a leading slash.
 */
function samplePath(pattern: string): string {
  return pattern
    .replace("{ServerId:guid}", serverId)
    .replace(/\{[A-Za-z]+:guid\}/g, sampleGuid)
    .replace(/\{[A-Za-z]+:long\}/g, "1")
    .replace(/\{[A-Za-z]+\}/g, "home")
    .replace(/^\//, "");
}

/** One page the pass visits. */
interface DiscoveryPage {
  /** The label of the page in the findings file. */
  label: string;
  /** The in-app path without a leading slash. */
  path: string;
}

const hostingPages: DiscoveryPage[] = hostingPatterns.map((pattern) => ({
  label: pattern,
  path: samplePath(pattern),
}));
const tabPages: DiscoveryPage[] = workspaceTabs
  .filter((tab) => tab !== "overview")
  .map((tab) => ({
    label: `/command/servers/{ServerId:guid}?tab=${tab}`,
    path: `command/servers/${serverId}?tab=${tab}`,
  }));
const smokePages: DiscoveryPage[] = [
  { label: "smoke: members list /command/members", path: "command/members" },
  { label: "smoke: role detail /command/roles/{RoleId}", path: "command/roles/1" },
  {
    label: "smoke: public page editor /command/content/pages/home/edit",
    path: "command/content/pages/home/edit",
  },
  { label: "smoke: home /", path: "" },
];
const pages = [...hostingPages, ...tabPages, ...smokePages];

/** What the pass recorded for one page and culture. */
interface DiscoveryRecord {
  page: string;
  culture: Culture;
  project: string;
  axe: AxeViolationFinding[];
  reflow: AuditFinding[];
  textSpacing: AuditFinding[];
  headings: AuditFinding[];
  focusRings: FocusRingSample[];
  unansweredRequests: string[];
  pageErrors: string[];
  error: string | null;
}

test.describe("accessibility discovery (opt-in, non-gating)", () => {
  test.skip(!discoveryEnabled, "Set UTF_A11Y_DISCOVERY=1 to run the discovery pass.");
  // One worker per project visits every page in order, so the findings can be written as one file per project.
  test.describe.configure({ mode: "serial" });

  const records: DiscoveryRecord[] = [];

  test("the pass found pages to visit", () => {
    expect(hostingPatterns.length).toBeGreaterThanOrEqual(11);
    expect(workspaceTabs.length).toBeGreaterThanOrEqual(5);
  });

  for (const culture of ["en", "de"] as const) {
    for (const entry of pages) {
      test(`${entry.label} (${culture})`, async ({ page }, testInfo) => {
        const record: DiscoveryRecord = {
          page: entry.label,
          culture,
          project: testInfo.project.name,
          axe: [],
          reflow: [],
          textSpacing: [],
          headings: [],
          focusRings: [],
          unansweredRequests: [],
          pageErrors: [],
          error: null,
        };
        records.push(record);
        page.on("pageerror", (error) => record.pageErrors.push(error.message));
        try {
          const session = await installApp(page, {
            culture,
            claims: ["adm", "sv", "vrf", "usr"],
            permissive: true,
            stream: true,
            handlers: [
              jsonHandler("GET", "/api/game-servers", [serverFixture, stoppedFixture]),
              jsonHandler("GET", `/api/game-servers/${serverId}`, serverFixture),
              jsonHandler(
                "GET",
                new RegExp(`^/api/game-servers/${serverId}/(jobs|operations)$`),
                [],
              ),
              (route, method, path) => fulfillMetricsRoute(route, method, path, serverId),
            ],
          });
          await page.goto(entry.path);
          await page.locator("main").first().waitFor({ timeout: 15_000 });
          await page.waitForLoadState("networkidle").catch(() => undefined);
          record.axe = (await collectAxe(page)).violations;
          record.headings = await collectHeadings(page);
          record.reflow = await collectReflow(page);
          record.textSpacing = await collectTextSpacing(page);
          record.focusRings = await collectFocusRings(page, 10);
          record.unansweredRequests = [...new Set(session.served)];
        } catch (error) {
          record.error = error instanceof Error ? error.message : String(error);
        }
      });
    }
  }

  test("write the findings file", () => {
    const testInfo = test.info();
    mkdirSync(testInfo.project.outputDir, { recursive: true });
    const file = join(testInfo.project.outputDir, `a11y-discovery-${testInfo.project.name}.json`);
    writeFileSync(file, JSON.stringify(records, null, 2));
    testInfo.annotations.push({ type: "findings-file", description: file });
    // The pass fails only when a page threw, never because of a finding.
    const thrown = records.filter((record) => record.error !== null);
    expect(
      thrown.map((record) => `${record.page} (${record.culture}): ${record.error}`),
      "pages that threw during discovery",
    ).toEqual([]);
  });
});
