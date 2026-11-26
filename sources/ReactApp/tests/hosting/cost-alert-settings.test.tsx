import { useQuery } from "@tanstack/react-query";
import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import { ApiError } from "@/api/client";
import i18n, { i18nReady } from "@/app/i18n";
import { CostAlertSettingsPanel } from "@/features/hosting/CostAlertSettingsPanel";
import {
  CostAlertSettingsConflictError,
  hostingQueryKeys,
  hostingService,
} from "@/features/hosting/services";
import type { CostAlertSettings } from "@/features/hosting/types";
import { costAlertSettings } from "../helpers/costAlertFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

const queryKey = hostingQueryKeys.costAlertSettings("mine");

/** Renders the panel for the given settings. */
function renderPanel(settings: CostAlertSettings = costAlertSettings()) {
  return renderWithProviders(<CostAlertSettingsPanel settings={settings} queryKey={queryKey} />);
}

/** Returns the amount field with the given one-based index. */
function amountField(index: number): HTMLInputElement {
  return screen.getByLabelText(`Alert amount ${index} (EUR)`);
}

describe("Cost alert amounts", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("states that no alert fires until an amount is added when there are no rows", () => {
    renderPanel(costAlertSettings({ thresholds: [] }));

    expect(screen.getByRole("heading", { name: "No alerts set" })).toBeVisible();
    expect(
      screen.getByText("You will not be alerted about cost until you add an amount."),
    ).toBeVisible();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    expect(screen.getByText("0 of 10 amounts set")).toBeVisible();
  });

  it("shows one field per saved amount and the counter below Add", () => {
    renderPanel(costAlertSettings({ thresholds: [5, 10.5] }));

    expect(amountField(1)).toHaveValue("5");
    expect(amountField(2)).toHaveValue("10.5");
    expect(screen.getByText("2 of 10 amounts set")).toBeVisible();
    expect(screen.queryByText("No alerts set")).not.toBeInTheDocument();
    const add = screen.getByRole("button", { name: "Add an amount" });
    const counter = screen.getByText("2 of 10 amounts set");
    expect(add.compareDocumentPosition(counter) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("writes the saved amounts with the decimal separator of the active language", async () => {
    await i18n.changeLanguage("de");
    renderPanel(costAlertSettings({ thresholds: [12.5] }));

    expect(screen.getByLabelText("Warnbetrag 1 (EUR)")).toHaveValue("12,5");
  });

  it("caps the field length and turns off text helpers", () => {
    renderPanel();

    const field = amountField(1);
    expect(field).toHaveAttribute("maxlength", "12");
    expect(field).toHaveAttribute("inputmode", "decimal");
    expect(field).toHaveAttribute("autocomplete", "off");
    expect(field).toHaveAttribute("spellcheck", "false");
    expect(field).toHaveAttribute("type", "text");
  });

  it("moves focus to the new field when an amount is added and announces it", async () => {
    const user = userEvent.setup();
    renderPanel(costAlertSettings({ thresholds: [5] }));

    await user.click(screen.getByRole("button", { name: "Add an amount" }));

    expect(amountField(2)).toHaveFocus();
    expect(screen.getByText("Amount field added.")).toBeInTheDocument();
    expect(screen.getByText("2 of 10 amounts set")).toBeVisible();
  });

  it("names every Remove button by its row and moves focus to the next, the previous, then Add", async () => {
    const user = userEvent.setup();
    renderPanel(costAlertSettings({ thresholds: [1, 2, 3] }));

    expect(screen.getByRole("button", { name: "Remove amount 2" })).toHaveTextContent(
      "Remove amount",
    );

    await user.click(screen.getByRole("button", { name: "Remove amount 1" }));
    expect(amountField(1)).toHaveValue("2");
    expect(amountField(1)).toHaveFocus();
    expect(screen.getByText("Amount removed.")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Remove amount 2" }));
    expect(amountField(1)).toHaveValue("2");
    expect(amountField(1)).toHaveFocus();

    await user.click(screen.getByRole("button", { name: "Remove amount 1" }));
    expect(screen.getByRole("button", { name: "Add an amount" })).toHaveFocus();
    expect(screen.getByRole("heading", { name: "No alerts set" })).toBeVisible();
  });

  it("disables Add at the maximum, described by the counter, and adds nothing", async () => {
    const user = userEvent.setup();
    renderPanel(costAlertSettings({ thresholds: [1, 2, 3], maxThresholds: 3 }));

    const add = screen.getByRole("button", { name: "Add an amount" });
    expect(add).toHaveAttribute("aria-disabled", "true");
    expect(add).toHaveAccessibleDescription("3 of 3 amounts set");
    await user.click(add);
    expect(screen.getAllByRole("textbox")).toHaveLength(3);
    add.focus();
    expect(add).toHaveFocus();
  });

  it("shows a message after blur but not while the first character is typed", async () => {
    const user = userEvent.setup();
    renderPanel(costAlertSettings({ thresholds: [] }));
    await user.click(screen.getByRole("button", { name: "Add an amount" }));

    await user.type(amountField(1), "a");
    expect(screen.queryByText("Enter a number, for example 12.50.")).not.toBeInTheDocument();
    expect(amountField(1)).not.toHaveAttribute("aria-invalid");

    await user.tab();
    expect(await screen.findByText("Enter a number, for example 12.50.")).toBeVisible();
    expect(amountField(1)).toHaveAttribute("aria-invalid", "true");
  });

  it("names the matching reason for each kind of invalid amount", async () => {
    const user = userEvent.setup();
    renderPanel(costAlertSettings({ thresholds: [5] }));
    const cases: [string, string][] = [
      ["1.234", "Use at most two decimal places."],
      ["0", "Enter an amount above 0."],
      ["10000.01", "Enter an amount of at most 10000."],
      ["5", "This amount is already in the list."],
    ];
    await user.click(screen.getByRole("button", { name: "Add an amount" }));

    for (const [text, message] of cases) {
      await user.clear(amountField(2));
      await user.type(amountField(2), text);
      await user.tab();
      expect(await screen.findByText(message)).toBeVisible();
    }
    await user.clear(amountField(2));
    await user.tab();
    expect(await screen.findByText("Enter an amount.")).toBeVisible();
  });

  it("does not send an invalid draft, shows every message and leaves blank rows in place", async () => {
    const user = userEvent.setup();
    const update = vi.spyOn(hostingService, "updateCostAlertSettings");
    renderPanel(costAlertSettings({ thresholds: [] }));
    await user.click(screen.getByRole("button", { name: "Add an amount" }));
    await user.click(screen.getByRole("button", { name: "Add an amount" }));

    await user.click(screen.getByRole("button", { name: "Save alert settings" }));

    expect(update).not.toHaveBeenCalled();
    expect(screen.getAllByText("Enter an amount.")).toHaveLength(2);
    expect(screen.getAllByRole("textbox")).toHaveLength(2);
  });

  it("accepts locale decimals and keeps entry order while editing", async () => {
    const user = userEvent.setup();
    const update = vi
      .spyOn(hostingService, "updateCostAlertSettings")
      .mockImplementation((_owner, request) =>
        Promise.resolve(
          costAlertSettings({
            revision: 2,
            thresholds: [...request.thresholds],
            discordEnabled: request.discordEnabled,
            emailEnabled: request.emailEnabled,
          }),
        ),
      );
    renderPanel(costAlertSettings({ thresholds: [] }));
    await user.click(screen.getByRole("button", { name: "Add an amount" }));
    await user.type(amountField(1), "12,5");
    await user.click(screen.getByRole("button", { name: "Add an amount" }));
    await user.type(amountField(2), "3");

    expect(amountField(1)).toHaveValue("12,5");
    expect(amountField(2)).toHaveValue("3");

    await user.click(screen.getByRole("button", { name: "Save alert settings" }));

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    expect(update.mock.calls[0]?.[1].thresholds).toEqual([3, 12.5]);
    await waitFor(() => expect(amountField(1)).toHaveValue("3"));
    expect(amountField(2)).toHaveValue("12.5");
  });

  it("lines the Remove button up with the control inside the field row", () => {
    renderPanel(costAlertSettings({ thresholds: [5] }));

    const remove = screen.getByRole("button", { name: "Remove amount 1" });
    expect(remove.closest(".form-field__row")).toContainElement(amountField(1));
  });
});

/** Loads the personal settings through a query, as the page does, and renders the panel from the result. */
function QueryHarness() {
  const settings = useQuery({
    queryKey,
    queryFn: ({ signal }) => hostingService.personalCostAlertSettings(signal),
  });
  return settings.data ? (
    <CostAlertSettingsPanel settings={settings.data} queryKey={queryKey} />
  ) : null;
}

/** Returns the alert line of the save row. */
function statusAlert(): HTMLElement {
  return screen
    .getAllByRole("alert")
    .find((element) => element.closest(".cost-alert-settings__status") !== null)!;
}

/** Returns the one polite status line of the save row. */
function statusLine(): HTMLElement {
  return screen
    .getAllByRole("status")
    .find((element) => element.closest(".cost-alert-settings__status") !== null)!;
}

describe("Cost alert settings form", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("keeps one status line mounted and walks pristine, dirty, saved and discarded", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "updateCostAlertSettings").mockResolvedValue(
      costAlertSettings({ revision: 2, thresholds: [5, 10, 20] }),
    );
    renderPanel();

    expect(statusLine()).toHaveTextContent("No unsaved changes.");
    await user.click(screen.getByRole("button", { name: "Add an amount" }));
    expect(statusLine()).toHaveTextContent("No unsaved changes.");
    await user.type(amountField(3), "20");
    expect(statusLine()).toHaveTextContent("You have unsaved changes.");

    await user.click(screen.getByRole("button", { name: "Discard changes" }));
    expect(statusLine()).toHaveTextContent("Changes discarded.");
    expect(screen.getAllByRole("textbox")).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Save alert settings" })).toHaveFocus();

    await user.click(screen.getByRole("button", { name: "Add an amount" }));
    await user.type(amountField(3), "20");
    await user.click(screen.getByRole("button", { name: "Save alert settings" }));
    expect(await screen.findByText("Saved.")).toBeVisible();
    expect(screen.getByRole("button", { name: "Save alert settings" })).toHaveFocus();
  });

  it("sends nothing for an invalid draft, says how many amounts to fix and focuses the first", async () => {
    const user = userEvent.setup();
    const update = vi.spyOn(hostingService, "updateCostAlertSettings");
    renderPanel(costAlertSettings({ thresholds: [5] }));
    await user.click(screen.getByRole("button", { name: "Add an amount" }));
    await user.type(amountField(2), "abc");
    await user.click(screen.getByRole("button", { name: "Add an amount" }));
    await user.click(screen.getByRole("button", { name: "Save alert settings" }));

    expect(update).not.toHaveBeenCalled();
    expect(statusAlert()).toHaveTextContent("Fix the 2 highlighted amounts before saving.");
    expect(amountField(2)).toHaveFocus();

    await user.clear(amountField(2));
    await user.type(amountField(2), "7");
    expect(statusAlert()).toHaveTextContent("Fix the highlighted amount before saving.");
  });

  it("makes every control aria-disabled and focusable while saving, without a second request", async () => {
    const user = userEvent.setup();
    let finish: (value: ReturnType<typeof costAlertSettings>) => void = () => undefined;
    const update = vi.spyOn(hostingService, "updateCostAlertSettings").mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    renderPanel();

    await user.click(screen.getByRole("button", { name: "Save alert settings" }));

    const save = await screen.findByRole("button", { name: "Saving..." });
    expect(save).toHaveAttribute("aria-disabled", "true");
    expect(save).toHaveFocus();
    expect(amountField(1)).toHaveAttribute("aria-disabled", "true");
    expect(amountField(1)).toHaveAttribute("readonly");
    for (const name of ["Add an amount", "Discard changes", "Remove amount 1"]) {
      expect(screen.getByRole("button", { name })).toHaveAttribute("aria-disabled", "true");
    }
    const email = screen.getByRole("switch", { name: "Email" }) as HTMLInputElement;
    expect(email).toHaveAttribute("aria-disabled", "true");
    const before = email.checked;
    await user.click(save);
    await user.click(email);
    expect(update).toHaveBeenCalledTimes(1);
    expect(email.checked).toBe(before);

    await act(async () => finish(costAlertSettings({ revision: 2 })));
    expect(await screen.findByRole("button", { name: "Save alert settings" })).toBeVisible();
  });

  it("keeps the draft and shows an alert when the save fails", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "updateCostAlertSettings").mockRejectedValue(
      new ApiError("boom", 500, "server"),
    );
    renderPanel(costAlertSettings({ thresholds: [5] }));
    await user.clear(amountField(1));
    await user.type(amountField(1), "8");

    await user.click(screen.getByRole("button", { name: "Save alert settings" }));

    await waitFor(() =>
      expect(statusAlert()).toHaveTextContent(
        "The settings could not be saved. Nothing was changed. Try again.",
      ),
    );
    expect(amountField(1)).toHaveValue("8");
    expect(screen.queryByText("boom")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reload settings" })).not.toBeInTheDocument();
  });

  it("keeps the draft on a conflict and replaces it with the latest settings only on Reload", async () => {
    const user = userEvent.setup();
    const personal = vi
      .spyOn(hostingService, "personalCostAlertSettings")
      .mockResolvedValueOnce(costAlertSettings({ revision: 1, thresholds: [5] }))
      .mockResolvedValue(costAlertSettings({ revision: 2, thresholds: [9] }));
    vi.spyOn(hostingService, "updateCostAlertSettings").mockRejectedValue(
      new CostAlertSettingsConflictError(),
    );
    renderWithProviders(<QueryHarness />);
    await user.clear(await screen.findByLabelText("Alert amount 1 (EUR)"));
    await user.type(amountField(1), "8");

    await user.click(screen.getByRole("button", { name: "Save alert settings" }));

    await waitFor(() =>
      expect(statusAlert()).toHaveTextContent(
        "These settings were changed elsewhere. Reload them and try again.",
      ),
    );
    expect(amountField(1)).toHaveValue("8");
    expect(personal).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole("button", { name: "Reload settings" }));

    await waitFor(() => expect(amountField(1)).toHaveValue("9"));
    expect(personal).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole("button", { name: "Reload settings" })).not.toBeInTheDocument();
    expect(statusLine()).toHaveTextContent("No unsaved changes.");
  });

  it("keeps the draft and the conflict when the reload itself fails", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "personalCostAlertSettings")
      .mockResolvedValueOnce(costAlertSettings({ thresholds: [5] }))
      .mockRejectedValue(new ApiError("down", 500, "server"));
    vi.spyOn(hostingService, "updateCostAlertSettings").mockRejectedValue(
      new CostAlertSettingsConflictError(),
    );
    renderWithProviders(<QueryHarness />);
    await user.clear(await screen.findByLabelText("Alert amount 1 (EUR)"));
    await user.type(amountField(1), "8");
    await user.click(screen.getByRole("button", { name: "Save alert settings" }));
    await screen.findByRole("button", { name: "Reload settings" });

    await user.click(screen.getByRole("button", { name: "Reload settings" }));

    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Reload settings" })).not.toHaveAttribute(
        "aria-disabled",
      ),
    );
    expect(amountField(1)).toHaveValue("8");
    expect(statusAlert()).toHaveTextContent("These settings were changed elsewhere");
  });

  it("shows German save failure and conflict text in the reserved status line", async () => {
    await i18n.changeLanguage("de");
    const user = userEvent.setup();
    vi.spyOn(hostingService, "updateCostAlertSettings")
      .mockRejectedValueOnce(new ApiError("boom", 500, "server"))
      .mockRejectedValueOnce(new CostAlertSettingsConflictError());
    renderPanel();

    await user.click(screen.getByRole("button", { name: "Warneinstellungen speichern" }));
    await waitFor(() =>
      expect(statusAlert()).toHaveTextContent(
        "Die Einstellungen konnten nicht gespeichert werden. Es wurde nichts geändert. Versuche es erneut.",
      ),
    );
    await user.click(screen.getByRole("button", { name: "Warneinstellungen speichern" }));
    await waitFor(() =>
      expect(statusAlert()).toHaveTextContent("Diese Einstellungen wurden anderswo geändert."),
    );
    expect(screen.getByRole("button", { name: "Einstellungen neu laden" })).toBeVisible();
  });

  it("is read-only for a viewer who cannot manage: one notice, disabled controls, no save row", () => {
    renderPanel(costAlertSettings({ canManage: false }));

    expect(screen.getAllByText("You can see these settings but not change them.")).toHaveLength(1);
    for (const field of screen.getAllByRole("textbox")) expect(field).toBeDisabled();
    for (const box of screen.getAllByRole("switch")) expect(box).toBeDisabled();
    expect(screen.getByRole("button", { name: "Add an amount" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Remove amount 1" })).toBeDisabled();
    expect(screen.queryByRole("button", { name: "Save alert settings" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Discard changes" })).not.toBeInTheDocument();
  });

  it("follows settings that change elsewhere while pristine and keeps a dirty draft", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "personalCostAlertSettings").mockResolvedValue(
      costAlertSettings({ revision: 1, thresholds: [5] }),
    );
    const { queryClient } = renderWithProviders(<QueryHarness />);
    expect(await screen.findByLabelText("Alert amount 1 (EUR)")).toHaveValue("5");

    act(() => {
      queryClient.setQueryData(queryKey, costAlertSettings({ revision: 2, thresholds: [7] }));
    });
    await waitFor(() => expect(amountField(1)).toHaveValue("7"));

    await user.clear(amountField(1));
    await user.type(amountField(1), "8");
    act(() => {
      queryClient.setQueryData(queryKey, costAlertSettings({ revision: 3, thresholds: [9] }));
    });
    await waitFor(() => expect(statusLine()).toHaveTextContent("You have unsaved changes."));
    expect(amountField(1)).toHaveValue("8");
  });

  it("asks before leaving with unsaved changes, with Stay here focused first", async () => {
    const user = userEvent.setup();
    const { router } = renderPanel();
    await user.type(amountField(1), "1");

    await act(async () => {
      void router.navigate("/elsewhere");
    });

    const dialog = await screen.findByRole("dialog", { name: "Leave without saving?" });
    expect(
      within(dialog).getByText("Your changes to the cost alert settings are not saved."),
    ).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Stay here" })).toHaveFocus();
    expect(router.state.location.pathname).toBe("/");

    await user.click(within(dialog).getByRole("button", { name: "Stay here" }));
    expect(router.state.location.pathname).toBe("/");

    await act(async () => {
      void router.navigate("/elsewhere");
    });
    await user.click(
      await within(await screen.findByRole("dialog")).findByRole("button", {
        name: "Discard and leave",
      }),
    );
    await waitFor(() => expect(router.state.location.pathname).toBe("/elsewhere"));
  });

  it("does not guard a pristine form and registers beforeunload only while dirty", async () => {
    const user = userEvent.setup();
    const { router } = renderPanel();
    const prevented = () => {
      const event = new Event("beforeunload", { cancelable: true });
      window.dispatchEvent(event);
      return event.defaultPrevented;
    };

    expect(prevented()).toBe(false);
    await user.type(amountField(1), "1");
    expect(prevented()).toBe(true);
    await user.clear(amountField(1));
    await user.type(amountField(1), "5");
    expect(prevented()).toBe(false);

    await act(async () => {
      void router.navigate("/elsewhere");
    });
    expect(screen.queryByRole("dialog", { name: "Leave without saving?" })).toBeNull();
    expect(router.state.location.pathname).toBe("/elsewhere");
  });

  it("never saves on Enter inside an amount field", async () => {
    const user = userEvent.setup();
    const update = vi.spyOn(hostingService, "updateCostAlertSettings");
    renderPanel();

    await user.type(amountField(1), "1{Enter}");

    expect(update).not.toHaveBeenCalled();
  });
});

/** One channel, availability and audience pair with the hint it must show in each language. */
interface HintCase {
  channel: "Discord direct message" | "Email";
  field: "discord" | "email";
  availability: CostAlertSettings["discord"];
  audience: CostAlertSettings["audience"];
  en: string;
  de: string;
}

const hintCases: HintCase[] = [
  {
    channel: "Discord direct message",
    field: "discord",
    availability: "Deliverable",
    audience: "Self",
    en: "Sent to your linked Discord account.",
    de: "Wird an dein verknüpftes Discord-Konto gesendet.",
  },
  {
    channel: "Discord direct message",
    field: "discord",
    availability: "Deliverable",
    audience: "Group",
    en: "Sent to each person who manages this account and has linked Discord.",
    de: "Wird an jede Person gesendet, die dieses Konto verwaltet und Discord verknüpft hat.",
  },
  {
    channel: "Discord direct message",
    field: "discord",
    availability: "NotLinked",
    audience: "Self",
    en: "No Discord account is linked to your profile, so no message can be sent yet.",
    de: "Mit deinem Profil ist kein Discord-Konto verknüpft, deshalb kann noch keine Nachricht gesendet werden.",
  },
  {
    channel: "Discord direct message",
    field: "discord",
    availability: "NotLinked",
    audience: "Group",
    en: "None of the people who manage this account has linked Discord, so no message can be sent yet.",
    de: "Niemand, der dieses Konto verwaltet, hat Discord verknüpft, deshalb kann noch keine Nachricht gesendet werden.",
  },
  {
    channel: "Discord direct message",
    field: "discord",
    availability: "ChannelUnavailable",
    audience: "Self",
    en: "Discord messages are not available right now.",
    de: "Discord-Nachrichten sind gerade nicht verfügbar.",
  },
  {
    channel: "Discord direct message",
    field: "discord",
    availability: "ChannelUnavailable",
    audience: "Group",
    en: "Discord messages are not available right now.",
    de: "Discord-Nachrichten sind gerade nicht verfügbar.",
  },
  {
    channel: "Email",
    field: "email",
    availability: "Deliverable",
    audience: "Self",
    en: "Sent to the email address on your profile. One email per alert.",
    de: "Wird an die E-Mail-Adresse in deinem Profil gesendet. Eine E-Mail pro Warnung.",
  },
  {
    channel: "Email",
    field: "email",
    availability: "Deliverable",
    audience: "Group",
    en: "Sent to each person who manages this account and has an email address. One email per person and alert.",
    de: "Wird an jede Person gesendet, die dieses Konto verwaltet und eine E-Mail-Adresse hat. Eine E-Mail pro Person und Warnung.",
  },
  {
    channel: "Email",
    field: "email",
    availability: "NoAddress",
    audience: "Self",
    en: "No email address is on your profile, so no email can be sent yet.",
    de: "In deinem Profil ist keine E-Mail-Adresse hinterlegt, deshalb kann noch keine E-Mail gesendet werden.",
  },
  {
    channel: "Email",
    field: "email",
    availability: "NoAddress",
    audience: "Group",
    en: "None of the people who manage this account has an email address, so no email can be sent yet.",
    de: "Niemand, der dieses Konto verwaltet, hat eine E-Mail-Adresse, deshalb kann noch keine E-Mail gesendet werden.",
  },
  {
    channel: "Email",
    field: "email",
    availability: "ChannelUnavailable",
    audience: "Self",
    en: "Email delivery is not set up on this server, so no email can be sent.",
    de: "Der E-Mail-Versand ist auf diesem Server nicht eingerichtet, deshalb kann keine E-Mail gesendet werden.",
  },
  {
    channel: "Email",
    field: "email",
    availability: "ChannelUnavailable",
    audience: "Group",
    en: "Email delivery is not set up on this server, so no email can be sent.",
    de: "Der E-Mail-Versand ist auf diesem Server nicht eingerichtet, deshalb kann keine E-Mail gesendet werden.",
  },
];

/** Matches the accessible description when it contains the text, whatever follows it (such as a link label). */
function containing(text: string): RegExp {
  return new RegExp(text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"));
}

describe("Cost alert channels panel", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the Where alerts go section with its description and three channel rows", () => {
    renderPanel();

    expect(screen.getByRole("heading", { level: 2, name: "Where alerts go" })).toBeVisible();
    expect(
      screen.getByText("Each alert is sent once per channel. Alerts in the app are always on."),
    ).toBeVisible();
    const section = screen.getByRole("region", { name: "Where alerts go" });
    expect(within(section).getAllByRole("switch")).toHaveLength(3);
  });

  it("shows In the app checked and disabled with its reason as the description", () => {
    renderPanel();

    const inApp = screen.getByRole("switch", { name: "In the app" });
    expect(inApp).toBeChecked();
    expect(inApp).toBeDisabled();
    expect(inApp).toHaveAccessibleDescription(
      "Always on Shown in the account menu and on the server page until you dismiss them.",
    );
  });

  it.each(hintCases)(
    "says the English hint for $channel when $availability and $audience",
    ({ channel, field, availability, audience, en }) => {
      renderPanel(costAlertSettings({ [field]: availability, audience }));

      const box = screen.getByRole("switch", { name: channel });
      expect(box).toHaveAccessibleDescription(containing(en));
      expect(box).toBeEnabled();
    },
  );

  it.each(hintCases)(
    "says the German hint for $channel when $availability and $audience",
    async ({ field, availability, audience, de }) => {
      await i18n.changeLanguage("de");
      renderPanel(costAlertSettings({ [field]: availability, audience }));

      const box = screen.getAllByRole("switch")[field === "discord" ? 1 : 2];
      expect(box).toHaveAccessibleDescription(containing(de));
    },
  );

  it("offers the account settings link on its own line only for a not linked personal owner", () => {
    renderPanel(costAlertSettings({ discord: "NotLinked", audience: "Self" }));

    const link = screen.getByRole("link", { name: "Open account settings" });
    expect(link).toHaveAttribute("href", "/settings");
    expect(link).toHaveClass("button--ghost");
    expect(link.parentElement).toHaveClass("cost-alert-settings__hint-link");
  });

  it.each([
    ["a group owner who has nobody linked", { discord: "NotLinked", audience: "Group" }],
    ["a linked personal owner", { discord: "Deliverable", audience: "Self" }],
    ["an unavailable channel", { discord: "ChannelUnavailable", audience: "Self" }],
  ] as const)("offers no account settings link for %s", (_name, overrides) => {
    renderPanel(costAlertSettings({ ...overrides }));

    expect(screen.queryByRole("link", { name: "Open account settings" })).toBeNull();
  });

  it("keeps an undeliverable channel checkable and marks the form dirty when it changes", async () => {
    const user = userEvent.setup();
    renderPanel(
      costAlertSettings({ discordEnabled: false, discord: "NotLinked", email: "NoAddress" }),
    );
    expect(screen.getByText("No unsaved changes.")).toBeVisible();

    const discord = screen.getByRole("switch", { name: "Discord direct message" });
    expect(discord).toBeEnabled();
    await user.click(discord);

    expect(discord).toBeChecked();
    expect(screen.getByText("You have unsaved changes.")).toBeVisible();
    expect(screen.getByRole("switch", { name: "In the app" })).toBeChecked();
  });

  it("never lets the viewer toggle the in-app row", async () => {
    const user = userEvent.setup();
    renderPanel();

    await user.click(screen.getByRole("switch", { name: "In the app" }));

    expect(screen.getByRole("switch", { name: "In the app" })).toBeChecked();
    expect(screen.getByText("No unsaved changes.")).toBeVisible();
  });

  it("names the message language of a personal owner in English and German", async () => {
    const { unmount } = renderPanel(costAlertSettings({ audience: "Self", messageLanguage: "en" }));
    expect(screen.getByText(/Discord and email messages are written in English\./)).toBeVisible();
    unmount();

    await i18n.changeLanguage("de");
    renderPanel(costAlertSettings({ audience: "Self", messageLanguage: "de" }));
    expect(screen.getByText(/werden auf Deutsch verfasst\./)).toBeVisible();
  });

  it("names the recorded language, not the interface language, for a personal owner", () => {
    renderPanel(costAlertSettings({ audience: "Self", messageLanguage: "de" }));

    expect(screen.getByText(/written in German\./)).toBeVisible();
  });

  it("says each person receives their own language for a group owner", () => {
    renderPanel(
      costAlertSettings({ audience: "Group", messageLanguage: null, discord: "Deliverable" }),
    );

    expect(screen.getByText("Each person receives messages in their own language.")).toBeVisible();
    expect(screen.queryByText(/written in/)).toBeNull();
  });

  it("disables the channel switches under the single read-only notice", () => {
    renderPanel(costAlertSettings({ canManage: false }));

    expect(screen.getAllByText("You can see these settings but not change them.")).toHaveLength(1);
    for (const name of ["In the app", "Discord direct message", "Email"]) {
      expect(screen.getByRole("switch", { name })).toBeDisabled();
    }
  });

  it("uses no details element, disclosure or select in the panel", () => {
    const { container } = renderPanel();

    expect(container.querySelector("details, select")).toBeNull();
  });
});

