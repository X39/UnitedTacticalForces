import { QueryClient, QueryObserver, useQuery } from "@tanstack/react-query";
import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/api/client";
import i18n, { i18nReady } from "@/app/i18n";
import { SettingsPanelStateProvider } from "@/components/server/useSettingsPanelState";
import { ServerAccessPanel } from "@/features/hosting/ServerAccessPanel";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, ServerAccess } from "@/features/hosting/types";
import {
  invalidateAfterAccessChange,
  revocationOrder,
} from "@/features/hosting/useAccessMutations";
import { fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { clearSettingsPanels } from "../helpers/settingsPanels";

/* The Access panel of the Settings section: claim gate, summary, list states and query options (D-01, D-02, D-14). */

/** The signed-in member of these tests; one access row carries this id. */
const signedInUserId = "10000000-0000-4000-8000-0000000000f1";

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: { primaryKey: signedInUserId, nickname: "Falcon One" },
    isAuthenticated: true,
    isLoading: false,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

/**
 * Builds a server whose access the viewer may manage unless the overrides say otherwise.
 *
 * @param overrides Members that replace the defaults.
 * @returns The server.
 */
function accessServer(overrides: Record<string, unknown> = {}): GameServer {
  return {
    ...fleetServer({ title: "Alpha Team" }),
    canModify: true,
    canManageAccess: true,
    ...overrides,
  } as unknown as GameServer;
}

/**
 * Builds the access list the API sends, with two members and one role unless the overrides say otherwise.
 *
 * @param overrides Members that replace the defaults.
 * @returns A fresh access list.
 */
function accessList(overrides: Partial<ServerAccess> = {}): ServerAccess {
  return {
    allowedPermissions: ["sv", "sv:view", "sv:operate"],
    assignablePermissions: ["sv", "sv:view", "sv:operate"],
    users: [
      {
        userId: signedInUserId,
        nickname: "Falcon One",
        detail: "#0000f1",
        permissions: ["sv:view", "sv:operate"],
      },
      {
        userId: "20000000-0000-4000-8000-0000000000a2",
        nickname: "Raven",
        detail: "#0000a2",
        permissions: ["sv"],
      },
    ],
    roles: [
      {
        roleId: 7,
        title: "Operators",
        description: "People who run the servers.",
        memberCount: 3,
        permissions: ["sv:view"],
      },
    ],
    ...overrides,
  };
}

/**
 * Renders the panel inside the Settings panel state it needs.
 *
 * @param server The server whose access is shown.
 * @returns The render result.
 */
function renderPanel(server: GameServer = accessServer()) {
  const ui: ReactElement = (
    <SettingsPanelStateProvider>
      <ServerAccessPanel server={server} />
    </SettingsPanelStateProvider>
  );
  return renderWithProviders(ui);
}

describe("Access panel", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("renders no heading, summary or request without canManageAccess", async () => {
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    renderPanel(accessServer({ canManageAccess: false }));

    await waitFor(() => expect(document.body).toBeInTheDocument());
    expect(screen.queryByRole("heading", { name: "Access" })).not.toBeInTheDocument();
    expect(screen.queryByText(/^Access: /)).not.toBeInTheDocument();
    expect(document.querySelector('[data-settings-panel="server-access"]')).toBeNull();
    expect(access).not.toHaveBeenCalled();
  });

  it("renders nothing when the flag is absent from an older payload", () => {
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const legacy = accessServer();
    delete (legacy as { canManageAccess?: boolean | null }).canManageAccess;
    renderPanel(legacy);

    expect(document.querySelector('[data-settings-panel="server-access"]')).toBeNull();
    expect(access).not.toHaveBeenCalled();
  });

  it("lists the members and roles and counts them in the summary", async () => {
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const server = accessServer();
    renderPanel(server);

    expect(await screen.findByText("Access: 3")).toBeInTheDocument();
    expect(access).toHaveBeenCalledWith(server.primaryKey, expect.anything());
    expect(screen.getByRole("heading", { level: 2, name: "Access" })).toBeVisible();
    expect(
      screen.getByRole("heading", { level: 3, name: "People and roles with access" }),
    ).toBeVisible();
    expect(screen.getByText("Falcon One")).toBeInTheDocument();
    expect(screen.getByText("Raven")).toBeInTheDocument();
    expect(screen.getByText("Operators")).toBeInTheDocument();
    expect(screen.getByText(/Administrators always have access/)).toBeInTheDocument();
  });

  it("keeps the API order: members first, then roles", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    renderPanel();

    await screen.findByText("Access: 3");
    const names = [...document.querySelectorAll(".access-list bdi")].map(
      (element) => element.textContent,
    );
    expect(names).toEqual(["Falcon One", "Raven", "Operators"]);
  });

  it("reads Nobody has been given access and shows the empty state at zero", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      accessList({ users: [], roles: [] }),
    );
    renderPanel();

    expect(await screen.findByText("Nobody has been given access")).toBeInTheDocument();
    expect(screen.getByText("No one has been given access")).toBeVisible();
    expect(screen.getByText(/Use Give access above/)).toBeVisible();
    expect(screen.queryByText(/^Access: /)).not.toBeInTheDocument();
  });

  it("shows no count while the list loads", async () => {
    vi.spyOn(hostingService, "serverAccess").mockReturnValue(new Promise(() => undefined));
    renderPanel();

    expect(await screen.findByText("Loading access…")).toBeVisible();
    expect(screen.queryByText(/^Access: /)).not.toBeInTheDocument();
    expect(screen.queryByText("Nobody has been given access")).not.toBeInTheDocument();
  });

  it("shows the error state without a count and retries with a real request", async () => {
    const user = userEvent.setup();
    const access = vi
      .spyOn(hostingService, "serverAccess")
      .mockRejectedValueOnce(new Error("access backend exploded"));
    renderPanel();

    expect(await screen.findByText("Access could not be loaded.")).toBeVisible();
    expect(screen.queryByText(/^Access: /)).not.toBeInTheDocument();
    expect(screen.queryByText("access backend exploded")).not.toBeInTheDocument();
    expect(access).toHaveBeenCalledTimes(1);

    access.mockResolvedValueOnce(accessList());
    await user.click(screen.getByRole("button", { name: "Try again" }));

    expect(await screen.findByText("Access: 3")).toBeInTheDocument();
    expect(access).toHaveBeenCalledTimes(2);
  });

  it("keeps the cached list, the count and an alert with Retry when a refetch fails", async () => {
    const user = userEvent.setup();
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const server = accessServer();
    const { queryClient } = renderPanel(server);
    await screen.findByText("Access: 3");

    access.mockRejectedValueOnce(new Error("blip at alt-tab time"));
    await queryClient.invalidateQueries({
      queryKey: hostingQueryKeys.access(server.primaryKey),
      exact: true,
    });

    expect(await screen.findByText(/Access could not be refreshed/)).toBeVisible();
    expect(screen.getByText("Access: 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Falcon One" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Raven" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Operators" })).toBeInTheDocument();
    expect(screen.queryByText("Access could not be loaded.")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Try again" }));

    await waitFor(() =>
      expect(screen.queryByText(/Access could not be refreshed/)).not.toBeInTheDocument(),
    );
    expect(screen.getByText("Access: 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Raven" })).toBeInTheDocument();
  });

  it("caches the list under the server key with staleTime 0 and refetch on focus", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const server = accessServer();
    const { queryClient } = renderPanel(server);

    await screen.findByText("Access: 3");
    const key = hostingQueryKeys.access(server.primaryKey);
    expect(key.slice(0, 3)).toEqual([...hostingQueryKeys.server(server.primaryKey)]);
    const options = queryClient.getQueryCache().find({ queryKey: key })?.observers[0]?.options;
    expect(options?.staleTime).toBe(0);
    expect(options?.refetchOnWindowFocus).toBe(true);
    expect(options?.refetchInterval).toBeUndefined();
  });

  it("refetches when the member opens a collapsed panel by hand, and not on the first render", async () => {
    const user = userEvent.setup();
    clearSettingsPanels();
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    renderPanel();

    expect(await screen.findByText("Access: 3")).toBeInTheDocument();
    expect(access).toHaveBeenCalledTimes(1);
    const header = screen.getByRole("button", { name: "Access" });
    expect(header).toHaveAttribute("aria-expanded", "false");

    await user.click(header);
    await waitFor(() => expect(access).toHaveBeenCalledTimes(2));
    expect(header).toHaveAttribute("aria-expanded", "true");

    await user.click(header);
    await user.click(header);
    await waitFor(() => expect(access).toHaveBeenCalledTimes(3));
  });

  it("does not refetch when the panel renders already open", async () => {
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    renderPanel();

    expect(await screen.findByText("Access: 3")).toBeInTheDocument();
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(access).toHaveBeenCalledTimes(1);
  });
});

