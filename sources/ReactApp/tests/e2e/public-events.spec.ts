import { expect, test } from "@playwright/test";

const eventId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";
const configuration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

function currentMonthOperation() {
  const now = new Date();
  const scheduledFor = new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), Math.min(24, now.getUTCDate() + 4), 18),
  ).toISOString();
  return {
    primaryKey: eventId,
    title: "Operation Copper Trail",
    description:
      "## Briefing\n\n**Coordinate reconnaissance** from the [published orders](/events/briefing).\n\n- Hold :radio: watch",
    terrainTitle: "Altis",
    modPackDefinitionTitle: "UTF Main",
    imagePath: "",
    scheduledFor,
    scheduledForOriginal: scheduledFor,
    timeStampCreated: now.toISOString(),
    acceptedCount: 3,
    rejectedCount: 1,
    maybeCount: 2,
    minimumAccepted: 12,
    hostedBy: {
      primaryKey: "2fdd4802-e081-4baa-9120-f1873653a057",
      nickname: "Raven",
      avatar: "",
      avatarMimeType: "image/png",
    },
    metaAcceptance: "Accepted",
  };
}

test.beforeEach(async ({ page }) => {
  const event = currentMonthOperation();
  await page.route("**/api/**", (route) => {
    throw new Error(`Unexpected API request: ${route.request().method()} ${route.request().url()}`);
  });
  await page.route("**/appsettings.json", (route) => route.fulfill({ json: configuration }));
  await page.route("**/media/**", (route) => route.fulfill({ status: 404, body: "" }));
  await page.route("**/api/users/me", (route) => route.fulfill({ status: 204 }));
  await page.route("**/api/users/authentication-providers", (route) =>
    route.fulfill({ json: { steam: true, discord: true } }),
  );
  await page.route("**/api/game-servers/notifications", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/game-servers", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/site-image-settings", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/events/upcoming", (route) => route.fulfill({ json: [event] }));
});

test("keeps operation readiness separate from personal attendance on the public board", async ({
  page,
}) => {
  await page.goto("events/upcoming?mode=board");

  await expect(page.getByRole("heading", { level: 1, name: "Upcoming operations" })).toBeVisible();
  const card = page.locator(".operation-board-card").first();
  await expect(card).toHaveClass(/operation-board-card--featured/);
  await expect(card.getByText("Low slots", { exact: true })).toBeVisible();
  await expect(card.getByText("Confirmed", { exact: true })).toBeVisible();
  const briefing = card.locator(".markdown--excerpt");
  await expect(briefing).toContainText("Briefing");
  await expect(briefing.getByText("Coordinate reconnaissance", { exact: true })).toBeVisible();
  await expect(briefing).toContainText("Hold 📻 watch");
  await expect(briefing.getByRole("link")).toHaveCount(0);
  await expect(briefing).not.toContainText("**");
  await expect(page.getByPlaceholder("Search operations")).toHaveCount(0);
});

test("keeps the calendar agenda usable without horizontal overflow on mobile", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("events/upcoming?mode=calendar");

  await expect(page.locator(".calendar-week-strip")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Operation Copper Trail" })).toBeVisible();
  await expect(page.locator(".calendar-detail > .markdown--excerpt")).toContainText(
    "Coordinate reconnaissance",
  );
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      ),
    )
    .toBeLessThanOrEqual(0);
});
