import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { act, fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
import { hostingService } from "@/features/hosting/services";
import type {
  GameConfiguration,
  GameServer,
  GameServerSecretStatus,
} from "@/features/hosting/types";
import { fleetServer, type FleetServerOptions } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/* The credentials panel of the Settings section: staged set and rotate changes, masked values (CHG-07, D-07, D-08). */

/** Builds one secret status of the Counter-Strike 2 fixtures. */
function secretStatus(overrides: Partial<GameServerSecretStatus>): GameServerSecretStatus {
  return {
    key: "gslt",
    displayName: "Game server login token",
    requiredForStart: true,
    generated: false,
    revealable: false,
    externalProvider: true,
    isConfigured: false,
    version: null,
    origin: null,
    timeStampCreated: null,
    acceptsChosenValue: true,
    valueFormat: "Token",
    ...overrides,
  };
}

const gslt = secretStatus({});
const rconPassword = secretStatus({
  key: "rcon-password",
  displayName: "RCON password",
  requiredForStart: false,
  generated: true,
  revealable: true,
  externalProvider: false,
  isConfigured: true,
  version: 3,
  origin: "Generated",
});

const typedValue = "token-123";

/** Builds the server the Settings section reads, with the credential rights the test needs. */
function credentialServer(
  options: FleetServerOptions = {},
  overrides: Record<string, unknown> = {},
): GameServer {
  return {
    ...fleetServer({ title: "Alpha Team", gameIdentifier: "counter-strike-2", ...options }),
    selectedModPackDefinitionId: null,
    selectedModPackDefinitionTitle: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    canModify: true,
    canManageSecrets: true,
    canRevealSecrets: false,
    ...overrides,
  } as unknown as GameServer;
}

/** Mocks every hosting request the Settings section makes, with the given credential list. */
function mockHostingQueries(secrets: GameServerSecretStatus[]): void {
  vi.spyOn(hostingService, "configuration").mockResolvedValue({
    serverId: "server",
    profileId: null,
    profileRevisionId: null,
    profileRevisionNumber: null,
    isComplete: true,
    entries: [],
  } as unknown as GameConfiguration);
  vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
  vi.spyOn(hostingService, "files").mockResolvedValue([]);
  vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as never);
  vi.spyOn(hostingService, "secrets").mockResolvedValue(secrets);
  vi.spyOn(hostingService, "setSecret").mockResolvedValue(undefined);
  vi.spyOn(hostingService, "rotateSecret").mockResolvedValue({
    key: "rcon-password",
    value: "generated-once-9f2c",
    version: 4,
  });
}

const server = credentialServer();

function renderSettings(target = server) {
  return renderWithProviders(<SettingsSection server={target} jobs={[]} />);
}

describe("Credentials panel staging", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockHostingQueries([gslt, rconPassword]);
  });

  it("shows the description, one article per secret with its status, the required warning and a masked value", async () => {
    renderSettings();

    expect(await screen.findByRole("heading", { level: 3, name: gslt.displayName })).toBeVisible();
    expect(screen.getByRole("heading", { level: 2, name: "Credentials" })).toBeVisible();
    expect(screen.getByText(/Values are encrypted at rest/)).toBeVisible();
    expect(screen.queryByRole("heading", { name: "Server credentials" })).not.toBeInTheDocument();

    const gsltRow = screen.getByRole("heading", { name: gslt.displayName }).closest("article");
    const rconRow = screen.getByRole("heading", { name: "RCON password" }).closest("article");
    expect(gsltRow).not.toBeNull();
    expect(rconRow).not.toBeNull();
    expect(within(gsltRow as HTMLElement).getByText("Not set")).toBeVisible();
    expect(
      within(gsltRow as HTMLElement).getByText("Required before this server can start."),
    ).toBeVisible();
    expect(within(gsltRow as HTMLElement).queryByText("Value hidden")).not.toBeInTheDocument();
    expect(within(rconRow as HTMLElement).getByText("Configured, version 3")).toBeVisible();
    expect(within(rconRow as HTMLElement).getByText("Value hidden")).toBeInTheDocument();
    expect(
      within(rconRow as HTMLElement).queryByText("Required before this server can start."),
    ).not.toBeInTheDocument();
  });

  it("shows the masked run as twelve bullets without a partial value or a length", async () => {
    const { container } = renderSettings();
    await screen.findByRole("heading", { name: "RCON password" });

    const mask = container.querySelector(".credential__mask");
    expect(mask).toHaveAttribute("aria-hidden", "true");
    expect(mask?.textContent).toBe("•".repeat(12));
  });

  it("stages a typed value, shows the Changed badge and Undo, and clearing the input removes the bar", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText(`New value for ${gslt.displayName}`);

    await user.type(input, typedValue);

    const bar = await screen.findByRole("region", { name: "Pending changes" });
    expect(
      within(bar).getByText("1 change staged. Nothing is saved until you apply it."),
    ).toBeVisible();
    const row = screen.getByRole("heading", { name: gslt.displayName }).closest("article");
    expect(badgeSlot(row as HTMLElement)).not.toHaveAttribute("aria-hidden");
    expect(badgeSlot(row as HTMLElement)).not.toHaveClass("credential__badge-slot--idle");
    expect(within(row as HTMLElement).getByText("Changed")).toBeVisible();
    expect(
      within(row as HTMLElement).getByRole("button", {
        name: `Undo change: ${gslt.displayName}`,
      }),
    ).toBeVisible();
    expect(hostingService.setSecret).not.toHaveBeenCalled();

    await user.clear(input);
    expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
    expect(badgeSlot(row as HTMLElement)).toHaveAttribute("aria-hidden", "true");
    expect(badgeSlot(row as HTMLElement)).toHaveClass("credential__badge-slot--idle");
  });

  it("uses a password input that does not autocomplete or spell-check", async () => {
    renderSettings();
    const input = await screen.findByLabelText(`New value for ${gslt.displayName}`);

    expect(input).toHaveAttribute("type", "password");
    expect(input).toHaveAttribute("autocomplete", "new-password");
    expect(input).toHaveAttribute("spellcheck", "false");
  });

  it("reviews the credential as Changed without ever showing the typed value", async () => {
    const user = userEvent.setup();
    renderSettings();
    await user.type(await screen.findByLabelText(`New value for ${gslt.displayName}`), typedValue);

    await user.click(screen.getByRole("button", { name: "Review changes" }));

    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    expect(within(dialog).getByText("Changed. The value is not shown.")).toBeVisible();
    expect(dialog.innerHTML).not.toContain(typedValue);
  });

  it("applies through the existing endpoint once and leaves no trace of the typed value", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText(`New value for ${gslt.displayName}`);
    await user.type(input, typedValue);
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });

    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() => expect(hostingService.setSecret).toHaveBeenCalledTimes(1));
    expect(hostingService.setSecret).toHaveBeenCalledWith(
      server.primaryKey,
      "gslt",
      typedValue,
      true,
    );
    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
    );
    expect(screen.getByLabelText(`New value for ${gslt.displayName}`)).toHaveValue("");
    expect(document.body.innerHTML).not.toContain(typedValue);
  });

  it("removes the staged change and clears the input with Undo change, keeping focus on the input", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText(`New value for ${gslt.displayName}`);
    await user.type(input, typedValue);

    await user.click(screen.getByRole("button", { name: `Undo change: ${gslt.displayName}` }));

    expect(input).toHaveValue("");
    expect(input).toHaveFocus();
    expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
    expect(document.body.innerHTML).not.toContain(typedValue);
  });

  it("renders no credentials heading, panel or secrets request without canManageSecrets", async () => {
    renderSettings(credentialServer({}, { canManageSecrets: false }));

    expect(await screen.findByRole("heading", { level: 2, name: "Configuration" })).toBeVisible();
    expect(screen.queryByRole("heading", { name: "Credentials" })).not.toBeInTheDocument();
    expect(screen.queryByText(/Values are encrypted at rest/)).not.toBeInTheDocument();
    expect(hostingService.secrets).not.toHaveBeenCalled();
  });

  it("renders no raw credential button or input element outside the shared controls and no hard-coded heading", async () => {
    const { container } = renderSettings();
    await screen.findByRole("heading", { name: gslt.displayName });

    const panel = container.querySelector(".credentials");
    expect(panel).not.toBeNull();
    for (const button of panel?.querySelectorAll("button") ?? [])
      expect(button).toHaveClass("button");
    for (const field of panel?.querySelectorAll("input") ?? [])
      expect(field).toHaveClass("form-field__control");
    expect(screen.queryByText("Security")).not.toBeInTheDocument();
  });

  it("shows loading, a retryable load error without error text, and the empty game", async () => {
    const user = userEvent.setup();
    const secrets = vi.spyOn(hostingService, "secrets");
    secrets.mockRejectedValueOnce(new Error("secret backend exploded"));
    renderSettings();

    expect(await screen.findByText("The credentials could not be loaded.")).toBeVisible();
    expect(screen.queryByText(/exploded/)).not.toBeInTheDocument();

    secrets.mockResolvedValueOnce([]);
    await user.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByText("This game has no credentials to manage.")).toBeVisible();
  });

  it("shows the loading copy while the secrets request is pending", async () => {
    vi.spyOn(hostingService, "secrets").mockReturnValue(new Promise(() => undefined));
    renderSettings();

    expect(await screen.findByText("Loading credentials…")).toBeVisible();
  });

  it("disables the input while the server is archived", async () => {
    renderSettings(credentialServer({}, { isArchived: true }));

    expect(await screen.findByLabelText(`New value for ${gslt.displayName}`)).toBeDisabled();
  });
});

