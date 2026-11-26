import { expect, test, type APIRequestContext, type Locator, type Page } from "@playwright/test";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import process from "node:process";
import { assertLocalUatTarget, defaultUatBaseUrl } from "./uatTarget";

/**
 * The base URL the suite drives, the same value `playwright.uat.config.ts` reads.
 *
 * The seeded specs mutate data, so they refuse to load against a host that is not this machine (T-69-38). The check
 * runs when a spec imports this module, before any test runs.
 */
export const uatBaseUrl = process.env.UAT_BASE_URL ?? defaultUatBaseUrl;
assertLocalUatTarget(uatBaseUrl);

/** The UI cultures the suite can switch to through the stored culture preference. */
export type Culture = "en" | "de";

/** Titles of the seeded UAT fleet servers (sources/Api/Data/UatFleetScenarioSeeder.cs). */
export const uatTitles = {
  off: "UAT 1 Off (no deployment)",
  running: "UAT 2 Running",
  terminated: "UAT 8 Off (terminated)",
  costing: "UAT 9 Costing (accrued cost)",
  metrics: "UAT 10 Metrics",
} as const;

/** Titles of the seeded automation scenarios UAT 11 to 16 (sources/Api/Data/UatFleetScenarioSeeder.cs). */
export const uatAutomationTitles = {
  idle: "UAT 11 Idle shutdown",
  maxRuntime: "UAT 12 Maximum runtime",
  scheduledStop: "UAT 13 Scheduled stop",
  scheduledStart: "UAT 14 Scheduled start",
  startSkipped: "UAT 15 Scheduled start without credit",
  startsPaused: "UAT 16 Scheduled starts paused",
} as const;

/** The hourly rate in EUR the seeder gives the costing scenario session. */
export const costingHourlyRateEur = 0.6;

/** The Mailpit HTTP API of the UAT stack. */
export const mailpitApi = process.env.UAT_MAILPIT_URL ?? "http://localhost:8025/api/v1";

/** Where screenshots for the human design review are written; they are not test artifacts. */
export const designShotsDirectory = resolve(process.env.UAT_SHOTS_DIR ?? "test-results/uat-shots");

/** The file that carries observations from the fresh phase to the restarted phase. */
const stateFile = resolve(process.env.UAT_STATE_FILE ?? "test-results/uat-state.json");

/** What the fresh phase hands to the restarted phase. */
export interface UatHandOver {
  /** The id of the costing server. */
  costingServerId: string;
  /** The threshold amounts whose alerts were still undismissed at the end of the fresh phase. */
  undismissedThresholds: number[];
  /** The threshold amounts whose alerts were dismissed in the fresh phase. */
  dismissedThresholds: number[];
  /** The subjects of all alert emails the fresh phase saw. */
  emailSubjects: string[];
  /** When the fresh phase finished; an alert email received later was sent again after the restart. */
  finishedAt: string;
  /** The title of the server the journey spec created; set by the journeys, absent before they ran. */
  createdServerTitle?: string;
  /** The id of the server the journey spec created; set by the journeys, absent before they ran. */
  createdServerId?: string;
  /** The title of the mod pack the journey spec applied to the seeded off server; absent before the journeys ran. */
  appliedModPackName?: string;
}

/** The journey fields of {@link UatHandOver}, the part that the journey spec owns. */
export type UatJourneyHandOver = Pick<
  UatHandOver,
  "createdServerTitle" | "createdServerId" | "appliedModPackName"
>;

/**
 * Reads the hand-over file when it exists.
 *
 * @returns The stored fields, or an empty object when no phase wrote the file yet.
 */
function readStoredHandOver(): Partial<UatHandOver> {
  return existsSync(stateFile)
    ? (JSON.parse(readFileSync(stateFile, "utf8")) as Partial<UatHandOver>)
    : {};
}

/**
 * Stores the hand-over of the fresh phase.
 *
 * The journey fields that an earlier spec of the same phase stored stay in the file, so the cost specs and the
 * journey spec can both write without erasing each other.
 *
 * @param value The observations to keep.
 */
export function writeHandOver(value: UatHandOver) {
  const { createdServerTitle, createdServerId, appliedModPackName } = readStoredHandOver();
  const journey: UatJourneyHandOver = {
    ...(createdServerTitle === undefined ? {} : { createdServerTitle }),
    ...(createdServerId === undefined ? {} : { createdServerId }),
    ...(appliedModPackName === undefined ? {} : { appliedModPackName }),
  };
  mkdirSync(dirname(stateFile), { recursive: true });
  writeFileSync(stateFile, JSON.stringify({ ...journey, ...value }, null, 2));
}

