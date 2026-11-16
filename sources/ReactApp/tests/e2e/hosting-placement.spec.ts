import { expect, test } from "@playwright/test";

const serverId = "10000000-0000-4000-8000-000000000001";
const server = {
  primaryKey: serverId,
  title: "Placement test server",
  gameIdentifier: "arma3",
  runtimeProviderIdentifier: "fake-cloud",
  runtimeProfileIdentifier: "medium",
  runtimeProfileDisplayName: "Medium",
  runtimeStorageStrategy: "IncludedDisk",
  externalStorageSizeGiB: null,
  estimatedHourlyCostEur: 0.2,
  estimatedMonthlyCostEur: 146,
  selectedConfigurationProfileId: null,
  selectedConfigurationProfileTitle: null,
  costOwnerId: serverId,
  costOwnerName: "Operations",
  createdById: serverId,
  isLaunchDisabled: false,
  isArchived: false,
  canModify: true,
  timeStampCreated: "2026-11-16T10:00:00Z",
  timeStampUpdated: "2026-11-16T10:00:00Z",
  pendingJobCount: 0,
  deployment: null,
  actions: [],
};

test("saves Arma placement through typed configuration and reloads its estimate", async ({
  page,
}) => {
  let placement = "separate-host";
  const requests: string[] = [];
  const configuration = () => ({
    serverId,
    profileId: null,
    profileRevisionId: null,
    profileRevisionNumber: null,
    isComplete: true,
    entries: [
      {
        key: "headlessClientPlacement",
        targetPath: "",
        group: "Hosting",
        displayName: "Headless Client placement",
        description: "Selects placement for the next deployment.",
        kind: "Selection",
        required: false,
        sensitive: false,
        isControlPlaneOnly: true,
        order: 10,
        minimum: null,
        maximum: null,
        allowedValues: [],
        choices: [
          { value: "separate-host", label: "Separate host", description: null },
          { value: "same-host", label: "Same host", description: null },
        ],
        value: placement,
        source: placement === "separate-host" ? "Default" : "Server",
        isCompatible: true,
        isRedacted: false,
        warning: null,
      },
    ],
  });
  await page.addInitScript(() => localStorage.setItem("UtfCulture", "en"));
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
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    requests.push(`${request.method()} ${path}`);
    if (path === "/api/users/me") {
      return route.fulfill({
        json: {
          primaryKey: serverId,
          nickname: "Operator",
          isBanned: false,
          isVerified: true,
          isDeleted: false,
          isVereinMember: false,
          claims: [{ identifier: "sv", value: serverId }],
          roles: [],
        },
      });
    }
    if (path === "/api/users/authentication-providers") {
      return route.fulfill({ json: { steam: true, discord: true } });
    }
    if (path === `/api/game-servers/${serverId}/configuration`) {
      if (request.method() === "PUT") {
        expect(request.postDataJSON()).toEqual({
          values: [{ key: "headlessClientPlacement", value: "same-host" }],
          resetKeys: [],
        });
        placement = "same-host";
      } else {
        expect(request.method()).toBe("GET");
      }
      return route.fulfill({ json: configuration() });
    }
    if (path === `/api/game-servers/${serverId}/storage-demand`) {
      const hostCount = placement === "same-host" ? 1 : 2;
      return route.fulfill({
        json: {
          steadyStateRequiredBytes: 1024,
          preparationWorkspaceBytes: 0,
          safetyMarginPercent: 0,
          safetyMarginBytes: 0,
          peakPreparationRequiredBytes: 1024,
          status: "Complete",
          calculatedAt: "2026-11-16T10:00:00Z",
          missingComponents: [],
          components: [],
          runtimeProfiles: [],
          placementEstimate: {
            hostCount,
            estimatedComputeHourlyCostEur: hostCount * 0.1,
            estimatedStorageHourlyCostEur: 0,
            estimatedHourlyCostEur: hostCount * 0.1,
            estimatedMonthlyCostEur: hostCount * 73,
          },
        },
      });
    }
    if (path === `/api/game-servers/${serverId}`) return route.fulfill({ json: server });
    if (
      [
        "/api/site-image-settings",
        "/api/game-servers/notifications",
        `/api/game-servers/${serverId}/jobs`,
        `/api/game-servers/${serverId}/operations`,
        `/api/game-servers/${serverId}/configuration/profile-options`,
      ].includes(path)
    ) {
      return route.fulfill({ json: [] });
    }
    throw new Error(`Unexpected API request: ${request.method()} ${path}`);
  });

  await page.goto(`command/servers/${serverId}?tab=configuration`);
  const selector = page.getByRole("combobox", { name: "Headless Client placement" });
  await expect(selector).toHaveValue("separate-host");
  await expect(selector.getByRole("option", { name: "Separate server", exact: true })).toHaveCount(
    1,
  );
  await expect(selector.getByRole("option", { name: "Same server", exact: true })).toHaveCount(1);
  await expect(page.getByText("€0.20", { exact: true })).toBeVisible();

  await selector.selectOption("same-host");
  await expect(page.getByText("€0.20", { exact: true })).not.toBeVisible();
  await page.getByRole("button", { name: "Save overrides", exact: true }).click();
  await expect(page.getByText("€0.10", { exact: true })).toBeVisible();
  const saveIndex = requests.indexOf(`PUT /api/game-servers/${serverId}/configuration`);
  expect(saveIndex).toBeGreaterThan(-1);
  expect(requests.slice(saveIndex + 1)).toContain(
    `GET /api/game-servers/${serverId}/storage-demand`,
  );

  await page.reload();
  await expect(selector).toHaveValue("same-host");
  await expect(page.getByText("€0.10", { exact: true })).toBeVisible();
  expect(requests.filter((request) => request.startsWith("PUT "))).toHaveLength(1);
  expect(requests.some((request) => request.startsWith("POST "))).toBe(false);
  expect(requests.some((request) => request.includes("/files/"))).toBe(false);
});