const revealedValue = "s3cr3t-rcon-value-that-is-quite-long-and-has-no-spaces-at-all-0123456789";
const adminPassword = secretStatus({
  key: "admin-password",
  displayName: "Admin password",
  requiredForStart: false,
  generated: false,
  revealable: true,
  externalProvider: false,
  isConfigured: true,
  version: 2,
  origin: "UserProvided",
  valueFormat: "SingleLine",
});

/** The single polite live region of the Settings section. */
function liveRegion(): HTMLElement {
  const region = document.querySelector<HTMLElement>('.sr-only[aria-live="polite"]');
  if (!region) throw new Error("The Settings live region is missing.");
  return region;
}

/** The badge slot of a credential row. */
function badgeSlot(row: HTMLElement): HTMLElement {
  const slot = row.querySelector<HTMLElement>(".credential__badge-slot");
  if (!slot) throw new Error("The credential row has no badge slot.");
  return slot;
}

/** Finds the article of a credential by its display name. */
function rowOf(name: string): HTMLElement {
  const row = screen.getByRole("heading", { name }).closest("article");
  if (!row) throw new Error(`No credential row for ${name}.`);
  return row;
}

describe("Credentials panel reveal and rotate", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockHostingQueries([gslt, rconPassword, adminPassword]);
    vi.spyOn(hostingService, "revealSecret").mockImplementation(async (_serverId, key) => ({
      key,
      value: key === "rcon-password" ? revealedValue : "admin-secret-value",
      version: key === "rcon-password" ? 3 : 2,
    }));
  });

  afterEach(() => {
    vi.useRealTimers();
    Reflect.deleteProperty(document, "visibilityState");
  });

  const revealer = credentialServer({}, { canRevealSecrets: true });

  /** Reveals the RCON password and waits until its value is on screen. */
  async function revealRcon(user: ReturnType<typeof userEvent.setup>): Promise<HTMLElement> {
    await user.click(await screen.findByRole("button", { name: "Reveal value of RCON password" }));
    const code = await within(rowOf("RCON password")).findByText(revealedValue);
    expect(code.tagName).toBe("CODE");
    return code;
  }

  it("reveals on demand in a code element of its row, keeps focus on the same button and announces it", async () => {
    const user = userEvent.setup();
    renderSettings(revealer);
    expect(hostingService.revealSecret).not.toHaveBeenCalled();

    await revealRcon(user);

    expect(hostingService.revealSecret).toHaveBeenCalledTimes(1);
    expect(hostingService.revealSecret).toHaveBeenCalledWith(revealer.primaryKey, "rcon-password");
    const hide = within(rowOf("RCON password")).getByRole("button", {
      name: "Hide value of RCON password",
    });
    expect(hide).toHaveTextContent("Hide value");
    expect(hide).toHaveFocus();
    expect(within(rowOf("RCON password")).queryByText("Value hidden")).not.toBeInTheDocument();
    await waitFor(() =>
      expect(liveRegion()).toHaveTextContent(
        "The value of RCON password is shown. It hides again in 30 seconds.",
      ),
    );
    expect(rowOf("Admin password").querySelector("code")).toBeNull();
  });

  it("wraps a long value inside its code element", async () => {
    const user = userEvent.setup();
    renderSettings(revealer);

    const code = await revealRcon(user);

    expect(code).toHaveClass("credential__code");
    expect(code.closest("article")).toBe(rowOf("RCON password"));
  });

  it("hides the value after 30 seconds and announces it", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderSettings(revealer);
    await revealRcon(user);

    act(() => {
      vi.advanceTimersByTime(29_000);
    });
    expect(screen.getByText(revealedValue)).toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(1_500);
    });

    expect(screen.queryByText(revealedValue)).not.toBeInTheDocument();
    expect(within(rowOf("RCON password")).getByText("Value hidden")).toBeInTheDocument();
    expect(liveRegion()).toHaveTextContent("The value of RCON password is hidden again.");
    expect(
      within(rowOf("RCON password")).getByRole("button", { name: "Reveal value of RCON password" }),
    ).toBeVisible();
  });

  it("hides the value with Hide value", async () => {
    const user = userEvent.setup();
    renderSettings(revealer);
    await revealRcon(user);

    await user.click(screen.getByRole("button", { name: "Hide value of RCON password" }));

    expect(screen.queryByText(revealedValue)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reveal value of RCON password" })).toHaveFocus();
  });

  it("hides the value when the tab becomes hidden", async () => {
    const user = userEvent.setup();
    renderSettings(revealer);
    await revealRcon(user);

    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      get: () => "hidden",
    });
    fireEvent(document, new Event("visibilitychange"));

    await waitFor(() => expect(screen.queryByText(revealedValue)).not.toBeInTheDocument());
  });

  it("keeps the value while the tab stays visible after a visibility change", async () => {
    const user = userEvent.setup();
    renderSettings(revealer);
    await revealRcon(user);

    fireEvent(document, new Event("visibilitychange"));

    expect(screen.getByText(revealedValue)).toBeInTheDocument();
  });

  it("hides the value when the section unmounts", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const { unmount } = renderSettings(revealer);
    await revealRcon(user);

    unmount();
    act(() => {
      vi.advanceTimersByTime(31_000);
    });

    expect(document.body.innerHTML).not.toContain(revealedValue);
  });

  it("shows only one revealed value: revealing another replaces the first", async () => {
    const user = userEvent.setup();
    renderSettings(revealer);
    await revealRcon(user);

    await user.click(screen.getByRole("button", { name: "Reveal value of Admin password" }));

    expect(await within(rowOf("Admin password")).findByText("admin-secret-value")).toBeVisible();
    expect(screen.queryByText(revealedValue)).not.toBeInTheDocument();
  });

  it("hides the value when its credential is staged for rotation, and Keep current value does not bring it back", async () => {
    const user = userEvent.setup();
    renderSettings(revealer);
    await revealRcon(user);

    await user.click(screen.getByRole("button", { name: "Rotate value of RCON password" }));
    expect(screen.queryByText(revealedValue)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Keep current value of RCON password" }));

    expect(screen.queryByText(revealedValue)).not.toBeInTheDocument();
    expect(within(rowOf("RCON password")).getByText("Value hidden")).toBeInTheDocument();
  });

  it("hides the value when a new value of its credential is typed", async () => {
    const user = userEvent.setup();
    renderSettings(revealer);
    await user.click(await screen.findByRole("button", { name: "Reveal value of Admin password" }));
    await screen.findByText("admin-secret-value");

    await user.type(screen.getByLabelText("New value for Admin password"), "x");

    expect(screen.queryByText("admin-secret-value")).not.toBeInTheDocument();
  });

  it("hides the value when a refetch brings a new version", async () => {
    const user = userEvent.setup();
    const { queryClient } = renderSettings(revealer);
    await revealRcon(user);

    vi.spyOn(hostingService, "secrets").mockResolvedValue([
      gslt,
      { ...rconPassword, version: 4 },
      adminPassword,
    ]);
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: ["hosting", "servers"] });
    });

    await waitFor(() => expect(screen.queryByText(revealedValue)).not.toBeInTheDocument());
    expect(within(rowOf("RCON password")).getByText("Configured, version 4")).toBeVisible();
  });

  it("shows the reveal error as an alert and no value when the reveal fails", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "revealSecret").mockRejectedValue(new Error("vault says no"));
    renderSettings(revealer);

    await user.click(await screen.findByRole("button", { name: "Reveal value of RCON password" }));

    const alert = await within(rowOf("RCON password")).findByRole("alert");
    expect(alert).toHaveTextContent("The value could not be revealed.");
    expect(within(rowOf("RCON password")).queryByText(/vault says no/)).not.toBeInTheDocument();
    expect(document.querySelector("code")).toBeNull();
    expect(within(rowOf("RCON password")).getByText("Value hidden")).toBeInTheDocument();
  });

  it("offers Reveal only to members who may reveal, for revealable and configured secrets", async () => {
    const view = renderSettings(credentialServer({}, { canRevealSecrets: false }));
    await screen.findByRole("heading", { name: "RCON password" });
    expect(screen.queryByRole("button", { name: /Reveal value/ })).not.toBeInTheDocument();
    view.unmount();

    mockHostingQueries([
      { ...gslt, revealable: true },
      secretStatus({ key: "plain", displayName: "Plain secret", isConfigured: true, version: 1 }),
      rconPassword,
    ]);
    renderSettings(revealer);
    await screen.findByRole("heading", { name: "RCON password" });

    expect(
      within(rowOf("RCON password")).getByRole("button", { name: "Reveal value of RCON password" }),
    ).toBeVisible();
    expect(
      within(rowOf("Plain secret")).queryByRole("button", { name: /Reveal value/ }),
    ).not.toBeInTheDocument();
    expect(
      within(rowOf(gslt.displayName)).queryByRole("button", { name: /Reveal value/ }),
    ).not.toBeInTheDocument();
  });

  it("keeps Reveal on an archived server while setting and rotating are disabled", async () => {
    renderSettings(credentialServer({}, { canRevealSecrets: true, isArchived: true }));

    expect(
      await screen.findByRole("button", { name: "Reveal value of RCON password" }),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Rotate value of RCON password" })).toBeDisabled();
    expect(screen.getByLabelText("New value for Admin password")).toBeDisabled();
  });

  it("stages a rotation, reviews it as a new value and applies it through the existing endpoint", async () => {
    const user = userEvent.setup();
    renderSettings();

    await user.click(await screen.findByRole("button", { name: "Rotate value of RCON password" }));

    const row = rowOf("RCON password");
    expect(within(row).getByText("A new value is generated on apply.")).toBeVisible();
    expect(badgeSlot(row)).not.toHaveAttribute("aria-hidden");
    expect(within(row).getByText("Changed")).toBeVisible();
    expect(
      within(row).getByRole("button", { name: "Keep current value of RCON password" }),
    ).toHaveTextContent("Keep current value");
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    expect(within(dialog).getByText("A new value will be generated.")).toBeVisible();
    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() => expect(hostingService.rotateSecret).toHaveBeenCalledTimes(1));
    expect(hostingService.rotateSecret).toHaveBeenCalledWith(server.primaryKey, "rcon-password");
    expect(hostingService.setSecret).not.toHaveBeenCalled();
  });

  it("undoes a staged rotation with Keep current value", async () => {
    const user = userEvent.setup();
    renderSettings();
    await user.click(await screen.findByRole("button", { name: "Rotate value of RCON password" }));

    await user.click(screen.getByRole("button", { name: "Keep current value of RCON password" }));

    expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Rotate value of RCON password" })).toBeVisible();
  });

  it("never writes a revealed value to storage, the console, the mutation cache, the URL or the DOM afterwards", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const setItem = vi.spyOn(Storage.prototype, "setItem");
    const consoleSpies = (["log", "info", "warn", "error", "debug", "trace"] as const).map(
      (method) => vi.spyOn(console, method).mockImplementation(() => undefined),
    );
    const { queryClient, router } = renderSettings(revealer);

    await revealRcon(user);
    await waitFor(() =>
      expect(
        queryClient
          .getMutationCache()
          .getAll()
          .every(
            (mutation) => !JSON.stringify(mutation.state.data ?? null).includes(revealedValue),
          ),
      ).toBe(true),
    );
    act(() => {
      vi.advanceTimersByTime(31_000);
    });

    const seen = (calls: unknown[][]) => calls.flat().map(String).join("|");
    expect(seen(setItem.mock.calls)).not.toContain(revealedValue);
    for (const spy of consoleSpies) expect(seen(spy.mock.calls)).not.toContain(revealedValue);
    expect(
      JSON.stringify(
        queryClient
          .getQueryCache()
          .getAll()
          .map((query) => query.state.data),
      ),
    ).not.toContain(revealedValue);
    expect(router.state.location.search + router.state.location.hash).not.toContain(revealedValue);
    expect(JSON.stringify(router.state.location.state ?? null)).not.toContain(revealedValue);
    expect(liveRegion().textContent).not.toContain(revealedValue);
    expect(document.body.innerHTML).not.toContain(revealedValue);
    expect(document.cookie).not.toContain(revealedValue);
  });
});

