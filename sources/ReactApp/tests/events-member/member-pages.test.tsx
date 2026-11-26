import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import { ApiError, getApiClient, initializeApiClient } from "@/api/client";
import { eventService, identityService } from "@/api/services";
import type { FullEvent, FullUser, ProfileSettings } from "@/api/types";
import type { PublicConfiguration } from "@/app/config";
import i18n, { i18nReady } from "@/app/i18n";
import { localReturnUrl } from "@/auth/auth";
import { AccountSettingsPage } from "@/features/events-member/AccountSettingsPage";
import { EventDetailPage } from "@/features/events-member/EventDetailPage";
import { eventsMemberService, submitAccountDeletion } from "@/features/events-member/services";
import type { EventSlot, KassenbuchPage } from "@/features/events-member/types";
import { VereinLedgerPage } from "@/features/events-member/VereinLedgerPage";
import { renderWithProviders } from "../helpers/renderWithProviders";

const authState = vi.hoisted(() => ({
  user: undefined as FullUser | undefined,
  isLoading: false,
  providers: { steam: true, discord: true },
  login: vi.fn(),
}));

vi.mock("@/auth/auth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/auth/auth")>()),
  useAuth: () => ({
    user: authState.user,
    isAuthenticated: Boolean(authState.user),
    isLoading: authState.isLoading,
    providers: authState.providers,
    providersLoading: false,
    login: authState.login,
    logout: vi.fn(),
  }),
}));