describe("Access subject rows", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("shows every row closed with a toggle named by the subject name inside an h3", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    renderPanel();

    await screen.findByText("Access: 3");
    for (const name of ["Falcon One", "Raven", "Operators"]) {
      const toggle = screen.getByRole("button", { name });
      expect(toggle).toHaveAttribute("aria-expanded", "false");
      expect(toggle.parentElement?.tagName).toBe("H3");
      expect(document.getElementById(toggle.getAttribute("aria-controls") ?? "")).toHaveAttribute(
        "hidden",
      );
    }
  });

  it("badges the kind of every row and the signed-in member's own row with You", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    renderPanel();

    await screen.findByText("Access: 3");
    const own = screen.getByRole("button", { name: "Falcon One" });
    expect(within(own).getByText("Member")).toBeInTheDocument();
    expect(within(own).getByText("You")).toBeInTheDocument();
    const other = screen.getByRole("button", { name: "Raven" });
    expect(within(other).getByText("Member")).toBeInTheDocument();
    expect(within(other).queryByText("You")).not.toBeInTheDocument();
    const role = screen.getByRole("button", { name: "Operators" });
    expect(within(role).getByText("Role")).toBeInTheDocument();
    expect(within(role).queryByText("You")).not.toBeInTheDocument();
  });

  it("summarizes the held permissions, Full control alone, the member fingerprint and the role reach", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    renderPanel();

    await screen.findByText("Access: 3");
    const own = screen.getByRole("button", { name: "Falcon One" });
    expect(within(own).getByText("View, Operate")).toBeInTheDocument();
    expect(within(own).getByText("#0000f1")).toBeInTheDocument();
    const full = screen.getByRole("button", { name: "Raven" });
    expect(within(full).getByText("Full control")).toBeInTheDocument();
    expect(within(full).queryByText(/View/)).not.toBeInTheDocument();
    const role = screen.getByRole("button", { name: "Operators" });
    expect(within(role).getByText("View")).toBeInTheDocument();
    expect(within(role).getByText("3 members")).toBeInTheDocument();
  });

  it("reads 1 member for a role with one member", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      accessList({
        users: [],
        roles: [
          { roleId: 9, title: "Solo", description: "", memberCount: 1, permissions: ["sv:view"] },
        ],
      }),
    );
    renderPanel();

    expect(await screen.findByText("1 member")).toBeInTheDocument();
  });

  it("shows an id the catalog does not know as raw code in the summary", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      accessList({
        roles: [],
        users: [
          {
            userId: "30000000-0000-4000-8000-0000000000c3",
            nickname: "Odd",
            detail: "#0000c3",
            permissions: ["sv:view", "sv:future-thing"],
          },
        ],
      }),
    );
    renderPanel();

    const toggle = await screen.findByRole("button", { name: "Odd" });
    const code = within(toggle).getByText("sv:future-thing");
    expect(code.tagName).toBe("CODE");
  });

  it("warns about a subject that holds permissions without View and Full control", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      accessList({
        users: [
          {
            userId: "30000000-0000-4000-8000-0000000000c3",
            nickname: "Ghost",
            detail: "#0000c3",
            permissions: ["sv:operate"],
          },
          {
            userId: "20000000-0000-4000-8000-0000000000a2",
            nickname: "Raven",
            detail: "#0000a2",
            permissions: ["sv", "sv:operate"],
          },
        ],
        roles: [],
      }),
    );
    renderPanel();

    const ghost = await screen.findByRole("button", { name: "Ghost" });
    expect(within(ghost).getByText("Can't see this server")).toBeInTheDocument();
    const warning = screen.getByText(
      "Without View, Ghost cannot see this server. Turn on View as well.",
    );
    expect(warning).not.toBeVisible();
    await user.click(ghost);
    expect(warning).toBeVisible();

    const raven = screen.getByRole("button", { name: "Raven" });
    expect(within(raven).queryByText("Can't see this server")).not.toBeInTheDocument();
    expect(screen.getAllByText(/^Without View, /)).toHaveLength(1);
  });

  it("shows the role description in the open row, wrapped as plain text", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      accessList({
        users: [],
        roles: [
          {
            roleId: 7,
            title: "Operators",
            description: "<b>Runs</b> the servers & more",
            memberCount: 3,
            permissions: ["sv:view"],
          },
        ],
      }),
    );
    renderPanel();

    await user.click(await screen.findByRole("button", { name: "Operators" }));
    const description = screen.getByText("<b>Runs</b> the servers & more");
    expect(description).toBeVisible();
    expect(description.querySelector("b")).toBeNull();
  });

  it("keeps an open row open after the access query refetches", async () => {
    const user = userEvent.setup();
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const server = accessServer();
    const { queryClient } = renderPanel(server);

    await screen.findByText("Access: 3");
    const raven = screen.getByRole("button", { name: "Raven" });
    await user.click(raven);
    expect(raven).toHaveAttribute("aria-expanded", "true");

    await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.access(server.primaryKey) });
    await waitFor(() => expect(access).toHaveBeenCalledTimes(2));

    expect(screen.getByRole("button", { name: "Raven" })).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("button", { name: "Falcon One" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
  });
});

/** A member who can still be given access, as the candidate search sends it. */
const aliceCandidate = {
  userId: "40000000-0000-4000-8000-0000000000d4",
  nickname: "Alice",
  detail: "#0000d4",
};

/**
 * Builds the access list after Alice was given View, in API order (members by nickname).
 *
 * @returns A fresh access list that includes Alice.
 */
function accessWithAlice(): ServerAccess {
  const base = accessList();
  return {
    ...base,
    users: [
      {
        userId: aliceCandidate.userId,
        nickname: aliceCandidate.nickname,
        detail: aliceCandidate.detail,
        permissions: ["sv:view"],
      },
      ...base.users,
    ],
  };
}

/**
 * Renders the panel, types a search into the member picker and chooses the first match.
 *
 * @param user The user-event instance.
 * @returns The render result.
 */
async function chooseAlice(user: ReturnType<typeof userEvent.setup>) {
  const rendered = renderPanel();
  await screen.findByText("Access: 3");
  await user.type(screen.getByRole("combobox", { name: "Find a member" }), "ali");
  await user.click(await screen.findByRole("option", { name: /Alice/ }));
  return rendered;
}