const generatedValue = "generated-once-9f2c";

describe("Credentials panel one-time value", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockHostingQueries([gslt, rconPassword]);
    vi.spyOn(hostingService, "revealSecret").mockRejectedValue(new Error("not expected"));
    // Like the API: after the rotation the saved list carries the new version.
    vi.spyOn(hostingService, "rotateSecret").mockImplementation(async (_serverId, key) => {
      vi.spyOn(hostingService, "secrets").mockResolvedValue([
        gslt,
        { ...rconPassword, version: 4 },
      ]);
      return { key, value: generatedValue, version: 4 };
    });
  });

  /** Stages a rotation of the RCON password, reviews it and applies it. */
  async function applyRotation(user: ReturnType<typeof userEvent.setup>): Promise<void> {
    await user.click(await screen.findByRole("button", { name: "Rotate value of RCON password" }));
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));
    await waitFor(() => expect(hostingService.rotateSecret).toHaveBeenCalledTimes(1));
  }

  it("shows the generated value once in its row, without Reveal, to a member who cannot reveal", async () => {
    const user = userEvent.setup();
    renderSettings();

    await applyRotation(user);

    const row = rowOf("RCON password");
    const code = await within(row).findByText(generatedValue);
    expect(code.tagName).toBe("CODE");
    expect(code).toHaveClass("credential__code", "credential__code--once");
    expect(
      within(row).getByText("New value. Copy it now: it is shown only this once."),
    ).toBeVisible();
    expect(within(row).queryByRole("button", { name: /Reveal value/ })).not.toBeInTheDocument();
    expect(within(row).queryByText("Value hidden")).not.toBeInTheDocument();
    expect(hostingService.revealSecret).not.toHaveBeenCalled();
  });

  it("shows no value in the review before the apply", async () => {
    const user = userEvent.setup();
    renderSettings();
    await user.click(await screen.findByRole("button", { name: "Rotate value of RCON password" }));

    await user.click(screen.getByRole("button", { name: "Review changes" }));

    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    expect(within(dialog).getByText("A new value will be generated.")).toBeVisible();
    expect(document.body.innerHTML).not.toContain(generatedValue);
  });

  it("keeps the value out of the apply result and the query and mutation caches", async () => {
    const user = userEvent.setup();
    const { queryClient } = renderSettings();

    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);

    expect(
      JSON.stringify(
        queryClient
          .getMutationCache()
          .getAll()
          .map((mutation) => [mutation.state.data, mutation.state.variables, mutation.state.error]),
      ),
    ).not.toContain(generatedValue);
    expect(
      JSON.stringify(
        queryClient
          .getQueryCache()
          .getAll()
          .map((query) => query.state.data),
      ),
    ).not.toContain(generatedValue);
  });

  it("removes the value when the section unmounts and does not bring it back on remount", async () => {
    const user = userEvent.setup();
    const view = renderSettings();
    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);

    view.unmount();
    expect(document.body.innerHTML).not.toContain(generatedValue);

    renderSettings();
    await screen.findByRole("heading", { name: "RCON password" });
    expect(document.body.innerHTML).not.toContain(generatedValue);
    expect(within(rowOf("RCON password")).getByText("Value hidden")).toBeInTheDocument();
  });

  it("removes the value when a new change of the credential is staged", async () => {
    const user = userEvent.setup();
    renderSettings();
    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);

    await user.click(screen.getByRole("button", { name: "Rotate value of RCON password" }));

    expect(screen.queryByText(generatedValue)).not.toBeInTheDocument();
    expect(document.body.innerHTML).not.toContain(generatedValue);
  });

  /** Replaces the clipboard the user-event setup installed with one whose writeText the test controls. */
  function stubClipboard(writeText: (text: string) => Promise<void>): void {
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });
  }

  /** Every text of the live and status regions of the page. */
  function announcements(): string {
    return Array.from(
      document.querySelectorAll('[aria-live], [role="status"], [role="alert"]'),
      (region) => region.textContent ?? "",
    ).join("|");
  }

  it("announces the new value once by name and never with the value", async () => {
    const user = userEvent.setup();
    renderSettings();

    await applyRotation(user);

    await waitFor(() =>
      expect(liveRegion()).toHaveTextContent(
        "A new value for RCON password was generated. It is shown once in Credentials.",
      ),
    );
    expect(announcements()).not.toContain(generatedValue);
  });

  it("copies the value only on click, exactly once per click", async () => {
    const user = userEvent.setup();
    const writeText = vi.fn(async () => undefined);
    renderSettings();
    stubClipboard(writeText);
    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);
    expect(writeText).not.toHaveBeenCalled();

    await user.click(within(rowOf("RCON password")).getByRole("button", { name: /Copy value/ }));

    expect(writeText).toHaveBeenCalledTimes(1);
    expect(writeText).toHaveBeenCalledWith(generatedValue);
    expect(
      await within(rowOf("RCON password")).findByText("The value of RCON password is copied."),
    ).toBeInTheDocument();
    expect(announcements()).not.toContain(generatedValue);
  });

  it("shows the shared failure text and keeps the value visible when the clipboard refuses", async () => {
    const user = userEvent.setup();
    const writeText = vi.fn(async () => {
      throw new Error("denied");
    });
    renderSettings();
    stubClipboard(writeText);
    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);

    await user.click(within(rowOf("RCON password")).getByRole("button", { name: /Copy value/ }));

    expect(
      await within(rowOf("RCON password")).findByText(
        "Copying failed. Select the text and copy it yourself.",
      ),
    ).toBeVisible();
    expect(within(rowOf("RCON password")).getByText(generatedValue)).toBeVisible();
  });

  it("hides the value with Hide value, brings the mask back and announces it", async () => {
    const user = userEvent.setup();
    renderSettings();
    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);

    await user.click(screen.getByRole("button", { name: "Hide value of RCON password" }));

    expect(screen.queryByText(generatedValue)).not.toBeInTheDocument();
    expect(document.body.innerHTML).not.toContain(generatedValue);
    expect(within(rowOf("RCON password")).getByText("Value hidden")).toBeInTheDocument();
    expect(liveRegion()).toHaveTextContent("The value of RCON password is hidden again.");
    expect(
      within(rowOf("RCON password")).queryByRole("button", { name: /Copy value/ }),
    ).not.toBeInTheDocument();
  });

  it("removes the value when a newer saved version arrives", async () => {
    const user = userEvent.setup();
    const { queryClient } = renderSettings();
    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);

    vi.spyOn(hostingService, "secrets").mockResolvedValue([gslt, { ...rconPassword, version: 5 }]);
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: ["hosting", "servers"] });
    });

    await waitFor(() => expect(screen.queryByText(generatedValue)).not.toBeInTheDocument());
    expect(within(rowOf("RCON password")).getByText("Configured, version 5")).toBeVisible();
  });

  it("removes the value when the credential is missing or no longer configured", async () => {
    const user = userEvent.setup();
    const { queryClient } = renderSettings();
    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);

    vi.spyOn(hostingService, "secrets").mockResolvedValue([
      gslt,
      { ...rconPassword, isConfigured: false, version: null },
    ]);
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: ["hosting", "servers"] });
    });
    await waitFor(() => expect(screen.queryByText(generatedValue)).not.toBeInTheDocument());

    // A later list that configures it again must not bring the value back.
    vi.spyOn(hostingService, "secrets").mockResolvedValue([gslt, { ...rconPassword, version: 4 }]);
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: ["hosting", "servers"] });
    });
    await within(rowOf("RCON password")).findByText("Configured, version 4");
    expect(screen.queryByText(generatedValue)).not.toBeInTheDocument();

    vi.spyOn(hostingService, "secrets").mockResolvedValue([gslt]);
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: ["hosting", "servers"] });
    });
    await waitFor(() =>
      expect(screen.queryByRole("heading", { name: "RCON password" })).not.toBeInTheDocument(),
    );
    expect(document.body.innerHTML).not.toContain(generatedValue);
  });

  it("keeps the value after 60 seconds because it has no timer", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderSettings();
    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);

    act(() => {
      vi.advanceTimersByTime(60_000);
    });

    expect(screen.getByText(generatedValue)).toBeInTheDocument();
    vi.useRealTimers();
  });

  it("never writes the value to storage, the console, the caches, the URL, the announcements or a toast", async () => {
    const user = userEvent.setup();
    const setItem = vi.spyOn(Storage.prototype, "setItem");
    const consoleSpies = (["log", "info", "warn", "error", "debug", "trace"] as const).map(
      (method) => vi.spyOn(console, method).mockImplementation(() => undefined),
    );
    const { queryClient, router } = renderSettings();

    await applyRotation(user);
    await within(rowOf("RCON password")).findByText(generatedValue);
    await user.click(screen.getByRole("button", { name: "Hide value of RCON password" }));

    const seen = (calls: unknown[][]) => calls.flat().map(String).join("|");
    expect(seen(setItem.mock.calls)).not.toContain(generatedValue);
    for (const spy of consoleSpies) expect(seen(spy.mock.calls)).not.toContain(generatedValue);
    expect(
      JSON.stringify(
        queryClient
          .getMutationCache()
          .getAll()
          .map((mutation) => [mutation.state.data, mutation.state.variables]),
      ),
    ).not.toContain(generatedValue);
    expect(
      JSON.stringify(
        queryClient
          .getQueryCache()
          .getAll()
          .map((query) => query.state.data),
      ),
    ).not.toContain(generatedValue);
    expect(router.state.location.search + router.state.location.hash).not.toContain(generatedValue);
    expect(JSON.stringify(router.state.location.state ?? null)).not.toContain(generatedValue);
    expect(announcements()).not.toContain(generatedValue);
    expect(document.cookie).not.toContain(generatedValue);
    expect(document.body.innerHTML).not.toContain(generatedValue);
  });
});

