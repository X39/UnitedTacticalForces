import { useQuery } from "@tanstack/react-query";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/api/client";
import i18n, { i18nReady } from "@/app/i18n";
import { SettingsPanelStateProvider } from "@/components/server/useSettingsPanelState";
import { ServerCostOwnerPanel } from "@/features/hosting/ServerCostOwnerPanel";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, ServerCostOwnerOption } from "@/features/hosting/types";
import { fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { clearSettingsPanels, seedSettingsPanels } from "../helpers/settingsPanels";

/* The Cost owner panel of the Settings section: facts, summary, frozen state, the read-only viewer and the transfer (D-01, D-03, D-11, D-12). */

/**
 * Builds a server whose cost owner the viewer may change unless the overrides say otherwise.
 *
 * @param overrides Members that replace the defaults.
 * @returns The server.
 */
function ownerServer(overrides: Record<string, unknown> = {}): GameServer {
  return {
    ...fleetServer({ title: "Alpha Team" }),
    canModify: true,
    ...overrides,
  } as unknown as GameServer;
}

/**
 * Renders the panel inside the Settings panel state it needs.
 *
 * @param server The server whose cost owner is shown.
 * @returns The render result.
 */
function renderPanel(server: GameServer = ownerServer()) {
  return renderWithProviders(
    <SettingsPanelStateProvider>
      <ServerCostOwnerPanel server={server} />
    </SettingsPanelStateProvider>,
  );
}

describe("Cost owner panel facts", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    clearSettingsPanels();
  });

  it("renders no panel for an archived server", () => {
    renderPanel(ownerServer({ isArchived: true }));

    expect(document.querySelector('[data-settings-panel="server-cost-owner"]')).toBeNull();
    expect(screen.queryByRole("heading", { name: "Cost owner" })).not.toBeInTheDocument();
  });

  it("summarizes the panel by the billed-to line", () => {
    renderPanel();

    expect(screen.getByRole("heading", { level: 2, name: "Cost owner" })).toBeVisible();
    expect(screen.getByText("Billed to Operations")).toBeInTheDocument();
  });

  it("says Assigned owner for an owner the viewer may not see", () => {
    renderPanel(ownerServer({ costOwnerName: "Assigned owner" }));

    expect(screen.getAllByText("Assigned owner")).toHaveLength(2);
    expect(screen.queryByText(/^Billed to/)).not.toBeInTheDocument();
  });

  it("says Cost owner unknown for an owner the API could not resolve", () => {
    renderPanel(ownerServer({ costOwnerName: "Unknown owner" }));

    expect(screen.getAllByText("Cost owner unknown")).toHaveLength(2);
  });

  it("shows the owner in a bdi and no Since row without an assignment date", () => {
    renderPanel();

    const facts = document.querySelector<HTMLElement>(".cost-owner-facts");
    expect(facts).not.toBeNull();
    expect(within(facts as HTMLElement).getByText("Cost owner")).toBeInTheDocument();
    expect(facts?.querySelector("bdi")?.textContent).toBe("Operations");
    expect(within(facts as HTMLElement).queryByText("Since")).not.toBeInTheDocument();
  });

  it("shows Since with the locale date when the assignment date is present", () => {
    renderPanel(ownerServer({ costOwnerAssignedAt: "2026-03-14T09:30:00Z" }));

    expect(screen.getByText("Since")).toBeInTheDocument();
    expect(screen.getByText("Mar 14, 2026")).toBeInTheDocument();
  });

  it("gives the owner value programmatic focus for the end of a transfer", () => {
    renderPanel();

    const value = document.querySelector<HTMLElement>(".cost-owner-facts dd");
    expect(value).toHaveAttribute("tabindex", "-1");
  });

  it("shows the frozen warning with the owner name and a Frozen badge in the header", () => {
    renderPanel(ownerServer({ costOwnerIsFrozen: true }));

    expect(
      screen.getByText(
        "Operations is frozen. This server cannot be started until another cost owner takes over.",
      ),
    ).toBeInTheDocument();
    const header = screen.getByRole("heading", { level: 2, name: /Cost owner/ });
    expect(within(header).getByText("Frozen")).toBeInTheDocument();
  });

  it("shows neither the warning nor the badge for an owner that is not frozen", () => {
    renderPanel(ownerServer({ costOwnerIsFrozen: false }));

    expect(screen.queryByText(/is frozen/)).not.toBeInTheDocument();
    expect(screen.queryByText("Frozen")).not.toBeInTheDocument();
  });

  it("shows the read-only sentence without canModify, with no picker and no options request", () => {
    const options = vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue([]);
    renderPanel(ownerServer({ canModify: false }));

    expect(
      screen.getByText("Only members who may change this server can transfer its cost owner."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(options).not.toHaveBeenCalled();
  });
});