describe("Access grant form", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("lists the first 20 candidates and says so when the search found more", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const many = Array.from({ length: 21 }, (_, index) => ({
      userId: `50000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
      nickname: `Pilot ${String(index).padStart(2, "0")}`,
      detail: `#${index}`,
    }));
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue(many);
    renderPanel();
    await screen.findByText("Access: 3");

    await user.type(screen.getByRole("combobox", { name: "Find a member" }), "pil");

    expect(await screen.findAllByRole("option")).toHaveLength(20);
    expect(screen.queryByRole("option", { name: /Pilot 20/ })).not.toBeInTheDocument();
    expect(screen.getByRole("listbox")).toHaveTextContent(
      "Showing the first 20 matches. Type more to narrow the search.",
    );
  });

  it("shows no narrowing row when the search found 20 candidates or fewer", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    renderPanel();
    await screen.findByText("Access: 3");

    await user.type(screen.getByRole("combobox", { name: "Find a member" }), "ali");

    await screen.findByRole("option", { name: /Alice/ });
    expect(screen.queryByText(/Showing the first/)).not.toBeInTheDocument();
  });

  it("searches members once, 250ms after typing, and lists nickname and detail", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const search = vi
      .spyOn(hostingService, "serverUserCandidates")
      .mockResolvedValue([aliceCandidate]);
    const server = accessServer();
    renderPanel(server);
    await screen.findByText("Access: 3");

    const input = screen.getByRole("combobox", { name: "Find a member" });
    await user.type(input, "ali");
    expect(search).not.toHaveBeenCalled();

    const option = await screen.findByRole("option", { name: /Alice/ });
    expect(search).toHaveBeenCalledTimes(1);
    expect(search).toHaveBeenCalledWith(server.primaryKey, "ali", expect.anything());
    expect(within(option).getByText("Alice")).toBeInTheDocument();
    expect(within(option).getByText("#0000d4")).toBeInTheDocument();
  });

  it("asks for nothing before a character is typed and shows the hint instead", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const search = vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([]);
    renderPanel();
    await screen.findByText("Access: 3");

    expect(screen.getByText("Type to search.")).toBeVisible();
    await new Promise((resolve) => setTimeout(resolve, 300));
    expect(search).not.toHaveBeenCalled();
  });

  it("replaces the input with the chosen token and makes Give access available", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    renderPanel();
    await screen.findByText("Access: 3");

    const submit = screen.getByRole("button", { name: "Give access" });
    expect(submit).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByText("Choose a member first.")).toBeVisible();

    await user.type(screen.getByRole("combobox", { name: "Find a member" }), "ali");
    await user.click(await screen.findByRole("option", { name: /Alice/ }));

    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.getByRole("group", { name: "Find a member" })).toHaveTextContent("Alice");
    expect(screen.getByRole("button", { name: "Give access" })).not.toHaveAttribute(
      "aria-disabled",
    );
    expect(screen.queryByText("Choose a member first.")).not.toBeInTheDocument();
  });

  it("grants View with exactly one call and ignores a second submit while pending", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    let finish: () => void = () => undefined;
    const grant = vi.spyOn(hostingService, "changeServerUserAccess").mockReturnValue(
      new Promise<void>((resolve) => {
        finish = resolve;
      }),
    );
    await chooseAlice(user);
    expect(grant).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Give access" }));
    await user.click(screen.getByRole("button", { name: "Giving access…" }));

    expect(grant).toHaveBeenCalledTimes(1);
    expect(grant).toHaveBeenCalledWith(expect.any(String), aliceCandidate.userId, "sv:view", true);
    expect(screen.getByRole("form", { name: "Give access" })).toHaveAttribute("aria-busy", "true");
    finish();
    await waitFor(() =>
      expect(screen.getByRole("form", { name: "Give access" })).toHaveAttribute(
        "aria-busy",
        "false",
      ),
    );
  });

  it("clears the picker, returns focus to it, announces and shows the new row closed", async () => {
    const user = userEvent.setup();
    const access = vi
      .spyOn(hostingService, "serverAccess")
      .mockResolvedValueOnce(accessList())
      .mockResolvedValue(accessWithAlice());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    await chooseAlice(user);

    await user.click(screen.getByRole("button", { name: "Give access" }));

    const input = await screen.findByRole("combobox", { name: "Find a member" });
    expect(input).toHaveValue("");
    await waitFor(() => expect(input).toHaveFocus());
    const announcement = screen.getByText("Access given to Alice.");
    expect(announcement.closest("[aria-live]")).toHaveAttribute("aria-live", "polite");

    const row = await screen.findByRole("button", { name: "Alice" });
    expect(row).toHaveAttribute("aria-expanded", "false");
    expect(access.mock.calls.length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("Access: 4")).toBeInTheDocument();
  });

  it("refreshes the exact queries of the access change and the candidates of that kind", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    const { queryClient } = await chooseAlice(user);
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");
    const id = accessServer().primaryKey;

    await user.click(screen.getByRole("button", { name: "Give access" }));

    await waitFor(() =>
      expect(invalidate).toHaveBeenCalledWith({
        queryKey: hostingQueryKeys.server(id),
        exact: true,
      }),
    );
    expect(invalidate).toHaveBeenCalledWith({ queryKey: hostingQueryKeys.fleet, exact: true });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: hostingQueryKeys.access(id), exact: true });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["me"], exact: true });
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: [...hostingQueryKeys.access(id), "candidates", "user"],
    });
    expect(invalidate).not.toHaveBeenCalledWith({ queryKey: hostingQueryKeys.fleet });
    expect(invalidate).not.toHaveBeenCalledWith({ queryKey: hostingQueryKeys.server(id) });
  });

  it("offers roles behind the segmented choice and grants View to the chosen role", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    const roles = vi
      .spyOn(hostingService, "serverRoleCandidates")
      .mockResolvedValue([{ roleId: 5, title: "Moderators", description: "", memberCount: 4 }]);
    const grantRole = vi.spyOn(hostingService, "changeServerRoleAccess").mockResolvedValue();
    const grantUser = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const server = accessServer();
    renderPanel(server);
    await screen.findByText("Access: 3");

    await user.type(screen.getByRole("combobox", { name: "Find a member" }), "ali");
    await user.click(await screen.findByRole("option", { name: /Alice/ }));
    expect(screen.getByRole("group", { name: "Find a member" })).toBeInTheDocument();

    const roleRadio = screen.getByRole("radio", { name: "Role" });
    await user.click(roleRadio);

    const input = screen.getByRole("combobox", { name: "Find a role" });
    expect(input).toHaveValue("");
    expect(roleRadio).toHaveFocus();
    expect(screen.queryByRole("group", { name: "Find a member" })).not.toBeInTheDocument();
    expect(screen.getByText("Choose a role first.")).toBeVisible();

    await user.type(input, "mod");
    const option = await screen.findByRole("option", { name: /Moderators/ });
    expect(within(option).getByText("4 members")).toBeInTheDocument();
    expect(roles).toHaveBeenCalledTimes(1);
    expect(roles).toHaveBeenCalledWith(server.primaryKey, "mod", expect.anything());
    await user.click(option);
    await user.click(screen.getByRole("button", { name: "Give access" }));

    await waitFor(() => expect(grantRole).toHaveBeenCalledTimes(1));
    expect(grantRole).toHaveBeenCalledWith(server.primaryKey, 5, "sv:view", true);
    expect(grantUser).not.toHaveBeenCalled();
    expect(await screen.findByText("Access given to Moderators.")).toBeInTheDocument();
  });

  it("never maps the member candidates through the role flavor after a switch", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    vi.spyOn(hostingService, "serverRoleCandidates").mockReturnValue(new Promise(() => undefined));
    renderPanel();
    await screen.findByText("Access: 3");

    await user.type(screen.getByRole("combobox", { name: "Find a member" }), "ali");
    await screen.findByRole("option", { name: /Alice/ });
    await user.click(screen.getByRole("radio", { name: "Role" }));
    await user.type(screen.getByRole("combobox", { name: "Find a role" }), "o");

    expect(await screen.findByText("Searching…")).toBeVisible();
    expect(screen.queryByRole("option")).not.toBeInTheDocument();
  });

  it("keeps Give access unavailable with its reason when View cannot be assigned", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      accessList({ assignablePermissions: ["sv:operate"] }),
    );
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    const grant = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    renderPanel();
    await screen.findByText("Access: 3");

    await user.type(screen.getByRole("combobox", { name: "Find a member" }), "ali");
    await user.click(await screen.findByRole("option", { name: /Alice/ }));

    const submit = screen.getByRole("button", { name: "Give access" });
    expect(submit).toHaveAttribute("aria-disabled", "true");
    expect(
      screen.getByText("You can only give access while you hold View on this server yourself."),
    ).toBeVisible();
    await user.click(submit);
    expect(grant).not.toHaveBeenCalled();
  });

  it("maps a failed grant to a message by status, never the server text, and keeps the choice", async () => {
    const user = userEvent.setup();
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    const grant = vi
      .spyOn(hostingService, "changeServerUserAccess")
      .mockRejectedValueOnce(new ApiError("internal path /srv/x", 403, "forbidden"))
      .mockRejectedValueOnce(new ApiError("internal path /srv/y", 404, "not-found"))
      .mockRejectedValueOnce(new ApiError("internal path /srv/z", 500, "server"));
    await chooseAlice(user);
    const submit = () => screen.getByRole("button", { name: "Give access" });

    await user.click(submit());
    const forbidden = await screen.findByRole("alert");
    expect(forbidden).toHaveTextContent("You may no longer manage access to this server.");
    await waitFor(() => expect(access.mock.calls.length).toBeGreaterThanOrEqual(2));

    await user.click(submit());
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent("That member or role no longer exists."),
    );

    await user.click(submit());
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent("Access was not given. Try again."),
    );
    expect(screen.queryByText(/internal path/)).not.toBeInTheDocument();
    expect(grant).toHaveBeenCalledTimes(3);
    expect(screen.getByRole("group", { name: "Find a member" })).toHaveTextContent("Alice");
    expect(screen.queryByText(/^Access given to/)).not.toBeInTheDocument();
  });

  it("disables the picker and the segmented choice while the grant is pending", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    vi.spyOn(hostingService, "changeServerUserAccess").mockReturnValue(
      new Promise<void>(() => undefined),
    );
    await chooseAlice(user);

    await user.click(screen.getByRole("button", { name: "Give access" }));

    const form = screen.getByRole("form", { name: "Give access" });
    expect(form).toHaveAttribute("aria-busy", "true");
    expect(within(form).getByRole("button", { name: "Giving access…" })).toBeVisible();
    expect(within(form).getByRole("button", { name: "Clear selection" })).toBeDisabled();
    expect(within(form).getByRole("radio", { name: "Member" })).toBeDisabled();
    expect(within(form).getByRole("radio", { name: "Role" })).toBeDisabled();
  });

  it("stages nothing: no review bar and no dialog", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    vi.spyOn(hostingService, "serverUserCandidates").mockResolvedValue([aliceCandidate]);
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    await chooseAlice(user);

    await user.click(screen.getByRole("button", { name: "Give access" }));

    await screen.findByText("Access given to Alice.");
    expect(screen.queryByRole("button", { name: "Review changes" })).not.toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

/** The other member of the switch tests; the signed-in member keeps View and Operate. */
const bobUserId = "50000000-0000-4000-8000-0000000000b5";

/** Every permission of the catalog, as an administrator may assign them. */
const everyPermission = [
  "sv",
  "sv:view",
  "sv:operate",
  "sv:modify",
  "sv:manage-access",
  "sv:delete-data",
  "sv:manage-secrets",
  "sv:reveal-secrets",
];

/**
 * Builds an access list with the signed-in member and Bob, who holds View only, and no role.
 *
 * @param bobPermissions The permissions Bob holds.
 * @returns A fresh access list.
 */
function accessWithBob(bobPermissions: string[] = ["sv:view"]): ServerAccess {
  return accessList({
    allowedPermissions: everyPermission,
    assignablePermissions: everyPermission,
    users: [
      {
        userId: signedInUserId,
        nickname: "Falcon One",
        detail: "#0000f1",
        permissions: ["sv:view", "sv:operate"],
      },
      { userId: bobUserId, nickname: "Bob", detail: "#0000b5", permissions: bobPermissions },
    ],
    roles: [],
  });
}

/**
 * Builds an access list with only the signed-in member, who may assign every permission.
 *
 * @param permissions The permissions the signed-in member holds on the server.
 * @returns A fresh access list.
 */
function ownRowOnly(permissions: string[]): ServerAccess {
  return accessList({
    allowedPermissions: everyPermission,
    assignablePermissions: everyPermission,
    users: [
      {
        userId: signedInUserId,
        nickname: "Falcon One",
        detail: "#0000f1",
        permissions,
      },
    ],
    roles: [],
  });
}

/**
 * Renders the panel and opens the row of a subject.
 *
 * @param user The user-event instance.
 * @param name The name of the subject whose row is opened.
 * @returns The render result and the server.
 */
async function openRow(user: ReturnType<typeof userEvent.setup>, name: string) {
  const server = accessServer();
  const rendered = renderPanel(server);
  await screen.findByText("Access: 2");
  await user.click(screen.getByRole("button", { name }));
  return { ...rendered, server };
}

/**
 * Finds the switch of the open row by its permission label.
 *
 * @param name The permission label.
 * @returns The switch element.
 */
function permissionSwitch(name: string): HTMLElement {
  return screen.getByRole("switch", { name });
}

describe("Access permission switches", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("shows eight switches in the fixed order, on exactly for the held permissions", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    await openRow(user, "Falcon One");

    const switches = screen.getAllByRole("switch");
    expect(switches).toHaveLength(8);
    expect(switches.map((element) => (element as HTMLInputElement).checked)).toEqual([
      false,
      true,
      true,
      false,
      false,
      false,
      false,
      false,
    ]);
    const names = [
      "Full control",
      "View",
      "Operate",
      "Change settings",
      "Manage access",
      "Delete data",
      "Manage credentials",
      "Reveal credentials",
    ];
    for (const [position, name] of names.entries())
      expect(switches[position]).toBe(permissionSwitch(name));
    expect(permissionSwitch("Operate")).toHaveAccessibleDescription(
      "Start, stop and run actions on the server.",
    );
    expect(permissionSwitch("View")).not.toHaveAttribute("aria-checked");
  });

  it("moves the switch at once and sends one grant call without staging", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    const change = vi
      .spyOn(hostingService, "changeServerUserAccess")
      .mockReturnValue(new Promise<void>(() => undefined));
    const { server } = await openRow(user, "Bob");

    expect(permissionSwitch("Operate")).not.toBeChecked();
    await user.click(permissionSwitch("Operate"));

    expect(permissionSwitch("Operate")).toBeChecked();
    expect(change).toHaveBeenCalledTimes(1);
    expect(change).toHaveBeenCalledWith(server.primaryKey, bobUserId, "sv:operate", true);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Review changes" })).not.toBeInTheDocument();
  });

  it("revokes with the same call and the other direction", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    const change = vi
      .spyOn(hostingService, "changeServerUserAccess")
      .mockReturnValue(new Promise<void>(() => undefined));
    const { server } = await openRow(user, "Bob");

    await user.click(permissionSwitch("View"));

    expect(permissionSwitch("View")).not.toBeChecked();
    expect(change).toHaveBeenCalledWith(server.primaryKey, bobUserId, "sv:view", false);
  });

  it("sends role toggles to the role endpoint with the numeric id", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const role = vi
      .spyOn(hostingService, "changeServerRoleAccess")
      .mockReturnValue(new Promise<void>(() => undefined));
    const member = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const server = accessServer();
    renderPanel(server);
    await screen.findByText("Access: 3");
    await user.click(screen.getByRole("button", { name: "Operators" }));

    await user.click(permissionSwitch("Operate"));

    expect(role).toHaveBeenCalledWith(server.primaryKey, 7, "sv:operate", true);
    expect(member).not.toHaveBeenCalled();
  });

  it("rolls back only the failed pair and shows the alert directly under its switch", async () => {
    const user = userEvent.setup();
    const access = vi
      .spyOn(hostingService, "serverAccess")
      .mockResolvedValueOnce(accessWithBob())
      .mockReturnValue(new Promise(() => undefined));
    let reject: (error: Error) => void = () => undefined;
    const change = vi
      .spyOn(hostingService, "changeServerUserAccess")
      .mockReturnValueOnce(
        new Promise<void>((_resolve, fail) => {
          reject = fail;
        }),
      )
      .mockResolvedValue(undefined);
    await openRow(user, "Bob");

    await user.click(permissionSwitch("Operate"));
    await user.click(permissionSwitch("View"));
    expect(permissionSwitch("Operate")).toBeChecked();
    expect(permissionSwitch("View")).not.toBeChecked();
    reject(new ApiError("internal path /srv/x", 500, "server"));

    await waitFor(() => expect(change).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(permissionSwitch("Operate")).not.toBeChecked());
    expect(permissionSwitch("View")).not.toBeChecked();
    const field = permissionSwitch("Operate").closest(".form-field") as HTMLElement;
    expect(within(field).getByRole("alert")).toHaveTextContent(
      "This permission was not changed. Try again.",
    );
    expect(screen.getAllByRole("alert")).toHaveLength(1);
    expect(screen.queryByText(/internal path/)).not.toBeInTheDocument();
    await waitFor(() => expect(access.mock.calls.length).toBeGreaterThanOrEqual(2));
  });

  it("clears the alert of a pair when it is toggled again", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess")
      .mockRejectedValueOnce(new ApiError("x", 500, "server"))
      .mockReturnValue(new Promise<void>(() => undefined));
    await openRow(user, "Bob");

    await user.click(permissionSwitch("Operate"));
    expect(await screen.findByRole("alert")).toBeVisible();
    await user.click(permissionSwitch("Operate"));

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("refreshes the exact queries once, after the last pending toggle settled", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    let finish: () => void = () => undefined;
    const change = vi
      .spyOn(hostingService, "changeServerUserAccess")
      .mockReturnValueOnce(
        new Promise<void>((resolve) => {
          finish = resolve;
        }),
      )
      .mockResolvedValue(undefined);
    const { queryClient, server } = await openRow(user, "Bob");
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");

    await user.click(permissionSwitch("Operate"));
    await user.click(permissionSwitch("Manage access"));
    expect(change).toHaveBeenCalledTimes(1);
    expect(invalidate).not.toHaveBeenCalled();
    finish();

    await waitFor(() => expect(change).toHaveBeenCalledTimes(2));
    const accessKey = hostingQueryKeys.access(server.primaryKey);
    await waitFor(() =>
      expect(invalidate).toHaveBeenCalledWith({ queryKey: accessKey, exact: true }),
    );
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: hostingQueryKeys.server(server.primaryKey),
      exact: true,
    });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: hostingQueryKeys.fleet, exact: true });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["me"], exact: true });
    expect(
      invalidate.mock.calls.filter(
        ([filters]) => JSON.stringify(filters?.queryKey) === JSON.stringify(accessKey),
      ),
    ).toHaveLength(1);
  });

  it("keeps a member whose last permission was switched off in the list until the refetch", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess")
      .mockResolvedValueOnce(accessWithBob())
      .mockReturnValue(new Promise(() => undefined));
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    await openRow(user, "Bob");

    await user.click(permissionSwitch("View"));

    await waitFor(() => expect(permissionSwitch("View")).not.toBeChecked());
    expect(screen.getByRole("button", { name: "Bob" })).toBeInTheDocument();
    expect(screen.getByText("Access: 2")).toBeInTheDocument();
  });
});

