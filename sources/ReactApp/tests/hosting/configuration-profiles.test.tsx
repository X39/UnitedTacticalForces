import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
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

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [{ path: "/command/configuration-profiles", element: <ConfigurationProfilesPage /> }],
    { initialEntries: ["/command/configuration-profiles"] },
  );
  return render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe("certified configuration-profile workflow", () => {
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

  it("validates required values and submits string arrays as JSON arrays", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByRole("heading", { name: "Create configuration profile" });
    await user.type(screen.getByLabelText("Profile title"), "Public operations");
    await user.click(screen.getByRole("button", { name: "Create profile" }));
    expect(await screen.findByText("Hostname is required.")).toBeVisible();
    expect(hostingService.createProfile).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText("Hostname"), "UTF Public");
    await user.type(screen.getByLabelText("Message of the day"), "Alpha\nBravo");
    await user.click(screen.getByRole("button", { name: "Create profile" }));

    await waitFor(() =>
      expect(hostingService.createProfile).toHaveBeenCalledWith("Public operations", "arma3", [
        { key: "hostname", value: "UTF Public" },
        { key: "motd[]", value: ["Alpha", "Bravo"] },
      ]),
    );
  });

  it("never renders a redacted sensitive revision value into an editable control", async () => {
    vi.mocked(hostingService.profiles).mockResolvedValue([profile]);
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
    renderPage();

    await user.click(await screen.findByRole("option", { name: /Public operations/ }));

    expect(await screen.findByText("Stored securely")).toBeVisible();
    expect(screen.getByRole("heading", { name: "Profile is read-only" })).toBeVisible();
    expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
  });

  it("loads direct grants and sends an exact scoped user permission", async () => {
    vi.mocked(hostingService.profiles).mockResolvedValue([profile]);
    vi.mocked(hostingService.profile).mockResolvedValue(details({ canManageAccess: true }));
    vi.mocked(hostingService.profileAccess).mockResolvedValue({
      allowedPermissions: ["cfg:view"],
      users: [],
      roles: [
        {
          roleId: 7,
          title: "Operators",
          description: "Hosting team",
          permissions: ["cfg:view"],
        },
      ],
    });
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("option", { name: /Public operations/ }));
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

  it("renders all page chrome in German", async () => {
    await act(async () => i18n.changeLanguage("de"));
    renderPage();

    expect(await screen.findByRole("heading", { name: "Konfigurationsprofile" })).toBeVisible();
    expect(
      await screen.findByRole("heading", { name: "Konfigurationsprofil erstellen" }),
    ).toBeVisible();
    expect(screen.getByLabelText("Profilname")).toBeVisible();
    expect(screen.getByRole("button", { name: "Profil erstellen" })).toBeVisible();
  });
});
