import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import type { FullUser, VereinMembershipRequest } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { Component as FieldReportDetailPage } from "@/pages/FieldReportDetailPage";
import { Component as FieldReportsPage } from "@/pages/FieldReportsPage";
import { Component as GdprPage } from "@/pages/GdprPage";
import { Component as JoinPage } from "@/pages/JoinPage";

const authState = vi.hoisted(() => ({
  user: undefined as FullUser | undefined,
  providers: { steam: true, discord: true },
  login: vi.fn(),
}));

const services = vi.hoisted(() => ({
  myMembershipRequest: vi.fn(),
  settings: vi.fn(),
  profileSettings: vi.fn(),
  createMembershipRequest: vi.fn(),
  withdrawMembershipRequest: vi.fn(),
  publishedPage: vi.fn(),
  fieldReportList: vi.fn(),
  fieldReportDetail: vi.fn(),
}));

vi.mock("@/auth/auth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/auth/auth")>()),
  useAuth: () => ({
    user: authState.user,
    isAuthenticated: Boolean(authState.user),
    isLoading: false,
    providers: authState.providers,
    providersLoading: false,
    login: authState.login,
    logout: vi.fn(),
  }),
}));

vi.mock("@/api/services", async (importOriginal) => {
  const original = await importOriginal<typeof import("@/api/services")>();
  return {
    ...original,
    vereinService: {
      ...original.vereinService,
      myMembershipRequest: services.myMembershipRequest,
      settings: services.settings,
      createMembershipRequest: services.createMembershipRequest,
      withdrawMembershipRequest: services.withdrawMembershipRequest,
    },
    publicPageService: { ...original.publicPageService, published: services.publishedPage },
    fieldReportService: {
      ...original.fieldReportService,
      list: services.fieldReportList,
      detail: services.fieldReportDetail,
    },
    identityService: {
      ...original.identityService,
      profileSettings: services.profileSettings,
    },
  };
});

const applicant = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Falcon One",
  eMail: "falcon@example.test",
  claims: [],
  roles: [],
  isVereinMember: false,
} as unknown as FullUser;

