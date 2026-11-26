import { expect, test, type Page } from "@playwright/test";
import {
  banner,
  bannerRegion,
  costingHourlyRateEur,
  evidence,
  listMail,
  openProfileMenu,
  openServer,
  openServerById,
  parseAmount,
  readHandOver,
  uatTitles,
  writeHandOver,
  type MailpitMessage,
} from "./support/uat";

/** How long the cost reconciler (30 s cadence) may take to raise or deliver an alert. */
const alertWait = 90_000;

/** How long the suite watches a page to prove that nothing dismisses an alert on its own. */
const noAutoDismissWatchMs = 75_000;

/** The thresholds the fresh phase adds after the first one, to have alerts to dismiss. */
const extraThresholds = ["1.00", "1.50"] as const;

/**
 * Escapes a text for use inside a regular expression.
 *
 * @param text The literal text.
 * @returns The escaped text.
 */
function escapeRegExp(text: string) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/** The costing server title as a regular expression fragment. */
const costingTitlePattern = escapeRegExp(uatTitles.costing);

/**
 * A pattern that matches a money amount in either separator style.
 *
 * @param amount The amount with a dot separator, for example `0.60`.
 * @returns The pattern fragment.
 */
function amountPattern(amount: string) {
  return amount.replace(".", "[.,]");
}

/**
 * The alert emails of the costing server in the Mailpit inbox.
 *
 * @param page The page whose request context is used.
 * @returns The messages, newest first.
 */
async function costingMail(page: Page): Promise<MailpitMessage[]> {
  const messages = await listMail(page.request);
  return messages.filter((message) =>
    `${message.Subject} ${message.Snippet}`.includes(uatTitles.costing),
  );
}

/**
 * Adds alert amounts on the cost alerts page reached through the costing server's cost strip, enables the email
 * channel and saves.
 *
 * @param page The page that shows the costing server workspace.
 * @param amounts The amounts to add.
 */
async function addAlertAmounts(page: Page, amounts: readonly string[]) {
  await page
    .locator("dl.cost-strip")
    .getByRole("link", { name: /^(Set an alert|Manage alerts)$/ })
    .click();
  await expect(page.getByRole("heading", { level: 1, name: "Cost alerts" })).toBeVisible();
  for (const amount of amounts) {
    const fields = page.getByLabel(/^Alert amount \d+ \(EUR\)$/);
    const before = await fields.count();
    await page.getByRole("button", { name: "Add an amount" }).click();
    await expect(fields).toHaveCount(before + 1);
    await page.getByLabel(`Alert amount ${before + 1} (EUR)`).fill(amount);
  }
  const email = page.getByRole("switch", { name: /^Email/ });
  if (!(await email.isChecked())) await email.check();
  await page.getByRole("button", { name: "Save alert settings" }).click();
  await expect(
    page.locator(".cost-alert-settings__status").getByText("Saved.", { exact: true }),
  ).toBeVisible();
}

/**
 * The thresholds the banner rows show, in display order.
 *
 * @param page The page that shows the workspace.
 * @returns The threshold amounts.
 */
async function bannerThresholds(page: Page): Promise<number[]> {
  const texts = await banner(page).locator(".cost-alert-banner__reached").allTextContents();
  return texts.map(parseAmount).sort((left, right) => left - right);
}

