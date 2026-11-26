import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { hostingService } from "@/features/hosting/services";
import {
  automationWith,
  managedServer,
  renderPanel,
  saveButton,
  savedRule,
} from "../helpers/automationPanelHarness";

/* The Schedules sub-section of the Automation panel: the time zone and the weekly start and stop rules (AUT-03). */

/** The limits of a server that allows three schedules, so the limit is easy to reach. */
const smallLimits = {
  idleMinutesMin: 5,
  idleMinutesMax: 720,
  maxRuntimeHoursMin: 1,
  maxRuntimeHoursMax: 168,
  maxSchedules: 3,
  warningLeadMinutes: 5,
};

const ruleGroup = (index: number) => screen.getByRole("group", { name: `Schedule ${index}` });
const addButton = () => screen.getByRole("button", { name: "Add a schedule" });
const zoneSelect = (): HTMLSelectElement => screen.getByLabelText("Time zone");

/** Picks the action, the days and the time of the rule group, the way a viewer would. */
function fillRule(
  group: HTMLElement,
  options: { action?: "Start" | "Stop"; days: string[]; time: string },
) {
  if (options.action === "Stop") {
    fireEvent.click(within(group).getByRole("radio", { name: /^Shut down and stop billing/ }));
  }
  for (const day of options.days) {
    fireEvent.click(within(group).getByRole("checkbox", { name: day }));
  }
  fireEvent.change(within(group).getByLabelText("Time"), { target: { value: options.time } });
}

