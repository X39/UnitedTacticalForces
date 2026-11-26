import { expect, test, type Locator, type Page } from "@playwright/test";
import { assertNoGermanLeak } from "../e2e/support/germanLeak";
import {
  evidence,
  fleetCard,
  openFleet,
  openServer,
  openServerById,
  readJourneyHandOver,
  recordJourneyHandOver,
  uatTitles,
  useCulture,
} from "./support/uat";

/**
 * How long the lifecycle executor (emulated Hetzner, no worker) may take from the confirmed start until the job
 * waits for the worker. The seeded stack provisions without any delay of its own, so this only covers the executor
 * polling interval and the workspace live update.
 */
const waitingForWorkerWait = 5 * 60_000;

/** The progress step a start job shows while the emulated deployment waits for a worker that never registers. */
const waitingStep = "Waiting for the server software";

/**
 * Builds a server title that no earlier run of the same stack used, so the fleet card is found unambiguously.
 *
 * @returns The title, for example `UAT Journey m3k9x2`.
 */
function uniqueServerTitle(): string {
  return `UAT Journey ${Date.now().toString(36)}`;
}

/**
 * The mod pack chooser of the Mods panel on the Settings tab.
 *
 * @param page The page that shows the Settings tab.
 * @returns The select locator.
 */
function modPackSelect(page: Page): Locator {
  return page.getByRole("combobox", { name: "Mod pack", exact: true });
}

/**
 * Opens the Settings tab of a seeded server and waits until the mod pack chooser lists its options.
 *
 * @param page The page to navigate.
 * @param id The server id.
 * @param title The server title the heading must show.
 * @returns The mod pack chooser.
 */
async function openSettings(page: Page, id: string, title: string): Promise<Locator> {
  await page.goto(`command/servers/${id}?tab=settings`);
  await expect(page.getByRole("heading", { level: 1, name: title })).toBeVisible({
    timeout: 60_000,
  });
  const select = modPackSelect(page);
  await expect(select).toBeVisible({ timeout: 60_000 });
  // The options load after the chooser renders: the empty choice plus at least one seeded pack.
  await expect.poll(() => select.locator("option").count(), { timeout: 60_000 }).toBeGreaterThan(1);
  return select;
}

/**
 * Removes the marker the chooser appends to the option of the pack that is currently saved, so the title is the same
 * before and after a save.
 *
 * @param label The text of an option, for example `Debug Recon Composition (current)`.
 * @returns The pack title without the English `(current)` marker.
 */
function packTitle(label: string): string {
  return label.replace(/\s*\(current\)$/, "").trim();
}

/**
 * The title of the mod pack the chooser currently shows.
 *
 * @param select The mod pack chooser.
 * @returns The title of the selected option, without the current marker.
 */
async function selectedModPack(select: Locator): Promise<string> {
  const label = await select.evaluate(
    (element) => (element as HTMLSelectElement).selectedOptions[0]?.textContent ?? "",
  );
  return packTitle(label);
}

