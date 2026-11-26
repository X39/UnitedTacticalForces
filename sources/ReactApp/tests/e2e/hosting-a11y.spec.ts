import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import {
  automationBlock,
  automationServer,
  automationServerId,
  mockAutomationRoutes,
} from "./support/automationMocks";
import type { ServerSchedule } from "@/features/hosting/types";
import { fulfillCostRoute } from "./support/costMocks";
import { mockFleetStream } from "./support/fleetStream";
import { fulfillMetricsRoute } from "./support/metricsMocks";
import { seedSettingsPanels } from "./support/settingsPanels";

const serverId = "10000000-0000-4000-8000-000000000a11";
const serverTitle = "A11y test server";
const wcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];
const operatePermissionReason = "Server Operate permission is required.";

type Culture = "en" | "de";

/** The server action shapes the API projects: start and stop are not dangerous, terminate carries a challenge. */
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
const stopAction = {
  id: "stop",
  label: "Stop",
  description: "Saves the game state and stops the server.",
  icon: "stop",
  group: "Lifecycle",
  order: 20,
  enabled: true,
  unavailableReason: null,
  dangerous: false,
  confirmationText: "Stopping releases the cloud machine after the data is saved.",
  confirmationChallenge: null,
  pending: false,
  expectedDurationSeconds: 120,
  costImpact: "StopsCost",
  costDescription: "Provider cost stops after verified cleanup.",
};
const terminateAction = {
  id: "terminate",
  label: "Terminate",
  description: "Immediately releases every provider resource.",
  icon: "delete",
  group: "Emergency",
  order: 1000,
  enabled: true,
  unavailableReason: null,
  dangerous: true,
  confirmationText: "Release every provider resource now.",
  confirmationChallenge: serverTitle,
  pending: false,
  expectedDurationSeconds: 120,
  costImpact: "StopsCost",
  costDescription: "Provider cost stops after verified cleanup.",
};

const runningDeployment = {
  status: "Running",
  stage: "Ready",
  possibleCost: true,
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
  resources: [],
};

function makeServer(deployment: object | null, actions: object[]) {
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
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    costOwnerId: serverId,
    costOwnerName: "Operations",
    createdById: serverId,
    isLaunchDisabled: false,
    isArchived: false,
    canModify: true,
    timeStampCreated: "2026-11-16T10:00:00Z",
    timeStampUpdated: "2026-11-16T10:00:00Z",
    pendingJobCount: 0,
    deployment,
    actions,
  };
}

/**
 * Installs the strict API mock set and returns the requests that no mock handled.
 *
 * Unmocked requests are aborted and recorded so a test can assert none happened.
 */
async function mockApi(
  page: Page,
  culture: Culture,
  server: object,
  options: { failStart?: boolean } = {},
): Promise<string[]> {
  const unexpected: string[] = [];
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
    if (path === `/api/game-servers/${serverId}`) return route.fulfill({ json: server });
    if (path === "/api/game-servers" && request.method() === "GET") {
      return route.fulfill({ json: [server] });
    }
    if (
      options.failStart &&
      request.method() === "POST" &&
      path === `/api/game-servers/${serverId}/actions/start`
    ) {
      return route.fulfill({ status: 500, json: { title: "Internal error" } });
    }
    if (
      request.method() === "GET" &&
      [
        "/api/site-image-settings",
        "/api/game-servers/notifications",
        `/api/game-servers/${serverId}/jobs`,
        `/api/game-servers/${serverId}/operations`,
      ].includes(path)
    ) {
      return route.fulfill({ json: [] });
    }
    if (await fulfillMetricsRoute(route, request.method(), path, serverId)) return;
    if (await fulfillCostRoute(route, request.method(), path, serverId)) return;
    unexpected.push(`${request.method()} ${path}`);
    return route.abort();
  });
  return unexpected;
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

const stoppedServer = () => makeServer(null, [startAction]);
const runningServer = () => makeServer(runningDeployment, [stopAction, terminateAction]);