/** Renders a routed page under the providers the app supplies. */
function renderPage(element: ReactElement, path = "/", entry = path) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter([{ path, element }], { initialEntries: [entry] });
  return render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe("join page", () => {
  beforeAll(async () => {
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
    authState.user = undefined;
    authState.login.mockReset();
    services.myMembershipRequest.mockReset().mockResolvedValue(null);
    services.settings.mockReset().mockResolvedValue({});
    services.profileSettings.mockReset().mockResolvedValue({});
    services.createMembershipRequest.mockReset();
    services.withdrawMembershipRequest.mockReset();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("renders exactly one h1 for a signed-out visitor and keeps the provider buttons shared", async () => {
    renderPage(<JoinPage />);

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const steam = screen.getByRole("button", { name: "Use your Steam community identity" });
    expect(steam).toHaveClass("button");
    await userEvent.click(steam);
    expect(authState.login).toHaveBeenCalledWith("steam");
    expect(screen.getByText("Sign in before applying")).toBeInTheDocument();
  });

  it("labels every field and marks the required ones", async () => {
    authState.user = applicant;
    renderPage(<JoinPage />);

    const firstName = await screen.findByLabelText(/^First name/);
    expect(firstName).toHaveAttribute("aria-required", "true");
    expect(firstName.closest(".form-field")).not.toBeNull();
    for (const label of [
      /^Last name/,
      /^Date of birth/,
      /^Email/,
      /^Phone number/,
      /^Address line 1/,
      /^Postal code/,
      /^City/,
      /^Country/,
      /^Why do you want to become a member/,
    ]) {
      expect(screen.getByLabelText(label)).toHaveAttribute("aria-required", "true");
    }
    expect(screen.getByLabelText(/^Address line 2/)).not.toHaveAttribute("aria-required");
    for (const checkbox of screen.getAllByRole("checkbox")) {
      expect(checkbox).toHaveAttribute("aria-required", "true");
    }
  });

  it("shows each field error on an invalid submit and sends nothing", async () => {
    authState.user = applicant;
    renderPage(<JoinPage />);

    await userEvent.click(await screen.findByRole("button", { name: "Submit membership request" }));

    const firstName = screen.getByLabelText(/^First name/);
    await waitFor(() => expect(firstName).toHaveAttribute("aria-invalid", "true"));
    const describedBy = firstName.getAttribute("aria-describedby") ?? "";
    expect(document.getElementById(describedBy)).toHaveTextContent(
      "Check this field and provide a valid value.",
    );
    expect(screen.getAllByRole("alert").length).toBeGreaterThanOrEqual(10);
    for (const checkbox of screen.getAllByRole("checkbox")) {
      expect(checkbox).toHaveAttribute("aria-invalid", "true");
    }
    expect(services.createMembershipRequest).not.toHaveBeenCalled();
  });

  it("submits the same payload as before the conversion", async () => {
    authState.user = applicant;
    services.createMembershipRequest.mockResolvedValue({
      primaryKey: 7,
      status: "Submitted",
      firstName: "Ada",
      lastName: "Lovelace",
      timeStampCreated: "2026-11-25T10:00:00Z",
      timeStampUpdated: "2026-11-25T10:00:00Z",
    } satisfies Partial<VereinMembershipRequest>);
    renderPage(<JoinPage />);

    await userEvent.type(await screen.findByLabelText(/^First name/), "Ada");
    await userEvent.type(screen.getByLabelText(/^Last name/), "Lovelace");
    await userEvent.type(screen.getByLabelText(/^Date of birth/), "1990-04-12");
    await userEvent.clear(screen.getByLabelText(/^Email/));
    await userEvent.type(screen.getByLabelText(/^Email/), "ada@example.test");
    await userEvent.type(screen.getByLabelText(/^Phone number/), "+49 30 1234");
    await userEvent.type(screen.getByLabelText(/^Address line 1/), "Analytical Street 1");
    await userEvent.type(screen.getByLabelText(/^Postal code/), "10115");
    await userEvent.type(screen.getByLabelText(/^City/), "Berlin");
    await userEvent.type(screen.getByLabelText(/^Why do you want to become a member/), "Teamwork");
    for (const checkbox of screen.getAllByRole("checkbox")) await userEvent.click(checkbox);
    await userEvent.click(screen.getByRole("button", { name: "Submit membership request" }));

    await waitFor(() => expect(services.createMembershipRequest).toHaveBeenCalledTimes(1));
    expect(services.createMembershipRequest.mock.calls[0]?.[0]).toEqual({
      firstName: "Ada",
      lastName: "Lovelace",
      dateOfBirth: "1990-04-12",
      email: "ada@example.test",
      phoneNumber: "+49 30 1234",
      addressLine1: "Analytical Street 1",
      addressLine2: null,
      postalCode: "10115",
      city: "Berlin",
      country: "Germany",
      motivation: "Teamwork",
      acknowledgesMembershipSeparation: true,
      acknowledgesDataProcessing: true,
      acknowledgesSatzung: true,
    });
  });

  it("keeps the single h1 above a failed request load and retries for real", async () => {
    authState.user = applicant;
    services.myMembershipRequest.mockRejectedValueOnce(new Error("boom"));
    renderPage(<JoinPage />);

    const alert = await screen.findByText("Your membership request could not be loaded.");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const retry = within(alert.closest("section") as HTMLElement).getByRole("button", {
      name: "Try again",
    });
    await userEvent.click(retry);
    await waitFor(() => expect(services.myMembershipRequest).toHaveBeenCalledTimes(2));
  });
});

/** Installs the browser stubs the pages need: storage for the culture and a language reset. */
async function prepareBrowser() {
  const entries = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (key: string) => entries.get(key) ?? null,
    setItem: (key: string, value: string) => void entries.set(key, value),
    removeItem: (key: string) => void entries.delete(key),
    clear: () => entries.clear(),
  });
  await i18nReady;
}

const structuredContent = JSON.stringify({
  intro: {
    kicker: "Privacy",
    title: "How we handle your data",
    description: "A short overview.",
    lastReviewedLabel: "Last reviewed",
    lastReviewedDate: "2026-11-01",
    processNote: "Ask us anything.",
  },
  openingCards: [{ kicker: "Rights", title: "Your rights", paragraphs: ["You may ask."] }],
  lifecycle: {
    kicker: "Lifecycle",
    title: "From sign-up to deletion",
    steps: [{ title: "Sign-up", paragraphs: ["We store your profile."] }],
  },
  dataMap: {
    kicker: "Data",
    title: "What we hold",
    cards: [
      {
        title: "Profile",
        dataLabel: "Data",
        data: "Name",
        purposeLabel: "Purpose",
        purpose: "Identity",
        whereLabel: "Where",
        where: "Database",
      },
    ],
  },
  visibility: {
    kicker: "Visibility",
    title: "Who sees it",
    cards: [{ title: "Staff", body: "Yes" }],
  },
  partners: {
    kicker: "Partners",
    title: "Who we share with",
    cards: [{ title: "Steam", body: "Login" }],
  },
  policyCards: [],
});

const publishedGdpr = {
  revision: {
    heroKicker: "",
    heroTitle: "",
    heroDescription: "",
    heroImagePath: null,
    markdown: "Plain **policy**.",
    contentJson: structuredContent,
  },
};

