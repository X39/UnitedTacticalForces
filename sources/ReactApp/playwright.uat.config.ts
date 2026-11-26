import { defineConfig, devices } from "@playwright/test";
import process from "node:process";

/**
 * Playwright configuration for the seeded-stack UAT suite.
 *
 * Unlike `playwright.config.ts`, this suite mocks nothing and starts no server: it drives the real local UAT stack
 * that `scripts/Start-Uat.ps1 -Fresh -NoBrowser` starts (Aspire, PostgreSQL, API, emulated Hetzner, Mailpit, React
 * dev server on port 5173). The tests mutate that stack, so they run serially in one worker.
 *
 * `UAT_PHASE=fresh` (default) runs the checks for a freshly seeded stack. `UAT_PHASE=restarted` runs the checks
 * that need the same database after a restart of the stack without `-Fresh`.
 */
const phase = process.env.UAT_PHASE === "restarted" ? "restarted" : "fresh";

export default defineConfig({
  testDir: "./tests/uat",
  outputDir: "./test-results/uat",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  forbidOnly: Boolean(process.env.CI),
  grep: phase === "restarted" ? /@restarted/ : /@fresh/,
  timeout: 15 * 60_000,
  expect: { timeout: 30_000 },
  reporter: [["list"], ["html", { outputFolder: "playwright-report/uat", open: "never" }]],
  use: {
    baseURL: process.env.UAT_BASE_URL ?? "http://localhost:5173/next/",
    screenshot: "on",
    trace: "retain-on-failure",
    actionTimeout: 30_000,
    navigationTimeout: 60_000,
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], viewport: { width: 1280, height: 900 } },
    },
  ],
});
