import { beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ApiError } from "@/api/client";
import {
  getCommandNavigationContext,
  isCommandNavigationItemActive,
} from "@/components/commandNavigation";
import i18n, { i18nReady } from "@/app/i18n";
import { CostAlertsPage } from "@/features/hosting/CostAlertsPage";
import { hostingService } from "@/features/hosting/services";
import { costAlertSettings } from "../helpers/costAlertFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

const ownerId = "c0000000-0000-4000-8000-000000000009";

/** Renders the page on its member route with an optional search string. */
function renderPage(search = "") {
  return renderWithProviders(<CostAlertsPage />, {
    path: "/command/servers/cost-alerts",
    route: `/command/servers/cost-alerts${search}`,
  });
}

describe("Cost alerts page", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("loads the personal settings without an owner and shows the page chrome", async () => {
    const personal = vi
      .spyOn(hostingService, "personalCostAlertSettings")
      .mockResolvedValue(costAlertSettings({ costOwnerName: "Falcon One" }));
    const byOwner = vi.spyOn(hostingService, "costAlertSettings");
    renderPage();

    expect(await screen.findByRole("heading", { level: 2, name: "Alert amounts" })).toBeVisible();
    expect(screen.getByRole("heading", { level: 1, name: "Cost alerts" })).toBeVisible();
    expect(personal).toHaveBeenCalledTimes(1);
    expect(byOwner).not.toHaveBeenCalled();
    const breadcrumbs = screen.getByRole("navigation", { name: "Breadcrumb" });
    expect(within(breadcrumbs).getByRole("link", { name: "Servers" })).toHaveAttribute(
      "href",
      "/command/servers",
    );
    expect(within(breadcrumbs).getByText("Cost alerts")).toHaveAttribute("aria-current", "page");
    expect(screen.getByText("Falcon One").tagName).toBe("BDI");
    expect(screen.getByText(/Cost owner:/)).toBeVisible();
    expect(screen.getByRole("heading", { level: 2, name: "Alert amounts" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Save alert settings" })).toBeVisible();
  });

  it("keeps the page in a capped column that holds both cards", async () => {
    vi.spyOn(hostingService, "personalCostAlertSettings").mockResolvedValue(costAlertSettings());
    const { container } = renderPage();

    await screen.findByRole("heading", { level: 2, name: "Alert amounts" });
    const column = container.querySelector(".cost-alerts-page");
    expect(column).not.toBeNull();
    expect(column?.querySelectorAll("section.workspace-panel")).toHaveLength(2);
  });

  it("loads the settings of the owner in the query string", async () => {
    const byOwner = vi
      .spyOn(hostingService, "costAlertSettings")
      .mockResolvedValue(costAlertSettings({ costOwnerId: ownerId }));
    renderPage(`?owner=${ownerId}`);

    expect(await screen.findByRole("heading", { level: 1, name: "Cost alerts" })).toBeVisible();
    expect(byOwner).toHaveBeenCalledWith(ownerId, expect.anything());
  });

  it("states that the viewer has no cost owner account on a 204", async () => {
    vi.spyOn(hostingService, "personalCostAlertSettings").mockResolvedValue(null);
    renderPage();

    expect(
      await screen.findByText(
        "You have no cost owner account yet. Cost alerts apply to the account that pays for a server.",
      ),
    ).toBeVisible();
    expect(screen.queryByRole("button", { name: "Save alert settings" })).not.toBeInTheDocument();
  });

  it("shows not found for a malformed owner without a request", async () => {
    const personal = vi.spyOn(hostingService, "personalCostAlertSettings");
    const byOwner = vi.spyOn(hostingService, "costAlertSettings");
    renderPage("?owner=not-a-guid");

    expect(await screen.findByText("That cost owner was not found.")).toBeVisible();
    expect(personal).not.toHaveBeenCalled();
    expect(byOwner).not.toHaveBeenCalled();
  });

  it("maps 404 to not found and 403 to forbidden", async () => {
    vi.spyOn(hostingService, "costAlertSettings").mockRejectedValueOnce(
      new ApiError("missing", 404, "not-found"),
    );
    const first = renderPage(`?owner=${ownerId}`);
    expect(await screen.findByText("That cost owner was not found.")).toBeVisible();
    first.unmount();

    vi.spyOn(hostingService, "costAlertSettings").mockRejectedValueOnce(
      new ApiError("denied", 403, "forbidden"),
    );
    renderPage(`?owner=${ownerId}`);
    expect(
      await screen.findByText("You cannot view the cost alerts of this account."),
    ).toBeVisible();
  });

  it("shows the load error with Try again and recovers on retry", async () => {
    const user = userEvent.setup();
    const personal = vi
      .spyOn(hostingService, "personalCostAlertSettings")
      .mockRejectedValueOnce(new ApiError("boom", 500, "server"))
      .mockResolvedValue(costAlertSettings());
    renderPage();

    expect(await screen.findByText("The cost alert settings could not be loaded.")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByRole("heading", { level: 2, name: "Alert amounts" })).toBeVisible();
    expect(personal).toHaveBeenCalledTimes(2);
  });

  it("shows the generic load error title under one h1, never the fleet title", async () => {
    vi.spyOn(hostingService, "personalCostAlertSettings").mockRejectedValue(
      new ApiError("boom", 500, "server"),
    );
    renderPage();

    expect(await screen.findByText("This couldn't be loaded")).toBeVisible();
    expect(screen.getByRole("alert")).not.toHaveTextContent(/servers/i);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Cost alerts" })).toBeVisible();
  });

  it("renders one h1 above the loading, forbidden, not-found and no-owner states", async () => {
    let release: (value: ReturnType<typeof costAlertSettings>) => void = () => undefined;
    vi.spyOn(hostingService, "personalCostAlertSettings").mockReturnValue(
      new Promise((resolve) => {
        release = resolve;
      }),
    );
    const loading = renderPage();
    const loadingHeading = screen.getByRole("heading", { level: 1, name: "Cost alerts" });
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByText("Loading cost alert settings...")).toBeVisible();
    release(costAlertSettings());
    expect(await screen.findByRole("heading", { level: 2, name: "Alert amounts" })).toBeVisible();
    expect(screen.getByRole("heading", { level: 1, name: "Cost alerts" })).toBe(loadingHeading);
    loading.unmount();

    vi.spyOn(hostingService, "costAlertSettings").mockRejectedValueOnce(
      new ApiError("denied", 403, "forbidden"),
    );
    const forbidden = renderPage(`?owner=${ownerId}`);
    expect(
      await screen.findByText("You cannot view the cost alerts of this account."),
    ).toBeVisible();
    expect(screen.getByText("You don't have access to this page")).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    forbidden.unmount();

    const notFound = renderPage("?owner=not-a-guid");
    expect(await screen.findByText("That cost owner was not found.")).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    notFound.unmount();

    vi.spyOn(hostingService, "personalCostAlertSettings").mockResolvedValue(null);
    renderPage();
    expect(await screen.findByText(/You have no cost owner account yet/)).toBeVisible();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("shows the loading panel and no form control until the settings have loaded", async () => {
    let release: (value: ReturnType<typeof costAlertSettings>) => void = () => undefined;
    vi.spyOn(hostingService, "personalCostAlertSettings").mockReturnValue(
      new Promise((resolve) => {
        release = resolve;
      }),
    );
    renderPage();

    expect(screen.getByText("Loading cost alert settings...")).toBeVisible();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    expect(screen.queryByRole("switch")).not.toBeInTheDocument();
    release(costAlertSettings());
    expect(await screen.findByRole("heading", { level: 2, name: "Alert amounts" })).toBeVisible();
  });

  it("adds an amount and saves it with the revision, flags and English language", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "personalCostAlertSettings").mockResolvedValue(
      costAlertSettings({ revision: 3, thresholds: [], discordEnabled: true, emailEnabled: false }),
    );
    const update = vi
      .spyOn(hostingService, "updateCostAlertSettings")
      .mockResolvedValue(costAlertSettings({ revision: 4, thresholds: [5], emailEnabled: false }));
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Add an amount" }));
    await user.type(screen.getByLabelText("Alert amount 1 (EUR)"), "5");
    await user.click(screen.getByRole("button", { name: "Save alert settings" }));

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    expect(update).toHaveBeenCalledWith("c0000000-0000-4000-8000-000000000001", {
      revision: 3,
      thresholds: [5],
      discordEnabled: true,
      emailEnabled: false,
      language: "en",
    });
    expect(await screen.findByText("Saved.")).toBeVisible();
    expect(await screen.findByText("Cost alert settings saved.")).toBeVisible();
  });

  it("sends German as the language when German is active", async () => {
    await i18n.changeLanguage("de");
    const user = userEvent.setup();
    vi.spyOn(hostingService, "personalCostAlertSettings").mockResolvedValue(
      costAlertSettings({ thresholds: [] }),
    );
    const update = vi
      .spyOn(hostingService, "updateCostAlertSettings")
      .mockResolvedValue(costAlertSettings({ thresholds: [5] }));
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Betrag hinzufügen" }));
    await user.type(screen.getByLabelText("Warnbetrag 1 (EUR)"), "5");
    await user.click(screen.getByRole("button", { name: "Warneinstellungen speichern" }));

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    expect(update.mock.calls[0]?.[1].language).toBe("de");
  });

  it("is titled Cost alerts in the navigation while the Servers item stays active", () => {
    const context = getCommandNavigationContext("/command/servers/cost-alerts");

    expect(context?.titleKey).toBe("Shared_CommandCenter_CurrentArea_CostAlerts");
    expect(context?.item.id).toBe("servers");
    expect(isCommandNavigationItemActive(context!.item, "/command/servers/cost-alerts")).toBe(true);
    expect(getCommandNavigationContext("/command/servers/server-1")?.titleKey).toBe(
      "Shared_CommandCenter_CurrentArea_ServerWorkspace",
    );
  });
});