/** Browser persistence, cookies, console output and error text: none may appear in the panel source. */
const forbiddenInPanel =
  /\b(?:localStorage|sessionStorage|indexedDB)\b|document\s*\.\s*cookie|\bconsole\s*\.|\.\s*message\b/;

/** Removes block and line comments so documentation prose cannot trigger or hide a finding. */
function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
}

describe("Credentials panel chosen values", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockHostingQueries([gslt, rconPassword]);
  });

  const chosenValue = "Chosen-Value_42";

  it("shows an input with the generated hint for a generated credential that accepts a chosen value", async () => {
    renderSettings();

    const input = await screen.findByLabelText("New value for RCON password");
    expect(input).toBeVisible();
    expect(
      within(rowOf("RCON password")).getByText(
        "Type your own value or use Rotate value to generate one. The value is not shown in the review.",
      ),
    ).toBeVisible();
    expect(
      within(rowOf(gslt.displayName)).getByText(
        "Staged when you enter a value. The value is not shown in the review.",
      ),
    ).toBeVisible();
  });

  it("stages a typed value, reviews it as changed and applies it as a set, never as a rotation", async () => {
    const user = userEvent.setup();
    renderSettings();
    await user.type(await screen.findByLabelText("New value for RCON password"), chosenValue);

    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    expect(within(dialog).getByText("Changed. The value is not shown.")).toBeVisible();
    expect(within(dialog).queryByText("A new value will be generated.")).not.toBeInTheDocument();
    expect(dialog.innerHTML).not.toContain(chosenValue);
    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() => expect(hostingService.setSecret).toHaveBeenCalledTimes(1));
    expect(hostingService.setSecret).toHaveBeenCalledWith(
      server.primaryKey,
      "rcon-password",
      chosenValue,
      false,
    );
    expect(hostingService.rotateSecret).not.toHaveBeenCalled();
  });

  it("replaces a staged rotation when a value is typed", async () => {
    const user = userEvent.setup();
    renderSettings();
    await user.click(await screen.findByRole("button", { name: "Rotate value of RCON password" }));
    expect(screen.getByText("A new value is generated on apply.")).toBeVisible();

    await user.type(screen.getByLabelText("New value for RCON password"), chosenValue);

    expect(screen.queryByText("A new value is generated on apply.")).not.toBeInTheDocument();
    expect(screen.getByText("A new value is staged.")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    expect(within(dialog).getByText("Changed. The value is not shown.")).toBeVisible();
    expect(within(dialog).queryByText("A new value will be generated.")).not.toBeInTheDocument();
  });

  it("offers Undo change instead of Rotate value while a typed value is staged", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for RCON password");
    const row = rowOf("RCON password");
    expect(
      within(row).getByRole("button", { name: "Rotate value of RCON password" }),
    ).toBeVisible();

    await user.type(input, chosenValue);

    expect(
      within(row).queryByRole("button", { name: "Rotate value of RCON password" }),
    ).not.toBeInTheDocument();
    expect(
      within(row).queryByRole("button", { name: "Keep current value of RCON password" }),
    ).not.toBeInTheDocument();
    await user.click(within(row).getByRole("button", { name: "Undo change: RCON password" }));

    expect(input).toHaveValue("");
    expect(input).toHaveFocus();
    expect(
      within(row).getByRole("button", { name: "Rotate value of RCON password" }),
    ).toBeVisible();
  });

  it("shows no input but Rotate value for a generated credential that accepts no chosen value", async () => {
    mockHostingQueries([gslt, { ...rconPassword, acceptsChosenValue: false }]);
    renderSettings();

    expect(
      await screen.findByRole("button", { name: "Rotate value of RCON password" }),
    ).toBeVisible();
    expect(screen.queryByLabelText("New value for RCON password")).not.toBeInTheDocument();
    expect(screen.getByLabelText(`New value for ${gslt.displayName}`)).toBeVisible();
  });
});

