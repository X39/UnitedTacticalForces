import { expect, test, type Page } from "@playwright/test";
import { assertNoGermanLeak } from "../support/germanLeak";
import {
  installJourney,
  journeyIds,
  journeyServer,
  journeyServerTitle,
} from "../support/journeyMocks";

/** The name the journey gives the new server. */
const serverName = "Journey server";

/** The raw stage tokens the API sends; none of them may ever be visible text. */
const rawStageText = /provider-creating|provider-deleting|waiting-for-worker/;

/** The visible copy this journey reads, copied from the German locale resources. */
const copy = {
  fleetTitle: "Server",
  create: "Server erstellen",
  preset: "Voreinstellung",
  startNow: /^Jetzt starten/,
  submit: "Server erstellen und starten",
  startTitle: `${serverName} starten?`,
  costFact: "Geschätzte Kosten",
  hourly: /0,05\s*€ pro Stunde, solange er läuft/,
  ownerFact: "Kostenträger",
  stopFact: "So beendest du es",
  startDismiss: "Server nicht starten",
  startConfirm: "Server starten",
  startToast: `Start für ${serverName} angefordert.`,
  startStep: "Cloud-Maschine wird vorbereitet",
  running: "Läuft",
  costing: "Kosten laufen",
  stop: "Herunterfahren und Abrechnung beenden",
  stopTitle: `${serverName} herunterfahren und Abrechnung beenden?`,
  stopDismiss: "Server weiterlaufen lassen",
  stopToast: `Herunterfahren für ${serverName} angefordert.`,
  stopStep: "Cloud-Maschine wird freigegeben",
  toastClose: "Schließen",
  navigation: "Serverbereiche",
  modPack: "Mod-Paket",
  barLabel: "Ausstehende Änderungen",
  bar: "1 Änderung vorgemerkt. Nichts wird gespeichert, bis du sie anwendest.",
  review: "Änderungen prüfen",
  apply: "1 Änderung anwenden",
  added: "Hinzugefügt (1)",
  removed: "Entfernt (1)",
  effectOff: `Diese Änderungen gelten, sobald ${journeyServerTitle} das nächste Mal startet.`,
  keepEditing: "Weiter bearbeiten",
} as const;

/**
 * Asserts that the page is German: the document language, and no English UI text. The English-text check is
 * `assertNoGermanLeak`, which also fails when the page has no visible text at all, so a step can never pass vacuously.
 *
 * @param page The page after the step that just ran.
 */
async function expectGerman(page: Page): Promise<void> {
  await expect(page.locator("html")).toHaveAttribute("lang", "de");
  await assertNoGermanLeak(page);
}

/**
 * J-3 (VER-02, L10N-03): J-1 and the first half of J-2 in German. After every step the page has `lang="de"` and no
 * English text, the action and dismiss labels are German and costs read with a comma decimal and the euro sign.
 */