// Account deletion posts a native form; the tests only need to see that it is asked for.
vi.mock("@/features/events-member/services", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/features/events-member/services")>()),
  submitAccountDeletion: vi.fn(),
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

const eventId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";
const userId = "34b29531-739a-41eb-983c-49c105f3475e";

const member = {
  primaryKey: userId,
  nickname: "Player One",
  avatar: null,
  avatarMimeType: null,
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  claims: [],
  roles: [],
} as unknown as FullUser;

const operation = {
  primaryKey: eventId,
  title: "Operation Copper Trail",
  description: "## Situation\nProceed through the **northern approach**.",
  terrainTitle: "Altis",
  modPackDefinitionTitle: "UTF Main",
  modPackRevisionPrimaryKey: 24,
  imagePath: "",
  terrainImagePath: "",
  scheduledFor: "2030-09-12T18:00:00Z",
  isVisible: true,
  acceptedCount: 3,
  rejectedCount: 1,
  maybeCount: 2,
  minimumAccepted: 12,
  owner: { primaryKey: userId, nickname: "Player One" },
  hostedBy: { primaryKey: "2fdd4802-e081-4baa-9120-f1873653a057", nickname: "Raven" },
} as unknown as FullEvent;

const slots = [
  {
    slotNumber: 1,
    assignedToFk: userId,
    eventFk: eventId,
    isSelfAssignable: true,
    isVisible: true,
    group: "Alpha",
    title: "Team Lead",
    roleSymbol: "Rifleman",
    side: "West",
  },
  {
    slotNumber: 2,
    assignedToFk: null,
    eventFk: eventId,
    isSelfAssignable: true,
    isVisible: true,
    group: "Alpha",
    title: "Rifleman",
    roleSymbol: "Rifleman",
    side: "West",
  },
] as unknown as EventSlot[];

/** Returns the visible headings of the rendered page in document order. */
function headings(): HTMLElement[] {
  return screen.getAllByRole("heading");
}

/** Renders the event detail page on its route for the given event id. */
function renderEvent(id: string = eventId) {
  return renderWithProviders(<EventDetailPage />, {
    route: `/events/${id}`,
    path: "/events/:eventId",
  });
}

describe("event detail page", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    authState.user = member;
    authState.isLoading = false;
    vi.spyOn(eventService, "detail").mockResolvedValue(operation);
    vi.spyOn(eventsMemberService, "slots").mockResolvedValue(slots);
    vi.spyOn(eventsMemberService, "assignees").mockResolvedValue(new Map());
    vi.spyOn(eventsMemberService, "modPackFormats").mockResolvedValue([]);
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the title as the one h1 of the public hero, ahead of every other heading", async () => {
    const { container } = renderEvent();

    const title = await screen.findByRole("heading", { level: 1, name: "Operation Copper Trail" });
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(headings()[0]).toBe(title);
    expect(title.closest(".public-hero")).not.toBeNull();
    expect(container.querySelectorAll("h1")).toHaveLength(1);
  });

  it("renders one h1 above the loading state", async () => {
    vi.spyOn(eventService, "detail").mockReturnValue(new Promise(() => undefined));
    renderEvent();

    expect(await screen.findByRole("status")).toHaveTextContent("Operation is loading.");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(headings()[0]).toHaveProperty("tagName", "H1");
  });

  it("renders one h1 above the not-found state for an unknown or malformed operation", async () => {
    vi.spyOn(eventService, "detail").mockResolvedValue(null as unknown as FullEvent);
    const { unmount } = renderEvent();

    expect(await screen.findByText("Operation unavailable")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("link", { name: "Back to operations" })).toHaveAttribute(
      "href",
      "/events/upcoming",
    );
    unmount();

    renderEvent("not-an-event-id");

    expect(await screen.findByText("Operation unavailable")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("renders one h1 above the forbidden state", async () => {
    vi.spyOn(eventService, "detail").mockRejectedValue(new ApiError("Forbidden", 403, "forbidden"));
    renderEvent();

    expect(await screen.findByText("You don't have access to this page")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("renders one h1 above the error state and retries both requests", async () => {
    const user = userEvent.setup();
    const detail = vi
      .spyOn(eventService, "detail")
      .mockRejectedValue(new ApiError("Server error", 500, "server"));
    const slotsRequest = vi.spyOn(eventsMemberService, "slots");
    renderEvent();

    const alert = await screen.findByRole("alert");
    expect(within(alert).getByRole("heading", { name: "Operation unavailable" })).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const detailCalls = detail.mock.calls.length;
    const slotCalls = slotsRequest.mock.calls.length;

    await user.click(within(alert).getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(detail.mock.calls.length).toBeGreaterThan(detailCalls));
    expect(slotsRequest.mock.calls.length).toBeGreaterThan(slotCalls);
  });

  it("names what each dismiss keeps instead of a generic cancel", async () => {
    const user = userEvent.setup();
    renderEvent();
    await screen.findByRole("heading", { level: 1, name: "Operation Copper Trail" });

    await user.click(screen.getByRole("button", { name: /Pending/ }));

    const dialog = document.querySelector("dialog");
    expect(dialog).not.toBeNull();
    const dismiss = within(dialog!).getByRole("button", { name: "Keep slot", hidden: true });
    expect(dismiss).toHaveClass("button");
    expect(dismiss).toHaveFocus();
    expect(within(dialog!).queryByText("Cancel")).toBeNull();
  });

  it("explains empty briefing and slot panels with a title and a next step", async () => {
    vi.spyOn(eventService, "detail").mockResolvedValue({ ...operation, description: "" });
    vi.spyOn(eventsMemberService, "slots").mockResolvedValue([]);
    renderEvent();

    expect(await screen.findByRole("heading", { name: "No briefing yet" })).toBeVisible();
    expect(
      screen.getByText("The organizers post the full briefing before the operation starts."),
    ).toBeVisible();
    expect(screen.getByRole("heading", { name: "No slots yet" })).toBeVisible();
    expect(screen.getByText("Slots appear here once the organizers configure them.")).toBeVisible();
  });

  it("renders every control of the page as a shared action control", async () => {
    const { container } = renderEvent();
    await screen.findByRole("heading", { level: 1, name: "Operation Copper Trail" });

    const buttons = Array.from(container.querySelectorAll("button"));
    expect(buttons.length).toBeGreaterThan(0);
    for (const button of buttons) expect(button).toHaveClass("button");
  });
});

const profile = {
  primaryKey: userId,
  nickname: "Player One",
  eMail: "player@example.test",
  avatar: "",
  avatarMimeType: "",
  isVerified: true,
  isVereinMember: false,
  vereinMembershipStatus: null,
  vereinMemberNumber: null,
  canDeleteAccount: true,
  accountDeletionBlockedReason: null,
  steamId64: null,
  discordId: null,
  discordUsername: null,
  firstName: null,
  lastName: null,
  dateOfBirth: null,
  phoneNumber: null,
  addressLine1: null,
  addressLine2: null,
  postalCode: null,
  city: null,
  country: null,
} as ProfileSettings;

/** The field labels of the account form, as the English locale spells them. */
const accountFieldLabels = [
  "Nickname",
  "Email",
  "First name",
  "Last name",
  "Date of birth",
  "Phone",
  "Address line 1",
  "Address line 2",
  "Postal code",
  "City",
  "Country",
];

/** Renders the account settings page as a signed-in member. */
function renderAccount() {
  return renderWithProviders(<AccountSettingsPage />, { route: "/settings", path: "/settings" });
}

describe("account settings page", () => {
  beforeAll(async () => {
    await i18nReady;
    initializeApiClient({
      Api: { BaseUrl: "http://localhost/api/" },
      Urls: { TeamSpeak3: "", Discord: "" },
      Ui: { ReactVariantEnabled: false, ReactBasePath: "/" },
    } as PublicConfiguration);
  });

  beforeEach(async () => {
    authState.user = member;
    authState.isLoading = false;
    authState.providers = { steam: true, discord: true };
    authState.login.mockReset();
    vi.mocked(submitAccountDeletion).mockReset();
    vi.spyOn(identityService, "profileSettings").mockResolvedValue(profile);
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders one page h1 and keeps every section heading below it", async () => {
    renderAccount();

    await screen.findByLabelText("Nickname");
    const title = screen.getByRole("heading", { level: 1, name: "Settings" });
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getAllByRole("heading")[0]).toBe(title);
    expect(screen.getAllByRole("heading", { level: 2 }).length).toBeGreaterThanOrEqual(4);
  });

  it("renders every account field as a shared field with its label", async () => {
    const { container } = renderAccount();
    await screen.findByLabelText("Nickname");

    for (const label of accountFieldLabels) {
      const control = screen.getByLabelText(label);
      expect(control, label).toHaveClass("form-field__control");
      expect(control.closest(".form-field"), label).not.toBeNull();
    }
    expect(container.querySelectorAll("label.field")).toHaveLength(0);
    for (const button of Array.from(container.querySelectorAll("button"))) {
      expect(button).toHaveClass("button");
    }
  });

  it("names the avatar chooser and restricts it to images", async () => {
    renderAccount();
    await screen.findByLabelText("Nickname");

    expect(screen.getByRole("button", { name: "Set avatar" })).toHaveClass("button");
    expect(screen.getByLabelText("Set avatar", { selector: "input" })).toHaveAttribute(
      "accept",
      "image/*",
    );
  });

  it("shows the field error next to the nickname and keeps the form unsaved", async () => {
    const user = userEvent.setup();
    const update = vi.spyOn(eventsMemberService, "updateProfileSettings");
    renderAccount();
    const nickname = await screen.findByLabelText("Nickname");

    await user.clear(nickname);
    await user.click(screen.getByRole("button", { name: "Save settings" }));

    expect(await screen.findByText("Nickname is required.")).toBeVisible();
    expect(nickname).toHaveAttribute("aria-invalid", "true");
    expect(update).not.toHaveBeenCalled();
  });

  it("saves the same payload the form held before the conversion", async () => {
    const user = userEvent.setup();
    const update = vi
      .spyOn(eventsMemberService, "updateProfileSettings")
      .mockResolvedValue(profile);
    renderAccount();
    const nickname = await screen.findByLabelText("Nickname");

    await user.clear(nickname);
    await user.type(nickname, "  Player Two ");
    await user.type(screen.getByLabelText("City"), "Berlin");
    await user.click(screen.getByRole("button", { name: "Save settings" }));

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    expect(update.mock.calls[0]?.[0]).toEqual({
      avatar: null,
      avatarMimeType: null,
      nickname: "Player Two",
      eMail: "player@example.test",
      firstName: "",
      lastName: "",
      dateOfBirth: null,
      phoneNumber: "",
      addressLine1: "",
      addressLine2: "",
      postalCode: "",
      city: "Berlin",
      country: "",
    });
  });

  it("names keeping the account in the deletion dialog and puts focus there", async () => {
    const user = userEvent.setup();
    renderAccount();
    await screen.findByLabelText("Nickname");

    await user.click(screen.getByRole("button", { name: "Delete account" }));

    const dialog = document.querySelector("dialog");
    expect(dialog).not.toBeNull();
    const dismiss = within(dialog!).getByRole("button", { name: "Keep account", hidden: true });
    expect(dismiss).toHaveFocus();
    expect(within(dialog!).queryByText("Cancel")).toBeNull();
    const confirm = within(dialog!).getByRole("button", {
      name: "Yes, I am sure I want to delete my account",
      hidden: true,
    });
    expect(confirm).toHaveClass("button--danger");

    await user.click(dismiss);
    expect(submitAccountDeletion).not.toHaveBeenCalled();
  });

  it("submits the account deletion with the unchanged local return URL only after the confirm", async () => {
    const user = userEvent.setup();
    renderAccount();
    await screen.findByLabelText("Nickname");

    await user.click(screen.getByRole("button", { name: "Delete account" }));
    const dialog = document.querySelector("dialog")!;
    await user.click(
      within(dialog).getByRole("button", {
        name: "Yes, I am sure I want to delete my account",
        hidden: true,
      }),
    );

    expect(submitAccountDeletion).toHaveBeenCalledTimes(1);
    expect(submitAccountDeletion).toHaveBeenCalledWith(
      localReturnUrl(getApiClient().url("users/me/delete")),
    );
  });

  it("keeps the delete action unavailable when the account cannot be deleted", async () => {
    vi.spyOn(identityService, "profileSettings").mockResolvedValue({
      ...profile,
      canDeleteAccount: false,
    });
    renderAccount();
    await screen.findByLabelText("Nickname");

    expect(screen.getByRole("button", { name: "Delete account" })).toBeDisabled();
  });

  it("renders one h1 above the loading state", async () => {
    vi.spyOn(identityService, "profileSettings").mockReturnValue(new Promise(() => undefined));
    renderAccount();

    expect(await screen.findByRole("status")).toHaveTextContent("Loading...");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getAllByRole("heading")[0]).toHaveProperty("tagName", "H1");
  });

  it("renders one h1 above the sign-in state and offers the provider sign-in", async () => {
    const user = userEvent.setup();
    authState.user = undefined;
    renderAccount();

    expect(await screen.findByText("Sign in to manage your account")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    expect(authState.login).toHaveBeenCalledWith("steam");
  });

  it("renders one h1 above the error state and retries the real request", async () => {
    const user = userEvent.setup();
    const request = vi
      .spyOn(identityService, "profileSettings")
      .mockRejectedValue(new ApiError("Server error", 500, "server"));
    renderAccount();

    const alert = await screen.findByRole("alert");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const calls = request.mock.calls.length;

    await user.click(within(alert).getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(request.mock.calls.length).toBeGreaterThan(calls));
  });
});

const ledgerMember = { ...member, isVereinMember: true } as unknown as FullUser;

const ledgerPage = {
  entries: [
    {
      primaryKey: "e1",
      direction: "Income",
      amount: 25,
      currency: "EUR",
      category: "Dues",
      description: "Annual dues",
      reference: "R-1",
      documentNumber: null,
      timeStampBooked: "2026-03-01T00:00:00Z",
    },
    {
      primaryKey: "e2",
      direction: "Expense",
      amount: 10,
      currency: "EUR",
      category: "Hosting",
      description: "Server rental",
      reference: null,
      documentNumber: "D-7",
      timeStampBooked: "2026-03-02T00:00:00Z",
    },
  ],
  totalCount: 2,
  incomeTotal: 25,
  expenseTotal: 10,
  balance: 15,
  currency: "EUR",
} as unknown as KassenbuchPage;

/** Renders the Verein ledger page. */
function renderLedger() {
  return renderWithProviders(<VereinLedgerPage />, {
    route: "/verein/kassenbuch",
    path: "/verein/kassenbuch",
  });
}

describe("Verein ledger page", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    authState.user = ledgerMember;
    authState.isLoading = false;
    authState.providers = { steam: true, discord: true };
    authState.login.mockReset();
    vi.spyOn(eventsMemberService, "vereinLedger").mockResolvedValue(ledgerPage);
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders one page h1 first and the ledger section below it as an h2", async () => {
    renderLedger();

    const title = await screen.findByRole("heading", { level: 1, name: "Kassenbuch" });
    expect(await screen.findByText("Annual dues")).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getAllByRole("heading")[0]).toBe(title);
    expect(screen.getAllByRole("heading", { level: 2 }).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByRole("table")).toHaveAccessibleName();
    expect(screen.getAllByRole("columnheader")).toHaveLength(5);
  });

  it("renders every control as a shared action control", async () => {
    const { container } = renderLedger();
    await screen.findByText("Annual dues");

    expect(screen.getByRole("link", { name: "Back to Verein" })).toHaveClass("button");
    for (const button of Array.from(container.querySelectorAll("button"))) {
      expect(button).toHaveClass("button");
    }
  });

  it("explains an empty ledger with a title and a next step", async () => {
    const user = userEvent.setup();
    const request = vi
      .spyOn(eventsMemberService, "vereinLedger")
      .mockResolvedValue({ ...ledgerPage, entries: [], totalCount: 0 });
    renderLedger();

    const empty = await screen.findByRole("heading", { name: "No ledger entries" });
    expect(empty).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const calls = request.mock.calls.length;

    await user.click(screen.getByRole("button", { name: "Refresh" }));

    await waitFor(() => expect(request.mock.calls.length).toBeGreaterThan(calls));
  });

  it("renders one h1 above the loading state", async () => {
    vi.spyOn(eventsMemberService, "vereinLedger").mockReturnValue(new Promise(() => undefined));
    renderLedger();

    expect(await screen.findByRole("status")).toHaveTextContent("Loading Kassenbuch");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("renders one h1 above the sign-in state and offers the provider sign-in", async () => {
    const user = userEvent.setup();
    authState.user = undefined;
    renderLedger();

    expect(await screen.findByText("Sign in required")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    expect(authState.login).toHaveBeenCalledWith("steam");
  });

  it("renders one h1 above the membership-required state and never asks for the ledger", async () => {
    const request = vi.spyOn(eventsMemberService, "vereinLedger");
    authState.user = member;
    renderLedger();

    expect(await screen.findByText("Membership required")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(request).not.toHaveBeenCalled();
  });

  it("renders one h1 above the error state and retries the real request", async () => {
    const user = userEvent.setup();
    const request = vi
      .spyOn(eventsMemberService, "vereinLedger")
      .mockRejectedValue(new ApiError("Server error", 500, "server"));
    renderLedger();

    const alert = await screen.findByRole("alert");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    const calls = request.mock.calls.length;

    await user.click(within(alert).getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(request.mock.calls.length).toBeGreaterThan(calls));
  });
});