describe("Access candidate cache after a change", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("marks cached candidate searches of both kinds stale once a toggle settles", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    const { queryClient, server } = await openRow(user, "Bob");
    const userKey = hostingQueryKeys.accessCandidates(server.primaryKey, "user", "bo");
    const roleKey = hostingQueryKeys.accessCandidates(server.primaryKey, "role", "op");
    queryClient.setQueryData(userKey, { kind: "user", items: [] });
    queryClient.setQueryData(roleKey, { kind: "role", items: [] });
    expect(queryClient.getQueryState(userKey)?.isInvalidated).toBe(false);

    await user.click(permissionSwitch("Operate"));

    await waitFor(() => expect(queryClient.getQueryState(userKey)?.isInvalidated).toBe(true));
    expect(queryClient.getQueryState(roleKey)?.isInvalidated).toBe(true);
    expect(queryClient.getQueryData(userKey)).toEqual({ kind: "user", items: [] });
  });

  it("does not wait for the candidate search to refetch before the settle step continues", async () => {
    const queryClient = new QueryClient();
    const key = hostingQueryKeys.accessCandidates("server-1", "user", "bo");
    // An on-screen search whose refetch never answers, as a slow or retrying request would.
    const observer = new QueryObserver(queryClient, {
      queryKey: key,
      queryFn: () => new Promise<never>(() => undefined),
    });
    const unsubscribe = observer.subscribe(() => undefined);
    try {
      const outcome = await Promise.race([
        invalidateAfterAccessChange(queryClient, "server-1").then(() => "settled"),
        new Promise<string>((resolve) => setTimeout(() => resolve("waited"), 300)),
      ]);

      expect(outcome).toBe("settled");
      expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
    } finally {
      unsubscribe();
      queryClient.clear();
    }
  });
});

