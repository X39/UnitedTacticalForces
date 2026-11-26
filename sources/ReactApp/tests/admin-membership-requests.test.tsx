import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { Component as MembershipRequestsPage } from "@/features/admin/VereinMembershipRequestsPage";
import {
  createMembershipTransition,
  membershipRequestMatchesView,
  normalizeMembershipRequest,
  normalizeMembershipRequestStatus,
  sortMembershipRequests,
  type MembershipRequest,
} from "@/features/admin/vereinMembershipRequests";
import * as membershipRequests from "@/features/admin/vereinMembershipRequests";
import { findToast, toastPoliteness } from "./helpers/toastQueries";

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
  nickname: "Verein admin",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: true,
  claims: [{ identifier: "verein:members", value: null }],
  roles: [],
} as FullUser;

const submittedRequest: MembershipRequest = {
  primaryKey: 41,
  userFk: "20000000-0000-4000-8000-000000000002",
  userNickname: "Falcon",
  firstName: "Ada",
  lastName: "Lovelace",
  dateOfBirth: "1992-04-18",
  email: "ada@example.test",
  phoneNumber: "+49 1234",
  addressLine1: "Main road 5",
  addressLine2: "",
  postalCode: "10115",
  city: "Berlin",
  country: "Germany",
  motivation: "I want to support the association.",
  acknowledgesMembershipSeparation: true,
  acknowledgesDataProcessing: true,
  acknowledgesSatzung: true,
  status: "Submitted",
  decisionReason: "",
  reviewedByFk: null,
  reviewedByName: null,
  timeStampCreated: "2026-08-12T10:00:00Z",
  timeStampUpdated: "2026-08-12T10:00:00Z",
  timeStampResolved: null,
};

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: "/verein", element: <p>Public Verein</p> },
      {
        path: "/command/verein/membership-requests",
        element: <MembershipRequestsPage />,
      },
    ],
    { initialEntries: ["/command/verein/membership-requests"] },
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

