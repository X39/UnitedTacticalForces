import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { Component as VereinSettingsPage } from "@/features/admin/VereinSettingsPage";
import {
  createVereinSettingsPayload,
  parseContributionAmount,
} from "@/features/admin/vereinSettingsAdmin";
import * as vereinSettingsAdmin from "@/features/admin/vereinSettingsAdmin";

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

const settings: vereinSettingsAdmin.VereinAdminSettings = {
  membershipContributionMonthlyAmount: 12.5,
  membershipContributionAnnualAmount: 150,
  membershipContributionCurrency: "EUR",
  membershipContributionPublicTextDe: "Deutscher Hinweis",
  membershipContributionPublicTextEn: "English notice",
  timeStampUpdated: "2026-08-13T12:00:00Z",
};

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [{ path: "/command/verein/settings", element: <VereinSettingsPage /> }],
    { initialEntries: ["/command/verein/settings"] },
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

describe("Verein contribution settings", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = administrator;
    vi.spyOn(vereinSettingsAdmin, "loadVereinAdminSettings").mockResolvedValue(settings);
    vi.spyOn(vereinSettingsAdmin, "updateVereinAdminSettings").mockResolvedValue(settings);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  it("normalizes localized positive amounts and paired empty values", () => {
    expect(parseContributionAmount(" 12,50 ")).toBe(12.5);
    expect(parseContributionAmount("1.234,56")).toBe(1234.56);
    expect(parseContributionAmount("1,234.56")).toBe(1234.56);
    expect(parseContributionAmount("")).toBeNull();
    expect(parseContributionAmount("-1")).toBeUndefined();
    expect(
      createVereinSettingsPayload({
        monthlyAmount: "",
        annualAmount: "",
        currency: " eur ",
        publicTextDe: " De ",
        publicTextEn: " En ",
      }),
    ).toEqual({
      membershipContributionMonthlyAmount: null,
      membershipContributionAnnualAmount: null,
      membershipContributionCurrency: "EUR",
      membershipContributionPublicTextDe: "De",
      membershipContributionPublicTextEn: "En",
    });
  });

  it("blocks unauthorized reads", async () => {
    authState.user = undefined;
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Verein permission required" }),
    ).toBeVisible();
    expect(vereinSettingsAdmin.loadVereinAdminSettings).not.toHaveBeenCalled();
  });

  it("loads, validates, previews, and saves the shared Join configuration", async () => {
    const user = userEvent.setup();
    renderPage();

    const monthly = await screen.findByLabelText("Amount per month");
    expect(monthly).toHaveValue("12.5");
    expect(screen.getByText("12.50 EUR per month · 150.00 EUR per year")).toBeVisible();

    await user.clear(monthly);
    await user.type(monthly, "20,50");
    const annual = screen.getByLabelText("Amount per year");
    await user.clear(annual);
    await user.type(annual, "246");
    await user.click(screen.getByRole("button", { name: "Save settings" }));

    await waitFor(() =>
      expect(vereinSettingsAdmin.updateVereinAdminSettings).toHaveBeenCalledWith({
        membershipContributionMonthlyAmount: 20.5,
        membershipContributionAnnualAmount: 246,
        membershipContributionCurrency: "EUR",
        membershipContributionPublicTextDe: "Deutscher Hinweis",
        membershipContributionPublicTextEn: "English notice",
      }),
    );
    const toast = (await screen.findByText("Verein settings saved.")).closest('[role="status"]');
    expect(toast).toHaveClass("app-toast");
  });

  it("localizes paired-amount validation", async () => {
    await act(async () => i18n.changeLanguage("de"));
    const user = userEvent.setup();
    renderPage();

    const annual = await screen.findByLabelText("Betrag pro Jahr");
    await user.clear(annual);
    await user.click(screen.getByRole("button", { name: "Einstellungen speichern" }));
    expect(
      await screen.findAllByText(
        "Bitte gib gültige positive Monats- und Jahresbeträge ein oder lasse beide Felder leer.",
      ),
    ).toHaveLength(2);
  });
});