test.describe.serial("66-2 and 66-3 cost alerts on the seeded stack", () => {
  let costingServerId = "";

  test("66-2 an alert at the hourly rate fires once with the full-hour amount @fresh", async ({
    page,
  }) => {
    expect(await costingMail(page), "no alert email before the alert is set").toHaveLength(0);
    costingServerId = await openServer(page, uatTitles.costing);
    await expect(page.locator("dl.cost-strip")).toContainText("None set");

    await addAlertAmounts(page, [costingHourlyRateEur.toFixed(2)]);
    await openServerById(page, costingServerId, uatTitles.costing);

    // A warning toast announces the alert live.
    const toast = page
      .locator(".app-toast")
      .filter({ hasText: new RegExp(`Cost alert: ${costingTitlePattern} reached .*0[.,]60`) });
    await expect(toast).toBeVisible({ timeout: alertWait });
    evidence(
      `toast: ${(await toast.first().textContent())?.trim()} (intent ${await toast.first().getAttribute("data-intent")})`,
    );
    expect(await toast.first().getAttribute("data-intent")).toBe("warning");

    // The banner names the threshold and the full-hour amount of the session.
    await expect(
      bannerRegion(page).getByRole("heading", { level: 2, name: "Cost alert", exact: true }),
    ).toBeVisible();
    await expect(banner(page).locator("li")).toHaveCount(1);
    await expect(banner(page).locator(".cost-alert-banner__reached")).toHaveText(
      /0[.,]60.* reached/,
    );
    const amountText =
      (await banner(page).locator(".cost-alert-banner__amount").textContent()) ?? "";
    const accrued = parseAmount(amountText);
    evidence(`banner amount line: ${amountText.trim()}`);
    expect(accrued).toBeGreaterThanOrEqual(1.2);
    expect(
      Math.abs(accrued / costingHourlyRateEur - Math.round(accrued / costingHourlyRateEur)),
    ).toBeLessThan(0.001);

    // The account menu lists the alert as a row.
    const menu = await openProfileMenu(page);
    const menuRows = menu.locator("[data-cost-alert-id]");
    await expect(menuRows).toHaveCount(1);
    await expect(menuRows).toContainText(uatTitles.costing);
    await expect(menuRows).toContainText(/Cost alert: .*0[.,]60 reached/);
    expect(
      parseAmount((await menuRows.locator(".cost-alert-item__amount").textContent()) ?? ""),
    ).toBeCloseTo(accrued, 2);
    await page.keyboard.press("Escape");

    // Exactly one email arrives, and no second one follows within another reconciler cycle.
    await expect.poll(async () => (await costingMail(page)).length, { timeout: alertWait }).toBe(1);
    await page.waitForTimeout(45_000);
    const mail = await costingMail(page);
    expect(mail).toHaveLength(1);
    evidence(
      `Mailpit: 1 email "${mail[0]?.Subject}" to ${mail[0]?.To.map((to) => to.Address).join(", ")}`,
    );
  });

  test("66-3 part A dismissing one alert shows a toast and nothing auto-dismisses @fresh", async ({
    page,
  }) => {
    expect(costingServerId).not.toBe("");
    await openServerById(page, costingServerId, uatTitles.costing);
    await addAlertAmounts(page, extraThresholds);
    await openServerById(page, costingServerId, uatTitles.costing);
    await expect(banner(page).locator("li")).toHaveCount(3, { timeout: alertWait });
    await expect(
      bannerRegion(page).getByRole("heading", { level: 2, name: "3 cost alerts", exact: true }),
    ).toBeVisible();
    expect(await bannerThresholds(page)).toEqual([0.6, 1, 1.5]);

    // Dismiss the 1.00 alert by hand: a success toast names the server and the amount.
    await banner(page)
      .getByRole("button", { name: new RegExp(`^Dismiss alert at .*${amountPattern("1.00")}$`) })
      .click();
    const toast = page.locator(".app-toast").filter({
      hasText: new RegExp(
        `Cost alert dismissed: ${costingTitlePattern}, .*${amountPattern("1.00")}\\.`,
      ),
    });
    await expect(toast).toBeVisible();
    expect(await toast.first().getAttribute("data-intent")).toBe("success");
    evidence(`dismiss toast: ${(await toast.first().textContent())?.trim()}`);
    await expect(banner(page).locator("li")).toHaveCount(2);

    // Nothing dismisses the remaining alerts on its own, neither live nor after a reload.
    await page.waitForTimeout(noAutoDismissWatchMs);
    await expect(banner(page).locator("li")).toHaveCount(2);
    await page.reload();
    await expect(banner(page).locator("li")).toHaveCount(2);
    expect(await bannerThresholds(page)).toEqual([0.6, 1.5]);
    const menu = await openProfileMenu(page);
    await expect(menu.locator("[data-cost-alert-id]")).toHaveCount(2);
    await page.keyboard.press("Escape");

    // One email per alert, three in total.
    await expect.poll(async () => (await costingMail(page)).length, { timeout: alertWait }).toBe(3);
    const subjects = (await costingMail(page)).map((message) => message.Subject);
    evidence(
      `after ${noAutoDismissWatchMs / 1000} s and a reload 2 alerts remain; Mailpit has ${subjects.length} emails`,
    );

    writeHandOver({
      costingServerId,
      undismissedThresholds: [0.6, 1.5],
      dismissedThresholds: [1],
      emailSubjects: subjects,
      finishedAt: new Date().toISOString(),
    });
  });

  test("66-3 part B after a restart undismissed alerts survive, dismissed stay gone, no new email @restarted", async ({
    page,
  }) => {
    const handOver = readHandOver();
    await openServerById(page, handOver.costingServerId, uatTitles.costing);
    await expect(banner(page).locator("li")).toHaveCount(handOver.undismissedThresholds.length);
    expect(await bannerThresholds(page)).toEqual(handOver.undismissedThresholds);
    let menu = await openProfileMenu(page);
    await expect(menu.locator("[data-cost-alert-id]")).toHaveCount(
      handOver.undismissedThresholds.length,
    );
    await page.keyboard.press("Escape");

    // Let the restarted reconciler run a few cycles: no alert comes back and no email is sent again.
    await page.waitForTimeout(alertWait);
    await page.reload();
    await expect(banner(page).locator("li")).toHaveCount(handOver.undismissedThresholds.length);
    expect(await bannerThresholds(page)).toEqual(handOver.undismissedThresholds);
    const after = (await costingMail(page)).filter(
      (message) => Date.parse(message.Created) > Date.parse(handOver.finishedAt),
    );
    expect(after, "no alert email after the restart").toHaveLength(0);
    evidence(
      `after restart: banner thresholds ${JSON.stringify(handOver.undismissedThresholds)}; 0 emails since ${handOver.finishedAt}`,
    );

    // Dismiss all: one success toast with the count, and the banner leaves.
    await bannerRegion(page)
      .getByRole("button", { name: `Dismiss all ${handOver.undismissedThresholds.length} alerts` })
      .click();
    const toast = page.locator(".app-toast").filter({
      hasText: `${handOver.undismissedThresholds.length} cost alerts for ${uatTitles.costing} dismissed.`,
    });
    await expect(toast).toBeVisible();
    expect(await toast.first().getAttribute("data-intent")).toBe("success");
    evidence(`dismiss-all toast: ${(await toast.first().textContent())?.trim()}`);
    await expect(banner(page)).toHaveCount(0);
    await page.reload();
    await expect(page.locator("dl.cost-strip")).toBeVisible();
    await expect(banner(page)).toHaveCount(0);
    menu = await openProfileMenu(page);
    await expect(menu.locator("[data-cost-alert-id]")).toHaveCount(0);
  });
});
