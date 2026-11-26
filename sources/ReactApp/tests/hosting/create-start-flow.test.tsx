import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StrictMode } from "react";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, Outlet, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import { ApiError } from "@/api/client";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { CostConfirmationProvider } from "@/components/server/CostConfirmationProvider";
import { CreateServerPage } from "@/features/hosting/CreateServerPage";
import { createStartState, hasCreateStartIntent } from "@/features/hosting/createStartIntent";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, LifecycleJob } from "@/features/hosting/types";
import { createCapability, createOwners, createStorageDemand } from "../helpers/createFixtures";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";

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

const startJob = { primaryKey: "job-1" } as LifecycleJob;

interface FlowOptions {
  /** Wraps the whole tree in React StrictMode. */
  strict?: boolean;
  /** Query client to use; a fresh client without retries is created when omitted. */
  queryClient?: QueryClient;
  /** Initial router entry. Defaults to the create page. */
  route?: string;
}

/** Layout route that mounts the real confirmation provider above both pages, as the app shell does. */
function FlowLayout() {
  return (
    <CostConfirmationProvider>
      <Outlet />
    </CostConfirmationProvider>
  );
}

/**
 * Renders the create page and the workspace under one router with the real confirmation provider.
 *
 * @param options StrictMode, query client and initial route.
 * @returns The router and the query client the pages run under.
 */
function renderFlow(options: FlowOptions = {}) {
  const queryClient =
    options.queryClient ??
    new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const router = createMemoryRouter(
    [
      {
        element: <FlowLayout />,
        children: [
          { path: "/command/servers/create", element: <CreateServerPage /> },
          { path: "/command/servers/:serverId", element: <ServerWorkspacePage /> },
        ],
      },
    ],
    { initialEntries: [options.route ?? "/command/servers/create"] },
  );
  const tree = (
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </I18nextProvider>
    </QueryClientProvider>
  );
  render(options.strict ? <StrictMode>{tree}</StrictMode> : tree);
  return { router, queryClient };
}

describe("hasCreateStartIntent", () => {
  it("accepts only an own createdStartPending property that is the literal true", () => {
    expect(hasCreateStartIntent({ createdStartPending: true })).toBe(true);
    expect(hasCreateStartIntent(createStartState)).toBe(true);
  });

  it.each([
    ["null", null],
    ["undefined", undefined],
    ["true", true],
    ["the string true", "true"],
    ["an array", [true]],
    ["a string flag", { createdStartPending: "true" }],
    ["a numeric flag", { createdStartPending: 1 }],
    ["a false flag", { createdStartPending: false }],
    ["an empty object", {}],
    ["an inherited flag", Object.create({ createdStartPending: true }) as unknown],
  ])("rejects %s", (_name, state) => {
    expect(hasCreateStartIntent(state)).toBe(false);
  });
});

