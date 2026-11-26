import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, Outlet, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import { eventService, fieldReportService, vereinService } from "@/api/services";
import type { FieldReport, FullUser, UpcomingEvent, VereinDocument } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { Component as HomePage } from "@/pages/HomePage";
import { Component as UnitPage } from "@/pages/UnitPage";
import { Component as UpcomingEventsPage } from "@/pages/UpcomingEventsPage";
import { MinuteDetailPage, MinutesPage, StatutesPage, VereinPage } from "@/pages/VereinPages";
import { parseCustomProperties, readProjectFile, resolveToken } from "./helpers/cssTokens";
import { renderWithProviders } from "./helpers/renderWithProviders";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isLoading: false,
    isAuthenticated: Boolean(authState.user),
  }),
}));

const configuration = {
  Api: { BaseUrl: "/api" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: false, ReactBasePath: "/" },
} as PublicConfiguration;

/** Renders the home page under an outlet that supplies the public configuration, as the community layout does. */
function renderHome() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      {
        path: "/",
        element: <Outlet context={configuration} />,
        children: [{ index: true, element: <HomePage /> }],
      },
    ],
    { initialEntries: ["/"] },
  );
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <RouterProvider router={router} />
      </I18nextProvider>
    </QueryClientProvider>,
  );
}

/** Builds a published Verein document with a revision. */
function vereinDocument(title = "Satzung 2026"): VereinDocument {
  return {
    primaryKey: 7,
    timeStampUpdated: "2026-08-01T00:00:00Z",
    revision: {
      version: 3,
      title,
      summary: "What the Verein agreed.",
      markdown: "Body of the document.",
      timeStampPublished: "2026-08-01T00:00:00Z",
    },
  } as unknown as VereinDocument;
}

/** Builds an upcoming operation in the current UTC month so the calendar view lists it. */
function operation(overrides: Partial<UpcomingEvent> = {}): UpcomingEvent {
  const now = new Date();
  const scheduledFor = new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), Math.min(24, now.getUTCDate() + 1), 18),
  ).toISOString();
  return {
    primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
    title: "Operation Copper Trail",
    description: "Briefing",
    terrainTitle: "Altis",
    modPackDefinitionTitle: "UTF Main",
    modPackRevisionTimeStampDownloaded: null,
    imageAssetFk: "00000000-0000-0000-0000-000000000000",
    imagePath: "",
    scheduledFor,
    scheduledForOriginal: scheduledFor,
    timeStampCreated: now.toISOString(),
    acceptedCount: 3,
    rejectedCount: 1,
    maybeCount: 2,
    minimumAccepted: 12,
    hostedBy: { primaryKey: "2fdd4802-e081-4baa-9120-f1873653a057", nickname: "Raven" },
    metaAcceptance: "Accepted",
    ...overrides,
  };
}

/** Every h1 element of the rendered document, in DOM order. */
function headingOnes(): HTMLElement[] {
  return screen.queryAllByRole("heading", { level: 1 });
}

