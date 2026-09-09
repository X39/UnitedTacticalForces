import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { HostingFinanceAccountPage } from "@/features/hosting/HostingFinanceAccountPage";
import { HostingFinancePage } from "@/features/hosting/HostingFinancePage";
import { HostingFinanceSettingsPage } from "@/features/hosting/HostingFinanceSettingsPage";
import { financeService } from "@/features/hosting/financeService";
import type { FinanceCostOwner } from "@/features/hosting/financeTypes";

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: { claims: [{ identifier: "adm", value: null }], roles: [] },
    isLoading: false,
    isAuthenticated: true,
  }),
}));

const owner = {
  costOwnerId: "10000000-0000-0000-0000-000000000001",
  kind: "ExternalOrganization",
  userId: null,
  displayName: "Partner unit",
  isFrozen: false,
  balance: 100,
  allowance: 200,
  reservedProviderCost: 20,
  spendableAmount: 80,
  currency: "EUR",
  timeStampUpdated: "2026-09-09T10:00:00Z",
  recentLedgerEntries: [],
} as FinanceCostOwner;

async function renderRoute(path: string) {
  await i18nReady;
  const router = createMemoryRouter(
    [
      { path: "/command/hosting-finance", element: <HostingFinancePage /> },
      {
        path: "/command/hosting-finance/accounts/:costOwnerId",
        element: <HostingFinanceAccountPage />,
      },
      { path: "/command/hosting-finance/settings", element: <HostingFinanceSettingsPage /> },
    ],
    { initialEntries: [path] },
  );
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe("hosting finance routes", () => {
  beforeEach(() => {
    vi.spyOn(financeService, "owners").mockResolvedValue({ owners: [owner], totalCount: 1 });
    vi.spyOn(financeService, "owner").mockResolvedValue(owner);
    vi.spyOn(financeService, "protocol").mockResolvedValue({ entries: [], totalCount: 0 });
    vi.spyOn(financeService, "ownerProtocol").mockResolvedValue({ entries: [], totalCount: 0 });
    vi.spyOn(financeService, "policy").mockResolvedValue({
      minimumSafetyRangeEur: 10,
      reservationSliceMinutes: 60,
      timeStampUpdated: null,
      updatedById: null,
    });
  });

  afterEach(() => vi.restoreAllMocks());

  it("renders the account overview as a full finance table", async () => {
    await renderRoute("/command/hosting-finance");
    expect(await screen.findByText("Partner unit")).toBeInTheDocument();
    expect(
      screen.getByRole("columnheader", { name: /Available before reserve/i }),
    ).toBeInTheDocument();
  });

  it("renders account position and its protocol", async () => {
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);
    expect(await screen.findByRole("heading", { name: "Partner unit" })).toBeInTheDocument();
    expect(await screen.findByText(/No ledger/i)).toBeInTheDocument();
  });

  it("renders global policy and installation protocol", async () => {
    await renderRoute("/command/hosting-finance/settings");
    expect(await screen.findByRole("heading", { name: /Global management/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/Protected reserve/i)).toBeInTheDocument();
  });
});