test.describe.serial("69-32 seeded journeys on the real stack", () => {
  test("69-32-1 create a server and start it through the cost confirmation until it waits for the worker @fresh", async ({
    page,
  }) => {
    await useCulture(page, "en");
    const title = uniqueServerTitle();

    await page.goto("command/servers/create");
    await expect(page.getByRole("heading", { level: 1, name: "Create server" })).toBeVisible({
      timeout: 60_000,
    });
    // The estimate renders once the capabilities, sizes and cost owner loaded, so the form is ready to submit.
    await expect(page.locator(".create-summary__hourly")).toHaveText(/per hour while it runs/, {
      timeout: 60_000,
    });
    await page.locator("input[name=title]").fill(title);
    await page.getByRole("radio", { name: /^Start it now/ }).check();
    await page.getByRole("button", { name: "Create server and start" }).click();

    // The workspace of the new server opens and asks for the cost confirmation before anything starts.
    await expect(page).toHaveURL(/\/command\/servers\/[0-9a-f-]{36}$/, { timeout: 60_000 });
    const serverId = /\/command\/servers\/([0-9a-f-]{36})$/.exec(page.url())?.[1] ?? "";
    expect(serverId, "the created server id is in the URL").not.toBe("");
    const dialog = page.locator("dialog[open]");
    await expect(dialog).toBeVisible({ timeout: 60_000 });
    await expect(dialog.getByRole("heading", { level: 2 })).toContainText(`Start ${title}?`);
    await expect(dialog.locator(".cost-confirm__amount")).toBeVisible();
    recordJourneyHandOver({ createdServerTitle: title, createdServerId: serverId });

    await dialog.getByRole("button", { name: "Start server", exact: true }).click();
    await expect(page.locator("dialog[open]")).toHaveCount(0);

    // The start job runs against the emulated provider; no worker registers, so it ends up waiting for one.
    const strip = page.locator("section.job-progress");
    const waiting = strip.locator(".job-progress__step-name", { hasText: waitingStep });
    const failed = page.getByRole("heading", { level: 2, name: /did not finish$/ });
    // A failed start replaces the strip with the failure panel, so wait for either and fail at once on the failure
    // instead of running into the long timeout.
    await expect(waiting.or(failed).first()).toBeVisible({ timeout: waitingForWorkerWait });
    await expect(
      failed,
      "the start job failed instead of waiting for the worker; the job failure panel names the reason",
    ).toHaveCount(0);
    await expect(waiting).toHaveText(waitingStep);
    await expect(strip.locator(".job-progress__action")).toHaveText("Start server");
    evidence(`${title} (${serverId}): the progress strip names "${waitingStep}"`);
  });

  test(
    "Deferred follow-up: a created server reaches Running with a real worker and provider",
    {
      tag: "@fresh",
    },
    () => {
      test.skip(
        true,
        "Deferred follow-up: a freshly created server reaches Running only with a real worker on a real provider machine, run at the milestone end (D-AUTO-6, D-20).",
      );
    },
  );

  test("69-32-2 change the mod pack of a seeded server, review and apply it @fresh", async ({
    page,
  }) => {
    await useCulture(page, "en");
    const id = await openServer(page, uatTitles.off);
    const select = await openSettings(page, id, uatTitles.off);

    const options = await select.locator("option").evaluateAll((items) =>
      items.map((item) => ({
        value: (item as HTMLOptionElement).value,
        label: item.textContent?.trim() ?? "",
      })),
    );
    const current = await select.inputValue();
    const target = options.find((option) => option.value !== "" && option.value !== current);
    expect(target, "a seeded mod pack other than the current one exists").toBeDefined();
    if (target === undefined) return;

    await select.selectOption({ value: target.value });
    const bar = page.getByRole("region", { name: "Pending changes" });
    await expect(bar).toBeVisible();
    await expect(
      bar.getByText("1 change staged. Nothing is saved until you apply it.", { exact: true }),
    ).toBeVisible();

    await bar.getByRole("button", { name: "Review changes" }).click();
    const dialog = page.getByRole("dialog", { name: "Review changes" });
    await expect(dialog).toBeVisible();
    // The review compares the pack contents first; Apply is ready once the comparison finished.
    const apply = dialog.getByRole("button", { name: "Apply 1 change" });
    await expect(apply).toBeVisible();
    await expect(apply).not.toHaveAttribute("aria-disabled", "true");
    await apply.click();

    await expect(page.getByText(/^1 change applied\./)).toBeVisible();
    await expect(dialog).toBeHidden();
    await expect(bar).toHaveCount(0);
    await expect(select).toHaveValue(target.value);

    // A reload proves the pack was saved on the server, not only shown by the form.
    await page.reload();
    const reloaded = await openSettings(page, id, uatTitles.off);
    await expect(reloaded).toHaveValue(target.value);
    expect(await selectedModPack(reloaded)).toBe(packTitle(target.label));

    recordJourneyHandOver({ appliedModPackName: packTitle(target.label) });
    evidence(
      `${uatTitles.off}: mod pack changed from "${current}" to "${target.label}" and applied`,
    );
  });

  test("69-32-3 German pass: the fleet and the workspace of a seeded server show no English text @fresh", async ({
    page,
  }) => {
    await useCulture(page, "de");
    await openFleet(page);
    await expect(fleetCard(page, uatTitles.running)).toBeVisible();
    await assertNoGermanLeak(page);

    const id = await openServer(page, uatTitles.off);
    await expect(page.locator("dl.cost-strip")).toBeVisible();
    await assertNoGermanLeak(page);

    await page.goto(`command/servers/${id}?tab=settings`);
    await expect(page.getByRole("combobox", { name: "Mod-Paket", exact: true })).toBeVisible({
      timeout: 60_000,
    });
    await assertNoGermanLeak(page);
    evidence(
      "fleet, workspace overview and workspace settings of UAT 1 Off: no English text in German",
    );
  });

  test("69-32-4 the created server and the applied mod pack survive a restart of the stack @restarted", async ({
    page,
  }) => {
    await useCulture(page, "en");
    const handOver = readJourneyHandOver();

    await openFleet(page);
    const card = fleetCard(page, handOver.createdServerTitle);
    await expect(card).toBeVisible();
    expect(await card.getAttribute("data-server-card")).toBe(handOver.createdServerId);
    await openServerById(page, handOver.createdServerId, handOver.createdServerTitle);

    const id = await openServer(page, uatTitles.off);
    const select = await openSettings(page, id, uatTitles.off);
    expect(await selectedModPack(select)).toBe(handOver.appliedModPackName);
    evidence(
      `after the restart: "${handOver.createdServerTitle}" is listed and ${uatTitles.off} still shows "${handOver.appliedModPackName}"`,
    );
  });
});
