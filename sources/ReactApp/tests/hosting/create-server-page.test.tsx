import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import { ApiError } from "@/api/client";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { CreateServerPage } from "@/features/hosting/CreateServerPage";
import { hostingService } from "@/features/hosting/services";
import { formatHourlyRate, formatMoney } from "@/features/hosting/model";
import type { GameServer } from "@/features/hosting/types";
import {
  counterStrikeCapability,
  createCapability,
  createContentOptions,
  createOwners,
  createProfile,
  createProvider,
  createStorageDemand,
} from "../helpers/createFixtures";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isLoading: false,
    isAuthenticated: Boolean(authState.user),
  }),
}));

const creator: FullUser = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Creator",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  claims: [{ identifier: "crt:sv", value: null }],
  roles: [],
} as FullUser;

const createdServer = {
  primaryKey: "10000000-0000-4000-8000-000000000099",
  title: "UTF One",
  actions: [],
  deployment: null,
} as unknown as GameServer;

/** Renders the page and a plain workspace element on the workspace route, as the app shell does. */
function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: "/command/servers/create", element: <CreateServerPage /> },
      { path: "/command/servers", element: <div>Server list</div> },
      { path: "/command/servers/:serverId", element: <div>Server workspace</div> },
    ],
    { initialEntries: ["/command/servers/create"] },
  );
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
  return { router, queryClient };
}

/**
 * Waits until the submit button shows no blocking reason and returns it.
 *
 * @param name The accessible name of the submit button.
 * @returns The ready submit button.
 */
async function readySubmit(name = "Create server and keep off"): Promise<HTMLElement> {
  return waitFor(() => {
    const button = screen.getByRole("button", { name });
    expect(button).not.toHaveAttribute("aria-disabled");
    return button;
  });
}

