import { expect, test } from "@playwright/test";

const eventId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";
const userId = "34b29531-739a-41eb-983c-49c105f3475e";
const configuration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};
const user = {
  primaryKey: userId,
  nickname: "Player One",
  avatar:
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
  avatarMimeType: "image/png",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  claims: [],
  roles: [],
};
const operation = {
  primaryKey: eventId,
  title: "Operation Copper Trail",
  description:
    "## Mission objectives\n- :objective: Coordinate the reconnaissance team.\n- :radio: Report extraction readiness.\n\n## Situation\nProceed through the **northern approach**.",
  terrainPrimaryKey: 1,
  terrainTitle: "Altis",
  terrainImagePath: "",
  terrainIsActive: true,
  modPackRevisionPrimaryKey: 24,
  modPackRevisionTimeStampCreated: "2026-08-01T00:00:00Z",
  modPackRevisionHtml: "<html></html>",
  modPackRevisionIsActive: true,
  modPackDefinitionPrimaryKey: 7,
  modPackDefinitionTimeStampCreated: "2026-08-01T00:00:00Z",
  modPackDefinitionTitle: "UTF Main",
  modPackDefinitionIsActive: true,
  modPackDefinitionIsComposition: false,
  imagePath: "",
  scheduledForOriginal: "2030-09-12T18:00:00Z",
  scheduledFor: "2030-09-12T18:00:00Z",
  timeStampCreated: "2026-08-01T10:00:00Z",
  isVisible: true,
  acceptedCount: 3,
  rejectedCount: 1,
  maybeCount: 2,
  minimumAccepted: 12,
  owner: { primaryKey: userId, nickname: "Player One" },
  hostedBy: { primaryKey: "2fdd4802-e081-4baa-9120-f1873653a057", nickname: "Raven" },
};
const slots = [
  {
    slotNumber: 1,
    assignedToFk: userId,
    eventFk: eventId,
    isSelfAssignable: true,
    isVisible: true,
    group: "Alpha",
    title: "Team Lead",
    roleSymbol: "Rifleman",
    side: "West",
  },
  {
    slotNumber: 2,
    assignedToFk: null,
    eventFk: eventId,
    isSelfAssignable: true,
    isVisible: true,
    group: "Alpha",
    title: "Rifleman",
    roleSymbol: "Rifleman",
    side: "West",
  },
  {
    slotNumber: 3,
    assignedToFk: null,
    eventFk: eventId,
    isSelfAssignable: false,
    isVisible: true,
    group: "Alpha",
    title: "Medic",
    roleSymbol: "Medic",
    side: "East",
  },
];

test("renders the event workflow and confirms attendance changes that release a slot", async ({
  page,
}) => {
  const mutationPaths: string[] = [];
  await page.route("**/api/**", (route) => {
    throw new Error(`Unexpected API request: ${route.request().method()} ${route.request().url()}`);
  });
  await page.route("**/appsettings.json", (route) => route.fulfill({ json: configuration }));
  await page.route("**/api/users/me", (route) => route.fulfill({ json: user }));
  await page.route("**/api/users/authentication-providers", (route) =>
    route.fulfill({ json: { steam: true, discord: true } }),
  );
  await page.route("**/api/game-servers/notifications", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/site-image-settings", (route) => route.fulfill({ json: [] }));
  await page.route(`**/api/events/${eventId}`, (route) => route.fulfill({ json: operation }));
  await page.route(`**/api/events/${eventId}/slotting/all`, (route) =>
    route.fulfill({ json: slots }),
  );
  await page.route(`**/api/users/${userId}`, (route) => route.fulfill({ json: user }));
  await page.route(`**/api/events/${eventId}/maybe`, (route) => {
    mutationPaths.push(new URL(route.request().url()).pathname);
    return route.fulfill({ status: 204 });
  });
  await page.route(`**/api/events/${eventId}/slotting/2/assign`, (route) => {
    mutationPaths.push(new URL(route.request().url()).pathname);
    return route.fulfill({ status: 204 });
  });
  await page.route("**/api/events/upcoming", (route) => route.fulfill({ json: [] }));

  await page.goto(`events/${eventId}`);

  await expect(page.getByRole("heading", { name: operation.title })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Slot register" })).toBeVisible();
  await expect(page.locator(".em-briefing-objectives")).toContainText(
    "Coordinate the reconnaissance team.",
  );
  await expect(page.locator('[data-tactical-icon="objective"]')).toBeVisible();
  await expect(page.locator('[data-tactical-icon="radio"]')).toBeVisible();
  const commanderNotes = page.locator(".em-briefing-notes");
  await expect(commanderNotes).toContainText("Proceed through the northern approach.");
  await expect(commanderNotes.locator("strong")).toHaveText("northern approach");
  await expect(commanderNotes).not.toContainText("Coordinate the reconnaissance team.");
  await expect(commanderNotes).not.toContainText("Report extraction readiness.");
  const westSide = page.locator('.em-slot-side[data-side="west"]');
  const eastSide = page.locator('.em-slot-side[data-side="east"]');
  await expect(westSide.getByRole("heading", { name: "Alpha" })).toBeVisible();
  await expect(eastSide.getByRole("heading", { name: "Alpha" })).toBeVisible();
  await expect(westSide.locator('[data-tactical-icon="rifleman"]').first()).toBeVisible();
  await expect(eastSide.locator('[data-tactical-icon="medical"]')).toBeVisible();
  await expect(
    westSide
      .locator(".em-slot-row", { hasText: "Team Lead" })
      .locator('[data-tactical-icon="rifleman"]'),
  ).toBeVisible();
  await expect(page.locator(".em-slot-avatars img")).toHaveAttribute(
    "src",
    `data:image/png;base64,${user.avatar}`,
  );
  const avatarBounds = await page.locator(".em-slot-avatars img").boundingBox();
  expect(avatarBounds?.width).toBe(32);
  expect(avatarBounds?.height).toBe(32);
  await expect(page.locator(".em-slot-status").first()).toContainText("Your slot");
  await expect(page.getByRole("button", { name: "Your slot" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Assign slot" })).toBeEnabled();

  await page.getByRole("button", { name: /Pending/ }).click();
  await expect(
    page.getByRole("heading", { name: "Confirm the loss of the slot selection" }),
  ).toBeVisible();
  expect(mutationPaths).toEqual([]);

  await page.getByRole("button", { name: "Confirm the change of the acceptance status" }).click();
  await expect.poll(() => mutationPaths).toEqual([`/api/events/${eventId}/maybe`]);

  await page.getByRole("button", { name: "Assign to self" }).click();
  await expect
    .poll(() => mutationPaths)
    .toEqual([`/api/events/${eventId}/maybe`, `/api/events/${eventId}/slotting/2/assign`]);

  await page.setViewportSize({ width: 390, height: 844 });
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      ),
    )
    .toBeLessThanOrEqual(0);
});
