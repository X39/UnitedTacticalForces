import { defineConfig, devices } from "@playwright/test";

/** The mobile viewports the audit, journey and mobile specs run at, in CSS pixels. */
const mobile390 = { width: 390, height: 844 } as const;
const mobile320 = { width: 320, height: 568 } as const;

/**
 * Playwright configuration for the mocked end-to-end suite.
 *
 * Three projects share one `webServer` pair (the classic stub and the built canary app):
 *
 * - `chromium` is the desktop project. It runs every existing spec exactly as before, plus the `audit` and
 *   `journeys` specs, and never the `mobile` specs.
 * - `mobile-390` (390x844) runs the `audit`, `journeys` and `mobile` specs.
 * - `mobile-320` (320x568, the narrowest supported layout) runs the `audit` and `mobile` specs.
 *
 * Both mobile projects emulate a touch phone (`isMobile`, `hasTouch`) at a device scale factor of 1 so that pixel
 * output does not depend on a display density.
 *
 * Screenshots resolve to `__screenshots__/{projectName}/{arg}{ext}` with no platform segment: baselines are
 * generated and compared in one pinned Linux container (`scripts/Update-VisualBaselines.ps1`), and pixel
 * comparisons run only when `UTF_VISUAL_BASELINES=1`. Layout metrics are asserted unconditionally.
 */
export default defineConfig({
  testDir: "./tests/e2e",
  snapshotPathTemplate: "{testDir}/__screenshots__/{projectName}/{arg}{ext}",
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? "github" : "list",
  expect: {
    toHaveScreenshot: {
      animations: "disabled",
      caret: "hide",
      scale: "css",
      maxDiffPixelRatio: 0.02,
      threshold: 0.2,
    },
  },
  use: {
    baseURL: "http://127.0.0.1:4173/next/",
    trace: "on-first-retry",
  },
  webServer: [
    {
      command: "node scripts/classic-stub.mjs",
      url: "http://127.0.0.1:4174/health",
      timeout: 30_000,
      reuseExistingServer: !process.env.CI,
    },
    {
      command: "npm run build:assets && npm run preview -- --port 4173",
      url: "http://127.0.0.1:4173/next/",
      timeout: 360_000,
      reuseExistingServer: !process.env.CI,
      env: { ...process.env, UTF_CLASSIC_TARGET: "http://127.0.0.1:4174" },
    },
  ],
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
      testIgnore: ["**/mobile/**"],
    },
    {
      name: "mobile-390",
      use: {
        ...devices["Desktop Chrome"],
        viewport: mobile390,
        isMobile: true,
        hasTouch: true,
        deviceScaleFactor: 1,
      },
      testMatch: ["**/audit/**/*.spec.ts", "**/journeys/**/*.spec.ts", "**/mobile/**/*.spec.ts"],
    },
    {
      name: "mobile-320",
      use: {
        ...devices["Desktop Chrome"],
        viewport: mobile320,
        isMobile: true,
        hasTouch: true,
        deviceScaleFactor: 1,
      },
      testMatch: ["**/audit/**/*.spec.ts", "**/mobile/**/*.spec.ts"],
    },
  ],
});
