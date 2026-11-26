import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
import { hostingService } from "@/features/hosting/services";
import type { GameConfiguration, GameServer } from "@/features/hosting/types";
import { configurationEntry } from "../helpers/configurationFixtures";
import { fleetServer, type FleetServerOptions } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/* The staged-change flow of the Settings section: stage, review, apply (CHG-03, CHG-04, D-01, D-03). */

/*
 * The credentials panel stages through plan 65-11; this stand-in stages and announces through the draft context. It
 * composes the same collapsible panel as the real one, with the staged count as its badge and the staged keys.
 */
vi.mock("@/features/hosting/ServerSecretsPanel", async () => {
  const { useServerChangeDraft } = await import("@/features/hosting/useServerChangeDraft");
  const { SettingsPanel } = await import("@/components/server/SettingsPanel");
  const { StatusBadge } = await import("@/components/primitives");
  return {
    ServerSecretsPanel: () => {
      const { draft, stageSecret, announce } = useServerChangeDraft();
      return (
        <SettingsPanel
          id="server-credentials"
          title="Credentials"
          badge={
            draft.secrets.size > 0 ? (
              <StatusBadge tone="info">{`${draft.secrets.size} changed`}</StatusBadge>
            ) : undefined
          }
          stagedKeys={[...draft.secrets.keys()]}
        >
          <button type="button" onClick={() => stageSecret("TOKEN", { kind: "rotate" })}>
            Stage test credential
          </button>
          <button type="button" onClick={() => announce("Panel says hello")}>
            Announce test
          </button>
        </SettingsPanel>
      );
    },
  };
});

const maxPlayers = configurationEntry({
  key: "server.cfg@maxPlayers",
  displayName: "Maximum players",
  kind: "Integer",
  minimum: 1,
  maximum: 128,
  value: 64,
  source: "Default",
  required: true,
});
const hostname = configurationEntry({
  key: "server.cfg@hostname",
  displayName: "Hostname",
  kind: "String",
  value: "Old name",
  source: "Server",
  order: 20,
});

function configurationWith(entries = [maxPlayers, hostname]): GameConfiguration {
  return {
    serverId: "server",
    profileId: null,
    profileRevisionId: null,
    profileRevisionNumber: null,
    isComplete: true,
    entries,
  } as unknown as GameConfiguration;
}

/** Builds the server the Settings section reads, with the full projection fields it needs. */
function settingsServer(
  options: FleetServerOptions = {},
  overrides: Record<string, unknown> = {},
): GameServer {
  return {
    ...fleetServer({ title: "Alpha Team", gameIdentifier: "arma3", ...options }),
    selectedModPackDefinitionId: null,
    selectedModPackDefinitionTitle: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    canModify: true,
    canManageSecrets: false,
    ...overrides,
  } as unknown as GameServer;
}

const server = settingsServer();

function renderSettings(target = server) {
  return renderWithProviders(<SettingsSection server={target} jobs={[]} />);
}