describe("Credentials panel value rules and typed value toggle", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockHostingQueries([gslt, rconPassword, adminPassword]);
  });

  const tokenMessage =
    "Use only letters, digits and symbols, without spaces, quotes or backslashes.";
  const singleLineMessage = "Use a single line without control characters.";
  const badToken = "two words";

  it("shows the token message with aria-invalid after blur and none before", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for RCON password");

    await user.type(input, badToken);
    expect(screen.queryByText(tokenMessage)).not.toBeInTheDocument();
    expect(input).not.toHaveAttribute("aria-invalid");

    await user.tab();
    expect(await screen.findByText(tokenMessage)).toBeVisible();
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(screen.queryByText(badToken)).not.toBeInTheDocument();
  });

  it("keeps the review closed with the invalid alert and moves focus to the invalid input", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for RCON password");
    await user.type(input, badToken);

    await user.click(screen.getByRole("button", { name: "Review changes" }));

    expect(screen.queryByRole("dialog", { name: "Review changes" })).not.toBeInTheDocument();
    expect(screen.getByText("Fix the highlighted setting before reviewing.")).toBeVisible();
    expect(await screen.findByText(tokenMessage)).toBeVisible();
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveFocus();
  });

  it("opens the review once the invalid value is corrected", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for RCON password");
    await user.type(input, badToken);
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    await user.clear(input);
    await user.type(input, "Chosen-Value_42");

    await user.click(screen.getByRole("button", { name: "Review changes" }));

    expect(await screen.findByRole("dialog", { name: "Review changes" })).toBeVisible();
  });

  it("refuses control characters in a single-line value and accepts spaces there", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for Admin password");

    fireEvent.change(input, { target: { value: "a\u0007b" } });
    fireEvent.blur(input);
    expect(await screen.findByText(singleLineMessage)).toBeVisible();
    expect(input).toHaveAttribute("aria-invalid", "true");

    await user.clear(input);
    await user.type(input, badToken);
    await user.tab();
    expect(screen.queryByText(singleLineMessage)).not.toBeInTheDocument();
    expect(screen.queryByText(tokenMessage)).not.toBeInTheDocument();
    expect(input).not.toHaveAttribute("aria-invalid");
  });

  it("maps a value of only spaces and an overlong value to fixed copy without the value", async () => {
    renderSettings();
    const input = await screen.findByLabelText("New value for Admin password");

    fireEvent.change(input, { target: { value: "   " } });
    fireEvent.blur(input);
    expect(await screen.findByText("Enter a value that is not only spaces.")).toBeVisible();

    fireEvent.change(input, { target: { value: "a".repeat(4097) } });
    expect(await screen.findByText("Use at most 4096 characters.")).toBeVisible();
  });

  it("shows the typed value on request and masks it again", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for RCON password");
    // The toggle always holds its slot; it is disabled while nothing is typed.
    expect(
      screen.getByRole("button", { name: "Show typed value of RCON password" }),
    ).toBeDisabled();
    await user.type(input, "Chosen-Value_42");

    const toggle = screen.getByRole("button", { name: "Show typed value of RCON password" });
    expect(toggle).toBeEnabled();
    expect(toggle).toHaveAttribute("aria-pressed", "false");
    expect(input).toHaveAttribute("type", "password");

    await user.click(toggle);
    expect(input).toHaveAttribute("type", "text");
    const pressed = screen.getByRole("button", { name: "Hide typed value of RCON password" });
    expect(pressed).toHaveAttribute("aria-pressed", "true");
    expect(pressed).toHaveTextContent("Hide typed value");

    await user.click(pressed);
    expect(input).toHaveAttribute("type", "password");
    expect(
      screen.getByRole("button", { name: "Show typed value of RCON password" }),
    ).toHaveAttribute("aria-pressed", "false");
  });

  it("masks the typed value again after Undo change", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for RCON password");
    await user.type(input, "Chosen-Value_42");
    await user.click(screen.getByRole("button", { name: "Show typed value of RCON password" }));
    expect(input).toHaveAttribute("type", "text");

    await user.click(screen.getByRole("button", { name: "Undo change: RCON password" }));
    expect(input).toHaveAttribute("type", "password");

    await user.type(input, "Other-Value_7");
    expect(input).toHaveAttribute("type", "password");
    expect(
      screen.getByRole("button", { name: "Show typed value of RCON password" }),
    ).toHaveAttribute("aria-pressed", "false");
  });

  it("masks the typed value again after the change was applied", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for RCON password");
    await user.type(input, "Chosen-Value_42");
    await user.click(screen.getByRole("button", { name: "Show typed value of RCON password" }));
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));
    await waitFor(() => expect(hostingService.setSecret).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
    );

    expect(input).toHaveValue("");
    expect(input).toHaveAttribute("type", "password");
    await user.type(input, "Another-Value_1");
    expect(input).toHaveAttribute("type", "password");
  });

  it("disables the toggle while the change is being applied", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "setSecret").mockReturnValue(new Promise(() => {}));
    renderSettings();
    await user.type(await screen.findByLabelText("New value for RCON password"), "Chosen-Value_42");
    expect(screen.getByRole("button", { name: "Show typed value of RCON password" })).toBeEnabled();
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });

    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() => expect(hostingService.setSecret).toHaveBeenCalledTimes(1));
    expect(
      screen.getByRole("button", { name: "Show typed value of RCON password" }),
    ).toBeDisabled();
  });

  it("never puts the typed value into an error text, an announcement or the review", async () => {
    const user = userEvent.setup();
    renderSettings();
    await user.type(
      await screen.findByLabelText("New value for RCON password"),
      'bad "value" with spaces',
    );
    await user.click(screen.getByRole("button", { name: "Review changes" }));

    await screen.findByText(tokenMessage);
    for (const alert of screen.getAllByRole("alert")) {
      expect(alert.textContent).not.toContain('bad "value"');
      expect(alert.textContent).not.toContain("with spaces");
    }
    expect(liveRegion().textContent).not.toContain("bad");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

/** The structure of a credential row that must not depend on the staging state. */
function rowStructure(row: HTMLElement) {
  const actions = row.querySelector(".credential__edit-actions");
  return {
    article: Array.from(row.children, (child) => child.className),
    badgeSlot: row.querySelector(".credential__badge-slot") !== null,
    editActions: Array.from(actions?.children ?? [], (child) => child.className),
  };
}

/** The visible text of the two slots of the edit actions, in order. */
function slotTexts(row: HTMLElement): string[] {
  const actions = row.querySelector(".credential__edit-actions");
  return Array.from(actions?.children ?? [], (child) => child.textContent ?? "");
}

describe("Credentials panel fixed slots", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockHostingQueries([gslt, rconPassword, adminPassword]);
    vi.spyOn(hostingService, "revealSecret").mockImplementation(async (_serverId, key) => ({
      key,
      value: key === "rcon-password" ? revealedValue : "admin-secret-value",
      version: key === "rcon-password" ? 3 : 2,
    }));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("keeps the structure of a generated row equal while staging, keeping, typing and undoing", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for RCON password");
    const row = rowOf("RCON password");
    const idle = rowStructure(row);
    expect(idle.badgeSlot).toBe(true);
    expect(idle.editActions).toEqual(["credential__slot", "credential__toggle-slot"]);

    await user.click(within(row).getByRole("button", { name: "Rotate value of RCON password" }));
    expect(rowStructure(row)).toEqual(idle);
    await user.click(
      within(row).getByRole("button", { name: "Keep current value of RCON password" }),
    );
    expect(rowStructure(row)).toEqual(idle);
    await user.type(input, "Chosen-Value_42");
    expect(rowStructure(row)).toEqual(idle);
    await user.click(within(row).getByRole("button", { name: "Undo change: RCON password" }));
    expect(rowStructure(row)).toEqual(idle);
  });

  it("keeps the structure of a user-provided row equal while a value is staged and undone", async () => {
    const user = userEvent.setup();
    renderSettings();
    const input = await screen.findByLabelText("New value for Admin password");
    const row = rowOf("Admin password");
    const idle = rowStructure(row);
    expect(idle.editActions).toEqual(["credential__slot", "credential__toggle-slot"]);

    await user.type(input, "x");
    expect(rowStructure(row)).toEqual(idle);
    await user.click(within(row).getByRole("button", { name: "Undo change: Admin password" }));
    expect(rowStructure(row)).toEqual(idle);
  });

  it("marks the badge slot idle and hidden from assistive technology only while nothing is staged", async () => {
    const user = userEvent.setup();
    renderSettings();
    await screen.findByLabelText("New value for RCON password");
    const row = rowOf("RCON password");
    expect(badgeSlot(row)).toHaveAttribute("aria-hidden", "true");
    expect(badgeSlot(row)).toHaveClass("credential__badge-slot--idle");

    await user.click(within(row).getByRole("button", { name: "Rotate value of RCON password" }));
    expect(badgeSlot(row)).not.toHaveAttribute("aria-hidden");
    expect(badgeSlot(row)).not.toHaveClass("credential__badge-slot--idle");

    await user.click(
      within(row).getByRole("button", { name: "Keep current value of RCON password" }),
    );
    expect(badgeSlot(row)).toHaveAttribute("aria-hidden", "true");
    expect(badgeSlot(row)).toHaveClass("credential__badge-slot--idle");
  });

  it("fills the context slot with Rotate value, Keep current value or Undo change and leaves it empty for a plain row", async () => {
    const user = userEvent.setup();
    renderSettings();
    await screen.findByLabelText("New value for RCON password");
    const generated = rowOf("RCON password");
    const plain = rowOf("Admin password");
    expect(slotTexts(generated)).toEqual(["Rotate value", "Show typed value"]);
    expect(slotTexts(plain)).toEqual(["", "Show typed value"]);

    await user.click(within(generated).getByRole("button", { name: /^Rotate value of/ }));
    expect(slotTexts(generated)).toEqual(["Keep current value", "Show typed value"]);
    await user.click(within(generated).getByRole("button", { name: /^Keep current value of/ }));
    await user.type(within(generated).getByLabelText("New value for RCON password"), "a");
    expect(slotTexts(generated)).toEqual(["Undo change", "Show typed value"]);
    await user.type(within(plain).getByLabelText("New value for Admin password"), "a");
    expect(slotTexts(plain)).toEqual(["Undo change", "Show typed value"]);
  });

  it("leaves the toggle slot empty when the credential takes no typed value", async () => {
    mockHostingQueries([gslt, { ...rconPassword, acceptsChosenValue: false }]);
    renderSettings();
    await screen.findByRole("button", { name: "Rotate value of RCON password" });

    expect(slotTexts(rowOf("RCON password"))).toEqual(["Rotate value", ""]);
  });

  it("shows exactly one status text per state and no separate staged notice", async () => {
    const user = userEvent.setup();
    const { container } = renderSettings();
    await screen.findByLabelText("New value for RCON password");
    const row = rowOf("RCON password");
    const status = () => row.querySelector(".credential__status")?.textContent;
    expect(status()).toBe("Configured, version 3");
    expect(row.querySelectorAll(".credential__status")).toHaveLength(1);

    await user.click(within(row).getByRole("button", { name: /^Rotate value of/ }));
    expect(status()).toBe("A new value is generated on apply.");
    await user.click(within(row).getByRole("button", { name: /^Keep current value of/ }));
    expect(status()).toBe("Configured, version 3");
    await user.type(within(row).getByLabelText("New value for RCON password"), "a");
    expect(status()).toBe("A new value is staged.");
    expect(container.querySelector(".credential__notice")).toBeNull();
    expect(within(rowOf(gslt.displayName)).getByText("Not set")).toBeVisible();
  });

  const revealer = credentialServer({}, { canRevealSecrets: true });

  /** Renders the section as a member who may reveal and returns the RCON password row. */
  async function renderRevealer(): Promise<HTMLElement> {
    renderSettings(revealer);
    await screen.findByRole("heading", { name: "RCON password" });
    return rowOf("RCON password");
  }

  it("keeps Reveal value in place while a rotation is staged and reveals the saved value", async () => {
    const user = userEvent.setup();
    const row = await renderRevealer();
    await user.click(within(row).getByRole("button", { name: /^Rotate value of/ }));

    await user.click(within(row).getByRole("button", { name: "Reveal value of RCON password" }));

    expect(await within(row).findByText(revealedValue)).toBeVisible();
    expect(hostingService.revealSecret).toHaveBeenCalledTimes(1);
    expect(within(row).getByText("A new value is generated on apply.")).toBeVisible();
  });

  it("hides a value revealed during a staged rotation when the staged entry changes afterwards", async () => {
    const user = userEvent.setup();
    const row = await renderRevealer();
    await user.click(within(row).getByRole("button", { name: /^Rotate value of/ }));
    await user.click(within(row).getByRole("button", { name: "Reveal value of RCON password" }));
    await within(row).findByText(revealedValue);

    await user.click(within(row).getByRole("button", { name: /^Keep current value of/ }));

    expect(screen.queryByText(revealedValue)).not.toBeInTheDocument();
    expect(within(row).getByText("Value hidden")).toBeInTheDocument();
  });

  it("hides a value revealed during a staged rotation after 30 seconds", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const row = await renderRevealer();
    await user.click(within(row).getByRole("button", { name: /^Rotate value of/ }));
    await user.click(within(row).getByRole("button", { name: "Reveal value of RCON password" }));
    await within(row).findByText(revealedValue);

    act(() => {
      vi.advanceTimersByTime(31_000);
    });

    expect(screen.queryByText(revealedValue)).not.toBeInTheDocument();
  });
});

