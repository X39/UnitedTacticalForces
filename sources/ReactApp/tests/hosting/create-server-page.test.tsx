import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import { ApiError } from "@/api/client";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { CreateServerPage } from "@/features/hosting/CreateServerPage";
import { hostingService } from "@/features/hosting/services";
import type {
  GameServer,
  ServerCapability,
  ServerCreationCostOwnerOption,
  StorageDemand,
} from "@/features/hosting/types";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isLoading: false,
    isAuthenticated: Boolean(authState.user),
  }),
}));

const creator: FullUser = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Creator",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  claims: [{ identifier: "crt:sv", value: null }],
  roles: [],
} as FullUser;

const capability = {
  gameIdentifier: "arma3",
  displayName: "Arma 3",
  description: "Dedicated Arma operations.",
  runtimeProviders: ["fake-cloud"],
  actions: [],
  modProviderBindings: [],
  modPackFormats: [],
  runtimeProviderProfiles: [
    {
      identifier: "fake-cloud",
      availabilityStatus: "available",
      profiles: [
        {
          identifier: "medium",
          displayName: "Medium",
          description: "Test compute profile.",
          virtualCpuCount: 4,
          memoryGiB: 8,
          diskGiB: 80,
          includedStorageBytes: 80 * 1_073_741_824,
          usableStorageBytes: 70 * 1_073_741_824,
          estimatedHourlyCostEur: 0.1,
          estimatedMonthlyCostEur: 73,
        },
      ],
      storage: {
        supportsExternalBlockStorage: true,
        minimumCapacityGiB: 20,
        maximumCapacityGiB: 500,
        capacityIncrementGiB: 10,
        estimatedHourlyCostPerGiBEur: 0.001,
        deploymentMountPath: "/srv/utf",
        readinessFailure: null,
      },
    },
  ],
} as ServerCapability;

const storageDemand = {
  steadyStateRequiredBytes: 40 * 1_073_741_824,
  preparationWorkspaceBytes: 10 * 1_073_741_824,
  safetyMarginPercent: 10,
  safetyMarginBytes: 5 * 1_073_741_824,
  peakPreparationRequiredBytes: 55 * 1_073_741_824,
  status: "Estimated",
  calculatedAt: "2026-08-13T12:00:00Z",
  measurementTimestamp: null,
  missingComponents: [],
  components: [],
  runtimeProfiles: [
    {
      runtimeProviderIdentifier: "fake-cloud",
      runtimeProfileIdentifier: "medium",
      displayName: "Medium",
      includedCapacityBytes: 80 * 1_073_741_824,
      usableCapacityBytes: 70 * 1_073_741_824,
      isSufficient: true,
      externalStorageRequired: false,
    },
  ],
} as StorageDemand;

const owners = [
  {
    costOwnerId: "10000000-0000-4000-8000-000000000010",
    displayName: "Personal budget",
    isFrozen: false,
    isCurrentUser: true,
  },
  {
    costOwnerId: "10000000-0000-4000-8000-000000000011",
    displayName: "Shared budget",
    isFrozen: false,
    isCurrentUser: false,
  },
] as ServerCreationCostOwnerOption[];

const createdServer = {
  primaryKey: "10000000-0000-4000-8000-000000000099",
  title: "UTF One",
  actions: [],
  deployment: null,
} as unknown as GameServer;

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: "/command/servers/create", element: <CreateServerPage /> },
      { path: "/command/servers/:serverId", element: <div>Server workspace</div> },
    ],
    { initialEntries: ["/command/servers/create"] },
  );
  return render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe("certified server creation workflow", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = creator;
    vi.spyOn(hostingService, "capabilities").mockResolvedValue([capability]);
    vi.spyOn(hostingService, "creationModPacks").mockResolvedValue([
      { primaryKey: 42, title: "Operations" },
    ]);
    vi.spyOn(hostingService, "profileOptions").mockResolvedValue([
      {
        primaryKey: "10000000-0000-4000-8000-000000000020",
        title: "Public operation",
        gameIdentifier: "arma3",
        isArchived: false,
        latestRevisionNumber: 3,
      },
    ]);
    vi.spyOn(hostingService, "estimateStorage").mockResolvedValue(storageDemand);
    vi.spyOn(hostingService, "creationCostOwners").mockResolvedValue(owners);
    vi.spyOn(hostingService, "create").mockResolvedValue(createdServer);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await i18n.changeLanguage("en");
  });

  it("enforces the create-server claim before loading any option catalog", async () => {
    authState.user = undefined;
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Server creation access required" }),
    ).toBeVisible();
    expect(hostingService.capabilities).not.toHaveBeenCalled();
  });

  it("renders the authorization state from the shared German resources", async () => {
    authState.user = undefined;
    await i18n.changeLanguage("de");
    renderPage();

    expect(
      await screen.findByRole("heading", {
        name: "Berechtigung zur Servererstellung erforderlich",
      }),
    ).toBeVisible();
  });

  it("submits selected content, storage, owner, and launch state then opens the workspace", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    renderPage();

    await user.type(await screen.findByLabelText("Server name"), "  UTF One  ");
    await user.selectOptions(screen.getByLabelText(/^Mod pack \(optional\)/), "42");
    await user.selectOptions(
      screen.getByLabelText(/^Configuration profile \(optional\)/),
      "10000000-0000-4000-8000-000000000020",
    );
    await user.selectOptions(
      screen.getByLabelText(/^Cost owner/),
      "10000000-0000-4000-8000-000000000011",
    );
    await user.click(screen.getByLabelText(/^Keep launch disabled/));
    const submit = screen.getByRole("button", { name: "Create server record" });
    await waitFor(() => expect(submit).toBeEnabled());
    await user.click(submit);

    await waitFor(() =>
      expect(hostingService.create).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "UTF One",
          gameIdentifier: "arma3",
          runtimeProviderIdentifier: "fake-cloud",
          runtimeProfileIdentifier: "medium",
          runtimeStorageStrategy: "AutomaticExternal",
          modPackDefinitionId: 42,
          configurationProfileId: "10000000-0000-4000-8000-000000000020",
          costOwnerId: "10000000-0000-4000-8000-000000000011",
          isLaunchDisabled: true,
        }),
      ),
    );
    expect(await screen.findByText("Server workspace")).toBeVisible();
  });

  it("keeps normalized API details out of a failed mutation state", async () => {
    vi.mocked(hostingService.create).mockRejectedValue(
      new ApiError("provider token secret", 500, "server"),
    );
    const user = userEvent.setup();
    renderPage();

    await user.type(await screen.findByLabelText("Server name"), "UTF One");
    const submit = screen.getByRole("button", { name: "Create server record" });
    await waitFor(() => expect(submit).toBeEnabled());
    await user.click(submit);

    expect(await screen.findByText("The server record could not be created.")).toBeVisible();
    expect(screen.queryByText("provider token secret")).not.toBeInTheDocument();
  });
});