describe("Settings change flow", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "configuration").mockResolvedValue(configurationWith());
    vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "files").mockResolvedValue([]);
    vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as never);
    vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
    vi.spyOn(hostingService, "updateConfiguration").mockResolvedValue(configurationWith());
  });

  it("renders the Settings heading and the Configuration panel heading", async () => {
    renderSettings();

    const heading = await screen.findByRole("heading", { level: 2, name: "Settings" });
    expect(heading).toHaveAttribute("tabindex", "-1");
    expect(await screen.findByRole("heading", { level: 2, name: "Configuration" })).toBeVisible();
    expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save overrides" })).not.toBeInTheDocument();
  });

  it("stages a value without saving and shows the pending bar; the original value removes it", async () => {
    const user = userEvent.setup();
    renderSettings();

    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "80");

    const bar = await screen.findByRole("region", { name: "Pending changes" });
    expect(
      within(bar).getByText("1 change staged. Nothing is saved until you apply it."),
    ).toBeVisible();
    expect(within(bar).getByRole("button", { name: "Review changes" })).toBeVisible();
    expect(within(bar).getByRole("button", { name: "Discard changes" })).toBeVisible();
    expect(hostingService.updateConfiguration).not.toHaveBeenCalled();

    await user.clear(field);
    await user.type(field, "64");
    expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
  });

  it("reviews the change with Before and After and the not-running notice, focusing Keep editing", async () => {
    const user = userEvent.setup();
    renderSettings();
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "80");

    await user.click(screen.getByRole("button", { name: "Review changes" }));

    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    const body = within(dialog).getByRole("region", { name: "Changes to review" });
    const item = within(body).getByText("Maximum players").closest("li");
    expect(item).not.toBeNull();
    const row = within(item as HTMLElement);
    expect(row.getByText("Before").nextElementSibling).toHaveTextContent("64");
    expect(row.getByText("After").nextElementSibling).toHaveTextContent("80");
    expect(
      within(dialog).getByText("These changes take effect the next time Alpha Team starts."),
    ).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Keep editing" })).toHaveFocus();
  });

  it("uses the running wording while the server has an active deployment", async () => {
    const user = userEvent.setup();
    renderSettings(settingsServer({ status: "Running" }));
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "80");

    await user.click(screen.getByRole("button", { name: "Review changes" }));

    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    expect(
      within(dialog).getByText(
        "Alpha Team keeps running with its current setup. These changes take effect the next time it starts.",
      ),
    ).toBeVisible();
  });

  it("applies once through the existing endpoint, then toasts, closes and focuses the heading", async () => {
    const user = userEvent.setup();
    renderSettings();
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "80");
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });

    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() => expect(hostingService.updateConfiguration).toHaveBeenCalledTimes(1));
    expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
      server.primaryKey,
      [{ key: "server.cfg@maxPlayers", value: 80 }],
      [],
    );
    expect(
      await screen.findByText("1 change applied. It takes effect the next time Alpha Team starts."),
    ).toBeVisible();
    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
    );
    expect(screen.queryByRole("dialog", { name: "Review changes" })).not.toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 2, name: "Settings" })).toHaveFocus(),
    );
  });

  it("shows no stale Applied state when a later change is reviewed after a complete run", async () => {
    const user = userEvent.setup();
    renderSettings();
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "80");
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const first = await screen.findByRole("dialog", { name: "Review changes" });
    await user.click(within(first).getByRole("button", { name: "Apply 1 change" }));
    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
    );

    await user.clear(screen.getByLabelText("Maximum players"));
    await user.type(screen.getByLabelText("Maximum players"), "90");
    await user.click(screen.getByRole("button", { name: "Review changes" }));

    const second = await screen.findByRole("dialog", { name: "Review changes" });
    expect(within(second).queryByText("Applied")).not.toBeInTheDocument();
    expect(within(second).getByRole("button", { name: "Apply 1 change" })).toBeVisible();
  });

  it("stages a reset to the inherited value and applies it as a reset", async () => {
    const user = userEvent.setup();
    renderSettings();
    await screen.findByLabelText("Hostname");

    await user.click(screen.getByRole("button", { name: "Reset to inherited: Hostname" }));
    expect(hostingService.updateConfiguration).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    expect(within(dialog).getByText("Inherited value")).toBeVisible();
    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() =>
      expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
        server.primaryKey,
        [],
        ["server.cfg@hostname"],
      ),
    );
    expect(hostingService.updateConfiguration).toHaveBeenCalledTimes(1);
  });

  it("asks before discarding, keeps the draft on Keep editing and discards without calling the API", async () => {
    const user = userEvent.setup();
    renderSettings();
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "80");

    await user.click(screen.getByRole("button", { name: "Discard changes" }));

    const dialog = await screen.findByRole("dialog", { name: "Discard staged changes?" });
    expect(
      within(dialog).getByText("This removes the staged change. Nothing on the server changes."),
    ).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Keep editing" })).toHaveFocus();
    await user.click(within(dialog).getByRole("button", { name: "Keep editing" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByRole("region", { name: "Pending changes" })).toBeVisible();
    expect(screen.getByLabelText("Maximum players")).toHaveValue(80);

    await user.click(screen.getByRole("button", { name: "Discard changes" }));
    const again = await screen.findByRole("dialog", { name: "Discard staged changes?" });
    await user.click(within(again).getByRole("button", { name: "Discard changes" }));

    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
    );
    expect(screen.getByLabelText("Maximum players")).toHaveValue(64);
    expect(hostingService.updateConfiguration).not.toHaveBeenCalled();
    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 2, name: "Settings" })).toHaveFocus(),
    );
  });

  it("uses the plural discard wording for several staged changes", async () => {
    const user = userEvent.setup();
    renderSettings();
    const players = await screen.findByLabelText("Maximum players");
    await user.clear(players);
    await user.type(players, "80");
    const name = screen.getByLabelText("Hostname");
    await user.clear(name);
    await user.type(name, "New name");

    await user.click(screen.getByRole("button", { name: "Discard changes" }));

    const dialog = await screen.findByRole("dialog", { name: "Discard staged changes?" });
    expect(
      within(dialog).getByText("This removes all 2 staged changes. Nothing on the server changes."),
    ).toBeVisible();
  });

  it("does not open the review while a staged value is invalid", async () => {
    const user = userEvent.setup();
    renderSettings();
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "500");

    fireEvent.click(screen.getByRole("button", { name: "Review changes" }));

    const bar = await screen.findByRole("region", { name: "Pending changes" });
    expect(await within(bar).findByRole("alert")).toHaveTextContent(
      "Fix the highlighted setting before reviewing.",
    );
    expect(screen.queryByRole("dialog", { name: "Review changes" })).not.toBeInTheDocument();
  });

  it("stops the review for an invalid value, explains the field and moves focus to it", async () => {
    const user = userEvent.setup();
    renderSettings();
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "1.5");

    const review = screen.getByRole("button", { name: "Review changes" });
    expect(review).toBeEnabled();
    await user.click(review);

    expect(screen.queryByRole("dialog", { name: "Review changes" })).not.toBeInTheDocument();
    const bar = screen.getByRole("region", { name: "Pending changes" });
    expect(within(bar).getByRole("alert")).toHaveTextContent(
      "Fix the highlighted setting before reviewing.",
    );
    expect(field).toHaveAttribute("aria-invalid", "true");
    const error = screen.getByText("Enter a whole number.");
    expect(field.getAttribute("aria-describedby")?.split(" ")).toContain(error.closest("p")?.id);
    // The field is focused on the next frame, once its panel is known to be shown.
    await waitFor(() => expect(field).toHaveFocus());
    expect(review).toBeEnabled();

    await user.clear(field);
    await user.type(field, "80");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByText("Enter a whole number.")).not.toBeInTheDocument();
    expect(field).not.toHaveAttribute("aria-invalid");
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    expect(await screen.findByRole("dialog", { name: "Review changes" })).toBeVisible();
  });

  it("names the range limits and shows an error after the field was left", async () => {
    const user = userEvent.setup();
    renderSettings();
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "500");
    expect(screen.queryByText("Use at most 128.")).not.toBeInTheDocument();

    await user.tab();

    expect(await screen.findByText("Use at most 128.")).toBeVisible();
    expect(field).toHaveAttribute("aria-invalid", "true");
    // The field error is announced by the shared field; the bar alert only appears after a review attempt.
    const bar = screen.getByRole("region", { name: "Pending changes" });
    expect(within(bar).queryByRole("alert")).not.toBeInTheDocument();
    await user.clear(field);
    await user.type(field, "0");
    await user.tab();
    expect(await screen.findByText("Use at least 1.")).toBeVisible();
  });

  it("tells a viewer without Modify rights once and disables every configuration control", async () => {
    renderSettings(settingsServer({}, { canModify: false }));

    expect(
      await screen.findByText("You can see these settings but not change them."),
    ).toBeVisible();
    expect(screen.getAllByText("You can see these settings but not change them.")).toHaveLength(1);
    expect(await screen.findByLabelText("Maximum players")).toBeDisabled();
    expect(screen.getByLabelText("Hostname")).toBeDisabled();
    expect(screen.queryByText(/This server is archived/)).not.toBeInTheDocument();
  });

  it("tells the viewer of an archived server that its settings can no longer be changed", async () => {
    renderSettings(settingsServer({}, { isArchived: true }));

    expect(
      await screen.findByText("This server is archived, so its settings can no longer be changed."),
    ).toBeVisible();
    expect(screen.queryByText("You can see these settings but not change them.")).toBeNull();
    expect(await screen.findByLabelText("Maximum players")).toBeDisabled();
  });

  it("announces the first staged change, the emptied draft and panel messages in one polite region", async () => {
    const user = userEvent.setup();
    const { container } = renderSettings(settingsServer({}, { canManageSecrets: true }));
    const field = await screen.findByLabelText("Maximum players");
    const region = container.querySelector<HTMLElement>('.sr-only[aria-live="polite"]');
    expect(region).not.toBeNull();
    expect(region).toHaveAttribute("aria-atomic", "true");
    expect(region).toBeEmptyDOMElement();

    await user.clear(field);
    await user.type(field, "80");
    await waitFor(() =>
      expect(region).toHaveTextContent("Changes are staged. Review them before applying."),
    );

    await user.click(screen.getByRole("button", { name: "Announce test" }));
    expect(region).toHaveTextContent("Panel says hello");

    await user.click(screen.getByRole("button", { name: "Discard changes" }));
    await user.click(
      within(await screen.findByRole("dialog", { name: "Discard staged changes?" })).getByRole(
        "button",
        { name: "Discard changes" },
      ),
    );
    await waitFor(() => expect(region).toHaveTextContent("No changes staged."));
    expect(container.querySelectorAll('.sr-only[aria-live="polite"]')).toHaveLength(1);
  });

  it("does not announce an empty draft after everything was applied", async () => {
    const user = userEvent.setup();
    const { container } = renderSettings();
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "80");
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
    );
    const region = container.querySelector<HTMLElement>('.sr-only[aria-live="polite"]');
    expect(region).not.toHaveTextContent("No changes staged.");
  });

  it("counts staged credentials in the credentials panel head", async () => {
    const user = userEvent.setup();
    renderSettings(settingsServer({}, { canManageSecrets: true }));
    const heading = await screen.findByRole("heading", { level: 2, name: "Credentials" });
    expect(within(heading.parentElement as HTMLElement).queryByText(/changed/)).toBeNull();

    await user.click(screen.getByRole("button", { name: "Stage test credential" }));

    expect(within(heading.parentElement as HTMLElement).getByText("1 changed")).toBeVisible();
  });
});