const badgeCases = [
  {
    channel: "Discord direct message",
    overrides: { discord: "Deliverable" },
    en: "Ready",
    de: "Bereit",
    tone: "success",
  },
  {
    channel: "Discord direct message",
    overrides: { discord: "NotLinked" },
    en: "Not linked",
    de: "Nicht verknüpft",
    tone: "warning",
  },
  {
    channel: "Discord direct message",
    overrides: { discord: "ChannelUnavailable" },
    en: "Not available",
    de: "Nicht verfügbar",
    tone: "neutral",
  },
  {
    channel: "Email",
    overrides: { email: "Deliverable" },
    en: "Ready",
    de: "Bereit",
    tone: "success",
  },
  {
    channel: "Email",
    overrides: { email: "NoAddress" },
    en: "No address",
    de: "Keine Adresse",
    tone: "warning",
  },
  {
    channel: "Email",
    overrides: { email: "ChannelUnavailable" },
    en: "Not available",
    de: "Nicht verfügbar",
    tone: "neutral",
  },
] as const;

/** The badge that opens the hint of the switch with the given index. */
function badgeOf(index: number): HTMLElement {
  const hint = screen.getAllByRole("switch")[index]?.closest(".form-field")?.querySelector("p");
  const badge = hint?.querySelector(".status-badge");
  if (!(badge instanceof HTMLElement)) throw new Error("The channel row has no status badge.");
  return badge;
}