/** The owners the picker lists; Falcon is the caller's own account and Frozen Org may not be chosen. */
const ownerList: ServerCostOwnerOption[] = [
  {
    costOwnerId: "c0000000-0000-4000-8000-0000000000a1",
    displayName: "Falcon",
    isFrozen: false,
    isCurrentUser: true,
    kind: "User",
  },
  {
    costOwnerId: "c0000000-0000-4000-8000-0000000000a2",
    displayName: "Verein Alpha",
    isFrozen: false,
    isCurrentUser: false,
    kind: "Verein",
  },
  {
    costOwnerId: "c0000000-0000-4000-8000-0000000000a3",
    displayName: "Frozen Org",
    isFrozen: true,
    isCurrentUser: false,
    kind: "ExternalOrganization",
  },
];

/**
 * Renders the panel the way the Settings section does: with the server read from the exact server query, so a
 * refetch after a transfer hands the panel the new owner.
 *
 * @param props.initial The server of the first render.
 */
function PanelHarness({ initial }: { initial: GameServer }) {
  const server = useQuery({
    queryKey: hostingQueryKeys.server(initial.primaryKey),
    queryFn: ({ signal }) => hostingService.server(initial.primaryKey, signal),
    initialData: initial,
    staleTime: Infinity,
  });
  return (
    <SettingsPanelStateProvider>
      <ServerCostOwnerPanel server={server.data ?? initial} />
    </SettingsPanelStateProvider>
  );
}

/**
 * Builds the deployment an unresolved server carries, so the transfer waits for the cloud machine.
 *
 * @param status The deployment status.
 * @returns The deployment of a fleet fixture with that status.
 */
function unresolvedDeployment(status: string) {
  return fleetServer({ title: "Alpha Team", status }).deployment;
}

/** The picker input of the open panel. */
const picker = () => screen.getByRole("combobox", { name: "New cost owner" });

/** The opening Transfer cost owner button of the panel, not the one in the dialog. */
const panelTransferButton = () => screen.getByRole("button", { name: /^Transfer cost owner/ });

/**
 * Focuses the picker and chooses the option with the given name.
 *
 * @param user The user event session.
 * @param name The display name of the owner to choose.
 */
async function chooseOwner(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.click(picker());
  await user.click(await screen.findByRole("option", { name: new RegExp(name) }));
}

