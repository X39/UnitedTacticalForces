import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { ConfigurationProfileCreatePage } from "@/features/hosting/ConfigurationProfileCreatePage";
import { ConfigurationProfileWorkspacePage } from "@/features/hosting/ConfigurationProfileWorkspacePage";
import { ConfigurationProfilesPage } from "@/features/hosting/ConfigurationProfilesPage";
import { hostingService } from "@/features/hosting/services";
import type {
  ConfigurationDefinition,
  ConfigurationProfileDetails,
  ConfigurationProfileSummary,
} from "@/features/hosting/types";

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-0000-0000-000000000001",
      nickname: "Commander",
      isBanned: false,
      isVerified: true,
      isDeleted: false,
      isVereinMember: false,
      claims: [{ identifier: "adm", value: null }],
      roles: [],
    },
    isLoading: false,
    isAuthenticated: true,
  }),
}));

const profile: ConfigurationProfileSummary = {
  primaryKey: "20000000-0000-0000-0000-000000000002",
  title: "Public operations",
  gameIdentifier: "arma3",
  isArchived: false,
  latestRevisionNumber: 1,
};

const archivedProfile: ConfigurationProfileSummary = {
  ...profile,
  primaryKey: "20000000-0000-0000-0000-000000000004",
  title: "Legacy baseline",
  isArchived: true,
};

const definitions: ConfigurationDefinition[] = [
  {
    key: "hostname",
    targetPath: "server.cfg",
    group: "General",
    displayName: "Hostname",
    description: "Server browser name.",
    kind: "String",
    required: true,
    sensitive: false,
    order: 10,
    defaultValue: null,
    minimum: null,
    maximum: null,
    allowedValues: [],
  },
  {
    key: "motd[]",
    targetPath: "server.cfg",
    group: "General",
    displayName: "Message of the day",
    description: "One line per message.",
    kind: "StringArray",
    required: false,
    sensitive: false,
    order: 20,
    defaultValue: [],
    minimum: null,
    maximum: null,
    allowedValues: [],
  },
  {
    key: "password",
    targetPath: "server.cfg",
    group: "Security",
    displayName: "Password",
    description: "Join secret.",
    kind: "String",
    required: false,
    sensitive: true,
    order: 30,
    defaultValue: null,
    minimum: null,
    maximum: null,
    allowedValues: [],
  },
];

function details(
  overrides: Partial<ConfigurationProfileDetails> = {},
): ConfigurationProfileDetails {
  return {
    profile,
    canModify: true,
    canManageAccess: false,
    canArchive: true,
    definitions,
    revisions: [
      {
        primaryKey: "30000000-0000-0000-0000-000000000003",
        revisionNumber: 1,
        timeStampCreated: "2026-08-13T12:00:00Z",
        values: { hostname: "UTF", password: "secret" },
        redactedKeys: [],
      },
    ],
    ...overrides,
  };
}

function renderPage(initialEntry = "/command/configuration-profiles") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: "/command/configuration-profiles", element: <ConfigurationProfilesPage /> },
      {
        path: "/command/configuration-profiles/create",
        element: <ConfigurationProfileCreatePage />,
      },
      {
        path: "/command/configuration-profiles/:profileId",
        element: <ConfigurationProfileWorkspacePage />,
      },
    ],
    { initialEntries: [initialEntry] },
  );
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
  return router;
}

