import { beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerConfigurationPanel } from "@/features/hosting/ServerConfigurationPanel";
import { ConfigurationProfileFields } from "@/features/hosting/ConfigurationProfileFields";
import { PlacementEstimate } from "@/features/hosting/PlacementEstimate";
import {
  configurationContribution,
  configurationContributionField,
} from "@/features/hosting/implementations/registry";
import { validateConfigurationEntry } from "@/features/hosting/workspaceModel";
import { hostingService } from "@/features/hosting/services";
import type {
  GameConfiguration,
  GameConfigurationEntry,
  ConfigurationDefinition,
  StorageDemand,
} from "@/features/hosting/types";

const entry: GameConfigurationEntry = {
  key: "headlessClientPlacement",
  targetPath: "",
  group: "Runtime",
  displayName: "Headless Client placement",
  description: "Placement",
  kind: "Selection",
  required: true,
  sensitive: false,
  order: 1,
  minimum: null,
  maximum: null,
  allowedValues: [],
  choices: [
    { value: "same-host", label: "Same host", description: null },
    { value: "separate-host", label: "Separate host", description: null },
  ],
  value: "separate-host",
  source: "Server",
  isCompatible: true,
  isRedacted: false,
  warning: null,
  isControlPlaneOnly: true,
};
const configuration: GameConfiguration = {
  serverId: "server",
  profileId: null,
  profileRevisionId: null,
  profileRevisionNumber: null,
  isComplete: true,
  entries: [entry],
};
const estimate = {
  hostCount: 2,
  estimatedComputeHourlyCostEur: 0.2,
  estimatedStorageHourlyCostEur: 0,
  estimatedHourlyCostEur: 0.2,
  estimatedMonthlyCostEur: 146,
};
function mount(canModify = true, gameIdentifier = "arma3") {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={client}>
        <RouterProvider
          router={createMemoryRouter([
            {
              path: "*",
              element: (
                <ServerConfigurationPanel
                  serverId="server"
                  gameIdentifier={gameIdentifier}
                  canModify={canModify}
                />
              ),
            },
          ])}
        />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}
