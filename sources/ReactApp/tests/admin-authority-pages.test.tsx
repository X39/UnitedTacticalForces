import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import type { ReactElement } from "react";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { Component as ClaimsPage } from "@/features/admin/ClaimsPage";
import { Component as RoleDetailPage } from "@/features/admin/RoleDetailPage";
import { Component as RolesPage } from "@/features/admin/RolesPage";
import * as authority from "@/features/admin/authority";
import * as roles from "@/features/admin/roles";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isLoading: false,
    isAuthenticated: Boolean(authState.user),
  }),
}));

const administrator = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Authority admin",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  claims: [{ identifier: "adm:rls", value: null }],
  roles: [],
} as FullUser;

const outsider = {
  ...administrator,
  nickname: "Plain member",
  claims: [],
} as FullUser;

const snapshot: authority.AuthoritySnapshot = {
  memberCount: 3,
  roleCount: 1,
  claimCount: 2,
  roles: [
    {
      role: { primaryKey: 7, title: "Operations", description: "Runs the operations desk" },
      claims: [{ primaryKey: 1, identifier: "evt:edit", title: "Edit events" }],
      memberCount: 2,
    },
  ],
  claims: [
    {
      claim: { primaryKey: 1, identifier: "evt:edit", title: "Edit events" },
      directMemberCount: 0,
      roleCount: 1,
      roleTitles: ["Operations"],
    },
    {
      claim: { primaryKey: 2, identifier: "evt:view", title: "View events" },
      directMemberCount: 0,
      roleCount: 0,
      roleTitles: [],
    },
  ],
};

const roleMembers: roles.RoleMembers = {
  role: { primaryKey: 7, title: "Operations" },
  assignedMemberCount: 1,
  availableMemberCount: 1,
  assignedMembers: [{ primaryKey: "m-1", nickname: "Alice", isVerified: true }],
  availableMembers: [{ primaryKey: "m-2", nickname: "Bob", isVerified: false }],
};

