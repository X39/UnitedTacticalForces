import { expect, test } from "@playwright/test";

const configuration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const administrator = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Treasurer",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: true,
  claims: [{ identifier: "verein:kasse", value: null }],
  roles: [],
};

test.beforeEach(async ({ page }) => {
  await page.route("**/api/**", (route) => {
    throw new Error(`Unexpected API request: ${route.request().method()} ${route.request().url()}`);
  });
  await page.route("**/appsettings.json", (route) => route.fulfill({ json: configuration }));
  await page.route("**/Insignia-48px.png", (route) =>
    route.fulfill({ status: 200, contentType: "image/png", body: "" }),
  );
  await page.route("**/api/users/me", (route) => route.fulfill({ json: administrator }));
  await page.route("**/api/users/authentication-providers", (route) =>
    route.fulfill({ json: { steam: true, discord: true } }),
  );
  await page.route("**/api/site-image-settings", (route) => route.fulfill({ json: [] }));
});

test("filters, updates, and safely deletes a Kassenbuch entry", async ({ page }) => {
  let description = "Voice server renewal";
  let deleted = false;
  const ledgerEntry = () => ({
    primaryKey: 42,
    timeStampBooked: "2026-08-12T22:00:00Z",
    direction: "Expense",
    amount: 125.5,
    currency: "EUR",
    category: "Infrastructure",
    description,
    reference: "Invoice 42",
    documentNumber: "UTF-2026-42",
    isDeleted: deleted,
    timeStampCreated: "2026-08-13T10:00:00Z",
    timeStampUpdated: "2026-08-14T10:00:00Z",
    createdByName: "Treasurer",
    updatedByName: "Treasurer",
  });
  await page.route("**/api/verein/kassenbuch?**", (route) => {
    const url = new URL(route.request().url());
    if (url.searchParams.get("search")) {
      expect(url.searchParams.get("search")).toBe("invoice");
      expect(url.searchParams.get("category")).toBe("Infrastructure");
    }
    return route.fulfill({
      json: {
        entries: deleted ? [] : [ledgerEntry()],
        totalCount: deleted ? 0 : 1,
        incomeTotal: 500,
        expenseTotal: deleted ? 0 : 125.5,
        balance: deleted ? 500 : 374.5,
        currency: "EUR",
      },
    });
  });
  await page.route("**/api/verein/kassenbuch/42", async (route) => {
    if (route.request().method() === "PUT") {
      const payload = route.request().postDataJSON() as { description?: string };
      description = payload.description ?? description;
      return route.fulfill({ json: ledgerEntry() });
    }
    expect(route.request().method()).toBe("DELETE");
    deleted = true;
    return route.fulfill({ json: ledgerEntry() });
  });

  await page.goto("command/verein/kassenbuch");
  await expect(
    page.getByRole("heading", { level: 1, name: "Kassenbuch", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("row", { name: /Voice server renewal/ })).toBeVisible();
  await page.getByPlaceholder("Search ledger entries").fill("invoice");
  await page.getByLabel("Category").fill("Infrastructure");
  await page.getByRole("button", { name: "Apply filters" }).click();
  await expect(page.getByRole("row", { name: /UTF-2026-42/ })).toBeVisible();

  await page.getByRole("link", { name: "Edit" }).click();
  await expect(page).toHaveURL(/\/next\/command\/verein\/kassenbuch\/42\/edit$/);
  await page.getByLabel("Description").fill("Voice server renewal 2027");
  await page.getByRole("button", { name: "Update entry" }).click();
  await expect(page).toHaveURL(/\/next\/command\/verein\/kassenbuch$/);
  await expect(page.getByText("Kassenbuch entry saved.")).toBeVisible();
  await expect(page.getByRole("row", { name: /Voice server renewal 2027/ })).toBeVisible();

  await page.getByRole("link", { name: "Edit" }).click();
  await page.getByRole("button", { name: "Delete", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText("member-visible Kassenbuch");
  await dialog.getByRole("button", { name: "Delete entry" }).click();
  await expect(page).toHaveURL(/\/next\/command\/verein\/kassenbuch$/);
  await expect(page.getByText("Kassenbuch entry deleted.")).toBeVisible();
  await expect(page.getByText("No ledger entries match the current filters.")).toBeVisible();
});

test("creates a Kassenbuch entry and preserves normalized money fields", async ({ page }) => {
  await page.route("**/api/verein/kassenbuch?**", (route) =>
    route.fulfill({
      json: {
        entries: [],
        totalCount: 0,
        incomeTotal: 0,
        expenseTotal: 0,
        balance: 0,
        currency: "EUR",
      },
    }),
  );
  await page.route("**/api/verein/kassenbuch", async (route) => {
    expect(route.request().method()).toBe("POST");
    expect(route.request().headers()["x-utf-ui-variant"]).toBe("react");
    expect(route.request().postDataJSON()).toMatchObject({
      direction: "Income",
      amount: 250.75,
      currency: "USD",
      category: "Donations",
      description: "Community donation",
      reference: "Campaign 2026",
      documentNumber: null,
    });
    return route.fulfill({
      json: {
        primaryKey: 43,
        timeStampBooked: "2026-08-14T00:00:00Z",
        direction: "Income",
        amount: 250.75,
        currency: "USD",
        category: "Donations",
        description: "Community donation",
        reference: "Campaign 2026",
        documentNumber: null,
        isDeleted: false,
      },
    });
  });

  await page.goto("command/verein/kassenbuch/create");
  await page.getByLabel("Amount").fill("250.75");
  await page.getByLabel("Currency").fill("usd");
  await page.getByLabel("Category").fill("Donations");
  await page.getByLabel("Description").fill("Community donation");
  await page.getByLabel("Reference").fill("Campaign 2026");
  await page.getByRole("button", { name: "Add entry" }).click();

  await expect(page).toHaveURL(/\/next\/command\/verein\/kassenbuch$/);
  await expect(page.getByText("Kassenbuch entry saved.")).toBeVisible();
});
