import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { canManageVereinLedger } from "@/auth/claims";
import { AppToastProvider } from "@/components/AppToasts";
import { Component as VereinKassenbuchEditorPage } from "@/features/admin/VereinKassenbuchEditorPage";
import { Component as VereinKassenbuchPage } from "@/features/admin/VereinKassenbuchPage";
import type { KassenbuchEntry, KassenbuchPage } from "@/features/admin/vereinKassenbuch";
import * as kassenbuch from "@/features/admin/vereinKassenbuch";
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
  nickname: "Treasurer",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: true,
  claims: [{ identifier: "verein:kasse", value: null }],
  roles: [],
} as FullUser;

const entry: KassenbuchEntry = {
  primaryKey: 42,
  timeStampBooked: "2026-08-12T22:00:00Z",
  direction: "Expense",
  amount: 125.5,
  currency: "EUR",
  category: "Infrastructure",
  description: "Voice server renewal",
  reference: "Invoice 42",
  documentNumber: "UTF-2026-42",
  isDeleted: false,
  timeStampCreated: "2026-08-13T10:00:00Z",
  timeStampUpdated: "2026-08-13T10:00:00Z",
  createdByName: "Treasurer",
  updatedByName: "Treasurer",
};

const page: KassenbuchPage = {
  entries: [entry],
  totalCount: 1,
  incomeTotal: 500,
  expenseTotal: 125.5,
  balance: 374.5,
  currency: "EUR",
};

function renderPage(initialEntry: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: "/verein/kassenbuch", element: <p>Member Kassenbuch</p> },
      { path: "/command/verein/kassenbuch", element: <VereinKassenbuchPage /> },
      {
        path: "/command/verein/kassenbuch/create",
        element: <VereinKassenbuchEditorPage />,
      },
      {
        path: "/command/verein/kassenbuch/:entryId/edit",
        element: <VereinKassenbuchEditorPage />,
      },
    ],
    { initialEntries: [initialEntry] },
  );
  const { unmount } = render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
  return { router, unmount };
}

