import { expect, test } from "@playwright/test";

const eventId = "01a0102f-3f75-7c4a-bd99-ed6273e83565";
const operatorId = "00000000-0000-0000-0000-000000000001";
const hostId = "2fdd4802-e081-4baa-9120-f1873653a057";
const assetId = "4a7e2d42-50b0-4a7d-a381-a4f20a1f5dc2";
const galleryAssets = Array.from({ length: 48 }, (_, index) => ({
  primaryKey: index === 0 ? assetId : `4a7e2d42-50b0-4a7d-a381-${String(index).padStart(12, "0")}`,
  originalFileName: index === 0 ? "operation-cover.webp" : `operation-cover-${index}.webp`,
  contentType: "image/webp",
  size: 1536 + index,
  imagePath: `/api/media-assets/${index === 0 ? assetId : `gallery-${index}`}/content`,
  tags: ["event"],
  usages: [],
}));

test("creates through the certified alias and edits in the command workspace", async ({ page }) => {
  let createdEvent: Record<string, unknown> | undefined;
  let createdSlot: Record<string, unknown> | undefined;
  let updatedEvent: Record<string, unknown> | undefined;
  let updatedSlot: Record<string, unknown> | undefined;

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
  await page.route("**/api/game-servers/notifications", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/game-servers/capabilities", (route) =>
    route.fulfill({
      json: [
        {
          gameIdentifier: "arma3",
          displayName: "Arma 3",
          requiresScenario: false,
          requiresModPack: true,
          actions: [],
          runtimeProviders: [],
          runtimeProviderProfiles: [],
        },
      ],
    }),
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
      json: galleryAssets,
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
  await page.route(`**/api/events/${eventId}/slotting/1/update`, async (route) => {
    updatedSlot = (await route.request().postDataJSON()) as Record<string, unknown>;
    return route.fulfill({ status: 204 });
  });

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
  const galleryDialog = page.locator("dialog.gallery-picker-dialog");
  const galleryWorkspace = galleryDialog.locator(".gallery-picker-dialog__workspace");
  const galleryResults = galleryWorkspace.locator(":scope > section");
  await page.setViewportSize({ width: 1440, height: 900 });
  const desktopScrollBeforePicker = await page.evaluate(() => window.scrollY);
  await page.getByRole("button", { name: "Select image" }).click();
  await expect(galleryDialog).toBeVisible();
  await expect(galleryDialog.locator("footer")).toBeVisible();
  const desktopDialogBounds = await galleryDialog.boundingBox();
  expect(desktopDialogBounds).not.toBeNull();
  expect(desktopDialogBounds!.x + desktopDialogBounds!.width).toBeLessThanOrEqual(1440);
  expect(desktopDialogBounds!.y + desktopDialogBounds!.height).toBeLessThanOrEqual(900);
  expect(desktopDialogBounds!.width).toBeLessThanOrEqual(962);
  expect(
    await galleryWorkspace.evaluate(
      (element) => getComputedStyle(element).gridTemplateColumns.split(" ").length,
    ),
  ).toBe(2);
  expect(
    await galleryDialog.evaluate((element) => element.scrollHeight <= element.clientHeight),
  ).toBe(true);
  expect(await page.evaluate(() => window.scrollY)).toBe(desktopScrollBeforePicker);
  await galleryDialog.locator("footer").getByRole("button", { name: "Cancel" }).click();

  await page.setViewportSize({ width: 1024, height: 640 });
  await page.getByRole("button", { name: "Select image" }).click();
  await expect(galleryDialog).toBeVisible();
  const documentScrollBeforePicker = await page.evaluate(() => window.scrollY);
  await expect(galleryDialog.locator("footer")).toBeVisible();
  const dialogBounds = await galleryDialog.boundingBox();
  expect(dialogBounds).not.toBeNull();
  expect(dialogBounds!.x).toBeGreaterThanOrEqual(0);
  expect(dialogBounds!.y).toBeGreaterThanOrEqual(0);
  expect(dialogBounds!.x + dialogBounds!.width).toBeLessThanOrEqual(1024);
  expect(dialogBounds!.y + dialogBounds!.height).toBeLessThanOrEqual(640);
  expect(dialogBounds!.width).toBeLessThanOrEqual(962);
  expect(
    await galleryDialog.evaluate((element) => element.scrollHeight <= element.clientHeight),
  ).toBe(true);
  expect(
    await galleryResults.evaluate((element) => element.scrollHeight > element.clientHeight),
  ).toBe(true);
  await galleryResults.evaluate((element) => {
    element.scrollTop = element.scrollHeight;
  });
  expect(await galleryResults.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
  expect(await page.evaluate(() => window.scrollY)).toBe(documentScrollBeforePicker);
  await page.getByRole("button", { name: /operation-cover\.webp/ }).click();
  await page.getByRole("button", { name: "Apply", exact: true }).click();
  const createImagePreview = page.locator(".event-authoring-image-preview");
  const createImageBounds = await createImagePreview.boundingBox();
  expect(createImageBounds).not.toBeNull();
  expect(createImageBounds!.width).toBeLessThanOrEqual(288);
  expect(createImageBounds!.width / createImageBounds!.height).toBeCloseTo(16 / 9, 1);
  expect(await createImagePreview.evaluate((element) => getComputedStyle(element).minHeight)).toBe(
    "0px",
  );
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
  const editImageBounds = await page.locator(".event-authoring-image-preview").boundingBox();
  expect(editImageBounds).not.toBeNull();
  expect(editImageBounds!.width).toBeLessThanOrEqual(288);
  expect(editImageBounds!.width / editImageBounds!.height).toBeCloseTo(16 / 9, 1);
  expect(createdEvent).toMatchObject({
    title: "Operation Northstar",
    hostedByFk: hostId,
    terrainFk: 3,
    modPackRevisionFk: 8,
    imageAssetFk: assetId,
  });
  expect(createdSlot).toMatchObject({ group: "Alpha", title: "Rifleman", side: "West" });

  await page.getByRole("tab", { name: /Objectives/ }).click();
  const objectiveIconGroup = page.locator("fieldset.event-objective-icon-picker");
  await expect(objectiveIconGroup).toHaveAccessibleName(/Icon Objective/);
  await expect(objectiveIconGroup.getByRole("radio")).toHaveCount(15);
  await expect(objectiveIconGroup.locator('[data-tactical-icon="objective"] svg')).toBeVisible();
  await objectiveIconGroup.locator('label[data-icon="map"]').click();
  await expect(objectiveIconGroup.getByRole("radio", { name: "Map" })).toBeChecked();
  await expect(objectiveIconGroup).toHaveAccessibleName(/Icon Map/);
  await page.getByRole("tab", { name: /Details/ }).click();

  await page.getByRole("textbox", { name: "Title" }).fill("Operation Northstar II");
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(page.getByText("Event changes and slot plan saved.").last()).toBeVisible();
  expect(updatedEvent).toMatchObject({
    title: "Operation Northstar II",
    description: expect.stringContaining("- :map: Secure the objective."),
  });

  await page.getByRole("textbox", { name: "Title" }).fill("");
  await page.getByRole("tab", { name: /Slots/ }).click();
  await page.getByRole("textbox", { name: "Group" }).fill("Bravo");
  await page.getByRole("button", { name: "Save slot plan", exact: true }).click();
  await expect(page.getByText("Slot plan saved.").last()).toBeVisible();
  expect(updatedSlot).toMatchObject({ group: "Bravo" });
});