for (const culture of ["en", "de"] as const) {
  test.describe(`confirmation dialog and deep-link notice (${culture})`, () => {
    test("start dialog is accessible and focuses the dismiss button", async ({ page }) => {
      const unexpected = await mockApi(page, culture, stoppedServer());

      await page.goto(`command/servers/${serverId}?action=start`);
      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      await expect(dialog.locator("[data-dialog-dismiss]")).toBeFocused();
      expect(await scan(page, "dialog[open]")).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("stop dialog is accessible", async ({ page }) => {
      const unexpected = await mockApi(page, culture, runningServer());

      await page.goto(`command/servers/${serverId}?action=stop`);
      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      await expect(dialog.locator("[data-dialog-dismiss]")).toBeFocused();
      expect(await scan(page, "dialog[open]")).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("terminate dialog with the typed challenge is accessible", async ({ page }) => {
      const unexpected = await mockApi(page, culture, runningServer());

      await page.goto(`command/servers/${serverId}?action=terminate`);
      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole("textbox")).toBeVisible();
      await expect(dialog.locator("[data-dialog-dismiss]")).toBeFocused();
      expect(await scan(page, "dialog[open]")).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("request-error state is accessible", async ({ page }) => {
      const unexpected = await mockApi(page, culture, stoppedServer(), { failStart: true });

      await page.goto(`command/servers/${serverId}?action=start`);
      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      await dialog.locator("button[type=submit]").click();
      await expect(dialog.getByRole("alert")).toBeVisible();
      expect(await scan(page, "dialog[open]")).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("deep-link notice for a disabled action is accessible and opens no dialog", async ({
      page,
    }) => {
      const unexpected = await mockApi(
        page,
        culture,
        makeServer(null, [
          { ...startAction, enabled: false, unavailableReason: operatePermissionReason },
        ]),
      );

      await page.goto(`command/servers/${serverId}?action=start`);
      const notice = page.locator(".state-panel--warning");
      await expect(notice).toBeVisible();
      await expect(page.locator("dialog[open]")).toHaveCount(0);
      expect(await scan(page, ".state-panel--warning")).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("start dialog fits a 320 by 568 viewport", async ({ page }) => {
      const unexpected = await mockApi(page, culture, stoppedServer());
      await page.setViewportSize({ width: 320, height: 568 });

      await page.goto(`command/servers/${serverId}?action=start`);
      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      const metrics = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        innerWidth: window.innerWidth,
      }));
      expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.innerWidth);
      const box = await dialog.boundingBox();
      expect(box).not.toBeNull();
      expect(box!.x).toBeGreaterThanOrEqual(0);
      expect(box!.width).toBeLessThanOrEqual(metrics.innerWidth);
      expect(unexpected).toEqual([]);
    });
  });
}

/** The automation panel card of the Settings section. */
const automationPanel = "#server-automation";

/** The visible copy the automation scans read, per culture. */
const automationCopy = {
  en: {
    title: "Automatic shutdown and schedules",
    idleEnable: "Shut down when idle",
    idleMinutes: "Idle time (minutes)",
    idleInvalid: "Enter a whole number of minutes between 5 and 720.",
    addSchedule: "Add a schedule",
    save: "Save automation settings",
    unavailable: "Automatic shutdown and schedules are not available for this server right now.",
    readOnly:
      "Only the server owner or an administrator can change these settings. You can see what is set.",
    pausedTitle: "Scheduled starts are paused",
    reconfirm: "Confirm scheduled starts",
    consentTitle: "Allow scheduled starts?",
    noticeTitle: /^Automatic shutdown at /,
  },
  de: {
    title: "Automatisches Herunterfahren und Zeitpläne",
    idleEnable: "Bei Leerlauf herunterfahren",
    idleMinutes: "Leerlaufzeit (Minuten)",
    idleInvalid: "Gib eine ganze Zahl an Minuten zwischen 5 und 720 ein.",
    addSchedule: "Zeitplan hinzufügen",
    save: "Automatisierung speichern",
    unavailable:
      "Automatisches Herunterfahren und Zeitpläne sind für diesen Server gerade nicht verfügbar.",
    readOnly:
      "Nur der Serverbesitzer oder eine Administration kann diese Einstellungen ändern. Du siehst, was eingestellt ist.",
    pausedTitle: "Geplante Starts sind pausiert",
    reconfirm: "Geplante Starts bestätigen",
    consentTitle: "Geplante Starts erlauben?",
    noticeTitle: /^Automatisches Herunterfahren um /,
  },
} as const;

/** A saved Friday 18:00 start rule, which is what makes the consent state matter. */
const savedStartRule: ServerSchedule = {
  id: "a0000000-0000-4000-8000-000000000001",
  action: "Start",
  days: [5],
  time: "18:00",
  nextRunAt: "2026-11-27T18:00:00Z",
  lastRun: null,
};

/**
 * Installs the automation mocks and the quiet update stream.
 *
 * @param page The page to mock.
 * @param culture The culture of the session.
 * @param options The server and the viewer's right.
 * @returns The requests no mock handled.
 */
async function mockAutomation(
  page: Page,
  culture: Culture,
  options: { server?: ReturnType<typeof automationServer>; canManage?: boolean } = {},
): Promise<string[]> {
  const handle = await mockAutomationRoutes(page, {
    server: options.server ?? automationServer(),
    canManage: options.canManage ?? true,
    culture,
  });
  await mockFleetStream(page);
  return handle.unexpected;
}

/**
 * Opens the Settings section on the automation panel and waits until its heading rendered.
 *
 * @param page The page to navigate.
 * @param culture The culture of the session.
 */
async function openAutomationPanel(page: Page, culture: Culture) {
  await page.goto(`command/servers/${automationServerId}?tab=settings#server-automation`);
  await expect(
    page.getByRole("heading", { level: 2, name: automationCopy[culture].title }),
  ).toBeVisible();
}

for (const culture of ["en", "de"] as const) {
  test.describe(`automation accessibility (${culture}, 390x844)`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test("panel has no axe violation when pristine, dirty and invalid", async ({ page }) => {
      const text = automationCopy[culture];
      const unexpected = await mockAutomation(page, culture);

      await openAutomationPanel(page, culture);
      expect(await scan(page, automationPanel)).toEqual([]);

      await page.locator(automationPanel).getByRole("switch", { name: text.idleEnable }).check();
      await page.locator(automationPanel).getByRole("button", { name: text.addSchedule }).click();
      await expect(page.locator(automationPanel).getByRole("group").first()).toBeVisible();
      expect(await scan(page, automationPanel)).toEqual([]);

      const minutes = page.locator(automationPanel).getByLabel(text.idleMinutes);
      await minutes.fill("2");
      await minutes.blur();
      await expect(page.locator(automationPanel).getByText(text.idleInvalid)).toBeVisible();
      await page.locator(automationPanel).getByRole("button", { name: text.save }).click();
      await expect(page.locator(automationPanel).getByRole("alert").first()).toBeVisible();
      expect(await scan(page, automationPanel)).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("read-only panel has no axe violation", async ({ page }) => {
      const text = automationCopy[culture];
      const unexpected = await mockAutomation(page, culture, {
        canManage: false,
        server: automationServer({
          canManage: false,
          automation: automationBlock({
            settings: {
              idleShutdownEnabled: true,
              idleShutdownMinutes: 45,
              schedules: [savedStartRule],
              version: 2,
            },
          }),
        }),
      });

      await openAutomationPanel(page, culture);
      await expect(page.locator(automationPanel).getByText(text.readOnly)).toBeVisible();
      await expect(
        page.locator(automationPanel).getByRole("switch", { name: text.idleEnable }),
      ).toBeDisabled();
      expect(await scan(page, automationPanel)).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("consent-needed panel and the open consent dialog have no axe violation", async ({
      page,
    }) => {
      const text = automationCopy[culture];
      const unexpected = await mockAutomation(page, culture, {
        server: automationServer({
          automation: automationBlock({
            settings: { schedules: [savedStartRule], version: 4 },
            startConsent: { state: "Needed" },
          }),
        }),
      });

      await openAutomationPanel(page, culture);
      await expect(page.getByText(text.pausedTitle)).toBeVisible();
      expect(await scan(page, automationPanel)).toEqual([]);

      await page.getByRole("button", { name: text.reconfirm }).click();
      const dialog = page.locator("dialog[open]");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole("heading", { name: text.consentTitle })).toBeVisible();
      expect(await scan(page, "dialog[open]")).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("unavailable panel has no axe violation", async ({ page }) => {
      const text = automationCopy[culture];
      const unexpected = await mockAutomation(page, culture, {
        server: automationServer({ automation: null }),
      });

      await openAutomationPanel(page, culture);
      await expect(page.locator(automationPanel).getByText(text.unavailable)).toBeVisible();
      expect(await scan(page, automationPanel)).toEqual([]);
      expect(unexpected).toEqual([]);
    });

    test("workspace with the pending stop notice has no axe violation", async ({ page }) => {
      const text = automationCopy[culture];
      const stopsAt = "2026-11-23T20:30:00Z";
      const unexpected = await mockAutomation(page, culture, {
        server: automationServer({
          automation: automationBlock({
            settings: { idleShutdownEnabled: true, idleShutdownMinutes: 30 },
            status: {
              idle: { state: "Idle", since: "2026-11-23T20:00:00Z", stopsAt },
              pendingStop: { reason: "Idle", stopsAt, limit: 30 },
            },
          }),
        }),
      });

      await page.goto(`command/servers/${automationServerId}`);
      await expect(page.getByRole("heading", { level: 2, name: text.noticeTitle })).toBeVisible();
      expect(await scan(page, ".next-hosting-page")).toEqual([]);
      expect(unexpected).toEqual([]);
    });
  });
}
