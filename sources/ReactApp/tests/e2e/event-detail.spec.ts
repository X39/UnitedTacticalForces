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
  description: "## Situation\nCoordinate the reconnaissance team.",
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
    side: "West",
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
});
