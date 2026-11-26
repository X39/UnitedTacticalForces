import { act, fireEvent, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useForm, type UseFormReturn } from "react-hook-form";
import { StorageOptions, type StorageOptionsProps } from "@/components/server/StorageOptions";
import type { ServerCreationFormValues } from "@/features/hosting/createServerModel";
import { createProfile, createProvider, createStorageDemand } from "../helpers/createFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

const baseValues: ServerCreationFormValues = {
  title: "",
  gameIdentifier: "",
  runtimeProviderIdentifier: "fake-cloud",
  runtimeProfileIdentifier: "medium",
  runtimeStorageStrategy: "IncludedDisk",
  externalStorageSizeGiB: null,
  modPackDefinitionId: null,
  terrainId: null,
  scenarioId: null,
  configurationProfileId: null,
  costOwnerId: null,
  isLaunchDisabled: false,
};

interface HarnessProps {
  overrides?: Partial<StorageOptionsProps>;
  values?: Partial<ServerCreationFormValues>;
  onForm?: (form: UseFormReturn<ServerCreationFormValues>) => void;
}

function Harness({ overrides, values, onForm }: HarnessProps) {
  const form = useForm<ServerCreationFormValues>({ defaultValues: { ...baseValues, ...values } });
  onForm?.(form);
  return (
    <StorageOptions
      form={form}
      provider={createProvider("fake-cloud")}
      profile={createProfile("medium")}
      demand={createStorageDemand()}
      estimateFetching={false}
      estimateFailed={false}
      misfit={false}
      onRetryEstimate={() => undefined}
      onMemberChange={() => undefined}
      {...overrides}
    />
  );
}

function renderStorage(props: HarnessProps = {}) {
  let formRef: UseFormReturn<ServerCreationFormValues> | undefined;
  const result = renderWithProviders(<Harness {...props} onForm={(form) => (formRef = form)} />);
  return { ...result, form: () => formRef! };
}

const withoutExternalStorage = createProvider("fake-cloud", {
  storage: {
    supportsExternalBlockStorage: false,
    minimumCapacityGiB: 0,
    maximumCapacityGiB: 0,
    capacityIncrementGiB: 0,
    estimatedHourlyCostPerGiBEur: null,
    deploymentMountPath: null,
    readinessFailure: null,
  },
} as never);