function renderRoute(path: string, pattern: string, element: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter([{ path: pattern, element }], { initialEntries: [path] });
  return render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

function renderRoleDetail(roleId: string) {
  return renderRoute(`/command/roles/${roleId}`, "/command/roles/:roleId", <RoleDetailPage />);
}

function renderClaims() {
  return renderRoute("/command/claims", "/command/claims", <ClaimsPage />);
}

function renderRoles() {
  return renderRoute("/command/roles", "/command/roles", <RolesPage />);
}

describe("authority pages", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = administrator;
    vi.spyOn(authority, "loadAuthoritySnapshot").mockResolvedValue(snapshot);
    vi.spyOn(roles, "loadRoleMembers").mockResolvedValue(roleMembers);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  describe("role detail", () => {
    it("renders one heading above the forbidden state and no role data", async () => {
      authState.user = outsider;
      renderRoleDetail("7");

      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Role detail");
      const forbidden = screen.getByRole("status");
      expect(forbidden).toHaveTextContent("Role administration is unavailable");
      expect(
        screen.getByRole("heading", { level: 1 }).compareDocumentPosition(forbidden) &
          Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
      expect(screen.queryByText("Operations")).not.toBeInTheDocument();
      expect(authority.loadAuthoritySnapshot).not.toHaveBeenCalled();
      expect(roles.loadRoleMembers).not.toHaveBeenCalled();
    });

    it("renders one heading above the not-found state for an invalid role id", async () => {
      renderRoleDetail("not-a-number");

      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("heading", { name: "Role not found" })).toBeInTheDocument();
      expect(screen.getByRole("link", { name: "Back to roles" })).toHaveAttribute(
        "href",
        "/command/roles",
      );
      expect(authority.loadAuthoritySnapshot).not.toHaveBeenCalled();
    });

    it("renders one heading above the not-found state for an unknown role id", async () => {
      renderRoleDetail("999");

      expect(await screen.findByRole("heading", { name: "Role not found" })).toBeInTheDocument();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("link", { name: "Back to roles" })).toBeInTheDocument();
    });

    it("renders the loaded role with shared controls and a role-specific dismiss label", async () => {
      const user = userEvent.setup();
      renderRoleDetail("7");

      expect(
        await screen.findByRole("heading", { level: 1, name: "Operations - Role detail" }),
      ).toBeInTheDocument();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(await screen.findByRole("button", { name: "Remove Edit events" })).toHaveClass(
        "button",
      );
      expect(screen.getByRole("button", { name: "Add View events" })).toHaveClass("button");
      expect(screen.getByRole("textbox", { name: /^Title/ })).toHaveValue("Operations");
      expect(screen.getByRole("searchbox", { name: "Search claims" })).toBeInTheDocument();

      await user.click(screen.getByRole("button", { name: "Delete role" }));
      const dialog = await screen.findByRole("dialog");
      expect(within(dialog).getByRole("button", { name: "Keep role" })).toBeInTheDocument();
      expect(within(dialog).getByRole("button", { name: "Delete role" })).toBeInTheDocument();
      expect(within(dialog).queryByRole("button", { name: "Cancel" })).not.toBeInTheDocument();
    });

    it("names how to clear a claim filter that matches nothing", async () => {
      const user = userEvent.setup();
      renderRoleDetail("7");

      await user.type(await screen.findByRole("searchbox", { name: "Search claims" }), "zzz");

      await waitFor(() =>
        expect(screen.getByRole("heading", { name: "No claims match" })).toBeInTheDocument(),
      );
      expect(screen.getByText("Clear the search to see all claims.")).toBeInTheDocument();
    });
  });

  describe("roles list", () => {
    it("renders one heading named like its navigation label above the forbidden state", () => {
      authState.user = outsider;
      renderRoles();

      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Roles");
      expect(screen.getByRole("status")).toHaveTextContent("Role administration is unavailable");
      expect(authority.loadAuthoritySnapshot).not.toHaveBeenCalled();
    });

    it("keeps submitting the search to the loader", async () => {
      const user = userEvent.setup();
      renderRoles();

      expect(await screen.findByRole("heading", { level: 1, name: "Roles" })).toBeInTheDocument();
      await user.type(await screen.findByRole("searchbox", { name: "Search roles" }), "ops");
      await user.click(screen.getByRole("button", { name: "Search" }));

      await waitFor(() =>
        expect(authority.loadAuthoritySnapshot).toHaveBeenLastCalledWith("ops", expect.anything()),
      );
    });

    it("opens a create form of shared fields whose dismiss names its effect", async () => {
      const user = userEvent.setup();
      renderRoles();

      await user.click(await screen.findByRole("button", { name: "Create role" }));

      expect(await screen.findByRole("textbox", { name: /^Title/ })).toBeRequired();
      expect(screen.getByRole("textbox", { name: "Description" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Don't create role" })).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Cancel" })).not.toBeInTheDocument();
    });

    it("names how to clear a filter that matches no role", async () => {
      const user = userEvent.setup();
      renderRoles();

      await user.click(
        within(await screen.findByRole("group", { name: "Role filters" })).getByRole("button", {
          name: /^Empty/,
        }),
      );

      expect(await screen.findByRole("heading", { name: "No roles match" })).toBeInTheDocument();
      expect(
        screen.getByText("Clear the search or filters, or create a role."),
      ).toBeInTheDocument();
    });

    it("shows the load failure with a retry that fetches again", async () => {
      const user = userEvent.setup();
      vi.mocked(authority.loadAuthoritySnapshot).mockRejectedValueOnce(new Error("Gateway down"));
      renderRoles();

      expect(await screen.findByText("Gateway down")).toBeInTheDocument();
      expect(screen.queryByRole("heading", { name: "No roles match" })).not.toBeInTheDocument();
      await user.click(screen.getByRole("button", { name: "Try again" }));

      expect(await screen.findByText("Operations")).toBeInTheDocument();
      expect(authority.loadAuthoritySnapshot).toHaveBeenCalledTimes(2);
    });
  });

  describe("claims", () => {
    it("renders one heading named like its navigation label above the forbidden state", () => {
      authState.user = outsider;
      renderClaims();

      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Claims");
      expect(screen.getByRole("status")).toHaveTextContent(
        "Your account does not currently expose claim administration.",
      );
      expect(authority.loadAuthoritySnapshot).not.toHaveBeenCalled();
    });

    it("lists the claims with counted filter chips and a labelled search", async () => {
      renderClaims();

      const chips = await screen.findByRole("group", { name: "Claim filters" });
      expect(await within(chips).findByRole("button", { name: /^All 2$/ })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
      expect(within(chips).getByRole("button", { name: /^Unused 1$/ })).toBeInTheDocument();
      expect(screen.getByRole("searchbox", { name: "Search claims" })).toBeInTheDocument();
    });

    it("names how to clear a filter that matches no claim", async () => {
      const user = userEvent.setup();
      renderClaims();

      await user.click(
        within(await screen.findByRole("group", { name: "Claim filters" })).getByRole("button", {
          name: /^Direct/,
        }),
      );

      expect(await screen.findByRole("heading", { name: "No claims match" })).toBeInTheDocument();
      expect(screen.getByText("Clear the search or filters to see all claims.")).toBeVisible();
    });

    it("shows the load failure with a retry and no misleading empty list", async () => {
      const user = userEvent.setup();
      vi.mocked(authority.loadAuthoritySnapshot).mockRejectedValueOnce(new Error("Gateway down"));
      renderClaims();

      expect(await screen.findByText(/Gateway down/)).toBeInTheDocument();
      expect(screen.queryByRole("heading", { name: "No claims match" })).not.toBeInTheDocument();
      await user.click(screen.getByRole("button", { name: "Try again" }));

      expect(await screen.findAllByText("Edit events")).not.toHaveLength(0);
      expect(authority.loadAuthoritySnapshot).toHaveBeenCalledTimes(2);
    });
  });
});