/**
 * Adds journey observations to the hand-over file without touching the other fields.
 *
 * @param fields The journey fields to record.
 */
export function recordJourneyHandOver(fields: UatJourneyHandOver) {
  mkdirSync(dirname(stateFile), { recursive: true });
  writeFileSync(stateFile, JSON.stringify({ ...readStoredHandOver(), ...fields }, null, 2));
}

/**
 * Reads the journey observations of the fresh phase.
 *
 * @returns The created server and the applied mod pack.
 * @throws Error when the fresh journeys did not record them, so the restarted phase cannot pass vacuously.
 */
export function readJourneyHandOver(): Required<UatJourneyHandOver> {
  const { createdServerTitle, createdServerId, appliedModPackName } = readStoredHandOver();
  if (!createdServerTitle || !createdServerId || !appliedModPackName) {
    throw new Error(
      "The journey hand-over is incomplete: run the fresh phase (UAT_PHASE=fresh) of journeys.spec.ts first.",
    );
  }
  return { createdServerTitle, createdServerId, appliedModPackName };
}

/**
 * Reads the hand-over of the fresh phase.
 *
 * @returns The observations the fresh phase stored.
 */
export function readHandOver(): UatHandOver {
  return JSON.parse(readFileSync(stateFile, "utf8")) as UatHandOver;
}

/** The file that carries observations of the automation specs from the fresh phase to the restarted phase. */
const automationStateFile = resolve(
  process.env.UAT_AUTOMATION_STATE_FILE ?? "test-results/uat-automation-state.json",
);

/** What the fresh phase of the automation specs hands to the restarted phase. */
export interface UatAutomationHandOver {
  /** The server ids of the automation scenarios, keyed by the `uatAutomationTitles` key. */
  serverIds: Record<string, string>;
  /** When the fresh phase finished; an automatic shutdown email received later was sent again after the restart. */
  finishedAt: string;
  /** The subjects of all automatic shutdown emails the fresh phase saw. */
  autoStopSubjects: string[];
}

/**
 * Stores the automation hand-over of the fresh phase.
 *
 * @param value The observations to keep.
 */
export function writeAutomationHandOver(value: UatAutomationHandOver) {
  mkdirSync(dirname(automationStateFile), { recursive: true });
  writeFileSync(automationStateFile, JSON.stringify(value, null, 2));
}

/**
 * Reads the automation hand-over of the fresh phase.
 *
 * @returns The observations the fresh phase stored.
 */
export function readAutomationHandOver(): UatAutomationHandOver {
  return JSON.parse(readFileSync(automationStateFile, "utf8")) as UatAutomationHandOver;
}

/**
 * Makes the app start in the given culture by presetting the stored culture preference.
 *
 * @param page The page to configure before its first navigation.
 * @param culture The culture to use.
 */
export async function useCulture(page: Page, culture: Culture) {
  await page.addInitScript((value) => localStorage.setItem("UtfCulture", value), culture);
}

/**
 * Opens the fleet page and waits until the seeded cards rendered.
 *
 * @param page The page to navigate.
 */
export async function openFleet(page: Page) {
  await page.goto("command/servers");
  await expect(fleetCard(page, uatTitles.off)).toBeVisible({ timeout: 60_000 });
}

/**
 * The fleet card of one server, found by its exact title.
 *
 * @param page The page that shows the fleet.
 * @param title The server title.
 * @returns The card locator.
 */
export function fleetCard(page: Page, title: string): Locator {
  return page
    .locator("article.fleet-card")
    .filter({ has: page.getByRole("heading", { level: 3, name: title, exact: true }) });
}

/**
 * Opens the workspace of a seeded server from the fleet page, the way a user does.
 *
 * @param page The page to navigate.
 * @param title The server title.
 * @returns The server id read from the card.
 */
export async function openServer(page: Page, title: string): Promise<string> {
  await openFleet(page);
  const card = fleetCard(page, title);
  const id = (await card.getAttribute("data-server-card")) ?? "";
  expect(id, `card of ${title} carries its id`).not.toBe("");
  await card.locator(`a[data-server-id="${id}"]`).click();
  await expect(page.getByRole("heading", { level: 1, name: title })).toBeVisible();
  return id;
}