describe("Automation panel schedules", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the empty state above the Add row with the counter and the browser zone as the default", () => {
    const { container } = renderPanel(
      managedServer({
        automation: automationWith({}, { limits: { ...smallLimits, maxSchedules: 8 } }),
      }),
    );

    expect(screen.getByRole("heading", { name: "Schedules" })).toBeVisible();
    expect(screen.getByRole("heading", { name: "No schedules set" })).toBeVisible();
    expect(screen.getByText("0 of 8 schedules set")).toBeVisible();
    expect(addButton()).not.toHaveAttribute("aria-disabled");
    expect(zoneSelect()).toHaveValue(new Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC");
    expect(container.querySelector("ul.automation-panel__rules")).toBeNull();
  });

  it("lists UTC first and the other zones in region groups that keep the shared select styling", () => {
    renderPanel();

    const select = zoneSelect();
    expect(select).toHaveClass("form-field__control--select");
    expect(select.firstElementChild).toHaveValue("UTC");
    const groups = [...select.querySelectorAll("optgroup")].map((group) => group.label);
    expect(groups).toContain("Europe");
    expect(groups).toEqual([...groups].sort());
    expect(within(select).getByRole("option", { name: "Europe/Berlin" })).toHaveAttribute(
      "lang",
      "en",
    );
    expect(within(select).getByRole("option", { name: "America/Los Angeles" })).toBeInTheDocument();
  });

  it("keeps a saved zone the runtime does not list as an option and does not call it a change", () => {
    const { onDirtyChange } = renderPanel(
      managedServer({ automation: automationWith({ timeZone: "Mars/Olympus_Mons" }) }),
    );

    expect(zoneSelect()).toHaveValue("Mars/Olympus_Mons");
    expect(within(zoneSelect()).getByRole("option", { name: "Mars/Olympus Mons" })).toBeVisible();
    expect(onDirtyChange).toHaveBeenLastCalledWith(false);
  });

  it("is not dirty with no rule and the default zone, and dirty once the zone changes", async () => {
    const user = userEvent.setup();
    const { onDirtyChange } = renderPanel();

    expect(onDirtyChange).toHaveBeenLastCalledWith(false);
    const browserZone = zoneSelect().value;
    await user.selectOptions(
      zoneSelect(),
      browserZone === "Asia/Tokyo" ? "America/Chicago" : "Asia/Tokyo",
    );
    expect(onDirtyChange).toHaveBeenLastCalledWith(true);
    expect(screen.getByText("You have unsaved changes.")).toBeVisible();
  });

  it("adds a blank rule with Start chosen, focuses its first radio and announces it without a change", async () => {
    const user = userEvent.setup();
    const { onDirtyChange, container } = renderPanel();
    await user.click(addButton());

    const group = ruleGroup(1);
    const start = within(group).getByRole("radio", { name: /^Start server/ });
    expect(start).toBeChecked();
    expect(start).toHaveFocus();
    expect(within(group).getByLabelText("Time")).toHaveValue("");
    expect(
      within(group)
        .getAllByRole("checkbox")
        .every((box) => !(box as HTMLInputElement).checked),
    ).toBe(true);
    expect(within(group).getByText("Choose days and a time.")).toBeVisible();
    expect(within(group).getByText("Not saved yet.")).toBeVisible();
    expect(within(group).getByText("Has not run yet.")).toBeVisible();
    expect(container.querySelector('[aria-live="polite"]')).toHaveTextContent("Schedule added.");
    expect(screen.queryByRole("heading", { name: "No schedules set" })).not.toBeInTheDocument();
    expect(screen.getByText("1 of 20 schedules set")).toBeVisible();
    expect(onDirtyChange).toHaveBeenLastCalledWith(false);
  });

  it("draws the seven weekdays as toggle chips that still toggle as checkboxes", async () => {
    const user = userEvent.setup();
    renderPanel();
    await user.click(addButton());
    const group = ruleGroup(1);

    const chips = group.querySelectorAll(".schedule-rule__day-grid .toggle-chip");
    expect(chips).toHaveLength(7);
    expect(group.querySelectorAll(".schedule-rule__day-grid .form-field__check")).toHaveLength(0);
    const friday = within(group).getByRole("checkbox", { name: "Fri" });
    expect(friday).not.toBeChecked();
    fireEvent.click(friday);
    expect(friday).toBeChecked();
    expect(friday.closest(".toggle-chip")).not.toBeNull();
  });

  it("updates the summary sentence as days and time change", async () => {
    const user = userEvent.setup();
    renderPanel();
    await user.click(addButton());
    const group = ruleGroup(1);

    fillRule(group, { days: ["Fri"], time: "18:00" });
    expect(within(group).getByText(/^Weekly on Friday at 6:00\sPM$/)).toBeVisible();

    for (const day of ["Mon", "Tue", "Wed", "Thu"]) {
      fireEvent.click(within(group).getByRole("checkbox", { name: day }));
    }
    expect(within(group).getByText(/^On weekdays at 6:00\sPM$/)).toBeVisible();

    for (const day of ["Sat", "Sun"])
      fireEvent.click(within(group).getByRole("checkbox", { name: day }));
    expect(within(group).getByText(/^Every day at 6:00\sPM$/)).toBeVisible();

    fireEvent.change(within(group).getByLabelText("Time"), { target: { value: "" } });
    expect(within(group).getByText("Choose days and a time.")).toBeVisible();
  });

  it("wraps the long German summary with the long weekday names", async () => {
    await i18n.changeLanguage("de");
    const user = userEvent.setup();
    renderPanel();
    await user.click(screen.getByRole("button", { name: "Zeitplan hinzufügen" }));
    const group = screen.getByRole("group", { name: "Zeitplan 1" });

    for (const day of [/^Mo/, /^Mi/, /^Fr/]) {
      fireEvent.click(within(group).getByRole("checkbox", { name: day }));
    }
    fireEvent.change(within(group).getByLabelText("Uhrzeit"), { target: { value: "18:00" } });

    expect(
      within(group).getByText("Wöchentlich am Montag, Mittwoch und Freitag um 18:00 Uhr"),
    ).toBeVisible();
    expect(screen.getByText("1 von 20 Zeitplänen festgelegt")).toBeVisible();
  });

  it("saves a Friday start and a Saturday stop with ISO days and HH:mm, then shows the saved runs in order", async () => {
    const user = userEvent.setup();
    const server = managedServer({
      automation: automationWith({ timeZone: "Europe/Berlin", version: 2 }),
    });
    const update = vi.spyOn(hostingService, "saveAutomationSettings").mockResolvedValue(
      automationWith({
        timeZone: "Europe/Berlin",
        version: 3,
        schedules: [
          savedRule({
            id: "b0000000-0000-4000-8000-000000000002",
            action: "Stop",
            days: [6],
            time: "00:00",
            nextRunAt: "2026-09-04T22:00:00Z",
          }),
          savedRule({ nextRunAt: "2026-09-04T16:00:00Z" }),
        ],
      }),
    );
    renderPanel(server);
    await user.click(addButton());
    fillRule(ruleGroup(1), { days: ["Fri"], time: "18:00" });
    await user.click(addButton());
    fillRule(ruleGroup(2), { action: "Stop", days: ["Sat"], time: "00:00" });
    await user.click(saveButton());
    // The start rule is new, so the consent dialog asks first.
    await user.click(await screen.findByRole("button", { name: "Allow scheduled starts" }));

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    const body = update.mock.calls[0]?.[1];
    expect(body).toEqual(
      expect.objectContaining({
        version: 2,
        timeZone: "Europe/Berlin",
        startConsent: true,
        schedules: [
          { id: null, action: "Start", days: [5], time: "18:00" },
          { id: null, action: "Stop", days: [6], time: "00:00" },
        ],
      }),
    );
    expect(await screen.findByText("Saved.")).toBeVisible();

    const first = ruleGroup(1);
    expect(within(first).getByRole("radio", { name: /^Start server/ })).toBeChecked();
    expect(within(first).getByText(/^Next run: Fri.*6:00\sPM$/)).toBeVisible();
    expect(within(first).getByText("Has not run yet.")).toBeVisible();
    const second = ruleGroup(2);
    expect(
      within(second).getByRole("radio", { name: /^Shut down and stop billing/ }),
    ).toBeChecked();
    expect(within(second).getByText(/^Next run: Sat.*12:00\sAM$/)).toBeVisible();
  });

  it("blocks a save with a rule without days or time and focuses the first invalid field", async () => {
    const user = userEvent.setup();
    const update = vi.spyOn(hostingService, "saveAutomationSettings");
    renderPanel();
    await user.click(addButton());
    const group = ruleGroup(1);
    expect(within(group).queryByText("Enter a time.")).not.toBeInTheDocument();

    await user.click(saveButton());

    expect(update).not.toHaveBeenCalled();
    expect(within(group).getByText("Choose at least one day.")).toBeVisible();
    expect(within(group).getByText("Enter a time.")).toBeVisible();
    expect(within(group).getByLabelText("Time")).toHaveAttribute("aria-invalid", "true");
    expect(within(group).getByLabelText("Time")).toHaveFocus();
    expect(screen.getByText("Fix the 2 highlighted fields before saving.")).toBeVisible();
  });

  it("marks both rules that share a day and time as duplicates", async () => {
    const user = userEvent.setup();
    const update = vi.spyOn(hostingService, "saveAutomationSettings");
    renderPanel();
    await user.click(addButton());
    await user.click(addButton());
    fillRule(ruleGroup(1), { days: ["Fri", "Sat"], time: "18:00" });
    fillRule(ruleGroup(2), { action: "Stop", days: ["Sat"], time: "18:00" });
    await user.click(saveButton());

    const message = "Another schedule already runs at this time on one of these days.";
    expect(update).not.toHaveBeenCalled();
    expect(within(ruleGroup(1)).getByText(message)).toBeVisible();
    expect(within(ruleGroup(2)).getByText(message)).toBeVisible();
    expect(within(ruleGroup(1)).getByLabelText("Time")).toHaveFocus();
  });

  it("renumbers on removal and moves focus to the next rule, then the previous one, then Add", async () => {
    const user = userEvent.setup();
    const { container } = renderPanel();
    for (let count = 0; count < 3; count += 1) await user.click(addButton());
    fillRule(ruleGroup(1), { days: ["Mon"], time: "08:00" });
    fillRule(ruleGroup(2), { days: ["Tue"], time: "09:00" });
    fillRule(ruleGroup(3), { days: ["Wed"], time: "10:00" });

    await user.click(screen.getByRole("button", { name: "Remove schedule 2" }));
    expect(screen.queryByRole("group", { name: "Schedule 3" })).not.toBeInTheDocument();
    expect(within(ruleGroup(2)).getByLabelText("Time")).toHaveValue("10:00");
    expect(within(ruleGroup(2)).getByRole("radio", { name: /^Start server/ })).toHaveFocus();
    expect(container.querySelector('[aria-live="polite"]')).toHaveTextContent("Schedule removed.");

    await user.click(screen.getByRole("button", { name: "Remove schedule 2" }));
    expect(within(ruleGroup(1)).getByRole("radio", { name: /^Start server/ })).toHaveFocus();

    await user.click(screen.getByRole("button", { name: "Remove schedule 1" }));
    expect(addButton()).toHaveFocus();
    expect(screen.getByRole("heading", { name: "No schedules set" })).toBeVisible();
    expect(screen.getByText("0 of 20 schedules set")).toBeVisible();
  });

  it("disables Add at the limit and describes it by the counter", async () => {
    const user = userEvent.setup();
    renderPanel(managedServer({ automation: automationWith({}, { limits: smallLimits }) }));
    for (let count = 0; count < 3; count += 1) await user.click(addButton());

    expect(addButton()).toHaveAttribute("aria-disabled", "true");
    expect(addButton()).toHaveAccessibleDescription("3 of 3 schedules set");
    await user.click(addButton());
    expect(screen.getAllByRole("group", { name: /^Schedule \d$/ })).toHaveLength(3);
  });

  it("describes the saved rule: an edited rule reads Not saved yet and a Failed run links to the activity", async () => {
    const user = userEvent.setup();
    renderPanel(
      managedServer({
        automation: automationWith({
          timeZone: "Europe/Berlin",
          schedules: [savedRule({ lastRun: { at: "2026-08-28T16:00:00Z", outcome: "Failed" } })],
        }),
      }),
    );
    const group = ruleGroup(1);

    expect(within(group).getByText(/^Next run: Fri.*6:00\sPM$/)).toBeVisible();
    expect(within(group).getByText(/^Last run, .*: did not finish\.$/)).toBeVisible();
    expect(within(group).getByRole("link", { name: "Open activity details" })).toHaveAttribute(
      "href",
      expect.stringContaining("tab=activity"),
    );

    fireEvent.change(within(group).getByLabelText("Time"), { target: { value: "19:30" } });
    expect(within(group).getByText("Not saved yet.")).toBeVisible();
    expect(within(group).queryByText(/^Next run:/)).not.toBeInTheDocument();
    await user.click(within(group).getByRole("checkbox", { name: "Sat" }));
    expect(within(group).getByText(/^Weekly on Friday and Saturday at 7:30\sPM$/)).toBeVisible();
  });

  it("shows the fixed sentence for a last-run outcome the client does not know", () => {
    renderPanel(
      managedServer({
        automation: automationWith({
          timeZone: "Europe/Berlin",
          schedules: [
            savedRule({
              lastRun: {
                at: "2026-08-28T16:00:00Z",
                outcome: "ThrownIntoTheSea" as string as "Failed",
              },
            }),
          ],
        }),
      }),
    );

    const group = ruleGroup(1);
    expect(within(group).getByText(/^Last run: .*\.$/)).toBeVisible();
    expect(within(group).queryByText(/ThrownIntoTheSea/)).not.toBeInTheDocument();
    expect(
      within(group).queryByRole("link", { name: "Open activity details" }),
    ).not.toBeInTheDocument();
  });

  it("tells a start that waits for a shutdown from a start closed by an unfinished shutdown", () => {
    const outcomes: ReadonlyArray<readonly ["WaitingForStop" | "SkippedStopUnfinished", RegExp]> = [
      ["WaitingForStop", /^Last run, .*: the start waits until the shutdown has finished\.$/],
      ["SkippedStopUnfinished", /^Last run, .*: skipped, the shutdown before it did not finish\.$/],
    ];
    for (const [outcome, sentence] of outcomes) {
      const { unmount } = renderPanel(
        managedServer({
          automation: automationWith({
            timeZone: "Europe/Berlin",
            schedules: [savedRule({ lastRun: { at: "2026-08-28T16:00:00Z", outcome } })],
          }),
        }),
      );

      expect(within(ruleGroup(1)).getByText(sentence)).toBeVisible();
      expect(within(ruleGroup(1)).queryByText(/^Last run: /)).not.toBeInTheDocument();
      unmount();
    }
  });

  it("shows saved rules read-only for a viewer who cannot manage them", () => {
    renderPanel(
      managedServer({
        canManage: false,
        automation: automationWith({ timeZone: "Europe/Berlin", schedules: [savedRule()] }),
      }),
    );

    const group = ruleGroup(1);
    expect(zoneSelect()).toBeDisabled();
    expect(within(group).getByLabelText("Time")).toBeDisabled();
    expect(within(group).getByRole("checkbox", { name: "Fri" })).toBeChecked();
    expect(within(group).getByRole("checkbox", { name: "Fri" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Remove schedule 1" })).toBeDisabled();
    expect(addButton()).toBeDisabled();
  });
});
