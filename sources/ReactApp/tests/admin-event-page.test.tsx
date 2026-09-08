import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { Component as EventAdminPage } from "@/features/admin/EventAdminPage";
import * as eventAdmin from "@/features/admin/eventAdmin";

const eventId = "ea928d0c-f6af-4194-9942-26031dc0ce3d";
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
  nickname: "Event administrator",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  claims: [{ identifier: "adm:ev", value: null }],
  roles: [],
} as FullUser;

function eventCreator(primaryKey: string): FullUser {
  return {
    ...administrator,
    primaryKey,
    nickname: "Event creator",
    claims: [{ identifier: "crt:ev", value: null }],
  };
}

const snapshot: eventAdmin.EventAdminSnapshot = {
  eventCount: 1,
  upcomingEvents: [],
  events: [
    {
      plainEvent: {
        primaryKey: eventId,
        title: "Operation Daybreak",
        scheduledFor: "2026-09-14T18:00:00Z",
        isVisible: true,
        acceptedCount: 14,
        minimumAccepted: 12,
        maybeCount: 1,
        rejectedCount: 0,
      },
      hostedBy: {
        primaryKey: "10000000-0000-4000-8000-000000000002",
        nickname: "Raven",
      },
      owner: { nickname: "Planner" },
      terrain: { title: "Altis" },
      modPackRevision: { definitionTitle: "UTF Main" },
    },
  ],
};

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      { path: "/command/events", element: <EventAdminPage /> },
      {
        path: "/command/events/:eventId",
        element: <h1>Event management destination</h1>,
      },
      { path: "/events/:eventId", element: <h1>Public slots destination</h1> },
    ],
    { initialEntries: ["/command/events"] },
  );
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

async function eventRow(): Promise<HTMLElement> {
  return screen.findByRole("row", { name: /Operation Daybreak/ });
}

describe("event administration navigation", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = administrator;
    vi.spyOn(eventAdmin, "loadEventAdminSnapshot").mockResolvedValue(snapshot);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    authState.user = undefined;
  });

  it("opens command management when non-action row content is clicked", async () => {
    const user = userEvent.setup();
    renderPage();

    const row = await eventRow();
    expect(row).toHaveAttribute("tabindex", "0");
    expect(within(row).getByRole("link", { name: "Manage Event" })).toHaveAttribute(
      "href",
      `/command/events/${eventId}`,
    );

    await user.click(within(row).getByText("Altis"));

    expect(
      await screen.findByRole("heading", { name: "Event management destination" }),
    ).toBeVisible();
  });

  it.each(["{Enter}", " "])("supports %s keyboard activation on the row", async (key) => {
    const user = userEvent.setup();
    renderPage();

    const row = await eventRow();
    row.focus();
    await user.keyboard(key);

    expect(
      await screen.findByRole("heading", { name: "Event management destination" }),
    ).toBeVisible();
  });

  it("keeps the public slots action separate from row management", async () => {
    const user = userEvent.setup();
    renderPage();

    const row = await eventRow();
    const publicSlotsLink = within(row).getByRole("link", { name: "Open slots" });
    publicSlotsLink.focus();
    await user.keyboard("{Enter}");

    expect(await screen.findByRole("heading", { name: "Public slots destination" })).toBeVisible();
  });

  it("opens management for the assigned host", async () => {
    authState.user = eventCreator("10000000-0000-4000-8000-000000000002");
    renderPage();

    const hostedRow = await eventRow();
    expect(hostedRow).toHaveAttribute("tabindex", "0");
    expect(within(hostedRow).getByRole("link", { name: "Manage Event" })).toBeVisible();
  });

  it("keeps events hosted by someone else read-only for creation-only users", async () => {
    authState.user = eventCreator("10000000-0000-4000-8000-000000000003");
    renderPage();

    const nonHostedRow = await eventRow();
    expect(nonHostedRow).not.toHaveAttribute("tabindex");
    expect(within(nonHostedRow).queryByRole("link", { name: "Manage Event" })).toBeNull();
    expect(within(nonHostedRow).getByRole("link", { name: "Open slots" })).toBeVisible();
  });
});
