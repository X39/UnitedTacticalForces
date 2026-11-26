import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { Component as MemberDetailPage } from "@/features/admin/MemberDetailPage";
import { Component as MembersPage } from "@/features/admin/MembersPage";
import * as members from "@/features/admin/members";
import type { CommunityMember, CommunitySnapshot, MemberDetail } from "@/features/admin/members";
import { renderWithProviders } from "./helpers/renderWithProviders";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isLoading: false,
    isAuthenticated: Boolean(authState.user),
  }),
}));

const rosterReader = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Roster reader",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  claims: [{ identifier: "verein:members", value: null }],
  roles: [],
} as FullUser;

const unverified: CommunityMember = {
  primaryKey: "20000000-0000-4000-8000-000000000001",
  nickname: "Alpha",
  isVerified: false,
};
const banned: CommunityMember = {
  primaryKey: "20000000-0000-4000-8000-000000000002",
  nickname: "Bravo",
  isVerified: true,
  isBanned: true,
};
const deleted: CommunityMember = {
  primaryKey: "20000000-0000-4000-8000-000000000003",
  nickname: "Charlie",
  isVerified: true,
  isDeleted: true,
};
const verified: CommunityMember = {
  primaryKey: "20000000-0000-4000-8000-000000000004",
  nickname: "Delta",
  isVerified: true,
  vereinMembershipStatus: "Active",
};

function snapshotOf(rows: CommunityMember[]): CommunitySnapshot {
  return {
    memberCount: rows.length,
    verifiedMemberCount: 1,
    unverifiedMemberCount: 1,
    bannedMemberCount: 1,
    deletedMemberCount: 1,
    activeVereinMemberCount: 1,
    suspendedVereinMemberCount: 0,
    endedVereinMemberCount: 0,
    roleCount: 2,
    claimCount: 4,
    members: rows,
  };
}

function rowOf(nickname: string): HTMLElement {
  return screen.getByRole("row", { name: new RegExp(nickname) });
}

