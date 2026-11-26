import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useEffect, useRef, useState } from "react";
import { beforeEach } from "vitest";
import { ApiError } from "@/api/client";
import i18n, { i18nReady } from "@/app/i18n";
import { ModPackDiff } from "@/components/server/ModPackDiff";
import { ReviewChangesDialog } from "@/components/server/ReviewChangesDialog";
import type { ModPackComparison, ModPackItem } from "@/features/hosting/modPackDiff";
import type { SavedServerChanges } from "@/features/hosting/serverChangeDraft";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
import { hostingService } from "@/features/hosting/services";
import type { GameConfiguration, GameServer } from "@/features/hosting/types";
import { useApplyServerChanges, useReviewGroups } from "@/features/hosting/useServerChangeApply";
import {
  ServerChangeDraftProvider,
  useServerChangeDraft,
  type ServerChangeDraftContextValue,
} from "@/features/hosting/useServerChangeDraft";
import { configurationEntry } from "../helpers/configurationFixtures";
import { fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/* The review dialog for every change kind, per-item removal, partial results, busy state (D-03, D-04, D-08). */

const secretValue = "s3cr3t-pass-9";
const maxPlayers = configurationEntry({
  key: "server.cfg@maxPlayers",
  displayName: "Maximum players",
  kind: "Integer",
  minimum: 1,
  maximum: 128,
  value: 64,
  required: true,
});
const configuration = {
  serverId: "server",
  profileId: null,
  profileRevisionId: null,
  profileRevisionNumber: null,
  isComplete: true,
  entries: [maxPlayers],
} as unknown as GameConfiguration;
const saved: SavedServerChanges = {
  modPackId: 1,
  profile: { profileId: null, revisionId: null },
  entries: configuration.entries,
};
const server = {
  ...fleetServer({ title: "Alpha Team", gameIdentifier: "arma3" }),
  selectedModPackDefinitionId: 1,
  selectedModPackDefinitionTitle: "Core",
  selectedConfigurationProfileId: null,
  selectedConfigurationProfileTitle: null,
  canModify: true,
  canManageSecrets: true,
} as unknown as GameServer;

type Seed = (draft: ServerChangeDraftContextValue) => void;

/** Stages everything the dialog can show: a mod pack, a profile, a value and a credential. */
const stageEverything: Seed = (draft) => {
  draft.stageModPack(2);
  draft.stageProfile({ profileId: "profile-1", revisionId: null });
  draft.stageValue("server.cfg@maxPlayers", "80");
  draft.stageSecret("RCON_PASSWORD", { kind: "set", value: secretValue, externalProvider: false });
};

/** Mounts the real review dialog over the real hooks, seeded through staging calls. */
function Harness({ seed, applied }: { seed: Seed; applied: (count: number) => void }) {
  const draft = useServerChangeDraft();
  const groups = useReviewGroups(server);
  const [dismissed, setDismissed] = useState(false);
  const applier = useApplyServerChanges(server, { onAllApplied: applied });
  const seeded = useRef(false);
  useEffect(() => {
    if (seeded.current) return;
    seeded.current = true;
    seed(draft);
  }, [draft, seed]);
  return (
    <ReviewChangesDialog
      open={draft.count > 0 && !dismissed}
      serverTitle={server.title}
      running={false}
      groups={groups}
      count={draft.count}
      results={applier.results}
      busy={applier.pending}
      onApply={applier.apply}
      onDismiss={() => setDismissed(true)}
      onRemoveItem={draft.removeItem}
    />
  );
}

function renderDialog(seed: Seed = stageEverything, applied: (count: number) => void = vi.fn()) {
  return renderWithProviders(
    <ServerChangeDraftProvider saved={saved} readOnly={false}>
      <Harness seed={seed} applied={applied} />
    </ServerChangeDraftProvider>,
  );
}

describe("ReviewChangesDialog", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "configuration").mockResolvedValue(configuration);
    vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([
      {
        primaryKey: "profile-1",
        title: "Operations",
        gameIdentifier: "arma3",
        isArchived: false,
        latestRevisionNumber: 3,
      },
    ]);
    vi.spyOn(hostingService, "modPackOptions").mockResolvedValue([
      { primaryKey: 1, title: "Core" },
      { primaryKey: 2, title: "Extended" },
    ]);
    vi.spyOn(hostingService, "secrets").mockResolvedValue([
      { key: "RCON_PASSWORD", displayName: "Remote console password" } as never,
    ]);
    vi.spyOn(hostingService, "changeModPack").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "selectConfigurationProfile").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "updateConfiguration").mockResolvedValue(configuration);
    vi.spyOn(hostingService, "setSecret").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "rotateSecret").mockResolvedValue({
      key: "RCON_PASSWORD",
      value: "generated-in-review-test",
      version: 2,
    });
  });

  it("renders the groups in their fixed order with counts and never shows a typed secret", async () => {
    renderDialog();

    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await within(dialog).findByText("Extended");
    expect(
      within(dialog)
        .getAllByRole("heading", { level: 3 })
        .map((h) => h.textContent),
    ).toEqual(["Mod pack", "Configuration profile", "Settings (1)", "Credentials (1)"]);
    const modPack = within(dialog).getByText("Extended").closest("li") as HTMLElement;
    expect(within(modPack).getByText("Core")).toHaveAttribute("lang", "en");
    const profile = within(dialog).getByText("Operations, following the latest revision");
    expect(profile.closest("li")).toHaveTextContent("Game defaults");
    const credential = within(dialog).getByText("Remote console password").closest("li");
    expect(credential).toHaveTextContent("Changed. The value is not shown.");
    expect(credential).not.toHaveTextContent("Before");
    expect(document.body.textContent).not.toContain(secretValue);
    expect(document.body.innerHTML).not.toContain(secretValue);
  });

  it("shows each setting as one compact row with hidden terms and an icon-only remove control", async () => {
    renderDialog();
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await within(dialog).findByText("Extended");

    const row = within(dialog).getByText("Maximum players").closest("li") as HTMLElement;
    const terms = Array.from(row.querySelectorAll("dt"));
    expect(terms.map((term) => term.textContent)).toEqual(["Before", "After"]);
    for (const term of terms) expect(term).toHaveClass("sr-only");
    const arrows = row.querySelectorAll(".change-item__values > svg");
    expect(arrows).toHaveLength(1);
    expect(arrows[0]).toHaveAttribute("aria-hidden", "true");
    expect(row.querySelector(".change-item__pair--before")).toHaveTextContent("64");
    expect(row.querySelector(".change-item__pair--after")).toHaveTextContent("80");

    const remove = within(row).getByRole("button", {
      name: "Remove from changes: Maximum players",
    });
    expect(remove).toHaveTextContent("");
    expect(remove).toHaveClass("button--icon-only");
    expect(remove).toHaveAttribute("data-remove-item", "value:server.cfg@maxPlayers");
    expect(remove.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
  });

  it("shows a credential row with only the after value", async () => {
    renderDialog((draft) =>
      draft.stageSecret("RCON_PASSWORD", {
        kind: "set",
        value: secretValue,
        externalProvider: false,
      }),
    );
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });

    const row = (await within(dialog).findByText("Remote console password")).closest(
      "li",
    ) as HTMLElement;
    expect(row.querySelectorAll("dt")).toHaveLength(1);
    expect(row.querySelectorAll(".change-item__values > svg")).toHaveLength(0);
    expect(row.querySelector(".change-item__pair--after")).toHaveTextContent(
      "Changed. The value is not shown.",
    );
    expect(document.body.innerHTML).not.toContain(secretValue);
  });

  it("opens with one compact note instead of an info panel and keeps the dialog class", async () => {
    renderDialog();
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await within(dialog).findByText("Extended");

    const note = dialog.querySelector("p.review-note") as HTMLElement;
    expect(note).toHaveTextContent("These changes take effect the next time Alpha Team starts.");
    expect(note.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
    expect(dialog.querySelector(".state-panel")).toBeNull();
    expect(dialog).toHaveClass("dialog--review");
    const body = dialog.querySelector(".dialog__body") as HTMLElement;
    expect(body.firstElementChild).toBe(dialog.querySelector("span[hidden]"));
    expect(note.previousElementSibling).toBe(dialog.querySelector("span[hidden]"));
    expect(dialog.querySelectorAll("section.review-group")).toHaveLength(4);
  });

  it("describes a generated credential as a new value", async () => {
    renderDialog((draft) => draft.stageSecret("RCON_PASSWORD", { kind: "rotate" }));

    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    expect(await within(dialog).findByText("A new value will be generated.")).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Apply 1 change" })).toBeVisible();
  });

  it("removes an item, moves focus on, and closes the dialog when the draft becomes empty", async () => {
    const user = userEvent.setup();
    renderDialog();
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await within(dialog).findByText("Extended");

    await user.click(
      within(dialog).getByRole("button", { name: "Remove from changes: Maximum players" }),
    );
    expect(within(dialog).queryByText("Maximum players")).not.toBeInTheDocument();
    await waitFor(() =>
      expect(
        within(dialog).getByRole("button", {
          name: "Remove from changes: Remote console password",
        }),
      ).toHaveFocus(),
    );

    await user.click(
      within(dialog).getByRole("button", {
        name: "Remove from changes: Remote console password",
      }),
    );
    await waitFor(() =>
      expect(
        within(dialog).getByRole("heading", { level: 2, name: "Review changes" }),
      ).toHaveFocus(),
    );

    await user.click(within(dialog).getByRole("button", { name: "Remove from changes: Mod pack" }));
    await user.click(
      within(dialog).getByRole("button", { name: "Remove from changes: Configuration profile" }),
    );
    await waitFor(() =>
      expect(screen.queryByRole("dialog", { name: "Review changes" })).not.toBeInTheDocument(),
    );
  });

  it("keeps failed and not-tried items with a reason, focuses the results line and retries them", async () => {
    const user = userEvent.setup();
    const applied = vi.fn();
    vi.mocked(hostingService.selectConfigurationProfile).mockRejectedValueOnce(
      new ApiError("must never be printed: profile", 409, "conflict"),
    );
    vi.mocked(hostingService.setSecret).mockRejectedValueOnce(
      new Error("must never be printed: secret"),
    );
    renderDialog(stageEverything, applied);
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });
    await within(dialog).findByText("Extended");

    await user.click(within(dialog).getByRole("button", { name: "Apply 4 changes" }));

    const alert = await within(dialog).findByRole("alert");
    expect(alert).toHaveTextContent("1 of 4 changes applied. The rest stay staged.");
    expect(alert).toHaveFocus();
    expect(within(dialog).queryByText("Extended")).not.toBeInTheDocument();
    const profile = within(dialog).getByText("Configuration profile", { selector: "span" });
    expect(profile.closest("li")).toHaveTextContent("Not applied");
    expect(profile.closest("li")).toHaveTextContent(
      "The server changed in the meantime. Reload and try again.",
    );
    const settings = within(dialog).getByText("Maximum players").closest("li");
    expect(settings).toHaveTextContent("Not tried");
    expect(settings).toHaveTextContent(
      "Not tried because the configuration profile change failed.",
    );
    const credential = within(dialog).getByText("Remote console password").closest("li");
    expect(credential).toHaveTextContent(
      "The change could not be applied. Nothing else was affected.",
    );
    expect(document.body.textContent).not.toContain("must never be printed");
    expect(hostingService.updateConfiguration).not.toHaveBeenCalled();
    expect(applied).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole("button", { name: "Try 3 changes again" }));

    await waitFor(() => expect(applied).toHaveBeenCalledWith(3));
    expect(hostingService.changeModPack).toHaveBeenCalledTimes(1);
    expect(hostingService.selectConfigurationProfile).toHaveBeenCalledTimes(2);
    expect(hostingService.updateConfiguration).toHaveBeenCalledTimes(1);
    expect(hostingService.setSecret).toHaveBeenCalledTimes(2);
    await waitFor(() =>
      expect(screen.queryByRole("dialog", { name: "Review changes" })).not.toBeInTheDocument(),
    );
  });

  it("maps a forbidden rejection to the forbidden reason", async () => {
    const user = userEvent.setup();
    vi.mocked(hostingService.selectConfigurationProfile).mockRejectedValue(
      new ApiError("must never be printed", 403, "forbidden"),
    );
    renderDialog((draft) => draft.stageProfile({ profileId: "profile-1", revisionId: null }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });

    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    expect(
      await within(dialog).findByText("You are not allowed to make this change."),
    ).toBeVisible();
    expect(within(dialog).getByRole("alert")).toHaveTextContent(
      "0 of 1 changes applied. The rest stay staged.",
    );
    expect(within(dialog).getByRole("button", { name: "Try 1 change again" })).toBeVisible();
    expect(document.body.textContent).not.toContain("must never be printed");
  });
});

