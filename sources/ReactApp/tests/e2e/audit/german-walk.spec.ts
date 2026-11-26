import { expect, test, type Page } from "@playwright/test";
import { fleetAction, fleetServer } from "../../helpers/fleetFixtures";
import { assertNoGermanLeak } from "../support/germanLeak";
import { fulfillMetricsRoute } from "../support/metricsMocks";
import { auditUserId, installApp, jsonHandler } from "../support/mockApi";
import { sliceOf } from "../support/screens/registryTypes";
import { serverScreens } from "../support/serverScreens";

/**
 * The registry-driven German walk (L10N-03, D-13).
 *
 * Every registry entry whose `audit.german` flag is set opens in German, visits each of its states and fails when a
 * visible text, `aria-label`, `title`, `placeholder`, `alt` or option label shows English (see `germanLeak.ts`). The
 * walk also asserts `html lang="de"` and an empty list of unexpected API requests. Titles end in the owning slice
 * (`@fleet`, `@workspace` or `@adjacent`), like the accessibility audit, and the spec runs in the desktop and both
 * phone projects because it lives under `audit`.
 *
 * A leak a plan may not fix itself is recorded as a known finding of the state with the rule `german-leak` and the
 * leaking text as target; the walk subtracts it and annotates the test.
 */

/** The rule id of a known finding that records a leaking text. */
const leakRule = "german-leak";

/** A leak recorded by this plan for a cause outside its files; it names the leaking text exactly. */
interface RecordedLeak {
  /** The tracking id of the recorded finding; it appears in the test annotation. */
  id: string;
  /** The leaking text as the page renders it. */
  text: string;
}

/**
 * Leaks of a registry entry that a plan may record instead of fixing, keyed by registry id. Every recorded cause has
 * been fixed, so the table is empty; a new record needs a tracked cause and an owner.
 */
const recordedLeaks: Readonly<Record<string, readonly RecordedLeak[]>> = {};

/** The entries the walk visits. */
const germanEntries = serverScreens.filter((entry) => entry.audit.german);

