import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { CommandCenterLayout } from "@/components/CommandCenterLayout";
import { CommunityLayout } from "@/components/CommunityLayout";
import { Component as AuthenticationPage } from "@/pages/AuthenticationPage";
import { hostingService } from "@/features/hosting/services";

const authState = vi.hoisted(() => ({
  user: undefined as FullUser | undefined,
  providers: { steam: true, discord: true },
  providersLoading: false,
  login: vi.fn(),
  logout: vi.fn(),
}));

vi.mock("@/auth/auth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/auth/auth")>()),
  useAuth: () => ({
    user: authState.user,
    isAuthenticated: Boolean(authState.user),
    isLoading: false,
    providers: authState.providers,
    providersLoading: authState.providersLoading,
    login: authState.login,
    logout: authState.logout,
  }),
}));

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

/** Every native button inside the container, including the ones in a closed dialog. */
function allButtons(container: HTMLElement): HTMLButtonElement[] {
  return Array.from(container.querySelectorAll("button"));
}

describe("community layout", () => {
  beforeAll(async () => {
    // The runtime's own experimental localStorage can shadow jsdom's without working; setCulture writes to it.
    const entries = new Map<string, string>();
    vi.stubGlobal("localStorage", {
      getItem: (key: string) => entries.get(key) ?? null,
      setItem: (key: string, value: string) => void entries.set(key, value),
      removeItem: (key: string) => void entries.delete(key),
      clear: () => entries.clear(),
    });
    // jsdom has no matchMedia; the layouts only ask whether the narrow navigation applies.
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: false,
      media: query,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    await i18nReady;
  });

  beforeEach(async () => {
    authState.user = undefined;
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("renders the language switch as a labelled select with the padded chevron", () => {
    renderLayout(<CommunityLayout configuration={configuration} />, "/");

    const select = screen.getByLabelText("Language");
    expect(select.tagName).toBe("SELECT");
    expect(select).toHaveClass("form-field__control--select");
    expect(select.parentElement).toHaveClass("form-field__select");
  });

  it("renders every button of the layout as a shared action control", () => {
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");

    const buttons = allButtons(container);
    expect(buttons.length).toBeGreaterThan(0);
    for (const button of buttons) expect(button).toHaveClass("button");
  });

  it("keeps the skip link as the first tab stop and points it at the main landmark", async () => {
    const user = userEvent.setup();
    renderLayout(<CommunityLayout configuration={configuration} />, "/");

    await user.tab();

    const skipLink = screen.getByRole("link", { name: "Skip to content" });
    expect(skipLink).toHaveFocus();
    expect(skipLink).toHaveAttribute("href", "#main-content");
    expect(document.getElementById("main-content")).not.toBeNull();
  });

  it("names the sign-in action in English and in German", async () => {
    renderLayout(<CommunityLayout configuration={configuration} />, "/");
    expect(screen.getByRole("button", { name: "Sign in" })).toBeInTheDocument();

    await act(async () => {
      await i18n.changeLanguage("de");
    });

    expect(screen.getByRole("button", { name: "Anmelden" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Sign in" })).toBeNull();
    expect(screen.getByLabelText("Sprache")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Zum Inhalt springen" })).toBeInTheDocument();
  });

  it("shows no English text in German, including the closed sign-in dialog", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");

    const dialog = container.querySelector("dialog");
    expect(dialog).not.toBeNull();
    const dismiss = within(dialog!).getByText("Nicht anmelden", { selector: "button" });
    expect(dismiss).toHaveClass("button");
    expect(screen.getByText("Gemeinschaftseinsätze, offen koordiniert.")).toBeInTheDocument();
    expect(container.textContent).not.toContain("Cancel");
    expect(container.textContent).not.toContain("Community operations");
  });

  it("opens the navigation from its toggle and closes it with Escape, keeping focus on the toggle", async () => {
    const user = userEvent.setup();
    renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const toggle = screen.getByLabelText("Toggle navigation");
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(toggle).toHaveAttribute("aria-controls", "community-navigation");

    await user.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");

    await user.keyboard("{Escape}");
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(toggle).toHaveFocus();
  });

  it("returns focus to the toggle when Escape is pressed inside the open navigation", async () => {
    const user = userEvent.setup();
    renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const toggle = screen.getByLabelText("Toggle navigation");

    await user.click(toggle);
    const nav = screen.getByRole("navigation", { name: "Community navigation" });
    within(nav).getAllByRole("link")[1]!.focus();
    await user.keyboard("{Escape}");

    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(toggle).toHaveFocus();
  });

  it("moves focus to the first navigation link when the menu opens and makes the page behind it inert", async () => {
    const user = userEvent.setup();
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const main = container.querySelector("main#main-content")!;
    const footer = container.querySelector("footer.site-footer")!;
    expect(main).not.toHaveAttribute("inert");
    expect(footer).not.toHaveAttribute("inert");

    await user.click(screen.getByLabelText("Toggle navigation"));

    const nav = screen.getByRole("navigation", { name: "Community navigation" });
    expect(within(nav).getAllByRole("link")[0]).toHaveFocus();
    expect(main).toHaveAttribute("inert");
    expect(footer).toHaveAttribute("inert");

    await user.keyboard("{Escape}");
    expect(main).not.toHaveAttribute("inert");
    expect(footer).not.toHaveAttribute("inert");
  });

  it("cycles Tab between the toggle and the navigation links while the menu is open", async () => {
    const user = userEvent.setup();
    renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const toggle = screen.getByLabelText("Toggle navigation");
    await user.click(toggle);
    const links = within(
      screen.getByRole("navigation", { name: "Community navigation" }),
    ).getAllByRole("link");
    const lastLink = links[links.length - 1]!;

    lastLink.focus();
    await user.tab();
    expect(toggle).toHaveFocus();

    await user.tab({ shift: true });
    expect(lastLink).toHaveFocus();
  });

  it("returns focus to the toggle when a navigation link closes the menu", async () => {
    const user = userEvent.setup();
    renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const toggle = screen.getByLabelText("Toggle navigation");

    await user.click(toggle);
    await user.click(
      // The test router only has the index route, so the link that stays on it keeps the layout mounted.
      within(screen.getByRole("navigation", { name: "Community navigation" })).getByRole("link", {
        name: "Home",
      }),
    );

    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(toggle).toHaveFocus();
  });

  it("does not take focus that the user moved outside the menu when Escape closes it", async () => {
    const user = userEvent.setup();
    renderLayout(<CommunityLayout configuration={configuration} />, "/");
    await user.click(screen.getByLabelText("Toggle navigation"));
    const language = screen.getByLabelText("Language");

    language.focus();
    await user.keyboard("{Escape}");

    expect(screen.getByLabelText("Toggle navigation")).toHaveAttribute("aria-expanded", "false");
    expect(language).toHaveFocus();
  });

  it("keeps focus in the open menu after a pointer click on its surface", async () => {
    const user = userEvent.setup();
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const toggle = screen.getByLabelText("Toggle navigation");
    await user.click(toggle);
    const nav = container.querySelector<HTMLElement>("nav#community-navigation")!;
    const links = within(nav).getAllByRole("link");
    const skipLink = screen.getByRole("link", { name: "Skip to content" });

    // The nav element itself is not focusable, so the click leaves focus on the body.
    await user.click(nav);
    await user.tab();
    expect(toggle).toHaveFocus();

    await user.click(nav);
    await user.tab({ shift: true });
    expect(links[links.length - 1]).toHaveFocus();
    expect(skipLink).not.toHaveFocus();
  });

  it("leaves Tab to the browser while a modal dialog is open over the open menu", async () => {
    const user = userEvent.setup();
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const toggle = screen.getByLabelText("Toggle navigation");
    await user.click(toggle);
    const nav = container.querySelector<HTMLElement>("nav#community-navigation")!;
    const dialog = document.createElement("dialog");
    const inside = document.createElement("button");
    dialog.append(inside);
    document.body.append(dialog);
    dialog.showModal();

    try {
      // A blank click leaves focus on the body, which is where the drawer cycle would take Tab over.
      await user.click(nav);
      expect(document.querySelector("dialog[open]")).toBe(dialog);
      expect(document.body).toHaveFocus();

      const notPrevented = fireEvent.keyDown(document, { key: "Tab" });

      expect(notPrevented).toBe(true);
      expect(toggle).not.toHaveFocus();
      expect(nav.contains(document.activeElement)).toBe(false);
    } finally {
      dialog.close();
      dialog.remove();
    }
  });

  it("keeps cycling Tab in the open menu once the dialog over it is closed", async () => {
    const user = userEvent.setup();
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const toggle = screen.getByLabelText("Toggle navigation");
    await user.click(toggle);
    const nav = container.querySelector<HTMLElement>("nav#community-navigation")!;
    const dialog = document.createElement("dialog");
    document.body.append(dialog);
    dialog.showModal();
    dialog.close();
    dialog.remove();

    await user.click(nav);
    await user.tab();

    expect(toggle).toHaveFocus();
  });

  it("takes the skip link and the scrim out of the tab order while the menu is open", async () => {
    const user = userEvent.setup();
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const skipLink = screen.getByRole("link", { name: "Skip to content" });
    expect(skipLink).not.toHaveAttribute("inert");

    await user.click(screen.getByLabelText("Toggle navigation"));

    expect(skipLink).toHaveAttribute("inert");
    expect(container.querySelector(".navigation-scrim")).toHaveAttribute("tabindex", "-1");

    await user.click(container.querySelector(".navigation-scrim")!);
    expect(screen.getByLabelText("Toggle navigation")).toHaveAttribute("aria-expanded", "false");
    expect(skipLink).not.toHaveAttribute("inert");
  });

  it("leaves Tab from a middle popover item to the profile menu while the menu is open", async () => {
    authState.user = member;
    const user = userEvent.setup();
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const toggle = screen.getByLabelText("Toggle navigation");
    await user.click(toggle);
    await user.click(container.querySelector(".account-profile__trigger")!);
    const popover = container.querySelector<HTMLElement>("#account-profile-panel")!;
    expect(within(popover).getByRole("button", { name: "Close profile menu" })).toHaveFocus();

    for (let press = 0; press < 2; press += 1) {
      await user.tab();
      expect(popover.contains(document.activeElement)).toBe(true);
      expect(toggle).not.toHaveFocus();
    }
  });

  it("closes one layer per Escape: the profile popover first, then the menu", async () => {
    authState.user = member;
    const user = userEvent.setup();
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");
    const toggle = screen.getByLabelText("Toggle navigation");
    const main = container.querySelector("main#main-content")!;
    await user.click(toggle);
    const trigger = container.querySelector<HTMLElement>(".account-profile__trigger")!;
    await user.click(trigger);
    expect(container.querySelector("#account-profile-panel")).not.toBeNull();

    await user.keyboard("{Escape}");
    expect(container.querySelector("#account-profile-panel")).toBeNull();
    expect(trigger).toHaveFocus();
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(main).toHaveAttribute("inert");

    await user.keyboard("{Escape}");
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(main).not.toHaveAttribute("inert");
  });

  it("keeps the header actions in their own Tab order while the menu is open", async () => {
    const user = userEvent.setup();
    renderLayout(<CommunityLayout configuration={configuration} />, "/");
    await user.click(screen.getByLabelText("Toggle navigation"));

    await user.click(screen.getByLabelText("Language"));
    await user.tab();

    expect(screen.getByRole("button", { name: "Sign in" })).toHaveFocus();
  });

  it("marks the current navigation item with aria-current", () => {
    renderLayout(<CommunityLayout configuration={configuration} />, "/");

    const nav = screen.getByRole("navigation", { name: "Community navigation" });
    expect(within(nav).getByRole("link", { name: "Home" })).toHaveAttribute("aria-current", "page");
  });

  it("opens the sign-in dialog from the sign-in button", async () => {
    const user = userEvent.setup();
    const { container } = renderLayout(<CommunityLayout configuration={configuration} />, "/");

    await user.click(screen.getByRole("button", { name: "Sign in" }));

    const dialog = container.querySelector("dialog")!;
    expect(dialog.open).toBe(true);
    expect(within(dialog).getByRole("button", { name: "Steam" })).toHaveClass("button");
    expect(within(dialog).getByRole("button", { name: "Discord" })).toHaveClass("button");
    expect(within(dialog).getByRole("button", { name: "Don't sign in" })).toHaveClass("button");
  });

  beforeEach(() => {
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
  });
});

describe("command center layout", () => {
  beforeAll(async () => {
    const entries = new Map<string, string>();
    vi.stubGlobal("localStorage", {
      getItem: (key: string) => entries.get(key) ?? null,
      setItem: (key: string, value: string) => void entries.set(key, value),
      removeItem: (key: string) => void entries.delete(key),
      clear: () => entries.clear(),
    });
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: false,
      media: query,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    await i18nReady;
  });

  beforeEach(async () => {
    authState.user = member;
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  afterEach(async () => {
    authState.user = undefined;
    vi.restoreAllMocks();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("renders the language switch as a labelled select with the padded chevron", () => {
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");

    const select = screen.getByLabelText("Language");
    expect(select.tagName).toBe("SELECT");
    expect(select).toHaveClass("form-field__control--select");
    expect(select.parentElement).toHaveClass("form-field__select");
  });

  it("renders every button of the layout as a shared action control", () => {
    const { container } = renderLayout(
      <CommandCenterLayout configuration={configuration} />,
      "/command",
    );

    const buttons = allButtons(container);
    expect(buttons.length).toBeGreaterThan(0);
    for (const button of buttons) expect(button).toHaveClass("button");
    expect(container.querySelector("select:not(.form-field__control)")).toBeNull();
  });

  it("keeps the command skip link as the first tab stop and points it at the content landmark", async () => {
    const user = userEvent.setup();
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");

    await user.tab();

    const skipLink = screen.getByRole("link", { name: "Skip to content" });
    expect(skipLink).toHaveFocus();
    expect(skipLink).toHaveAttribute("href", "#command-content");
    expect(document.getElementById("command-content")).not.toBeNull();
  });

  it("marks the current navigation item with aria-current", () => {
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");

    const nav = screen.getByRole("navigation", { name: "Command Center navigation" });
    const current = within(nav)
      .getAllByRole("link")
      .filter((link) => link.getAttribute("aria-current") === "page");
    expect(current).toHaveLength(1);
    expect(current[0]).toHaveTextContent("Overview");
    expect(current[0]).toHaveClass("active");
  });

  it("wraps navigation labels in the drawer instead of truncating them", () => {
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");

    const nav = screen.getByRole("navigation", { name: "Command Center navigation" });
    const label = within(nav).getAllByRole("link")[0]!.querySelector("span")!;
    const style = getComputedStyle(label);
    expect(style.whiteSpace).toBe("normal");
    expect(style.textOverflow).not.toBe("ellipsis");
  });

  it("opens and closes the drawer from its toggles and returns focus to the opener on Escape", async () => {
    const user = userEvent.setup();
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");
    const toggle = screen.getByLabelText("Open Command Center navigation");
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(toggle).toHaveAttribute("aria-controls", "command-navigation");

    await user.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");

    const close = screen.getAllByLabelText("Close Command Center navigation");
    expect(close.length).toBeGreaterThan(0);
    close[0]!.focus();
    await user.keyboard("{Escape}");

    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(toggle).toHaveFocus();
  });

  it("moves focus to the close button when the drawer opens and makes the covered column inert", async () => {
    const user = userEvent.setup();
    const { container } = renderLayout(
      <CommandCenterLayout configuration={configuration} />,
      "/command",
    );
    const main = container.querySelector(".command-main")!;
    const drawer = document.getElementById("command-navigation")!;
    expect(main).not.toHaveAttribute("inert");

    await user.click(screen.getByLabelText("Open Command Center navigation"));

    expect(
      within(drawer).getByRole("button", { name: "Close Command Center navigation" }),
    ).toHaveFocus();
    expect(main).toHaveAttribute("inert");
  });

  it("cycles Tab and Shift+Tab inside the open drawer", async () => {
    const user = userEvent.setup();
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");
    const drawer = document.getElementById("command-navigation")!;
    await user.click(screen.getByLabelText("Open Command Center navigation"));
    const close = within(drawer).getByRole("button", { name: "Close Command Center navigation" });
    const links = within(drawer).getAllByRole("link");
    const lastLink = links[links.length - 1]!;

    lastLink.focus();
    await user.tab();
    expect(close).toHaveFocus();

    await user.tab({ shift: true });
    expect(lastLink).toHaveFocus();
  });

  it("keeps focus in the open drawer after a pointer click on its surface", async () => {
    const user = userEvent.setup();
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");
    const drawer = document.getElementById("command-navigation")!;
    await user.click(screen.getByLabelText("Open Command Center navigation"));
    const close = within(drawer).getByRole("button", { name: "Close Command Center navigation" });
    const links = within(drawer).getAllByRole("link");
    const skipLink = screen.getByRole("link", { name: "Skip to content" });
    const brand = drawer.querySelector(".command-nav__brand strong")!;

    // The brand text is not focusable, so the click leaves focus on the body.
    await user.click(brand);
    await user.tab({ shift: true });
    expect(links[links.length - 1]).toHaveFocus();

    await user.click(brand);
    await user.tab();
    expect(close).toHaveFocus();
    expect(skipLink).not.toHaveFocus();
  });

  it("keeps the drawer open when an inner layer already handled Escape", async () => {
    const user = userEvent.setup();
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");
    const toggle = screen.getByLabelText("Open Command Center navigation");
    await user.click(toggle);

    // An inner layer listens on the document and marks the Escape it consumed as handled.
    const innerLayer = (event: KeyboardEvent) => {
      if (event.key === "Escape") event.preventDefault();
    };
    document.addEventListener("keydown", innerLayer);
    try {
      await user.keyboard("{Escape}");
      expect(toggle).toHaveAttribute("aria-expanded", "true");
    } finally {
      document.removeEventListener("keydown", innerLayer);
    }

    await user.keyboard("{Escape}");
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(toggle).toHaveFocus();
  });

  it("takes the skip link and the scrim out of the tab order while the drawer is open", async () => {
    const user = userEvent.setup();
    const { container } = renderLayout(
      <CommandCenterLayout configuration={configuration} />,
      "/command",
    );
    const skipLink = screen.getByRole("link", { name: "Skip to content" });
    expect(skipLink).not.toHaveAttribute("inert");

    await user.click(screen.getByLabelText("Open Command Center navigation"));

    expect(skipLink).toHaveAttribute("inert");
    expect(container.querySelector(".command-scrim")).toHaveAttribute("tabindex", "-1");

    await user.click(container.querySelector(".command-scrim")!);
    expect(screen.getByLabelText("Open Command Center navigation")).toHaveAttribute(
      "aria-expanded",
      "false",
    );
    expect(skipLink).not.toHaveAttribute("inert");
  });

  it("returns focus to the toggle when the drawer closes with Escape, the close button or a link", async () => {
    const user = userEvent.setup();
    const { container } = renderLayout(
      <CommandCenterLayout configuration={configuration} />,
      "/command",
    );
    const toggle = screen.getByLabelText("Open Command Center navigation");
    const drawer = document.getElementById("command-navigation")!;
    const main = container.querySelector(".command-main")!;

    await user.click(toggle);
    await user.keyboard("{Escape}");
    expect(toggle).toHaveFocus();
    expect(main).not.toHaveAttribute("inert");

    await user.click(toggle);
    await user.click(
      within(drawer).getByRole("button", { name: "Close Command Center navigation" }),
    );
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(toggle).toHaveFocus();

    await user.click(toggle);
    await user.click(within(drawer).getAllByRole("link")[0]!);
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(toggle).toHaveFocus();
  });

  it("shows one h1 above the forbidden state for a viewer without command access", () => {
    authState.user = undefined;
    renderLayout(<CommandCenterLayout configuration={configuration} />, "/command");

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Command Center");
    expect(screen.getByRole("status")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Back to site" })).toHaveAttribute("href", "/");
  });
});

describe("authentication page", () => {
  /** Renders the page on its route with an optional query string. */
  function renderPage(search = "", action = "login") {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const router = createMemoryRouter(
      [{ path: "/authentication/:action", element: <AuthenticationPage /> }],
      { initialEntries: [`/authentication/${action}${search}`] },
    );
    return render(
      <I18nextProvider i18n={i18n}>
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      </I18nextProvider>,
    );
  }

  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    authState.user = undefined;
    authState.providers = { steam: true, discord: true };
    authState.providersLoading = false;
    authState.login.mockReset();
    authState.logout.mockReset();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  afterEach(async () => {
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("renders one PageHeader h1 above provider buttons that are shared controls", () => {
    const { container } = renderPage();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Sign in");
    const buttons = allButtons(container);
    expect(buttons).toHaveLength(2);
    for (const button of buttons) expect(button).toHaveClass("button");
    expect(container.querySelector(".page-stack")).not.toBeNull();
  });

  it("starts the provider login with the safe local return path", async () => {
    const user = userEvent.setup();
    renderPage("?returnUrl=%2Fnext%2Fwiki");

    await user.click(screen.getByRole("button", { name: /Steam/ }));
    await user.click(screen.getByRole("button", { name: /Discord/ }));

    expect(authState.login).toHaveBeenNthCalledWith(1, "steam", "/next/wiki");
    expect(authState.login).toHaveBeenNthCalledWith(2, "discord", "/next/wiki");
  });

  it("replaces a foreign return address with the default path before login", async () => {
    const user = userEvent.setup();
    renderPage(`?returnUrl=${encodeURIComponent("https://evil.example/next/steal")}`);

    await user.click(screen.getByRole("button", { name: /Steam/ }));

    expect(authState.login).toHaveBeenCalledOnce();
    expect(authState.login).toHaveBeenCalledWith("steam", "/next/");
  });

  it("names the provider hints from locale keys in German", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });
    renderPage();

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Anmelden");
    expect(screen.queryByText("Use your Steam community identity")).toBeNull();
  });

  it("shows the shared loading state while the providers load", () => {
    authState.providersLoading = true;
    authState.providers = { steam: false, discord: false };
    renderPage();

    expect(screen.getByRole("status")).toHaveTextContent("Loading...");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("shows one h1 above the loading state while the logout runs, and logs out once", () => {
    renderPage("?returnUrl=%2Fnext%2F", "logout");

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Logout");
    expect(screen.getByRole("status")).toHaveTextContent("Loading...");
    expect(authState.logout).toHaveBeenCalledOnce();
    expect(authState.logout).toHaveBeenCalledWith("/next/");
  });
});
