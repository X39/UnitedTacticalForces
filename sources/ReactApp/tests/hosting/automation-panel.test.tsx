import { QueryClient, useQuery } from "@tanstack/react-query";
import { act, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import { ApiError } from "@/api/client";
import i18n, { i18nReady } from "@/app/i18n";
import { SettingsPanelStateProvider } from "@/components/server/useSettingsPanelState";
import { AutomationPanel } from "@/features/hosting/AutomationPanel";
import {
  AutomationSettingsConflictError,
  AutomationSettingsForbiddenError,
  hostingQueryKeys,
  hostingService,
} from "@/features/hosting/services";
import type { GameServer, ServerAutomation } from "@/features/hosting/types";
import { savedRule } from "../helpers/automationPanelHarness";
import { fleetServer, serverAutomation } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/* The Automation panel: idle shutdown and maximum runtime with their own draft and save row (AUT-01, AUT-02). */

/** The automation block with the given saved settings members replaced. */
function automationWith(
  settings: Partial<ServerAutomation["settings"]> = {},
  overrides: Partial<ServerAutomation> = {},
): ServerAutomation {
  const base = serverAutomation(overrides);
  return { ...base, settings: { ...base.settings, ...settings } };
}

/** A server the viewer may manage unless the options say otherwise. */
function managedServer(
  options: {
    automation?: ServerAutomation | null;
    canManage?: boolean | null;
    canManageCostAlerts?: boolean | null;
    archived?: boolean;
  } = {},
): GameServer {
  const server = fleetServer({
    automation: options.automation === undefined ? automationWith() : options.automation,
    canManageAutomation: options.canManage === undefined ? true : options.canManage,
    canManageCostAlerts: options.canManageCostAlerts ?? null,
  });
  return options.archived ? { ...server, isArchived: true } : server;
}

/** Hosts the panel on the server query, so a save or a refetch changes the server the panel receives. */
function Harness({
  server,
  onDirtyChange,
  registerDiscard,
  queryClient,
}: {
  server: GameServer;
  onDirtyChange: (dirty: boolean) => void;
  registerDiscard: (discard: () => void) => void;
  queryClient: QueryClient;
}) {
  const key = hostingQueryKeys.server(server.primaryKey);
  const query = useQuery({
    queryKey: key,
    queryFn: () => Promise.resolve(queryClient.getQueryData<GameServer>(key) ?? server),
    staleTime: Infinity,
  });
  return (
    <SettingsPanelStateProvider>
      <AutomationPanel
        server={query.data ?? server}
        onDirtyChange={onDirtyChange}
        registerDiscard={registerDiscard}
      />
    </SettingsPanelStateProvider>
  );
}

/** Renders the panel for the given server and returns its spies and the query client. */
function renderPanel(server: GameServer = managedServer(), route = "/") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  queryClient.setQueryData(hostingQueryKeys.server(server.primaryKey), server);
  const onDirtyChange = vi.fn();
  const registerDiscard = vi.fn();
  const view = renderWithProviders(
    <Harness
      server={server}
      onDirtyChange={onDirtyChange}
      registerDiscard={registerDiscard}
      queryClient={queryClient}
    />,
    { queryClient, route },
  );
  return { ...view, queryClient, onDirtyChange, registerDiscard, server };
}

const idleBox = () => screen.getByRole("switch", { name: "Shut down when idle" });
const idleField = (): HTMLInputElement => screen.getByLabelText("Idle time (minutes)");
const hoursBox = () => screen.getByRole("switch", { name: "Limit the runtime" });
const hoursField = (): HTMLInputElement => screen.getByLabelText("Maximum runtime (hours)");
const saveButton = () =>
  screen.getByRole("button", { name: /^(Save automation settings|Saving\.\.\.)$/ });

