import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach } from "vitest";
import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { Component as ContentHubPage } from "@/features/admin/ContentHubPage";
import { Component as PublicPageDetailPage } from "@/features/admin/PublicPageDetailPage";
import { Component as PublicPageEditorPage } from "@/features/admin/PublicPageEditorPage";
import { Component as PublicPagesPage } from "@/features/admin/PublicPagesPage";
import { renderWithProviders } from "./helpers/renderWithProviders";

const auth = vi.hoisted(() => ({ claims: [] as string[] }));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-4000-8000-000000000001",
      nickname: "Editor",
      isBanned: false,
      isVerified: true,
      isDeleted: false,
      isVereinMember: false,
      claims: auth.claims.map((identifier) => ({ identifier, value: null })),
      roles: [],
    },
    isLoading: false,
    isAuthenticated: true,
  }),
}));

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

function pageDto() {
  return {
    primaryKey: 7,
    slug: "operations",
    isDeleted: false,
    timeStampCreated: "2026-08-01T08:00:00Z",
    timeStampUpdated: "2026-08-14T08:00:00Z",
    revision: { primaryKey: 5, version: 3, title: "Operations", isPublished: false },
    revisions: [
      { primaryKey: 5, version: 3, title: "Operations", isPublished: false },
      {
        primaryKey: 4,
        version: 2,
        title: "Operations",
        isPublished: true,
        timeStampPublished: "2026-08-02T08:00:00Z",
      },
    ],
  };
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** Installs a fetch stub that answers by request path and records every call. */
function stubFetch(answer: (path: string, method: string) => Response | Promise<Response>) {
  const fetchMock = vi.fn(async (input: URL | string, init?: RequestInit) =>
    answer(String(input), init?.method ?? "GET"),
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function mount(element: ReactElement, route: string, path: string) {
  return renderWithProviders(element, { route, path });
}

/**
 * Asserts the routed page renders exactly one page header h1 with the given text and that it is the first h1 of
 * the document. A Markdown preview may add its own content h1 below it; that rendering is a closed non-goal.
 */
function expectOneHeader(name: string) {
  const headerHeadings = document.querySelectorAll(".page-header h1");
  expect(headerHeadings).toHaveLength(1);
  expect(headerHeadings[0]).toHaveTextContent(name);
  expect(screen.getAllByRole("heading", { level: 1 })[0]).toBe(headerHeadings[0]);
}

describe("content administration pages", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    initializeApiClient(configuration);
    auth.claims = ["content:pages"];
  });

  afterEach(async () => {
    vi.unstubAllGlobals();
    await act(async () => i18n.changeLanguage("en"));
  });

  describe("content hub", () => {
    it("titles the page like its navigation entry in both languages", async () => {
      mount(<ContentHubPage />, "/command/content", "/command/content");
      expectOneHeader("Content and documents");
      expect(screen.getByRole("link", { name: /website pages/i })).toBeVisible();
      expect(document.querySelector("button.button, input.button")).toBeNull();
      expect(document.querySelectorAll(".page-header .kicker")).toHaveLength(0);
    });

    it("renders the German title", async () => {
      await act(async () => i18n.changeLanguage("de"));
      mount(<ContentHubPage />, "/command/content", "/command/content");
      expectOneHeader("Inhalte und Dokumente");
    });

    it("keeps the header above the forbidden state", () => {
      auth.claims = [];
      mount(<ContentHubPage />, "/command/content", "/command/content");
      expectOneHeader("Content and documents");
      expect(screen.getByRole("status")).toHaveTextContent("No content area available");
    });
  });

  describe("public pages list", () => {
    const route = "/command/content/pages";

    it("renders one header above the forbidden state and sends no request", () => {
      auth.claims = [];
      const fetchMock = stubFetch(() => json([]));
      mount(<PublicPagesPage />, route, route);
      expectOneHeader("Public pages");
      expect(screen.getByRole("status")).toHaveTextContent("Public page access required");
      expect(fetchMock).not.toHaveBeenCalled();
    });

    it("renders one header above the loading state", () => {
      stubFetch(() => new Promise<Response>(() => undefined));
      mount(<PublicPagesPage />, route, route);
      expectOneHeader("Public pages");
      expect(screen.getByRole("status")).toHaveTextContent("Loading public pages...");
    });

    it("renders one header above an error with a retry that refetches", async () => {
      const user = userEvent.setup();
      const fetchMock = stubFetch(() => json({ message: "Down" }, 500));
      mount(<PublicPagesPage />, route, route);

      const alert = await screen.findByRole("alert");
      expectOneHeader("Public pages");
      expect(screen.queryByText("No public pages yet")).not.toBeInTheDocument();
      await user.click(within(alert).getByRole("button", { name: "Try again" }));
      await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(2));
    });

    it("renders an empty state that names the next step", async () => {
      stubFetch(() => json([]));
      mount(<PublicPagesPage />, route, route);
      expect(await screen.findByText("No public pages yet")).toBeVisible();
      expectOneHeader("Public pages");
    });

    it("names the row actions after the page and uses only shared controls", async () => {
      stubFetch(() => json([pageDto()]));
      mount(<PublicPagesPage />, route, route);
      expect(await screen.findByRole("link", { name: "Open: Operations" })).toBeVisible();
      expect(screen.getByRole("link", { name: /^Edit.*: Operations$/ })).toBeVisible();
      expect(document.querySelector("input.button")).toBeNull();
    });
  });

  describe("public page detail", () => {
    const route = "/command/content/pages/operations";
    const path = "/command/content/pages/:slug";

    it("renders one header above the forbidden state", () => {
      auth.claims = [];
      mount(<PublicPageDetailPage />, route, path);
      expectOneHeader("Public page");
      expect(screen.getByRole("status")).toHaveTextContent("Public page access required");
    });

    it("renders one header above the loading state", () => {
      stubFetch(() => new Promise<Response>(() => undefined));
      mount(<PublicPageDetailPage />, route, path);
      expectOneHeader("Public page");
      expect(screen.getByRole("status")).toHaveTextContent("Loading public pages...");
    });

    it("renders one header above an error with a retry", async () => {
      const user = userEvent.setup();
      const fetchMock = stubFetch(() => json({ message: "Down" }, 500));
      mount(<PublicPageDetailPage />, route, path);

      const alert = await screen.findByRole("alert");
      expectOneHeader("Public page");
      await user.click(within(alert).getByRole("button", { name: "Try again" }));
      await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(2));
    });

    it("renders one header above the not found state with the create action", async () => {
      stubFetch(() => json({}, 404));
      mount(<PublicPageDetailPage />, route, path);

      expect(await screen.findByText("Page not found")).toBeVisible();
      expectOneHeader("Public page");
      expect(screen.getByRole("link", { name: "Create first revision" })).toHaveAttribute(
        "href",
        "/command/content/pages/operations/edit",
      );
    });

    it("sends no publication request until the named revision action is pressed", async () => {
      const user = userEvent.setup();
      const fetchMock = stubFetch(() => json(pageDto()));
      mount(<PublicPageDetailPage />, route, path);

      const publish = await screen.findByRole("button", { name: "Publish: Revision 3" });
      expect(screen.getByRole("button", { name: "Unpublish: Revision 2" })).toBeVisible();
      expect(fetchMock.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(0);

      await user.click(publish);
      await waitFor(() =>
        expect(fetchMock.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(1),
      );
      expect(String(fetchMock.mock.calls.find(([, init]) => init?.method === "POST")?.[0])).toMatch(
        /\/public-pages\/admin\/revisions\/5\/publish$/,
      );
    });
  });

  describe("GDPR content editor", () => {
    it("renders shared buttons and context-named fields and toggles its preview", async () => {
      const user = userEvent.setup();
      stubFetch(() => json({}, 404));
      mount(
        <PublicPageEditorPage />,
        "/command/content/pages/gdpr/edit",
        "/command/content/pages/:slug/edit",
      );

      const toggle = await screen.findByRole("button", { name: "Show preview" });
      expect(toggle).toHaveClass("button", "button--ghost");
      expect(toggle).toHaveAttribute("aria-expanded", "false");

      const kicker = screen.getByRole("textbox", { name: "Intro: Kicker" });
      expect(kicker).toHaveClass("form-field__control");
      expect(screen.getByRole("textbox", { name: "Intro: Description" })).toHaveClass(
        "form-field__control--multiline",
      );

      const before = (kicker as HTMLInputElement).value;
      await user.type(kicker, "!");
      expect(kicker).toHaveValue(`${before}!`);
      await user.click(toggle);
      expect(screen.getByRole("button", { name: "Hide preview" })).toHaveAttribute(
        "aria-expanded",
        "true",
      );
    });
  });

  describe("public page editor states", () => {
    const route = "/command/content/pages/home/edit";
    const path = "/command/content/pages/:slug/edit";

    it("renders one header above the forbidden state", () => {
      auth.claims = [];
      mount(<PublicPageEditorPage />, route, path);
      expectOneHeader("Edit public page: home");
      expect(screen.getByRole("status")).toHaveTextContent("Public page access required");
    });

    it("renders one header above the loading state", () => {
      stubFetch(() => new Promise<Response>(() => undefined));
      mount(<PublicPageEditorPage />, route, path);
      expectOneHeader("Edit public page: home");
      expect(screen.getByRole("status")).toHaveTextContent("Loading public pages...");
    });

    it("opens the editor for a page that does not exist yet", async () => {
      stubFetch(() => json({}, 404));
      mount(<PublicPageEditorPage />, route, path);

      expect(await screen.findByLabelText("Title")).toBeVisible();
      expectOneHeader("Edit public page: home");
      expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    });

    it("renders one header above an error with a retry", async () => {
      const user = userEvent.setup();
      const fetchMock = stubFetch(() => json({ message: "Down" }, 500));
      mount(<PublicPageEditorPage />, route, path);

      const alert = await screen.findByRole("alert");
      expectOneHeader("Edit public page: home");
      await user.click(within(alert).getByRole("button", { name: "Try again" }));
      await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(2));
    });
  });
});