describe("Cost alert channel status badges", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("opens the In the app hint with an always on success badge", () => {
    renderPanel();

    const badge = badgeOf(0);
    expect(badge).toHaveTextContent("Always on");
    expect(badge).toHaveClass("status-badge--success", "cost-alert-settings__badge");
    expect(badge.parentElement).toHaveTextContent(/^Always on Shown in the account menu/);
  });

  it.each(badgeCases)(
    "shows the $en badge in $tone for $channel",
    ({ channel, overrides, en, tone }) => {
      renderPanel(costAlertSettings({ ...overrides }));

      const badge = badgeOf(channel === "Email" ? 2 : 1);
      expect(badge).toHaveTextContent(en);
      expect(badge).toHaveClass(`status-badge--${tone}`);
      const box = screen.getByRole("switch", { name: channel });
      expect(box).toHaveAccessibleDescription(new RegExp(`^${en} `));
      expect(box).toBeEnabled();
    },
  );

  it.each(badgeCases)(
    "shows the German $de badge for $channel",
    async ({ channel, overrides, de }) => {
      await i18n.changeLanguage("de");
      renderPanel(costAlertSettings({ ...overrides }));

      expect(badgeOf(channel === "Email" ? 2 : 1)).toHaveTextContent(de);
    },
  );

  it("shows the German always on badge for the In the app row", async () => {
    await i18n.changeLanguage("de");
    renderPanel();

    expect(badgeOf(0)).toHaveTextContent("Immer aktiv");
  });

  it("keeps the account settings link below the Not linked badge in the description", () => {
    renderPanel(costAlertSettings({ discord: "NotLinked", audience: "Self" }));

    const box = screen.getByRole("switch", { name: "Discord direct message" });
    expect(box).toHaveAccessibleDescription(/^Not linked .*Open account settings$/);
    expect(screen.getByRole("link", { name: "Open account settings" })).toBeVisible();
  });

  it("lists the three channels and ends the card with the language line and its icon", () => {
    renderPanel(costAlertSettings({ audience: "Self", messageLanguage: "en" }));

    const card = screen.getByRole("region", { name: "Where alerts go" });
    const list = within(card).getByRole("list");
    expect(within(list).getAllByRole("listitem")).toHaveLength(3);
    const line = card.lastElementChild as HTMLElement;
    expect(line).toHaveClass("cost-alert-settings__language");
    expect(line).toHaveTextContent(/written in English/);
    expect(line.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
  });
});

