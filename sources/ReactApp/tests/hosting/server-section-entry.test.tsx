import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, within } from "@testing-library/react";
import type { ReactElement } from "react";
import { beforeEach } from "vitest";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import type { PublicConfiguration } from "@/app/config";
import { AppToastProvider } from "@/components/AppToasts";
import { CommandCenterLayout } from "@/components/CommandCenterLayout";
import { CommunityLayout } from "@/components/CommunityLayout";
import { hostingService } from "@/features/hosting/services";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isAuthenticated: Boolean(authState.user),
    isLoading: false,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

const configuration = {
  Api: { BaseUrl: "/api" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: false, ReactBasePath: "/" },
} as PublicConfiguration;

/** Builds a signed-in member holding exactly one bare claim of the given identifier. */
function userWithClaim(identifier: string): FullUser {
  return {
    primaryKey: "10000000-0000-4000-8000-000000000001",
    nickname: "Member",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims: [
      {
        primaryKey: 1,
        identifier,
        value: null,
        title: identifier,
        category: "Hosting",
        description: "",
        isPrefix: false,
        valueType: null,
      },
    ],
    roles: [],
  } as unknown as FullUser;
}

function renderLayout(layout: ReactElement, path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [{ path, element: layout, children: [{ index: true, element: <p>section content</p> }] }],
    { initialEntries: [path] },
  );
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

function communityNavigation(): HTMLElement {
  return screen.getByRole("navigation", { name: i18n.t("Shared_CommunityNav_Aria") });
}

function commandNavigation(): HTMLElement {
  return screen.getByRole("navigation", { name: i18n.t("Shared_CommandCenter_NavigationAria") });
}

const serverClaims = ["sv", "sv:view"] as const;

describe("server section entry for bare sv and sv:view members (ACC-02, D-01, D-02)", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
  });

  afterEach(async () => {
    authState.user = undefined;
    vi.restoreAllMocks();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it.each(serverClaims)(
    "shows the Command Center link in the community header for a bare %s member",
    (claim) => {
      authState.user = userWithClaim(claim);
      renderLayout(<CommunityLayout configuration={configuration} />, "/");

      const link = within(communityNavigation()).getByRole("link", { name: "Command Center" });

      expect(link).toHaveAttribute("href", "/command");
    },
  );

  it.each(serverClaims)(
    "adds no Servers link to the community header for a bare %s member",
    (claim) => {
      authState.user = userWithClaim(claim);
      renderLayout(<CommunityLayout configuration={configuration} />, "/");

      expect(
        within(communityNavigation()).queryByRole("link", { name: /^servers?$/i }),
      ).not.toBeInTheDocument();
    },
  );

  it.each(serverClaims)(
    "shows the Servers item in the command navigation for a bare %s member in both languages",
    async (claim) => {
      authState.user = userWithClaim(claim);
      renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");

      const english = within(commandNavigation()).getByRole("link", { name: "Servers" });
      expect(english).toHaveAttribute("href", "/command/servers");

      await act(async () => {
        await i18n.changeLanguage("de");
      });

      const german = within(commandNavigation()).getByRole("link", { name: "Server" });
      expect(german).toHaveAttribute("href", "/command/servers");
    },
  );

  it("hides the Servers item from a member whose claim grants no server access", () => {
    authState.user = userWithClaim("mp");
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");

    const navigation = commandNavigation();

    expect(within(navigation).queryByRole("link", { name: /^servers?$/i })).not.toBeInTheDocument();
    expect(
      within(navigation)
        .getAllByRole("link")
        .some((link) => link.getAttribute("href") === "/command/servers"),
    ).toBe(false);
  });

  it.each(["en", "de"] as const)(
    "resolves the navigation label and the fleet heading to the same text in %s",
    async (language) => {
      await act(async () => {
        await i18n.changeLanguage(language);
      });

      expect(i18n.t("Hosting_Nav_Servers")).toBe(i18n.t("Hosting_Servers_Title"));
      expect(i18n.t("Hosting_Nav_Servers")).toBe(language === "en" ? "Servers" : "Server");
    },
  );
});