describe("Verein Kassenbuch administration", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = administrator;
    vi.spyOn(kassenbuch, "loadKassenbuchPage").mockResolvedValue(page);
    vi.spyOn(kassenbuch, "findKassenbuchEntry").mockResolvedValue(entry);
    vi.spyOn(kassenbuch, "createKassenbuchEntry").mockResolvedValue({ ...entry, primaryKey: 43 });
    vi.spyOn(kassenbuch, "updateKassenbuchEntry").mockResolvedValue(entry);
    vi.spyOn(kassenbuch, "deleteKassenbuchEntry").mockResolvedValue({
      ...entry,
      isDeleted: true,
    });
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  it("matches the classic Verein-admin affordance and blocks valued ledger claims", async () => {
    const valuedAdmin = {
      ...administrator,
      claims: [{ identifier: "adm:verein", value: "resource-only" }],
    } as FullUser;
    const valuedLedgerClaim = {
      ...administrator,
      claims: [{ identifier: "verein:kasse", value: "resource-only" }],
    } as FullUser;
    expect(canManageVereinLedger(valuedAdmin)).toBe(true);
    expect(canManageVereinLedger(valuedLedgerClaim)).toBe(false);
    expect(canManageVereinLedger(administrator)).toBe(true);
    authState.user = valuedLedgerClaim;

    renderPage("/command/verein/kassenbuch");
    expect(
      await screen.findByRole("heading", { name: "Kassenbuch access required" }),
    ).toBeVisible();
    expect(kassenbuch.loadKassenbuchPage).not.toHaveBeenCalled();
  });

  it("shows filtered totals and applies explicit date, category, and search filters", async () => {
    const user = userEvent.setup();
    renderPage("/command/verein/kassenbuch");

    expect(await screen.findByRole("heading", { name: "Kassenbuch", level: 1 })).toBeVisible();
    expect(await screen.findByRole("row", { name: /Voice server renewal/ })).toHaveTextContent(
      "UTF-2026-42",
    );
    expect(screen.getByText(/374\.50/)).toBeVisible();
    await user.type(screen.getByPlaceholderText("Search ledger entries"), "invoice");
    await user.type(screen.getByLabelText("Category"), "Infrastructure");
    await user.type(screen.getByLabelText("From"), "2026-08-01");
    await user.type(screen.getByLabelText("To"), "2026-08-31");
    await user.click(screen.getByRole("button", { name: "Apply filters" }));

    await waitFor(() =>
      expect(kassenbuch.loadKassenbuchPage).toHaveBeenLastCalledWith(
        expect.objectContaining({
          search: "invoice",
          category: "Infrastructure",
          from: expect.any(String),
          to: expect.any(String),
        }),
        expect.any(AbortSignal),
      ),
    );
  });

  it("validates and creates a localized ledger entry", async () => {
    const user = userEvent.setup();
    renderPage("/command/verein/kassenbuch/create");

    const amountInput = screen.getByRole("spinbutton", { name: "Amount" });
    const currencyInput = screen.getByRole("textbox", { name: "Currency" });
    await user.click(await screen.findByRole("button", { name: "Add entry" }));
    expect(await screen.findByText("Enter an amount greater than zero.")).toBeVisible();
    await user.selectOptions(screen.getByLabelText("Direction"), "Income");
    await user.type(amountInput, "250.75");
    await user.clear(currencyInput);
    await user.type(currencyInput, "usd");
    await user.type(screen.getByLabelText("Category"), "Donations");
    await user.type(screen.getByLabelText("Description"), "Community donation");
    await user.type(screen.getByLabelText("Reference"), "Campaign 2026");
    await user.click(screen.getByRole("button", { name: "Add entry" }));

    await waitFor(() =>
      expect(kassenbuch.createKassenbuchEntry).toHaveBeenCalledWith(
        expect.objectContaining({
          direction: "Income",
          amount: 250.75,
          currency: "USD",
          category: "Donations",
          description: "Community donation",
          reference: "Campaign 2026",
        }),
      ),
    );
    const savedToast = await findToast("Kassenbuch entry saved.");
    expect(savedToast).toHaveClass("app-toast");
    expect(toastPoliteness(savedToast)).toBe("polite");
  });

  it("loads an edit target and keeps destructive deletion behind confirmation", async () => {
    const user = userEvent.setup();
    renderPage("/command/verein/kassenbuch/42/edit");

    const editor = await screen.findByRole("form", { name: "Kassenbuch entry editor" });
    expect(within(editor).getByLabelText("Description")).toHaveValue("Voice server renewal");
    await user.click(screen.getByRole("button", { name: "Delete" }));
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText(/member-visible Kassenbuch/)).toBeVisible();
    expect(kassenbuch.deleteKassenbuchEntry).not.toHaveBeenCalled();
    await user.click(within(dialog).getByRole("button", { name: "Delete entry" }));

    await waitFor(() => expect(kassenbuch.deleteKassenbuchEntry).toHaveBeenCalledWith(42));
    const deletedToast = await findToast("Kassenbuch entry deleted.");
    expect(deletedToast).toHaveClass("app-toast");
    expect(toastPoliteness(deletedToast)).toBe("polite");
  });

  it("renders one h1 above the forbidden and not-found states of the ledger pages", async () => {
    authState.user = undefined;
    for (const path of ["/command/verein/kassenbuch", "/command/verein/kassenbuch/create"]) {
      const { unmount } = renderPage(path);
      expect(
        await screen.findByRole("heading", { name: "Kassenbuch access required" }),
      ).toBeVisible();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      unmount();
    }

    authState.user = administrator;
    renderPage("/command/verein/kassenbuch/not-a-number/edit");
    expect(await screen.findByRole("heading", { name: "Ledger entry not found" })).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("shows an empty state with a next step when no ledger entry matches", async () => {
    vi.mocked(kassenbuch.loadKassenbuchPage).mockResolvedValue({
      ...page,
      entries: [],
      totalCount: 0,
    });
    renderPage("/command/verein/kassenbuch");

    expect(await screen.findByRole("heading", { name: "No ledger entries match" })).toBeVisible();
    expect(screen.getByText("Clear the filters or add a ledger entry.")).toBeVisible();
  });

  it("offers Keep editing and Leave without saving when leaving an edited entry", async () => {
    const user = userEvent.setup();
    const { router } = renderPage("/command/verein/kassenbuch/create");

    await user.type(await screen.findByLabelText("Category"), "Donations");
    await user.click(screen.getByRole("link", { name: "Leave without saving" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "Keep editing" })).toBeVisible();
    expect(router.state.location.pathname).toBe("/command/verein/kassenbuch/create");

    await user.click(within(dialog).getByRole("button", { name: "Keep editing" }));
    expect(router.state.location.pathname).toBe("/command/verein/kassenbuch/create");
    expect(screen.getByLabelText("Category")).toHaveValue("Donations");

    await user.click(screen.getByRole("link", { name: "Leave without saving" }));
    const second = await screen.findByRole("dialog");
    await user.click(within(second).getByRole("button", { name: "Leave without saving" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/command/verein/kassenbuch"));
  });

  it("names the dismiss of the delete dialog after what it keeps and focuses it first", async () => {
    const user = userEvent.setup();
    renderPage("/command/verein/kassenbuch/42/edit");

    await screen.findByRole("form", { name: "Kassenbuch entry editor" });
    await user.click(screen.getByRole("button", { name: "Delete" }));
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "Keep ledger entry" })).toHaveFocus();
    expect(within(dialog).queryByRole("button", { name: "Cancel" })).toBeNull();
  });

  it("renders the command workflow from German resources", async () => {
    await act(async () => i18n.changeLanguage("de"));
    renderPage("/command/verein/kassenbuch");

    expect(await screen.findByRole("button", { name: "Filter anwenden" })).toBeVisible();
    expect(screen.getByRole("link", { name: "Eintrag hinzufügen" })).toBeVisible();
  });
});
