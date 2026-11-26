import { act, fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingService } from "@/features/hosting/services";
import type { GameConfiguration, GameServer } from "@/features/hosting/types";
import { configurationEntry } from "../helpers/configurationFixtures";
import { fleetServer, serverAutomation } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/* The Settings leave guard: every path out of Settings asks first while changes are staged (CHG-05, D-10). */

const auth = vi.hoisted(() => ({
  claims: [{ identifier: "sv:view" }] as { identifier: string }[],
}));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-4000-8000-000000000001",
      nickname: "Falcon One",
      isBanned: false,
      isDeleted: false,
      isVereinMember: true,
      claims: auth.claims.map((claim, index) => ({
        primaryKey: index + 1,
        identifier: claim.identifier,
        value: null,
        title: claim.identifier,
        category: "Hosting",
        description: "",
        isPrefix: false,
        valueType: null,
      })),
      roles: [],
    } as unknown as FullUser,
    isAuthenticated: true,
    isLoading: false,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

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
const configuration = {
  serverId: "server",
  profileId: null,
  profileRevisionId: null,
  profileRevisionNumber: null,
  isComplete: true,
  entries: [maxPlayers],
} as unknown as GameConfiguration;

const target = {
  ...fleetServer({ title: "Alpha Team", gameIdentifier: "arma3" }),
  selectedModPackDefinitionId: null,
  selectedModPackDefinitionTitle: null,
  selectedConfigurationProfileId: null,
  selectedConfigurationProfileTitle: null,
  canModify: true,
  canManageSecrets: false,
} as unknown as GameServer;
const serverPath = `/command/servers/${target.primaryKey}`;

/** The same server with an automation block the viewer may manage. */
const automationTarget = {
  ...target,
  automation: serverAutomation(),
  canManageAutomation: true,
} as unknown as GameServer;

function setup(search = "?tab=settings", server: GameServer = target) {
  vi.spyOn(hostingService, "server").mockResolvedValue(server);
  vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
  vi.spyOn(hostingService, "operations").mockResolvedValue([]);
  vi.spyOn(hostingService, "files").mockResolvedValue([]);
  vi.spyOn(hostingService, "preservationSelections").mockResolvedValue([]);
  vi.spyOn(hostingService, "configuration").mockResolvedValue(configuration);
  vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
  vi.spyOn(hostingService, "storageDemand").mockResolvedValue({} as never);
  vi.spyOn(hostingService, "secrets").mockResolvedValue([]);
  return renderWithProviders(<ServerWorkspacePage />, {
    path: "/command/servers/:serverId",
    route: `${serverPath}${search}`,
  });
}

/** Stages 80 for the maximum players setting and waits for the pending bar. */
async function stageChange(user: ReturnType<typeof userEvent.setup>) {
  const field = await screen.findByLabelText("Maximum players");
  await user.clear(field);
  await user.type(field, "80");
  await screen.findByRole("region", { name: "Pending changes" });
}

/** Turns on the maximum runtime limit in the automation panel, which makes its draft dirty. */
async function editAutomation(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("switch", { name: "Limit the runtime" }));
  expect(await screen.findByText("You have unsaved changes.")).toBeVisible();
}

/** Returns the link of a workspace section from the section navigation. */
function sectionLink(name: string): HTMLElement {
  return within(screen.getByRole("navigation", { name: "Server sections" })).getByRole("link", {
    name,
  });
}

/** Dispatches a cancelable beforeunload event and reports whether a handler prevented it. */
function unloadPrevented(): boolean {
  const event = new Event("beforeunload", { cancelable: true });
  window.dispatchEvent(event);
  return event.defaultPrevented;
}

