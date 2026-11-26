import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { HostingFinanceAccountPage } from "@/features/hosting/HostingFinanceAccountPage";
import { HostingFinancePage } from "@/features/hosting/HostingFinancePage";
import { HostingFinanceSettingsPage } from "@/features/hosting/HostingFinanceSettingsPage";
import { financeService } from "@/features/hosting/financeService";
import type {
  FinanceCorrectableDeployment,
  FinanceCostOwner,
} from "@/features/hosting/financeTypes";
import { hostingService } from "@/features/hosting/services";
import financeCss from "@/features/hosting/finance.css?raw";
import { costAlertSettings } from "../helpers/costAlertFixtures";

const authState = vi.hoisted(() => ({
  claims: [{ identifier: "adm", value: null }] as { identifier: string; value: null }[],
}));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: { claims: authState.claims, roles: [] },
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
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe("hosting finance routes", () => {
  beforeEach(() => {
    authState.claims = [{ identifier: "adm", value: null }];
    vi.spyOn(hostingService, "costAlertSettings").mockResolvedValue(
      costAlertSettings({ costOwnerId: owner.costOwnerId, costOwnerName: owner.displayName }),
    );
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
    expect(screen.getByText("Page 1 of 1")).toBeInTheDocument();
  });

  it("renders account position and its protocol", async () => {
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);
    expect(await screen.findByRole("heading", { name: "Partner unit" })).toBeInTheDocument();
    expect(await screen.findByText(/No ledger/i)).toBeInTheDocument();
  });

  it("shows the owner's cost alerts between the metrics and the protocol", async () => {
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);

    const heading = await screen.findByRole("heading", { level: 2, name: "Cost alerts" });
    expect(hostingService.costAlertSettings).toHaveBeenCalledWith(
      owner.costOwnerId,
      expect.anything(),
    );
    const section = heading.closest("section") as HTMLElement;
    expect(
      await within(section).findByRole("heading", { level: 3, name: "Alert amounts" }),
    ).toBeVisible();
    expect(within(section).getByLabelText("Alert amount 1 (EUR)")).toHaveValue("5");
    expect(within(section).getByRole("button", { name: "Save alert settings" })).toBeVisible();
    for (const name of ["Alert amounts", "Where alerts go"]) {
      const card = within(section).getByRole("region", { name });
      expect(card).toHaveClass("workspace-panel");
      expect(within(card).getByRole("heading", { level: 3, name })).toBeVisible();
    }
    expect(section.closest(".cost-alerts-page")).toBeNull();
    expect(screen.queryByRole("navigation", { name: /breadcrumb/i })).toBeNull();
    const balance = screen.getByText("Prepaid balance");
    const protocol = await screen.findByText(/No ledger/i);
    expect(
      balance.compareDocumentPosition(heading) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(
      heading.compareDocumentPosition(protocol) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("shows the cost alerts read-only when the viewer may not manage them", async () => {
    vi.spyOn(hostingService, "costAlertSettings").mockResolvedValue(
      costAlertSettings({ costOwnerId: owner.costOwnerId, canManage: false }),
    );
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);

    const field = await screen.findByLabelText("Alert amount 1 (EUR)");
    expect(field).toBeDisabled();
    expect(screen.getByText("You can see these settings but not change them.")).toBeVisible();
    expect(screen.queryByRole("button", { name: "Save alert settings" })).toBeNull();
  });

  it("shows a loading panel and then an error panel in the section without hiding the account", async () => {
    vi.spyOn(hostingService, "costAlertSettings").mockRejectedValue(new Error("down"));
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);

    expect(await screen.findByText("The cost alert settings could not be loaded.")).toBeVisible();
    expect(screen.getByRole("heading", { name: "Partner unit" })).toBeVisible();
    expect(screen.getByRole("heading", { level: 2, name: "Cost alerts" })).toBeVisible();
  });

  it("requests no cost alert settings for a viewer without finance access", async () => {
    authState.claims = [];
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);

    expect(await screen.findByText(/permission to inspect hosting balances/i)).toBeInTheDocument();
    expect(hostingService.costAlertSettings).not.toHaveBeenCalled();
    expect(screen.queryByRole("heading", { name: "Cost alerts" })).toBeNull();
  });

  it("renders global policy and installation protocol", async () => {
    await renderRoute("/command/hosting-finance/settings");
    expect(await screen.findByLabelText(/Protected reserve/i)).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1, name: /Global management/i })).toBeVisible();
  });

  it("clears a failed account creation when the dialog is reopened", async () => {
    const user = userEvent.setup();
    vi.spyOn(financeService, "createOwner").mockRejectedValue(new Error("creation failed"));
    await renderRoute("/command/hosting-finance");

    await user.click(await screen.findByRole("button", { name: "Add account" }));
    const dialog = screen.getByRole("dialog");
    await user.selectOptions(within(dialog).getByLabelText("Type"), "ExternalOrganization");
    await user.type(within(dialog).getByLabelText("Organization name"), "Partner two");
    await user.click(within(dialog).getByRole("button", { name: "Create account" }));
    expect(
      await within(dialog).findByText("The finance action could not be completed."),
    ).toBeVisible();

    await user.click(within(dialog).getByRole("button", { name: "Don't add account" }));
    await user.click(screen.getByRole("button", { name: "Add account" }));
    expect(
      within(dialog).queryByText("The finance action could not be completed."),
    ).not.toBeInTheDocument();
  });
});

