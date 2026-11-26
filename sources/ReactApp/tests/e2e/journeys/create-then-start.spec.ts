import { expect, test, type Page } from "@playwright/test";
import { installJourney, journeyIds } from "../support/journeyMocks";

/** The name the journey gives the new server. */
const serverName = "Journey server";

/** The raw stage tokens the API sends; none of them may ever be visible text. */
const rawStageText = /provider-creating|provider-deleting|waiting-for-worker/;

/**
 * Asserts that focus did not fall to the page body after a confirmation closed.
 *
 * The shared confirmation returns focus to the control that asked. A confirmed action turns that control into the
 * pending "In progress" button, which React mounts anew, so the provider falls back to the workspace heading, the
 * documented target for a control that no longer exists. Either place keeps the keyboard position; the body does not.
 *
 * @param page The page whose focus is checked, after the dialog closed.
 */
async function expectFocusReturned(page: Page): Promise<void> {
  await expect
    .poll(() =>
      page.evaluate(() => {
        const active = document.activeElement;
        if (active === null || active === document.body) return "body";
        if (active.id === "workspace-title") return "heading";
        return active.closest(".workspace-header__action") === null ? "elsewhere" : "action";
      }),
    )
    .toMatch(/^(heading|action)$/);
}

/**
 * J-1 (VER-02): a member creates a server from the fleet, starts it through the shared cost confirmation, watches the
 * progress strip name the stage, sees Running with the cost chip after a pushed frame and stops it through the same
 * confirmation. The run covers the desktop project and the 390 phone width.
 */
test.describe("J-1 create then start", () => {
  test("creates, starts, shows Running with the cost chip and stops through the confirmations", async ({
    page,
  }) => {
    const journey = await installJourney(page, { culture: "en" });

    // From the fleet to the create page.
    await page.goto("command/servers");
    await expect(
      page.getByRole("heading", { level: 1, name: "Servers", exact: true }),
    ).toBeVisible();
    await page.getByRole("link", { name: "Create server", exact: true }).first().click();
    await expect(page.getByRole("heading", { level: 1, name: "Create server" })).toBeVisible();

    // A name, a preset and the start choice.
    await page.locator("input[name=title]").fill(serverName);
    await page
      .getByRole("combobox", { name: "Preset", exact: true })
      .selectOption({ label: "Operations" });
    await page.getByRole("radio", { name: /^Start it now/ }).check();
    await page.getByRole("button", { name: "Create server and start" }).click();

    // The workspace opens the shared start confirmation with the cost facts.
    await expect(page).toHaveURL(new RegExp(`/command/servers/${journeyIds.created}$`));
    const dialog = page.locator("dialog[open]");
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole("heading", { name: `Start ${serverName}?` })).toBeVisible();
    await expect(dialog.getByText("Estimated cost", { exact: true })).toBeVisible();
    await expect(dialog.getByText(/0\.05.* per hour while it runs/)).toBeVisible();
    await expect(dialog.getByText("Cost owner", { exact: true })).toBeVisible();
    await expect(dialog.getByText("Operations", { exact: true })).toBeVisible();
    await expect(dialog.getByText("How to stop", { exact: true })).toBeVisible();
    await expect(dialog.getByRole("button", { name: "Don't start server" })).toBeFocused();
    expect(journey.requests.filter((request) => request.path.endsWith("/actions/start"))).toEqual(
      [],
    );
    expect(journey.requests[0]).toMatchObject({
      method: "POST",
      path: "/api/game-servers",
      body: { title: serverName, modPackDefinitionId: 7 },
    });

    // Confirming queues the start; the toast is the start toast and the strip names the stage.
    await dialog.getByRole("button", { name: "Start server", exact: true }).click();
    await expect(page.locator("dialog[open]")).toHaveCount(0);
    await expectFocusReturned(page);
    await expect(
      page.locator(".app-toast").filter({ hasText: `Start requested for ${serverName}.` }),
    ).toHaveCount(1);
    const strip = page.locator(".job-progress");
    await expect(strip).toBeVisible();
    await expect(strip.locator('[aria-current="step"]')).toContainText(
      "Preparing the cloud machine",
    );
    await expect(page.locator("body")).not.toContainText(rawStageText);
    await expect(page.locator(".workspace-header .server-state-badge")).toHaveText("Starting");

    // A pushed frame turns the server to Running with the cost chip.
    await journey.pushRunning(journeyIds.created);
    await expect(page.locator(".workspace-header .server-state-badge")).toHaveText("Running");
    await expect(page.locator(".workspace-header .server-cost-indicator")).toContainText(
      "Costing now",
    );
    await expect(strip).toHaveCount(0);
    await expect(page.locator("body")).not.toContainText(rawStageText);

    // Stop goes through the same confirmation; focus returns to the button that asked.
    const stop = page.getByRole("button", { name: "Shut down and stop billing" });
    await stop.click();
    const stopDialog = page.locator("dialog[open]");
    await expect(
      stopDialog.getByRole("heading", { name: `Shut down ${serverName} and stop billing?` }),
    ).toBeVisible();
    await expect(stopDialog.getByRole("button", { name: "Keep server running" })).toBeFocused();
    await stopDialog.getByRole("button", { name: "Shut down and stop billing" }).click();
    await expect(page.locator("dialog[open]")).toHaveCount(0);
    await expect(
      page.locator(".app-toast").filter({ hasText: `Shutdown requested for ${serverName}.` }),
    ).toHaveCount(1);
    await expectFocusReturned(page);
    await expect(page.locator(".job-progress .job-progress__step--current")).toContainText(
      "Releasing the cloud machine",
    );
    await expect(page.locator("body")).not.toContainText(rawStageText);

    // The pushed stop result brings the server back to Off with no cost.
    await journey.pushStopped(journeyIds.created);
    await expect(page.locator(".workspace-header .server-state-badge")).toHaveText("Off");
    await expect(page.locator(".workspace-header .server-cost-indicator")).toContainText(
      "Not costing",
    );

    expect(journey.requests.map((request) => `${request.method} ${request.path}`)).toEqual([
      "POST /api/game-servers",
      `POST /api/game-servers/${journeyIds.created}/actions/start`,
      `POST /api/game-servers/${journeyIds.created}/actions/stop`,
    ]);
    expect(journey.session.unexpected).toEqual([]);
  });
});