/**
 * Opens the workspace of a server directly by id.
 *
 * @param page The page to navigate.
 * @param id The server id.
 * @param title The server title the heading must show.
 */
export async function openServerById(page: Page, id: string, title: string) {
  await page.goto(`command/servers/${id}`);
  await expect(page.getByRole("heading", { level: 1, name: title })).toBeVisible({
    timeout: 60_000,
  });
}

/**
 * The accrued cost value wrapper of the cost strip.
 *
 * @param page The page that shows the workspace.
 * @returns The locator.
 */
export function accruedRow(page: Page): Locator {
  return page.locator('[data-slot="accrued-cost"]');
}

/**
 * The cost alert banner list of the workspace.
 *
 * @param page The page that shows the workspace.
 * @returns The locator.
 */
export function banner(page: Page): Locator {
  return page.locator(".cost-alert-banner");
}

/**
 * The whole banner status region, including its footer with cost-stop and Dismiss all.
 *
 * @param page The page that shows the workspace.
 * @returns The locator.
 */
export function bannerRegion(page: Page): Locator {
  return page.locator("section.state-panel").filter({ has: page.locator(".cost-alert-banner") });
}

/**
 * The metrics panel of the workspace Overview.
 *
 * @param page The page that shows the workspace.
 * @returns The locator.
 */
export function metricsPanel(page: Page): Locator {
  return page.locator('[data-slot="metrics"]');
}

/** The ids of the four metrics tiles. */
export type MetricTileId = "uptime" | "players" | "cpu" | "memory";

/** The four metrics tile ids in display order. */
export const metricTileIds: readonly MetricTileId[] = ["uptime", "players", "cpu", "memory"];

/**
 * The tile of one metric.
 *
 * @param page The page that shows the workspace.
 * @param id The tile id.
 * @returns The tile locator.
 */
export function metricTile(page: Page, id: MetricTileId): Locator {
  return metricsPanel(page).locator(`[data-tile="${id}"]`);
}

/**
 * Parses the first money amount with two decimals (either separator) out of a text.
 *
 * @param text The text that shows an amount.
 * @returns The amount, or `NaN` when none is shown.
 */
export function parseAmount(text: string): number {
  const match = /(\d+)[.,](\d{2})/.exec(text);
  return match ? Number(`${match[1]}.${match[2]}`) : Number.NaN;
}

/**
 * Opens the profile menu and waits until its panel shows.
 *
 * @param page The page that shows the app shell.
 * @param culture The active culture.
 * @returns The menu dialog.
 */
export async function openProfileMenu(page: Page, culture: Culture = "en"): Promise<Locator> {
  const trigger = culture === "en" ? /^Open profile menu for / : /^Profilmenü für .* öffnen$/;
  await page.getByRole("button", { name: trigger }).click();
  const menu = page.getByRole("dialog", { name: culture === "en" ? "Profile menu" : "Profilmenü" });
  await expect(menu).toBeVisible();
  return menu;
}

/** One message of the Mailpit message list. */
export interface MailpitMessage {
  /** The Mailpit message id. */
  ID: string;
  /** The subject line. */
  Subject: string;
  /** When Mailpit received the message. */
  Created: string;
  /** The recipients. */
  To: { Address: string; Name: string }[];
  /** The start of the text body. */
  Snippet: string;
}

/**
 * Lists every message in the Mailpit inbox, newest first.
 *
 * @param request The API request context.
 * @returns The messages.
 */
export async function listMail(request: APIRequestContext): Promise<MailpitMessage[]> {
  const response = await request.get(`${mailpitApi}/messages?limit=200`);
  expect(response.ok(), "Mailpit answers").toBeTruthy();
  const body = (await response.json()) as { messages: MailpitMessage[] };
  return body.messages;
}

/**
 * Writes a design-review screenshot of the full page.
 *
 * @param page The page to capture.
 * @param name The file name without extension.
 */
export async function designShot(page: Page, name: string) {
  mkdirSync(designShotsDirectory, { recursive: true });
  await page.screenshot({ path: resolve(designShotsDirectory, `${name}.png`), fullPage: true });
}

/**
 * Records one line of evidence on the test and prints it, so the list reporter shows it too.
 *
 * @param description The evidence.
 */
export function evidence(description: string) {
  test.info().annotations.push({ type: "evidence", description });
  console.log(`[evidence] ${test.info().title}: ${description}`);
}