describe("storage options", () => {
  it("offers the three strategies when the provider supports external block storage", () => {
    renderStorage();

    const options = within(screen.getByLabelText("Storage")).getAllByRole("option");
    expect(options.map((option) => option.textContent)).toEqual([
      "Included profile disk only",
      "Automatically sized external storage",
      "Explicitly sized external storage",
    ]);
  });

  it("offers only the included disk when the provider has no external block storage", () => {
    renderStorage({ overrides: { provider: withoutExternalStorage } });

    const options = within(screen.getByLabelText("Storage")).getAllByRole("option");
    expect(options.map((option) => option.textContent)).toEqual(["Included profile disk only"]);
  });

  it("shows the capacity field only for the explicitly sized strategy, with the provider bounds", () => {
    renderStorage({
      values: { runtimeStorageStrategy: "FixedExternal", externalStorageSizeGiB: 30 },
    });

    const capacity = screen.getByLabelText(/External capacity/);
    expect(capacity).toHaveAttribute("min", "20");
    expect(capacity).toHaveAttribute("max", "500");
    expect(capacity).toHaveAttribute("step", "10");
    expect(capacity).toHaveValue(30);
  });

  it("hides the capacity field for the other strategies", () => {
    renderStorage({ values: { runtimeStorageStrategy: "AutomaticExternal" } });

    expect(screen.queryByLabelText(/External capacity/)).not.toBeInTheDocument();
  });

  it("maps an empty capacity to null and an entry to a number", () => {
    const { form } = renderStorage({
      values: { runtimeStorageStrategy: "FixedExternal", externalStorageSizeGiB: 30 },
    });
    const capacity = screen.getByLabelText(/External capacity/);

    fireEvent.change(capacity, { target: { value: "" } });
    expect(form().getValues("externalStorageSizeGiB")).toBeNull();

    fireEvent.change(capacity, { target: { value: "40" } });
    expect(form().getValues("externalStorageSizeGiB")).toBe(40);
  });

  it("shows a schema error at the capacity field", async () => {
    const { form } = renderStorage({
      values: { runtimeStorageStrategy: "FixedExternal", externalStorageSizeGiB: 31 },
    });

    act(() => form().setError("externalStorageSizeGiB", { message: "Enter a valid capacity." }));

    expect(await screen.findByText("Enter a valid capacity.")).toBeInTheDocument();
    expect(screen.getByLabelText(/External capacity/)).toBeInvalid();
  });

  it("sets the minimum capacity and reports the change when the strategy becomes explicit", async () => {
    const onMemberChange = vi.fn();
    const { form } = renderStorage({ overrides: { onMemberChange } });

    await userEvent.selectOptions(screen.getByLabelText("Storage"), "FixedExternal");

    expect(onMemberChange).toHaveBeenCalledWith("runtimeStorageStrategy");
    expect(form().getValues("runtimeStorageStrategy")).toBe("FixedExternal");
    expect(form().getValues("externalStorageSizeGiB")).toBe(20);
    expect(await screen.findByLabelText(/External capacity/)).toHaveValue(20);
  });

  it("shows the facts with the combined price for the included disk", () => {
    const { container } = renderStorage();

    const facts = within(container.querySelector(".create-storage-facts") as HTMLElement);
    expect(facts.getByText("Included profile disk")).toBeInTheDocument();
    expect(facts.getByText("80 GiB")).toBeInTheDocument();
    expect(facts.getByText("Estimated peak requirement")).toBeInTheDocument();
    expect(facts.getByText("55 GiB")).toBeInTheDocument();
    expect(facts.getByText("Selected deployment capacity")).toBeInTheDocument();
    expect(facts.getByText("70 GiB")).toBeInTheDocument();
    const price = screen.getByText("Combined estimated price").nextElementSibling;
    expect(price?.textContent).toMatch(/0\.10.*\/hour · .*73\.00.*\/month/);
    expect(
      screen.getByText("Deployment content uses the included server-root filesystem."),
    ).toBeInTheDocument();
  });

  it("adds the external storage price to the combined price", () => {
    renderStorage({ values: { runtimeStorageStrategy: "AutomaticExternal" } });

    expect(screen.getByText("Selected raw external capacity")).toBeInTheDocument();
    const price = screen.getByText("Combined estimated price").nextElementSibling;
    expect(price?.textContent).toMatch(/0\.17.*\/hour · .*124\.10.*\/month/);
    expect(
      screen.getByText(/Deployment content uses the verified external filesystem at/),
    ).toBeInTheDocument();
  });

  it("shows the price as unavailable and never as zero when it is unknown", () => {
    renderStorage({
      overrides: {
        profile: createProfile("medium", {
          estimatedHourlyCostEur: null,
          estimatedMonthlyCostEur: null,
        } as never),
      },
    });

    const price = screen.getByText("Combined estimated price").nextElementSibling;
    expect(price).toHaveTextContent("Price unavailable");
    expect(price?.textContent).not.toMatch(/0[.,]00/);
  });

  it("warns about external storage only for the external strategies", () => {
    const warning =
      "External deployment storage is ephemeral and is deleted during full cost-stop or termination.";
    const first = renderStorage();
    expect(screen.queryByText(warning)).not.toBeInTheDocument();
    first.unmount();

    const second = renderStorage({ values: { runtimeStorageStrategy: "AutomaticExternal" } });
    expect(screen.getByText(warning)).toBeInTheDocument();
    second.unmount();

    renderStorage({
      values: { runtimeStorageStrategy: "FixedExternal", externalStorageSizeGiB: 30 },
    });
    expect(screen.getByText(warning)).toBeInTheDocument();
  });

  it("shows the loading copy while the estimate is fetched", () => {
    renderStorage({ overrides: { estimateFetching: true } });

    expect(screen.getByText("Calculating storage demand…")).toBeInTheDocument();
  });

  it("shows fixed failure copy with a retry and never the error text", async () => {
    const onRetryEstimate = vi.fn();
    renderStorage({
      overrides: { estimateFailed: true, demand: undefined, onRetryEstimate },
    });

    expect(
      screen.getByText("Storage estimate unavailable. The estimate could not be refreshed."),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(onRetryEstimate).toHaveBeenCalledTimes(1);
  });

  it("shows the invalid storage warning on a misfit", () => {
    renderStorage({ overrides: { misfit: true } });

    expect(
      screen.getByText(
        "The selected storage does not satisfy the estimated peak requirement or provider limits.",
      ),
    ).toBeInTheDocument();
  });

  it("keeps the demand breakdown behind a collapsed Storage details disclosure", async () => {
    const { container } = renderStorage({
      overrides: {
        demand: createStorageDemand({
          missingComponents: ["Workshop mods"],
          components: [
            {
              identifier: "game-files",
              displayName: "Game files",
              kind: "Base",
              requiredBytes: 30 * 1_073_741_824,
              status: "Estimated",
              source: "catalog",
            },
          ],
        } as never),
      },
    });

    const details = container.querySelector(".create-storage-details") as HTMLElement;
    const toggle = within(details).getByRole("button", { name: "Storage details" });
    expect(toggle).toHaveAttribute("aria-expanded", "false");

    await userEvent.click(toggle);

    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(within(details).getByRole("region", { name: "Storage details" })).toBeVisible();
    expect(within(details).getByText("Steady-state usage")).toBeInTheDocument();
    expect(within(details).getByText("Recommended peak capacity")).toBeInTheDocument();
    expect(
      within(details).getByText("This estimate uses conservative fallbacks for: Workshop mods."),
    ).toBeInTheDocument();
    expect(within(details).getByText("Component breakdown")).toBeInTheDocument();
    expect(within(details).getByText("Game files")).toBeInTheDocument();
    expect(within(details).getByText("Sufficient")).toBeInTheDocument();
  });

  it("shows no storage details without a demand", () => {
    const { container } = renderStorage({ overrides: { demand: undefined } });

    expect(container.querySelector(".create-storage-details")).toBeNull();
  });
});
