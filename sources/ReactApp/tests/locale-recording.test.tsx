import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach } from "vitest";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { identityService } from "@/api/services";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { CommandCenterLayout } from "@/components/CommandCenterLayout";
import { CommunityLayout } from "@/components/CommunityLayout";
import { hostingService } from "@/features/hosting/services";

const member = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Falcon One",
  eMail: "falcon@example.test",
  avatar: null,
  avatarMimeType: null,
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  steamId64: null,
  discordId: null,
  discordUsername: null,
  claims: [
    {
      primaryKey: 1,
      identifier: "sv",
      value: null,
      title: "Servers",
      category: "Hosting",
      description: "",
      isPrefix: false,
      valueType: null,
    },
  ],
  roles: [],
} as unknown as FullUser;

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

/** Renders a layout with an empty index route, under the providers the app supplies. */
function renderLayout(layout: ReactElement, path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [{ path, element: layout, children: [{ index: true, element: <p>content</p> }] }],
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

/** The two layouts that own a language select, with the route each is mounted on. */
const layouts = [
  ["command center", <CommandCenterLayout key="c" configuration={configuration} />, "/command"],
  ["community", <CommunityLayout key="m" configuration={configuration} />, "/"],
] as const;

describe.each(layouts)("language switch in the %s layout", (_name, layout, path) => {
  beforeAll(async () => {
    // The runtime's own experimental localStorage can shadow jsdom's without working; setCulture writes to it.
    const entries = new Map<string, string>();
    vi.stubGlobal("localStorage", {
      getItem: (key: string) => entries.get(key) ?? null,
      setItem: (key: string, value: string) => void entries.set(key, value),
      removeItem: (key: string) => void entries.delete(key),
      clear: () => entries.clear(),
    });
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    authState.user = member;
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
  });

  afterEach(async () => {
    authState.user = undefined;
    vi.restoreAllMocks();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("records the new language once for a signed-in user", async () => {
    const user = userEvent.setup();
    const record = vi.spyOn(identityService, "recordLocale").mockResolvedValue(undefined);
    renderLayout(layout, path);

    await user.selectOptions(screen.getByLabelText("Language"), "de");

    await waitFor(() => expect(record).toHaveBeenCalledTimes(1));
    expect(record).toHaveBeenCalledWith("de");
    expect(i18n.resolvedLanguage).toBe("de");

    await user.selectOptions(screen.getByLabelText("Sprache"), "en");
    await waitFor(() => expect(record).toHaveBeenCalledTimes(2));
    expect(record).toHaveBeenLastCalledWith("en");
  });

  it("shows nothing and does not throw when the recording fails", async () => {
    const user = userEvent.setup();
    const record = vi
      .spyOn(identityService, "recordLocale")
      .mockRejectedValue(new Error("locale down"));
    const unhandled = vi.fn();
    process.on("unhandledRejection", unhandled);
    try {
      renderLayout(layout, path);

      await user.selectOptions(screen.getByLabelText("Language"), "de");
      await waitFor(() => expect(record).toHaveBeenCalledTimes(1));
      await act(async () => {
        await Promise.resolve();
      });

      expect(screen.queryByRole("alert")).toBeNull();
      expect(screen.queryByText(/locale down/)).toBeNull();
      expect(unhandled).not.toHaveBeenCalled();
      expect(i18n.resolvedLanguage).toBe("de");
    } finally {
      process.off("unhandledRejection", unhandled);
    }
  });
});

describe("language switch while signed out", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    authState.user = undefined;
    vi.restoreAllMocks();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  // The command center layout has no language select without a signed-in user, so only the public layout applies.
  it("sends nothing in the community layout but still switches the language", async () => {
    authState.user = undefined;
    const user = userEvent.setup();
    const record = vi.spyOn(identityService, "recordLocale").mockResolvedValue(undefined);
    renderLayout(<CommunityLayout configuration={configuration} />, "/");

    await user.selectOptions(screen.getByLabelText("Language"), "de");

    expect(i18n.resolvedLanguage).toBe("de");
    expect(record).not.toHaveBeenCalled();
  });
});
