import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { Component as EventAdminPage } from "@/features/admin/EventAdminPage";
import { Component as EventCreatePage } from "@/features/admin/EventCreatePage";
import { Component as EventEditPage } from "@/features/admin/EventEditPage";
import * as eventAdmin from "@/features/admin/eventAdmin";
import {
  eventAuthoringService,
  type EventAuthoringSnapshot,
} from "@/features/admin/eventAuthoring";
import * as terrains from "@/features/admin/terrains";
import { hostingService } from "@/features/hosting/services";

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

const hostId = "10000000-0000-4000-8000-000000000002";

const person = { primaryKey: hostId, nickname: "Raven", avatar: "", avatarMimeType: "" };

const authoringSnapshot: EventAuthoringSnapshot = {
  event: {
    primaryKey: eventId,
    title: "Operation Daybreak",
    description: "Brief",
    terrainPrimaryKey: 4,
    terrainTitle: "Altis",
    terrainGameIdentifier: "arma3",
    scenarioPrimaryKey: "scenario-1",
    scenarioTitle: "Dawn raid",
    modPackRevisionPrimaryKey: 9,
    modPackDefinitionTitle: "UTF Main",
    imageAssetFk: "",
    imagePath: "",
    scheduledForOriginal: "2026-09-14T18:00:00Z",
    scheduledFor: "2026-09-14T18:00:00Z",
    timeStampCreated: "2026-08-01T10:00:00Z",
    isVisible: true,
    acceptedCount: 14,
    rejectedCount: 0,
    maybeCount: 1,
    minimumAccepted: 12,
    owner: person,
    hostedBy: person,
  },
  slots: [],
};

function renderEditor() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      { path: "/command/events/:eventId", element: <EventEditPage /> },
      { path: "/command/events", element: <h1>Events list destination</h1> },
    ],
    { initialEntries: [`/command/events/${eventId}`] },
  );
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
  return router;
}

describe("event editor page", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    authState.user = administrator;
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([]);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await i18n.changeLanguage("en");
  });

  it("renders one shared page header while the editor loads", () => {
    vi.spyOn(eventAuthoringService, "load").mockReturnValue(new Promise(() => undefined));
    renderEditor();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
    expect(screen.getByRole("status")).toHaveTextContent("Loading event editor");
  });

  it("keeps the header above the load error and retries through the real query", async () => {
    const load = vi
      .spyOn(eventAuthoringService, "load")
      .mockRejectedValueOnce(new Error("offline"))
      .mockResolvedValue(authoringSnapshot);
    const user = userEvent.setup();
    renderEditor();

    expect(await screen.findByRole("alert")).toHaveTextContent("offline");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);

    await user.click(screen.getByRole("button", { name: "Try again" }));

    expect(
      await screen.findByRole("heading", { level: 1, name: "Operation Daybreak" }),
    ).toBeVisible();
    expect(load).toHaveBeenCalledTimes(2);
  });

  it("keeps the header above the not-found state", async () => {
    vi.spyOn(eventAuthoringService, "load").mockResolvedValue(undefined);
    renderEditor();

    expect(await screen.findByText("Event not found")).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("link", { name: "Back to events" })).toHaveAttribute(
      "href",
      "/command/events",
    );
  });

  it("keeps the header above the forbidden state for a user who may not edit this event", async () => {
    authState.user = eventCreator("10000000-0000-4000-8000-000000000003");
    vi.spyOn(eventAuthoringService, "load").mockResolvedValue(authoringSnapshot);
    renderEditor();

    expect(await screen.findByText("Event administrator access required")).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("renders the loaded editor with exactly one h1 that names the event", async () => {
    vi.spyOn(eventAuthoringService, "load").mockResolvedValue(authoringSnapshot);
    renderEditor();

    const heading = await screen.findByRole("heading", { level: 1, name: "Operation Daybreak" });
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(heading.closest("header")).toHaveClass("page-header");
    expect(screen.getByRole("textbox", { name: "Title" })).toHaveValue("Operation Daybreak");
  });

  it("labels the scenario picker from locale keys in English", async () => {
    vi.spyOn(eventAuthoringService, "load").mockResolvedValue(authoringSnapshot);
    renderEditor();

    const picker = await screen.findByRole("combobox", { name: "Scenario" });
    expect(picker).toHaveAttribute("placeholder", "Search scenarios");
  });

  it("labels the scenario picker in German", async () => {
    await i18n.changeLanguage("de");
    vi.spyOn(eventAuthoringService, "load").mockResolvedValue(authoringSnapshot);
    renderEditor();

    const picker = await screen.findByRole("combobox", { name: "Szenario" });
    expect(picker).toHaveAttribute("placeholder", "Szenarien suchen");
    expect(screen.queryByText("Scenario")).toBeNull();
  });

  it("offers Keep editing and Leave without saving when leaving an edited event", async () => {
    vi.spyOn(eventAuthoringService, "load").mockResolvedValue(authoringSnapshot);
    const user = userEvent.setup();
    const router = renderEditor();
    const title = await screen.findByRole("textbox", { name: "Title" });

    await user.type(title, " II");
    await act(async () => {
      await router.navigate("/command/events");
    });

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "Keep editing event" })).toBeVisible();
    expect(router.state.location.pathname).toBe(`/command/events/${eventId}`);

    await user.click(within(dialog).getByRole("button", { name: "Keep editing event" }));
    expect(router.state.location.pathname).toBe(`/command/events/${eventId}`);
    expect(screen.getByRole("textbox", { name: "Title" })).toHaveValue("Operation Daybreak II");

    await act(async () => {
      await router.navigate("/command/events");
    });
    const second = await screen.findByRole("dialog");
    await user.click(within(second).getByRole("button", { name: "Leave without saving" }));

    expect(await screen.findByRole("heading", { name: "Events list destination" })).toBeVisible();
  });
});