describe("configuration-profile routes", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    vi.spyOn(hostingService, "profiles").mockResolvedValue([]);
    vi.spyOn(hostingService, "profileCatalog").mockResolvedValue({
      canCreate: true,
      games: [{ gameIdentifier: "arma3", definitions }],
    });
    vi.spyOn(hostingService, "profile").mockResolvedValue(details());
    vi.spyOn(hostingService, "profileAccess").mockResolvedValue({
      allowedPermissions: ["cfg", "cfg:view", "cfg:modify", "cfg:manage-access", "cfg:archive"],
      users: [],
      roles: [],
    });
    vi.spyOn(hostingService, "createProfile").mockResolvedValue(profile);
    vi.spyOn(hostingService, "createProfileRevision").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "archiveProfile").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "changeProfileUserAccess").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "changeProfileRoleAccess").mockResolvedValue(undefined);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    await act(async () => i18n.changeLanguage("en"));
  });

  it("filters the full-width library and navigates through profile links", async () => {
    vi.mocked(hostingService.profiles).mockResolvedValue([profile, archivedProfile]);
    const user = userEvent.setup();
    const router = renderPage();

    expect(await screen.findByText("Total profiles")).toBeVisible();
    expect(screen.getByText("Public operations")).toBeVisible();
    expect(screen.getByText("Legacy baseline")).toBeVisible();

    await user.selectOptions(screen.getByLabelText("Status"), "archived");
    expect(screen.queryByText("Public operations")).not.toBeInTheDocument();
    expect(screen.getByText("Legacy baseline")).toBeVisible();

    await user.click(screen.getByRole("link", { name: "Open profile" }));
    expect(router.state.location.pathname).toBe(
      `/command/configuration-profiles/${archivedProfile.primaryKey}`,
    );
  });

  it("validates the creation wizard and serializes string arrays", async () => {
    const user = userEvent.setup();
    const router = renderPage("/command/configuration-profiles/create");

    await screen.findByRole("heading", { name: "Basics" });
    await user.type(screen.getByLabelText("Profile title"), "Public operations");
    await user.click(screen.getByRole("button", { name: "Next: configuration" }));
    await user.click(screen.getByRole("button", { name: "Review profile" }));
    expect(await screen.findByText("Hostname is required.")).toBeVisible();

    await user.type(screen.getByLabelText("Hostname"), "UTF Public");
    await user.type(screen.getByLabelText("Message of the day"), "Alpha\nBravo");
    await user.type(screen.getByLabelText("Password"), "secret");
    await user.click(screen.getByRole("button", { name: "Review profile" }));
    expect(await screen.findByText("Configured securely")).toBeVisible();
    expect(screen.queryByText("secret")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Create profile" }));

    await waitFor(() =>
      expect(hostingService.createProfile).toHaveBeenCalledWith("Public operations", "arma3", [
        { key: "hostname", value: "UTF Public" },
        { key: "motd[]", value: ["Alpha", "Bravo"] },
        { key: "password", value: "secret" },
      ]),
    );
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(
        `/command/configuration-profiles/${profile.primaryKey}`,
      ),
    );
  });

  it("publishes changed values from the dedicated configuration tab", async () => {
    const user = userEvent.setup();
    renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

    const hostname = await screen.findByLabelText("Hostname");
    await user.clear(hostname);
    await user.type(hostname, "UTF Updated");
    expect(screen.getByText("1 unsaved change(s)")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Publish revision" }));

    await waitFor(() =>
      expect(hostingService.createProfileRevision).toHaveBeenCalledWith(profile.primaryKey, [
        { key: "hostname", value: "UTF Updated" },
        { key: "password", value: "secret" },
      ]),
    );
  });

  it("keeps redacted values out of controls and revision output for read-only profiles", async () => {
    vi.mocked(hostingService.profile).mockResolvedValue(
      details({
        canModify: false,
        canArchive: false,
        revisions: [
          {
            primaryKey: "30000000-0000-0000-0000-000000000003",
            revisionNumber: 1,
            timeStampCreated: "2026-08-13T12:00:00Z",
            values: { hostname: "UTF", password: null },
            redactedKeys: ["password"],
          },
        ],
      }),
    );
    const user = userEvent.setup();
    renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

    expect(await screen.findByRole("heading", { name: "Profile is read-only" })).toBeVisible();
    expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
    await user.click(screen.getByRole("tab", { name: "Revisions" }));
    expect(await screen.findByText("Stored securely")).toBeVisible();
  });

  it("falls back from an unauthorized access deep link", async () => {
    const router = renderPage(`/command/configuration-profiles/${profile.primaryKey}?tab=access`);

    expect(await screen.findByRole("tab", { name: "Configuration" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(screen.queryByRole("tab", { name: "Access" })).not.toBeInTheDocument();
    await waitFor(() => expect(router.state.location.search).toBe("?tab=configuration"));
  });

  it("loads grants and sends exact scoped access changes", async () => {
    vi.mocked(hostingService.profile).mockResolvedValue(details({ canManageAccess: true }));
    vi.mocked(hostingService.profileAccess).mockResolvedValue({
      allowedPermissions: ["cfg:view"],
      users: [],
      roles: [
        { roleId: 7, title: "Operators", description: "Hosting team", permissions: ["cfg:view"] },
      ],
    });
    const user = userEvent.setup();
    renderPage(`/command/configuration-profiles/${profile.primaryKey}?tab=access`);

    expect(await screen.findByRole("heading", { name: "Direct role grants" })).toBeVisible();
    await user.type(screen.getByLabelText("User ID"), "40000000-0000-4000-8000-000000000004");
    await user.click(screen.getByRole("button", { name: "Grant" }));
    await waitFor(() =>
      expect(hostingService.changeProfileUserAccess).toHaveBeenCalledWith(
        profile.primaryKey,
        "40000000-0000-4000-8000-000000000004",
        "cfg:view",
        true,
      ),
    );
  });

  it("renders the library chrome in German", async () => {
    await act(async () => i18n.changeLanguage("de"));
    vi.mocked(hostingService.profiles).mockResolvedValue([profile]);
    renderPage();

    expect(await screen.findByRole("heading", { name: "Konfigurationsprofile" })).toBeVisible();
    expect(await screen.findByLabelText("Profile durchsuchen")).toBeVisible();
    expect(screen.getByRole("link", { name: "Neues Profil" })).toBeVisible();
  });
});