describe("Access focus after the last permission is removed", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("moves focus to the list heading when the refetch drops the toggled subject", async () => {
    const user = userEvent.setup();
    const withoutBob = accessWithBob();
    withoutBob.users = withoutBob.users.filter((entry) => entry.userId !== bobUserId);
    vi.spyOn(hostingService, "serverAccess")
      .mockResolvedValueOnce(accessWithBob())
      .mockResolvedValue(withoutBob);
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    await openRow(user, "Bob");

    await user.click(permissionSwitch("View"));

    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Bob" })).not.toBeInTheDocument(),
    );
    expect(
      screen.getByRole("heading", { level: 3, name: "People and roles with access" }),
    ).toHaveFocus();
    expect(screen.getByText("Access: 1")).toBeInTheDocument();
  });

  it("moves focus to the list heading when the refetch lands well after the toggle resolved", async () => {
    const user = userEvent.setup();
    const withoutBob = accessWithBob();
    withoutBob.users = withoutBob.users.filter((entry) => entry.userId !== bobUserId);
    let land: (value: ServerAccess) => void = () => undefined;
    const serverAccess = vi
      .spyOn(hostingService, "serverAccess")
      .mockResolvedValueOnce(accessWithBob())
      .mockReturnValue(
        new Promise<ServerAccess>((resolve) => {
          land = resolve;
        }),
      );
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    await openRow(user, "Bob");

    await user.click(permissionSwitch("View"));
    // The refetch has started and stays unanswered; the toggle call itself has long resolved.
    await waitFor(() => expect(serverAccess).toHaveBeenCalledTimes(2));
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 50));
    });
    // The optimistic cache still lists Bob with nothing switched on, and focus rests on the switch.
    expect(screen.getByRole("button", { name: "Bob" })).toBeInTheDocument();
    expect(permissionSwitch("View")).toHaveFocus();

    await act(async () => {
      land(withoutBob);
    });

    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Bob" })).not.toBeInTheDocument(),
    );
    expect(
      screen.getByRole("heading", { level: 3, name: "People and roles with access" }),
    ).toHaveFocus();
  });

  it("leaves focus alone when the member has moved it elsewhere before the refetch lands", async () => {
    const user = userEvent.setup();
    const withoutBob = accessWithBob();
    withoutBob.users = withoutBob.users.filter((entry) => entry.userId !== bobUserId);
    let land: (value: ServerAccess) => void = () => undefined;
    vi.spyOn(hostingService, "serverAccess")
      .mockResolvedValueOnce(accessWithBob())
      .mockReturnValue(
        new Promise<ServerAccess>((resolve) => {
          land = resolve;
        }),
      );
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    await openRow(user, "Bob");

    await user.click(permissionSwitch("View"));
    await waitFor(() => expect(permissionSwitch("View")).not.toBeChecked());
    await user.click(screen.getByRole("button", { name: "Falcon One" }));
    const falcon = screen.getByRole("button", { name: "Falcon One" });
    expect(falcon).toHaveFocus();
    land(withoutBob);

    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Bob" })).not.toBeInTheDocument(),
    );
    expect(falcon).toHaveFocus();
  });

  it("does not pull focus to the heading when the subject leaves later for another reason", async () => {
    const user = userEvent.setup();
    const withoutBob = accessWithBob();
    withoutBob.users = withoutBob.users.filter((entry) => entry.userId !== bobUserId);
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    const { queryClient, server } = await openRow(user, "Bob");

    await user.click(permissionSwitch("Operate"));
    await waitFor(() => expect(hostingService.changeServerUserAccess).toHaveBeenCalled());
    await waitFor(() => expect(permissionSwitch("Operate")).not.toHaveAttribute("aria-busy"));
    // The toggle has settled and Bob is still listed. Focus rests on the document, as after a click on blank space.
    (document.activeElement as HTMLElement).blur();
    expect(document.body).toHaveFocus();

    // Another manager removes Bob; the change reaches this panel through a refetch the member did not start.
    act(() => {
      queryClient.setQueryData(hostingQueryKeys.access(server.primaryKey), withoutBob);
    });

    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Bob" })).not.toBeInTheDocument(),
    );
    expect(
      screen.getByRole("heading", { level: 3, name: "People and roles with access" }),
    ).not.toHaveFocus();
  });
});

describe("Access focus memory after a delayed refetch", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("forgets the toggled subject once a late refetch keeps it listed", async () => {
    const user = userEvent.setup();
    const withoutBob = accessWithBob();
    withoutBob.users = withoutBob.users.filter((entry) => entry.userId !== bobUserId);
    let land: (value: ServerAccess) => void = () => undefined;
    const serverAccess = vi
      .spyOn(hostingService, "serverAccess")
      .mockResolvedValueOnce(accessWithBob())
      .mockReturnValue(
        new Promise<ServerAccess>((resolve) => {
          land = resolve;
        }),
      );
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue(undefined);
    const { queryClient, server } = await openRow(user, "Bob");

    await user.click(permissionSwitch("Operate"));
    await waitFor(() => expect(serverAccess).toHaveBeenCalledTimes(2));
    await act(async () => {
      land(accessWithBob());
    });
    await waitFor(() => expect(permissionSwitch("Operate")).not.toHaveAttribute("aria-busy"));
    (document.activeElement as HTMLElement).blur();
    expect(document.body).toHaveFocus();

    // Another manager removes Bob later; the member did nothing to cause it.
    act(() => {
      queryClient.setQueryData(hostingQueryKeys.access(server.primaryKey), withoutBob);
    });

    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Bob" })).not.toBeInTheDocument(),
    );
    expect(
      screen.getByRole("heading", { level: 3, name: "People and roles with access" }),
    ).not.toHaveFocus();
  });
});