describe("Automation panel", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("saves idle shutdown with the loaded version, then shows the saved status and a toast", async () => {
    const user = userEvent.setup();
    const server = managedServer({ automation: automationWith({ version: 3 }) });
    const update = vi
      .spyOn(hostingService, "saveAutomationSettings")
      .mockResolvedValue(
        automationWith({ idleShutdownEnabled: true, idleShutdownMinutes: 45, version: 4 }),
      );
    const { onDirtyChange } = renderPanel(server);

    expect(screen.getByText("No unsaved changes.")).toHaveAttribute("role", "status");
    await user.click(idleBox());
    await user.clear(idleField());
    await user.type(idleField(), "45");
    expect(screen.getByText("You have unsaved changes.")).toBeVisible();
    await user.click(saveButton());

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    expect(update).toHaveBeenCalledWith(
      server.primaryKey,
      expect.objectContaining({
        version: 3,
        idleShutdownEnabled: true,
        idleShutdownMinutes: 45,
        maxRuntimeEnabled: false,
        maxRuntimeHours: 6,
        startConsent: false,
      }),
    );
    expect(await screen.findByText("Saved.")).toBeVisible();
    expect(await screen.findByText("Automation settings saved.")).toBeInTheDocument();
    expect(saveButton()).toHaveFocus();
    expect(idleField()).toHaveValue("45");
    expect(onDirtyChange).toHaveBeenLastCalledWith(false);
  });

  it("shows the status list of the saved state, which a draft never changes", async () => {
    const user = userEvent.setup();
    const { container } = renderPanel();

    const list = container.querySelector('dl[aria-label="Current automation status"]');
    expect(list).not.toBeNull();
    expect(list?.querySelectorAll("dt")).toHaveLength(3);
    expect(list?.textContent).toContain("Idle shutdownOff");
    await user.click(idleBox());
    expect(list?.textContent).toContain("Idle shutdownOff");
  });

  it("keeps the minutes field disabled and filled while its switch is off", () => {
    renderPanel(managedServer({ automation: automationWith({ idleShutdownMinutes: 30 }) }));

    expect(idleBox()).not.toBeChecked();
    expect(idleField()).toBeDisabled();
    expect(idleField()).toHaveValue("30");
    expect(hoursBox()).not.toBeChecked();
    expect(hoursField()).toBeDisabled();
    expect(hoursField()).toHaveValue("6");
    expect(idleField()).toHaveAttribute("maxlength", "4");
    expect(hoursField()).toHaveAttribute("maxlength", "3");
    expect(idleField()).toHaveAttribute("inputmode", "numeric");
  });

  it("operates the enable settings as switches with state words, Space and the dirty state", async () => {
    const user = userEvent.setup();
    const { onDirtyChange } = renderPanel();

    expect(idleBox()).toHaveAttribute("role", "switch");
    expect(idleBox().closest("label")).toHaveTextContent("Enabled");
    expect(idleBox().closest("label")).toHaveTextContent("Disabled");
    expect(idleField()).toBeDisabled();

    idleBox().focus();
    await user.keyboard(" ");
    expect(idleBox()).toBeChecked();
    expect(idleField()).toBeEnabled();
    expect(onDirtyChange).toHaveBeenLastCalledWith(true);
    expect(screen.getByText("You have unsaved changes.")).toBeVisible();
  });

  it("shows the range message after blur and blocks the save with the invalid status", async () => {
    const user = userEvent.setup();
    const update = vi.spyOn(hostingService, "saveAutomationSettings");
    renderPanel();
    await user.click(idleBox());
    await user.clear(idleField());
    await user.type(idleField(), "3");

    expect(screen.queryByText(/Enter a whole number of minutes/)).not.toBeInTheDocument();
    await user.tab();
    expect(
      await screen.findByText("Enter a whole number of minutes between 5 and 720."),
    ).toBeVisible();
    expect(idleField()).toHaveAttribute("aria-invalid", "true");

    await user.click(hoursBox());
    await user.click(saveButton());

    expect(update).not.toHaveBeenCalled();
    expect(screen.getByText("Fix the highlighted field before saving.")).toBeVisible();
    expect(idleField()).toHaveFocus();
  });

  it("does not validate or send a disabled number", async () => {
    const user = userEvent.setup();
    const update = vi
      .spyOn(hostingService, "saveAutomationSettings")
      .mockResolvedValue(automationWith({ maxRuntimeEnabled: true, version: 1 }));
    renderPanel(managedServer({ automation: automationWith({ idleShutdownMinutes: 30 }) }));
    await user.click(hoursBox());
    await user.click(saveButton());

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    expect(update.mock.calls[0]?.[1]).toEqual(
      expect.objectContaining({
        idleShutdownEnabled: false,
        idleShutdownMinutes: 30,
        maxRuntimeEnabled: true,
        maxRuntimeHours: 6,
      }),
    );
  });

  it("disables every control while saving and labels the button Saving...", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "saveAutomationSettings").mockReturnValue(
      new Promise(() => undefined),
    );
    renderPanel();
    await user.click(hoursBox());
    await user.click(saveButton());

    expect(await screen.findByRole("button", { name: "Saving..." })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
    expect(hoursBox()).toHaveAttribute("aria-disabled", "true");
    expect(hoursField()).toHaveAttribute("readonly");
    expect(hoursField()).toHaveAttribute("aria-disabled", "true");
  });

  it("keeps the draft on a conflict and replaces it only when Reload settings is chosen", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "saveAutomationSettings").mockRejectedValue(
      new AutomationSettingsConflictError(),
    );
    const { queryClient, server } = renderPanel(
      managedServer({ automation: automationWith({ version: 1 }) }),
    );
    await user.click(idleBox());
    await user.clear(idleField());
    await user.type(idleField(), "45");
    await user.click(saveButton());

    expect(
      await screen.findByText("These settings were changed elsewhere. Reload them and try again."),
    ).toBeVisible();
    act(() => {
      queryClient.setQueryData(hostingQueryKeys.server(server.primaryKey), {
        ...server,
        automation: automationWith({ idleShutdownMinutes: 90, version: 2 }),
      });
    });
    expect(idleBox()).toBeChecked();
    expect(idleField()).toHaveValue("45");

    await user.click(screen.getByRole("button", { name: "Reload settings" }));

    await waitFor(() => expect(idleField()).toHaveValue("90"));
    expect(idleBox()).not.toBeChecked();
    expect(screen.queryByRole("button", { name: "Reload settings" })).not.toBeInTheDocument();
  });

  it("reports a rejected right and a failed save in the alert line and keeps the draft", async () => {
    const user = userEvent.setup();
    const update = vi
      .spyOn(hostingService, "saveAutomationSettings")
      .mockRejectedValueOnce(new AutomationSettingsForbiddenError())
      .mockRejectedValueOnce(new ApiError("unavailable", 503, "server"));
    renderPanel();
    await user.click(hoursBox());
    await user.click(saveButton());

    expect(
      await screen.findByText("You are no longer allowed to change these settings."),
    ).toBeVisible();
    expect(screen.getByRole("alert")).toHaveTextContent("You are no longer allowed");
    expect(hoursBox()).toBeChecked();

    await user.click(saveButton());
    await waitFor(() => expect(update).toHaveBeenCalledTimes(2));
    expect(
      await screen.findByText("The settings could not be saved. Nothing was changed. Try again."),
    ).toBeVisible();
    expect(screen.queryByRole("button", { name: "Reload settings" })).not.toBeInTheDocument();
    expect(hoursBox()).toBeChecked();
  });

  it("discards the draft, reports it and focuses Save", async () => {
    const user = userEvent.setup();
    const { onDirtyChange } = renderPanel();
    await user.click(hoursBox());
    expect(onDirtyChange).toHaveBeenLastCalledWith(true);

    await user.click(screen.getByRole("button", { name: "Discard changes" }));

    expect(hoursBox()).not.toBeChecked();
    expect(screen.getByText("Changes discarded.")).toBeVisible();
    expect(saveButton()).toHaveFocus();
    expect(onDirtyChange).toHaveBeenLastCalledWith(false);
  });

  it("hands the host a discard callback that resets the draft", async () => {
    const user = userEvent.setup();
    const { registerDiscard, onDirtyChange } = renderPanel();
    await user.click(hoursBox());
    const discard = registerDiscard.mock.calls.at(-1)?.[0] as () => void;

    act(() => discard());

    expect(hoursBox()).not.toBeChecked();
    expect(onDirtyChange).toHaveBeenLastCalledWith(false);
  });

  it("shows every control disabled and one read-only notice without a save row", () => {
    renderPanel(
      managedServer({ canManage: false, automation: automationWith({ maxRuntimeEnabled: true }) }),
    );

    const notice =
      "Only the server owner or an administrator can change these settings. You can see what is set.";
    expect(screen.getAllByText(notice)).toHaveLength(1);
    expect(idleBox()).toBeDisabled();
    expect(idleField()).toBeDisabled();
    expect(hoursBox()).toBeDisabled();
    expect(hoursBox()).toBeChecked();
    expect(hoursField()).toBeDisabled();
    expect(hoursField()).toHaveValue("6");
    expect(idleBox()).toHaveAccessibleDescription(expect.stringContaining("Only the server owner"));
    expect(
      screen.queryByRole("button", { name: "Save automation settings" }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Discard changes" })).not.toBeInTheDocument();
  });

  it("shows the archived notice instead of the read-only notice and no save row", () => {
    renderPanel(managedServer({ archived: true }));

    expect(
      screen.getByText("This server is archived, so its settings can no longer be changed."),
    ).toBeVisible();
    expect(screen.queryByText(/Only the server owner/)).not.toBeInTheDocument();
    expect(idleBox()).toBeDisabled();
    expect(
      screen.queryByRole("button", { name: "Save automation settings" }),
    ).not.toBeInTheDocument();
  });

  it("renders only the title and the unavailable state without an automation block", () => {
    renderPanel(managedServer({ automation: null }));

    expect(screen.getByRole("heading", { name: "Automatic shutdown and schedules" })).toBeVisible();
    expect(
      screen.getByText(
        "Automatic shutdown and schedules are not available for this server right now.",
      ),
    ).toBeVisible();
    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
    expect(screen.queryByRole("switch")).not.toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    // The header button of the panel is the only button; there is no save row without a form.
    expect(screen.getAllByRole("button")).toHaveLength(1);
    expect(
      screen.getByRole("button", { name: "Automatic shutdown and schedules" }),
    ).toBeInTheDocument();
  });

  it("disables idle shutdown with a visible reason when the game cannot report players", () => {
    renderPanel(
      managedServer({ automation: automationWith({}, { idleShutdown: { available: false } }) }),
    );

    const reason =
      "This game does not report how many players are online, so idle shutdown is not available.";
    expect(screen.getByText(reason)).toBeVisible();
    expect(idleBox()).toBeDisabled();
    expect(idleBox()).toHaveAccessibleDescription(reason);
    expect(idleField()).toBeDisabled();
    expect(idleField()).toHaveAccessibleDescription(expect.stringContaining(reason));
    expect(hoursBox()).toBeEnabled();
  });

  it("states the warning lead and offers the alerts link only with the cost alert right", () => {
    const { unmount } = renderPanel(managedServer({ canManageCostAlerts: true }));

    expect(
      screen.getByText(
        "You are warned 5 minutes before an automatic shutdown, in the app and through the channels chosen for cost alerts.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Choose where alerts go" })).toHaveAttribute(
      "href",
      expect.stringContaining("/command/servers/cost-alerts?owner="),
    );
    unmount();

    renderPanel(managedServer({ canManageCostAlerts: false }));
    expect(screen.queryByRole("link", { name: "Choose where alerts go" })).not.toBeInTheDocument();
  });

  it("uses a primary save button, never gold, and focuses the header button on the deep link", async () => {
    const { container } = renderPanel(managedServer(), "/?tab=settings#server-automation");

    expect(saveButton()).toHaveClass("button--primary");
    expect(container.querySelector(".button--gold")).toBeNull();
    expect(container.querySelector("#server-automation")).toBeInTheDocument();
    // The focus moves on the next animation frame, to the header button and not to the heading (C-08).
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Automatic shutdown and schedules" }),
      ).toHaveFocus(),
    );
  });

  it("keeps an unsaved edit while the panel is collapsed and opened again", async () => {
    const user = userEvent.setup();
    renderPanel();
    await user.click(idleBox());
    expect(idleBox()).toBeChecked();

    const header = screen.getByRole("button", { name: "Automatic shutdown and schedules" });
    await user.click(header);
    expect(header).toHaveAttribute("aria-expanded", "false");
    await user.click(header);

    expect(header).toHaveAttribute("aria-expanded", "true");
    expect(idleBox()).toBeChecked();
  });

  it("summarizes the saved automations and badges a dirty draft as unsaved", async () => {
    const user = userEvent.setup();
    const { unmount } = renderPanel();
    expect(screen.getByText("Nothing automated")).toBeInTheDocument();
    expect(screen.queryByText("Unsaved")).not.toBeInTheDocument();
    await user.click(idleBox());
    expect(screen.getByText("Unsaved")).toBeInTheDocument();
    unmount();

    renderPanel(managedServer({ automation: automationWith({ maxRuntimeEnabled: true }) }));
    expect(screen.getByText("1 automation active")).toBeInTheDocument();
  });

  it("counts the idle shutdown, the maximum runtime and every saved rule", () => {
    renderPanel(
      managedServer({
        automation: automationWith({
          idleShutdownEnabled: true,
          maxRuntimeEnabled: true,
          schedules: [savedRule()],
        }),
      }),
    );
    expect(screen.getByText("3 automations active")).toBeInTheDocument();
  });

  it("shows no summary without an automation block", () => {
    renderPanel(managedServer({ automation: null }));
    expect(screen.queryByText("Nothing automated")).not.toBeInTheDocument();
    expect(screen.queryByText(/automations? active/)).not.toBeInTheDocument();
  });
});