describe("ModPackDiff disclosure", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });

  const mod = (title: string): ModPackItem => ({ key: `workshop:${title}`, title });

  /** Builds a ready comparison from the three groups. */
  function ready(added: string[], removed: string[], unchanged: string[] = []): ModPackComparison {
    return {
      status: "ready",
      before: [],
      after: [],
      diff: { added: added.map(mod), removed: removed.map(mod), unchanged: unchanged.map(mod) },
    };
  }

  it("renders a closed disclosure counting added plus removed mods, and opens to the groups", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ModPackDiff comparison={ready(["A", "B"], ["C"], ["D"])} />);

    const toggle = screen.getByRole("button", { name: "Show 3 mod changes" });
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByText("Added (2)")).not.toBeVisible();

    await user.click(toggle);

    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText("Added (2)")).toBeVisible();
    expect(screen.getByText("Removed (1)")).toBeVisible();
    expect(screen.getByText("Unchanged (1)")).toBeVisible();
  });

  it("uses the singular title for one change", () => {
    renderWithProviders(<ModPackDiff comparison={ready(["A"], [])} />);

    expect(screen.getByRole("button", { name: "Show 1 mod change" })).toBeVisible();
  });

  it("keeps the same-mods and could-not-compare lines outside a disclosure", () => {
    const { unmount } = renderWithProviders(<ModPackDiff comparison={ready([], [], ["A"])} />);
    expect(screen.getByText("Both packs contain the same mods.")).toBeVisible();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    unmount();

    renderWithProviders(<ModPackDiff comparison={{ ...ready([], []), status: "failed" }} />);
    expect(
      screen.getByText("The contents could not be compared. You can still apply this change."),
    ).toBeVisible();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});