describe("member list page", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = rosterReader;
    vi.spyOn(members, "loadCommunitySnapshot").mockResolvedValue(
      snapshotOf([unverified, banned, deleted, verified]),
    );
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  it("takes every badge tone from the account lexicon", async () => {
    const user = userEvent.setup();
    renderWithProviders(<MembersPage />);

    await screen.findByRole("row", { name: /Alpha/ });
    expect(within(rowOf("Alpha")).getByText("Unverified")).toHaveClass("status-badge--neutral");
    expect(within(rowOf("Bravo")).getByText("Banned")).toHaveClass("status-badge--danger");
    expect(within(rowOf("Charlie")).getByText("Deleted")).toHaveClass("status-badge--danger");

    await user.click(screen.getByRole("button", { name: "Verified" }));
    expect(within(rowOf("Delta")).getByText("Verified")).toHaveClass("status-badge--success");
  });

  it("renders one h1 that equals the navigation label", async () => {
    renderWithProviders(<MembersPage />);

    expect(await screen.findAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Members" })).toBeVisible();
  });

  it("renders the h1 above the loading state", async () => {
    vi.spyOn(members, "loadCommunitySnapshot").mockReturnValue(new Promise(() => undefined));
    renderWithProviders(<MembersPage />);

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(await screen.findByText("Loading members")).toBeVisible();
  });

  it("renders the h1 above the forbidden state and reads nothing", () => {
    authState.user = undefined;
    renderWithProviders(<MembersPage />);

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent(
      "Your account does not currently expose member administration.",
    );
    expect(members.loadCommunitySnapshot).not.toHaveBeenCalled();
  });

  it("shows the error state with a retry that refetches and no misleading empty list", async () => {
    const user = userEvent.setup();
    vi.spyOn(members, "loadCommunitySnapshot").mockRejectedValue(new Error("down"));
    renderWithProviders(<MembersPage />);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Could not load members: down");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.queryByText("No members match")).not.toBeInTheDocument();

    await user.click(within(alert).getByRole("button", { name: "Try again" }));
    await waitFor(() => expect(members.loadCommunitySnapshot).toHaveBeenCalledTimes(2));
  });

  it("names how to clear the filter when nothing matches", async () => {
    vi.spyOn(members, "loadCommunitySnapshot").mockResolvedValue(snapshotOf([]));
    renderWithProviders(<MembersPage />);

    expect(
      await screen.findByRole("heading", { level: 2, name: "No members match" }),
    ).toBeVisible();
    expect(screen.getByText("Clear the search or filters to see all members.")).toBeVisible();
  });

  it("filters through chips of one pressed group and keeps every control shared", async () => {
    const user = userEvent.setup();
    const { container } = renderWithProviders(<MembersPage />);

    await screen.findByRole("row", { name: /Alpha/ });
    const group = screen.getByRole("group", { name: "Member filters" });
    expect(within(group).getByRole("button", { name: "Attention" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );

    await user.click(within(group).getByRole("button", { name: "Verein" }));
    expect(within(group).getByRole("button", { name: "Verein" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(screen.getByRole("row", { name: /Delta/ })).toBeVisible();
    expect(screen.queryByRole("row", { name: /Alpha/ })).not.toBeInTheDocument();
    for (const button of container.querySelectorAll("button")) {
      expect(button.className.split(" ")).toContain("button");
    }
  });

  it("opens a member from the manage link", async () => {
    renderWithProviders(<MembersPage />);

    await screen.findByRole("row", { name: /Alpha/ });
    expect(within(rowOf("Alpha")).getByRole("link", { name: "Manage" })).toHaveAttribute(
      "href",
      "/command/members/20000000-0000-4000-8000-000000000001",
    );
  });
});

const detailId = "20000000-0000-4000-8000-000000000009";

const globalAdministrator = {
  primaryKey: "10000000-0000-4000-8000-000000000002",
  nickname: "Administrator",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: true,
  claims: [{ identifier: "adm", value: null }],
  roles: [],
} as FullUser;

/** Builds the detail of the member under test; the options switch off the membership or the verification. */
function detailOf(options: { membership?: boolean; verified?: boolean } = {}): MemberDetail {
  const membership = {
    status: "Active",
    memberNumber: "UTF-1",
    notes: "",
    timeStampStarted: "2026-01-01T00:00:00Z",
  } as NonNullable<MemberDetail["membership"]>;
  return {
    user: {
      primaryKey: detailId,
      nickname: "Falcon",
      eMail: "falcon@example.test",
      isVerified: options.verified ?? true,
      isBanned: false,
      isDeleted: false,
    } as MemberDetail["user"],
    ...(options.membership === false ? {} : { membership }),
    assignedRoles: [{ primaryKey: 7, title: "Medic", description: "Heals" }],
    assignedClaims: [],
    authority: { roles: [], claims: [] } as unknown as NonNullable<MemberDetail["authority"]>,
  };
}

/** Lists the tone of every status badge in the container that shows exactly the given text. */
function badgeTones(container: HTMLElement, text: string): string[] {
  return [...container.querySelectorAll(".status-badge")]
    .filter((badge) => badge.textContent === text)
    .map((badge) => /status-badge--(\w+)/.exec(badge.className)?.[1] ?? "none");
}

function renderDetail(id: string = detailId) {
  return renderWithProviders(<MemberDetailPage />, {
    path: "/command/members/:userId",
    route: `/command/members/${id}`,
  });
}

describe("member detail page", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = globalAdministrator;
    vi.spyOn(members, "loadMemberDetail").mockResolvedValue(detailOf());
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  it("takes account and membership tones from the lexicon", async () => {
    const { container } = renderDetail();

    await screen.findByRole("heading", { level: 1, name: "Falcon" });
    expect(badgeTones(container, "Verified")).toEqual(["success", "success"]);
    expect(badgeTones(container, "Active")).toEqual(["success", "success"]);
  });

  it("shows a neutral badge for a member without formal membership and an unverified account", async () => {
    vi.spyOn(members, "loadMemberDetail").mockResolvedValue(
      detailOf({ membership: false, verified: false }),
    );
    const { container } = renderDetail();

    await screen.findByRole("heading", { level: 1, name: "Falcon" });
    expect(badgeTones(container, "No formal membership")).toEqual(["neutral", "neutral"]);
    expect(badgeTones(container, "Unverified")).toEqual(["neutral", "neutral"]);
  });

  it.each([
    [
      "en",
      {
        profile: "Discard profile changes",
        membership: "Discard membership changes",
        ban: "Don't ban member",
        endMembership: "Keep membership",
      },
    ],
    [
      "de",
      {
        profile: "Profiländerungen verwerfen",
        membership: "Mitgliedschaftsänderungen verwerfen",
        ban: "Mitglied nicht sperren",
        endMembership: "Mitgliedschaft behalten",
      },
    ],
  ])("names every dismiss in %s and never reads Cancel", async (language, labels) => {
    const user = userEvent.setup();
    await act(async () => i18n.changeLanguage(language));
    renderDetail();

    const nickname = await screen.findByRole("textbox", {
      name: language === "en" ? /^Nickname/ : /^Spitzname/,
    });
    await user.type(nickname, "x");
    const discardProfile = screen.getByRole("button", { name: labels.profile });
    expect(discardProfile).toBeEnabled();
    await user.click(discardProfile);
    expect(nickname).toHaveValue("Falcon");

    const status = screen.getByRole("combobox", {
      name: language === "en" ? "Membership status" : "Mitgliedsstatus",
    });
    await user.selectOptions(status, "Suspended");
    await user.click(screen.getByRole("button", { name: labels.membership }));
    expect(status).toHaveValue("Active");

    await user.click(
      screen.getByRole("checkbox", { name: language === "en" ? "Banned" : "Gesperrt" }),
    );
    await user.click(
      screen.getByRole("button", {
        name: language === "en" ? "Save profile" : "Profil speichern",
      }),
    );
    expect(await screen.findByRole("button", { name: labels.ban })).toBeVisible();
    await user.click(screen.getByRole("button", { name: labels.ban }));

    await user.selectOptions(status, "Ended");
    await user.click(
      screen.getByRole("button", {
        name: language === "en" ? "Save membership" : "Mitgliedschaft speichern",
      }),
    );
    expect(await screen.findByRole("button", { name: labels.endMembership })).toBeVisible();

    expect(screen.queryByRole("button", { name: /^(Cancel|Abbrechen)$/ })).not.toBeInTheDocument();
  });

  it("renders one h1 above the forbidden state and reads nothing", () => {
    authState.user = undefined;
    renderDetail();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(
      screen.getByText("Your account does not currently expose member administration."),
    ).toBeVisible();
    expect(members.loadMemberDetail).not.toHaveBeenCalled();
  });

  it("renders one h1 above the not-found state for an invalid identifier", () => {
    renderDetail("not-a-guid");

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 2, name: "Member not found" })).toBeVisible();
    expect(screen.getByRole("link", { name: "Back to roster" })).toHaveAttribute(
      "href",
      "/command/members",
    );
    expect(members.loadMemberDetail).not.toHaveBeenCalled();
  });

  it("renders one h1 above the not-found state for an unknown member", async () => {
    vi.spyOn(members, "loadMemberDetail").mockResolvedValue(undefined);
    renderDetail();

    expect(
      await screen.findByRole("heading", { level: 2, name: "Member not found" }),
    ).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("renders one h1 above the loading state", () => {
    vi.spyOn(members, "loadMemberDetail").mockReturnValue(new Promise(() => undefined));
    renderDetail();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("status")).toHaveTextContent("Loading member");
  });

  it("shows the load error with a retry that refetches", async () => {
    const user = userEvent.setup();
    vi.spyOn(members, "loadMemberDetail").mockRejectedValue(new Error("down"));
    renderDetail();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Could not load member: down");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);

    await user.click(within(alert).getByRole("button", { name: "Try again" }));
    await waitFor(() => expect(members.loadMemberDetail).toHaveBeenCalledTimes(2));
  });
});
