/**
 * Page-level proof that an expected "none" answer from the API reaches the designed not-found state.
 *
 * These tests deliberately run the real page components against the real services and the real API client, with
 * only `fetch` stubbed. Tests that replace a service module or spy on a service method cannot see a defect in
 * the service's result mapping: review finding CR-01 was a read that resolved `undefined` for a 404, which
 * TanStack Query treats as a failed load, so the pages showed the load-error panel instead of the empty state
 * while every mocked-service test stayed green. Do not mock the services module in this file.
 */
import { act, screen } from "@testing-library/react";
import { beforeEach } from "vitest";
import { initializeApiClient } from "@/api/client";
import type { FullUser } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { EventDetailPage } from "@/features/events-member/EventDetailPage";
import { Component as FieldReportDetailPage } from "@/pages/FieldReportDetailPage";
import { Component as GdprPage } from "@/pages/GdprPage";
import { renderWithProviders } from "./helpers/renderWithProviders";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/auth/auth")>()),
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

// The shared icon double lists only the icons other suites need; the event page also draws these.
vi.mock("@fluentui/react-icons", async (importOriginal) => {
  const Icon = () => <svg data-testid="fluent-icon" aria-hidden="true" />;
  return {
    ...(await importOriginal<Record<string, unknown>>()),
    ArrowDownload20Regular: Icon,
    Box20Regular: Icon,
    CalendarLtr20Regular: Icon,
    Checkmark20Regular: Icon,
    Clock20Regular: Icon,
    Cube20Regular: Icon,
    Map20Regular: Icon,
    PeopleTeam20Regular: Icon,
    Person20Regular: Icon,
  };
});

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const eventId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";

/** The status the stub answers per pathname; every other pathname is recorded as unexpected. */
type Answers = Record<string, number>;

/** Pathnames requested that no answer was declared for; each test asserts that it stays empty. */
let unexpected: string[] = [];

/** Stubs fetch so that each declared pathname answers its status with an empty body. */
function stubApi(answers: Answers): void {
  unexpected = [];
  vi.stubGlobal(
    "fetch",
    vi.fn((input: URL | string) => {
      const pathname = new URL(String(input), "http://localhost").pathname;
      const status = answers[pathname];
      if (status === undefined) {
        unexpected.push(pathname);
        return Promise.resolve(new Response(null, { status: 500 }));
      }
      return Promise.resolve(new Response(null, { status }));
    }),
  );
  initializeApiClient(configuration);
}

/** Asserts the page shows an empty state and neither an error panel nor a retry action. */
function expectNoErrorState(): void {
  expect(screen.queryByRole("alert")).toBeNull();
  expect(screen.queryByRole("button", { name: "Try again" })).toBeNull();
  expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  expect(unexpected).toEqual([]);
}

describe("expected none answers reach the designed empty state", () => {
  beforeEach(async () => {
    const entries = new Map<string, string>();
    vi.stubGlobal("localStorage", {
      getItem: (key: string) => entries.get(key) ?? null,
      setItem: (key: string, value: string) => void entries.set(key, value),
      removeItem: (key: string) => void entries.delete(key),
      clear: () => entries.clear(),
    });
    await i18nReady;
    await act(async () => {
      await i18n.changeLanguage("en");
    });
    authState.user = undefined;
  });

  afterEach(() => vi.unstubAllGlobals());

  it("shows the operation not-found state for an unknown event", async () => {
    stubApi({
      [`/api/events/${eventId}`]: 404,
      [`/api/events/${eventId}/slotting/all`]: 204,
      [`/api/events/${eventId}/mod-pack-formats`]: 404,
    });
    renderWithProviders(<EventDetailPage />, {
      route: `/events/${eventId}`,
      path: "/events/:eventId",
    });

    expect(
      await screen.findByText("The requested operation was not found or is not visible to you."),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Back to operations" })).toHaveAttribute(
      "href",
      "/events/upcoming",
    );
    expectNoErrorState();
  });

  it("shows the field report not-found state for an unknown slug", async () => {
    stubApi({ "/api/field-reports/unknown-report": 404 });
    renderWithProviders(<FieldReportDetailPage />, {
      route: "/field-reports/unknown-report",
      path: "/field-reports/:slug",
    });

    expect(await screen.findByText("Field report not found")).toBeInTheDocument();
    expect(
      screen.getByText("The requested report is not published or no longer exists."),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Back to reports" })).toHaveAttribute(
      "href",
      "/field-reports",
    );
    expectNoErrorState();
  });

  it.each([404, 204])(
    "shows the unavailable GDPR state when the API answers %i",
    async (status) => {
      stubApi({ "/api/public-pages/gdpr": status });
      renderWithProviders(<GdprPage />, { route: "/gdpr", path: "/gdpr" });

      expect(
        await screen.findByText(
          "The GDPR page has not been published from the command center yet.",
        ),
      ).toBeInTheDocument();
      expectNoErrorState();
    },
  );
});
