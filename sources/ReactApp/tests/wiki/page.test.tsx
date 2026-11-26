import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import { AuthProvider } from "@/auth/auth";
import { Component as WikiPage } from "@/features/wiki/WikiPage";

const pageId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";
const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

function response(body: unknown, status = 200): Response {
  if (status === 204) return new Response(null, { status });
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function renderWiki(fetchMock: ReturnType<typeof vi.fn>) {
  vi.stubGlobal("fetch", fetchMock);
  initializeApiClient(configuration);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter([{ path: "/wiki/:pageId", element: <WikiPage /> }], {
    initialEntries: [`/wiki/${pageId}`],
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe("wiki reading page", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("loads public article content without requesting privileged revision history", async () => {
    const fetchMock = vi.fn(async (input: URL | RequestInfo) => {
      const url = input instanceof URL ? input : new URL(String(input));
      if (url.pathname.endsWith("/users/me")) return response(null, 204);
      if (url.pathname.endsWith("/users/authentication-providers"))
        return response({ steam: false, discord: false });
      if (url.pathname.endsWith("/wiki/all/header"))
        return response([
          { primaryKey: pageId, name: "Training/Radio", lastModified: "2026-08-10T12:00:00Z" },
        ]);
      if (url.pathname.endsWith(`/wiki/${pageId}`))
        return response({
          primaryKey: pageId,
          title: "Training/Radio",
          timeStampCreated: "2026-08-09T12:00:00Z",
          isDeleted: false,
          revisions: [
            {
              primaryKey: "revision-1",
              timeStampCreated: "2026-08-10T12:00:00Z",
              markdown: "# Radio\n\nUse the assigned net.",
              author: null,
              comment: "",
            },
          ],
        });
      throw new Error(`Unexpected request: ${url.pathname}`);
    });

    renderWiki(fetchMock);

    expect(await screen.findByRole("heading", { name: "Radio", level: 2 })).toBeVisible();
    expect(screen.getAllByText("Use the assigned net.")).toHaveLength(2);
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) => String(input).includes("/revisions/header")),
      ).toBe(false),
    );
    expect(screen.queryByRole("button", { name: /history/i })).not.toBeInTheDocument();
  });

  it("loads revision history only when the identity has a base wiki claim", async () => {
    const fetchMock = vi.fn(async (input: URL | RequestInfo) => {
      const url = input instanceof URL ? input : new URL(String(input));
      if (url.pathname.endsWith("/users/me"))
        return response({
          primaryKey: "user-1",
          nickname: "Editor",
          isBanned: false,
          isDeleted: false,
          isVerified: true,
          isVereinMember: false,
          claims: [{ identifier: "wiki:modify", value: null }],
          roles: [],
        });
      if (url.pathname.endsWith("/users/authentication-providers"))
        return response({ steam: false, discord: false });
      if (url.pathname.endsWith("/wiki/all/header"))
        return response([
          { primaryKey: pageId, name: "Training/Radio", lastModified: "2026-08-10T12:00:00Z" },
        ]);
      if (url.pathname.endsWith("/revisions/header"))
        return response([
          {
            primaryKey: "revision-1",
            timeStampCreated: "2026-08-10T12:00:00Z",
            authorPrimaryKey: "user-1",
            authorName: "Editor",
            comment: "Initial",
          },
        ]);
      if (url.pathname.endsWith(`/wiki/${pageId}`))
        return response({
          primaryKey: pageId,
          title: "Training/Radio",
          timeStampCreated: "2026-08-09T12:00:00Z",
          isDeleted: false,
          revisions: [
            {
              primaryKey: "revision-1",
              timeStampCreated: "2026-08-10T12:00:00Z",
              markdown: "# Radio",
              author: { nickname: "Editor" },
              comment: "Initial",
            },
          ],
        });
      throw new Error(`Unexpected request: ${url.pathname}`);
    });

    renderWiki(fetchMock);

    await screen.findByRole("heading", { name: "Radio", level: 2 });
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) => String(input).includes("/revisions/header")),
      ).toBe(true),
    );
    expect(screen.getAllByRole("tab", { name: /history/i }).length).toBeGreaterThan(0);
  });
});

const editorUser = {
  primaryKey: "user-1",
  nickname: "Editor",
  isBanned: false,
  isDeleted: false,
  isVerified: true,
  isVereinMember: false,
  claims: [
    { identifier: "wiki:modify", value: null },
    { identifier: "wiki:delete", value: null },
    { identifier: "crt:wiki", value: null },
  ],
  roles: [],
};

