import { expect, test } from "@playwright/test";

const eventId = "16979a3f-57bc-49ea-8f93-c22ecfbd6eb2";
const operatorId = "00000000-0000-0000-0000-000000000001";
const hostId = "2fdd4802-e081-4baa-9120-f1873653a057";
const assetId = "4a7e2d42-50b0-4a7d-a381-a4f20a1f5dc2";

test("creates through the certified alias and edits in the command workspace", async ({ page }) => {
  let createdEvent: Record<string, unknown> | undefined;
  let createdSlot: Record<string, unknown> | undefined;
  let updatedEvent: Record<string, unknown> | undefined;

  await page.route("**/api/**", (route) => {
    if (!new URL(route.request().url()).pathname.startsWith("/api/")) return route.continue();
    throw new Error(`Unexpected API request: ${route.request().method()} ${route.request().url()}`);
  });
  await page.route("**/appsettings.json", (route) =>
    route.fulfill({
      json: {
        Api: { BaseUrl: "/api/" },
        Urls: { TeamSpeak3: "", Discord: "" },
        Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
      },
    }),
  );
  await page.route("**/Insignia-48px.png", (route) =>
    route.fulfill({ status: 200, contentType: "image/png", body: "" }),
  );
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      json: {
        primaryKey: operatorId,
        nickname: "Planner",
        isBanned: false,
        isVerified: true,
        isDeleted: false,
        isVereinMember: false,
        claims: [{ identifier: "adm:ev", value: null }],
        roles: [],
      },
    }),
  );
  await page.route("**/api/users/authentication-providers", (route) =>
    route.fulfill({ json: { steam: true, discord: true } }),
  );
  await page.route("**/api/site-image-settings", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/users/all?**", (route) =>
    route.fulfill({
      json: [
        {
          primaryKey: hostId,
          nickname: "Raven",
          isBanned: false,
          isVerified: true,
          isDeleted: false,
          claims: [],
          roles: [],
        },
      ],
    }),
  );
  await page.route("**/api/terrains/all/count", (route) => route.fulfill({ json: 1 }));
  await page.route("**/api/terrains/all?**", (route) =>
    route.fulfill({
      json: [
        {
          primaryKey: 3,
          title: "Altis",
          gameIdentifier: "arma3",
          isActive: true,
          imageAssetFk: assetId,
          imagePath: `/api/media-assets/${assetId}/content`,
        },
      ],
    }),
  );
  await page.route("**/api/mod-packs/all?**", (route) =>
    route.fulfill({
      json: [
        {
          primaryKey: 4,
          title: "UTF Main",
          gameIdentifier: "arma3",
          isActive: true,
          isComposition: false,
          owner: { primaryKey: operatorId, nickname: "Planner" },
          modPackRevisions: [
            {
              primaryKey: 8,
              definitionFk: 4,
              isActive: true,
              timeStampCreated: "2026-08-14T09:00:00Z",
            },
          ],
        },
      ],
    }),
  );
  await page.route("**/api/media-assets?**", (route) =>
    route.fulfill({
      json: [
        {
          primaryKey: assetId,
          originalFileName: "operation-cover.webp",
          contentType: "image/webp",
          size: 1536,
          imagePath: `/api/media-assets/${assetId}/content`,
          tags: ["event"],
          usages: [],
        },
      ],
    }),
  );
  await page.route("**/api/media-assets/*/content", (route) =>
    route.fulfill({ status: 200, contentType: "image/webp", body: "" }),
  );
  await page.route("**/api/events/create", async (route) => {
    createdEvent = (await route.request().postDataJSON()) as Record<string, unknown>;
    return route.fulfill({ json: { primaryKey: eventId } });
  });
  await page.route(`**/api/events/${eventId}/slotting/create`, async (route) => {
    createdSlot = (await route.request().postDataJSON()) as Record<string, unknown>;
    return route.fulfill({ json: { slotNumber: 1, eventFk: eventId, ...createdSlot } });
  });
  await page.route(`**/api/events/${eventId}`, (route) =>
    route.fulfill({
      json: {
        primaryKey: eventId,
        title: "Operation Northstar",
        description: "# Situation\n\nSecure the objective.",
        terrainPrimaryKey: 3,
        terrainTitle: "Altis",
        modPackRevisionPrimaryKey: 8,
        modPackDefinitionPrimaryKey: 4,
        modPackDefinitionTitle: "UTF Main",
        imageAssetFk: assetId,
        imagePath: `/api/media-assets/${assetId}/content`,
        scheduledForOriginal: "2026-08-15T18:00:00Z",
        scheduledFor: "2026-08-15T18:00:00Z",
        timeStampCreated: "2026-08-14T09:00:00Z",
        isVisible: true,
        acceptedCount: 0,
        rejectedCount: 0,
        maybeCount: 0,
        minimumAccepted: 8,
        owner: { primaryKey: operatorId, nickname: "Planner" },
        hostedBy: { primaryKey: hostId, nickname: "Raven" },
      },
    }),
  );
  await page.route(`**/api/events/${eventId}/slotting/all`, (route) =>
    route.fulfill({
      json: [
        {
          slotNumber: 1,
          eventFk: eventId,
          assignedToFk: null,
          group: "Alpha",
          title: "Rifleman",
          side: "West",
          isSelfAssignable: true,
          isVisible: true,
        },
      ],
    }),
  );
  await page.route(`**/api/events/${eventId}/update`, async (route) => {
    updatedEvent = (await route.request().postDataJSON()) as Record<string, unknown>;
    return route.fulfill({ status: 204 });
  });
  await page.route(`**/api/events/${eventId}/slotting/1/update`, (route) =>
    route.fulfill({ status: 204 }),
  );

  await page.goto("events/create");
  await expect(page.getByRole("heading", { name: "Create Event", exact: true })).toBeVisible();
  await expect(page.getByText("Command Center").first()).toBeVisible();
  await page.getByRole("textbox", { name: "Title" }).fill("Operation Northstar");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.getByRole("combobox", { name: "Host" }).click();
  await page.getByRole("option", { name: "Raven" }).click();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.getByRole("combobox", { name: "Terrain" }).click();
  await page.getByRole("option", { name: "Altis" }).click();
  await page.getByRole("combobox", { name: "Mod pack revision" }).click();
  await page.getByRole("option", { name: "UTF Main" }).click();
  await page.getByRole("button", { name: "Select image" }).click();
  await page.getByRole("button", { name: /operation-cover\.webp/ }).click();
  await page.getByRole("button", { name: "Apply", exact: true }).click();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page
    .getByRole("textbox", { name: "Briefing markdown" })
    .fill("# Situation\n\nSecure the objective.");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.getByRole("button", { name: "Add slot" }).click();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(page.getByRole("button", { name: "Create event", exact: true })).toBeEnabled();
  await page
    .locator("#event-create-form")
    .evaluate((form: HTMLFormElement) => form.requestSubmit());

  await expect(page).toHaveURL(new RegExp(`/next/command/events/${eventId}$`));
  expect(createdEvent).toMatchObject({
    title: "Operation Northstar",
    hostedByFk: hostId,
    terrainFk: 3,
    modPackRevisionFk: 8,
    imageAssetFk: assetId,
  });
  expect(createdSlot).toMatchObject({ group: "Alpha", title: "Rifleman", side: "West" });

  await page.getByRole("textbox", { name: "Title" }).fill("Operation Northstar II");
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(page.getByText("Event changes and slot plan saved.")).toBeVisible();
  expect(updatedEvent).toMatchObject({ title: "Operation Northstar II" });
});
