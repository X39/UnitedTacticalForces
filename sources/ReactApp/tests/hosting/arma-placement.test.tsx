import { beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n, { i18nReady } from "@/app/i18n";
import { SettingsSection } from "@/features/hosting/sections/SettingsSection";
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
  GameServer,
  StorageDemand,
} from "@/features/hosting/types";
import { fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

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
  tier: "Common",
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
  const server = {
    ...fleetServer({ gameIdentifier }),
    primaryKey: "server",
    selectedModPackDefinitionId: null,
    selectedModPackDefinitionTitle: null,
    selectedConfigurationProfileId: null,
    selectedConfigurationProfileTitle: null,
    canModify,
    canManageSecrets: false,
  } as unknown as GameServer;
  return renderWithProviders(<SettingsSection server={server} jobs={[]} />);
}
/** Opens the review dialog of the staged changes and returns it. */
async function openReview(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("button", { name: "Review changes" }));
  return screen.findByRole("dialog", { name: "Review changes" });
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
it("renders both friendly choices once, applies the exact typed value, and refreshes saved estimates", async () => {
  const user = userEvent.setup();
  mount();
  const select = await screen.findByLabelText("Headless Client placement");
  expect(select).toHaveValue("separate-host");
  expect(screen.getAllByRole("option", { name: "Same server" })).toHaveLength(1);
  await screen.findByText("2 hosts");
  expect(hostingService.files).not.toHaveBeenCalled();
  expect(screen.queryByText("Based on the saved settings, not the staged changes.")).toBeNull();
  await user.selectOptions(select, "same-host");
  // The saved estimate stays visible while the change is staged, with the note that it ignores the change.
  expect(screen.getByText("2 hosts")).toBeVisible();
  expect(screen.getByText("Based on the saved settings, not the staged changes.")).toBeVisible();
  expect(hostingService.updateConfiguration).not.toHaveBeenCalled();
  const dialog = await openReview(user);
  await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));
  await waitFor(() =>
    expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
      "server",
      [{ key: "headlessClientPlacement", value: "same-host" }],
      [],
    ),
  );
  await waitFor(() => expect(hostingService.storageDemand).toHaveBeenCalledTimes(2));
});
it("keeps the selection staged after a failed apply, keeps the saved estimate and prints no raw error", async () => {
  vi.mocked(hostingService.updateConfiguration).mockRejectedValue(new Error("Save failed"));
  const user = userEvent.setup();
  mount();
  await user.selectOptions(await screen.findByLabelText("Headless Client placement"), "same-host");
  const dialog = await openReview(user);
  await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));
  await waitFor(() => expect(hostingService.updateConfiguration).toHaveBeenCalledTimes(1));
  expect(await within(dialog).findByRole("alert")).toHaveTextContent(
    "0 of 1 changes applied. The rest stay staged.",
  );
  expect(within(dialog).getByRole("button", { name: "Try 1 change again" })).toBeVisible();
  expect(screen.getByLabelText("Headless Client placement")).toHaveValue("same-host");
  expect(await screen.findByText("2 hosts")).toBeVisible();
  expect(screen.getByText("Based on the saved settings, not the staged changes.")).toBeVisible();
  expect(screen.queryByText("Save failed")).not.toBeInTheDocument();
});
it("stages a reset next to an unrelated draft and applies both in one request", async () => {
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
  await user.click(
    screen.getByRole("button", { name: "Reset to inherited: Headless Client placement" }),
  );
  expect(hostingService.updateConfiguration).not.toHaveBeenCalled();
  expect(screen.getByLabelText("Hostname")).toHaveValue("oldnew");
  expect(screen.getByRole("region", { name: "Pending changes" })).toHaveTextContent(
    "2 changes staged",
  );
  const dialog = await openReview(user);
  await user.click(within(dialog).getByRole("button", { name: "Apply 2 changes" }));
  await waitFor(() =>
    expect(hostingService.updateConfiguration).toHaveBeenCalledWith(
      "server",
      [{ key: "hostname", value: "oldnew" }],
      ["headlessClientPlacement"],
    ),
  );
  expect(hostingService.updateConfiguration).toHaveBeenCalledTimes(1);
});
it("disables placement for readers and retains generic fallback choices", async () => {
  const rendered = mount(false);
  expect(await screen.findByLabelText("Headless Client placement")).toBeDisabled();
  expect(screen.queryByRole("button", { name: /^Reset to inherited/ })).not.toBeInTheDocument();
  expect(screen.queryByRole("region", { name: "Pending changes" })).not.toBeInTheDocument();
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
it.each([{ pending: true }, { unavailable: true }])(
  "never displays stale estimate for %s",
  (state) => {
    render(<PlacementEstimate estimate={estimate} stagedNote {...state} />);
    expect(screen.queryByText("2 hosts")).not.toBeInTheDocument();
    expect(
      screen.queryByText("Based on the saved settings, not the staged changes."),
    ).not.toBeInTheDocument();
  },
);
it("keeps the estimate visible with the saved-settings note while changes are staged", () => {
  const { rerender } = render(<PlacementEstimate estimate={estimate} />);
  expect(screen.getByText("2 hosts")).toBeVisible();
  expect(
    screen.queryByText("Based on the saved settings, not the staged changes."),
  ).not.toBeInTheDocument();
  rerender(<PlacementEstimate estimate={estimate} stagedNote />);
  expect(screen.getByText("2 hosts")).toBeVisible();
  expect(screen.getByText("Based on the saved settings, not the staged changes.")).toBeVisible();
});
it("shows unavailable rather than a zero quote", () => {
  render(<PlacementEstimate estimate={null} />);
  expect(screen.getByText("Estimate unavailable")).toBeVisible();
});

it("disables fields while applying and cannot expose a quote before configuration refetch completes", async () => {
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
  const dialog = await openReview(user);
  await user.click(within(dialog).getByRole("button", { name: "Apply 1 change" }));
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
    entries: [{ ...entry, targetPath: "server.cfg", isControlPlaneOnly: true, tier: "Advanced" }],
  });
  mount();
  expect(await screen.findByLabelText("Headless Client placement")).toBeEnabled();
  expect(screen.getByText("Deployment settings")).toBeVisible();
  expect(screen.queryByText("server.cfg")).not.toBeInTheDocument();
  expect(hostingService.files).not.toHaveBeenCalled();
});