/** A fetch stub that serves one article and the identity given. */
function wikiFetch(user: unknown) {
  return vi.fn(async (input: URL | RequestInfo) => {
    const url = input instanceof URL ? input : new URL(String(input));
    if (url.pathname.endsWith("/users/me")) return response(user, user ? 200 : 204);
    if (url.pathname.endsWith("/users/authentication-providers"))
      return response({ steam: false, discord: false });
    if (url.pathname.endsWith("/wiki/all/header"))
      return response([
        { primaryKey: pageId, name: "Training/Radio", lastModified: "2026-08-10T12:00:00Z" },
      ]);
    if (url.pathname.endsWith("/revisions/header"))
      return response([
        {
          primaryKey: "revision-1",
          timeStampCreated: "2026-08-10T12:00:00Z",
          authorPrimaryKey: "user-1",
          authorName: "Editor",
          comment: "Initial",
        },
      ]);
    if (url.pathname.endsWith(`/wiki/${pageId}`))
      return response({
        primaryKey: pageId,
        title: "Training/Radio",
        timeStampCreated: "2026-08-09T12:00:00Z",
        isDeleted: false,
        revisions: [
          {
            primaryKey: "revision-1",
            timeStampCreated: "2026-08-10T12:00:00Z",
            markdown: "Use the assigned net.",
            author: { nickname: "Editor" },
            comment: "Initial",
          },
        ],
      });
    throw new Error(`Unexpected request: ${url.pathname}`);
  });
}

describe("wiki page header and controls", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("renders one h1 through the shared page header and a labelled search", async () => {
    const { container } = renderWiki(wikiFetch(editorUser));

    await screen.findByRole("heading", { name: "Radio", level: 2 });
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(container.querySelector(".page-header h1")).not.toBeNull();
    expect(screen.getByRole("searchbox", { name: /search/i })).toBeVisible();
  });

  it("renders every button outside the editor as a shared control", async () => {
    const { container } = renderWiki(wikiFetch(editorUser));

    await screen.findByRole("heading", { name: "Radio", level: 2 });
    const buttons = Array.from(container.querySelectorAll("button"));
    expect(buttons.length).toBeGreaterThan(5);
    for (const button of buttons) expect(button).toHaveClass("button");
    expect(screen.getAllByRole("tab").length).toBeGreaterThan(0);
  });

  it("keeps the editor working with labelled fields and a named close control", async () => {
    renderWiki(wikiFetch(editorUser));

    await screen.findByRole("heading", { name: "Radio", level: 2 });
    await userEvent.click(screen.getAllByRole("button", { name: "Edit" })[0]!);
    const editor = (await screen.findByRole("heading", { name: "Edit article" })).closest(
      ".wiki-editor",
    ) as HTMLElement;
    expect(within(editor).getByLabelText(/^Article title/)).toHaveValue("Training/Radio");
    expect(within(editor).getByRole("button", { name: "Close editor" })).toBeVisible();
    await userEvent.click(within(editor).getByRole("button", { name: "Leave without saving" }));
    await waitFor(() => expect(screen.queryByRole("heading", { name: "Edit article" })).toBeNull());
  });
});

describe("wiki stylesheet focus", () => {
  const css = readFileSync(resolve(process.cwd(), "src/features/wiki/wiki.css"), "utf8");

  it("removes no outline without a replacement ring", () => {
    expect(css).not.toMatch(/outline:\s*(none|0)\b/);
  });

  it.each([
    ".wiki-tree__page",
    ".wiki-intel__tabs button",
    ".wiki-related a",
    ".wiki-revisions button",
    ".wiki-editor__close",
  ])("shows the focus ring token on %s", (selector) => {
    const rules = css
      .split("}")
      .map((chunk) => chunk.split("{") as [string, string?])
      .filter(
        ([selectors, body]) =>
          body !== undefined && selectors.includes(`${selector}:focus-visible`),
      );
    expect(rules.length).toBeGreaterThan(0);
    for (const [, body] of rules) {
      expect(body).toContain("outline: var(--utf-focus-ring)");
      expect(body).toMatch(/outline-offset: (calc\()?var\(--utf-focus-offset\)/);
    }
  });
});