describe("Cost alert add row", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("holds the Add button and the counter in one row", () => {
    renderPanel(costAlertSettings({ thresholds: [5, 10.5] }));

    const add = screen.getByRole("button", { name: "Add an amount" });
    const row = add.closest(".cost-alert-settings__add");
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText("2 of 10 amounts set")).toBeVisible();
  });

  it("keeps the counter mounted with no rows, so nothing shifts when the first row is added", async () => {
    const user = userEvent.setup();
    renderPanel(costAlertSettings({ thresholds: [] }));

    const counter = screen.getByText("0 of 10 amounts set");
    await user.click(screen.getByRole("button", { name: "Add an amount" }));

    expect(screen.getByText("1 of 10 amounts set")).toBe(counter);
  });

  it("keeps Add aria-disabled and described by the counter at the limit", () => {
    renderPanel(costAlertSettings({ thresholds: [1, 2], maxThresholds: 2 }));

    const add = screen.getByRole("button", { name: "Add an amount" });
    expect(add).toHaveAttribute("aria-disabled", "true");
    expect(add).toHaveAccessibleDescription("2 of 2 amounts set");
  });
});

describe("Cost alert settings cards", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the amounts and the channels as two raised cards with a heading and a description", () => {
    renderPanel();

    const amounts = screen.getByRole("region", { name: "Alert amounts" });
    const channels = screen.getByRole("region", { name: "Where alerts go" });
    for (const card of [amounts, channels]) {
      expect(card).toHaveClass("workspace-panel", "cost-alert-settings__panel");
      expect(within(card).getByRole("heading", { level: 2 })).toBeVisible();
      expect(card.querySelector("p.workspace-panel__muted")).not.toBeNull();
    }
    expect(amounts.tagName).toBe("SECTION");
    expect(channels.tagName).toBe("SECTION");
  });

  it("renders h3 headings in the cards when the host page already has an h2 section", () => {
    renderWithProviders(
      <CostAlertSettingsPanel
        settings={costAlertSettings()}
        queryKey={queryKey}
        headingLevel={3}
      />,
    );

    for (const name of ["Alert amounts", "Where alerts go"]) {
      const card = screen.getByRole("region", { name });
      expect(card).toHaveClass("workspace-panel");
      expect(within(card).getByRole("heading", { level: 3, name })).toBeVisible();
    }
  });
});