describe("hosting finance account dialog and states", () => {
  beforeEach(() => {
    authState.claims = [{ identifier: "adm", value: null }];
    vi.spyOn(hostingService, "costAlertSettings").mockResolvedValue(
      costAlertSettings({ costOwnerId: owner.costOwnerId, costOwnerName: owner.displayName }),
    );
    vi.spyOn(financeService, "owner").mockResolvedValue(owner);
    vi.spyOn(financeService, "ownerProtocol").mockResolvedValue({ entries: [], totalCount: 0 });
  });

  afterEach(() => vi.restoreAllMocks());

  it("opens a correction on the shared dialog shell with a named dismiss label", async () => {
    const user = userEvent.setup();
    vi.spyOn(financeService, "correctableDeployments").mockResolvedValue([]);
    vi.spyOn(financeService, "owners").mockResolvedValue({ owners: [], totalCount: 0 });
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);

    await user.click(await screen.findByRole("button", { name: "Record correction" }));
    const dialog = screen.getByRole("dialog", { name: "Correct deployment cost owner" });
    expect(dialog).toHaveClass("dialog", "finance-dialog");
    expect(within(dialog).getByText("Cost owners")).toHaveClass("kicker");
    expect(within(dialog).getByLabelText(/Provider cost to move/)).toBeRequired();
    expect(within(dialog).getByRole("button", { name: "Keep cost owner" })).toBeVisible();
    expect(within(dialog).queryByRole("button", { name: "Cancel" })).toBeNull();

    const cancel = new Event("cancel", { cancelable: true });
    fireEvent(dialog, cancel);
    expect(cancel.defaultPrevented).toBe(true);
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("submits the payment payload unchanged", async () => {
    const user = userEvent.setup();
    const record = vi.spyOn(financeService, "recordPayment").mockResolvedValue(owner);
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);

    await user.click(await screen.findByRole("button", { name: "Record payment" }));
    const dialog = screen.getByRole("dialog", { name: "Record a received payment" });
    await user.type(within(dialog).getByLabelText(/^Amount \(EUR\)/), "12.5");
    await user.type(within(dialog).getByLabelText(/^Bank transfer reference/), "REF-1");
    fireEvent.change(within(dialog).getByLabelText(/^Effective booking date/), {
      target: { value: "2026-09-10T08:30" },
    });
    await user.type(within(dialog).getByLabelText(/^Reason/), "March transfer");
    await user.click(within(dialog).getByRole("button", { name: "Record a received payment" }));

    expect(record).toHaveBeenCalledWith(
      owner.costOwnerId,
      12.5,
      "REF-1",
      "March transfer",
      new Date("2026-09-10T08:30").toISOString(),
    );
  });

  it("submits the correction payload unchanged", async () => {
    const user = userEvent.setup();
    const deployment = {
      deploymentId: "20000000-0000-0000-0000-000000000002",
      serverTitle: "Operation server",
      remainingCorrectableAmount: 9,
      currency: "EUR",
    } as FinanceCorrectableDeployment;
    const destination = { ...owner, costOwnerId: "30000000-0000-0000-0000-000000000003" };
    vi.spyOn(financeService, "correctableDeployments").mockResolvedValue([deployment]);
    vi.spyOn(financeService, "owners").mockResolvedValue({
      owners: [owner, { ...destination, displayName: "Other unit" }],
      totalCount: 2,
    });
    const correct = vi
      .spyOn(financeService, "correctDeploymentCostOwner")
      .mockResolvedValue({} as never);
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);

    await user.click(await screen.findByRole("button", { name: "Record correction" }));
    const dialog = screen.getByRole("dialog", { name: "Correct deployment cost owner" });
    await user.selectOptions(
      await within(dialog).findByLabelText(/^Deployment ID/),
      deployment.deploymentId,
    );
    await user.selectOptions(
      await within(dialog).findByLabelText(/^Destination account/),
      destination.costOwnerId,
    );
    await user.type(within(dialog).getByLabelText(/^Provider cost to move/), "4.25");
    fireEvent.change(within(dialog).getByLabelText(/^Effective booking date/), {
      target: { value: "2026-09-10T08:30" },
    });
    await user.type(within(dialog).getByLabelText(/^Reason/), "Wrong owner");
    await user.click(within(dialog).getByRole("button", { name: "Correct deployment cost owner" }));

    expect(correct).toHaveBeenCalledWith(
      owner.costOwnerId,
      deployment.deploymentId,
      destination.costOwnerId,
      4.25,
      "Wrong owner",
      new Date("2026-09-10T08:30").toISOString(),
    );
  });

  it("renders exactly one h1 in the loading, forbidden and error states", async () => {
    vi.spyOn(financeService, "owner").mockReturnValue(new Promise(() => undefined));
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(await screen.findByText("Loading...")).toBeVisible();
    cleanup();

    authState.claims = [];
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);
    expect(await screen.findByText(/permission to inspect hosting balances/i)).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    cleanup();

    authState.claims = [{ identifier: "adm", value: null }];
    vi.spyOn(financeService, "owner").mockRejectedValue(new Error("down"));
    await renderRoute(`/command/hosting-finance/accounts/${owner.costOwnerId}`);
    expect(await screen.findByRole("button", { name: "Try again" })).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByText("This couldn't be loaded")).toBeVisible();
  });
});