describe("public hero pages", () => {
  beforeAll(async () => {
    // The runtime's own experimental localStorage can shadow jsdom's without working; the page stores its view mode.
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
    window.history.replaceState({}, "", "/events/upcoming");
    await act(async () => i18n.changeLanguage("en"));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("upcoming operations", () => {
    it("renders exactly one h1 through the hero, first in DOM order, while loading", () => {
      vi.spyOn(eventService, "upcoming").mockReturnValue(new Promise(() => undefined));
      const { container } = renderWithProviders(<UpcomingEventsPage />);

      const headings = headingOnes();
      expect(headings).toHaveLength(1);
      expect(headings[0]).toHaveTextContent("Upcoming Operations");
      expect(container.querySelector("section.public-hero h1")).toBe(headings[0]);
      expect(container.querySelector("h1, h2, h3")).toBe(headings[0]);
      expect(screen.getByRole("status")).toHaveTextContent("Operations are loading.");
    });

    it("keeps the single h1 above the error state and retries with a real refetch", async () => {
      const upcoming = vi
        .spyOn(eventService, "upcoming")
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue([operation()]);
      const user = userEvent.setup();
      const { container } = renderWithProviders(<UpcomingEventsPage />);

      const alert = await screen.findByRole("alert");
      expect(headingOnes()).toHaveLength(1);
      expect(container.querySelector("h1, h2, h3")).toBe(headingOnes()[0]);
      expect(alert).toHaveTextContent("Upcoming operations could not be loaded");

      await user.click(within(alert).getByRole("button", { name: "Try again" }));

      await waitFor(() => expect(upcoming).toHaveBeenCalledTimes(2));
      await waitFor(() => expect(screen.queryByRole("alert")).not.toBeInTheDocument());
      expect(headingOnes()).toHaveLength(1);
    });

    it("takes attendance badge tones from the lexicon and renders unknown values neutral", async () => {
      window.history.replaceState({}, "", "/events/upcoming?mode=board");
      vi.spyOn(eventService, "upcoming").mockResolvedValue([
        operation({
          primaryKey: "00000000-0000-4000-8000-000000000001",
          metaAcceptance: "Accepted",
        }),
        operation({
          primaryKey: "00000000-0000-4000-8000-000000000002",
          title: "Second operation",
          metaAcceptance: "Rejected",
        }),
        operation({
          primaryKey: "00000000-0000-4000-8000-000000000003",
          title: "Third operation",
          metaAcceptance: "Unrecognized" as unknown as UpcomingEvent["metaAcceptance"],
        }),
      ]);
      renderWithProviders(<UpcomingEventsPage />);

      const cards = await screen.findAllByRole("article");
      expect(cards).toHaveLength(3);
      expect(
        within(cards[0]!).getByText("Confirmed", { selector: ".status-badge--success" }),
      ).toBeInTheDocument();
      expect(within(cards[1]!).getByText("Declined")).toHaveClass("status-badge--danger");
      expect(within(cards[2]!).getByText("Not assigned")).toHaveClass("status-badge--neutral");
      // Readiness stays separate from attendance: three of twelve accepted needs signups.
      expect(within(cards[0]!).getAllByText("Low slots")[0]).toHaveClass("status-badge--warning");
    });

    it("switches mode and filters through shared controls and keeps the mode parameter", async () => {
      window.history.replaceState({}, "", "/events/upcoming?mode=board");
      vi.spyOn(eventService, "upcoming").mockResolvedValue([operation()]);
      const user = userEvent.setup();
      const { container } = renderWithProviders(<UpcomingEventsPage />);

      await screen.findByRole("article");
      const modes = screen.getByRole("group", { name: "Operations view mode" });
      expect(within(modes).getByRole("button", { name: "Board" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
      expect(within(modes).getByRole("button", { name: "Calendar" })).toHaveAttribute(
        "aria-pressed",
        "false",
      );
      const filters = screen.getByRole("group", { name: "Operation status filter" });
      await user.click(within(filters).getByRole("button", { name: "Needs signups" }));
      expect(within(filters).getByRole("button", { name: "Needs signups" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );

      await user.click(within(modes).getByRole("button", { name: "Calendar" }));

      expect(container.querySelector(".calendar-workspace")).not.toBeNull();
      expect(screen.queryByRole("group", { name: "Operation status filter" })).toBeNull();
      expect(headingOnes()).toHaveLength(1);
    });

    it("renders no raw control and no button look-alike outside the shared controls", async () => {
      vi.spyOn(eventService, "upcoming").mockResolvedValue([operation()]);
      const { container } = renderWithProviders(<UpcomingEventsPage />);

      await screen.findByRole("heading", { name: "Operation Copper Trail", level: 2 });
      for (const control of container.querySelectorAll("button, a.button")) {
        // Every button and button-styled link is rendered by ActionButton or ActionLink, which carry the base class.
        expect(control).toHaveClass("button");
      }
      expect(container.querySelector("input, select, textarea, details")).toBeNull();
    });
  });
});

describe("home page", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    await act(async () => i18n.changeLanguage("en"));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders one h1 through the hero and lexicon tones for operation and attendance badges", async () => {
    vi.spyOn(eventService, "upcoming").mockResolvedValue([
      operation({ metaAcceptance: "Maybe", acceptedCount: 3, minimumAccepted: 12 }),
    ]);
    vi.spyOn(fieldReportService, "list").mockResolvedValue([]);
    const { container } = renderHome();

    await screen.findAllByText("Low slots");
    const headings = headingOnes();
    expect(headings).toHaveLength(1);
    expect(headings[0]).toHaveTextContent("Operation Copper Trail");
    expect(container.querySelector("section.public-hero h1")).toBe(headings[0]);
    expect(container.querySelector("h1, h2, h3")).toBe(headings[0]);
    for (const badge of screen.getAllByText("Low slots")) {
      expect(badge).toHaveClass("status-badge--warning");
    }
    for (const badge of screen.getAllByText("Maybe")) {
      expect(badge).toHaveClass("status-badge--warning");
    }
  });

  it("shows the shared empty and error states with a working retry", async () => {
    const upcoming = vi
      .spyOn(eventService, "upcoming")
      .mockRejectedValueOnce(new Error("offline"))
      .mockResolvedValue([]);
    vi.spyOn(fieldReportService, "list").mockResolvedValue([] as FieldReport[]);
    const user = userEvent.setup();
    renderHome();

    const alerts = await screen.findAllByRole("alert");
    expect(headingOnes()).toHaveLength(1);
    await user.click(within(alerts[0]!).getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(upcoming).toHaveBeenCalledTimes(2));
    expect(await screen.findAllByText("No upcoming operations")).not.toHaveLength(0);
    expect(await screen.findByText("No field reports yet")).toBeInTheDocument();
  });
});

describe("Verein pages", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  /** Asserts that the document holds exactly one h1 and that it is the first heading. */
  function expectOneLeadingH1(container: HTMLElement, name: string | RegExp) {
    const headings = headingOnes();
    expect(headings).toHaveLength(1);
    expect(headings[0]).toHaveAccessibleName(name);
    expect(container.querySelector("h1, h2, h3")).toBe(headings[0]);
  }

  it("renders the statutes loading, error and not-found states below one h1", async () => {
    const statutes = vi
      .spyOn(vereinService, "statutes")
      .mockReturnValueOnce(new Promise(() => undefined));
    const loading = renderWithProviders(<StatutesPage />);
    expectOneLeadingH1(loading.container, "Satzung");
    expect(screen.getByRole("status")).toHaveTextContent("Loading Satzung");
    loading.unmount();

    statutes.mockRejectedValueOnce(new Error("offline"));
    const failed = renderWithProviders(<StatutesPage />);
    const alert = await screen.findByRole("alert");
    expectOneLeadingH1(failed.container, "Satzung");
    expect(alert).toHaveTextContent("Satzung could not be loaded.");
    expect(within(alert).getByRole("button", { name: "Try again" })).toBeInTheDocument();
    failed.unmount();

    statutes.mockResolvedValueOnce(null as unknown as VereinDocument);
    const empty = renderWithProviders(<StatutesPage />);
    expect(await screen.findByText("No Satzung published yet")).toBeInTheDocument();
    expectOneLeadingH1(empty.container, "Satzung");
  });

  it("renders a published document with its title as the only h1 and no hand-written heading", async () => {
    vi.spyOn(vereinService, "statutes").mockResolvedValue(vereinDocument());
    const { container } = renderWithProviders(<StatutesPage />);

    await screen.findByText("Body of the document.");
    expectOneLeadingH1(container, "Satzung 2026");
    expect(container.querySelector("section.public-hero h1")).toBe(headingOnes()[0]);
    expect(screen.getByText("What the Verein agreed.")).toBeInTheDocument();
    const back = screen.getByRole("link", { name: /Back to Verein/ });
    expect(back).toHaveClass("button");
    expect(back).toHaveAttribute("href", "/verein");
  });

  it("renders the minute detail not-found and error states below one h1", async () => {
    const minute = vi.spyOn(vereinService, "minute").mockRejectedValue(new Error("gone"));
    const invalid = renderWithProviders(<MinuteDetailPage />, {
      route: "/verein/beschluesse/not-a-number",
      path: "/verein/beschluesse/:documentId",
    });
    expect(await screen.findByRole("status")).toBeInTheDocument();
    expectOneLeadingH1(invalid.container, "Verein document");
    expect(minute).not.toHaveBeenCalled();
    invalid.unmount();

    const failed = renderWithProviders(<MinuteDetailPage />, {
      route: "/verein/beschluesse/7",
      path: "/verein/beschluesse/:documentId",
    });
    const alert = await screen.findByRole("alert");
    expectOneLeadingH1(failed.container, "Verein document");
    expect(alert).toHaveTextContent("Document could not be loaded.");
  });

  it("renders the minutes list with one h1 and the shared empty state", async () => {
    vi.spyOn(vereinService, "minutes").mockResolvedValue([]);
    const { container } = renderWithProviders(<MinutesPage />);

    expect(await screen.findByText("No published documents")).toBeInTheDocument();
    expectOneLeadingH1(container, "Beschlüsse and minutes");
    expect(screen.getByRole("link", { name: "Back to Verein" })).toHaveClass("button");
  });

  it("renders the Verein landing page with one hero h1 and no raw control", async () => {
    vi.spyOn(vereinService, "settings").mockResolvedValue({} as never);
    vi.spyOn(vereinService, "statutes").mockResolvedValue(vereinDocument());
    vi.spyOn(vereinService, "minutes").mockResolvedValue([]);
    const { container } = renderWithProviders(<VereinPage />);

    await screen.findByRole("heading", { level: 2, name: "Documents and records" });
    expect(headingOnes()).toHaveLength(1);
    expect(container.querySelector("section.public-hero h1")).toBe(headingOnes()[0]);
    expect(container.querySelector("button, input, select, textarea")).toBeNull();
  });
});

describe("unit page", () => {
  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    await act(async () => i18n.changeLanguage("en"));
  });

  it("renders one hero h1 first, sections from h2 and no raw control", () => {
    const { container } = renderWithProviders(<UnitPage />);

    const headings = headingOnes();
    expect(headings).toHaveLength(1);
    expect(container.querySelector("section.public-hero h1")).toBe(headings[0]);
    expect(container.querySelector("h1, h2, h3")).toBe(headings[0]);
    expect(screen.getAllByRole("heading", { level: 2 }).length).toBeGreaterThanOrEqual(3);
    expect(container.querySelector("button, input, select, textarea")).toBeNull();
    for (const link of container.querySelectorAll("section.public-hero a")) {
      expect(link).toHaveClass("button");
    }
  });
});

describe("rhythm below the public hero", () => {
  const tokens = parseCustomProperties(readProjectFile("src/styles/tokens.css"));
  const expected = resolveToken("--utf-space-lg", [tokens]);

  beforeAll(async () => i18nReady);

  beforeEach(async () => {
    authState.user = undefined;
    window.history.replaceState({}, "", "/");
    await act(async () => i18n.changeLanguage("en"));
    vi.spyOn(eventService, "upcoming").mockResolvedValue([]);
    vi.spyOn(fieldReportService, "list").mockResolvedValue([]);
    vi.spyOn(vereinService, "settings").mockResolvedValue({} as never);
    vi.spyOn(vereinService, "statutes").mockResolvedValue(vereinDocument());
    vi.spyOn(vereinService, "minutes").mockResolvedValue([]);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  /** Reads the top padding of the first block that follows the hero, with the token reference resolved. */
  function firstBlockPadding(container: HTMLElement): string {
    const block = container.querySelector(
      ".public-page > .public-hero + .content-section, .public-page > .public-hero + .page-wrap",
    );
    expect(block, "a block follows the hero").not.toBeNull();
    return resolveToken("--probe", [
      new Map([["--probe", getComputedStyle(block!).paddingTop]]),
      tokens,
    ]);
  }

  it.each([
    ["upcoming operations", () => renderWithProviders(<UpcomingEventsPage />).container],
    ["home", () => renderHome().container],
    ["unit", () => renderWithProviders(<UnitPage />).container],
    ["Verein", () => renderWithProviders(<VereinPage />).container],
    ["Satzung document", () => renderWithProviders(<StatutesPage />).container],
  ])("starts the first block of the %s page at the large spacing token", (_name, renderPage) => {
    expect(firstBlockPadding(renderPage())).toBe(expected);
  });

  it("keeps the hero lede and actions on spacing tokens", () => {
    const css = readProjectFile("src/pages/publicEvents.css");
    const lede = /\.public-hero \.public-hero__description \{([^}]*)\}/.exec(css)?.[1] ?? "";
    const actions = /\.public-hero \.public-hero__actions \{([^}]*)\}/.exec(css)?.[1] ?? "";
    expect(lede).toMatch(/margin:\s*var\(--utf-space-md\) 0 var\(--utf-space-lg\)/);
    expect(lede).toMatch(/max-width:\s*var\(--utf-measure\)/);
    expect(actions).toMatch(/gap:\s*var\(--utf-space-sm\)/);
  });
});