test.describe("german walk", () => {
  test("visits at least one registry entry", () => {
    expect(germanEntries.length).toBeGreaterThan(0);
  });

  test("detects English text in the browser (non-vacuity)", async ({ page }) => {
    const [entry] = germanEntries;
    if (entry === undefined) throw new Error("The registry has no German entry.");
    await entry.setup(page, "en");
    await page.goto(entry.route.replace(/^\//, ""));
    for (const state of entry.states) {
      await state.open?.(page);
      await state.ready(page);
    }
    // The English page declares lang="en", which the check treats as an excluded subtree; pose it as German.
    await page.evaluate(() => document.documentElement.setAttribute("lang", "de"));
    const failure = await assertNoGermanLeak(page).then(
      () => undefined,
      (error: unknown) => (error instanceof Error ? error.message : String(error)),
    );
    expect(failure).toMatch(/English text\(s\) on a German page/);
  });

  for (const entry of germanEntries) {
    const slice = sliceOf(entry.id);
    if (slice === undefined) {
      throw new Error(
        `Registry entry ${entry.id} is not in the required manifest (requiredRegistry).`,
      );
    }
    test(`${entry.id} (de) @${slice}`, async ({ page }) => {
      const session = await entry.setup(page, "de");
      const pageErrors: string[] = [];
      page.on("pageerror", (error) => pageErrors.push(error.message));

      await page.goto(entry.route.replace(/^\//, ""));
      for (const state of entry.states) {
        await test.step(`${entry.id} ${state.name}`, async () => {
          await state.open?.(page);
          await state.ready(page);
          await expect(page.locator("html")).toHaveAttribute("lang", "de");
          const known = (state.knownFindings ?? [])
            .filter((finding) => finding.rule === leakRule)
            .map((finding) => finding.target);
          const recorded = recordedLeaks[entry.id] ?? [];
          const subtracted = await assertNoGermanLeak(page, [
            ...known,
            ...recorded.map((leak) => leak.text),
          ]);
          for (const text of subtracted) {
            const id = recorded.find((leak) => leak.text.toLowerCase() === text.toLowerCase())?.id;
            test.info().annotations.push({
              type: "known-german-leak",
              description: `${id ?? leakRule} ${entry.id} ${state.name}: ${text}`,
            });
          }
        });
      }

      expect(session.unexpected).toEqual([]);
      expect(pageErrors).toEqual([]);
    });
  }
});

/**
 * Builds a fleet of running servers, which is what the cost line counts.
 *
 * @param count How many servers the fleet holds.
 * @returns Fresh server objects in the shape of `GET /api/game-servers`.
 */
function runningFleet(count: number) {
  return Array.from({ length: count }, (_, position) =>
    fleetServer({
      index: position + 1,
      title: `Gamma Ops ${position + 1}`,
      status: "Running",
      costOwnerUserId: auditUserId,
      actions: [fleetAction("stop")],
    }),
  );
}

/** The instant of the accrued cost the workspace shows; `Europe/Berlin` renders it as 21.11.26, 11:00. */
const accruedAsOf = "2026-11-21T10:00:00Z";

/**
 * Builds the detail read of a running, costing server, with the fields the workspace needs beyond the fleet shape.
 *
 * @returns The server body of `GET /api/game-servers/{id}`.
 */
function costedServer() {
  const server = fleetServer({
    index: 7,
    title: "Gamma Ops",
    status: "Running",
    costOwnerUserId: auditUserId,
    actions: [fleetAction("stop")],
  });
  return {
    ...server,
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

/**
 * Installs the strict mock of the costed workspace in German and opens it.
 *
 * @param page The page to mock and navigate.
 * @param query The query string to open the workspace with, such as `?tab=activity`; empty for the Overview tab.
 */
async function openCostedWorkspace(page: Page, query: string): Promise<void> {
  const server = costedServer();
  const id = server.primaryKey;
  await installApp(page, {
    culture: "de",
    stream: true,
    handlers: [
      jsonHandler("GET", `/api/game-servers/${id}`, server),
      jsonHandler("GET", `/api/game-servers/${id}/accrued-cost`, {
        gameServerId: id,
        session: {
          deploymentId: "50000000-0000-4000-8000-000000000b66",
          amount: 0.42,
          currency: "EUR",
          asOf: accruedAsOf,
          isFinal: false,
          unavailableReason: null,
          billingUnitSeconds: 3600,
          billedThrough: "2026-11-21T10:40:00Z",
        },
      }),
      jsonHandler("GET", `/api/game-servers/${id}/jobs`, []),
      jsonHandler("GET", `/api/game-servers/${id}/operations`, []),
      (route, method, path) => fulfillMetricsRoute(route, method, path, id),
    ],
  });
  await page.goto(`command/servers/${id}${query}`);
  await expect(page.getByRole("heading", { level: 1, name: server.title })).toBeVisible();
}

test.describe("german formatting", () => {
  test.use({ timezoneId: "Europe/Berlin" });

  test("the fleet count reads 1 Server and 2 Server through the _one and _other keys", async ({
    page,
  }) => {
    for (const [count, expected] of [
      [1, /^1 Server verursacht Kosten/],
      [2, /^2 Server verursachen Kosten/],
    ] as const) {
      const session = await installApp(page, {
        culture: "de",
        stream: true,
        handlers: [jsonHandler("GET", "/api/game-servers", runningFleet(count))],
      });
      await page.goto("command/servers");
      await expect(page.locator("[data-server-card]")).toHaveCount(count);
      await expect(page.locator(".fleet-cost-line__text")).toHaveText(expected);
      expect(session.unexpected).toEqual([]);
      await page.unrouteAll({ behavior: "ignoreErrors" });
    }
  });

  test("the accrued cost reads like 0,42 € and its time uses the German short date", async ({
    page,
  }) => {
    await openCostedWorkspace(page, "");
    const row = page.locator('[data-slot="accrued-cost"]');
    await expect(row.locator(".accrued-cost__value")).toHaveText(/^0,42\s€$/);
    const expectedTime = new Intl.DateTimeFormat("de-DE", {
      dateStyle: "short",
      timeStyle: "short",
      timeZone: "Europe/Berlin",
    }).format(new Date(accruedAsOf));
    await expect(row.locator("time").first()).toHaveAttribute("datetime", accruedAsOf);
    await expect(row.locator("time").first()).toHaveText(`Stand ${expectedTime}`);
  });

  test("the activity badge reads a German relative time", async ({ page }) => {
    await openCostedWorkspace(page, "?tab=activity");
    const relative = page.locator(".live-badge__updated time");
    await expect(relative).toHaveText(/^(jetzt|vor \d+ (Sekunden?|Minuten?))$/);
  });
});