describe("Cost owner transfer", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
    clearSettingsPanels();
    seedSettingsPanels(["server-cost-owner"]);
  });

  it("requests no owners until the picker takes focus, then lists the first owners", async () => {
    const user = userEvent.setup();
    const options = vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    const server = ownerServer();
    renderPanel(server);

    await screen.findByRole("combobox", { name: "New cost owner" });
    expect(options).not.toHaveBeenCalled();

    await user.click(picker());
    expect(await screen.findAllByRole("option")).toHaveLength(3);
    expect(options).toHaveBeenCalledTimes(1);
    expect(options).toHaveBeenCalledWith(server.primaryKey, "", expect.anything());
  });

  it("shows Your account or the kind per owner and marks a frozen owner as not selectable", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    renderPanel();
    await user.click(await screen.findByRole("combobox", { name: "New cost owner" }));

    const falcon = await screen.findByRole("option", { name: /Falcon/ });
    expect(within(falcon).getByText("Your account")).toBeInTheDocument();
    expect(falcon.querySelector("bdi")?.textContent).toBe("Falcon");
    const verein = screen.getByRole("option", { name: /Verein Alpha/ });
    expect(within(verein).getByText("Association account")).toBeInTheDocument();
    const frozen = screen.getByRole("option", { name: /Frozen Org/ });
    expect(frozen).toHaveAttribute("aria-disabled", "true");
    expect(within(frozen).getByText("Frozen, cannot be chosen")).toBeInTheDocument();
  });

  it("searches the typed text once it has rested for 250ms", async () => {
    const user = userEvent.setup();
    const options = vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    const server = ownerServer();
    renderPanel(server);
    await user.click(await screen.findByRole("combobox", { name: "New cost owner" }));
    await screen.findAllByRole("option");

    await user.type(picker(), "ver");

    await waitFor(() =>
      expect(options).toHaveBeenLastCalledWith(server.primaryKey, "ver", expect.anything()),
    );
    expect(options).not.toHaveBeenCalledWith(server.primaryKey, "v", expect.anything());
    expect(options).not.toHaveBeenCalledWith(server.primaryKey, "ve", expect.anything());
  });

  it("reads There is no other cost owner you may use when the empty query returns nothing", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue([]);
    renderPanel();
    await user.click(await screen.findByRole("combobox", { name: "New cost owner" }));

    expect(
      (await screen.findAllByText("There is no other cost owner you may use.")).length,
    ).toBeGreaterThan(0);
    expect(screen.queryByText("The cost owners could not be loaded.")).not.toBeInTheDocument();
  });

  it("tells a failed load apart from an empty one and retries it", async () => {
    const user = userEvent.setup();
    const options = vi
      .spyOn(hostingService, "serverCostOwnerOptions")
      .mockRejectedValueOnce(new ApiError("boom", 500, "server"))
      .mockResolvedValue(ownerList);
    renderPanel();
    await user.click(await screen.findByRole("combobox", { name: "New cost owner" }));

    expect(await screen.findByText("The cost owners could not be loaded.")).toBeVisible();
    expect(screen.queryByText("There is no other cost owner you may use.")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findAllByRole("option")).toHaveLength(3);
    expect(options).toHaveBeenCalledTimes(2);
  });

  it("explains an unavailable transfer by priority: running, then jobs, then nothing chosen", async () => {
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    const { unmount } = renderPanel(
      ownerServer({ deployment: unresolvedDeployment("Running"), pendingJobCount: 2 }),
    );
    expect(panelTransferButton()).toHaveAttribute("aria-disabled", "true");
    expect(
      screen.getByText(/^Stop the server first\. The cost owner can only change/),
    ).toBeVisible();
    expect(screen.queryByText(/^Wait until the pending jobs/)).not.toBeInTheDocument();
    unmount();

    const jobs = renderPanel(ownerServer({ pendingJobCount: 2 }));
    expect(
      screen.getByText("Wait until the pending jobs of this server have finished."),
    ).toBeVisible();
    jobs.unmount();

    renderPanel(ownerServer());
    expect(panelTransferButton()).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByText("Choose the new cost owner first.")).toBeVisible();
  });

  it("keeps the button unavailable for a server that still has a stopped deployment", async () => {
    renderPanel(ownerServer({ deployment: unresolvedDeployment("Terminated") }));

    expect(panelTransferButton()).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByText(/^Stop the server first\./)).toBeVisible();
  });

  it("opens a confirmation that names the new owner, the server and the old owner", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    renderPanel();
    await chooseOwner(user, "Falcon");
    expect(panelTransferButton()).not.toHaveAttribute("aria-disabled");

    await user.click(panelTransferButton());

    const dialog = await screen.findByRole("dialog", { name: "Transfer cost owner?" });
    expect(
      within(dialog).getByText(
        "From now on, Falcon pays for every start of Alpha Team, not Operations.",
      ),
    ).toBeVisible();
    const items = within(dialog).getAllByRole("listitem");
    expect(items.map((item) => item.textContent)).toEqual([
      "Costs already incurred stay with Operations.",
      "Cost alerts for this server follow Falcon.",
    ]);
    const dismiss = within(dialog).getByRole("button", { name: "Don't transfer" });
    await waitFor(() => expect(dismiss).toHaveFocus());
    expect(within(dialog).getByRole("button", { name: "Transfer cost owner" }).className).toContain(
      "button--gold",
    );
    expect(dismiss.className).toContain("button--primary");
  });

  it("reads Assigned owner for a hidden old owner in the confirmation", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    renderPanel(ownerServer({ costOwnerName: "Assigned owner" }));
    await chooseOwner(user, "Falcon");
    await user.click(panelTransferButton());

    const dialog = await screen.findByRole("dialog", { name: "Transfer cost owner?" });
    expect(
      within(dialog).getByText(
        "From now on, Falcon pays for every start of Alpha Team, not Assigned owner.",
      ),
    ).toBeVisible();
  });

  it("sends nothing and closes when the member does not transfer", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    const change = vi.spyOn(hostingService, "changeServerCostOwner").mockResolvedValue(undefined);
    renderPanel();
    await chooseOwner(user, "Falcon");
    await user.click(panelTransferButton());
    await user.click(await screen.findByRole("button", { name: "Don't transfer" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(change).not.toHaveBeenCalled();
  });

  it("transfers once, closes, clears the picker, toasts and focuses the new owner", async () => {
    const user = userEvent.setup();
    const server = ownerServer();
    let current = server;
    vi.spyOn(hostingService, "server").mockImplementation(async () => current);
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    const change = vi
      .spyOn(hostingService, "changeServerCostOwner")
      .mockImplementation(async () => {
        current = {
          ...server,
          costOwnerId: ownerList[0]!.costOwnerId,
          costOwnerName: "Falcon",
        } as GameServer;
      });
    renderWithProviders(<PanelHarness initial={server} />);
    await chooseOwner(user, "Falcon");
    await user.click(panelTransferButton());
    const dialog = await screen.findByRole("dialog", { name: "Transfer cost owner?" });

    await user.click(within(dialog).getByRole("button", { name: "Transfer cost owner" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(change).toHaveBeenCalledTimes(1);
    expect(change).toHaveBeenCalledWith(server.primaryKey, ownerList[0]!.costOwnerId);
    expect(await screen.findByText("Falcon now pays for Alpha Team.")).toBeInTheDocument();
    expect(screen.getByText("Billed to Falcon")).toBeInTheDocument();
    // The picker is back to its input, empty, and the button asks for a new choice.
    expect(picker()).toHaveValue("");
    expect(screen.getByText("Choose the new cost owner first.")).toBeVisible();
    await waitFor(() => {
      const value = document.querySelector<HTMLElement>(".cost-owner-facts dd");
      expect(value).toHaveFocus();
      expect(value?.textContent).toBe("Falcon");
    });
  });

  it("blocks dismissal while the transfer is pending and shows the busy label", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    const change = vi
      .spyOn(hostingService, "changeServerCostOwner")
      .mockReturnValue(new Promise(() => undefined));
    renderPanel();
    await chooseOwner(user, "Falcon");
    await user.click(panelTransferButton());
    const dialog = await screen.findByRole("dialog", { name: "Transfer cost owner?" });
    await user.click(within(dialog).getByRole("button", { name: "Transfer cost owner" }));

    const busy = await within(dialog).findByRole("button", { name: "Transferring…" });
    expect(busy).toHaveAttribute("aria-disabled", "true");
    await user.click(busy);
    await user.keyboard("{Escape}");
    expect(screen.getByRole("dialog", { name: "Transfer cost owner?" })).toBeInTheDocument();
    expect(change).toHaveBeenCalledTimes(1);
  });

  it.each([
    [409, "The server has a cloud machine that is still running or being removed."],
    [400, "That cost owner is frozen or no longer available. Choose another one."],
    [403, "You may not use that cost owner."],
    [500, "The cost owner was not changed. Try again."],
  ])("keeps the dialog open with the message of HTTP %i", async (status, message) => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    vi.spyOn(hostingService, "changeServerCostOwner").mockRejectedValue(
      new ApiError("nope", status, "server"),
    );
    renderPanel();
    await chooseOwner(user, "Falcon");
    await user.click(panelTransferButton());
    const dialog = await screen.findByRole("dialog", { name: "Transfer cost owner?" });
    await user.click(within(dialog).getByRole("button", { name: "Transfer cost owner" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(message);
    expect(screen.getByRole("dialog", { name: "Transfer cost owner?" })).toBeInTheDocument();
    expect(screen.queryByText(/now pays for/)).not.toBeInTheDocument();
  });

  it("keeps the dialog open with the generic message for a network failure", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverCostOwnerOptions").mockResolvedValue(ownerList);
    vi.spyOn(hostingService, "changeServerCostOwner").mockRejectedValue(new TypeError("offline"));
    renderPanel();
    await chooseOwner(user, "Falcon");
    await user.click(panelTransferButton());
    const dialog = await screen.findByRole("dialog", { name: "Transfer cost owner?" });
    await user.click(within(dialog).getByRole("button", { name: "Transfer cost owner" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "The cost owner was not changed. Try again.",
    );
  });
});