describe("create then start flow", () => {
  const created: GameServer = fleetServer({ title: "Created server" });

  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = creator;
    // The unsaved-changes guard asks before leaving the dirty create form.
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([createCapability()]);
    vi.spyOn(hostingService, "creationModPacks").mockResolvedValue([]);
    vi.spyOn(hostingService, "profileOptions").mockResolvedValue([]);
    vi.spyOn(hostingService, "estimateStorage").mockResolvedValue(createStorageDemand());
    vi.spyOn(hostingService, "creationCostOwners").mockResolvedValue(createOwners());
    vi.spyOn(hostingService, "create").mockResolvedValue(created);
    vi.spyOn(hostingService, "server").mockResolvedValue(created);
    vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
    vi.spyOn(hostingService, "operations").mockResolvedValue([]);
    vi.spyOn(hostingService, "executeAction").mockResolvedValue(startJob);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await i18n.changeLanguage("en");
  });

  /** Makes the create and server queries resolve a different server. */
  function serveInstead(server: GameServer) {
    vi.mocked(hostingService.create).mockResolvedValue(server);
    vi.mocked(hostingService.server).mockResolvedValue(server);
  }

  /** The notice section of a created server whose start was not confirmed. */
  async function findNotice() {
    const text = await screen.findByText("was created and is still off.", { exact: false });
    const section = text.closest("section");
    if (section === null) throw new Error("The notice is not inside a section.");
    return section;
  }

  /** Creates with the start intent and dismisses the dialog that opens. */
  async function createAndDismissStart() {
    const user = await submitWith("Create server and start");
    const dialog = await screen.findByRole("dialog", { name: "Start Created server?" });
    await user.click(within(dialog).getByRole("button", { name: "Don't start server" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    return user;
  }

  /**
   * Types a name, chooses what happens after creating and activates the create control.
   *
   * @param buttonName The accessible name of the create control: it names the chosen outcome.
   */
  async function submitWith(buttonName: "Create server and start" | "Create server and keep off") {
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText(/^Server name/), "Created server");
    if (buttonName === "Create server and start") {
      await user.click(screen.getByRole("radio", { name: /^Start it now/ }));
    }
    // A blocked create control stays focusable but shows its reason and is aria-disabled.
    const submit = await waitFor(() => {
      const button = screen.getByRole("button", { name: buttonName });
      expect(button).not.toHaveAttribute("aria-disabled");
      return button;
    });
    await user.click(submit);
    return user;
  }

  it("opens the start confirmation over the workspace after creating with the start intent", async () => {
    const { router } = renderFlow();
    await submitWith("Create server and start");

    expect(hostingService.create).toHaveBeenCalledTimes(1);
    const dialog = await screen.findByRole("dialog", { name: "Start Created server?" });
    expect(within(dialog).getByRole("button", { name: "Don't start server" })).toBeVisible();
    expect(router.state.location.pathname).toBe(`/command/servers/${created.primaryKey}`);
    expect(hostingService.executeAction).not.toHaveBeenCalled();
  });

  it("opens the confirmation when the created server is already in the query cache", async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    queryClient.setQueryData(hostingQueryKeys.server(created.primaryKey), created);
    renderFlow({ queryClient });
    await submitWith("Create server and start");

    expect(await screen.findByRole("dialog", { name: "Start Created server?" })).toBeVisible();
  });

  it("clears the router state after the workspace read it", async () => {
    const { router } = renderFlow();
    await submitWith("Create server and start");
    await screen.findByRole("dialog", { name: "Start Created server?" });

    await waitFor(() => expect(router.state.location.state).toBeNull());
    expect(router.state.location.pathname).toBe(`/command/servers/${created.primaryKey}`);
  });

  it("executes the start with the new server id when the dialog is confirmed", async () => {
    renderFlow();
    const user = await submitWith("Create server and start");

    const dialog = await screen.findByRole("dialog", { name: "Start Created server?" });
    await user.click(within(dialog).getByRole("button", { name: "Start server" }));

    await waitFor(() =>
      expect(hostingService.executeAction).toHaveBeenCalledWith(
        created.primaryKey,
        "start",
        undefined,
      ),
    );
  });

  it("requests no confirmation when the member keeps the server off", async () => {
    const { router } = renderFlow();
    await submitWith("Create server and keep off");
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/command/servers/${created.primaryKey}`),
    );
    await waitFor(() => expect(hostingService.server).toHaveBeenCalled());
    await new Promise((resolve) => window.setTimeout(resolve, 50));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(hostingService.executeAction).not.toHaveBeenCalled();
  });

  it("neither navigates nor requests a confirmation when the create fails", async () => {
    vi.mocked(hostingService.create).mockRejectedValue(new ApiError("rejected", 500, "server"));
    const { router } = renderFlow();
    await submitWith("Create server and start");

    expect(
      await screen.findByText(
        "The server could not be created. Nothing was started and nothing costs. Check your entries and try again.",
      ),
    ).toBeVisible();
    await new Promise((resolve) => window.setTimeout(resolve, 50));
    expect(router.state.location.pathname).toBe("/command/servers/create");
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(hostingService.server).not.toHaveBeenCalled();
  });
  it("shows the not-started notice after the dialog is dismissed and never executes the start", async () => {
    renderFlow();
    await createAndDismissStart();

    const notice = await findNotice();
    expect(notice).toHaveAttribute("role", "status");
    expect(within(notice).getByText("Created server")).toBeVisible();
    expect(notice).toHaveTextContent(
      'Created server was created and is still off. Nothing costs until you confirm the start. Use "Start server" when you are ready.',
    );
    expect(hostingService.executeAction).not.toHaveBeenCalled();
  });

  it("removes the notice when the member dismisses it", async () => {
    renderFlow();
    const user = await createAndDismissStart();
    const notice = await findNotice();

    await user.click(within(notice).getByRole("button", { name: "Dismiss notice" }));

    await waitFor(() =>
      expect(screen.queryByText("was created and is still off.", { exact: false })).toBeNull(),
    );
  });

  it("removes the notice once the confirmed start is accepted", async () => {
    renderFlow();
    const user = await submitWith("Create server and start");
    await findNotice();

    const dialog = await screen.findByRole("dialog", { name: "Start Created server?" });
    await user.click(within(dialog).getByRole("button", { name: "Start server" }));

    await waitFor(() => expect(hostingService.executeAction).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(screen.queryByText("was created and is still off.", { exact: false })).toBeNull(),
    );
  });

  it("requests nothing and explains the reason when the start action is unavailable", async () => {
    serveInstead(
      fleetServer({
        title: "Created server",
        isLaunchDisabled: true,
        actions: [
          fleetAction("start", { enabled: false, unavailableReason: "Server launch is disabled." }),
        ],
      }),
    );
    renderFlow();
    await submitWith("Create server and start");

    const notice = await findNotice();
    expect(within(notice).getByText("Starting is turned off for this server.")).toBeVisible();
    await new Promise((resolve) => window.setTimeout(resolve, 50));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(hostingService.executeAction).not.toHaveBeenCalled();
  });

  it("requests nothing and shows no reason when the server has no start action", async () => {
    serveInstead(fleetServer({ title: "Created server", actions: [] }));
    renderFlow();
    await submitWith("Create server and start");

    const notice = await findNotice();
    expect(notice).not.toHaveTextContent("turned off");
    await new Promise((resolve) => window.setTimeout(resolve, 50));
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("removes the notice when the derived server state leaves off", async () => {
    const { queryClient } = renderFlow();
    await createAndDismissStart();
    await findNotice();

    act(() => {
      queryClient.setQueryData(
        hostingQueryKeys.server(created.primaryKey),
        fleetServer({ title: "Created server", status: "Provisioning" }),
      );
    });

    await waitFor(() =>
      expect(screen.queryByText("was created and is still off.", { exact: false })).toBeNull(),
    );
  });

  it("opens exactly one dialog under StrictMode", async () => {
    renderFlow({ strict: true });
    await submitWith("Create server and start");

    await screen.findByRole("dialog", { name: "Start Created server?" });
    await new Promise((resolve) => window.setTimeout(resolve, 50));
    expect(screen.getAllByRole("dialog")).toHaveLength(1);
  });

  it("shows neither dialog nor notice when the workspace is reloaded without state", async () => {
    renderFlow({ route: `/command/servers/${created.primaryKey}` });

    await waitFor(() => expect(hostingService.server).toHaveBeenCalled());
    await screen.findByRole("heading", { name: "Created server" });
    await new Promise((resolve) => window.setTimeout(resolve, 50));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(screen.queryByText("was created and is still off.", { exact: false })).toBeNull();
  });

  it("wraps a long title without spaces in the name element of the notice", async () => {
    const title = "A".repeat(256);
    serveInstead(fleetServer({ title, actions: [] }));
    renderFlow();
    await submitWith("Create server and start");

    const notice = await findNotice();
    const name = notice.querySelector(".create-notice__name");
    expect(name).not.toBeNull();
    expect(name).toHaveTextContent(title);
  });
});
