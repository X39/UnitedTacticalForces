import { expect, test } from "@playwright/test";
import {
  accruedRow,
  costingHourlyRateEur,
  evidence,
  openServer,
  parseAmount,
  uatTitles,
} from "./support/uat";

/** One hour, the billing unit of the emulated Hetzner provider. */
const hourMs = 3_600_000;

/** How long the value is watched to prove that it does not creep per minute. */
const stableWatchMs = 90_000;

/** A step this close is waited for instead of only checking that the instant is in the future. */
const stepWaitLimitMs = 6 * 60_000;

test.describe("66-1 started-hour accrued value", () => {
  test("the accrued row bills per started hour and does not creep per minute @fresh", async ({
    page,
  }) => {
    await openServer(page, uatTitles.costing);
    const row = accruedRow(page);
    const value = row.locator(".accrued-cost__value");
    await expect(value).toContainText(/\d[.,]\d{2}/);
    await expect(row).toContainText(/Billed per started hour\. The next hour starts at /);

    const nextHourTime = row.locator("time").nth(1);
    const nextHour = Date.parse((await nextHourTime.getAttribute("datetime")) ?? "");
    const first = parseAmount((await value.textContent()) ?? "");
    const startedHours = Math.round(first / costingHourlyRateEur);
    evidence(
      `accrued ${first.toFixed(2)} EUR = ${startedHours} started hours at ${costingHourlyRateEur} EUR; next hour ${new Date(nextHour).toISOString()}`,
    );

    // The value is a whole number of started hours at the session rate, never a prorated amount.
    expect(first).toBeCloseTo(startedHours * costingHourlyRateEur, 2);
    expect(startedHours).toBeGreaterThanOrEqual(2);
    // The next hour lies ahead, at most one billing unit away.
    const untilStep = nextHour - Date.now();
    expect(untilStep).toBeGreaterThan(0);
    expect(untilStep).toBeLessThanOrEqual(hourMs);

    if (untilStep > stableWatchMs + 15_000) {
      // No per-minute creep: the value stays the same while no new started hour begins.
      const deadline = Date.now() + stableWatchMs;
      while (Date.now() < deadline) {
        await page.waitForTimeout(15_000);
        expect(parseAmount((await value.textContent()) ?? "")).toBeCloseTo(first, 2);
      }
      await page.reload();
      const reloaded = accruedRow(page).locator(".accrued-cost__value");
      await expect(reloaded).toContainText(/\d[.,]\d{2}/);
      expect(parseAmount((await reloaded.textContent()) ?? "")).toBeCloseTo(first, 2);
    }

    if (nextHour - Date.now() <= stepWaitLimitMs) {
      // The next started hour is close: the value steps by exactly one hourly rate once it begins.
      await expect
        .poll(
          async () =>
            parseAmount(
              (await accruedRow(page).locator(".accrued-cost__value").textContent()) ?? "",
            ),
          { timeout: Math.max(nextHour - Date.now(), 0) + 120_000, intervals: [5_000] },
        )
        .toBeCloseTo(first + costingHourlyRateEur, 2);
      evidence(
        `stepped to ${(first + costingHourlyRateEur).toFixed(2)} EUR after ${new Date(nextHour).toISOString()}`,
      );
    }
  });
});