describe("Settings leave guard", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("asks before a section link leaves Settings, focusing Stay here and keeping the URL", async () => {
    const user = userEvent.setup();
    const { router } = setup();
    await stageChange(user);

    await user.click(sectionLink("Files"));

    const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
    expect(
      within(dialog).getByText(
        "You have 1 staged change that is not applied. Leaving discards it.",
      ),
    ).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Stay here" })).toHaveFocus();
    expect(router.state.location.search).toBe("?tab=settings");
  });

  it("asks for the breadcrumb and the overview link as well", async () => {
    const user = userEvent.setup();
    setup();
    await stageChange(user);

    await user.click(sectionLink("Overview"));
    expect(await screen.findByRole("dialog", { name: "Leave without applying?" })).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Stay here" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());

    const breadcrumb = screen.getByRole("navigation", { name: "Breadcrumb" });
    await user.click(within(breadcrumb).getByRole("link", { name: "Servers" }));
    expect(await screen.findByRole("dialog", { name: "Leave without applying?" })).toBeVisible();
  });

  it("keeps the draft after Stay here and after Escape", async () => {
    const user = userEvent.setup();
    setup();
    await stageChange(user);

    await user.click(sectionLink("Files"));
    await screen.findByRole("dialog", { name: "Leave without applying?" });
    await user.click(screen.getByRole("button", { name: "Stay here" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByText(/1 change staged/)).toBeVisible();
    expect(screen.getByLabelText("Maximum players")).toHaveValue(80);

    await user.click(sectionLink("Files"));
    const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
    // Escape reaches the native dialog as a cancelable cancel event, which jsdom does not synthesize from keys.
    act(() => {
      dialog.dispatchEvent(new Event("cancel", { cancelable: true }));
    });
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByText(/1 change staged/)).toBeVisible();
  });

  it("clears the draft and navigates on Discard and leave", async () => {
    const user = userEvent.setup();
    const { router } = setup();
    await stageChange(user);

    await user.click(sectionLink("Files"));
    const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
    await user.click(within(dialog).getByRole("button", { name: "Discard and leave" }));

    await waitFor(() => expect(router.state.location.search).toBe("?tab=files"));
    await user.click(sectionLink("Settings"));
    expect(await screen.findByLabelText("Maximum players")).toHaveValue(64);
    expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
  });

  it("stays and opens the review dialog on Review changes", async () => {
    const user = userEvent.setup();
    const { router } = setup();
    await stageChange(user);

    await user.click(sectionLink("Files"));
    const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
    await user.click(within(dialog).getByRole("button", { name: "Review changes" }));

    expect(await screen.findByRole("dialog", { name: "Review changes" })).toBeVisible();
    expect(screen.queryByRole("dialog", { name: "Leave without applying?" })).toBeNull();
    expect(router.state.location.search).toBe("?tab=settings");
  });

  it("holds a Back navigation and leaves the history unchanged on Stay here", async () => {
    const user = userEvent.setup();
    const { router } = setup("?tab=overview");
    await act(async () => {
      await router.navigate(`${serverPath}?tab=settings`);
    });
    await stageChange(user);
    const before = router.state.location.key;

    await act(async () => {
      await router.navigate(-1);
    });
    const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
    expect(router.state.location.search).toBe("?tab=settings");
    await user.click(within(dialog).getByRole("button", { name: "Stay here" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(router.state.location.key).toBe(before);
    expect(router.state.location.search).toBe("?tab=settings");
    expect(screen.getByText(/1 change staged/)).toBeVisible();
  });

  it("never asks for a replace or hash change that stays on Settings", async () => {
    const user = userEvent.setup();
    const { router } = setup();
    await stageChange(user);

    await act(async () => {
      await router.navigate(`${serverPath}?tab=settings&action=x`, { replace: true });
    });
    await act(async () => {
      await router.navigate(`${serverPath}?tab=settings#notes`);
    });

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(router.state.location.hash).toBe("#notes");
    expect(screen.getByLabelText("Maximum players")).toHaveValue(80);
  });

  it("blocks nothing without staged changes", async () => {
    const user = userEvent.setup();
    const { router } = setup();
    await screen.findByLabelText("Maximum players");

    await user.click(sectionLink("Files"));

    await waitFor(() => expect(router.state.location.search).toBe("?tab=files"));
    expect(screen.queryByRole("dialog", { name: "Leave without applying?" })).toBeNull();
    expect(unloadPrevented()).toBe(false);
  });

  it("prompts on tab close only while staged, never after Discard and leave or an apply", async () => {
    const user = userEvent.setup();
    const confirm = vi.spyOn(window, "confirm");
    setup();
    expect(unloadPrevented()).toBe(false);
    await stageChange(user);
    expect(unloadPrevented()).toBe(true);

    await user.click(sectionLink("Files"));
    const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
    await user.click(within(dialog).getByRole("button", { name: "Discard and leave" }));
    await waitFor(() => expect(unloadPrevented()).toBe(false));

    vi.spyOn(hostingService, "updateConfiguration").mockResolvedValue(configuration);
    await user.click(sectionLink("Settings"));
    await stageChange(user);
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const review = await screen.findByRole("dialog", { name: "Review changes" });
    await user.click(within(review).getByRole("button", { name: "Apply 1 change" }));
    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
    );
    expect(unloadPrevented()).toBe(false);
    expect(confirm).not.toHaveBeenCalled();
  });

  it("still asks with the same outcomes while an apply is running", async () => {
    const user = userEvent.setup();
    let finish: (value: GameConfiguration) => void = () => undefined;
    vi.spyOn(hostingService, "updateConfiguration").mockReturnValue(
      new Promise<GameConfiguration>((resolve) => {
        finish = resolve;
      }),
    );
    const { router } = setup();
    await stageChange(user);
    await user.click(screen.getByRole("button", { name: "Review changes" }));
    const review = await screen.findByRole("dialog", { name: "Review changes" });
    await user.click(within(review).getByRole("button", { name: "Apply 1 change" }));
    await waitFor(() => expect(hostingService.updateConfiguration).toHaveBeenCalledTimes(1));

    await act(async () => {
      await router.navigate(`${serverPath}?tab=files`);
    });
    const leave = await screen.findByRole("dialog", { name: "Leave without applying?" });
    expect(within(leave).getByRole("button", { name: "Stay here" })).toBeVisible();
    expect(within(leave).getByRole("button", { name: "Discard and leave" })).toBeVisible();
    expect(within(leave).getByRole("button", { name: "Review changes" })).toBeVisible();
    await user.click(within(leave).getByRole("button", { name: "Stay here" }));
    expect(router.state.location.search).toBe("?tab=settings");

    await act(async () => {
      finish(configuration);
    });
    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument(),
    );
  });
  describe("leave dialog layout and backdrop (D-13)", () => {
    /** Fires a pointer event with coordinates; jsdom reports an all-zero rect, so 500/500 lies outside it. */
    function pointer(type: "pointerdown" | "click", target: Element, clientX = 500, clientY = 500) {
      fireEvent(
        target,
        new MouseEvent(type, { bubbles: true, cancelable: true, clientX, clientY }),
      );
    }

    it("orders Discard and leave, Stay here and Review changes with the right variants", async () => {
      const user = userEvent.setup();
      setup();
      await stageChange(user);

      await user.click(sectionLink("Files"));
      const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });

      const buttons = within(dialog).getAllByRole("button");
      expect(buttons.map((button) => button.textContent)).toEqual([
        "Discard and leave",
        "Stay here",
        "Review changes",
      ]);
      expect(buttons[0]).toHaveClass("button--danger");
      expect(buttons[1]).toHaveClass("button--primary");
      expect(buttons[2]).toHaveClass("button--gold");
      for (const button of buttons) expect(button).not.toHaveClass("button--ghost");
      expect(buttons[1]).toHaveFocus();
    });

    it("offers two buttons and no Review while only the automation draft is dirty", async () => {
      const user = userEvent.setup();
      setup("?tab=settings", automationTarget);
      await editAutomation(user);

      await user.click(sectionLink("Files"));
      const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });

      expect(
        within(dialog)
          .getAllByRole("button")
          .map((button) => button.textContent),
      ).toEqual(["Discard and leave", "Stay here"]);
    });

    it("treats a backdrop click as Stay here and keeps everything staged", async () => {
      const user = userEvent.setup();
      const { router } = setup();
      await stageChange(user);
      await user.click(sectionLink("Files"));
      const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });

      pointer("pointerdown", dialog);
      pointer("click", dialog);

      await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
      expect(router.state.location.search).toBe("?tab=settings");
      expect(screen.getByText(/1 change staged/)).toBeVisible();
      expect(screen.getByLabelText("Maximum players")).toHaveValue(80);
    });

    it("closes the discard dialog as Keep editing on a backdrop click and keeps the draft", async () => {
      const user = userEvent.setup();
      setup();
      await stageChange(user);
      const bar = screen.getByRole("region", { name: "Pending changes" });
      await user.click(within(bar).getByRole("button", { name: "Discard changes" }));
      const dialog = await screen.findByRole("dialog", { name: "Discard staged changes?" });

      pointer("pointerdown", dialog);
      pointer("click", dialog);

      await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
      expect(screen.getByText(/1 change staged/)).toBeVisible();
      expect(screen.getByLabelText("Maximum players")).toHaveValue(80);
    });

    it("keeps the delete server confirmation open on a backdrop click", async () => {
      const user = userEvent.setup();
      // The location hash opens the Server record panel, which holds the delete confirmation.
      setup("?tab=settings#server-record");
      await user.click(await screen.findByRole("button", { name: "Delete server" }));
      const dialog = await screen.findByRole("dialog", { name: "Delete server" });

      pointer("pointerdown", dialog);
      pointer("click", dialog);

      expect(screen.getByRole("dialog", { name: "Delete server" })).toBeVisible();
    });

    it("ignores a click inside the leave dialog box", async () => {
      const user = userEvent.setup();
      setup();
      await stageChange(user);
      await user.click(sectionLink("Files"));
      const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
      const description = within(dialog).getByText(/staged change that is not applied/);

      pointer("pointerdown", description);
      pointer("click", description);

      expect(screen.getByRole("dialog", { name: "Leave without applying?" })).toBeVisible();
    });
  });

  describe("with unsaved automation settings", () => {
    it("asks with the automation sentence, offers no Review and focuses Stay here", async () => {
      const user = userEvent.setup();
      const { router } = setup("?tab=settings", automationTarget);
      await editAutomation(user);

      await user.click(sectionLink("Files"));

      const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
      expect(
        within(dialog).getByText("Your changes to the automation settings are not saved."),
      ).toBeVisible();
      expect(within(dialog).queryByRole("button", { name: "Review changes" })).toBeNull();
      expect(within(dialog).getByRole("button", { name: "Stay here" })).toHaveFocus();
      expect(router.state.location.search).toBe("?tab=settings");

      await user.click(within(dialog).getByRole("button", { name: "Stay here" }));
      await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
      expect(screen.getByRole("switch", { name: "Limit the runtime" })).toBeChecked();
    });

    it("discards the automation draft on Discard and leave and stops prompting", async () => {
      const user = userEvent.setup();
      const { router } = setup("?tab=settings", automationTarget);
      await editAutomation(user);
      expect(unloadPrevented()).toBe(true);

      await user.click(sectionLink("Files"));
      const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
      await user.click(within(dialog).getByRole("button", { name: "Discard and leave" }));

      await waitFor(() => expect(router.state.location.search).toBe("?tab=files"));
      await waitFor(() => expect(unloadPrevented()).toBe(false));
      await user.click(sectionLink("Settings"));
      expect(await screen.findByRole("switch", { name: "Limit the runtime" })).not.toBeChecked();
    });

    it("uses one dialog with the combined sentence and Review when both are dirty", async () => {
      const user = userEvent.setup();
      const { router } = setup("?tab=settings", automationTarget);
      await stageChange(user);
      await editAutomation(user);

      await user.click(sectionLink("Files"));

      const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
      expect(screen.getAllByRole("dialog")).toHaveLength(1);
      expect(
        within(dialog).getByText(
          "You have staged server changes and unsaved automation settings. Leaving discards both.",
        ),
      ).toBeVisible();
      expect(within(dialog).getByRole("button", { name: "Review changes" })).toBeVisible();
      expect(within(dialog).getByRole("button", { name: "Stay here" })).toHaveFocus();

      await user.click(within(dialog).getByRole("button", { name: "Review changes" }));
      expect(await screen.findByRole("dialog", { name: "Review changes" })).toBeVisible();
      expect(router.state.location.search).toBe("?tab=settings");
    });

    it("discards both drafts on Discard and leave", async () => {
      const user = userEvent.setup();
      const { router } = setup("?tab=settings", automationTarget);
      await stageChange(user);
      await editAutomation(user);

      await user.click(sectionLink("Files"));
      const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
      await user.click(within(dialog).getByRole("button", { name: "Discard and leave" }));

      await waitFor(() => expect(router.state.location.search).toBe("?tab=files"));
      await user.click(sectionLink("Settings"));
      expect(await screen.findByLabelText("Maximum players")).toHaveValue(64);
      expect(screen.getByRole("switch", { name: "Limit the runtime" })).not.toBeChecked();
      expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
    });

    it("never asks without any dirty draft", async () => {
      const user = userEvent.setup();
      const { router } = setup("?tab=settings", automationTarget);
      await screen.findByRole("switch", { name: "Limit the runtime" });

      await user.click(sectionLink("Files"));

      await waitFor(() => expect(router.state.location.search).toBe("?tab=files"));
      expect(screen.queryByRole("dialog", { name: "Leave without applying?" })).toBeNull();
    });
  });
});