describe("review dialog stylesheet", () => {
  const source = readFileSync(
    resolve(import.meta.dirname, "..", "..", "src", "components", "server", "change.css"),
    "utf8",
  );
  const rules = Array.from(
    source.replace(/\/\*[\s\S]*?\*\//g, "").matchAll(/([^{}]+)\{([^{}]*)\}/g),
  );

  it("holds exactly one dialog--review rule and it sets only the width", () => {
    const review = rules.filter((rule) => (rule[1] ?? "").includes("dialog--review"));
    expect(review).toHaveLength(1);
    const declarations = (review[0]?.[2] ?? "")
      .split(";")
      .map((line) => line.trim())
      .filter(Boolean);
    expect(declarations).toHaveLength(1);
    expect(declarations[0]).toBe(
      "width: min(var(--utf-review-dialog-width), calc(100vw - var(--utf-dialog-gutter)))",
    );
  });
});

describe("Settings section while applying", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    vi.spyOn(hostingService, "configuration").mockResolvedValue(configuration);
    vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "files").mockResolvedValue([]);
    vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as never);
    vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
  });

  it("is busy, blocks Escape and disables the controls until the request settles", async () => {
    const user = userEvent.setup();
    let finish!: (value: GameConfiguration) => void;
    vi.spyOn(hostingService, "updateConfiguration").mockImplementation(
      () => new Promise((resolve) => (finish = resolve)),
    );
    renderWithProviders(
      <SettingsSection server={{ ...server, canManageSecrets: false } as GameServer} jobs={[]} />,
    );
    const field = await screen.findByLabelText("Maximum players");
    await user.clear(field);
    await user.type(field, "80");
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const dialog = await screen.findByRole("dialog", { name: "Review changes" });

    await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));

    await waitFor(() => expect(dialog).toHaveAttribute("aria-busy", "true"));
    expect(within(dialog).getByRole("button", { name: "Applying changes…" })).toBeVisible();
    const cancelEvent = new Event("cancel", { cancelable: true });
    fireEvent(dialog, cancelEvent);
    expect(cancelEvent.defaultPrevented).toBe(true);
    expect(dialog).toHaveAttribute("open");
    expect(field).toBeDisabled();

    finish(configuration);
    await waitFor(() => expect(dialog).not.toHaveAttribute("open"));
    expect(field).toBeEnabled();
  });
});
