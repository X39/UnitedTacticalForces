import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { Component as PublicPageEditorPage } from "@/features/admin/PublicPageEditorPage";

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-4000-8000-000000000001",
      nickname: "Editor",
      isBanned: false,
      isVerified: true,
      isDeleted: false,
      isVereinMember: false,
      claims: [{ identifier: "content:pages", value: null }],
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

function existingPage() {
  return {
    slug: "operations",
    timeStampUpdated: "2026-08-14T08:00:00Z",
    revision: {
      primaryKey: 5,
      version: 3,
      title: "Operations",
      summary: "Current operations",
      heroKicker: "Command",
      heroTitle: "Operations",
      heroDescription: "Current operational briefings",
      heroImageAssetFk: "20000000-0000-4000-8000-000000000002",
      heroImagePath: "media-assets/20000000-0000-4000-8000-000000000002/content",
      markdown: "# Operations\n\nReady.",
      isPublished: true,
      timeStampCreated: "2026-08-14T08:00:00Z",
    },
    revisions: [{ version: 3 }],
  };
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      {
        path: "/command/content/pages/:slug/edit",
        element: <PublicPageEditorPage />,
      },
      { path: "/command/content/pages/:slug", element: <h1>Detail</h1> },
      { path: "*", element: <div /> },
    ],
    { initialEntries: ["/command/content/pages/operations/edit"] },
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

describe("certified public-page editor workflow", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => initializeApiClient(configuration));

  afterEach(async () => {
    vi.unstubAllGlobals();
    await act(async () => i18n.changeLanguage("en"));
  });

  it("loads an existing revision, keeps publish opt-in, and creates the next revision", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify(existingPage()), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify(existingPage()), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByRole("heading", { name: "Edit Operations" })).toBeVisible();
    expect(screen.getByLabelText("Title")).toHaveValue("Operations");
    expect(screen.getByLabelText("Publish this revision immediately")).not.toBeChecked();
    expect(screen.getAllByText("Next revision v4")).toHaveLength(2);

    await user.clear(screen.getByLabelText("Revision comment"));
    await user.type(screen.getByLabelText("Revision comment"), "Refresh operations copy");
    await user.click(screen.getByRole("button", { name: "Save revision" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
    const [, init] = fetchMock.mock.calls[1] as [URL, RequestInit];
    expect(JSON.parse(String(init.body))).toMatchObject({
      title: "Operations",
      comment: "Refresh operations copy",
      isPublished: false,
    });
    expect(await screen.findByRole("heading", { name: "Detail" })).toBeVisible();
  });

  it("renders localized German authoring chrome", async () => {
    await act(async () => i18n.changeLanguage("de"));
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify(existingPage()), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );
    renderPage();

    expect(await screen.findByRole("heading", { name: "Operations bearbeiten" })).toBeVisible();
    expect(screen.getByLabelText("Revisionskommentar")).toBeVisible();
    expect(screen.getByRole("button", { name: "Revision speichern" })).toBeVisible();
  });

  it("shows failed revision saves as non-blocking toasts", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify(existingPage()), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ message: "The revision could not be stored." }), {
          status: 500,
          headers: { "Content-Type": "application/json" },
        }),
      );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderPage();

    await screen.findByRole("heading", { name: "Edit Operations" });
    await user.type(screen.getByLabelText("Revision comment"), "Retry this revision");
    await user.click(screen.getByRole("button", { name: "Save revision" }));

    const toast = await screen.findByRole("alert");
    expect(toast).toHaveClass("app-toast");
    expect(toast).toHaveTextContent(/public page revision could not be saved/i);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