test.describe("J-3 German locale", () => {
  test("create then start in German, then stage and review a mod pack change", async ({ page }) => {
    const journey = await installJourney(page, {
      culture: "de",
      servers: [journeyServer({ id: journeyIds.off, title: journeyServerTitle, modPackId: 1 })],
    });

    // The fleet with its create link.
    await page.goto("command/servers");
    await expect(
      page.getByRole("heading", { level: 1, name: copy.fleetTitle, exact: true }),
    ).toBeVisible();
    await expect(page.getByText(journeyServerTitle).first()).toBeVisible();
    await expectGerman(page);

    // The create page.
    await page.getByRole("link", { name: copy.create, exact: true }).first().click();
    await expect(page.getByRole("heading", { level: 1, name: copy.create })).toBeVisible();
    await expectGerman(page);
    await page.locator("input[name=title]").fill(serverName);
    await page
      .getByRole("combobox", { name: copy.preset, exact: true })
      .selectOption({ label: "Operations" });
    await page.getByRole("radio", { name: copy.startNow }).check();
    await expectGerman(page);

    // The start confirmation: German labels and a German currency format.
    await page.getByRole("button", { name: copy.submit }).click();
    await expect(page).toHaveURL(new RegExp(`/command/servers/${journeyIds.created}$`));
    const dialog = page.locator("dialog[open]");
    await expect(dialog.getByRole("heading", { name: copy.startTitle })).toBeVisible();
    await expect(dialog.getByText(copy.costFact, { exact: true })).toBeVisible();
    await expect(dialog.getByText(copy.hourly)).toBeVisible();
    await expect(dialog.getByText(copy.ownerFact, { exact: true })).toBeVisible();
    await expect(dialog.getByText(copy.stopFact, { exact: true })).toBeVisible();
    await expect(dialog.getByRole("button", { name: copy.startDismiss })).toBeFocused();
    await expect(
      dialog.getByRole("button", { name: copy.startConfirm, exact: true }),
    ).toBeVisible();
    await expectGerman(page);

    // Confirming: the German toast with its German close label, and the strip names the stage in German.
    await dialog.getByRole("button", { name: copy.startConfirm, exact: true }).click();
    await expect(page.locator("dialog[open]")).toHaveCount(0);
    const startToast = page.locator(".app-toast").filter({ hasText: copy.startToast });
    await expect(startToast).toHaveCount(1);
    await expect(startToast.locator(".app-toast__dismiss")).toHaveAttribute(
      "aria-label",
      copy.toastClose,
    );
    await expect(page.locator('.job-progress [aria-current="step"]')).toContainText(copy.startStep);
    await expect(page.locator("body")).not.toContainText(rawStageText);
    await expectGerman(page);

    // The pushed frame: Running with the cost chip.
    await journey.pushRunning(journeyIds.created);
    await expect(page.locator(".workspace-header .server-state-badge")).toHaveText(copy.running);
    await expect(page.locator(".workspace-header .server-cost-indicator")).toContainText(
      copy.costing,
    );
    await expectGerman(page);

    // The stop confirmation and its German toast.
    await page.getByRole("button", { name: copy.stop }).click();
    const stopDialog = page.locator("dialog[open]");
    await expect(stopDialog.getByRole("heading", { name: copy.stopTitle })).toBeVisible();
    await expect(stopDialog.getByRole("button", { name: copy.stopDismiss })).toBeFocused();
    await expectGerman(page);
    await stopDialog.getByRole("button", { name: copy.stop }).click();
    await expect(page.locator("dialog[open]")).toHaveCount(0);
    await expect(page.locator(".app-toast").filter({ hasText: copy.stopToast })).toHaveCount(1);
    await expect(page.locator(".job-progress .job-progress__step--current")).toContainText(
      copy.stopStep,
    );
    await expect(page.locator("body")).not.toContainText(rawStageText);
    await expectGerman(page);

    // The first half of J-2 on the other server: stage a pack, see the count and the diff.
    await page.goto(`command/servers/${journeyIds.off}?tab=settings`);
    const modPack = page.getByRole("combobox", { name: copy.modPack, exact: true });
    await expect(modPack).toBeVisible();
    await expectGerman(page);
    await modPack.selectOption({ value: "2" });
    const bar = page.getByRole("region", { name: copy.barLabel });
    await expect(bar.getByText(copy.bar, { exact: true })).toBeVisible();
    await expectGerman(page);
    await bar.getByRole("button", { name: copy.review }).click();
    const review = page.getByRole("dialog", { name: copy.review });
    await expect(review).toBeVisible();
    await expect(review.getByText(copy.effectOff, { exact: true })).toBeVisible();
    await review.getByRole("button", { name: "2 Mod-Änderungen anzeigen" }).click();
    await expect(review.getByText(copy.added, { exact: true })).toBeVisible();
    await expect(review.getByText("Mod C", { exact: true })).toBeVisible();
    await expect(review.getByText(copy.removed, { exact: true })).toBeVisible();
    await expect(review.getByText("Mod A", { exact: true })).toBeVisible();
    await expect(review.getByRole("button", { name: copy.apply })).toBeVisible();
    await expectGerman(page);
    await review.getByRole("button", { name: copy.keepEditing }).click();
    await expect(review).toBeHidden();
    await expectGerman(page);

    expect(journey.session.unexpected).toEqual([]);
  });
});