describe("hosting finance lists on shared controls", () => {
  beforeEach(() => {
    authState.claims = [{ identifier: "adm", value: null }];
    vi.spyOn(financeService, "owners").mockResolvedValue({ owners: [owner], totalCount: 1 });
    vi.spyOn(financeService, "protocol").mockResolvedValue({ entries: [], totalCount: 0 });
    vi.spyOn(financeService, "policy").mockResolvedValue({
      minimumSafetyRangeEur: 10,
      reservationSliceMinutes: 60,
      timeStampUpdated: null,
      updatedById: null,
    });
  });

  afterEach(() => vi.restoreAllMocks());

  it("filters the owners list through the search field, the kind select and the status chips", async () => {
    const user = userEvent.setup();
    await renderRoute("/command/hosting-finance");

    await screen.findByText("Partner unit");
    await user.type(screen.getByRole("searchbox", { name: "Find an account" }), "Par");
    await user.selectOptions(screen.getByRole("combobox", { name: "Type" }), "Verein");
    const frozen = screen.getByRole("button", { name: "Frozen" });
    expect(frozen).toHaveAttribute("aria-pressed", "false");
    await user.click(frozen);

    expect(screen.getByRole("button", { name: "Frozen" })).toHaveAttribute("aria-pressed", "true");
    expect(financeService.owners).toHaveBeenLastCalledWith(
      "Par",
      0,
      20,
      "Verein",
      true,
      expect.anything(),
    );
  });

  it("shows a named empty state and a retryable error on the owners list", async () => {
    vi.spyOn(financeService, "owners").mockResolvedValue({ owners: [], totalCount: 0 });
    await renderRoute("/command/hosting-finance");
    expect(await screen.findByText("No accounts match")).toBeVisible();
    expect(screen.getByText("Try a different search.")).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    cleanup();

    vi.spyOn(financeService, "owners").mockRejectedValue(new Error("down"));
    await renderRoute("/command/hosting-finance");
    expect(await screen.findByRole("button", { name: "Try again" })).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("renders the settings fields as labelled shared fields below one h1", async () => {
    await renderRoute("/command/hosting-finance/settings");
    expect(await screen.findByLabelText(/Protected reserve/i)).toHaveValue(10);
    expect(screen.getByLabelText(/Provider-cost reservation/i)).toHaveValue(60);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("searchbox", { name: "Find an account" })).toBeVisible();
    expect(screen.getByRole("combobox", { name: "Type" })).toBeVisible();
    expect(screen.getByLabelText("From")).toHaveAttribute("type", "date");
    expect(screen.getByLabelText("To (exclusive)")).toHaveAttribute("type", "date");
  });

  it("keeps the focus ring on a focused finance table row", () => {
    expect(financeCss).not.toMatch(/outline:\s*(none|0)\b/);
    expect(financeCss).toMatch(
      /\.finance-table tbody tr:focus-visible\s*\{[^}]*outline:\s*var\(--utf-focus-ring\)/,
    );
  });
});