describe("Credentials panel focus after the member's own apply", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    mockHostingQueries([gslt, rconPassword]);
    vi.spyOn(hostingService, "rotateSecret").mockImplementation(async (_serverId, key) => {
      vi.spyOn(hostingService, "secrets").mockResolvedValue([
        gslt,
        { ...rconPassword, version: 4 },
      ]);
      return { key, value: generatedValue, version: 4 };
    });
    Element.prototype.scrollIntoView = vi.fn();
  });

  afterEach(() => {
    Reflect.deleteProperty(Element.prototype, "scrollIntoView");
  });

  /** Reviews and applies whatever is staged. */
  async function applyStaged(user: ReturnType<typeof userEvent.setup>): Promise<void> {
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await user.click(within(dialog).getByRole("button", { name: /^Apply \d+ changes?$/ }));
  }

  it("focuses the one-time block of a rotated credential, named by its once-label, and scrolls it into view", async () => {
    const user = userEvent.setup();
    renderSettings();
    await user.click(await screen.findByRole("button", { name: "Rotate value of RCON password" }));

    await applyStaged(user);

    const block = await within(rowOf("RCON password")).findByRole("group", {
      name: "New value. Copy it now: it is shown only this once.",
    });
    await waitFor(() => expect(block).toHaveFocus());
    expect(block).toHaveAttribute("tabindex", "-1");
    expect(block).toContainElement(screen.getByText(generatedValue));
    expect(Element.prototype.scrollIntoView).toHaveBeenCalledWith({
      block: "nearest",
      behavior: "auto",
    });
    expect(screen.getByRole("heading", { level: 2, name: "Settings" })).not.toHaveFocus();
  });

  it("focuses the status line of a credential that saved a typed value", async () => {
    const user = userEvent.setup();
    renderSettings();
    await user.type(await screen.findByLabelText(`New value for ${gslt.displayName}`), typedValue);

    await applyStaged(user);

    const status = rowOf(gslt.displayName).querySelector<HTMLElement>(".credential__status");
    expect(status).not.toBeNull();
    await waitFor(() => expect(status).toHaveFocus());
    expect(status).toHaveAttribute("tabindex", "-1");
    expect(screen.getByRole("heading", { level: 2, name: "Settings" })).not.toHaveFocus();
    expect(document.body.innerHTML).not.toContain(typedValue);
  });

  it("opens the Credentials panel before it focuses when the member closed it", async () => {
    const user = userEvent.setup();
    renderSettings();
    await user.click(await screen.findByRole("button", { name: "Rotate value of RCON password" }));
    await user.click(screen.getByRole("button", { name: /^Credentials/ }));
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });

    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    const block = await within(rowOf("RCON password")).findByRole("group");
    await waitFor(() => expect(block).toHaveFocus());
  });

  it("moves no focus on a background refetch after the apply", async () => {
    const user = userEvent.setup();
    const { queryClient } = renderSettings();
    await user.click(await screen.findByRole("button", { name: "Rotate value of RCON password" }));
    await applyStaged(user);
    const block = await within(rowOf("RCON password")).findByRole("group");
    await waitFor(() => expect(block).toHaveFocus());
    const input = screen.getByLabelText(`New value for ${gslt.displayName}`);
    input.focus();
    vi.mocked(Element.prototype.scrollIntoView).mockClear();

    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: ["hosting", "servers"] });
    });
    await act(async () => {
      await new Promise((resolve) => requestAnimationFrame(() => resolve(undefined)));
    });

    expect(input).toHaveFocus();
    expect(Element.prototype.scrollIntoView).not.toHaveBeenCalled();
  });
});

describe("Credentials panel source scan", () => {
  const panelSource = readFileSync(
    resolve(
      import.meta.dirname,
      "..",
      "..",
      "src",
      "features",
      "hosting",
      "ServerSecretsPanel.tsx",
    ),
    "utf8",
  );

  it("finds a forbidden pattern in an inline bad sample, so the scan is live", () => {
    expect(forbiddenInPanel.test('window.localStorage.setItem("v", value);')).toBe(true);
    expect(forbiddenInPanel.test("console.log(value);")).toBe(true);
    expect(forbiddenInPanel.test("setText(error.message);")).toBe(true);
    expect(forbiddenInPanel.test("const consoleWidth = 3;")).toBe(false);
  });

  it("holds no storage, cookie, console or error-text access", () => {
    expect(panelSource.length).toBeGreaterThan(0);
    expect(forbiddenInPanel.exec(stripComments(panelSource))).toBeNull();
  });

  it("reveals through a mutation that is not kept in the cache and hides on every trigger", () => {
    expect(panelSource).toContain("gcTime: 0");
    expect(panelSource).toContain("visibilitychange");
    expect(panelSource).toMatch(/30_000|30000/);
  });
});
