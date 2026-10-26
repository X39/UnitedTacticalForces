import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/e2e",
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? "github" : "list",
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
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