describe("Verein membership request administration", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = administrator;
    vi.spyOn(membershipRequests, "loadMembershipRequests").mockResolvedValue([submittedRequest]);
    vi.spyOn(membershipRequests, "transitionMembershipRequest").mockResolvedValue({
      ...submittedRequest,
      status: "Approved",
      decisionReason: "Application reviewed",
      reviewedByName: "Verein admin",
      timeStampUpdated: "2026-08-13T10:00:00Z",
      timeStampResolved: "2026-08-13T10:00:00Z",
    });
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  it("normalizes legacy statuses and transition payloads", () => {
    expect(normalizeMembershipRequestStatus(1)).toBe("AcceptedForContact");
    expect(normalizeMembershipRequestStatus("3")).toBe("Rejected");
    expect(
      normalizeMembershipRequest({ status: 2 } as unknown as Parameters<
        typeof normalizeMembershipRequest
      >[0]).status,
    ).toBe("Approved");
    expect(createMembershipTransition("Rejected", " No quorum ", "ignored", "ignored")).toEqual({
      status: "Rejected",
      reason: "No quorum",
      memberNumber: null,
      notes: null,
    });
    expect(createMembershipTransition("Approved", " Accepted ", " UTF-42 ", " Note ")).toEqual({
      status: "Approved",
      reason: "Accepted",
      memberNumber: "UTF-42",
      notes: "Note",
    });
    expect(createMembershipTransition("Approved", " ", "", "")).toBeUndefined();
  });

  it("filters and prioritizes open requests", () => {
    const approved = {
      ...submittedRequest,
      primaryKey: 42,
      status: "Approved",
      timeStampUpdated: "2026-08-13T12:00:00Z",
    } satisfies MembershipRequest;
    expect(membershipRequestMatchesView(submittedRequest, "open")).toBe(true);
    expect(membershipRequestMatchesView(approved, "open")).toBe(false);
    expect(
      sortMembershipRequests([approved, submittedRequest]).map((row) => row.primaryKey),
    ).toEqual([41, 42]);
  });

  it("blocks unauthorized reads", async () => {
    authState.user = undefined;
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Membership access required" }),
    ).toBeVisible();
    expect(membershipRequests.loadMembershipRequests).not.toHaveBeenCalled();
  });

  it("renders one h1 that equals the navigation label above every state", async () => {
    renderPage();

    expect(await screen.findAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Membership requests" })).toBeVisible();
  });

  it("renders one h1 above the forbidden state", () => {
    authState.user = undefined;
    renderPage();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent(
      "Membership access required",
    );
  });

  it("renders one h1 above the loading state", () => {
    vi.spyOn(membershipRequests, "loadMembershipRequests").mockReturnValue(
      new Promise(() => undefined),
    );
    renderPage();

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("status")).toHaveTextContent("Loading Verein workspace");
  });

  it("shows the error state with a retry that refetches and no misleading empty queue", async () => {
    const user = userEvent.setup();
    vi.spyOn(membershipRequests, "loadMembershipRequests").mockRejectedValue(new Error("down"));
    renderPage();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Membership requests could not be loaded: down");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.queryByText("No membership requests match")).not.toBeInTheDocument();

    await user.click(within(alert).getByRole("button", { name: "Try again" }));
    await waitFor(() => expect(membershipRequests.loadMembershipRequests).toHaveBeenCalledTimes(2));
  });

  it("names how to clear the filter when no request matches", async () => {
    vi.spyOn(membershipRequests, "loadMembershipRequests").mockResolvedValue([]);
    renderPage();

    expect(
      await screen.findByRole("heading", { level: 2, name: "No membership requests match" }),
    ).toBeVisible();
    expect(screen.getByText("Clear the search or filters to see all requests.")).toBeVisible();
  });

  it("takes every request status tone from the membership request map", async () => {
    const user = userEvent.setup();
    const statuses = [
      "Submitted",
      "AcceptedForContact",
      "Approved",
      "Rejected",
      "Withdrawn",
    ] as const;
    vi.spyOn(membershipRequests, "loadMembershipRequests").mockResolvedValue(
      statuses.map((status, index) => ({
        ...submittedRequest,
        primaryKey: 50 + index,
        firstName: `Applicant${index}`,
        status,
      })),
    );
    renderPage();

    await screen.findByLabelText("Selected membership request");
    await user.click(screen.getByRole("button", { name: /^All statuses/ }));
    const tones = ["warning", "info", "success", "danger", "neutral"];
    const labels = ["Submitted", "Accepted for contact", "Approved", "Rejected", "Withdrawn"];
    labels.forEach((label, index) => {
      const row = screen.getByRole("row", { name: new RegExp(`Applicant${index}`) });
      expect(within(row).getByText(label)).toHaveClass(`status-badge--${tones[index]}`);
    });
  });

  it("filters through counted chips of one pressed group", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByLabelText("Selected membership request");
    const group = screen.getByRole("group", { name: "Membership request filters" });
    expect(within(group).getByRole("button", { name: /^Open/ })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    await user.click(within(group).getByRole("button", { name: /^Approved/ }));
    expect(within(group).getByRole("button", { name: /^Approved/ })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(await screen.findByText("No request selected")).toBeVisible();
  });

  it("loads applicant evidence and records an approval", async () => {
    const user = userEvent.setup();
    renderPage();

    const detail = await screen.findByLabelText("Selected membership request");
    expect(within(detail).getByRole("heading", { name: "Ada Lovelace" })).toBeVisible();
    expect(within(detail).getByText("I want to support the association.")).toBeVisible();
    expect(within(detail).getAllByText("Yes")).toHaveLength(3);

    await user.type(within(detail).getByLabelText("Decision reason"), "Application reviewed");
    await user.type(within(detail).getByLabelText("Member number"), "UTF-42");
    await user.type(within(detail).getByLabelText("Internal notes"), "Welcome pack pending");
    await user.click(within(detail).getByRole("button", { name: "Approve" }));

    await waitFor(() =>
      expect(membershipRequests.transitionMembershipRequest).toHaveBeenCalledWith(41, {
        status: "Approved",
        reason: "Application reviewed",
        memberNumber: "UTF-42",
        notes: "Welcome pack pending",
      }),
    );
    const toast = await findToast("Membership request updated.");
    expect(toast).toHaveClass("app-toast");
    expect(toastPoliteness(toast)).toBe("polite");
    expect(within(detail).getByText("Approved")).toBeVisible();
  });
});