describe("GDPR page", () => {
  beforeAll(prepareBrowser);

  beforeEach(async () => {
    services.publishedPage.mockReset();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("renders one h1 and no hand-rolled button with structured content", async () => {
    services.publishedPage.mockResolvedValue(publishedGdpr);
    const { container } = renderPage(<GdprPage />);

    await screen.findByRole("heading", { name: "How we handle your data", level: 2 });
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("link", { name: "Source code" })).toHaveClass("button");
    expect(container.querySelectorAll("button")).toHaveLength(0);
    for (const link of container.querySelectorAll("a[href^='http']")) {
      expect(link).toHaveClass("button");
    }
  });

  it("renders one h1 in the loading, error and unavailable branches", async () => {
    services.publishedPage.mockReturnValueOnce(new Promise(() => undefined));
    const loading = renderPage(<GdprPage />);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("status", { name: "" })).toBeInTheDocument();
    loading.unmount();

    services.publishedPage.mockRejectedValueOnce(new Error("down"));
    const failed = renderPage(<GdprPage />);
    await screen.findByRole("alert");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    failed.unmount();

    services.publishedPage.mockResolvedValueOnce({ revision: null });
    renderPage(<GdprPage />);
    await screen.findByRole("heading", { name: "Privacy notice unavailable", level: 2 });
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("retries a failed load for real", async () => {
    services.publishedPage.mockRejectedValueOnce(new Error("down"));
    services.publishedPage.mockResolvedValueOnce({ revision: null });
    renderPage(<GdprPage />);

    await userEvent.click(await screen.findByRole("button", { name: "Try again" }));
    await waitFor(() => expect(services.publishedPage).toHaveBeenCalledTimes(2));
  });
});

const reportSummary = (over: Record<string, unknown>) => ({
  primaryKey: String(over.slug),
  title: "Operation recap",
  excerpt: "What happened.",
  category: "Operations",
  heroImagePath: null,
  isFeatured: false,
  sortOrder: 0,
  timeStampPublished: "2026-11-01T10:00:00Z",
  timeStampUpdated: "2026-11-01T10:00:00Z",
  ...over,
});

describe("public field report pages", () => {
  beforeAll(prepareBrowser);

  beforeEach(async () => {
    services.fieldReportList.mockReset();
    services.fieldReportDetail.mockReset();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("filters the list with shared chips and one h1", async () => {
    services.fieldReportList.mockResolvedValue([
      reportSummary({ slug: "alpha", title: "Alpha recap", category: "Operations" }),
      reportSummary({ slug: "bravo", title: "Bravo notes", category: "Training" }),
    ]);
    renderPage(<FieldReportsPage />);

    expect(await screen.findByRole("heading", { name: "Bravo notes", level: 2 })).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const group = screen.getByRole("group", { name: "Field report filters" });
    const all = within(group).getByRole("button", { name: "All" });
    expect(all).toHaveAttribute("aria-pressed", "true");
    expect(all).toHaveClass("button");
    await userEvent.click(within(group).getByRole("button", { name: "Training" }));
    expect(within(group).getByRole("button", { name: "Training" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    const list = screen.getByRole("region", { name: "Field report list" });
    expect(within(list).queryByRole("heading", { name: "Alpha recap", level: 2 })).toBeNull();
    expect(within(list).getByRole("heading", { name: "Bravo notes", level: 2 })).toBeVisible();
  });

  it("shows the shared empty state and a working retry", async () => {
    services.fieldReportList.mockResolvedValueOnce([]);
    const empty = renderPage(<FieldReportsPage />);
    expect(
      await screen.findByRole("heading", { name: "No field reports yet", level: 2 }),
    ).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    empty.unmount();

    services.fieldReportList.mockRejectedValueOnce(new Error("down"));
    services.fieldReportList.mockResolvedValueOnce([]);
    renderPage(<FieldReportsPage />);
    await userEvent.click(await screen.findByRole("button", { name: "Try again" }));
    await waitFor(() => expect(services.fieldReportList).toHaveBeenCalledTimes(3));
  });

  it("renders one h1 on the detail page in every state", async () => {
    const route = "/field-reports/:slug";
    const entry = "/field-reports/alpha";

    services.fieldReportDetail.mockReturnValueOnce(new Promise(() => undefined));
    const loading = renderPage(<FieldReportDetailPage />, route, entry);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    loading.unmount();

    services.fieldReportDetail.mockRejectedValueOnce(new Error("down"));
    const failed = renderPage(<FieldReportDetailPage />, route, entry);
    await screen.findByRole("alert");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
    failed.unmount();

    services.fieldReportDetail.mockResolvedValueOnce(null);
    const missing = renderPage(<FieldReportDetailPage />, route, entry);
    await screen.findByRole("heading", { name: "Field report not found", level: 2 });
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("link", { name: "Back to reports" })).toHaveClass("button");
    missing.unmount();

    services.fieldReportDetail.mockResolvedValueOnce({
      ...reportSummary({ slug: "alpha", title: "Alpha recap" }),
      markdown: "Full text.",
    });
    renderPage(<FieldReportDetailPage />, route, entry);
    expect(await screen.findByRole("heading", { name: "Alpha recap", level: 1 })).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("link", { name: "Back to reports" })).toHaveClass("button");
  });
});