describe("guided create page", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = creator;
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([createCapability()]);
    vi.spyOn(hostingService, "creationModPacks").mockResolvedValue([]);
    vi.spyOn(hostingService, "profileOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "terrains").mockResolvedValue([]);
    vi.spyOn(hostingService, "scenarios").mockResolvedValue([]);
    vi.spyOn(hostingService, "estimateStorage").mockResolvedValue(createStorageDemand());
    vi.spyOn(hostingService, "creationCostOwners").mockResolvedValue(createOwners());
    vi.spyOn(hostingService, "create").mockResolvedValue(createdServer);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await i18n.changeLanguage("en");
  });

  it("enforces the create-server claim before loading any option catalog", async () => {
    authState.user = undefined;
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Server creation access required" }),
    ).toBeVisible();
    expect(hostingService.capabilities).not.toHaveBeenCalled();
    expect(hostingService.creationCostOwners).not.toHaveBeenCalled();
  });

  it("renders the authorization state from the shared German resources", async () => {
    authState.user = undefined;
    await i18n.changeLanguage("de");
    renderPage();

    expect(
      await screen.findByRole("heading", {
        name: "Berechtigung zur Servererstellung erforderlich",
      }),
    ).toBeVisible();
  });

  it("shows the breadcrumbs and the heading while the capabilities load", async () => {
    vi.mocked(hostingService.capabilities).mockReturnValue(new Promise(() => undefined));
    renderPage();

    expect(await screen.findByText("Loading game capabilities…")).toBeVisible();
    expect(screen.getByRole("navigation", { name: "Breadcrumb" })).toBeVisible();
    expect(screen.getByRole("heading", { level: 1, name: "Create server" })).toBeVisible();
  });

  it("offers to try again when the capabilities fail to load", async () => {
    vi.mocked(hostingService.capabilities).mockRejectedValueOnce(
      new ApiError("provider token secret", 500, "server"),
    );
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByText("Server creation options could not be loaded.")).toBeVisible();
    expect(screen.queryByText("provider token secret")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1, name: "Create server" })).toBeVisible();

    await user.click(screen.getByRole("button", { name: "Try again" }));

    expect(await screen.findByLabelText(/^Server name/)).toBeVisible();
    expect(hostingService.capabilities).toHaveBeenCalledTimes(2);
  });

  it("shows the empty state when no game can be created", async () => {
    vi.mocked(hostingService.capabilities).mockResolvedValue([]);
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "No hosting capability available" }),
    ).toBeVisible();
  });

  it("composes the guided default view in the documented order", async () => {
    const { router } = renderPage();

    const name = await screen.findByLabelText(/^Server name/);
    const nav = screen.getByRole("navigation", { name: "Breadcrumb" });
    expect(within(nav).getByRole("link", { name: "Servers" })).toHaveAttribute(
      "href",
      "/command/servers",
    );
    expect(within(nav).getByText("Create server")).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("heading", { level: 2, name: "Your server" })).toBeVisible();
    expect(screen.getByText("Arma 3")).toBeVisible();
    expect(screen.queryByLabelText("Game")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2, name: "After creating" })).toBeVisible();
    expect(screen.getByRole("radio", { name: /^Keep it off/ })).toBeChecked();
    expect(screen.getByRole("link", { name: "Leave without creating" })).toHaveAttribute(
      "href",
      "/command/servers",
    );
    const summary = screen.getByRole("heading", { level: 2, name: "Estimated cost" });
    expect(summary).toBeVisible();
    await waitFor(() => expect(screen.getByLabelText(/^Size/)).toHaveValue("medium"));
    expect(await screen.findByText("Billed to Personal budget")).toBeVisible();
    expect(await screen.findByText(/per hour while it runs/)).toBeVisible();

    const form = name.closest("form") as HTMLFormElement;
    const last = form.lastElementChild as HTMLElement;
    expect(last).toContainElement(summary);
    expect(router.state.location.pathname).toBe("/command/servers/create");
  });

  it("creates with only a name and keeps the server off", async () => {
    const user = userEvent.setup();
    const { router } = renderPage();

    await user.type(await screen.findByLabelText(/^Server name/), "  UTF One  ");
    await user.click(await readySubmit());

    await waitFor(() => expect(hostingService.create).toHaveBeenCalledTimes(1));
    expect(hostingService.create).toHaveBeenCalledWith({
      title: "UTF One",
      gameIdentifier: "arma3",
      runtimeProviderIdentifier: "fake-cloud",
      runtimeProfileIdentifier: "medium",
      runtimeStorageStrategy: "AutomaticExternal",
      externalStorageSizeGiB: null,
      modPackDefinitionId: null,
      terrainId: null,
      scenarioId: null,
      configurationProfileId: null,
      costOwnerId: null,
      isLaunchDisabled: false,
      configurationProfileRevisionId: null,
    });
    expect(await screen.findByText("UTF One was created and is off.")).toBeVisible();
    expect(await screen.findByText("Server workspace")).toBeVisible();
    expect(router.state.location.pathname).toBe(`/command/servers/${createdServer.primaryKey}`);
    expect(router.state.location.state).toBeNull();
    expect(window.confirm).not.toHaveBeenCalled();
  });

  it("selects the cheapest priced size by default", async () => {
    vi.mocked(hostingService.capabilities).mockResolvedValue([
      createCapability({
        runtimeProviderProfiles: [
          createProvider("fake-cloud", {
            profiles: [
              createProfile("large", { estimatedHourlyCostEur: 0.4 }),
              createProfile("small", { estimatedHourlyCostEur: 0.05 }),
            ],
          }),
        ],
      }),
    ]);
    renderPage();

    await waitFor(() => expect(screen.getByLabelText(/^Size/)).toHaveValue("small"));
  });

  it("keeps the size the member chose", async () => {
    vi.mocked(hostingService.capabilities).mockResolvedValue([
      createCapability({
        runtimeProviderProfiles: [
          createProvider("fake-cloud", {
            profiles: [
              createProfile("large", { estimatedHourlyCostEur: 0.4 }),
              createProfile("small", { estimatedHourlyCostEur: 0.05 }),
            ],
          }),
        ],
      }),
    ]);
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => expect(screen.getByLabelText(/^Size/)).toHaveValue("small"));
    await user.selectOptions(screen.getByLabelText(/^Size/), "large");
    await user.type(screen.getByLabelText(/^Server name/), "Big one");
    await user.click(await readySubmit());

    await waitFor(() =>
      expect(hostingService.create).toHaveBeenCalledWith(
        expect.objectContaining({ runtimeProfileIdentifier: "large" }),
      ),
    );
  });

  it("resets provider and size to the new game's defaults when the game changes", async () => {
    vi.mocked(hostingService.capabilities).mockResolvedValue([
      createCapability(),
      createCapability({
        gameIdentifier: "other-game",
        displayName: "Other game",
        runtimeProviderProfiles: [
          createProvider("second-cloud", {
            profiles: [createProfile("tiny", { estimatedHourlyCostEur: 0.02 })],
          }),
        ],
      }),
    ]);
    const user = userEvent.setup();
    renderPage();

    const game = await screen.findByLabelText("Game");
    await waitFor(() => expect(screen.getByLabelText(/^Size/)).toHaveValue("medium"));
    await user.selectOptions(game, "other-game");

    await waitFor(() => expect(screen.getByLabelText(/^Size/)).toHaveValue("tiny"));
    await user.type(screen.getByLabelText(/^Server name/), "Second");
    await user.click(await readySubmit());
    await waitFor(() =>
      expect(hostingService.create).toHaveBeenCalledWith(
        expect.objectContaining({
          gameIdentifier: "other-game",
          runtimeProviderIdentifier: "second-cloud",
          runtimeProfileIdentifier: "tiny",
        }),
      ),
    );
  });

  it("makes no request for an empty name and marks the field invalid", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByLabelText(/^Server name/);
    await user.click(await readySubmit());

    const name = screen.getByLabelText(/^Server name/);
    await waitFor(() => expect(name).toBeInvalid());
    expect(screen.getByText("Enter a server name.")).toBeVisible();
    expect(hostingService.create).not.toHaveBeenCalled();
  });

  it("shows a fixed error for a rejected create and stays on the page", async () => {
    vi.mocked(hostingService.create).mockRejectedValue(
      new ApiError("provider token secret", 500, "server"),
    );
    const user = userEvent.setup();
    const { router } = renderPage();

    await user.type(await screen.findByLabelText(/^Server name/), "UTF One");
    await user.click(await readySubmit());

    expect(
      await screen.findByText(
        "The server could not be created. Nothing was started and nothing costs. Check your entries and try again.",
      ),
    ).toBeVisible();
    expect(screen.queryByText("provider token secret")).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/command/servers/create");
  });

  describe("advanced options", () => {
    const profileId = "10000000-0000-4000-8000-000000000020";
    const sharedOwnerId = "10000000-0000-4000-8000-000000000011";

    /** Opens the Advanced options disclosure through its toggle button. */
    async function openAdvanced(user: ReturnType<typeof userEvent.setup>) {
      await user.click(await screen.findByRole("button", { name: "Advanced options" }));
      await waitFor(() =>
        expect(screen.getByRole("button", { name: "Advanced options" })).toHaveAttribute(
          "aria-expanded",
          "true",
        ),
      );
    }

    it("keeps Advanced options collapsed and summarizes the effective choices", async () => {
      renderPage();

      await screen.findByLabelText(/^Server name/);
      const disclosure = document.querySelector(".disclosure.create-advanced") as HTMLElement;
      expect(screen.getByRole("button", { name: "Advanced options" })).toHaveAttribute(
        "aria-expanded",
        "false",
      );
      await waitFor(() => {
        const line = disclosure.querySelector(".disclosure__summary-line");
        expect(line).toHaveTextContent("Storage: Automatically sized external storage");
        expect(line).toHaveTextContent("Mod pack: No mod pack");
        expect(line).toHaveTextContent("Configuration: Game defaults");
      });
    });

    it("sends every Advanced choice with the create request", async () => {
      vi.mocked(hostingService.creationModPacks).mockResolvedValue([
        { primaryKey: 42, title: "Operations" },
      ]);
      vi.mocked(hostingService.profileOptions).mockResolvedValue([
        {
          primaryKey: profileId,
          title: "Public operation",
          gameIdentifier: "arma3",
          isArchived: false,
          latestRevisionNumber: 3,
        },
      ]);
      const user = userEvent.setup();
      renderPage();

      await user.type(await screen.findByLabelText(/^Server name/), "Full");
      await openAdvanced(user);
      await within(screen.getByLabelText("Mod pack")).findByRole("option", { name: "Operations" });
      await user.selectOptions(screen.getByLabelText("Mod pack"), "42");
      await within(screen.getByLabelText("Configuration profile")).findByRole("option", {
        name: "Public operation (revision 3)",
      });
      await user.selectOptions(screen.getByLabelText("Configuration profile"), profileId);
      await user.selectOptions(screen.getByLabelText("Storage"), "FixedExternal");
      const capacity = await screen.findByLabelText(/^External capacity/);
      await user.clear(capacity);
      await user.type(capacity, "80");
      await user.selectOptions(screen.getByLabelText("Cost owner"), sharedOwnerId);
      await user.click(screen.getByLabelText(/^Keep launch disabled/));
      await user.click(await readySubmit());

      await waitFor(() => expect(hostingService.create).toHaveBeenCalledTimes(1));
      expect(hostingService.create).toHaveBeenCalledWith({
        title: "Full",
        gameIdentifier: "arma3",
        runtimeProviderIdentifier: "fake-cloud",
        runtimeProfileIdentifier: "medium",
        runtimeStorageStrategy: "FixedExternal",
        externalStorageSizeGiB: 80,
        modPackDefinitionId: 42,
        terrainId: null,
        scenarioId: null,
        configurationProfileId: profileId,
        costOwnerId: sharedOwnerId,
        isLaunchDisabled: true,
        configurationProfileRevisionId: null,
      });
    });

    it("forces Keep it off while launching is disabled and does not undo it afterwards", async () => {
      const user = userEvent.setup();
      renderPage();

      await screen.findByLabelText(/^Server name/);
      await user.click(screen.getByRole("radio", { name: /^Start it now/ }));
      expect(screen.getByRole("radio", { name: /^Start it now/ })).toBeChecked();
      await openAdvanced(user);

      await user.click(screen.getByLabelText(/^Keep launch disabled/));

      expect(screen.getByRole("radio", { name: /^Keep it off/ })).toBeChecked();
      expect(screen.getByRole("radio", { name: /^Start it now/ })).toBeDisabled();
      expect(screen.getByText("Starting is turned off for this server.")).toBeVisible();
      expect(screen.getByRole("button", { name: "Create server and keep off" })).toBeVisible();

      await user.click(screen.getByLabelText(/^Keep launch disabled/));

      expect(screen.getByRole("radio", { name: /^Start it now/ })).toBeEnabled();
      expect(screen.getByRole("radio", { name: /^Keep it off/ })).toBeChecked();
    });

    it("opens Advanced options and focuses the cost owner when the owner is to be changed", async () => {
      const user = userEvent.setup();
      renderPage();

      await user.click(await screen.findByRole("button", { name: "Change cost owner" }));

      expect(screen.getByRole("button", { name: "Advanced options" })).toHaveAttribute(
        "aria-expanded",
        "true",
      );
      await waitFor(() => expect(screen.getByLabelText("Cost owner")).toHaveFocus());
    });

    it("moves to the size and storage of the other provider when the provider changes", async () => {
      vi.mocked(hostingService.capabilities).mockResolvedValue([
        createCapability({
          runtimeProviderProfiles: [
            createProvider("alpha-cloud"),
            createProvider("beta-cloud", {
              profiles: [createProfile("tiny", { estimatedHourlyCostEur: 0.02 })],
              storage: null,
            }),
          ],
        }),
      ]);
      const demand = createStorageDemand();
      const [fit] = demand.runtimeProfiles;
      if (fit === undefined) throw new Error("The fixture has no size fit.");
      vi.mocked(hostingService.estimateStorage).mockResolvedValue(
        createStorageDemand({
          runtimeProfiles: [
            ...demand.runtimeProfiles,
            { ...fit, runtimeProviderIdentifier: "beta-cloud", runtimeProfileIdentifier: "tiny" },
          ],
        }),
      );
      const user = userEvent.setup();
      renderPage();

      await waitFor(() => expect(screen.getByLabelText(/^Size/)).toHaveValue("medium"));
      await openAdvanced(user);
      expect(screen.getByLabelText("Storage")).toHaveValue("AutomaticExternal");
      await user.selectOptions(screen.getByLabelText("Runtime provider"), "beta-cloud");

      await waitFor(() => expect(screen.getByLabelText(/^Size/)).toHaveValue("tiny"));
      expect(screen.getByLabelText("Storage")).toHaveValue("IncludedDisk");
      await user.type(screen.getByLabelText(/^Server name/), "Beta");
      await user.click(await readySubmit());
      await waitFor(() =>
        expect(hostingService.create).toHaveBeenCalledWith(
          expect.objectContaining({
            runtimeProviderIdentifier: "beta-cloud",
            runtimeProfileIdentifier: "tiny",
            runtimeStorageStrategy: "IncludedDisk",
          }),
        ),
      );
    });

    it("fills a required terrain and scenario with the defaults of the game", async () => {
      vi.mocked(hostingService.capabilities).mockResolvedValue([counterStrikeCapability()]);
      vi.mocked(hostingService.terrains).mockResolvedValue(
        createContentOptions([
          { primaryKey: "1", sourceIdentifier: "de_mirage", title: "Mirage" },
          { primaryKey: "2", sourceIdentifier: "de_dust2", title: "Dust 2" },
        ]),
      );
      vi.mocked(hostingService.scenarios).mockResolvedValue(
        createContentOptions([
          { primaryKey: "20000000-0000-4000-8000-000000000001", sourceIdentifier: "competitive" },
        ]),
      );
      const user = userEvent.setup();
      renderPage();

      await waitFor(() => expect(screen.getByLabelText(/^Map/)).toHaveValue("2"));
      await user.type(screen.getByLabelText(/^Server name/), "Counter");
      await user.click(await readySubmit());

      await waitFor(() =>
        expect(hostingService.create).toHaveBeenCalledWith(
          expect.objectContaining({
            gameIdentifier: "cs2",
            terrainId: 2,
            scenarioId: "20000000-0000-4000-8000-000000000001",
          }),
        ),
      );
    });

    it("shows a capacity outside the provider bounds as a field error without blocking the submit", async () => {
      const user = userEvent.setup();
      renderPage();

      await screen.findByLabelText(/^Server name/);
      await openAdvanced(user);
      await user.selectOptions(screen.getByLabelText("Storage"), "FixedExternal");
      const capacity = await screen.findByLabelText(/^External capacity/);
      await user.clear(capacity);
      await user.type(capacity, "15");
      await user.tab();

      expect(
        await screen.findByText("Enter a valid positive whole-number capacity."),
      ).toBeVisible();
      expect(capacity).toBeInvalid();
      await readySubmit();
      expect(screen.queryByText(/does not fit this setup/)).not.toBeInTheDocument();
    });
  });

  describe("invalid submit and Advanced auto-open", () => {
    /** Returns whether the Advanced options disclosure is open. */
    function advancedOpen(): boolean {
      return (
        screen.getByRole("button", { name: "Advanced options" }).getAttribute("aria-expanded") ===
        "true"
      );
    }

    /** Chooses explicitly sized external storage with a capacity off the provider's step, then closes Advanced. */
    async function enterBadCapacity(user: ReturnType<typeof userEvent.setup>) {
      await user.click(await screen.findByText("Advanced options"));
      await user.selectOptions(screen.getByLabelText("Storage"), "FixedExternal");
      const capacity = await screen.findByLabelText(/^External capacity/);
      await user.clear(capacity);
      await user.type(capacity, "25");
      await user.click(screen.getByText("Advanced options"));
      await waitFor(() => expect(advancedOpen()).toBe(false));
    }

    it("focuses the empty name and shows the singular alert without a request", async () => {
      const user = userEvent.setup();
      renderPage();

      await screen.findByLabelText(/^Server name/);
      await user.click(await readySubmit());

      await waitFor(() => expect(screen.getByLabelText(/^Server name/)).toHaveFocus());
      expect(screen.getByText("Check the highlighted field before creating.")).toBeVisible();
      expect(hostingService.create).not.toHaveBeenCalled();
    });

    it("opens Advanced and focuses a hidden invalid capacity", async () => {
      const user = userEvent.setup();
      renderPage();

      await user.type(await screen.findByLabelText(/^Server name/), "Hidden");
      await enterBadCapacity(user);
      await user.click(await readySubmit());

      await waitFor(() => expect(advancedOpen()).toBe(true));
      await waitFor(() => expect(screen.getByLabelText(/^External capacity/)).toHaveFocus());
      expect(screen.getByText("Check the highlighted field before creating.")).toBeVisible();
      expect(hostingService.create).not.toHaveBeenCalled();
    });

    it("focuses the first invalid field in DOM order and counts both", async () => {
      const user = userEvent.setup();
      renderPage();

      await screen.findByLabelText(/^Server name/);
      await enterBadCapacity(user);
      await user.click(await readySubmit());

      await waitFor(() => expect(screen.getByLabelText(/^Server name/)).toHaveFocus());
      expect(screen.getByText("Check the 2 highlighted fields before creating.")).toBeVisible();
      expect(hostingService.create).not.toHaveBeenCalled();
    });

    it("opens Advanced by itself when a required terrain has no value", async () => {
      vi.mocked(hostingService.capabilities).mockResolvedValue([counterStrikeCapability()]);
      vi.mocked(hostingService.terrains).mockResolvedValue([]);
      vi.mocked(hostingService.scenarios).mockResolvedValue(
        createContentOptions([
          { primaryKey: "20000000-0000-4000-8000-000000000001", sourceIdentifier: "competitive" },
        ]),
      );
      renderPage();

      await waitFor(() => expect(advancedOpen()).toBe(true));
      expect(await screen.findByText("Choose a Map to continue.")).toBeVisible();
      expect(screen.getByLabelText(/^Map/)).toBeInvalid();
    });

    it("opens Advanced by itself when the storage does not fit and lets the member close it", async () => {
      vi.mocked(hostingService.capabilities).mockResolvedValue([
        createCapability({
          runtimeProviderProfiles: [createProvider("fake-cloud", { storage: null })],
        }),
      ]);
      const demand = createStorageDemand();
      const [fit] = demand.runtimeProfiles;
      if (fit === undefined) throw new Error("The fixture has no size fit.");
      vi.mocked(hostingService.estimateStorage).mockResolvedValue(
        createStorageDemand({ runtimeProfiles: [{ ...fit, isSufficient: false }] }),
      );
      const user = userEvent.setup();
      renderPage();

      await waitFor(() => expect(advancedOpen()).toBe(true));
      await user.click(screen.getByText("Advanced options"));
      await waitFor(() => expect(advancedOpen()).toBe(false));
      await user.type(screen.getByLabelText(/^Server name/), "Closed again");

      expect(advancedOpen()).toBe(false);
    });
  });

  describe("early block", () => {
    const frozenReason =
      "Personal budget is frozen, so it cannot pay for a new server. Choose another cost owner.";

    /** Types a name so that only the blocking reason can keep the member from creating. */
    async function typeName(user: ReturnType<typeof userEvent.setup>) {
      await user.type(await screen.findByLabelText(/^Server name/), "Blocked");
    }

    it("blocks a frozen owner before any request and lets the member choose another owner", async () => {
      vi.mocked(hostingService.creationCostOwners).mockResolvedValue(
        createOwners({ frozenSelf: true }),
      );
      const user = userEvent.setup();
      renderPage();

      await typeName(user);
      expect(
        await screen.findByText(
          "Personal budget is frozen and cannot take on a new server. Choose another cost owner.",
        ),
      ).toBeVisible();
      expect(await screen.findByText(frozenReason)).toBeVisible();
      await user.click(screen.getByRole("button", { name: "Create server and keep off" }));
      await user.type(screen.getByLabelText(/^Server name/), "{Enter}");
      expect(hostingService.create).not.toHaveBeenCalled();

      await user.click(screen.getByRole("button", { name: "Change cost owner" }));
      await user.selectOptions(
        screen.getByLabelText("Cost owner"),
        "10000000-0000-4000-8000-000000000011",
      );
      await waitFor(() => expect(screen.queryByText(frozenReason)).not.toBeInTheDocument());
      await user.click(await readySubmit());

      await waitFor(() =>
        expect(hostingService.create).toHaveBeenCalledWith(
          expect.objectContaining({ costOwnerId: "10000000-0000-4000-8000-000000000011" }),
        ),
      );
    });

    it("blocks while the cost owners are loading", async () => {
      vi.mocked(hostingService.creationCostOwners).mockReturnValue(new Promise(() => undefined));
      const user = userEvent.setup();
      renderPage();

      await typeName(user);
      expect(await screen.findAllByText("Checking who pays…")).toHaveLength(2);
      await user.type(screen.getByLabelText(/^Server name/), "{Enter}");
      expect(hostingService.create).not.toHaveBeenCalled();
    });

    it("blocks when the cost owners failed and checks them again on request", async () => {
      vi.mocked(hostingService.creationCostOwners).mockRejectedValueOnce(
        new ApiError("owner secret", 500, "server"),
      );
      const user = userEvent.setup();
      renderPage();

      expect(await screen.findByText("Who pays could not be checked. Try again.")).toBeVisible();
      expect(screen.queryByText("owner secret")).not.toBeInTheDocument();
      await user.click(screen.getByRole("button", { name: "Check cost owner again" }));

      expect(await screen.findByText("Billed to Personal budget")).toBeVisible();
      expect(hostingService.creationCostOwners).toHaveBeenCalledTimes(2);
      await readySubmit();
    });

    /** Serves a capability that requires a terrain and a scenario, with the scenario list resolved. */
    function serveContentGame() {
      vi.mocked(hostingService.capabilities).mockResolvedValue([counterStrikeCapability()]);
      vi.mocked(hostingService.scenarios).mockResolvedValue(
        createContentOptions([
          { primaryKey: "20000000-0000-4000-8000-000000000001", sourceIdentifier: "competitive" },
        ]),
      );
    }

    it("blocks while a required option list is loading", async () => {
      serveContentGame();
      vi.mocked(hostingService.terrains).mockReturnValue(new Promise(() => undefined));
      const user = userEvent.setup();
      renderPage();

      await typeName(user);
      expect(await screen.findByText("Loading the options for this game…")).toBeVisible();
      await user.type(screen.getByLabelText(/^Server name/), "{Enter}");
      expect(hostingService.create).not.toHaveBeenCalled();
    });

    it("blocks when a required option list failed", async () => {
      serveContentGame();
      vi.mocked(hostingService.terrains).mockRejectedValue(new ApiError("terrain", 500, "server"));
      renderPage();
      expect(
        await screen.findByText("The options for this game could not be loaded. Try again."),
      ).toBeVisible();
    });

    it("blocks when no provider is available for the game", async () => {
      vi.mocked(hostingService.capabilities).mockResolvedValue([
        createCapability({
          runtimeProviderProfiles: [createProvider("fake-cloud", { availabilityStatus: "paused" })],
        }),
      ]);
      renderPage();

      expect(
        await screen.findByText("No cloud provider is available for this game right now."),
      ).toBeVisible();
    });

    it("blocks while the storage check runs and when it failed", async () => {
      vi.mocked(hostingService.estimateStorage).mockReturnValue(new Promise(() => undefined));
      const user = userEvent.setup();
      renderPage();

      await typeName(user);
      expect(await screen.findByText("Checking whether this setup fits…")).toBeVisible();
      const summary = screen.getByRole("complementary", { name: "Estimated cost" });
      expect(within(summary).getByText("Calculating…")).toBeVisible();
      await user.type(screen.getByLabelText(/^Server name/), "{Enter}");
      expect(hostingService.create).not.toHaveBeenCalled();
    });

    it("blocks with the failure reason when the storage check failed", async () => {
      vi.mocked(hostingService.estimateStorage).mockRejectedValue(
        new ApiError("estimate secret", 500, "server"),
      );
      renderPage();

      expect(await screen.findByText("The storage check failed. Try again.")).toBeVisible();
      expect(screen.queryByText(/estimate secret/)).not.toBeInTheDocument();
    });

    it("blocks when the selected storage does not fit the setup", async () => {
      vi.mocked(hostingService.capabilities).mockResolvedValue([
        createCapability({
          runtimeProviderProfiles: [createProvider("fake-cloud", { storage: null })],
        }),
      ]);
      const demand = createStorageDemand();
      const [fit] = demand.runtimeProfiles;
      if (fit === undefined) throw new Error("The fixture has no size fit.");
      vi.mocked(hostingService.estimateStorage).mockResolvedValue(
        createStorageDemand({ runtimeProfiles: [{ ...fit, isSufficient: false }] }),
      );
      renderPage();

      expect(
        await screen.findByText(
          "The selected storage does not fit this setup. Change the size or storage under Advanced options.",
        ),
      ).toBeVisible();
    });
  });

  describe("live estimate", () => {
    it("adds the explicit external capacity to the size price and derives the month", async () => {
      const user = userEvent.setup();
      renderPage();

      await screen.findByLabelText(/^Server name/);
      await user.click(screen.getByText("Advanced options"));
      await user.selectOptions(screen.getByLabelText("Storage"), "FixedExternal");
      const capacity = await screen.findByLabelText(/^External capacity/);
      await user.clear(capacity);
      await user.type(capacity, "100");

      const hourly = 0.1 + 100 * 0.001;
      const summary = screen.getByRole("complementary", { name: "Estimated cost" });
      await waitFor(() =>
        expect(
          within(summary).getByText(`${formatHourlyRate(hourly)} per hour while it runs`),
        ).toBeVisible(),
      );
      expect(
        within(summary).getByText(`about ${formatMoney(hourly * 730)} per month if left running`),
      ).toBeVisible();
    });

    it("never invents an amount for a size without a price and does not block", async () => {
      vi.mocked(hostingService.capabilities).mockResolvedValue([
        createCapability({
          runtimeProviderProfiles: [
            createProvider("fake-cloud", {
              profiles: [createProfile("medium", { estimatedHourlyCostEur: null })],
            }),
          ],
        }),
      ]);
      renderPage();

      // The estimate settles first: the storage check runs before the summary may say anything final.
      await readySubmit();
      expect(
        screen.getByText("Cost estimate not available. You can still create the server."),
      ).toBeVisible();
      const summary = screen.getByRole("complementary", { name: "Estimated cost" });
      expect(within(summary).queryByText(/per hour while it runs/)).not.toBeInTheDocument();
    });
  });

  describe("presets", () => {
    const profileId = "10000000-0000-4000-8000-000000000020";
    const otherProfileId = "10000000-0000-4000-8000-000000000021";

    /** Serves two configuration profiles and one mod pack. */
    function servePresets() {
      vi.mocked(hostingService.creationModPacks).mockResolvedValue([
        { primaryKey: 42, title: "Operations" },
      ]);
      vi.mocked(hostingService.profileOptions).mockResolvedValue([
        {
          primaryKey: profileId,
          title: "Public operation",
          gameIdentifier: "arma3",
          isArchived: false,
          latestRevisionNumber: 3,
        },
        {
          primaryKey: otherProfileId,
          title: "Training",
          gameIdentifier: "arma3",
          isArchived: false,
          latestRevisionNumber: 1,
        },
      ]);
    }

    /** Waits for the preset select with both option groups loaded. */
    async function presetSelect(): Promise<HTMLElement> {
      const select = await screen.findByLabelText("Preset");
      await within(select).findByRole("option", { name: "Operations" });
      return select;
    }

    it("offers the presets between the name and the size with the applied line", async () => {
      servePresets();
      renderPage();

      const select = await presetSelect();
      expect(select).toHaveValue("default");
      expect(
        screen.getByText(
          "This preset creates the server and keeps it off. You decide when to start.",
        ),
      ).toBeVisible();
      const name = screen.getByLabelText(/^Server name/);
      const size = screen.getByLabelText(/^Size/);
      expect(name.compareDocumentPosition(select) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(select.compareDocumentPosition(size) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it("applies a profile preset, keeps the server off and announces it", async () => {
      servePresets();
      const user = userEvent.setup();
      renderPage();

      await presetSelect();
      await user.click(screen.getByRole("radio", { name: /^Start it now/ }));
      expect(screen.getByRole("radio", { name: /^Start it now/ })).toBeChecked();
      await user.selectOptions(screen.getByLabelText("Preset"), `profile:${profileId}`);

      expect(screen.getByRole("radio", { name: /^Keep it off/ })).toBeChecked();
      expect(screen.getByLabelText("Preset")).toHaveFocus();
      expect(
        screen.getByText(
          "Preset applied: Public operation. The server will be created and kept off.",
        ),
      ).toBeVisible();
      await waitFor(() =>
        expect(
          document.querySelector(".disclosure.create-advanced .disclosure__summary-line"),
        ).toHaveTextContent("Configuration: Public operation"),
      );
      await user.click(screen.getByText("Advanced options"));
      expect(screen.getByLabelText("Configuration profile")).toHaveValue(profileId);
    });

    it("creates with the mod pack the preset stands for and no start intent", async () => {
      servePresets();
      const user = userEvent.setup();
      const { router } = renderPage();

      await presetSelect();
      await user.selectOptions(screen.getByLabelText("Preset"), "modPack:42");
      await user.type(screen.getByLabelText(/^Server name/), "Preset server");
      await user.click(await readySubmit());

      await waitFor(() => expect(hostingService.create).toHaveBeenCalledTimes(1));
      expect(hostingService.create).toHaveBeenCalledWith(
        expect.objectContaining({ modPackDefinitionId: 42, configurationProfileId: null }),
      );
      await waitFor(() =>
        expect(router.state.location.pathname).toBe(`/command/servers/${createdServer.primaryKey}`),
      );
      expect(router.state.location.state).toBeNull();
    });

    it("reads Customized when the Advanced fields diverge from every preset", async () => {
      servePresets();
      const user = userEvent.setup();
      renderPage();

      await presetSelect();
      await user.selectOptions(screen.getByLabelText("Preset"), "modPack:42");
      await user.click(screen.getByText("Advanced options"));
      await user.selectOptions(screen.getByLabelText("Configuration profile"), profileId);

      expect(screen.getByLabelText("Preset")).toHaveValue("custom");
      expect(
        screen.getByText("Customized: your Advanced options differ from this preset."),
      ).toBeVisible();
    });

    it("clears both values when Game defaults is chosen and still announces it", async () => {
      servePresets();
      const user = userEvent.setup();
      renderPage();

      await presetSelect();
      await user.selectOptions(screen.getByLabelText("Preset"), "modPack:42");
      await user.selectOptions(screen.getByLabelText("Preset"), "default");

      expect(screen.getByLabelText("Preset")).toHaveValue("default");
      expect(
        screen.getByText("Preset applied: Game defaults. The server will be created and kept off."),
      ).toBeVisible();
      await user.type(screen.getByLabelText(/^Server name/), "Defaults");
      await user.click(await readySubmit());
      await waitFor(() =>
        expect(hostingService.create).toHaveBeenCalledWith(
          expect.objectContaining({ modPackDefinitionId: null, configurationProfileId: null }),
        ),
      );
    });

    it("omits the Preset field without profiles and mod packs but keeps the Advanced defaults", async () => {
      const user = userEvent.setup();
      renderPage();

      await screen.findByLabelText(/^Server name/);
      await readySubmit();
      expect(screen.queryByLabelText("Preset")).not.toBeInTheDocument();
      await user.click(screen.getByText("Advanced options"));
      expect(screen.getByRole("option", { name: "Game defaults" })).toBeInTheDocument();
      expect(screen.getByRole("option", { name: "No mod pack" })).toBeInTheDocument();
    });

    it("shows the options error with Try again and still lets the member create", async () => {
      vi.mocked(hostingService.profileOptions).mockRejectedValueOnce(
        new ApiError("profile secret", 500, "server"),
      );
      const user = userEvent.setup();
      renderPage();

      const [message] = await screen.findAllByText(
        "Some options could not be loaded. Nothing is lost; try again.",
      );
      expect(message).toBeVisible();
      expect(screen.queryByText("profile secret")).not.toBeInTheDocument();
      await user.type(screen.getByLabelText(/^Server name/), "Resilient");
      await user.click(await readySubmit());
      await waitFor(() => expect(hostingService.create).toHaveBeenCalledTimes(1));
      expect(hostingService.profileOptions).toHaveBeenCalledTimes(1);
    });

    it("loads the preset lists again on Try again", async () => {
      vi.mocked(hostingService.profileOptions).mockRejectedValueOnce(
        new ApiError("profile secret", 500, "server"),
      );
      const user = userEvent.setup();
      renderPage();

      const [retry] = await screen.findAllByRole("button", { name: "Try again" });
      if (retry === undefined) throw new Error("The retry button is missing.");
      await user.click(retry);

      await waitFor(() => expect(hostingService.profileOptions).toHaveBeenCalledTimes(2));
    });
  });

  describe("pending and request failure", () => {
    const errorText =
      "The server could not be created. Nothing was started and nothing costs. Check your entries and try again.";

    it("is busy and inert while creating and creates only once", async () => {
      let resolveCreate: (server: GameServer) => void = () => undefined;
      vi.mocked(hostingService.create).mockReturnValue(
        new Promise<GameServer>((resolve) => {
          resolveCreate = resolve;
        }),
      );
      const user = userEvent.setup();
      renderPage();

      const name = await screen.findByLabelText(/^Server name/);
      await user.type(name, "Pending one");
      await user.click(await readySubmit());

      const form = name.closest("form") as HTMLFormElement;
      await waitFor(() => expect(form).toHaveAttribute("aria-busy", "true"));
      expect(document.querySelector(".create-page__main")).toHaveAttribute("inert");
      expect(screen.getByRole("button", { name: /Creating server/ })).toBeVisible();
      fireEvent.submit(form);
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(hostingService.create).toHaveBeenCalledTimes(1);

      resolveCreate(createdServer);
      expect(await screen.findByText("Server workspace")).toBeVisible();
      expect(hostingService.create).toHaveBeenCalledTimes(1);
    });

    it("shows a focused fixed error, stays editable and checks the cost owners again", async () => {
      vi.mocked(hostingService.create).mockRejectedValueOnce(
        new ApiError("provider token secret", 500, "server"),
      );
      const user = userEvent.setup();
      renderPage();

      const name = await screen.findByLabelText(/^Server name/);
      await user.type(name, "Failing");
      await user.click(await readySubmit());

      const alert = await screen.findByText(errorText);
      await waitFor(() => expect(alert).toHaveFocus());
      const form = name.closest("form") as HTMLFormElement;
      expect(form).not.toHaveAttribute("aria-busy", "true");
      expect(document.querySelector(".create-page__main")).not.toHaveAttribute("inert");
      expect(name).toBeEnabled();
      await waitFor(() => expect(hostingService.creationCostOwners).toHaveBeenCalledTimes(2));
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
      expect(screen.queryByText("provider token secret")).not.toBeInTheDocument();

      await user.click(await readySubmit());

      await waitFor(() => expect(hostingService.create).toHaveBeenCalledTimes(2));
      await waitFor(() => expect(screen.queryByText(errorText)).not.toBeInTheDocument());
    });

    it("accepts a 256-character name and echoes it in full in the toast", async () => {
      const longName = "n".repeat(256);
      const user = userEvent.setup();
      renderPage();

      await user.click(await screen.findByLabelText(/^Server name/));
      await user.paste(longName);
      await user.click(await readySubmit());

      await waitFor(() =>
        expect(hostingService.create).toHaveBeenCalledWith(
          expect.objectContaining({ title: longName }),
        ),
      );
      expect(await screen.findByText(`${longName} was created and is off.`)).toBeVisible();
    });
  });

  describe("announcements", () => {
    /** Returns the one polite live region of the page. */
    function liveRegion(): HTMLElement {
      return document.querySelector('form [aria-live="polite"]') as HTMLElement;
    }

    afterEach(() => {
      vi.useRealTimers();
    });

    it("announces a game change", async () => {
      vi.mocked(hostingService.capabilities).mockResolvedValue([
        createCapability(),
        createCapability({
          gameIdentifier: "other-game",
          displayName: "Other game",
          runtimeProviderProfiles: [
            createProvider("second-cloud", {
              profiles: [createProfile("tiny", { estimatedHourlyCostEur: 0.02 })],
            }),
          ],
        }),
      ]);
      const user = userEvent.setup();
      renderPage();

      await user.selectOptions(await screen.findByLabelText("Game"), "other-game");

      expect(liveRegion()).toHaveTextContent("Options reset for Other game.");
    });

    it("does not announce the first estimate", async () => {
      renderPage();

      await readySubmit();
      await new Promise((resolve) => setTimeout(resolve, 700));

      expect(liveRegion()).toBeEmptyDOMElement();
    });

    it("announces a changed hourly figure once, 600ms after the last change", async () => {
      vi.mocked(hostingService.capabilities).mockResolvedValue([
        createCapability({
          runtimeProviderProfiles: [
            createProvider("fake-cloud", {
              profiles: [
                createProfile("small", { estimatedHourlyCostEur: 0.05 }),
                createProfile("large", { estimatedHourlyCostEur: 0.4 }),
              ],
            }),
          ],
        }),
      ]);
      renderPage();
      await waitFor(() => expect(screen.getByLabelText(/^Size/)).toHaveValue("small"));
      await readySubmit();
      await new Promise((resolve) => setTimeout(resolve, 700));
      expect(liveRegion()).toBeEmptyDOMElement();

      // Fake timers replace only the debounce; the events are fired directly because user-event cannot drive them.
      vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
      fireEvent.change(screen.getByLabelText(/^Size/), { target: { value: "large" } });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(599);
      });
      expect(liveRegion()).toBeEmptyDOMElement();
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1);
      });
      const summary = screen.getByRole("complementary", { name: "Estimated cost" });
      const hourly = within(summary)
        .getByText(/per hour while it runs/)
        .textContent?.replace(" per hour while it runs", "");
      expect(hourly).toBeTruthy();
      expect(liveRegion()).toHaveTextContent(`Estimated cost is now ${hourly} per hour.`);
    });

    it("announces exactly once for a burst of capacity changes", async () => {
      renderPage();
      await readySubmit();
      await new Promise((resolve) => setTimeout(resolve, 700));
      const user = userEvent.setup();
      await user.click(screen.getByText("Advanced options"));
      await user.selectOptions(screen.getByLabelText("Storage"), "FixedExternal");
      const capacity = await screen.findByLabelText(/^External capacity/);
      await new Promise((resolve) => setTimeout(resolve, 700));

      vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
      const changes: MutationRecord[] = [];
      const observer = new MutationObserver((records) => changes.push(...records));
      observer.observe(liveRegion(), { childList: true, characterData: true, subtree: true });
      for (const value of ["2", "20", "200"]) {
        fireEvent.change(capacity, { target: { value } });
      }
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1_000);
      });
      observer.disconnect();

      expect(liveRegion()).toHaveTextContent(
        `Estimated cost is now ${formatHourlyRate(0.1 + 200 * 0.001)} per hour.`,
      );
      expect(changes).toHaveLength(1);
    });
  });

  it("opens the workspace with the start intent when Start it now is chosen", async () => {
    const user = userEvent.setup();
    const { router } = renderPage();

    await user.type(await screen.findByLabelText(/^Server name/), "UTF One");
    await user.click(screen.getByRole("radio", { name: /^Start it now/ }));
    await user.click(await readySubmit("Create server and start"));

    expect(await screen.findByText("UTF One was created. Confirm the start next.")).toBeVisible();
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/command/servers/${createdServer.primaryKey}`),
    );
    expect(router.state.location.state).toEqual({ createdStartPending: true });
  });
});