beforeEach(async () => {
  await i18nReady;
  await i18n.changeLanguage("en");
  vi.restoreAllMocks();
  vi.spyOn(hostingService, "configuration").mockResolvedValue(configuration);
  vi.spyOn(hostingService, "serverConfigurationProfileOptions").mockResolvedValue([]);
  vi.spyOn(hostingService, "storageDemand").mockResolvedValue({
    placementEstimate: estimate,
  } as StorageDemand);
  vi.spyOn(hostingService, "files").mockResolvedValue([]);
  vi.spyOn(hostingService, "updateConfiguration").mockResolvedValue(configuration);
});
it("registers only implementation-specific fields and validates typed choices", () => {
  expect(configurationContribution("ARMA3")?.fields.headlessClientPlacement).toBeDefined();
  expect(configurationContribution("unknown")).toBeUndefined();
  expect(validateConfigurationEntry(entry, "other")).toContain("unsupported");
  expect(validateConfigurationEntry(entry, "same-host")).toBeUndefined();
});
it("renders both friendly choices once, saves exact typed value, and refreshes saved estimates", async () => {
  const user = userEvent.setup();
  mount();
  const select = await screen.findByLabelText("Headless Client placement");
  expect(select).toHaveValue("separate-host");
  expect(screen.getAllByRole("option", { name: "Same server" })).toHaveLength(1);
  await screen.findByText("2 hosts");
  expect(hostingService.files).not.toHaveBeenCalled();
  await user.selectOptions(select, "same-host");
  expect(screen.queryByText("2 hosts")).not.toBeInTheDocument();
  expect(
    screen.getByText("Save configuration changes to refresh the placement estimate."),
  ).toBeVisible();
  await user.click(screen.getByRole("button", { name: "Save overrides" }));
  await waitFor(() =>
    expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
      "server",
      [{ key: "headlessClientPlacement", value: "same-host" }],
      [],
    ),
  );
  await waitFor(() => expect(hostingService.storageDemand).toHaveBeenCalledTimes(2));
});
it("keeps selection and error after failed save and hides old estimate", async () => {
  vi.mocked(hostingService.updateConfiguration).mockRejectedValue(new Error("Save failed"));
  const user = userEvent.setup();
  mount();
  await user.selectOptions(await screen.findByLabelText("Headless Client placement"), "same-host");
  await user.click(screen.getByRole("button", { name: "Save overrides" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Save failed");
  expect(screen.getByLabelText("Headless Client placement")).toHaveValue("same-host");
  expect(screen.queryByText("2 hosts")).not.toBeInTheDocument();
});
it("preserves unrelated drafts when resetting inherited placement", async () => {
  vi.mocked(hostingService.configuration).mockResolvedValue({
    ...configuration,
    entries: [
      entry,
      {
        ...entry,
        key: "hostname",
        displayName: "Hostname",
        kind: "String",
        value: "old",
        source: "Default",
        choices: [],
      },
    ],
  });
  const user = userEvent.setup();
  mount();
  await user.type(await screen.findByLabelText("Hostname"), "new");
  await user.click(screen.getByRole("button", { name: "Reset to inherited" }));
  await waitFor(() =>
    expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
      "server",
      [],
      ["headlessClientPlacement"],
    ),
  );
  expect(screen.getByLabelText("Hostname")).toHaveValue("oldnew");
  expect(screen.getByRole("button", { name: "Save overrides" })).toBeEnabled();
});
it("disables placement for readers and retains generic fallback choices", async () => {
  const rendered = mount(false);
  expect(await screen.findByLabelText("Headless Client placement")).toBeDisabled();
  expect(screen.queryByRole("button", { name: "Save overrides" })).not.toBeInTheDocument();
  rendered.unmount();
  mount(true, "unknown");
  const select = await screen.findByLabelText("Headless Client placement");
  expect(within(select).getByRole("option", { name: "Same host" })).toHaveValue("same-host");
});
it("reuses placement contribution in profile fields", async () => {
  render(
    <ConfigurationProfileFields
      gameIdentifier="arma3"
      definitions={[{ ...entry, defaultValue: "separate-host" } as ConfigurationDefinition]}
      values={{ headlessClientPlacement: "same-host" }}
      issues={[]}
      onChange={vi.fn()}
    />,
  );
  expect(screen.getByRole("option", { name: "Same server" })).toBeVisible();
});
it.each([{ pending: true }, { unavailable: true }, { dirty: true }])(
  "never displays stale estimate for %s",
  (state) => {
    render(<PlacementEstimate estimate={estimate} {...state} />);
    expect(screen.queryByText("2 hosts")).not.toBeInTheDocument();
  },
);
it("shows unavailable rather than a zero quote", () => {
  render(<PlacementEstimate estimate={null} />);
  expect(screen.getByText("Estimate unavailable")).toBeVisible();
});

it("disables fields while saving and cannot expose a quote before configuration refetch completes", async () => {
  let resolveSave!: (value: GameConfiguration) => void;
  let resolveConfiguration!: (value: GameConfiguration) => void;
  vi.mocked(hostingService.updateConfiguration).mockImplementation(
    () =>
      new Promise((resolve) => {
        resolveSave = resolve;
      }),
  );
  vi.mocked(hostingService.configuration)
    .mockResolvedValueOnce(configuration)
    .mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveConfiguration = resolve;
        }),
    );
  const user = userEvent.setup();
  mount();
  const select = await screen.findByLabelText("Headless Client placement");
  await screen.findByText("2 hosts");
  await user.selectOptions(select, "same-host");
  await user.click(screen.getByRole("button", { name: "Save overrides" }));
  expect(select).toBeDisabled();
  resolveSave(configuration);
  await waitFor(() => expect(hostingService.storageDemand).toHaveBeenCalledTimes(2));
  expect(screen.queryByText("2 hosts")).not.toBeInTheDocument();
  resolveConfiguration({ ...configuration, entries: [{ ...entry, value: "same-host" }] });
  await waitFor(() => expect(select).toBeEnabled());
  expect(select).toHaveValue("same-host");
  expect(await screen.findByText("2 hosts")).toBeVisible();
});

it("ignores prototype identifiers and inherited field names", () => {
  expect(configurationContribution("constructor")).toBeUndefined();
  expect(configurationContribution("toString")).toBeUndefined();
  expect(
    configurationContributionField(configurationContribution("arma3"), "constructor"),
  ).toBeUndefined();
  expect(
    configurationContributionField(configurationContribution("arma3"), "toString"),
  ).toBeUndefined();
});
it("offers exactly two placement choices even for optional server values", async () => {
  vi.mocked(hostingService.configuration).mockResolvedValue({
    ...configuration,
    entries: [{ ...entry, required: false }],
  });
  mount();
  expect(
    within(await screen.findByLabelText("Headless Client placement")).getAllByRole("option"),
  ).toHaveLength(2);
});

it("groups control-plane metadata independently of a nonempty file target", async () => {
  vi.mocked(hostingService.configuration).mockResolvedValue({
    ...configuration,
    entries: [{ ...entry, targetPath: "server.cfg", isControlPlaneOnly: true }],
  });
  mount();
  expect(await screen.findByLabelText("Headless Client placement")).toBeEnabled();
  expect(screen.getByText("Deployment settings")).toBeVisible();
  expect(screen.queryByText("server.cfg")).not.toBeInTheDocument();
  expect(hostingService.files).not.toHaveBeenCalled();
});