describe("event administration header and controls", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    authState.user = administrator;
    vi.spyOn(eventAdmin, "loadEventAdminSnapshot").mockResolvedValue(snapshot);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await i18n.changeLanguage("en");
  });

  it("names the page like its navigation entry in English", async () => {
    renderPage();

    expect(await screen.findByRole("heading", { level: 1, name: "Events" })).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("names the page like its navigation entry in German", async () => {
    await i18n.changeLanguage("de");
    renderPage();

    expect(await screen.findByRole("heading", { level: 1, name: "Veranstaltungen" })).toBeVisible();
  });

  it("keeps one h1 above the forbidden state", () => {
    authState.user = { ...administrator, claims: [] };
    renderPage();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
    expect(screen.getByRole("status")).toBeVisible();
  });

  it("filters through the shared chips and the shared search field", async () => {
    const user = userEvent.setup();
    renderPage();
    await eventRow();

    const search = screen.getByRole("searchbox");
    await user.type(search, "no such operation");
    expect(await screen.findByText("No events match")).toBeVisible();

    await user.click(screen.getByRole("button", { name: "Clear search" }));
    expect(await eventRow()).toBeVisible();

    const chips = screen.getByRole("group", { name: "Event filters" });
    await user.click(within(chips).getByRole("button", { name: /Hidden/ }));
    expect(within(chips).getByRole("button", { name: /Hidden/ })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(await screen.findByText("No events match")).toBeVisible();
  });

  it("uses the lexicon tone for a hidden event", async () => {
    vi.mocked(eventAdmin.loadEventAdminSnapshot).mockResolvedValue({
      ...snapshot,
      events: snapshot.events.map((row) => ({
        ...row,
        plainEvent: { ...row.plainEvent, isVisible: false },
      })),
    });
    renderPage();

    const row = await eventRow();
    expect(within(row).getByText("Hidden")).toHaveClass("status-badge--neutral");
  });
});

const terrain = {
  primaryKey: 4,
  title: "Altis",
  gameIdentifier: "arma3",
  imageAssetFk: "",
  imagePath: "",
  isActive: true,
};

function renderCreate() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      { path: "/command/events/create", element: <EventCreatePage /> },
      { path: "/command/events", element: <h1>Events list destination</h1> },
    ],
    { initialEntries: ["/command/events/create"] },
  );
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
  return router;
}

describe("event create page", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    await i18n.changeLanguage("en");
    authState.user = administrator;
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([
      { gameIdentifier: "arma3", requiresScenario: true, requiresModPack: false },
    ] as never);
    vi.spyOn(terrains, "loadTerrainWindow").mockResolvedValue({
      terrains: [terrain],
      totalCount: 1,
    });
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await i18n.changeLanguage("en");
  });

  it("keeps one h1 above the forbidden state", () => {
    authState.user = undefined;
    renderCreate();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByText("Event administrator access required")).toBeVisible();
  });

  it("validates the title through the shared field", async () => {
    const user = userEvent.setup();
    renderCreate();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const title = screen.getByRole("textbox", { name: "Title" });
    await user.click(title);
    await user.tab();

    expect(await screen.findByRole("alert")).toHaveTextContent("Enter an operation title.");
    expect(title).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByRole("button", { name: "Next" })).toHaveAttribute("disabled");
  });

  it.each([
    ["en", "Scenario", "Search scenarios"],
    ["de", "Szenario", "Szenarien suchen"],
  ])("labels the scenario picker from locale keys in %s", async (language, label, placeholder) => {
    await i18n.changeLanguage(language);
    const user = userEvent.setup();
    renderCreate();

    await user.click(screen.getByRole("button", { name: new RegExp(`^3`) }));
    await user.click(screen.getByRole("combobox", { name: /Terrain|Karte/ }));
    await user.click(await screen.findByRole("option", { name: /Altis/ }));

    const picker = await screen.findByRole("combobox", { name: label });
    expect(picker).toHaveAttribute("placeholder", placeholder);
  });
});