describe("Access permission rules", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  const otherSevenNames = [
    "View",
    "Operate",
    "Change settings",
    "Manage access",
    "Delete data",
    "Manage credentials",
    "Reveal credentials",
  ];

  it("shows the seven other switches on and disabled while Full control is held", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob(["sv"]));
    await openRow(user, "Bob");

    expect(permissionSwitch("Full control")).toBeChecked();
    expect(permissionSwitch("Full control")).toBeEnabled();
    for (const name of otherSevenNames) {
      expect(permissionSwitch(name)).toBeChecked();
      expect(permissionSwitch(name)).toBeDisabled();
      expect(permissionSwitch(name)).toHaveAccessibleDescription("Included in Full control.");
    }
  });

  it("revokes only Full control and shows the stored grants again", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob(["sv", "sv:operate"]));
    const change = vi
      .spyOn(hostingService, "changeServerUserAccess")
      .mockReturnValue(new Promise<void>(() => undefined));
    const { server } = await openRow(user, "Bob");

    await user.click(permissionSwitch("Full control"));

    expect(change).toHaveBeenCalledTimes(1);
    expect(change).toHaveBeenCalledWith(server.primaryKey, bobUserId, "sv", false);
    expect(permissionSwitch("Full control")).not.toBeChecked();
    expect(permissionSwitch("Operate")).toBeChecked();
    expect(permissionSwitch("Operate")).toBeEnabled();
    expect(permissionSwitch("View")).not.toBeChecked();
    expect(permissionSwitch("View")).toBeEnabled();
  });

  it("disables a switch the member may not assign and says why", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue({
      ...accessWithBob(),
      assignablePermissions: ["sv:view"],
    });
    const change = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    await openRow(user, "Bob");

    expect(permissionSwitch("View")).toBeEnabled();
    expect(permissionSwitch("View")).toHaveAccessibleDescription(
      "See the server, its status and activity.",
    );
    expect(permissionSwitch("Operate")).toBeDisabled();
    expect(permissionSwitch("Operate")).toHaveAccessibleDescription(
      "You can only give or remove permissions you hold yourself.",
    );
    await user.click(permissionSwitch("Operate"));
    expect(change).not.toHaveBeenCalled();
  });

  it("marks a switch busy while its request is pending and sends no second request", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    const change = vi
      .spyOn(hostingService, "changeServerUserAccess")
      .mockReturnValue(new Promise<void>(() => undefined));
    await openRow(user, "Bob");

    await user.click(permissionSwitch("Operate"));
    expect(permissionSwitch("Operate")).toHaveAttribute("aria-busy", "true");
    await user.click(permissionSwitch("Operate"));

    expect(change).toHaveBeenCalledTimes(1);
    expect(permissionSwitch("Operate")).toBeChecked();
    expect(permissionSwitch("Manage access")).not.toHaveAttribute("aria-busy");
    expect(permissionSwitch("Manage access")).toBeEnabled();
  });

  it("asks before the signed-in member removes their own View, with Keep permission focused", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess")
      .mockResolvedValueOnce(accessWithBob())
      .mockReturnValue(new Promise(() => undefined));
    const change = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const { server } = await openRow(user, "Falcon One");

    await user.click(permissionSwitch("View"));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Remove this permission from yourself?")).toBeVisible();
    expect(
      within(dialog).getByText(/You are removing a permission from yourself on/),
    ).toBeVisible();
    expect(within(dialog).getByText("View").tagName).toBe("BDI");
    await waitFor(() =>
      expect(within(dialog).getByRole("button", { name: "Keep permission" })).toHaveFocus(),
    );
    expect(change).not.toHaveBeenCalled();
    expect(document.querySelectorAll<HTMLInputElement>('[role="switch"]')[1]?.checked).toBe(true);

    await user.click(within(dialog).getByRole("button", { name: "Remove permission" }));

    await waitFor(() =>
      expect(change).toHaveBeenCalledWith(server.primaryKey, signedInUserId, "sv:view", false),
    );
    expect(permissionSwitch("View")).not.toBeChecked();
  });

  it("keeps the permission when the member answers Keep permission", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    const change = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    await openRow(user, "Falcon One");

    await user.click(permissionSwitch("View"));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Keep permission" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(change).not.toHaveBeenCalled();
    expect(permissionSwitch("View")).toBeChecked();
  });

  it("asks before the signed-in member removes their own Full control or Manage access", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      ownRowOnly(["sv", "sv:manage-access", "sv:operate"]),
    );
    const change = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    renderPanel();
    await screen.findByText("Access: 1");
    await user.click(screen.getByRole("button", { name: "Falcon One" }));

    await user.click(permissionSwitch("Full control"));
    expect(await screen.findByRole("dialog")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Keep permission" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());

    expect(change).not.toHaveBeenCalled();
    expect(permissionSwitch("Full control")).toBeChecked();
  });

  it("asks for Manage access on the own row when Full control is not held", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      ownRowOnly(["sv:view", "sv:manage-access"]),
    );
    const change = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    renderPanel();
    await screen.findByText("Access: 1");
    await user.click(screen.getByRole("button", { name: "Falcon One" }));

    await user.click(permissionSwitch("Manage access"));

    expect(await screen.findByRole("dialog")).toBeVisible();
    expect(change).not.toHaveBeenCalled();
    expect(permissionSwitch("Manage access")).toBeChecked();
  });

  it("revokes Operate on the own row and grants on the own row without a dialog", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    const change = vi
      .spyOn(hostingService, "changeServerUserAccess")
      .mockReturnValue(new Promise<void>(() => undefined));
    const { server } = await openRow(user, "Falcon One");

    await user.click(permissionSwitch("Operate"));
    expect(change).toHaveBeenCalledWith(server.primaryKey, signedInUserId, "sv:operate", false);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    await user.click(permissionSwitch("Manage access"));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("announces the result politely and keeps focus on the switch", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    await openRow(user, "Bob");

    await user.click(permissionSwitch("Operate"));
    const granted = await screen.findByText("Bob: Operate granted");
    expect(granted.closest("[aria-live]")).toHaveAttribute("aria-live", "polite");
    expect(permissionSwitch("Operate")).toHaveFocus();

    await user.click(permissionSwitch("View"));
    expect(await screen.findByText("Bob: View removed")).toBeInTheDocument();
    expect(permissionSwitch("View")).toHaveFocus();
  });

  it("does not announce a failed change", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockRejectedValue(
      new ApiError("x", 500, "server"),
    );
    await openRow(user, "Bob");

    await user.click(permissionSwitch("Operate"));

    expect(await screen.findByRole("alert")).toBeVisible();
    expect(screen.queryByText(/^Bob: /)).not.toBeInTheDocument();
  });
});

