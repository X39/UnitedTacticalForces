import { act, fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import { ApiError } from "@/api/client";
import i18n, { i18nReady } from "@/app/i18n";
import {
  AutomationSettingsConflictError,
  hostingQueryKeys,
  hostingService,
} from "@/features/hosting/services";
import type { GameServer } from "@/features/hosting/types";
import {
  automationWith,
  managedServer,
  renderPanel,
  saveButton,
  savedRule,
} from "../helpers/automationPanelHarness";

/* The scheduled-start consent dialog and the consent-needed notice of the Automation panel (D-08, AUT-03). */

const stopRule = savedRule({
  id: "b0000000-0000-4000-8000-000000000002",
  action: "Stop",
  days: [6],
  time: "00:00",
  nextRunAt: "2026-09-04T22:00:00Z",
});

/** A managed server with cost estimates, whose automation holds the given saved rules. */
function serverWith(
  options: {
    schedules?: ReturnType<typeof savedRule>[];
    consent?: "NotRequired" | "Given" | "Needed";
    canManage?: boolean;
    estimates?: boolean;
  } = {},
): GameServer {
  const server = managedServer({
    canManage: options.canManage ?? true,
    automation: automationWith(
      { timeZone: "Europe/Berlin", schedules: options.schedules ?? [], version: 4 },
      { startConsent: { state: options.consent ?? "NotRequired" } },
    ),
  });
  return options.estimates === false
    ? server
    : { ...server, estimatedHourlyCostEur: 0.0119, estimatedMonthlyCostEur: 8.5 };
}

const addButton = () => screen.getByRole("button", { name: "Add a schedule" });
const consentDialog = () => screen.getByRole("dialog", { name: "Allow scheduled starts?" });

/** Adds a rule and fills it in. */
async function addRule(
  user: ReturnType<typeof userEvent.setup>,
  number: number,
  options: { action?: "Start" | "Stop"; day: string; time: string },
) {
  await user.click(addButton());
  const group = screen.getByRole("group", { name: `Schedule ${number}` });
  if (options.action === "Stop") {
    fireEvent.click(within(group).getByRole("radio", { name: /^Shut down and stop billing/ }));
  }
  fireEvent.click(within(group).getByRole("checkbox", { name: options.day }));
  fireEvent.change(within(group).getByLabelText("Time"), { target: { value: options.time } });
}

describe("Scheduled start consent", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("opens the dialog with the cost owner, the estimates and the start count instead of sending", async () => {
    const user = userEvent.setup();
    const update = vi.spyOn(hostingService, "saveAutomationSettings");
    renderPanel(serverWith());
    await addRule(user, 1, { day: "Fri", time: "18:00" });
    await addRule(user, 2, { day: "Sat", time: "18:00" });
    await user.click(saveButton());

    const dialog = consentDialog();
    expect(update).not.toHaveBeenCalled();
    expect(within(dialog).getByText("Cost owner").nextElementSibling).toHaveTextContent(
      "Operations",
    );
    expect(within(dialog).getByText("Per hour").nextElementSibling).toHaveTextContent("€0.0119");
    expect(
      within(dialog).getByText("Per month if left running").nextElementSibling,
    ).toHaveTextContent("€8.50");
    expect(within(dialog).getByText("2 schedules start the server")).toBeVisible();
    expect(
      within(dialog).getByText("A start is skipped when the cost owner cannot pay for it."),
    ).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Keep editing" })).toHaveFocus();
  });

  it("reads No estimate for missing cost estimates and one schedule in the singular", async () => {
    const user = userEvent.setup();
    renderPanel(serverWith({ estimates: false }));
    await addRule(user, 1, { day: "Fri", time: "18:00" });
    await user.click(saveButton());

    const dialog = consentDialog();
    expect(within(dialog).getByText("Per hour").nextElementSibling).toHaveTextContent(
      "No estimate",
    );
    expect(
      within(dialog).getByText("Per month if left running").nextElementSibling,
    ).toHaveTextContent("No estimate");
    expect(within(dialog).getByText("1 schedule starts the server")).toBeVisible();
  });

  it("sends one update with the consent, blocks both buttons meanwhile, then closes and focuses Save", async () => {
    const user = userEvent.setup();
    let finish: (value: ReturnType<typeof automationWith>) => void = () => undefined;
    const update = vi.spyOn(hostingService, "saveAutomationSettings").mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    renderPanel(serverWith());
    await addRule(user, 1, { day: "Fri", time: "18:00" });
    await user.click(saveButton());
    await user.click(
      within(consentDialog()).getByRole("button", { name: "Allow scheduled starts" }),
    );

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    expect(update.mock.calls[0]?.[1]).toEqual(
      expect.objectContaining({
        version: 4,
        startConsent: true,
        schedules: [{ id: null, action: "Start", days: [5], time: "18:00" }],
      }),
    );
    const dialog = consentDialog();
    expect(within(dialog).getByRole("button", { name: "Saving..." })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
    expect(within(dialog).getByRole("button", { name: "Keep editing" })).toBeDisabled();

    await act(async () => {
      finish(
        automationWith(
          { timeZone: "Europe/Berlin", version: 5, schedules: [savedRule()] },
          { startConsent: { state: "Given" } },
        ),
      );
    });

    await waitFor(() =>
      expect(
        screen.queryByRole("dialog", { name: "Allow scheduled starts?" }),
      ).not.toBeInTheDocument(),
    );
    expect(await screen.findByText("Saved.")).toBeVisible();
    expect(await screen.findByText("Automation settings saved.")).toBeInTheDocument();
    expect(saveButton()).toHaveFocus();
  });

  it("shows a failed save and a conflict inside the dialog and keeps the draft when editing goes on", async () => {
    const user = userEvent.setup();
    const update = vi
      .spyOn(hostingService, "saveAutomationSettings")
      .mockRejectedValueOnce(new ApiError("unavailable", 503, "server"))
      .mockRejectedValueOnce(new AutomationSettingsConflictError());
    renderPanel(serverWith());
    await addRule(user, 1, { day: "Fri", time: "18:00" });
    await user.click(saveButton());
    const confirm = () =>
      within(consentDialog()).getByRole("button", { name: "Allow scheduled starts" });

    await user.click(confirm());
    expect(
      await within(consentDialog()).findByText(
        "The settings could not be saved. Nothing was changed. Try again.",
      ),
    ).toBeVisible();
    expect(within(consentDialog()).getByRole("alert")).toBeVisible();

    await user.click(confirm());
    expect(
      await within(consentDialog()).findByText(
        "These settings were changed elsewhere. Reload them and try again.",
      ),
    ).toBeVisible();
    expect(update).toHaveBeenCalledTimes(2);

    await user.click(within(consentDialog()).getByRole("button", { name: "Keep editing" }));

    expect(
      screen.queryByRole("dialog", { name: "Allow scheduled starts?" }),
    ).not.toBeInTheDocument();
    expect(saveButton()).toHaveFocus();
    const group = screen.getByRole("group", { name: "Schedule 1" });
    expect(within(group).getByLabelText("Time")).toHaveValue("18:00");
    expect(within(group).getByRole("checkbox", { name: "Fri" })).toBeChecked();
    expect(screen.getByRole("button", { name: "Reload settings" })).toBeVisible();
  });

  it("declining keeps the draft and sends nothing", async () => {
    const user = userEvent.setup();
    const update = vi.spyOn(hostingService, "saveAutomationSettings");
    renderPanel(serverWith());
    await addRule(user, 1, { day: "Fri", time: "18:00" });
    await user.click(saveButton());
    await user.click(within(consentDialog()).getByRole("button", { name: "Keep editing" }));

    expect(update).not.toHaveBeenCalled();
    expect(
      screen.queryByRole("dialog", { name: "Allow scheduled starts?" }),
    ).not.toBeInTheDocument();
    expect(saveButton()).toHaveFocus();
    expect(screen.getByText("You have unsaved changes.")).toBeVisible();
  });

  it("asks again for a Start rule that changed, never for stop rules or other settings", async () => {
    const user = userEvent.setup();
    const update = vi
      .spyOn(hostingService, "saveAutomationSettings")
      .mockResolvedValue(automationWith({ version: 5, schedules: [savedRule(), stopRule] }));
    renderPanel(serverWith({ schedules: [savedRule(), stopRule], consent: "Given" }));

    const stop = screen.getByRole("group", { name: "Schedule 2" });
    fireEvent.change(within(stop).getByLabelText("Time"), { target: { value: "01:30" } });
    await user.click(screen.getByRole("switch", { name: "Limit the runtime" }));
    await user.click(saveButton());

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    expect(
      screen.queryByRole("dialog", { name: "Allow scheduled starts?" }),
    ).not.toBeInTheDocument();
    expect(update.mock.calls[0]?.[1]).toEqual(expect.objectContaining({ startConsent: false }));
    await screen.findByText("Saved.");

    const start = screen.getByRole("group", { name: "Schedule 1" });
    fireEvent.change(within(start).getByLabelText("Time"), { target: { value: "19:00" } });
    await user.click(saveButton());
    expect(consentDialog()).toBeVisible();
    expect(update).toHaveBeenCalledTimes(1);
  });

  it("explains paused starts and lets a manager confirm them again with the saved settings", async () => {
    const user = userEvent.setup();
    const update = vi
      .spyOn(hostingService, "saveAutomationSettings")
      .mockResolvedValue(
        automationWith(
          { timeZone: "Europe/Berlin", version: 5, schedules: [savedRule(), stopRule] },
          { startConsent: { state: "Given" } },
        ),
      );
    const { queryClient, server } = renderPanel(
      serverWith({ schedules: [savedRule(), stopRule], consent: "Needed" }),
    );

    expect(screen.getByRole("heading", { name: "Scheduled starts are paused" })).toBeVisible();
    expect(
      screen.getByText(
        "The cost owner of this server changed. Confirm scheduled starts again to resume them.",
      ),
    ).toBeVisible();
    const start = screen.getByRole("group", { name: "Schedule 1" });
    expect(within(start).getByText("Paused until scheduled starts are confirmed.")).toBeVisible();
    expect(
      within(screen.getByRole("group", { name: "Schedule 2" })).getByText(/^Next run: Sat/),
    ).toBeVisible();

    // An unsaved edit elsewhere in the panel is not part of the reconfirmation.
    await user.click(screen.getByRole("switch", { name: "Limit the runtime" }));
    await user.click(screen.getByRole("button", { name: "Confirm scheduled starts" }));
    expect(within(consentDialog()).getByText("1 schedule starts the server")).toBeVisible();
    await user.click(
      within(consentDialog()).getByRole("button", { name: "Allow scheduled starts" }),
    );

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    expect(update.mock.calls[0]?.[1]).toEqual(
      expect.objectContaining({
        version: 4,
        startConsent: true,
        maxRuntimeEnabled: false,
        schedules: [
          { id: savedRule().id, action: "Start", days: [5], time: "18:00" },
          { id: stopRule.id, action: "Stop", days: [6], time: "00:00" },
        ],
      }),
    );
    expect(await screen.findByText("Automation settings saved.")).toBeInTheDocument();
    expect(screen.getByRole("switch", { name: "Limit the runtime" })).toBeChecked();
    expect(
      queryClient.getQueryData<GameServer>(hostingQueryKeys.server(server.primaryKey))?.automation
        ?.startConsent.state,
    ).toBe("Given");
    expect(
      screen.queryByRole("heading", { name: "Scheduled starts are paused" }),
    ).not.toBeInTheDocument();
  });

  it("tells a viewer who cannot manage the settings to ask the server owner, without a button", () => {
    renderPanel(serverWith({ schedules: [savedRule()], consent: "Needed", canManage: false }));

    expect(screen.getByRole("heading", { name: "Scheduled starts are paused" })).toBeVisible();
    expect(
      screen.getByText(
        "The cost owner of this server changed. The server owner has to confirm scheduled starts again.",
      ),
    ).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "Confirm scheduled starts" }),
    ).not.toBeInTheDocument();
  });

  it("shows no paused notice for a Stop rule only or when no consent is needed", () => {
    const { unmount } = renderPanel(serverWith({ schedules: [stopRule], consent: "Needed" }));
    expect(
      screen.queryByRole("heading", { name: "Scheduled starts are paused" }),
    ).not.toBeInTheDocument();
    unmount();

    renderPanel(serverWith({ schedules: [savedRule()], consent: "Given" }));
    expect(
      screen.queryByRole("heading", { name: "Scheduled starts are paused" }),
    ).not.toBeInTheDocument();
    expect(screen.getByText(/^Next run: Fri/)).toBeVisible();
  });
});
