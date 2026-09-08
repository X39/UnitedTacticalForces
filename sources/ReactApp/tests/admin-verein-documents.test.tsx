import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { Component as VereinDocumentDetailPage } from "@/features/admin/VereinDocumentDetailPage";
import { Component as VereinDocumentEditorPage } from "@/features/admin/VereinDocumentEditorPage";
import { Component as VereinDocumentsPage } from "@/features/admin/VereinDocumentsPage";
import {
  latestPublishedVereinRevision,
  normalizeVereinDocument,
  normalizeVereinDocumentPayload,
  validateVereinDocument,
  vereinDocumentMatches,
  type VereinAdminDocument,
} from "@/features/admin/vereinDocuments";
import * as vereinDocuments from "@/features/admin/vereinDocuments";

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
  nickname: "Verein editor",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: true,
  claims: [{ identifier: "verein:documents", value: null }],
  roles: [],
} as FullUser;

const document: VereinAdminDocument = {
  primaryKey: 7,
  kind: "Minutes",
  isDeleted: false,
  revision: {
    primaryKey: 71,
    documentFk: 7,
    version: 1,
    title: "Assembly minutes 2026",
    summary: "Annual assembly decisions.",
    markdown: "# Decisions\n\nThe budget was approved.",
    comment: "Initial draft",
    isPublished: false,
    timeStampPublished: null,
    timeStampCreated: "2026-08-12T10:00:00Z",
    authorFk: administrator.primaryKey,
    authorName: "Verein editor",
  },
  revisions: [],
  timeStampCreated: "2026-08-12T10:00:00Z",
  timeStampUpdated: "2026-08-12T10:00:00Z",
};
document.revisions = [document.revision!];

function renderPage(initialEntry: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: "/command/verein/documents", element: <VereinDocumentsPage /> },
      { path: "/command/verein/documents/create", element: <VereinDocumentEditorPage /> },
      {
        path: "/command/verein/documents/:documentId",
        element: <VereinDocumentDetailPage />,
      },
      {
        path: "/command/verein/documents/:documentId/edit",
        element: <VereinDocumentEditorPage />,
      },
    ],
    { initialEntries: [initialEntry] },
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

describe("Verein document administration", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = administrator;
    vi.spyOn(vereinDocuments, "loadVereinDocuments").mockResolvedValue([document]);
    vi.spyOn(vereinDocuments, "createVereinDocument").mockResolvedValue(document);
    vi.spyOn(vereinDocuments, "createVereinDocumentRevision").mockResolvedValue(document);
    vi.spyOn(vereinDocuments, "setVereinRevisionPublished").mockResolvedValue({
      ...document,
      revision: { ...document.revision!, isPublished: true },
      revisions: [{ ...document.revision!, isPublished: true }],
    });
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  it("normalizes legacy kinds, revision history, filters, and publication order", () => {
    const normalized = normalizeVereinDocument({
      primaryKey: 7,
      kind: 1 as never,
      revision: { primaryKey: 72, version: 2, title: "Latest" },
      revisions: [
        {
          primaryKey: 71,
          version: 1,
          isPublished: true,
          timeStampPublished: "2026-08-12T10:00:00Z",
        },
        {
          primaryKey: 72,
          version: 2,
          isPublished: true,
          timeStampPublished: "2026-08-13T10:00:00Z",
        },
      ],
    });
    expect(normalized.kind).toBe("Minutes");
    expect(latestPublishedVereinRevision(normalized)?.version).toBe(2);
    expect(vereinDocumentMatches(normalized, "Minutes", "latest")).toBe(true);
    expect(vereinDocumentMatches(normalized, "Satzung", "")).toBe(false);
  });

  it("validates and trims revision payloads", () => {
    expect(
      validateVereinDocument(
        { title: "", summary: "", markdown: "body", comment: "", isPublished: false },
        "Minutes",
        false,
      ),
    ).toBe("title");
    expect(
      validateVereinDocument(
        { title: "Satzung", summary: "", markdown: "body", comment: "", isPublished: false },
        "Satzung",
        true,
      ),
    ).toBe("satzung");
    expect(
      normalizeVereinDocumentPayload({
        title: " Title ",
        summary: " Summary ",
        markdown: " Body ",
        comment: " ",
        isPublished: true,
      }),
    ).toEqual({
      title: "Title",
      summary: "Summary",
      markdown: "Body",
      comment: null,
      isPublished: true,
    });
  });

  it("blocks unauthorized document reads", async () => {
    authState.user = undefined;
    renderPage("/command/verein/documents");

    expect(await screen.findByRole("heading", { name: "Document access required" })).toBeVisible();
    expect(vereinDocuments.loadVereinDocuments).not.toHaveBeenCalled();
  });

  it("lists governance documents and filters their search", async () => {
    const user = userEvent.setup();
    renderPage("/command/verein/documents");

    expect(await screen.findByRole("heading", { name: "Verein documents" })).toBeVisible();
    expect(await screen.findByRole("link", { name: "Assembly minutes 2026" })).toBeVisible();
    await user.type(
      screen.getByPlaceholderText("Search documents, summaries, or kinds"),
      "missing",
    );
    expect(await screen.findByText("No documents match the current filter.")).toBeVisible();
  });

  it("creates a validated document and preserves the selected kind", async () => {
    vi.mocked(vereinDocuments.loadVereinDocuments).mockResolvedValue([]);
    const user = userEvent.setup();
    renderPage("/command/verein/documents/create");

    await user.selectOptions(await screen.findByLabelText("Document kind"), "Minutes");
    await user.type(screen.getByLabelText("Title"), "Assembly minutes 2027");
    await user.type(screen.getByLabelText("Summary"), "Budget decision");
    await user.type(screen.getByLabelText("Markdown"), "# Decision\n\nApproved.");
    await user.click(screen.getByLabelText("Publish immediately"));
    await user.click(screen.getByRole("button", { name: "Create document" }));

    await waitFor(() =>
      expect(vereinDocuments.createVereinDocument).toHaveBeenCalledWith("Minutes", {
        title: "Assembly minutes 2027",
        summary: "Budget decision",
        markdown: "# Decision\n\nApproved.",
        comment: null,
        isPublished: true,
      }),
    );
  });

  it("publishes a selected revision from the audit history", async () => {
    const user = userEvent.setup();
    renderPage("/command/verein/documents/7");

    await user.click(await screen.findByRole("button", { name: "Publish latest" }));
    await waitFor(() =>
      expect(vereinDocuments.setVereinRevisionPublished).toHaveBeenCalledWith(71, true),
    );
    const toast = await screen.findByRole("status");
    expect(toast).toHaveClass("app-toast");
    expect(toast).toHaveTextContent("Publication state updated.");
  });
});