describe("Access remove all", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  const threePermissions = ["sv:view", "sv:operate", "sv:modify"];

  it("orders the revokes in catalog order with the access managing permissions last", () => {
    expect(revocationOrder(["sv:modify", "sv:view", "sv:operate"])).toEqual(threePermissions);
    expect(revocationOrder(["sv", "sv:manage-access", "sv:future", "sv:view", "sv:view"])).toEqual([
      "sv:future",
      "sv:view",
      "sv:manage-access",
      "sv",
    ]);
    expect(revocationOrder([])).toEqual([]);
  });

  it("ends the opened row with a ghost Remove all access button", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob(threePermissions));
    await openRow(user, "Bob");

    const button = screen.getByRole("button", { name: "Remove all access" });
    expect(button).toBeEnabled();
    expect(button).not.toHaveAttribute("aria-disabled");
    const content = button.closest(".access-row__content") as HTMLElement;
    expect(content.lastElementChild).toContainElement(button);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("makes Remove all access unavailable with the reason when a stored permission is not assignable", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue({
      ...accessWithBob(["sv:view", "sv:modify"]),
      assignablePermissions: ["sv:view", "sv:operate"],
    });
    await openRow(user, "Bob");

    const button = screen.getByRole("button", { name: "Remove all access" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).toHaveAccessibleDescription(
      "Some of this access includes permissions you don't hold yourself, so you can't remove all of it.",
    );
    await user.click(button);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("opens a danger dialog with Keep access focused", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob(threePermissions));
    const change = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const { server } = await openRow(user, "Bob");

    await user.click(screen.getByRole("button", { name: "Remove all access" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Remove all access?")).toBeVisible();
    expect(
      within(dialog).getByText(
        `Bob loses every permission on ${server.title}. You can give access again at any time.`,
      ),
    ).toBeVisible();
    await waitFor(() =>
      expect(within(dialog).getByRole("button", { name: "Keep access" })).toHaveFocus(),
    );
    expect(within(dialog).getByRole("button", { name: "Remove access" })).toBeVisible();
    expect(change).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole("button", { name: "Keep access" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(change).not.toHaveBeenCalled();
  });

  it("uses the self variant of the dialog on the own row", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    await openRow(user, "Falcon One");

    await user.click(screen.getByRole("button", { name: "Remove all access" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Remove your own access?")).toBeVisible();
    expect(
      within(dialog).getByText(/You lose every permission you hold directly on/),
    ).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Keep access" })).toBeVisible();
  });

  it("revokes each stored permission once, in order, and is busy meanwhile", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob(threePermissions));
    const release: Array<() => void> = [];
    const change = vi.spyOn(hostingService, "changeServerUserAccess").mockImplementation(
      () =>
        new Promise<void>((resolve) => {
          release.push(resolve);
        }),
    );
    const { server } = await openRow(user, "Bob");
    await user.click(screen.getByRole("button", { name: "Remove all access" }));
    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Remove access" }));

    expect(await within(dialog).findByRole("button", { name: "Removing…" })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
    expect(dialog).toHaveAttribute("aria-busy", "true");
    expect(within(dialog).getByRole("button", { name: "Keep access" })).toBeDisabled();
    await waitFor(() => expect(change).toHaveBeenCalledTimes(1));
    release[0]?.();
    await waitFor(() => expect(change).toHaveBeenCalledTimes(2));
    release[1]?.();
    await waitFor(() => expect(change).toHaveBeenCalledTimes(3));
    release[2]?.();

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(change.mock.calls).toEqual([
      [server.primaryKey, bobUserId, "sv:view", false],
      [server.primaryKey, bobUserId, "sv:operate", false],
      [server.primaryKey, bobUserId, "sv:modify", false],
    ]);
  });

  it("revokes the permissions of a role through the role endpoint", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessList());
    const role = vi.spyOn(hostingService, "changeServerRoleAccess").mockResolvedValue();
    const member = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const server = accessServer();
    renderPanel(server);
    await screen.findByText("Access: 3");
    await user.click(screen.getByRole("button", { name: "Operators" }));
    await user.click(screen.getByRole("button", { name: "Remove all access" }));

    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Remove access" }),
    );

    await waitFor(() => expect(role).toHaveBeenCalledWith(server.primaryKey, 7, "sv:view", false));
    expect(role).toHaveBeenCalledTimes(1);
    expect(member).not.toHaveBeenCalled();
  });

  it("stops at the first failure, keeps the dialog open with the alert and fetches the list again", async () => {
    const user = userEvent.setup();
    const access = vi
      .spyOn(hostingService, "serverAccess")
      .mockResolvedValueOnce(accessWithBob(threePermissions))
      .mockResolvedValue(accessWithBob(["sv:operate", "sv:modify"]));
    const change = vi
      .spyOn(hostingService, "changeServerUserAccess")
      .mockResolvedValueOnce(undefined)
      .mockRejectedValueOnce(new ApiError("internal path /srv/x", 500, "server"))
      .mockResolvedValue(undefined);
    await openRow(user, "Bob");
    await user.click(screen.getByRole("button", { name: "Remove all access" }));
    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Remove access" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "Not all access was removed. Reload the list to see what is left.",
    );
    expect(change).toHaveBeenCalledTimes(2);
    expect(screen.getByRole("dialog")).toBeVisible();
    expect(screen.queryByText(/internal path/)).not.toBeInTheDocument();
    await waitFor(() => expect(access.mock.calls.length).toBeGreaterThanOrEqual(2));
    const row = screen.getByRole("button", { name: "Bob" });
    expect(await within(row).findByText("Operate, Change settings")).toBeVisible();
    expect(screen.queryByText("All access removed for Bob.")).not.toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "Remove access" })).toBeVisible();
  });

  it("says that access can no longer be managed when a revoke answers 403", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob(threePermissions));
    vi.spyOn(hostingService, "changeServerUserAccess").mockRejectedValue(
      new ApiError("nope", 403, "forbidden"),
    );
    await openRow(user, "Bob");
    await user.click(screen.getByRole("button", { name: "Remove all access" }));
    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Remove access" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "You may no longer manage access to this server.",
    );
  });

  it("closes the dialog, announces the result and focuses the list heading on success", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob(threePermissions));
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const { queryClient, server } = await openRow(user, "Bob");
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");
    await user.click(screen.getByRole("button", { name: "Remove all access" }));

    await user.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Remove access" }),
    );

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    const announced = await screen.findByText("All access removed for Bob.");
    expect(announced.closest("[aria-live]")).toHaveAttribute("aria-live", "polite");
    expect(
      screen.getByRole("heading", { level: 3, name: "People and roles with access" }),
    ).toHaveFocus();
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: hostingQueryKeys.access(server.primaryKey),
      exact: true,
    });
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: hostingQueryKeys.server(server.primaryKey),
      exact: true,
    });
  });
});

/** What the server read of the live harness answers: the server, a missing server or a refusal. */
type LiveAnswer = GameServer | null | "forbidden";

/**
 * Renders the panel from a server query, as the workspace does: a refetch that reports `canManageAccess` false makes
 * the panel disappear, and a 404 or 403 leaves nothing of the server to render.
 *
 * @param props.serverId The id of the server; it keys the query the settle step refreshes.
 * @param props.answer Returns what the server read answers at this moment.
 * @param props.onAccessLost The callback the Settings section would hand over.
 */
function LivePanel({
  serverId,
  answer,
  onAccessLost,
}: {
  serverId: string;
  answer: () => LiveAnswer;
  onAccessLost?: (() => void) | undefined;
}) {
  const server = useQuery({
    queryKey: hostingQueryKeys.server(serverId),
    queryFn: async () => {
      const current = answer();
      if (current === "forbidden") throw new ApiError("forbidden", 403, "forbidden");
      return current;
    },
  });
  if (!server.data) return null;
  return <ServerAccessPanel server={server.data} onAccessLost={onAccessLost} />;
}

/**
 * Renders the live harness and waits for the access list of the first server answer.
 *
 * @param answer Returns what the server read answers at this moment.
 * @param server The server of the first answer.
 * @param onAccessLost The callback the Settings section would hand over.
 * @returns The render result.
 */
async function renderLive(answer: () => LiveAnswer, server: GameServer, onAccessLost?: () => void) {
  const rendered = renderWithProviders(
    <SettingsPanelStateProvider>
      <LivePanel serverId={server.primaryKey} answer={answer} onAccessLost={onAccessLost} />
    </SettingsPanelStateProvider>,
  );
  await screen.findByText("Access: 2");
  return rendered;
}

describe("Access lost own access", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("tells the member, removes the panel and calls onAccessLost when Manage access is gone", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const initial = accessServer();
    let current: LiveAnswer = initial;
    const lost = vi.fn();
    await renderLive(() => current, initial, lost);
    await user.click(screen.getByRole("button", { name: "Bob" }));
    current = accessServer({ canManageAccess: false });

    await user.click(permissionSwitch("Operate"));

    expect(
      await screen.findByText("You can no longer manage access to Alpha Team."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 2, name: "Access" })).not.toBeInTheDocument();
    expect(lost).toHaveBeenCalledTimes(1);
  });

  it("completes the settle step after the panel unmounted, for Remove all access on the own row", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const initial = accessServer();
    let current: LiveAnswer = initial;
    const lost = vi.fn();
    await renderLive(() => current, initial, lost);
    await user.click(screen.getByRole("button", { name: "Falcon One" }));
    await user.click(screen.getByRole("button", { name: "Remove all access" }));
    const dialog = await screen.findByRole("dialog");
    current = accessServer({ canManageAccess: false });

    await user.click(within(dialog).getByRole("button", { name: "Remove access" }));

    expect(
      await screen.findByText("You can no longer manage access to Alpha Team."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 2, name: "Access" })).not.toBeInTheDocument();
    expect(lost).toHaveBeenCalledTimes(1);
  });

  it("leaves for the fleet with replace and a toast when the refreshed server read answers 403", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const initial = accessServer();
    let current: LiveAnswer = initial;
    const lost = vi.fn();
    const { router } = await renderLive(() => current, initial, lost);
    await user.click(screen.getByRole("button", { name: "Bob" }));
    current = "forbidden";

    await user.click(permissionSwitch("Operate"));

    expect(await screen.findByText("You can no longer open Alpha Team.")).toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toBe("/command/servers"));
    expect(router.state.historyAction).toBe("REPLACE");
    expect(screen.queryByText("You can no longer manage access to Alpha Team.")).toBeNull();
    expect(lost).not.toHaveBeenCalled();
  });

  it("leaves for the fleet when the refreshed server read resolves to nothing", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const initial = accessServer();
    let current: LiveAnswer = initial;
    const { router } = await renderLive(() => current, initial);
    await user.click(screen.getByRole("button", { name: "Bob" }));
    current = null;

    await user.click(permissionSwitch("View"));

    expect(await screen.findByText("You can no longer open Alpha Team.")).toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toBe("/command/servers"));
    expect(router.state.historyAction).toBe("REPLACE");
  });

  it("neither toasts nor leaves while the member keeps the right to manage access", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    const change = vi.spyOn(hostingService, "changeServerUserAccess").mockResolvedValue();
    const initial = accessServer();
    const lost = vi.fn();
    const { router } = await renderLive(() => initial, initial, lost);
    await user.click(screen.getByRole("button", { name: "Bob" }));

    await user.click(permissionSwitch("Operate"));

    await waitFor(() => expect(change).toHaveBeenCalledTimes(1));
    await screen.findByText("Bob: Operate granted");
    expect(screen.queryByText(/You can no longer/)).toBeNull();
    expect(router.state.location.pathname).toBe("/");
    expect(lost).not.toHaveBeenCalled();
  });

  it("shows the forbidden message under the switch on a 403 and still refreshes the queries", async () => {
    const user = userEvent.setup();
    const access = vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockRejectedValue(
      new ApiError("no longer allowed", 403, "forbidden"),
    );
    const { queryClient, server } = await openRow(user, "Bob");
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");

    await user.click(permissionSwitch("Operate"));

    const field = permissionSwitch("Operate").closest(".form-field") as HTMLElement;
    expect(await within(field).findByRole("alert")).toHaveTextContent(
      "You may no longer manage access to this server.",
    );
    expect(screen.queryByText("This permission was not changed. Try again.")).toBeNull();
    expect(screen.queryByText(/no longer allowed/)).toBeNull();
    expect(permissionSwitch("Operate")).not.toBeChecked();
    await waitFor(() =>
      expect(invalidate).toHaveBeenCalledWith({
        queryKey: hostingQueryKeys.server(server.primaryKey),
        exact: true,
      }),
    );
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["me"], exact: true });
    await waitFor(() => expect(access.mock.calls.length).toBeGreaterThanOrEqual(2));
  });

  it("keeps the generic message for any other failure", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(accessWithBob());
    vi.spyOn(hostingService, "changeServerUserAccess").mockRejectedValue(
      new ApiError("x", 500, "server"),
    );
    await openRow(user, "Bob");

    await user.click(permissionSwitch("Operate"));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "This permission was not changed. Try again.",
    );
  });
});

/**
 * Builds an access list with a given number of plain members and roles, all holding View.
 *
 * @param members The nicknames of the members.
 * @param roles The titles of the roles.
 * @param extraMembers Further members, for example one that cannot see the server.
 * @returns A fresh access list.
 */
function listOf(
  members: string[],
  roles: string[],
  extraMembers: ServerAccess["users"] = [],
): ServerAccess {
  return accessList({
    allowedPermissions: everyPermission,
    assignablePermissions: everyPermission,
    users: [
      ...members.map((nickname, position) => ({
        userId: `70000000-0000-4000-8000-${String(position).padStart(12, "0")}`,
        nickname,
        detail: `#${position}`,
        permissions: ["sv:view"],
      })),
      ...extraMembers,
    ],
    roles: roles.map((title, position) => ({
      roleId: 100 + position,
      title,
      description: "",
      memberCount: 2,
      permissions: ["sv:view"],
    })),
  });
}

/** Six members and two roles: eight subjects, none of which lacks View. */
const eightSubjects = (): ServerAccess =>
  listOf(["Alice", "Alina", "Bob", "Carol", "Dave", "Erin"], ["Operators", "Moderators"]);

/** The number of rows the list shows. */
const shownRows = (): number => document.querySelectorAll(".access-list > li").length;

/** The filter field of the list. */
const filterField = (): HTMLElement => screen.getByRole("searchbox", { name: "Filter the list" });

describe("Access list filter", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    vi.restoreAllMocks();
    await i18n.changeLanguage("en");
  });

  it("offers no filter with seven subjects", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      listOf(["Alice", "Alina", "Bob", "Carol", "Dave", "Erin"], ["Operators"]),
    );
    renderPanel();

    await screen.findByText("Access: 7");
    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
    expect(screen.queryByText(/entr(y|ies) shown/)).not.toBeInTheDocument();
    expect(shownRows()).toBe(7);
  });

  it("offers the filter and a polite result line from eight subjects", async () => {
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(eightSubjects());
    renderPanel();

    await screen.findByText("Access: 8");
    expect(filterField()).toHaveAttribute("placeholder", "Name or kind");
    const line = screen.getByText("8 entries shown");
    expect(line).toHaveAttribute("aria-live", "polite");
    expect(shownRows()).toBe(8);
  });

  it("keeps the rows whose name matches, ignoring case", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(eightSubjects());
    renderPanel();
    await screen.findByText("Access: 8");

    await user.type(filterField(), "ALI");

    expect(screen.getByText("2 entries shown")).toBeInTheDocument();
    expect(shownRows()).toBe(2);
    expect(screen.getByRole("button", { name: "Alice" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Alina" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Bob" })).not.toBeInTheDocument();
  });

  it("matches the translated kind, so Role keeps the roles", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(eightSubjects());
    renderPanel();
    await screen.findByText("Access: 8");

    await user.type(filterField(), "Role");

    expect(screen.getByText("2 entries shown")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Operators" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Moderators" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Alice" })).not.toBeInTheDocument();
  });

  it("reads 1 entry shown for a single match", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(eightSubjects());
    renderPanel();
    await screen.findByText("Access: 8");

    await user.type(filterField(), "carol");

    expect(screen.getByText("1 entry shown")).toBeInTheDocument();
  });

  it("never hides a subject that cannot see the server", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(
      listOf(
        ["Alice", "Bob", "Carol", "Dave", "Erin"],
        ["Operators", "Moderators"],
        [
          {
            userId: "80000000-0000-4000-8000-0000000000a1",
            nickname: "Zed",
            detail: "#zed",
            permissions: ["sv:modify"],
          },
        ],
      ),
    );
    renderPanel();
    await screen.findByText("Access: 8");

    await user.type(filterField(), "alice");

    expect(screen.getByRole("button", { name: "Alice" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Zed" })).toBeInTheDocument();
    expect(screen.getByText("2 entries shown")).toBeInTheDocument();

    await user.clear(filterField());
    await user.type(filterField(), "nobody-is-called-this");
    expect(screen.getByRole("button", { name: "Zed" })).toBeInTheDocument();
    expect(screen.queryByText("Nobody matches")).not.toBeInTheDocument();
    expect(screen.getByText("1 entry shown")).toBeInTheDocument();
  });

  it("shows Nobody matches and a clear action that brings everyone back", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(eightSubjects());
    renderPanel();
    await screen.findByText("Access: 8");

    await user.type(filterField(), "zzz");

    expect(screen.getByText("Nobody matches")).toBeVisible();
    expect(screen.getByText("Clear the filter to see everyone with access.")).toBeVisible();
    expect(screen.getByText("0 entries shown")).toBeInTheDocument();
    expect(shownRows()).toBe(0);
    const empty = screen.getByText("Nobody matches").closest("section") as HTMLElement;

    await user.click(within(empty).getByRole("button", { name: "Clear filter" }));

    expect(filterField()).toHaveValue("");
    expect(filterField()).toHaveFocus();
    expect(screen.queryByText("Nobody matches")).not.toBeInTheDocument();
    expect(shownRows()).toBe(8);
    expect(screen.getByText("8 entries shown")).toBeInTheDocument();
  });

  it("keeps the filter out of the URL", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(eightSubjects());
    const { router } = renderPanel();
    await screen.findByText("Access: 8");

    await user.type(filterField(), "ali");

    expect(router.state.location.search).toBe("");
    expect(router.state.location.hash).toBe("");
  });

  it("keeps an open row open and its switches working while the filter narrows the list", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "serverAccess").mockResolvedValue(eightSubjects());
    vi.spyOn(hostingService, "changeServerUserAccess").mockReturnValue(
      new Promise<void>(() => undefined),
    );
    renderPanel();
    await screen.findByText("Access: 8");
    await user.type(filterField(), "alice");
    await user.click(screen.getByRole("button", { name: "Alice" }));

    expect(permissionSwitch("Operate")).toBeEnabled();
    await user.click(permissionSwitch("Operate"));
    expect(permissionSwitch("Operate")).toBeChecked();
  });
});
